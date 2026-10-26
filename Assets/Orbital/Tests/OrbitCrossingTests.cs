using System;
using Ara3D;
using NUnit.Framework;
using Orbital.Core.Navigation;
using Orbital.Core.TrajectorySystem;
using static Orbital.Tests.OrbitTestUtility;
using Case = Orbital.Tests.StaticOrbitTests.Case;

namespace Orbital.Tests
{
    public class OrbitCrossingTests
    {
        private const double MoonMu = 4.9048695e12;
        private const double MoonSphere = 6.6e7;

        private static readonly Case[] Hyperbolic = StaticOrbitTests.Hyperbolic;

        private static readonly Case[] Elliptic =
        {
            new Case { Name = "ell-a", Inc = 0.5, Ecc = 0.5, Periapsis = 7e6, Lan = 1.0, Aop = 2.0, M0 = 0.3 },
            new Case { Name = "ell-b", Inc = 2.6, Ecc = 0.8, Periapsis = 9e6, Lan = 5.0, Aop = 0.4, M0 = 2.2 },
            new Case { Name = "ell-c", Inc = 0.2, Ecc = 0.95, Periapsis = 7e6, Lan = 3.0, Aop = 5.5, M0 = 5.5 },
        };

        // ---------------------------------------------------------------- leaving a sphere

        [Test]
        public void Apocenter_IsInfiniteForOpenOrbits([ValueSource(nameof(Hyperbolic))] Case c)
        {
            Assert.IsTrue(double.IsPositiveInfinity(c.Orbit().Apocenter));
        }

        private static void AssertExit(StaticOrbit orbit, double radius, double from, string name, double? maxDelay = null)
        {
            Assert.IsTrue(orbit.TryFindExit(radius, from, out double t), name + " no exit found");
            Assert.GreaterOrEqual(t, from - 1e-6, name + " exit in the past");
            if (maxDelay.HasValue) Assert.LessOrEqual(t - from, maxDelay.Value + 1e-6, name + " exit later than one period");

            var (pos, vel) = orbit.GetSample(t);
            Assert.AreEqual(radius, pos.Length(), radius * 1e-9, name + " radius at exit");
            Assert.Greater(DVector3.Dot(pos, vel), 0, name + " not moving outward");

            // Independent check: it is the first outward crossing, so there is none earlier.
            int samples = 20000;
            bool wasInside = orbit.GetPositionAtT(from).Length() < radius;
            for (int i = 1; i < samples; i++)
            {
                double s = from + (t - from) * i / samples;
                bool inside = orbit.GetPositionAtT(s).Length() < radius;
                Assert.IsFalse(wasInside && !inside, name + " crossed outward earlier, at about " + s);
                wasInside = inside;
            }
        }

        [Test]
        public void Exit_Hyperbolic_FindsOutwardCrossing([ValueSource(nameof(Hyperbolic))] Case c)
        {
            StaticOrbit orbit = c.Orbit();
            foreach (double from in new[] { -5000.0, 0.0, 3000.0 })
                AssertExit(orbit, 1e9, from, $"{c.Name} from={from}");
        }

        [Test]
        public void Exit_Elliptic_FindsOutwardCrossingWithinOnePeriod([ValueSource(nameof(Elliptic))] Case c)
        {
            StaticOrbit orbit = c.Orbit();
            double apoapsis = (1 + c.Ecc) * c.Sma;
            double radius = c.Periapsis + 0.7 * (apoapsis - c.Periapsis);
            for (int i = 0; i < 12; i++)
            {
                double from = orbit.Period * i / 12.0;
                AssertExit(orbit, radius, from, $"{c.Name} from={from}", orbit.Period);
            }
        }

        [Test]
        public void Exit_NotFound_WhenSphereIsOutsideTheOrbit([ValueSource(nameof(Elliptic))] Case c)
        {
            StaticOrbit orbit = c.Orbit();
            double apoapsis = (1 + c.Ecc) * c.Sma;
            Assert.IsFalse(orbit.TryFindExit(apoapsis * 1.01, 0, out _), "bigger than the orbit");
            Assert.IsFalse(orbit.TryFindExit(c.Periapsis * 0.99, 0, out _), "smaller than the orbit");
        }

