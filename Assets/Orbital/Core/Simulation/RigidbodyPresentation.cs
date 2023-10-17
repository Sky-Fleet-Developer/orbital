using Ara3D;
using Orbital.Core.TrajectorySystem;
using UnityEngine;

namespace Orbital.Core.Simulation
{
    [RequireComponent(typeof(Rigidbody))]
    public class RigidbodyPresentation : MonoBehaviour
    {
        private Rigidbody _rigidbody;
        private IRigidBody _master;
        private SimulationSpace _simulationSpace;
        private ITrajectorySampler _trajectory;
        private double _interpolationLastTime = 0;
        private const double TrajectoryUpdateThreshold = 2;
        private Vector3 _positionToInterpolationLast;
        private Vector3 _velocityToInterpolationLast;
        private Vector3 _positionToInterpolationNext;
        private Vector3 _velocityToInterpolationNext;
        
        public Vector3 Velocity
        {
            get => _rigidbody.velocity;
            set => _rigidbody.velocity = value;
        }
        public Vector3 Position
        {
            get => transform.localPosition;
            set => transform.localPosition = value;
        }

        public Quaternion Rotation
        {
            get => transform.localRotation;
            set => transform.localRotation = value;
        }
        
        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        public void Init(IRigidBody component, SimulationSpace simulationSpace)
        {
            _master = component;
            _simulationSpace = simulationSpace;
            _trajectory = _master.Trajectory;
        }

        private void Update()
        {
            switch (_master.Mode)
            {
                case RigidBodyMode.Sleep:
                    FollowTrajectory();
                    break;
                case RigidBodyMode.Simulation:
                    break;
            }
        }

        private void FollowTrajectory()
        {
            /*float interpolationValue = (float)((TimeService.WorldTime - _interpolationLastTime) / TrajectoryUpdateThreshold);
            if (interpolationValue > 1)
            {
                interpolationValue = 0;
                RecordInterpolationData();
            }
            Position = Vector3.Lerp(_positionToInterpolationLast, _positionToInterpolationNext, interpolationValue);
            Velocity = Vector3.Lerp(_velocityToInterpolationLast, _velocityToInterpolationNext, interpolationValue);*/
            (DVector3 pos, DVector3 vel) = _trajectory.GetSample(TimeService.WorldTime);
            (DVector3 spacePos, DVector3 spaceVel) = _simulationSpace.SampleTrajectory(TimeService.WorldTime);
            Position = pos - spacePos;
            Velocity = vel - spaceVel;
        }

        private void RecordInterpolationData()
        {
            double nextTime = TimeService.WorldTime + TrajectoryUpdateThreshold;
            (DVector3 spacePos, DVector3 spaceVel) = _simulationSpace.SampleTrajectory(nextTime);
            (_positionToInterpolationNext, _velocityToInterpolationNext) = _trajectory.GetSample(nextTime);
            _positionToInterpolationNext -= (Vector3)spacePos;
            _velocityToInterpolationNext -= (Vector3)spaceVel;
            _positionToInterpolationLast = Position;
            _velocityToInterpolationLast = Velocity;
            _interpolationLastTime = TimeService.WorldTime;
        }

        private void OnCollisionEnter(Collision other)
        {
            if (_master.Mode == RigidBodyMode.Sleep)
            {
                _master.AwakeFromSleep();
            }
        }
    }
}