using System;

namespace Orbital.Core.Flight
{
    /// <summary>What the descent instruments show. Everything is computed for a full throttle burn against the velocity relative to the ground.</summary>
    public struct LandingAdvice
    {
        /// <summary>The coast ends on the ground.</summary>
        public bool Falling;
        /// <summary>Seconds of coasting to the ground.</summary>
        public double TimeToImpact;
        /// <summary>Speed relative to the ground at that moment, m/s.</summary>
        public double ImpactSpeed;
        /// <summary>Seconds until the last moment to start the burn so that it stops at <see cref="LandingAdvisor.TargetHeight"/>. 0 = now or late.</summary>
        public double BurnIn;
        /// <summary>How long that burn lasts, seconds.</summary>
        public double BurnTime;
        /// <summary>Fuel of that burn, kg.</summary>
        public double FuelNeeded;
        /// <summary>A burn started now stops the craft above the ground.</summary>
        public bool CanStop;
        /// <summary>Height where a full burn started now ends the speed, m; 0 when the ground comes first.</summary>
        public double StopHeightNow;
        /// <summary>Speed at the ground for a full burn started now, m/s (0 when it stops above the ground).</summary>
        public double SpeedAtGroundNow;
        /// <summary>The tank runs dry before the craft stops, if the burn starts now.</summary>
        public bool OutOfFuel;
        /// <summary>Angle between the nose and the direction against the velocity relative to the ground, degrees.</summary>
        public double RetrogradeError;
    }

    /// <summary>
    /// The descent instrument (stage 4.1): when to light the engine. A point mass under the body's gravity, numerically: first the coast down to the ground,
    /// then, from a point of that coast, a burn at full throttle always against the velocity relative to the rotating ground, with the mass falling. The burn
    /// that ends just above the ground is found by bisection along the coast. Turning the craft takes time and is not counted: the burn assumes the nose is
    /// already on the retrograde, see <see cref="LandingAdvice.RetrogradeError"/>. Cheap enough for a few calls a second. Never touches the simulation.
    /// </summary>
    public static class LandingAdvisor
    {
        /// <summary>The planned burn ends this high, meters: a margin to finish the landing by hand.</summary>
        public const double TargetHeight = 50.0;
        private const int MaxCoastSamples = 4096;
        private const int MaxBurnSteps = 2000;
        private const int BisectionIterations = 14;

        private struct Sample
        {
            public double Time;
            public Vec3 Position;
            public Vec3 Velocity;
        }

        [ThreadStatic] private static Sample[] _coast;

        /// <summary>The advice for a flying craft; <see cref="LandingAdvice.Falling"/> stays false when the coast does not reach the ground.</summary>
        /// <param name="exitRadius">The coast ends beyond this distance (the sphere of influence): gravity of this body stops counting there.</param>
        public static LandingAdvice Compute(in FlightState s, in FlightParams p, double exitRadius)
        {
            LandingAdvice a = default;
            if (s.Mode != FlightMode.Flying || !(p.Surface.Radius > 0.0) || !(p.Nu > 0.0)) return a;
            Vec3 relative = s.Velocity - Surface.GroundVelocity(p.Surface, s.Position);
            double speed = Vec3.Length(relative);
            if (speed > 1e-3)
            {
                Vec3 nose = DQuat.Rotate(s.Orientation, Vec3.UnitY);
                double cos = Vec3.Dot(nose, relative * (-1.0 / speed));
                a.RetrogradeError = Math.Acos(Math.Max(-1.0, Math.Min(1.0, cos))) * 180.0 / Math.PI;
            }
            if (!ReachesGround(s.Position, s.Velocity, p)) return a;

            Sample[] coast = _coast ??= new Sample[MaxCoastSamples];
            int count = Coast(s, p, exitRadius, coast, out double impactTime, out double impactSpeed);
            if (count == 0) return a;
            a.Falling = true;
            a.TimeToImpact = impactTime;
            a.ImpactSpeed = impactSpeed;

            double mass = p.Craft.DryMass + s.MainFuel + s.RcsFuel;
            Burn(coast[0], p, mass, s.MainFuel, out double stopHeight, out double burnTime, out double fuel, out double groundSpeed, out bool dry);
            a.StopHeightNow = stopHeight;
            a.SpeedAtGroundNow = groundSpeed;
            a.OutOfFuel = dry;
            a.CanStop = !dry && stopHeight > 0.0;
            a.BurnTime = burnTime;
            a.FuelNeeded = fuel;
            if (dry || stopHeight < TargetHeight) return a; // now or too late

            int low = 0, high = count - 1; // low: stops above the target; high: the ground itself
            for (int i = 0; i < BisectionIterations && high - low > 1; i++)
            {
                int middle = (low + high) / 2;
                Burn(coast[middle], p, mass, s.MainFuel, out double h, out _, out _, out _, out bool noFuel);
                if (!noFuel && h >= TargetHeight) low = middle;
                else high = middle;
            }
            // the samples are seconds apart up high: find the moment between the two
            double earlier = 0.0, later = coast[high].Time - coast[low].Time;
            for (int i = 0; i < BisectionIterations; i++)
            {
                double middle = 0.5 * (earlier + later);
                Burn(CoastOn(coast[low], p.Nu, middle), p, mass, s.MainFuel, out double h, out _, out _, out _, out bool noFuel);
                if (!noFuel && h >= TargetHeight) earlier = middle;
                else later = middle;
            }
            Burn(CoastOn(coast[low], p.Nu, earlier), p, mass, s.MainFuel, out _, out double t, out double f, out _, out _);
            a.BurnIn = coast[low].Time + earlier;
            a.BurnTime = t;
            a.FuelNeeded = f;
            return a;
        }

