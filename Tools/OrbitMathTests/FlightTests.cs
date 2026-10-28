using System;
using NUnit.Framework;
using Orbital.Core.Flight;

namespace Orbital.Tests
{
    public static class FlightFixture
    {
        public const double EarthNu = 3.98588e14;
        public const double EarthRadius = 6.371e6;
        public const double EarthOmega = 2.0 * Math.PI / 86164.0905;

        public static SurfaceData Earth(double omega = EarthOmega) => new SurfaceData
        {
            Kind = SurfaceKind.Sphere,
            Radius = EarthRadius,
            Axis = new Vec3(0, 1, 0),
            EquatorX = new Vec3(1, 0, 0),
            EquatorY = new Vec3(0, 0, -1), // Axis x EquatorX
            Omega = omega,
            Phase0 = 0,
        };

        public static FlightParams Params(double omega = EarthOmega, bool rcsMass = true)
        {
            FlightConfig c = FlightConfig.Default();
            if (!rcsMass) c.RcsPropellantCapacity = 0;
            return new FlightParams { Craft = c, Surface = Earth(omega), Nu = EarthNu, MaxLandingSpeed = 5.0 };
        }

        /// <summary>Far from any planet: gravity is negligible, no ground.</summary>
        public static FlightParams DeepSpace()
        {
            FlightParams p = Params(0.0);
            p.Nu = 1e-3;
            p.Surface.Radius = 1.0;
            p.Craft.ThrustOffset = Vec3.Zero; // a perfectly aligned engine; the offset torque has its own test
            return p;
        }

        public static FlightState SpaceCraft(in FlightParams p)
        {
            FlightState s = FlightKernel.Fresh(p.Craft);
            s.Mode = FlightMode.Flying;
            s.Position = new Vec3(0, 1e12, 0);
            s.Velocity = Vec3.Zero;
            return s;
        }

        public static void AssertNear(Vec3 expected, Vec3 actual, double tol, string msg = "")
        {
            Assert.LessOrEqual(Vec3.Length(expected - actual), tol, msg + " expected " + expected.X + "," + expected.Y + "," + expected.Z + " actual " + actual.X + "," + actual.Y + "," + actual.Z);
        }
    }

    public class EngineTests
    {
        private static readonly EngineConfig E = FlightConfig.Default().Engine;

        [Test]
        public void ThrustAndMassFlow_FollowTheFormulas()
        {
            Assert.AreEqual(300000.0, EngineModel.Thrust(E, 0.5), 1e-9);
            double mdot = EngineModel.MassFlow(E, 1.0);
            Assert.AreEqual(600000.0 / (450 * 9.80665), mdot, 1e-9);
            Assert.AreEqual(mdot / 2, EngineModel.MassFlow(E, 0.5), 1e-12);
        }

        [Test]
        public void SmoothThrottle_MovesAtQuarterPerSecond_AndStaysInRange()
        {
            double t = 0;
            for (int i = 0; i < 200; i++) t = EngineModel.UpdateThrottle(E, t, +1, false, false, 0.01);
            Assert.AreEqual(0.5, t, 1e-9, "2 s of shift");
            for (int i = 0; i < 100; i++) t = EngineModel.UpdateThrottle(E, t, -1, false, false, 0.01);
            Assert.AreEqual(0.25, t, 1e-9, "1 s of ctrl");
            for (int i = 0; i < 1000; i++) t = EngineModel.UpdateThrottle(E, t, +1, false, false, 0.01);
            Assert.AreEqual(1.0, t, 0, "clamped to 1");
            for (int i = 0; i < 1000; i++) t = EngineModel.UpdateThrottle(E, t, -1, false, false, 0.01);
            Assert.AreEqual(0.0, t, 0, "clamped to 0");
        }

        [Test]
        public void FullAndZero_Jump_ZeroWins()
        {
            Assert.AreEqual(1.0, EngineModel.UpdateThrottle(E, 0.1, 0, true, false, 0.01));
            Assert.AreEqual(0.0, EngineModel.UpdateThrottle(E, 0.9, 0, false, true, 0.01));
            Assert.AreEqual(0.0, EngineModel.UpdateThrottle(E, 0.9, 1, true, true, 0.01));
        }

        [Test]
        public void Fuel_NeverGoesBelowZero_AndThrustFadesWhenTheTankRunsDry()
        {
            double fuel = 10;
            EngineModel.Burn(E, 1.0, fuel, 5000, 1.0, out double thrust, out double used, out double dv);
            Assert.AreEqual(10.0, used, 1e-12, "only what is in the tank");
            Assert.AreEqual(10.0 * E.Isp * EngineModel.G0 / 1.0, thrust, 1e-6, "mean thrust over the step");
            Assert.Less(thrust, E.MaxThrust);
            fuel -= used;
            EngineModel.Burn(E, 1.0, fuel, 5000, 1.0, out thrust, out used, out dv);
            Assert.AreEqual(0.0, used);
            Assert.AreEqual(0.0, thrust);
            Assert.AreEqual(0.0, dv);
        }

        [Test]
        public void SumOfStepDeltaV_EqualsTsiolkovsky()
        {
            double fuel = 40000, dry = 5000, total = 0, burned = 0;
            for (int i = 0; i < 600; i++)
            {
                EngineModel.Burn(E, 0.8, fuel, dry, 0.1, out _, out double used, out double dv);
                fuel -= used;
                burned += used;
                total += dv;
            }
            Assert.AreEqual(EngineModel.DeltaV(E.Isp, 45000, burned), total, 1e-6);
            Assert.AreEqual(0.8 * 600000.0 / (450 * 9.80665) * 60, burned, 1e-9);
        }

        [Test]
        public void KernelBurnInDeepSpace_GainsTsiolkovskyDeltaV()
        {
            FlightParams p = FlightFixture.DeepSpace();
            p.Craft.RcsPropellantCapacity = 0;
            FlightState s = FlightFixture.SpaceCraft(p);
            s.Throttle = 1.0;
            double m0 = FlightKernel.TotalMass(p.Craft, s);
            ControlInput none = default;
            for (int i = 0; i < 1000; i++) FlightKernel.Step(ref s, none, p, i * 0.1, 0.1, out _);
            double burned = 40000 - s.MainFuel;
            double expected = EngineModel.DeltaV(450, m0, burned);
            Assert.AreEqual(expected, Vec3.Length(s.Velocity), expected * 1e-6, "velocity along +Y equals the rocket equation");
            Assert.Greater(s.Velocity.Y, 0);
            Assert.AreEqual(100.0 * 600000.0 / (450 * 9.80665), burned, 1e-6);
        }

