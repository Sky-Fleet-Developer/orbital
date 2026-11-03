using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Orbital.Core.Config;
using Orbital.Core.Flight;

namespace Orbital.Tests
{
    /// <summary>Stage 2b.2, task A: the default thrust line offset is 5 mm (user decision 2026-10-29). Numbers of the default craft.</summary>
    public class ThrustOffsetDefaultTests
    {
        private const double G0 = 9.80665;

        private static double Tilt(in FlightState s, in FlightParams p) => FlightTelemetry.Compute(s, p).TiltDegrees;

        [Test]
        public void TheDefaultOffsetIsFiveMillimetres_InTheCodeAndInTheSharedFile()
        {
            FlightConfig d = FlightConfig.Default();
            Assert.AreEqual(0.005, d.ThrustOffset.X);
            Assert.AreEqual(0.0, d.ThrustOffset.Y);
            Assert.AreEqual(0.0, d.ThrustOffset.Z);
            string dir = ConfigDir();
            if (dir == null) Assert.Ignore("Assets/_game/Config not found");
            FlightConfig file = GameConfigLoader.FromDirectory(dir).Flight;
            Assert.AreEqual(d.ThrustOffset.X, file.ThrustOffset.X, "flightconfig.json says the same");
        }

        [Test]
        public void TheBudgetOfTheDefaultCraft_TorqueRcsHoldTimeBurnTimeGyro()
        {
            FlightConfig c = FlightConfig.Default();
            double torque = c.ThrustOffset.X * c.Engine.MaxThrust;                       // N*m at full thrust
            double rcsForce = torque / c.Rcs.Arm;                                        // N of RCS thrust to cancel it
            double rcsFlow = rcsForce / (c.Rcs.Isp * G0);                                // kg/s
            double rcsHold = c.RcsPropellantCapacity / rcsFlow;                          // s
            double mainFlow = EngineModel.MassFlow(c.Engine, 1.0);                       // kg/s
            double mainBurn = c.MainFuelCapacity / mainFlow;                             // s at full thrust
            double gyroFill = c.Gyro.MomentumCapacity / torque;                          // s until the gyro alone is full
            double rcsForWholeBurn = mainBurn * rcsFlow;                                 // kg
            Console.WriteLine($"default craft: offset {c.ThrustOffset.X * 1000:F1} mm, torque {torque:F0} N*m (gyro {c.Gyro.MaxTorque:F0} N*m, RCS {c.Rcs.MaxTorque.Z:F0} N*m), " +
                              $"RCS {rcsFlow:F3} kg/s -> {rcsHold:F0} s on {c.RcsPropellantCapacity:F0} kg, main burn {mainBurn:F0} s, " +
                              $"RCS for the whole burn {rcsForWholeBurn:F0} kg, gyro alone full after {gyroFill:F1} s");
            Assert.AreEqual(3000.0, torque, 1e-9);
            Assert.Less(torque, c.Gyro.MaxTorque, "the gyro torque is enough, only its store is not");
            Assert.AreEqual(10.0, gyroFill, 1e-9);
            Assert.AreEqual(261.5, rcsHold, 0.5);
            Assert.AreEqual(294.2, mainBurn, 0.5);
            Assert.Less(rcsHold, mainBurn, "the RCS still runs dry about half a minute before the main tank (gimbal later)");
        }

