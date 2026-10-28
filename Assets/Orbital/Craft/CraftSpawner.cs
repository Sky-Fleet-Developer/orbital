using System;
using System.Globalization;
using System.Text;
using Ara3D;
using Ara3D.Double;
using Orbital.Core;
using Orbital.Core.Bodies;
using Orbital.Core.Config;
using Orbital.Core.Flight;
using Orbital.Core.Handles;
using Orbital.Core.TrajectorySystem;
using Orbital.Technology;
using Orbital.Technology.Components;
using UnityEngine;
using Zenject;

namespace Orbital.Craft
{
    public enum CraftSpawnKind
    {
        /// <summary>The start point from startpoints.json (<see cref="CraftSpawner"/> field start point id; empty = the default one).</summary>
        StartPoint,
        /// <summary>A point of the ground of the chosen body: latitude, longitude, altitude (0 = standing on the ground).</summary>
        Surface,
        /// <summary>A circular orbit around the chosen body.</summary>
        CircularOrbit,
    }

    /// <summary>
    /// Flight test spawner: ONE component, added by hand to a root object of the scene Main. When the world is loaded it reads the shared configs,
    /// finds the body, builds the ground from the model data, puts the player craft at the chosen point and flies it with the own double precision
    /// physics (ecs world <see cref="CraftSimulation"/>, fixed step), draws it (<see cref="CraftPresenter"/>), reads the keyboard through the new
    /// Input System (<see cref="CraftInput"/>) and shows a greybox HUD (<see cref="CraftHud"/>). Everything is created from code; the scene is not touched.
    /// Limits of stage 2b: one body (no change of the sphere of influence), no network, the craft is not drawn on the map.
    /// </summary>
    public class CraftSpawner : MonoBehaviour, IViewInitializer, IUpdateHandler, IFixedUpdateHandler, ILateUpdateHandler
    {
        private const string EditorConfigFolder = "Assets/_game/Config/";
        private const double DegToRad = Math.PI / 180.0;

        [Header("Where")]
        [SerializeField] private CraftSpawnKind spawnKind = CraftSpawnKind.StartPoint;
        [Tooltip("Start point id for the StartPoint kind; empty = the default point of startpoints.json.")]
        [SerializeField] private string startPointId = "";
        [Tooltip("Name of the body in the scene (case does not matter) for the Surface and CircularOrbit kinds.")]
        [SerializeField] private string bodyName = "Earth";
        [Header("Surface kind (degrees, meters)")]
        [SerializeField] private double latitudeDegrees;
        [SerializeField] private double longitudeDegrees;
        [Tooltip("0 = standing on the ground; above 0 the craft starts in the air with the speed of the ground and falls.")]
        [SerializeField] private double altitudeMeters;
        [Header("CircularOrbit kind")]
        [SerializeField] private double orbitAltitudeMeters = 300000;
        [Tooltip("Tilt of the orbit plane from the equator of the body.")]
        [SerializeField] private double inclinationDegrees;
        [Tooltip("Angle from the ascending node along the orbit.")]
        [SerializeField] private double phaseDegrees;

        [Header("Craft")]
        [Tooltip("Optional look of the craft (the prefab from the menu Orbital/Build Greybox Vehicle); empty = the same greybox is built from primitives.")]
        [SerializeField] private GameObject vehiclePrefab;
        [Tooltip("Craft model id in craftspec.json; empty = the default model.")]
        [SerializeField] private string craftId = "";
        [SerializeField] private string callsign = "Pilot";
        [Tooltip("Same as the server maxLandingSpeed: touchdown relative to the ground faster than this destroys the craft.")]
        [SerializeField] private double maxLandingSpeed = 5.0;
        [Tooltip("Hold a landed craft upright until the kernel has a tip-over model (see the stage 2b notes). Switch off to see the inverted pendulum.")]
        [SerializeField] private bool groundSupport = true;

        [Header("Shared configs (empty = read Assets/_game/Config in the Editor)")]
        [SerializeField] private TextAsset flightConfigJson;
        [SerializeField] private TextAsset fuelsJson;
        [SerializeField] private TextAsset craftSpecJson;
        [SerializeField] private TextAsset startPointsJson;

