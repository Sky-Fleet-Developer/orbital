using System;
using System.Threading;
using Ara3D;
using Orbital.Core.Utilities;

namespace Orbital.Core.TrajectorySystem
{
    public static partial class TrajectoryUtility
    {
        public static double RadiusAtTrueAnomaly(this StaticOrbit tr, double tA) =>
            tr.SemiLatusRectum * (1.0 / (1.0 + tr.Eccentricity * Math.Cos(tA)));

        public static double GetOrbitalStateVectorsAtOrbitTime(this StaticOrbit tr, double orbitTime, out DVector3 pos, out DVector3 vel)
        {
            return tr.GetOrbitalStateVectorsAtTrueAnomaly(tr.TrueAnomalyAtT(orbitTime), out pos, out vel);
        }
        
        public static double GetOrbitalStateVectorsAtTrueAnomaly(this StaticOrbit tr, double trueAnomaly, out DVector3 pos, out DVector3 vel)
        {
            double cos = Math.Cos(trueAnomaly);
            double sin = Math.Sin(trueAnomaly);
            double minor = tr.SemiMajorAxis * (1.0 - tr.Eccentricity * tr.Eccentricity);
            double num4 = Math.Sqrt(tr.Nu / minor);
            double num5 = -sin * num4; //-
            double num6 = (cos + tr.Eccentricity) * num4;
            double vectorsAtTrueAnomaly = minor / (1.0 + tr.Eccentricity * cos);
            double cosM = cos * vectorsAtTrueAnomaly;
            double sinM = sin * vectorsAtTrueAnomaly;
            pos = tr.RotationMatrix.Forward() * cosM + tr.RotationMatrix.Right() * sinM;
            vel = tr.RotationMatrix.Forward() * num5 + tr.RotationMatrix.Right() * num6; //-
            return vectorsAtTrueAnomaly;
        }

        #region Time
        public static double TimeOfTrueAnomaly(this StaticOrbit tr, double trueAnomaly, double time) => tr.GetTimeAtMeanAnomaly(tr.GetMeanAnomaly(tr.GetEccentricAnomaly(trueAnomaly)), time);

        public static double GetTimeAtMeanAnomaly(this StaticOrbit tr, double meanAnomaly, double time)
        {
            double meanAnomalyAtUt = tr.GetMeanAnomalyAtTime(time);
            double angle = meanAnomaly - meanAnomalyAtUt;
            if (tr.Eccentricity < 1.0)
            {
                angle = MathUtilities.ClampRadiansTwoPI(angle);
            }
            return time + angle / tr.GetMeanMotion(tr.SemiMajorAxis);
        }
        #endregion

        #region MeanAnomaly
        public static double GetMeanAnomalyAtTime(this StaticOrbit tr, double time)
        {
            double angle = tr.GetMeanMotion(tr.SemiMajorAxis) * (time - tr.Epoch) + tr.MeanAnomalyAtEpoch;
            if (tr.Eccentricity < 1.0)
            {
                angle = MathUtilities.ClampRadiansTwoPI(angle);
            }
            return angle;
        }
        public static double GetMeanAnomaly(this StaticOrbit tr, double e)
        {
            if (tr.Eccentricity < 1.0)
            {
                return MathUtilities.ClampRadiansTwoPI(e - tr.Eccentricity * Math.Sin(e));
            }
            else
            {
                if (!double.IsInfinity(e)) return tr.Eccentricity * Math.Sinh(e) - e;
                return e;
            }
        }
        #endregion
        public static double GetMeanMotion(this StaticOrbit tr, double sma)
        {
            double num = Math.Abs(sma);
            return Math.Sqrt(tr.Nu / (num * num * num));
        }
        #region TrueAnomaly

        public static double GetTrueAnomaly(this StaticOrbit tr, double e)
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

        public static double TrueAnomalyAtRadiusSimple(this StaticOrbit tr, double r) =>
            Math.Acos(tr.SemiLatusRectum / (r * tr.Eccentricity) - 1.0 / tr.Eccentricity);

        public static double TrueAnomalyAtRadius(this StaticOrbit tr, double r, double t)
        {
            //double trueAnomaly = tr.GetTrueAnomaly(tr.SolveEccentricAnomaly((t - tr.Epoch) * tr.MeanMotion, tr.Eccentricity));
            //double ta = 
            var p = tr.GetPositionFromTrueAnomaly(0);
            var v = tr.GetOrbitalVelocityAtTrueAnomaly(0);
            double num1 = DVector3.Cross(p, v).LengthSquared() / tr.Nu;
            if (tr.Eccentricity < 1.0)
            {
                r = Math.Min(Math.Max(tr.Pericenter, r), tr.Apocenter);
            }
            else
                r = Math.Max(tr.Pericenter, r);

            double num2 = tr.Eccentricity * r;
            return Math.Acos(num1 / num2 - 1.0 / tr.Eccentricity);
        }

        public static double TrueAnomalyAtT(this StaticOrbit tr, double T)
        {
            double num = tr.SolveEccentricAnomaly((T - tr.Epoch) * tr.MeanMotion + tr.MeanAnomalyAtEpoch, tr.Eccentricity);
            if (!double.IsNaN(num)) return tr.GetTrueAnomaly(num);

            return double.NaN;
        }

        #endregion

