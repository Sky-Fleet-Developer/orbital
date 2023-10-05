using System;
using System.Collections.Generic;
using Ara3D;
using Orbital.Model.Handles;
using Orbital.Model.Simulation;
using Orbital.Model.TrajectorySystem;
using UnityEngine;
using Zenject;

namespace Orbital.Model.SystemComponents
{
    public class RigidBodySystemComponent : SystemComponent<RigidBodyVariables, RigidBodySettings>, ITrajectorySettingsHolder
    {
        [SerializeField] private RigidBodyVariables variables;
        [SerializeField] private RigidBodySettings settings;
        private MassSystemComponent _parent;
        private RelativeTrajectory _trajectory;
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

        public void Present()
        {
            if (_mode != RigidBodyMode.Trajectory) throw new Exception("Already presents!");
            _isSleep = true;
            _mode = RigidBodyMode.Sleep;
            ModeChangedHandler?.Invoke(_mode);
        }

        public void RemovePresent()
        {
            if (_mode == RigidBodyMode.Trajectory) throw new Exception("Present is not exists!");
            _isSleep = false;
            _mode = RigidBodyMode.Trajectory;
            ModeChangedHandler?.Invoke(_mode);
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
