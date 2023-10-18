using System;
using Orbital.Core.Simulation;
using UnityEngine;
using Zenject;

namespace Orbital.Player
{
    [RequireComponent(typeof(DynamicBody))]
    public class BindRigidbodyToPlayer : MonoBehaviour
    {
        [SerializeField] private PlayerToBind playerToBind;
        [Inject] private CameraModel _camera;
        private DynamicBody _rigidbody;

        private void Start()
        {
            _rigidbody = GetComponent<DynamicBody>();
            if (_rigidbody.Mode != DynamicBodyMode.Trajectory)
            {
                _camera.BindToRigidbody(_rigidbody.Presentation);
            }
            else
            {
                _rigidbody.ModeChangedHandler += OnModeChanged;
            }
        }

        private void OnModeChanged(DynamicBodyMode mode)
        {
            _rigidbody.ModeChangedHandler -= OnModeChanged;
            _camera.BindToRigidbody(_rigidbody.Presentation);
        }
    }

    public enum PlayerToBind
    {
        Self = 0,
        Player1 = 1,
        Player2 = 2,
        Player3 = 3
    }
}