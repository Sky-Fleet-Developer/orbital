using System;
using Ara3D;
using Orbital.Core.TrajectorySystem;

namespace Orbital.Core.Simulation
{
    public interface IDynamicBody
    {
        public IStaticBody Parent { get; }
        public IStaticTrajectory Trajectory { get; }
        public void Init();
        /*public void Present();
        public void RemovePresent();*/
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
