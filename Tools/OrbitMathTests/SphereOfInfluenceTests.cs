using System;
using NUnit.Framework;
using Orbital.Core.TrajectorySystem;

namespace Orbital.Tests
{
    /// <summary>Laplace sphere of influence r = a * (m / M)^(2/5); masses enter as gravitational parameters.</summary>
    public class SphereOfInfluenceTests
    {
        private const double SunNu = 1.32712440018e20;
        private const double EarthNu = 3.986004418e14;
        private const double MoonNu = 4.9048695e12;
        private const double MarsNu = 4.282837e13;

        [Test]
        public void Moon_AroundEarth_IsAbout66ThousandKm()
        {
            double r = TrajectoryUtility.GetSphereOfInfluenceRadius(3.844e8, MoonNu, EarthNu);
            Assert.That(r, Is.EqualTo(66.1e6).Within(1.5e6));
        }

        [Test]
        public void Earth_AroundSun_IsAbout925ThousandKm()
        {
            double r = TrajectoryUtility.GetSphereOfInfluenceRadius(1.495978707e11, EarthNu, SunNu);
            Assert.That(r, Is.EqualTo(925e6).Within(10e6));
        }

        [Test]
        public void Mars_AroundSun_IsAbout577ThousandKm()
        {
            double r = TrajectoryUtility.GetSphereOfInfluenceRadius(2.279392e11, MarsNu, SunNu);
            Assert.That(r, Is.EqualTo(577e6).Within(10e6));
        }

        [Test]
        public void Root_HasInfiniteRadius()
        {
            Assert.IsTrue(double.IsPositiveInfinity(TrajectoryUtility.GetSphereOfInfluenceRadius(0, SunNu, 0)));
        }

        [Test]
        public void ScalesWithTwoFifthsPowerOfMassRatio_AndLinearlyWithAxis()
        {
            double r1 = TrajectoryUtility.GetSphereOfInfluenceRadius(1e9, 1e12, 1e15);
            double r2 = TrajectoryUtility.GetSphereOfInfluenceRadius(2e9, 1e12, 1e15);
            double r3 = TrajectoryUtility.GetSphereOfInfluenceRadius(1e9, 32e12, 1e15);
            Assert.AreEqual(2.0, r2 / r1, 1e-12);
            Assert.AreEqual(4.0, r3 / r1, 1e-12); // 32^(2/5) = 4
        }

        [Test]
        public void NegativeSemiMajorAxis_UsesMagnitude()
        {
            Assert.AreEqual(TrajectoryUtility.GetSphereOfInfluenceRadius(1e9, 1e12, 1e15),
                TrajectoryUtility.GetSphereOfInfluenceRadius(-1e9, 1e12, 1e15), 1e-6);
        }

        // ---- minimum radius: r = max(Laplace, R * k), 1 < k < 2


        [Test]
        public void Phobos_GetsRadiusTimesFactor_WhenLaplaceIsSmaller()
        {
            double a = 9376000, phobosNu = 6.6743e-11 * 1.0659e16, radius = 11267;
            double laplace = TrajectoryUtility.GetSphereOfInfluenceRadius(a, phobosNu, MarsNu);
            Assert.That(laplace, Is.LessThan(radius), "the plain Laplace sphere is inside the body");
            double r = TrajectoryUtility.GetSphereOfInfluenceRadius(a, phobosNu, MarsNu, radius);
            Assert.That(r, Is.EqualTo(1.5 * radius).Within(1e-9));
            Assert.That(r, Is.GreaterThanOrEqualTo(16900.0));
        }

        [Test]
        public void MinimumDoesNotChangeBigBodies_AndFactorIsConfigurable()
        {
            double plain = TrajectoryUtility.GetSphereOfInfluenceRadius(3.844e8, MoonNu, EarthNu);
            Assert.AreEqual(plain, TrajectoryUtility.GetSphereOfInfluenceRadius(3.844e8, MoonNu, EarthNu, 1.7374e6), 1e-6);
            Assert.AreEqual(1.2 * 11267, TrajectoryUtility.GetSphereOfInfluenceRadius(9376000, 1e5, MarsNu, 11267, 1.2), 1e-9);
            Assert.AreEqual(1.99 * 11267, TrajectoryUtility.GetSphereOfInfluenceRadius(9376000, 1e5, MarsNu, 11267, 1.99), 1e-9);
        }

        [Test]
        public void FactorOutsideOneToTwo_IsRejected()
        {
            foreach (double k in new[] { 1.0, 2.0, 0.5, 3.0, double.NaN, -1.5 })
                Assert.Throws<ArgumentOutOfRangeException>(() => TrajectoryUtility.GetSphereOfInfluenceRadius(1e9, 1e12, 1e15, 100, k), "k = " + k);
            Assert.AreEqual(1.5, TrajectoryUtility.DefaultMinRadiusFactor);
        }

        [Test]
        public void Root_StaysInfinite_EvenWithARadius()
        {
            Assert.IsTrue(double.IsPositiveInfinity(TrajectoryUtility.GetSphereOfInfluenceRadius(0, SunNu, 0, 6.957e8)));
        }

        [Test]
        public void UnknownRadiusOrBarycenter_AppliesNoMinimum()
        {
            Assert.AreEqual(TrajectoryUtility.GetSphereOfInfluenceRadius(1e9, 1e12, 1e15),
                TrajectoryUtility.GetSphereOfInfluenceRadius(1e9, 1e12, 1e15, 0.0), 0);
        }
    }
}
