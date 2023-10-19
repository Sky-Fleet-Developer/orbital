using System;
using System.Collections.Generic;
using Ara3D;
using Ara3D.Double;
using Orbital.Core.Simulation;
using Unity.Collections;

namespace Orbital.Core.TrajectorySystem
{
    public interface ITrajectorySampler
    {
        public (DVector3 position, DVector3 velocity) GetSample(double time, bool positionRequired = true, bool velocityRequired = true);
    }

    public interface IStaticTrajectory : ITrajectorySampler
    {
        public double Eccentricity { get; }
        public double SemiMajorAxis { get; }
        public double SemiMinorAxis { get; }
        public double PericenterRadius { get; }
        public double Period { get; }
        public double TimeShift { get; }
        public bool IsZero { get; }
        public DMatrix4x4 RotationMatrix { get; }
        public void Calculate();
    }

    public interface IDynamicTrajectory
    {
        public NativeArray<Mark> Path { get; }
        public int Length { get; }
        public event Action PathChangedHandler;
    }
}