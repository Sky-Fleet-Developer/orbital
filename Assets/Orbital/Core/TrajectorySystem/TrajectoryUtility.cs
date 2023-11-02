using System;
using System.Threading;
using Ara3D;
using Orbital.Core.KSPSource;

namespace Orbital.Core.TrajectorySystem
{
    public static class TrajectoryUtility
    {
        public static DVector3 GetGlobalPosition(this TreeContainer tree, IMassSystem massSystem, double time)
        {
            DVector3 result = DVector3.Zero;
            IMassSystem cache = massSystem;
            while (cache != tree.Root)
            {
                result += tree._trajectories[cache].GetSample(time, true, false).position;
                tree._parents.TryGetValue(cache, out cache);
            }

            return result;
        }

        public static DVector3 GetGlobalPosition(this TreeContainer tree, IStaticBody staticBody, double time)
        {
            return tree.GetGlobalPosition(tree._massPerComponent[staticBody], time);
        }


        public static double RadiusAtTrueAnomaly(this IStaticTrajectory tr, double tA) =>
            tr.SemiLatusRectum * (1.0 / (1.0 + tr.Eccentricity * Math.Cos(tA)));

        public static double GetOrbitalStateVectorsAtOrbitTime(this IStaticTrajectory tr, double orbitTime, out DVector3 pos, out DVector3 vel)
        {
            return tr.GetOrbitalStateVectorsAtTrueAnomaly(tr.TrueAnomalyAtT(orbitTime), out pos, out vel);
        }
        
        public static double GetOrbitalStateVectorsAtTrueAnomaly(this IStaticTrajectory tr, double trueAnomaly, out DVector3 pos, out DVector3 vel)
        {
            double cos = Math.Cos(trueAnomaly);
            double sin = Math.Sin(trueAnomaly);
            double minor = tr.SemiMajorAxis * (1.0 - tr.Eccentricity * tr.Eccentricity);
            double num4 = Math.Sqrt(tr.Nu / minor);
            double num5 = -sin * num4;
            double num6 = (cos + tr.Eccentricity) * num4;
            double vectorsAtTrueAnomaly = minor / (1.0 + tr.Eccentricity * cos);
            double cosM = cos * vectorsAtTrueAnomaly;
            double sinM = sin * vectorsAtTrueAnomaly;
            pos = tr.RotationMatrix.Forward() * cosM + tr.RotationMatrix.Right() * sinM;
            vel = tr.RotationMatrix.Forward() * num5 + tr.RotationMatrix.Right() * num6;
            return vectorsAtTrueAnomaly;
        }

        #region Time
        public static double TimeOfTrueAnomaly(this IStaticTrajectory tr, double trueAnomaly, double time) => tr.GetTimeAtMeanAnomaly(tr.GetMeanAnomaly(tr.GetEccentricAnomaly(trueAnomaly)), time);

        public static double GetTimeAtMeanAnomaly(this IStaticTrajectory tr, double meanAnomaly, double time)
        {
            double meanAnomalyAtUt = tr.GetMeanAnomalyAtTime(time);
            double angle = meanAnomaly - meanAnomalyAtUt;
            if (tr.Eccentricity < 1.0)
            {
                angle = UtilMath.ClampRadiansTwoPI(angle);
            }
            return time + angle / tr.GetMeanMotion(tr.SemiMajorAxis);
        }
        #endregion

        #region MeanAnomaly
        public static double GetMeanAnomalyAtTime(this IStaticTrajectory tr, double time)
        {
            double angle = tr.MeanAnomalyAtEpoch + tr.GetMeanMotion(tr.SemiMajorAxis) * (time - tr.Epoch);
            if (tr.Eccentricity < 1.0)
            {
                angle = UtilMath.ClampRadiansTwoPI(angle);
            }
            return angle;
        }
        public static double GetMeanAnomaly(this IStaticTrajectory tr, double e)
        {
            if (tr.Eccentricity < 1.0)
            {
                return UtilMath.ClampRadiansTwoPI(e - tr.Eccentricity * Math.Sin(e));
            }
            else
            {
                if (!double.IsInfinity(e)) return tr.Eccentricity * Math.Sinh(e) - e;
                return e;
            }
        }
        #endregion
        public static double GetMeanMotion(this IStaticTrajectory tr, double sma)
        {
            double num = Math.Abs(sma);
            return Math.Sqrt(tr.Nu / (num * num * num));
        }
        #region TrueAnomaly

