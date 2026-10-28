using System;

namespace Orbital.Core.Flight
{
    /// <summary>
    /// Everything the navigation ball shows, in the local horizon frame of the craft: components are (East, North, Up). The ball itself is fixed
    /// to this frame; the craft is the fixed mark in the middle of the picture.
    /// <para>
    /// Cockpit convention (the keys: W pitches the nose toward +Z of the body, E turns it toward -X, D rolls about +Y): forward = body +Y (the nose),
    /// the pilot's top = body -Z, the pilot's right = body -X. So W is nose down, E is yaw right, D is roll right.
    /// </para>
    /// Picture: <see cref="ToView"/> maps a direction to (x right, y up, z toward the viewer is negative): the nose is at (0, 0, -1), a direction to
    /// the pilot's right is at positive x, the hemisphere in front of the craft faces the viewer (z &lt; 0).
    /// </summary>
    public struct NavBallData
    {
        public Vec3 Right, Up, Forward;     // cockpit axes in (East, North, Up) coordinates, unit, orthogonal
        public Vec3 Prograde;               // unit direction of the velocity in (East, North, Up); valid when HasVelocity
        public bool HasVelocity;
        public Vec3 SasTargetNose;          // unit direction of the nose at the SAS target; valid when HasSasTarget
        public bool HasSasTarget;
        public double Speed;                // m/s, the speed the markers are made from (relative to the ground or inertial)
        public bool OrbitMode;              // true: inertial (orbital) velocity, false: velocity relative to the ground
        public double HeadingDegrees;       // of the nose, 0 north, 90 east, [0, 360)
        public double PitchDegrees;         // of the nose above the horizon, [-90, 90]
        public double RollDegrees;          // about the nose, positive = right wing down, (-180, 180]

        public Vec3 Retrograde => Prograde * -1.0;
    }

    public static class NavBall
    {
        /// <summary>Speed below which the velocity markers are not shown (m/s): the direction of a tiny velocity is noise.</summary>
        public const double MinMarkerSpeed = 0.05;

        /// <summary>Local horizon of a position: unit East, North and Up in the inertial frame. Defined also at the poles and at the center.</summary>
        public static void Horizon(in SurfaceData surface, Vec3 position, out Vec3 east, out Vec3 north, out Vec3 up)
        {
            up = Vec3.Normalize(position);
            if (Vec3.LengthSquared(up) == 0.0) up = Vec3.Normalize(surface.Axis);
            if (Vec3.LengthSquared(up) == 0.0) up = Vec3.UnitY;
            east = Vec3.Cross(surface.Axis, up);
            if (Vec3.LengthSquared(east) < 1e-18)
            {
                // on the rotation axis (a pole) every horizontal direction is as good as another: take a fixed one
                east = Vec3.Cross(surface.EquatorX, up);
                if (Vec3.LengthSquared(east) < 1e-18) east = Vec3.Cross(Vec3.UnitX, up);
                if (Vec3.LengthSquared(east) < 1e-18) east = Vec3.Cross(Vec3.UnitZ, up);
            }
            east = Vec3.Normalize(east);
            north = Vec3.Cross(up, east);
        }

