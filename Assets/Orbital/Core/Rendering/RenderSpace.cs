using System;
using Ara3D;

namespace Orbital.Core.Rendering
{
    /// <summary>Where and how big to draw something: a camera-relative position and a uniform scale for its mesh.</summary>
    public readonly struct RenderPlacement
    {
        public readonly float X, Y, Z;
        public readonly float Scale;
        /// <summary>True when the distance was compressed, so the object belongs to the far (scaled space) layer.</summary>
        public readonly bool Compressed;

        public RenderPlacement(float x, float y, float z, float scale, bool compressed)
        {
            X = x;
            Y = y;
            Z = z;
            Scale = scale;
            Compressed = compressed;
        }
    }

    /// <summary>
    /// Maps double precision world positions to float positions relative to the camera, so the engine never sees large numbers.
    /// Within <see cref="NearLimit"/> the mapping is exact. Beyond it the distance is compressed logarithmically and the
    /// object is scaled down by the same factor, which keeps its direction and angular size unchanged.
    /// </summary>
    public sealed class RenderSpace
    {
        public double NearLimit { get; }

        public RenderSpace(double nearLimit)
        {
            if (!(nearLimit > 0.0)) throw new ArgumentOutOfRangeException(nameof(nearLimit));
            NearLimit = nearLimit;
        }

        /// <summary>Rendered distance for a true distance: identity up to the near limit, then near * (1 + ln(d / near)).</summary>
        public double CompressDistance(double distance)
        {
            if (distance <= NearLimit) return distance;
            return NearLimit * (1.0 + Math.Log(distance / NearLimit));
        }

        public RenderPlacement Place(DVector3 camera, DVector3 position)
        {
            DVector3 offset = position - camera;
            double distance = offset.Length();
            if (distance <= NearLimit) return new RenderPlacement((float) offset.x, (float) offset.y, (float) offset.z, 1f, false);

            double compressed = CompressDistance(distance);
            double factor = compressed / distance;
            return new RenderPlacement((float) (offset.x * factor), (float) (offset.y * factor), (float) (offset.z * factor), (float) factor, true);
        }
    }
}