        public static double GetTrueAnomaly(this IStaticTrajectory tr, double e)
        {
            double trueAnomaly;
            if (tr.Eccentricity < 1.0)
            {
                double num1 = Math.Cos(e / 2.0);
                double num2 = Math.Sin(e / 2.0);
                trueAnomaly = 2.0 * Math.Atan2(Math.Sqrt(1.0 + tr.Eccentricity) * num2,
                    Math.Sqrt(1.0 - tr.Eccentricity) * num1);
            }
            else if (double.IsPositiveInfinity(e))
            {
                trueAnomaly = Math.Acos(-1.0 / tr.Eccentricity);
            }
            else if (double.IsNegativeInfinity(e))
            {
                trueAnomaly = -Math.Acos(-1.0 / tr.Eccentricity);
            }
            else
            {
                double num3 = Math.Sinh(e / 2.0);
                double num4 = Math.Cosh(e / 2.0);
                trueAnomaly = 2.0 * Math.Atan2(Math.Sqrt(tr.Eccentricity + 1.0) * num3,
                    Math.Sqrt(tr.Eccentricity - 1.0) * num4);
            }

            return trueAnomaly;
        }

        internal static double TrueAnomalyAtRadiusSimple(this IStaticTrajectory tr, double r) =>
            Math.Acos(tr.SemiLatusRectum / (r * tr.Eccentricity) - 1.0 / tr.Eccentricity);

        public static double TrueAnomalyAtRadius(this IStaticTrajectory tr, double r)
        {
            double num1 = DVector3.Cross(tr.GetPositionFromEccAnomaly(0), tr.GetOrbitalVelocityAtOrbitTime(0))
                .LengthSquared() / tr.Nu;
            if (tr.Eccentricity < 1.0)
            {
                r = Math.Min(Math.Max(tr.Pericenter, r), tr.Apocenter);
            }
            else
                r = Math.Max(tr.Pericenter, r);

            double num2 = tr.Eccentricity * r;
            return Math.Acos(num1 / num2 - 1.0 / tr.Eccentricity);
        }

        public static double TrueAnomalyAtT(this IStaticTrajectory tr, double T)
        {
            double num = tr.SolveEccentricAnomaly((T - tr.Epoch) * tr.MeanMotion - tr.MeanAnomalyAtEpoch, tr.Eccentricity);
            if (!double.IsNaN(num)) return tr.GetTrueAnomaly(num);

            return double.NaN;
        }

        #endregion

        #region EccentricAnomaly
        public static double GetEccentricAnomaly(this IStaticTrajectory tr, double tA)
        {
            double cos = Math.Cos(tA / 2.0);
            double sin = Math.Sin(tA / 2.0);
            double eccentricAnomaly;
            if (tr.Eccentricity < 1.0)
            {
                eccentricAnomaly = 2.0 * Math.Atan2(Math.Sqrt(1.0 - tr.Eccentricity) * sin,
                    Math.Sqrt(1.0 + tr.Eccentricity) * cos);
            }
            else
            {
                double num3 = Math.Sqrt((tr.Eccentricity - 1.0) / (tr.Eccentricity + 1.0)) * sin / cos;
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
        private static double SolveEccentricAnomalyHyp(this IStaticTrajectory tr, double m, double ecc,
            double maxError = 1E-07)
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
                    num1 = (tr.Eccentricity * Math.Sinh(num3) - num3 - m) / (tr.Eccentricity * Math.Cosh(num3) - 1.0);
                    num3 -= num1;
                }

                return num3;
            }
        }

        public static double SolveEccentricAnomaly(this IStaticTrajectory tr, double m, double ecc,
            double maxError = 1E-07, int maxIterations = 8)
        {
            if (tr.Eccentricity >= 1.0)
            {
                return tr.SolveEccentricAnomalyHyp(m, tr.Eccentricity, maxError);
            }
            else
            {
                if (tr.Eccentricity < 0.8) return SolveEccentricAnomalyStd(m, tr.Eccentricity, maxError);
                return tr.SolveEccentricAnomalyExtremeEcc(m, tr.Eccentricity, maxIterations);
            }
        }

