using Ara3D;
using UnityEngine;

namespace Orbital.Core
{
    public interface IHierarchyElement
    {
        public int Id { get; set; }
        public Transform Transform { get; }
        //public DVector3 LocalPosition { get; }
    }
}