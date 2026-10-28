using System;
using UnityEngine;

namespace Orbital.Core.Bodies
{
    /// <summary>Physical size of the body. Read by collision, the minimum sphere of influence, the server and the map scale.</summary>
    [Serializable]
    public class PhysicalFacet : IBodyFacet
    {
        public const int TypeId = 3;
        int IBodyFacet.Id => TypeId;

        [Tooltip("Mean radius in meters. 0 = unknown, the body has no surface.")]
        public double radiusMeters;
    }
}
