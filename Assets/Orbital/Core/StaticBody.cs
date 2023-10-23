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
    public class StaticBody : SystemComponent<StaticBodyVariables, StaticBodySettings>, IFixedUpdateHandler, IStaticBody, IStaticBodyAccessor
    {
        private StaticBodySettings _settings;
        [ShowInInspector] private StaticBodyVariables _variables;
        [Inject] private World _world;
        private IMassSystem _massSystem;
        private IStaticTrajectory _trajectory;
        private IStaticBody _parent;

        IMassSystem IStaticBody.MassSystem => _massSystem;

        IStaticBody IStaticBody.Parent => _parent;
        IStaticBody IStaticBodyAccessor.Self => this;

        IMassSystem IStaticBodyAccessor.MassSystem
        {
            get => _massSystem;
            set => _massSystem = value;
        }
        IStaticBody IStaticBodyAccessor.Parent
        {
            get => _parent;
            set => _parent = value;
        }
        IStaticTrajectory IStaticBodyAccessor.Trajectory
        {
            get => _trajectory;
            set => _trajectory = value;
        }

        //public DVector3 Position => _world.GetGlobalPosition(this);
        public DVector3 LocalPosition => _trajectory.GetPositionAtT(TimeService.WorldTime);
        
        public double Mass => _massSystem.Mass;
        public IStaticTrajectory Trajectory => _trajectory;
        
        public override StaticBodySettings Settings
        {
            get => _settings;
            set => _settings = value;
        }

        public override StaticBodyVariables Variables
        {
            get => _variables;
            set => _variables = value;
        }

        void IFixedUpdateHandler.FixedUpdate()
        {
            _variables.localPosition = _trajectory?.GetSample(TimeService.WorldTime, true, false).position ?? DVector3.Zero;
        }
    }

    [Serializable]
    public struct StaticBodyVariables
    {
        public DVector3 localPosition;
    }

    [Serializable]
    public struct StaticBodySettings
    {
    }
}