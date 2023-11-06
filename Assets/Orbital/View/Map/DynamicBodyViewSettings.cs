using UnityEngine;

namespace Orbital.View.Map
{
    public class DynamicBodyViewSettings : ScriptableObject
    {
        public Material orbitMaterial;
        public Material nodeMaterial;
        public Mesh nodeMesh;
        public Material selfMaterial;
        public Mesh selfMesh;

        public int orbitMaxVerticesCount;
    }
}