using System;
using Orbital.Core.Bodies;
using UnityEngine;

namespace Orbital.View.Map
{
    /// <summary>Atmosphere shell around the body.</summary>
    [Serializable]
    public class AtmosphereFacet : IBodyFacet
    {
        public const int TypeId = 2;
        int IBodyFacet.Id => TypeId;

        [Tooltip("Replaces the atmosphere material of the view settings.")]
        public Material material;
        public Color color = new Color(0.4f, 0.6f, 1f, 1f);
        [Tooltip("Height of the shell as a fraction of the body radius.")]
        [Range(0.001f, 1f)] public float thickness = 0.05f;
        [Range(0f, 1f)] public float opacity = 0.35f;
    }
}