        [Test]
        public void KernelStopsThrustWhenTheTankIsEmpty()
        {
            FlightParams p = FlightFixture.DeepSpace();
            p.Craft.RcsPropellantCapacity = 0;
            FlightState s = FlightFixture.SpaceCraft(p);
            s.MainFuel = 100;
            s.Throttle = 1.0;
            ControlInput none = default;
            for (int i = 0; i < 20; i++) FlightKernel.Step(ref s, none, p, i * 0.1, 0.1, out _);
            Assert.AreEqual(0.0, s.MainFuel);
            double v = s.Velocity.Y;
            for (int i = 0; i < 20; i++) FlightKernel.Step(ref s, none, p, 2 + i * 0.1, 0.1, out StepReport r);
            Assert.AreEqual(v, s.Velocity.Y, 1e-9, "no more acceleration");
        }
    }

    public class AngularTests
    {
        [Test]
        public void Quaternion_RotatesAndRoundTrips()
        {
            DQuat q = DQuat.FromAxisAngle(new Vec3(0, 0, 1), Math.PI / 2);
            FlightFixture.AssertNear(new Vec3(0, 1, 0), DQuat.Rotate(q, new Vec3(1, 0, 0)), 1e-15);
            Vec3 v = new Vec3(0.3, -1.2, 2.5);
            DQuat r = DQuat.FromAxisAngle(new Vec3(1, 2, 3), 0.77);
            FlightFixture.AssertNear(v, DQuat.RotateInverse(r, DQuat.Rotate(r, v)), 1e-14);
            Vec3 a = Vec3.Normalize(new Vec3(1, 2, 3)), b = Vec3.Normalize(new Vec3(-2, 0.5, 1));
            FlightFixture.AssertNear(b, DQuat.Rotate(DQuat.FromTo(a, b), a), 1e-14);
        }

        [Test]
        public void TorqueFree_SymmetricTop_ConservesAngularMomentumAndEnergy()
        {
            Vec3 inertia = FlightConfig.Default().Inertia;
            AngularState s = new AngularState { Orientation = DQuat.FromAxisAngle(new Vec3(1, 1, 0), 0.4), Omega = new Vec3(0.3, 0.5, 0.1) };
            Vec3 l0 = RigidBodyAngular.TotalMomentumInertial(s, inertia, Vec3.Zero);
            double e0 = RigidBodyAngular.RotationalEnergy(inertia, s.Omega);
            for (int i = 0; i < 6000; i++) RigidBodyAngular.Step(ref s, inertia, Vec3.Zero, Vec3.Zero, Vec3.Zero, 0.01);
            Assert.LessOrEqual(Vec3.Length(RigidBodyAngular.TotalMomentumInertial(s, inertia, Vec3.Zero) - l0), Vec3.Length(l0) * 1e-9);
            Assert.AreEqual(e0, RigidBodyAngular.RotationalEnergy(inertia, s.Omega), e0 * 1e-9);
            double n = Math.Sqrt(s.Orientation.W * s.Orientation.W + s.Orientation.X * s.Orientation.X + s.Orientation.Y * s.Orientation.Y + s.Orientation.Z * s.Orientation.Z);
            Assert.AreEqual(1.0, n, 1e-14, "quaternion stays normalized");
        }

        [Test]
        public void TorqueFree_AsymmetricBody_ConservesAngularMomentum()
        {
            Vec3 inertia = new Vec3(1e6, 2e6, 3e6);
            AngularState s = new AngularState { Orientation = DQuat.Identity, Omega = new Vec3(0.05, 0.4, 0.02) };
            Vec3 l0 = RigidBodyAngular.TotalMomentumInertial(s, inertia, Vec3.Zero);
            for (int i = 0; i < 4000; i++) RigidBodyAngular.Step(ref s, inertia, Vec3.Zero, Vec3.Zero, Vec3.Zero, 0.01);
            Assert.LessOrEqual(Vec3.Length(RigidBodyAngular.TotalMomentumInertial(s, inertia, Vec3.Zero) - l0), Vec3.Length(l0) * 1e-9);
        }

        [Test]
        public void StoredGyroMomentum_IsPartOfTheConservedTotal()
        {
            Vec3 inertia = FlightConfig.Default().Inertia;
            Vec3 h = new Vec3(5000, 0, 2000);
            AngularState s = new AngularState { Orientation = DQuat.FromAxisAngle(new Vec3(0, 1, 1), 0.9), Omega = new Vec3(0.02, 0.1, -0.03) };
            Vec3 l0 = RigidBodyAngular.TotalMomentumInertial(s, inertia, h);
            for (int i = 0; i < 3000; i++) RigidBodyAngular.Step(ref s, inertia, Vec3.Zero, h, Vec3.Zero, 0.01);
            Assert.LessOrEqual(Vec3.Length(RigidBodyAngular.TotalMomentumInertial(s, inertia, h) - l0), Vec3.Length(l0) * 1e-8);
        }

        [Test]
        public void Symmetry_OppositeTorquesGiveMirrorResults()
        {
            Vec3 inertia = FlightConfig.Default().Inertia;
            AngularState a = new AngularState { Orientation = DQuat.Identity }, b = a;
            for (int i = 0; i < 500; i++)
            {
                RigidBodyAngular.Step(ref a, inertia, new Vec3(5e4, 0, 0), Vec3.Zero, Vec3.Zero, 0.01);
                RigidBodyAngular.Step(ref b, inertia, new Vec3(-5e4, 0, 0), Vec3.Zero, Vec3.Zero, 0.01);
            }
            Assert.AreEqual(-a.Omega.X, b.Omega.X, 1e-12);
            Assert.AreEqual(5e4 / 1.7e6 * 5.0, a.Omega.X, 1e-9, "omega = torque / I * t");
            Assert.AreEqual(0.0, a.Omega.Y, 1e-12);
            Assert.AreEqual(0.0, a.Omega.Z, 1e-12);
            // and the torque undone brings the rate back
            for (int i = 0; i < 500; i++) RigidBodyAngular.Step(ref a, inertia, new Vec3(-5e4, 0, 0), Vec3.Zero, Vec3.Zero, 0.01);
            Assert.AreEqual(0.0, a.Omega.X, 1e-12);
        }

