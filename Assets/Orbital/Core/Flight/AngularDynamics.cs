using System;

namespace Orbital.Core.Flight
{
    /// <summary>Attitude of the craft: orientation (body to inertial) and angular velocity in the body frame.</summary>
    public struct AngularState
    {
        public DQuat Orientation;
        public Vec3 Omega;
    }

    /// <summary>
    /// Rigid body rotation with a diagonal inertia tensor and an internal momentum store (the gyro).
    /// Euler equations: I dw/dt = torque - dH/dt - w x (I w + H), dq/dt = q * (0, w) / 2. Integrated with RK4, then normalized.
    /// </summary>
    public static class RigidBodyAngular
    {
        public static Vec3 AngularMomentum(Vec3 inertia, Vec3 omega) => Vec3.Scale(inertia, omega);

        /// <summary>Total angular momentum of craft plus gyro in the inertial frame; conserved without external torque.</summary>
        public static Vec3 TotalMomentumInertial(in AngularState s, Vec3 inertia, Vec3 gyroH)
        {
            return DQuat.Rotate(s.Orientation, Vec3.Scale(inertia, s.Omega) + gyroH);
        }

        public static double RotationalEnergy(Vec3 inertia, Vec3 omega) =>
            0.5 * (inertia.X * omega.X * omega.X + inertia.Y * omega.Y * omega.Y + inertia.Z * omega.Z * omega.Z);

        private static void Derivative(Vec3 inertia, DQuat q, Vec3 w, Vec3 torque, Vec3 h, Vec3 hDot, out DQuat dq, out Vec3 dw)
        {
            Vec3 iw = Vec3.Scale(inertia, w);
            Vec3 rhs = torque - hDot - Vec3.Cross(w, iw + h);
            dw = new Vec3(rhs.X / inertia.X, rhs.Y / inertia.Y, rhs.Z / inertia.Z);
            DQuat wq = new DQuat(0, w.X, w.Y, w.Z);
            DQuat p = DQuat.Multiply(q, wq);
            dq = new DQuat(0.5 * p.W, 0.5 * p.X, 0.5 * p.Y, 0.5 * p.Z);
        }

        private static DQuat Add(DQuat q, DQuat d, double s) => new DQuat(q.W + d.W * s, q.X + d.X * s, q.Y + d.Y * s, q.Z + d.Z * s);

        /// <summary>
        /// One step. torque: external torque on the craft (body frame); gyroH: stored momentum at the start; gyroHdot: its constant rate
        /// of change over the step (the torque the gyro exerts on the craft is -gyroHdot).
        /// </summary>
        public static void Step(ref AngularState s, Vec3 inertia, Vec3 torque, Vec3 gyroH, Vec3 gyroHdot, double dt)
        {
            DQuat q = s.Orientation;
            Vec3 w = s.Omega;
            Derivative(inertia, q, w, torque, gyroH, gyroHdot, out DQuat k1q, out Vec3 k1w);
            Derivative(inertia, Add(q, k1q, dt / 2), w + k1w * (dt / 2), torque, gyroH + gyroHdot * (dt / 2), gyroHdot, out DQuat k2q, out Vec3 k2w);
            Derivative(inertia, Add(q, k2q, dt / 2), w + k2w * (dt / 2), torque, gyroH + gyroHdot * (dt / 2), gyroHdot, out DQuat k3q, out Vec3 k3w);
            Derivative(inertia, Add(q, k3q, dt), w + k3w * dt, torque, gyroH + gyroHdot * dt, gyroHdot, out DQuat k4q, out Vec3 k4w);
            DQuat nq = new DQuat(
                q.W + dt / 6 * (k1q.W + 2 * k2q.W + 2 * k3q.W + k4q.W),
                q.X + dt / 6 * (k1q.X + 2 * k2q.X + 2 * k3q.X + k4q.X),
                q.Y + dt / 6 * (k1q.Y + 2 * k2q.Y + 2 * k3q.Y + k4q.Y),
                q.Z + dt / 6 * (k1q.Z + 2 * k2q.Z + 2 * k3q.Z + k4q.Z));
            s.Orientation = DQuat.Normalize(nq);
            s.Omega = w + (k1w + k2w * 2 + k3w * 2 + k4w) * (dt / 6);
        }
    }

    /// <summary>Result of an RCS step.</summary>
    public struct RcsOutput
    {
        public Vec3 Torque;        // N*m on the craft, body frame
        public double Propellant;  // kg burned during the step
    }

