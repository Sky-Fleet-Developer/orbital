using System;
using System.Collections.Generic;
using Ara3D;
using Orbital.Model.Handles;
using Orbital.Model.Simulation;
using Orbital.Model.TrajectorySystem;
using Sirenix.OdinInspector;
using UnityEngine;
using Zenject;

namespace Orbital.Model.SystemComponents
{
    public sealed class RigidBodySystemComponent : SystemComponent<RigidBodyVariables, RigidBodySettings>, ITrajectorySettingsHolder
    {
        [SerializeField] private RigidBodyVariables variables;
        [SerializeField] private RigidBodySettings settings;
        private MassSystemComponent _parent;
        private RelativeTrajectory _trajectory;
        private Rigidbody _presentation;
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
            ModeChangedHandler?.Invoke(_mode);
        }

        public void Sleep()
        {
            if (_mode == RigidBodyMode.Trajectory) throw new Exception("Can't sleep in Trajectory mode!");
            _isSleep = true;
            _mode = RigidBodyMode.Sleep;
            ModeChangedHandler?.Invoke(_mode);
        }

        public void Present(Observer observer)
        {
            if (_mode != RigidBodyMode.Trajectory) throw new Exception("Already presents!");
            _isSleep = true;
            _mode = RigidBodyMode.Sleep;

            _observer = observer;
            DVector3 origin = observer.LocalPosition;
            DVector3 originVelocity = observer.LocalVelocity;
            Vector3 localPosition = LocalPosition - origin;
            
            _presentation = Instantiate(Settings.dynamicPresentation, observer.Root);
            _presentation.transform.localPosition = localPosition;
            _presentation.transform.localRotation = Quaternion.identity;
            _presentation.velocity = LocalVelocity - originVelocity;

            ModeChangedHandler?.Invoke(_mode);
        }

        public void RemovePresent()
        {
            if (_mode == RigidBodyMode.Trajectory) throw new Exception("Present is not exists!");
            _isSleep = false;
            _mode = RigidBodyMode.Trajectory;

            SetupTrajectoryFromPresentation();
            Trajectory.Calculate();
            
            Destroy(_presentation.gameObject);
            
            ModeChangedHandler?.Invoke(_mode);
        }
        [Button]
        private async void SetupTrajectoryFromPresentation()
        {
            DVector3 position = (DVector3)(_presentation.transform.localPosition) + _observer.LocalPosition;
            DVector3 velocity = (DVector3) (_presentation.velocity) + _observer.LocalVelocity;
            await _trajectory.SetupFromSimulation(position, velocity);
            TrajectorySettings settingsToUpdate = variables.trajectorySettings;
            _trajectory.UpdateSettings(ref settingsToUpdate);
            variables.trajectorySettings = settingsToUpdate;
        }
    }

    [Serializable]
    public struct RigidBodySettings
    {
        public Rigidbody dynamicPresentation;
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
    
    public enum RigidBodyMode
    {
        Trajectory = 0,
        Sleep = 1,
        Simulation = 2
    }
}
