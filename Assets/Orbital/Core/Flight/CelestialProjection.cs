using System;

namespace Orbital.Core.Flight
{
    /// <summary>
    /// A camera that a body is projected for (stage 3.5): the eye in the global frame, the horizontal field of view and the frame width in
    /// pixels. Engine-free: the flight camera and a telescope camera (any FOV and resolution) both fill this in.
    /// </summary>
    public struct ProjectionCamera
    {
        /// <summary>Global position of the eye, meters.</summary>
        public Vec3 Eye;
        /// <summary>Horizontal field of view, radians.</summary>
        public double HorizontalFovRad;
        /// <summary>Frame width, pixels.</summary>
        public int WidthPx;

        /// <summary>The angle covered by one pixel, radians: the horizontal field of view divided by the frame width.</summary>
        public double RadiansPerPixel => HorizontalFovRad / WidthPx;
    }

    /// <summary>How a body is drawn in scaled space: a disk of the real angular size, or just a point.</summary>
    public enum BodyDrawMode
    {
        Disk,
        Point
    }

    /// <summary>
    /// One body placed in scaled space (stage 3.5): the exact direction and angular radius of the real body from the eye, and the impostor's
    /// position and radius at the fixed scaled distance.
    /// </summary>
    public struct ProjectedBody
    {
        /// <summary>Unit direction from the eye to the center of the body.</summary>
        public Vec3 Direction;
        /// <summary>Distance from the eye to the center of the body, meters.</summary>
        public double Distance;
        /// <summary>Angular radius of the body as seen from the eye, radians; PI/2 when the eye is inside the body or on its surface.</summary>
        public double AngularRadius;
        /// <summary>Position of the impostor: <see cref="Direction"/> times the scaled distance.</summary>
        public Vec3 ScaledPosition;
        /// <summary>Radius of the impostor: the scaled distance times the body radius over the distance, so the angular size is exact.</summary>
        public double ScaledRadius;
        /// <summary>Point when the angular diameter is below the camera's minimum pixels, disk otherwise.</summary>
        public BodyDrawMode Mode;
        /// <summary>True when the eye is inside the body or on its surface (d &lt;= R).</summary>
        public bool Inside;
    }

    /// <summary>
    /// Engine-free projection of a world body into scaled space (stage 3.5). The impostor is placed at the scaled distance with the radius
    /// <c>L * R / d</c>, so its angular radius equals the real one exactly: <c>asin(r / L) == asin(R / d)</c>. The point/disk threshold is
    /// measured in the pixels of the given camera: the angular diameter in pixels is <c>2 * angularRadius / (FOV / width)</c>, and a body
    /// thinner than <paramref name="minPixels"/> is drawn as a point.
    /// </summary>
    public static class CelestialProjection
    {
        /// <summary>Default distance of the impostor from the eye, meters.</summary>
        public const double DefaultScaledDistance = 1e5;
        /// <summary>Default minimum angular diameter, pixels: below it a body is drawn as a point.</summary>
        public const double DefaultMinPixels = 2.0;

        /// <summary>
        /// Projects one body for the camera. Degenerate cases stay finite: a zero radius body is a point of zero radius; the eye inside the body
        /// (d &lt;= R, d = 0 included) is a disk of the scaled distance radius covering PI/2 radians with a fixed unit direction at the center.
        /// </summary>
        public static ProjectedBody Project(in ProjectionCamera cam, Vec3 bodyCenter, double bodyRadius, double scaledDistance, double minPixels)
        {
            Vec3 offset = bodyCenter - cam.Eye;
            double d = Vec3.Length(offset);
            Vec3 direction = d > 0.0 ? offset / d : Vec3.UnitY;

            ProjectedBody b = default;
            b.Direction = direction;
            b.Distance = d;

            if (bodyRadius <= 0.0)
            {
                // no surface to draw or to be inside: always a point of zero radius
                b.AngularRadius = 0.0;
                b.ScaledPosition = direction * scaledDistance;
                b.ScaledRadius = 0.0;
                b.Mode = BodyDrawMode.Point;
                return b;
            }

            if (d <= bodyRadius)
            {
                b.AngularRadius = Math.PI / 2.0;
                b.ScaledPosition = direction * scaledDistance;
                b.ScaledRadius = scaledDistance;
                b.Mode = BodyDrawMode.Disk;
                b.Inside = true;
                return b;
            }

            b.AngularRadius = Math.Asin(bodyRadius / d);
            b.ScaledPosition = direction * scaledDistance;
            b.ScaledRadius = scaledDistance * bodyRadius / d;
            b.Mode = 2.0 * b.AngularRadius * cam.WidthPx < minPixels * cam.HorizontalFovRad ? BodyDrawMode.Point : BodyDrawMode.Disk;
            return b;
        }
    }
}
