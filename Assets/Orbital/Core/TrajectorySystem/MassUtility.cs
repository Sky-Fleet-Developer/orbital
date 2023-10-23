using System;
using System.Collections.Generic;
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

                trajectory.Calculate(child.Settings);
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
            
        }*/
    }
}