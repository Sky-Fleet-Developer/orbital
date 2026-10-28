using System;
using System.Collections.Generic;
using NUnit.Framework;
using Orbital.Core.Config;
using Orbital.Core.Flight;

namespace Orbital.Tests
{
    /// <summary>A small test rig: one craft in deep space stepped like the client does (keys -> control -> kernel -> recorder).</summary>
    public sealed class SasRig
    {
        public const double Dt = 0.02;
        public FlightParams P;
        public FlightState S;
        public InputLatch Latch;
        public RecorderState Recorder;
        public RecorderConfig RecorderCfg = RecorderConfig.Default();
        public readonly List<ThrustSegment> Segments = new List<ThrustSegment>();
        public double Time;
        public bool Naive;          // comparison controller: integral reset every step, target captured at the instant of release
        public double MaxErrorDeg;  // largest error angle seen by Run

        public SasRig(double rcsTank = 3000.0, double thrustOffsetX = 0.05)
        {
            P = FlightFixture.DeepSpace();
            P.Craft.RcsPropellantCapacity = rcsTank;
            P.Craft.ThrustOffset = new Vec3(thrustOffsetX, 0, 0);
            S = FlightFixture.SpaceCraft(P);
        }

        public double ErrorDeg => SasController.ErrorAngle(S) * 180.0 / Math.PI;

        /// <summary>Runs for the given time; keys(t) gives the keys held at the time t (since the start of the rig).</summary>
        public void Run(double seconds, Func<double, KeyState> keys)
        {
            int n = (int) Math.Round(seconds / Dt);
            for (int i = 0; i < n; i++) Step(keys(Time));
        }

        public void Step(KeyState keys)
        {
            ControlInput c = InputMapper.Map(keys, ref Latch);
            bool manual = c.Pitch != 0 || c.Roll != 0 || c.Yaw != 0;
            if (Naive)
            {
                S.SasIntegral = Vec3.Zero;
                if (S.SasOn != 0 && S.SasPhase == SasPhase.Settle && !manual) { S.SasPhase = SasPhase.Hold; S.SasTarget = S.Orientation; }
            }
            FlightKernel.Step(ref S, c, P, Time, Dt, out StepReport _);
            RecorderSample sample;
            sample.Time = Time + Dt;
            sample.Throttle = S.Throttle;
            sample.ThrustDirection = DQuat.Rotate(S.Orientation, Vec3.UnitY);
            sample.Sas = S.SasOn;
            sample.Resting = S.Mode != FlightMode.Flying;
            ThrustSegmentRecorder.Update(ref Recorder, sample, RecorderCfg);
            while (ThrustSegmentRecorder.TryDequeue(ref Recorder, Time + Dt, RecorderCfg, out ThrustSegment seg)) Segments.Add(seg);
            Time += Dt;
            MaxErrorDeg = Math.Max(MaxErrorDeg, ErrorDeg);
        }

        public static KeyState Keys(params ControlKey[] pressed)
        {
            KeyState k = default;
            foreach (ControlKey key in pressed) ControlKeys.Set(ref k, key, true);
            return k;
        }

        /// <summary>X (full throttle), R and T are pressed for one step at the start: thrust on, RCS on, SAS on.</summary>
        public static Func<double, KeyState> BurnWithSasAndRcs() =>
            t => t < 0.03 ? Keys(ControlKey.X, ControlKey.R, ControlKey.T) : Keys(ControlKey.X);

        public static bool Finite(in FlightState s)
        {
            double sum = s.Position.X + s.Position.Y + s.Position.Z + s.Velocity.X + s.Velocity.Y + s.Velocity.Z + s.Orientation.W + s.Orientation.X + s.Orientation.Y +
                         s.Orientation.Z + s.Omega.X + s.Omega.Y + s.Omega.Z + s.GyroH.X + s.GyroH.Y + s.GyroH.Z + s.MainFuel + s.RcsFuel + s.Throttle +
                         s.SasTarget.W + s.SasTarget.X + s.SasTarget.Y + s.SasTarget.Z + s.SasIntegral.X + s.SasIntegral.Y + s.SasIntegral.Z;
            return !double.IsNaN(sum) && !double.IsInfinity(sum);
        }

        public static double Angle(DQuat a, DQuat b) => Vec3.Length(DQuat.ToRotationVector(DQuat.Multiply(DQuat.Conjugate(a), b))) * 180.0 / Math.PI;
    }

