using Orbital.Core;
using Orbital.Core.Bodies;
using UnityEngine;

namespace Orbital.View.Map
{
    public class CelestialViewSettings : ViewSettings<IStaticBody>
    {
        public Material orbitMaterial;
        public Mesh orbitMesh;
        public Material celestialMaterial;
        public Mesh celestialMesh;
        [Tooltip("Transparent material of the atmosphere shell. Atmospheres are skipped while it is empty.")]
        public Material atmosphereMaterial;
        [Tooltip("Per-body data (size, rotation, look), matched by object name.")]
        public BodyTable bodyTable;
        [Tooltip("Disc diameter in map units for bodies without a record or without a radius.")]
        public float fallbackDiameter = 0.08f;
    }
}