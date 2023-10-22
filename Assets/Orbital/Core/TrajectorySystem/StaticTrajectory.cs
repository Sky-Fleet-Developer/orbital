using System;
using System.Threading;
using Ara3D;
using Ara3D.Double;
using UnityEngine;

namespace Orbital.Core.TrajectorySystem
{
    public enum SystemType
    {
        SingleCenter = 0,
        DoubleSystem = 1,
    }
    
    public class StaticTrajectory : ITrajectorySampler, IStaticTrajectory
    {
        public const double Deg2Rad = 0.01745329;
        public const double Rad2Deg = 57.29578;
        
        private IMass _other;
        private ITrajectorySettingsHolder _self;
        
        public double inclination {get; private set;}
        public double longitudeAscendingNode {get; private set;}
        public double argumentOfPeriapsis {get; private set;}
        public double eccentricity {get; private set;}
        public double semiMajorAxis {get; private set;}
        public double epoch {get; private set;}
        public double period {get; private set;}
        public double meanAnomaly {get; private set;}
        public double orbitalEnergy;
        public double trueAnomaly;
        public double eccentricAnomaly;
        public double radius;
        public double altitude;
        public double orbitPercent;
        public double orbitTime;
        public double OrbitTimeAtEpoch;
        public double timeToPe;
        public double timeToAp;
        public DVector3 h;
        public DVector3 eccVec;
        public DVector3 an;
        public double meanAnomalyAtEpoch;
        public double meanMotion;
        
        
        private DMatrix4x4 _rotationMatrix;
        public DMatrix4x4 RotationMatrix => _rotationMatrix;
        private SystemType _systemType;

        public double semiMinorAxis
        {
            get
            {
                if (eccentricity < 1.0)
                {
                    return semiMajorAxis * Math.Sqrt(1.0 - eccentricity * eccentricity);
                }

                return semiMajorAxis * Math.Sqrt(eccentricity * eccentricity - 1.0);
            }
        }

        private double Nu => _other.Mass * MassUtility.G;
        public double semiLatusRectum => h.LengthSquared() / Nu;
        public double PeR => (1.0 - eccentricity) * semiMajorAxis;

        public double ApR => (1.0 + eccentricity) * semiMajorAxis;
        
        public double GetMeanMotion(double sma)
        {
            double num = Math.Abs(sma);
            return Math.Sqrt(Nu / (num * num * num));
        }

        public StaticTrajectory(ITrajectorySettingsHolder self, IMass other, SystemType systemType)
        {
            _self = self;
            _other = other;
            _systemType = systemType;
            _rotationMatrix = DMatrix4x4.Identity;
        }
        
        public void Calculate()
        {
            TrajectorySettings settings = _self.Settings;
            SetOrbit(settings.inclination * Deg2Rad, settings.eccentricity, settings.semiMajorAxis,
                settings.longitudeAscendingNode * Deg2Rad, settings.argumentOfPeriapsis * Deg2Rad, 0, settings.timeShift);
            /*switch (_systemType)
            {
                case SystemType.SingleCenter:
                    CalculateForSingleCenter(ref settings);
                    break;
                case SystemType.DoubleSystem:
                    CalculateForDoubleSystem(ref settings);
                    break;
            }*/
        }
        
        public void SetOrbit(double inclination,
            double eccentricity,
            double semiMajorAxis,
            double longitudeAscendingNode,
            double argumentOfPeriapsis,
            double meanAnomalyAtEpoch,
            double epoch)
        {
            this.inclination = inclination;
            this.eccentricity = eccentricity;
            this.semiMajorAxis = semiMajorAxis;
            this.longitudeAscendingNode = longitudeAscendingNode;
            this.argumentOfPeriapsis = argumentOfPeriapsis;
            this.meanAnomalyAtEpoch = meanAnomalyAtEpoch;
            this.epoch = epoch;
            Init();
        }
        
