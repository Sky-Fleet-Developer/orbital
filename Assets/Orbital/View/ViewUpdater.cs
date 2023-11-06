using Ara3D;
using Orbital.Core;
using Orbital.Core.TrajectorySystem;
using Plugins.LocalPool;
using UnityEngine;

namespace Orbital.View
{
    public abstract class ViewUpdater
    {
        public abstract void Update(double time);
        public abstract void Dispose(Pool<ViewContainer> pool);
    }
}