        [Header("View")]
        [SerializeField] private CraftViewSettings view = new CraftViewSettings();
        [SerializeField] private bool showHud = true;

        [Inject] private World _world;
        [Inject] private DiContainer _container;

        private bool _started;
        private bool _registered;
        private bool _ready;
        private GameConfig _config;
        private FlightConfig _flight;
        private FlightParams _flightParams;
        private SurfaceData _surface;
        private string _bodyTitle = string.Empty;
        private bool _isOrbit;
        private double _spawnLatitude;
        private double _spawnLongitude;
        private double _spawnAltitude;
        private CraftSimulation _simulation;
        private CraftPresenter _presenter;
        private CraftInput _input;
        private CraftHud _hud;
        private NavBallView _navBall;
        private bool _navOrbit;     // navigation ball shows the orbital (inertial) velocity instead of the one relative to the surface
        private float _nextHudTime;
        private readonly StringBuilder _text = new StringBuilder(1024);

        int IOrderHolder.Order => 10; // after TimeProvider (-1)

        // The bootstrapper calls this once the world is loaded and bound.
        void IViewInitializer.InitializeView()
        {
            if (!Application.isPlaying || _started) return;
            _started = true;
            if (_world == null || !_world.IsLoaded)
            {
                Debug.LogError("CraftSpawner: the world is not loaded, nothing to spawn onto", this);
                return;
            }
            view.layer = Mathf.Clamp(view.layer, 0, 31);
            LoadConfig();
            if (!Setup()) return;
            _input = new CraftInput();
            Respawn();
            _presenter = new CraftPresenter(view, vehiclePrefab, _surface, (float) _flight.CgHeight, PadLatitude(), PadLongitude());
            _presenter.SetFlightView(true);
            GameObject hudObject = new GameObject("[CraftHud]");
            hudObject.transform.SetParent(transform, false);
            _hud = hudObject.AddComponent<CraftHud>();
            _hud.Help = HelpText;
            _hud.enabled = showHud;
            _navBall = new NavBallView();
            _hud.NavBall = _navBall;
            HandlesRegister.RegisterHandlers(this, _container);
            _registered = true;
            _ready = true;
        }

        void IUpdateHandler.Update()
        {
            if (!_ready) return;
            _input.Poll();
            if (_input.ToggleViewPressed) _presenter.SetFlightView(!_presenter.FlightViewActive);
            if (_input.ToggleCameraPressed) _presenter.SetHorizonCamera(!_presenter.HorizonCamera);
            if (_input.ToggleHelpPressed) _hud.ShowHelp = !_hud.ShowHelp;
            if (_input.ToggleNavModePressed) _navOrbit = !_navOrbit;
            if (_input.RespawnPressed) Respawn();
        }

        void IFixedUpdateHandler.FixedUpdate()
        {
            if (!_ready) return;
            _simulation.Clock.Dt = Time.fixedDeltaTime;
            _simulation.SetKeys(_input.Consume());
            _simulation.Step();
        }

        void ILateUpdateHandler.LateUpdate()
        {
            if (!_ready) return;
            FlightState state = _simulation.State;
            _presenter.Present(state, _simulation.Clock.Time);
            if (showHud) _navBall.Update(state, _surface, _navOrbit);
            if (showHud && Time.unscaledTime >= _nextHudTime)
            {
                _nextHudTime = Time.unscaledTime + 0.1f;
                UpdateHud(state);
            }
        }

        private void OnDestroy()
        {
            if (_registered && _container != null)
            {
                try
                {
                    HandlesRegister.UnregisterHandlers(this, _container);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"CraftSpawner: handlers were not unregistered: {e.Message}");
                }
                _registered = false;
            }
            _ready = false;
            _presenter?.Dispose();
            _navBall?.Dispose();
            _input?.Dispose();
            _simulation?.Dispose();
        }

        // ------------------------------------------------------------------ setup

