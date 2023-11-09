using System;
using Ara3D;
using Orbital.Core;
using Orbital.Core.TrajectorySystem;
using Orbital.Core.Utilities;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
#if UNITY_EDITOR
#endif

namespace Orbital.Test
{
    [ExecuteAlways]
    public class TrajectoryTest : MonoBehaviour
    {
        public StaticBody body;
        private IStaticBody _body;
        private IStaticBody _parent;
        private World _world;

        public bool needSample;
        public StaticOrbit Orbit;
        public DVector3 pos;
        public DVector3 vel;
        public double testT;
        public double testOmega;
        public double t;
        public double epoch;
        public double testEpoch;
        private const float scale = 4.456328E-09F;

        [ShowInInspector]
        public double _root => SubdivideAndFindRoots(_body.Orbit, Orbit,30);

        [ShowInInspector] public double angleBetweenOrbits => AngleBetweenOrbits(_body.Orbit, Orbit) / Math.PI * 180;
        [ShowInInspector] public double angle_a;
        [ShowInInspector] public double angle_b;
        public double AngleBetweenOrbits(StaticOrbit a, StaticOrbit b)
        {
            DVector3 axis = DVector3.Cross(a.H, b.H).Normalize();
            if (axis.IsNaN())
            {
                axis = a.RotationMatrix.Forward();
            }
            double aPeriapsisAngle = MathUtilities.AngleBetween(axis, a.RotationMatrix.Forward(), a.H);
            Debug.DrawRay(Vector3.zero, a.RotationMatrix.Forward() * 10, Color.black);
            Debug.DrawRay(Vector3.zero, axis * 10, Color.black);
            angle_a = aPeriapsisAngle/ Math.PI * 180;
            double bPeriapsisAngle = MathUtilities.AngleBetween(axis, b.RotationMatrix.Forward(), b.H);
            Debug.DrawRay(Vector3.zero, b.RotationMatrix.Forward() * 10, Color.white);
            angle_b = bPeriapsisAngle/ Math.PI * 180;

            var pA = a.GetPositionFromTrueAnomaly(aPeriapsisAngle);
            Debug.DrawRay(Vector3.zero, pA.Normalize() * 20, Color.yellow);
            var pa2 = a.GetPositionFromTrueAnomaly(aPeriapsisAngle + 0.1);
            Debug.DrawLine(pa2.Normalize() * 20, pA.Normalize() * 20, Color.yellow);

            var pB = b.GetPositionFromTrueAnomaly(-bPeriapsisAngle);
            Debug.DrawRay(Vector3.zero, pB.Normalize() * 10, Color.red);
            var pb2 = b.GetPositionFromTrueAnomaly(-bPeriapsisAngle - 0.1 * Math.Sign(DVector3.Dot(a.H, b.H)));
            Debug.DrawLine(pb2.Normalize() * 10, pB.Normalize() * 10, Color.red);
            
            return aPeriapsisAngle - bPeriapsisAngle;
        }
        
        [ShowInInspector] private double ArgumentOfPeriapsis => Orbit.ArgumentOfPeriapsis;
        [ShowInInspector] private double LongitudeAscendingNode => Orbit.LongitudeAscendingNode;
        [ShowInInspector] private double Inclination => Orbit.Inclination;
        [ShowInInspector] private double Other_ArgumentOfPeriapsis => _body.Orbit.ArgumentOfPeriapsis;
        [ShowInInspector] private double Other_LongitudeAscendingNode => _body.Orbit.LongitudeAscendingNode;
        [ShowInInspector] private double Other_Inclination => _body.Orbit.Inclination;

        void Refresh()
        {
            _body = body;
            _parent = GetComponentInParent<IStaticBody>();
            _world = GetComponentInParent<World>();
            _world.Load();
            Orbit = new StaticOrbit();
            Orbit.Nu = _parent.GravParameter;
            if (needSample)
            {
                _body.Orbit.GetOrbitalStateVectorsAtOrbitTime(0, out pos, out vel);
            }
            Vector3 h = Vector3.Cross(pos, vel).normalized;
            Quaternion quaternion = Quaternion.AngleAxis((float)testOmega, h);
            Orbit.Calculate(quaternion * pos, quaternion * vel, epoch);
        }

