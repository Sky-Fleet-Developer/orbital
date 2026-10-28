using System;

namespace Orbital.Core.Flight
{
    public static class FlightMode
    {
        public const int Flying = 0;
        public const int Landed = 1;
        public const int Crashed = 2;
    }

    /// <summary>State of one craft. Blittable: doubles, ints and nested blittable structs only.</summary>
    public struct FlightState
    {
        public Vec3 Position;      // parent-centered inertial frame, m (valid in Flying; recomputed from Latitude/Longitude when Landed)
        public Vec3 Velocity;      // m/s, same frame
        public DQuat Orientation;  // body to inertial
        public Vec3 Omega;         // rad/s, body frame
        public Vec3 GyroH;         // N*m*s, stored in the gyro, body frame
        public double MainFuel;    // kg
        public double RcsFuel;     // kg
        public double Throttle;    // 0..1
        public int Mode;           // FlightMode
        public int Dumping;        // 1 while the RCS unloads the gyro
        public double Latitude;    // rad, body-fixed; used when Landed
        public double Longitude;   // rad, body-fixed; used when Landed
        // Stability assist (stage 2b.1). All zero = off; an all-zero state from an older save is valid (the target is set when the SAS is switched on).
        public int SasOn;          // 1 while the SAS is on (copy of the T switch after the last step)
        public int SasPhase;       // SasPhase: Hold / Manual / Settle
        public DQuat SasTarget;    // target orientation, body to inertial
        public Vec3 SasIntegral;   // N*m, body frame: torque that cancels the constant disturbance (not tied to the target)
    }

    /// <summary>Read-only parameters shared by a batch of crafts.</summary>
    public struct FlightParams
    {
        public FlightConfig Craft;
        public SurfaceData Surface;
        public double Nu;               // gravitational parameter of the body the craft is around
        public double MaxLandingSpeed;  // m/s relative to the ground (server ServerConfig.MaxLandingSpeed)
    }

    /// <summary>What a step consumed and what happened. The server gets the resources from here (stage 1.4).</summary>
    public struct StepReport
    {
        public double MainFuelUsed;   // kg
        public double RcsFuelUsed;    // kg, RCS (pilot and unloading); the gyro uses no propellant
        public double MeanThrust;     // N
        public double DeltaV;         // m/s along the thrust line (Tsiolkovsky for the step)
        public int ModeBefore;
        public int ModeAfter;
        public double ImpactSpeed;    // m/s relative to the ground when a touchdown happened in this step
        public double TouchdownTime;  // game time of the touchdown, or NaN
    }

    /// <summary>
    /// Advances one craft by dt. A pure function of its arguments: no statics, no allocations, no external state, so any number of crafts
    /// can be advanced in any order or in parallel with the same result. dt should be small (0.02 to 0.5 s): thrust direction is taken at
    /// the start of the step.
    /// </summary>
    public static class FlightKernel
    {
        public static double TotalMass(in FlightConfig c, in FlightState s) => c.DryMass + s.MainFuel + s.RcsFuel;

        /// <summary>
        /// Principal moments for the current propellant: <see cref="FlightConfig.Inertia"/> is the value with full tanks, each kg burned
        /// takes <see cref="FlightConfig.FuelInertiaPerKg"/> away (never below 1 percent of the full value). The center of mass is assumed fixed.
        /// </summary>
        public static Vec3 EffectiveInertia(in FlightConfig c, double mainFuel, double rcsFuel)
        {
            double missing = (c.MainFuelCapacity - mainFuel) + (c.RcsPropellantCapacity - rcsFuel);
            if (missing < 0) missing = 0;
            return new Vec3(
                Math.Max(c.Inertia.X - missing * c.FuelInertiaPerKg.X, 0.01 * c.Inertia.X),
                Math.Max(c.Inertia.Y - missing * c.FuelInertiaPerKg.Y, 0.01 * c.Inertia.Y),
                Math.Max(c.Inertia.Z - missing * c.FuelInertiaPerKg.Z, 0.01 * c.Inertia.Z));
        }

        public static FlightState Fresh(in FlightConfig c)
        {
            FlightState s = default;
            s.Orientation = DQuat.Identity;
            s.SasTarget = DQuat.Identity;
            s.MainFuel = c.MainFuelCapacity;
            s.RcsFuel = c.RcsPropellantCapacity;
            return s;
        }

