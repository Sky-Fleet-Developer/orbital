using System.Linq;
using Ara3D;
using Orbital.Core.TrajectorySystem;

namespace Orbital.Core.Navigation
{
    public static class OrbitTransitionUtility
    {
        private const double LowTolerance = 0.995;
        private const double HighTolerance = 1.005;

        /// <summary>How many orbital periods ahead a bound orbit is searched for encounters with child bodies.</summary>
        private const double EncounterSearchPeriods = 1;

        public static OrbitEnding GetEnding(this StaticOrbit orbit, IStaticBody parent, double fromTime)
        {
            bool isLeaves = IsLeavesGravityRadius(orbit,
                MassUtility.GetGravityRadius(parent.GravParameter) * HighTolerance, fromTime,
                out DVector3 leavePoint, out double leaveTime);

            double horizon = isLeaves ? leaveTime - fromTime : orbit.Period * EncounterSearchPeriods;
            IStaticBody internalCollision =
                CollideTrajectoryWithChildren(orbit, parent, fromTime, horizon, out double collisionTime);

            if (isLeaves)
            {
                if (internalCollision != null && collisionTime < leaveTime)
                {
                    return new OrbitEnding(OrbitEndingType.Entry, orbit.GetPositionAtT(collisionTime), collisionTime,
                        internalCollision);
                }

                if (parent.Parent != null)
                    return new OrbitEnding(OrbitEndingType.Leave, leavePoint, leaveTime, parent.Parent);
                else return new OrbitEnding(OrbitEndingType.Cycle, DVector3.Zero, leaveTime, parent);
            }

            if (internalCollision != null)
            {
                return new OrbitEnding(OrbitEndingType.Entry, orbit.GetPositionAtT(collisionTime), collisionTime,
                    internalCollision);
            }

            return new OrbitEnding(OrbitEndingType.Cycle, DVector3.Zero, double.PositiveInfinity, parent);
        }

        public static bool IsLeavesGravityRadius(this StaticOrbit orbit, double gravityRadius, double fromTime,
            out DVector3 leavePoint, out double leaveTime)
        {
            if (orbit.TryFindExit(gravityRadius, fromTime, out leaveTime))
            {
                leavePoint = orbit.GetPositionAtT(leaveTime);
                return true;
            }

            leavePoint = DVector3.Zero;
            leaveTime = -1;
            return false;
        }

        /// <summary>The first child whose sphere of influence the orbit enters within <paramref name="horizon"/> seconds.</summary>
        public static IStaticBody CollideTrajectoryWithChildren(this StaticOrbit orbit, IStaticBody body, double time,
            double horizon, out double collisionTime)
        {
            bool IsProjectionCrossing(StaticOrbit other, double width)
            {
                double apo = other.Apocenter + width;
                double peri = other.Pericenter - width;
                return !(orbit.Pericenter > apo || orbit.Apocenter < peri);
            }

            (double time, IStaticBody body)[] collisions = body.Children
                .Where(x => x.IsSatellite)
                .Select(x => (radius: MassUtility.GetGravityRadius(x.GravParameter) * LowTolerance, body: x))
                .Where(x => IsProjectionCrossing(x.body.Orbit, x.radius))
                .Select(x => (time: OrbitCrossing.TryFindEntry(orbit, x.body.Orbit, x.radius, time, horizon,
                    out double entryTime)
                    ? entryTime
                    : double.NaN, x.body))
                .Where(x => !double.IsNaN(x.time))
                .OrderBy(x => x.time).ToArray();

            if (collisions.Length == 0)
            {
                collisionTime = 0;
                return null;
            }

            collisionTime = collisions[0].time;
            return collisions[0].body;
        }
    }
}
