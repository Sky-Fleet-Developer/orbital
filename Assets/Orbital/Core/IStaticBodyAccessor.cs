using Orbital.Core.Bodies;
using Orbital.Core.TrajectorySystem;

namespace Orbital.Core
{
    public interface IStaticBodyAccessor
    {
        public IStaticBody Self { get; }
        public IMassSystem MassSystem { get; set; }
        public IStaticBody Parent { get; set; }
        public IStaticBody[] Children { get; set; }
        public StaticOrbit Orbit { get; set; }
        public bool IsSatellite { get; set; }
        /// <summary>Per-body data (radius, rotation...) from the body table; null when the body has no record.</summary>
        public BodyRecord Record { get; set; }
    }
}