        private static double SolveEccentricAnomalyStd(double m, double ecc, double maxError = 1E-07)
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

        private static double SolveEccentricAnomalyExtremeEcc(this IStaticTrajectory tr, double m, double ecc,
            int iterations = 8)
        {
            try
            {
                double num1 = m + 0.85 * tr.Eccentricity * Math.Sign(Math.Sin(m));
                for (int index = 0; index < iterations; ++index)
                {
                    double num2 = ecc * Math.Sin(num1);
                    double num3 = ecc * Math.Cos(num1);
                    double num4 = num1 - num2 - m;
                    double num5 = 1.0 - num3;
                    double num6 = num2;
                    num1 += -5.0 * num4 /
                            (num5 + Math.Sign(num5) * Math.Sqrt(Math.Abs(16.0 * num5 * num5 - 20.0 * num4 * num6)));
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

        #endregion

        #region Speed

        public static double GetOrbitalSpeedAtDistance(this IStaticTrajectory tr, double d) =>
            Math.Sqrt(tr.Nu * (2.0 / d - 1.0 / tr.SemiMajorAxis));

        public static double GetOrbitalSpeedAtPos(this IStaticTrajectory tr, DVector3 pos) =>
            tr.GetOrbitalSpeedAtDistance(pos.Length());

        #endregion

        #region Position

        public static DVector3 GetPositionAtT(this IStaticTrajectory tr, double T)
        {
            if (tr == null || double.IsInfinity(tr.MeanMotion)) return DVector3.Zero;
            return tr.GetPositionFromTrueAnomaly(tr.GetTrueAnomaly(tr.SolveEccentricAnomaly((T - tr.Epoch) * tr.MeanMotion - tr.MeanAnomalyAtEpoch, tr.Eccentricity)));
        }

        public static DVector3 GetPositionFromMeanAnomaly(this IStaticTrajectory tr, double m) =>
            tr.GetPositionFromEccAnomaly(tr.SolveEccentricAnomaly(m, tr.Eccentricity, 1E-05));

        public static DVector3 GetPositionFromTrueAnomaly(this IStaticTrajectory tr, double trueAnomaly)
        {
            double cos = Math.Cos(trueAnomaly);
            double sin = Math.Sin(trueAnomaly);
            DVector3 r = tr.SemiLatusRectum / (1.0 + tr.Eccentricity * cos) * (tr.RotationMatrix.Forward() * cos + tr.RotationMatrix.Right() * sin);
            return r;
        }

        public static DVector3 GetPositionFromEccAnomaly(this IStaticTrajectory tr, double E)
        {
            double cos;
            double sin;
            if (tr.Eccentricity < 1.0)
            {
                cos = tr.SemiMajorAxis * (Math.Cos(E) - tr.Eccentricity);
                sin = tr.SemiMajorAxis * Math.Sqrt(1.0 - tr.Eccentricity * tr.Eccentricity) * Math.Sin(E);
            }
            else if (tr.Eccentricity > 1.0)
            {
                cos = -tr.SemiMajorAxis * (tr.Eccentricity - Math.Cosh(E));
                sin = -tr.SemiMajorAxis * Math.Sqrt(tr.Eccentricity * tr.Eccentricity - 1.0) * Math.Sinh(E);
            }
            else
            {
                cos = 0.0;
                sin = 0.0;
            }

            return tr.RotationMatrix.Right() * sin + tr.RotationMatrix.Forward() * cos;
        }

        #endregion

        #region Velocity

        public static DVector3 GetOrbitalVelocityAtOrbitTime(this IStaticTrajectory tr, double orbitTime) =>
            tr.GetOrbitalVelocityAtTrueAnomaly(tr.TrueAnomalyAtT(orbitTime));

        public static DVector3 GetOrbitalVelocityAtTrueAnomaly(this IStaticTrajectory tr, double tA)
        {
            double cos = Math.Cos(tA);
            double sin = Math.Sin(tA);
            double num3 = Math.Sqrt(tr.Nu / (tr.SemiMajorAxis * (1.0 - tr.Eccentricity * tr.Eccentricity)));
            double fwd = -sin * num3;
            double right = (cos + tr.Eccentricity) * num3;
            return tr.RotationMatrix.Forward() * fwd + tr.RotationMatrix.Right() * right;
        }

        #endregion
    }
}