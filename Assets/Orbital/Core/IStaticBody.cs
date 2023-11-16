using System.Collections.Generic;
using Ara3D;
using Orbital.Core.TrajectorySystem;

namespace Orbital.Core
{
    public interface IStaticBody
    {
        public IStaticBody ParentCelestial { get; }
        public IMassSystem MassSystem { get; }
        public IEnumerable<IStaticBody> Children { get; }
        public DVector3 Position => ParentCelestial == null ? DVector3.Zero : ParentCelestial.Position + LocalPosition;
        public DVector3 LocalPosition { get; }
        public DVector3 GetPositionAtT(double t) => ParentCelestial == null ? DVector3.Zero : ParentCelestial.GetPositionAtT(t) + Orbit.GetPositionAtT(t);
        public StaticOrbit Orbit { get; }
        public double GravParameter { get; }
        public bool IsSatellite { get; }
    }
}