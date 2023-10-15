using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Ara3D;
using Orbital.Model.Handles;
using Orbital.Model.SystemComponents;
using Orbital.Model.TrajectorySystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace Orbital.Model.Simulation
{
    public class Observer : SystemComponent<ObserverVariables, ObserverSettings>, IUpdateByFrequencyHandler, IObserverTriggerHandler
    {
        [SerializeField] private ObserverVariables variables;
        [SerializeField] private ObserverSettings settings;
        [SerializeField] private float maxDistanceToAnchor;
        private IRigidBody _anchor;
        [Inject] private ObserverService _observerService;
        [Inject] private DiContainer _diContainer;
        private RuntimeTrajectory _trajectory;
        private double _maxDistanceToAnchorSqr;
        
        public Transform Root => _observerService.GetRootFor(this);
        public override ObserverVariables Variables
        {
            get => variables;
            set => variables = value;
        }

        public override ObserverSettings Settings
        {
            get => settings;
            set => settings = value;
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
            }
        }

        public MassSystemComponent Parent => _anchor.Parent;

        private void Awake()
        {
            _maxDistanceToAnchorSqr = maxDistanceToAnchor * maxDistanceToAnchor;
        }

        protected override void Start()
        {
            base.Start();
            _trajectory = new RuntimeTrajectory();
            _diContainer.Inject(_trajectory);
            InitDelayed();
        }

        private async void InitDelayed()
        {
            await Task.Yield();
            Anchor = GetComponentInParent<IRigidBody>();
            _observerService.RegisterObserver(this);
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
            var sample = _anchor.Trajectory.GetSample(TimeService.WorldTime);
            _trajectory.Place(sample.position, sample.velocity);
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
        
        int IOrderHolder.Order => 1;
        UpdateFrequency IUpdateByFrequencyHandler.Frequency => UpdateFrequency.Every10Frame;

        void IUpdateByFrequencyHandler.Update()
        {
            double distSqr = (_trajectory.Position - _anchor.Trajectory.GetSample(TimeService.WorldTime).position).LengthSquared();
            if (distSqr > _maxDistanceToAnchorSqr)
            {
                RefreshAnchorPosition();
            }
        }
        
    }
    [Serializable]
    public struct ObserverSettings
    {
    }

    [Serializable]
    public struct ObserverVariables
    {
    }
}