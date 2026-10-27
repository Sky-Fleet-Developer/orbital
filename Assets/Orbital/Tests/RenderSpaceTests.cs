using System;
using Ara3D;
using NUnit.Framework;
using Orbital.Core.Rendering;

namespace Orbital.Tests
{
    public class RenderSpaceTests
    {
        private const double Near = 1e5;

        private static readonly DVector3 Camera = new DVector3(1.5e11, -3.0e10, 4.0e9);

        private static DVector3 Direction(double yaw, double pitch) =>
            new DVector3(Math.Cos(pitch) * Math.Cos(yaw), Math.Sin(pitch), Math.Cos(pitch) * Math.Sin(yaw));

        private static double Length(RenderPlacement p) => Math.Sqrt((double) p.X * p.X + (double) p.Y * p.Y + (double) p.Z * p.Z);

        [TestCase(0.0)]
        [TestCase(1.0)]
        [TestCase(5e4)]
        [TestCase(99999.0)]
        public void Nearby_IsExactAndUnscaled(double distance)
        {
            RenderSpace space = new RenderSpace(Near);
            DVector3 position = Camera + Direction(0.7, 0.3) * distance;
            RenderPlacement p = space.Place(Camera, position);
            Assert.IsFalse(p.Compressed);
            Assert.AreEqual(1f, p.Scale);
            Assert.AreEqual(distance, Length(p), Math.Max(1e-3, distance * 1e-6));
        }

        [Test]
        public void Nearby_KeepsMillimetrePrecisionAtAstronomicalWorldCoordinates()
        {
            RenderSpace space = new RenderSpace(Near);
            DVector3 position = Camera + new DVector3(12.345, -6.789, 0.001);
            RenderPlacement p = space.Place(Camera, position);
            Assert.AreEqual(12.345, p.X, 1e-4);
            Assert.AreEqual(-6.789, p.Y, 1e-4);
            Assert.AreEqual(0.001, p.Z, 1e-6);
        }

        [Test]
        public void Compression_IsContinuousAtTheNearLimit()
        {
            RenderSpace space = new RenderSpace(Near);
            Assert.AreEqual(Near, space.CompressDistance(Near), 1e-9);
            Assert.AreEqual(Near + 1, space.CompressDistance(Near + 1), 1e-3);
            // slope is 1 at the seam, so there is no visible kink
            double slope = (space.CompressDistance(Near * 1.000001) - space.CompressDistance(Near)) / (Near * 0.000001);
            Assert.AreEqual(1.0, slope, 1e-3);
        }

        [Test]
        public void Compression_IsStrictlyIncreasingSoDepthOrderIsKept()
        {
            RenderSpace space = new RenderSpace(Near);
            double previous = -1;
            for (double d = 1; d < 1e15; d *= 1.37)
            {
                double c = space.CompressDistance(d);
                Assert.Greater(c, previous, "distance " + d);
                Assert.LessOrEqual(c, d + 1e-6, "never farther than the truth, distance " + d);
                previous = c;
            }
        }

        [Test]
        public void Compression_StaysInsideAFarClipPlaneForTheWholeSolarSystem()
        {
            RenderSpace space = new RenderSpace(Near);
            Assert.Less(space.CompressDistance(1e13), 3e6);
        }

        [TestCase(1e6)]
        [TestCase(3.844e8)]
        [TestCase(1.5e11)]
        [TestCase(4.5e12)]
        public void Far_KeepsDirectionAndAngularSize(double distance)
        {
            RenderSpace space = new RenderSpace(Near);
            DVector3 direction = Direction(2.1, -0.4);
            const double bodyRadius = 1.7374e6;
            RenderPlacement p = space.Place(Camera, Camera + direction * distance);
            Assert.IsTrue(p.Compressed);

            double rendered = Length(p);
            Assert.AreEqual(direction.x, p.X / rendered, 1e-6);
            Assert.AreEqual(direction.y, p.Y / rendered, 1e-6);
            Assert.AreEqual(direction.z, p.Z / rendered, 1e-6);

            double trueAngle = Math.Atan2(bodyRadius, distance);
            double renderedAngle = Math.Atan2(bodyRadius * p.Scale, rendered);
            Assert.AreEqual(trueAngle, renderedAngle, trueAngle * 1e-5);
        }

        [Test]
        public void Far_ChangesSmoothlyAsTheCameraMoves()
        {
            RenderSpace space = new RenderSpace(Near);
            DVector3 position = Camera + Direction(0.2, 0.1) * 5e8;
            RenderPlacement a = space.Place(Camera, position);
            RenderPlacement b = space.Place(Camera + new DVector3(10, 0, 0), position);
            Assert.AreEqual(a.X, b.X, 1e-2 * Math.Abs(a.X) + 1);
            Assert.AreEqual(a.Scale, b.Scale, 1e-5 * a.Scale);
        }

        [Test]
        public void Placement_IsContinuousAcrossTheSeam()
        {
            RenderSpace space = new RenderSpace(Near);
            DVector3 d = Direction(1.0, 0.5);
            RenderPlacement inside = space.Place(Camera, Camera + d * (Near - 0.5));
            RenderPlacement outside = space.Place(Camera, Camera + d * (Near + 0.5));
            Assert.AreEqual(Length(inside) + 1.0, Length(outside), 0.05);
            Assert.AreEqual(1f, inside.Scale);
            Assert.AreEqual(1.0, outside.Scale, 1e-5);
        }

        [Test]
        public void InvalidNearLimit_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RenderSpace(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RenderSpace(double.NaN));
        }
    }
}
