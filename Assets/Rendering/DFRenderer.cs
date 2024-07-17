using UnityEngine;

public class DFRenderer : MonoBehaviour
{
    public Material dfMaterial;

    public float DistanceThreshold;
    public int MaxSteps;
    public float MaxMarchLength;
    public float MaxStepLength;

    public RenderTexture SdfVolumeTexture
    {
        set => dfMaterial.SetTexture("_SdfVolumeTexture", value);
    }

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

        dfMaterial.SetFloat("_DistanceThreshold", DistanceThreshold);
        dfMaterial.SetInt("_MaxSteps", MaxSteps);
        dfMaterial.SetFloat("_MaxMarchLength", MaxMarchLength);
        dfMaterial.SetFloat("_MaxStepLength", MaxStepLength);
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        Graphics.Blit(src, null, dfMaterial);
    }
}