        [Test]
        public void RcsTorque_AndPropellantFollowTheNumbers()
        {
            RcsConfig rcs = FlightConfig.Default().Rcs;
            RcsOutput o = RcsModel.Fire(rcs, new Vec3(1, 0, 0), 200, 1.0);
            Assert.AreEqual(5e4, o.Torque.X, 1e-9);
            Assert.AreEqual(5e4 / 2.0 / (200 * 9.80665), o.Propellant, 1e-12);
            RcsOutput half = RcsModel.Fire(rcs, new Vec3(-0.5, 0, 0), 200, 1.0);
            Assert.AreEqual(-2.5e4, half.Torque.X, 1e-9);
            Assert.AreEqual(o.Propellant / 2, half.Propellant, 1e-12);
            RcsOutput idle = RcsModel.Fire(rcs, Vec3.Zero, 200, 1.0);
            Assert.AreEqual(0.0, idle.Propellant);
            RcsOutput starved = RcsModel.Fire(rcs, new Vec3(1, 0, 0), o.Propellant / 4, 1.0);
            Assert.AreEqual(0.25 * 5e4, starved.Torque.X, 1e-6, "torque fades with the propellant");
            Assert.AreEqual(o.Propellant / 4, starved.Propellant, 1e-12);
            Assert.AreEqual(0.0, RcsModel.Fire(rcs, new Vec3(1, 0, 0), 0, 1.0).Torque.X);
        }

        [Test]
        public void Gyro_SaturatesAtCapacity_AndMomentumIsExchangedWithTheBody()
        {
            GyroConfig g = FlightConfig.Default().Gyro;
            Vec3 inertia = FlightConfig.Default().Inertia;
            AngularState s = new AngularState { Orientation = DQuat.Identity };
            Vec3 h = Vec3.Zero;
            double previous = 0;
            for (int i = 0; i < 20000; i++) // 200 s of full pitch command
            {
                Vec3 hdot = GyroModel.PilotHdot(g, h, new Vec3(1, 0, 0));
                RigidBodyAngular.Step(ref s, inertia, Vec3.Zero, h, hdot, 0.01);
                h = GyroModel.ClampStore(g, h + hdot * 0.01);
                Assert.LessOrEqual(Math.Abs(h.X), g.MomentumCapacity + 1e-9, "never above the capacity");
                if (i == 100) previous = s.Omega.X;
            }
            Assert.AreEqual(8000.0 / 1.7e6 * 1.0, previous, 1e-4, "early on the torque is the full 8000 N*m");
            Assert.AreEqual(-g.MomentumCapacity, h.X, g.MomentumCapacity * 1e-3, "store full");
            Assert.AreEqual(g.MomentumCapacity / 1.7e6, s.Omega.X, 1e-6, "craft got the opposite momentum: I*w + H = 0");
            double after = s.Omega.X;
            for (int i = 0; i < 500; i++)
            {
                Vec3 hdot = GyroModel.PilotHdot(g, h, new Vec3(1, 0, 0));
                RigidBodyAngular.Step(ref s, inertia, Vec3.Zero, h, hdot, 0.01);
                h = GyroModel.ClampStore(g, h + hdot * 0.01);
            }
            Assert.AreEqual(after, s.Omega.X, 1e-6, "a saturated gyro adds nothing");
            // the opposite direction is still available
            Vec3 back = GyroModel.PilotHdot(g, h, new Vec3(-1, 0, 0));
            Assert.AreEqual(8000.0, back.X, 1e-9);
        }

        [Test]
        public void Gyro_Dump_Hysteresis_AndDirectionAgainstH()
        {
            FlightConfig c = FlightConfig.Default();
            Vec3 high = new Vec3(0.9 * c.Gyro.MomentumCapacity, 0, -0.5 * c.Gyro.MomentumCapacity);
            Assert.IsTrue(GyroModel.UpdateDumping(c.Gyro, high, false));
            Assert.IsTrue(GyroModel.UpdateDumping(c.Gyro, new Vec3(0.5 * c.Gyro.MomentumCapacity, 0, 0), true), "keeps going between the thresholds");
            Assert.IsFalse(GyroModel.UpdateDumping(c.Gyro, new Vec3(0.5 * c.Gyro.MomentumCapacity, 0, 0), false));
            Assert.IsFalse(GyroModel.UpdateDumping(c.Gyro, new Vec3(0.1 * c.Gyro.MomentumCapacity, 0, 0), true), "stops below DumpStop");
            Vec3 cmd = GyroModel.DumpCommand(c.Gyro, c.Rcs, high);
            Assert.Less(cmd.X, 0);
            Assert.Greater(cmd.Z, 0);
            Assert.AreEqual(0.0, cmd.Y);
        }

        [Test]
        public void KernelDump_RcsRelievesTheGyro_WithoutRotatingTheCraft()
        {
            FlightParams p = FlightFixture.DeepSpace();
            FlightState s = FlightFixture.SpaceCraft(p);
            s.GyroH = new Vec3(0.95 * p.Craft.Gyro.MomentumCapacity, 0, 0);
            ControlInput rcs = new ControlInput { RcsOn = true };
            double fuel0 = s.RcsFuel;
            for (int i = 0; i < 20000 && Math.Abs(s.GyroH.X) > 0.15 * p.Craft.Gyro.MomentumCapacity; i++)
                FlightKernel.Step(ref s, rcs, p, i * 0.01, 0.01, out _);
            Assert.LessOrEqual(Math.Abs(s.GyroH.X), 0.2 * p.Craft.Gyro.MomentumCapacity + 1e-6, "H relieved down to the stop threshold");
            Assert.Less(Vec3.Length(s.Omega), 1e-9, "the craft did not turn while unloading");
            Assert.Less(s.RcsFuel, fuel0, "unloading costs RCS propellant");
            // RCS off: no unloading
            FlightState off = FlightFixture.SpaceCraft(p);
            off.GyroH = new Vec3(0.95 * p.Craft.Gyro.MomentumCapacity, 0, 0);
            for (int i = 0; i < 500; i++) FlightKernel.Step(ref off, default, p, i * 0.01, 0.01, out _);
            Assert.AreEqual(0.95 * p.Craft.Gyro.MomentumCapacity, off.GyroH.X, 1e-9);
        }

        // ---------------------------------------------------------------- the key tests: ground against space

        private static FlightState OnPad(in FlightParams p, double tiltDegrees)
        {
            FlightState s = FlightKernel.Fresh(p.Craft);
            s.Mode = FlightMode.Landed;
            s.Latitude = 0.3;
            s.Longitude = 0.2;
            // craft body +Y is the local vertical, tilted about body Z
            Vec3 up = Vec3.Normalize(Surface.Point(p.Surface, s.Latitude, s.Longitude, 0));
            Vec3 east = Vec3.Normalize(Vec3.Cross(p.Surface.Axis, up));
            DQuat toUp = DQuat.FromTo(Vec3.UnitY, up);
            DQuat tilt = DQuat.FromAxisAngle(Vec3.UnitZ, tiltDegrees * Math.PI / 180);
            s.Orientation = DQuat.Normalize(DQuat.Multiply(toUp, tilt));
            return s;
        }

