using Ara3D;
using Orbital.Core.Handles;
using Orbital.Core.TrajectorySystem;
using Zenject;

namespace Orbital.Core.Simulation
{
    public class RuntimeTrajectory : IFixedUpdateHandler, ITrajectorySampler
    {
        private IStaticBody _parent;
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

        public void Attach(IStaticBody parent)
        {
            _parent = parent;
            _nu = parent.MassSystem.Mass * MassUtility.G;
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

        public (DVector3 position, DVector3 velocity) GetSample(double time, bool positionRequired = true, bool velocityRequired = true)
        {
            double deltaTime = time - _lastUpdateTime;
            double r = _position.Length();
            DVector3 acceleration = -_position * _nu / (r * r * r);
            DVector3 velocity = velocityRequired ? (_velocity + acceleration * deltaTime) : DVector3.Zero;
            DVector3 position = positionRequired ? (_position + _velocity * deltaTime + acceleration * (deltaTime * deltaTime * 0.5)) : DVector3.Zero;
            return (position, velocity);
        }
    }
}
