using System;
using Ara3D;
using Ara3D.Double;
using Orbital.Model.Services;
using Orbital.Model.SystemComponents;
using UnityEngine;

namespace Orbital.Model.TrajectorySystem
{
    public enum SystemType
    {
        SingleCenter = 0,
        DoubleSystem = 1
    }
    
    public class RelativeTrajectory
    {
        public const double Deg2Rad = 0.01745329;
        public const double Rad2Deg = 57.29578;
        
        private IMass _other;
        private IMass _self;
        private DVector3[] _path;
        public double Eccentricity { get; private set; }
        public double SemiMajorAxis { get; private set; }
        public double SemiMinorAxis { get; private set; }
        public double PericenterRadius { get; private set; }
        public double Period { get; private set; }
        public double LatitudeShift { get; private set; }
        public double LongitudeShift { get; private set; }
        public double Inclination { get; private set; }
        public double TimeShift { get; private set; }
        public bool IsZero { get; private set; }

        private DMatrix4x4 _rotationMatrix;
        private SystemType _systemType;

        public RelativeTrajectory(IMass self, IMass other, SystemType systemType)
        {
            _self = self;
            _other = other;
            _systemType = systemType;
        }
        
        public void Calculate()
        {
            CelestialSettings settings = _self.Settings;
            LatitudeShift = settings.latitudeShift * Deg2Rad;
            LongitudeShift = settings.longitudeShift * Deg2Rad;
            Inclination = settings.inclination * Deg2Rad;
            TimeShift = settings.timeShift * 0.01f;
            switch (_systemType)
            {
                case SystemType.SingleCenter:
                    CalculateForSingleCenter(ref settings);
                    break;
                case SystemType.DoubleSystem:
                    CalculateForDoubleSystem(ref settings);
                    break;
            }
        }

        private void CalculateForSingleCenter(ref CelestialSettings settings)
        {
            PericenterRadius = settings.pericenterRadius;
            Eccentricity = GetEccentricity(settings.pericenterSpeed, settings.pericenterRadius, _other.Mass, OrbitCalculationService.G);
            SemiMajorAxis = GetSemiMajorAxis(Eccentricity, settings.pericenterRadius);
            SemiMinorAxis = GetSemiMinorAxis(Eccentricity, SemiMajorAxis);
            Period = GetPeriod(SemiMajorAxis, OrbitCalculationService.G, _other.Mass + _self.Mass);
            IsZero = Period is 0 or double.NaN;
            if (!IsZero)
            {
                _rotationMatrix = DMatrix4x4.CreateRotation(LatitudeShift, LongitudeShift, 0) * DMatrix4x4.CreateRotation(0, 0, Inclination);
            }
        }

        private void CalculateForDoubleSystem(ref CelestialSettings settings)
        {
            Period = settings.period;
            PericenterRadius = settings.pericenterRadius;
            SemiMajorAxis = GetSemiMajorAxis(_other.Mass, Period, OrbitCalculationService.G);
            Eccentricity = GetEccentricity(PericenterRadius, SemiMajorAxis);
            SemiMinorAxis = GetSemiMinorAxis(Eccentricity, SemiMajorAxis);
            IsZero = Period is 0 or double.NaN;
            if (!IsZero)
            {
                _rotationMatrix = DMatrix4x4.CreateRotation(LatitudeShift, LongitudeShift, 0) *
                                  DMatrix4x4.CreateRotation(0, 0, Inclination);
            }
        }

        public DVector3 GetPosition(double t)
        {
            if(IsZero) return DVector3.Zero;
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
            double eccentricAnomaly = CalculateEccentricAnomaly(meanAnomaly, Eccentricity);

            // Вычисление координат (x, y)
            double z = SemiMajorAxis * (Math.Cos(eccentricAnomaly) - Eccentricity);
            double x = SemiMinorAxis * Math.Sin(eccentricAnomaly);
            return new DVector3((float)x, 0, (float)z);
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
            return Math.Pow((g * m * Math.Pow(t, 2)) / (4 * Math.Pow(Math.PI, 2)), 1.0 / 3);
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
        public static double CalculateEccentricAnomaly(double meanAnomaly, double e)
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
    }
}
