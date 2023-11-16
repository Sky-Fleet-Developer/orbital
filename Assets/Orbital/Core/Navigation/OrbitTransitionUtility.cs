using System;
using System.Linq;
using Ara3D;
using Orbital.Core.TrajectorySystem;
using UnityEngine;

namespace Orbital.Core.Navigation
{
    public static class OrbitTransitionUtility
    {
        private const double LowTolerance = 0.995;
        private const double HighTolerance = 1.005;

        public static OrbitEnding GetEnding(this StaticOrbit orbit, IStaticBody parent, double fromTime)
        {
            IStaticBody internalCollision =
                CollideTrajectoryWithChildren(orbit, parent, fromTime, out double collisionTime);
            DVector3 collisionPos = DVector3.Zero;
            /*if (internalCollision != null)
            {
                //orbit.GetOrbitalStateVectorsAtOrbitTime(collisionTime, out collisionPos, out DVector3 vel);
                //bool isGrowth = DVector3.Dot(collisionPos, vel) > 0;
                //if (isGrowth)
                {
                    return new OrbitEnding(OrbitEndingType.Entry, collisionPos, collisionTime, internalCollision);
                }
            }*/

            bool isLeaves = IsLeavesGravityRadius(orbit,
                MassUtility.GetGravityRadius(parent.GravParameter) * HighTolerance, fromTime,
                out DVector3 leavePoint, out double leaveTime);
            if (isLeaves)
            {
                if (internalCollision != null && collisionTime < leaveTime)
                {
                    return new OrbitEnding(OrbitEndingType.Entry, collisionPos, collisionTime, internalCollision);
                }

                if (parent.ParentCelestial != null)
                    return new OrbitEnding(OrbitEndingType.Leave, leavePoint, leaveTime, parent.ParentCelestial);
                else return new OrbitEnding(OrbitEndingType.Cycle, DVector3.Zero, leaveTime, parent);
            }

            if (internalCollision != null)
            {
                return new OrbitEnding(OrbitEndingType.Entry, collisionPos, collisionTime, internalCollision);
            }

            return new OrbitEnding(OrbitEndingType.Cycle, DVector3.Zero, double.PositiveInfinity, parent);
        }

        public static bool IsLeavesGravityRadius(this StaticOrbit orbit, double gravityRadius, double fromTime,
            out DVector3 leavePoint, out double leaveTime)
        {
            bool result = orbit.Eccentricity > 1 || orbit.Apocenter > gravityRadius;
            if (result)
            {
                double leaveTrueAnomaly = orbit.TrueAnomalyAtRadius(gravityRadius, fromTime);
                leavePoint = orbit.GetPositionFromTrueAnomaly(leaveTrueAnomaly);
                leaveTime = orbit.TimeOfTrueAnomaly(leaveTrueAnomaly, fromTime);
                return true;
            }

            leavePoint = DVector3.Zero;
            leaveTime = -1;
            return false;
        }

        public static IStaticBody CollideTrajectoryWithChildren(this StaticOrbit orbit, IStaticBody body, double time,
            out double collisionTime)
        {
            bool IsProjectionCrossing(StaticOrbit other, double width)
            {
                double apo = other.Apocenter + width;
                double peri = other.Pericenter - width;
                return !(orbit.Pericenter > apo || orbit.Apocenter < peri);
            }


            (double time, IStaticBody body, double radius)[] collisions = body.Children
                .Where(x => x.IsSatellite)
                .Select(x => (radius: MassUtility.GetGravityRadius(x.GravParameter) * LowTolerance, body: x))
                .Where(x => IsProjectionCrossing(x.body.Orbit, x.radius))
                .Select(x => (time: GetClosestPointTimeForDistance2(orbit, x.body.Orbit, x.radius, time),
                    x.body, x.radius))
                .Where(x => !double.IsNaN(x.time))
                .OrderBy(x => x.Item1).ToArray();

            if (collisions.Length == 0)
            {
                collisionTime = 0;
                return null;
            }

            collisionTime = collisions[0].time;
            return collisions[0].body;
        }

