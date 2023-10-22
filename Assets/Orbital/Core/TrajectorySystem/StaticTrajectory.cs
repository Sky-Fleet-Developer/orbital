using System;
using System.Threading;
using Ara3D;
using Ara3D.Double;
using Orbital.Core.KSPSource;
using UnityEngine;

namespace Orbital.Core.TrajectorySystem
{
    public class StaticTrajectory : ITrajectorySampler, IStaticTrajectory
    {
        public const double Deg2Rad = 0.01745329;
        public const double Rad2Deg = 57.29578;
        
        private IMass _other;
        
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

        public StaticTrajectory(IMass other)
        {
            _other = other;
            _rotationMatrix = DMatrix4x4.Identity;
        }

        public void Init()
        {
            _rotationMatrix = GetRotation(inclination, longitudeAscendingNode, argumentOfPeriapsis);
            an = DVector3.Cross(DVector3.up, _rotationMatrix.Up());
            if (an.LengthSquared() == 0.0)
            {
                an = DVector3.right;
            }

            eccVec = _rotationMatrix.Right() * eccentricity;
            double d = Nu * semiMajorAxis * (1.0 - eccentricity * eccentricity);
            h = _rotationMatrix.Up() * Math.Sqrt(d);
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
        
        public void Calculate(TrajectorySettings settings)
        {
            SetOrbit(settings.inclination * Deg2Rad, settings.eccentricity, settings.semiMajorAxis, settings.longitudeAscendingNode * Deg2Rad, settings.argumentOfPeriapsis * Deg2Rad, 0, settings.timeShift);
        }
        
        public void Calculate(DVector3 position, DVector3 velocity)
        {
            UpdateFromFixedVectors(position, velocity);
            if (!double.IsNaN(argumentOfPeriapsis))
                return;
            
            DVector3 lhs = Quaternion.AngleAxis(-(float) longitudeAscendingNode, Vector3.up) * Vector3.right;
            DVector3 xzy = eccVec.XZY;
            double d = DVector3.Dot(lhs, xzy) / (lhs.Length() * xzy.Length());
            if (d > 1.0)
            {
                argumentOfPeriapsis = 0.0;
            }
            else if (d < -1.0)
            {
                argumentOfPeriapsis = 180.0;
            }
            else
            {
                argumentOfPeriapsis = Math.Acos(d);
            }
        }

        public void DrawGizmos()
        {
            int drawResolution = 20;
            Color color = Color.black;
            float scale = 1.470588E-07F;
            
            if (eccentricity < 1.0)
            {
                for (double num = 0.0; num < 2.0 * Math.PI; num += drawResolution * (Math.PI / 180.0))
                {
                    Vector3 positionFromTrueAnomaly1 = getPositionFromTrueAnomaly(num % (2.0 * Math.PI));
                    Vector3 positionFromTrueAnomaly2 = getPositionFromTrueAnomaly((num + drawResolution * (Math.PI / 180.0)) % (2.0 * Math.PI));
                    if (color == Color.black)
                    {
                        Debug.DrawLine(positionFromTrueAnomaly1 * scale, positionFromTrueAnomaly2 * scale, Color.Lerp(Color.yellow, Color.green, Mathf.InverseLerp((float) getOrbitalSpeedAtDistance(PeR), (float) getOrbitalSpeedAtDistance(ApR), (float) getOrbitalSpeedAtPos(positionFromTrueAnomaly1))));
                    }
                    else
                    {
                        Debug.DrawLine(positionFromTrueAnomaly1 * scale, positionFromTrueAnomaly2 * scale, color);
                    }
                }
            }
            else
            {
                for (double tA = -Math.Acos(-(1.0 / eccentricity)) + drawResolution * (Math.PI / 180.0); tA < Math.Acos(-(1.0 / eccentricity)) - drawResolution * (Math.PI / 180.0); tA += drawResolution * (Math.PI / 180.0))
                {
                    if (color == Color.black)
                    {
                        Debug.DrawLine(getPositionFromTrueAnomaly(tA) * scale, getPositionFromTrueAnomaly(Math.Min(Math.Acos(-(1.0 / eccentricity)), tA + drawResolution * (Math.PI / 180.0))) * scale, Color.green);
                    }
                    else
                        Debug.DrawLine(getPositionFromTrueAnomaly(tA) * scale, getPositionFromTrueAnomaly(Math.Min(Math.Acos(-(1.0 / eccentricity)), tA + drawResolution * (Math.PI / 180.0))) * scale, color);
                }
            }

            Debug.DrawLine(getRelativePositionAtT(orbitTime) * scale, Vector3.zero, Color.green);
            //Debug.DrawRay(getRelativePositionAtT(orbitTime)), new DVector3(vel.x, vel.z, vel.y) * 0.0099999997764825821, Color.white);
            Debug.DrawLine(Vector3.zero, (an.XZY * radius) * scale, Color.cyan);
            Debug.DrawLine(Vector3.zero, getRelativePositionAtT(0.0) * scale, Color.magenta);
            Debug.DrawRay(Vector3.zero, h.XZY * scale, Color.blue);
        }


        public void UpdateFromFixedVectors(DVector3 position, DVector3 velocity)
        {
            DVector3 fwd, up = _rotationMatrix.Up(), right;
            h = DVector3.Cross(position, velocity);
            if (h.LengthSquared().Equals(0.0))
            {
                inclination = Math.Acos(position.z / position.Length()) * (180.0 / Math.PI);
                an = DVector3.Cross(position, DVector3.up);
            }
            else
            {
                an = DVector3.Cross(DVector3.up, h);
                up = h / h.Length();
                inclination = UtilMath.AngleBetween(up, DVector3.up) * (180.0 / Math.PI);
            }

            if (an.LengthSquared().Equals(0.0))
            {
                an = DVector3.right;
            }

            longitudeAscendingNode = Math.Atan2(an.y, an.x) * (180.0 / Math.PI);
            longitudeAscendingNode = (longitudeAscendingNode + 360.0) % 360.0;
            eccVec = (DVector3.Dot(velocity, velocity) / Nu - 1.0 / position.Length()) * position -
                     DVector3.Dot(position, velocity) * velocity / Nu;
            eccentricity = eccVec.Length();
            orbitalEnergy = velocity.LengthSquared() / 2.0 - Nu / position.Length();
            double num;
            if (eccentricity >= 1.0)
            {
                num = -semiLatusRectum / (eccVec.LengthSquared() - 1.0);
            }
            else
                num = -Nu / (2.0 * orbitalEnergy);

            semiMajorAxis = num;
            if (eccentricity.Equals(0.0))
            {
                right = an.Normalize();
                argumentOfPeriapsis = 0.0;
            }
            else
            {
                right = eccVec.Normalize();
                argumentOfPeriapsis = UtilMath.AngleBetween(an, right);
                if (DVector3.Dot(DVector3.Cross(an, right), h) < 0.0)
                {
                    argumentOfPeriapsis = 2.0 * Math.PI - argumentOfPeriapsis;
                }
            }

            if (h.LengthSquared().Equals(0.0))
            {
                fwd = an.Normalize();
                up = DVector3.Cross(right, fwd);
            }
            else
                fwd = DVector3.Cross(up, right);

            argumentOfPeriapsis *= 180.0 / Math.PI;
            meanMotion = GetMeanMotion(semiMajorAxis);
            double x = DVector3.Dot(position, right);
            trueAnomaly = Math.Atan2(DVector3.Dot(position, fwd), x);
            eccentricAnomaly = GetEccentricAnomaly(trueAnomaly);
            meanAnomaly = GetMeanAnomaly(eccentricAnomaly);
            meanAnomalyAtEpoch = meanAnomaly;
            orbitTime = meanAnomaly / meanMotion;
            OrbitTimeAtEpoch = orbitTime;
            if (eccentricity < 1.0)
            {
                period = 2.0 * Math.PI / meanMotion;
                orbitPercent = meanAnomaly / (2.0 * Math.PI);
                orbitPercent = (orbitPercent + 1.0) % 1.0;
                timeToPe = (period - orbitTime) % period;
                timeToAp = timeToPe - period / 2.0;
                if (timeToAp < 0.0)
                {
                    timeToAp += period;
                }
            }
            else
            {
                period = double.PositiveInfinity;
                orbitPercent = 0.0;
                timeToPe = -orbitTime;
                timeToAp = double.PositiveInfinity;
            }

            radius = position.Length();
            //altitude = radius - _other.Radius;
            _rotationMatrix = DMatrix4x4.LookRotation(fwd, up);
            //debugH = h;
            //debugAN = an;
            //debugEccVec = eccVec;
            //OrbitFrameX = right;
            //OrbitFrameY = fwd;
            //OrbitFrameZ = up;
        }

        public static DMatrix4x4 GetRotation(double inclination, double longitudeAscendingNode, double argumentOfPeriapsis)
        {
            DMatrix4x4 lan = DMatrix4x4.CreateRotation(0, longitudeAscendingNode, 0);
            DMatrix4x4 inc = DMatrix4x4.CreateRotation(0, 0, inclination);
            DMatrix4x4 aop = DMatrix4x4.CreateRotation(0, argumentOfPeriapsis, 0);

            return aop * inc * lan;
        }

        public double GetEccentricAnomaly(double tA)
        {
            double num1 = Math.Cos(tA / 2.0);
            double num2 = Math.Sin(tA / 2.0);
            double eccentricAnomaly;
            if (eccentricity < 1.0)
            {
                eccentricAnomaly = 2.0 * Math.Atan2(Math.Sqrt(1.0 - eccentricity) * num2,
                    Math.Sqrt(1.0 + eccentricity) * num1);
            }
            else
            {
                double num3 = Math.Sqrt((eccentricity - 1.0) / (eccentricity + 1.0)) * num2 / num1;
                if (num3 >= 1.0)
                {
                    eccentricAnomaly = double.PositiveInfinity;
                }
                else if (num3 <= -1.0)
                {
                    eccentricAnomaly = double.NegativeInfinity;
                }
                else
                    eccentricAnomaly = Math.Log((1.0 + num3) / (1.0 - num3));
            }

            return eccentricAnomaly;
        }

        public double GetMeanAnomaly(double E)
        {
            if (eccentricity < 1.0)
            {
                return UtilMath.ClampRadiansTwoPI(E - eccentricity * Math.Sin(E));
            }
            else
            {
                if (!double.IsInfinity(E)) return eccentricity * Math.Sinh(E) - E;
                return E;
            }
        }

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
                GetOrbitalStateVectorsAtorbitTime(time, out pos, out vel);
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
        public double getOrbitalSpeedAtPos(DVector3 pos) => getOrbitalSpeedAtDistance(pos.Length());

        public DVector3 getRelativePositionAtT(double T) => getPositionFromTrueAnomaly(GetTrueAnomaly(solveEccentricAnomaly(T * meanMotion, eccentricity)));
        
        public DVector3 getPositionFromTrueAnomaly(double tA)
        {
            double num1 = Math.Cos(tA);
            double num2 = Math.Sin(tA);
            DVector3 r = semiLatusRectum / (1.0 + eccentricity * num1) * (_rotationMatrix.Right() * num1 + _rotationMatrix.Forward() * num2);
            return r;
        }
        
        public double GetOrbitalStateVectorsAtorbitTime(double orbitTime, out DVector3 pos, out DVector3 vel)
        {
            return this.GetOrbitalStateVectorsAtTrueAnomaly(this.TrueAnomalyAtT(orbitTime), out pos, out vel);
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
            pos = _rotationMatrix.Right() * num7 + _rotationMatrix.Forward() * num8;
            vel = _rotationMatrix.Right() * num5 + _rotationMatrix.Forward() * num6;
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
