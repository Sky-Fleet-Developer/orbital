using System;
using Orbital.Core.Handles;
using Sirenix.OdinInspector;
using UnityEngine;
using Zenject;

namespace Orbital.Player
{
    public class CameraControllerService : MonoBehaviour, IUpdateHandler
    {
        [SerializeField] private float yawSensitivity;
        [SerializeField] private float pitchSensitivity;
        [SerializeField] private float maxCounterLagAngleDelta;
        [SerializeField] private float armSensitivity;
        [SerializeField] private float armMin;
        [SerializeField] private float armNonlinearity;
        [Inject] private CameraModel _cameraModel;
        [Inject] private DiContainer _diContainer;
        private float _yawInput;
        private float _pitchInput;
        private float _armInput;
        private Quaternion _orientation = Quaternion.identity;
        private bool _isActive = false;

        private void Start()
        {
            _cameraModel.Variables.direction = _cameraModel.transform.forward;
            _cameraModel.TargetChangedHandler += OnTargetChanged;
        }

        private void OnTargetChanged()
        {
            if (_cameraModel.Target && !_isActive)
            {
                HandlesRegister.RegisterHandlers(this, _diContainer);
            }
            else if (_isActive && !_cameraModel.Target)
            {
                HandlesRegister.UnregisterHandlers(this, _diContainer);
            }
        }

        private void ReadInput()
        {
            _yawInput = Mathf.Clamp(Input.GetAxis("Mouse X") * yawSensitivity * Time.deltaTime, -maxCounterLagAngleDelta, maxCounterLagAngleDelta);
            _pitchInput = Mathf.Clamp(Input.GetAxis("Mouse Y") * pitchSensitivity * Time.deltaTime, -maxCounterLagAngleDelta, maxCounterLagAngleDelta);
            _armInput = -Input.GetAxis("Mouse ScrollWheel") * armSensitivity;
        }

        private void ResetInput()
        {
            _yawInput = 0;
            _pitchInput = 0;
        }

        void IUpdateHandler.Update()
        {
            ReadInput();
            _orientation = _orientation * Quaternion.Euler(_pitchInput, _yawInput, 0);
            CamaraVariables vars = _cameraModel.Variables;
            vars.direction = _orientation * Vector3.forward;
            vars.up = _orientation * Vector3.up;
            vars.arm = Mathf.Max(armMin, vars.arm + _armInput * (1 - armNonlinearity) + _armInput * (vars.arm + 1) * armNonlinearity);
            ResetInput();
        }
    }
}