    public class SasInputTests
    {
        [Test]
        public void TKeyTogglesTheSasOnAPress_NotWhileHeld()
        {
            InputLatch latch = default;
            Assert.IsFalse(InputMapper.Map(default, ref latch).SasOn, "off by default");
            KeyState t = SasRig.Keys(ControlKey.T);
            Assert.IsTrue(InputMapper.Map(t, ref latch).SasOn, "press: on");
            for (int i = 0; i < 100; i++) Assert.IsTrue(InputMapper.Map(t, ref latch).SasOn, "held: stays on");
            InputMapper.Map(default, ref latch);
            Assert.IsFalse(InputMapper.Map(t, ref latch).SasOn, "second press: off");
        }

        [Test]
        public void TheSasAndRcsSwitchesAreIndependent()
        {
            InputLatch latch = default;
            ControlInput c = InputMapper.Map(SasRig.Keys(ControlKey.T), ref latch);
            Assert.IsTrue(c.SasOn);
            Assert.IsFalse(c.RcsOn);
            InputMapper.Map(default, ref latch);
            c = InputMapper.Map(SasRig.Keys(ControlKey.R), ref latch);
            Assert.IsTrue(c.SasOn);
            Assert.IsTrue(c.RcsOn);
        }

        [Test]
        public void ATapBetweenTwoStepsIsNotLost()
        {
            KeyState acc = ControlKeys.Or(default, SasRig.Keys(ControlKey.T));
            acc = ControlKeys.Or(acc, default);
            Assert.IsTrue(acc.T);
        }
    }

    public class SasMathTests
    {
        [Test]
        public void RotationVectorRoundTrip()
        {
            Random rnd = new Random(7);
            for (int i = 0; i < 200; i++)
            {
                Vec3 rv = new Vec3(rnd.NextDouble() - 0.5, rnd.NextDouble() - 0.5, rnd.NextDouble() - 0.5) * 5.0; // up to about 4.3 rad
                double angle = Vec3.Length(rv);
                if (angle > Math.PI) rv = rv * ((2 * Math.PI - angle) / angle) * -1.0; // the equivalent rotation inside [0, pi]
                Vec3 back = DQuat.ToRotationVector(DQuat.FromRotationVector(rv));
                FlightFixture.AssertNear(rv, back, 1e-12, "round trip " + i);
            }
            Assert.AreEqual(Vec3.Zero.X, DQuat.ToRotationVector(DQuat.Identity).X);
            Assert.AreEqual(0.0, Vec3.Length(DQuat.ToRotationVector(DQuat.Identity)));
            Assert.AreEqual(0.0, Vec3.Length(DQuat.ToRotationVector(default)), "the all-zero quaternion of an old save counts as identity");
            FlightFixture.AssertNear(DQuat.ToRotationVector(new DQuat(-1, 0, 0, 0)), Vec3.Zero, 1e-15, "-q is the same rotation");
        }

        [Test]
        public void ErrorVectorIsInTheBodyFrame()
        {
            FlightState s = FlightKernel.Fresh(FlightConfig.Default());
            s.SasOn = 1;
            s.Orientation = DQuat.FromAxisAngle(Vec3.UnitY, 1.0);
            s.SasTarget = DQuat.Multiply(s.Orientation, DQuat.FromAxisAngle(Vec3.UnitZ, 0.1));
            FlightFixture.AssertNear(new Vec3(0, 0, 0.1), SasController.ErrorVector(s), 1e-12);
            Assert.AreEqual(0.1, SasController.ErrorAngle(s), 1e-12);
            s.SasOn = 0;
            Assert.AreEqual(0.0, SasController.ErrorAngle(s));
        }
    }

    public class SasControllerTests
    {
        // (a) the disturbance of the default craft (5 cm thrust offset, about 30 kN*m at full thrust), RCS on: the SAS holds the attitude
        [Test]
        public void HoldsTheAttitudeWithTheThrustOffsetAndTheRcs_LongRun()
        {
            SasRig rig = new SasRig();
            rig.Run(0.04, SasRig.BurnWithSasAndRcs());
            double worstAfterWarmUp = 0, worstEarly = 0;
            for (int i = 0; i < 8000; i++) // 160 s
            {
                rig.Step(SasRig.Keys(ControlKey.X));
                if (rig.Time > 40.0) worstAfterWarmUp = Math.Max(worstAfterWarmUp, rig.ErrorDeg);
                else worstEarly = Math.Max(worstEarly, rig.ErrorDeg);
                Assert.IsTrue(SasRig.Finite(rig.S), "finite at " + rig.Time);
            }
            Console.WriteLine($"SAS hold: peak error while the integral builds up {worstEarly:F3} deg, after 40 s {worstAfterWarmUp:E2} deg, " +
                              $"integral {rig.S.SasIntegral.Z:F0} N*m, RCS fuel left {rig.S.RcsFuel:F0} kg, |H| {Vec3.Length(rig.S.GyroH):F0}");
            Assert.Less(worstAfterWarmUp, 0.1, "error below 0.1 degrees from 40 s to 160 s");
            Assert.Less(worstEarly, 5.0, "the build-up of the integral does not tip the craft");
            double thrustTorque = 0.05 * rig.P.Craft.Engine.MaxThrust;
            Assert.AreEqual(-thrustTorque, rig.S.SasIntegral.Z, 0.05 * thrustTorque, "the integral is the estimate of the disturbing torque");
            Assert.AreEqual(0.0, rig.S.SasIntegral.X, 100.0);
            Assert.Greater(rig.S.RcsFuel, 0.0, "the test tank is big enough");
            Assert.Less(Vec3.Length(rig.S.GyroH), rig.P.Craft.Gyro.DumpStart * rig.P.Craft.Gyro.MomentumCapacity,
                "the gyro is not driven into the unloading zone: the RCS carries the constant part");
        }

