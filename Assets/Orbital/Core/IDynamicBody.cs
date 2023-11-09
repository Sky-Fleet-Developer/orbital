using System;
using Ara3D;
using Orbital.Core.Navigation;
using Orbital.Core.TrajectorySystem;
using UnityEngine;

namespace Orbital.Core
{
    public interface IDynamicBody
    {
        public IStaticBody Parent { get; }
        public StaticOrbit Orbit { get; }
        public OrbitEnding Ending { get; }
        public double Mass { get; }
        public double MassInv { get; }
        public DVector3 Position => Parent.Position + LocalPosition;
        public DVector3 LocalPosition => Orbit.GetPositionAtT(TimeService.WorldTime);
        public DVector3 GetPositionAtT(double t) => Parent.GetPositionAtT(t) + Orbit.GetPositionAtT(t);
        public event Action OrbitChangedHandler;
        public void Init();
        /*public void Present();
        public void RemovePresent();*/
        void AddForce(Vector3 force);
    }
}
