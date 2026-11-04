using System;
using NUnit.Framework;
using Orbital.Core.Flight;

namespace Orbital.Core.Tests
{
    /// <summary>Stage 3.6: the painter's order of the sky. All impostors sit at one scaled distance, only the real distance tells who covers whom.</summary>
    [TestFixture]
    public class Stage3p6Tests
    {
        private static BodyViewEntry Entry(double distance) => new BodyViewEntry {Projection = new ProjectedBody {Distance = distance}};

        [Test]
        public void FarToNear_SortsByRealDistance_FarthestFirst()
        {
            BodyViewEntry[] entries = {Entry(5.0), Entry(100.0), Entry(1.0), Entry(50.0)};
            int[] order = new int[entries.Length];
            CelestialView.FarToNearOrder(entries, order);
            Assert.That(order, Is.EqualTo(new[] {1, 3, 0, 2}));
        }

        [Test]
        public void FarToNear_Ties_KeepEntryOrder()
        {
            BodyViewEntry[] entries = {Entry(2.0), Entry(2.0), Entry(3.0), Entry(2.0)};
            int[] order = new int[entries.Length];
            CelestialView.FarToNearOrder(entries, order);
            Assert.That(order, Is.EqualTo(new[] {2, 0, 1, 3}));
        }

        [Test]
        public void FarToNear_ShortOrder_Throws_EmptyIsFine()
        {
            Assert.Throws<ArgumentException>(() => CelestialView.FarToNearOrder(new[] {Entry(1.0), Entry(2.0)}, new int[1]));
            Assert.DoesNotThrow(() => CelestialView.FarToNearOrder(ReadOnlySpan<BodyViewEntry>.Empty, Span<int>.Empty));
        }

        [Test]
        public void Transit_MoonInFrontOfPlanet_IsPaintedLast_ThoughItsImpostorIsSmaller()
        {
            // Io over Jupiter seen from the Earth: the far bigger impostor would win any depth test at one scaled distance
            ProjectionCamera cam = new ProjectionCamera {Eye = new Vec3(0, 0, 0), HorizontalFovRad = 0.001, WidthPx = 512};
            ProjectedBody jupiter = CelestialProjection.Project(in cam, new Vec3(6.3e11 + 4.2e8, 0, 0), 7e7, 1e5, 3.0);
            ProjectedBody io = CelestialProjection.Project(in cam, new Vec3(6.3e11, 0, 0), 1.8e6, 1e5, 3.0);
            Assert.That(io.ScaledRadius, Is.LessThan(jupiter.ScaledRadius));
            BodyViewEntry[] entries = {new BodyViewEntry {Projection = io}, new BodyViewEntry {Projection = jupiter}};
            int[] order = new int[2];
            CelestialView.FarToNearOrder(entries, order);
            Assert.That(order, Is.EqualTo(new[] {1, 0}));
        }
    }
}
