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
        public DVector3 Position => Parent == null ? DVector3.Zero : Parent.Position + LocalPosition;
        public DVector3 LocalPosition { get; }
        public DVector3 GetPositionAtT(double t) => Parent == null ? DVector3.Zero : Parent.GetPositionAtT(t) + Orbit.GetPositionAtT(t);
        public StaticOrbit Orbit { get; }
        public double GravParameter { get; }
        public bool IsSatellite { get; }

        /// <summary>Mean radius in meters, 0 when unknown (no record in the body table): then no minimum sphere of influence is applied.</summary>
        public double Radius { get; }
    }
}