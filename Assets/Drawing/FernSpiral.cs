using SDF;
using UnityEngine;

public class FernSpiral : MonoBehaviour
{
    public uint SdfVolumeSideLength;
    public uint SdfVolumeNumCellsPerDimension;

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

    public Color InitialColor = Color.black;
    public Vector3 ColorGrowthRate = new(0.1f, 0.1f, 0.1f); // rgb / second
    private Color _color;

    void Start()
    {
        _sdfVolumeTexture = new VolumeTexture(SdfVolumeSideLength, SdfVolumeNumCellsPerDimension);
        _sdfVolumeTexture.ConfigureRenderer(GetComponent<DFRenderer>());

        _color = InitialColor;

        _marker = new(_sdfVolumeTexture, MarkerPosition, Quaternion.identity, MarkerRadius, _color);
    }

    void Update()
    {
        if (MarkerRadius > 0.01f)
        {
            _color = new(
                _color.r + ColorGrowthRate.x * Time.deltaTime,
                _color.g + ColorGrowthRate.y * Time.deltaTime,
                _color.b + ColorGrowthRate.z * Time.deltaTime);

            MarkerPosition += MarkerDirection * MarkerMovementSpeed * Time.deltaTime;
            Quaternion rotation = Quaternion.RotateTowards(Quaternion.identity, MarkerDirectionDrift, MarkerMaxTurnSpeed * Time.deltaTime);
            MarkerDirection = rotation * MarkerDirection;
            _marker.MarkTo(_sdfVolumeTexture, MarkerPosition, Quaternion.identity, MarkerRadius, _color);

            MarkerMaxTurnSpeed *= 1 + (MarkerMaxTurnSpeedGrowthRate - 1) * Time.deltaTime;
            MarkerRadius *= 1 + (MarkerRadiusGrowthRate - 1) * Time.deltaTime;
        }

        _sdfVolumeTexture.Render();
    }
}
