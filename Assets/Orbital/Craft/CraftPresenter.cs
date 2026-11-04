using System;
using Orbital.Core.Flight;
using Orbital.View.Flight;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Orbital.Craft
{
    /// <summary>A start pad on the ground of a body: radians in the rotating body frame; Body is an index of the body table (-1 without a table).</summary>
    public struct StartPad
    {
        public int Body;
        public double Latitude;
        public double Longitude;
    }

    /// <summary>
    /// Draws the craft the player flies: a floating-origin view in which the craft is always at the origin and everything else is placed by the
    /// difference of double precision positions (so a planet radius of millions of meters costs no precision near the craft). Made of a craft
    /// object (prefab or the greybox from the factory), a low-polygon planet sphere for the far view, a ground patch bent along the planet right under
    /// the craft (exact near the ground), a pad and poles fixed to the ground at the start point (they show the motion), a light and a camera.
    /// The view lives on its own layer under its own root; the scene's map camera is switched off while the flight view is on.
    /// Since stage 3.6, with a body table, the view has two cameras in a URP stack: Base draws the sky (<see cref="ScaledSpaceView"/>: every body at its
    /// exact direction and angular size, any distance), the Local camera is an overlay and draws the craft, the ground and the near planet as a real sphere.
    /// </summary>
    public sealed class CraftPresenter : IDisposable
    {
        private const float PlanetShrink = 0.999f; // the sphere is low-polygon: keep it under the exact ground patch
        private const float BasisSmoothingSeconds = 1.0f; // after a body change the V camera's vertical turns over about this much real time

        private readonly FlightCameraSettings _cameraSettings;
        private readonly LocationViewSettings _location;
        private SurfaceData _surface;
        private readonly StartPad[] _pads;
        private readonly float _cgHeight;
        private readonly GreyboxMaterials _materials;
        private readonly GameObject _root;
        private readonly Transform _craft;
        private readonly Transform _flame;
        private readonly Transform _planet;
        private readonly BodyFrameTable _frames;
        private readonly ScaledSpaceView _sky;
        private readonly Camera _skyCamera;      // the Base camera of the stack; null without a body table
        private readonly Transform _nearSphere;  // the current body as a real sphere in Local while the craft is low
        private readonly Material _nearMaterial;
        private readonly SunLightRig _sun;
        private NearPlanetState _nearState;
        private int _debugAimBody = -1;
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
        private int _parent = -1;           // index of the current body in _frames (-1 without a table)
        private Vec3 _up = Vec3.UnitY;
        private Vec3 _east = Vec3.UnitX;
        private Quaternion _basis;          // the smoothed horizon basis (stage 3.4): slerped towards the exact vertical of the current body
        private bool _basisSet;
        private double _altitude;
        private bool _patchActive = true;
        private bool _markersActive = true;

        public bool FlightViewActive => _flightView;
        /// <summary>Root of the flight view (other crafts are added under it, stage 2c.7).</summary>
        public Transform Root => _root.transform;
        public GreyboxMaterials Materials => _materials;
        public float CgHeight => _cgHeight;
        public int Layer => _cameraSettings.layer;
        public bool HorizonCamera => _horizonCamera;

        /// <param name="vehiclePrefab">Craft look; null builds the greybox from primitives.</param>
        /// <param name="materialTemplate">Template of the greybox materials; null = the default material of a primitive (Editor only, magenta in a player).</param>
        /// <param name="pads">The start pads; the nearest one of the current body is drawn when the craft is close to it.</param>
        /// <param name="frames">The world's body table: the sky of all bodies and the near planet (stage 3.6); null = one sphere (the scene world).</param>
        /// <param name="initialParent">Index of the craft's body in the table.</param>
        /// <param name="looks">View data parallel to the table (<see cref="BodyLook.Read"/>); required with a table.</param>
        public CraftPresenter(FlightViewSettings settings, GameObject vehiclePrefab, Material materialTemplate, in SurfaceData surface,
            float cgHeight, StartPad[] pads, BodyFrameTable frames = null, int initialParent = -1, BodyLook[] looks = null)
        {
            _cameraSettings = settings.FlightCamera;
            _location = settings.Location;
            _materials = new GreyboxMaterials(materialTemplate);
            _frames = frames;
            _parent = frames != null ? initialParent : -1;
            _surface = frames != null && initialParent >= 0 ? frames[initialParent].Surface : surface;
            _cgHeight = cgHeight;
            _pads = pads;
            _root = new GameObject("[CraftView]");

            GameObject craft = vehiclePrefab != null ? Object.Instantiate(vehiclePrefab) : GreyboxVehicleFactory.Build(cgHeight, _materials.Get);
            craft.name = "Craft";
            _craft = craft.transform;
            _craft.SetParent(_root.transform, false);
            _flame = _craft.Find(GreyboxVehicleFactory.FlameName);

            if (frames != null)
            {
                // stage 3.6: the near planet is one sphere with the look of the body's disk in the sky, so the hand-over at the threshold does not show
                _nearMaterial = ScaledSpaceView.NewBodyMaterial(Color.white, 0f, true);
                GameObject sphere = new GameObject("NearPlanet", typeof(MeshFilter), typeof(MeshRenderer));
                sphere.GetComponent<MeshRenderer>().sharedMaterial = _nearMaterial;
                sphere.transform.SetParent(_root.transform, false);
                sphere.SetActive(false);
                _nearSphere = sphere.transform;
            }
            else
            {
                GameObject planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                planet.name = "Planet";
                RemoveCollider(planet);
                planet.GetComponent<Renderer>().sharedMaterial = _materials.Get("planet");
                planet.transform.SetParent(_root.transform, false);
                planet.transform.localScale = Vector3.one * (float) (2.0 * surface.Radius * PlanetShrink);
                _planet = planet.transform;
            }

            int cells = Mathf.Max(2, _location.patchCells);
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
                    triangles[t++] = a; triangles[t++] = c; triangles[t++] = b; // the vertices go through ToVector, which mirrors the winding
                    triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
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
                    _poleOffsets[k] = new Vector2(i * _location.poleSpacing, j * _location.poleSpacing);
                    k++;
                }
            }

            _sun = new SunLightRig(settings.Lighting, _root.transform);

            _mapCamera = Camera.main;
            GameObject cameraObject = new GameObject("FlightCamera");
            cameraObject.transform.SetParent(_craft, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.nearClipPlane = _cameraSettings.nearClip;
            _camera.farClipPlane = FarOf(_surface.Radius);
            _camera.fieldOfView = _cameraSettings.fieldOfView;
            _camera.cullingMask = 1 << _cameraSettings.layer;
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
            if (frames != null)
            {
                GameObject skyObject = new GameObject("SkyCamera");
                skyObject.transform.SetParent(_root.transform, false);
                _skyCamera = skyObject.AddComponent<Camera>();
                _skyCamera.depth = _camera.depth;
                _camera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
                _skyCamera.GetUniversalAdditionalCameraData().cameraStack.Add(_camera);
            }
            _distance = Mathf.Clamp(_cameraSettings.distance, _cameraSettings.minDistance, _cameraSettings.maxDistance);

            foreach (Transform child in _root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = _cameraSettings.layer;
            }
            if (frames != null)
            {
                _sky = new ScaledSpaceView(frames, looks, _root.transform, _skyCamera, settings.Sky);
                _nearSphere.GetComponent<MeshFilter>().sharedMesh = _sky.Sphere;
                if (_parent >= 0) ApplyBodyLook();
            }
            ApplyView();
        }

        /// <summary>The flight map replaces the scene's map camera (stage 4): that camera stays off in both views.</summary>
        public bool OwnMap { get; set; }
        /// <summary>The camera depth of the flight view, for a view that takes its place.</summary>
        public float CameraDepth => _camera.depth;

        /// <summary>Flight view on: the flight camera and objects are shown and the scene's map camera is off. Off: the other way round.</summary>
        public void SetFlightView(bool active)
        {
            _flightView = active;
            ApplyView();
        }

        private void ApplyView()
        {
            _root.SetActive(_flightView);
            if (_mapCamera != null) _mapCamera.enabled = !_flightView && !OwnMap;
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
                _distance = Mathf.Clamp(_distance * Mathf.Pow(0.87f, zoomNotches), _cameraSettings.minDistance, _cameraSettings.maxDistance);
            }
            if (drag == Vector2.zero || !_flightView) return;
            if (!_horizonCamera) SetHorizonCamera(true);
            _yaw = Mathf.Repeat(_yaw + drag.x * _cameraSettings.dragSpeed + 180f, 360f) - 180f;
            _pitch = Mathf.Clamp(_pitch - drag.y * _cameraSettings.dragSpeed, MinPitch(), 85f);
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
            Vector3 up = ToVector(_up), east = ToVector(_east), north = ToVector(Vec3.Cross(_up, _east));
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
            Vec3 targetUp = r > 0 ? pos / r : Vec3.UnitY;
            Vec3 targetEast = East(targetUp);
            UpdateBasis(targetUp, targetEast);
            Vec3 up = _up;
            Vec3 east = _east;
            Vec3 north = Vec3.Cross(up, east);
            _altitude = r - _surface.Radius;

            _craft.localPosition = Vector3.zero;
            _craft.localRotation = ToQuaternion(state.Orientation);
            PresentFlame(state);

            bool nearPlanet = false;
            if (_frames != null)
            {
                nearPlanet = NearPlanetMode.Update(ref _nearState, _parent, _altitude, _surface.Radius);
                if (_nearSphere.gameObject.activeSelf != nearPlanet) _nearSphere.gameObject.SetActive(nearPlanet);
                if (nearPlanet) _nearSphere.localPosition = ToVector(pos * -1.0);
                double far = nearPlanet ? NearPlanetMode.LocalFar(_altitude, _surface.Radius) : 0.0;
                _camera.farClipPlane = Mathf.Max(_cameraSettings.minFar, (float) far);
            }
            else
            {
                _planet.localPosition = ToVector(pos * -1.0);
            }

            bool patchNeeded = _altitude < _location.patchHalfSize * 3.0;
            if (patchNeeded != _patchActive)
            {
                _patchActive = patchNeeded;
                _patchObject.SetActive(patchNeeded);
            }
            // the patch follows the exact vertical of the body (not the smoothed camera basis)
            if (patchNeeded) UpdatePatch(pos, targetUp, targetEast, Vec3.Cross(targetUp, targetEast));

            PresentMarkers(pos, time);
            PresentCamera(up, east, north);
            if (_frames != null) PresentSky(pos, time, nearPlanet);
        }

        // The sky follows the Local camera exactly; the eye is the camera itself (the craft is at the origin of the view), so a close body gets its parallax.
        private void PresentSky(Vec3 pos, double time, bool nearPlanet)
        {
            Transform camera = _camera.transform;
            Vector3 eyeOffset = camera.position;
            Vec3 eye = _frames.GlobalPosition(_parent, time, pos) + new Vec3(eyeOffset.x, eyeOffset.y, eyeOffset.z);
            if (_debugAimBody >= 0)
            {
                _frames.GlobalState(_debugAimBody, time, out Vec3 target, out _);
                camera.rotation = Quaternion.LookRotation(ToVector(Vec3.Normalize(target - eye)), Vector3.up);
            }
            _skyCamera.transform.rotation = camera.rotation;
            _skyCamera.fieldOfView = _camera.fieldOfView;
            _sky.Present(time, in eye, nearPlanet ? _parent : -1);
            Vec3 light = _sky.LightDirection;
            _sun.Point(light);
            ScaledSpaceView.SetSunDirection(_nearMaterial, light);
        }

        /// <summary>Automated player runs (stage 3.6): the camera looks at this body with this vertical field of view; body -1 / fov 0 change nothing.</summary>
        public void SetDebugAim(int body, float fieldOfView)
        {
            _debugAimBody = body;
            if (fieldOfView > 0f) _camera.fieldOfView = fieldOfView;
        }

        /// <summary>
        /// The craft moved to another body (stage 3.4): the ground, the sphere radius and the camera far plane take the new body's numbers; the
        /// start pad and the poles are shown only while the craft is around the body it started on. The view objects and the camera are kept:
        /// the floating origin stays on the craft, so the picture does not jump.
        /// </summary>
        public void SetBody(int parent)
        {
            if (_frames == null || parent < 0 || parent >= _frames.Count) return;
            _parent = parent;
            _surface = _frames[parent].Surface;
            ApplyBodyLook();
        }

        // The near planet and the ground take the look of the current body.
        private void ApplyBodyLook()
        {
            BodyLook look = _sky.LookOf(_parent);
            _nearSphere.localScale = Vector3.one * (float) (_surface.Radius * PlanetShrink);
            _nearMaterial.SetColor("_Tint", look.Tint);
            Material ground = _materials.Get("ground");
            ground.color = look.Tint;
            if (ground.HasProperty("_BaseColor")) ground.SetColor("_BaseColor", look.Tint);
        }

        private static float FarOf(double radius) => Mathf.Max(20000f, (float) (radius * 3.0));

        // The horizon camera's basis (up = local vertical, east) follows the current body. On a body change the vertical jumps by an arbitrary
        // angle: the basis is slerped over about BasisSmoothingSeconds of real time, so the V camera turns smoothly; the patch and the markers
        // keep using the exact vertical. Yaw and pitch of the camera stay where the player put them relative to the basis.
        private void UpdateBasis(Vec3 targetUp, Vec3 targetEast)
        {
            Vector3 up = ToVector(targetUp);
            Vector3 east = ToVector(targetEast);
            Vector3 north = ToVector(Vec3.Cross(targetUp, targetEast));
            Quaternion target = Quaternion.LookRotation(north, up);
            if (!_basisSet)
            {
                _basis = target;
                _basisSet = true;
            }
            else
            {
                float alpha = 1f - Mathf.Exp(-Time.unscaledDeltaTime / BasisSmoothingSeconds);
                _basis = Quaternion.Slerp(_basis, target, alpha);
            }
            Vector3 smoothUp = _basis * Vector3.up;
            Vector3 smoothEast = _basis * Vector3.right; // LookRotation(north, up): right is east, as on a map seen from above
            _up = FromVector(smoothUp);
            _east = FromVector(smoothEast);
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
            int cells = Mathf.Max(2, _location.patchCells);
            int stride = cells + 1;
            double half = _location.patchHalfSize;
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
            // the nearest pad of the current body; there is one set of objects, it moves between the pads
            bool near = false;
            Vec3 padPoint = default;
            double nearest = _location.markersVisibleDistance;
            foreach (StartPad pad in _pads)
            {
                if (pad.Body != _parent) continue;
                Vec3 point = Surface.Point(_surface, pad.Latitude, pad.Longitude, time);
                double distance = Vec3.Length(point - pos);
                if (distance >= nearest) continue;
                nearest = distance;
                padPoint = point;
                near = true;
            }
            if (near)
            {
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
            if (near != _markersActive)
            {
                _markersActive = near;
                _markers.SetActive(near);
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
                // on the side of the pilot's top (body -Z, local +Z): the nose is up on the screen, the pilot's right is on the right
                camera.localPosition = new Vector3(0f, _cgHeight, distance);
                camera.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
        }

        // East at a point: the rotation axis crossed with the vertical (any horizontal direction at the poles).
        private Vec3 East(Vec3 up)
        {
            Vec3 east = Vec3.Cross(_surface.Axis, up);
            if (Vec3.Length(east) < 1e-6) east = Vec3.Cross(_surface.EquatorX, up);
            return Vec3.Normalize(east);
        }

        // The simulation is right-handed, Unity is left-handed: Z changes sign on the way to the engine. Copying the numbers as they are shows the world
        // in a mirror: the keys, which are right for the pilot of the simulation, turn the other way on the screen (found 2026-11-04).
        internal static Vector3 ToVector(Vec3 v) => new Vector3((float) v.X, (float) v.Y, (float) -v.Z);

        internal static Vec3 FromVector(Vector3 v) => new Vec3(v.x, v.y, -v.z);

        internal static Quaternion ToQuaternion(DQuat q) => new Quaternion((float) -q.X, (float) -q.Y, (float) q.Z, (float) q.W);

        private static void RemoveCollider(GameObject go)
        {
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
        }

        public void Dispose()
        {
            if (_mapCamera != null) _mapCamera.enabled = true;
            _sky?.Dispose();
            if (_nearMaterial != null) Object.Destroy(_nearMaterial);
            if (_root != null) Object.Destroy(_root);
            if (_patch != null) Object.Destroy(_patch);
            _materials.Dispose();
        }
    }
}
