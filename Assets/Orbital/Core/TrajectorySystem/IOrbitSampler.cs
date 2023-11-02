using System;
using Ara3D;
using Ara3D.Double;
using Orbital.Core.Simulation;
using Unity.Collections;

namespace Orbital.Core.TrajectorySystem
{
    public interface IOrbitSampler
    {
        public (DVector3 position, DVector3 velocity) GetSample(double time, bool positionRequired = true,
            bool velocityRequired = true);
    }


    public interface IStaticOrbit : IOrbitSampler
    {
        public double Inclination { get; }
        public double Eccentricity { get; }
        public double SemiMajorAxis { get; }
        public double LongitudeAscendingNode { get; }
        public double ArgumentOfPeriapsis { get; }
        public double SemiLatusRectum { get; }
        public double Nu { get; set; }
        public double MeanMotion { get; }
        double Epoch { get; }
        public double SemiMinorAxis => SemiMajorAxis * Math.Sqrt(1 - Eccentricity * Eccentricity);
        public double Period { get; }
        public double Pericenter { get; }
        public double Apocenter { get; }
        public double MeanAnomalyAtEpoch { get; }
        public DMatrix4x4 RotationMatrix { get; }
        public void Calculate(TrajectorySettings settings, double epoch);
        public void Calculate(DVector3 position, DVector3 velocity, double epoch);
        public event Action WasChangedHandler;
        public void DrawGizmos(DVector3 offset);
        public void DrawGizmosByT(double from, double to, DVector3 offset);
    }

    public interface IDynamicTrajectory
    {
        public NativeArray<Mark> Path { get; }
        public int Length { get; }
        public event Action PathChangedHandler;
    }
}