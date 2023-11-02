using Orbital.Core.TrajectorySystem;

namespace Orbital.Core.Simulation
{
    public interface IDynamicBody
    {
        public IStaticBody Parent { get; }
        public IStaticOrbit Orbit { get; }
        public void Init();
        /*public void Present();
        public void RemovePresent();*/
    }
    
    internal interface IDynamicBodyAccessor
    {
        public IDynamicBody Self { get; }
        public IStaticBody Parent { get; set; }
        public IStaticOrbit Orbit { get; set; }

    }
    
    public enum DynamicBodyMode
    {
        Trajectory = 0,
        Sleep = 1,
        Simulation = 2
    }
}
