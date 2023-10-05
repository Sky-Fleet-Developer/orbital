using Orbital.Model.Simulation;
using Orbital.Model.SystemComponents;

namespace Orbital.Model.Handles
{
    public interface IObserverTriggerHandler
    {
        void OnRigidbodyEnter(RigidBodySystemComponent component, Observer observer);
        void OnRigidbodyExit(RigidBodySystemComponent component, Observer observer);
    }
}