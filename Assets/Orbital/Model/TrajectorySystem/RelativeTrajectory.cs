using System;
using System.Threading.Tasks;
using Ara3D;
using Ara3D.Double;
using Orbital.Model.SystemComponents;
using UnityEngine;

namespace Orbital.Model.TrajectorySystem
{
    public enum SystemType
    {
        SingleCenter = 0,
        DoubleSystem = 1,
        RigidBody = 2
    }
    
    public class RelativeTrajectory
    {
        public const double Deg2Rad = 0.01745329;
        public const double Rad2Deg = 57.29578;
        
        private IMass _other;
        private ITrajectorySettingsHolder _self;
        public double Eccentricity { get; private set; }
        public double SemiMajorAxis { get; private set; }
        public double SemiMinorAxis { get; private set; }
        public double PericenterRadius { get; private set; }
        public double Period { get; private set; }
        public double TimeShift { get; private set; }
        public bool IsZero { get; private set; }

        private DMatrix4x4 _rotationMatrix;
        private SystemType _systemType;

        public DMatrix4x4 RotationMatrix => _rotationMatrix;

        public RelativeTrajectory(ITrajectorySettingsHolder self, IMass other, SystemType systemType)
        {
            _self = self;
            _other = other;
            _systemType = systemType;
            _rotationMatrix = DMatrix4x4.Identity;
        }
        
        public void Calculate()
        {
            TrajectorySettings settings = _self.Settings;
            TimeShift = settings.timeShift * 0.01f;
            switch (_systemType)
            {
                case SystemType.SingleCenter: case SystemType.RigidBody:
                    CalculateForSingleCenter(ref settings);
                    break;
                case SystemType.DoubleSystem:
                    CalculateForDoubleSystem(ref settings);
                    break;
            }
        }
        

        private void CalculateForSingleCenter(ref TrajectorySettings settings)
        {
            PericenterRadius = settings.pericenterRadius;
            Eccentricity = GetEccentricity(settings.pericenterSpeed, settings.pericenterRadius, _other.Mass, MassUtility.G);
            SemiMajorAxis = GetSemiMajorAxis(Eccentricity, settings.pericenterRadius);
            SemiMinorAxis = GetSemiMinorAxis(Eccentricity, SemiMajorAxis);
            Period = GetPeriod(SemiMajorAxis, MassUtility.G, _other.Mass);
            IsZero = Period is 0 or double.NaN;
            if (!IsZero)
            {
                CreateRotation(ref settings);
            }
        }

        private void CalculateForDoubleSystem(ref TrajectorySettings settings)
        {
            Period = settings.period;
            PericenterRadius = settings.pericenterRadius;
            SemiMajorAxis = GetSemiMajorAxis(_other.Mass, Period, MassUtility.G);
            Eccentricity = GetEccentricity(PericenterRadius, SemiMajorAxis);
            SemiMinorAxis = GetSemiMinorAxis(Eccentricity, SemiMajorAxis);
            IsZero = Period is 0 or double.NaN;
            if (!IsZero)
            {
                CreateRotation(ref settings);
            }
        }
        
        private static AsyncThreadScheduler _calculationScheduler = new AsyncThreadScheduler(3);
        
        public async Task SetupFromSimulation(DVector3 position, DVector3 velocity)
        {
            double r = position.Length();
            DVector3 hh = DVector3.Cross(position, velocity);
            double h = hh.Length();
            //double i = Math.Acos(hh.y / h);
            double vSqr = velocity.LengthSquared();
            // semiMajorAxis
            SemiMajorAxis = 1 / (2 / r - vSqr / (MassUtility.G * _other.Mass));
            Eccentricity = Math.Sqrt(1 - (h * h / (MassUtility.G * _other.Mass * SemiMajorAxis)));
            SemiMinorAxis = GetSemiMinorAxis(Eccentricity, SemiMajorAxis);
            Period = GetPeriod(SemiMajorAxis, MassUtility.G, _other.Mass);
            DVector3? pericenter = await _calculationScheduler.Schedule(() => IterativeSimulation.FindPericenter(velocity, position, SemiMajorAxis, _other.Mass, 1, out _, out _));

            _rotationMatrix = DMatrix4x4.LookRotation(pericenter.Value, hh);
            PericenterRadius = pericenter.Value.Length();
            DVector3 flatPos = _rotationMatrix.GetInverse() * position;

            //true anomaly
            double u = Math.Atan2(flatPos.x, flatPos.z);

            // eccentric anomaly
            double E = CalculateEccentricAnomalyByTrue(u, Eccentricity);

            //mean anomaly
            double M = E - Eccentricity * Math.Sin(E);

            TimeShift = -M / (2 * Math.PI);

            /*Debug.Log($"Расстояние (r): {r} m");
            Debug.Log($"Большая полуось (a): {SemiMajorAxis} m");
            Debug.Log($"Эксцентриситет (e): {Eccentricity}");
            Debug.Log($"Перицентр : {pericenter}");*/
        }

