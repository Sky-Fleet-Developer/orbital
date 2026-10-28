using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Orbital.Core.Config;
using Orbital.Core.Flight;

namespace Orbital.Tests
{
    /// <summary>
    /// Stage 2b: the engine-free parts of the client flight test: key mapping, the shared JSON configs read as the client reads them,
    /// the ground of a body from its rotation data, the spawn states, the telemetry, the ground support and a deterministic run through the
    /// same sequence the ecs systems use (keys -> control -> kernel step -> ground support).
    /// </summary>
    public class ControlKeysTests
    {
        [Test]
        public void EveryKeyHasItsOwnFieldAndPath()
        {
            Assert.AreEqual(Enum.GetValues(typeof(ControlKey)).Length, ControlKeys.Count);
            var paths = Enumerable.Range(0, ControlKeys.Count).Select(i => ControlKeys.DefaultPath((ControlKey) i)).ToList();
            Assert.AreEqual(ControlKeys.Count, paths.Distinct().Count(), "paths are unique");
            Assert.IsTrue(paths.All(p => p.StartsWith("<Keyboard>/")));
            for (int i = 0; i < ControlKeys.Count; i++)
            {
                KeyState state = default;
                ControlKeys.Set(ref state, (ControlKey) i, true);
                for (int j = 0; j < ControlKeys.Count; j++)
                {
                    Assert.AreEqual(i == j, ControlKeys.Get(state, (ControlKey) j), $"key {(ControlKey) i} sets only itself ({(ControlKey) j})");
                }
                ControlKeys.Set(ref state, (ControlKey) i, false);
                Assert.IsFalse(ControlKeys.Get(state, (ControlKey) i));
            }
        }

        [Test]
        public void KeysMapToTheControlsOfTheDesign()
        {
            InputLatch latch = default;
            ControlInput Map(params ControlKey[] keys)
            {
                KeyState s = default;
                foreach (ControlKey k in keys) ControlKeys.Set(ref s, k, true);
                return InputMapper.Map(s, ref latch);
            }
            Assert.AreEqual(1.0, Map(ControlKey.W).Pitch);
            Assert.AreEqual(-1.0, Map(ControlKey.S).Pitch);
            Assert.AreEqual(1.0, Map(ControlKey.D).Roll);
            Assert.AreEqual(-1.0, Map(ControlKey.A).Roll);
            Assert.AreEqual(1.0, Map(ControlKey.E).Yaw);
            Assert.AreEqual(-1.0, Map(ControlKey.Q).Yaw);
            Assert.AreEqual(1.0, Map(ControlKey.Shift).ThrottleSmooth);
            Assert.AreEqual(-1.0, Map(ControlKey.Ctrl).ThrottleSmooth);
            Assert.IsTrue(Map(ControlKey.X).ThrottleFull);
            Assert.IsTrue(Map(ControlKey.Z).ThrottleZero);
            Assert.AreEqual(0.0, Map(ControlKey.W, ControlKey.S).Pitch, "opposite keys cancel");
        }

        [Test]
        public void ATapBetweenTwoStepsIsNotLostAndAHeldKeyTogglesOnce()
        {
            // frame polls and 50 Hz steps do not line up: the accumulated state keeps the tap
            KeyState accumulated = default;
            KeyState tap = default;
            ControlKeys.Set(ref tap, ControlKey.R, true);
            accumulated = ControlKeys.Or(accumulated, tap);         // frame with R down
            accumulated = ControlKeys.Or(accumulated, default);     // frame with R already up
            InputLatch latch = default;
            Assert.IsTrue(InputMapper.Map(accumulated, ref latch).RcsOn, "the R press reaches the step");
            Assert.IsTrue(InputMapper.Map(default, ref latch).RcsOn, "release does not switch it off");
            // held over three steps: one toggle only
            latch = default;
            Assert.IsTrue(InputMapper.Map(tap, ref latch).RcsOn);
            Assert.IsTrue(InputMapper.Map(tap, ref latch).RcsOn);
            Assert.IsTrue(InputMapper.Map(tap, ref latch).RcsOn);
            Assert.IsTrue(InputMapper.Map(default, ref latch).RcsOn);
            Assert.IsFalse(InputMapper.Map(tap, ref latch).RcsOn, "the next press switches it off");
        }

