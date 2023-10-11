using System;
using Orbital.Model.SystemComponents;
using Orbital.Model.TrajectorySystem;
using UnityEngine;

namespace Orbital.Model.Simulation
{
    [RequireComponent(typeof(Rigidbody))]
    public class RigidbodyPresentation : MonoBehaviour
    {
        private Rigidbody _rigidbody;
        private RigidBodySystemComponent _master;
        private Observer _observer;
        private RelativeTrajectory _trajectory;
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

        public void Init(RigidBodySystemComponent component, Observer observer)
        {
            _master = component;
            _observer = observer;
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
            float interpolationValue = (float)((TimeService.WorldTime - _interpolationLastTime) / TrajectoryUpdateThreshold);
            if (interpolationValue > 1)
            {
                interpolationValue = 0;
                RecordInterpolationData();
            }
            Position = Vector3.Lerp(_positionToInterpolationLast, _positionToInterpolationNext, interpolationValue);
            Velocity = Vector3.Lerp(_velocityToInterpolationLast, _velocityToInterpolationNext, interpolationValue);
        }

        private void RecordInterpolationData()
        {
            double nextTime = TimeService.WorldTime + TrajectoryUpdateThreshold;
            _positionToInterpolationNext = _trajectory.GetPosition(nextTime) - _observer.Trajectory.GetPosition(nextTime);
            _velocityToInterpolationNext = _trajectory.GetVelocity(nextTime) - _observer.Trajectory.GetVelocity(nextTime);
            _positionToInterpolationLast = Position;
            _velocityToInterpolationLast = Velocity;
            _interpolationLastTime = TimeService.WorldTime;
        }

        private void OnCollisionEnter(Collision other)
        {
            if (_master.IsSleep)
            {
                Debug.Log("Awake");
                _master.AwakeFromSleep();
            }
        }
    }
}