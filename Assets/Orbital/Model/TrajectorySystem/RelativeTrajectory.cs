using System;
using Ara3D;
using Orbital.Model.Services;
using UnityEngine;

namespace Orbital.Model.TrajectorySystem
{
    public class RelativeTrajectory
    {
        private IMass _parent;
        private IMass _self;
        private DVector3[] _path;
        private double _eccentricity;
        private double _largeSemiAxis;
        private double _period;
        private double _latitudeShift;
        private double _longitudeShift;

        /// <param name="pericenterSpeed">speed for counterclockwise in near point</param>
        /// <param name="pericenterRadius">minimal distance to parent</param>
        /// <param name="latitudeShift">offset in degrees by X axis</param>
        /// <param name="longitudeShift">offset in degrees by Y axis</param>
        public void Calculate(double pericenterSpeed, double pericenterRadius, double latitudeShift, double longitudeShift)
        {
            _latitudeShift = latitudeShift;
            _longitudeShift = longitudeShift;
            _eccentricity = GetEccentricity(pericenterSpeed, pericenterRadius, _parent.Mass, OrbitCalculationService.G);
            _largeSemiAxis = GetLargeSemiAxis(_eccentricity, pericenterRadius);
            _period = GetPeriod(_largeSemiAxis, OrbitCalculationService.G, _parent.Mass);
        }
        
        public DVector3 GetFlatPosition(double t)
        {
            // Средняя аномалия в момент времени t
            double meanAnomaly = 2 * Math.PI * t / _period;

            // Решение уравнения Кеплера для нахождения эксцентрической аномалии E
            double eccentricAnomaly = CalculateEccentricAnomaly(meanAnomaly, _eccentricity);

            // Вычисление координат (x, y)
            double x = _largeSemiAxis * (Math.Cos(eccentricAnomaly) - _eccentricity);
            double y = _largeSemiAxis * Math.Sqrt(1 - _eccentricity * _eccentricity) * Math.Sin(eccentricAnomaly);
            return new DVector3((float)x, 0, (float)y);
        }

        public DVector3 TransformByShift(DVector3 vector)
        {
            double x1 = vector.x * Math.Cos(_longitudeShift) - vector.y * Math.Sin(_longitudeShift);
            double y1 = vector.x * Math.Sin(_longitudeShift) + vector.y * Math.Cos(_longitudeShift);
            double z1 = vector.z;

            double x2 = x1 * Math.Cos(_latitudeShift) - z1 * Math.Sin(_latitudeShift);
            double y2 = y1;
            double z2 = x1 * Math.Sin(_latitudeShift) + z1 * Math.Cos(_latitudeShift);

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
        public static double GetLargeSemiAxis(double e, double r)
        {
            return r / (1 - e);
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
