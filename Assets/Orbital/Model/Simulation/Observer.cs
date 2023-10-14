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
        private IRigidBody _anchor;
        [Inject] private ObserverService _observerService;
        [Inject] private DiContainer _diContainer;
        private RuntimeTrajectory _trajectory;
        public Transform Root => _observerService.GetRootFor(this);
        public override ObserverVariables Variables
        {
            get => variables;
            set => variables = value;
        }

        public IRigidBody Anchor
        {
            get => _anchor;
            set
            {
                if (_anchor != null)
                {
                    _anchor.ModeChangedHandler -= OnAnchorModeChanged;
                    if (_anchor.Parent != value.Parent) _trajectory.Attach(value.Parent);
                }
                else
                {
                    _trajectory.Attach(value.Parent);
                }
                _anchor = value;
                _anchor.ModeChangedHandler += OnAnchorModeChanged;
                RefreshAnchorPosition();
                Debug.LogError("Has no logic for anchor change");
            }
        }

        public MassSystemComponent Parent => _anchor.Parent;

        protected override void Start()
        {
            base.Start();
            Anchor = GetComponentInParent<IRigidBody>();
            _observerService.RegisterObserver(this);
            _trajectory.Inject(_diContainer);
        }

        private void OnAnchorModeChanged(RigidBodyMode mode)
        {

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

        private void RefreshAnchorPosition()
        {
            _trajectory.Place(_anchor.LocalPosition, _anchor.LocalVelocity);
        }

        int IOrderHolder.Order => 1;

        void IFixedUpdateHandler.FixedUpdate()
        {
        }

        void IObserverTriggerHandler.OnRigidbodyEnter(IRigidBody component, Observer observer)
        {
        }

        void IObserverTriggerHandler.OnRigidbodyExit(IRigidBody component, Observer observer)
        {
        }

        public DVector3 Position => _trajectory.Position;
        public DVector3 Velocity => _trajectory.Velocity;

        public (DVector3 position, DVector3 velocity) SampleTrajectory(double time)
        {
            return _trajectory.GetSample(time);
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