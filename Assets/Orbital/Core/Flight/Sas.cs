using System;

namespace Orbital.Core.Flight
{
    /// <summary>Phase of the stability assist while it is on (<see cref="FlightState.SasPhase"/>).</summary>
    public static class SasPhase
    {
        /// <summary>The target orientation is fixed and the controller holds it.</summary>
        public const int Hold = 0;
        /// <summary>The pilot holds an attitude key: the controller yields, the target follows the orientation, the integral is frozen.</summary>
        public const int Manual = 1;
        /// <summary>Keys released: the rotation is braked first; when the rate is below the capture threshold the current orientation becomes the target.</summary>
        public const int Settle = 2;
    }

    /// <summary>
    /// Numbers of the stability assist. The controller is a PD law written with a natural frequency and a damping ratio (closed loop
    /// theta'' + 2 z wn theta' + wn^2 theta = 0 in the linear range) plus an integral that estimates the constant disturbing torque.
    /// </summary>
    public struct SasConfig
    {
        public double NaturalFrequency; // rad/s, wn
        public double Damping;          // damping ratio z (1 = critical)
        public double IntegralTime;     // s, Ti: the integral gain is wn^2 / Ti; 0 or less switches the integral off
        public double MaxRate;          // rad/s, largest turn rate the controller asks for when the error is large
        public double CaptureRate;      // rad/s: after the keys are released the target is captured when the rate is below this
        public double IntegralLimit;    // fraction of the torque the actuators can deliver (RCS when it is available, else the gyro) that the integral may reach
        public double AuthorityMargin;  // fraction of the available acceleration the braking profile of a large turn may use (0..1)

        public static SasConfig Default() => new SasConfig
        {
            NaturalFrequency = 0.8,
            Damping = 1.0,
            IntegralTime = 3.0,
            MaxRate = 0.3,
            CaptureRate = 0.002,
            IntegralLimit = 0.9,
            AuthorityMargin = 0.5,
        };
    }

    /// <summary>What the controller asks of the actuators in one step: commands in [-1, 1] per body axis (X pitch, Y roll, Z yaw), added to the pilot's own.</summary>
    public struct SasOutput
    {
        public Vec3 GyroCommand;
        public Vec3 RcsCommand;
    }

    /// <summary>
    /// Stability assist (SAS): holds an orientation fixed in the inertial frame.
    /// <para>
    /// Integral: <see cref="FlightState.SasIntegral"/> is the torque (N*m, body frame) the controller adds to cancel a constant disturbance
    /// (an engine whose thrust line misses the center of mass). It is a property of the craft, not of the target: it is NOT reset by pilot
    /// input or by a change of the target, only when the SAS is switched on, and it is limited to what the actuators can deliver. While the pilot
    /// turns the craft the integral is frozen (and applied as a feed-forward through the RCS); after the keys are released a rate loop with
    /// its own integral brakes the rotation, and only then the orientation is captured, so the captured angle does not sag.
    /// </para>
    /// <para>
    /// Allocation: the gyro takes the fast PD part (inside its torque limit and soft zone); the RCS, when it is on and has propellant, takes the constant
    /// part and whatever the gyro cannot give. Without the RCS the gyro does everything and control is lost honestly when it saturates.
    /// </para>
    /// Pure and allocation-free; the only state is in <see cref="FlightState"/>.
    /// </summary>
    public static class SasController
    {
        /// <summary>Angle in radians between the current orientation and the target (0 when the SAS is off).</summary>
        public static double ErrorAngle(in FlightState s)
        {
            if (s.SasOn == 0) return 0.0;
            return Vec3.Length(ErrorVector(s));
        }

        /// <summary>Rotation vector (body frame) that takes the current orientation to the target.</summary>
        public static Vec3 ErrorVector(in FlightState s)
        {
            DQuat err = DQuat.Multiply(DQuat.Conjugate(DQuat.Normalize(s.Orientation)), DQuat.Normalize(s.SasTarget));
            return DQuat.ToRotationVector(err);
        }

