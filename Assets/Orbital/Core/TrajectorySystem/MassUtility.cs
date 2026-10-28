using System;
using System.Collections.Generic;
using Orbital.Core;

namespace Orbital.Core.TrajectorySystem
{
    public static partial class MassUtility
    {
        public const double G = 6.67430e-11;
        /// <summary>
        /// Sphere of influence of a body around its parent: Laplace r = a * (m / M)^(2/5), not less than Radius * k. Infinite for the root body.
        /// The only implementation is <see cref="TrajectoryUtility.GetSphereOfInfluenceRadius"/>; masses enter as gravitational parameters (ratio is the same).
        /// </summary>
        public static double GetSphereOfInfluenceRadius(IStaticBody body, double minRadiusFactor = TrajectoryUtility.DefaultMinRadiusFactor)
        {
            if (body.Parent == null) return double.PositiveInfinity;
            // body.Radius is a stub (1) until a real radius reaches Core; see Docs/GameplayPrototype.md, stage 1.3.
            return TrajectoryUtility.GetSphereOfInfluenceRadius(body.Orbit.SemiMajorAxis, body.GravParameter, body.Parent.GravParameter, body.Radius, minRadiusFactor);
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