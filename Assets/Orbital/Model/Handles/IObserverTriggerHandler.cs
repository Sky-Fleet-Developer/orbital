using Orbital.Model.Simulation;
using Orbital.Model.SystemComponents;

namespace Orbital.Model.Handles
{
    public interface IObserverTriggerHandler
    {
        void OnRigidbodyEnter(IRigidBody body, Observer observer);
        void OnRigidbodyExit(IRigidBody body, Observer observer);
    }
}