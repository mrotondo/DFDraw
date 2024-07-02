using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DFRenderer : MonoBehaviour
{
    public Material dfMaterial;

    public GameObject box;

    [Range(1, 1000)]
    public int sdfVolumeSideLength;
    public Texture3D sdfVolumeTexture;

    [Range(0, 1)]
    public float distanceThreshold;
    [Range(0, 200)]
    public int maxSteps;
    [Range(0, 200)]
    public float maxMarchLength;


    // Start is called before the first frame update
    void Start()
    {
        // Try without createUnitialized=true if things aren't working
        // Try to find a smaller representation for this later - maybe A8? Since we only need distance.
        sdfVolumeTexture = CreateSdfVolumeTexture(sdfVolumeSideLength);
        dfMaterial.SetTexture("_SdfVolumeTexture", sdfVolumeTexture);
    }

    private Texture3D CreateSdfVolumeTexture(int size) {
        var texture = new Texture3D(sdfVolumeSideLength, sdfVolumeSideLength, sdfVolumeSideLength, TextureFormat.Alpha8, false, true);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color32[] colors = new Color32[size * size * size];

        float inverseResolution = 1.0f / (size - 1.0f);
        for (int z = 0; z < size; z++)
        {
            int zOffset = z * size * size;
            for (int y = 0; y < size; y++)
            {
                int yOffset = y * size;
                for (int x = 0; x < size; x++)
                {
                    // This only works because we're a unit cube with our lower front left at 0,0,0
                    // We'll need a smarter transform later to get cell position
                    Vector3 center = new(0.5f, 0.5f, 0.5f);
                    Vector3 position = new(x * inverseResolution, y * inverseResolution, z * inverseResolution);
                    float sphereRadius = 0.25f;
                    float sphereDistance = Vector3.Distance(position, center) - sphereRadius;

                    colors[x + yOffset + zOffset] = new Color(0, 0, 0, sphereDistance);
                }
            }
        }

        texture.SetPixels32(colors);
        texture.Apply();  
        return texture;
    }

    // Update is called once per frame
    void Update()
    {
        var camera = GetComponent<Camera>();
        dfMaterial.SetVector("_CamPosition", camera.transform.position);
        dfMaterial.SetFloat("_VerticalFieldOfView", Mathf.Deg2Rad * camera.fieldOfView);
        dfMaterial.SetFloat("_FarClipDistance", camera.farClipPlane);
        dfMaterial.SetFloat("_AspectRatio", camera.aspect);
        dfMaterial.SetVector("_CamRight", camera.transform.right);
        dfMaterial.SetVector("_CamUp", camera.transform.up);
        dfMaterial.SetVector("_CamForward", camera.transform.forward);
        dfMaterial.SetMatrix("_CamInverseProjectionMatrix", (camera.cameraToWorldMatrix * camera.projectionMatrix).inverse);

        dfMaterial.SetMatrix("_BoxInverseTransform", box.transform.worldToLocalMatrix);
        dfMaterial.SetMatrix("_BoxTransform", box.transform.localToWorldMatrix);

        dfMaterial.SetFloat("_DistanceThreshold", distanceThreshold);
        dfMaterial.SetInt("_MaxSteps", maxSteps);
        dfMaterial.SetFloat("_MaxMarchLength", maxMarchLength);
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        Graphics.Blit(src, null, dfMaterial);
    }
}
