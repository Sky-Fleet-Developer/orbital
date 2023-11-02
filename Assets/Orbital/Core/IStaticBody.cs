using System.Collections.Generic;
using Ara3D;
using Orbital.Core.TrajectorySystem;

namespace Orbital.Core
{
    public interface IStaticBody
    {
        public IMassSystem MassSystem { get; }
        public IStaticBody Parent { get; }
        public IEnumerable<IStaticBody> Children { get; }
        public DVector3 Position { get; }
        public DVector3 LocalPosition { get; }
        public IStaticTrajectory Trajectory { get; }
        public double GravParameter => MassSystem.Mass * MassUtility.G;
        public bool IsSatellite { get; }

        public double Radius
        {
            get => 1;
        }
    }
}