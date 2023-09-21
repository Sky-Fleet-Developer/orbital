using System.Collections.Generic;
using System.Linq;
using Orbital.Model.Services;
using Sirenix.OdinInspector;
using Zenject;

namespace Orbital.Model
{
    public class Body
    {
        [ShowInInspector] private List<Component> _components = new();

        [Inject] private ComponentsRegistrationService _registrationService;

        public void AddComponent<T>(T component) where T : Component
        {
            _components.Add(component);
            _registrationService.RegisterComponent(component);
        }

        public void RemoveComponent<T>(T component) where T : Component
        {
            _components.Remove(component);
            _registrationService.UnregisterComponent(component);
        }

        public IEnumerable<Component> Components => _components;

        public T GetComponent<T>() where T : Component
        {
            return _components.FirstOrDefault(x => x is T) as T;
        }
    }
}