        // The real flight with the default craft: pad, R + T + full throttle on the first step, the whole burn.
        [Test]
        public void LiftOffWithSasAndRcs_TheDefaultCraft_HoldsForMostOfTheBurn()
        {
            FlightParams p = FlightFixture.Params();
            FlightState s = SpawnBuilder.OnSurface(p.Craft, p.Surface, 0.0, 0.0, 0.0, 0.0);
            InputLatch latch = default;
            RecorderState recorder = default;
            RecorderConfig rc = RecorderConfig.Default();
            int segments = 0, segmentsWhileRcs = 0;
            double t = 0, worstWhileRcs = 0, worstErrorWhileRcs = 0, rcsEmptyAt = double.NaN, mainEmptyAt = double.NaN, tiltAtMainEmpty = double.NaN;
            for (int i = 0; i < 16000; i++) // 320 s
            {
                KeyState keys = default;
                ControlKeys.Set(ref keys, ControlKey.X, true);
                if (i == 0) { ControlKeys.Set(ref keys, ControlKey.R, true); ControlKeys.Set(ref keys, ControlKey.T, true); }
                ControlInput c = InputMapper.Map(keys, ref latch);
                FlightKernel.Step(ref s, c, p, t, 0.02, out StepReport _);
                LandedSupport.HoldUpright(ref s, p.Surface);
                t += 0.02;
                Record(ref recorder, rc, s, t, ref segments);
                if (s.RcsFuel > 0.0)
                {
                    worstWhileRcs = Math.Max(worstWhileRcs, Tilt(s, p));
                    worstErrorWhileRcs = Math.Max(worstErrorWhileRcs, SasController.ErrorAngle(s) * 180.0 / Math.PI);
                    segmentsWhileRcs = segments;
                }
                else if (double.IsNaN(rcsEmptyAt)) rcsEmptyAt = t;
                if (double.IsNaN(mainEmptyAt) && s.MainFuel <= 0.0) { mainEmptyAt = t; tiltAtMainEmpty = Tilt(s, p); }
                Assert.IsTrue(SasRig.Finite(s), "finite at " + t);
            }
            Console.WriteLine($"Lift-off, default craft (5 mm): worst SAS error while the RCS has propellant {worstErrorWhileRcs:F3} deg, worst tilt from the local vertical {worstWhileRcs:F3} deg, RCS empty at {rcsEmptyAt:F1} s, " +
                              $"main tank empty at {mainEmptyAt:F1} s (tilt then {tiltAtMainEmpty:F1} deg), thrust segments {segments} ({segmentsWhileRcs} while the SAS held), mode {s.Mode}");
            Assert.Less(worstErrorWhileRcs, 0.5, "the SAS holds the rocket while the RCS has propellant");
            // the target is inertial: the local vertical turns with the planet (about 1.1 degrees in 264 s) and with the flight, so the tilt grows a bit
            Assert.Less(worstWhileRcs, 3.0);
            Assert.Greater(rcsEmptyAt, 230.0, "200 kg last about 4 minutes against 3 kN*m (26 s against 30 kN*m before)");
            Assert.IsFalse(double.IsNaN(mainEmptyAt), "the main tank burned out");
            Assert.Greater(mainEmptyAt, rcsEmptyAt, "the RCS still runs dry before the end of the burn");
        }

        [Test]
        public void InDeepSpace_TheDefaultTankHoldsTheDefaultOffsetToTheStatedPrecision()
        {
            SasRig rig = new SasRig(rcsTank: 200.0, thrustOffsetX: FlightConfig.Default().ThrustOffset.X);
            rig.Run(0.04, SasRig.BurnWithSasAndRcs());
            double worstLate = 0;
            for (int i = 0; i < 6000; i++) // 120 s
            {
                rig.Step(SasRig.Keys(ControlKey.X));
                if (rig.Time > 30.0) worstLate = Math.Max(worstLate, rig.ErrorDeg);
            }
            double torque = rig.P.Craft.ThrustOffset.X * rig.P.Craft.Engine.MaxThrust;
            Console.WriteLine($"Deep space, default craft: worst error 30..120 s {worstLate:E2} deg, integral {rig.S.SasIntegral.Z:F0} N*m, RCS left {rig.S.RcsFuel:F1} kg, peak {rig.MaxErrorDeg:F3} deg");
            Assert.Less(worstLate, 0.1);
            Assert.Less(rig.MaxErrorDeg, 1.0, "the small offset hardly moves the nose while the integral builds up");
            Assert.AreEqual(-torque, rig.S.SasIntegral.Z, 0.05 * torque, "the integral is the estimate of the 3 kN*m");
            Assert.Greater(rig.S.RcsFuel, 0.0);
        }

