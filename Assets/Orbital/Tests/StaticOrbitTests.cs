using System;
using Ara3D;
using NUnit.Framework;
using Orbital.Core.TrajectorySystem;
using static Orbital.Tests.OrbitTestUtility;

namespace Orbital.Tests
{
    public class StaticOrbitTests
    {
        public struct Case
        {
            public string Name;
            public double Inc, Ecc, Periapsis, Lan, Aop, M0;

            public double Sma => SemiMajorFromPeriapsis(Periapsis, Ecc);
            public StaticOrbit Orbit(double epoch = 0) => Make(Inc, Ecc, Sma, Lan, Aop, M0, epoch);
            public override string ToString() => Name;
        }

        public static readonly Case[] Elliptic =
        {
            new Case { Name = "circular-equatorial", Inc = 0, Ecc = 0, Periapsis = 7e6, Lan = 0, Aop = 0, M0 = 0 },
            new Case { Name = "low-ecc-inclined", Inc = 0.5, Ecc = 0.05, Periapsis = 7e6, Lan = 1.0, Aop = 2.0, M0 = 0.3 },
            new Case { Name = "generic", Inc = 0.9, Ecc = 0.3, Periapsis = 8e6, Lan = 2.5, Aop = 4.0, M0 = 1.7 },
            new Case { Name = "polar", Inc = Math.PI / 2, Ecc = 0.2, Periapsis = 7e6, Lan = 0.7, Aop = 1.1, M0 = 5.0 },
            new Case { Name = "retrograde", Inc = 2.6, Ecc = 0.4, Periapsis = 9e6, Lan = 5.0, Aop = 0.4, M0 = 2.2 },
            new Case { Name = "high-ecc", Inc = 0.3, Ecc = 0.9, Periapsis = 7e6, Lan = 3.0, Aop = 5.5, M0 = 3.5 },
            new Case { Name = "very-high-ecc", Inc = 1.2, Ecc = 0.98, Periapsis = 7e6, Lan = 0.2, Aop = 2.2, M0 = 0.1 },
        };

        public static readonly Case[] Hyperbolic =
        {
            new Case { Name = "hyp-e1.2", Inc = 0.4, Ecc = 1.2, Periapsis = 7e6, Lan = 1.0, Aop = 2.0, M0 = -0.5 },
            new Case { Name = "hyp-e3", Inc = 1.0, Ecc = 3.0, Periapsis = 7e6, Lan = 4.0, Aop = 1.0, M0 = 0.8 },
        };

        public static readonly Case[] All = Concat(Elliptic, Hyperbolic);

        private static Case[] Concat(Case[] a, Case[] b)
        {
            Case[] result = new Case[a.Length + b.Length];
            a.CopyTo(result, 0);
            b.CopyTo(result, a.Length);
            return result;
        }

        private static double PositionTolerance(Case c) => Math.Abs(c.Sma) * 1e-9;

        // ---------------------------------------------------------------- units

        [Test]
        public void DegreesToRadians_IsExact()
        {
            StaticOrbit orbit = new StaticOrbit { Nu = Mu };
            TrajectorySettings settings = new TrajectorySettings
                { eccentricity = 0.1f, semiMajorAxis = 7e6f, inclination = 180, argumentOfPeriapsis = 90, longitudeAscendingNode = 45 };
            orbit.Calculate(settings, 0);
            Assert.AreEqual(Math.PI, orbit.Inclination, 1e-12);
            Assert.AreEqual(Math.PI / 2, orbit.ArgumentOfPeriapsis, 1e-12);
            Assert.AreEqual(Math.PI / 4, orbit.LongitudeAscendingNode, 1e-12);
        }

        // ---------------------------------------------------------------- analytic invariants

        [Test]
        public void Period_MatchesKeplersThirdLaw([ValueSource(nameof(Elliptic))] Case c)
        {
            StaticOrbit orbit = c.Orbit();
            double expected = 2 * Math.PI * Math.Sqrt(c.Sma * c.Sma * c.Sma / Mu);
            Assert.AreEqual(expected, orbit.Period, expected * 1e-12);
        }

        [Test]
        public void Position_RepeatsAfterOnePeriod([ValueSource(nameof(Elliptic))] Case c)
        {
            StaticOrbit orbit = c.Orbit();
            foreach (double t in new[] { 0.0, 1234.5, 5000.0 })
                AssertVectorNear(orbit.GetPositionAtT(t), orbit.GetPositionAtT(t + orbit.Period), PositionTolerance(c), c.Name);
        }

