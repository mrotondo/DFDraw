using SDF;
using UnityEngine;

public class FernSpiral : MonoBehaviour
{
    public uint SdfVolumeSideLength;

    private VolumeTexture _sdfVolumeTexture;
    private Marker _marker;

    public Vector3 MarkerPosition = new(0.25f, 0.0f, 0.5f);
    public float MarkerMovementSpeed = 0.5f; // world units / second
    public Vector3 MarkerDirection = new(-0.5f, 0.5f, 0f);
    public Quaternion MarkerDirectionDrift = Quaternion.AngleAxis(-90, Vector3.forward);
    public float MarkerMaxTurnSpeed = 45f; // degrees / second
    public float MarkerMaxTurnSpeedGrowthRate = 1.0003f; // growth rate / second
    public float MarkerRadius = 0.1f;
    public float MarkerRadiusGrowthRate = 0.999f; // growth rate / second

    void Start()
    {
        _sdfVolumeTexture = new VolumeTexture(SdfVolumeSideLength);
        _sdfVolumeTexture.ConfigureRenderer(GetComponent<DFRenderer>());

        _marker = new(MarkerPosition, Quaternion.identity, MarkerRadius);
    }

    void Update()
    {
        if (MarkerRadius > 0.01f)
        {
            MarkerPosition += MarkerDirection * MarkerMovementSpeed * Time.deltaTime;
            Quaternion rotation = Quaternion.RotateTowards(Quaternion.identity, MarkerDirectionDrift, MarkerMaxTurnSpeed * Time.deltaTime);
            MarkerDirection = rotation * MarkerDirection;
            _marker.MarkTo(MarkerPosition, Quaternion.identity, MarkerRadius);

            MarkerMaxTurnSpeed *= 1 + (MarkerMaxTurnSpeedGrowthRate - 1) * Time.deltaTime;
            MarkerRadius *= 1 + (MarkerRadiusGrowthRate - 1) * Time.deltaTime;
        }

        _marker.Render(_sdfVolumeTexture);
    }
}
