using System;
using System.Runtime.CompilerServices;
using Ara3D;

namespace UnityEngine
{
    public struct QuaternionD
    {
        public double x;
        public double y;
        public double z;
        public double w;

        public QuaternionD(double x, double y, double z, double w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public QuaternionD(DVector3 X, DVector3 Y, DVector3 Z)
        {
            double x1 = X.x;
            double y1 = X.y;
            double z1 = X.z;
            double x2 = Y.x;
            double y2 = Y.y;
            double z2 = Y.z;
            double x3 = Z.x;
            double y3 = Z.y;
            double z3 = Z.z;
            if (x1 + y2 + z3 >= 0.0)
            {
                double d = x1 + y2 + z3 + 1.0;
                double num = 0.5 / Math.Sqrt(d);
                w = d * num;
                z = (y1 - x2) * num;
                y = (x3 - z1) * num;
                x = (z2 - y3) * num;
            }
            else
            {
                if (x1 > y2)
                {
                    if (x1 > z3)
                    {
                        double d = x1 - y2 - z3 + 1.0;
                        double num = 0.5 / Math.Sqrt(d);
                        x = d * num;
                        y = (y1 + x2) * num;
                        z = (x3 + z1) * num;
                        w = (z2 - y3) * num;
                        return;
                    }
                }

                if (y2 > z3)
                {
                    double d = -x1 + y2 - z3 + 1.0;
                    double num = 0.5 / Math.Sqrt(d);
                    y = d * num;
                    x = (y1 + x2) * num;
                    w = (x3 - z1) * num;
                    z = (z2 + y3) * num;
                }
                else
                {
                    double d = -x1 - y2 + z3 + 1.0;
                    double num = 0.5 / Math.Sqrt(d);
                    z = d * num;
                    w = (y1 - x2) * num;
                    x = (x3 + z1) * num;
                    y = (z2 + y3) * num;
                }
            }
        }

        public double this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0:
                        return x;
                    case 1:
                        return y;
                    case 2:
                        return z;
                    case 3:
                        return w;
                    default:
                        throw new IndexOutOfRangeException("Invalid QuaternionD index!");
                }
            }
            set
            {
                switch (index)
                {
                    case 0:
                        x = value;
                        break;
                    case 1:
                        y = value;
                        break;
                    case 2:
                        z = value;
                        break;
                    case 3:
                        w = value;
                        break;
                    default:
                        throw new IndexOutOfRangeException("Invalid QuaternionD index!");
                }
            }
        }

        public void FrameVectors(out DVector3 frameX, out DVector3 frameY, out DVector3 frameZ)
        {
            frameX = new DVector3(1.0 - 2.0 * y * y - 2.0 * z * z,
                2.0 * x * y + 2.0 * w * z, 2.0 * x * z - 2.0 * w * y);
            frameY = new DVector3(2.0 * x * y - 2.0 * w * z,
                1.0 - 2.0 * x * x - 2.0 * z * z, 2.0 * y * z + 2.0 * w * x);
            frameZ = new DVector3(2.0 * x * z + 2.0 * w * y,
                2.0 * y * z - 2.0 * w * x, 1.0 - 2.0 * x * x - 2.0 * y * y);
        }

        public QuaternionD swizzle => new QuaternionD(-x, -z, -y, w);

        public static implicit operator Quaternion(QuaternionD q) =>
            new Quaternion((float) q.x, (float) q.y, (float) q.z, (float) q.w);

        public static implicit operator QuaternionD(Quaternion q) =>
            new QuaternionD(q.x, q.y, q.z, q.w);

        public static QuaternionD identity => new QuaternionD(0.0, 0.0, 0.0, 1.0);

        public DVector3 eulerAngles
        {
            get => Internal_ToEulerRad(this) * (180.0 / Math.PI);
            set => this = Internal_FromEulerRad(value * (Math.PI / 180.0));
        }

        public static double Dot(QuaternionD a, QuaternionD b) => a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;

        public static QuaternionD AngleAxis(double angle, DVector3 axis)
        {
            double magnitude = axis.Length();
            double x;
            double y;
            double z;
            double w;
            if (magnitude > 0.0001)
            {
                double num1 = Math.Cos(angle * (Math.PI / 180.0) / 2.0);
                double num2 = Math.Sin(angle * (Math.PI / 180.0) / 2.0);
                x = axis.x / magnitude * num2;
                y = axis.y / magnitude * num2;
                z = axis.z / magnitude * num2;
                w = num1;
            }
            else
            {
                w = 1.0;
                x = 0.0;
                y = 0.0;
                z = 0.0;
            }

            return new QuaternionD(x, y, z, w);
        }

        public void ToAngleAxis(out double angle, out DVector3 axis)
        {
            Internal_ToAxisAngleRad(this, out axis, out angle);
            angle *= 180.0 / Math.PI;
        }

        public static QuaternionD FromToRotation(DVector3 fromDirection, DVector3 toDirection) =>
            INTERNAL_CALL_FromToRotation(ref fromDirection, ref toDirection);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern QuaternionD INTERNAL_CALL_FromToRotation(
            ref DVector3 fromDirection,
            ref DVector3 toDirection);

        public void SetFromToRotation(DVector3 fromDirection, DVector3 toDirection) =>
            this = FromToRotation(fromDirection, toDirection);

        public static QuaternionD LookRotation(DVector3 forward, DVector3 up)
        {
            QuaternionD quaternionD = new QuaternionD();
            forward = forward.Normalize();
            DVector3 rhs = DVector3.Cross(up, forward).Normalize();
            up = DVector3.Cross(forward, rhs);
            double x1 = forward.x;
            double y1 = forward.y;
            double z1 = forward.z;
            double x2 = up.x;
            double y2 = up.y;
            double z2 = up.z;
            double x3 = rhs.x;
            double y3 = rhs.y;
            double z3 = rhs.z;
            double num1 = x3 + y2 + z1;
            if (num1 > 0.0)
            {
                double num2 = Math.Sqrt(num1 + 1.0);
                quaternionD.w = num2 * 0.5;
                double num3 = 0.5 / num2;
                quaternionD.x = (z2 - y1) * num3;
                quaternionD.y = (x1 - z3) * num3;
                quaternionD.z = (y3 - x2) * num3;
                return quaternionD;
            }
            else
            {
                if (x3 >= y2)
                {
                    if (x3 >= z1)
                    {
                        double num4 = Math.Sqrt(1.0 + x3 - y2 - z1);
                        double num5 = 0.5 / num4;
                        quaternionD.x = 0.5 * num4;
                        quaternionD.y = (y3 + x2) * num5;
                        quaternionD.z = (z3 + x1) * num5;
                        quaternionD.w = (z2 - y1) * num5;
                        return quaternionD;
                    }
                }

                if (y2 > z1)
                {
                    double num6 = Math.Sqrt(1.0 + y2 - x3 - z1);
                    double num7 = 0.5 / num6;
                    quaternionD.x = (x2 + y3) * num7;
                    quaternionD.y = 0.5 * num6;
                    quaternionD.z = (y1 + z2) * num7;
                    quaternionD.w = (x1 - z3) * num7;
                    return quaternionD;
                }
                else
                {
                    double num8 = Math.Sqrt(1.0 + z1 - x3 - y2);
                    double num9 = 0.5 / num8;
                    quaternionD.x = (x1 + z3) * num9;
                    quaternionD.y = (y1 + z2) * num9;
                    quaternionD.z = 0.5 * num8;
                    quaternionD.w = (y3 - x2) * num9;
                    return quaternionD;
                }
            }
        }

        public static QuaternionD LookRotation(DVector3 forward)
        {
            DVector3 up = DVector3.up;
            return LookRotation(forward, up);
        }

        public void SetLookRotation(DVector3 view)
        {
            DVector3 up = DVector3.up;
            SetLookRotation(view, up);
        }

        public void SetLookRotation(DVector3 view, DVector3 up) => this = LookRotation(view, up);

        public static QuaternionD Slerp(QuaternionD from, QuaternionD to, double t) =>
            INTERNAL_CALL_Slerp(ref from, ref to, t);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern QuaternionD INTERNAL_CALL_Slerp(
            ref QuaternionD from,
            ref QuaternionD to,
            double t);

        public static QuaternionD Lerp(QuaternionD from, QuaternionD to, double t) =>
            INTERNAL_CALL_Lerp(ref from, ref to, t);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern QuaternionD INTERNAL_CALL_Lerp(
            ref QuaternionD from,
            ref QuaternionD to,
            double t);

        public static QuaternionD RotateTowards(
            QuaternionD from,
            QuaternionD to,
            double maxDegreesDelta)
        {
            double t = Math.Min(1.0, maxDegreesDelta / Angle(from, to));
            return UnclampedSlerp(from, to, t);
        }

        private static QuaternionD UnclampedSlerp(QuaternionD from, QuaternionD to, double t) =>
            INTERNAL_CALL_UnclampedSlerp(ref from, ref to, t);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern QuaternionD INTERNAL_CALL_UnclampedSlerp(
            ref QuaternionD from,
            ref QuaternionD to,
            double t);

        public static QuaternionD Inverse(QuaternionD q) => new QuaternionD(-q.x, -q.y, -q.z, q.w);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern QuaternionD INTERNAL_CALL_Inverse(ref QuaternionD rotation);

        public override string ToString() => string.Format("({0:F1}, {1:F1}, {2:F1}, {3:F1})", x,
            y, z, w);

        public string ToString(string format) => string.Format("({0}, {1}, {2}, {3})", x.ToString(format),
            y.ToString(format), z.ToString(format), w.ToString(format));

        public static double Angle(QuaternionD a, QuaternionD b) =>
            Math.Acos(Math.Min(Math.Abs(Dot(a, b)), 1.0)) * 2.0 * (180.0 / Math.PI);

        public static QuaternionD Euler(double x, double y, double z) =>
            Internal_FromEulerRad(new DVector3(x, y, z) * (Math.PI / 180.0));

        public static QuaternionD Euler(DVector3 euler) => Internal_FromEulerRad(euler * (Math.PI / 180.0));

        private static DVector3 Internal_ToEulerRad(QuaternionD rotation) =>
            INTERNAL_CALL_Internal_ToEulerRad(ref rotation);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern DVector3 INTERNAL_CALL_Internal_ToEulerRad(ref QuaternionD rotation);

        private static QuaternionD Internal_FromEulerRad(DVector3 euler) =>
            INTERNAL_CALL_Internal_FromEulerRad(ref euler);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern QuaternionD INTERNAL_CALL_Internal_FromEulerRad(ref DVector3 euler);

        private static void Internal_ToAxisAngleRad(QuaternionD q, out DVector3 axis, out double angle) =>
            INTERNAL_CALL_Internal_ToAxisAngleRad(ref q, out axis, out angle);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern void INTERNAL_CALL_Internal_ToAxisAngleRad(
            ref QuaternionD q,
            out DVector3 axis,
            out double angle);

        [Obsolete(
            "Use QuaternionD.Euler instead. This function was deprecated because it uses radians instad of degrees")]
        public static QuaternionD EulerRotation(double x, double y, double z) =>
            Internal_FromEulerRad(new DVector3(x, y, z));

        [Obsolete(
            "Use QuaternionD.Euler instead. This function was deprecated because it uses radians instad of degrees")]
        public static QuaternionD EulerRotation(DVector3 euler) => Internal_FromEulerRad(euler);

        [Obsolete(
            "Use QuaternionD.Euler instead. This function was deprecated because it uses radians instad of degrees")]
        public void SetEulerRotation(double x, double y, double z) =>
            this = Internal_FromEulerRad(new DVector3(x, y, z));

        [Obsolete(
            "Use QuaternionD.Euler instead. This function was deprecated because it uses radians instad of degrees")]
        public void SetEulerRotation(DVector3 euler) => this = Internal_FromEulerRad(euler);

        [Obsolete(
            "Use QuaternionD.eulerAngles instead. This function was deprecated because it uses radians instad of degrees")]
        public DVector3 ToEuler() => Internal_ToEulerRad(this);

        [Obsolete(
            "Use QuaternionD.Euler instead. This function was deprecated because it uses radians instad of degrees")]
        public static QuaternionD EulerAngles(double x, double y, double z) =>
            Internal_FromEulerRad(new DVector3(x, y, z));

        [Obsolete(
            "Use QuaternionD.Euler instead. This function was deprecated because it uses radians instad of degrees")]
        public static QuaternionD EulerAngles(DVector3 euler) => Internal_FromEulerRad(euler);

        [Obsolete(
            "Use QuaternionD.ToAngleAxis instead. This function was deprecated because it uses radians instad of degrees")]
        public void ToAxisAngle(out DVector3 axis, out double angle) =>
            Internal_ToAxisAngleRad(this, out axis, out angle);

        [Obsolete(
            "Use QuaternionD.Euler instead. This function was deprecated because it uses radians instad of degrees")]
        public void SetEulerAngles(double x, double y, double z) => SetEulerRotation(new DVector3(x, y, z));

        [Obsolete(
            "Use QuaternionD.Euler instead. This function was deprecated because it uses radians instad of degrees")]
        public void SetEulerAngles(DVector3 euler) => this = EulerRotation(euler);

        [Obsolete(
            "Use QuaternionD.eulerAngles instead. This function was deprecated because it uses radians instad of degrees")]
        public static DVector3 ToEulerAngles(QuaternionD rotation) => Internal_ToEulerRad(rotation);

        [Obsolete(
            "Use QuaternionD.eulerAngles instead. This function was deprecated because it uses radians instad of degrees")]
        public DVector3 ToEulerAngles() => Internal_ToEulerRad(this);

        [Obsolete(
            "Use QuaternionD.AngleAxis instead. This function was deprecated because it uses radians instad of degrees")]
        public static QuaternionD AxisAngle(DVector3 axis, double angle) =>
            INTERNAL_CALL_AxisAngle(ref axis, angle);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern QuaternionD INTERNAL_CALL_AxisAngle(ref DVector3 axis, double angle);

        [Obsolete(
            "Use QuaternionD.AngleAxis instead. This function was deprecated because it uses radians instad of degrees")]
        public void SetAxisAngle(DVector3 axis, double angle) => this = AxisAngle(axis, angle);

        public override int GetHashCode() => x.GetHashCode() ^ y.GetHashCode() << 2 ^
                                             z.GetHashCode() >> 2 ^ w.GetHashCode() >> 1;

        public override bool Equals(object other)
        {
            if (!(other is QuaternionD quaternionD))
            {
                return false;
            }
            else
            {
                if (x.Equals(quaternionD.x))
                {
                    if (y.Equals(quaternionD.y))
                    {
                        if (z.Equals(quaternionD.z))
                        {
                            return w.Equals(quaternionD.w);
                        }
                    }
                }

                return false;
            }
        }

        public static QuaternionD operator *(QuaternionD lhs, QuaternionD rhs) => new QuaternionD(
            lhs.w * rhs.x + lhs.x * rhs.w + lhs.y * rhs.z - lhs.z * rhs.y,
            lhs.w * rhs.y + lhs.y * rhs.w + lhs.z * rhs.x - lhs.x * rhs.z,
            lhs.w * rhs.z + lhs.z * rhs.w + lhs.x * rhs.y - lhs.y * rhs.x,
            lhs.w * rhs.w - lhs.x * rhs.x - lhs.y * rhs.y - lhs.z * rhs.z);

        public static DVector3 operator *(QuaternionD rotation, DVector3 point)
        {
            double num1 = rotation.x * 2.0;
            double num2 = rotation.y * 2.0;
            double num3 = rotation.z * 2.0;
            double num4 = rotation.x * num1;
            double num5 = rotation.y * num2;
            double num6 = rotation.z * num3;
            double num7 = rotation.x * num2;
            double num8 = rotation.x * num3;
            double num9 = rotation.y * num3;
            double num10 = rotation.w * num1;
            double num11 = rotation.w * num2;
            double num12 = rotation.w * num3;
            DVector3 DVector3;
            DVector3.x = (1.0 - (num5 + num6)) * point.x + (num7 - num12) * point.y + (num8 + num11) * point.z;
            DVector3.y = (num7 + num12) * point.x + (1.0 - (num4 + num6)) * point.y + (num9 - num10) * point.z;
            DVector3.z = (num8 - num11) * point.x + (num9 + num10) * point.y + (1.0 - (num4 + num5)) * point.z;
            return DVector3;
        }

        public static bool operator ==(QuaternionD lhs, QuaternionD rhs)
        {
            if (lhs.x == rhs.x)
            {
                if (lhs.y == rhs.y)
                {
                    if (lhs.z == rhs.z)
                    {
                        return lhs.w == rhs.w;
                    }
                }
            }

            return false;
        }

        public static bool operator !=(QuaternionD lhs, QuaternionD rhs)
        {
            if (lhs.x == rhs.x)
            {
                if (lhs.y == rhs.y)
                {
                    if (lhs.z == rhs.z)
                    {
                        return lhs.w != rhs.w;
                    }
                }
            }

            return true;
        }
    }
}