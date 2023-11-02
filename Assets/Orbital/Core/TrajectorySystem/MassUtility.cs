using System;
using System.Collections.Generic;
using System.Linq;
using Ara3D;
using Orbital.Core.Utilities;
using UnityEngine;

namespace Orbital.Core.TrajectorySystem
{
    public static partial class MassUtility
    {
        public const double G = 6.67430e-11;
        private const double GravityEdgeInv = 100;
        public static double GetGravityRadius(double Nu)
        {
            return Math.Sqrt(Nu * GravityEdgeInv);
        }
        
        public static Dictionary<IMassSystem, Transform> GetMap(this IMassSystem mRoot, Transform tRoot)
        {
            Dictionary<IMassSystem, Transform> result = new();
            result.Add(mRoot, tRoot);
            AddMapRecursively(result, mRoot, tRoot);
            return result;
        }

        private static void AddMapRecursively(Dictionary<IMassSystem, Transform> dictionary, IMassSystem mRoot, Transform tRoot)
        {
            int i = -1;
            foreach (IMassSystem mass in mRoot.GetContent())
            {
                i++;
                if (mass == null) continue;
                Transform tChild = tRoot.FindRegex($".*\\[{i}\\]$");
                if (tChild)
                {
                    dictionary.Add(mass, tChild);
                    AddMapRecursively(dictionary, mass, tChild);
                }
            }
        }

        public static IEnumerable<IMassSystem> GetRecursively(this IMassSystem value)
        {
            foreach (IMassSystem content in value.GetContent())
            {
                yield return content;
                if (content == null) continue;
                foreach (IMassSystem mass in content.GetRecursively())
                {
                    yield return mass;
                }
            }
        }

        /*public static Dictionary<IMass, StaticTrajectory> MakeTrajectories(this IMass mRoot)
        {
            Dictionary<IMass, StaticTrajectory> result = new();
            FillTrajectoriesRecursively(result, mRoot);
            return result;
        }*/

        public static void FillTrajectoriesRecursively(this IMassSystem massSystem, Dictionary<IMassSystem, IStaticTrajectory> container)
        {
            void SetupElement(IMassSystem child, IMassSystem other)
            {
                if (child == null) return;
                if (other == null) return;
                if (!container.TryGetValue(child, out IStaticTrajectory trajectory))
                {
                    trajectory = new StaticTrajectory(other);
                    container.Add(child, trajectory);
                }

                trajectory.Calculate(child.Settings, TimeService.WorldTime);
                FillTrajectoriesRecursively(child, container);
            }

            if (massSystem is DoubleSystemBranch doubleSystemBranch)
            {
                SetupElement(doubleSystemBranch.ChildA, doubleSystemBranch.ChildB);
                SetupElement(doubleSystemBranch.ChildB, doubleSystemBranch.ChildA);
            }
            else
            {
                foreach (IMassSystem child in massSystem.GetContent())
                {
                    SetupElement(child, massSystem);
                }
            }
        }

        public static OrbitExodus GetOrbitExodus(IStaticTrajectory trajectory, IStaticBody parent, double fromTime, out DVector3 endPoint, out double endTime, out IStaticBody transitionBody)
        {
            double gravityRadius = GetGravityRadius(parent.GravParameter);
            IStaticBody internalCollision = CollideTrajectoryWithChildren(trajectory, parent, gravityRadius, fromTime, out double collisionTime);
            DVector3 collisionPos = DVector3.Zero;
            if (internalCollision != null)
            {
                trajectory.GetOrbitalStateVectorsAtOrbitTime(collisionTime, out collisionPos, out DVector3 vel);
                bool isGrowth = DVector3.Dot(collisionPos, vel) > 0;
                if (isGrowth)
                {
                    endTime = collisionTime;
                    endPoint = collisionPos;
                    transitionBody = internalCollision;
                    return OrbitExodus.Entry;
                }
            }

            bool isLeaves = IsTrajectoryLeavesGravityRadius(trajectory, gravityRadius, fromTime, out DVector3 leavePoint, out double leaveTime);
            if (isLeaves)
            {
                if (internalCollision != null)
                {
                    if (collisionTime < leaveTime)
                    {
                        endTime = collisionTime;
                        endPoint = collisionPos;
                        transitionBody = internalCollision;
                        return OrbitExodus.Entry;
                    }

                    endPoint = leavePoint;
                    endTime = leaveTime;
                    transitionBody = parent.Parent;
                    return OrbitExodus.Leave;
                }

                endPoint = leavePoint;
                endTime = leaveTime;
                transitionBody = parent.Parent;
                return OrbitExodus.Leave;
            }

            endPoint = DVector3.Zero;
            endTime = -1;
            transitionBody = parent;
            return OrbitExodus.Cycle;
        }
        