        [Test]
        public void Exit_NotFound_ForCircularOrbit()
        {
            StaticOrbit orbit = Make(0.3, 0, 7e6, 1, 0, 0);
            Assert.IsFalse(orbit.TryFindExit(8e6, 0, out _));
            Assert.IsFalse(orbit.TryFindExit(6e6, 0, out _));
        }

        [Test]
        public void Exit_NotFound_WhenHyperbolicCraftHasAlreadyLeft()
        {
            StaticOrbit orbit = Hyperbolic[0].Orbit();
            Assert.IsTrue(orbit.TryFindExit(1e8, 0, out double t));
            Assert.IsFalse(orbit.TryFindExit(1e8, t + 1000, out _));
        }

        // ---------------------------------------------------------------- entering a sphere

        private static StaticOrbit MoonOrbit() => Make(0.09, 0.05, 3.844e8, 0.4, 1.3, 2.0);

        /// <summary>Craft orbit around the parent that passes at the given offset and relative velocity from the moon at time t0.</summary>
        private static StaticOrbit CraftNearMoon(StaticOrbit moon, double t0, DVector3 offset, DVector3 relativeVelocity)
        {
            var (moonPos, moonVel) = moon.GetSample(t0);
            StaticOrbit craft = new StaticOrbit { Nu = Mu };
            craft.Calculate(moonPos + offset, moonVel + relativeVelocity, t0);
            return craft;
        }

        private static DVector3 RandomUnit(Random random)
        {
            while (true)
            {
                DVector3 v = new DVector3(random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1);
                double length = v.Length();
                if (length > 0.1 && length <= 1) return v / length;
            }
        }

        [Test]
        public void Entry_FindsFirstCrossingOfKnownEncounter()
        {
            StaticOrbit moon = MoonOrbit();
            double t0 = 1e6;
            Random random = new Random(7);
            for (int i = 0; i < 6; i++)
            {
                DVector3 offset = RandomUnit(random) * (0.6 * MoonSphere);
                DVector3 towardsMoon = (offset * -1).Normalize();
                DVector3 relative = towardsMoon * (300 + 1500 * i);
                // Run the encounter backwards so that the craft is inside at t0 and was outside before.
                StaticOrbit craft = CraftNearMoon(moon, t0, offset, relative * -1);
                double from = t0 - 5e5;
                if ((craft.GetPositionAtT(from) - moon.GetPositionAtT(from)).Length() <= MoonSphere) continue;

                Assert.IsTrue(OrbitCrossing.TryFindEntry(craft, moon, MoonSphere, from, 5e5, out double t), "case " + i);
                Assert.Less(t, t0, "case " + i);
                double gap = (craft.GetPositionAtT(t) - moon.GetPositionAtT(t)).Length();
                Assert.AreEqual(MoonSphere, gap, 0.05, "distance at entry, case " + i);
                Assert.Less((craft.GetPositionAtT(t + 5) - moon.GetPositionAtT(t + 5)).Length(), MoonSphere + 1, "moving inward, case " + i);
            }
        }