        public static void Step(ref FlightState s, in ControlInput input, in FlightParams p, double time, double dt, out StepReport report)
        {
            report = default;
            report.ModeBefore = s.Mode;
            report.TouchdownTime = double.NaN;
            if (s.Mode == FlightMode.Crashed)
            {
                report.ModeAfter = s.Mode;
                return;
            }
            ref readonly FlightConfig c = ref p.Craft;
            double massStart = TotalMass(c, s);

            // throttle and the engine
            s.Throttle = EngineModel.UpdateThrottle(c.Engine, s.Throttle, input.ThrottleSmooth, input.ThrottleFull, input.ThrottleZero, dt);
            if (s.MainFuel <= 0) s.Throttle = 0.0; // nothing to burn: the lever is dead, not just the thrust
            EngineModel.Burn(c.Engine, s.Throttle, s.MainFuel, c.DryMass + s.RcsFuel, dt, out double meanThrust, out double fuelUsed, out double dv);
            Vec3 thrustDir = DQuat.Rotate(s.Orientation, Vec3.UnitY);

            // ground position, lift-off
            Vec3 normal;
            if (s.Mode == FlightMode.Landed)
            {
                s.Position = Surface.Point(p.Surface, s.Latitude, s.Longitude, time);
                s.Velocity = Surface.GroundVelocity(p.Surface, s.Position);
                normal = Surface.Normal(p.Surface, s.Position);
                double weight = massStart * p.Nu / (p.Surface.Radius * p.Surface.Radius);
                if (meanThrust * Vec3.Dot(thrustDir, normal) > weight) s.Mode = FlightMode.Flying;
            }
            else normal = Surface.Normal(p.Surface, s.Position);

            // attitude: gyro (always), RCS (when on), unloading, SAS, disturbances
            Vec3 axes = InputMapper.AxesVector(input);
            Vec3 inertia = EffectiveInertia(c, s.MainFuel, s.RcsFuel);
            Vec3 torque = Vec3.Zero;
            double rcsUsed = 0;
            Vec3 dumpCommand = Vec3.Zero;
            if (input.RcsOn)
            {
                s.Dumping = GyroModel.UpdateDumping(c.Gyro, s.GyroH, s.Dumping != 0) ? 1 : 0;
                if (s.Dumping != 0) dumpCommand = GyroModel.DumpCommand(c.Gyro, c.Rcs, s.GyroH);
            }
            else s.Dumping = 0;
            // stability assist: commands added to the pilot's; the RCS share left free by the unloading is what it may use
            Vec3 rcsCap = new Vec3(1.0 - Math.Abs(dumpCommand.X), 1.0 - Math.Abs(dumpCommand.Y), 1.0 - Math.Abs(dumpCommand.Z));
            SasController.Step(ref s, input, c, inertia, input.RcsOn && s.RcsFuel > 0.0, rcsCap, dt, out SasOutput sas);
            Vec3 hdot = GyroModel.PilotHdot(c.Gyro, s.GyroH, axes + sas.GyroCommand);
            if (input.RcsOn)
            {
                RcsOutput pilot = RcsModel.Fire(c.Rcs, axes + sas.RcsCommand, s.RcsFuel, dt);
                torque = torque + pilot.Torque;
                rcsUsed += pilot.Propellant;
                if (s.Dumping != 0)
                {
                    RcsOutput dump = RcsModel.Fire(c.Rcs, dumpCommand, s.RcsFuel - rcsUsed, dt);
                    torque = torque + dump.Torque;
                    hdot = hdot + dump.Torque; // the gyro takes the unloading torque into its store: the craft feels nothing
                    rcsUsed += dump.Propellant;
                }
            }
            // thrust line not through the center of mass
            torque = torque + Vec3.Cross(c.ThrustOffset, new Vec3(0, meanThrust, 0));
            // standing on legs: gravity acts on the center of mass above the contact point (inverted pendulum)
            if (s.Mode == FlightMode.Landed)
            {
                Vec3 upBody = DQuat.RotateInverse(s.Orientation, normal);
                double g = p.Nu / (p.Surface.Radius * p.Surface.Radius);
                torque = torque + Vec3.Cross(new Vec3(0, c.CgHeight, 0), upBody * (-massStart * g));
            }
            AngularState a;
            a.Orientation = s.Orientation;
            a.Omega = s.Omega;
            RigidBodyAngular.Step(ref a, inertia, torque, s.GyroH, hdot, dt);
            s.Orientation = a.Orientation;
            s.Omega = a.Omega;
            s.GyroH = GyroModel.ClampStore(c.Gyro, s.GyroH + hdot * dt);

            // translation
            double fuelBurnedFraction = 1.0;
            if (s.Mode == FlightMode.Landed)
            {
                s.Position = Surface.Point(p.Surface, s.Latitude, s.Longitude, time + dt);
                s.Velocity = Surface.GroundVelocity(p.Surface, s.Position);
            }
            else
            {
                double mdot = dt > 0 ? (fuelUsed + rcsUsed) / dt : 0.0;
                Vec3 force = thrustDir * meanThrust;
                Translate(p.Nu, s.Position, s.Velocity, massStart, mdot, force, 0.0, dt, out Vec3 np, out Vec3 nv);
                if (Vec3.Length(np) <= p.Surface.Radius)
                {
                    double lo = 0, hi = dt;
                    for (int i = 0; i < 60; i++)
                    {
                        double mid = 0.5 * (lo + hi);
                        Translate(p.Nu, s.Position, s.Velocity, massStart, mdot, force, 0.0, mid, out Vec3 mp, out Vec3 mv);
                        if (Vec3.Length(mp) > p.Surface.Radius) lo = mid; else hi = mid;
                    }
                    Translate(p.Nu, s.Position, s.Velocity, massStart, mdot, force, 0.0, hi, out np, out nv);
                    np = np / Vec3.Length(np) * p.Surface.Radius;
                    report.ImpactSpeed = Surface.SpeedRelativeToGround(p.Surface, np, nv);
                    report.TouchdownTime = time + hi;
                    fuelBurnedFraction = dt > 0 ? hi / dt : 1.0;
                    if (Surface.IsSoftTouchdown(p.Surface, np, nv, p.MaxLandingSpeed))
                    {
                        s.Mode = FlightMode.Landed;
                        Surface.ToBodyCoordinates(p.Surface, np, time + hi, out s.Latitude, out s.Longitude);
                        s.Position = np;
                        s.Velocity = Surface.GroundVelocity(p.Surface, np);
                        s.Omega = Vec3.Zero;
                    }
                    else
                    {
                        s.Mode = FlightMode.Crashed;
                        s.Position = np;
                        s.Velocity = nv;
                    }
                }
                else
                {
                    s.Position = np;
                    s.Velocity = nv;
                }
            }

            fuelUsed *= fuelBurnedFraction;
            rcsUsed *= fuelBurnedFraction;
            s.MainFuel -= fuelUsed;
            if (s.MainFuel < 0) s.MainFuel = 0;
            if (s.MainFuel <= 0) s.Throttle = 0.0; // the tank ran dry inside this step
            s.RcsFuel -= rcsUsed;
            if (s.RcsFuel < 0) s.RcsFuel = 0;
            report.MainFuelUsed = fuelUsed;
            report.RcsFuelUsed = rcsUsed;
            report.MeanThrust = meanThrust;
            report.DeltaV = dv * fuelBurnedFraction;
            report.ModeAfter = s.Mode;
        }

