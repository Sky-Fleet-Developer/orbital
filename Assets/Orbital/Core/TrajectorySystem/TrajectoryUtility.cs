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

        public static double GetOrbitalStateVectorsAtOrbitTime(this IStaticTrajectory tr, double orbitTime,
            out DVector3 pos, out DVector3 vel)
        {
            return tr.GetOrbitalStateVectorsAtTrueAnomaly(tr.TrueAnomalyAtT(orbitTime), out pos, out vel);
        }
        
        public static double GetOrbitalStateVectorsAtTrueAnomaly(this IStaticTrajectory tr, double trueAnomaly, out DVector3 pos, out DVector3 vel)
        {
            double num1 = Math.Cos(trueAnomaly);
            double num2 = Math.Sin(trueAnomaly);
            double num3 = tr.SemiMajorAxis * (1.0 - tr.Eccentricity * tr.Eccentricity);
            double num4 = Math.Sqrt(tr.Nu / num3);
            double num5 = -num2 * num4;
            double num6 = (num1 + tr.Eccentricity) * num4;
            double vectorsAtTrueAnomaly = num3 / (1.0 + tr.Eccentricity * num1);
            double num7 = num1 * vectorsAtTrueAnomaly;
            double num8 = num2 * vectorsAtTrueAnomaly;
            pos = tr.RotationMatrix.Right() * num7 + tr.RotationMatrix.Forward() * num8;
            vel = tr.RotationMatrix.Right() * num5 + tr.RotationMatrix.Forward() * num6;
            return vectorsAtTrueAnomaly;
        }

        #region MeanAnomaly
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
            double num1 = DVector3.Cross(tr.GetRelativePositionFromEccAnomaly(0), tr.GetOrbitalVelocityAtOrbitTime(0))
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
            double num = tr.SolveEccentricAnomaly(T * tr.MeanMotion, tr.Eccentricity);
            if (!double.IsNaN(num)) return tr.GetTrueAnomaly(num);

            return double.NaN;
        }

        #endregion

        #region EccentricAnomaly
        public static double GetEccentricAnomaly(this IStaticTrajectory tr, double tA)
        {
            double num1 = Math.Cos(tA / 2.0);
            double num2 = Math.Sin(tA / 2.0);
            double eccentricAnomaly;
            if (tr.Eccentricity < 1.0)
            {
                eccentricAnomaly = 2.0 * Math.Atan2(Math.Sqrt(1.0 - tr.Eccentricity) * num2,
                    Math.Sqrt(1.0 + tr.Eccentricity) * num1);
            }
            else
            {
                double num3 = Math.Sqrt((tr.Eccentricity - 1.0) / (tr.Eccentricity + 1.0)) * num2 / num1;
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
            if (double.IsInfinity(tr.MeanMotion)) return DVector3.Zero;
            return tr.GetPositionFromTrueAnomaly(
                tr.GetTrueAnomaly(tr.SolveEccentricAnomaly(T * tr.MeanMotion, tr.Eccentricity)));
        }

        public static DVector3 GetPositionFromMeanAnomaly(this IStaticTrajectory tr, double m) =>
            tr.GetPositionFromEccAnomaly(tr.SolveEccentricAnomaly(m, tr.Eccentricity, 1E-05));

        public static DVector3 GetPositionFromTrueAnomaly(this IStaticTrajectory tr, double trueAnomaly)
        {
            double cos = Math.Cos(trueAnomaly);
            double sin = Math.Sin(trueAnomaly);
            DVector3 r = tr.SemiLatusRectum / (1.0 + tr.Eccentricity * cos) *
                         (tr.RotationMatrix.Right() * cos + tr.RotationMatrix.Forward() * sin);
            return r;
        }

        public static DVector3 GetPositionFromEccAnomaly(this IStaticTrajectory tr, double E)
        {
            double right;
            double fwd;
            if (tr.Eccentricity < 1.0)
            {
                right = tr.SemiMajorAxis * (Math.Cos(E) - tr.Eccentricity);
                fwd = tr.SemiMajorAxis * Math.Sqrt(1.0 - tr.Eccentricity * tr.Eccentricity) * Math.Sin(E);
            }
            else if (tr.Eccentricity > 1.0)
            {
                right = -tr.SemiMajorAxis * (tr.Eccentricity - Math.Cosh(E));
                fwd = -tr.SemiMajorAxis * Math.Sqrt(tr.Eccentricity * tr.Eccentricity - 1.0) * Math.Sinh(E);
            }
            else
            {
                right = 0.0;
                fwd = 0.0;
            }

            return tr.RotationMatrix.Forward() * fwd + tr.RotationMatrix.Right() * right;
        }

        public static DVector3 GetRelativePositionFromEccAnomaly(this IStaticTrajectory tr, double E)
        {
            double fwd;
            double right;
            if (tr.Eccentricity < 1.0)
            {
                fwd = tr.SemiMajorAxis * (Math.Cos(E) - tr.Eccentricity);
                right = tr.SemiMajorAxis * Math.Sqrt(1.0 - tr.Eccentricity * tr.Eccentricity) * Math.Sin(E);
            }
            else if (tr.Eccentricity > 1.0)
            {
                fwd = -tr.SemiMajorAxis * (tr.Eccentricity - Math.Cosh(E));
                right = -tr.SemiMajorAxis * Math.Sqrt(tr.Eccentricity * tr.Eccentricity - 1.0) * Math.Sinh(E);
            }
            else
            {
                fwd = 0.0;
                right = 0.0;
            }

            return tr.RotationMatrix.Forward() * fwd + tr.RotationMatrix.Right() * right;
        }

        #endregion

        #region Velocity

        public static DVector3 GetOrbitalVelocityAtOrbitTime(this IStaticTrajectory tr, double orbitTime) =>
            tr.GetOrbitalVelocityAtTrueAnomaly(tr.TrueAnomalyAtT(orbitTime));

        public static DVector3 GetOrbitalVelocityAtTrueAnomaly(this IStaticTrajectory tr, double tA)
        {
            double num1 = Math.Cos(tA);
            double num2 = Math.Sin(tA);
            double num3 = Math.Sqrt(tr.Nu / (tr.SemiMajorAxis * (1.0 - tr.Eccentricity * tr.Eccentricity)));
            double fwd = -num2 * num3;
            double right = (num1 + tr.Eccentricity) * num3;
            return tr.RotationMatrix.Forward() * fwd + tr.RotationMatrix.Right() * right;
        }

        #endregion
    }
}