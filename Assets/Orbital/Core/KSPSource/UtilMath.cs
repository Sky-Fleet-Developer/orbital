using System;
using Ara3D;

namespace Orbital.Core.KSPSource
{
    public class UtilMath
    {
        public const double Rad2Deg = 57.295779513082323;
        public const double Deg2Rad = 0.017453292519943295;
        public static double TwoPI = 2.0 * Math.PI;
        public static float TwoPIf = 6.28318548f;
        public static double HalfPI = Math.PI / 2.0;
        public static float HalfPIf = 1.57079637f;
        public static float RPM2RadPerSec = TwoPIf / 60f;
        public static float RadPerSec2RPM = 60f / TwoPIf;
        
        
        public static double ClampRadiansTwoPI(double angle)
        {
            angle %= TwoPI;
            if (angle >= 0.0)
                return angle;
            return angle + TwoPI;
        }
        
        public static double AngleBetween(DVector3 v, DVector3 w)
        {
            DVector3 vector3d1 = v * w.Length();
            DVector3 vector3d2 = w * v.Length();
            DVector3 vector3d3 = vector3d1 - vector3d2;
            double magnitude1 = vector3d3.Length();
            vector3d3 = vector3d1 + vector3d2;
            double magnitude2 = vector3d3.Length();
            return 2.0 * Math.Atan2(magnitude1, magnitude2);
        }
    }
}