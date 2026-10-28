using System;
using Leopotam.Ecs;
#if UNITY_EDITOR
using Leopotam.Ecs.UnityIntegration;
#endif
using Orbital.Core.Flight;
using Orbital.Technology.Components;
using Orbital.Technology.Systems;
using UnityEngine;

namespace Orbital.Technology
{
    /// <summary>
    /// The ecs world of the craft simulation: one player craft entity with the flight components and the systems that step it
    /// (input -> kernel step -> ground support -> thrust recorder). Plain class: the owner calls <see cref="Step"/> from its fixed update,
    /// so there is no per-object Update. The Editor shows the world in the ecs observers like <see cref="EcsAdapter"/> does.
    /// </summary>
    public sealed class CraftSimulation : IDisposable
    {
        private readonly EcsWorld _world;
        private readonly EcsSystems _systems;
        private EcsEntity _player;
        private bool _hasPlayer;

        public FlightClock Clock { get; }

        public bool HasPlayer => _hasPlayer && _player.IsAlive();

        public CraftSimulation(double startTime, double dt, bool groundSupport, RecorderConfig recorder)
        {
            Clock = new FlightClock { Time = startTime, Dt = dt, GroundSupport = groundSupport, Recorder = recorder };
            _world = new EcsWorld();
#if UNITY_EDITOR
            EcsWorldObserver.Create(_world);
#endif
            _systems = new EcsSystems(_world, "Craft simulation");
            _systems
                .Add(new FlightInputSystem())
                .Add(new FlightStepSystem())
                .Add(new LandedSupportSystem())
                .Add(new ThrustRecorderSystem())
                .Inject(Clock);
            _systems.Init();
#if UNITY_EDITOR
            EcsSystemsObserver.Create(_systems);
#endif
        }

        /// <summary>Creates the player craft (replacing the previous one) in the given state and returns nothing: read it through <see cref="State"/>.</summary>
        public void CreatePlayerCraft(in FlightState state, in FlightParams flightParams, string callsign)
        {
            DestroyPlayer();
            _player = _world.NewEntity();
            _player.Get<FlightStateComponent>().State = state;
            _player.Get<FlightParamsComponent>().Params = flightParams;
            _player.Get<KeyStateComponent>();
            _player.Get<InputLatchComponent>();
            _player.Get<ControlInputComponent>();
            _player.Get<StepReportComponent>();
            _player.Get<ThrustRecorderComponent>();
            _player.Get<PlayerControlled>().Callsign = callsign;
            _hasPlayer = true;
        }

        public void DestroyPlayer()
        {
            if (_hasPlayer && _player.IsAlive()) _player.Destroy();
            _hasPlayer = false;
        }

        /// <summary>Keys for the next step. Do nothing when there is no craft.</summary>
        public void SetKeys(in KeyState keys)
        {
            if (!HasPlayer) return;
            _player.Get<KeyStateComponent>().Keys = keys;
        }

        /// <summary>Runs all systems once for the current <see cref="FlightClock.Time"/> and then advances the clock by its Dt.</summary>
        public void Step()
        {
            _systems.Run();
            Clock.Time += Clock.Dt;
        }

        public FlightState State => HasPlayer ? _player.Get<FlightStateComponent>().State : default;

        public FlightParams Params => HasPlayer ? _player.Get<FlightParamsComponent>().Params : default;

        public ControlInput Control => HasPlayer ? _player.Get<ControlInputComponent>().Input : default;

        public StepReportComponent Report => HasPlayer ? _player.Get<StepReportComponent>() : default;

        public ThrustRecorderComponent Recorder => HasPlayer ? _player.Get<ThrustRecorderComponent>() : default;

        public void Dispose()
        {
            try
            {
                DestroyPlayer();
                _systems.Destroy();
                _world.Destroy();
            }
            catch (Exception e)
            {
                // The editor observers of the ecs may already be gone when the scene is torn down.
                Debug.LogWarning($"CraftSimulation: error on dispose: {e.Message}");
            }
        }
    }
}
