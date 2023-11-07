using System;
using Unity.Collections;
using UnityEngine;

namespace Ara3D.Double
{
    public struct DMatrix4x4
    {
        private ArrColumns _array;
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

        public DMatrix4x4 GetInverse()
        {
            var value = new DMatrix4x4() {_array = this._array};
            value.Inverse();
            return value;
        }
        
        public void Inverse()
        {
            double[][] augmentedMatrix = new double[4][];
            for (int index = 0; index < 4; index++)
            {
                augmentedMatrix[index] = new double[8];
            }

            // Creating an extended matrix by combining the original matrix with the unit matrix
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    augmentedMatrix[i][j] = _array[i, j];
                    augmentedMatrix[i][j + 4] = (i == j) ? 1.0 : 0.0;
                }
            }

            // Straight Gaussian stroke
            for (int i = 0; i < 4; i++)
            {
                double pivot = augmentedMatrix[i][i];

                if (pivot == 0)
                {
                    throw new InvalidOperationException("The matrix is irreversible.");
                }

                for (int j = 0; j < 2 * 4; j++)
                {
                    augmentedMatrix[i][j] /= pivot;
                }

                for (int k = 0; k < 4; k++)
                {
                    if (k != i)
                    {
                        double factor = augmentedMatrix[k][i];
                        for (int j = 0; j < 8; j++)
                        {
                            augmentedMatrix[k][j] -= factor * augmentedMatrix[i][j];
                        }
                    }
                }
            }

            // Extracting the inverted matrix from the expanded matrix
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    _array[i, j] = augmentedMatrix[i][j + 4];
                }
            }
        }

        public DVector3 Right()
        {
            return new DVector3(_array[0, 0], _array[1, 0], _array[2, 0]);
        }
        
        public DVector3 Up()
        {
            return new DVector3(_array[0, 1], _array[1, 1], _array[2, 1]);
        }
        
        public DVector3 Forward()
        {
            return new DVector3(_array[0, 2], _array[1, 2], _array[2, 2]);
        }
        
        public static DMatrix4x4 Identity
        {
            get
            {
                var arr = new ArrColumns();
                arr[0] = new ArrString(1, 0, 0, 0);
                arr[1] = new ArrString(0, 1, 0, 0);
                arr[2] = new ArrString(0, 0, 1, 0);
                arr[3] = new ArrString(0, 0, 0, 1);
                return new()
                {
                    _array = arr
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

            var arr = new ArrColumns();
            arr[0] = new ArrString(cosYaw * cosPitch,
                cosYaw * sinPitch * sinRoll - sinYaw * cosRoll,
                cosYaw * sinPitch * cosRoll + sinYaw * sinRoll,
                0);
            arr[1] = new ArrString(sinYaw * cosPitch,
                sinYaw * sinPitch * sinRoll + cosYaw * cosRoll,
                sinYaw * sinPitch * cosRoll - cosYaw * sinRoll,
                0);
            arr[2] = new ArrString(-sinPitch,
                cosPitch * sinRoll,
                cosPitch * cosRoll,
                0);
            arr[3] = new ArrString(0,
                0,
                0,
                1);

            return new()
            {
                _array = arr
            };
        }

        public static DMatrix4x4 LookRotation(DVector3 forward, DVector3 up)
        {
            forward = forward.Normalize();
            up = up.Normalize();
            DVector3 right = DVector3.Cross(forward, up);
            up = DVector3.Cross(forward, right);
            var arr = new ArrColumns();
            arr[0] = new ArrString(right.x, up.x, forward.x, 0); 
            arr[1] = new ArrString(right.y, up.y, forward.y, 0); 
            arr[2] = new ArrString(right.z, up.z, forward.z, 0); 
            arr[3] = new ArrString(0, 0, 0, 1); 
            return new ()
            {
                _array = arr
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


        public static implicit operator Matrix4x4(DMatrix4x4 value)
        {
            Matrix4x4 result = new Matrix4x4();
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    result[i, j] = (float)value._array[i, j];
                }
            }

            return result;
        }

        private struct ArrString
        {
            public double v0;
            public double v1;
            public double v2;
            public double v3;

            public ArrString(double v0, double v1, double v2, double v3)
            {
                this.v0 = v0;
                this.v1 = v1;
                this.v2 = v2;
                this.v3 = v3;
            }
            
            public double this[int index]
            {
                get
                {
                    switch (index)
                    {
                        case 0: return v0;
                        case 1: return v1;
                        case 2: return v2;
                        case 3: return v3;
                        default: throw new IndexOutOfRangeException();
                    }
                }
                set
                {
                    switch (index)
                    {
                        case 0: v0 = value; return;
                        case 1: v1 = value; return;
                        case 2: v2 = value; return;
                        case 3: v3 = value; return;
                        default: throw new IndexOutOfRangeException();
                    }
                }
            }
        }
        private struct ArrColumns
        {
            public ArrString v0;
            public ArrString v1;
            public ArrString v2;
            public ArrString v3;

            public ArrString this[int i]
            {
                get
                {
                    switch (i)
                    {
                        case 0: return v0;
                        case 1: return v1;
                        case 2: return v2;
                        case 3: return v3;
                        default: throw new IndexOutOfRangeException();
                    }
                }
                set
                {
                    switch (i)
                    {
                        case 0: v0 = value; return;
                        case 1: v1 = value; return;
                        case 2: v2 = value; return;
                        case 3: v3 = value; return;
                        default: throw new IndexOutOfRangeException();
                    }
                }
            }
            
            public double this[int a, int b]
            {
                get
                {
                    switch (a)
                    {
                        case 0: return v0[b];
                        case 1: return v1[b];
                        case 2: return v2[b];
                        case 3: return v3[b];
                        default: throw new IndexOutOfRangeException();
                    }
                }
                set
                {
                    switch (a)
                    {
                        case 0: v0[b] = value; return;
                        case 1: v1[b] = value; return;
                        case 2: v2[b] = value; return;
                        case 3: v3[b] = value; return;
                        default: throw new IndexOutOfRangeException();
                    }
                }
            }
        }
    }
    
}