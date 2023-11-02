using System;
using Ara3D;
using Orbital.Core.TrajectorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Orbital.Core.Simulation
{
    public partial class DynamicBody : SystemComponent<DynamicBodyVariables, DynamicBodySettings>, IDynamicBody, IDynamicBodyAccessor
    {
        private static AsyncThreadScheduler _trajectoryRefreshScheduler = new AsyncThreadScheduler(3);

        [SerializeField] private DynamicBodyVariables variables;
        [SerializeField] private DynamicBodySettings settings;
        private World _world;
        //private Track _trajectoryTrack;
        private IStaticOrbit _orbit;
        private IStaticBody _parent;

        private DynamicBodyMode _mode = DynamicBodyMode.Trajectory;

        #region InterfaceImplementation
        public IStaticOrbit Orbit => _orbit;
        public IStaticBody Parent => _parent;
        [ShowInInspector] public DynamicBodyMode Mode => _mode;
        IDynamicBody IDynamicBodyAccessor.Self => this;
        IStaticBody IDynamicBodyAccessor.Parent
        {
            get => _parent;
            set => _parent = value;
        }
        IStaticOrbit IDynamicBodyAccessor.Orbit
        {
            get => _orbit;
            set => _orbit = value;
        }
        #endregion

        public override DynamicBodyVariables Variables
        {
            get => variables;
            set => variables = value;
        }
        public override DynamicBodySettings Settings
        {
            get => settings;
            set => settings = value;
        }
        
        protected override void Start()
        {
            base.Start();
            Init();
        }

        public void Init()
        {
            if(_parent != null && _world != null) return;
            _parent = GetComponentInParent<IStaticBody>();
            _world = GetComponentInParent<World>();
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                _world.Load();
            }
            #endif
            _world.RegisterRigidBody(this);
            _orbit.Calculate(variables.position, variables.velocity, TimeService.WorldTime);
        }

        private void OnValidate()
        {
            if(_orbit == null) return;
            Init();
        }
    }

    [Serializable]
    public struct DynamicBodyVariables
    {
        public DVector3 velocity;
        public DVector3 position;
    }

    [Serializable]
    public struct DynamicBodySettings
    {
    }
}