using System;
using Orbital.Core.Bodies;
using Orbital.Core.Flight;
using Orbital.View.Flight;
using Orbital.View.Map;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Orbital.Craft
{
    /// <summary>View data of one body of the table for <see cref="ScaledSpaceView"/>, read from <see cref="AppearanceFacet"/>.</summary>
    public struct BodyLook
    {
        public Color Tint;
        public float Emission;
        public double Albedo;
        public bool Star;

        /// <summary>
        /// Looks parallel to the body table. A body without a record in the data source is grey; without any emissive body the root is the star,
        /// so the light always has a source. <paramref name="nameOf"/> gives the name of a table id for the data source.
        /// </summary>
        public static BodyLook[] Read(BodyFrameTable frames, IBodyDataSource data, Func<string, string> nameOf)
        {
            BodyLook[] looks = new BodyLook[frames.Count];
            int missing = 0;
            bool star = false;
            for (int i = 0; i < looks.Length; i++)
            {
                looks[i] = new BodyLook {Tint = new Color(0.6f, 0.6f, 0.6f), Albedo = 0.3};
                if (frames[i].Virtual) continue;
                if (data != null && data.TryGet(nameOf(frames.IdOf(i)), out BodyRecord record) && record.TryGetFacet(out AppearanceFacet facet))
                {
                    looks[i].Tint = facet.tint * facet.brightness;
                    looks[i].Tint.a = 1f;
                    looks[i].Emission = facet.emission;
                    looks[i].Albedo = facet.albedo;
                    looks[i].Star = facet.emission > 0f;
                    star |= looks[i].Star;
                }
                else missing++;
            }
            if (missing > 0) Debug.LogWarning($"ScaledSpaceView: {missing} bodies have no AppearanceFacet in the body table, drawn grey");
            if (!star)
            {
                for (int i = 0; i < looks.Length; i++)
                {
                    if (frames[i].Parent >= 0 || frames[i].Virtual) continue;
                    looks[i].Star = true;
                    looks[i].Emission = 2f;
                    looks[i].Tint = new Color(1f, 0.9f, 0.7f);
                    break;
                }
            }
            return looks;
        }
    }

    /// <summary>
    /// The sky of a camera (stage 3.6): every real body of the table at its exact direction and angular size (<see cref="CelestialView"/>), as a
    /// sphere at the scaled distance or as a point of a constant pixel size with the exposed flux. Depth means nothing at one scaled distance, so the
    /// bodies are painted far to near by their real distance. The view knows nothing of the flight: it gets a camera (any field of view, any target,
    /// on a layer of its own, positioned by the owner) and an eye, so a second instance is a telescope. <see cref="IStarField"/> stars are the camera's
    /// background until the generator appears (order: sky, this view, Local camera).
    /// </summary>
    public sealed class ScaledSpaceView : IDisposable
    {
        private const int FarthestQueue = 3000; // transparent range: the queue is the painter's order, the farthest body first

        private readonly BodyFrameTable _frames;
        private readonly Camera _camera;
        private readonly SkySettings _settings;
        private readonly BodyViewParams[] _params;
        private readonly BodyViewEntry[] _entries;
        private readonly int[] _order;
        private readonly BodyLook[] _looks;
        private readonly int _sun;
        private readonly GameObject _root;
        private readonly Mesh _sphere;
        private readonly Mesh _quad;
        private readonly Transform[] _disks;
        private readonly Transform[] _points;
        private readonly Material[] _diskMaterials;
        private readonly Material[] _pointMaterials;
        private static readonly int SunDir = Shader.PropertyToID("_SunDir");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>Unit direction from the last eye to the Sun: the light of the Local layer.</summary>
        public Vec3 LightDirection { get; private set; } = Vec3.UnitY;
        /// <summary>The sphere of the disks, radius 1; the owner may draw the near planet with it.</summary>
        public Mesh Sphere => _sphere;

        /// <param name="camera">Gets its clear, clip and layer setup here; its position, rotation, field of view and target belong to the owner.</param>
        public ScaledSpaceView(BodyFrameTable frames, BodyLook[] looks, Transform parent, Camera camera, SkySettings settings)
        {
            _frames = frames;
            _looks = looks;
            _camera = camera;
            _settings = settings;
            int layer = settings.layer;
            camera.nearClipPlane = 1f;
            camera.cullingMask = 1 << layer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            ApplyCameraSettings();
            int count = frames.Count;
            _params = new BodyViewParams[count];
            _entries = new BodyViewEntry[count];
            _order = new int[count];
            _disks = new Transform[count];
            _points = new Transform[count];
            _diskMaterials = new Material[count];
            _pointMaterials = new Material[count];
            _root = new GameObject("[ScaledSpace]");
            _root.transform.SetParent(parent, false);
            _sphere = BuildSphere(128, 64);
            _quad = BuildQuad();
            _sun = -1;
            Shader pointShader = LoadShader("Orbital/ScaledPoint");
            for (int i = 0; i < count; i++)
            {
                _params[i] = new BodyViewParams {Albedo = looks[i].Albedo, Star = looks[i].Star};
                if (_sun < 0 && looks[i].Star) _sun = i;
                if (frames[i].Virtual || frames[i].Radius <= 0) continue;
                string id = frames.IdOf(i);
                _diskMaterials[i] = NewBodyMaterial(looks[i].Tint, looks[i].Emission, false);
                _disks[i] = MakeRenderer("Disk " + id, _sphere, _diskMaterials[i], layer);
                _pointMaterials[i] = new Material(pointShader);
                _points[i] = MakeRenderer("Point " + id, _quad, _pointMaterials[i], layer);
            }
        }

        /// <summary>Material of a lit ball. Far = no depth, the queue is set by the painter's order; near = normal depth among the Local objects.</summary>
        public static Material NewBodyMaterial(Color tint, float emission, bool near)
        {
            Material material = new Material(LoadShader("Orbital/ScaledBody"));
            material.SetColor("_Tint", tint);
            material.SetFloat("_Emission", emission);
            material.SetFloat("_ZWrite", near ? 1f : 0f);
            material.SetFloat("_ZTest", near ? 4f : 8f); // CompareFunction LessEqual / Always
            material.renderQueue = near ? 2000 : FarthestQueue;
            return material;
        }

        public static void SetSunDirection(Material material, Vec3 direction)
        {
            Vector3 engine = CraftPresenter.ToVector(direction);
            material.SetVector(SunDir, new Vector4(engine.x, engine.y, engine.z, 0f));
        }

        public BodyLook LookOf(int body) => _looks[body];

        /// <summary>Puts the sky where it is for the eye at the game time; the camera must already be placed. The body <paramref name="nearBody"/> (-1 = none) is drawn by the owner.</summary>
        public void Present(double time, in Vec3 eye, int nearBody)
        {
            ApplyCameraSettings();
            double distance = _settings.scaledDistance;
            ExposureParams exposure = _settings.Exposure;
            ProjectionCamera projection = new ProjectionCamera
            {
                Eye = eye,
                HorizontalFovRad = Camera.VerticalToHorizontalFieldOfView(_camera.fieldOfView, _camera.aspect) * Mathf.Deg2Rad,
                WidthPx = Mathf.Max(1, _camera.pixelWidth),
            };
            int n = CelestialView.Fill(_frames, time, in projection, _params, distance, _settings.minDiskPixels, nearBody, _entries);
            CelestialView.FarToNearOrder(_entries.AsSpan(0, n), _order);

            Vec3 sun = default;
            if (_sun >= 0)
            {
                _frames.GlobalState(_sun, time, out sun, out _);
                LightDirection = SunLight.Direction(sun, eye);
            }

            Vector3 origin = _camera.transform.position;
            Quaternion rotation = _camera.transform.rotation;
            float pointHalf = (float) (0.5 * _settings.pointPixels * projection.RadiansPerPixel * distance);
            for (int rank = 0; rank < n; rank++)
            {
                ref readonly BodyViewEntry entry = ref _entries[_order[rank]];
                int body = entry.TableIndex;
                Transform disk = _disks[body];
                if (disk == null) continue;
                Transform point = _points[body];
                ref readonly ProjectedBody p = ref entry.Projection;
                bool hidden = entry.NearPlanet || p.Inside;
                bool showDisk = !hidden && p.Mode == BodyDrawMode.Disk;
                double brightness = hidden || showDisk ? 0.0 : Exposure.PointBrightness(entry.Flux, in exposure);
                bool showPoint = brightness > 0.0;
                if (disk.gameObject.activeSelf != showDisk) disk.gameObject.SetActive(showDisk);
                if (point.gameObject.activeSelf != showPoint) point.gameObject.SetActive(showPoint);
                Vector3 position = origin + CraftPresenter.ToVector(p.ScaledPosition);
                int queue = FarthestQueue + rank;
                if (showDisk)
                {
                    disk.position = position;
                    disk.localScale = Vector3.one * (float) p.ScaledRadius;
                    Material material = _diskMaterials[body];
                    if (material.renderQueue != queue) material.renderQueue = queue;
                    if (_sun >= 0) SetSunDirection(material, SunLight.Direction(sun, eye + p.Direction * p.Distance));
                }
                if (showPoint)
                {
                    point.position = position;
                    point.rotation = rotation;
                    point.localScale = Vector3.one * pointHalf;
                    Material material = _pointMaterials[body];
                    if (material.renderQueue != queue) material.renderQueue = queue;
                    Color color = _looks[body].Tint * (float) brightness;
                    color.a = 1f;
                    material.SetColor(ColorId, color);
                }
            }
        }

        private void ApplyCameraSettings()
        {
            _camera.backgroundColor = _settings.background;
            _camera.farClipPlane = _settings.scaledDistance * 2.5f;
        }

        private Transform MakeRenderer(string name, Mesh mesh, Material material, int layer)
        {
            GameObject go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = layer;
            go.transform.SetParent(_root.transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            go.SetActive(false);
            return go.transform;
        }

        private static Shader LoadShader(string name)
        {
            Shader shader = Resources.Load<Shader>(name);
            if (shader == null) throw new InvalidOperationException($"ScaledSpaceView: no shader Resources/{name}");
            return shader;
        }

        // A latitude-longitude sphere of radius 1, normals outward; the disks are big on the screen, so the primitive's polygons would show.
        public static Mesh BuildSphere(int segments, int rings)
        {
            int stride = segments + 1;
            Vector3[] vertices = new Vector3[stride * (rings + 1)];
            for (int j = 0; j <= rings; j++)
            {
                float theta = Mathf.PI * j / rings;
                for (int i = 0; i <= segments; i++)
                {
                    float phi = 2f * Mathf.PI * i / segments;
                    vertices[j * stride + i] = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                }
            }
            int[] triangles = new int[segments * rings * 6];
            int t = 0;
            for (int j = 0; j < rings; j++)
            {
                for (int i = 0; i < segments; i++)
                {
                    int a = j * stride + i, b = a + 1, c = a + stride, d = c + 1;
                    triangles[t++] = a; triangles[t++] = b; triangles[t++] = c;
                    triangles[t++] = b; triangles[t++] = d; triangles[t++] = c;
                }
            }
            Mesh mesh = new Mesh {name = "ScaledSphere", vertices = vertices, normals = vertices, triangles = triangles};
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildQuad()
        {
            Mesh mesh = new Mesh
            {
                name = "ScaledPoint",
                vertices = new[] {new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f), new Vector3(-1f, 1f, 0f), new Vector3(1f, 1f, 0f)},
                uv = new[] {new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f)},
                triangles = new[] {0, 2, 1, 1, 2, 3},
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        public void Dispose()
        {
            for (int i = 0; i < _disks.Length; i++)
            {
                if (_diskMaterials[i] != null) Object.Destroy(_diskMaterials[i]);
                if (_pointMaterials[i] != null) Object.Destroy(_pointMaterials[i]);
            }
            if (_root != null) Object.Destroy(_root);
            if (_sphere != null) Object.Destroy(_sphere);
            if (_quad != null) Object.Destroy(_quad);
        }
    }
}