        [TestCase(1e6)]
        [TestCase(5e8)]
        public void Entry_AgreesWithBruteForceSampling(double t0)
        {
            StaticOrbit moon = MoonOrbit();
            double from = t0 - 1e5, horizon = 2e5, step = 5;
            Random random = new Random(2024);
            int withEntry = 0, without = 0;
            for (int i = 0; i < 60; i++)
            {
                DVector3 offset = RandomUnit(random) * (MoonSphere * (0.3 + random.NextDouble() * 2.7));
                DVector3 relative = RandomUnit(random) * (100 + random.NextDouble() * 4900);
                StaticOrbit craft = CraftNearMoon(moon, t0, offset, relative);

                double expected = double.NaN;
                bool wasOutside = false;
                for (double t = from; t <= from + horizon; t += step)
                {
                    double gap = (craft.GetPositionAtT(t) - moon.GetPositionAtT(t)).Length() - MoonSphere;
                    if (gap > 0) wasOutside = true;
                    else if (wasOutside)
                    {
                        expected = t;
                        break;
                    }
                }

                bool found = OrbitCrossing.TryFindEntry(craft, moon, MoonSphere, from, horizon, out double actual);
                if (double.IsNaN(expected))
                {
                    without++;
                    Assert.IsFalse(found, $"case {i}: found an entry at {actual} but sampling saw none");
                }
                else
                {
                    withEntry++;
                    Assert.IsTrue(found, $"case {i}: missed an entry near {expected}");
                    Assert.LessOrEqual(Math.Abs(actual - expected), step + 1e-6, $"case {i}");
                }
            }

            Assert.GreaterOrEqual(withEntry, 5, "scenario generator produced too few encounters");
            Assert.GreaterOrEqual(without, 5, "scenario generator produced too few misses");
        }

        [Test]
        public void Entry_NotFound_WhenOrbitsAreFarApart()
        {
            StaticOrbit moon = MoonOrbit();
            StaticOrbit lowOrbit = Make(0.3, 0.01, 7e6, 1, 1, 0);
            Assert.IsFalse(OrbitCrossing.TryFindEntry(lowOrbit, moon, MoonSphere, 0, lowOrbit.Period * 3, out _));
        }

        [Test]
        public void Entry_NotFound_ForEmptyOrInfiniteWindow()
        {
            StaticOrbit moon = MoonOrbit();
            StaticOrbit craft = CraftNearMoon(moon, 0, new DVector3(MoonSphere * 0.5, 0, 0), new DVector3(0, 500, 0));
            Assert.IsFalse(OrbitCrossing.TryFindEntry(craft, moon, MoonSphere, 0, 0, out _));
            Assert.IsFalse(OrbitCrossing.TryFindEntry(craft, moon, MoonSphere, 0, double.PositiveInfinity, out _));
        }

        // ---------------------------------------------------------------- handoff between parents

        [Test]
        public void Handoff_RelativeStateKeepsPositionAndVelocity_ThenLeavesThroughSameSphere()
        {
            StaticOrbit moon = MoonOrbit();
            double t0 = 1e6;
            DVector3 offset = RandomUnit(new Random(5)) * (0.6 * MoonSphere);
            StaticOrbit craft = CraftNearMoon(moon, t0, offset, (offset * -1).Normalize() * -1500);
            double from = t0 - 5e5;
            Assert.IsTrue(OrbitCrossing.TryFindEntry(craft, moon, MoonSphere, from, 5e5, out double entry));

            var (craftPos, craftVel) = craft.GetSample(entry);
            var (moonPos, moonVel) = moon.GetSample(entry);
            StaticOrbit local = new StaticOrbit { Nu = MoonMu };
            local.Calculate(craftPos - moonPos, craftVel - moonVel, entry);

            var (localPos, localVel) = local.GetSample(entry);
            AssertVectorNear(craftPos - moonPos, localPos, 1e-3, "position");
            AssertVectorNear(craftVel - moonVel, localVel, 1e-9, "velocity");
            Assert.AreEqual(MoonSphere, localPos.Length(), 0.05);

            // Moving inward through the sphere, the local orbit must come out of the same sphere later.
            Assert.IsTrue(local.TryFindExit(MoonSphere, entry + 1, out double exit));
            Assert.Greater(exit, entry);
            Assert.AreEqual(MoonSphere, local.GetPositionAtT(exit).Length(), MoonSphere * 1e-9);

            // Back in the parent frame the position is continuous too.
            DVector3 backInParentFrame = local.GetPositionAtT(entry) + moonPos;
            AssertVectorNear(craftPos, backInParentFrame, 1e-3, "round trip");
        }
    }
}
