using System;
using Ara3D;
using Orbital.Model.SystemComponents;
using Orbital.Model.TrajectorySystem;

namespace Orbital.Model.Simulation
{
    public interface IRigidBody
    {
        public MassSystemComponent Parent { get; }
        public RigidBodyMode Mode { get; }
        public ITrajectorySampler Trajectory { get; }

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
