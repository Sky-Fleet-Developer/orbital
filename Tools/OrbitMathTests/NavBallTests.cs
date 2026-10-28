using System;
using System.Diagnostics;
using NUnit.Framework;
using Orbital.Core.Flight;

namespace Orbital.Tests
{
    public class NavBallTests
    {
        private const double Deg = Math.PI / 180.0;
        private static readonly SurfaceData Earth = FlightFixture.Earth();

        // Body to inertial from the images of the body axes (columns of the rotation matrix); they must form a proper rotation.
        private static DQuat FromBasis(Vec3 bx, Vec3 by, Vec3 bz)
        {
            double m00 = bx.X, m10 = bx.Y, m20 = bx.Z, m01 = by.X, m11 = by.Y, m21 = by.Z, m02 = bz.X, m12 = bz.Y, m22 = bz.Z;
            double tr = m00 + m11 + m22, w, x, y, z;
            if (tr > 0) { double s = Math.Sqrt(tr + 1.0) * 2; w = 0.25 * s; x = (m21 - m12) / s; y = (m02 - m20) / s; z = (m10 - m01) / s; }
            else if (m00 > m11 && m00 > m22) { double s = Math.Sqrt(1.0 + m00 - m11 - m22) * 2; w = (m21 - m12) / s; x = 0.25 * s; y = (m01 + m10) / s; z = (m02 + m20) / s; }
            else if (m11 > m22) { double s = Math.Sqrt(1.0 + m11 - m00 - m22) * 2; w = (m02 - m20) / s; x = (m01 + m10) / s; y = 0.25 * s; z = (m12 + m21) / s; }
            else { double s = Math.Sqrt(1.0 + m22 - m00 - m11) * 2; w = (m10 - m01) / s; x = (m02 + m20) / s; y = (m12 + m21) / s; z = 0.25 * s; }
            return DQuat.Normalize(new DQuat(w, x, y, z));
        }

        /// <summary>Orientation with the nose along forward and the pilot's top along top (cockpit: right = forward x top, body -X = right, body -Z = top).</summary>
        private static DQuat Pose(Vec3 forward, Vec3 top)
        {
            Vec3 right = Vec3.Cross(forward, top);
            return FromBasis(right * -1.0, forward, top * -1.0);
        }

        // craft over the equator at (R + 1000, 0, 0): up = +X, north = +Y (the axis), east = -Z
        private static readonly Vec3 Up = new Vec3(1, 0, 0), North = new Vec3(0, 1, 0), East = new Vec3(0, 0, -1);

        private static FlightState Craft(Vec3 forward, Vec3 top, Vec3? velocity = null)
        {
            FlightState s = default;
            s.Position = new Vec3(FlightFixture.EarthRadius + 1000.0, 0, 0);
            s.Velocity = velocity ?? Surface.GroundVelocity(Earth, s.Position);
            s.Orientation = Pose(forward, top);
            return s;
        }

        private static void AssertV(Vec3 expected, Vec3 actual, double tol = 1e-9) => FlightFixture.AssertNear(expected, actual, tol);

        [Test]
        public void TheHorizonOfTheTestCraftIsAsExpected()
        {
            NavBall.Horizon(Earth, new Vec3(FlightFixture.EarthRadius, 0, 0), out Vec3 e, out Vec3 n, out Vec3 u);
            AssertV(East, e, 1e-12);
            AssertV(North, n, 1e-12);
            AssertV(Up, u, 1e-12);
            AssertV(u, Vec3.Cross(e, n), 1e-12); // East x North = Up: a right-handed frame
        }

