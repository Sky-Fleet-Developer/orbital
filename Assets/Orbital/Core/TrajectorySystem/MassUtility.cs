using System;
using System.Collections.Generic;

namespace Orbital.Core.TrajectorySystem
{
    public static partial class MassUtility
    {
        public const double G = 6.67430e-11;
        private const double GravityEdgeInv = 500;
        public static double GetGravityRadius(double Nu)
        {
            return Math.Sqrt(Nu * GravityEdgeInv);
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

        /*public static Dictionary<IMass, StaticOrbit> MakeTrajectories(this IMass mRoot)
        {
            Dictionary<IMass, StaticOrbit> result = new();
            FillTrajectoriesRecursively(result, mRoot);
            return result;
        }*/

        public static void FillTrajectoriesRecursively(this IMassSystem massSystem, Dictionary<IMassSystem, StaticOrbit> container, double time)
        {
            void SetupElement(IMassSystem child, IMassSystem other)
            {
                if (child == null) return;
                if (other == null) return;
                if (!container.TryGetValue(child, out StaticOrbit orbit))
                {
                    orbit = new StaticOrbit();
                    orbit.Nu = other.Mass * G;
                    container.Add(child, orbit);
                }

                orbit.Calculate(child.Settings, time);
                container[child] = orbit;
                FillTrajectoriesRecursively(child, container, time);
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
    }
}