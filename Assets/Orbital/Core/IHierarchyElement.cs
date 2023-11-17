using Ara3D;
using UnityEngine;

namespace Orbital.Core
{
    public interface IHierarchyElement
    {
        public int Id { get; set; }
        public int ParentId { get; }
        public DVector3 LocalPosition { get; }
        public Vector3 LocalEulerAngles { get; }
        public string Name { get; }
        public Transform Transform { get; }
    }
}