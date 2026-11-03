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
using Orbital.Core.Net;
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
    public enum CraftNetMode
    {
        /// <summary>Local flight test as in stage 2b: the craft lives on the client only.</summary>
        Offline,
        /// <summary>Stage 2c.6: the craft lives on the server; Enter asks it for a spawn, the time follows the server.</summary>
        Online,
    }

    public class CraftSpawner : MonoBehaviour, IViewInitializer, IUpdateHandler, IFixedUpdateHandler, ILateUpdateHandler
    {
        private const string EditorConfigFolder = "Assets/_game/Config/";
        private const double DegToRad = Math.PI / 180.0;

        [Header("Network (stage 2c.6)")]
        [SerializeField] private CraftNetMode netMode = CraftNetMode.Offline;
        [Tooltip("Server address for Online; the environment variable ORBITAL_SERVER_URL wins. Callsign and tokens: persistentDataPath/orbital-client.json.")]
        [SerializeField] private string serverUrl = ClientSettings.DefaultUrl;

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
        [Tooltip("Template of the runtime greybox materials (a URP Lit material, e.g. Assets/_game/Prefabs/Craft/Materials/Greybox_hull.mat); empty = the default material of a primitive, which works in the Editor only: a player build gets the built-in Standard shader and draws magenta.")]
        [SerializeField] private Material greyboxMaterialTemplate;
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

        [Header("Test tools (offline only)")]
        [Tooltip("Time scale slider in the HUD (a test tool: there is no player time acceleration). Shown only in the Editor and in development builds.")]
        [SerializeField] private bool timeScaleSlider = true;
        [Tooltip("Most simulation steps per fixed tick; above it the real time scale is lower than the set one (shown in the HUD).")]
        [SerializeField] private int maxStepsPerTick = GameClock.DefaultMaxStepsPerTick;

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
        private long _nextStep;     // index of the next step of the craft on the step grid of TimeProvider (the craft catches up to its StepCount)
        private float _rateRealStart = -1f;
        private double _rateGameStart;
        private double _measuredRate = 1.0; // game seconds per real second over the last second
        private readonly StringBuilder _text = new StringBuilder(1024);
        // online (stage 2c.6)
        private CraftNetLink _net;
        private GhostPresenter _ghosts;
        private InputLatch _latch;
        private ControlInput _lastControl;
        private DisciplineConfig _discipline = DisciplineConfig.Default();
        private bool Online => _net != null;

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
            if (EffectiveNetMode() == CraftNetMode.Online) StartOnline();
            else Respawn();
            _presenter = new CraftPresenter(view, vehiclePrefab, greyboxMaterialTemplate, _surface, (float) _flight.CgHeight, PadLatitude(), PadLongitude());
            _presenter.SetFlightView(true);
            if (Online) _ghosts = new GhostPresenter(_presenter);
            GameObject hudObject = new GameObject("[CraftHud]");
            hudObject.transform.SetParent(transform, false);
            _hud = hudObject.AddComponent<CraftHud>();
            _hud.Help = HelpText;
            _hud.enabled = showHud;
            _hud.ShowTimeScale = !Online && timeScaleSlider && Debug.isDebugBuild; // the Editor counts as a debug build
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
            if (_input.RespawnPressed)
            {
                if (Online) OnlineSpawn();
                else Respawn();
            }
        }

        // TimeProvider (order -1) owns the scaled game time: each fixed tick it adds 0, 1 or many fixed steps. The craft makes exactly those steps,
        // all with the same fixed Dt (the step does not grow with the scale), so its time and the world time stay on one grid. The keys are sampled
        // once per tick and used for every step of that tick (the toggles R and T fire once: they react to the press, not to the held key);
        // in slow motion a tick without a step does not consume the keys, they wait in CraftInput for the next step.
        void IFixedUpdateHandler.FixedUpdate()
        {
            if (!_ready) return;
            if (Online)
            {
                OnlineFixedUpdate();
                return;
            }
            long target = TimeProvider.StepCount;
            if (_nextStep >= target) return;
            KeyState keys = _input.Consume();
            _simulation.Clock.Dt = TimeProvider.StepSeconds;
            while (_nextStep < target)
            {
                _simulation.SetKeys(keys);
                _simulation.Clock.Time = TimeProvider.TimeOfStep(_nextStep);
                _simulation.Step();
                _nextStep++;
            }
            _simulation.Clock.Time = TimeProvider.TimeOfStep(_nextStep);
        }

        void ILateUpdateHandler.LateUpdate()
        {
            if (!_ready) return;
            FlightState state = Online ? _net.Link.State : _simulation.State;
            if (Online)
            {
                if (_net.Link.HasCraft)
                {
                    FlightState shown = state;
                    shown.Position = state.Position + _net.Link.View.Offset; // a soft correction is caught up by the view, not by a jump
                    _presenter.Present(shown, CraftTime);
                    _net.AdvanceOthers(CraftTime);
                    _ghosts?.Present(shown.Position, _net.OwnParent, _net.Others);
                    _ghosts?.PresentWrecks(shown.Position, _net.OwnParent, _net.Wrecks, _surface, CraftTime);
                }
                else state = default;
            }
            else _presenter.Present(state, CraftTime);
            if (showHud && (!Online || _net.Link.HasCraft)) _navBall.Update(state, _surface, _navOrbit);
            MeasureRate();
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
            _ghosts?.Dispose();
            _ghosts = null;
            if (_net != null)
            {
                _net.Dispose();
                _net = null;
                TimeProvider.SetExternalTime(false);
            }
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
                Debug.LogError($"CraftSpawner: body '{found.name}' has no radius (no radius in the world file, or no PhysicalFacet in the body table for the source Scene), it has no surface", this);
                return false;
            }
            _bodyTitle = BodyTable.KeyOf(found.name);
            _surface = SurfaceBuilder.Build(body.Radius, ReadRotation(found), ReadOrbitFrame(body, _world.EpochZero), _world.EpochZero);
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

            TimeProvider.SetMaxStepsPerTick(maxStepsPerTick);
            _nextStep = TimeProvider.StepCount;
            _simulation = new CraftSimulation(TimeProvider.TimeOfStep(_nextStep), TimeProvider.StepSeconds, groundSupport, RecorderConfig.Default());
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

        private static OrbitFrame ReadOrbitFrame(IStaticBody body, double epochZero)
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
            frame.PositionAtEpochZero = ToVec(orbit.GetSample(epochZero, true, false).position);
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
            double time = CraftTime;
            _simulation.Clock.Time = time;
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

        /// <summary>Game time of the craft: the start of its next step (equal to the world time once the craft has caught up in the fixed tick).</summary>
        private double CraftTime => Online ? (_net.Link.HasCraft ? _net.Link.Time : TimeProvider.WorldTime) : TimeProvider.TimeOfStep(_nextStep);

        // ------------------------------------------------------------------ online (stage 2c.6)

        /// <summary>Environment variable that overrides the net mode (online / offline): automated player runs without touching the scene.</summary>
        public const string EnvNetMode = "ORBITAL_NET_MODE";
        /// <summary>Environment variable: 1 = send a spawn by itself when the account has no craft (automated player runs).</summary>
        public const string EnvAutoSpawn = "ORBITAL_AUTOSPAWN";
        /// <summary>Environment variable: seconds of full thrust with SAS right after the spawn, then the engine is cut (automated player runs).</summary>
        public const string EnvAutoBurn = "ORBITAL_AUTOBURN";
        private bool _autoSpawn;
        private bool _autoSpawnSent;
        private double _autoBurn;
        private double _autoBurnStart = double.NaN;

        private CraftNetMode EffectiveNetMode()
        {
            string v = Environment.GetEnvironmentVariable(EnvNetMode);
            if (string.Equals(v, "online", StringComparison.OrdinalIgnoreCase)) return CraftNetMode.Online;
            if (string.Equals(v, "offline", StringComparison.OrdinalIgnoreCase)) return CraftNetMode.Offline;
            return netMode;
        }

        private void StartOnline()
        {
            ClientSettings settings = ClientSettingsStore.Load();
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ClientSettings.EnvUrl)) && !string.IsNullOrWhiteSpace(serverUrl))
                settings.ServerUrl = serverUrl.Trim();
            _net = new CraftNetLink(settings, callsign, OnlineParams);
            _net.Link.HardStep = TimeProvider.StepSeconds;
            Application.runInBackground = true; // the server time goes on while the window is not focused
            TimeProvider.SetExternalTime(true);
            _autoSpawn = Environment.GetEnvironmentVariable(EnvAutoSpawn) == "1";
            double.TryParse(Environment.GetEnvironmentVariable(EnvAutoBurn), NumberStyles.Float, CultureInfo.InvariantCulture, out _autoBurn);
            _net.Connect();
            Debug.Log($"CraftSpawner: online, server {settings.ServerUrl}, callsign {_net.Session.Callsign}", this);
        }

        /// <summary>Flight parameters of a server craft: the body of the start point (as offline) with the craft config from the server snapshot.</summary>
        private FlightParams OnlineParams(CraftSnapshotDto snapshot)
        {
            FlightParams p = _flightParams;
            p.Craft = StateMapper.FlightConfigOf(snapshot, _flight);
            return p;
        }

        /// <summary>Enter online: a spawn only without a craft or after its destruction (while flying it would abandon the craft on the server).</summary>
        private void OnlineSpawn()
        {
            NetSession s = _net.Session;
            if (s.State == SessionState.NoCraft || _net.Destroyed != null) s.Spawn(string.IsNullOrEmpty(startPointId) ? null : startPointId);
            else if (_net.Link.HasCraft && _net.Link.State.Mode == FlightMode.Crashed) Debug.Log("CraftSpawner: the crash is not confirmed by the server yet", this);
        }

        // test hook (ORBITAL_AUTOBURN): full thrust with SAS for the given seconds after the first step, then the engine cut, SAS stays on
        private void AutoBurn(ref ControlInput c, double time)
        {
            if (double.IsNaN(_autoBurnStart)) _autoBurnStart = time;
            c.SasOn = true;
            if (time - _autoBurnStart < _autoBurn) c.ThrottleFull = true;
            else c.ThrottleZero = true;
        }

        private void OnlineFixedUpdate()
        {
            double real = Time.realtimeSinceStartupAsDouble;
            _net.Update(real);
            NetSession s = _net.Session;
            double target = s.ServerTime(real);
            if (!double.IsNaN(target)) TimeProvider.Discipline(target, s.TimeScale, _discipline);
            OwnCraftLink link = _net.Link;
            if (_autoSpawn && !_autoSpawnSent && s.State == SessionState.NoCraft)
            {
                _autoSpawnSent = true;
                OnlineSpawn();
            }
            if (!link.HasCraft) return;
            double dt = TimeProvider.StepSeconds;
            double world = TimeProvider.WorldTime;
            if (world - link.Time > 5.0)
            {
                _net.RequestHardSync(); // far behind (a jump of the clock): the server state carries the craft to the present
                return;
            }
            if (link.Time + dt * 0.5 > world) return;
            KeyState keys = _input.Consume();
            int guard = 0;
            while (link.Time + dt * 0.5 <= world && guard++ < TimeProvider.MaxStepsPerTick)
            {
                _lastControl = InputMapper.Map(keys, ref _latch);
                if (_autoBurn > 0) AutoBurn(ref _lastControl, link.Time);
                link.Step(_lastControl, dt, real);
                if (groundSupport) LandedSupport.HoldUpright(ref link.State, _surface);
            }
            _net.Update(real); // send what the steps recorded
        }

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
            ControlInput control = Online ? _lastControl : _simulation.Control;
            StepReportComponent report = Online ? default : _simulation.Report;
            ThrustRecorderComponent recorder = _simulation.Recorder;
            CultureInfo c = CultureInfo.InvariantCulture;
            StringBuilder sb = _text;
            sb.Clear();
            sb.Append(callsign).Append(" @ ").Append(_bodyTitle).Append("    t = ").Append(CraftTime.ToString("F0", c)).Append(" s\n");
            sb.Append("Mode: ").Append(ModeName(state.Mode));
            if (state.Mode == FlightMode.Crashed && !Online) sb.Append("  (impact ").Append(report.LastImpactSpeed.ToString("F1", c)).Append(" m/s, limit ").Append(_flightParams.MaxLandingSpeed.ToString("F1", c)).Append(")");
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
            if (Online)
            {
                OwnCraftLink l = _net.Link;
                sb.Append("Sent: ").Append(l.SegmentsSent).Append(" segments, ").Append(l.AttitudesSent).Append(" attitudes; corrections soft ")
                    .Append(l.Soft).Append(", hard ").Append(l.Hard);
            }
            else sb.Append("Thrust segments: ").Append(recorder.SentCount).Append(" (queued ").Append(recorder.State.Count).Append(", dropped ").Append(recorder.State.Overflowed)
                .Append(", ").Append(recorder.State.Active ? "burning" : "idle").Append(")");

            _hud.Telemetry = sb.ToString();
            _hud.Throttle = (float) state.Throttle;
            _hud.MainFuel = (float) t.MainFuelFraction;
            _hud.RcsFuel = (float) t.RcsFuelFraction;
            _hud.Gyro = (float) t.GyroFraction;
            _hud.RcsOn = control.RcsOn;
            _hud.Banner = state.Mode == FlightMode.Crashed ? "CRASHED - press Enter" : string.Empty;
            _hud.TimeScaleInfo = TimeScaleText(c, _measuredRate);
            if (Online) UpdateOnlineHud(state, c);
        }

        private void UpdateOnlineHud(in FlightState state, CultureInfo c)
        {
            NetSession s = _net.Session;
            double serverTime = s.ServerTime(Time.realtimeSinceStartupAsDouble);
            _hud.Others = _ghosts != null ? _ghosts.ListText : string.Empty;
            _hud.NetLine = _net.StatusLine() + "   server t " + (double.IsNaN(serverTime) ? "-" : serverTime.ToString("F1", c));
            if (_net.Destroyed != null) _hud.Banner = "DESTROYED (" + _net.Destroyed.Reason + ") - press Enter for a new craft";
            else if (s.State == SessionState.NoCraft) _hud.Banner = "No craft - press Enter to spawn";
            else if (s.State == SessionState.Refused) _hud.Banner = "Server refused: " + s.LastError;
            else if (s.State != SessionState.Ready) _hud.Banner = "Connecting... (" + s.State + ")";
            else if (state.Mode == FlightMode.Crashed) _hud.Banner = "CRASHED - awaiting server";
            else if (!s.HasControl) _hud.Banner = "Another session has the control";
            // the scale slider only for an admin session: it asks the server, the server pushes the new scale to everyone
            bool admin = s.IsAdmin && timeScaleSlider;
            _hud.ShowTimeScale = admin;
            _hud.RequestTimeScale = admin ? (Action<double>) (v => s.SetTimeScale(v)) : null;
        }

        // Real rate of the game time (game seconds per real second) over about one second: shows what the scale really gives when the cap
        // or the frame rate (the engine drops fixed ticks after a long frame) holds it back.
        private void MeasureRate()
        {
            float now = Time.unscaledTime;
            if (_rateRealStart < 0f)
            {
                _rateRealStart = now;
                _rateGameStart = TimeProvider.WorldTime;
                return;
            }
            float span = now - _rateRealStart;
            if (span < 1f) return;
            _measuredRate = (TimeProvider.WorldTime - _rateGameStart) / span;
            _rateRealStart = now;
            _rateGameStart = TimeProvider.WorldTime;
        }

        private static string TimeScaleText(CultureInfo c, double measuredRate)
        {
            string text = "Time x" + ScaleText(TimeProvider.TimeScale, c) + "   real x" + ScaleText(measuredRate, c);
            if (TimeProvider.TimeScaleLimited) text += "   LIMITED to x" + ScaleText(TimeProvider.EffectiveTimeScale, c) + " (" + TimeProvider.MaxStepsPerTick + " steps/tick)";
            return text;
        }

        private static string ScaleText(double value, CultureInfo c) => value >= 10.0 ? value.ToString("F0", c) : value.ToString("0.##", c);

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
