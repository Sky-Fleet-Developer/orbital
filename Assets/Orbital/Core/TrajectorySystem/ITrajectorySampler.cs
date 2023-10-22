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
        public double Inclination {get;}
        public double Eccentricity {get;}
        public double SemiMajorAxis {get;}
        public double LongitudeAscendingNode {get;}
        public double ArgumentOfPeriapsis {get;}
        public double Epoch {get;}
        public double SemiMinorAxis => SemiMajorAxis * Math.Sqrt(1 - Eccentricity * Eccentricity);
        public double Period { get; }
        public double PeR { get; }
        public double ApR { get; }
        public DVector3 GetPositionFromTrueAnomaly(double trueAnomaly);
        public DMatrix4x4 RotationMatrix { get; }
        public void Calculate(TrajectorySettings settings);
        public void Calculate(DVector3 position, DVector3 velocity);
        public event Action WasChangedHandler;
        public void DrawGizmos();
        public DVector3 GetPositionAtT(double T);
        public DVector3 GetPositionFromMeanAnomaly(double m);
        public DVector3 GetPositionFromEccAnomaly(double m);
    }

    public interface IDynamicTrajectory
    {
        public NativeArray<Mark> Path { get; }
        public int Length { get; }
        public event Action PathChangedHandler;
    }
}