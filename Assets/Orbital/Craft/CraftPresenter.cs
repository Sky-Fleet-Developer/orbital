using System;
using Orbital.Core.Flight;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Orbital.Craft
{
    /// <summary>Look of the flight view. All lengths in meters.</summary>
    [Serializable]
    public class CraftViewSettings
    {
        [Tooltip("Layer of everything the flight view draws; the flight camera draws only this layer. Any index 0..31, rarely used ones are best.")]
        public int layer = 31;
        [Tooltip("Half size of the ground patch under the craft (a square of this half side, bent along the planet).")]
        public float patchHalfSize = 4000f;
        [Tooltip("Cells of the ground patch along one side; the vertices are recomputed every frame.")]
        public int patchCells = 40;
        [Tooltip("Distance between the poles around the start pad.")]
        public float poleSpacing = 250f;
        [Tooltip("The pad and the poles are drawn while the craft is closer than this to the pad.")]
        public float markersVisibleDistance = 20000f;
        [Tooltip("Distance of the camera from the craft (the start value; the mouse wheel changes it).")]
        public float cameraDistance = 45f;
        [Tooltip("Mouse wheel zoom limits, meters.")]
        public float cameraMinDistance = 8f;
        public float cameraMaxDistance = 5000f;
        [Tooltip("Orbit camera: degrees per pixel of mouse drag.")]
        public float cameraDragSpeed = 0.25f;
        public float fieldOfView = 60f;
    }

    /// <summary>
    /// Draws the craft the player flies: a floating-origin view in which the craft is always at the origin and everything else is placed by the
    /// difference of double precision positions (so a planet radius of millions of meters costs no precision near the craft). Made of a craft
    /// object (prefab or the greybox from the factory), a low-polygon planet sphere for the far view, a ground patch bent along the planet right under
    /// the craft (exact near the ground), a pad and poles fixed to the ground at the start point (they show the motion), a light and a camera.
    /// The view lives on its own layer under its own root; the scene's map camera is switched off while the flight view is on.
    /// </summary>
    public sealed class CraftPresenter : IDisposable
    {
        private const float PlanetShrink = 0.999f; // the sphere is low-polygon: keep it under the exact ground patch

        private readonly CraftViewSettings _settings;
        private readonly SurfaceData _surface;
        private readonly double _padLatitude;
        private readonly double _padLongitude;
        private readonly float _cgHeight;
        private readonly GreyboxMaterials _materials;
        private readonly GameObject _root;
        private readonly Transform _craft;
        private readonly Transform _flame;
        private readonly Transform _planet;
        private readonly Camera _camera;
        private readonly Camera _mapCamera;
        private readonly GameObject _patchObject;
        private readonly Mesh _patch;
        private readonly Vector3[] _vertices;
        private readonly Vector3[] _normals;
        private readonly GameObject _markers;
        private readonly Transform _pad;
        private readonly Transform[] _poles;
        private readonly Vector2[] _poleOffsets;
        private bool _flightView = true;
        private bool _horizonCamera;
        // orbit camera (V or a mouse drag): up is the local vertical (gravity), yaw from local east toward north, pitch above the horizon
        private float _yaw;
        private float _pitch = 3f;
        private float _distance;
        private Vec3 _up = Vec3.UnitY;
        private Vec3 _east = Vec3.UnitX;
        private double _altitude;
        private bool _patchActive = true;
        private bool _markersActive = true;

        public bool FlightViewActive => _flightView;
        /// <summary>Root of the flight view (other crafts are added under it, stage 2c.7).</summary>
        public Transform Root => _root.transform;
        public GreyboxMaterials Materials => _materials;
        public float CgHeight => _cgHeight;
        public int Layer => _settings.layer;
        public bool HorizonCamera => _horizonCamera;

        /// <param name="vehiclePrefab">Craft look; null builds the greybox from primitives.</param>
        /// <param name="materialTemplate">Template of the greybox materials; null = the default material of a primitive (Editor only, magenta in a player).</param>
        /// <param name="padLatitude">Where the pad is, radians in the rotating body frame.</param>
        public CraftPresenter(CraftViewSettings settings, GameObject vehiclePrefab, Material materialTemplate, in SurfaceData surface, float cgHeight, double padLatitude, double padLongitude)
        {
            _settings = settings;
            _materials = new GreyboxMaterials(materialTemplate);
            _surface = surface;
            _cgHeight = cgHeight;
            _padLatitude = padLatitude;
            _padLongitude = padLongitude;
            _root = new GameObject("[CraftView]");

            GameObject craft = vehiclePrefab != null ? Object.Instantiate(vehiclePrefab) : GreyboxVehicleFactory.Build(cgHeight, _materials.Get);
            craft.name = "Craft";
            _craft = craft.transform;
            _craft.SetParent(_root.transform, false);
            _flame = _craft.Find(GreyboxVehicleFactory.FlameName);

            GameObject planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            planet.name = "Planet";
            RemoveCollider(planet);
            planet.GetComponent<Renderer>().sharedMaterial = _materials.Get("planet");
            planet.transform.SetParent(_root.transform, false);
            planet.transform.localScale = Vector3.one * (float) (2.0 * surface.Radius * PlanetShrink);
            _planet = planet.transform;

            int cells = Mathf.Max(2, settings.patchCells);
            int stride = cells + 1;
            _vertices = new Vector3[stride * stride];
            _normals = new Vector3[stride * stride];
            int[] triangles = new int[cells * cells * 6];
            int t = 0;
            for (int j = 0; j < cells; j++)
            {
                for (int i = 0; i < cells; i++)
                {
                    int a = j * stride + i, b = a + 1, c = a + stride, d = c + 1;
                    triangles[t++] = a; triangles[t++] = b; triangles[t++] = c;
                    triangles[t++] = b; triangles[t++] = d; triangles[t++] = c;
                }
            }
            _patch = new Mesh {name = "GroundPatch"};
            _patch.MarkDynamic();
            _patch.vertices = _vertices;
            _patch.normals = _normals;
            _patch.triangles = triangles;
            _patchObject = new GameObject("GroundPatch", typeof(MeshFilter), typeof(MeshRenderer));
            _patchObject.transform.SetParent(_root.transform, false);
            _patchObject.GetComponent<MeshFilter>().sharedMesh = _patch;
            _patchObject.GetComponent<MeshRenderer>().sharedMaterial = _materials.Get("ground");

            _markers = new GameObject("Markers");
            _markers.transform.SetParent(_root.transform, false);
            GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = "Pad";
            RemoveCollider(pad);
            pad.GetComponent<Renderer>().sharedMaterial = _materials.Get("pad");
            pad.transform.SetParent(_markers.transform, false);
            pad.transform.localScale = new Vector3(40f, 0.05f, 40f);
            _pad = pad.transform;
            _poles = new Transform[24];
            _poleOffsets = new Vector2[24];
            int k = 0;
            for (int j = -2; j <= 2; j++)
            {
                for (int i = -2; i <= 2; i++)
                {
                    if (i == 0 && j == 0) continue;
                    GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pole.name = "Pole";
                    RemoveCollider(pole);
                    pole.GetComponent<Renderer>().sharedMaterial = _materials.Get("pole");
                    pole.transform.SetParent(_markers.transform, false);
                    pole.transform.localScale = new Vector3(0.6f, 14f, 0.6f);
                    _poles[k] = pole.transform;
                    _poleOffsets[k] = new Vector2(i * settings.poleSpacing, j * settings.poleSpacing);
                    k++;
                }
            }

            GameObject sun = new GameObject("FlightSun");
            sun.transform.SetParent(_root.transform, false);
            sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;

            _mapCamera = Camera.main;
            GameObject cameraObject = new GameObject("FlightCamera");
            cameraObject.transform.SetParent(_craft, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = Mathf.Max(20000f, (float) (surface.Radius * 3.0));
            _camera.fieldOfView = settings.fieldOfView;
            _camera.cullingMask = 1 << settings.layer;
            if (RenderSettings.skybox != null)
            {
                _camera.clearFlags = CameraClearFlags.Skybox;
            }
            else
            {
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(0.02f, 0.03f, 0.08f);
            }
            _camera.depth = _mapCamera != null ? _mapCamera.depth + 1f : 0f;
            _distance = Mathf.Clamp(settings.cameraDistance, settings.cameraMinDistance, settings.cameraMaxDistance);

            foreach (Transform child in _root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = settings.layer;
            }
            ApplyView();
        }

        /// <summary>Flight view on: the flight camera and objects are shown and the scene's map camera is off. Off: the other way round.</summary>
        public void SetFlightView(bool active)
        {
            _flightView = active;
            ApplyView();
        }

        private void ApplyView()
        {
            _root.SetActive(_flightView);
            if (_mapCamera != null) _mapCamera.enabled = !_flightView;
        }

        /// <summary>
        /// Camera behind the craft and turning with it, or the orbit camera: around the craft, always level (up = the local vertical, away from
        /// the planet's center), turned by the mouse.
        /// </summary>
        public void SetHorizonCamera(bool horizon)
        {
            if (horizon && !_horizonCamera) CaptureOrbitFromCamera();
            _horizonCamera = horizon;
            _camera.transform.SetParent(horizon ? _root.transform : _craft, false);
        }

        /// <summary>
        /// Mouse control of the camera for this frame: <paramref name="drag"/> in pixels turns the orbit camera (a drag from the camera behind
        /// switches to the orbit camera where the camera is now), <paramref name="zoomNotches"/> of the wheel change the distance in both modes.
        /// </summary>
        public void ControlCamera(Vector2 drag, float zoomNotches)
        {
            if (zoomNotches != 0f)
            {
                _distance = Mathf.Clamp(_distance * Mathf.Pow(0.87f, zoomNotches), _settings.cameraMinDistance, _settings.cameraMaxDistance);
            }
            if (drag == Vector2.zero || !_flightView) return;
            if (!_horizonCamera) SetHorizonCamera(true);
            _yaw = Mathf.Repeat(_yaw + drag.x * _settings.cameraDragSpeed + 180f, 360f) - 180f;
            _pitch = Mathf.Clamp(_pitch - drag.y * _settings.cameraDragSpeed, MinPitch(), 85f);
        }

        // Lowest pitch that keeps the camera a meter above the ground (the target is the center of mass).
        private float MinPitch()
        {
            double clearance = _altitude + _cgHeight - 1.0;
            double sin = -clearance / Math.Max(1.0, _distance);
            return sin <= -1.0 ? -85f : Mathf.Max(-85f, (float) (Math.Asin(sin) * 180.0 / Math.PI));
        }

        // The orbit camera starts where the camera is now (from behind the craft), so switching does not jump.
        private void CaptureOrbitFromCamera()
        {
            Vector3 target = ToVector(_up) * _cgHeight;
            Vector3 offset = _root.transform.InverseTransformPoint(_camera.transform.position) - target;
            if (offset.sqrMagnitude < 1e-6f) return;
            Vector3 up = ToVector(_up), east = ToVector(_east), north = Vector3.Cross(up, east);
            Vector3 look = -offset.normalized;
            _pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(-Vector3.Dot(look, up), -1f, 1f)) * Mathf.Rad2Deg, -85f, 85f);
            Vector3 flat = look - up * Vector3.Dot(look, up);
            if (flat.sqrMagnitude > 1e-6f) _yaw = Mathf.Atan2(Vector3.Dot(flat, north), Vector3.Dot(flat, east)) * Mathf.Rad2Deg;
        }

        /// <summary>Puts everything where it is at the given state and game time. Call after the simulation step, once per frame.</summary>
        public void Present(in FlightState state, double time)
        {
            if (!_flightView) return;
            Vec3 pos = state.Position;
            double r = Vec3.Length(pos);
            Vec3 up = r > 0 ? pos / r : Vec3.UnitY;
            Vec3 east = East(up);
            Vec3 north = Vec3.Cross(up, east);
            _up = up;
            _east = east;
            _altitude = r - _surface.Radius;

            _craft.localPosition = Vector3.zero;
            _craft.localRotation = ToQuaternion(state.Orientation);
            PresentFlame(state);
            _planet.localPosition = ToVector(pos * -1.0);

            double altitude = r - _surface.Radius;
            bool patchNeeded = altitude < _settings.patchHalfSize * 3.0;
            if (patchNeeded != _patchActive)
            {
                _patchActive = patchNeeded;
                _patchObject.SetActive(patchNeeded);
            }
            if (patchNeeded) UpdatePatch(pos, up, east, north);

            PresentMarkers(pos, time);
            PresentCamera(up, east, north);
        }

        private void PresentFlame(in FlightState state)
        {
            if (_flame == null) return;
            float length = (float) (state.Throttle * 12.0);
            bool on = length > 0.05f && state.Mode != FlightMode.Crashed;
            if (_flame.gameObject.activeSelf != on) _flame.gameObject.SetActive(on);
            if (!on) return;
            _flame.localScale = new Vector3(1.3f, length * 0.5f, 1.3f);
            _flame.localPosition = new Vector3(0f, GreyboxVehicleFactory.NozzleExitY - length * 0.5f, 0f);
        }

        // The ground right under the craft: points of the planet sphere on a square grid of the tangent plane, relative to the craft.
        private void UpdatePatch(Vec3 pos, Vec3 up, Vec3 east, Vec3 north)
        {
            int cells = Mathf.Max(2, _settings.patchCells);
            int stride = cells + 1;
            double half = _settings.patchHalfSize;
            double radius = _surface.Radius;
            Vec3 center = up * radius;
            for (int j = 0; j <= cells; j++)
            {
                double dy = (j / (double) cells * 2.0 - 1.0) * half;
                for (int i = 0; i <= cells; i++)
                {
                    double dx = (i / (double) cells * 2.0 - 1.0) * half;
                    Vec3 normal = Vec3.Normalize(center + east * dx + north * dy);
                    Vec3 point = normal * radius;
                    int index = j * stride + i;
                    _vertices[index] = ToVector(point - pos);
                    _normals[index] = ToVector(normal);
                }
            }
            _patch.vertices = _vertices;
            _patch.normals = _normals;
            _patch.RecalculateBounds();
        }

        private void PresentMarkers(Vec3 pos, double time)
        {
            Vec3 padPoint = Surface.Point(_surface, _padLatitude, _padLongitude, time);
            bool near = Vec3.Length(padPoint - pos) < _settings.markersVisibleDistance;
            if (near != _markersActive)
            {
                _markersActive = near;
                _markers.SetActive(near);
            }
            if (!near) return;
            Vec3 padUp = Vec3.Normalize(padPoint);
            Vec3 padEast = East(padUp);
            Vec3 padNorth = Vec3.Cross(padUp, padEast);
            _pad.localPosition = ToVector(padPoint + padUp * 0.03 - pos);
            _pad.localRotation = Quaternion.FromToRotation(Vector3.up, ToVector(padUp));
            for (int k = 0; k < _poles.Length; k++)
            {
                Vec3 normal = Vec3.Normalize(padUp * _surface.Radius + padEast * _poleOffsets[k].x + padNorth * _poleOffsets[k].y);
                Vec3 foot = normal * _surface.Radius;
                _poles[k].localPosition = ToVector(foot + normal * 7.0 - pos);
                _poles[k].localRotation = Quaternion.FromToRotation(Vector3.up, ToVector(normal));
            }
        }

        private void PresentCamera(Vec3 up, Vec3 east, Vec3 north)
        {
            Transform camera = _camera.transform;
            float distance = _distance;
            if (_horizonCamera)
            {
                _pitch = Mathf.Max(_pitch, MinPitch()); // the craft came down: lift the camera out of the ground
                Vector3 upF = ToVector(up);
                Vector3 target = upF * _cgHeight;
                float yaw = _yaw * Mathf.Deg2Rad, pitch = _pitch * Mathf.Deg2Rad;
                Vector3 flat = ToVector(east) * Mathf.Cos(yaw) + ToVector(north) * Mathf.Sin(yaw);
                Vector3 look = flat * Mathf.Cos(pitch) - upF * Mathf.Sin(pitch);
                Vector3 position = target - look * distance;
                camera.localPosition = position;
                camera.localRotation = Quaternion.LookRotation(look, upF);
            }
            else
            {
                camera.localPosition = new Vector3(0f, _cgHeight, -distance);
                camera.localRotation = Quaternion.identity;
            }
        }

        // East at a point: the rotation axis crossed with the vertical (any horizontal direction at the poles).
        private Vec3 East(Vec3 up)
        {
            Vec3 east = Vec3.Cross(_surface.Axis, up);
            if (Vec3.Length(east) < 1e-6) east = Vec3.Cross(_surface.EquatorX, up);
            return Vec3.Normalize(east);
        }

        internal static Vector3 ToVector(Vec3 v) => new Vector3((float) v.X, (float) v.Y, (float) v.Z);

        internal static Quaternion ToQuaternion(DQuat q) => new Quaternion((float) q.X, (float) q.Y, (float) q.Z, (float) q.W);

        private static void RemoveCollider(GameObject go)
        {
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
        }

        public void Dispose()
        {
            if (_mapCamera != null) _mapCamera.enabled = true;
            if (_root != null) Object.Destroy(_root);
            if (_patch != null) Object.Destroy(_patch);
            _materials.Dispose();
        }
    }
}
