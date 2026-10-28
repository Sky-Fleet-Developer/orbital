using System;

namespace Orbital.Core.Flight
{
    /// <summary>
    /// Initial states of a craft for tests and start points: standing or hovering over a ground point, or on a circular orbit.
    /// Pure functions of their arguments, same frame as <see cref="FlightKernel"/> (parent-centered inertial).
    /// </summary>
    public static class SpawnBuilder
    {
        /// <summary>
        /// A craft over body-fixed <paramref name="latitude"/>/<paramref name="longitude"/> (radians), nose up. Altitude 0 (or less) means
        /// standing on the ground (Landed), above 0 it starts in Flying with the velocity of the ground below it, so it falls.
        /// </summary>
        public static FlightState OnSurface(in FlightConfig craft, in SurfaceData surface, double latitude, double longitude, double altitude, double time)
        {
            FlightState s = FlightKernel.Fresh(craft);
            Vec3 ground = Surface.Point(surface, latitude, longitude, time);
            Vec3 normal = Vec3.Normalize(ground);
            s.Latitude = latitude;
            s.Longitude = longitude;
            s.Orientation = DQuat.FromTo(Vec3.UnitY, normal);
            if (altitude <= 0)
            {
                s.Mode = FlightMode.Landed;
                s.Position = ground;
            }
            else
            {
                s.Mode = FlightMode.Flying;
                s.Position = ground + normal * altitude;
            }
            s.Velocity = Surface.GroundVelocity(surface, s.Position);
            return s;
        }

        /// <summary>
        /// A circular orbit at <paramref name="altitude"/> above the ground, at time 0 of the surface rotation. The plane contains the equator
        /// reference direction (the ascending node) and is tilted from the equator by <paramref name="inclination"/>; <paramref name="phase"/> is
        /// the angle from the node along the orbit. The nose points along the velocity.
        /// </summary>
        public static FlightState CircularOrbit(in FlightConfig craft, in SurfaceData surface, double nu, double altitude, double inclination, double phase)
        {
            FlightState s = FlightKernel.Fresh(craft);
            Vec3 node = surface.EquatorX;
            Vec3 normal = surface.Axis * Math.Cos(inclination) + Vec3.Cross(node, surface.Axis) * Math.Sin(inclination);
            Vec3 radial = node * Math.Cos(phase) + Vec3.Cross(normal, node) * Math.Sin(phase);
            Vec3 along = Vec3.Cross(normal, radial);
            double r = surface.Radius + altitude;
            s.Mode = FlightMode.Flying;
            s.Position = radial * r;
            s.Velocity = along * Math.Sqrt(nu / r);
            s.Orientation = DQuat.FromTo(Vec3.UnitY, along);
            Surface.ToBodyCoordinates(surface, s.Position, 0.0, out s.Latitude, out s.Longitude);
            return s;
        }
    }

    /// <summary>
    /// Stand-in for the legs until the kernel gets a tip-over model: a craft in Landed mode is held upright (the nose is turned onto the local
    /// vertical, pitch and yaw rates are removed; roll about the long axis stays). Without it the standing craft is an inverted pendulum
    /// that falls over within seconds (see Docs/GameplayPrototype.md, stage 2b). Client only, the server does not call it.
    /// </summary>
    public static class LandedSupport
    {
        public static void HoldUpright(ref FlightState s, in SurfaceData surface)
        {
            if (s.Mode != FlightMode.Landed) return;
            Vec3 normal = Surface.Normal(surface, s.Position);
            Vec3 nose = DQuat.Rotate(s.Orientation, Vec3.UnitY);
            DQuat fix = DQuat.FromTo(nose, normal);
            s.Orientation = DQuat.Normalize(DQuat.Multiply(fix, s.Orientation));
            s.Omega = new Vec3(0.0, s.Omega.Y, 0.0);
        }
    }
}