        private void LoadConfig()
        {
            try
            {
                _config = GameConfigLoader.FromJson(
                    TextOf(flightConfigJson, GameConfigLoader.FlightFile),
                    TextOf(fuelsJson, GameConfigLoader.FuelsFile),
                    TextOf(craftSpecJson, GameConfigLoader.CraftSpecFile),
                    TextOf(startPointsJson, GameConfigLoader.StartPointsFile));
            }
            catch (Exception e)
            {
                Debug.LogError($"CraftSpawner: cannot read the shared configs, built-in defaults are used: {e.Message}", this);
                _config = new GameConfig();
            }
        }

        private static string TextOf(TextAsset asset, string file)
        {
            if (asset != null) return asset.text;
#if UNITY_EDITOR
            TextAsset loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(EditorConfigFolder + file);
            if (loaded != null) return loaded.text;
#endif
            return null;
        }

        private bool Setup()
        {
            string bodyKey = bodyName;
            double latitude = latitudeDegrees, longitude = longitudeDegrees, altitude = altitudeMeters;
            bool orbit = spawnKind == CraftSpawnKind.CircularOrbit;
            if (spawnKind == CraftSpawnKind.StartPoint)
            {
                StartPointDef point = _config.FindStartPoint(startPointId);
                if (point == null)
                {
                    Debug.LogError($"CraftSpawner: no start point '{startPointId}' (and no default one) in startpoints.json", this);
                    return false;
                }
                if (!point.IsSurface)
                {
                    Debug.LogError($"CraftSpawner: start point '{point.Id}' is of kind '{point.Kind}', only surface points are supported", this);
                    return false;
                }
                bodyKey = point.BodyId;
                latitude = point.Latitude;
                longitude = point.Longitude;
                altitude = 0;
            }

            StaticBody found = FindBody(bodyKey);
            if (found == null)
            {
                Debug.LogError($"CraftSpawner: no body '{bodyKey}' among the bodies of the world", this);
                return false;
            }
            IStaticBody body = found;
            if (body.Radius <= 0)
            {
                Debug.LogError($"CraftSpawner: body '{found.name}' has no radius (no PhysicalFacet in the body table), it has no surface", this);
                return false;
            }
            _bodyTitle = BodyTable.KeyOf(found.name);
            _surface = SurfaceBuilder.Build(body.Radius, ReadRotation(found), ReadOrbitFrame(body));
            _flight = _config.ResolveFlight(craftId);
            _flightParams = new FlightParams
            {
                Craft = _flight,
                Surface = _surface,
                Nu = body.GravParameter,
                MaxLandingSpeed = maxLandingSpeed,
            };
            _isOrbit = orbit;
            _spawnLatitude = latitude * DegToRad;
            _spawnLongitude = longitude * DegToRad;
            _spawnAltitude = altitude;

            _simulation = new CraftSimulation(TimeProvider.WorldTime, Time.fixedDeltaTime, groundSupport, RecorderConfig.Default());
            return true;
        }

        // The same body can appear twice in the scene tree: as the central mass of a branch and as the branch itself (which has the orbit around
        // the parent). The branch is the body of the world, so a satellite with a parent wins; the root (no parent) comes second.
        private StaticBody FindBody(string key)
        {
            string wanted = Normalize(key);
            StaticBody best = null;
            int bestScore = -1;
            foreach (StaticBody candidate in _world.GetComponentsInChildren<StaticBody>(true))
            {
                if (Normalize(BodyTable.KeyOf(candidate.name)) != wanted) continue;
                IStaticBody body = candidate;
                int score = body.Parent != null && body.IsSatellite ? 2 : (body.Parent == null ? 1 : 0);
                if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }
            return best;
        }

        private static string Normalize(string name)
        {
            return string.IsNullOrEmpty(name) ? string.Empty : name.Replace(" ", "").Replace("-", "").ToLowerInvariant();
        }

        private static RotationParams ReadRotation(StaticBody body)
        {
            RotationParams result = default;
            BodyRecord record = ((IStaticBodyAccessor) body).Record;
            RotationFacet facet = record?.GetFacet<RotationFacet>();
            if (facet == null) return result;
            result.PeriodSeconds = facet.rotationPeriodSeconds;
            result.ObliquityDegrees = facet.axialTiltDegrees;
            result.AxisAzimuthDegrees = facet.axisAzimuthDegrees;
            result.InitialAngleDegrees = facet.initialAngleDegrees;
            result.TidallyLocked = facet.tidallyLocked;
            return result;
        }

