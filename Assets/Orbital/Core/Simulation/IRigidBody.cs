using System;
using Orbital.Core.TrajectorySystem;

namespace Orbital.Core.Simulation
{
    public interface IRigidBody
    {
        public MassSystemComponent Parent { get; }
        public RigidBodyMode Mode { get; }
        public ITrajectorySampler Trajectory { get; }
        public event Action<RigidBodyMode> ModeChangedHandler;
        public void Present(SimulationSpace simulationSpace);
        public void RemovePresent();
        public void AwakeFromSleep();

    }
    
    public enum RigidBodyMode
    {
        Trajectory = 0,
        Sleep = 1,
        Simulation = 2
    }
}
