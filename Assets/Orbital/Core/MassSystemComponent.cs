using System;
using Ara3D;
using Orbital.Core.Handles;
using Orbital.Core.TrajectorySystem;
using Sirenix.OdinInspector;
using UnityEngine;
using Zenject;

namespace Orbital.Core
{
    [ExecuteInEditMode]
    public class MassSystemComponent : SystemComponent<CelestialVariables, CelestialSettings>, IFixedUpdateHandler
    {
        private CelestialSettings _settings;
        [ShowInInspector] private CelestialVariables _variables;
        [Inject] private World _world;
        private IMassSystem _massSystem;
        private RelativeTrajectory _trajectory;

        public double Mass => _massSystem.Mass;
        public RelativeTrajectory Trajectory => _trajectory;
        
        public override CelestialSettings Settings
        {
            get => _settings;
            set => _settings = value;
        }

        public override CelestialVariables Variables
        {
            get => _variables;
            set => _variables = value;
        }

        public void Setup(IMassSystem massSystem, RelativeTrajectory trajectory)
        {
            _massSystem = massSystem;
            _trajectory = trajectory;
        }

        void IFixedUpdateHandler.FixedUpdate()
        {
            _variables.localPosition = _trajectory?.GetPosition(TimeService.WorldTime) ?? DVector3.Zero;
        }
    }

    [Serializable]
    public struct CelestialVariables
    {
        public DVector3 localPosition;
    }

    [Serializable]
    public struct CelestialSettings
    {
    }
}