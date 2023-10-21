using System;
using Ara3D;
using UnityEngine;

namespace Orbital.Core.KSPSource
{
    public partial class Planetarium
    {
        public struct CelestialFrame
        {
            public DVector3 X;
            public DVector3 Y;
            public DVector3 Z;

            public DVector3 WorldToLocal(DVector3 r)
            {
                double x = DVector3.Dot(r, X);
                double num1 = DVector3.Dot(r, Y);
                double num2 = DVector3.Dot(r, Z);
                double y = num1;
                double z = num2;
                return new DVector3(x, y, z);
            }

            public DVector3 LocalToWorld(DVector3 r) => r.x * X + r.y * Y + r.z * Z;

            public QuaternionD Rotation => new QuaternionD(X, Y, Z);

            public static void SetFrame(double A, double B, double C, ref CelestialFrame cf)
            {
                double num1 = Math.Cos(A);
                double num2 = Math.Sin(A);
                double z = Math.Cos(B);
                double num3 = Math.Sin(B);
                double num4 = Math.Cos(C);
                double num5 = Math.Sin(C);
                cf.X = new DVector3(num1 * num4 - num2 * z * num5, num2 * num4 + num1 * z * num5, num3 * num5);
                cf.Y = new DVector3(-num1 * num5 - num2 * z * num4, -num2 * num5 + num1 * z * num4, num3 * num4);
                cf.Z = new DVector3(num2 * num3, -num1 * num3, z);
            }

            public static void OrbitalFrame(
                double LAN,
                double Inc,
                double ArgPe,
                ref CelestialFrame cf)
            {
                LAN *= Math.PI / 180.0;
                Inc *= Math.PI / 180.0;
                ArgPe *= Math.PI / 180.0;
                SetFrame(LAN, Inc, ArgPe, ref cf);
            }

            public static void PlanetaryFrame(
                double ra,
                double dec,
                double rot,
                ref CelestialFrame cf)
            {
                ra = (ra - 90.0) * Math.PI / 180.0;
                dec = (dec - 90.0) * Math.PI / 180.0;
                rot = (rot + 90.0) * Math.PI / 180.0;
                SetFrame(ra, dec, rot, ref cf);
            }
        }
    }
}