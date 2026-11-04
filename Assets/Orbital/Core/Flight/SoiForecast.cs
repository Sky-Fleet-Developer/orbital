using System;
using Ara3D;
using Orbital.Core.Navigation;
using Orbital.Core.TrajectorySystem;

namespace Orbital.Core.Flight
{
    /// <summary>
    /// When the craft changes its sphere of influence if it keeps coasting. Pure Kepler from the current state against the bodies of the table, with the
    /// thresholds of <see cref="SoiRule"/>; the thrust is not taken into account. Not for the hot path: the search walks the conic. Since stage 4 the map's
    /// <see cref="TrajectoryForecast"/> chains these events on a worker thread and the HUD reads the result.
    /// </summary>
    public static class SoiForecast
    {
        /// <summary>Longest search for an open orbit, seconds (about 10 years, as the server's planning horizon).</summary>
        public const double UnboundHorizon = 3.15e8;
        /// <summary>A trajectory closer to radial than this (|H| / (r * v)) is not a conic the orbit math supports.</summary>
        private const double MinAngularMomentumRatio = 1e-6;

        /// <summary>
        /// The first change of body after <paramref name="time"/>: <paramref name="toBody"/> is the child the craft enters or the parent it returns to.
        /// False when there is none: a closed orbit that meets nothing, a nearly radial trajectory, or a craft that reaches the ground first.
        /// </summary>
        public static bool TryNext(BodyFrameTable table, int parent, Vec3 position, Vec3 velocity, double time, out double when, out int toBody)
        {
            TrajectoryEnd end = NextEvent(table, parent, position, velocity, time, out _, out when, out toBody);
            if (end == TrajectoryEnd.EnterBody || end == TrajectoryEnd.LeaveBody) return true;
            when = double.NaN;
            toBody = -1;
            return false;
        }

        /// <summary>
        /// What ends the coast on the current conic, and when: a change of body, the ground, one full revolution (<see cref="TrajectoryEnd.Closed"/>) or
        /// the search horizon of an open orbit. <see cref="TrajectoryEnd.None"/> for a state that has no usable conic (radial or zero speed).
        /// </summary>
        public static TrajectoryEnd NextEvent(BodyFrameTable table, int parent, Vec3 position, Vec3 velocity, double time,
            out StaticOrbit orbit, out double when, out int toBody)
        {
            orbit = default;
            when = double.NaN;
            toBody = -1;
            if (table == null || parent < 0 || parent >= table.Count) return TrajectoryEnd.None;
            double r = Vec3.Length(position), v = Vec3.Length(velocity);
            if (!(r > 0.0) || !(v > 0.0)) return TrajectoryEnd.None;
            if (Vec3.Length(Vec3.Cross(position, velocity)) < MinAngularMomentumRatio * r * v) return TrajectoryEnd.None;

            ref readonly BodyFrame body = ref table[parent];
            orbit = new StaticOrbit {Nu = body.Nu};
            orbit.Calculate(new DVector3(position.X, position.Y, position.Z), new DVector3(velocity.X, velocity.Y, velocity.Z), time);
            bool bound = orbit.Eccentricity < 1.0;

            TrajectoryEnd end = bound ? TrajectoryEnd.Closed : TrajectoryEnd.OpenEnd;
            when = time + (bound ? orbit.Period : UnboundHorizon);
            if (TryFindImpact(orbit, body.Radius, time, out double impact) && impact < when)
            {
                end = TrajectoryEnd.Impact;
                when = impact;
            }
            if (body.Parent >= 0 && orbit.TryFindExit(body.SoiRadius * SoiRule.ExitFactor, time, out double exit) && exit < when)
            {
                end = TrajectoryEnd.LeaveBody;
                when = exit;
                toBody = body.Parent;
            }
            foreach (int child in table.ChildrenOf(parent))
            {
                ref readonly BodyFrame c = ref table[child];
                double soi = c.SoiRadius * SoiRule.EntryFactor;
                if (orbit.Pericenter > c.Orbit.Apocenter + soi || orbit.Apocenter < c.Orbit.Pericenter - soi) continue;
                double horizon = when - time;
                if (horizon > 0.0 && OrbitCrossing.TryFindEntry(orbit, c.Orbit, soi, time, horizon, out double enter) && enter < when)
                {
                    end = TrajectoryEnd.EnterBody;
                    when = enter;
                    toBody = child;
                }
            }
            return end;
        }

        /// <summary>The first time at or after <paramref name="time"/> the conic comes down to <paramref name="radius"/>, moving inward.</summary>
        public static bool TryFindImpact(in StaticOrbit orbit, double radius, double time, out double when)
        {
            when = double.NaN;
            if (!(radius > 0.0) || !(orbit.Pericenter < radius)) return false;
            double e = orbit.Eccentricity;
            if (!(e > 0.0) || (e < 1.0 && orbit.Apocenter <= radius)) return false;
            double cosNu = (orbit.SemiLatusRectum / radius - 1.0) / e;
            double nu = -Math.Acos(Math.Max(-1.0, Math.Min(1.0, cosNu)));
            double t = orbit.TimeOfTrueAnomaly(nu, time);
            if (double.IsNaN(t) || (e >= 1.0 && t < time)) return false;
            when = t;
            return true;
        }
    }
}