        public static bool IsTrajectoryLeavesGravityRadius(IStaticTrajectory trajectory, double gravityRadius, double fromTime, out DVector3 leavePoint, out double leaveTime)
        {
            bool result = trajectory.Apocenter > gravityRadius;
            if (result)
            {
                double leaveTrueAnomaly = trajectory.TrueAnomalyAtRadius(gravityRadius);
                leavePoint = trajectory.GetPositionFromTrueAnomaly(leaveTrueAnomaly);
                leaveTime = trajectory.TimeOfTrueAnomaly(leaveTrueAnomaly, fromTime);
                return true;
            }

            leavePoint = DVector3.Zero;
            leaveTime = -1;
            return false;
        }
        
        public static IStaticBody CollideTrajectoryWithChildren(IStaticTrajectory trajectory, IStaticBody body, double gravityRadius, double time, out double collisionTime)
        {
            bool IsProjectionCrossing(IStaticTrajectory other, double width)
            {
                double apo = other.Apocenter + width;
                double peri = other.Pericenter - width;
                return !(trajectory.Pericenter > apo || trajectory.Apocenter < peri);
            }

            
            (double time, double distance, IStaticBody body)[] collisions = body.Children
                .Where(x => IsProjectionCrossing(x.Trajectory, gravityRadius))
                .Select(b => (GetClosestPointTimeForDistance(trajectory, b.Trajectory, gravityRadius, time, out double distance), distance, b))
                .Where(x => x.distance < gravityRadius * 1.0001)
                .OrderBy(x => x.Item1).ToArray();

            if (collisions.Length == 0)
            {
                collisionTime = 0;
                return null;
            }

            collisionTime = collisions[0].time;
            return collisions[0].body;
        }

        /*public static void DrawClosestPoint(IStaticTrajectory a, IStaticTrajectory b, double scale)
        {
            
        }*/
        
        public static double GetClosestPointTimeForDistance(IStaticTrajectory a, IStaticTrajectory b, double wantedDistance, double startTime, out double distance/*, Vector3 drawOffset, float scale*/)
        {
            double rSqr = wantedDistance * wantedDistance;
            //// rough approximation
            double endTime = startTime + a.Eccentricity < 0 ? a.Period : b.Period;
            int accuracy = 30;
            double maxDelta = 0.5;

            double step = (endTime - startTime) / accuracy;
            double? firstComingTime = null;
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
                
                if(isLessThenWanted) break;

                /*if (d < rSqr)
                {
                    firstComingTime ??= t;
                }
                else if(distances[0] < d)
                {
                    break;
                }*/
            }
            //Debug.DrawLine(drawOffset + (Vector3)a.GetPositionAtT(times[0] - a.Epoch) * scale, drawOffset + (Vector3)b.GetPositionAtT(times[0] - b.Epoch) * scale, Color.yellow, 0.5f);
            //Debug.DrawLine(drawOffset + (Vector3)a.GetPositionAtT(times[1] - a.Epoch) * scale, drawOffset + (Vector3)b.GetPositionAtT(times[1] - b.Epoch) * scale, Color.red, 0.5f);
            double delta = double.MaxValue;

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

            int cycle = 0;
            while (delta > maxDelta)
            {
                IterateBinarySearch();

                //Debug.DrawLine(drawOffset + (Vector3)a.GetPositionAtT(times[0] - a.Epoch) * scale, drawOffset + (Vector3)b.GetPositionAtT(times[0] - b.Epoch) * scale, Color.blue, 0.5f);
                //Debug.DrawLine(drawOffset + (Vector3)a.GetPositionAtT(times[1] - a.Epoch) * scale, drawOffset + (Vector3)b.GetPositionAtT(times[1] - b.Epoch) * scale, Color.green, 0.5f);
                if (cycle++ > 30)
                {
                    Debug.Log("Can't solve");
                    distance = Math.Sqrt(distances[0]);
                    return times[0];
                }
            }

            void IterateBinarySearch()
            {
                double tMid = (times[0] + times[1]) * 0.5;
                double dSqr = LengthSqrOffset(tMid);
                if (dSqr < distances[0])
                {
                    delta = Math.Abs(times[0] - tMid);
                    distances[1] = distances[0];
                    times[1] = times[0];
                    distances[0] = dSqr;
                    times[0] = tMid;
                }
                else //if (dSqr < distances[1])
                {
                    delta = Math.Abs(times[0] - tMid);
                    distances[1] = dSqr;
                    times[1] = tMid;
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
            distance = Math.Sqrt(distances[0]);
            return times[0];
        }

        //private static 
    }
}