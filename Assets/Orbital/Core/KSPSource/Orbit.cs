using System;
using System.Threading;
using Ara3D;
using UnityEngine;

namespace Orbital.Core.KSPSource
{
    [Serializable]
    public class Orbit
    {
        private IStaticBody referenceBody;
        public double eccentricity;
        public double semiMajorAxis;
        public double inclination;
        public double longitudeAscendingNode;
        public double argumentOfPeriapsis;
        public double epoch;
        public const double Rad2Deg = 57.295779513082323;
        public const double Deg2Rad = 0.017453292519943295;
        public Planetarium.CelestialFrame OrbitFrame;
        public DVector3 pos;
        public DVector3 vel;
        public double orbitalEnergy;
        public double meanAnomaly;
        public double trueAnomaly;
        public double eccentricAnomaly;
        public double radius;
        public double altitude;
        public double orbitalSpeed;
        public double orbitPercent;
        public double ObT;
        public double ObTAtEpoch;
        public double timeToPe;
        public double timeToAp;
        public DVector3 h;
        public DVector3 eccVec;
        public DVector3 an;
        public double meanAnomalyAtEpoch;
        public double meanMotion;
        public double period;
        public Vector3 OrbitFrameX;
        public Vector3 OrbitFrameY;
        public Vector3 OrbitFrameZ;
        public Vector3 debugPos;
        public Vector3 debugVel;
        public Vector3 debugH;
        public Vector3 debugAN;
        public Vector3 debugEccVec;
        public double mag;
        private double drawResolution = 15.0;
        public int numClosePoints;
        public double FEVp;
        public double FEVs;
        public double SEVp;
        public double SEVs;
        public double UTappr;
        public double UTsoi;
        public double ClAppr;
        public double CrAppr;
        public double ClEctr1;
        public double ClEctr2;
        public double timeToTransition1;
        public double timeToTransition2;
        public double nearestTT;
        public double nextTT;
        public DVector3 secondaryPosAtTransition1;
        public DVector3 secondaryPosAtTransition2;
        public double closestTgtApprUT;
        public double StartUT;
        public double EndUT;
        public bool activePatch;
        public Orbit closestEncounterPatch;
        public IStaticBody closestEncounterBody;

        /* public static Orbit.FindClosestPointsDelegate FindClosestPoints =
             new Orbit.FindClosestPointsDelegate(Orbit._FindClosestPoints);
 
         public static Orbit.SolveClosestApproachDelegate SolveClosestApproach =
             new Orbit.SolveClosestApproachDelegate(Orbit._SolveClosestApproach);
 
         public static Orbit.SolveSOI_Delegate SolveSOI = new Orbit.SolveSOI_Delegate(Orbit._SolveSOI);
         public static Orbit.SolveSOI_BSPDelegate SolveSOI_BSP = new Orbit.SolveSOI_BSPDelegate(Orbit._SolveSOI_BSP);
         public Orbit.EncounterSolutionLevel closestEncounterLevel;
         public Orbit.PatchTransitionType patchStartTransition;
         public Orbit.PatchTransitionType patchEndTransition;*/
        public Orbit nextPatch;
        public Orbit previousPatch;
        public double fromE;
        public double toE;
        public double sampleInterval;
        public double E;
        public double V;
        public double fromV;
        public double toV;
        public bool debug_returnFullEllipseTrajectory;

        public Orbit()
        {
        }

        public Orbit(
            double inc,
            double e,
            double sma,
            double lan,
            double argPe,
            double mEp,
            double t,
            IStaticBody body)
        {
            SetOrbit(inc, e, sma, lan, argPe, mEp, t, body);
        }

        public Orbit(Orbit orbit)
        {
            inclination = orbit.inclination;
            eccentricity = orbit.eccentricity;
            semiMajorAxis = orbit.semiMajorAxis;
            longitudeAscendingNode = orbit.longitudeAscendingNode;
            argumentOfPeriapsis = orbit.argumentOfPeriapsis;
            meanAnomalyAtEpoch = orbit.meanAnomalyAtEpoch;
            epoch = orbit.epoch;
            referenceBody = orbit.referenceBody;
            Init();
        }

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

        public double semiLatusRectum => h.LengthSquared() / referenceBody.gravParameter;
        public double PeR => (1.0 - eccentricity) * semiMajorAxis;

        public double ApR => (1.0 + eccentricity) * semiMajorAxis;

        public double PeA => PeR - referenceBody.Radius;

        public double ApA => ApR - referenceBody.Radius;

        public double GetMeanMotion(double sma)
        {
            double num = Math.Abs(sma);
            return Math.Sqrt(referenceBody.gravParameter / (num * num * num));
        }

