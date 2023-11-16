﻿using Ara3D;
using UnityEngine;

namespace Orbital.Core
{
    public interface ILocalSpaceBody
    {
        public int Id => Transform.gameObject.GetInstanceID();
        public Transform Transform { get; }
        public DVector3 LocalPosition { get; }
    }
}