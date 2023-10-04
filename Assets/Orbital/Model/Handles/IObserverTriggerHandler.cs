using Orbital.Model.SystemComponents;

namespace Orbital.Model.Handles
{
    public interface IObserverTriggerHandler
    {
        void OnRigidbodyEnter(RigidBodySystemComponent component);
        void OnRigidbodyExit(RigidBodySystemComponent component);
    }
}