        /// <summary>
        /// One control step. Updates the SAS fields of the state and returns the commands. <paramref name="rcsCap"/> is the part of the RCS
        /// (per axis, 0..1) that the unloading of the gyro leaves free.
        /// </summary>
        public static void Step(ref FlightState s, in ControlInput input, in FlightConfig c, Vec3 inertia, bool rcsAvailable, Vec3 rcsCap, double dt, out SasOutput o)
        {
            o = default;
            if (!input.SasOn)
            {
                s.SasOn = 0;
                s.SasPhase = SasPhase.Hold;
                s.SasIntegral = Vec3.Zero;
                s.SasTarget = s.Orientation;
                return;
            }
            if (s.SasOn == 0)
            {
                s.SasOn = 1;
                s.SasPhase = SasPhase.Settle;
                s.SasIntegral = Vec3.Zero;
                s.SasTarget = s.Orientation;
            }
            if (s.Mode != FlightMode.Flying)
            {
                // standing on the ground (or destroyed): the ground holds the craft, the target follows, the estimate starts again at lift-off
                s.SasPhase = SasPhase.Hold;
                s.SasIntegral = Vec3.Zero;
                s.SasTarget = s.Orientation;
                return;
            }
            SasConfig cfg = c.Sas;
            Vec3 axes = InputMapper.AxesVector(input);
            bool manual = axes.X != 0.0 || axes.Y != 0.0 || axes.Z != 0.0;
            if (manual)
            {
                s.SasPhase = SasPhase.Manual;
                s.SasTarget = s.Orientation;
            }
            else if (s.SasPhase == SasPhase.Manual) s.SasPhase = SasPhase.Settle;
            if (s.SasPhase == SasPhase.Settle && Vec3.Length(s.Omega) < cfg.CaptureRate)
            {
                s.SasPhase = SasPhase.Hold;
                s.SasTarget = s.Orientation;
            }
            if (!(cfg.NaturalFrequency > 0.0) || !(cfg.Damping > 0.0) || !(dt > 0.0)) return;

            double wn = cfg.NaturalFrequency, z = cfg.Damping;
            bool hold = s.SasPhase == SasPhase.Hold, settle = s.SasPhase == SasPhase.Settle;
            Vec3 theta = hold ? ErrorVector(s) : Vec3.Zero;
            if (settle) s.SasTarget = s.Orientation;

            double g = c.Gyro.MaxTorque;
            Vec3 gyroMax = new Vec3(g, g, g);
            Vec3 rcsMax = rcsAvailable ? c.Rcs.MaxTorque : Vec3.Zero;
            Vec3 authority = gyroMax + rcsMax;
            Vec3 limit = (rcsAvailable ? c.Rcs.MaxTorque : gyroMax) * cfg.IntegralLimit;
            Vec3 w = s.Omega;
            Vec3 integral = s.SasIntegral;
            double ki = cfg.IntegralTime > 0.0 ? wn * wn / cfg.IntegralTime : 0.0;
            double kRate = cfg.IntegralTime > 0.0 ? 2.0 * z * wn / cfg.IntegralTime : 0.0;
            Vec3 integralNew = new Vec3(
                Axis(cfg, hold, settle, theta.X, w.X, inertia.X, authority.X, limit.X, integral.X, ki, kRate, dt, out double pdX),
                Axis(cfg, hold, settle, theta.Y, w.Y, inertia.Y, authority.Y, limit.Y, integral.Y, ki, kRate, dt, out double pdY),
                Axis(cfg, hold, settle, theta.Z, w.Z, inertia.Z, authority.Z, limit.Z, integral.Z, ki, kRate, dt, out double pdZ));
            Vec3 pd = new Vec3(pdX, pdY, pdZ);
            s.SasIntegral = integralNew;

            if (manual)
            {
                // the pilot flies; the constant part is still cancelled by the RCS so that a short pulse is not eaten by the disturbance
                if (rcsAvailable) o.RcsCommand = RcsCommand(integralNew, Vec3.Zero, c.Rcs.MaxTorque, rcsCap);
                return;
            }
            Vec3 total = pd + integralNew;
            if (rcsAvailable)
            {
                o.GyroCommand = Command(pd, gyroMax);
                Vec3 gyroActual = GyroModel.PilotHdot(c.Gyro, s.GyroH, o.GyroCommand) * -1.0; // the craft gets minus dH/dt
                o.RcsCommand = RcsCommand(total, gyroActual, c.Rcs.MaxTorque, rcsCap);
            }
            else
            {
                o.GyroCommand = Command(total, gyroMax);
            }
        }

        // One body axis: the PD torque and the new integral (no growth into saturation; limited to what the actuators can give).
        private static double Axis(in SasConfig cfg, bool hold, bool settle, double theta, double w, double inertia, double authority, double limit,
            double integral, double ki, double kRate, double dt, out double pd)
        {
            double wn = cfg.NaturalFrequency, z = cfg.Damping;
            double wDesired = 0.0;
            if (hold)
            {
                double lin = wn / (2.0 * z) * theta;
                double aMax = inertia > 0.0 ? authority / inertia : 0.0;
                double brake = Math.Sqrt(2.0 * cfg.AuthorityMargin * aMax * Math.Abs(theta));
                double lim = Math.Min(cfg.MaxRate, brake);
                wDesired = lin > lim ? lim : (lin < -lim ? -lim : lin);
            }
            pd = inertia * 2.0 * z * wn * (wDesired - w);
            double total = pd + integral;
            double delta = 0.0;
            if (hold) delta = inertia * ki * theta * dt;
            else if (settle) delta = -inertia * kRate * w * dt;
            if (Math.Abs(total) > authority && delta * total > 0.0) delta = 0.0;
            double result = integral + delta;
            if (result > limit) result = limit;
            else if (result < -limit) result = -limit;
            return result;
        }

        private static Vec3 Command(Vec3 torque, Vec3 max) => new Vec3(Ratio(torque.X, max.X, 1.0), Ratio(torque.Y, max.Y, 1.0), Ratio(torque.Z, max.Z, 1.0));

        private static Vec3 RcsCommand(Vec3 wanted, Vec3 givenByGyro, Vec3 rcsMax, Vec3 cap) => new Vec3(
            Ratio(wanted.X - givenByGyro.X, rcsMax.X, cap.X), Ratio(wanted.Y - givenByGyro.Y, rcsMax.Y, cap.Y), Ratio(wanted.Z - givenByGyro.Z, rcsMax.Z, cap.Z));

        private static double Ratio(double torque, double max, double cap)
        {
            if (!(max > 0.0)) return 0.0;
            double r = torque / max;
            return r > cap ? cap : (r < -cap ? -cap : r);
        }
    }
}
