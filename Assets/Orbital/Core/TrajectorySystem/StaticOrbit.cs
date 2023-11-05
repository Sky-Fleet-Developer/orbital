using System;
using Ara3D;
using Ara3D.Double;
using Orbital.Core.Utilities;
using UnityEngine;

namespace Orbital.Core.TrajectorySystem
{
    public class StaticOrbit : IStaticOrbit
    {
        private const double Deg2Rad = 0.01745329;
        private const double Rad2Deg = 57.29578;
        
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
        public double OrbitPercent {get; private set;}
        public double OrbitTime {get; private set;}
        public double OrbitTimeAtEpoch {get; private set;}
        public double TimeToPe {get; private set;}
        public double TimeToAp {get; private set;}
        public DVector3 H {get; private set;}
        public DVector3 EccVec {get; private set;}
        public DVector3 AscendingNode {get; private set;}
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

        public double Nu { get; set; }// => _other.Mass * MassUtility.G;
        public double SemiLatusRectum => H.LengthSquared() / Nu;
        public double Pericenter => (1.0 - Eccentricity) * SemiMajorAxis;

        public double Apocenter => (1.0 + Eccentricity) * SemiMajorAxis;
        
        public StaticOrbit()
        {
            _rotationMatrix = DMatrix4x4.Identity;
        }
        public StaticOrbit(IMass other)
        {
            Nu = other.Mass * MassUtility.G;
            _rotationMatrix = DMatrix4x4.Identity;
        }

        public void Init()
        {
            _rotationMatrix = GetRotation(Inclination, LongitudeAscendingNode, ArgumentOfPeriapsis);
            AscendingNode = DVector3.Cross(DVector3.up, _rotationMatrix.Up());
            if (AscendingNode.LengthSquared() == 0.0)
            {
                AscendingNode = DVector3.right;
            }

            EccVec = _rotationMatrix.Right() * Eccentricity;
            double d = Nu * SemiMajorAxis * (1.0 - Eccentricity * Eccentricity);
            H = _rotationMatrix.Up() * Math.Sqrt(d);
            MeanMotion = this.GetMeanMotion(SemiMajorAxis);
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
        
        public void Calculate(TrajectorySettings settings, double epoch)
        {
            SetOrbit(settings.inclination * Deg2Rad, settings.eccentricity, settings.semiMajorAxis, settings.longitudeAscendingNode * Deg2Rad, settings.argumentOfPeriapsis * Deg2Rad, 0, epoch + settings.timeShift);
        }
        
        public void Calculate(DVector3 position, DVector3 velocity, double epoch)
        {
            Epoch = epoch;
            UpdateFromFixedVectors(position, velocity);
            if (!double.IsNaN(ArgumentOfPeriapsis))
                return;
            
            DVector3 lhs = Quaternion.AngleAxis(-(float) LongitudeAscendingNode, Vector3.up) * Vector3.right;
            double d = DVector3.Dot(lhs, EccVec) / (lhs.Length() * EccVec.Length());
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
            DVector3 right, up = DVector3.up, fwd;
            H = DVector3.Cross(velocity, position);
            if (H.LengthSquared().Equals(0.0))
            {
                Inclination = Math.Acos(position.y / position.Length());
                AscendingNode = DVector3.Cross(position, DVector3.up);
            }
            else
            {
                AscendingNode = DVector3.Cross(DVector3.up, H);
                up = H / H.Length();
                Inclination = MathUtilities.AngleBetween(up, DVector3.up);
            }

            if (AscendingNode.LengthSquared().Equals(0.0))
            {
                AscendingNode = DVector3.forward;
            }

            LongitudeAscendingNode = MathUtilities.ClampRadiansTwoPI(Math.Atan2(AscendingNode.x, AscendingNode.z));
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
                fwd = AscendingNode.Normalize();
                ArgumentOfPeriapsis = 0.0;
            }
            else
            {
                fwd = EccVec.Normalize();
                ArgumentOfPeriapsis = MathUtilities.AngleBetween(AscendingNode, fwd);
                if (DVector3.Dot(DVector3.Cross(AscendingNode, fwd), H) < 0.0)
                {
                    ArgumentOfPeriapsis = 2.0 * Math.PI - ArgumentOfPeriapsis;
                }
            }

            if (H.LengthSquared().Equals(0.0))
            {
                right = AscendingNode.Normalize();
                up = DVector3.Cross(fwd, right);
            }
            else
                right = DVector3.Cross(up, fwd);

            MeanMotion = this.GetMeanMotion(SemiMajorAxis);
            double x = DVector3.Dot(position, fwd);
            TrueAnomaly = Math.Atan2(DVector3.Dot(position, right), x);
            EccentricAnomaly = this.GetEccentricAnomaly(TrueAnomaly);
            MeanAnomaly = this.GetMeanAnomaly(EccentricAnomaly);
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
            _rotationMatrix = DMatrix4x4.LookRotation(fwd, up);
            Debug.DrawRay(Vector3.zero, up, Color.green, 1);
            Debug.DrawRay(Vector3.zero, fwd, Color.blue, 1);
            Debug.DrawRay(Vector3.zero, right, Color.red, 1);
            WasChangedHandler?.Invoke();
        }

        public static DMatrix4x4 GetRotation(double inclination, double longitudeAscendingNode, double argumentOfPeriapsis)
        {
            DMatrix4x4 lan = DMatrix4x4.CreateRotation(0, longitudeAscendingNode, 0);
            DMatrix4x4 inc = DMatrix4x4.CreateRotation(0, 0, inclination);
            DMatrix4x4 aop = DMatrix4x4.CreateRotation(0, argumentOfPeriapsis, 0);

            return aop * inc * lan;
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
                this.GetOrbitalStateVectorsAtOrbitTime(time, out pos, out vel);
            }

