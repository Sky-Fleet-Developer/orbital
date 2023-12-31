using System;
using Ara3D;
using Orbital.Core.TrajectorySystem;
using UnityEngine;

namespace Orbital.Core.Simulation
{
    public static class IterativeSimulation
    {
        private abstract class Simulation
        {
            private const double MaxEccentricity = 0.977;
            
            protected readonly double ParentMass;
            protected readonly DVector3 InitVelocity;
            protected readonly DVector3 InitPosition;
            protected DVector3 Position;
            protected DVector3 Velocity;
            protected double Period;
            protected double SemiMajorAxis;
            protected double Eccentricity;
            protected bool IsCycle;
            protected double CurrentTime;
            protected double Radius;
            private readonly int _maxIterations;
            private readonly double _nu;
            private double _semiMajorAxisInv;
            //public FileStream DepStream;
            //public TextWriter Writer;

            public Simulation(DVector3 initVelocity, DVector3 initPosition, double parentMass,
                int maxIterations = 10000)
            {
                ParentMass = parentMass;
                InitVelocity = initVelocity;
                InitPosition = initPosition;
                _nu = MassUtility.G * ParentMass;
                SemiMajorAxis = 1 / (2 / initPosition.Length() - initVelocity.LengthSquared() / _nu);
                _maxIterations = maxIterations;
                double h = DVector3.Cross(initPosition, initVelocity).Length();
                Eccentricity = Math.Sqrt(1 - (h * h / (MassUtility.G * parentMass * SemiMajorAxis)));
                IsCycle = Math.Abs(Eccentricity) < MaxEccentricity;
                CurrentTime = 0;
                if (IsCycle)
                {
                    _semiMajorAxisInv = 1 / SemiMajorAxis;
                    Period = StaticOrbit.GetPeriod(SemiMajorAxis, MassUtility.G, parentMass);
                }
                else
                {
                    Debug.Log("Need to implement cross-system navigation");
                }
                //if (File.Exists("G:/Projects/orbital/DepStream.txt"))
                //{
                //    File.Delete("G:/Projects/orbital/DepStream.txt");
                //}

                //DepStream = new FileStream("G:/Projects/orbital/DepStream.txt", FileMode.Create);
                //Writer = new StreamWriter(DepStream);
            }


            private DVector3 GetGravityAcceleration(ref DVector3 position, double radius)
            {
                return -position * _nu / (radius * radius * radius);
            }

            protected abstract double DeltaTime { get; }

            public void Run()
            {
                Velocity = InitVelocity;
                Position = InitPosition;
                //DVector3 acceleration = GetGravityAcceleration(ref Position, Position.Length());
                for (int i = 0; i < _maxIterations; i++)
                {
                    Radius = Position.Length();
                    if (IsCycle)
                    {
                        CorrectImpulse(Radius, ref Velocity, ref Position);
                    }

                    BeforeIteration();
                    
                    DVector3 lastPos = Position;
                    double dt = DeltaTime;
                    DVector3 acc = GetGravityAcceleration(ref Position, Radius);
                    /*DVector3 b = (acc - acceleration) / dt;
                    acceleration = acc;
                    
                    double halfDtSqr = (dt * dt * 0.5);
                    Position += Velocity * dt + acceleration * halfDtSqr + b * (dt*dt*dt*0.166666666666);
                    Velocity += acceleration * dt + b * halfDtSqr;*/
                    Leapfrog();
                    
                    Vector3 pS = Position / 22440000;
                    Debug.DrawLine(pS, lastPos / 22440000, Color.red, 0.2f);
                    Debug.DrawLine(pS, pS + Vector3.up * ((float) SemiMajorAxis * 2e-9f), Color.red, 0.2f);

                    CurrentTime += dt;
                    if (IsComplete(Position, Velocity))
                    {
                        Debug.Log($"Result reached in {i} iteration");
                        return;
                    }
                    
                    void Leapfrog()
                    {
                        // Полушаг для скорости
                        Velocity += acc * 0.5 * dt;
                        // Полушаг для позиции
                        Position += Velocity * dt;
                        // Полушаг для скорости (дополнительное обновление)
                        Radius = Position.Length();
                        Velocity += (-Position * _nu / (Radius * Radius * Radius)) * 0.5 * dt;
                    }
                }
                Debug.Log($"Result not reached in {_maxIterations} iterations");
            }

            //public void Dispose()
            //{
            //    Writer.Close();
            //    DepStream.Close();
            //    Writer.Dispose();
            //    DepStream.Dispose();
            //}
            
            //v = v0 / currentEnergy * energy
            private void CorrectImpulse(double radius, ref DVector3 velocity, ref DVector3 position)
            {
                double vSqr = velocity.LengthSquared();

                double r = 1 / (vSqr / (2 * _nu) + 1 / (2 * SemiMajorAxis));

                position *= (r / radius);

                double vSqrWanted = _nu * (2 / radius - _semiMajorAxisInv);
                double mul = Math.Sqrt(vSqrWanted / vSqr);
                velocity *= mul;
            }

