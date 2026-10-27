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
    }
}