        [Test]
        public void WithoutTheDisturbanceNothingMoves()
        {
            SasRig rig = new SasRig(thrustOffsetX: 0.0);
            rig.Run(0.04, SasRig.BurnWithSasAndRcs());
            rig.Run(60, t => SasRig.Keys(ControlKey.X));
            Assert.Less(rig.MaxErrorDeg, 1e-6);
            Assert.Less(Vec3.Length(rig.S.Omega), 1e-9);
        }

        [Test]
        public void DefaultsOffBeforeTheKeyAndTheCraftTipsOver()
        {
            SasRig rig = new SasRig();
            rig.Run(30, t => SasRig.Keys(ControlKey.X, ControlKey.R));
            Assert.AreEqual(0, rig.S.SasOn);
            double tilt = Vec3.Angle(DQuat.Rotate(rig.S.Orientation, Vec3.UnitY), Vec3.UnitY) * 180 / Math.PI;
            Assert.Greater(tilt, 30.0, "without SAS the offset tips the craft (the reason for this stage)");
        }

        // (b) the key requirement: short pulses do not make the held attitude sag again and again
        private static List<double> PulseSeries(SasRig rig, int pulses, out double totalDrift)
        {
            rig.Run(0.04, SasRig.BurnWithSasAndRcs());
            rig.Run(45, t => SasRig.Keys(ControlKey.X)); // the integral has converged
            List<double> sags = new List<double>();
            DQuat start = rig.S.Orientation;
            totalDrift = 0.0;
            for (int k = 0; k < pulses; k++)
            {
                ControlKey key = k % 2 == 0 ? ControlKey.W : ControlKey.E; // pitch, yaw
                double pulseEnd = rig.Time + 0.5;
                rig.Run(0.5, t => SasRig.Keys(ControlKey.X, key));
                // brake and capture
                int guard = 0;
                while (rig.S.SasPhase != SasPhase.Hold && guard++ < 3000) rig.Step(SasRig.Keys(ControlKey.X));
                Assert.Less(guard, 3000, "the target was captured");
                DQuat captured = rig.S.SasTarget;
                rig.Run(10, t => SasRig.Keys(ControlKey.X));
                double sag = SasRig.Angle(captured, rig.S.Orientation);
                sags.Add(sag);
                totalDrift += sag;
            }
            return sags;
        }

        [Test]
        public void ManyShortPulses_TheCapturedAttitudeDoesNotSagAndTheErrorDoesNotAccumulate()
        {
            SasRig rig = new SasRig();
            List<double> sags = PulseSeries(rig, 12, out double total);
            Console.WriteLine("SAS pulses: sag after capture per pulse (deg): " + string.Join(" ", sags.ConvertAll(v => v.ToString("F3"))) + $"; total {total:F3}");
            foreach (double sag in sags) Assert.Less(sag, 0.2, "the held attitude stays where it was captured");
            Assert.Less(total, 1.5, "the sag does not accumulate over 12 pulses");
            Assert.Less(sags[sags.Count - 1], sags[0] + 0.05, "later pulses are not worse than the first");
            Assert.Less(rig.ErrorDeg, 0.1, "the final error is small");
            Assert.Greater(rig.S.SasIntegral.Z, -1.1 * 0.05 * 600000, "the integral stayed in its limits");
            Assert.AreEqual(-0.05 * 600000, rig.S.SasIntegral.Z, 0.06 * 0.05 * 600000, "pulses and captures did not reset the integral");
        }

        [Test]
        public void TheNaiveControllerWithAResetSagsEveryTimeAndTheErrorAccumulates()
        {
            SasRig naive = new SasRig { Naive = true };
            List<double> sags = PulseSeries(naive, 12, out double total);
            Console.WriteLine("naive SAS (integral reset, instant capture): sag per pulse (deg): " + string.Join(" ", sags.ConvertAll(v => v.ToString("F3"))) + $"; total {total:F3}");
            SasRig good = new SasRig();
            PulseSeries(good, 12, out double goodTotal);
            foreach (double sag in sags) Assert.Greater(sag, 0.8, "every pulse costs a degree or more (what KSP does)");
            Assert.Greater(total, 10.0, "the sags add up");
            Assert.Greater(total, 10.0 * goodTotal, "the integral that survives the pulses is what removes it");
        }

