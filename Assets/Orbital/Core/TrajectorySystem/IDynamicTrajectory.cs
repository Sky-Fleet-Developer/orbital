using System;
using Orbital.Core.Simulation;
using Unity.Collections;

namespace Orbital.Core.TrajectorySystem
{
    public interface IDynamicTrajectory
    {
        public NativeArray<Mark> Path { get; }
        public int Length { get; }
        public event Action PathChangedHandler;
    }
}