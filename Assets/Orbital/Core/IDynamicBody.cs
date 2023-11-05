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
}