        [Test]
        public void PilotInputDoesNotResetTheIntegral_AndFreezesIt()
        {
            SasRig rig = new SasRig();
            rig.Run(0.04, SasRig.BurnWithSasAndRcs());
            rig.Run(45, t => SasRig.Keys(ControlKey.X));
            Vec3 before = rig.S.SasIntegral;
            Assert.Greater(Math.Abs(before.Z), 20000.0);
            rig.Run(2.0, t => SasRig.Keys(ControlKey.X, ControlKey.W));
            Assert.AreEqual(SasPhase.Manual, rig.S.SasPhase);
            FlightFixture.AssertNear(before, rig.S.SasIntegral, 1e-9, "frozen while the pilot flies");
            // the target follows the orientation while the pilot flies
            Assert.Less(SasRig.Angle(rig.S.Orientation, rig.S.SasTarget), Vec3.Length(rig.S.Omega) * SasRig.Dt * 180 / Math.PI + 1e-9, "at most one step behind");
            rig.Step(SasRig.Keys(ControlKey.X));
            Assert.AreEqual(SasPhase.Settle, rig.S.SasPhase, "released: the rotation is braked before the target is captured");
        }

        [Test]
        public void TheTargetIsNotCapturedWhileTheCraftStillTurns()
        {
            SasRig rig = new SasRig(thrustOffsetX: 0.0);
            rig.Run(0.04, SasRig.BurnWithSasAndRcs());
            rig.Run(5, t => SasRig.Keys(ControlKey.W)); // a long pitch pulse: the craft turns
            Assert.Greater(Vec3.Length(rig.S.Omega), 0.01);
            rig.Step(default);
            Assert.AreEqual(SasPhase.Settle, rig.S.SasPhase);
            int steps = 0;
            while (rig.S.SasPhase == SasPhase.Settle && steps++ < 5000) rig.Step(default);
            Assert.AreEqual(SasPhase.Hold, rig.S.SasPhase);
            Assert.Less(Vec3.Length(rig.S.Omega), rig.P.Craft.Sas.CaptureRate * 1.5, "captured only after the rate dropped");
            Assert.Less(SasRig.Angle(rig.S.Orientation, rig.S.SasTarget), 0.01);
        }

        // (c) no RCS: the gyro alone holds what it can and then control is lost honestly
        [Test]
        public void WithoutTheRcsTheGyroHoldsASmallDisturbanceAndThenLosesControl()
        {
            SasRig rig = new SasRig(thrustOffsetX: 0.0075); // 4.5 kN*m at full thrust: inside the gyro torque (8 kN*m), but its store fills up
            rig.Run(0.04, t => SasRig.Keys(ControlKey.X, ControlKey.T));
            double worstEarly = 0;
            double saturationTime = double.NaN;
            for (int i = 0; i < 4000; i++) // 80 s
            {
                rig.Step(SasRig.Keys(ControlKey.X));
                Assert.IsTrue(SasRig.Finite(rig.S), "finite at " + rig.Time);
                if (rig.Time < 4.0) worstEarly = Math.Max(worstEarly, rig.ErrorDeg);
                if (double.IsNaN(saturationTime) && GyroModel.Saturation(rig.P.Craft.Gyro, rig.S.GyroH) > 0.95) saturationTime = rig.Time;
                Assert.LessOrEqual(GyroModel.Saturation(rig.P.Craft.Gyro, rig.S.GyroH), 1.0 + 1e-9, "the store never exceeds its capacity");
            }
            Console.WriteLine($"SAS without RCS: error in the first 4 s {worstEarly:F3} deg, gyro full at {saturationTime:F1} s, final error {rig.ErrorDeg:F1} deg");
            Assert.Less(worstEarly, 1.0, "held while the gyro has room");
            Assert.IsFalse(double.IsNaN(saturationTime), "the store fills up under a constant torque");
            Assert.Greater(rig.ErrorDeg, 10.0, "after the saturation the control is lost");
            Assert.AreEqual(0, rig.S.Dumping, "RCS is off: no unloading");
        }

        [Test]
        public void WithoutTheRcsALargeDisturbanceIsNotHeldFromTheStart_NoNaN()
        {
            SasRig rig = new SasRig(); // 30 kN*m against 8 kN*m of the gyro
            rig.Run(0.04, t => SasRig.Keys(ControlKey.X, ControlKey.T));
            for (int i = 0; i < 3000; i++)
            {
                rig.Step(SasRig.Keys(ControlKey.X));
                Assert.IsTrue(SasRig.Finite(rig.S), "finite at " + rig.Time);
            }
            Assert.Greater(rig.MaxErrorDeg, 30.0);
            Assert.LessOrEqual(Math.Abs(rig.S.SasIntegral.Z), 0.9 * rig.P.Craft.Gyro.MaxTorque + 1e-9, "the integral is limited to what the gyro can give");
        }