        public void Init()
        {
            _rotationMatrix = GetRotation(inclination, longitudeAscendingNode, argumentOfPeriapsis);
            an = DVector3.Cross(DVector3.forward, _rotationMatrix.GetUp());
            if (an.LengthSquared() == 0.0)
            {
                an = DVector3.right;
            }

            eccVec = _rotationMatrix.GetRight() * eccentricity;
            double d = Nu * semiMajorAxis * (1.0 - eccentricity * eccentricity);
            h = _rotationMatrix.GetUp() * Math.Sqrt(d);
            meanMotion = GetMeanMotion(semiMajorAxis);
            meanAnomaly = meanAnomalyAtEpoch;
            orbitTime = meanAnomaly / meanMotion;
            OrbitTimeAtEpoch = orbitTime;
            if (eccentricity < 1.0)
            {
                period = 2.0 * Math.PI / meanMotion;
                orbitPercent = meanAnomaly / (2.0 * Math.PI);
            }
            else
            {
                period = double.PositiveInfinity;
                orbitPercent = 0.0;
            }
        }

        public static DMatrix4x4 GetRotation(double inclination, double longitudeAscendingNode, double argumentOfPeriapsis)
        {
            DMatrix4x4 lan = DMatrix4x4.CreateRotation(0, longitudeAscendingNode, 0);
            DMatrix4x4 inc = DMatrix4x4.CreateRotation(0, 0, inclination);
            DMatrix4x4 aop = DMatrix4x4.CreateRotation(0, argumentOfPeriapsis, 0);

            return aop * inc * lan;
        }
        
        /*private void CalculateForSingleCenter(ref TrajectorySettings settings)
        {
            PericenterRadius = settings.pericenterRadius;
            eccentricity = GetEccentricity(settings.pericenterSpeed, settings.pericenterRadius, _other.Mass, MassUtility.G);
            semiMajorAxis = GetSemiMajorAxis(eccentricity, settings.pericenterRadius);
            SemiMinorAxis = GetSemiMinorAxis(eccentricity, semiMajorAxis);
            period = GetPeriod(semiMajorAxis, MassUtility.G, _other.Mass);
            IsZero = period is 0 or double.NaN;
            if (!IsZero)
            {
                CreateRotation(ref settings);
            }
        }

        private void CalculateForDoubleSystem(ref TrajectorySettings settings)
        {
            period = settings.period;
            PericenterRadius = settings.pericenterRadius;
            semiMajorAxis = GetSemiMajorAxis(_other.Mass, period, MassUtility.G);
            eccentricity = GetEccentricity(PericenterRadius, semiMajorAxis);
            SemiMinorAxis = GetSemiMinorAxis(eccentricity, semiMajorAxis);
            IsZero = period is 0 or double.NaN;
            if (!IsZero)
            {
                CreateRotation(ref settings);
            }
        }
        */
        private static AsyncThreadScheduler _calculationScheduler = new AsyncThreadScheduler(3);
        
        /*public async Task SetupFromSimulation(DVector3 position, DVector3 velocity)
        {
            double r = position.Length();
            DVector3 hh = DVector3.Cross(position, velocity);
            double h = hh.Length();
            //double i = Math.Acos(hh.y / h);
            double vSqr = velocity.LengthSquared();
            // semiMajorAxis
            semiMajorAxis = 1 / (2 / r - vSqr / (MassUtility.G * _other.Mass));
            eccentricity = Math.Sqrt(1 - (h * h / (MassUtility.G * _other.Mass * semiMajorAxis)));
            SemiMinorAxis = GetSemiMinorAxis(eccentricity, semiMajorAxis);
            period = GetPeriod(semiMajorAxis, MassUtility.G, _other.Mass);
            DVector3? pericenter = await _calculationScheduler.Schedule(() => IterativeSimulation.FindPericenter(velocity, position, semiMajorAxis, _other.Mass, 1, out _, out _));

            _rotationMatrix = DMatrix4x4.LookRotation(pericenter.Value, hh);
            PericenterRadius = pericenter.Value.Length();
            DVector3 flatPos = _rotationMatrix.GetInverse() * position;

            //true anomaly
            double u = Math.Atan2(flatPos.x, flatPos.z);

            // eccentric anomaly
            double E = CalculateEccentricAnomalyByTrue(u, eccentricity);

            //mean anomaly
            double M = E - eccentricity * Math.Sin(E);

            TimeShift = -M / (2 * Math.PI);
        }*/