        /// <summary>Sign of a component that is a real rotation, not numerical dust.</summary>
        private static double Dominant(double v) => Math.Abs(v) > 1e-4 ? Math.Sign(v) : 0.0;

        private static Vec3 OmegaAfter(FlightState s, in FlightParams p, ControlInput input, int steps, double dt = 0.01)
        {
            int start = s.Mode;
            for (int i = 0; i < steps; i++)
            {
                FlightKernel.Step(ref s, input, p, i * dt, dt, out _);
                if (start == FlightMode.Landed && s.Mode != FlightMode.Landed) break;
            }
            return s.Omega;
        }

        [Test]
        public void OnTheGround_TheGyroDoesNotChangeTheAngularVelocity_ButTheRcsDoes()
        {
            FlightParams p = FlightFixture.Params();
            FlightState pad = OnPad(p, 3.0);
            Vec3 free = OmegaAfter(pad, p, default, 100);
            Assert.Greater(Vec3.Length(free), 0.02, "gravity topples a tilted rocket standing on its tail");
            Vec3 against = new Vec3(Dominant(free.X), Dominant(free.Y), Dominant(free.Z)) * -1.0;
            ControlInput gyroOnly = new ControlInput { Pitch = against.X, Roll = against.Y, Yaw = against.Z, RcsOn = false };
            Vec3 withGyro = OmegaAfter(pad, p, gyroOnly, 100);
            double change = Vec3.Length(withGyro - free) / Vec3.Length(free);
            Assert.Less(change, 0.10, "the full gyro torque changes the tipping rate by less than 10 percent: useless on the ground");
            // the RCS (5e4 N*m, 6 times the gyro) does matter
            ControlInput rcs = gyroOnly;
            rcs.RcsOn = true;
            Vec3 withRcs = OmegaAfter(pad, p, rcs, 100);
            Assert.Greater(Vec3.Length(withRcs - free) / Vec3.Length(free), 0.15, "RCS works on the ground");
        }

        [Test]
        public void InSpace_TheGyroTurnsTheCraft_AndTheRcsDoesToo()
        {
            FlightParams p = FlightFixture.DeepSpace();
            FlightState s = FlightFixture.SpaceCraft(p);
            ControlInput gyroOnly = new ControlInput { Pitch = 1 };
            Vec3 w = OmegaAfter(s, p, gyroOnly, 300); // 3 s
            Assert.AreEqual(8000.0 / 1.7e6 * 3.0, w.X, 1e-4, "gyro: about 5e-3 rad/s^2");
            Assert.Greater(w.X, 0.01);
            Assert.AreEqual(0.0, OmegaAfter(s, p, default, 300).X, 1e-12, "nothing happens without input");
            ControlInput rcs = new ControlInput { Pitch = 1, RcsOn = true };
            Vec3 wr = OmegaAfter(s, p, rcs, 300);
            Assert.AreEqual((8000.0 + 5e4) / 1.7e6 * 3.0, wr.X, 2e-4, "RCS adds its torque to the gyro");
            Assert.Greater(wr.X, 3 * w.X);
        }

        [Test]
        public void Rcs_SwitchedOff_DoesNotBurnPropellant()
        {
            FlightParams p = FlightFixture.DeepSpace();
            FlightState s = FlightFixture.SpaceCraft(p);
            for (int i = 0; i < 100; i++) FlightKernel.Step(ref s, new ControlInput { Pitch = 1, Yaw = -1 }, p, i * 0.01, 0.01, out StepReport r);
            Assert.AreEqual(p.Craft.RcsPropellantCapacity, s.RcsFuel);
            for (int i = 0; i < 100; i++) FlightKernel.Step(ref s, new ControlInput { Pitch = 1, RcsOn = true }, p, i * 0.01, 0.01, out StepReport r);
            Assert.AreEqual(p.Craft.RcsPropellantCapacity - 5e4 / 2.0 / (200 * 9.80665) * 1.0, s.RcsFuel, 1e-9);
        }
    }

    public class InputTests
    {
        [Test]
        public void Keys_MapToAxes()
        {
            InputLatch latch = default;
            ControlInput c = InputMapper.Map(new KeyState { W = true, D = true, E = true }, ref latch);
            Assert.AreEqual(1, c.Pitch);
            Assert.AreEqual(1, c.Roll);
            Assert.AreEqual(1, c.Yaw);
            c = InputMapper.Map(new KeyState { S = true, A = true, Q = true }, ref latch);
            Assert.AreEqual(-1, c.Pitch);
            Assert.AreEqual(-1, c.Roll);
            Assert.AreEqual(-1, c.Yaw);
            c = InputMapper.Map(new KeyState { W = true, S = true, A = true, D = true, Q = true, E = true }, ref latch);
            Assert.AreEqual(0, c.Pitch);
            Assert.AreEqual(0, c.Roll);
            Assert.AreEqual(0, c.Yaw);
            Vec3 axes = InputMapper.AxesVector(InputMapper.Map(new KeyState { W = true, D = true, Q = true }, ref latch));
            Assert.AreEqual(new Vec3(1, 1, -1).X, axes.X);
            Assert.AreEqual(1, axes.Y, "roll is about the long axis Y");
            Assert.AreEqual(-1, axes.Z, "yaw is about Z");
        }

        [Test]
        public void ThrottleKeys()
        {
            InputLatch latch = default;
            Assert.AreEqual(1, InputMapper.Map(new KeyState { Shift = true }, ref latch).ThrottleSmooth);
            Assert.AreEqual(-1, InputMapper.Map(new KeyState { Ctrl = true }, ref latch).ThrottleSmooth);
            Assert.AreEqual(0, InputMapper.Map(new KeyState { Shift = true, Ctrl = true }, ref latch).ThrottleSmooth);
            Assert.IsTrue(InputMapper.Map(new KeyState { X = true }, ref latch).ThrottleFull);
            Assert.IsTrue(InputMapper.Map(new KeyState { Z = true }, ref latch).ThrottleZero);
        }

