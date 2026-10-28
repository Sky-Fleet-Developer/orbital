using System;

namespace Orbital.Core.Flight
{
    /// <summary>Main engine and tank arithmetic. Pure static functions.</summary>
    public static class EngineModel
    {
        /// <summary>Standard gravity used to turn specific impulse into exhaust velocity; same value as the server (Propagator.G0).</summary>
        public const double G0 = 9.80665;

        public static double Thrust(in EngineConfig e, double throttle) => e.MaxThrust * throttle;

        /// <summary>Mass flow at the given throttle, kg/s: F / (Isp * g0).</summary>
        public static double MassFlow(in EngineConfig e, double throttle) => e.MaxThrust * throttle / (e.Isp * G0);

        /// <summary>
        /// New throttle after dt: x jumps to full, z to zero (z wins), otherwise shift/ctrl (smoothAxis +1/-1) move it
        /// at ThrottleRate per second. Always within [0, 1].
        /// </summary>
        public static double UpdateThrottle(in EngineConfig e, double throttle, double smoothAxis, bool toFull, bool toZero, double dt)
        {
            if (toZero) return 0.0;
            if (toFull) return 1.0;
            double t = throttle + smoothAxis * e.ThrottleRate * dt;
            return t < 0.0 ? 0.0 : (t > 1.0 ? 1.0 : t);
        }

        /// <summary>
        /// Burns for dt. The fuel never goes below zero: when the tank runs dry inside the step the thrust is averaged over the step
        /// (the engine fires only part of it). Returns the mean thrust, the fuel burned and the velocity gained along the thrust line.
        /// </summary>
        public static void Burn(in EngineConfig e, double throttle, double fuel, double dryAndOtherMass, double dt,
            out double meanThrust, out double fuelUsed, out double deltaV)
        {
            double mdot = MassFlow(e, throttle);
            double wanted = mdot * dt;
            fuelUsed = wanted <= fuel ? wanted : (fuel > 0 ? fuel : 0);
            meanThrust = dt > 0 ? fuelUsed * e.Isp * G0 / dt : 0.0;
            double m0 = dryAndOtherMass + fuel;
            double m1 = m0 - fuelUsed;
            deltaV = fuelUsed > 0 && m1 > 0 ? e.Isp * G0 * Math.Log(m0 / m1) : 0.0;
        }

        /// <summary>Tsiolkovsky: delta-v for burning fuelUsed out of total mass m0 (same formula as the server's Propagator.DeltaV).</summary>
        public static double DeltaV(double isp, double m0, double fuelUsed)
        {
            double m1 = m0 - fuelUsed;
            return m1 <= 0 ? double.PositiveInfinity : isp * G0 * Math.Log(m0 / m1);
        }
    }
}
