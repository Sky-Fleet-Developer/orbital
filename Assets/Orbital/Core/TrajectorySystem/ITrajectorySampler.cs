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

    public interface ITrajectoryRefSampler
    {
        public double GetOrbitalStateVectorsAtOrbitTime(double orbitTime, out DVector3 pos, out DVector3 vel);
        public double GetOrbitalStateVectorsAtTrueAnomaly(double trueAnomaly, out DVector3 pos, out DVector3 vel);
        public double Epoch { get; }
    }
    
    public interface IStaticTrajectory : ITrajectorySampler, ITrajectoryRefSampler
    {
        public double Inclination {get;}
        public double Eccentricity {get;}
        public double SemiMajorAxis {get;}
        public double LongitudeAscendingNode {get;}
        public double ArgumentOfPeriapsis {get;}
        //double ITrajectoryRefSampler.Epoch {get;}
        public double SemiMinorAxis => SemiMajorAxis * Math.Sqrt(1 - Eccentricity * Eccentricity);
        public double Period { get; }
        public double PeR { get; }
        public double ApR { get; }
        public DVector3 GetPositionFromTrueAnomaly(double trueAnomaly);
        public DMatrix4x4 RotationMatrix { get; }
        public void Calculate(TrajectorySettings settings);
        public void Calculate(DVector3 position, DVector3 velocity);
        public event Action WasChangedHandler;
        public void DrawGizmos(DVector3 offset);
        public void DrawGizmosByT(double from, double to, DVector3 offset);
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