        public void UpdateSettings(ref TrajectorySettings settings)
        {
            settings.pericenterRadius = (float)PericenterRadius;
            settings.period = (float)Period;
            DVector3 fwd = _rotationMatrix * new DVector3(0, 0, 1);
            DVector3 up = _rotationMatrix * new DVector3(0, 0, 1);
            DVector3 right = DVector3.Cross(fwd, up);
            settings.longitudeShift = (float)(Math.Atan2(fwd.x, fwd.z) * Rad2Deg);
            Debug.Log("longitudeShift:" + settings.longitudeShift);
            settings.latitudeShift = (float)(Math.Asin(fwd.y) * Rad2Deg);
            Debug.Log("latitudeShift:" + settings.latitudeShift);
            settings.inclination = (float)(Math.Asin(right.x) * Rad2Deg);
            Debug.Log("inclination:" + settings.inclination);
            settings.timeShift = (float)(TimeShift * 100);
            Debug.Log("timeShift:" + settings.timeShift);
        }

        private void CreateRotation(ref TrajectorySettings settings)
        {
            _rotationMatrix = DMatrix4x4.CreateRotation(settings.latitudeShift * Deg2Rad, settings.longitudeShift * Deg2Rad, 0) * DMatrix4x4.CreateRotation(0, 0, settings.inclination * Deg2Rad);
        }

        public DVector3 GetPosition(double t)
        {
            return TransformByShift(GetFlatPosition(t));
        }

        public DVector3 GetFlatPosition(double t)
        {
            if (IsZero)
            {
                return new DVector3(0, 0, 0);
            }
            // Средняя аномалия в момент времени t
            double meanAnomaly = 2 * Math.PI * (t / Period + TimeShift);

            // Решение уравнения Кеплера для нахождения эксцентрической аномалии E
            double eccentricAnomaly = CalculateEccentricAnomalyByMean(meanAnomaly, Eccentricity);

            // Вычисление координат (x, y)
            double z = SemiMajorAxis * (Math.Cos(eccentricAnomaly) - Eccentricity);
            double x = SemiMinorAxis * Math.Sin(eccentricAnomaly);
            return new DVector3((float)x, 0, (float)z);
        }
        
        public DVector3 GetVelocity(double t)
        {
            return TransformByShift(GetFlatVelocity(t));
        }
        
        public DVector3 GetFlatVelocity(double t)
        {
            if (IsZero)
            {
                return new DVector3(0, 0, 0);
            }
            /*double M = 2 * Math.PI * (t / Period + TimeShift);
            double eccentricAnomaly = CalculateEccentricAnomaly(M, Eccentricity);
            double v = 2 * Math.Atan(Math.Sqrt((1 + Eccentricity) / (1 - Eccentricity)) * Math.Tan(eccentricAnomaly / 2));
            
            // Рассчитываем расстояние от спутника до центрального тела
            double r = SemiMajorAxis * (1 - Eccentricity * Math.Cos(eccentricAnomaly));

            // Производные радиус-вектора и эксцентрической аномалии по времени
            double vr = Math.Sqrt(MassUtility.G * _other.Mass / SemiMinorAxis) * Eccentricity * Math.Sin(v);
            double vn = Math.Sqrt(MassUtility.G * _other.Mass / SemiMinorAxis) * (1 + Eccentricity * Math.Cos(v));

            return new DVector3(vr * Math.Sin(v) + vn * Math.Cos(v), 0, vr * Math.Cos(v) - vn * Math.Sin(v));*/

            DVector3 a = GetFlatPosition(t - 1);
            DVector3 b = GetFlatPosition(t + 1);
            return (b - a) * 0.5;
        }

        public DVector3 TransformByShift(DVector3 vector)
        {
            return _rotationMatrix * vector;
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

        public RelativeTrajectory Clone()
        {
            return this.MemberwiseClone() as RelativeTrajectory;
        }
    }
}
