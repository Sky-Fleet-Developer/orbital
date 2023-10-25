using System;
using System.Threading.Tasks;
using Ara3D;
using Orbital.Core.TrajectorySystem;
using Sirenix.OdinInspector;
using UnityEngine;
using Zenject;

namespace Orbital.Core.Simulation
{
    public partial class DynamicBody : SystemComponent<DynamicBodyVariables, DynamicBodySettings>, IDynamicBody, IDynamicBodyAccessor
    {
        private static AsyncThreadScheduler _trajectoryRefreshScheduler = new AsyncThreadScheduler(3);

        [SerializeField] private DynamicBodyVariables variables;
        [SerializeField] private DynamicBodySettings settings;
        private World _world;
        //private Track _trajectoryTrack;
        private IStaticTrajectory _trajectory;
        private IStaticBody _parent;

        private DynamicBodyMode _mode = DynamicBodyMode.Trajectory;

        #region InterfaceImplementation
        public IStaticTrajectory Trajectory => _trajectory;
        public IStaticBody Parent => _parent;
        [ShowInInspector] public DynamicBodyMode Mode => _mode;
        IDynamicBody IDynamicBodyAccessor.Self => this;
        IStaticBody IDynamicBodyAccessor.Parent
        {
            get => _parent;
            set => _parent = value;
        }
        IStaticTrajectory IDynamicBodyAccessor.Trajectory
        {
            get => _trajectory;
            set => _trajectory = value;
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
            _trajectory.Calculate(variables.position, variables.velocity, TimeService.WorldTime);
        }

        private void OnValidate()
        {
            if(_trajectory == null) return;
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