        private static Sample CoastOn(in Sample from, double nu, double dt)
        {
            Sample next = from;
            Step(ref next.Position, ref next.Velocity, nu, default, 0.0, dt);
            next.Time += dt;
            return next;
        }

        // The conic only says yes or no. A radial fall has no conic but a zero pericenter, which is the right answer.
        private static bool ReachesGround(Vec3 position, Vec3 velocity, in FlightParams p)
        {
            double r = Vec3.Length(position);
            double energy = 0.5 * Vec3.Dot(velocity, velocity) - p.Nu / r;
            Vec3 h = Vec3.Cross(position, velocity);
            double h2 = Vec3.Dot(h, h);
            double e2 = 1.0 + 2.0 * energy * h2 / (p.Nu * p.Nu);
            double pericenter = h2 / p.Nu / (1.0 + Math.Sqrt(Math.Max(0.0, e2)));
            if (pericenter >= p.Surface.Radius) return false;
            return energy < 0.0 || Vec3.Dot(position, velocity) < 0.0; // an open conic going away never returns
        }

        private static int Coast(in FlightState s, in FlightParams p, double exitRadius, Sample[] samples, out double impactTime, out double impactSpeed)
        {
            impactTime = 0.0;
            impactSpeed = 0.0;
            Vec3 pos = s.Position, vel = s.Velocity;
            double time = 0.0;
            int count = 0;
            while (count < samples.Length)
            {
                samples[count++] = new Sample {Time = time, Position = pos, Velocity = vel};
                double height = Surface.Altitude(p.Surface, pos);
                if (height <= 0.0)
                {
                    impactTime = time;
                    impactSpeed = Vec3.Length(vel - Surface.GroundVelocity(p.Surface, pos));
                    return count;
                }
                double r = Vec3.Length(pos);
                if (r > exitRadius) return 0;
                double v = Math.Max(1.0, Vec3.Length(vel));
                // a step is a small part of the way down, or of the turn around the body up high
                double dt = Math.Max(0.02, Math.Min(0.02 * r / v, Math.Max(0.05 * height, 2.0) / v));
                Step(ref pos, ref vel, p.Nu, default, 0.0, dt);
                time += dt;
            }
            return 0; // not on the ground within the samples: too far ahead to advise
        }

        // A burn from a point of the coast. Ends when the speed relative to the ground is gone, at the ground or with an empty tank.
        private static void Burn(in Sample from, in FlightParams p, double mass, double fuel, out double stopHeight, out double burnTime, out double fuelUsed,
            out double groundSpeed, out bool dry)
        {
            Vec3 pos = from.Position, vel = from.Velocity;
            double thrust = p.Craft.Engine.MaxThrust;
            double flow = EngineModel.MassFlow(p.Craft.Engine, 1.0);
            double left = fuel;
            burnTime = 0.0;
            fuelUsed = 0.0;
            groundSpeed = 0.0;
            dry = false;
            stopHeight = Surface.Altitude(p.Surface, pos);
            double firstSpeed = Vec3.Length(vel - Surface.GroundVelocity(p.Surface, pos));
            double dt = Math.Max(0.01, Math.Min(1.0, firstSpeed * mass / thrust / 100.0));
            for (int i = 0; i < MaxBurnSteps; i++)
            {
                Vec3 relative = vel - Surface.GroundVelocity(p.Surface, pos);
                double speed = Vec3.Length(relative);
                double accel = thrust / mass;
                stopHeight = Surface.Altitude(p.Surface, pos);
                if (stopHeight <= 0.0)
                {
                    stopHeight = 0.0;
                    groundSpeed = speed;
                    return;
                }
                if (speed <= accel * dt) return;
                double used = flow * dt;
                if (used >= left)
                {
                    dry = true;
                    return;
                }
                Step(ref pos, ref vel, p.Nu, relative * (-1.0 / speed), accel, dt);
                left -= used;
                mass -= used;
                fuelUsed += used;
                burnTime += dt;
            }
        }

        // RK4 with a thrust of a fixed direction for the step
        private static void Step(ref Vec3 pos, ref Vec3 vel, double nu, Vec3 direction, double accel, double dt)
        {
            Vec3 thrust = direction * accel;
            Vec3 a1 = Gravity(pos, nu) + thrust;
            Vec3 p2 = pos + vel * (dt * 0.5), v2 = vel + a1 * (dt * 0.5);
            Vec3 a2 = Gravity(p2, nu) + thrust;
            Vec3 p3 = pos + v2 * (dt * 0.5), v3 = vel + a2 * (dt * 0.5);
            Vec3 a3 = Gravity(p3, nu) + thrust;
            Vec3 p4 = pos + v3 * dt, v4 = vel + a3 * dt;
            Vec3 a4 = Gravity(p4, nu) + thrust;
            pos = pos + (vel + v2 * 2.0 + v3 * 2.0 + v4) * (dt / 6.0);
            vel = vel + (a1 + a2 * 2.0 + a3 * 2.0 + a4) * (dt / 6.0);
        }

        private static Vec3 Gravity(Vec3 position, double nu)
        {
            double r2 = Vec3.Dot(position, position);
            return position * (-nu / (r2 * Math.Sqrt(r2)));
        }
    }
}
