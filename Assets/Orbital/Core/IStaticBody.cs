using Ara3D;
using Orbital.Core.TrajectorySystem;

namespace Orbital.Core
{
    public interface IStaticBody
    {
        public IMassSystem MassSystem { get; }
        public IStaticBody Parent { get; }
        public DVector3 Position { get; }
        public DVector3 LocalPosition { get; }
        public IStaticTrajectory Trajectory { get; }
        double gravParameter => MassSystem.Mass * MassUtility.G;
        double Radius
        {
            get => 1;
        }
    }
}