        [Test]
        public void TheGyroAloneHoldsTheDefaultOffsetOnlyUntilItsStoreIsFull()
        {
            SasRig rig = new SasRig(rcsTank: 200.0, thrustOffsetX: FlightConfig.Default().ThrustOffset.X);
            rig.Run(0.04, t => SasRig.Keys(ControlKey.X, ControlKey.T)); // SAS without RCS
            double worstEarly = 0, fullAt = double.NaN;
            for (int i = 0; i < 3000; i++) // 60 s
            {
                rig.Step(SasRig.Keys(ControlKey.X));
                if (rig.Time < 5.0) worstEarly = Math.Max(worstEarly, rig.ErrorDeg);
                if (double.IsNaN(fullAt) && GyroModel.Saturation(rig.P.Craft.Gyro, rig.S.GyroH) > 0.95) fullAt = rig.Time;
                Assert.IsTrue(SasRig.Finite(rig.S));
            }
            Console.WriteLine($"Gyro only, default craft: error in the first 5 s {worstEarly:F3} deg, store 95 % full at {fullAt:F1} s, error at 60 s {rig.ErrorDeg:F1} deg");
            Assert.Less(worstEarly, 1.0);
            Assert.Less(fullAt, 20.0, "3 kN*m fill 30 kN*m*s in about 10 s");
            Assert.Greater(rig.ErrorDeg, 10.0, "then the control is lost: the RCS is needed for a long burn");
        }

        // The load on the synchronisation: the recorder closes a segment when the thrust turns by 1 degree. Without SAS the offset turns the craft,
        // so the number of segments in a free burn is the measure (see "Смещение линии тяги" in Docs/GameplayPrototype.md).
        [Test]
        public void WithoutSas_TheDefaultOffsetTipsTheCraftLater_AndGivesFewerSegments()
        {
            (double tilt30, int seg60, double firstDegree) strong = FreeBurn(SasRig.StrongOffset);
            (double tilt30, int seg60, double firstDegree) small = FreeBurn(FlightConfig.Default().ThrustOffset.X);
            (double tilt30, int seg60, double firstDegree) none = FreeBurn(0.0);
            Console.WriteLine($"free burn, no SAS: 5 cm -> tilt at 30 s {strong.tilt30:F1} deg, 1 deg after {strong.firstDegree:F1} s, segments in 60 s {strong.seg60}; " +
                              $"5 mm -> {small.tilt30:F1} deg, {small.firstDegree:F1} s, {small.seg60}; no offset -> {none.tilt30:F2} deg, {none.seg60}");
            Assert.Greater(strong.tilt30, 30.0);
            Assert.Less(small.tilt30, strong.tilt30);
            Assert.Greater(small.tilt30, 1.0, "5 mm still turns the craft without the SAS");
            Assert.Greater(small.firstDegree, strong.firstDegree);
            Assert.LessOrEqual(small.seg60, strong.seg60);
            Assert.Less(none.tilt30, 1e-6);
        }

        private static (double, int, double) FreeBurn(double offset)
        {
            SasRig rig = new SasRig(rcsTank: 200.0, thrustOffsetX: offset);
            int segments = 0;
            double tilt30 = 0, firstDegree = double.NaN;
            for (int i = 0; i < 3000; i++) // 60 s
            {
                rig.Step(SasRig.Keys(ControlKey.X));
                double tilt = Vec3.Angle(DQuat.Rotate(rig.S.Orientation, Vec3.UnitY), Vec3.UnitY) * 180 / Math.PI;
                if (Math.Abs(rig.Time - 30.0) < 0.011) tilt30 = tilt;
                if (double.IsNaN(firstDegree) && tilt >= 1.0) firstDegree = rig.Time;
            }
            segments = rig.Segments.Count;
            return (tilt30, segments, firstDegree);
        }

