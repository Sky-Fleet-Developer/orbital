using System;
using Ara3D;
using NUnit.Framework;
using Orbital.Core.TrajectorySystem;

namespace Orbital.Tests
{
    public static class OrbitTestUtility
    {
        /// <summary>Earth-like gravitational parameter, m^3/s^2.</summary>
        public const double Mu = 3.986004418e14;

        public static StaticOrbit Make(double inclination, double eccentricity, double semiMajorAxis,
            double lan, double aop, double meanAnomalyAtEpoch, double epoch = 0, double mu = Mu)
        {
            StaticOrbit orbit = new StaticOrbit { Nu = mu };
            orbit.SetOrbit(inclination, eccentricity, semiMajorAxis, lan, aop, meanAnomalyAtEpoch, epoch);
            return orbit;
        }

        /// <summary>Semi-major axis is negative for hyperbolas.</summary>
        public static double SemiMajorFromPeriapsis(double periapsis, double eccentricity) =>
            periapsis / (1.0 - eccentricity);

        public static double Energy(DVector3 pos, DVector3 vel, double mu) =>
            vel.LengthSquared() / 2.0 - mu / pos.Length();

        public static DVector3 AngularMomentum(DVector3 pos, DVector3 vel) => DVector3.Cross(pos, vel);

        public static double WrapAngle(double angle)
        {
            angle %= 2 * Math.PI;
            if (angle > Math.PI) angle -= 2 * Math.PI;
            if (angle < -Math.PI) angle += 2 * Math.PI;
            return angle;
        }

        public static void AssertVectorNear(DVector3 expected, DVector3 actual, double tolerance, string message = "")
        {
            double distance = (expected - actual).Length();
            Assert.LessOrEqual(distance, tolerance,
                $"{message} expected ({expected.x:R}, {expected.y:R}, {expected.z:R}) actual ({actual.x:R}, {actual.y:R}, {actual.z:R}) distance {distance:E3}");
        }

        public static void AssertAngleNear(double expected, double actual, double tolerance, string message = "")
        {
            Assert.LessOrEqual(Math.Abs(WrapAngle(expected - actual)), tolerance,
                $"{message} expected {expected:R} actual {actual:R}");
        }

        /// <summary>Reference two-body propagator: RK4 in Cartesian coordinates, independent of the code under test.</summary>
        public static void PropagateRk4(DVector3 pos, DVector3 vel, double mu, double duration, double step,
            out DVector3 resultPos, out DVector3 resultVel)
        {
            int count = (int) Math.Ceiling(Math.Abs(duration) / step);
            double dt = duration / count;
            for (int i = 0; i < count; i++)
            {
                DVector3 k1p = vel, k1v = Accel(pos, mu);
                DVector3 k2p = vel + k1v * (dt / 2), k2v = Accel(pos + k1p * (dt / 2), mu);
                DVector3 k3p = vel + k2v * (dt / 2), k3v = Accel(pos + k2p * (dt / 2), mu);
                DVector3 k4p = vel + k3v * dt, k4v = Accel(pos + k3p * dt, mu);
                pos += (k1p + k2p * 2 + k3p * 2 + k4p) * (dt / 6);
                vel += (k1v + k2v * 2 + k3v * 2 + k4v) * (dt / 6);
            }

            resultPos = pos;
            resultVel = vel;
        }

        private static DVector3 Accel(DVector3 pos, double mu)
        {
            double r = pos.Length();
            return pos * (-mu / (r * r * r));
        }
    }
}