        [Test]
        public void R_TogglesOnPress_NotOnHold()
        {
            InputLatch latch = default;
            Assert.IsFalse(InputMapper.Map(default, ref latch).RcsOn);
            Assert.IsTrue(InputMapper.Map(new KeyState { R = true }, ref latch).RcsOn, "press: on");
            for (int i = 0; i < 100; i++) Assert.IsTrue(InputMapper.Map(new KeyState { R = true }, ref latch).RcsOn, "held: stays on");
            Assert.IsTrue(InputMapper.Map(default, ref latch).RcsOn, "released: stays on");
            Assert.IsFalse(InputMapper.Map(new KeyState { R = true }, ref latch).RcsOn, "second press: off");
            Assert.IsFalse(InputMapper.Map(default, ref latch).RcsOn);
        }
    }

    public class ContactTests
    {
        private static readonly FlightParams P = FlightFixture.Params(rcsMass: false);

        [Test]
        public void Surface_PointAndCoordinates_RoundTrip_AndGroundSpeed()
        {
            SurfaceData s = FlightFixture.Earth();
            foreach (double t in new[] { 0.0, 1234.5, 5e6 })
            {
                Vec3 p = Surface.Point(s, 0.6, -1.2, t);
                Assert.AreEqual(s.Radius, Vec3.Length(p), 1e-6);
                Surface.ToBodyCoordinates(s, p, t, out double lat, out double lon);
                Assert.AreEqual(0.6, lat, 1e-12);
                Assert.AreEqual(-1.2, lon, 1e-12);
                Assert.AreEqual(0.0, Surface.Altitude(s, p), 1e-6);
                Assert.AreEqual(1.0, Vec3.Length(Surface.Normal(s, p)), 1e-15);
            }
            Vec3 eq = Surface.Point(s, 0, 0.3, 100);
            Assert.AreEqual(FlightFixture.EarthOmega * FlightFixture.EarthRadius, Vec3.Length(Surface.GroundVelocity(s, eq)), 1e-9, "equator speed = omega * R");
            Assert.AreEqual(464.6, Vec3.Length(Surface.GroundVelocity(s, eq)), 0.5);
            Assert.AreEqual(0.5, Vec3.Length(Surface.GroundVelocity(s, Surface.Point(s, Math.PI / 3, 0, 0))) / Vec3.Length(Surface.GroundVelocity(s, eq)), 1e-9);
            Assert.Less(Vec3.Length(Surface.GroundVelocity(s, Surface.Point(s, Math.PI / 2, 0, 0))), 1e-6);
        }

        private static FlightState Pad()
        {
            FlightState s = FlightKernel.Fresh(P.Craft);
            s.Mode = FlightMode.Landed;
            s.Latitude = 0.5;
            s.Longitude = 0.1;
            Vec3 up = Vec3.Normalize(Surface.Point(P.Surface, 0.5, 0.1, 0));
            s.Orientation = DQuat.FromTo(Vec3.UnitY, up);
            return s;
        }

        [Test]
        public void LandedCraft_FollowsTheRotatingGround()
        {
            FlightState s = Pad();
            for (int i = 0; i < 100; i++) FlightKernel.Step(ref s, default, P, 1000 + i * 1.0, 1.0, out _);
            Assert.AreEqual(FlightMode.Landed, s.Mode);
            Vec3 expected = Surface.Point(P.Surface, 0.5, 0.1, 1100);
            FlightFixture.AssertNear(expected, s.Position, 1e-6);
            Assert.AreEqual(FlightFixture.EarthOmega * FlightFixture.EarthRadius * Math.Cos(0.5), Vec3.Length(s.Velocity), 1e-6);
        }

        [Test]
        public void LiftOff_NeedsThrustAboveWeight()
        {
            double weightThrottle = (5000 + 40000) * FlightFixture.EarthNu / (FlightFixture.EarthRadius * FlightFixture.EarthRadius) / 600000.0;
            FlightState low = Pad();
            low.Throttle = weightThrottle * 0.98;
            ControlInput none = default;
            FlightKernel.Step(ref low, none, P, 0, 0.1, out StepReport r1);
            Assert.AreEqual(FlightMode.Landed, low.Mode, "98 percent of the weight: stays");
            FlightState high = Pad();
            high.Throttle = weightThrottle * 1.02;
            FlightKernel.Step(ref high, none, P, 0, 0.1, out StepReport r2);
            Assert.AreEqual(FlightMode.Flying, high.Mode, "102 percent: lifts off");
        }

        [Test]
        public void Takeoff_InheritsTheGroundVelocity()
        {
            FlightState s = Pad();
            s.Throttle = 1.0;
            Vec3 ground = Surface.GroundVelocity(P.Surface, Surface.Point(P.Surface, 0.5, 0.1, 0));
            for (int i = 0; i < 20; i++) FlightKernel.Step(ref s, default, P, i * 0.05, 0.05, out _);
            Assert.AreEqual(FlightMode.Flying, s.Mode);
            Vec3 relative = s.Velocity - Surface.GroundVelocity(P.Surface, s.Position);
            Assert.Greater(Vec3.Length(ground), 300);
            Assert.Less(Vec3.Length(relative), 6.0, "after 1 s it has only climbed");
            Assert.Greater(Vec3.Dot(relative, Surface.Normal(P.Surface, s.Position)), 1.0);
            Assert.AreEqual(Vec3.Length(ground), Vec3.Length(s.Velocity), 10.0);
        }

        [Test]
        public void Touchdown_IsJudgedRelativeToTheGround()
        {
            double t = 500;
            Vec3 pos = Surface.Point(P.Surface, 0.0, 0.0, t) * (1 + 1.0 / FlightFixture.EarthRadius); // 1 m up
            Vec3 ground = Surface.GroundVelocity(P.Surface, pos);

            FlightState soft = FlightKernel.Fresh(P.Craft);
            soft.Mode = FlightMode.Flying;
            soft.Position = pos;
            soft.Velocity = ground;
            soft.Throttle = 0;
            for (int i = 0; i < 20 && soft.Mode == FlightMode.Flying; i++) FlightKernel.Step(ref soft, default, P, t + i * 0.1, 0.1, out _);
            Assert.AreEqual(FlightMode.Landed, soft.Mode, "moves with the ground, only a 1 m drop");
            Assert.AreEqual(0.0, Surface.Altitude(P.Surface, soft.Position), 1e-6);

            FlightState hard = FlightKernel.Fresh(P.Craft);
            hard.Mode = FlightMode.Flying;
            hard.Position = pos;
            hard.Velocity = Vec3.Zero; // at rest in space, the ground rushes at 465 m/s
            for (int i = 0; i < 20 && hard.Mode == FlightMode.Flying; i++) FlightKernel.Step(ref hard, default, P, t + i * 0.1, 0.1, out _);
            Assert.AreEqual(FlightMode.Crashed, hard.Mode);

            FlightState fall = FlightKernel.Fresh(P.Craft);
            fall.Mode = FlightMode.Flying;
            fall.Position = Surface.Point(P.Surface, 0.0, 0.0, t) * (1 + 100.0 / FlightFixture.EarthRadius);
            fall.Velocity = Surface.GroundVelocity(P.Surface, fall.Position);
            StepReport rep = default;
            for (int i = 0; i < 100 && fall.Mode == FlightMode.Flying; i++) FlightKernel.Step(ref fall, default, P, t + i * 0.1, 0.1, out rep);
            Assert.AreEqual(FlightMode.Crashed, fall.Mode, "a 100 m free fall arrives at 44 m/s");
            Assert.Greater(rep.ImpactSpeed, 40);
            Assert.Less(rep.ImpactSpeed, 48);
            Assert.IsFalse(double.IsNaN(rep.TouchdownTime));
        }
    }

