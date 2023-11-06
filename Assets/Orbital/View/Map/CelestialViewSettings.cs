using Orbital.Core;
using UnityEngine;

namespace Orbital.View.Map
{
    public class CelestialViewSettings : ViewSettings<IStaticBody>
    {
        public Material orbitMaterial;
        public Mesh orbitMesh;
        public Material celestialMaterial;
        public Mesh celestialMesh;
    }
}