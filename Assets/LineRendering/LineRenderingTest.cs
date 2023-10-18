using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
public class LineRenderingTest : MonoBehaviour 
{
    public Material material;
    public Vector3[] positions;
    public int[] indices;
    public Camera camera;

    GraphicsBuffer meshTriangles;
    GraphicsBuffer meshPositions;

    void OnEnable()
    {
        // note: remember to check "Read/Write" on the mesh asset to get access to the geometry data
        meshTriangles = new GraphicsBuffer(GraphicsBuffer.Target.Structured, indices.Length, sizeof(int));
        meshTriangles.SetData(indices);
        meshPositions = new GraphicsBuffer(GraphicsBuffer.Target.Structured, positions.Length, 3 * sizeof(float));
        meshPositions.SetData(positions);
    }

    private void OnValidate()
    {
        OnDisable();
        OnEnable();
    }

    void OnDisable()
    {
        meshTriangles?.Dispose();
        meshTriangles = null;
        meshPositions?.Dispose();
        meshPositions = null;
    }

    void Update()
    {
        RenderParams rp = new RenderParams(material);
        rp.worldBounds = new Bounds(Vector3.zero, 10000*Vector3.one); // use tighter bounds
        rp.matProps = new MaterialPropertyBlock();
        rp.matProps.SetBuffer("_Positions", meshPositions);
        rp.matProps.SetInt("_BaseVertexIndex", 0);
        rp.matProps.SetMatrix("_ObjectToWorld", Matrix4x4.Translate(new Vector3(-4.5f, 0, 0)));
        rp.matProps.SetFloat("_NumInstances", 10.0f);
        Graphics.RenderPrimitivesIndexed(rp, MeshTopology.Lines, meshTriangles, meshTriangles.count, 0, 10);
    }
}