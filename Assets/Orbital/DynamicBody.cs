using System;
using Ara3D;
using Orbital.Core;
using Orbital.Core.Navigation;
using Orbital.Core.Simulation;
using Orbital.Core.TrajectorySystem;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Assertions;

namespace Orbital
{
    public class DynamicBody : SystemComponent<DynamicBodyVariables, DynamicBodySettings>, IDynamicBody, IHierarchyElement
    {
        [SerializeField] private DynamicBodyVariables variables;
        [SerializeField] private DynamicBodySettings settings;
        private World _world;
        //private Track _trajectoryTrack;
        [ShowInInspector] private NavigationPath _path;
        private double _massInv;

        #region InterfaceImplementation
        Transform IHierarchyElement.Transform => transform;
        string IHierarchyElement.Name => name;
        int IHierarchyElement.ParentId => transform.parent.gameObject.GetInstanceID();
        public DVector3 LocalPosition => Orbit.GetPositionAtT(TimeService.WorldTime);
        public Vector3 LocalEulerAngles => Vector3.zero;
        public StaticOrbit Orbit => _path.GetOrbitAtTime(TimeService.WorldTime);
        public event Action OrbitChangedHandler;
        public IStaticBody ParentCelestial => _path.GetParentAtTime(TimeService.WorldTime);
        public OrbitEnding Ending => _path.GetEndingAtTime(TimeService.WorldTime);
        public double Mass => settings.mass;
        public double MassInv => _massInv;
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
            _massInv = 1 / settings.mass;
            Assert.IsFalse(double.IsNaN(_massInv));
            _path = new NavigationPath();
            _path.Calculate(GetComponentInParent<IStaticBody>(), variables.position, variables.velocity, TimeService.WorldTime);
            _path.BuildTransitions(TimeService.WorldTime);
            _world.RegisterRigidBody(this);
        }

        public void AddForce(Vector3 force)
        {
            var orbit = Orbit;
            orbit.GetOrbitalStateVectorsAtOrbitTime(TimeService.WorldTime, out DVector3 pos, out DVector3 vel);
            vel += (DVector3)force * _massInv;
            _path.Calculate(ParentCelestial, pos, vel, TimeService.WorldTime);
            OrbitChangedHandler?.Invoke();
            variables.position = pos;
            variables.velocity = vel;
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
        public double mass;
    }
}