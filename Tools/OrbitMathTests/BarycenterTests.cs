using System;
using System.Collections.Generic;
using Ara3D;
using NUnit.Framework;
using Orbital.Core.TrajectorySystem;

namespace Orbital.Tests
{
    /// <summary>
    /// Stage 1.5a: the center of mass of a double system (Pluto-Charon). m_A * a_A = m_B * a_B (linear in the masses),
    /// orbit nu = G * m_other^3 / (m_A + m_B)^2 so that both bodies turn with the common mean motion sqrt(G M / d^3).
    /// </summary>
    public class BarycenterTests
    {
        private const double G = MassUtility.G;
        // Pluto and Charon as in the scene (float32 masses), separation 19596 km (real semi-major axis of the Charon orbit around Pluto)
        private static readonly double MPluto = (float) 1.303e22, MCharon = (float) 1.586e21;
        private const double Separation = 19596000.0;
        private const double RealPeriod = 551857.0;   // s, rotation period of Pluto = orbital period of the pair (locked), approximate

        [Test]
        public void PartnerSemiMajorAxis_IsLinearInTheMassRatio()
        {
            double aPluto = Separation * MCharon / (MPluto + MCharon);
            double aCharon = TrajectoryUtility.GetBarycentricPartnerSemiMajorAxis(aPluto, MPluto, MCharon);
            Assert.That(aCharon, Is.EqualTo(Separation * MPluto / (MPluto + MCharon)).Within(1e-6));
            Assert.That(MPluto * aPluto, Is.EqualTo(MCharon * aCharon).Within(MPluto * aPluto * 1e-12), "center of mass: m1 a1 = m2 a2");
            Assert.That(aPluto + aCharon, Is.EqualTo(Separation).Within(1e-6));
            // the old cube-root rule gives a different ratio
            Assert.That(aCharon / aPluto, Is.EqualTo(MPluto / MCharon).Within(1e-9));
            Assert.That(aCharon / aPluto, Is.Not.EqualTo(Math.Pow(MPluto / MCharon, 1.0 / 3.0)).Within(1.0));
        }

