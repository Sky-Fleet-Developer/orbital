using System;
using UnityEngine;
using UnityEngine.AI;

namespace Orbital.Player
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class PlayerMotor : MonoBehaviour
    {
        [SerializeField] private float horizontalRotationSpeed;
        [SerializeField] private float verticalRotationSpeed;
        [SerializeField] private Vector2 verticalMinMax;
        [SerializeField] private Transform head;
        [SerializeField] private float moveSpeed;
        private NavMeshAgent _agent;
        private float _verticalRotation;
        private float _horizontalRotation;
        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
        }

        private void LateUpdate()
        {
            _horizontalRotation += Mathf.Clamp(Input.GetAxis("Mouse X") * horizontalRotationSpeed * Time.deltaTime, -15, 15);
            _verticalRotation = Mathf.Clamp(_verticalRotation + Mathf.Clamp(Input.GetAxis("Mouse Y") * verticalRotationSpeed * Time.deltaTime, -15, 15), verticalMinMax.x, verticalMinMax.y);
            Quaternion rotation = Quaternion.Euler(0, _horizontalRotation, 0);
            _agent.velocity = rotation * new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical")) * moveSpeed;
            transform.localRotation = rotation;
            head.localRotation = Quaternion.Euler(-_verticalRotation, 0, 0);
        }
        

    }
}