            public abstract bool IsComplete(DVector3 position, DVector3 velocity);
            public virtual void BeforeIteration(){}
        }

        private abstract class UniformDeltaTimeSimulation : Simulation
        {
            private double _deltaTime;

            protected UniformDeltaTimeSimulation(DVector3 initVelocity, DVector3 initPosition, double parentMass,
                double deltaTime, int maxIterations = 10000) : base(initVelocity, initPosition, parentMass,
                maxIterations)
            {
                _deltaTime = deltaTime;
            }

            protected override double DeltaTime => _deltaTime;
        }

        private abstract class UniformDeltaPositionSimulation : Simulation
        {
            //private double _period;
            private readonly double _step;

            protected UniformDeltaPositionSimulation(DVector3 initVelocity, DVector3 initPosition, double parentMass,
                double step, int maxIterations = 10000) : base(initVelocity, initPosition, parentMass, maxIterations)
            {
                _step = step;
            }

            protected override double DeltaTime => _step / Velocity.Length();
        }

        private abstract class NonUniformDeltaPositionSimulation : Simulation
        {
            private const double MinimalDt = 1;
            private const double DeltaAngleCutoffInv = 100;
            
            private readonly double _step;
            private readonly double _deltaTimeStep;
            private readonly double _nonuniformity;
            private double _wantedDeltaAngle;
            private DVector3 _velocityCache;
            private double _deltaTime;
            private double _radiusFactor;
            protected NonUniformDeltaPositionSimulation(DVector3 initVelocity, DVector3 initPosition, double parentMass,
                int accuracy, double nonuniformity, int maxIterations = 10000) : base(initVelocity, initPosition,
                parentMass, maxIterations)
            {
                /*if (IsCycle)
                {
                    double b = StaticOrbit.GetSemiMinorAxis(eccentricity, semiMajorAxis);

                    double lApprox = Math.PI * Math.Sqrt(2 * (semiMajorAxis * semiMajorAxis + b * b));
                    _step = lApprox / accuracy;
                    _deltaTimeStep = period / accuracy;
                    double maxNonuniformity =
                        (MinimalDt - _deltaTimeStep) / (_step / initVelocity.Length() - _deltaTimeStep);
                    _nonuniformity = Math.Max(Math.Min(nonuniformity, maxNonuniformity), 1);
                }
                else
                {
                    _deltaTimeStep = 30;
                    _step = initVelocity.Length() * _deltaTimeStep;
                    _nonuniformity = nonuniformity;
                }*/
                accuracy = (int) (Mathf.Lerp((float) accuracy, (float) maxIterations, Mathf.Abs((float) Eccentricity)));
                _nonuniformity = nonuniformity;
                _wantedDeltaAngle = Math.PI * 2 / accuracy;
                _velocityCache = initVelocity.Normalize();
                _deltaTime = MinimalDt;
                _radiusFactor = 1 / initPosition.Length();
            }

            public override void BeforeIteration()
            {
                DVector3 vN = Velocity.Normalize();
                double deltaAngle = Math.Acos(Vector3.Dot(vN, _velocityCache)) + 0.00000001;
                deltaAngle /= 1 + (_radiusFactor * Radius - 1) * _nonuniformity;
                //double ddt = (_wantedDeltaAngle - deltaAngle);
                //bool isIncrease = ddt > 0;

                double influence = Math.Min(deltaAngle * DeltaAngleCutoffInv, 1);
                _deltaTime *= 1 + (_wantedDeltaAngle / deltaAngle - 1) * influence; //+= ddt * (isIncrease ? _nonuniformity.x : _nonuniformity.y);

                _velocityCache = vN;
            }

            protected override double DeltaTime => _deltaTime * _radiusFactor * Radius;
            /* {
                 get
                 {
                     double vInv = 1 / Velocity.Length();
                     double value = Math.Max(Math.Max(_deltaTimeStep + (_step * vInv - _deltaTimeStep) * _nonuniformity,
                                                            MinimalDt * vInv), MinimalDt);
                     //Writer.WriteLine($"{DVector3.Dot(Position.Normalize(), Velocity.Normalize()) : 0.0000000000}; {value : 00000.0000000000}");
                     return value;
                 }
             }*/

        }


        private class GetCloseToCenterSimulation : UniformDeltaTimeSimulation
        {
            private double _dotCache;
            public DVector3 ResultPosition { get; private set; }
            public DVector3 ResultVelocity { get; private set; }
            public double ResultTime { get; private set; }
            private DVector3 _lastPosition;
            private DVector3 _lastVelocity;
            public bool HasResult { get; private set; }

            public GetCloseToCenterSimulation(DVector3 initVelocity, DVector3 initPosition, double parentMass,
                double deltaTime, int maxIterations = 10000) : base(initVelocity, initPosition, parentMass, deltaTime,
                maxIterations)
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