        [Test]
        public void LevelNoseNorth_ReadsZeroAnglesAndTheBallIsInTheCockpitFrame()
        {
            NavBallData d = NavBall.Compute(Craft(North, Up), Earth, false);
            AssertV(new Vec3(0, 1, 0), d.Forward);   // (East, North, Up) components
            AssertV(new Vec3(0, 0, 1), d.Up);
            AssertV(new Vec3(1, 0, 0), d.Right);
            Assert.AreEqual(0.0, d.PitchDegrees, 1e-9);
            Assert.AreEqual(0.0, d.HeadingDegrees, 1e-9);
            Assert.AreEqual(0.0, d.RollDegrees, 1e-9);
            AssertV(new Vec3(0, 0, -1), NavBall.ToView(d, new Vec3(0, 1, 0)), 1e-12);  // north is the center of the picture
            AssertV(new Vec3(1, 0, 0), NavBall.ToView(d, new Vec3(1, 0, 0)), 1e-12);   // east is to the right
            AssertV(new Vec3(0, 1, 0), NavBall.ToView(d, new Vec3(0, 0, 1)), 1e-12);   // up is up
            AssertV(new Vec3(-1, 0, 0), NavBall.ToView(d, new Vec3(-1, 0, 0)), 1e-12); // west is to the left
            Assert.Greater(NavBall.ToView(d, new Vec3(0, -1, 0)).Z, 0.0, "south is behind the ball");
        }

        [Test]
        public void HeadingIncreasesClockwise()
        {
            Assert.AreEqual(90.0, NavBall.Compute(Craft(East, Up), Earth, false).HeadingDegrees, 1e-9);
            Assert.AreEqual(180.0, NavBall.Compute(Craft(North * -1.0, Up), Earth, false).HeadingDegrees, 1e-9);
            Assert.AreEqual(270.0, NavBall.Compute(Craft(East * -1.0, Up), Earth, false).HeadingDegrees, 1e-9);
            Vec3 ne = Vec3.Normalize(North + East);
            Assert.AreEqual(45.0, NavBall.Compute(Craft(ne, Up), Earth, false).HeadingDegrees, 1e-9);
        }

        [Test]
        public void PitchAndRoll_FollowTheKeys()
        {
            FlightState level = Craft(North, Up);
            // W: rotation about body +X: the nose goes down in the cockpit frame
            FlightState w = level;
            w.Orientation = DQuat.ApplyBodyRotation(level.Orientation, Vec3.UnitX * (20 * Deg));
            NavBallData dw = NavBall.Compute(w, Earth, false);
            Assert.AreEqual(-20.0, dw.PitchDegrees, 1e-9, "W pitches the nose down");
            Assert.AreEqual(0.0, dw.RollDegrees, 1e-9);
            // E: about body +Z: yaw right
            FlightState e = level;
            e.Orientation = DQuat.ApplyBodyRotation(level.Orientation, Vec3.UnitZ * (30 * Deg));
            Assert.AreEqual(30.0, NavBall.Compute(e, Earth, false).HeadingDegrees, 1e-9, "E yaws right (heading grows)");
            // D: about body +Y: roll right (positive)
            FlightState dkey = level;
            dkey.Orientation = DQuat.ApplyBodyRotation(level.Orientation, Vec3.UnitY * (40 * Deg));
            NavBallData dd = NavBall.Compute(dkey, Earth, false);
            Assert.AreEqual(40.0, dd.RollDegrees, 1e-9, "D rolls right");
            Assert.AreEqual(0.0, dd.PitchDegrees, 1e-9);
            // upside down
            FlightState inverted = Craft(North, Up * -1.0);
            Assert.AreEqual(180.0, Math.Abs(NavBall.Compute(inverted, Earth, false).RollDegrees), 1e-9);
        }

