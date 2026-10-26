using System;
using System.Threading.Tasks;
using Ara3D;
using NUnit.Framework;
using Orbital.Core.TrajectorySystem;
using static Orbital.Tests.OrbitTestUtility;
using Case = Orbital.Tests.StaticOrbitTests.Case;

namespace Orbital.Tests
{
    /// <summary>Conventions, derived fields and long-run behaviour of <see cref="StaticOrbit"/>.</summary>
    public class OrbitDerivedTests
    {
        private static readonly Case[] All = StaticOrbitTests.All;
        private static readonly Case[] Elliptic = StaticOrbitTests.Elliptic;
        private static readonly Case[] Hyperbolic = StaticOrbitTests.Hyperbolic;

        private static double PositionTolerance(Case c) => Math.Abs(c.Sma) * 1e-9;

        // ---------------------------------------------------------------- conventions

        [Test]
        public void H_IsPositionCrossVelocity([ValueSource(nameof(All))] Case c)
        {
            StaticOrbit orbit = c.Orbit();
            var (pos, vel) = orbit.GetSample(321);
            AssertVectorNear(AngularMomentum(pos, vel), orbit.H, orbit.H.Length() * 1e-9, c.Name + " SetOrbit");

            StaticOrbit rebuilt = new StaticOrbit { Nu = Mu };
            rebuilt.Calculate(pos, vel, 321);
            AssertVectorNear(AngularMomentum(pos, vel), rebuilt.H, orbit.H.Length() * 1e-9, c.Name + " Calculate");
        }

        [Test]
        public void Inclination_IsAngleBetweenOrbitNormalAndWorldUp([ValueSource(nameof(All))] Case c)
        {
            StaticOrbit orbit = c.Orbit();
            var (pos, vel) = orbit.GetSample(111);
            DVector3 normal = AngularMomentum(pos, vel).Normalize();
            Assert.AreEqual(Math.Cos(c.Inc), DVector3.Dot(normal, DVector3.up), 1e-9, c.Name);
        }

        [Test]
        public void LongitudeOfAscendingNode_IsDirectionOfAscendingCrossing([ValueSource(nameof(All))] Case c)
        {
            if (c.Inc == 0) Assert.Pass("no node for an equatorial orbit");
            StaticOrbit orbit = c.Orbit();
            bool open = c.Ecc >= 1;
            double span = open ? 40000 : orbit.Period;
            double start = open ? -span / 2 : 0;
            int steps = 20000;
            for (int i = 0; i < steps; i++)
            {
                double t0 = start + span * i / steps, t1 = start + span * (i + 1) / steps;
                var (p0, _) = orbit.GetSample(t0);
                var (p1, _) = orbit.GetSample(t1);
                if (p0.y < 0 && p1.y >= 0)
                {
                    double f = -p0.y / (p1.y - p0.y);
                    DVector3 node = p0 + (p1 - p0) * f;
                    double lan = Math.Atan2(node.x, node.z);
                    AssertAngleNear(c.Lan, lan, 1e-3, c.Name);
                    return;
                }
            }

            Assert.Fail(c.Name + ": no ascending crossing found");
        }

        [Test]
        public void ArgumentOfPeriapsis_IsAngleFromAscendingNodeToPeriapsis([ValueSource(nameof(All))] Case c)
        {
            if (c.Inc == 0) Assert.Pass("no node for an equatorial orbit");
            Case atPe = c;
            atPe.M0 = 0;
            StaticOrbit orbit = atPe.Orbit();
            DVector3 periapsis = orbit.GetPositionAtT(0).Normalize();
            DVector3 node = orbit.AscendingNode.Normalize();
            DVector3 normal = orbit.H.Normalize();
            double angle = Math.Atan2(DVector3.Dot(DVector3.Cross(node, periapsis), normal), DVector3.Dot(node, periapsis));
            AssertAngleNear(c.Aop, angle, 1e-9, c.Name);
        }

        // ---------------------------------------------------------------- derived fields

