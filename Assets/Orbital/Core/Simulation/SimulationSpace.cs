using System;
using System.Threading.Tasks;
using Ara3D;
using Orbital.Core.Handles;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace Orbital.Core.Simulation
{
    public class SimulationSpace : SystemComponent<SimulationSpaceVariables, SimulationSpaceSettings>, IUpdateByFrequencyHandler
    {
        [SerializeField] private SimulationSpaceVariables variables;
        [SerializeField] private SimulationSpaceSettings settings;
        [SerializeField] private float maxDistanceToAnchor;
        private IDynamicBody _anchor;
        [Inject] private SimulationService _simulationService;
        [Inject] private DiContainer _diContainer;
        private RuntimeTrajectory _trajectory;
        private double _maxDistanceToAnchorSqr;
        
        public Transform Root => _simulationService.GetRootFor(this);
        public override SimulationSpaceVariables Variables
        {
            get => variables;
            set => variables = value;
        }

        public override SimulationSpaceSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        public IDynamicBody Anchor
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

        public IStaticBody Parent => _anchor.Parent;

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
            _simulationService.RegisterSimulation(this);
            Anchor = GetComponentInParent<IDynamicBody>();
        }

        private void OnAnchorModeChanged(DynamicBodyMode mode)
        {

        }

        protected override void OnDestroy()
        {
            _anchor.ModeChangedHandler -= OnAnchorModeChanged;
            _simulationService.UnregisterSimulation(this);
            base.OnDestroy();
        }

        public void RegisterComplete(Scene scene)
        {
        }

        private void RefreshAnchorPosition()
        {
            Debug.Log("Refresh anchor position");
            var sample = _anchor.TrajectorySampler.GetSample(TimeService.WorldTime);
            DVector3 deltaPosition = sample.position - _trajectory.Position;
            DVector3 deltaVelocity = sample.velocity - _trajectory.Velocity;
            _simulationService.SimulationWasMoved(this, deltaPosition, deltaVelocity);
            _trajectory.Place(sample.position, sample.velocity);
        }
        
        public DVector3 Position => _trajectory.Position;
        public DVector3 Velocity => _trajectory.Velocity;

        public (DVector3 position, DVector3 velocity) SampleTrajectory(double time)
        {
            return _trajectory.GetSample(time);
        }
        
        int IOrderHolder.Order => -1;
        UpdateFrequency IUpdateByFrequencyHandler.Frequency => UpdateFrequency.Every10Frame;

        void IUpdateByFrequencyHandler.Update()
        {
            double distSqr = (_trajectory.Position - _anchor.TrajectorySampler.GetSample(TimeService.WorldTime).position).LengthSquared();
            if (distSqr > _maxDistanceToAnchorSqr)
            {
                RefreshAnchorPosition();
            }
        }
        
    }
    [Serializable]
    public struct SimulationSpaceSettings
    {
    }

    [Serializable]
    public struct SimulationSpaceVariables
    {
    }
}