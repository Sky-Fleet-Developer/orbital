using UnityEngine;

namespace Orbital.View
{
    public class CircleViewBuffers
    {
        public GraphicsBuffer Indices { get; private set; }
        public GraphicsBuffer Positions { get; private set; }
        public GraphicsBuffer Matrices { get; private set; }
        private int _matricesCount;
    
        public int RegisterMatrix()
        {
            return _matricesCount++;
        }
        
        public void PrepareBuffers(int accuracy)
        {
            Indices = new GraphicsBuffer(GraphicsBuffer.Target.Structured, accuracy * 2 + 2, sizeof(int));
            {
                int[] arr = new int[accuracy * 2];
                for (int i = 0; i < accuracy; i++)
                {
                    arr[i * 2] = i;
                    arr[i * 2 + 1] = i + 1;
                }
                Indices.SetData(arr);
            }
            Positions = new GraphicsBuffer(GraphicsBuffer.Target.Structured, accuracy + 1, 2 * sizeof(float));
            {
                Vector2[] arr = new Vector2[accuracy + 1];
                for (int i = 0; i <= accuracy; i++)
                {
                    float angle = ((float) i / accuracy) * Mathf.PI * 2;
                    arr[i] = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                }
                Positions.SetData(arr);
            }
        }

        public void RefreshMatrices()
        {
            Matrices = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _matricesCount, 16 * sizeof(float));
        }

        public void SetMatrix(int matrixId, Matrix4x4 value)
        {
            Matrices.SetData(new [] {value}, 0, matrixId, 1);
        }

        public void ReleaseBuffers()
        {
            Indices.Dispose();
            Indices = null;
            Positions.Dispose();
            Positions = null;
            Matrices.Dispose();
            Matrices = null;
        }
    }
}