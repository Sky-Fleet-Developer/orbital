using System;
using System.Collections.Generic;
using System.Linq;
using Ara3D;
using Orbital.Core.Utilities;
using UnityEngine;

namespace Orbital.Core.TrajectorySystem
{
    public static class MassUtility
    {
        public const double G = 6.67430e-11;
        private const double GravityEdgeInv = 50;
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
        public static bool IsTrajectoryLeavesGravityRadius(IStaticTrajectory trajectory, IStaticBody body, out DVector3 leavePoint)
        {
            double gravityRadius = GetGravityRadius(body.GravParameter);
            bool result = gravityRadius > trajectory.SemiMajorAxis;
            if (result)
            {
                leavePoint = trajectory.GetPositionFromTrueAnomaly(trajectory.TrueAnomalyAtRadius(gravityRadius));
                return true;
            }
            else
            {
                leavePoint = DVector3.Zero;
                return false;
            }
        }
        
        /*public static bool CollideTrajectoryWithChildren(IStaticTrajectory trajectory, IStaticBody body, double time)
        {
            bool IsProjectionCrossing(IStaticTrajectory other, double width)
            {
                double apo = other.Apocenter + width;
                double peri = other.Pericenter - width;
                return !(trajectory.Pericenter > apo || trajectory.Apocenter < peri);
            }

            
            IStaticBody[] children = body.Children.ToArray().Where(x =>
            {
                double gravityRadius = GetGravityRadius(x.GravParameter);

                bool a = IsProjectionCrossing(x.Trajectory, gravityRadius);
                if (!a) return false;
                double closestPointTime = GetClosestPointTime(trajectory, x.Trajectory, time);
                //return b;
            }).ToArray();
            
            
        }*/

        /*public static void DrawClosestPoint(IStaticTrajectory a, IStaticTrajectory b, double scale)
        {
            
        }*/
        
        public static double GetClosestPointTime(IStaticTrajectory a, IStaticTrajectory b, double startTime, Vector3 drawOffset, float scale)
        {
            //// rough approximation
            //double rSqr = width * width;
            double endTime = startTime + a.Eccentricity < 0 ? a.Period : b.Period;
            int accuracy = 30;
            double step = (endTime - startTime) / accuracy;
            double? firstComingTime = null;
            double[] distances = {double.MaxValue, double.MaxValue};
            double[] times = {startTime, startTime};
            for (double t = startTime; t < endTime; t += step)
            {
                double d = LengthSqr(a, b, t);
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
                
                
                /*if (d < rSqr)
                {
                    firstComingTime ??= t;
                }
                else if(distances[0] < d)
                {
                    break;
                }*/
            }
            Debug.DrawLine(drawOffset + (Vector3)a.GetPositionAtT(times[0] - a.Epoch) * scale, drawOffset + (Vector3)b.GetPositionAtT(times[0] - b.Epoch) * scale, Color.yellow, 0.5f);
            Debug.DrawLine(drawOffset + (Vector3)a.GetPositionAtT(times[1] - a.Epoch) * scale, drawOffset + (Vector3)b.GetPositionAtT(times[1] - b.Epoch) * scale, Color.red, 0.5f);
            double delta = double.MaxValue;


            
            int cycle = 0;
            double maxDelta = 1;
            while (delta > maxDelta)
            {
                double[] timesCache = {times[0], times[1]};
                for (int i = 0; i < 2; i++)
                {
                    IterateBinarySearch();
                }

                if (Math.Abs(times[0] - timesCache[0]) < 1e-10)
                {
                    double t = timesCache[0] + (timesCache[0] - timesCache[1]) * 0.75f;
                    double d = LengthSqr(a, b, t);
                    if (d < distances[0])
                    {
                        times[1] =  times[0];
                        distances[1] = times[0];
                        times[0] = t;
                        distances[0] = d;
                    }
                    else
                    {
                        times[1] = t;
                        distances[1] = d;
                    }
                }
                //Debug.DrawLine(drawOffset + (Vector3)a.GetPositionAtT(times[0] - a.Epoch) * scale, drawOffset + (Vector3)b.GetPositionAtT(times[0] - b.Epoch) * scale, Color.blue, 0.5f);
                //Debug.DrawLine(drawOffset + (Vector3)a.GetPositionAtT(times[1] - a.Epoch) * scale, drawOffset + (Vector3)b.GetPositionAtT(times[1] - b.Epoch) * scale, Color.green, 0.5f);
                if (cycle++ > 30)
                {
                    Debug.Log("Can't solve");
                    return times[0];
                }
            }

            void IterateBinarySearch()
            {
                double tMid = (times[0] + times[1]) * 0.5;
                double dSqr = LengthSqr(a, b, tMid);
                if (dSqr < distances[0])
                {
                    delta = Math.Abs(times[0] - tMid);
                    distances[1] = distances[0];
                    times[1] = times[0];
                    distances[0] = dSqr;
                    times[0] = tMid;
                }
                else if (dSqr < distances[1])
                {
                    delta = Math.Abs(times[0] - tMid);
                    distances[1] = dSqr;
                    times[1] = tMid;
                }
                else
                {
                    times[1] = times[0] + (times[0] - times[1]) * 0.5;
                    distances[1] = LengthSqr(a, b, times[1]);
                }
            }

            double LengthSqr(IStaticTrajectory a, IStaticTrajectory b, double time)
            {
                var aa = a.GetPositionAtT(time - a.Epoch);
                var bb = b.GetPositionAtT(time - b.Epoch);
               // Debug.DrawLine(drawOffset + (Vector3)aa * scale, drawOffset + (Vector3)bb * scale, Color.cyan, 0.5f);
                return (aa - bb).LengthSquared();
            }
            return times[0];
        }

        //private static 
    }
}