        [Test]
        public void Position_AtEpoch_FollowsMeanAnomalyAtEpoch([ValueSource(nameof(All))] Case c)
        {
            StaticOrbit orbit = c.Orbit(epoch: 1000);
            double ea = orbit.SolveEccentricAnomaly(c.M0, c.Ecc);
            AssertVectorNear(orbit.GetPositionFromEccAnomaly(ea), orbit.GetPositionAtT(1000), PositionTolerance(c), c.Name);
        }

        [Test]
        public void Pericenter_ReachedWhenMeanAnomalyIsZero([ValueSource(nameof(All))] Case c)
        {
            Case atPe = c;
            atPe.M0 = 0;
            StaticOrbit orbit = atPe.Orbit();
            Assert.AreEqual(c.Periapsis, orbit.GetPositionAtT(0).Length(), c.Periapsis * 1e-9, c.Name);
        }

        [Test]
        public void StateVectors_ConserveEnergyAndAngularMomentum([ValueSource(nameof(All))] Case c)
        {
            StaticOrbit orbit = c.Orbit();
            double expectedEnergy = -Mu / (2 * c.Sma);
            double expectedH = Math.Sqrt(Mu * c.Sma * (1 - c.Ecc * c.Ecc));
            bool open = double.IsInfinity(orbit.Period);
            double span = open ? 20000 : orbit.Period * 3;
            for (int i = 0; i <= 60; i++)
            {
                double t = span * i / 60.0 - (open ? span / 2 : 0);
                var (pos, vel) = orbit.GetSample(t);
                Assert.AreEqual(expectedEnergy, Energy(pos, vel, Mu), Math.Abs(expectedEnergy) * 1e-9, $"{c.Name} t={t}");
                Assert.AreEqual(expectedH, AngularMomentum(pos, vel).Length(), expectedH * 1e-9, $"{c.Name} t={t}");
            }
        }

        [Test]
        public void StateVectors_PositionMatchesPositionAtT([ValueSource(nameof(All))] Case c)
        {
            StaticOrbit orbit = c.Orbit();
            for (double t = -1500; t <= 1500; t += 250)
            {
                var (pos, _) = orbit.GetSample(t);
                AssertVectorNear(orbit.GetPositionAtT(t), pos, PositionTolerance(c), $"{c.Name} t={t}");
            }
        }

        [Test]
        public void StateVectors_VelocityIsDerivativeOfPosition([ValueSource(nameof(All))] Case c)
        {
            StaticOrbit orbit = c.Orbit();
            const double h = 0.01;
            for (double t = -1500; t <= 1500; t += 500)
            {
                DVector3 numeric = (orbit.GetPositionAtT(t + h) - orbit.GetPositionAtT(t - h)) / (2 * h);
                var (_, vel) = orbit.GetSample(t);
                AssertVectorNear(numeric, vel, 1e-3, $"{c.Name} t={t}");
            }
        }

        [Test]
        public void StateVectors_AgreeWithIndependentNumericIntegration([ValueSource(nameof(All))] Case c)
        {
            StaticOrbit orbit = c.Orbit();
            var (p0, v0) = orbit.GetSample(0);
            double duration = double.IsInfinity(orbit.Period) ? 3000 : Math.Min(orbit.Period * 0.75, 6000);
            PropagateRk4(p0, v0, Mu, duration, 0.25, out DVector3 p1, out DVector3 v1);
            var (pa, va) = orbit.GetSample(duration);
            AssertVectorNear(p1, pa, 1.0, c.Name + " position");
            AssertVectorNear(v1, va, 1e-3, c.Name + " velocity");
        }

        // ---------------------------------------------------------------- state vectors -> elements

        [Test]
        public void Calculate_FromStateAtEpoch_ReproducesState([ValueSource(nameof(All))] Case c)
        {
            double epoch = 4321;
            var (pos, vel) = c.Orbit().GetSample(epoch);

            StaticOrbit rebuilt = new StaticOrbit { Nu = Mu };
            rebuilt.Calculate(pos, vel, epoch);

            var (pos2, vel2) = rebuilt.GetSample(epoch);
            AssertVectorNear(pos, pos2, PositionTolerance(c), c.Name + " position");
            AssertVectorNear(vel, vel2, 1e-6, c.Name + " velocity");
        }

        [Test]
        public void Calculate_FromStateAtEpoch_PropagatesLikeSource([ValueSource(nameof(All))] Case c)
        {
            double epoch = 4321;
            StaticOrbit source = c.Orbit();
            var (pos, vel) = source.GetSample(epoch);

            StaticOrbit rebuilt = new StaticOrbit { Nu = Mu };
            rebuilt.Calculate(pos, vel, epoch);

            foreach (double dt in new[] { 100.0, 1000.0, 2500.0, -700.0 })
            {
                var (pa, va) = source.GetSample(epoch + dt);
                var (pb, vb) = rebuilt.GetSample(epoch + dt);
                AssertVectorNear(pa, pb, 1e-3, $"{c.Name} dt={dt} position");
                AssertVectorNear(va, vb, 1e-6, $"{c.Name} dt={dt} velocity");
            }
        }

