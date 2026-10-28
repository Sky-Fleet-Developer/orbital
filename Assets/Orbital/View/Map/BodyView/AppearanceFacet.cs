using System;
using UnityEngine;

namespace Orbital.View.Map
{
    /// <summary>Disc of the body: physical size, tint, brightness and self-emission (stars).</summary>
    [Serializable]
    public class AppearanceFacet : IBodyFacet
    {
        public const int TypeId = 0;
        int IBodyFacet.Id => TypeId;

        [Tooltip("Mean radius in meters. 0 = use the fallback diameter of the view settings.")]
        public double radiusMeters;
        [Tooltip("Exaggeration of this body on top of the map-wide multiplier. 1 = real scale.")]
        public float sizeMultiplier = 1f;
        public Color tint = Color.white;
        [Tooltip("Multiplies the tint of the lit body.")]
        public float brightness = 1f;
        [Tooltip("HDR emission intensity of the tint. 0 = lit by the scene only; use for stars.")]
        public float emission;

        [Header("Rotation (read by the server world generator and by clients)")]
        [Tooltip("Sidereal rotation period in seconds. 0 = the body does not spin.")]
        public double rotationPeriodSeconds;
        [Tooltip("Axial tilt (obliquity) in degrees, measured from the normal of the body orbit (the ecliptic normal for the root). Above 90 = retrograde spin.")]
        public double axialTiltDegrees;
        [Tooltip("Direction of the tilt around the orbit normal, degrees from the periapsis direction.")]
        public double axisAzimuthDegrees;
        [Tooltip("Rotation angle at world time 0, degrees. Ignored for tidally locked bodies.")]
        public double initialAngleDegrees;
        [Tooltip("Rotation rate equals the mean motion of the orbit around the parent; the prime meridian faces the parent at time 0.")]
        public bool tidallyLocked;
    }
}