        private static OrbitFrame ReadOrbitFrame(IStaticBody body)
        {
            OrbitFrame frame = default;
            if (body.Parent == null) return frame;
            StaticOrbit orbit = body.Orbit;
            DMatrix4x4 matrix = orbit.RotationMatrix;
            frame.HasParent = true;
            frame.Normal = ToVec(matrix.Up());
            frame.Forward = ToVec(matrix.Forward());
            frame.Right = ToVec(matrix.Right());
            frame.MeanMotion = orbit.MeanMotion;
            frame.PositionAtTimeZero = ToVec(orbit.GetSample(0.0, true, false).position);
            return frame;
        }

        private static Vec3 ToVec(DVector3 v) => new Vec3(v.x, v.y, v.z);

        // ------------------------------------------------------------------ spawn

        private FlightState MakeInitialState(double time)
        {
            if (_isOrbit)
            {
                return SpawnBuilder.CircularOrbit(_flight, _surface, _flightParams.Nu, orbitAltitudeMeters, inclinationDegrees * DegToRad, phaseDegrees * DegToRad);
            }
            return SpawnBuilder.OnSurface(_flight, _surface, _spawnLatitude, _spawnLongitude, _spawnAltitude, time);
        }

        /// <summary>A fresh craft (full tanks) at the chosen point; the game time goes on.</summary>
        private void Respawn()
        {
            double time = _simulation.Clock.Time;
            FlightState state = MakeInitialState(time);
            _simulation.CreatePlayerCraft(state, _flightParams, callsign);
            if (_isOrbit && _presenter == null)
            {
                // the pad stays on the ground below the first position
                Surface.ToBodyCoordinates(_surface, state.Position, time, out _spawnLatitude, out _spawnLongitude);
            }
        }

        private double PadLatitude() => _spawnLatitude;

        private double PadLongitude() => _spawnLongitude;

        // ------------------------------------------------------------------ HUD

        private const string HelpText =
            "CONTROLS\n" +
            "W / S        pitch\n" +
            "A / D        roll\n" +
            "Q / E        yaw\n" +
            "Shift / Ctrl throttle up / down\n" +
            "X / Z        throttle full / zero\n" +
            "R            RCS on / off\n" +
            "T            SAS (hold attitude) on / off\n" +
            "B            nav ball: surface / orbit velocity\n" +
            "Enter        new craft at the start\n" +
            "C            flight view / map view\n" +
            "V            camera behind / on horizon\n" +
            "F1           hide this help\n" +
            "The gyro is always on; RCS needs R.\n" +
            "SAS holds the attitude; with RCS off it uses\n" +
            "the gyro only and gives up when it saturates.";