        #region EccentricAnomaly
        public static double GetEccentricAnomaly(this StaticOrbit tr, double tA)
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
        /// <summary>
        /// Solves e*sinh(H) - H = m with Newton's method. Parabolic and hyperbolic orbits only (ecc &gt;= 1).
        /// </summary>
        private static double SolveEccentricAnomalyHyp(double m, double ecc, double maxError, int maxIterations)
        {
            if (double.IsInfinity(m) || double.IsNaN(m))
            {
                return m;
            }

            double h = Math.Asinh(2.0 * m / ecc);
            for (int i = 0; i < maxIterations; i++)
            {
                double step = (ecc * Math.Sinh(h) - h - m) / (ecc * Math.Cosh(h) - 1.0);
                h -= step;
                if (!(Math.Abs(step) > maxError * Math.Max(1.0, Math.Abs(h)))) break;
            }

            return h;
        }

        /// <summary>
        /// Solves Kepler's equation for the eccentric (elliptic) or hyperbolic anomaly.
        /// <paramref name="m"/> is the mean anomaly and may be any number of revolutions away from zero.
        /// Never loops more than <paramref name="maxIterations"/> times.
        /// </summary>
        public static double SolveEccentricAnomaly(this StaticOrbit tr, double m, double ecc,
            double maxError = 1E-13, int maxIterations = 50)
        {
            if (ecc >= 1.0)
            {
                return SolveEccentricAnomalyHyp(m, ecc, maxError, maxIterations);
            }

            // Kepler's equation is invariant under a shift of both M and E by 2*pi: solve inside one revolution.
            double revolutions = Math.Round(m / MathUtilities.TwoPI);
            double reduced = m - revolutions * MathUtilities.TwoPI;
            double solution = ecc < 0.8
                ? SolveEccentricAnomalyStd(reduced, ecc, maxError, maxIterations)
                : SolveEccentricAnomalyExtremeEcc(reduced, ecc, maxError, maxIterations);
            return solution + revolutions * MathUtilities.TwoPI;
        }

        private static double SolveEccentricAnomalyStd(double m, double ecc, double maxError, int maxIterations)
        {
            double ea = m + ecc * Math.Sin(m) + 0.5 * ecc * ecc * Math.Sin(2.0 * m);
            for (int i = 0; i < maxIterations; i++)
            {
                double step = (m - (ea - ecc * Math.Sin(ea))) / (1.0 - ecc * Math.Cos(ea));
                ea += step;
                if (!(Math.Abs(step) > maxError * Math.Max(1.0, Math.Abs(ea)))) break;
            }

            return ea;
        }

        private static double SolveEccentricAnomalyExtremeEcc(double m, double ecc, double maxError, int maxIterations)
        {
            double ea = m + 0.85 * ecc * Math.Sign(Math.Sin(m));
            for (int i = 0; i < maxIterations; i++)
            {
                double eSin = ecc * Math.Sin(ea);
                double eCos = ecc * Math.Cos(ea);
                double f = ea - eSin - m;
                double f1 = 1.0 - eCos;
                double step = 5.0 * f / (f1 + Math.Sign(f1) * Math.Sqrt(Math.Abs(16.0 * f1 * f1 - 20.0 * f * eSin)));
                ea -= step;
                if (!(Math.Abs(step) > maxError * Math.Max(1.0, Math.Abs(ea)))) break;
            }

            return ea;
        }

        #endregion

        #region Speed

        public static double GetOrbitalSpeedAtDistance(this StaticOrbit tr, double d) =>
            Math.Sqrt(tr.Nu * (2.0 / d - 1.0 / tr.SemiMajorAxis));

        public static double GetOrbitalSpeedAtPos(this StaticOrbit tr, DVector3 pos) =>
            tr.GetOrbitalSpeedAtDistance(pos.Length());

        #endregion

        #region Position

        public static DVector3 GetPositionAtT(this StaticOrbit tr, double T)
        {
            if (double.IsInfinity(tr.MeanMotion) || tr.Period == 0) return DVector3.Zero;
            return tr.GetPositionFromTrueAnomaly(tr.GetTrueAnomaly(tr.SolveEccentricAnomaly((T - tr.Epoch) * tr.MeanMotion + tr.MeanAnomalyAtEpoch, tr.Eccentricity)));
        }

        public static DVector3 GetPositionFromMeanAnomaly(this StaticOrbit tr, double m) =>
            tr.GetPositionFromEccAnomaly(tr.SolveEccentricAnomaly(m, tr.Eccentricity, 1E-05));

        public static DVector3 GetPositionFromTrueAnomaly(this StaticOrbit tr, double trueAnomaly)
        {
            double cos = Math.Cos(trueAnomaly);
            double sin = Math.Sin(trueAnomaly);
            DVector3 r = tr.SemiLatusRectum / (1.0 + tr.Eccentricity * cos) * (tr.RotationMatrix.Forward() * cos + tr.RotationMatrix.Right() * sin);
            return r;
        }

        public static DVector3 GetPositionFromEccAnomaly(this StaticOrbit tr, double E)
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

        public static DVector3 GetOrbitalVelocityAtOrbitTime(this StaticOrbit tr, double orbitTime) =>
            tr.GetOrbitalVelocityAtTrueAnomaly(tr.TrueAnomalyAtT(orbitTime));

        public static DVector3 GetOrbitalVelocityAtTrueAnomaly(this StaticOrbit tr, double tA)
        {
            double cos = Math.Cos(tA);
            double sin = Math.Sin(tA);
            double num3 = Math.Sqrt(tr.Nu / (tr.SemiMajorAxis * (1.0 - tr.Eccentricity * tr.Eccentricity)));
            double fwd = -sin * num3; //-
            double right = (cos + tr.Eccentricity) * num3;
            return tr.RotationMatrix.Forward() * fwd + tr.RotationMatrix.Right() * right; //-
        }

        #endregion
    }
}