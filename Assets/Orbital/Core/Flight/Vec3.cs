using System;

namespace Orbital.Core.Flight
{
    /// <summary>
    /// Double precision 3-vector for the flight core. Blittable, no references, so the same data can go to Jobs/Burst or a GPU buffer.
    /// All math is static and pure.
    /// </summary>
    public struct Vec3
    {
        public double X, Y, Z;

        public Vec3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Vec3 Zero => new Vec3(0, 0, 0);
        public static Vec3 UnitX => new Vec3(1, 0, 0);
        public static Vec3 UnitY => new Vec3(0, 1, 0);
        public static Vec3 UnitZ => new Vec3(0, 0, 1);

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator -(Vec3 a) => new Vec3(-a.X, -a.Y, -a.Z);
        public static Vec3 operator *(Vec3 a, double s) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 operator *(double s, Vec3 a) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 operator /(Vec3 a, double s) => new Vec3(a.X / s, a.Y / s, a.Z / s);

        public static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Vec3 Cross(Vec3 a, Vec3 b) =>
            new Vec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        public static double LengthSquared(Vec3 a) => a.X * a.X + a.Y * a.Y + a.Z * a.Z;
        public static double Length(Vec3 a) => Math.Sqrt(a.X * a.X + a.Y * a.Y + a.Z * a.Z);

        /// <summary>Unit vector, or zero for a zero vector.</summary>
        public static Vec3 Normalize(Vec3 a)
        {
            double l = Length(a);
            return l > 0 ? a / l : new Vec3(0, 0, 0);
        }

        public static Vec3 Scale(Vec3 a, Vec3 b) => new Vec3(a.X * b.X, a.Y * b.Y, a.Z * b.Z);

        /// <summary>Angle between two vectors, radians.</summary>
        public static double Angle(Vec3 a, Vec3 b)
        {
            Vec3 c = Cross(a, b);
            return Math.Atan2(Length(c), Dot(a, b));
        }
    }
}
