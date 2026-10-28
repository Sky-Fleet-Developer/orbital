using System;

namespace Orbital.Core.Flight
{
    /// <summary>
    /// Double precision rotation quaternion (W, X, Y, Z). Rotates body-frame vectors into the inertial frame:
    /// world = Rotate(q, body). The engine float rotation type is not used so the core stays engine-free and accurate.
    /// </summary>
    public struct DQuat
    {
        public double W, X, Y, Z;

        public DQuat(double w, double x, double y, double z)
        {
            W = w;
            X = x;
            Y = y;
            Z = z;
        }

        public static DQuat Identity => new DQuat(1, 0, 0, 0);

        public static DQuat Multiply(DQuat a, DQuat b) => new DQuat(
            a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z,
            a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
            a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
            a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W);

        public static DQuat Conjugate(DQuat q) => new DQuat(q.W, -q.X, -q.Y, -q.Z);

        public static DQuat Normalize(DQuat q)
        {
            double n = Math.Sqrt(q.W * q.W + q.X * q.X + q.Y * q.Y + q.Z * q.Z);
            return n > 0 ? new DQuat(q.W / n, q.X / n, q.Y / n, q.Z / n) : Identity;
        }

        /// <summary>Body to inertial.</summary>
        public static Vec3 Rotate(DQuat q, Vec3 v)
        {
            Vec3 u = new Vec3(q.X, q.Y, q.Z);
            Vec3 t = Vec3.Cross(u, v) * 2.0;
            return v + t * q.W + Vec3.Cross(u, t);
        }

        /// <summary>Inertial to body.</summary>
        public static Vec3 RotateInverse(DQuat q, Vec3 v) => Rotate(Conjugate(q), v);

        /// <summary>Rotation by the vector rv: axis = rv / |rv|, angle = |rv| radians.</summary>
        public static DQuat FromRotationVector(Vec3 rv)
        {
            double a = Vec3.Length(rv);
            if (a < 1e-300) return Identity;
            double s = Math.Sin(a * 0.5) / a;
            return new DQuat(Math.Cos(a * 0.5), rv.X * s, rv.Y * s, rv.Z * s);
        }

        public static DQuat FromAxisAngle(Vec3 axis, double angle) => FromRotationVector(Vec3.Normalize(axis) * angle);

        /// <summary>Applies a body-frame rotation vector to the orientation: q * exp(rv).</summary>
        public static DQuat ApplyBodyRotation(DQuat q, Vec3 rv) => Normalize(Multiply(q, FromRotationVector(rv)));

        /// <summary>Shortest rotation taking unit vector a to unit vector b.</summary>
        public static DQuat FromTo(Vec3 a, Vec3 b)
        {
            Vec3 c = Vec3.Cross(a, b);
            double d = Vec3.Dot(a, b);
            if (d < -1 + 1e-15)
            {
                Vec3 perp = Math.Abs(a.X) < 0.9 ? Vec3.Cross(a, Vec3.UnitX) : Vec3.Cross(a, Vec3.UnitY);
                return FromAxisAngle(perp, Math.PI);
            }
            return Normalize(new DQuat(1 + d, c.X, c.Y, c.Z));
        }
    }
}
