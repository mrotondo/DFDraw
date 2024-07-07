using System.Collections.Generic;
using SDF;
using UnityEngine;

public class Marker
{
    private Vector3 _position;
    private Quaternion _orientation;
    private float _scale;

    private readonly List<Mark> _marks;
    private readonly float _translationMarkThreshold = 0.03f;  // units: world space distance
    private readonly float _rotationMarkThreshold = 5f;  // units: degrees
    private readonly float _scaleMarkThreshold = 0.2f;  // units: ratio

    public Marker(Vector3 initialPosition, Quaternion initialOrientation, float initialScale)
    {
        _position = initialPosition;
        _orientation = initialOrientation;
        _scale = Mathf.Max(initialScale, Mathf.Epsilon);

        _marks = new List<Mark>
        {
            new(_position, _orientation, _scale)
        };
    }

    // TODO: Only update after cumulative movement/rotation/scale that passes a threshold
    public void MarkTo(Vector3 newPosition, Quaternion newOrientation, float newScale)
    {
        int numTranslationMarks = Mathf.CeilToInt(Vector3.Distance(_position, newPosition) / _translationMarkThreshold);

        int numRotationMarks = Mathf.CeilToInt(Quaternion.Angle(_orientation, newOrientation) / _rotationMarkThreshold);

        newScale = Mathf.Max(newScale, Mathf.Epsilon);
        float scaleRatio = Mathf.Min(_scale, newScale) / Mathf.Max(_scale, newScale);
        int numScaleMarks = Mathf.CeilToInt(scaleRatio / _scaleMarkThreshold);

        int numMarks = Mathf.Max(numTranslationMarks, numRotationMarks, numScaleMarks);
        for (int i = 0; i < numMarks; i++)
        {
            float t = (i + 1.0f) / numMarks;
            Vector3 markPosition = Vector3.Lerp(_position, newPosition, t);
            Quaternion markOrientation = Quaternion.Slerp(_orientation, newOrientation, t);
            float markScale = Mathf.Lerp(_scale, newScale, t);  // Won't generate linear size changes, but might be fine
            _marks.Add(new(markPosition, markOrientation, markScale));
        }

        _position = newPosition;
        _orientation = newOrientation;
        _scale = newScale;
    }

    public void Render(VolumeTexture sdfVolumetexture)
    {
        foreach (var mark in _marks)
        {
            sdfVolumetexture.BlitSphere(mark.Position, mark.Scale);
        }
        _marks.Clear();
    }

    private class Mark
    {
        public Vector3 Position;
        public Quaternion Orientation;
        public float Scale;

        public Mark(Vector3 position, Quaternion orientation, float scale)
        {
            Position = position;
            Orientation = orientation;
            Scale = scale;
        }
    }
}
