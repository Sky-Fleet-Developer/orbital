using System;

namespace Orbital.Core.Flight
{
    /// <summary>
    /// Kind of ground. Chosen explicitly (a tag in blittable data and a switch in the static functions) instead of an interface:
    /// no virtual calls, no boxing, nothing a Burst job or a GPU kernel cannot take. A height map is added later as one more kind
    /// whose samples live in a separate array that is passed next to the SurfaceData.
    /// </summary>
    public enum SurfaceKind
    {
        Sphere = 0,
    }

    /// <summary>
    /// The ground of one body in the parent-centered inertial frame. For a sphere: radius and the rotation of the planet
    /// (axis, rate, phase, and the two equator reference directions). Same numbers as the server world uses
    /// (Body.Axis, EquatorX, EquatorY, Omega, Phase0).
    /// </summary>
    public struct SurfaceData
    {
        public SurfaceKind Kind;
        public double Radius;
        public Vec3 Axis;
        public Vec3 EquatorX;
        public Vec3 EquatorY;
        public double Omega;   // rad/s about Axis
        public double Phase0;  // rotation angle at time 0
    }

    /// <summary>Contact queries. Every function takes the SurfaceData by reference and has no side effects.</summary>
    public static class Surface
    {
        /// <summary>Rotation angle of the planet at time t, radians (reduced modulo 2 pi like the server).</summary>
        public static double Angle(in SurfaceData s, double time) => s.Phase0 + (s.Omega * time) % (2.0 * Math.PI);

        /// <summary>Height above the ground of the point (inertial frame).</summary>
        public static double Altitude(in SurfaceData s, Vec3 pos)
        {
            switch (s.Kind)
            {
                default:
                    return Vec3.Length(pos) - s.Radius;
            }
        }

        /// <summary>Unit normal of the ground below the point.</summary>
        public static Vec3 Normal(in SurfaceData s, Vec3 pos)
        {
            switch (s.Kind)
            {
                default:
                    return Vec3.Normalize(pos);
            }
        }

        /// <summary>Velocity of the ground below the point in the inertial frame: omega x r.</summary>
        public static Vec3 GroundVelocity(in SurfaceData s, Vec3 pos) => Vec3.Cross(s.Axis * s.Omega, pos);

        /// <summary>Position of a ground-fixed point (body-fixed latitude, longitude) in the inertial frame at time t.</summary>
        public static Vec3 Point(in SurfaceData s, double lat, double lon, double time)
        {
            double a = lon + Angle(s, time);
            double c = Math.Cos(lat);
            return (s.EquatorX * Math.Cos(a) + s.EquatorY * Math.Sin(a)) * (s.Radius * c) + s.Axis * (s.Radius * Math.Sin(lat));
        }

        /// <summary>Body-fixed latitude and longitude of the ground point below an inertial position.</summary>
        public static void ToBodyCoordinates(in SurfaceData s, Vec3 pos, double time, out double lat, out double lon)
        {
            double r = Vec3.Length(pos);
            double sinLat = Vec3.Dot(pos, s.Axis) / r;
            lat = Math.Asin(sinLat < -1 ? -1 : (sinLat > 1 ? 1 : sinLat));
            double a = Math.Atan2(Vec3.Dot(pos, s.EquatorY), Vec3.Dot(pos, s.EquatorX));
            lon = a - Angle(s, time);
            lon -= 2.0 * Math.PI * Math.Round(lon / (2.0 * Math.PI));
        }

        /// <summary>Speed relative to the rotating ground; the number a touchdown is judged by.</summary>
        public static double SpeedRelativeToGround(in SurfaceData s, Vec3 pos, Vec3 vel) => Vec3.Length(vel - GroundVelocity(s, pos));

        /// <summary>True when a touchdown at this position and velocity is soft (relative speed within the limit).</summary>
        public static bool IsSoftTouchdown(in SurfaceData s, Vec3 pos, Vec3 vel, double maxLandingSpeed) =>
            SpeedRelativeToGround(s, pos, vel) <= maxLandingSpeed;
    }
}