        /// <param name="orbitMode">false: velocity relative to the rotating ground (surface mode); true: inertial velocity (orbit mode).</param>
        public static NavBallData Compute(in FlightState s, in SurfaceData surface, bool orbitMode)
        {
            NavBallData d = default;
            Horizon(surface, s.Position, out Vec3 east, out Vec3 north, out Vec3 up);
            DQuat q = DQuat.Normalize(s.Orientation);
            Vec3 forward = DQuat.Rotate(q, Vec3.UnitY);
            Vec3 top = DQuat.Rotate(q, new Vec3(0, 0, -1));
            Vec3 right = DQuat.Rotate(q, new Vec3(-1, 0, 0));
            d.Forward = ToHorizon(forward, east, north, up);
            d.Up = ToHorizon(top, east, north, up);
            d.Right = ToHorizon(right, east, north, up);

            Vec3 velocity = orbitMode ? s.Velocity : s.Velocity - Surface.GroundVelocity(surface, s.Position);
            d.OrbitMode = orbitMode;
            d.Speed = Vec3.Length(velocity);
            if (d.Speed > MinMarkerSpeed && !double.IsNaN(d.Speed) && !double.IsInfinity(d.Speed))
            {
                d.HasVelocity = true;
                d.Prograde = ToHorizon(velocity / d.Speed, east, north, up);
            }
            if (s.SasOn != 0)
            {
                d.HasSasTarget = true;
                d.SasTargetNose = ToHorizon(DQuat.Rotate(DQuat.Normalize(s.SasTarget), Vec3.UnitY), east, north, up);
            }

            // angles of the nose: the horizon components of the forward vector
            const double radToDeg = 180.0 / Math.PI;
            double sinPitch = Clamp(d.Forward.Z);
            d.PitchDegrees = Math.Asin(sinPitch) * radToDeg;
            double heading = Math.Atan2(d.Forward.X, d.Forward.Y) * radToDeg;
            if (heading < 0) heading += 360.0;
            if (heading >= 360.0) heading -= 360.0;
            d.HeadingDegrees = heading;

            // roll: the angle of the pilot's top from the vertical (from north when the nose is vertical), about the nose
            Vec3 f = d.Forward;
            Vec3 refUp = new Vec3(0, 0, 1) - f * f.Z;
            if (Vec3.LengthSquared(refUp) < 1e-12) refUp = new Vec3(0, 1, 0) - f * f.Y;
            refUp = Vec3.Normalize(refUp);
            Vec3 refRight = Vec3.Cross(f, refUp);
            d.RollDegrees = Math.Atan2(Vec3.Dot(d.Up, refRight), Vec3.Dot(d.Up, refUp)) * radToDeg;
            return d;
        }

        /// <summary>The picture coordinates of a direction given in (East, North, Up): x right, y up, z negative on the hemisphere facing the viewer.</summary>
        public static Vec3 ToView(in NavBallData d, Vec3 direction) =>
            new Vec3(Vec3.Dot(direction, d.Right), Vec3.Dot(direction, d.Up), -Vec3.Dot(direction, d.Forward));

        /// <summary>The direction (East, North, Up) that the picture point (x, y) on the front hemisphere shows; the inverse of <see cref="ToView"/>.</summary>
        public static Vec3 FromView(in NavBallData d, double x, double y)
        {
            double zf = 1.0 - x * x - y * y;
            zf = zf > 0.0 ? Math.Sqrt(zf) : 0.0;
            return d.Right * x + d.Up * y + d.Forward * zf;
        }

        private static Vec3 ToHorizon(Vec3 v, Vec3 east, Vec3 north, Vec3 up) => new Vec3(Vec3.Dot(v, east), Vec3.Dot(v, north), Vec3.Dot(v, up));

        private static double Clamp(double v) => v < -1.0 ? -1.0 : (v > 1.0 ? 1.0 : v);
    }

    /// <summary>
    /// Draws the ball into an RGBA byte buffer (size x size, row 0 at the bottom, like a Unity texture): sky above and ground below the horizon, a grid
    /// every 10 degrees, heavier lines on the horizon, the meridians of the four directions and the 90 degree parallels. Outside the circle the alpha is 0.
    /// Pure arithmetic over arrays (no engine types), so it is covered by tests; the client uploads the buffer to a texture.
    /// </summary>
    public static class NavBallRaster
    {
        public const double GridStepDegrees = 10.0;

        // colors (r, g, b)
        private static readonly byte[] Sky = { 56, 112, 214 };
        private static readonly byte[] Ground = { 140, 92, 44 };
        private static readonly byte[] Line = { 255, 255, 255 };
        private static readonly byte[] Horizon = { 255, 255, 255 };
        private static readonly byte[] North = { 255, 90, 70 };

        public static int BufferLength(int size) => size * size * 4;

