using System;
using UnityEngine;

namespace Orbital.View.Map
{
    /// <summary>Hard surface. Bodies without it (gas giants, stars) keep the plain material.</summary>
    [Serializable]
    public class SurfaceFacet : IBodyFacet
    {
        public const int TypeId = 1;
        int IBodyFacet.Id => TypeId;

        [Tooltip("Replaces the default celestial material.")]
        public Material material;
        public Color color = Color.white;
        [Range(0f, 1f)] public float smoothness = 0.2f;
        [Range(0f, 1f)] public float metallic;
    }
}