        /*public void UpdateSettings(ref TrajectorySettings settings)
        {
            settings.pericenterRadius = (float)PericenterRadius;
            settings.period = (float)period;
            DVector3 fwd = _rotationMatrix * new DVector3(0, 0, 1);
            DVector3 up = _rotationMatrix * new DVector3(0, 0, 1);
            DVector3 right = DVector3.Cross(fwd, up);
            settings.longitudeAscendingNode = (float)(Math.Atan2(fwd.x, fwd.z) * Rad2Deg);
            Debug.Log("longitudeAscendingNode:" + settings.longitudeAscendingNode);
            settings.argumentOfPeriapsis = (float)(Math.Asin(fwd.y) * Rad2Deg);
            Debug.Log("argumentOfPeriapsis:" + settings.argumentOfPeriapsis);
            settings.inclination = (float)(Math.Asin(right.x) * Rad2Deg);
            Debug.Log("inclination:" + settings.inclination);
            //settings.timeShift = (float)(TimeShift * 100);
            //Debug.Log("timeShift:" + settings.timeShift);
        }*/

        /*private void CreateRotation(ref TrajectorySettings settings)
        {
            _rotationMatrix = DMatrix4x4.CreateRotation(settings.argumentOfPeriapsis * Deg2Rad, settings.longitudeAscendingNode * Deg2Rad, 0) * DMatrix4x4.CreateRotation(0, 0, settings.inclination * Deg2Rad);
        }*/

        public (DVector3 position, DVector3 velocity) GetSample(double time, bool positionRequired = true, bool velocityRequired = true)
        {
            DVector3 pos, vel;
            if (double.IsInfinity(meanMotion))
            {
                pos = DVector3.Zero;
                vel = DVector3.Zero;
            }
            else
            {
                GetOrbitalStateVectorsAtObT(time, out pos, out vel);
            }

            return (pos, vel); //(positionRequired ? GetPosition(time) : DVector3.Zero, velocityRequired ? GetVelocity(time) : DVector3.Zero);
        }
        
        public double TrueAnomalyAtT(double T)
        {
            double num = this.solveEccentricAnomaly(T * this.meanMotion, this.eccentricity);
            if (!double.IsNaN(num)) return this.GetTrueAnomaly(num);

            return double.NaN;
        }
        
               private double solveEccentricAnomalyHyp(double M, double ecc, double maxError = 1E-07)
        {
            if (double.IsInfinity(M))
            {
                return M;
            }
            else
            {
                double num1 = 1.0;
                double num2 = 2.0 * M / ecc;
                double num3 = Math.Log(Math.Sqrt(num2 * num2 + 1.0) + num2);
                while (Math.Abs(num1) > maxError)
                {
                    num1 = (eccentricity * Math.Sinh(num3) - num3 - M) / (eccentricity * Math.Cosh(num3) - 1.0);
                    num3 -= num1;
                }
                return num3;
            }
        }

        public double solveEccentricAnomaly(double M, double ecc, double maxError = 1E-07, int maxIterations = 8)
        {
            if (eccentricity >= 1.0)
            {

                        return solveEccentricAnomalyHyp(M, eccentricity, maxError);
                
            }
            else
            {
                if (eccentricity < 0.8)
                    return solveEccentricAnomalyStd(M, eccentricity, maxError);
                return solveEccentricAnomalyExtremeEcc(M, eccentricity, maxIterations);
            }
        }
        private double solveEccentricAnomalyStd(double M, double ecc, double maxError = 1E-07)
        {
            double num1 = 1.0;
            double num2 = M + ecc * Math.Sin(M) + 0.5 * ecc * ecc * Math.Sin(2.0 * M);
            while (Math.Abs(num1) > maxError)
            {
                double num3 = num2 - ecc * Math.Sin(num2);
                num1 = (M - num3) / (1.0 - ecc * Math.Cos(num2));
                num2 += num1;
            }
            return num2;
        }
        