        [Test]
        public void PartnerSemiMajorAxis_EqualMasses_EqualAxes_AndBadMassesThrow()
        {
            Assert.That(TrajectoryUtility.GetBarycentricPartnerSemiMajorAxis(1e7, 5e22, 5e22), Is.EqualTo(1e7));
            Assert.Throws<ArgumentOutOfRangeException>(() => TrajectoryUtility.GetBarycentricPartnerSemiMajorAxis(1e7, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => TrajectoryUtility.GetBarycentricOrbitNu(1, 1, -1));
        }

        [Test]
        public void OrbitNu_BothBodiesHaveTheMeanMotionOfTheRelativeOrbit()
        {
            double total = G * (MPluto + MCharon);
            double aPluto = Separation * MCharon / (MPluto + MCharon);
            double aCharon = Separation * MPluto / (MPluto + MCharon);
            double nuPluto = TrajectoryUtility.GetBarycentricOrbitNu(total, MPluto, MCharon);
            double nuCharon = TrajectoryUtility.GetBarycentricOrbitNu(total, MCharon, MPluto);
            double nRelative = Math.Sqrt(total / (Separation * Separation * Separation));
            Assert.That(Math.Sqrt(nuPluto / (aPluto * aPluto * aPluto)), Is.EqualTo(nRelative).Within(nRelative * 1e-12));
            Assert.That(Math.Sqrt(nuCharon / (aCharon * aCharon * aCharon)), Is.EqualTo(nRelative).Within(nRelative * 1e-12));
            Assert.That(nuPluto, Is.EqualTo(G * Math.Pow(MCharon, 3) / Math.Pow(MPluto + MCharon, 2)).Within(nuPluto * 1e-12));
            Assert.That(2 * Math.PI / nRelative, Is.EqualTo(RealPeriod).Within(RealPeriod * 1e-3), "the period matches the real one (6.387 days)");
        }

        private static StaticOrbit Circular(double nu, double a, double aopDeg)
        {
            StaticOrbit o = new StaticOrbit { Nu = nu };
            o.Calculate(new TrajectorySettings { mass = 1, eccentricity = 0, semiMajorAxis = (float) a, inclination = 0, argumentOfPeriapsis = (float) aopDeg, longitudeAscendingNode = 0, timeShift = 0 }, 0);
            return o;
        }

        private static (double[] pos, double[] vel) Sample(StaticOrbit o, double t)
        {
            var (p, v) = o.GetSample(t);
            return (new[] { p.x, p.y, p.z }, new[] { v.x, v.y, v.z });
        }

        [Test]
        public void BarycentricOrbits_MatchAnIndependentTwoBodyIntegration_OverOnePeriod()
        {
            double total = G * (MPluto + MCharon);
            double aPluto = Separation * MCharon / (MPluto + MCharon);
            double aCharon = Separation * MPluto / (MPluto + MCharon);
            StaticOrbit oP = Circular(TrajectoryUtility.GetBarycentricOrbitNu(total, MPluto, MCharon), aPluto, 0);
            StaticOrbit oC = Circular(TrajectoryUtility.GetBarycentricOrbitNu(total, MCharon, MPluto), aCharon, 180);
            // state vector: pluto pos(3), charon pos(3), pluto vel(3), charon vel(3), started from the static orbits
            double[] y = new double[12];
            var (pp, pv) = Sample(oP, 0);
            var (cp, cv) = Sample(oC, 0);
            for (int i = 0; i < 3; i++) { y[i] = pp[i]; y[3 + i] = cp[i]; y[6 + i] = pv[i]; y[9 + i] = cv[i]; }

            double[] Deriv(double[] s)
            {
                double[] d = new double[12];
                double dx = s[3] - s[0], dy = s[4] - s[1], dz = s[5] - s[2];
                double r = Math.Sqrt(dx * dx + dy * dy + dz * dz), k = G / (r * r * r);
                for (int i = 0; i < 3; i++)
                {
                    d[i] = s[6 + i]; d[3 + i] = s[9 + i];
                }
                d[6] = k * MCharon * dx; d[7] = k * MCharon * dy; d[8] = k * MCharon * dz;
                d[9] = -k * MPluto * dx; d[10] = -k * MPluto * dy; d[11] = -k * MPluto * dz;
                return d;
            }

            double[] Add(double[] a, double[] b, double f)
            {
                double[] r = new double[12];
                for (int i = 0; i < 12; i++) r[i] = a[i] + b[i] * f;
                return r;
            }

            double h = 5.0, t = 0, worst = 0;
            int steps = (int) (RealPeriod / h);
            for (int n = 1; n <= steps; n++)
            {
                double[] k1 = Deriv(y), k2 = Deriv(Add(y, k1, h / 2)), k3 = Deriv(Add(y, k2, h / 2)), k4 = Deriv(Add(y, k3, h));
                for (int i = 0; i < 12; i++) y[i] += h / 6 * (k1[i] + 2 * k2[i] + 2 * k3[i] + k4[i]);
                t += h;
                if (n % 5000 == 0 || n == steps)
                {
                    var (sp, _) = Sample(oP, t);
                    var (sc, _) = Sample(oC, t);
                    for (int i = 0; i < 3; i++)
                    {
                        worst = Math.Max(worst, Math.Abs(sp[i] - y[i]));
                        worst = Math.Max(worst, Math.Abs(sc[i] - y[3 + i]));
                    }
                }
            }
            Assert.Less(worst, 5.0, "static barycentric orbits follow the two-body motion within meters over a whole period (separation 2e7 m)");
            // barycenter stays at the origin
            double bx = (MPluto * y[0] + MCharon * y[3]) / (MPluto + MCharon);
            Assert.Less(Math.Abs(bx), 5.0);
        }

        // ------------------------------------------------------------------ the real classes: DoubleSystemBranch and MassUtility

        private static IMassSystem Body(double mass, double a, double aopDeg) => new CelestialBody
        {
            Settings = new TrajectorySettings { mass = (float) mass, eccentricity = 0, semiMajorAxis = (float) a, inclination = 0, argumentOfPeriapsis = (float) aopDeg, longitudeAscendingNode = 0, timeShift = 0 },
        };

        [Test]
        public void DoubleSystemBranch_Setter_UsesTheLinearMassRatio()
        {
            DoubleSystemBranch branch = new DoubleSystemBranch(Body(MPluto, 1, 0), Body(MCharon, 1, 0));
            branch.LocalTrajectorySettings = new DoubleSystemTrajectorySettings
            {
                aMass = (float) MPluto, bMass = (float) MCharon, eccentricity = 0, aPericenterRadius = 2126386f, timeShift = 0,
            };
            double aA = branch.ChildA.Settings.semiMajorAxis, aB = branch.ChildB.Settings.semiMajorAxis;
            Assert.That(aA * MPluto, Is.EqualTo(aB * MCharon).Within(aA * MPluto * 1e-6), "m_A a_A = m_B a_B");
            Assert.That(aB / aA, Is.EqualTo(MPluto / MCharon).Within(1e-5), "ratio is linear (8.2), not the cube root (2.0)");
        }

        private static (IMassSystem root, DoubleSystemBranch pair) PlutoCharonInTheSun()
        {
            double aPluto = Separation * MCharon / (MPluto + MCharon), aCharon = Separation * MPluto / (MPluto + MCharon);
            DoubleSystemBranch pair = new DoubleSystemBranch(Body(MPluto, aPluto, 0), Body(MCharon, aCharon, 180))
            {
                Settings = new TrajectorySettings { mass = (float) (MPluto + MCharon), eccentricity = 0.2488f, semiMajorAxis = 5.9064e12f, inclination = 17.14f, timeShift = 0 },
            };
            IMassSystem sun = new CelestialBody { Settings = new TrajectorySettings { mass = 1.98847e30f } };
            return (new SingleCenterBranch(sun, new List<IMassSystem> { pair }), pair);
        }

        [Test]
        public void FillTrajectories_GivesTheMembersOfADoubleSystemTheBarycentricNu()
        {
            var (root, pair) = PlutoCharonInTheSun();
            Dictionary<IMassSystem, StaticOrbit> orbits = new Dictionary<IMassSystem, StaticOrbit>();
            root.FillTrajectoriesRecursively(orbits, 0);
            double ma = pair.ChildA.Mass, mb = pair.ChildB.Mass;
            Assert.That(orbits[pair.ChildA].Nu, Is.EqualTo(G * Math.Pow(mb, 3) / Math.Pow(ma + mb, 2)).Within(orbits[pair.ChildA].Nu * 1e-12));
            Assert.That(orbits[pair.ChildB].Nu, Is.EqualTo(G * Math.Pow(ma, 3) / Math.Pow(ma + mb, 2)).Within(orbits[pair.ChildB].Nu * 1e-12));
            // the system itself orbits the central mass of its parent as any other child
            Assert.That(orbits[pair].Nu, Is.EqualTo(root.GetContent().GetEnumerator().Current == null ? orbits[pair].Nu : orbits[pair].Nu));
            Assert.That(orbits[pair].Nu, Is.EqualTo(1.98847e30f * G).Within(1e-3 * G * 1e30));
        }

        [Test]
        public void PlutoCharon_InTheTree_HasTheRealSeparation_AndABarycenterBetweenAndOutsideTheBodies()
        {
            var (root, pair) = PlutoCharonInTheSun();
            Dictionary<IMassSystem, StaticOrbit> orbits = new Dictionary<IMassSystem, StaticOrbit>();
            root.FillTrajectoriesRecursively(orbits, 0);
            const double plutoRadius = 1188300, charonRadius = 606000;
            foreach (double t in new[] { 0.0, 1e5, 3.3e5, 1e7 })
            {
                var (pp, _) = orbits[pair.ChildA].GetSample(t);
                var (cp, _) = orbits[pair.ChildB].GetSample(t);
                double d = (cp - pp).Length();
                Assert.That(d, Is.EqualTo(1.96e7).Within(1.96e7 * 1e-3), "distance between the bodies, real 1.96e7 m, t=" + t);
                Assert.That(pp.Length() + cp.Length(), Is.EqualTo(d).Within(1.0), "the barycenter lies on the segment between them");
                DVector3 weighted = pp * pair.ChildA.Mass + cp * pair.ChildB.Mass;
                // float32 rounding of the data (a and mass, 1e-7) shifts the two mean motions slightly: the phase drifts ~1e-5 rad by t = 1e7 s
                Assert.That(weighted.Length(), Is.LessThan(5e-5 * pair.ChildA.Mass * pp.Length()), "mass-weighted position is zero (the center of mass), t=" + t);
                Assert.Greater(pp.Length(), plutoRadius, "the barycenter is outside Pluto");
                Assert.Greater(cp.Length(), charonRadius, "and outside Charon");
            }
            double period = orbits[pair.ChildA].Period;
            Assert.That(period, Is.EqualTo(RealPeriod).Within(RealPeriod * 1e-3));
            Assert.That(orbits[pair.ChildB].Period, Is.EqualTo(period).Within(period * 1e-6), "one period for both (float32 data: ~1e-7)");
        }
    }
}
