using System;
using Core.Patterns.State;
using Orbital.Core;
using Orbital.Core.Handles;
using Orbital.Core.Simulation;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Orbital.Player
{
    public class CameraModel : SystemComponent<CamaraVariables, CamaraSettings>, ILateUpdateHandler, IStateMachine
    {
        [SerializeField] private CamaraSettings settings;
        [SerializeField] private CamaraVariables variables;
        public State CurrentState { get; set; }
        public Quaternion Rotation => transform.localRotation;
        private RigidbodyPresentation _target;
        public RigidbodyPresentation Target => _target;
        public event Action TargetChangedHandler;
        private float _armInterpolated;

        public override CamaraSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        public override CamaraVariables Variables
        {
            get => variables;
            set => variables = value;
        }

        private void Awake()
        {
            CurrentState = new IdleState();
        }
        
        public void BindToRigidbody(RigidbodyPresentation rigidbody)
        {
            transform.SetParent(rigidbody.transform.parent);
            _target = rigidbody;
            CurrentState = new OrbitState(rigidbody, this);
            TargetChangedHandler?.Invoke();
        }

        public void LateUpdate()
        {
            CurrentState.Update();
        }

        private class IdleState : State
        {
            public override void Update() { }
        }
        private class OrbitState : State<CameraModel>
        {
            private RigidbodyPresentation _rigidbody;
            public OrbitState(RigidbodyPresentation rigidbody, CameraModel master) : base(master)
            {
                _rigidbody = rigidbody;
            }
            
            public override void Update()
            {
                Master._armInterpolated = Mathf.Lerp(Master._armInterpolated, Master.Variables.arm, Time.deltaTime * Master.settings.armDumping);
                Vector3 direction = Master.Variables.originOffset + Master.Variables.direction * Master._armInterpolated;
                Quaternion rotation = Quaternion.LookRotation(-direction, Master.Variables.up);
                Master.transform.localPosition = _rigidbody.Position + direction;
                Master.transform.localRotation = rotation * Quaternion.Euler(Master.settings.rotationOffset);
            }
        }
    }

    [Serializable]
    public class CamaraVariables
    {
        public Vector3 originOffset;
        public Vector3 direction;
        public Vector3 up;
        public float arm;
    }
    
    [Serializable]
    public struct CamaraSettings
    {
        public Vector3 rotationOffset;
        public float armDumping;
    }
}