        public void SetOrbit(
            double inc,
            double e,
            double sma,
            double lan,
            double argPe,
            double mEp,
            double t,
            IStaticBody body)
        {
            inclination = inc;
            eccentricity = e;
            semiMajorAxis = sma;
            longitudeAscendingNode = lan;
            argumentOfPeriapsis = argPe;
            meanAnomalyAtEpoch = mEp;
            epoch = t;
            referenceBody = body;
            Init();
        }

        public void Init()
        {
            Planetarium.CelestialFrame.OrbitalFrame(longitudeAscendingNode, inclination, argumentOfPeriapsis, ref OrbitFrame);
            an = DVector3.Cross(DVector3.forward, OrbitFrame.Z);
            if (an.LengthSquared() == 0.0)
            {
                an = DVector3.right;
            }

            eccVec = Planetarium.Zup.WorldToLocal(OrbitFrame.X * eccentricity);
            double d = referenceBody.gravParameter * semiMajorAxis * (1.0 - eccentricity * eccentricity);
            h = Planetarium.Zup.WorldToLocal(OrbitFrame.Z * Math.Sqrt(d));
            meanMotion = GetMeanMotion(semiMajorAxis);
            meanAnomaly = meanAnomalyAtEpoch;
            ObT = meanAnomaly / meanMotion;
            ObTAtEpoch = ObT;
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

        public static double SafeAcos(double c)
        {
            if (c > 1.0)
            {
                c = 1.0;
            }
            else if (c < -1.0)
            {
                c = -1.0;
            }

            return Math.Acos(c);
        }
        
        public double getObtAtUT(double UT)
        {
            double obtAtUt;
            if (this.eccentricity < 1.0)
            {
                if (double.IsInfinity(UT))
                {
                    if (!Thread.CurrentThread.IsBackground)
                    {
                        Debug.Log("getObtAtUT infinite UT on elliptical orbit UT: " + UT.ToString() + ", returning NaN\n" + Environment.StackTrace);
                        return double.NaN;
                    }
                }
                obtAtUt = (UT - this.epoch + this.ObTAtEpoch) % this.period;
                if (obtAtUt > this.period / 2.0)
                {
                    obtAtUt -= this.period;
                }
            }
            else if (double.IsInfinity(UT))
            {
                return UT;
            }
            else obtAtUt = this.ObTAtEpoch + (UT - this.epoch);

            return obtAtUt;
        }
        
        public double TrueAnomalyAtUT(double UT) => this.TrueAnomalyAtT(this.getObtAtUT(UT));

        public double TrueAnomalyAtT(double T)
        {
            double num = this.solveEccentricAnomaly(T * this.meanMotion, this.eccentricity);
            if (!double.IsNaN(num)) return this.GetTrueAnomaly(num);

            return double.NaN;
        }
        
        public double GetOrbitalStateVectorsAtUT(double UT, out DVector3 pos, out DVector3 vel) => this.GetOrbitalStateVectorsAtObT(this.getObtAtUT(UT), UT, out pos, out vel);

        public double GetOrbitalStateVectorsAtObT(double ObT, double UT, out DVector3 pos, out DVector3 vel)
        {
            return this.GetOrbitalStateVectorsAtTrueAnomaly(this.TrueAnomalyAtT(ObT), UT, out pos, out vel);
        }

        public double GetOrbitalStateVectorsAtTrueAnomaly(double tA, double UT, out DVector3 pos, out DVector3 vel)
        {
            double num1 = Math.Cos(tA);
            double num2 = Math.Sin(tA);
            double num3 = this.semiMajorAxis * (1.0 - this.eccentricity * this.eccentricity);
            double num4 = Math.Sqrt(this.referenceBody.gravParameter / num3);
            double num5 = -num2 * num4;
            double num6 = (num1 + this.eccentricity) * num4;
            double vectorsAtTrueAnomaly = num3 / (1.0 + this.eccentricity * num1);
            double num7 = num1 * vectorsAtTrueAnomaly;
            double num8 = num2 * vectorsAtTrueAnomaly;
            /*if (worldToLocal)
            {

                Planetarium.CelestialFrame tempZup = new Planetarium.CelestialFrame();
                Planetarium.ZupAtT(UT, this.referenceBody, ref tempZup);
                pos = tempZup.WorldToLocal(this.OrbitFrame.X * num7 + this.OrbitFrame.Y * num8);
                vel = tempZup.WorldToLocal(this.OrbitFrame.X * num5 + this.OrbitFrame.Y * num6);
            }
            else*/
            {
                pos = this.OrbitFrame.X * num7 + this.OrbitFrame.Y * num8;
                vel = this.OrbitFrame.X * num5 + this.OrbitFrame.Y * num6;
            }
            return vectorsAtTrueAnomaly;
        }

        public void UpdateFromStateVectors(DVector3 pos, DVector3 vel, IStaticBody refBody)
        {
            pos = Planetarium.Zup.LocalToWorld(pos);
            vel = Planetarium.Zup.LocalToWorld(vel);
            
        }

        public void UpdateFromFixedVectors(DVector3 pos, DVector3 vel, IStaticBody refBody)
        {
            referenceBody = refBody;
            h = DVector3.Cross(pos, vel);
            if (h.LengthSquared().Equals(0.0))
            {
                inclination = Math.Acos(pos.z / pos.Length()) * (180.0 / Math.PI);
                an = DVector3.Cross(pos, DVector3.forward);
            }
            else
            {
                an = DVector3.Cross(DVector3.forward, h);
                OrbitFrame.Z = h / h.Length();
                inclination = UtilMath.AngleBetween(OrbitFrame.Z, DVector3.forward) * (180.0 / Math.PI);
            }

            if (an.LengthSquared().Equals(0.0))
            {
                an = DVector3.right;
            }

            longitudeAscendingNode = Math.Atan2(an.y, an.x) * (180.0 / Math.PI);
            longitudeAscendingNode = (longitudeAscendingNode + 360.0) % 360.0;
            eccVec = (DVector3.Dot(vel, vel) / refBody.gravParameter - 1.0 / pos.Length()) * pos -
                     DVector3.Dot(pos, vel) * vel / refBody.gravParameter;
            eccentricity = eccVec.Length();
            orbitalEnergy = vel.LengthSquared() / 2.0 - refBody.gravParameter / pos.Length();
            double num;
            if (eccentricity >= 1.0)
            {
                num = -semiLatusRectum / (eccVec.LengthSquared() - 1.0);
            }
            else
                num = -refBody.gravParameter / (2.0 * orbitalEnergy);

            semiMajorAxis = num;
            if (eccentricity.Equals(0.0))
            {
                OrbitFrame.X = an.Normalize();
                argumentOfPeriapsis = 0.0;
            }
            else
            {
                OrbitFrame.X = eccVec.Normalize();
                argumentOfPeriapsis = UtilMath.AngleBetween(an, OrbitFrame.X);
                if (DVector3.Dot(DVector3.Cross(an, OrbitFrame.X), h) < 0.0)
                {
                    argumentOfPeriapsis = 2.0 * Math.PI - argumentOfPeriapsis;
                }
            }

            if (h.LengthSquared().Equals(0.0))
            {
                OrbitFrame.Y = an.Normalize();
                OrbitFrame.Z = DVector3.Cross(OrbitFrame.X, OrbitFrame.Y);
            }
            else
                OrbitFrame.Y = DVector3.Cross(OrbitFrame.Z, OrbitFrame.X);

            argumentOfPeriapsis *= 180.0 / Math.PI;
            meanMotion = GetMeanMotion(semiMajorAxis);
            double x = DVector3.Dot(pos, OrbitFrame.X);
            trueAnomaly = Math.Atan2(DVector3.Dot(pos, OrbitFrame.Y), x);
            eccentricAnomaly = GetEccentricAnomaly(trueAnomaly);
            meanAnomaly = GetMeanAnomaly(eccentricAnomaly);
            meanAnomalyAtEpoch = meanAnomaly;
            ObT = meanAnomaly / meanMotion;
            ObTAtEpoch = ObT;
            if (eccentricity < 1.0)
            {
                period = 2.0 * Math.PI / meanMotion;
                orbitPercent = meanAnomaly / (2.0 * Math.PI);
                orbitPercent = (orbitPercent + 1.0) % 1.0;
                timeToPe = (period - ObT) % period;
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
                timeToPe = -ObT;
                timeToAp = double.PositiveInfinity;
            }

            radius = pos.Length();
            altitude = radius - refBody.Radius;
            this.pos = Planetarium.Zup.WorldToLocal(pos);
            this.vel = Planetarium.Zup.WorldToLocal(vel);
            h = Planetarium.Zup.WorldToLocal(h);
            debugPos = this.pos;
            debugVel = this.vel;
            debugH = h;
            debugAN = an;
            debugEccVec = eccVec;
            OrbitFrameX = OrbitFrame.X;
            OrbitFrameY = OrbitFrame.Y;
            OrbitFrameZ = OrbitFrame.Z;
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
        public DVector3 getPositionFromTrueAnomaly(double tA) => referenceBody.LocalPosition + getRelativePositionFromTrueAnomaly(tA).XZY;

        public DVector3 getPositionFromTrueAnomaly(double tA, bool worldToLocal)
        {
            double num1 = Math.Cos(tA);
            double num2 = Math.Sin(tA);
            DVector3 r = semiLatusRectum / (1.0 + eccentricity * num1) * (OrbitFrame.X * num1 + OrbitFrame.Y * num2);
            if (worldToLocal) return Planetarium.Zup.WorldToLocal(r);
            return r;
            
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
        public double getOrbitalSpeedAtDistance(double d) => Math.Sqrt(referenceBody.gravParameter * (2.0 / d - 1.0 / semiMajorAxis));
        public double getOrbitalSpeedAtPos(DVector3 pos) => getOrbitalSpeedAtDistance((referenceBody.LocalPosition - pos).Length());

        public DVector3 getRelativePositionFromTrueAnomaly(double tA) => getPositionFromTrueAnomaly(tA, true);

        public DVector3 getPositionAtT(double T) => referenceBody.LocalPosition + getRelativePositionAtT(T).XZY;
        public DVector3 getRelativePositionAtT(double T) => getRelativePositionFromTrueAnomaly(GetTrueAnomaly(solveEccentricAnomaly(T * meanMotion, eccentricity)));
        //public DVector3 getRelativePositionFromMeanAnomaly(double M) => this.getRelativePositionFromEccAnomaly(this.solveEccentricAnomaly(M, this.eccentricity, 1E-05));
        //public double TimeOfTrueAnomaly(double tA, double UT) => this.getUTAtMeanAnomaly(this.GetMeanAnomaly(this.GetEccentricAnomaly(tA)), UT);
        
        
        
        public void DrawOrbit(Color color)
        {
            if (eccentricity < 1.0)
            {
                for (double num = 0.0; num < 2.0 * Math.PI; num += drawResolution * (Math.PI / 180.0))
                {
                    Vector3 positionFromTrueAnomaly1 = getPositionFromTrueAnomaly(num % (2.0 * Math.PI));
                    Vector3 positionFromTrueAnomaly2 = getPositionFromTrueAnomaly((num + drawResolution * (Math.PI / 180.0)) % (2.0 * Math.PI));
                    if (color == Color.black)
                    {
                        Debug.DrawLine(ScaledSpace.LocalToScaledSpace(positionFromTrueAnomaly1), ScaledSpace.LocalToScaledSpace(positionFromTrueAnomaly2), Color.Lerp(Color.yellow, Color.green, Mathf.InverseLerp((float) getOrbitalSpeedAtDistance(PeR), (float) getOrbitalSpeedAtDistance(ApR), (float) getOrbitalSpeedAtPos(positionFromTrueAnomaly1))));
                    }
                    else
                    {
                        Debug.DrawLine(ScaledSpace.LocalToScaledSpace(positionFromTrueAnomaly1), ScaledSpace.LocalToScaledSpace(positionFromTrueAnomaly2), color);
                    }
                }
            }
            else
            {
                for (double tA = -Math.Acos(-(1.0 / eccentricity)) + drawResolution * (Math.PI / 180.0);
                    tA < Math.Acos(-(1.0 / eccentricity)) - drawResolution * (Math.PI / 180.0);
                    tA += drawResolution * (Math.PI / 180.0))
                {
                    if (color == Color.black)
                    {
                        Debug.DrawLine(ScaledSpace.LocalToScaledSpace(getPositionFromTrueAnomaly(tA)), ScaledSpace.LocalToScaledSpace(getPositionFromTrueAnomaly(Math.Min(Math.Acos(-(1.0 / eccentricity)), tA + drawResolution * (Math.PI / 180.0)))), Color.green);
                    }
                    else
                        Debug.DrawLine(ScaledSpace.LocalToScaledSpace(getPositionFromTrueAnomaly(tA)), ScaledSpace.LocalToScaledSpace(getPositionFromTrueAnomaly(                                Math.Min(Math.Acos(-(1.0 / eccentricity)), tA + drawResolution * (Math.PI / 180.0)))), color);
                }
            }

            Debug.DrawLine(ScaledSpace.LocalToScaledSpace(getPositionAtT(ObT)), ScaledSpace.LocalToScaledSpace(referenceBody.LocalPosition), Color.green);
            Debug.DrawRay(ScaledSpace.LocalToScaledSpace(getPositionAtT(ObT)), new DVector3(vel.x, vel.z, vel.y) * 0.0099999997764825821, Color.white);
            Debug.DrawLine(ScaledSpace.LocalToScaledSpace(referenceBody.LocalPosition), ScaledSpace.LocalToScaledSpace(referenceBody.LocalPosition + (an.XZY * radius)), Color.cyan);
            Debug.DrawLine(ScaledSpace.LocalToScaledSpace(referenceBody.LocalPosition), ScaledSpace.LocalToScaledSpace(getPositionAtT(0.0)), Color.magenta);
            Debug.DrawRay(ScaledSpace.LocalToScaledSpace(referenceBody.LocalPosition), ScaledSpace.LocalToScaledSpace(h.XZY), Color.blue);
        }
    }
}