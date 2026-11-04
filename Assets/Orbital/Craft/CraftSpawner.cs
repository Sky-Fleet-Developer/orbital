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
using Orbital.Core.Worlds;
using Orbital.Technology;
using Orbital.Technology.Components;
using Orbital.View.Flight;
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

        [Header("Shared configs (empty = read Assets/_game/Config in the Editor)")]
        [SerializeField] private TextAsset flightConfigJson;
        [SerializeField] private TextAsset fuelsJson;
        [SerializeField] private TextAsset craftSpecJson;
        [SerializeField] private TextAsset startPointsJson;

        [Header("Settings")]
        [Tooltip("Online or offline, server, callsign (Assets/_game/Settings/NetworkSettings.asset).")]
        [SerializeField] private NetworkSettings networkSettings;
        [Tooltip("Craft model and look (Assets/_game/Settings/CraftSettings.asset).")]
        [SerializeField] private CraftSettings craftSettings;
        [Tooltip("The container with the flight view settings: camera, sky, lighting, ground (Assets/_game/ViewSettings/ViewSettingsContainer.asset, its nested assets).")]
        [SerializeField] private ScriptableObjectContainer.Container viewSettings;
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
        // stage 3.4: the world's body table (null for the scene world) and the body the craft starts on
        private BodyFrameTable _frames;
        private int _spawnParent = -1;
        private string _spawnBodyId;
        private int _presentedBody = -1; // the body the presenter is configured for
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
        // stage 4: the map and the forecast of the own craft
        private FlightMapView _map;
        private TrajectoryForecaster _forecast;
        private float _nextForecast;
        private bool _forecastReported;
        private float _nextAdvice;
        private LandingAdvice _advice;
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
            if (networkSettings == null)
            {
                Debug.LogWarning("CraftSpawner: no NetworkSettings, using the default values", this);
                networkSettings = ScriptableObject.CreateInstance<NetworkSettings>();
            }
            if (craftSettings == null)
            {
                Debug.LogWarning("CraftSpawner: no CraftSettings, using the default values", this);
                craftSettings = ScriptableObject.CreateInstance<CraftSettings>();
            }
            LoadConfig();
            ReadAutomationEnv();
            if (!Setup()) return;
            _input = new CraftInput();
            if (EffectiveNetMode() == CraftNetMode.Online) StartOnline();
            else Respawn();
            _presenter = new CraftPresenter(new FlightViewSettings(viewSettings), craftSettings.vehiclePrefab, craftSettings.greyboxMaterialTemplate, _surface, (float) _flight.CgHeight, MakePads(),
                _frames, _spawnParent, _frames != null ? BodyLook.Read(_frames, _world.BodyData, BodyNameOf) : null);
            _presentedBody = _spawnParent;
            ApplyDebugView();
            if (_frames != null)
            {
                FlightViewSettings view = new FlightViewSettings(viewSettings);
                _forecast = new TrajectoryForecaster(_frames);
                _map = new FlightMapView(_frames, BodyLook.Read(_frames, _world.BodyData, BodyNameOf), BodyNameOf, view.Map, transform, _presenter.CameraDepth);
                _presenter.OwnMap = true;
            }
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
            ApplyDebugMap();
            HandlesRegister.RegisterHandlers(this, _container);
            _registered = true;
            _ready = true;
        }

        void IUpdateHandler.Update()
        {
            if (!_ready) return;
            _input.Poll();
            if (_input.ToggleViewPressed) SetMapOpen(_presenter.FlightViewActive);
            if (_input.ToggleCameraPressed) _presenter.SetHorizonCamera(!_presenter.HorizonCamera);
            if (_map != null && _map.Active) _map.Control(_input.CameraDrag, _input.CameraZoom, _input.ClickPressed && !HudPanels.Covers(_input.MousePosition), _input.FocusCraftPressed);
            else _presenter.ControlCamera(_input.CameraDrag, _input.CameraZoom);
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
                double stepTime = TimeProvider.TimeOfStep(_nextStep);
                if (_autoBurn > 0) ApplyAutoBurnToKeys(ref keys, stepTime);
                if (_hasAutoKeys) keys = ControlKeys.Or(keys, _autoKeys);
                int parentBefore = _simulation.Parent;
                _simulation.SetKeys(keys);
                _simulation.Clock.Time = stepTime;
                _simulation.Step();
                _nextStep++;
                LogSoiChange(parentBefore, _simulation.Parent, TimeProvider.TimeOfStep(_nextStep));
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
                    PresentBody(_net.Link.Parent);
                    _presenter.Present(shown, CraftTime);
                    _net.AdvanceOthers(CraftTime);
                    _ghosts?.Present(shown.Position, _net.Link.Parent, _net.Link.ParentId, _net.Others, _frames, CraftTime);
                    _ghosts?.PresentWrecks(shown.Position, _net.Link.ParentId, _net.Wrecks, _net.Link.Params.Surface, CraftTime);
                }
                else state = default;
            }
            else
            {
                PresentBody(_simulation.Parent);
                _presenter.Present(state, CraftTime);
            }
            if (showHud && (!Online || _net.Link.HasCraft))
            {
                SurfaceData surface = Online ? _net.Link.Params.Surface : _simulation.Params.Surface;
                _navBall.Update(state, surface, _navOrbit);
            }
            PresentMap(state);
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
            _map?.Dispose();
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
            bool bodyOverride = !string.IsNullOrWhiteSpace(_spawnBodyOverride); // ORBITAL_SPAWN_BODY, offline only (ReadAutomationEnv)
            string craftId = craftSettings.craftId;
            _orbitAltitude = orbitAltitudeMeters;
            _orbitInclination = inclinationDegrees * DegToRad;
            _orbitPhase = phaseDegrees * DegToRad;
            if ((spawnKind == CraftSpawnKind.StartPoint || _startPointOverride != null) && !bodyOverride)
            {
                StartPointDef point = _config.FindStartPoint(StartPointId);
                if (point == null)
                {
                    Debug.LogError($"CraftSpawner: no start point '{StartPointId}' (and no default one) in startpoints.json", this);
                    return false;
                }
                if (!point.IsSurface && !point.IsSpace)
                {
                    Debug.LogError($"CraftSpawner: start point '{point.Id}' is of the unknown kind '{point.Kind}'", this);
                    return false;
                }
                bodyKey = point.BodyId;
                latitude = point.Latitude;
                longitude = point.Longitude;
                altitude = 0;
                orbit = point.IsSpace;
                if (orbit)
                {
                    _orbitAltitude = point.AltitudeMeters;
                    _orbitInclination = point.InclinationDeg * DegToRad;
                    _orbitPhase = point.PhaseDeg * DegToRad;
                }
                if (!string.IsNullOrEmpty(point.CraftId)) craftId = point.CraftId;
            }
            else if (bodyOverride)
            {
                bodyKey = _spawnBodyOverride; // a point of the chosen body's ground (the surface kind), coordinates from the inspector fields
                orbit = _spawnOrbitAltitude > 0; // ORBITAL_SPAWN_ALT: a circular orbit around it instead
                if (orbit) _orbitAltitude = _spawnOrbitAltitude;
                else if (_spawnHeight > 0) altitude = _spawnHeight;
            }

            _flight = _config.ResolveFlight(craftId);
            _frames = _world.Frames;
            if (_frames != null)
            {
                // stage 3.4: the body comes from the world's table (the same numbers the server reads from world.json), not from the scene
                string id = WorldFileLoader.IdOf(bodyKey);
                int index = _frames.IndexOf(id);
                if (index < 0)
                {
                    Debug.LogError($"CraftSpawner: no body '{bodyKey}' (id '{id}') in the body table of the world", this);
                    return false;
                }
                ref readonly BodyFrame b = ref _frames[index];
                if (b.Virtual || b.Radius <= 0)
                {
                    Debug.LogError($"CraftSpawner: body '{bodyKey}' has no surface (it is virtual or has no radius)", this);
                    return false;
                }
                _spawnParent = index;
                _spawnBodyId = id;
                _bodyTitle = BodyNameOf(id);
                _surface = b.Surface;
                _flightParams = new FlightParams
                {
                    Craft = _flight,
                    Surface = _surface,
                    Nu = b.Nu,
                    MaxLandingSpeed = _config.MaxLandingSpeed,
                };
            }
            else
            {
                // the scene world (no shared file): the old path through the scene's StaticBody
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
                _spawnParent = -1;
                _spawnBodyId = null;
                _bodyTitle = BodyTable.KeyOf(found.name);
                _surface = SurfaceBuilder.Build(body.Radius, ReadRotation(found), ReadOrbitFrame(body, _world.EpochZero), _world.EpochZero);
                _flightParams = new FlightParams
                {
                    Craft = _flight,
                    Surface = _surface,
                    Nu = body.GravParameter,
                    MaxLandingSpeed = _config.MaxLandingSpeed,
                };
            }
            _isOrbit = orbit;
            _spawnLatitude = latitude * DegToRad;
            _spawnLongitude = longitude * DegToRad;
            _spawnAltitude = altitude;

            TimeProvider.SetMaxStepsPerTick(maxStepsPerTick);
            _nextStep = TimeProvider.StepCount;
            _simulation = new CraftSimulation(TimeProvider.TimeOfStep(_nextStep), TimeProvider.StepSeconds, craftSettings.groundSupport, RecorderConfig.Default());
            _simulation.Clock.Frames = _frames; // stage 3.4: the sphere of influence rule of the offline flight
            return true;
        }

        private string BodyNameOf(string id)
        {
            WorldFileDef definition = _world.Definition;
            if (definition?.Bodies != null)
            {
                foreach (WorldBodyDef body in definition.Bodies)
                {
                    if (body != null && body.Id == id && !string.IsNullOrEmpty(body.Name)) return body.Name;
                }
            }
            return id;
        }

        private string TitleOf(int parent)
        {
            return _frames != null && parent >= 0 ? BodyNameOf(_frames.IdOf(parent)) : _bodyTitle;
        }

        private int CurrentParent => Online ? _net.Link.Parent : _simulation.Parent;

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
                return SpawnBuilder.CircularOrbit(_flight, _surface, _flightParams.Nu, _orbitAltitude, _orbitInclination, _orbitPhase);
            }
            return SpawnBuilder.OnSurface(_flight, _surface, _spawnLatitude, _spawnLongitude, _spawnAltitude, time);
        }

        /// <summary>A fresh craft (full tanks) at the chosen point; the game time goes on.</summary>
        private void Respawn()
        {
            double time = CraftTime;
            _simulation.Clock.Time = time;
            FlightState state = MakeInitialState(time);
            _simulation.CreatePlayerCraft(state, _flightParams, networkSettings.callsign, _spawnParent);
        }

        // The pads the view can show: every surface start point of the world's bodies, and the spawn place itself when it is on the ground.
        private StartPad[] MakePads()
        {
            System.Collections.Generic.List<StartPad> pads = new System.Collections.Generic.List<StartPad>();
            if (!_isOrbit) pads.Add(new StartPad {Body = _spawnParent, Latitude = _spawnLatitude, Longitude = _spawnLongitude});
            if (_frames != null && _config.StartPoints?.StartPoints != null)
            {
                foreach (StartPointDef point in _config.StartPoints.StartPoints)
                {
                    int body = _frames.IndexOf(point.BodyId);
                    if (point.IsSurface && body >= 0) pads.Add(new StartPad {Body = body, Latitude = point.Latitude * DegToRad, Longitude = point.Longitude * DegToRad});
                }
            }
            return pads.ToArray();
        }

        private string StartPointId => _startPointOverride ?? startPointId;

        /// <summary>Game time of the craft: the start of its next step (equal to the world time once the craft has caught up in the fixed tick).</summary>
        private double CraftTime => Online ? (_net.Link.HasCraft ? _net.Link.Time : TimeProvider.WorldTime) : TimeProvider.TimeOfStep(_nextStep);

        // ------------------------------------------------------------------ online (stage 2c.6)

        /// <summary>Environment variable that overrides the net mode (online / offline): automated player runs without touching the scene.</summary>
        public const string EnvNetMode = "ORBITAL_NET_MODE";
        /// <summary>Environment variable: 1 = send a spawn by itself when the account has no craft (automated player runs).</summary>
        public const string EnvAutoSpawn = "ORBITAL_AUTOSPAWN";
        /// <summary>Environment variable: seconds of full thrust with SAS right after the spawn, then the engine is cut (automated player runs).</summary>
        public const string EnvAutoBurn = "ORBITAL_AUTOBURN";
        /// <summary>Environment variable (stage 3.4): offline spawn on this body (e.g. Phobos), forced to the Surface kind with the inspector coordinates.</summary>
        public const string EnvSpawnBody = "ORBITAL_SPAWN_BODY";
        private bool _autoSpawn;
        private bool _autoSpawnSent;
        private double _autoBurn;
        private double _autoBurnStart = double.NaN;
        private string _spawnBodyOverride;
        /// <summary>Environment variable (stage 3.6): with ORBITAL_SPAWN_BODY, a circular orbit at this altitude, meters, instead of the surface.</summary>
        public const string EnvSpawnAlt = "ORBITAL_SPAWN_ALT";
        /// <summary>Environment variable (stage 3.6): the camera looks at this body (table id, e.g. mars), for screenshots of the sky.</summary>
        public const string EnvViewBody = "ORBITAL_VIEW_BODY";
        /// <summary>Environment variable (stage 3.6): vertical field of view of the camera, degrees (a narrow one checks the point/disk rule).</summary>
        public const string EnvViewFov = "ORBITAL_VIEW_FOV";
        /// <summary>Environment variable (stage 3.7): start point id instead of the inspector's, also with another spawn kind.</summary>
        public const string EnvStartPoint = "ORBITAL_START_POINT";
        private string _startPointOverride;
        /// <summary>Environment variable (stage 4): 1 = the map is open from the start (screenshots).</summary>
        public const string EnvMap = "ORBITAL_MAP";
        /// <summary>Environment variable (stage 4): the map looks at this body (table id) instead of the craft.</summary>
        public const string EnvMapFocus = "ORBITAL_MAP_FOCUS";
        /// <summary>Environment variable (stage 4): the map's view distance, meters.</summary>
        public const string EnvMapDistance = "ORBITAL_MAP_DISTANCE";
        /// <summary>Environment variable (stage 4): the map's cursor is here, "x,y" in pixels from the bottom left (the information window on a screenshot).</summary>
        public const string EnvMapMouse = "ORBITAL_MAP_MOUSE";
        /// <summary>Environment variable (stage 4.1): with ORBITAL_SPAWN_BODY, the height above the ground, meters: the craft falls from there.</summary>
        public const string EnvSpawnHeight = "ORBITAL_SPAWN_HEIGHT";
        /// <summary>Environment variable (controls check): these keys are held all the time, letters of ControlKey separated by commas, e.g. "R,E" (offline).</summary>
        public const string EnvAutoKeys = "ORBITAL_AUTOKEYS";
        /// <summary>Environment variable (controls check): 1 = the level orbit camera from the start, it shows the craft turning.</summary>
        public const string EnvViewHorizon = "ORBITAL_VIEW_HORIZON";
        /// <summary>Environment variable (panels check): these panels start folded, for this run only: net, flight, nav, others, descent, help, time, map.</summary>
        public const string EnvHudCollapse = "ORBITAL_HUD_COLLAPSE";
        private KeyState _autoKeys;
        private bool _hasAutoKeys;
        private Vector2? _mouseOverride;
        private double _spawnHeight;
        private readonly StringBuilder _descentText = new StringBuilder(256);
        private double _spawnOrbitAltitude;
        private double _orbitAltitude;
        private double _orbitInclination;
        private double _orbitPhase;

        private void ApplyDebugMap()
        {
            if (_map == null || Environment.GetEnvironmentVariable(EnvMap) != "1") return;
            CultureInfo c = CultureInfo.InvariantCulture;
            SetMapOpen(true);
            string focus = Environment.GetEnvironmentVariable(EnvMapFocus);
            int body = !string.IsNullOrWhiteSpace(focus) ? _frames.IndexOf(focus.Trim()) : -1;
            if (body >= 0) _map.FocusBody(body);
            if (double.TryParse(Environment.GetEnvironmentVariable(EnvMapDistance), NumberStyles.Float, c, out double distance) && distance > 0) _map.SetDistance(distance);
            string[] mouse = (Environment.GetEnvironmentVariable(EnvMapMouse) ?? string.Empty).Split(',');
            if (mouse.Length == 2 && float.TryParse(mouse[0], NumberStyles.Float, c, out float x) && float.TryParse(mouse[1], NumberStyles.Float, c, out float y)) _mouseOverride = new Vector2(x, y);
        }

        private void ApplyDebugView()
        {
            string body = Environment.GetEnvironmentVariable(EnvViewBody);
            float.TryParse(Environment.GetEnvironmentVariable(EnvViewFov), NumberStyles.Float, CultureInfo.InvariantCulture, out float fov);
            int index = _frames != null && !string.IsNullOrWhiteSpace(body) ? _frames.IndexOf(body.Trim()) : -1;
            if (index >= 0 || fov > 0f) _presenter.SetDebugAim(index, fov);
            if (Environment.GetEnvironmentVariable(EnvViewHorizon) == "1") _presenter.SetHorizonCamera(true);
        }

        private CraftNetMode EffectiveNetMode()
        {
            string v = Environment.GetEnvironmentVariable(EnvNetMode);
            if (string.Equals(v, "online", StringComparison.OrdinalIgnoreCase)) return CraftNetMode.Online;
            if (string.Equals(v, "offline", StringComparison.OrdinalIgnoreCase)) return CraftNetMode.Offline;
            return networkSettings.mode;
        }

        /// <summary>Automated player runs without touching the scene (ORBITAL_AUTOSPAWN, ORBITAL_AUTOBURN, ORBITAL_SPAWN_BODY).</summary>
        private void ReadAutomationEnv()
        {
            _autoSpawn = Environment.GetEnvironmentVariable(EnvAutoSpawn) == "1";
            double.TryParse(Environment.GetEnvironmentVariable(EnvAutoBurn), NumberStyles.Float, CultureInfo.InvariantCulture, out _autoBurn);
            double.TryParse(Environment.GetEnvironmentVariable(EnvSpawnAlt), NumberStyles.Float, CultureInfo.InvariantCulture, out _spawnOrbitAltitude);
            double.TryParse(Environment.GetEnvironmentVariable(EnvSpawnHeight), NumberStyles.Float, CultureInfo.InvariantCulture, out _spawnHeight);
            foreach (string name in (Environment.GetEnvironmentVariable(EnvHudCollapse) ?? string.Empty).Split(','))
            {
                if (!string.IsNullOrWhiteSpace(name)) HudPanels.SetCollapsed(name.Trim(), true, false);
            }
            foreach (string name in (Environment.GetEnvironmentVariable(EnvAutoKeys) ?? string.Empty).Split(','))
            {
                if (!Enum.TryParse(name.Trim(), out ControlKey key)) continue;
                ControlKeys.Set(ref _autoKeys, key, true);
                _hasAutoKeys = true;
            }
            string startPoint = Environment.GetEnvironmentVariable(EnvStartPoint);
            if (!string.IsNullOrWhiteSpace(startPoint)) _startPointOverride = startPoint.Trim();
            string spawnBody = Environment.GetEnvironmentVariable(EnvSpawnBody);
            if (!string.IsNullOrWhiteSpace(spawnBody) && EffectiveNetMode() != CraftNetMode.Online)
            {
                _spawnBodyOverride = spawnBody.Trim();
                Debug.Log($"CraftSpawner: ORBITAL_SPAWN_BODY: offline spawn on '{_spawnBodyOverride}'", this);
            }
        }

        private void StartOnline()
        {
            ClientSettings settings = ClientSettingsStore.Load();
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ClientSettings.EnvUrl)) && !string.IsNullOrWhiteSpace(networkSettings.serverUrl))
                settings.ServerUrl = networkSettings.serverUrl.Trim();
            _net = new CraftNetLink(settings, networkSettings.callsign, OnlineParams, _frames);
            _net.Link.HardStep = TimeProvider.StepSeconds;
            Application.runInBackground = true; // the server time goes on while the window is not focused
            TimeProvider.SetExternalTime(true);
            _net.Connect();
            Debug.Log($"CraftSpawner: online, server {settings.ServerUrl}, callsign {_net.Session.Callsign}", this);
        }

        /// <summary>Flight parameters of a server craft: the body by the server's ParentId from the world's table (stage 3.4), the craft config from
        /// the server snapshot; an unknown id falls back to the body of the start point as before.</summary>
        private FlightParams OnlineParams(CraftSnapshotDto snapshot)
        {
            FlightParams p = _flightParams;
            if (_frames != null) _frames.ApplyTo(snapshot.ParentId, ref p);
            p.Craft = StateMapper.FlightConfigOf(snapshot, _flight);
            return p;
        }

        /// <summary>Enter online: a spawn only without a craft or after its destruction (while flying it would abandon the craft on the server).</summary>
        private void OnlineSpawn()
        {
            NetSession s = _net.Session;
            if (s.State == SessionState.NoCraft || _net.Destroyed != null || _net.CrashConfirmed)
            {
                // a crash the server has confirmed: the spawn abandons the wreck at once (no wait for craft_destroyed, stage 2c.8)
                if (_net.CrashConfirmed) Debug.Log("CraftSpawner: new craft now, the server has the old one crashed (wreck kept)", this);
                s.Spawn(string.IsNullOrEmpty(StartPointId) ? null : StartPointId);
            }
            else if (_net.Link.HasCraft && _net.Link.State.Mode == FlightMode.Crashed) Debug.Log("CraftSpawner: the crash is not confirmed by the server yet", this);
        }

        // test hook (ORBITAL_AUTOBURN): full thrust with SAS for the given seconds after the first step, then the engine cut, SAS stays on
        private bool AutoBurnActive(double time)
        {
            if (double.IsNaN(_autoBurnStart)) _autoBurnStart = time;
            return time - _autoBurnStart < _autoBurn;
        }

        private void AutoBurn(ref ControlInput c, double time)
        {
            c.SasOn = true;
            if (AutoBurnActive(time)) c.ThrottleFull = true;
            else c.ThrottleZero = true;
        }

        // the same hook offline: X = full throttle, T = the SAS switch (its front edge turns the SAS on at the first step), Z = cut
        private void ApplyAutoBurnToKeys(ref KeyState keys, double time)
        {
            keys.T = true;
            if (AutoBurnActive(time)) keys.X = true;
            else keys.Z = true;
        }

        private void SetMapOpen(bool open)
        {
            _presenter.SetFlightView(!open);
            _map?.SetActive(open);
            _nextForecast = 0f;
        }

        // Stage 4: the forecast runs on a worker, often while the map is open and rarely for the HUD line alone. The map gets what is ready.
        private void PresentMap(in FlightState state)
        {
            if (_forecast == null) return;
            _forecast.Poll();
            if (_forecast.LastError != null && !_forecastReported)
            {
                _forecastReported = true;
                Debug.LogError("CraftSpawner: the trajectory forecast failed: " + _forecast.LastError, this);
            }
            bool exists = !Online || _net.Link.HasCraft;
            int parent = CurrentParent;
            if (!exists || state.Mode != FlightMode.Flying || parent < 0) _forecast.Clear();
            else if (Time.unscaledTime >= _nextForecast)
            {
                bool open = _map != null && _map.Active;
                if (_forecast.Request(parent, state.Position, state.Velocity, CraftTime, open ? _map.FrameBodyFor(parent) : parent))
                    _nextForecast = Time.unscaledTime + (open ? 0.1f : 0.5f);
            }
            if (_map == null || !_map.Active) return;
            FlightParams parameters = Online ? _net.Link.Params : _simulation.Params;
            MapCraft craft = new MapCraft
            {
                Exists = exists && parent >= 0,
                Callsign = Online ? _net.Session.Callsign : networkSettings.callsign,
                Parent = parent,
                Position = state.Position,
                Velocity = state.Velocity,
                Mode = state.Mode,
                Altitude = Surface.Altitude(parameters.Surface, state.Position),
            };
            Vector2 mouse = _mouseOverride ?? _input.MousePosition;
            if (HudPanels.Covers(mouse)) mouse = new Vector2(-10000f, -10000f); // nothing of the map under a panel
            _map.Present(CraftTime, craft, _forecast.Latest, Online ? _net.Others : null, mouse);
        }

        // What ends the current conic, from the forecast. The search is off the main thread now, the countdown is every refresh of the HUD.
        private void AppendSoiHint(StringBuilder sb, in FlightState state, CultureInfo c)
        {
            if (_forecast == null || state.Mode != FlightMode.Flying) return;
            TrajectoryPlot plot = _forecast.Latest;
            if (plot.Count == 0 || plot.Parent != CurrentParent) return;
            ref readonly TrajectorySegment first = ref plot.Segments[0];
            if (first.NextBody < 0) return;
            double left = first.End - CraftTime;
            if (left < 0.0) return;
            sb.Append("Next SOI: ").Append(TitleOf(first.NextBody)).Append(" in ").Append(FlightMapView.Duration(left)).Append(" (coasting)\n");
        }

        // The descent instrument (stage 4.1). A few times a second: a tiny fraction of a frame, see Stage4Tests.
        private void UpdateDescent(in FlightState state, in FlightParams parameters, CultureInfo c)
        {
            if (Time.unscaledTime >= _nextAdvice)
            {
                _nextAdvice = Time.unscaledTime + 0.2f;
                double exit = _frames != null && CurrentParent >= 0 ? _frames[CurrentParent].SoiRadius : double.PositiveInfinity;
                _advice = LandingAdvisor.Compute(state, parameters, exit);
            }
            if (!_advice.Falling)
            {
                _hud.Descent = string.Empty;
                return;
            }
            LandingAdvice a = _advice;
            string call;
            Color color;
            if (!a.CanStop && a.OutOfFuel)
            {
                call = "NOT ENOUGH FUEL";
                color = new Color(1f, 0.25f, 0.2f);
            }
            else if (!a.CanStop)
            {
                call = "TOO LATE";
                color = new Color(1f, 0.25f, 0.2f);
            }
            else if (a.BurnIn <= 0.0)
            {
                call = "BURN NOW";
                color = new Color(1f, 0.55f, 0.1f);
            }
            else
            {
                call = "BURN IN " + FlightMapView.Duration(a.BurnIn);
                color = a.BurnIn < 10.0 ? new Color(1f, 0.85f, 0.2f) : new Color(0.4f, 1f, 0.6f);
            }
            _hud.DescentCall = call;
            _hud.DescentColor = color;
            StringBuilder sb = _descentText;
            sb.Clear();
            sb.Append("Impact in ").Append(FlightMapView.Duration(a.TimeToImpact)).Append(" at ").Append(FlightMapView.Speed(a.ImpactSpeed)).Append('\n');
            sb.Append("Burn lasts ").Append(FlightMapView.Duration(a.BurnTime)).Append(", fuel ").Append(a.FuelNeeded.ToString("F0", c)).Append(" kg\n");
            if (a.CanStop) sb.Append("Full burn now stops at ").Append(FlightMapView.Length(a.StopHeightNow)).Append('\n');
            else sb.Append("Full burn now: ground at ").Append(FlightMapView.Speed(a.SpeedAtGroundNow)).Append('\n');
            sb.Append("Nose off retrograde: ").Append(a.RetrogradeError.ToString("F0", c)).Append(" deg\n");
            sb.Append("Landing limit ").Append(FlightMapView.Speed(parameters.MaxLandingSpeed)).Append(", plan stops at ").Append(LandingAdvisor.TargetHeight.ToString("F0", c)).Append(" m");
            _hud.Descent = sb.ToString();
        }

        /// <summary>One log line on every sphere of influence change of the own craft (stage 3.4, the smoke scenario greps for it).</summary>
        private void LogSoiChange(int before, int after, double time)
        {
            if (_frames == null || before == after) return;
            string from = before >= 0 ? _frames.IdOf(before) : "-";
            string to = after >= 0 ? _frames.IdOf(after) : "-";
            Debug.Log($"SOI: {from} -> {to} at t={time.ToString("F2", CultureInfo.InvariantCulture)}", this);
            _bodyTitle = TitleOf(after);
        }

        /// <summary>Reconfigures the presenter when the craft moved to another body (stage 3.4): the markers only on the start body.</summary>
        private void PresentBody(int parent)
        {
            if (_frames == null || parent < 0 || parent == _presentedBody) return;
            _presenter.SetBody(parent);
            _presentedBody = parent;
            _bodyTitle = TitleOf(parent);
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
                int parentBefore = link.Parent;
                link.Step(_lastControl, dt, real);
                LogSoiChange(parentBefore, link.Parent, link.Time);
                if (craftSettings.groundSupport) LandedSupport.HoldUpright(ref link.State, link.Params.Surface);
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
            "M or C       flight view / map view\n" +
            "F            map: follow the craft\n" +
            "V            camera behind / orbit (level)\n" +
            "RMB drag     orbit the camera, wheel zoom\n" +
            "F1           fold this help; + / - fold the panels\n" +
            "The gyro is always on; RCS needs R.\n" +
            "SAS holds the attitude; with RCS off it uses\n" +
            "the gyro only and gives up when it saturates.";

        private void UpdateHud(in FlightState state)
        {
            FlightParams parameters = Online ? _net.Link.Params : _simulation.Params; // the current body, not the start one (stage 3.4)
            FlightTelemetry t = FlightTelemetry.Compute(state, parameters);
            ControlInput control = Online ? _lastControl : _simulation.Control;
            StepReportComponent report = Online ? default : _simulation.Report;
            ThrustRecorderComponent recorder = _simulation.Recorder;
            CultureInfo c = CultureInfo.InvariantCulture;
            StringBuilder sb = _text;
            sb.Clear();
            sb.Append(Online ? _net.Session.Callsign : networkSettings.callsign).Append(" @ ").Append(_bodyTitle);
            if (_frames != null && CurrentParent >= 0)
            {
                double soi = _frames[CurrentParent].SoiRadius;
                sb.Append("    SOI ").Append(double.IsInfinity(soi) ? "-" : Distance(soi, c));
            }
            sb.Append("    t = ").Append(CraftTime.ToString("F0", c)).Append(" s\n");
            AppendSoiHint(sb, state, c);
            sb.Append("Mode: ").Append(ModeName(state.Mode));
            if (state.Mode == FlightMode.Crashed && !Online) sb.Append("  (impact ").Append(report.LastImpactSpeed.ToString("F1", c)).Append(" m/s, limit ").Append(parameters.MaxLandingSpeed.ToString("F1", c)).Append(")");
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
            UpdateDescent(state, parameters, c);
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
            if (s.AdminRefusal != null)
                _hud.NetLine += s.AdminRefusal == "admin_disabled"
                    ? "   admin off: serverconfig.json admin.enabled = true, restart"
                    : "   admin: refused (" + s.AdminRefusal + ")";
            if (_net.Destroyed != null) _hud.Banner = "DESTROYED (" + _net.Destroyed.Reason + ") - press Enter for a new craft";
            else if (s.State == SessionState.NoCraft) _hud.Banner = "No craft - press Enter to spawn";
            else if (s.State == SessionState.Refused) _hud.Banner = "Server refused: " + s.LastError;
            else if (s.State != SessionState.Ready) _hud.Banner = "Connecting... (" + s.State + ")";
            else if (_net.CrashConfirmed) _hud.Banner = "CRASHED (confirmed by the server) - press Enter for a new craft now";
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
