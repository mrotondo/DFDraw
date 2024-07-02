using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DFRenderer : MonoBehaviour
{
    public Material dfMaterial;

    public GameObject box;

    public int SdfVolumeSideLength;

    public float DistanceThreshold;
    public int MaxSteps;
    public float MaxMarchLength;

    private Texture3D _sdfVolumeTexture;

    // Start is called before the first frame update
    void Start()
    {
        // Try without createUnitialized=true if things aren't working
        // Try to find a smaller representation for this later - maybe A8? Since we only need distance.
        _sdfVolumeTexture = SdfVolumeTextureUtils.CreateCubeSdfVolumeTexture(SdfVolumeSideLength);
        dfMaterial.SetTexture("_SdfVolumeTexture", _sdfVolumeTexture);
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

        dfMaterial.SetFloat("_DistanceThreshold", DistanceThreshold);
        dfMaterial.SetInt("_MaxSteps", MaxSteps);
        dfMaterial.SetFloat("_MaxMarchLength", MaxMarchLength);
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        Graphics.Blit(src, null, dfMaterial);
    }
}