        public static double GetClosestPointTimeForDistance2(StaticOrbit a, StaticOrbit b, double wantedDistance, double startTime)
        {
            double rSqr = wantedDistance * wantedDistance;
            double endTime = startTime + a.Eccentricity < 0 ? a.Period : b.Period;

            const int parts = 30;
            double step = (endTime - startTime) / parts;
            for (int i = 0; i < parts; i++)
            {
                double rootT = HandlePart(startTime + i * step, startTime + (i + 1.1) * step);
                if (!double.IsNaN(rootT))
                {
                    return rootT;
                }
            }

            return double.NaN;

            double HandlePart(double startTime, double endTime)
            {
                const int maxIterations = 30;
                double epsilon = 1e-2;
                double time = startTime;
                
                for (int i = 0; i < maxIterations; i++)
                {
                    (double f, double df) = Evaluate(time);
                    double dt = -f / df;
                    time += dt;
                    if (time > endTime || time < startTime) break;

                    if (Math.Abs(dt) < epsilon) return time;
                    
                    (double f, double df) Evaluate(double t)
                    {
                        const double e = 1e-5;
                        const double eInv = 1e5;
                        double ft = LengthSqrOffset(time);
                        double ft2 = LengthSqrOffset(time + e);
                        return (ft, (ft2 - ft) * eInv);
                    }
                }
                return double.NaN;
            }

            double LengthSqr(double time)
            {
                var aa = a.GetPositionAtT(time);
                var bb = b.GetPositionAtT(time);
                //Debug.DrawLine(drawOffset + (Vector3)aa * scale, drawOffset + (Vector3)bb * scale, Color.cyan, 0.5f);
                return (aa - bb).LengthSquared();
            }

            double LengthSqrOffset(double time)
            {
                var aa = a.GetPositionAtT(time);
                var bb = b.GetPositionAtT(time);
                //Debug.DrawLine(drawOffset + (Vector3)aa * scale, drawOffset + (Vector3)bb * scale, Color.cyan, 0.5f);
                return Math.Abs((aa - bb).LengthSquared() - rSqr);
            }
        }

