using System;
using Ara3D;
using Orbital.Model.Handles;
using Orbital.Model.Services;
using Orbital.Model.SystemComponents;
using Orbital.Model.TrajectorySystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace Orbital.Model.Simulation
{
    public class Observer : SystemComponent<ObserverVariables, ObserverSettings>, IFixedUpdateHandler, IObserverTriggerHandler
    {
        [SerializeField] private ObserverVariables variables;
        private Scene _scene;
        private RigidBodySystemComponent _anchor;
        [Inject] private ObserverService _observerService;

        public override ObserverVariables Variables
        {
            get => variables;
            set => variables = value;
        }

        public RigidBodySystemComponent Anchor
        {
            get => _anchor;
            set
            {
                _anchor = value;
                Debug.LogError("Has no logic for anchor change");
            }
        }

        public MassSystemComponent Parent => _anchor.Parent;

        protected override void Start()
        {
            base.Start();
            _anchor = GetComponentInParent<RigidBodySystemComponent>();
            _observerService.RegisterObserver(this);
        }

        public void RegisterComplete(Scene scene)
        {
            _scene = scene;
        }

        int IOrderHolder.Order => 1;
        public DVector3 LocalPosition => _anchor.LocalPosition;

        void IFixedUpdateHandler.FixedUpdate()
        {
            variables.relativePosition = _anchor.LocalPosition;
        }

        void IObserverTriggerHandler.OnRigidbodyEnter(RigidBodySystemComponent component)
        {
            
        }
        
        void IObserverTriggerHandler.OnRigidbodyExit(RigidBodySystemComponent component)
        {
            
        }
    }

    public struct ObserverSettings
    {
    }

    [Serializable]
    public struct ObserverVariables
    {
        public DVector3 relativePosition;
    }
}