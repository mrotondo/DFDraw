using System.Collections.Generic;
using SDF;
using UnityEngine;

public class Marker
{
    private Vector3 _position;
    private Quaternion _orientation;
    private float _scale;

    private readonly float _translationMarkThreshold = 0.1f;  // units: world space distance
    private readonly float _rotationMarkThreshold = 5f;  // units: degrees
    private readonly float _scaleMarkThreshold = 0.4f;  // units: ratio

    public Marker(VolumeTexture sdfVolumeTexture, Vector3 initialPosition, Quaternion initialOrientation, float initialScale)
    {
        _position = initialPosition;
        _orientation = initialOrientation;
        _scale = Mathf.Max(initialScale, Mathf.Epsilon);

        sdfVolumeTexture.EnqueueSphere(initialPosition, initialScale);
    }

    // TODO: Only update after cumulative movement/rotation/scale that passes a threshold
    public void MarkTo(VolumeTexture sdfVolumeTexture, Vector3 newPosition, Quaternion newOrientation, float newScale)
    {
        float numTranslationMarks = Vector3.Distance(_position, newPosition) / _translationMarkThreshold;

        // int numRotationMarks = Mathf.CeilToInt(Quaternion.Angle(_orientation, newOrientation) / _rotationMarkThreshold);

        newScale = Mathf.Max(newScale, Mathf.Epsilon);
        float scaleRatio = 1 - Mathf.Min(_scale, newScale) / Mathf.Max(_scale, newScale);
        float numScaleMarks = scaleRatio / _scaleMarkThreshold;

        int numMarks = Mathf.CeilToInt(Mathf.Max(numTranslationMarks, /*numRotationMarks,*/ numScaleMarks));
        for (int i = 0; i < numMarks; i++)
        {
            float t = (i + 1.0f) / numMarks;
            Vector3 markPosition = Vector3.Lerp(_position, newPosition, t);
            // Orientation is currently unused while we're just marking with spheres
            // Quaternion markOrientation = Quaternion.Slerp(_orientation, newOrientation, t);
            float markScale = Mathf.Lerp(_scale, newScale, t);  // Won't generate linear size changes, but might be fine

            sdfVolumeTexture.EnqueueSphere(markPosition, markScale);
        }

        _position = newPosition;
        _orientation = newOrientation;
        _scale = newScale;
    }
}