        [Test]
        public void ANoseUpCraftHasPitch90AndFiniteAngles()
        {
            NavBallData d = NavBall.Compute(Craft(Up, North * -1.0), Earth, false);
            Assert.AreEqual(90.0, d.PitchDegrees, 1e-6);
            Assert.IsFalse(double.IsNaN(d.HeadingDegrees + d.RollDegrees));
            NavBallData down = NavBall.Compute(Craft(Up * -1.0, North), Earth, false);
            Assert.AreEqual(-90.0, down.PitchDegrees, 1e-6);
            Assert.IsFalse(double.IsNaN(down.HeadingDegrees + down.RollDegrees));
            // the vertical craft in the standard launch pose (SpawnBuilder.OnSurface)
            FlightParams p = FlightFixture.Params();
            FlightState pad = SpawnBuilder.OnSurface(p.Craft, p.Surface, 0.5, 0.3, 0.0, 0.0);
            NavBallData onPad = NavBall.Compute(pad, p.Surface, false);
            Assert.AreEqual(90.0, onPad.PitchDegrees, 1e-6);
            Assert.IsFalse(onPad.HasVelocity, "standing on the ground: no velocity relative to it");
        }

        [Test]
        public void SurfaceAndOrbitModesUseTheirOwnVelocity()
        {
            FlightState s = Craft(North, Up); // velocity = the velocity of the ground below: 0 relative to it
            NavBallData surface = NavBall.Compute(s, Earth, false);
            Assert.IsFalse(surface.HasVelocity);
            NavBallData orbit = NavBall.Compute(s, Earth, true);
            Assert.IsTrue(orbit.HasVelocity);
            Assert.AreEqual(FlightFixture.EarthOmega * (FlightFixture.EarthRadius + 1000.0), orbit.Speed, 1e-6);
            AssertV(new Vec3(1, 0, 0), orbit.Prograde, 1e-9); // eastwards
            AssertV(new Vec3(-1, 0, 0), orbit.Retrograde, 1e-9);

            FlightState climbing = s;
            climbing.Velocity = climbing.Velocity + Up * 100.0;
            NavBallData c = NavBall.Compute(climbing, Earth, false);
            Assert.IsTrue(c.HasVelocity);
            AssertV(new Vec3(0, 0, 1), c.Prograde, 1e-9);
            AssertV(new Vec3(0, 0, -1), c.Retrograde, 1e-9);
            Assert.AreEqual(100.0, c.Speed, 1e-6);
            // prograde of a climbing craft that points north and level: above the nose in the picture
            Vec3 view = NavBall.ToView(c, c.Prograde);
            Assert.AreEqual(1.0, view.Y, 1e-9);
        }

        [Test]
        public void ATinyVelocityHasNoMarker()
        {
            FlightState s = Craft(North, Up);
            s.Velocity = s.Velocity + new Vec3(0.001, 0, 0);
            Assert.IsFalse(NavBall.Compute(s, Earth, false).HasVelocity);
        }

        [Test]
        public void TheSasTargetMarkerShowsTheTargetNose()
        {
            FlightState s = Craft(North, Up);
            Assert.IsFalse(NavBall.Compute(s, Earth, false).HasSasTarget, "off: no marker");
            s.SasOn = 1;
            s.SasTarget = Pose(Vec3.Normalize(North + Up), Vec3.Normalize(Up - North)); // 45 degrees above the horizon
            NavBallData d = NavBall.Compute(s, Earth, false);
            Assert.IsTrue(d.HasSasTarget);
            AssertV(Vec3.Normalize(new Vec3(0, 1, 1)), d.SasTargetNose, 1e-9);
            Vec3 view = NavBall.ToView(d, d.SasTargetNose);
            Assert.AreEqual(Math.Sin(45 * Deg), view.Y, 1e-9, "above the center");
            Assert.AreEqual(0.0, view.X, 1e-9);
            // on target the marker is in the middle
            s.SasTarget = s.Orientation;
            AssertV(new Vec3(0, 0, -1), NavBall.ToView(NavBall.Compute(s, Earth, false), NavBall.Compute(s, Earth, false).SasTargetNose), 1e-9);
        }

