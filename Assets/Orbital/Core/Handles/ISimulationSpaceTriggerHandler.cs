using Orbital.Core.Simulation;

namespace Orbital.Core.Handles
{
    public interface ISimulationSpaceTriggerHandler
    {
        void OnRigidbodyEnter(IRigidBody body, SimulationSpace simulationSpace);
        void OnRigidbodyExit(IRigidBody body, SimulationSpace simulationSpace);
    }
}