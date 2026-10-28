using System;

namespace Orbital.Core.Flight
{
    /// <summary>Spin of a body as plain numbers (the fields of the RotationFacet), so this file does not need the engine.</summary>
    public struct RotationParams
    {
        public double PeriodSeconds;      // sidereal period; 0 = does not spin
        public double ObliquityDegrees;   // tilt of the axis from the orbit normal (above 90 = retrograde)
        public double AxisAzimuthDegrees; // direction of the tilt around the orbit normal, from the periapsis direction
        public double InitialAngleDegrees;
        public bool TidallyLocked;
    }

    /// <summary>The frame of the body's orbit around its parent, as the orbit rotation matrix gives it (Forward = periapsis, Up = normal).</summary>
    public struct OrbitFrame
    {
        public bool HasParent;            // false for the root body
        public Vec3 Normal;
        public Vec3 Forward;
        public Vec3 Right;
        public double MeanMotion;         // rad/s
        public Vec3 PositionAtTimeZero;   // relative to the parent
    }

    /// <summary>
    /// Builds the <see cref="SurfaceData"/> of a body from its radius, spin and orbit. The same rules as the server world (Body.InitRotation),
    /// so a craft stands on the same point of the ground on both sides.
    /// </summary>
    public static class SurfaceBuilder
    {
        public static SurfaceData Build(double radius, in RotationParams rotation, in OrbitFrame orbit)
        {
            SurfaceData s = default;
            s.Kind = SurfaceKind.Sphere;
            s.Radius = radius;
            if (rotation.PeriodSeconds == 0 && !rotation.TidallyLocked)
            {
                // non-rotating default: axis Y, meridian X
                s.Axis = Vec3.UnitY;
                s.EquatorX = Vec3.UnitX;
                s.EquatorY = Vec3.Cross(s.Axis, s.EquatorX);
                return s;
            }
            Vec3 n = Vec3.UnitY, f = Vec3.UnitX, r;
            if (orbit.HasParent)
            {
                n = orbit.Normal;
                f = orbit.Forward;
                r = orbit.Right;
            }
            else r = Vec3.Cross(n, f);

            double eps = rotation.ObliquityDegrees * Math.PI / 180.0;
            double az = rotation.AxisAzimuthDegrees * Math.PI / 180.0;
            s.Axis = Vec3.Normalize(n * Math.Cos(eps) + (f * Math.Cos(az) + r * Math.Sin(az)) * Math.Sin(eps));
            bool locked = rotation.TidallyLocked && orbit.HasParent;
            s.Omega = locked ? orbit.MeanMotion : 2.0 * Math.PI / rotation.PeriodSeconds;
            // prime meridian reference: the periapsis direction projected on the equator plane
            Vec3 g = f - s.Axis * Vec3.Dot(f, s.Axis);
            if (Vec3.Length(g) < 1e-6) g = r - s.Axis * Vec3.Dot(r, s.Axis);
            s.EquatorX = Vec3.Normalize(g);
            s.EquatorY = Vec3.Cross(s.Axis, s.EquatorX);
            s.Phase0 = rotation.InitialAngleDegrees * Math.PI / 180.0;
            if (locked)
            {
                Vec3 toParent = -orbit.PositionAtTimeZero;
                s.Phase0 = Math.Atan2(Vec3.Dot(toParent, s.EquatorY), Vec3.Dot(toParent, s.EquatorX));
            }
            return s;
        }
    }
}
