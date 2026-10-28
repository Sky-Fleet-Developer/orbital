using System;
using UnityEngine;

namespace Orbital.Core.Bodies
{
    /// <summary>Spin of the body. The rotation angle is a deterministic function of time, identical on the server and on clients.</summary>
    [Serializable]
    public class RotationFacet : IBodyFacet
    {
        public const int TypeId = 4;
        int IBodyFacet.Id => TypeId;

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
