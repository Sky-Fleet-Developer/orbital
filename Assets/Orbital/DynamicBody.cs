using System;
using Ara3D;
using Orbital.Core.TrajectorySystem;
using Orbital.Navigation;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Orbital.Core.Simulation
{
    public partial class DynamicBody : SystemComponent<DynamicBodyVariables, DynamicBodySettings>, IDynamicBody
    {
        [SerializeField] private DynamicBodyVariables variables;
        [SerializeField] private DynamicBodySettings settings;
        private World _world;
        //private Track _trajectoryTrack;
        private NavigationPath _path;

        #region InterfaceImplementation
        public IStaticOrbit Orbit => _path.GetOrbitAtTime(TimeService.WorldTime);
        public IStaticBody Parent => _path.GetParentAtTime(TimeService.WorldTime);
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
            _world = GetComponentInParent<World>();
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                _world.Load();
            }
            #endif
            
            _path = new NavigationPath();
            _path.Calculate(GetComponentInParent<IStaticBody>(), variables.position, variables.velocity, TimeService.WorldTime);
            _path.BuildTransitions(0);
            _world.RegisterRigidBody(this);
        }

        private void OnValidate()
        {
            Init();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _path.Dispose();
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