        private static void Record(ref RecorderState r, in RecorderConfig rc, in FlightState s, double t, ref int count)
        {
            RecorderSample sample;
            sample.Time = t;
            sample.Throttle = s.Throttle;
            sample.ThrustDirection = DQuat.Rotate(s.Orientation, Vec3.UnitY);
            sample.Sas = s.SasOn;
            sample.Resting = s.Mode != FlightMode.Flying;
            ThrustSegmentRecorder.Update(ref r, sample, rc);
            while (ThrustSegmentRecorder.TryDequeue(ref r, t, rc, out ThrustSegment _)) count++;
        }

        internal static string ConfigDir()
        {
            string dir = AppContext.BaseDirectory;
            for (int i = 0; i < 10 && dir != null; i++)
            {
                string candidate = System.IO.Path.Combine(dir, "Assets", "_game", "Config");
                if (System.IO.Directory.Exists(candidate)) return candidate;
                dir = System.IO.Path.GetDirectoryName(dir);
            }
            return null;
        }
    }

    /// <summary>Stage 2b.2, task B: the scaled game clock (fixed step, substeps per tick, accumulator, cap).</summary>
    public class GameClockTests
    {
        private const double Dt = 0.02;

        private static List<int> Pattern(double scale, int ticks, int cap = GameClock.DefaultMaxStepsPerTick)
        {
            GameClock c = GameClock.Create(0.0, Dt, scale, cap);
            List<int> steps = new List<int>();
            for (int i = 0; i < ticks; i++) steps.Add(GameClock.Tick(ref c));
            return steps;
        }

        [Test]
        public void ScaleOne_IsOneStepPerTick()
        {
            CollectionAssert.AreEqual(new[] { 1, 1, 1, 1, 1, 1 }, Pattern(1.0, 6));
        }