        [Test]
        public void DerivedFields_SetOrbitAndCalculateAgree([ValueSource(nameof(All))] Case c)
        {
            if (c.Ecc == 0 || c.Inc == 0) Assert.Pass("anomalies are not defined for this geometry");
            double epoch = 555;
            StaticOrbit a = c.Orbit(epoch);
            var (pos, vel) = a.GetSample(epoch);
            StaticOrbit b = new StaticOrbit { Nu = Mu };
            b.Calculate(pos, vel, epoch);

            Assert.AreEqual(b.Radius, a.Radius, b.Radius * 1e-9, "Radius");
            Assert.AreEqual(b.OrbitalEnergy, a.OrbitalEnergy, Math.Abs(b.OrbitalEnergy) * 1e-9, "OrbitalEnergy");
            AssertAngleNear(b.TrueAnomaly, a.TrueAnomaly, 1e-8, "TrueAnomaly");
            if (c.Ecc < 1)
            {
                AssertAngleNear(b.EccentricAnomaly, a.EccentricAnomaly, 1e-8, "EccentricAnomaly");
                Assert.AreEqual(b.OrbitPercent, a.OrbitPercent, 1e-8, "OrbitPercent");
                Assert.AreEqual(b.TimeToPe, a.TimeToPe, b.Period * 1e-8, "TimeToPe");
                Assert.AreEqual(b.TimeToAp, a.TimeToAp, b.Period * 1e-8, "TimeToAp");
            }
            else
            {
                Assert.AreEqual(b.EccentricAnomaly, a.EccentricAnomaly, 1e-8, "EccentricAnomaly");
                Assert.AreEqual(b.TimeToPe, a.TimeToPe, Math.Abs(b.TimeToPe) * 1e-8 + 1e-6, "TimeToPe");
            }
        }

        [Test]
        public void TimeToPeriapsisAndApoapsis_PointAtThoseRadii([ValueSource(nameof(Elliptic))] Case c)
        {
            if (c.Ecc == 0 || c.Inc == 0) Assert.Pass("no distinguished apsides");
            double epoch = 900;
            StaticOrbit orbit = c.Orbit(epoch);
            var (pos, vel) = orbit.GetSample(epoch);
            StaticOrbit rebuilt = new StaticOrbit { Nu = Mu };
            rebuilt.Calculate(pos, vel, epoch);
            Assert.GreaterOrEqual(rebuilt.TimeToPe, 0);
            Assert.Less(rebuilt.TimeToPe, rebuilt.Period);
            Assert.AreEqual(c.Periapsis, rebuilt.GetPositionAtT(epoch + rebuilt.TimeToPe).Length(), c.Periapsis * 1e-8, "periapsis");
            double apoapsis = (1 + c.Ecc) * c.Sma;
            Assert.AreEqual(apoapsis, rebuilt.GetPositionAtT(epoch + rebuilt.TimeToAp).Length(), apoapsis * 1e-8, "apoapsis");
        }

        // ---------------------------------------------------------------- lookups by radius, anomaly, time

        [Test]
        public void TrueAnomalyAtRadius_GivesPointAtThatRadius([ValueSource(nameof(All))] Case c)
        {
            if (c.Ecc == 0) Assert.Pass("every point of a circle has the same radius");
            StaticOrbit orbit = c.Orbit();
            double rMax = c.Ecc < 1 ? (1 + c.Ecc) * c.Sma : c.Periapsis * 20;
            foreach (double f in new[] { 0.02, 0.25, 0.5, 0.75, 0.98 })
            {
                double r = c.Periapsis + (rMax - c.Periapsis) * f;
                double nu = orbit.TrueAnomalyAtRadius(r, 0);
                Assert.AreEqual(r, orbit.GetPositionFromTrueAnomaly(nu).Length(), r * 1e-9, $"{c.Name} r={r}");
                double nuSimple = orbit.TrueAnomalyAtRadiusSimple(r);
                Assert.AreEqual(r, orbit.GetPositionFromTrueAnomaly(nuSimple).Length(), r * 1e-9, $"{c.Name} simple r={r}");
            }
        }