        public static double GetClosestPointTimeForDistance(StaticOrbit a, StaticOrbit b, double wantedDistance,
            double startTime, out double distance /*, Vector3 drawOffset, float scale*/)
        {
            double rSqr = wantedDistance * wantedDistance;
            double endTime = startTime + a.Eccentricity < 0 ? a.Period : b.Period;
            int accuracy = 70;
            double maxDelta = 0.5;

            double step = (endTime - startTime) / accuracy;
            double[] distances = {double.MaxValue, double.MaxValue};
            double[] times = {startTime, startTime};
            for (double t = startTime; t < endTime; t += step)
            {
                double d = LengthSqr(t);
                bool isLessThenWanted = d < rSqr;
                d = Math.Abs(d - rSqr);
                if (d < distances[0])
                {
                    distances[1] = distances[0];
                    times[1] = times[0];
                    distances[0] = d;
                    times[0] = t;
                }
                else if (d < distances[1])
                {
                    distances[1] = d;
                    times[1] = t;
                }

                if (isLessThenWanted) break;
            }

            if (LengthSqr(times[0]) < rSqr)
            {
            }

            double maxTime = Math.Max(times[0], times[1]);

            //Debug.DrawLine(drawOffset + (Vector3)a.GetPositionAtT(times[0] - a.Epoch) * scale, drawOffset + (Vector3)b.GetPositionAtT(times[0] - b.Epoch) * scale, Color.yellow, 0.5f);
            //Debug.DrawLine(drawOffset + (Vector3)a.GetPositionAtT(times[1] - a.Epoch) * scale, drawOffset + (Vector3)b.GetPositionAtT(times[1] - b.Epoch) * scale, Color.red, 0.5f);
            double delta = double.MaxValue;

            if (times[0] > startTime + 1)
            {
                double deltaD0 = LengthSqrOffset(times[0] + Math.Sign(times[1] - times[0]) * maxDelta) - distances[0];

                if (deltaD0 > 0)
                {
                    double timeInv = times[0] + (times[0] - times[1]);
                    double invTimeD = LengthSqrOffset(timeInv);
                    if (invTimeD < distances[0])
                    {
                        times[1] = times[0];
                        distances[1] = times[0];
                        times[0] = timeInv;
                        distances[0] = invTimeD;
                    }
                    else
                    {
                        times[1] = timeInv;
                        distances[1] = invTimeD;
                    }
                }
            }

            int cycle = 0;
            while (delta > maxDelta)
            {
                IterateBinarySearch();

                //Debug.DrawLine(drawOffset + (Vector3)a.GetPositionAtT(times[0] - a.Epoch) * scale, drawOffset + (Vector3)b.GetPositionAtT(times[0] - b.Epoch) * scale, Color.blue, 0.5f);
                //Debug.DrawLine(drawOffset + (Vector3)a.GetPositionAtT(times[1] - a.Epoch) * scale, drawOffset + (Vector3)b.GetPositionAtT(times[1] - b.Epoch) * scale, Color.green, 0.5f);
                if (cycle++ > 40)
                {
                    Debug.Log("Can't solve");
                    distance = Math.Sqrt(distances[0]);
                    return times[0];
                }
            }

            if (times[0] < startTime)
            {
                distance = Math.Sqrt(LengthSqr(startTime));
                return startTime;
            }

            void IterateBinarySearch()
            {
                double t0, t1;
                if (times[0] < times[1])
                {
                    t0 = times[0];
                    t1 = times[1];
                }
                else
                {
                    t0 = times[1];
                    t1 = times[0];
                }

                bool isApproach = false;
                const int parts = 4;
                for (int i = 1; i < parts; i++)
                {
                    double t = t0 + (t1 - t0) * i / parts;
                    double d = LengthSqr(t);
                    bool isLessThenWanted = d < rSqr;
                    double dSqr = Math.Abs(d - rSqr);
                    if (dSqr < distances[0])
                    {
                        delta = Math.Abs(t0 - t);
                        distances[1] = distances[0];
                        times[1] = times[0];
                        distances[0] = dSqr;
                        times[0] = t;
                        isApproach = true;
                    }
                    else if (dSqr < distances[1] || isLessThenWanted)
                    {
                        delta = Math.Abs(t0 - t);
                        distances[1] = dSqr;
                        times[1] = t;
                        isApproach = true;
                    }

                    if (isLessThenWanted) break;
                }

                if (!isApproach)
                {
                    var t = (times[0] + times[1]) * 0.5;
                    var d = LengthSqrOffset(times[1]);
                    if (d < distances[0])
                    {
                        distances[1] = distances[0];
                        times[1] = times[0];
                        distances[0] = d;
                        times[0] = t;
                    }
                    else
                    {
                        distances[1] = d;
                        times[1] = t;
                    }
                }

                /*else
                {
                    Debug.Log("inverse");
                    times[1] = times[0] + (times[1] - times[0]) * 0.8;
                    distances[1] = LengthSqr(a, b, times[1]);
                }*/
            }

            double LengthSqr(double time)
            {
                var aa = a.GetPositionAtT(time);
                var bb = b.GetPositionAtT(time);
                //Debug.DrawLine(drawOffset + (Vector3)aa * scale, drawOffset + (Vector3)bb * scale, Color.cyan, 0.5f);
                return (aa - bb).LengthSquared();
            }

            double LengthSqrOffset(double time)
            {
                var aa = a.GetPositionAtT(time);
                var bb = b.GetPositionAtT(time);
                //Debug.DrawLine(drawOffset + (Vector3)aa * scale, drawOffset + (Vector3)bb * scale, Color.cyan, 0.5f);
                return Math.Abs((aa - bb).LengthSquared() - rSqr);
            }

            distance = Math.Sqrt(LengthSqr(times[0]));
            return times[0];
        }
    }
}