        [Test]
        public void Calculate_FromState_RecoversShapeElements([ValueSource(nameof(All))] Case c)
        {
            var (pos, vel) = c.Orbit().GetSample(777);
            StaticOrbit rebuilt = new StaticOrbit { Nu = Mu };
            rebuilt.Calculate(pos, vel, 777);
            Assert.AreEqual(c.Sma, rebuilt.SemiMajorAxis, Math.Abs(c.Sma) * 1e-9, c.Name + " a");
            Assert.AreEqual(c.Ecc, rebuilt.Eccentricity, 1e-9, c.Name + " e");
            Assert.AreEqual(Energy(pos, vel, Mu), rebuilt.OrbitalEnergy, Math.Abs(rebuilt.OrbitalEnergy) * 1e-9, c.Name + " energy");
        }

        [Test]
        public void Calculate_FromState_RecoversOrientation([ValueSource(nameof(Elliptic))] Case c)
        {
            if (c.Ecc == 0 || c.Inc == 0) Assert.Pass("orientation angle undefined for this geometry");
            var (pos, vel) = c.Orbit().GetSample(777);
            StaticOrbit rebuilt = new StaticOrbit { Nu = Mu };
            rebuilt.Calculate(pos, vel, 777);
            AssertAngleNear(c.Inc, rebuilt.Inclination, 1e-8, c.Name + " inclination");
            AssertAngleNear(c.Lan, rebuilt.LongitudeAscendingNode, 1e-8, c.Name + " LAN");
            AssertAngleNear(c.Aop, rebuilt.ArgumentOfPeriapsis, 1e-8, c.Name + " AoP");
        }

        [Test]
        public void Calculate_FromState_RecoversTrueAnomaly([ValueSource(nameof(Elliptic))] Case c)
        {
            if (c.Ecc == 0) Assert.Pass("true anomaly of a circle is arbitrary");
            StaticOrbit source = c.Orbit();
            double expectedNu = source.GetTrueAnomaly(source.SolveEccentricAnomaly(c.M0, c.Ecc));
            var (pos, vel) = source.GetSample(0);
            StaticOrbit rebuilt = new StaticOrbit { Nu = Mu };
            rebuilt.Calculate(pos, vel, 0);
            AssertAngleNear(expectedNu, rebuilt.TrueAnomaly, 1e-8, c.Name);
        }

        // ---------------------------------------------------------------- degenerate geometry

        [Test]
        public void Calculate_FromState_EquatorialOrbit_ProducesFiniteElements()
        {
            StaticOrbit source = Make(0, 0.2, 7e6, 0, 0.7, 1.0);
            var (pos, vel) = source.GetSample(0);
            StaticOrbit rebuilt = new StaticOrbit { Nu = Mu };
            rebuilt.Calculate(pos, vel, 0);
            Assert.IsFalse(double.IsNaN(rebuilt.ArgumentOfPeriapsis), "AoP is NaN");
            Assert.IsFalse(double.IsNaN(rebuilt.LongitudeAscendingNode), "LAN is NaN");
            var (p2, v2) = rebuilt.GetSample(1500);
            var (p3, v3) = source.GetSample(1500);
            AssertVectorNear(p3, p2, 1e-3, "position");
            AssertVectorNear(v3, v2, 1e-6, "velocity");
        }

        [Test]
        public void Calculate_FromState_CircularInclinedOrbit_PropagatesLikeSource()
        {
            StaticOrbit source = Make(0.7, 0, 7e6, 1.2, 0, 2.0);
            var (pos, vel) = source.GetSample(0);
            StaticOrbit rebuilt = new StaticOrbit { Nu = Mu };
            rebuilt.Calculate(pos, vel, 0);
            var (p2, _) = rebuilt.GetSample(1500);
            var (p3, _) = source.GetSample(1500);
            AssertVectorNear(p3, p2, 1e-3);
        }

        // ---------------------------------------------------------------- equality

        [Test]
        public void Equals_DistinguishesDifferentOrbits()
        {
            StaticOrbit a = Make(0.5, 0.3, 8e6, 1, 2, 0);
            StaticOrbit b = Make(0.5 + 1e-3, 0.3 - 1e-3, 8e6, 1, 2, 0);
            Assert.IsFalse(a.Equals(b));
        }

        [Test]
        public void Equals_IsConsistentWithHashCode()
        {
            StaticOrbit a = Make(0.5, 0.3, 8e6, 1, 2, 0);
            StaticOrbit b = Make(0.5, 0.3, 8e6, 1, 2, 0);
            Assert.IsTrue(a.Equals(b));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }
    }
}
