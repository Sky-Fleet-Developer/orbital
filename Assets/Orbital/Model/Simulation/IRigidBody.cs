using System;
using Ara3D;
using Orbital.Model.SystemComponents;

namespace Orbital.Model.Simulation
{
    public interface IRigidBody
    {
        public MassSystemComponent Parent { get; }
        public RigidBodyMode Mode { get; }
        public DVector3 LocalPosition { get; }
        public DVector3 LocalVelocity { get; }
        public event Action<RigidBodyMode> ModeChangedHandler;
        public void Present(Observer observer);
        public void RemovePresent();
        public void AwakeFromSleep();

    }
    
    public enum RigidBodyMode
    {
        Trajectory = 0,
        Sleep = 1,
        Simulation = 2
    }
}
