using Ara3D;
using Orbital.Core.Handles;
using Orbital.Core.TrajectorySystem;
using Zenject;

namespace Orbital.Core.Simulation
{
    public class RuntimeTrajectory : IFixedUpdateHandler, ITrajectorySampler
    {
        private MassSystemComponent _parent;
        private double _nu;
        private bool _isReadyToWork = false;
        private DVector3 _position;
        private DVector3 _velocity;
        private DiContainer _diContainer;
        private double _lastUpdateTime;

        public DVector3 Position => _position;
        public DVector3 Velocity => _velocity;

        [Inject]
        public void Inject(DiContainer container)
        {
            _diContainer = container;
        }

        public void Attach(MassSystemComponent parent)
        {
            _parent = parent;
            _nu = parent.Mass * MassUtility.G;
            if (_isReadyToWork)
            {
                HandlesRegister.UnregisterHandlers(this, _diContainer);
                _isReadyToWork = false;
            }
        }

        public void Place(DVector3 position, DVector3 velocity)
        {
            _position = position;
            _velocity = velocity;
            if (!_isReadyToWork)
            {
                HandlesRegister.RegisterHandlers(this, _diContainer);
                _isReadyToWork = true;
            }
        }

        void IFixedUpdateHandler.FixedUpdate()
        {
            (_position, _velocity) = GetSample(TimeService.WorldTime);
            _lastUpdateTime = TimeService.WorldTime;
        }

        public (DVector3 position, DVector3 velocity) GetSample(double time)
        {
            double deltaTime = time - _lastUpdateTime;
            double r = _position.Length();
            DVector3 accleration = -_position * _nu / (r * r * r);
            DVector3 velocity = _velocity + accleration * deltaTime;
            DVector3 position = _position + _velocity * deltaTime + accleration * (deltaTime * deltaTime * 0.5);
            return (position, velocity);
        }
    }
}
