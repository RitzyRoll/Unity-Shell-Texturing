using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ShellTextureGeo : MonoBehaviour
{
    [Serializable, StructLayout(LayoutKind.Sequential)]
    public struct InputVertex
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector2 uv;
    }

    [Serializable, StructLayout(LayoutKind.Sequential)]
    public struct InputTriangle
    {
        public InputVertex vertex0;
        public InputVertex vertex1;
        public InputVertex vertex2;
    }

    public ComputeShader shellTextureGeoCS;
    public Material renderingMaterial;
    [Min(1)]
    public int layers = 1;
    public float heightOffset = 0;
    public bool castShadows = false;
    public bool realtimeUpdate = false;

    private int kernelID;
    private int threadGroupSize;

    private int[] indirectArgs = new int[] {0, 1, 0, 0};

    private List<InputTriangle> inputTriangles;

    private ComputeBuffer inputTrianglesBuffer;
    private ComputeBuffer drawTrianglesBuffer;
    private ComputeBuffer indirectArgsBuffer;

    private const int INPUTTRIANGLES_STRIDE = (3 * (3 + 3 + 2)) * sizeof(float);
    private const int DRAWTRIANGLES_STRIDE = (3 * (3 + 3 + 2 + 4)) * sizeof(float);
    private const int INDIRECTARGS_STRIDE = 4 * sizeof(int);

    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private int triangleCount;
    bool initialized = false;
    private Bounds bounds;

    private void OnEnable()
    {
        mesh = GetComponent<MeshFilter>().sharedMesh;
        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.enabled = false;
        triangleCount = mesh.triangles.Length / 3;

        SetupBuffer();
        SetupData();
        GenerateGeometry();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            initialized = false;
            SetupBuffer();
            SetupData();
            GenerateGeometry();
        }
    }

    private void OnDisable()
    {
        ReleaseBuffers();
    }

    private void SetupBuffer()
    {
        inputTrianglesBuffer = new ComputeBuffer(triangleCount, INPUTTRIANGLES_STRIDE, ComputeBufferType.Structured, ComputeBufferMode.Immutable);
        drawTrianglesBuffer = new ComputeBuffer(triangleCount * layers, DRAWTRIANGLES_STRIDE, ComputeBufferType.Append | ComputeBufferType.Structured);
        indirectArgsBuffer = new ComputeBuffer(1, INDIRECTARGS_STRIDE, ComputeBufferType.IndirectArguments);
    }

    private void ReleaseBuffers()
    {
        ReleaseBuffer(inputTrianglesBuffer);
        ReleaseBuffer(drawTrianglesBuffer);
        ReleaseBuffer(indirectArgsBuffer);
    }

    private void ReleaseBuffer(ComputeBuffer buffer)
    {
        if (buffer != null)
        {
            buffer.Release();
            buffer = null;
        }
    }

    private void SetupData()
    {
        if (mesh == null)
        {
            return;
        }

        inputTriangles = new List<InputTriangle>();
        for (int i = 0; i < triangleCount; i++)
        {
            InputTriangle inputTriangle = new InputTriangle();
            inputTriangles.Add(inputTriangle);
        }

        for (int i = 0; i<mesh.triangles.Length; i+=3)
        {
            int trianle = i / 3;
            InputTriangle tri = inputTriangles[trianle];

            tri.vertex0.position = mesh.vertices[mesh.triangles[i]];
            tri.vertex0.normal = mesh.normals[mesh.triangles[i]];
            tri.vertex0.uv = mesh.uv[mesh.triangles[i]];

            tri.vertex1.position = mesh.vertices[mesh.triangles[i + 1]];
            tri.vertex1.normal = mesh.normals[mesh.triangles[i + 1]];
            tri.vertex1.uv = mesh.uv[mesh.triangles[i + 1]];

            tri.vertex2.position = mesh.vertices[mesh.triangles[i + 2]];
            tri.vertex2.normal = mesh.normals[mesh.triangles[i + 2]];
            tri.vertex2.uv = mesh.uv[mesh.triangles[i + 2]];

            inputTriangles[trianle] = tri;
        }

        inputTrianglesBuffer.SetData(inputTriangles);
        drawTrianglesBuffer.SetCounterValue(0);
        indirectArgsBuffer.SetData(indirectArgs);
        bounds = meshRenderer.bounds;
        //bounds.Expand(heightOffset);
    }

    private void GenerateGeometry()
    {
        if (mesh == null || shellTextureGeoCS == null || renderingMaterial == null)
        {
            return;
        }

        kernelID = shellTextureGeoCS.FindKernel("ShellTextureGeo");
        shellTextureGeoCS.GetKernelThreadGroupSizes(kernelID, out uint threadGroupSizeX, out _, out _);
        threadGroupSize = Mathf.CeilToInt((float)triangleCount / threadGroupSizeX);

        shellTextureGeoCS.SetBuffer(kernelID, "_InputTrianglesBuffer", inputTrianglesBuffer);
        shellTextureGeoCS.SetBuffer(kernelID, "_DrawTrianglesBuffer", drawTrianglesBuffer);
        shellTextureGeoCS.SetBuffer(kernelID, "_IndirectArgsBuffer", indirectArgsBuffer);

        shellTextureGeoCS.SetInt("_TriangleCount", triangleCount);

        shellTextureGeoCS.SetMatrix("_LocalToWorld", transform.localToWorldMatrix);
        shellTextureGeoCS.SetInt("_Layers", layers);
        shellTextureGeoCS.SetFloat("_HeightOffset", heightOffset);

        renderingMaterial.SetBuffer("_DrawTrianglesBuffer", drawTrianglesBuffer);
        shellTextureGeoCS.Dispatch(kernelID, threadGroupSize, 1, 1);

        initialized = true;
    }

    private void Update()
    {
        if (!initialized) { return; }

        // Если включен реал-тайм режим — перегенерируем геометрию
        if (realtimeUpdate)
        {
            // Сбрасываем счётчик буфера перед записью
            drawTrianglesBuffer.SetCounterValue(0);

            // Сбрасываем аргументы для индиректной отрисовки
            indirectArgs[0] = 0; // numVerticiesPerInstance = 0
            indirectArgsBuffer.SetData(indirectArgs);

            // Обновляем матрицу трансформации
            shellTextureGeoCS.SetMatrix("_LocalToWorld", transform.localToWorldMatrix);

            // Пересчитываем геометрию
            shellTextureGeoCS.Dispatch(kernelID, threadGroupSize, 1, 1);
        }

        Graphics.DrawProceduralIndirect(
            renderingMaterial,
            meshRenderer.bounds,
            MeshTopology.Triangles,
            indirectArgsBuffer,
            0,
            null,
            null,
            castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off,
            true,
            gameObject.layer);
    }
}