        [Test]
        public void ViewMapping_RoundTripsOnTheFrontHemisphere()
        {
            Random rnd = new Random(3);
            for (int i = 0; i < 100; i++)
            {
                FlightState s = Craft(North, Up);
                s.Orientation = DQuat.Normalize(new DQuat(rnd.NextDouble() - 0.5, rnd.NextDouble() - 0.5, rnd.NextDouble() - 0.5, rnd.NextDouble() - 0.5));
                NavBallData d = NavBall.Compute(s, Earth, false);
                AssertV(new Vec3(1, 0, 0), new Vec3(Vec3.Length(d.Right), Vec3.Length(d.Up) - 1, Vec3.Length(d.Forward) - 1), 1e-9);
                Assert.AreEqual(0.0, Vec3.Dot(d.Right, d.Up), 1e-9);
                Assert.AreEqual(0.0, Vec3.Dot(d.Right, d.Forward), 1e-9);
                double x = rnd.NextDouble() * 1.4 - 0.7, y = rnd.NextDouble() * 1.4 - 0.7;
                Vec3 dir = NavBall.FromView(d, x, y);
                Assert.AreEqual(1.0, Vec3.Length(dir), 1e-9);
                Vec3 view = NavBall.ToView(d, dir);
                Assert.AreEqual(x, view.X, 1e-9);
                Assert.AreEqual(y, view.Y, 1e-9);
                Assert.Less(view.Z, 0.0);
            }
        }

        [Test]
        public void PolesAndTheCenterDoNotProduceNaN()
        {
            foreach (Vec3 position in new[] { new Vec3(0, FlightFixture.EarthRadius, 0), new Vec3(0, -FlightFixture.EarthRadius, 0), Vec3.Zero, new Vec3(1, 1e-20, 0) })
            {
                FlightState s = default;
                s.Position = position;
                s.Velocity = new Vec3(10, 20, 30);
                s.Orientation = DQuat.Identity;
                s.SasOn = 1;
                s.SasTarget = DQuat.Identity;
                foreach (bool orbit in new[] { false, true })
                {
                    NavBallData d = NavBall.Compute(s, Earth, orbit);
                    double sum = d.Forward.X + d.Forward.Y + d.Forward.Z + d.Up.X + d.Up.Y + d.Up.Z + d.Right.X + d.Right.Y + d.Right.Z +
                                 d.Prograde.X + d.Prograde.Y + d.Prograde.Z + d.PitchDegrees + d.HeadingDegrees + d.RollDegrees +
                                 d.SasTargetNose.X + d.SasTargetNose.Y + d.SasTargetNose.Z;
                    Assert.IsFalse(double.IsNaN(sum) || double.IsInfinity(sum), $"position {position.X},{position.Y},{position.Z} orbit {orbit}");
                    Assert.AreEqual(1.0, Vec3.Length(d.Forward), 1e-9);
                }
            }
            // the default (all zero) state of an old save
            FlightState zero = default;
            NavBallData z = NavBall.Compute(zero, Earth, false);
            Assert.IsFalse(double.IsNaN(z.PitchDegrees + z.HeadingDegrees + z.RollDegrees));
        }

        // ------------------------------------------------------------------ raster

        private static byte[] Render(NavBallData d, int size = 96)
        {
            byte[] buffer = new byte[NavBallRaster.BufferLength(size)];
            NavBallRaster.Render(d, size, buffer);
            return buffer;
        }

        private static (byte r, byte g, byte b, byte a) Pixel(byte[] buffer, int size, double x, double y)
        {
            int px = (int) ((x + 1.0) * 0.5 * size), py = (int) ((y + 1.0) * 0.5 * size);
            int o = (py * size + px) * 4;
            return (buffer[o], buffer[o + 1], buffer[o + 2], buffer[o + 3]);
        }