        [Test]
        public void SwitchingTheRcsOffLimitsTheIntegralAtOnce_AndTheEmptyTankDoesTheSame()
        {
            SasRig rig = new SasRig();
            rig.Run(0.04, SasRig.BurnWithSasAndRcs());
            rig.Run(40, t => SasRig.Keys(ControlKey.X));
            Assert.Greater(Math.Abs(rig.S.SasIntegral.Z), 25000.0);
            rig.Step(SasRig.Keys(ControlKey.X, ControlKey.R)); // RCS off
            Assert.LessOrEqual(Math.Abs(rig.S.SasIntegral.Z), 0.9 * rig.P.Craft.Gyro.MaxTorque + 1e-9);
            Assert.IsTrue(SasRig.Finite(rig.S));

            SasRig small = new SasRig(rcsTank: 20.0); // runs dry after a few seconds
            small.Run(0.04, SasRig.BurnWithSasAndRcs());
            for (int i = 0; i < 4000; i++)
            {
                small.Step(SasRig.Keys(ControlKey.X));
                Assert.IsTrue(SasRig.Finite(small.S), "finite at " + small.Time);
                Assert.GreaterOrEqual(small.S.RcsFuel, 0.0);
            }
            Assert.AreEqual(0.0, small.S.RcsFuel, 1e-9, "the tank was emptied");
            Assert.LessOrEqual(Math.Abs(small.S.SasIntegral.Z), 0.9 * small.P.Craft.Gyro.MaxTorque + 1e-9);
        }

        [Test]
        public void EngagingOnASpinningCraftBrakesItAndCapturesTheOrientation()
        {
            SasRig rig = new SasRig(thrustOffsetX: 0.0);
            rig.S.Omega = new Vec3(0.2, 0.05, -0.1);
            rig.Run(0.04, t => SasRig.Keys(ControlKey.R, ControlKey.T));
            rig.Run(120, t => default);
            Assert.AreEqual(SasPhase.Hold, rig.S.SasPhase);
            Assert.Less(Vec3.Length(rig.S.Omega), 1e-3);
            Assert.Less(rig.ErrorDeg, 0.2);
            Assert.IsTrue(SasRig.Finite(rig.S));
        }

        [Test]
        public void SwitchingOffResetsTheEstimate_AndOnAgainStartsFromTheCurrentOrientation()
        {
            SasRig rig = new SasRig();
            rig.Run(0.04, SasRig.BurnWithSasAndRcs());
            rig.Run(40, t => SasRig.Keys(ControlKey.X));
            Assert.Greater(Math.Abs(rig.S.SasIntegral.Z), 20000.0);
            rig.Step(SasRig.Keys(ControlKey.X, ControlKey.T));
            Assert.AreEqual(0, rig.S.SasOn);
            FlightFixture.AssertNear(Vec3.Zero, rig.S.SasIntegral, 0.0);
            rig.Step(SasRig.Keys(ControlKey.X));
            rig.Step(SasRig.Keys(ControlKey.X, ControlKey.T));
            Assert.AreEqual(1, rig.S.SasOn);
            Assert.Less(SasRig.Angle(rig.S.Orientation, rig.S.SasTarget), 0.01, "the target is the orientation of the moment of switching on");
            FlightFixture.AssertNear(Vec3.Zero, rig.S.SasIntegral, 1000.0, "the estimate starts again (it has only had one step to grow)");
        }

        [Test]
        public void OnTheGroundTheTargetFollowsAndTheEstimateStartsAtLiftOff()
        {
            FlightParams p = FlightFixture.Params();
            FlightState s = SpawnBuilder.OnSurface(p.Craft, p.Surface, 0.3, 0.2, 0.0, 0.0);
            InputLatch latch = default;
            ControlInput c = InputMapper.Map(SasRig.Keys(ControlKey.T, ControlKey.R), ref latch);
            FlightKernel.Step(ref s, c, p, 0.0, 0.02, out _);
            Assert.AreEqual(1, s.SasOn);
            Assert.AreEqual(SasPhase.Hold, s.SasPhase);
            Assert.Less(SasRig.Angle(s.Orientation, s.SasTarget), 1e-9);
            FlightFixture.AssertNear(Vec3.Zero, s.SasIntegral, 0.0);
        }

