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
        private const double Deg2Rad = 0.01745329;
        private const double Rad2Deg = 57.29578;
        
        private IMass _other;
        
        public double Inclination {get; private set;}
        public double LongitudeAscendingNode {get; private set;}
        public double ArgumentOfPeriapsis {get; private set;}
        public double Eccentricity {get; private set;}
        public double SemiMajorAxis {get; private set;}
        public double Epoch {get; private set;}
        public double Period {get; private set;}
        public double MeanAnomaly {get; private set;}
        public double OrbitalEnergy {get; private set;}
        public double TrueAnomaly {get; private set;}
        public double EccentricAnomaly {get; private set;}
        public double Radius {get; private set;}
        public double Altitude {get; private set;}
        public double OrbitPercent {get; private set;}
        public double OrbitTime {get; private set;}
        public double OrbitTimeAtEpoch {get; private set;}
        public double TimeToPe {get; private set;}
        public double TimeToAp {get; private set;}
        public DVector3 H {get; private set;}
        public DVector3 EccVec {get; private set;}
        public DVector3 An {get; private set;}
        public double MeanAnomalyAtEpoch {get; private set;}
        public double MeanMotion {get; private set;}
        public event Action WasChangedHandler;
        
        private DMatrix4x4 _rotationMatrix;
        public DMatrix4x4 RotationMatrix => _rotationMatrix;

        public double SemiMinorAxis
        {
            get
            {
                if (Eccentricity < 1.0)
                {
                    return SemiMajorAxis * Math.Sqrt(1.0 - Eccentricity * Eccentricity);
                }

                return SemiMajorAxis * Math.Sqrt(Eccentricity * Eccentricity - 1.0);
            }
        }

        private double Nu => _other.Mass * MassUtility.G;
        public double SemiLatusRectum => H.LengthSquared() / Nu;
        public double PeR => (1.0 - Eccentricity) * SemiMajorAxis;

        public double ApR => (1.0 + Eccentricity) * SemiMajorAxis;
        
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
            _rotationMatrix = GetRotation(Inclination, LongitudeAscendingNode, ArgumentOfPeriapsis);
            An = DVector3.Cross(DVector3.up, _rotationMatrix.Up());
            if (An.LengthSquared() == 0.0)
            {
                An = DVector3.right;
            }

            EccVec = _rotationMatrix.Right() * Eccentricity;
            double d = Nu * SemiMajorAxis * (1.0 - Eccentricity * Eccentricity);
            H = _rotationMatrix.Up() * Math.Sqrt(d);
            MeanMotion = GetMeanMotion(SemiMajorAxis);
            MeanAnomaly = MeanAnomalyAtEpoch;
            OrbitTime = MeanAnomaly / MeanMotion;
            OrbitTimeAtEpoch = OrbitTime;
            if (Eccentricity < 1.0)
            {
                Period = 2.0 * Math.PI / MeanMotion;
                OrbitPercent = MeanAnomaly / (2.0 * Math.PI);
            }
            else
            {
                Period = double.PositiveInfinity;
                OrbitPercent = 0.0;
            }
            WasChangedHandler?.Invoke();
        }
        
        public void SetOrbit(double inclination,
            double eccentricity,
            double semiMajorAxis,
            double longitudeAscendingNode,
            double argumentOfPeriapsis,
            double meanAnomalyAtEpoch,
            double epoch)
        {
            Inclination = inclination;
            Eccentricity = eccentricity;
            SemiMajorAxis = semiMajorAxis;
            LongitudeAscendingNode = longitudeAscendingNode;
            ArgumentOfPeriapsis = argumentOfPeriapsis;
            MeanAnomalyAtEpoch = meanAnomalyAtEpoch;
            Epoch = epoch;
            Init();
        }
        
        public void Calculate(TrajectorySettings settings)
        {
            SetOrbit(settings.inclination * Deg2Rad, settings.eccentricity, settings.semiMajorAxis, settings.longitudeAscendingNode * Deg2Rad, settings.argumentOfPeriapsis * Deg2Rad, 0, settings.timeShift);
        }
        
        public void Calculate(DVector3 position, DVector3 velocity)
        {
            UpdateFromFixedVectors(position, velocity);
            if (!double.IsNaN(ArgumentOfPeriapsis))
                return;
            
            DVector3 lhs = Quaternion.AngleAxis(-(float) LongitudeAscendingNode, Vector3.up) * Vector3.right;
            DVector3 xzy = EccVec.XZY;
            double d = DVector3.Dot(lhs, xzy) / (lhs.Length() * xzy.Length());
            if (d > 1.0)
            {
                ArgumentOfPeriapsis = 0.0;
            }
            else if (d < -1.0)
            {
                ArgumentOfPeriapsis = 180.0;
            }
            else
            {
                ArgumentOfPeriapsis = Math.Acos(d);
            }
        }


        public void UpdateFromFixedVectors(DVector3 position, DVector3 velocity)
        {
            DVector3 fwd, up = _rotationMatrix.Up(), right;
            H = DVector3.Cross(position, velocity);
            if (H.LengthSquared().Equals(0.0))
            {
                Inclination = Math.Acos(position.z / position.Length()) * (180.0 / Math.PI);
                An = DVector3.Cross(position, DVector3.up);
            }
            else
            {
                An = DVector3.Cross(DVector3.up, H);
                up = H / H.Length();
                Inclination = UtilMath.AngleBetween(up, DVector3.up) * (180.0 / Math.PI);
            }

            if (An.LengthSquared().Equals(0.0))
            {
                An = DVector3.right;
            }

            LongitudeAscendingNode = Math.Atan2(An.y, An.x) * (180.0 / Math.PI);
            LongitudeAscendingNode = (LongitudeAscendingNode + 360.0) % 360.0;
            EccVec = (DVector3.Dot(velocity, velocity) / Nu - 1.0 / position.Length()) * position -
                     DVector3.Dot(position, velocity) * velocity / Nu;
            Eccentricity = EccVec.Length();
            OrbitalEnergy = velocity.LengthSquared() / 2.0 - Nu / position.Length();
            double num;
            if (Eccentricity >= 1.0)
            {
                num = -SemiLatusRectum / (EccVec.LengthSquared() - 1.0);
            }
            else
                num = -Nu / (2.0 * OrbitalEnergy);

            SemiMajorAxis = num;
            if (Eccentricity.Equals(0.0))
            {
                right = An.Normalize();
                ArgumentOfPeriapsis = 0.0;
            }
            else
            {
                right = EccVec.Normalize();
                ArgumentOfPeriapsis = UtilMath.AngleBetween(An, right);
                if (DVector3.Dot(DVector3.Cross(An, right), H) < 0.0)
                {
                    ArgumentOfPeriapsis = 2.0 * Math.PI - ArgumentOfPeriapsis;
                }
            }

            if (H.LengthSquared().Equals(0.0))
            {
                fwd = An.Normalize();
                up = DVector3.Cross(right, fwd);
            }
            else
                fwd = DVector3.Cross(up, right);

            ArgumentOfPeriapsis *= 180.0 / Math.PI;
            MeanMotion = GetMeanMotion(SemiMajorAxis);
            double x = DVector3.Dot(position, right);
            TrueAnomaly = Math.Atan2(DVector3.Dot(position, fwd), x);
            EccentricAnomaly = GetEccentricAnomaly(TrueAnomaly);
            MeanAnomaly = GetMeanAnomaly(EccentricAnomaly);
            MeanAnomalyAtEpoch = MeanAnomaly;
            OrbitTime = MeanAnomaly / MeanMotion;
            OrbitTimeAtEpoch = OrbitTime;
            if (Eccentricity < 1.0)
            {
                Period = 2.0 * Math.PI / MeanMotion;
                OrbitPercent = MeanAnomaly / (2.0 * Math.PI);
                OrbitPercent = (OrbitPercent + 1.0) % 1.0;
                TimeToPe = (Period - OrbitTime) % Period;
                TimeToAp = TimeToPe - Period / 2.0;
                if (TimeToAp < 0.0)
                {
                    TimeToAp += Period;
                }
            }
            else
            {
                Period = double.PositiveInfinity;
                OrbitPercent = 0.0;
                TimeToPe = -OrbitTime;
                TimeToAp = double.PositiveInfinity;
            }

            Radius = position.Length();
            //altitude = radius - _other.Radius;
            _rotationMatrix = DMatrix4x4.LookRotation(fwd, up);
            WasChangedHandler?.Invoke();
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
            if (Eccentricity < 1.0)
            {
                eccentricAnomaly = 2.0 * Math.Atan2(Math.Sqrt(1.0 - Eccentricity) * num2,
                    Math.Sqrt(1.0 + Eccentricity) * num1);
            }
            else
            {
                double num3 = Math.Sqrt((Eccentricity - 1.0) / (Eccentricity + 1.0)) * num2 / num1;
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

        public double GetMeanAnomaly(double e)
        {
            if (Eccentricity < 1.0)
            {
                return UtilMath.ClampRadiansTwoPI(e - Eccentricity * Math.Sin(e));
            }
            else
            {
                if (!double.IsInfinity(e)) return Eccentricity * Math.Sinh(e) - e;
                return e;
            }
        }

        public (DVector3 position, DVector3 velocity) GetSample(double time, bool positionRequired = true, bool velocityRequired = true)
        {
            DVector3 pos, vel;
            if (double.IsInfinity(MeanMotion))
            {
                pos = DVector3.Zero;
                vel = DVector3.Zero;
            }
            else
            {
                GetOrbitalStateVectorsAtOrbitTime(time, out pos, out vel);
            }

            return (pos, vel); //(positionRequired ? GetPosition(time) : DVector3.Zero, velocityRequired ? GetVelocity(time) : DVector3.Zero);
        }
        
        public double TrueAnomalyAtT(double T)
        {
            double num = SolveEccentricAnomaly(T * MeanMotion, Eccentricity);
            if (!double.IsNaN(num)) return GetTrueAnomaly(num);

            return double.NaN;
        }
        
               private double SolveEccentricAnomalyHyp(double m, double ecc, double maxError = 1E-07)
        {
            if (double.IsInfinity(m))
            {
                return m;
            }
            else
            {
                double num1 = 1.0;
                double num2 = 2.0 * m / ecc;
                double num3 = Math.Log(Math.Sqrt(num2 * num2 + 1.0) + num2);
                while (Math.Abs(num1) > maxError)
                {
                    num1 = (Eccentricity * Math.Sinh(num3) - num3 - m) / (Eccentricity * Math.Cosh(num3) - 1.0);
                    num3 -= num1;
                }
                return num3;
            }
        }

        public double SolveEccentricAnomaly(double m, double ecc, double maxError = 1E-07, int maxIterations = 8)
        {
            if (Eccentricity >= 1.0)
            {

                        return SolveEccentricAnomalyHyp(m, Eccentricity, maxError);
                
            }
            else
            {
                if (Eccentricity < 0.8)
                    return SolveEccentricAnomalyStd(m, Eccentricity, maxError);
                return SolveEccentricAnomalyExtremeEcc(m, Eccentricity, maxIterations);
            }
        }
        private double SolveEccentricAnomalyStd(double m, double ecc, double maxError = 1E-07)
        {
            double num1 = 1.0;
            double num2 = m + ecc * Math.Sin(m) + 0.5 * ecc * ecc * Math.Sin(2.0 * m);
            while (Math.Abs(num1) > maxError)
            {
                double num3 = num2 - ecc * Math.Sin(num2);
                num1 = (m - num3) / (1.0 - ecc * Math.Cos(num2));
                num2 += num1;
            }
            return num2;
        }
        
        private double SolveEccentricAnomalyExtremeEcc(double m, double ecc, int iterations = 8)
        {
            try
            {
                double num1 = m + 0.85 * Eccentricity * Math.Sign(Math.Sin(m));
                for (int index = 0; index < iterations; ++index)
                {
                    double num2 = ecc * Math.Sin(num1);
                    double num3 = ecc * Math.Cos(num1);
                    double num4 = num1 - num2 - m;
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
        
        public double GetTrueAnomaly(double e)
        {
            double trueAnomaly;
            if (Eccentricity < 1.0)
            {

                double num1 = Math.Cos(e / 2.0);
                double num2 = Math.Sin(e / 2.0);
                trueAnomaly = 2.0 * Math.Atan2(Math.Sqrt(1.0 + Eccentricity) * num2, Math.Sqrt(1.0 - Eccentricity) * num1);
            }
            else if (double.IsPositiveInfinity(e))
            {
                
                trueAnomaly = Math.Acos(-1.0 / Eccentricity);
            }
            else if (double.IsNegativeInfinity(e))
            {
                trueAnomaly = -Math.Acos(-1.0 / Eccentricity);
            }
            else
            {
                double num3 = Math.Sinh(e / 2.0);
                double num4 = Math.Cosh(e / 2.0);
                trueAnomaly = 2.0 * Math.Atan2(Math.Sqrt(Eccentricity + 1.0) * num3, Math.Sqrt(Eccentricity - 1.0) * num4);
            }
            return trueAnomaly;
        }
        public double GetOrbitalSpeedAtDistance(double d) => Math.Sqrt(Nu * (2.0 / d - 1.0 / SemiMajorAxis));
        public double GetOrbitalSpeedAtPos(DVector3 pos) => GetOrbitalSpeedAtDistance(pos.Length());

        public DVector3 GetPositionAtT(double T) => GetPositionFromTrueAnomaly(GetTrueAnomaly(SolveEccentricAnomaly(T * MeanMotion, Eccentricity)));
        public DVector3 GetPositionFromMeanAnomaly(double m) => this.GetPositionFromEccAnomaly(SolveEccentricAnomaly(m, Eccentricity, 1E-05));

        public DVector3 GetPositionFromTrueAnomaly(double trueAnomaly)
        {
            double num1 = Math.Cos(trueAnomaly);
            double num2 = Math.Sin(trueAnomaly);
            DVector3 r = SemiLatusRectum / (1.0 + Eccentricity * num1) * (_rotationMatrix.Right() * num1 + _rotationMatrix.Forward() * num2);
            return r;
        }

        public DVector3 GetPositionFromEccAnomaly(double E)
        {
            double right;
            double fwd;
            if (this.Eccentricity < 1.0)
            {
                right = this.SemiMajorAxis * (Math.Cos(E) - this.Eccentricity);
                fwd = this.SemiMajorAxis * Math.Sqrt(1.0 - this.Eccentricity * this.Eccentricity) * Math.Sin(E);
            }
            else if (this.Eccentricity > 1.0)
            {
                right = -this.SemiMajorAxis * (this.Eccentricity - Math.Cosh(E));
                fwd = -this.SemiMajorAxis * Math.Sqrt(this.Eccentricity * this.Eccentricity - 1.0) * Math.Sinh(E);
            }
            else
            {
                right = 0.0;
                fwd = 0.0;
            }

            return _rotationMatrix.Forward() * fwd + _rotationMatrix.Right() * right;
        }

        public double GetOrbitalStateVectorsAtOrbitTime(double orbitTime, out DVector3 pos, out DVector3 vel)
        {
            return GetOrbitalStateVectorsAtTrueAnomaly(TrueAnomalyAtT(orbitTime), out pos, out vel);
        }

        public double GetOrbitalStateVectorsAtTrueAnomaly(double tA, out DVector3 pos, out DVector3 vel)
        {
            double num1 = Math.Cos(tA);
            double num2 = Math.Sin(tA);
            double num3 = SemiMajorAxis * (1.0 - Eccentricity * Eccentricity);
            double num4 = Math.Sqrt(Nu / num3);
            double num5 = -num2 * num4;
            double num6 = (num1 + Eccentricity) * num4;
            double vectorsAtTrueAnomaly = num3 / (1.0 + Eccentricity * num1);
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
            double e = trueAnomaly; // Начальное приближение

            for (int i = 0; i < maxIterations; i++)
            {
                double deltaE = (e + eccentricity * Math.Sin(e) - trueAnomaly) / (1 + eccentricity * Math.Cos(e));
                e -= deltaE;

                if (Math.Abs(deltaE) < tolerance)
                {
                    return e;
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

         public void DrawGizmos()
        {
            int drawResolution = 20;
            Color color = Color.black;
            float scale = 1.470588E-07F;
            
            if (Eccentricity < 1.0)
            {
                for (double num = 0.0; num < 2.0 * Math.PI; num += drawResolution * (Math.PI / 180.0))
                {
                    Vector3 positionFromTrueAnomaly1 = GetPositionFromTrueAnomaly(num % (2.0 * Math.PI));
                    Vector3 positionFromTrueAnomaly2 = GetPositionFromTrueAnomaly((num + drawResolution * (Math.PI / 180.0)) % (2.0 * Math.PI));
                    if (color == Color.black)
                    {
                        Debug.DrawLine(positionFromTrueAnomaly1 * scale, positionFromTrueAnomaly2 * scale, Color.Lerp(Color.yellow, Color.green, Mathf.InverseLerp((float) GetOrbitalSpeedAtDistance(PeR), (float) GetOrbitalSpeedAtDistance(ApR), (float) GetOrbitalSpeedAtPos(positionFromTrueAnomaly1))));
                    }
                    else
                    {
                        Debug.DrawLine(positionFromTrueAnomaly1 * scale, positionFromTrueAnomaly2 * scale, color);
                    }
                }
            }
            else
            {
                for (double tA = -Math.Acos(-(1.0 / Eccentricity)) + drawResolution * (Math.PI / 180.0); tA < Math.Acos(-(1.0 / Eccentricity)) - drawResolution * (Math.PI / 180.0); tA += drawResolution * (Math.PI / 180.0))
                {
                    if (color == Color.black)
                    {
                        Debug.DrawLine(GetPositionFromTrueAnomaly(tA) * scale, GetPositionFromTrueAnomaly(Math.Min(Math.Acos(-(1.0 / Eccentricity)), tA + drawResolution * (Math.PI / 180.0))) * scale, Color.green);
                    }
                    else
                        Debug.DrawLine(GetPositionFromTrueAnomaly(tA) * scale, GetPositionFromTrueAnomaly(Math.Min(Math.Acos(-(1.0 / Eccentricity)), tA + drawResolution * (Math.PI / 180.0))) * scale, color);
                }
            }

            Debug.DrawLine(GetPositionAtT(OrbitTime) * scale, Vector3.zero, Color.green);
            //Debug.DrawRay(getRelativePositionAtT(orbitTime)), new DVector3(vel.x, vel.z, vel.y) * 0.0099999997764825821, Color.white);
            Debug.DrawLine(Vector3.zero, (An.XZY * Radius) * scale, Color.cyan);
            Debug.DrawLine(Vector3.zero, GetPositionAtT(0.0) * scale, Color.magenta);
            Debug.DrawRay(Vector3.zero, H.XZY * scale, Color.blue);
        }
        
        public StaticTrajectory Clone()
        {
            return MemberwiseClone() as StaticTrajectory;
        }
    }
}