    public class RecorderTests
    {
        private static readonly RecorderConfig C = RecorderConfig.Default();
        private static readonly Vec3 Up = Vec3.UnitY;

        private static RecorderSample S(double t, double throttle, Vec3 dir, bool resting = false) =>
            new RecorderSample { Time = t, Throttle = throttle, ThrustDirection = dir, Resting = resting };

        private static void Run(ref RecorderState r, double from, double to, double throttle, Vec3 dir, double dt = 0.05)
        {
            for (double t = from; t <= to + 1e-9; t += dt) ThrustSegmentRecorder.Update(ref r, S(t, throttle, dir), C);
        }

        [Test]
        public void SteadyBurn_BecomesSegmentsOfAtMostMaxDuration()
        {
            RecorderState r = default;
            Run(ref r, 0, 50, 0.8, Up);
            ThrustSegmentRecorder.Update(ref r, S(50.05, 0, Up), C);
            Assert.AreEqual(3, r.Count, "20 s + 20 s + the rest");
            double total = 0;
            double now = 100;
            while (ThrustSegmentRecorder.TryDequeue(ref r, now, C, out ThrustSegment seg))
            {
                Assert.LessOrEqual(seg.Duration, C.MaxDuration + 0.05);
                Assert.AreEqual(0.8, seg.Throttle, 1e-12);
                total += seg.Duration;
                now += 2;
            }
            Assert.AreEqual(50.05, total, 0.06, "segments cover the whole burn");
        }

        [Test]
        public void SmallThrottleJitter_DoesNotCloseTheSegment()
        {
            RecorderState r = default;
            for (double t = 0; t <= 10; t += 0.05)
                ThrustSegmentRecorder.Update(ref r, S(t, 0.8 + ((int) (t / 0.05) % 2 == 0 ? 0.02 : -0.02), Up), C);
            Assert.AreEqual(0, r.Count);
            Assert.IsTrue(r.Active);
        }

        [Test]
        public void ThrottleStep_ClosesAfterTheMinimumDuration()
        {
            RecorderState r = default;
            Run(ref r, 0, 3, 0.5, Up);
            Run(ref r, 3.05, 6, 0.9, Up);
            ThrustSegmentRecorder.Update(ref r, S(6.05, 0, Up), C);
            Assert.AreEqual(2, r.Count);
            ThrustSegmentRecorder.TryDequeue(ref r, 10, C, out ThrustSegment a);
            ThrustSegmentRecorder.TryDequeue(ref r, 20, C, out ThrustSegment b);
            Assert.AreEqual(0.5, a.Throttle, 1e-12);
            Assert.AreEqual(3.05, a.Duration, 0.06);
            Assert.AreEqual(0.9, b.Throttle, 1e-12);
            Assert.AreEqual(3.05, b.StartTime, 0.06);
        }

        [Test]
        public void TurnBeyondOneDegree_Closes_SmallWobbleDoesNot()
        {
            RecorderState r = default;
            Vec3 wobble = Vec3.Normalize(new Vec3(Math.Sin(0.5 * Math.PI / 180), Math.Cos(0.5 * Math.PI / 180), 0));
            Vec3 turned = Vec3.Normalize(new Vec3(Math.Sin(2.0 * Math.PI / 180), Math.Cos(2.0 * Math.PI / 180), 0));
            Run(ref r, 0, 2, 0.7, Up);
            Run(ref r, 2.05, 4, 0.7, wobble);
            Assert.AreEqual(0, r.Count, "half a degree is below the threshold");
            Run(ref r, 4.05, 6, 0.7, turned);
            Assert.AreEqual(1, r.Count, "two degrees closes the segment");
            ThrustSegmentRecorder.TryDequeue(ref r, 10, C, out ThrustSegment seg);
            Assert.Less(Vec3.Angle(seg.Direction, Up), 1e-12);
        }

        [Test]
        public void ChatteringThrottle_NeverMakesSegmentsShorterThanTheMinimum()
        {
            RecorderState r = default;
            double t = 0;
            for (int i = 0; i < 400; i++)
            {
                double throttle = (i / 4) % 2 == 0 ? 0.3 : 0.9; // flips every 0.2 s
                ThrustSegmentRecorder.Update(ref r, S(t, throttle, Up), C);
                t += 0.05;
            }
            ThrustSegmentRecorder.Update(ref r, S(t, 0, Up), C);
            double now = 100;
            int n = 0;
            while (ThrustSegmentRecorder.TryDequeue(ref r, now, C, out ThrustSegment seg))
            {
                if (n++ < 6) Assert.GreaterOrEqual(seg.Duration, C.MinDuration - 1e-9);
                now += 2;
            }
            Assert.LessOrEqual(n + r.Overflowed, 20 / 0.5 + 1, "at most one segment per half second, however fast the jitter");
        }

        [Test]
        public void OnTheGround_BelowWeight_NothingIsRecorded()
        {
            RecorderState r = default;
            for (double t = 0; t <= 20; t += 0.05) ThrustSegmentRecorder.Update(ref r, S(t, 0.5, Up, resting: true), C);
            Assert.IsFalse(r.Active);
            Assert.AreEqual(0, r.Count);
            // lift-off: the segment starts when thrust exceeds weight
            for (double t = 20.05; t <= 25; t += 0.05) ThrustSegmentRecorder.Update(ref r, S(t, 0.9, Up, resting: false), C);
            Assert.IsTrue(r.Active);
            Assert.AreEqual(20.05, r.Open.StartTime, 1e-9);
        }