        public static void Render(in NavBallData d, int size, byte[] rgba)
        {
            if (size <= 0 || rgba == null || rgba.Length < BufferLength(size)) throw new ArgumentException("buffer too small", nameof(rgba));
            double half = size * 0.5;
            const double deg = Math.PI / 180.0;
            double step = GridStepDegrees * deg;
            for (int py = 0; py < size; py++)
            {
                double y = (py + 0.5 - half) / half;
                for (int px = 0; px < size; px++)
                {
                    double x = (px + 0.5 - half) / half;
                    int o = (py * size + px) * 4;
                    double r2 = x * x + y * y;
                    if (r2 > 1.0)
                    {
                        rgba[o] = rgba[o + 1] = rgba[o + 2] = rgba[o + 3] = 0;
                        continue;
                    }
                    Vec3 dir = NavBall.FromView(d, x, y);
                    double sinEl = dir.Z < -1.0 ? -1.0 : (dir.Z > 1.0 ? 1.0 : dir.Z);
                    double el = Math.Asin(sinEl);
                    double cosEl = Math.Sqrt(Math.Max(0.0, 1.0 - sinEl * sinEl));
                    double az = Math.Atan2(dir.X, dir.Y);
                    double zf = Math.Sqrt(Math.Max(0.0, 1.0 - r2));
                    // angular size of one pixel here (the sphere is stretched toward its rim), at least a little to keep the lines visible
                    double pixelAngle = 1.0 / (half * Math.Max(zf, 0.25));

                    byte[] baseColor = sinEl >= 0.0 ? Sky : Ground;
                    double rr = baseColor[0], gg = baseColor[1], bb = baseColor[2];

                    // parallels: every 10 degrees; the horizon heavier
                    double dEl = Math.Abs(el - Math.Round(el / step) * step);
                    double wParallel = (Math.Abs(el) < step * 0.5 ? 2.0 : 0.9) * pixelAngle;
                    double aParallel = Coverage(dEl, wParallel);
                    // meridians: every 10 degrees of azimuth, scaled by cos(elevation); the four directions heavier
                    double azDeg = az / deg;
                    double nearest = Math.Round(azDeg / GridStepDegrees) * GridStepDegrees;
                    double dAz = Math.Abs(azDeg - nearest) * deg * cosEl;
                    bool cardinal = Math.Abs(nearest % 90.0) < 1e-6;
                    double wMeridian = (cardinal ? 1.8 : 0.9) * pixelAngle;
                    double aMeridian = cosEl < 0.02 ? 0.0 : Coverage(dAz, wMeridian);
                    // the lines die out toward the poles of the ball where the meridians crowd together
                    aMeridian *= Math.Min(1.0, cosEl * 3.0);

                    double a = Math.Max(aParallel * (Math.Abs(el) < step * 0.5 ? 1.0 : 0.7), aMeridian * (cardinal ? 0.9 : 0.55));
                    byte[] lineColor = Line;
                    if (aParallel >= aMeridian && Math.Abs(el) < step * 0.5) lineColor = Horizon;
                    else if (aMeridian > aParallel && cardinal && Math.Abs(nearest) < 1e-6 && dir.Y > 0) lineColor = North;
                    rr += (lineColor[0] - rr) * a;
                    gg += (lineColor[1] - gg) * a;
                    bb += (lineColor[2] - bb) * a;

                    // soft edge of the disc
                    double edge = Math.Min(1.0, (1.0 - Math.Sqrt(r2)) * half);
                    rgba[o] = (byte) Math.Round(rr);
                    rgba[o + 1] = (byte) Math.Round(gg);
                    rgba[o + 2] = (byte) Math.Round(bb);
                    rgba[o + 3] = (byte) Math.Round(255.0 * Math.Max(0.0, edge));
                }
            }
        }

        // 1 inside the line, falling to 0 over one more half width
        private static double Coverage(double distance, double width)
        {
            double t = 1.0 - (distance - width * 0.5) / Math.Max(width * 0.5, 1e-12);
            return t < 0.0 ? 0.0 : (t > 1.0 ? 1.0 : t);
        }
    }
}