        private void UpdateHud(in FlightState state)
        {
            FlightTelemetry t = FlightTelemetry.Compute(state, _flightParams);
            ControlInput control = _simulation.Control;
            StepReportComponent report = _simulation.Report;
            ThrustRecorderComponent recorder = _simulation.Recorder;
            CultureInfo c = CultureInfo.InvariantCulture;
            StringBuilder sb = _text;
            sb.Clear();
            sb.Append(callsign).Append(" @ ").Append(_bodyTitle).Append("    t = ").Append(_simulation.Clock.Time.ToString("F0", c)).Append(" s\n");
            sb.Append("Mode: ").Append(ModeName(state.Mode));
            if (state.Mode == FlightMode.Crashed) sb.Append("  (impact ").Append(report.LastImpactSpeed.ToString("F1", c)).Append(" m/s, limit ").Append(_flightParams.MaxLandingSpeed.ToString("F1", c)).Append(")");
            sb.Append('\n');
            sb.Append("Altitude: ").Append(Distance(t.Altitude, c)).Append("    Vertical: ").Append(t.VerticalSpeed.ToString("F1", c)).Append(" m/s\n");
            sb.Append("Ground speed: ").Append(t.GroundSpeed.ToString("F1", c)).Append(" m/s  (horizontal ").Append(t.HorizontalSpeed.ToString("F1", c)).Append(")\n");
            sb.Append("Inertial speed: ").Append(t.InertialSpeed.ToString("F1", c)).Append(" m/s\n");
            sb.Append("Throttle: ").Append((state.Throttle * 100.0).ToString("F0", c)).Append("%    TWR ").Append(t.ThrustToWeight.ToString("F2", c)).Append('\n');
            sb.Append("Mass: ").Append((t.Mass / 1000.0).ToString("F2", c)).Append(" t\n");
            sb.Append("Main fuel: ").Append(state.MainFuel.ToString("F0", c)).Append(" kg (").Append((t.MainFuelFraction * 100.0).ToString("F0", c)).Append("%)\n");
            sb.Append("RCS fuel: ").Append(state.RcsFuel.ToString("F1", c)).Append(" kg (").Append((t.RcsFuelFraction * 100.0).ToString("F0", c)).Append("%)    RCS ").Append(control.RcsOn ? "ON" : "off").Append('\n');
            sb.Append("Gyro store: ").Append((t.GyroFraction * 100.0).ToString("F0", c)).Append('%').Append(state.Dumping != 0 ? "  UNLOADING" : string.Empty).Append('\n');
            sb.Append("Tilt from vertical: ").Append(t.TiltDegrees.ToString("F1", c)).Append(" deg\n");
            sb.Append("Rates deg/s: pitch ").Append(t.PitchRateDegPerSec.ToString("F2", c)).Append("  roll ").Append(t.RollRateDegPerSec.ToString("F2", c))
                .Append("  yaw ").Append(t.YawRateDegPerSec.ToString("F2", c)).Append('\n');
            sb.Append("SAS: ");
            if (t.SasOn)
            {
                sb.Append("ON ").Append(SasPhaseName(t.SasPhase)).Append("  error ").Append(t.SasErrorDegrees.ToString("F2", c)).Append(" deg");
                if (!control.RcsOn || state.RcsFuel <= 0.0) sb.Append("  (gyro only)");
            }
            else sb.Append("off  (T)");
            sb.Append('\n');
            if (_navBall != null && _navBall.HasData)
            {
                NavBallData nav = _navBall.Data;
                sb.Append("Nose: heading ").Append(nav.HeadingDegrees.ToString("F1", c)).Append("  pitch ").Append(nav.PitchDegrees.ToString("F1", c))
                    .Append("  roll ").Append(nav.RollDegrees.ToString("F1", c)).Append('\n');
                _hud.NavCaption = (nav.OrbitMode ? "ORBIT " : "SURFACE ") + nav.Speed.ToString("F1", c) + " m/s    (B)";
            }
            sb.Append("Thrust segments: ").Append(recorder.SentCount).Append(" (queued ").Append(recorder.State.Count).Append(", dropped ").Append(recorder.State.Overflowed)
                .Append(", ").Append(recorder.State.Active ? "burning" : "idle").Append(")");

            _hud.Telemetry = sb.ToString();
            _hud.Throttle = (float) state.Throttle;
            _hud.MainFuel = (float) t.MainFuelFraction;
            _hud.RcsFuel = (float) t.RcsFuelFraction;
            _hud.Gyro = (float) t.GyroFraction;
            _hud.RcsOn = control.RcsOn;
            _hud.Banner = state.Mode == FlightMode.Crashed ? "CRASHED - press Enter" : string.Empty;
        }

        private static string SasPhaseName(int phase)
        {
            switch (phase)
            {
                case SasPhase.Manual: return "pilot";
                case SasPhase.Settle: return "braking";
                default: return "hold";
            }
        }

        private static string ModeName(int mode)
        {
            switch (mode)
            {
                case FlightMode.Flying: return "FLYING";
                case FlightMode.Landed: return "LANDED";
                default: return "CRASHED";
            }
        }

        private static string Distance(double meters, CultureInfo c)
        {
            return Math.Abs(meters) >= 10000.0 ? (meters / 1000.0).ToString("F1", c) + " km" : meters.ToString("F1", c) + " m";
        }
    }
}
