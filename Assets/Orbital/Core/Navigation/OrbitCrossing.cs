using System;
using Ara3D;
using Orbital.Core.TrajectorySystem;

namespace Orbital.Core.Navigation
{
    /// <summary>Finds when a craft orbit crosses the boundary of a sphere of influence. Pure orbit math, no scene types.</summary>
    public static class OrbitCrossing
    {
        private const int MaxSamples = 200000;
        private const int BisectionIterations = 100;
        private const double TimePrecision = 1e-6;

        /// <summary>
        /// First time at or after <paramref name="fromTime"/> when the orbit moves outward through <paramref name="radius"/>
        /// around its parent. False when the orbit never gets that far, or never gets inside it.
        /// </summary>
        public static bool TryFindExit(this StaticOrbit orbit, double radius, double fromTime, out double time)
        {
            time = double.NaN;
            double e = orbit.Eccentricity;
            if (e <= 0.0) return false;
            if (radius <= orbit.Pericenter || radius >= orbit.Apocenter) return false;

            double cosNu = (orbit.SemiLatusRectum / radius - 1.0) / e;
            double trueAnomaly = Math.Acos(Math.Max(-1.0, Math.Min(1.0, cosNu)));
            double crossing = orbit.TimeOfTrueAnomaly(trueAnomaly, fromTime);
            if (e >= 1.0 && crossing < fromTime) return false;

            time = crossing;
            return true;
        }

        /// <summary>
        /// First time in [<paramref name="fromTime"/>, <paramref name="fromTime"/> + <paramref name="horizon"/>] when the craft
        /// goes from outside to inside a sphere of <paramref name="radius"/> centred on the body. Both orbits are around the same parent.
        /// A craft that starts inside only counts after it has left and come back.
        /// </summary>
        public static bool TryFindEntry(StaticOrbit craft, StaticOrbit body, double radius, double fromTime, double horizon,
            out double time)
        {
            time = double.NaN;
            if (!(horizon > 0.0) || double.IsInfinity(horizon) || !(radius > 0.0)) return false;

            // The distance between the two cannot change faster than the sum of their top speeds, so a step of a
            // fraction of the radius divided by that sum cannot jump over a crossing.
            double maxSpeed = craft.GetOrbitalSpeedAtDistance(craft.Pericenter) + body.GetOrbitalSpeedAtDistance(body.Pericenter);
            double step = 0.25 * radius / maxSpeed;
            if (!(step > 0.0) || double.IsInfinity(step) || horizon / step > MaxSamples) step = horizon / MaxSamples;

            double endTime = fromTime + horizon;
            double previousTime = fromTime;
            double previousGap = Gap(craft, body, radius, previousTime);
            for (double t = fromTime + step; ; t += step)
            {
                bool last = t >= endTime;
                if (last) t = endTime;
                double gap = Gap(craft, body, radius, t);

                if (previousGap > 0.0 && gap <= 0.0)
                {
                    time = Bisect(craft, body, radius, previousTime, t);
                    return true;
                }

                if (previousGap > 0.0 && gap > 0.0 && Math.Min(previousGap, gap) < maxSpeed * step)
                {
                    double closest = ClosestApproach(craft, body, previousTime, t);
                    if (Gap(craft, body, radius, closest) <= 0.0)
                    {
                        time = Bisect(craft, body, radius, previousTime, closest);
                        return true;
                    }
                }

                if (last) return false;
                previousTime = t;
                previousGap = gap;
            }
        }

        /// <summary>Distance between the two bodies minus the radius: positive outside, negative inside.</summary>
        private static double Gap(StaticOrbit craft, StaticOrbit body, double radius, double time) =>
            (craft.GetPositionAtT(time) - body.GetPositionAtT(time)).Length() - radius;

        /// <summary>Root of <see cref="Gap"/> between a time outside (low) and a time inside (high).</summary>
        private static double Bisect(StaticOrbit craft, StaticOrbit body, double radius, double outside, double inside)
        {
            for (int i = 0; i < BisectionIterations && Math.Abs(inside - outside) > TimePrecision; i++)
            {
                double middle = 0.5 * (outside + inside);
                if (Gap(craft, body, radius, middle) > 0.0) outside = middle;
                else inside = middle;
            }

            return inside;
        }

        /// <summary>Golden-section search for the time of least separation inside a short interval.</summary>
        private static double ClosestApproach(StaticOrbit craft, StaticOrbit body, double from, double to)
        {
            const double invPhi = 0.6180339887498949;
            double a = from, b = to;
            double c = b - (b - a) * invPhi, d = a + (b - a) * invPhi;
            double fc = Gap(craft, body, 0.0, c), fd = Gap(craft, body, 0.0, d);
            for (int i = 0; i < 80 && b - a > TimePrecision; i++)
            {
                if (fc < fd)
                {
                    b = d; d = c; fd = fc;
                    c = b - (b - a) * invPhi; fc = Gap(craft, body, 0.0, c);
                }
                else
                {
                    a = c; c = d; fc = fd;
                    d = a + (b - a) * invPhi; fd = Gap(craft, body, 0.0, d);
                }
            }

            return 0.5 * (a + b);
        }
    }
}
