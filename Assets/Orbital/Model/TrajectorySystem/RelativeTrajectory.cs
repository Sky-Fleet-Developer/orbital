using System;
using Ara3D;
using Orbital.Model.Components;
using Orbital.Model.Services;
using UnityEngine;

namespace Orbital.Model.TrajectorySystem
{
    public class RelativeTrajectory
    {
        public const double Deg2Rad = 0.01745329;
        public const double Rad2Deg = 57.29578;
        
        private IMass _parent;
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
        public double PeriodShift { get; private set; }
        public bool IsZero { get; private set; }

        public RelativeTrajectory(IMass self, IMass parent)
        {
            _self = self;
            _parent = parent;
        }
        
        public void Calculate()
        {
            CelestialSettings settings = _self.Settings;
            LatitudeShift = settings.latitudeShift * Deg2Rad;
            LongitudeShift = settings.longitudeShift * Deg2Rad;
            Inclination = settings.inclination * Deg2Rad;
            PeriodShift = settings.periodShift * 0.01f;
            PericenterRadius = settings.pericenterRadius;
            Eccentricity = GetEccentricity(settings.pericenterSpeed, settings.pericenterRadius, _parent.Mass - _self.Mass, OrbitCalculationService.G);
            SemiMajorAxis = GetSemiMajorAxis(Eccentricity, settings.pericenterRadius);
            SemiMinorAxis = GetSemiMinorAxis(Eccentricity, SemiMajorAxis);
            Period = GetPeriod(SemiMajorAxis, OrbitCalculationService.G, _parent.Mass);
            IsZero = Period == 0 || double.IsNaN(Period);
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
            double meanAnomaly = 2 * Math.PI * t / Period + PeriodShift;

            // Решение уравнения Кеплера для нахождения эксцентрической аномалии E
            double eccentricAnomaly = CalculateEccentricAnomaly(meanAnomaly, Eccentricity);

            // Вычисление координат (x, y)
            double x = SemiMajorAxis * (Math.Cos(eccentricAnomaly) - Eccentricity);
            double y = SemiMajorAxis * Math.Sqrt(1 - Eccentricity * Eccentricity) * Math.Sin(eccentricAnomaly);
            return new DVector3((float)x, 0, (float)y);
        }

        public DVector3 TransformByShift(DVector3 vector)
        {
            double x1 = vector.x * Math.Cos(LongitudeShift) - vector.y * Math.Sin(LongitudeShift);
            double y1 = vector.x * Math.Sin(LongitudeShift) + vector.y * Math.Cos(LongitudeShift);
            double z1 = vector.z;

            double x2 = x1 * Math.Cos(LatitudeShift) - z1 * Math.Sin(LatitudeShift);
            double y2 = y1;
            double z2 = x1 * Math.Sin(LatitudeShift) + z1 * Math.Cos(LatitudeShift);

            return new DVector3(x2, y2, z2);
        }

        /// <param name="v">speed counterclockwise in near point</param>
        /// <param name="r">minimal distance to parent</param>
        /// <param name="m">parent mass</param>
        /// <param name="g">gravitational constant</param>
        /// <returns>Kepler orbit eccentricity</returns>
        public static double GetEccentricity(double v, double r, double m, double g)
        { 
            return (v * v * r) / (g * m) - 1; //search for eccentricity
        }

        /// <param name="e">eccentricity</param>
        /// <param name="r">minimal distance to parent</param>
        /// <returns>large semi-axis for Kepler orbit</returns>
        public static double GetSemiMajorAxis(double e, double r)
        {
            return r / (1 - e);
        }
        
        public static double GetSemiMinorAxis(double e, double a)
        {
            return a * Math.Sqrt(1 - e * e);
        }

        /// <param name="a">large semi-axis</param>
        /// <param name="g">gravitational constant</param>
        /// <param name="m">parent mass</param>
        /// <returns></returns>
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
                value -= delta / (1 - e * Math.Cos(value));

                if (Math.Abs(delta) < epsilon)
                    break;
            }

            return value;
        }
    }
}