        public double SubdivideAndFindRoots(StaticOrbit a, StaticOrbit b, int subdividingParts)
        {
            DVector3 axis = DVector3.Cross(a.H, b.H).Normalize();
            if (axis.IsNaN())
            {
                axis = -a.RotationMatrix.Forward();
            }
            double theta1 = MathUtilities.AngleBetween(axis, a.RotationMatrix.Forward(), a.H);
            double theta2 = MathUtilities.AngleBetween(axis, b.RotationMatrix.Forward(), b.H);

            double a1 = a.SemiMajorAxis;
            double a2 = b.SemiMajorAxis;
            double e1 = a.Eccentricity;
            double e2 = b.Eccentricity;
            
            double step = Math.PI * 2 / (subdividingParts - 1);
            double bSide = Math.Sign(DVector3.Dot(a.H, b.H));
            for (int i = 0; i < subdividingParts; i++)
            {
                var pA = a.GetPositionFromTrueAnomaly(theta1 + step * i);
                var pB = b.GetPositionFromTrueAnomaly(theta2 + step * i);
                Color c = Color.Lerp(Color.green, Color.red, (float) i / subdividingParts);
                Debug.DrawRay(Vector3.zero, pA, c);
                Debug.DrawRay(Vector3.zero, pB, c);
                double root = FindRoot(a1, e1, a2, e2, theta1 + step * i, theta2 + step * i, step * 1.2);
                if (!double.IsNaN(root))
                {
                    return (step * i + root) * bSide + Math.PI;
                }
            }
            return double.NaN;
        }
        
        public static double FindRoot(double a1, double e1, double a2, double e2, double theta1, double theta2, double delta)
        {
            double epsilon = 1e-1; // Точность
            double maxIterations = 20; // Максимальное число итераций

            double theta = 0;
            double intersectionAngle = double.NaN;

            for (int i = 0; i < maxIterations; i++)
            {
                double r1 = a1 * (1 - e1 * e1) / (1 + e1 * Math.Cos(theta1 + theta));
                double r2 = a2 * (1 - e2 * e2) / (1 + e2 * Math.Cos(theta2 + theta));

                double f = r1 - r2;
                double df = (a1 * e1 * Math.Sin(theta1 + theta)) / (1 + e1 * Math.Cos(theta1 + theta))
                            - (a2 * e2 * Math.Sin(theta2 + theta)) / (1 + e2 * Math.Cos(theta2 + theta));

                theta -= f / df;
                
                if(theta > delta || theta < 0) break;

                if (Math.Abs(f) < epsilon)
                {
                    intersectionAngle = theta;
                    break;
                }
            }

            return intersectionAngle;
        }
        
        private void OnValidate()
        {
            Refresh();
            //Vector3 scaledPos = _parent.LocalPosition * scale;
            // double gravityRadius = MassUtility.GetGravityRadius(_body.GravParameter);
            //t = MassUtility.GetClosestPointTimeForDistance(Orbit, _body.Orbit, gravityRadius, 0, out double distance);
        }

        void Update()
        {
            if (!enabled) return;
            if (_parent == null)
            {
                Refresh();
            }
        }
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!enabled) return;
            Handles.color = Color.green * 0.7f;
            Vector3 scaledPos = _parent.LocalPosition * scale;
            var a = (Vector3) Orbit.GetPositionAtT(t) * scale;
            //var b = (Vector3) _body.Orbit.GetPositionAtT(t - _body.Orbit.Epoch) * scale;
            Debug.DrawLine(scaledPos, scaledPos + new Vector3((float)Math.Sin(_root), 0, (float)Math.Cos(_root)) * 25, Color.cyan);
            //double periapsis = Math.PI * 0.5 - Orbit.ArgumentOfPeriapsis;
            //double root2 = periapsis + (periapsis - _root);
            
            //Debug.DrawLine(scaledPos, scaledPos + new Vector3((float)Math.Sin(root2), 0, (float)Math.Cos(root2)) * 25, Color.yellow);
            //Debug.DrawLine(scaledPos, scaledPos + b, Color.cyan);
            //var at = (Vector3) Orbit.GetPositionAtT(testEpoch) * scale;
            //var bt = (Vector3) _body.Orbit.GetPositionAtT(testEpoch - _body.Orbit.Epoch) * scale;
            //Debug.DrawLine(scaledPos, scaledPos + at, Color.magenta);
            //Debug.DrawRay(pos * scale, vel * 0.005f, Color.red);
            //Debug.DrawLine(scaledPos, scaledPos + bt, Color.magenta);
            //var d = Quaternion.LookRotation(a - b, Vector3.up) * Quaternion.Euler(90, 0, 0);
            //Handles.CircleHandleCap(-1, scaledPos + b, d, (float)MassUtility.GetGravityRadius(_body.GravParameter) * scale, EventType.Repaint);
            Handles.color = Color.magenta * 0.7f;
            //var dt = Quaternion.LookRotation(at - bt, Vector3.up) * Quaternion.Euler(90, 0, 0);
            //Handles.CircleHandleCap(-1, scaledPos + bt, dt, (float)MassUtility.GetGravityRadius(_body.GravParameter) * scale, EventType.Repaint);
            //testD = (a - b).magnitude / scale;
            Orbit.DrawGizmos(_parent.LocalPosition + DVector3.up * 10000);
        }
#endif
    }
}
