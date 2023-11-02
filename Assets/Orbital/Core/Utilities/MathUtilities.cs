using System;
using Ara3D;

namespace Orbital.Core.Utilities
{
    public class MathUtilities
    {
        public static double TwoPI = 2.0 * Math.PI;
        public static float TwoPIf = 6.28318548f;
        
        
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