        private double solveEccentricAnomalyExtremeEcc(double M, double ecc, int iterations = 8)
        {
            try
            {
                double num1 = M + 0.85 * eccentricity * Math.Sign(Math.Sin(M));
                for (int index = 0; index < iterations; ++index)
                {
                    double num2 = ecc * Math.Sin(num1);
                    double num3 = ecc * Math.Cos(num1);
                    double num4 = num1 - num2 - M;
                    double num5 = 1.0 - num3;
                    double num6 = num2;
                    num1 += -5.0 * num4 / (num5 + Math.Sign(num5) * Math.Sqrt(Math.Abs(16.0 * num5 * num5 - 20.0 * num4 * num6)));
                }
                return num1;
            }
            catch (Exception ex)
            {
                if (!Thread.CurrentThread.IsBackground)
                {
                    Console.WriteLine(ex);
                }
                return double.NaN;
            }
        }
        
        public double GetTrueAnomaly(double E)
        {
            double trueAnomaly;
            if (eccentricity < 1.0)
            {

                double num1 = Math.Cos(E / 2.0);
                double num2 = Math.Sin(E / 2.0);
                trueAnomaly = 2.0 * Math.Atan2(Math.Sqrt(1.0 + eccentricity) * num2, Math.Sqrt(1.0 - eccentricity) * num1);
            }
            else if (double.IsPositiveInfinity(E))
            {
                
                trueAnomaly = Math.Acos(-1.0 / eccentricity);
            }
            else if (double.IsNegativeInfinity(E))
            {
                trueAnomaly = -Math.Acos(-1.0 / eccentricity);
            }
            else
            {
                double num3 = Math.Sinh(E / 2.0);
                double num4 = Math.Cosh(E / 2.0);
                trueAnomaly = 2.0 * Math.Atan2(Math.Sqrt(eccentricity + 1.0) * num3, Math.Sqrt(eccentricity - 1.0) * num4);
            }
            return trueAnomaly;
        }
        public double getOrbitalSpeedAtDistance(double d) => Math.Sqrt(Nu * (2.0 / d - 1.0 / semiMajorAxis));
        
        public DVector3 getRelativePositionAtT(double T) => getPositionFromTrueAnomaly(GetTrueAnomaly(solveEccentricAnomaly(T * meanMotion, eccentricity)));
        
        public DVector3 getPositionFromTrueAnomaly(double tA)
        {
            double num1 = Math.Cos(tA);
            double num2 = Math.Sin(tA);
            DVector3 r = semiLatusRectum / (1.0 + eccentricity * num1) * (_rotationMatrix.GetRight() * num1 + _rotationMatrix.GetForward() * num2);
            return r;
        }
        
        public double GetOrbitalStateVectorsAtObT(double ObT, out DVector3 pos, out DVector3 vel)
        {
            return this.GetOrbitalStateVectorsAtTrueAnomaly(this.TrueAnomalyAtT(ObT), out pos, out vel);
        }

        public double GetOrbitalStateVectorsAtTrueAnomaly(double tA, out DVector3 pos, out DVector3 vel)
        {
            double num1 = Math.Cos(tA);
            double num2 = Math.Sin(tA);
            double num3 = this.semiMajorAxis * (1.0 - this.eccentricity * this.eccentricity);
            double num4 = Math.Sqrt(Nu / num3);
            double num5 = -num2 * num4;
            double num6 = (num1 + this.eccentricity) * num4;
            double vectorsAtTrueAnomaly = num3 / (1.0 + this.eccentricity * num1);
            double num7 = num1 * vectorsAtTrueAnomaly;
            double num8 = num2 * vectorsAtTrueAnomaly;
            pos = _rotationMatrix.GetRight() * num7 + _rotationMatrix.GetForward() * num8;
            vel = _rotationMatrix.GetRight() * num5 + _rotationMatrix.GetForward() * num6;
            return vectorsAtTrueAnomaly;
        }

