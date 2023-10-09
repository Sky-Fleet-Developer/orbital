using System;
using Ara3D;
using UnityEngine;

namespace Orbital.Model.TrajectorySystem
{
    public static class IterativeSimulation
    {
        private abstract class Simulation
        {
            private double _parentMass;
            private DVector3 _initVelocity;
            private DVector3 _initPosition;
            private double _deltaTime;
            private int _maxIterations;
            
            public Simulation(DVector3 initVelocity, DVector3 initPosition, double parentMass, double deltaTime, int maxIterations = 10000)
            {
                _parentMass = parentMass;
                _initVelocity = initVelocity;
                _initPosition = initPosition;
                _deltaTime = deltaTime;
                _maxIterations = maxIterations;
            }

            public void Run()
            {
                DVector3 position = _initPosition;
                DVector3 velocity = _initVelocity;
                for (int i = 0; i < _maxIterations; i++)
                {
                    double r = position.Length();
                    //DVector3 lastPos = position;
                    // Полушаг для скорости
                    velocity += (-position * MassUtility.G * _parentMass / (r * r * r)) * 0.5 * _deltaTime;
    
                    // Полушаг для позиции
                    position += velocity * _deltaTime;
    
                    // Полушаг для скорости (дополнительное обновление)
                    r = position.Length();
                    velocity += (-position * MassUtility.G * _parentMass / (r * r * r)) * 0.5 * _deltaTime;
                    
                    //Debug.DrawLine(position / 448800000, lastPos / 448800000, Color.red, 10);
                    
                    if (IsComplete(position, velocity))
                    {
                        Debug.Log($"Result reached in {i} iteration");
                        break;
                    }
                }
            }

            public abstract bool IsComplete(DVector3 position, DVector3 velocity);
        }
        
        private class GetCloseToCenterSimulation : Simulation
        {
            private double _dotCache;
            public DVector3 ResultPosition { get; private set; }
            public DVector3 ResultVelocity { get; private set; }
            private DVector3 _lastPosition;
            private DVector3 _lastVelocity;
            public bool HasResult { get; private set; }
            public GetCloseToCenterSimulation(DVector3 initVelocity, DVector3 initPosition, double parentMass, double deltaTime, int maxIterations = 10000) : base(initVelocity, initPosition, parentMass, deltaTime, maxIterations)
            {
                _dotCache = initPosition.Dot(initVelocity);
                _lastPosition = initPosition;
                _lastVelocity = initVelocity;
                HasResult = false;
            }

            public override bool IsComplete(DVector3 position, DVector3 velocity)
            {
                double dot = position.Dot(velocity);
                bool a = dot > 0, b = _dotCache > 0;
                if (a != b)
                {
                    ResultPosition = _lastPosition;
                    ResultVelocity = _lastVelocity;
                    HasResult = true;
                    return true;
                }

                _lastPosition = position;
                _lastVelocity = velocity;
                return false;
            }
        }
        
        private class FindCenterSimulation : Simulation
        {
            private double _dotCache;
            public DVector3 ResultPosition { get; private set; }
            public DVector3 ResultVelocity { get; private set; }
            private DVector3 _lastPosition;
            private DVector3 _lastVelocity;
            public bool HasResult { get; private set; }
            public FindCenterSimulation(DVector3 initVelocity, DVector3 initPosition, double parentMass, double deltaTime, int maxIterations = 10000) : base(initVelocity, initPosition, parentMass, deltaTime, maxIterations)
            {
                _dotCache = initPosition.Dot(initVelocity);
                _lastPosition = initPosition;
                _lastVelocity = initVelocity;
                HasResult = false;
            }

            public override bool IsComplete(DVector3 position, DVector3 velocity)
            {
                double dot = position.Dot(velocity);
                bool a = dot > 0, b = _dotCache > 0;
                if (a != b)
                {
                    double dotCacheClear = _dotCache / (_lastPosition.Length() * _lastVelocity.Length());
                    double dotClear = dot / (position.Length() * velocity.Length());
                    double t = -dotCacheClear / (dotClear - dotCacheClear);
                    
                    ResultPosition = DVector3.Lerp(_lastPosition, position, t);
                    ResultVelocity = DVector3.Lerp(_lastVelocity, velocity, t);
                    HasResult = true;
                    return true;
                }

                _lastPosition = position;
                _lastVelocity = velocity;

                return false;
            }
        }

        public static DVector3? FindPericenter(DVector3 initVelocity, DVector3 initPosition, double semiMajorAxis, double parentMass, double deltaTime)
        {
            GetCloseToCenterSimulation roughSimulation = new GetCloseToCenterSimulation(initVelocity, initPosition, parentMass, deltaTime * 30);
            roughSimulation.Run();
            if (roughSimulation.HasResult)
            {
                FindCenterSimulation simulation = new FindCenterSimulation(roughSimulation.ResultVelocity, roughSimulation.ResultPosition, parentMass, deltaTime);
                simulation.Run();
                if (simulation.HasResult)
                {
                    double centerLengthSqr = simulation.ResultPosition.LengthSquared();
                    bool isPericenter = centerLengthSqr < semiMajorAxis * semiMajorAxis;
                    if (!isPericenter)
                    {
                        double length = Math.Sqrt(centerLengthSqr);
                        return -simulation.ResultPosition / length * (semiMajorAxis * 2 - length);
                    }

                    return simulation.ResultPosition;
                }
            }
            
            return null;
        }
    }
}
