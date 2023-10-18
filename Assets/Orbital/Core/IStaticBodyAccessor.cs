using Orbital.Core.TrajectorySystem;

namespace Orbital.Core
{
    internal interface IStaticBodyAccessor
    {
        public IStaticBody Self { get; }
        public IMassSystem MassSystem { get; set; }
        public IStaticBody Parent { get; set; }
        public IStaticTrajectory Trajectory { get; set; }
    }
}