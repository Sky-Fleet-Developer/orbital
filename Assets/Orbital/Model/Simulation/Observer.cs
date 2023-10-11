using System;
using Ara3D;
using Orbital.Model.Handles;
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
        private RigidBodySystemComponent _anchor;
        private RelativeTrajectory _trajectory;
        [Inject] private ObserverService _observerService;
        public Transform Root => _observerService.GetRootFor(this);
        public override ObserverVariables Variables
        {
            get => variables;
            set => variables = value;
        }

        public RelativeTrajectory Trajectory => _trajectory;

        public RigidBodySystemComponent Anchor
        {
            get => _anchor;
            set
            {
                if(_anchor != null) _anchor.ModeChangedHandler -= OnAnchorModeChanged;
                _anchor = value;
                _anchor.ModeChangedHandler += OnAnchorModeChanged;
                CloneAnchorTrajectory();
                Debug.LogError("Has no logic for anchor change");
            }
        }

        public MassSystemComponent Parent => _anchor.Parent;

        protected override void Start()
        {
            base.Start();
            Anchor = GetComponentInParent<RigidBodySystemComponent>();
            _observerService.RegisterObserver(this);
        }

        private void OnAnchorModeChanged(RigidBodyMode mode)
        {
            if (mode != RigidBodyMode.Simulation)
            {
                CloneAnchorTrajectory();
            }
        }

        private void CloneAnchorTrajectory()
        {
            _trajectory = _anchor.Trajectory.Clone();
        }

        protected override void OnDestroy()
        {
            _anchor.ModeChangedHandler -= OnAnchorModeChanged;
            _observerService.UnregisterObserver(this);
            base.OnDestroy();
        }

        public void RegisterComplete(Scene scene)
        {
        }

        int IOrderHolder.Order => 1;
        public DVector3 LocalPosition => _trajectory.GetPosition(TimeService.WorldTime);
        public DVector3 LocalVelocity => _trajectory.GetVelocity(TimeService.WorldTime);

        void IFixedUpdateHandler.FixedUpdate()
        {
        }

        void IObserverTriggerHandler.OnRigidbodyEnter(RigidBodySystemComponent component, Observer observer)
        {
            if (observer == this)
            {
                
            }
        }
        
        void IObserverTriggerHandler.OnRigidbodyExit(RigidBodySystemComponent component, Observer observer)
        {
            
        }
    }

    public struct ObserverSettings
    {
    }

    [Serializable]
    public struct ObserverVariables
    {
    }
}