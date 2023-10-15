using Orbital.Core.Simulation;

namespace Orbital.Core.Handles
{
    public interface IObserverTriggerHandler
    {
        void OnRigidbodyEnter(IRigidBody body, Observer observer);
        void OnRigidbodyExit(IRigidBody body, Observer observer);
    }
}