        [Test]
        public void SendInterval_Limits_HowFastSegmentsLeave()
        {
            RecorderState r = default;
            for (int k = 0; k < 4; k++)
            {
                Run(ref r, k * 3.0, k * 3.0 + 2.9, 0.2 + 0.2 * k, Up);
                Run(ref r, k * 3.0 + 2.95, k * 3.0 + 2.99, 0.0, Up);
                ThrustSegmentRecorder.Update(ref r, S(k * 3.0 + 3.0, 0.0, Up), C);
            }
            Assert.GreaterOrEqual(r.Count, 3);
            Assert.IsTrue(ThrustSegmentRecorder.TryDequeue(ref r, 20.0, C, out _));
            Assert.IsFalse(ThrustSegmentRecorder.TryDequeue(ref r, 20.4, C, out _), "0.4 s later: too early");
            Assert.IsTrue(ThrustSegmentRecorder.TryDequeue(ref r, 21.1, C, out _));
        }

        [Test]
        public void VeryShortTail_IsCountedNotSent()
        {
            RecorderState r = default;
            ThrustSegmentRecorder.Update(ref r, S(0, 0.9, Up), C);
            ThrustSegmentRecorder.Update(ref r, S(0.05, 0.0, Up), C);
            Assert.AreEqual(0, r.Count);
            Assert.AreEqual(0.9 * 0.05, r.DroppedImpulse, 1e-12);
        }
    }

    public class BatchTests
    {
        private static FlightState MakeCraft(int i, in FlightParams p)
        {
            FlightState s = FlightKernel.Fresh(p.Craft);
            s.Mode = i % 5 == 0 ? FlightMode.Landed : FlightMode.Flying;
            s.Latitude = 0.1 * (i % 7);
            s.Longitude = 0.05 * i;
            s.Position = Surface.Point(p.Surface, s.Latitude, s.Longitude, 0) * (1 + (i % 3) * 20.0 / FlightFixture.EarthRadius);
            s.Velocity = Surface.GroundVelocity(p.Surface, s.Position) + new Vec3(0.1 * i, 3.0, -0.2 * i);
            s.Orientation = DQuat.FromAxisAngle(new Vec3(1, 2, i + 1), 0.05 * i);
            s.Omega = new Vec3(0.001 * i, 0, -0.0005 * i);
            s.Throttle = (i % 4) * 0.3;
            s.MainFuel = 30000 + 100 * i;
            return s;
        }

        private static ControlInput MakeInput(int i) => new ControlInput
        {
            Pitch = (i % 3) - 1, Roll = (i % 2) * 1.0, Yaw = ((i + 1) % 3) - 1, RcsOn = i % 2 == 0, ThrottleSmooth = (i % 4 == 1) ? 1 : 0,
        };

        private static bool Same(in FlightState a, in FlightState b) =>
            a.Position.X == b.Position.X && a.Position.Y == b.Position.Y && a.Position.Z == b.Position.Z &&
            a.Velocity.X == b.Velocity.X && a.Velocity.Y == b.Velocity.Y && a.Velocity.Z == b.Velocity.Z &&
            a.Orientation.W == b.Orientation.W && a.Orientation.X == b.Orientation.X && a.Orientation.Y == b.Orientation.Y && a.Orientation.Z == b.Orientation.Z &&
            a.Omega.X == b.Omega.X && a.Omega.Y == b.Omega.Y && a.Omega.Z == b.Omega.Z &&
            a.GyroH.X == b.GyroH.X && a.GyroH.Y == b.GyroH.Y && a.GyroH.Z == b.GyroH.Z &&
            a.MainFuel == b.MainFuel && a.RcsFuel == b.RcsFuel && a.Throttle == b.Throttle && a.Mode == b.Mode && a.Dumping == b.Dumping &&
            a.Latitude == b.Latitude && a.Longitude == b.Longitude;

        [Test]
        public void Batch_EqualsOneByOne_Bitwise_AfterManySteps()
        {
            FlightParams p = FlightFixture.Params();
            const int N = 64;
            FlightState[] single = new FlightState[N];
            ControlInput[] inputs = new ControlInput[N];
            FlightSoA soa = FlightSoA.Allocate(N);
            for (int i = 0; i < N; i++)
            {
                single[i] = MakeCraft(i, p);
                inputs[i] = MakeInput(i);
                soa.Store(i, single[i]);
            }
            StepReport[] reports = new StepReport[N];
            for (int step = 0; step < 100; step++)
            {
                FlightBatch.StepAll(soa, inputs, p, step * 0.05, 0.05, reports);
                for (int i = 0; i < N; i++) FlightKernel.Step(ref single[i], inputs[i], p, step * 0.05, 0.05, out _);
            }
            for (int i = 0; i < N; i++) Assert.IsTrue(Same(soa.Load(i), single[i]), "craft " + i);
        }

        [Test]
        public void Batch_DoesNotDependOnOrderOrOnHowTheRangeIsSplit()
        {
            FlightParams p = FlightFixture.Params();
            const int N = 48;
            ControlInput[] inputs = new ControlInput[N];
            FlightSoA a = FlightSoA.Allocate(N), b = FlightSoA.Allocate(N), c = FlightSoA.Allocate(N);
            for (int i = 0; i < N; i++)
            {
                inputs[i] = MakeInput(i);
                FlightState s = MakeCraft(i, p);
                a.Store(i, s);
                b.Store(i, s);
                c.Store(i, s);
            }
            for (int step = 0; step < 60; step++)
            {
                double t = step * 0.05;
                FlightBatch.StepRange(a, inputs, p, t, 0.05, 0, N, default);              // one range
                FlightBatch.StepRange(b, inputs, p, t, 0.05, 0, 17, default);             // three chunks, last first
                FlightBatch.StepRange(b, inputs, p, t, 0.05, 30, N, default);
                FlightBatch.StepRange(b, inputs, p, t, 0.05, 17, 30, default);
                for (int i = N - 1; i >= 0; i--) FlightBatch.StepRange(c, inputs, p, t, 0.05, i, i + 1, default); // reverse, one by one
            }
            for (int i = 0; i < N; i++)
            {
                Assert.IsTrue(Same(a.Load(i), b.Load(i)), "chunked, craft " + i);
                Assert.IsTrue(Same(a.Load(i), c.Load(i)), "reversed, craft " + i);
            }
        }

