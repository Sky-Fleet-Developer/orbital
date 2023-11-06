using UnityEngine;

namespace Orbital.View.Utilities
{
    public static class MeshUtils
    {
        public static Mesh GenerateLineMesh(string name, int verticesCount)
        {
            var mesh = new Mesh {name = name};
            mesh.MarkDynamic();
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000);
            
            {
                Vector3[] arr = new Vector3[verticesCount];
                for (int i = 0; i < verticesCount; i++)
                {
                    arr[i] = Vector3.zero;
                }
                mesh.SetVertices(arr);
            }
            {
                int[] arr = new int[verticesCount * 2];
                for (int i = 0; i < verticesCount - 1; i++)
                {
                    arr[i * 2] = i;
                    arr[i * 2 + 1] = i + 1;
                }
                arr[verticesCount * 2 - 2] = verticesCount - 1;
                arr[verticesCount * 2 - 1] = 0;
                mesh.SetIndices(arr, MeshTopology.Lines, 0);
            }
            return mesh;
        }
    }
}