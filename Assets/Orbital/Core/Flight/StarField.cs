using System;

namespace Orbital.Core.Flight
{
    /// <summary>Color of one star point, 8 bits per channel (like the NavBall raster). Blittable.</summary>
    public struct StarColor
    {
        public byte R, G, B;
    }

    /// <summary>
    /// One background star to draw (stage 3.4a/3.5b): a unit direction from the eye (stars are infinitely far, no parallax),
    /// the light flux in the same units as <see cref="BodyFlux"/> (1 = the Sun at 1 AU) and its color. Blittable, no references.
    /// </summary>
    public struct StarPoint
    {
        public Vec3 Direction;
        public double Flux;
        public StarColor Color;
    }

    /// <summary>
    /// Background stars (stage 3.4a/3.5b): the stars inside the cone around the camera direction down to a limiting flux,
    /// written into a caller-owned buffer. Implementations must not allocate per frame (the buffer is outside; an array,
    /// a stackalloc span or a future NativeArray all work). The procedural generator is designed and plugged in later;
    /// the default implementation is empty.
    /// </summary>
    public interface IStarField
    {
        /// <summary>
        /// Fills <paramref name="buffer"/>[0..n) with the stars inside the cone around the unit <paramref name="direction"/>
        /// with the half angle <paramref name="coneHalfAngleRad"/> whose Flux is at least <paramref name="minFlux"/>, and
        /// returns n (at most the buffer length). Leaves the rest of the buffer alone.
        /// </summary>
        int Query(Vec3 direction, double coneHalfAngleRad, double minFlux, Span<StarPoint> buffer);
    }

    /// <summary>Empty star field: no stars. One shared instance; Query allocates nothing and leaves the buffer alone.</summary>
    public sealed class EmptyStarField : IStarField
    {
        public static readonly EmptyStarField Instance = new EmptyStarField();

        public int Query(Vec3 direction, double coneHalfAngleRad, double minFlux, Span<StarPoint> buffer) => 0;
    }
}
