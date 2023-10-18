using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
public class LineRenderingTest : MonoBehaviour 
{
    public Vector3[] positions;
    public Mesh mesh;
    
    /*GraphicsBuffer meshTriangles;
    GraphicsBuffer meshPositions;*/

    void OnEnable()
    {
        // note: remember to check "Read/Write" on the mesh asset to get access to the geometry data
        /*meshTriangles = new GraphicsBuffer(GraphicsBuffer.Target.Structured, indices.Length, sizeof(int));
        meshTriangles.SetData(indices);
        meshPositions = new GraphicsBuffer(GraphicsBuffer.Target.Structured, positions.Length, 3 * sizeof(float));
        meshPositions.SetData(positions);*/
        mesh = new Mesh();
        mesh.name = "Lines";
        mesh.vertices = positions;
        int[] arr = new int[positions.Length * 2 - 2];
        for (int i = 0; i < positions.Length - 1; i++)
        {
            arr[i * 2] = i;
            arr[i * 2 + 1] = i + 1;
        }
        mesh.SetIndices(arr, MeshTopology.Lines, 0);
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10);
        mesh.MarkModified();
        GetComponent<MeshFilter>().mesh = mesh;
    }

    private void OnValidate()
    {
        OnDisable();
        OnEnable();
    }

    void OnDisable()
    {
        /*meshTriangles?.Dispose();
        meshTriangles = null;
        meshPositions?.Dispose();
        meshPositions = null;*/
    }

    void Update()
    {
       /* RenderParams rp = new RenderParams(material);
        rp.worldBounds = new Bounds(Vector3.zero, 10000*Vector3.one); // use tighter bounds
        rp.matProps = new MaterialPropertyBlock();
        rp.matProps.SetBuffer("_Positions", meshPositions);
        rp.matProps.SetMatrix("_ObjectToWorld", Matrix4x4.Translate(new Vector3(-4.5f, 0, 0)));
        rp.matProps.SetFloat("_NumInstances", 10.0f);
        Graphics.RenderPrimitivesIndexed(rp, MeshTopology.Lines, meshTriangles, meshTriangles.count, 0, 10);*/
    }
}