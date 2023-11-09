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
            DVector3 v1 = v * w.Length();
            DVector3 v2 = w * v.Length();
            DVector3 v3 = v1 - v2;
            double length1 = v3.Length();
            v3 = v1 + v2;
            double length2 = v3.Length();
            return 2.0 * Math.Atan2(length1, length2);
        }
        
        public static double AngleBetween(DVector3 vector1, DVector3 vector2, DVector3 axisVector)
        {
            vector1 = DVector3.ProjectOnPlane(vector1, axisVector);
            vector2 = DVector3.ProjectOnPlane(vector2, axisVector);
            DVector3 cross = DVector3.Cross(vector1, vector2);
            if (cross == DVector3.Zero) return DVector3.Dot(vector1, vector2) > 0 ? 0 : Math.PI;
            double sign = -Math.Sign(DVector3.Dot(axisVector, cross));
            

            return AngleBetween(vector1, vector2) * sign;
        }
    }
}