            return (pos, vel);
        }

        /// <param name="e">Eccentricity</param>
        /// <param name="r">minimal distance to parent</param>
        public static double GetSemiMajorAxis(double e, double r)
        {
            return r / (1 - e);
        }

        /// <param name="a">large semi-axis</param>
        /// <param name="g">gravitational constant</param>
        /// <param name="m">parent mass</param>
        public static double GetPeriod(double a, double g, double m)
        {
            return 2 * Math.PI * Math.Sqrt(Math.Pow(a, 3) / (g * m)); //https://ru.wikipedia.org/wiki/%D0%9E%D1%80%D0%B1%D0%B8%D1%82%D0%B0%D0%BB%D1%8C%D0%BD%D1%8B%D0%B9_%D0%BF%D0%B5%D1%80%D0%B8%D0%BE%D0%B4
        }
        

        public void DrawGizmosByT(double from, double to, DVector3 offset)
        {
            int drawResolution = 30;
            Color color = Color.black;
            float scale = 4.456328E-09F;
            Vector3 scaledOffset = offset * scale;
            double delta = to - from;
            if (delta > Period)
            {
                DrawGizmos(offset);
                return;
            }

            double step = delta / drawResolution;
            for (double time = from; time < to; time += step)
            {
                Vector3 positionFromTrueAnomaly1 = this.GetPositionAtT(time);
                Vector3 positionFromTrueAnomaly2 = this.GetPositionAtT(time + step);
                if (color == Color.black)
                {
                    Debug.DrawLine(positionFromTrueAnomaly1 * scale + scaledOffset, positionFromTrueAnomaly2 * scale + scaledOffset, Color.Lerp(Color.yellow, Color.green, Mathf.InverseLerp((float) this.GetOrbitalSpeedAtDistance(Pericenter), (float) this.GetOrbitalSpeedAtDistance(Apocenter), (float) this.GetOrbitalSpeedAtPos(positionFromTrueAnomaly1))));
                }
                else
                {
                    Debug.DrawLine(positionFromTrueAnomaly1 * scale + scaledOffset, positionFromTrueAnomaly2 * scale + scaledOffset, color);
                }
            }
        }

        public void DrawGizmos(DVector3 offset)
        {
            int drawResolution = 10;
            Color color = Color.black;
            float scale = 4.456328E-09F;
            Vector3 scaledOffset = offset * scale;

            if (Eccentricity < 1.0)
            {
                for (double num = 0.0; num < 2.0 * Math.PI; num += drawResolution * (Math.PI / 180.0))
                {
                    Vector3 positionFromTrueAnomaly1 = this.GetPositionFromTrueAnomaly(num % (2.0 * Math.PI));
                    Vector3 positionFromTrueAnomaly2 = this.GetPositionFromTrueAnomaly((num + drawResolution * (Math.PI / 180.0)) % (2.0 * Math.PI));
                    if (color == Color.black)
                    {
                        Debug.DrawLine(positionFromTrueAnomaly1 * scale + scaledOffset, positionFromTrueAnomaly2 * scale + scaledOffset, Color.Lerp(Color.yellow, Color.green, Mathf.InverseLerp((float) this.GetOrbitalSpeedAtDistance(Pericenter), (float) this.GetOrbitalSpeedAtDistance(Apocenter), (float) this.GetOrbitalSpeedAtPos(positionFromTrueAnomaly1))));
                    }
                    else
                    {
                        Debug.DrawLine(positionFromTrueAnomaly1 * scale + scaledOffset, positionFromTrueAnomaly2 * scale + scaledOffset, color);
                    }
                }
            }
            else
            {
                for (double tA = -Math.Acos(-(1.0 / Eccentricity)) + drawResolution * (Math.PI / 180.0); tA < Math.Acos(-(1.0 / Eccentricity)) - drawResolution * (Math.PI / 180.0); tA += drawResolution * (Math.PI / 180.0))
                {
                    if (color == Color.black)
                    {
                        Debug.DrawLine((Vector3)this.GetPositionFromTrueAnomaly(tA) * scale + scaledOffset, (Vector3)(this.GetPositionFromTrueAnomaly(Math.Min(Math.Acos(-(1.0 / Eccentricity)), tA + drawResolution * (Math.PI / 180.0))) * scale) + scaledOffset, Color.green);
                    }
                    else
                        Debug.DrawLine((Vector3)this.GetPositionFromTrueAnomaly(tA) * scale + scaledOffset, (Vector3)(this.GetPositionFromTrueAnomaly(Math.Min(Math.Acos(-(1.0 / Eccentricity)), tA + drawResolution * (Math.PI / 180.0))) * scale) + scaledOffset, color);
                }
            }

            Debug.DrawLine((Vector3)(this.GetPositionAtT(OrbitTime) * scale) + scaledOffset,  scaledOffset, Color.green);
            //Debug.DrawRay(getRelativePositionAtT(orbitTime)), new DVector3(vel.x, vel.z, vel.y) * 0.0099999997764825821, Color.white);
            Debug.DrawLine(scaledOffset, (Vector3)((AscendingNode * Radius) * scale) + scaledOffset, Color.cyan);
            Debug.DrawLine(scaledOffset, (Vector3)(this.GetPositionAtT(0.0) * scale) + scaledOffset, Color.magenta);
            Debug.DrawRay(scaledOffset, (Vector3)(H * scale) + scaledOffset, Color.blue);
        }
        
        public StaticOrbit Clone()
        {
            return MemberwiseClone() as StaticOrbit;
        }
    }
}