                ResultTime += DeltaTime;
                _lastPosition = position;
                _lastVelocity = velocity;
                return false;
            }
        }

        private class FindCenterSimulation : UniformDeltaTimeSimulation
        {
            private double _dotCache;
            public DVector3 ResultPosition { get; private set; }
            public DVector3 ResultVelocity { get; private set; }
            public double ResultTime { get; private set; }
            private DVector3 _lastPosition;
            private DVector3 _lastVelocity;
            public bool HasResult { get; private set; }

            public FindCenterSimulation(DVector3 initVelocity, DVector3 initPosition, double parentMass,
                double deltaTime, int maxIterations = 10000) : base(initVelocity, initPosition, parentMass, deltaTime,
                maxIterations)
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
                    ResultTime += DeltaTime * t;
                    HasResult = true;
                    return true;
                }

                ResultTime += DeltaTime;
                _lastPosition = position;
                _lastVelocity = velocity;

                return false;
            }
        }

        private class CircleOrbitSimulation : NonUniformDeltaPositionSimulation
        {
            private bool isPiWasReached = false;

            public CircleOrbitSimulation(DVector3 initVelocity, DVector3 initPosition, double parentMass, int accuracy,
                double nonuniformity, int maxIterations = 10000) : base(initVelocity, initPosition, parentMass,
                accuracy, nonuniformity, maxIterations)
            {
            }

            public override bool IsComplete(DVector3 position, DVector3 velocity)
            {
                if (!isPiWasReached)
                {
                    isPiWasReached = (position - InitPosition).Dot(InitVelocity) < 0;
                    return false;
                }
                else
                {
                    return (position - InitPosition).Dot(InitVelocity) > 0;
                }
            }
        }

        private class FillContainerSimulation : CircleOrbitSimulation
        {
            private TrajectoryContainer _coniainer;
            private int _index = 0;
            private double _startTime;
            public FillContainerSimulation(TrajectoryContainer container, double startTime, DVector3 initVelocity, DVector3 initPosition, double parentMass, int accuracy, double nonuniformity) : base(initVelocity, initPosition, parentMass, accuracy, nonuniformity, container.Capacity - 1)
            {
                _coniainer = container;
                _startTime = startTime;
                _coniainer[0] = new Mark(initPosition, initVelocity, startTime);
            }
            public override bool IsComplete(DVector3 position, DVector3 velocity)
            {
                bool result = base.IsComplete(position, velocity);
                _index++;
                if (!result)
                {
                    _coniainer[_index] = new Mark(position, velocity, _startTime + CurrentTime);
                }
                else
                {
                    double time = (InitPosition - position).Length() - (InitVelocity + velocity).Length() * 0.5;
                    _coniainer[_index] = new Mark(InitPosition, InitVelocity, _startTime + _coniainer[_index - 1].TimeMark + time);
                }
                return result;
            }
        }

        public static DVector3? FindPericenter(DVector3 initVelocity, DVector3 initPosition, double semiMajorAxis,
            double parentMass, double deltaTime, out double lastCenterTime, out bool isWasPericenter)
        {
            GetCloseToCenterSimulation roughSimulation =
                new GetCloseToCenterSimulation(-initVelocity, initPosition, parentMass, deltaTime * 30);
            roughSimulation.Run();
            if (roughSimulation.HasResult)
            {
                FindCenterSimulation simulation = new FindCenterSimulation(roughSimulation.ResultVelocity,
                    roughSimulation.ResultPosition, parentMass, deltaTime);
                simulation.Run();
                if (simulation.HasResult)
                {
                    double centerLengthSqr = simulation.ResultPosition.LengthSquared();
                    bool isPericenter = centerLengthSqr < semiMajorAxis * semiMajorAxis;
                    lastCenterTime = roughSimulation.ResultTime + simulation.ResultTime;
                    isWasPericenter = isPericenter;
                    if (!isPericenter)
                    {
                        double length = Math.Sqrt(centerLengthSqr);
                        return -simulation.ResultPosition / length * (semiMajorAxis * 2 - length);
                    }

                    return simulation.ResultPosition;
                }
            }

            isWasPericenter = false;
            lastCenterTime = 0;
            return null;
        }

        public static void DrawTrajectoryCircle(DVector3 initPosition, DVector3 initVelocity, double parentMass,
            int accuracy, double nonuniformity)
        {
            CircleOrbitSimulation simulation =
                new CircleOrbitSimulation(initVelocity, initPosition, parentMass, accuracy, nonuniformity);
            simulation.Run();
        }

        public static void FillTrajectoryContainer(TrajectoryContainer container, double startTime, DVector3 initPosition, DVector3 initVelocity, double parentMass,
            int accuracy, float nonuniformity)
        {
            FillContainerSimulation simulation = new FillContainerSimulation(container, startTime, initVelocity, initPosition, parentMass, accuracy, nonuniformity);
            simulation.Run();
        }
    }
}