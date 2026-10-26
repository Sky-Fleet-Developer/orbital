using System;
using NUnit.Framework;
using Orbital.Core.TrajectorySystem;
using static Orbital.Tests.OrbitTestUtility;

namespace Orbital.Tests
{
    public class KeplerSolverTests
    {
        private static readonly double[] EllipticEccentricities = { 0, 1e-6, 0.1, 0.5, 0.79, 0.8, 0.9, 0.99, 0.999 };
        private static readonly double[] HyperbolicEccentricities = { 1.001, 1.1, 2, 5, 20 };
        private static readonly double[] MeanAnomalies =
            { -50, -3, -0.1, 0, 1e-6, 0.5, Math.PI - 1e-6, Math.PI, 6, 100, 1000 };

        [Test]
        public void Elliptic_SolutionSatisfiesKeplerEquation([ValueSource(nameof(EllipticEccentricities))] double e,
            [ValueSource(nameof(MeanAnomalies))] double m)
        {
            StaticOrbit orbit = Make(0, e, 7e6, 0, 0, 0);
            double ea = orbit.SolveEccentricAnomaly(m, e);
            Assert.IsFalse(double.IsNaN(ea), $"NaN for e={e} M={m}");
            Assert.AreEqual(m, ea - e * Math.Sin(ea), 1e-9, $"e={e} M={m} E={ea:R}");
        }

        [Test]
        public void Hyperbolic_SolutionSatisfiesKeplerEquation([ValueSource(nameof(HyperbolicEccentricities))] double e,
            [ValueSource(nameof(MeanAnomalies))] double m)
        {
            StaticOrbit orbit = Make(0, e, -7e6, 0, 0, 0);
            double h = orbit.SolveEccentricAnomaly(m, e);
            Assert.IsFalse(double.IsNaN(h), $"NaN for e={e} M={m}");
            Assert.AreEqual(m, e * Math.Sinh(h) - h, 1e-9 * Math.Max(1, Math.Abs(m)), $"e={e} M={m} H={h:R}");
        }

        [Test]
        public void Elliptic_TrueAndEccentricAnomalyRoundTrip([ValueSource(nameof(EllipticEccentricities))] double e)
        {
            if (e == 0) Assert.Pass("circular: anomalies coincide");
            StaticOrbit orbit = Make(0, e, 7e6, 0, 0, 0);
            for (double nu = -3.1; nu < 3.1; nu += 0.2)
            {
                double back = orbit.GetTrueAnomaly(orbit.GetEccentricAnomaly(nu));
                AssertAngleNear(nu, back, 1e-9, $"e={e}");
            }
        }

        [Test]
        public void Hyperbolic_TrueAndEccentricAnomalyRoundTrip([ValueSource(nameof(HyperbolicEccentricities))] double e)
        {
            StaticOrbit orbit = Make(0, e, -7e6, 0, 0, 0);
            double limit = Math.Acos(-1.0 / e) - 0.05;
            for (double nu = -limit; nu < limit; nu += limit / 10)
            {
                double back = orbit.GetTrueAnomaly(orbit.GetEccentricAnomaly(nu));
                Assert.AreEqual(nu, back, 1e-9, $"e={e}");
            }
        }

        [Test]
        public void Elliptic_MeanAnomalyFromEccentricMatchesKepler([ValueSource(nameof(EllipticEccentricities))] double e)
        {
            StaticOrbit orbit = Make(0, e, 7e6, 0, 0, 0);
            for (double ea = 0.1; ea < 6.2; ea += 0.3)
                Assert.AreEqual(ea - e * Math.Sin(ea), orbit.GetMeanAnomaly(ea), 1e-12);
        }
    }
}
