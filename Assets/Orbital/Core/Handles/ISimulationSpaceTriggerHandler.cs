using Orbital.Core.Simulation;

namespace Orbital.Core.Handles
{
    public interface ISimulationSpaceTriggerHandler
    {
        void OnRigidbodyEnter(IDynamicBody body, SimulationSpace simulationSpace);
        void OnRigidbodyExit(IDynamicBody body, SimulationSpace simulationSpace);
    }
}