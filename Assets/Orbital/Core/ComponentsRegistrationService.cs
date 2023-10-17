using Orbital.Core.Handles;
using UnityEngine;
using Zenject;

namespace Orbital.Core
{
    public class ComponentsRegistrationService : MonoBehaviour
    {
        [Inject] private DiContainer _container;

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