        [Test]
        public void Raster_SkyAboveAndGroundBelowTheHorizon_AndTransparentCorners()
        {
            const int size = 96;
            NavBallData d = NavBall.Compute(Craft(North, Up), Earth, false);
            byte[] buffer = Render(d, size);
            var sky = Pixel(buffer, size, 0.2, 0.45);
            var ground = Pixel(buffer, size, 0.2, -0.45);
            Assert.Greater(sky.b, sky.r + 50, "blue sky above the horizon");
            Assert.Greater(ground.r, ground.b + 50, "brown ground below");
            Assert.AreEqual(255, sky.a);
            Assert.AreEqual(0, Pixel(buffer, size, 0.97, 0.97).a, "outside the disc");
            Assert.AreEqual(0, buffer[3], "corner");
            // tilted over by 180 degrees (upside down): the sky is below
            byte[] inverted = Render(NavBall.Compute(Craft(North, Up * -1.0), Earth, false), size);
            var skyDown = Pixel(inverted, size, 0.2, -0.45);
            Assert.Greater(skyDown.b, skyDown.r + 50, "upside down: the sky is at the bottom");
            // nose up: the center looks at the zenith, the whole picture is mostly sky
            byte[] zenith = Render(NavBall.Compute(Craft(Up, North * -1.0), Earth, false), size);
            var center = Pixel(zenith, size, 0.0, 0.0);
            Assert.Greater(center.b, center.r, "nose up looks into the sky");
            byte[] nadir = Render(NavBall.Compute(Craft(Up * -1.0, North), Earth, false), size);
            var below = Pixel(nadir, size, 0.1, 0.1);
            Assert.Greater(below.r, below.b, "nose down looks at the ground");
        }

        [Test]
        public void Raster_DrawsTheHorizonLineAndTheGrid()
        {
            const int size = 128;
            NavBallData d = NavBall.Compute(Craft(North, Up), Earth, false);
            byte[] buffer = Render(d, size);
            var onHorizon = Pixel(buffer, size, 0.35, 0.0);
            Assert.Greater(onHorizon.r, 200, "the horizon line is bright");
            Assert.Greater(onHorizon.g, 200);
            // the parallel at 30 degrees: sin = 0.5 straight above the center
            // somewhere on the vertical line x=0.2 there are brighter than sky pixels (grid lines)
            int bright = 0;
            for (int py = size / 2 + 2; py < size - 4; py++)
            {
                int px = (int) (1.2 * 0.5 * size);
                int o = (py * size + px) * 4;
                if (buffer[o] > 150 && buffer[o + 3] == 255) bright++;
            }
            Assert.Greater(bright, 3, "grid lines every 10 degrees cross the column");
            Assert.Less(bright, size / 2 - 6, "but they are lines, not a fill");
            // the north meridian is red and in the center column above the horizon
            var north = Pixel(buffer, size, 0.0, 0.3);
            Assert.Greater(north.r, north.b, "north meridian marked");
        }

        [Test]
        public void Raster_IsDeterministic_AndFastEnough_AndSurvivesEveryPose()
        {
            const int size = 128;
            NavBallData d = NavBall.Compute(Craft(Vec3.Normalize(North + East + Up), Up), Earth, false);
            CollectionAssert.AreEqual(Render(d, size), Render(d, size));
            Stopwatch sw = Stopwatch.StartNew();
            const int n = 20;
            byte[] buffer = new byte[NavBallRaster.BufferLength(size)];
            for (int i = 0; i < n; i++) NavBallRaster.Render(d, size, buffer);
            sw.Stop();
            Console.WriteLine($"NavBall raster {size}x{size}: {sw.Elapsed.TotalMilliseconds / n:F2} ms per picture");
            Random rnd = new Random(11);
            for (int i = 0; i < 40; i++)
            {
                FlightState s = Craft(North, Up);
                s.Orientation = DQuat.Normalize(new DQuat(rnd.NextDouble() - 0.5, rnd.NextDouble() - 0.5, rnd.NextDouble() - 0.5, rnd.NextDouble() - 0.5));
                Assert.DoesNotThrow(() => NavBallRaster.Render(NavBall.Compute(s, Earth, i % 2 == 0), 48, new byte[NavBallRaster.BufferLength(48)]));
            }
            Assert.Throws<ArgumentException>(() => NavBallRaster.Render(d, 64, new byte[10]));
        }
    }
}
