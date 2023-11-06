using UnityEngine;

namespace Orbital.View
{
    public struct ViewContainer
    {
        public Transform Transform;
        public MeshRenderer MeshRenderer;
        public MeshFilter MeshFilter;

        public ViewContainer(Transform transform)
        {
            Transform = transform;
            MeshFilter = transform.GetComponent<MeshFilter>();
            MeshRenderer = transform.GetComponent<MeshRenderer>();
        }
    }
}