        [Test]
        public void TimeOfTrueAnomaly_IsWhenOrbitReachesThatAnomaly([ValueSource(nameof(Elliptic))] Case c)
        {
            if (c.Ecc == 0 || c.Inc == 0) Assert.Pass("true anomaly is arbitrary for this geometry");
            StaticOrbit orbit = c.Orbit();
            foreach (double nu in new[] { 0.3, 1.5, 2.9, -2.0, -0.4 })
            {
                double now = 777;
                double t = orbit.TimeOfTrueAnomaly(nu, now);
                Assert.GreaterOrEqual(t, now - 1e-6, $"{c.Name} nu={nu} not in the future");
                Assert.Less(t, now + orbit.Period + 1e-6, $"{c.Name} nu={nu} more than a period away");
                AssertVectorNear(orbit.GetPositionFromTrueAnomaly(nu), orbit.GetPositionAtT(t), PositionTolerance(c) * 10, $"{c.Name} nu={nu}");
            }
        }

        [Test]
        public void OrbitalSpeed_MatchesVisViva([ValueSource(nameof(All))] Case c)
        {
            StaticOrbit orbit = c.Orbit();
            for (double t = -1500; t <= 1500; t += 500)
            {
                var (pos, vel) = orbit.GetSample(t);
                Assert.AreEqual(vel.Length(), orbit.GetOrbitalSpeedAtPos(pos), vel.Length() * 1e-9, $"{c.Name} t={t}");
            }
        }

        // ---------------------------------------------------------------- long runs

        [Test]
        public void LongRun_PositionAfterManyPeriodsMatchesFirstPeriod([ValueSource(nameof(Elliptic))] Case c)
        {
            StaticOrbit orbit = c.Orbit();
            const double periods = 100000;
            foreach (double t in new[] { 0.0, 1111.0, 3333.0 })
                AssertVectorNear(orbit.GetPositionAtT(t), orbit.GetPositionAtT(t + orbit.Period * periods), c.Sma * 1e-6, c.Name);
        }

        // ---------------------------------------------------------------- solver robustness

        [Test]
        public void Solver_RandomSweep_SatisfiesKeplerEquation()
        {
            Random random = new Random(12345);
            for (int i = 0; i < 4000; i++)
            {
                double e = random.NextDouble() < 0.5 ? random.NextDouble() : 1 - Math.Pow(10, -1 - random.NextDouble() * 6);
                double m = (random.NextDouble() - 0.5) * 2 * Math.PI * Math.Pow(10, random.Next(0, 4));
                StaticOrbit orbit = Make(0, e, 7e6, 0, 0, 0);
                double ea = orbit.SolveEccentricAnomaly(m, e);
                Assert.AreEqual(m, ea - e * Math.Sin(ea), 1e-8 * Math.Max(1, Math.Abs(m) * 1e-3), $"e={e:R} M={m:R} E={ea:R}");
            }

            for (int i = 0; i < 2000; i++)
            {
                double e = 1.0005 + random.NextDouble() * 10;
                double m = (random.NextDouble() - 0.5) * 2 * Math.Pow(10, random.Next(-3, 4));
                StaticOrbit orbit = Make(0, e, -7e6, 0, 0, 0);
                double h = orbit.SolveEccentricAnomaly(m, e);
                Assert.AreEqual(m, e * Math.Sinh(h) - h, 1e-8 * Math.Max(1, Math.Abs(m)), $"e={e:R} M={m:R} H={h:R}");
            }
        }

        [TestCase(0.3)]
        [TestCase(0.95)]
        [TestCase(1.5)]
        public void Solver_Terminates_ForHugeMeanAnomaly(double e)
        {
            StaticOrbit orbit = Make(0, e, e < 1 ? 7e6 : -7e6, 0, 0, 0);
            double result = double.NaN;
            Task task = Task.Run(() => result = orbit.SolveEccentricAnomaly(1e17, e));
            Assert.IsTrue(task.Wait(3000), "solver did not terminate");
            Assert.IsFalse(double.IsNaN(result));
        }
    }
}
