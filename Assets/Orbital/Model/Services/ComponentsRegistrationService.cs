using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace Orbital.Model.Services
{
    public class ComponentsRegistrationService : MonoBehaviour
    {
        [Inject] private LoopEmitterService _loopEmitterService;
        private Queue<Component> _componentsToCallStart = new();

        private void Start()
        {
            _loopEmitterService.BeforeUpdateHandler += OnBeforeUpdate;
        }

        private void OnDestroy()
        {
            _loopEmitterService.BeforeUpdateHandler -= OnBeforeUpdate;
        }

        private void OnBeforeUpdate()
        {
            while (_componentsToCallStart.Count > 0)
            {
                Component component = _componentsToCallStart.Dequeue();
                component.Start();
            }
        }

        public void RegisterComponent<T>(T component) where T : Component
        {
            _loopEmitterService.Add(component);
            _componentsToCallStart.Enqueue(component);
        }

        public void UnregisterComponent<T>(T component) where T : Component
        {
            _loopEmitterService.Remove(component);
        }
    }
}
