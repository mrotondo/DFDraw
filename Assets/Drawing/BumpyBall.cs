using System.Collections.Generic;
using SDF;
using UnityEngine;

public class BumpyBall : MonoBehaviour
{
    public uint SdfVolumeSideLength;
    public uint SdfVolumeNumCellsPerDimension;

    private SpheresVolumeTexture _sdfVolumeTexture;
    private List<Ball> _balls;

    public int NumBalls = 5;
    public int NumBumps = 10;
    public float ShrinkFactor = 0.5f;

    void Start()
    {
        _sdfVolumeTexture = new SpheresVolumeTexture(SdfVolumeSideLength, SdfVolumeNumCellsPerDimension);
        _sdfVolumeTexture.ConfigureRenderer(GetComponent<DFRenderer>());

        _balls = new List<Ball>();
        for (int i = 0; i < NumBalls; i++)
        {
            Vector3 ballPosition = new(Random.Range(0.2f, 0.8f), Random.Range(0.2f, 0.8f), Random.Range(0.2f, 0.8f));
            float ballRadius = Random.Range(0.05f, 0.1f);
            Color ballColor = Random.ColorHSV();
            _balls.Add(new Ball(_sdfVolumeTexture, ballPosition, ballRadius, ballColor, NumBumps, ShrinkFactor));
        }
    }

    void Update()
    {
        _sdfVolumeTexture.Render();
    }

    private class Ball
    {
        private Marker _marker;
        private List<Ball> bumps;

        public Ball(SpheresVolumeTexture volumeTexture, Vector3 position, float radius, Color color, int numBumps, float shrinkFactor)
        {
            _marker = new(volumeTexture, position, Quaternion.identity, radius, color);

            bumps = new List<Ball>();
            for (int i = 0; i < numBumps; i++)
            {
                Vector3 bumpPosition = position + radius * Random.onUnitSphere;
                float bumpRadius = radius * shrinkFactor;
                bumps.Add(new Ball(volumeTexture, bumpPosition, bumpRadius, color + Color.white * 0.25f, numBumps / 2, shrinkFactor));
            }
        }
    }
}
