using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DFRenderer : MonoBehaviour
{
    public Material dfMaterial;
    public GameObject box;

    [Range(0.0f, 10.0f)]
    public float sampleDistance;

    [Range(0, 1)]
    public float distanceThreshold;
    [Range(0, 200)]
    public int maxSteps;
    [Range(0, 200)]
    public float maxMarchLength;

    // Start is called before the first frame update
    void Start()
    {
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

        dfMaterial.SetFloat("_SampleDistance", sampleDistance);
        dfMaterial.SetFloat("_DistanceThreshold", distanceThreshold);
        dfMaterial.SetInt("_MaxSteps", maxSteps);
        dfMaterial.SetFloat("_MaxMarchLength", maxMarchLength);
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        Graphics.Blit(src, dst, dfMaterial);
    }
}
