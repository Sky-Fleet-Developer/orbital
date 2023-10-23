using System;
using Ara3D;
using Orbital.Core.TrajectorySystem;

namespace Orbital.Core.Simulation
{
    public interface IDynamicBody
    {
        public IStaticBody Parent { get; }
        public DynamicBodyMode Mode { get; }
        public ITrajectoryRefSampler TrajectorySampler { get; }
        public IStaticTrajectory Trajectory { get; }
        public RigidbodyPresentation Presentation { get; }
        public event Action<DynamicBodyMode> ModeChangedHandler;
        public void Init();
        public void Present(SimulationSpace simulationSpace);
        public void RemovePresent();
        public void SetVelocityDirty();
        public void SimulationWasMoved(DVector3 deltaPosition, DVector3 deltaVelocity);
    }
    
    internal interface IDynamicBodyAccessor
    {
        public IDynamicBody Self { get; }
        public IStaticBody Parent { get; set; }
        public IStaticTrajectory Trajectory { get; set; }

    }
    
    public enum DynamicBodyMode
    {
        Trajectory = 0,
        Sleep = 1,
        Simulation = 2
    }
}