        [Test]
        public void StateFromAnOlderSaveWorks_AllZeroSasFields()
        {
            // FlightState written before stage 2b.1: no SAS fields (zeros, including the all-zero target quaternion)
            FlightParams p = FlightFixture.DeepSpace();
            FlightState s = FlightFixture.SpaceCraft(p);
            s.SasTarget = default;
            ControlInput off = default;
            FlightKernel.Step(ref s, off, p, 0, 0.02, out _);
            Assert.AreEqual(0, s.SasOn);
            Assert.IsTrue(SasRig.Finite(s));
            s.SasTarget = default;
            ControlInput on = new ControlInput { SasOn = true, RcsOn = true };
            for (int i = 0; i < 200; i++) FlightKernel.Step(ref s, on, p, i * 0.02, 0.02, out _);
            Assert.AreEqual(1, s.SasOn);
            Assert.IsTrue(SasRig.Finite(s));
            Assert.Less(SasController.ErrorAngle(s), 1e-9);
        }

        [Test]
        public void WithoutTheSasTheKernelBehavesAsBefore_ExtraStateStaysIdle()
        {
            SasRig rig = new SasRig();
            rig.Run(20, t => SasRig.Keys(ControlKey.X, ControlKey.R));
            Assert.AreEqual(0, rig.S.SasOn);
            FlightFixture.AssertNear(Vec3.Zero, rig.S.SasIntegral, 0.0);
            Assert.AreEqual(0.0, rig.ErrorDeg);
        }

        [Test]
        public void TheRcsUnloadingStillWorksUnderTheSas()
        {
            SasRig rig = new SasRig(thrustOffsetX: 0.0);
            rig.S.GyroH = new Vec3(0.95 * rig.P.Craft.Gyro.MomentumCapacity, 0, 0);
            rig.Run(0.04, t => SasRig.Keys(ControlKey.R, ControlKey.T));
            rig.Run(120, t => default);
            Assert.LessOrEqual(Math.Abs(rig.S.GyroH.X), 0.2 * rig.P.Craft.Gyro.MomentumCapacity + 1e-6);
            Assert.Less(rig.ErrorDeg, 0.5, "the attitude is held while the RCS relieves the gyro");
            Assert.IsTrue(SasRig.Finite(rig.S));
        }

        // (d) determinism, batch identity, segments
        private static SasRig RunScenario(bool batch)
        {
            SasRig rig = new SasRig();
            Func<double, KeyState> keys = t =>
            {
                if (t < 0.03) return SasRig.Keys(ControlKey.X, ControlKey.R, ControlKey.T);
                if (t > 30 && t < 30.5) return SasRig.Keys(ControlKey.X, ControlKey.W);
                if (t > 50 && t < 50.4) return SasRig.Keys(ControlKey.X, ControlKey.E);
                if (t > 70 && t < 70.02) return SasRig.Keys(ControlKey.X, ControlKey.T);
                if (t > 75 && t < 75.02) return SasRig.Keys(ControlKey.X, ControlKey.T);
                return SasRig.Keys(ControlKey.X);
            };
            if (!batch) { rig.Run(100, keys); return rig; }
            // the same run through the structure-of-arrays batch path
            FlightSoA soa = FlightSoA.Allocate(1);
            soa.Store(0, rig.S);
            ControlInput[] input = new ControlInput[1];
            StepReport[] report = new StepReport[1];
            int n = (int) Math.Round(100 / SasRig.Dt);
            for (int i = 0; i < n; i++)
            {
                input[0] = InputMapper.Map(keys(rig.Time), ref rig.Latch);
                FlightBatch.StepAll(soa, input, rig.P, rig.Time, SasRig.Dt, report);
                rig.S = soa.Load(0);
                RecorderSample sample;
                sample.Time = rig.Time + SasRig.Dt;
                sample.Throttle = rig.S.Throttle;
                sample.ThrustDirection = DQuat.Rotate(rig.S.Orientation, Vec3.UnitY);
                sample.Sas = rig.S.SasOn;
                sample.Resting = rig.S.Mode != FlightMode.Flying;
                ThrustSegmentRecorder.Update(ref rig.Recorder, sample, rig.RecorderCfg);
                while (ThrustSegmentRecorder.TryDequeue(ref rig.Recorder, rig.Time + SasRig.Dt, rig.RecorderCfg, out ThrustSegment seg)) rig.Segments.Add(seg);
                rig.Time += SasRig.Dt;
            }
            return rig;
        }

