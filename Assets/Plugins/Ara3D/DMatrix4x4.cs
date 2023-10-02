using System;
using UnityEngine;

namespace Ara3D.Double
{
    public struct DMatrix4x4
    {
        private double[,] _array;
        /*private double m00;
        private double m01;
        private double m02;
        private double m03;
        private double m10;
        private double m11;
        private double m12;
        private double m13;
        private double m20;
        private double m21;
        private double m22;
        private double m23;
        private double m30;
        private double m31;
        private double m32;
        private double m33;*/

        /*
         |m00 m01 m02 m03|
         |m10 m11 m12 m13|
         |m20 m21 m22 m23|
         |m30 m31 m32 m33|
         */

        public double this[int a, int b]
        {
            get => _array[a, b];
            set => _array[a, b] = value;
        }

        public static DMatrix4x4 Identity
        {
            get
            {
                return new()
                {
                    _array = new double[4, 4]
                    {
                        {1, 0, 0, 0},
                        {0, 1, 0, 0},
                        {0, 0, 1, 0},
                        {0, 0, 0, 1}
                    }
                };
            }
        }

        /// <summary>
        /// Creates matrix from Euler angles in radians
        /// </summary>
        public static DMatrix4x4 CreateRotation(double roll, double pitch, double yaw)
        {
            double cosRoll = Math.Cos(roll);
            double sinRoll = Math.Sin(roll);
            double cosPitch = Math.Cos(pitch);
            double sinPitch = Math.Sin(pitch);
            double cosYaw = Math.Cos(yaw);
            double sinYaw = Math.Sin(yaw);

            return new()
            {
                _array = new double[4, 4]
                {
                    {
                        cosYaw * cosPitch,
                        cosYaw * sinPitch * sinRoll - sinYaw * cosRoll,
                        cosYaw * sinPitch * cosRoll + sinYaw * sinRoll,
                        0,
                    },
                    {
                        sinYaw * cosPitch,
                        sinYaw * sinPitch * sinRoll + cosYaw * cosRoll,
                        sinYaw * sinPitch * cosRoll - cosYaw * sinRoll,
                        0,
                    },
                    {
                        -sinPitch,
                        cosPitch * sinRoll,
                        cosPitch * cosRoll,
                        0,
                    },
                    {
                        0,
                        0,
                        0,
                        1
                    }
                }
            };
        }

        public static DVector3 operator *(DMatrix4x4 m, DVector3 v)
        {
            return new()
            {
                x = m[0,0] * v.x + m[0,1] * v.y + m[0,2] * v.z + m[0,3],
                y = m[1,0] * v.x + m[1,1] * v.y + m[1,2] * v.z + m[1,3],
                z = m[2,0] * v.x + m[2,1] * v.y + m[2,2] * v.z + m[2,3]
            };
        }

        public static DMatrix4x4 operator *(DMatrix4x4 m1, DMatrix4x4 m2)
        {
            DMatrix4x4 result = Identity;
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    result[i, j] = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        result[i, j] += m1[i, k] * m2[k, j];
                    }
                }
            }
            return result;
        }
    }
}