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
        public double inclination {get;}
        public double eccentricity {get;}
        public double semiMajorAxis {get;}
        public double longitudeAscendingNode {get;}
        public double argumentOfPeriapsis {get;}
        public double epoch {get;}
        public double SemiMinorAxis => semiMajorAxis * Math.Sqrt(1 - eccentricity * eccentricity);
        public double period { get; }
        public double PeR { get; }
        public double ApR { get; }
        public DMatrix4x4 RotationMatrix { get; }
        public void Calculate(TrajectorySettings settings);
        public void Calculate(DVector3 position, DVector3 velocity);
        public void DrawGizmos();
    }

    public interface IDynamicTrajectory
    {
        public NativeArray<Mark> Path { get; }
        public int Length { get; }
        public event Action PathChangedHandler;
    }
}