        private static void AssertSame(FlightState a, FlightState b)
        {
            Assert.AreEqual(a.Orientation, b.Orientation);
            Assert.AreEqual(a.Omega, b.Omega);
            Assert.AreEqual(a.GyroH, b.GyroH);
            Assert.AreEqual(a.Position, b.Position);
            Assert.AreEqual(a.Velocity, b.Velocity);
            Assert.AreEqual(a.MainFuel, b.MainFuel);
            Assert.AreEqual(a.RcsFuel, b.RcsFuel);
            Assert.AreEqual(a.Throttle, b.Throttle);
            Assert.AreEqual(a.SasOn, b.SasOn);
            Assert.AreEqual(a.SasPhase, b.SasPhase);
            Assert.AreEqual(a.SasTarget, b.SasTarget);
            Assert.AreEqual(a.SasIntegral, b.SasIntegral);
        }

        [Test]
        public void TwoRunsGiveBitIdenticalStates()
        {
            AssertSame(RunScenario(false).S, RunScenario(false).S);
        }

        [Test]
        public void TheBatchPathGivesTheSameStateAndTheSameSegmentsAsTheSingleCraftPath()
        {
            SasRig single = RunScenario(false), batch = RunScenario(true);
            AssertSame(single.S, batch.S);
            Assert.AreEqual(single.Segments.Count, batch.Segments.Count);
            for (int i = 0; i < single.Segments.Count; i++)
            {
                Assert.AreEqual(single.Segments[i].StartTime, batch.Segments[i].StartTime);
                Assert.AreEqual(single.Segments[i].Duration, batch.Segments[i].Duration);
                Assert.AreEqual(single.Segments[i].Throttle, batch.Segments[i].Throttle);
                Assert.AreEqual(single.Segments[i].Direction, batch.Segments[i].Direction);
                Assert.AreEqual(single.Segments[i].Sas, batch.Segments[i].Sas);
            }
            Assert.Greater(single.Segments.Count, 3);
        }

        [Test]
        public void TheSegmentsCarryTheSasBit_AndAChangeClosesTheSegment()
        {
            RecorderState r = default;
            RecorderConfig cfg = RecorderConfig.Default();
            List<ThrustSegment> sent = new List<ThrustSegment>();
            for (int i = 0; i <= 800; i++)
            {
                RecorderSample sample;
                sample.Time = 1 + i * 0.05;
                sample.Throttle = 1.0;
                sample.ThrustDirection = Vec3.UnitY;
                sample.Sas = sample.Time >= 10.0 ? 1 : 0;
                sample.Resting = false;
                ThrustSegmentRecorder.Update(ref r, sample, cfg);
                while (ThrustSegmentRecorder.TryDequeue(ref r, sample.Time, cfg, out ThrustSegment seg)) sent.Add(seg);
            }
            Assert.GreaterOrEqual(sent.Count, 2);
            Assert.AreEqual(0, sent[0].Sas);
            Assert.AreEqual(10.0, sent[0].StartTime + sent[0].Duration, 0.06, "the first segment ends when the SAS goes on");
            Assert.AreEqual(1, sent[1].Sas);
        }

        [Test]
        public void TheKnownResultIsStable_ReferenceForTheServerTest()
        {
            SasRig rig = RunScenario(false);
            Console.WriteLine($"SAS reference run: orientation {rig.S.Orientation.W:R} {rig.S.Orientation.X:R} {rig.S.Orientation.Y:R} {rig.S.Orientation.Z:R} integralZ {rig.S.SasIntegral.Z:R} rcsFuel {rig.S.RcsFuel:R}");
            Assert.IsTrue(SasRig.Finite(rig.S));
        }

        [Test]
        public void TelemetryShowsTheSasState()
        {
            SasRig rig = new SasRig();
            rig.Run(0.04, SasRig.BurnWithSasAndRcs());
            rig.Run(2, t => SasRig.Keys(ControlKey.X));
            FlightTelemetry t0 = FlightTelemetry.Compute(rig.S, rig.P);
            Assert.IsTrue(t0.SasOn);
            Assert.AreEqual(rig.ErrorDeg, t0.SasErrorDegrees, 1e-12);
            Assert.AreEqual(rig.S.SasPhase, t0.SasPhase);
            FlightTelemetry off = FlightTelemetry.Compute(FlightFixture.SpaceCraft(rig.P), rig.P);
            Assert.IsFalse(off.SasOn);
            Assert.AreEqual(0.0, off.SasErrorDegrees);
        }
    }

    public class SasConfigTests
    {
        [Test]
        public void TheDefaultsAreInTheFlightConfig()
        {
            SasConfig d = SasConfig.Default();
            Assert.AreEqual(d.NaturalFrequency, FlightConfig.Default().Sas.NaturalFrequency);
            Assert.Greater(d.NaturalFrequency, 0.0);
            Assert.Greater(d.CaptureRate, 0.0);
        }