    /// <summary>Six thruster groups: plus and minus about each body axis.</summary>
    public static class RcsModel
    {
        /// <summary>Torque for a command in [-1, 1] per axis, limited by the remaining propellant. Returns the propellant burned.</summary>
        public static RcsOutput Fire(in RcsConfig cfg, Vec3 command, double propellantLeft, double dt)
        {
            Vec3 c = new Vec3(Clamp(command.X), Clamp(command.Y), Clamp(command.Z));
            Vec3 torque = Vec3.Scale(cfg.MaxTorque, c);
            RcsOutput o;
            o.Propellant = 0;
            o.Torque = torque;
            if (dt <= 0) return o;
            double force = (Math.Abs(torque.X) + Math.Abs(torque.Y) + Math.Abs(torque.Z)) / cfg.Arm;
            double wanted = force / (cfg.Isp * EngineModel.G0) * dt;
            if (wanted <= 0) return o;
            if (wanted > propellantLeft)
            {
                double f = propellantLeft > 0 ? propellantLeft / wanted : 0.0;
                o.Torque = torque * f;
                wanted = propellantLeft > 0 ? propellantLeft : 0.0;
            }
            o.Propellant = wanted;
            return o;
        }

        private static double Clamp(double v) => v < -1 ? -1 : (v > 1 ? 1 : v);
    }

    /// <summary>
    /// Gyro: exerts a torque -dH/dt on the craft by changing its stored momentum H. Always on. The torque is limited per axis and fades
    /// out as an axis fills up. Saturation is relieved with the RCS: the RCS torque goes against H while the gyro takes the same torque
    /// into its store, so the craft feels nothing and H drops.
    /// </summary>
    public static class GyroModel
    {
        /// <summary>Rate of change of H for a pilot command in [-1, 1] per axis, with saturation. The craft receives the negative of it.</summary>
        public static Vec3 PilotHdot(in GyroConfig cfg, Vec3 h, Vec3 command)
        {
            return new Vec3(AxisHdot(cfg, h.X, command.X), AxisHdot(cfg, h.Y, command.Y), AxisHdot(cfg, h.Z, command.Z));
        }

        private static double AxisHdot(in GyroConfig cfg, double h, double command)
        {
            double c = command < -1 ? -1 : (command > 1 ? 1 : command);
            double hdot = -c * cfg.MaxTorque; // the craft gets +c * MaxTorque
            if (hdot * h > 0) // grows |H|: fade out near the capacity
            {
                double room = (cfg.MomentumCapacity - Math.Abs(h)) / (cfg.SoftZone * cfg.MomentumCapacity);
                double scale = room < 0 ? 0 : (room > 1 ? 1 : room);
                hdot *= scale;
            }
            return hdot;
        }

        /// <summary>Largest |H| component relative to the capacity.</summary>
        public static double Saturation(in GyroConfig cfg, Vec3 h)
        {
            double m = Math.Max(Math.Abs(h.X), Math.Max(Math.Abs(h.Y), Math.Abs(h.Z)));
            return m / cfg.MomentumCapacity;
        }

        /// <summary>Hysteresis of the unloading mode.</summary>
        public static bool UpdateDumping(in GyroConfig cfg, Vec3 h, bool dumping)
        {
            double s = Saturation(cfg, h);
            return dumping ? s > cfg.DumpStop : s > cfg.DumpStart;
        }

        /// <summary>RCS command (fraction of the RCS maximum per axis) that unloads H: against H on the axes that are above DumpStop.</summary>
        public static Vec3 DumpCommand(in GyroConfig cfg, in RcsConfig rcs, Vec3 h)
        {
            return new Vec3(DumpAxis(cfg, rcs.MaxTorque.X, h.X), DumpAxis(cfg, rcs.MaxTorque.Y, h.Y), DumpAxis(cfg, rcs.MaxTorque.Z, h.Z));
        }

        private static double DumpAxis(in GyroConfig cfg, double maxTorque, double h)
        {
            if (Math.Abs(h) <= cfg.DumpStop * cfg.MomentumCapacity || maxTorque <= 0) return 0.0;
            return -(h > 0 ? 1.0 : -1.0) * cfg.DumpTorqueFraction;
        }

        /// <summary>Clamps H into the physical capacity.</summary>
        public static Vec3 ClampStore(in GyroConfig cfg, Vec3 h)
        {
            double c = cfg.MomentumCapacity;
            return new Vec3(Math.Max(-c, Math.Min(c, h.X)), Math.Max(-c, Math.Min(c, h.Y)), Math.Max(-c, Math.Min(c, h.Z)));
        }
    }
}
