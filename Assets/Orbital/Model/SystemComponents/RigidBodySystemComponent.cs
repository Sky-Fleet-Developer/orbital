using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ara3D;
using Orbital.Model.Handles;
using Orbital.Model.Simulation;
using Orbital.Model.TrajectorySystem;
using Sirenix.OdinInspector;
using UnityEngine;
using Zenject;

namespace Orbital.Model.SystemComponents
{
    public sealed class RigidBodySystemComponent : SystemComponent<RigidBodyVariables, RigidBodySettings>, IRigidBody, ITrajectorySettingsHolder
    {
        [SerializeField] private RigidBodyVariables variables;
        [SerializeField] private RigidBodySettings settings;
        private MassSystemComponent _parent;
        private RelativeTrajectory _trajectory;
        private RigidbodyPresentation _presentation;
        private Observer _observer;
        [Inject] private World _world;
        [Inject] private ObserverService _observerService;
        private bool _isSleep;
        private RigidBodyMode _mode;
        public bool IsSleep => _isSleep;
        public RigidBodyMode Mode => _mode;
        public event Action<RigidBodyMode> ModeChangedHandler;
        public MassSystemComponent Parent => _parent;
        public RelativeTrajectory Trajectory => _trajectory;
        public DVector3 LocalPosition => Trajectory.GetPosition(TimeService.WorldTime);
        public DVector3 LocalVelocity => Trajectory.GetVelocity(TimeService.WorldTime);

        private float _awakeTime = 0;
        private const float AwakeDelay = 1;

        public override RigidBodyVariables Variables
        {
            get => variables;
        }

        TrajectorySettings ITrajectorySettingsHolder.Settings
        {
            get => variables.trajectorySettings;
            set => variables.trajectorySettings = value;
        }

        public override RigidBodySettings Settings
        {
            get => settings;
            set => settings = value;
        }

        protected override void Start()
        {
            base.Start();
            _parent = GetComponentInParent<MassSystemComponent>();
            _world.RegisterRigidBody(this, out _trajectory);
            _trajectory.Calculate();
        }
        
        public void AwakeFromSleep()
        {
            if (_mode == RigidBodyMode.Trajectory) throw new Exception("Can't accelerate in Trajectory mode!");
            _isSleep = false;
            _mode = RigidBodyMode.Simulation;
            if (IsSleepTimerInCondition())
            {
                StartSleepTimer();
            }
            else
            {
                _awakeTime = Time.realtimeSinceStartup;
            }
            ModeChangedHandler?.Invoke(_mode);
        }

        private async void StartSleepTimer()
        {
            _awakeTime = Time.realtimeSinceStartup;

            do
            {
                await Task.Delay((int) (_awakeTime + AwakeDelay - Time.realtimeSinceStartup) * 998);
            } while (!IsSleepTimerInCondition());
        }

        private bool IsSleepTimerInCondition()
        {
            return _awakeTime + AwakeDelay < Time.realtimeSinceStartup;
        }

        private async void Sleep()
        {
            if (_mode == RigidBodyMode.Trajectory) throw new Exception("Can't sleep in Trajectory mode!");
            _isSleep = true;
            _mode = RigidBodyMode.Sleep;
            await PrepareForSleep();
            ModeChangedHandler?.Invoke(_mode);
        }

        public void Present(Observer observer)
        {
            if (_mode != RigidBodyMode.Trajectory) throw new Exception("Already presents!");
            _isSleep = true;
            _mode = RigidBodyMode.Sleep;

            _observer = observer;
            DVector3 origin = observer.Position;
            DVector3 originVelocity = observer.Velocity;
            Vector3 localPosition = LocalPosition - origin;
            
            _presentation = Instantiate(Settings.presentation, observer.Root);
            _presentation.Init(this, observer);
            _presentation.Position = localPosition;
            _presentation.Rotation = Quaternion.identity;
            _presentation.Velocity = LocalVelocity - originVelocity;

            ModeChangedHandler?.Invoke(_mode);
        }

        public async void RemovePresent()
        {
            if (_mode == RigidBodyMode.Trajectory) throw new Exception("Present is not exists!");
            if (!_isSleep)
            {
                await PrepareForSleep();
            }
            _isSleep = false;
            _mode = RigidBodyMode.Trajectory;

            Destroy(_presentation.gameObject);
            
            ModeChangedHandler?.Invoke(_mode);
        }

        private async Task PrepareForSleep()
        {
            await SetupTrajectoryFromPresentation();
            Trajectory.Calculate();
        }
        
        public async Task SetupTrajectoryFromPresentation()
        {
            DVector3 position = (DVector3)(_presentation.Position) + _observer.Position;
            DVector3 velocity = (DVector3) (_presentation.Velocity) + _observer.Velocity;
            await _trajectory.SetupFromSimulation(position, velocity);
            TrajectorySettings settingsToUpdate = variables.trajectorySettings;
            _trajectory.UpdateSettings(ref settingsToUpdate);
            variables.trajectorySettings = settingsToUpdate;
            _trajectory.Calculate();
        }
    }

    [Serializable]
    public struct RigidBodySettings
    {
        public RigidbodyPresentation presentation;
    }

    [Serializable]
    public struct RigidBodyVariables
    {
        public TrajectorySettings trajectorySettings;
      //  public SimulationVariables simulationVariables;
    }

    [Serializable]
    public struct SimulationVariables
    {
        public Vector3 velocity;
        public Vector3 position;
    }
    

}
