using Orbital.Core.Handles;
using UnityEngine;
using Zenject;

namespace Orbital.Core.SystemComponents
{
    public class ComponentsRegistrationService : MonoBehaviour
    {
        [Inject] private DiContainer _container;
        //private Queue<Component> _componentsToCallStart = new();

        /*private void Start()
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
        }*/

        public void RegisterComponent<T>(T component) where T : Component
        {
            HandlesRegister.RegisterHandlers(component, _container);
        }

        public void UnregisterComponent<T>(T component) where T : Component
        {
            HandlesRegister.UnregisterHandlers(component, _container);
        }
    }
}
