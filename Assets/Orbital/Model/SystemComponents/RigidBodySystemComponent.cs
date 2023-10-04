using System;
using System.Collections.Generic;
using Ara3D;
using Orbital.Model.Handles;
using Orbital.Model.Services;
using Orbital.Model.TrajectorySystem;
using UnityEngine;
using Zenject;

namespace Orbital.Model.SystemComponents
{
    public class RigidBodySystemComponent : SystemComponent<RigidBodyVariables, RigidBodySettings>, ITrajectorySettingsHolder, IFixedUpdateHandler
    {
        [SerializeField] private RigidBodyVariables variables;
        private MassSystemComponent _parent;
        private RelativeTrajectory _trajectory;
        private RigidBodyMode _mode = RigidBodyMode.Trajectory;
        [Inject] private World _world;
        [Inject] private TimeService _timeService;

        public MassSystemComponent Parent => _parent;
        public RelativeTrajectory Trajectory => _trajectory;
        public RigidBodyMode Mode => _mode;
        public DVector3 LocalPosition => variables.localPosition;

        public override RigidBodyVariables Variables
        {
            get => variables;
        }

        TrajectorySettings ITrajectorySettingsHolder.Settings
        {
            get => variables.trajectorySettings;
            set => variables.trajectorySettings = value;
        }
        
        protected override void Start()
        {
            base.Start();
            _parent = GetComponentInParent<MassSystemComponent>();
            _world.RegisterRigidBody(this, out _trajectory);
            _trajectory.Calculate();
        }

        public void Simulate()
        {
            
        }

        void IFixedUpdateHandler.FixedUpdate()
        {
            variables.localPosition = _trajectory?.GetPosition(_timeService.WorldTime) ?? DVector3.Zero;
        }
    }

    public enum RigidBodyMode
    {
        Trajectory = 0,
        Simulation = 1
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
        public SimulationVariables simulationVariables;
        public DVector3 localPosition;
    }

    [Serializable]
    public struct SimulationVariables
    {
        public Vector3 velocity;
        public Vector3 position;
    }
}