        [Test]
        public void ScaleAQuarter_IsOneStepEveryFourthTick()
        {
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1 }, Pattern(0.25, 12));
        }

        [Test]
        public void ScaleTen_IsTenStepsPerTick()
        {
            CollectionAssert.AreEqual(new[] { 10, 10, 10 }, Pattern(10.0, 3));
        }

        [Test]
        public void ScaleATenth_IsExact_NoMissingStepFromRounding()
        {
            List<int> p = Pattern(0.1, 100000);
            for (int i = 0; i < p.Count; i++) Assert.AreEqual(i % 10 == 9 ? 1 : 0, p[i], "tick " + i);
        }

        [Test]
        public void AFractionalScaleSpreadsTheStepsEvenly()
        {
            GameClock c = GameClock.Create(0.0, Dt, 2.5);
            CollectionAssert.AreEqual(new[] { 2, 3, 2, 3 }, new[] { GameClock.Tick(ref c), GameClock.Tick(ref c), GameClock.Tick(ref c), GameClock.Tick(ref c) });
            Assert.AreEqual(10, c.Steps);
        }

        [Test]
        public void TheCapLimitsTheStepsPerTick_DropsTheBacklog_AndSaysSo()
        {
            GameClock c = GameClock.Create(0.0, Dt, 100.0, maxStepsPerTick: 40);
            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(40, GameClock.Tick(ref c));
                Assert.IsTrue(c.LastTickLimited);
            }
            Assert.AreEqual(2000, c.Steps, "no backlog: 40 per tick, not catching up later");
            Assert.AreEqual(50, c.LimitedTicks);
            Assert.AreEqual(40.0, c.EffectiveScale);
            GameClock.SetScale(ref c, 1.0);
            Assert.AreEqual(1, GameClock.Tick(ref c), "back to normal at once, no burst of old steps");
            Assert.IsFalse(c.LastTickLimited);
            Assert.AreEqual(1.0, c.EffectiveScale);
        }

        [Test]
        public void AtTheTopOfTheSliderTheDefaultCapGivesTwoHundredStepsPerTick()
        {
            GameClock c = GameClock.Create(0.0, Dt, GameClock.DefaultMaxScale);
            Assert.AreEqual(1000.0, c.Scale);
            Assert.AreEqual(200, GameClock.Tick(ref c));
            Assert.IsTrue(c.LastTickLimited);
            Assert.AreEqual(200.0, c.EffectiveScale);
            GameClock.SetScale(ref c, 200.0);
            Assert.AreEqual(200, GameClock.Tick(ref c));
            Assert.IsFalse(c.LastTickLimited, "exactly the cap is not a cut");
        }

        [Test]
        public void TheScaleIsClampedAndRounded()
        {
            GameClock c = GameClock.Create(0.0, Dt);
            GameClock.SetScale(ref c, 1e6);
            Assert.AreEqual(GameClock.DefaultMaxScale, c.Scale);
            GameClock.SetScale(ref c, 0.0);
            Assert.AreEqual(GameClock.DefaultMinScale, c.Scale);
            GameClock.SetScale(ref c, double.NaN);
            Assert.AreEqual(1.0, c.Scale);
            GameClock.SetScale(ref c, 3.14159);
            Assert.AreEqual(3.142, c.Scale, 1e-12);
            Assert.Throws<ArgumentOutOfRangeException>(() => GameClock.Create(0.0, 0.0));
        }

        [Test]
        public void TheTimeIsStartPlusStepsTimesTheStep_NoDriftOverManyTicks()
        {
            GameClock c = GameClock.Create(1000.0, Dt, 1.0);
            double summed = 1000.0;
            const int ticks = 50 * 3600 * 24; // a day of real time at 50 ticks per second
            for (int i = 0; i < ticks; i++)
            {
                GameClock.Tick(ref c);
                summed += Dt;
            }
            Assert.AreEqual(ticks, c.Steps);
            Assert.AreEqual(1000.0 + ticks * Dt, c.Time, 0.0, "exact product, not a sum");
            Console.WriteLine($"a day of ticks: clock {c.Time:R} s, running float sum {summed:R} s, the sum drifted {summed - c.Time:E2} s");
            Assert.AreEqual(c.TimeOfStep(c.Steps), c.Time);
        }

        [Test]
        public void ChangingTheScaleKeepsTheSteps_AndTheTimeStaysOnTheStepGrid()
        {
            GameClock c = GameClock.Create(0.0, Dt, 0.25);
            long total = 0;
            double[] scales = { 0.25, 3.0, 0.1, 47.5, 1.0, 0.333 };
            int[] ticks = { 7, 11, 23, 5, 9, 31 };
            for (int k = 0; k < scales.Length; k++)
            {
                GameClock.SetScale(ref c, scales[k]);
                for (int i = 0; i < ticks[k]; i++)
                {
                    long before = c.Steps;
                    int n = GameClock.Tick(ref c);
                    Assert.AreEqual(before, c.LastTickFirstStep);
                    total += n;
                }
            }
            Assert.AreEqual(total, c.Steps);
            Assert.AreEqual(total * Dt, c.Time, 0.0);
            Assert.GreaterOrEqual(c.Accumulator, 0);
            Assert.Less(c.Accumulator, GameClock.Unit);
        }

        [Test]
        public void TheSliderIsLogarithmic_AndRoundTrips()
        {
            GameClock c = GameClock.Create(0.0, Dt);
            Assert.AreEqual(GameClock.DefaultMinScale, GameClock.ScaleFromSlider(c, 0.0), 1e-12);
            Assert.AreEqual(GameClock.DefaultMaxScale, GameClock.ScaleFromSlider(c, 1.0), 1e-9);
            Assert.AreEqual(Math.Sqrt(GameClock.DefaultMinScale * GameClock.DefaultMaxScale), GameClock.ScaleFromSlider(c, 0.5), 1e-9, "the middle is the geometric mean");
            foreach (double s in new[] { 0.1, 0.25, 1.0, 7.0, 100.0 })
                Assert.AreEqual(s, GameClock.ScaleFromSlider(c, GameClock.SliderFromScale(c, s)), 1e-9);
        }
    }

    /// <summary>
    /// Stage 2b.2: the craft stepped through the scaled clock the way <c>CraftSpawner</c> does it: keys are sampled once per engine tick and applied
    /// to every step of that tick; ticks without a step keep the keys for the next step. The kernel always gets the same fixed step.
    /// </summary>
    public class ScaledFlightTests
    {
        private const double Dt = 0.02;

        private sealed class ScaledRun
        {
            public GameClock Clock;
            public FlightParams P = FlightFixture.Params();
            public FlightState S;
            public InputLatch Latch;
            public RecorderState Recorder;
            public readonly RecorderConfig Rc = RecorderConfig.Default();
            public readonly List<ThrustSegment> Segments = new List<ThrustSegment>();
            public long CraftSteps;    // steps the craft has made (the index of its next step)
            private KeyState _pending; // keys held since the last step (the accumulation of CraftInput)

            public ScaledRun(double scale)
            {
                Clock = GameClock.Create(0.0, Dt, scale);
                S = SpawnBuilder.OnSurface(P.Craft, P.Surface, 0.0, 0.0, 0.0, 0.0);
            }

            public void Tick(KeyState keysOfThisTick)
            {
                _pending = ControlKeys.Or(_pending, keysOfThisTick);
                GameClock.Tick(ref Clock);
                if (CraftSteps >= Clock.Steps) return; // slow motion: no step in this tick, the keys wait
                KeyState keys = _pending;
                _pending = default;
                while (CraftSteps < Clock.Steps)
                {
                    double t = Clock.TimeOfStep(CraftSteps);
                    ControlInput c = InputMapper.Map(keys, ref Latch);
                    FlightKernel.Step(ref S, c, P, t, Dt, out StepReport _);
                    LandedSupport.HoldUpright(ref S, P.Surface);
                    RecorderSample sample;
                    sample.Time = t + Dt;
                    sample.Throttle = S.Throttle;
                    sample.ThrustDirection = DQuat.Rotate(S.Orientation, Vec3.UnitY);
                    sample.Sas = S.SasOn;
                    sample.Resting = S.Mode != FlightMode.Flying;
                    ThrustSegmentRecorder.Update(ref Recorder, sample, Rc);
                    while (ThrustSegmentRecorder.TryDequeue(ref Recorder, t + Dt, Rc, out ThrustSegment seg)) Segments.Add(seg);
                    CraftSteps++;
                }
            }
        }

        private static KeyState Keys(params ControlKey[] pressed) => SasRig.Keys(pressed);

        // R, T and full throttle on the first tick, then the throttle held: the toggles fire once whatever the number of steps in that tick.
        private static ScaledRun Fly(double scale, double gameSeconds)
        {
            ScaledRun run = new ScaledRun(scale);
            bool first = true;
            while (run.Clock.Time < gameSeconds - 1e-9)
            {
                run.Tick(first ? Keys(ControlKey.X, ControlKey.R, ControlKey.T) : Keys(ControlKey.X));
                first = false;
            }
            return run;
        }

        [Test]
        public void TheSameFlightAtScales1And10And025_GivesBitIdenticalStatesAndSegments()
        {
            ScaledRun one = Fly(1.0, 60.0), ten = Fly(10.0, 60.0), quarter = Fly(0.25, 60.0);
            Assert.AreEqual(3000, one.CraftSteps);
            Assert.AreEqual(one.CraftSteps, ten.CraftSteps);
            Assert.AreEqual(one.CraftSteps, quarter.CraftSteps);
            foreach (ScaledRun other in new[] { ten, quarter })
            {
                Assert.AreEqual(one.S.Position, other.S.Position);
                Assert.AreEqual(one.S.Velocity, other.S.Velocity);
                Assert.AreEqual(one.S.Orientation, other.S.Orientation);
                Assert.AreEqual(one.S.Omega, other.S.Omega);
                Assert.AreEqual(one.S.MainFuel, other.S.MainFuel);
                Assert.AreEqual(one.S.RcsFuel, other.S.RcsFuel);
                Assert.AreEqual(one.S.SasIntegral, other.S.SasIntegral);
                Assert.AreEqual(one.Segments.Count, other.Segments.Count);
                for (int i = 0; i < one.Segments.Count; i++)
                {
                    Assert.AreEqual(one.Segments[i].StartTime, other.Segments[i].StartTime);
                    Assert.AreEqual(one.Segments[i].Duration, other.Segments[i].Duration);
                    Assert.AreEqual(one.Segments[i].Direction, other.Segments[i].Direction);
                }
            }
            Assert.AreEqual(1, one.S.SasOn, "T toggled once");
            Assert.AreEqual(1, ten.S.SasOn, "T held over ten steps of one tick toggled once, not ten times");
            Assert.AreEqual(FlightMode.Flying, one.S.Mode);
            Assert.Greater(one.Segments.Count, 1);
        }

        [Test]
        public void TheCraftAndTheWorldClockStayInStep_AtAnyScale()
        {
            ScaledRun run = new ScaledRun(1.0);
            double[] scales = { 1.0, 0.1, 37.0, 0.25, 100.0, 2.5 };
            foreach (double scale in scales)
            {
                GameClock.SetScale(ref run.Clock, scale);
                for (int i = 0; i < 97; i++)
                {
                    run.Tick(Keys(ControlKey.X));
                    Assert.AreEqual(run.Clock.Steps, run.CraftSteps, "the craft made exactly the steps of the world clock");
                    Assert.AreEqual(run.Clock.Time, run.Clock.TimeOfStep(run.CraftSteps), 0.0);
                }
            }
        }

        [Test]
        public void InSlowMotionATapInATickWithoutAStepIsNotLost()
        {
            ScaledRun run = new ScaledRun(0.25);
            run.Tick(Keys(ControlKey.T)); // tick 1: no step (0.25), the tap is kept
            Assert.AreEqual(0, run.CraftSteps);
            run.Tick(default);
            run.Tick(default);
            run.Tick(default);            // tick 4: the step, with the tap of tick 1
            Assert.AreEqual(1, run.CraftSteps);
            Assert.AreEqual(1, run.S.SasOn);
        }

        // The cost of one craft step (kernel + ground support + recorder, no ecs) on this machine; used to pick the cap of the clock.
        [Test]
        public void MeasureTheCostOfAStep()
        {
            ScaledRun warm = Fly(100.0, 20.0);
            Stopwatch sw = Stopwatch.StartNew();
            ScaledRun run = Fly(100.0, 300.0);
            sw.Stop();
            double usPerStep = sw.Elapsed.TotalMilliseconds * 1000.0 / run.CraftSteps;
            double msPerTickAtCap = usPerStep * GameClock.DefaultMaxStepsPerTick / 1000.0;
            Console.WriteLine($"craft step: {usPerStep:F2} us ({run.CraftSteps} steps in {sw.Elapsed.TotalMilliseconds:F0} ms); at the cap of " +
                              $"{GameClock.DefaultMaxStepsPerTick} steps per tick {msPerTickAtCap:F2} ms per tick, {msPerTickAtCap * 50:F0} ms per real second at 50 ticks/s");
            Assert.Greater(run.CraftSteps, 0);
            Assert.Greater(warm.CraftSteps, 0);
        }
    }
}
