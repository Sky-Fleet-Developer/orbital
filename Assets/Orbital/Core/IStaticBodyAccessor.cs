using Orbital.Core.TrajectorySystem;

namespace Orbital.Core
{
    internal interface IStaticBodyAccessor
    {
        public IStaticBody Self { get; }
        public IMassSystem MassSystem { get; set; }
        public IStaticBody Parent { get; set; }
        public IStaticBody[] Children { get; set; }
        public IStaticOrbit Orbit { get; set; }
        public bool IsSatellite { get; set; }
        public World World { set; }
    }
}