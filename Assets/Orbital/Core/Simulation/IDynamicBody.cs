using System;
using Orbital.Core.TrajectorySystem;

namespace Orbital.Core.Simulation
{
    public interface IDynamicBody
    {
        public IStaticBody Parent { get; }
        public DynamicBodyMode Mode { get; }
        public ITrajectorySampler TrajectorySampler { get; }
        public TrajectoryContainer TrajectoryContainer { get; }
        public event Action<DynamicBodyMode> ModeChangedHandler;
        public void Present(SimulationSpace simulationSpace);
        public void RemovePresent();
        public void AwakeFromSleep();

    }
    
    public enum DynamicBodyMode
    {
        Trajectory = 0,
        Sleep = 1,
        Simulation = 2
    }
}