        /// <summary>RK4 for position and velocity under point-mass gravity and a constant force; the mass falls linearly (same scheme as the server BurnIntegrator).</summary>
        public static void Translate(double nu, Vec3 pos, Vec3 vel, double m0, double mdot, Vec3 force, double t, double h, out Vec3 np, out Vec3 nv)
        {
            double mA = m0 - mdot * t, mB = m0 - mdot * (t + h / 2), mC = m0 - mdot * (t + h);
            Vec3 k1p = vel, k1v = Accel(nu, pos, force, mA);
            Vec3 k2p = vel + k1v * (h / 2), k2v = Accel(nu, pos + k1p * (h / 2), force, mB);
            Vec3 k3p = vel + k2v * (h / 2), k3v = Accel(nu, pos + k2p * (h / 2), force, mB);
            Vec3 k4p = vel + k3v * h, k4v = Accel(nu, pos + k3p * h, force, mC);
            np = pos + (k1p + k2p * 2 + k3p * 2 + k4p) * (h / 6);
            nv = vel + (k1v + k2v * 2 + k3v * 2 + k4v) * (h / 6);
        }

        private static Vec3 Accel(double nu, Vec3 pos, Vec3 force, double mass)
        {
            double r = Vec3.Length(pos);
            return pos * (-nu / (r * r * r)) + force / mass;
        }
    }
}