        [Test]
        public void Batch_CraftsDoNotInfluenceEachOther()
        {
            FlightParams p = FlightFixture.Params();
            FlightSoA crowd = FlightSoA.Allocate(10), alone = FlightSoA.Allocate(1);
            ControlInput[] inputs = new ControlInput[10];
            for (int i = 0; i < 10; i++) { crowd.Store(i, MakeCraft(i, p)); inputs[i] = MakeInput(i); }
            alone.Store(0, MakeCraft(3, p));
            for (int step = 0; step < 50; step++)
            {
                FlightBatch.StepAll(crowd, inputs, p, step * 0.05, 0.05, default);
                FlightBatch.StepAll(alone, new[] { inputs[3] }, p, step * 0.05, 0.05, default);
            }
            Assert.IsTrue(Same(crowd.Load(3), alone.Load(0)));
        }
    }
    public class InertiaTests
    {
        [Test]
        public void EffectiveInertia_FullTanks_IsTheConfiguredValue_EmptyTanks_SubtractTheFuel()
        {
            FlightConfig c = FlightConfig.Default();
            Vec3 full = FlightKernel.EffectiveInertia(c, c.MainFuelCapacity, c.RcsPropellantCapacity);
            Assert.AreEqual(c.Inertia.X, full.X, 1e-9);
            Assert.AreEqual(c.Inertia.Y, full.Y, 1e-9);
            Assert.AreEqual(c.Inertia.Z, full.Z, 1e-9);
            Vec3 dry = FlightKernel.EffectiveInertia(c, 0, 0);
            double fuel = c.MainFuelCapacity + c.RcsPropellantCapacity;
            Assert.AreEqual(c.Inertia.X - fuel * c.FuelInertiaPerKg.X, dry.X, 1e-6);
            Assert.AreEqual(c.Inertia.Y - fuel * c.FuelInertiaPerKg.Y, dry.Y, 1e-6);
            Assert.Greater(dry.X, 0);
            Assert.Greater(dry.Y, 0);
        }

        [Test]
        public void EffectiveInertia_IsLinearInTheFuelMass_AndMonotonic()
        {
            FlightConfig c = FlightConfig.Default();
            double prev = 0;
            for (double f = 0; f <= c.MainFuelCapacity; f += 4000)
            {
                double x = FlightKernel.EffectiveInertia(c, f, c.RcsPropellantCapacity).X;
                Assert.Greater(x, prev);
                Assert.AreEqual(c.Inertia.X - (c.MainFuelCapacity - f) * c.FuelInertiaPerKg.X, x, 1e-6);
                prev = x;
            }
        }

        [Test]
        public void EffectiveInertia_WithoutFuelCoefficient_DoesNotDependOnFuel_AndIsClamped()
        {
            FlightConfig c = FlightConfig.Default();
            c.FuelInertiaPerKg = Vec3.Zero; // the field of an older config file
            Assert.AreEqual(c.Inertia.X, FlightKernel.EffectiveInertia(c, 0, 0).X, 1e-12);
            c.FuelInertiaPerKg = new Vec3(1e6, 1e6, 1e6); // nonsense: would go negative
            Vec3 v = FlightKernel.EffectiveInertia(c, 0, 0);
            Assert.AreEqual(0.01 * c.Inertia.X, v.X, 1e-9);
            Assert.AreEqual(0.01 * c.Inertia.Y, v.Y, 1e-9);
        }

        [Test]
        public void Kernel_LighterCraft_TurnsFasterUnderTheSameTorque()
        {
            FlightParams p = FlightFixture.DeepSpace();
            ControlInput input = new ControlInput { Pitch = 1.0, RcsOn = true };
            FlightState full = FlightKernel.Fresh(p.Craft);
            FlightState empty = FlightKernel.Fresh(p.Craft);
            empty.MainFuel = 0;
            FlightKernel.Step(ref full, input, p, 0, 0.01, out _);
            FlightKernel.Step(ref empty, input, p, 0, 0.01, out _);
            Vec3 iFull = FlightKernel.EffectiveInertia(p.Craft, p.Craft.MainFuelCapacity, p.Craft.RcsPropellantCapacity);
            Vec3 iEmpty = FlightKernel.EffectiveInertia(p.Craft, 0, p.Craft.RcsPropellantCapacity);
            Assert.Greater(empty.Omega.X, full.Omega.X);
            Assert.AreEqual(iFull.X / iEmpty.X, empty.Omega.X / full.Omega.X, 1e-6, "angular rate is inversely proportional to the inertia");
        }

        [Test]
        public void Kernel_BurningFuel_KeepsTheAngularRate_WhileTheMomentumFallsWithTheInertia()
        {
            // The burned propellant carries its share of the rotation away (jet damping): with an aligned engine a spinning craft keeps
            // its rate while the inertia, and with it I * w, falls in proportion.
            FlightParams p = FlightFixture.DeepSpace();
            FlightState s = FlightKernel.Fresh(p.Craft);
            s.Omega = new Vec3(0.02, 0, 0);
            s.Throttle = 1.0;
            ControlInput input = new ControlInput { ThrottleFull = true };
            double i0 = FlightKernel.EffectiveInertia(p.Craft, s.MainFuel, s.RcsFuel).X;
            for (int i = 0; i < 400; i++) FlightKernel.Step(ref s, input, p, i * 0.1, 0.1, out _);
            double i1 = FlightKernel.EffectiveInertia(p.Craft, s.MainFuel, s.RcsFuel).X;
            Assert.Less(s.MainFuel, p.Craft.MainFuelCapacity - 1000);
            Assert.Less(i1, i0);
            Assert.AreEqual(0.02, s.Omega.X, 1e-9);
        }

        [Test]
        public void EmptyTank_LocksThrottleAtZero_AndIgnoresTheLever()
        {
            FlightParams p = FlightFixture.DeepSpace();
            FlightState s = FlightKernel.Fresh(p.Craft);
            s.MainFuel = 50;
            ControlInput full = new ControlInput { ThrottleFull = true };
            for (int i = 0; i < 400 && s.MainFuel > 0; i++) FlightKernel.Step(ref s, full, p, i * 0.1, 0.1, out _);
            Assert.AreEqual(0.0, s.MainFuel);
            Assert.AreEqual(0.0, s.Throttle, "throttle drops to zero in the step that empties the tank");
            for (int i = 0; i < 20; i++) FlightKernel.Step(ref s, full, p, 100 + i * 0.1, 0.1, out StepReport r);
            Assert.AreEqual(0.0, s.Throttle, "the lever stays dead with no fuel");
            ControlInput smooth = new ControlInput { ThrottleSmooth = 1.0 };
            for (int i = 0; i < 100; i++) FlightKernel.Step(ref s, smooth, p, 110 + i * 0.1, 0.1, out _);
            Assert.AreEqual(0.0, s.Throttle);
        }
    }
}