        [Test]
        public void TheJsonSectionIsReadAndPartialFilesKeepTheDefaults()
        {
            FlightConfig f = GameConfigLoader.ParseFlight("{\"sas\": {\"naturalFrequency\": 1.5, \"integralTime\": 7}}");
            Assert.AreEqual(1.5, f.Sas.NaturalFrequency);
            Assert.AreEqual(7.0, f.Sas.IntegralTime);
            Assert.AreEqual(SasConfig.Default().Damping, f.Sas.Damping, "what the file does not say comes from the defaults");
            FlightConfig none = GameConfigLoader.ParseFlight("{\"dryMass\": 100}");
            Assert.AreEqual(SasConfig.Default().MaxRate, none.Sas.MaxRate);
        }

        [Test]
        public void TheSharedFileMatchesTheDefaults()
        {
            string dir = FindConfigDir();
            if (dir == null) Assert.Ignore("Assets/_game/Config not found");
            GameConfig config = GameConfigLoader.FromDirectory(dir);
            SasConfig file = config.Flight.Sas, d = SasConfig.Default();
            Assert.AreEqual(d.NaturalFrequency, file.NaturalFrequency);
            Assert.AreEqual(d.Damping, file.Damping);
            Assert.AreEqual(d.IntegralTime, file.IntegralTime);
            Assert.AreEqual(d.MaxRate, file.MaxRate);
            Assert.AreEqual(d.CaptureRate, file.CaptureRate);
            Assert.AreEqual(d.IntegralLimit, file.IntegralLimit);
            Assert.AreEqual(d.AuthorityMargin, file.AuthorityMargin);
        }

        [Test]
        public void ADisabledConfigDoesNotProduceNaN()
        {
            SasRig rig = new SasRig();
            rig.P.Craft.Sas = default; // all zeros: no controller
            rig.Run(0.04, SasRig.BurnWithSasAndRcs());
            rig.Run(20, t => SasRig.Keys(ControlKey.X));
            Assert.IsTrue(SasRig.Finite(rig.S));
            SasRig noIntegral = new SasRig();
            noIntegral.P.Craft.Sas.IntegralTime = 0.0;
            noIntegral.Run(0.04, SasRig.BurnWithSasAndRcs());
            noIntegral.Run(60, t => SasRig.Keys(ControlKey.X));
            Assert.IsTrue(SasRig.Finite(noIntegral.S));
            Assert.Greater(noIntegral.ErrorDeg, 0.5, "without the integral a static error stays");
        }

        private static string FindConfigDir()
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
}

namespace Orbital.Tests
{
    /// <summary>The real flight: lift-off from the pad with the default craft (5 cm offset, 200 kg of RCS propellant), as the client flies it.</summary>
    public class SasLiftOffTests
    {
        private static double Tilt(in FlightState s, in FlightParams p) => FlightTelemetry.Compute(s, p).TiltDegrees;

        [Test]
        public void LiftOffWithSasAndRcs_HoldsTheRocketUntilTheRcsPropellantRunsOut()
        {
            FlightParams p = FlightFixture.Params();
            FlightState s = SpawnBuilder.OnSurface(p.Craft, p.Surface, 0.0, 0.0, 0.0, 0.0);
            InputLatch latch = default;
            double t = 0, tiltAt15 = 0, worstTo15 = 0, tiltAt60 = 0, emptyAt = double.NaN;
            for (int i = 0; i < 3500; i++) // 70 s
            {
                KeyState keys = default;
                ControlKeys.Set(ref keys, ControlKey.X, true);
                if (i == 0) { ControlKeys.Set(ref keys, ControlKey.R, true); ControlKeys.Set(ref keys, ControlKey.T, true); }
                ControlInput c = InputMapper.Map(keys, ref latch);
                FlightKernel.Step(ref s, c, p, t, 0.02, out StepReport _);
                LandedSupport.HoldUpright(ref s, p.Surface);
                t += 0.02;
                if (t <= 15.0) worstTo15 = Math.Max(worstTo15, Tilt(s, p));
                if (Math.Abs(t - 15.0) < 0.011) tiltAt15 = Tilt(s, p);
                if (Math.Abs(t - 60.0) < 0.011) tiltAt60 = Tilt(s, p);
                if (double.IsNaN(emptyAt) && s.RcsFuel <= 0.0) emptyAt = t;
                Assert.IsTrue(SasRig.Finite(s), "finite at " + t);
            }
            Console.WriteLine($"Lift-off with SAS+RCS: worst tilt in 15 s {worstTo15:F2} deg, at 15 s {tiltAt15:F2}, RCS empty at {emptyAt:F1} s, tilt at 60 s {tiltAt60:F1} deg, mode {s.Mode}");
            Assert.Less(worstTo15, 3.0, "while the RCS has propellant the rocket does not tip over");
            Assert.IsFalse(double.IsNaN(emptyAt), "200 kg of RCS propellant do not last: the constant 30 kN*m costs about 7.6 kg/s");
        }
    }
}