        [Test]
        public void OrKeepsEveryKeyOfBothStates()
        {
            KeyState a = default, b = default;
            for (int i = 0; i < ControlKeys.Count; i++)
            {
                ControlKeys.Set(ref (i % 2 == 0 ? ref a : ref b), (ControlKey) i, true);
            }
            KeyState all = ControlKeys.Or(a, b);
            for (int i = 0; i < ControlKeys.Count; i++) Assert.IsTrue(ControlKeys.Get(all, (ControlKey) i));
        }
    }

    public class SharedConfigLoadTests
    {
        private static string ConfigDir()
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null)
            {
                string candidate = Path.Combine(dir, "Assets", "_game", "Config", GameConfigLoader.FlightFile);
                if (File.Exists(candidate)) return Path.GetDirectoryName(candidate);
                dir = Path.GetDirectoryName(dir);
            }
            Assert.Fail("Assets/_game/Config not found above " + AppContext.BaseDirectory);
            return null;
        }

        [Test]
        public void TheShippedFlightConfigEqualsTheBuiltInDefault()
        {
            GameConfig config = GameConfigLoader.FromDirectory(ConfigDir());
            // compare every number through the same serializer, so a field added to FlightConfig is covered automatically
            string expected = JObject.FromObject(FlightConfig.Default()).ToString();
            string actual = JObject.FromObject(config.Flight).ToString();
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void CatalogsOfTheShippedFiles()
        {
            GameConfig config = GameConfigLoader.FromDirectory(ConfigDir());
            Assert.AreEqual("standard", config.FindFuel(null).Id);
            Assert.AreEqual(450.0, config.FindFuel("standard").IspSeconds);
            CraftDef craft = config.FindCraft(null);
            Assert.AreEqual("standard", craft.Id);
            Assert.AreEqual(5000.0, craft.DryMass);
            Assert.AreEqual(40000.0, craft.MaxFuel);
            Assert.AreEqual(600000.0, craft.ThrustN);
            StartPointDef point = config.FindStartPoint(null);
            Assert.AreEqual("earth-equator", point.Id);
            Assert.IsTrue(point.IsSurface);
            Assert.AreEqual("earth", point.BodyId);
            Assert.AreEqual(0.0, point.Latitude);
            Assert.AreEqual(0.0, point.Longitude);
            Assert.IsNull(config.FindStartPoint("nowhere"), "an unknown id is not replaced by the default");
            Assert.IsNull(config.FindCraft("nothing"));
        }

        [Test]
        public void ResolveFlightTakesMassThrustAndIspFromTheCraftAndItsFuel()
        {
            GameConfig config = GameConfigLoader.FromJson(null, "{\"fuels\":[{\"id\":\"hot\",\"ispSeconds\":520}],\"defaultFuel\":\"hot\"}",
                "{\"crafts\":[{\"id\":\"big\",\"dryMass\":9000,\"maxFuel\":70000,\"thrustN\":900000,\"fuel\":\"hot\"}],\"defaultCraft\":\"big\"}", null);
            FlightConfig f = config.ResolveFlight();
            Assert.AreEqual(9000.0, f.DryMass);
            Assert.AreEqual(70000.0, f.MainFuelCapacity);
            Assert.AreEqual(900000.0, f.Engine.MaxThrust);
            Assert.AreEqual(520.0, f.Engine.Isp);
            Assert.AreEqual(FlightConfig.Default().Inertia.X, f.Inertia.X, "the rest stays as in the flight config");
            Assert.AreEqual(FlightConfig.Default().Engine.ThrottleRate, f.Engine.ThrottleRate);
        }

        [Test]
        public void ResolveFlightWithoutCatalogsGivesTheFlightConfigAsItIs()
        {
            GameConfig config = GameConfigLoader.FromJson("{\"dryMass\": 123}", null, null, null);
            Assert.AreEqual(123.0, config.ResolveFlight().DryMass);
        }

        [Test]
        public void ThePartialFlightFileKeepsDefaultsIgnoresCaseAndUnknownFields()
        {
            FlightConfig f = GameConfigLoader.ParseFlight("{\"DRYMASS\": 1234, \"engine\": {\"Isp\": 300}, \"cgHeight\": 9.5, \"inertia\": {\"y\": 7}, \"somethingNew\": 1}");
            FlightConfig d = FlightConfig.Default();
            Assert.AreEqual(1234.0, f.DryMass);
            Assert.AreEqual(300.0, f.Engine.Isp);
            Assert.AreEqual(d.Engine.MaxThrust, f.Engine.MaxThrust, "a nested object is merged, not replaced");
            Assert.AreEqual(9.5, f.CgHeight);
            Assert.AreEqual(7.0, f.Inertia.Y);
            Assert.AreEqual(d.Inertia.X, f.Inertia.X);
            Assert.AreEqual(d.Gyro.MaxTorque, f.Gyro.MaxTorque);
        }

        [Test]
        public void MissingFilesLeaveTheBuiltInValues()
        {
            GameConfig config = GameConfigLoader.FromJson(null, "", "   ", null);
            Assert.AreEqual(FlightConfig.Default().DryMass, config.Flight.DryMass);
            Assert.IsNull(config.FindCraft(null));
            Assert.IsNull(config.FindStartPoint(null));
        }

        [Test]
        public void TheClientCraftOfTheShippedFilesMatchesTheServerDefaultCraft()
        {
            GameConfig config = GameConfigLoader.FromDirectory(ConfigDir());
            FlightConfig f = config.ResolveFlight();
            FlightConfig d = FlightConfig.Default();
            Assert.AreEqual(d.DryMass, f.DryMass);
            Assert.AreEqual(d.MainFuelCapacity, f.MainFuelCapacity);
            Assert.AreEqual(d.Engine.MaxThrust, f.Engine.MaxThrust);
            Assert.AreEqual(d.Engine.Isp, f.Engine.Isp);
        }
    }

    public class SurfaceBuilderTests
    {
        private const double Deg = Math.PI / 180.0;

        private static OrbitFrame Frame(Vec3 forward, Vec3 normal, Vec3 right, double meanMotion = 0, Vec3 pos0 = default) => new OrbitFrame
        {
            HasParent = true, Forward = forward, Normal = normal, Right = right, MeanMotion = meanMotion, PositionAtTimeZero = pos0,
        };

        [Test]
        public void ABodyWithoutSpinGetsTheNonRotatingDefault()
        {
            SurfaceData s = SurfaceBuilder.Build(1000, default, default);
            Assert.AreEqual(1000.0, s.Radius);
            Assert.AreEqual(0.0, s.Omega);
            FlightFixture.AssertNear(Vec3.UnitY, s.Axis, 1e-15);
            FlightFixture.AssertNear(Vec3.UnitX, s.EquatorX, 1e-15);
            FlightFixture.AssertNear(Vec3.Cross(s.Axis, s.EquatorX), s.EquatorY, 1e-15);
        }

        [Test]
        public void TheRootBodyIsTiltedFromTheWorldYByTheObliquity()
        {
            RotationParams rot = new RotationParams {PeriodSeconds = 86164.0905, ObliquityDegrees = 23.44};
            SurfaceData s = SurfaceBuilder.Build(6.371e6, rot, default);
            Assert.AreEqual(23.44, Vec3.Angle(Vec3.UnitY, s.Axis) / Deg, 1e-9);
            Assert.AreEqual(2.0 * Math.PI / 86164.0905, s.Omega, 1e-18);
            Assert.AreEqual(1.0, Vec3.Length(s.Axis), 1e-14);
            Assert.AreEqual(0.0, Vec3.Dot(s.Axis, s.EquatorX), 1e-14);
            Assert.AreEqual(1.0, Vec3.Length(s.EquatorX), 1e-14);
            Assert.AreEqual(1.0, Vec3.Length(s.EquatorY), 1e-14);
            Assert.AreEqual(0.0, Vec3.Dot(s.EquatorX, s.EquatorY), 1e-14);
        }

        [Test]
        public void TheTiltIsMeasuredFromTheOrbitNormalAndAzimuthTurnsItAroundTheNormal()
        {
            OrbitFrame frame = Frame(Vec3.UnitX, Vec3.UnitY, Vec3.UnitZ);
            RotationParams rot = new RotationParams {PeriodSeconds = 1000, ObliquityDegrees = 30, AxisAzimuthDegrees = 90};
            SurfaceData s = SurfaceBuilder.Build(1000, rot, frame);
            FlightFixture.AssertNear(Vec3.UnitY * Math.Cos(30 * Deg) + Vec3.UnitZ * Math.Sin(30 * Deg), s.Axis, 1e-14, "azimuth 90 degrees tilts toward Right");
            rot.ObliquityDegrees = 177.36; // Venus-like: retrograde
            s = SurfaceBuilder.Build(1000, rot, frame);
            Assert.Less(Vec3.Dot(s.Axis, Vec3.UnitY), 0.0, "retrograde spin points below the orbit plane");
            Assert.Greater(s.Omega, 0.0, "the period stays positive, the tilt makes it retrograde");
        }

        [Test]
        public void ATidallyLockedBodyTurnsWithTheOrbitAndFacesItsParent()
        {
            // motion goes Forward -> Right (Right = Normal x Forward); at time 0 the parent is in the direction -Forward from the body
            Vec3 forward = Vec3.UnitX, normal = Vec3.UnitY, right = Vec3.Cross(normal, forward);
            double n = 2.66e-6;
            OrbitFrame frame = Frame(forward, normal, right, n, forward * 4e8);
            RotationParams rot = new RotationParams {TidallyLocked = true, ObliquityDegrees = 6.68, PeriodSeconds = 1};
            SurfaceData s = SurfaceBuilder.Build(1.7374e6, rot, frame);
            Assert.AreEqual(n, s.Omega, 1e-20, "rate equals the mean motion");
            double period = 2.0 * Math.PI / n;
            // the tilt (6.68 degrees) keeps the meridian within that angle of the parent direction (at time 0 it is the parent direction projected on the equator)
            foreach (double t in new[] {0.0, period / 8, period / 4, period / 2, 0.77 * period})
            {
                Vec3 meridian = Vec3.Normalize(Surface.Point(s, 0.0, 0.0, t));
                Vec3 toParent = (forward * Math.Cos(n * t) + right * Math.Sin(n * t)) * -1.0;
                Assert.GreaterOrEqual(Vec3.Dot(meridian, toParent), Math.Cos(6.68 * Deg) - 1e-9, $"t = {t}");
            }
        }

        [Test]
        public void AFastSpinnerHasAConsistentGround()
        {
            RotationParams rot = new RotationParams {PeriodSeconds = 10000, ObliquityDegrees = 25, InitialAngleDegrees = 40};
            SurfaceData s = SurfaceBuilder.Build(2e6, rot, default);
            Assert.AreEqual(40 * Deg, s.Phase0, 1e-15);
            // the round trip between ground coordinates and inertial position holds at any time
            foreach (double t in new[] {0.0, 1234.5, 9.9e6})
            {
                Vec3 p = Surface.Point(s, 0.3, -1.1, t);
                Assert.AreEqual(s.Radius, Vec3.Length(p), 1e-6);
                Surface.ToBodyCoordinates(s, p, t, out double lat, out double lon);
                Assert.AreEqual(0.3, lat, 1e-12);
                Assert.AreEqual(-1.1, lon, 1e-12);
            }
        }
    }

    public class SpawnAndSupportTests
    {
        private static FlightParams P() => FlightFixture.Params();

        [Test]
        public void StandingOnTheGroundIsLandedWithTheGroundVelocityAndTheNoseUp()
        {
            FlightParams p = P();
            FlightState s = SpawnBuilder.OnSurface(p.Craft, p.Surface, 0.4, 1.0, 0.0, 5000.0);
            Assert.AreEqual(FlightMode.Landed, s.Mode);
            Assert.AreEqual(p.Surface.Radius, Vec3.Length(s.Position), 1e-6);
            FlightFixture.AssertNear(Surface.GroundVelocity(p.Surface, s.Position), s.Velocity, 1e-9);
            FlightFixture.AssertNear(Vec3.Normalize(s.Position), DQuat.Rotate(s.Orientation, Vec3.UnitY), 1e-12);
            Assert.AreEqual(p.Craft.MainFuelCapacity, s.MainFuel);
            Assert.AreEqual(0.4, s.Latitude);
            Assert.AreEqual(1.0, s.Longitude);
            // the state is what the kernel keeps: one step does not move a craft that stands
            Vec3 before = s.Position;
            FlightKernel.Step(ref s, default, p, 5000.0, 0.02, out StepReport r);
            Assert.AreEqual(FlightMode.Landed, s.Mode);
            FlightFixture.AssertNear(Surface.Point(p.Surface, 0.4, 1.0, 5000.02), s.Position, 1e-6);
        }

        [Test]
        public void HoveringStartsFlyingAtTheAltitudeWithTheGroundSpeed()
        {
            FlightParams p = P();
            FlightState s = SpawnBuilder.OnSurface(p.Craft, p.Surface, 0.0, 0.0, 250.0, 0.0);
            Assert.AreEqual(FlightMode.Flying, s.Mode);
            Assert.AreEqual(250.0, Surface.Altitude(p.Surface, s.Position), 1e-6);
            FlightFixture.AssertNear(Surface.GroundVelocity(p.Surface, s.Position), s.Velocity, 1e-9);
        }

        [Test]
        public void ACraftDroppedFromHalfAMeterLandsAndOneFromHundredCrashes()
        {
            FlightParams p = P();
            FlightState soft = SpawnBuilder.OnSurface(p.Craft, p.Surface, 0.0, 0.0, 0.5, 0.0);
            FlightState hard = SpawnBuilder.OnSurface(p.Craft, p.Surface, 0.0, 0.0, 100.0, 0.0);
            double t = 0;
            for (int i = 0; i < 1500; i++)
            {
                FlightKernel.Step(ref soft, default, p, t, 0.02, out StepReport _);
                FlightKernel.Step(ref hard, default, p, t, 0.02, out StepReport _);
                t += 0.02;
            }
            Assert.AreEqual(FlightMode.Landed, soft.Mode, "about 3 m/s at touchdown, below the 5 m/s limit");
            Assert.AreEqual(FlightMode.Crashed, hard.Mode, "about 44 m/s");
        }

        [Test]
        public void ACircularOrbitIsCircularWithTheRightPlaneAndStaysSoUnderTheKernel()
        {
            FlightParams p = P();
            double inclination = 51.6 * Math.PI / 180.0;
            FlightState s = SpawnBuilder.CircularOrbit(p.Craft, p.Surface, p.Nu, 400000.0, inclination, 0.7);
            double r = p.Surface.Radius + 400000.0;
            Assert.AreEqual(r, Vec3.Length(s.Position), 1e-6);
            Assert.AreEqual(Math.Sqrt(p.Nu / r), Vec3.Length(s.Velocity), 1e-9);
            Assert.AreEqual(0.0, Vec3.Dot(s.Position, s.Velocity), 1e-3, "velocity is horizontal");
            Vec3 normal = Vec3.Normalize(Vec3.Cross(s.Position, s.Velocity));
            Assert.AreEqual(inclination, Vec3.Angle(normal, p.Surface.Axis), 1e-12, "tilt of the plane from the equator");
            FlightFixture.AssertNear(Vec3.Normalize(s.Velocity), DQuat.Rotate(s.Orientation, Vec3.UnitY), 1e-12);
            Assert.AreEqual(FlightMode.Flying, s.Mode);

            double energy = Vec3.LengthSquared(s.Velocity) / 2 - p.Nu / r;
            double angularMomentum = Vec3.Length(Vec3.Cross(s.Position, s.Velocity));
            double t = 0;
            for (int i = 0; i < 6000; i++) // 50 minutes at 0.5 s
            {
                FlightKernel.Step(ref s, default, p, t, 0.5, out StepReport _);
                t += 0.5;
            }
            Assert.AreEqual(FlightMode.Flying, s.Mode);
            double energyAfter = Vec3.LengthSquared(s.Velocity) / 2 - p.Nu / Vec3.Length(s.Position);
            Assert.AreEqual(energy, energyAfter, Math.Abs(energy) * 1e-8, "energy");
            Assert.AreEqual(angularMomentum, Vec3.Length(Vec3.Cross(s.Position, s.Velocity)), angularMomentum * 1e-8, "angular momentum");
            Assert.AreEqual(r, Vec3.Length(s.Position), r * 1e-4, "stays circular");
        }

        [Test]
        public void HoldUpright_TurnsTheNoseOntoTheVerticalAndKeepsOnlyRoll()
        {
            FlightParams p = P();
            FlightState s = SpawnBuilder.OnSurface(p.Craft, p.Surface, 0.2, 0.3, 0.0, 0.0);
            Vec3 normal = Vec3.Normalize(s.Position);
            s.Orientation = DQuat.Normalize(DQuat.Multiply(DQuat.FromAxisAngle(new Vec3(1, 0, 0), 0.5), s.Orientation));
            s.Omega = new Vec3(0.1, 0.2, 0.3);
            Assert.Greater(Vec3.Angle(DQuat.Rotate(s.Orientation, Vec3.UnitY), normal), 0.1);
            LandedSupport.HoldUpright(ref s, p.Surface);
            Assert.AreEqual(0.0, Vec3.Angle(DQuat.Rotate(s.Orientation, Vec3.UnitY), normal), 1e-9);
            Assert.AreEqual(0.0, s.Omega.X);
            Assert.AreEqual(0.2, s.Omega.Y);
            Assert.AreEqual(0.0, s.Omega.Z);
            Assert.AreEqual(1.0, s.Orientation.W * s.Orientation.W + s.Orientation.X * s.Orientation.X + s.Orientation.Y * s.Orientation.Y + s.Orientation.Z * s.Orientation.Z, 1e-12);
        }

        [Test]
        public void HoldUpright_DoesNothingInTheAirOrAfterACrash()
        {
            FlightParams p = P();
            FlightState s = SpawnBuilder.OnSurface(p.Craft, p.Surface, 0.0, 0.0, 100.0, 0.0);
            s.Orientation = DQuat.FromAxisAngle(Vec3.UnitX, 0.5);
            s.Omega = new Vec3(0.1, 0.1, 0.1);
            FlightState before = s;
            LandedSupport.HoldUpright(ref s, p.Surface);
            Assert.AreEqual(before.Orientation.X, s.Orientation.X);
            Assert.AreEqual(before.Omega.Z, s.Omega.Z);
            s.Mode = FlightMode.Crashed;
            LandedSupport.HoldUpright(ref s, p.Surface);
            Assert.AreEqual(before.Orientation.X, s.Orientation.X);
        }

        [Test]
        public void WithoutSupportAStandingCraftFallsOverAndWithSupportItStandsForHalfAnHour()
        {
            FlightParams p = P();
            FlightState free = SpawnBuilder.OnSurface(p.Craft, p.Surface, 0.0, 0.0, 0.0, 0.0);
            FlightState held = free;
            double t = 0;
            double maxTilt = 0;
            double maxTiltHeld = 0;
            for (int i = 0; i < 90000; i++) // half an hour at 0.02 s
            {
                FlightKernel.Step(ref free, default, p, t, 0.02, out StepReport _);
                FlightKernel.Step(ref held, default, p, t, 0.02, out StepReport _);
                LandedSupport.HoldUpright(ref held, p.Surface);
                t += 0.02;
                if (i < 3000) maxTilt = Math.Max(maxTilt, FlightTelemetry.Compute(free, p).TiltDegrees);
                maxTiltHeld = Math.Max(maxTiltHeld, FlightTelemetry.Compute(held, p).TiltDegrees);
            }
            TestContext.WriteLine($"unsupported: largest tilt in the first minute {maxTilt:F1} deg; supported: largest tilt in half an hour {maxTiltHeld:E2} deg");
            Assert.Greater(maxTilt, 45.0, "the inverted pendulum of the kernel falls over within a minute");
            Assert.AreEqual(FlightMode.Landed, held.Mode);
            Assert.Less(maxTiltHeld, 1e-6);
            Assert.AreEqual(p.Craft.MainFuelCapacity, held.MainFuel, 1e-9, "standing burns nothing");
        }

        [Test]
        public void ARunThroughTheSystemSequenceIsDeterministic()
        {
            FlightState a = Run();
            FlightState b = Run();
            Assert.AreEqual(a.Position.X, b.Position.X);
            Assert.AreEqual(a.Position.Y, b.Position.Y);
            Assert.AreEqual(a.Position.Z, b.Position.Z);
            Assert.AreEqual(a.Velocity.X, b.Velocity.X);
            Assert.AreEqual(a.Orientation.W, b.Orientation.W);
            Assert.AreEqual(a.Orientation.Z, b.Orientation.Z);
            Assert.AreEqual(a.MainFuel, b.MainFuel);
            Assert.AreEqual(a.RcsFuel, b.RcsFuel);
            Assert.AreEqual(a.Throttle, b.Throttle);
            Assert.Greater(a.MainFuel, 0.0);
            Assert.Less(a.MainFuel, FlightConfig.Default().MainFuelCapacity, "the script burned fuel");
            Assert.Less(a.RcsFuel, FlightConfig.Default().RcsPropellantCapacity, "and used the RCS");
        }

        // The sequence of the ecs systems (FlightInputSystem, FlightStepSystem, LandedSupportSystem, ThrustRecorderSystem) on plain data,
        // driven by a key script: lift off from the pad, pitch over, burn, coast.
        private static FlightState Run()
        {
            FlightParams p = P();
            FlightState s = SpawnBuilder.OnSurface(p.Craft, p.Surface, 0.0, 0.0, 0.0, 0.0);
            InputLatch latch = default;
            RecorderState recorder = default;
            RecorderConfig rc = RecorderConfig.Default();
            double t = 0;
            for (int i = 0; i < 6000; i++)
            {
                KeyState keys = default;
                double sec = i * 0.02;
                if (sec > 1 && sec < 30) ControlKeys.Set(ref keys, ControlKey.X, true);
                if (sec > 30) ControlKeys.Set(ref keys, ControlKey.Z, true);
                if (i == 100) ControlKeys.Set(ref keys, ControlKey.R, true);
                if (sec > 5 && sec < 6) ControlKeys.Set(ref keys, ControlKey.W, true);
                ControlInput control = InputMapper.Map(keys, ref latch);
                FlightKernel.Step(ref s, control, p, t, 0.02, out StepReport _);
                LandedSupport.HoldUpright(ref s, p.Surface);
                RecorderSample sample;
                sample.Time = t + 0.02;
                sample.Throttle = s.Throttle;
                sample.ThrustDirection = DQuat.Rotate(s.Orientation, Vec3.UnitY);
                sample.Sas = s.SasOn;
                sample.Resting = s.Mode != FlightMode.Flying;
                ThrustSegmentRecorder.Update(ref recorder, sample, rc);
                t += 0.02;
            }
            return s;
        }
    }

    public class TelemetryTests
    {
        [Test]
        public void AStandingCraftReadsZeroSpeedAndFullTanks()
        {
            FlightParams p = FlightFixture.Params();
            FlightState s = SpawnBuilder.OnSurface(p.Craft, p.Surface, 0.5, 0.5, 0.0, 100.0);
            FlightTelemetry t = FlightTelemetry.Compute(s, p);
            Assert.AreEqual(0.0, t.Altitude, 1e-6);
            Assert.AreEqual(0.0, t.GroundSpeed, 1e-9);
            Assert.Greater(t.InertialSpeed, 100.0, "the ground moves in the inertial frame");
            Assert.AreEqual(0.0, t.TiltDegrees, 1e-6);
            Assert.AreEqual(1.0, t.MainFuelFraction);
            Assert.AreEqual(1.0, t.RcsFuelFraction);
            Assert.AreEqual(0.0, t.GyroFraction);
            Assert.AreEqual(0.0, t.ThrustToWeight);
            Assert.AreEqual(FlightKernel.TotalMass(p.Craft, s), t.Mass);
        }

        [Test]
        public void VerticalAndHorizontalSpeedAreSplitAlongTheLocalVertical()
        {
            FlightParams p = FlightFixture.Params(0.0);
            FlightState s = SpawnBuilder.OnSurface(p.Craft, p.Surface, 0.0, 0.0, 1000.0, 0.0);
            Vec3 up = Vec3.Normalize(s.Position);
            Vec3 east = Vec3.Normalize(Vec3.Cross(p.Surface.Axis, up));
            s.Velocity = up * 30.0 + east * 40.0;
            FlightTelemetry t = FlightTelemetry.Compute(s, p);
            Assert.AreEqual(1000.0, t.Altitude, 1e-6);
            Assert.AreEqual(30.0, t.VerticalSpeed, 1e-9);
            Assert.AreEqual(40.0, t.HorizontalSpeed, 1e-6);
            Assert.AreEqual(50.0, t.GroundSpeed, 1e-6);
        }

        [Test]
        public void ThrustToWeightAndTiltFollowThrottleAndAttitude()
        {
            FlightParams p = FlightFixture.Params(0.0);
            FlightState s = SpawnBuilder.OnSurface(p.Craft, p.Surface, 0.0, 0.0, 500.0, 0.0);
            s.Throttle = 1.0;
            s.Orientation = DQuat.Normalize(DQuat.Multiply(DQuat.FromAxisAngle(new Vec3(0, 0, 1), 0.5), s.Orientation));
            FlightTelemetry t = FlightTelemetry.Compute(s, p);
            double r = Vec3.Length(s.Position);
            double weight = FlightKernel.TotalMass(p.Craft, s) * p.Nu / (r * r);
            Assert.AreEqual(p.Craft.Engine.MaxThrust / weight, t.ThrustToWeight, 1e-9);
            Assert.AreEqual(0.5 * 180.0 / Math.PI, t.TiltDegrees, 1e-6);
            s.Omega = new Vec3(0.01, 0.02, 0.03);
            t = FlightTelemetry.Compute(s, p);
            Assert.AreEqual(0.01 * 180.0 / Math.PI, t.PitchRateDegPerSec, 1e-12);
            Assert.AreEqual(0.02 * 180.0 / Math.PI, t.RollRateDegPerSec, 1e-12);
            Assert.AreEqual(0.03 * 180.0 / Math.PI, t.YawRateDegPerSec, 1e-12);
        }
    }
}
