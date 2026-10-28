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
            // body.Radius comes from the body table (PhysicalFacet) through World/WorldSceneBinder; 0 (no record, barycenter) means no minimum.
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
            void SetupElement(IMassSystem child, IMassSystem other, bool barycentric = false)
            {
                if (child == null) return;
                if (other == null) return;
                if (!container.TryGetValue(child, out StaticOrbit orbit))
                {
                    orbit = new StaticOrbit();
                    // members of a double system orbit its barycenter: nu = G * m_other^3 / (m_child + m_other)^2 (mean motion common to both);
                    // an ordinary child orbits the whole mass of its parent
                    orbit.Nu = barycentric
                        ? TrajectoryUtility.GetBarycentricOrbitNu((child.Mass + other.Mass) * G, child.Mass, other.Mass)
                        : other.Mass * G;
                    container.Add(child, orbit);
                }

                orbit.Calculate(child.Settings, time);
                container[child] = orbit;
                FillTrajectoriesRecursively(child, container, time);
            }

            if (massSystem is DoubleSystemBranch doubleSystemBranch)
            {
                SetupElement(doubleSystemBranch.ChildA, doubleSystemBranch.ChildB, true);
                SetupElement(doubleSystemBranch.ChildB, doubleSystemBranch.ChildA, true);
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