        /// <param name="v">speed counterclockwise in near point</param>
        /// <param name="r">minimal distance to parent</param>
        /// <param name="m">other mass</param>
        /// <param name="g">gravitational constant</param>
        /// <returns>Kepler orbit eccentricity</returns>
        public static double GetEccentricity(double v, double r, double m, double g)
        { 
            return (v * v * r) / (g * m) - 1;
        }
        
        public static double GetEccentricity(double r, double a)
        {
            return 1 - r / a;
        }

        /// <param name="e">eccentricity</param>
        /// <param name="r">minimal distance to parent</param>
        public static double GetSemiMajorAxis(double e, double r)
        {
            return r / (1 - e);
        }
        
        /// <param name="m">other mass</param>
        /// <param name="t">period</param>
        /// <param name="g">gravitational constant</param>
        public static double GetSemiMajorAxis(double m, double t, double g)
        {
            return Math.Pow((g * m * t*t) / (4 * Math.PI*Math.PI), 1.0 / 3);
        }
        
        public static double GetSemiMinorAxis(double e, double a)
        {
            return a * Math.Sqrt(1 - e * e);
        }

        /// <param name="a">large semi-axis</param>
        /// <param name="g">gravitational constant</param>
        /// <param name="m">parent mass</param>
        public static double GetPeriod(double a, double g, double m)
        {
            return 2 * Math.PI * Math.Sqrt(Math.Pow(a, 3) / (g * m)); //https://ru.wikipedia.org/wiki/%D0%9E%D1%80%D0%B1%D0%B8%D1%82%D0%B0%D0%BB%D1%8C%D0%BD%D1%8B%D0%B9_%D0%BF%D0%B5%D1%80%D0%B8%D0%BE%D0%B4
        }

        // Метод для расчета эксцентрической аномалии методом Ньютона
        public static double CalculateEccentricAnomalyByMean(double meanAnomaly, double e)
        {
            double value = meanAnomaly; // Начальное приближение

            // Точность для вычисления
            double epsilon = 1e-6;

            // Итеративный метод Ньютона
            while (true)
            {
                double delta = value - e * Math.Sin(value) - meanAnomaly;
                if (double.IsNaN(delta)) return 0;
                value -= delta / (1 - e * Math.Cos(value));

                if (Math.Abs(delta) < epsilon)
                    break;
            }

            return value;
        }
        
        public static double CalculateEccentricAnomalyByTrue(double trueAnomaly, double eccentricity, double tolerance = 1e-6, int maxIterations = 100)
        {
            double E = trueAnomaly; // Начальное приближение

            for (int i = 0; i < maxIterations; i++)
            {
                double deltaE = (E + eccentricity * Math.Sin(E) - trueAnomaly) / (1 + eccentricity * Math.Cos(E));
                E -= deltaE;

                if (Math.Abs(deltaE) < tolerance)
                {
                    return E;
                }
            }

            // Если не удалось достичь точности за заданное количество итераций, можно выбрасывать исключение или возвращать NaN
            throw new Exception("Не удалось найти эксцентрическую аномалию с заданной точностью.");
        }
        
        static double CalculateTrueAnomaly(double eccentricity, double eccentricAnomaly)
        {
            double trueAnomaly = 2 * Math.Atan2(Math.Sqrt(1 + eccentricity) * Math.Sin(eccentricAnomaly / 2), Math.Sqrt(1 - eccentricity) * Math.Cos(eccentricAnomaly / 2));
            return trueAnomaly;
        }

        public StaticTrajectory Clone()
        {
            return this.MemberwiseClone() as StaticTrajectory;
        }
    }
}
