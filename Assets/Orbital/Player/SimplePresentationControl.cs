using System;
using Orbital.Core.Simulation;
using UnityEngine;
using Zenject;

namespace Orbital.Player
{
    [RequireComponent(typeof(IDynamicBody))]
    public class SimplePresentationControl : MonoBehaviour
    {
        [SerializeField] private float acceleration;
        [Inject] private CameraModel _cameraModel;
        private IDynamicBody _body;
        private RigidbodyPresentation _presentation;
        private void Awake()
        {
            _body = GetComponent<IDynamicBody>();
            _body.ModeChangedHandler += BodyOnModeChangedHandler;
        }

        private void OnDestroy()
        {
            _body.ModeChangedHandler -= BodyOnModeChangedHandler;
        }

        private void BodyOnModeChangedHandler(DynamicBodyMode mode)
        {
            bool condition = mode != DynamicBodyMode.Trajectory;
            _presentation = condition ? _body.Presentation : null;
            enabled = condition;
        }

        private void FixedUpdate()
        {
            Vector3 input = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
            if (input.sqrMagnitude > 0.01f)
            {
                _presentation.AddForce(_cameraModel.Rotation * input * (Time.deltaTime * acceleration));
            }
        }
    }
}
