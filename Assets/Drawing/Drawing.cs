using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Drawing : MonoBehaviour
{
    public int SdfVolumeSideLength;

    private Texture3D _sdfVolumeTexture;
    private Marker _marker;

    private Vector3 _markerPosition;
    private Vector3 _markerDirection;
    private Quaternion _markerDirectionDrift;
    private float _markerMaxTurnSpeed;
    private float _markerMovementSpeed;


    void Start()
    {
        _sdfVolumeTexture = SdfVolumeTextureUtils.CreateCubeSdfVolumeTexture(SdfVolumeSideLength);
        var renderer = GetComponent<DFRenderer>();
        renderer.SdfVolumeTexture = _sdfVolumeTexture;

        _markerPosition = new(0.5f, 0.0f, 0.5f);
        _markerDirection = Vector3.up;
        _markerDirectionDrift = Quaternion.AngleAxis(90, Vector3.forward);
        _markerMaxTurnSpeed = 30f; // degrees / second
        _markerMovementSpeed = 0.5f; // world units / second

        _marker = new(_markerPosition, Quaternion.identity, 0.1f);
    }

    void Update()
    {
        _markerPosition += _markerDirection * _markerMovementSpeed * Time.deltaTime;
        Quaternion rotation = Quaternion.RotateTowards(Quaternion.identity, _markerDirectionDrift, _markerMaxTurnSpeed * Time.deltaTime);
        _markerDirection = rotation * _markerDirection;
        _marker.MarkTo(_markerPosition, Quaternion.identity, 0.1f);

        _marker.Render(_sdfVolumeTexture);
    }
}
