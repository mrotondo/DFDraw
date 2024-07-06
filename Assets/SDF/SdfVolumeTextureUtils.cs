using System;
using Unity.Collections;
using UnityEngine;

public class SdfVolumeTextureUtils
{
    public static Texture3D CreateCubeSdfVolumeTexture(int size)
    {
        var texture = new Texture3D(size, size, size, TextureFormat.Alpha8, false, true)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        InitSdfVolumeTexture(texture, 1.0f);

        return texture;
    }

    private static void UpdateSdf(Texture3D sdfVolumeTexture, Func<Vector3, float, (bool, float)> distanceFunc)
    {
        NativeArray<byte> data = sdfVolumeTexture.GetPixelData<byte>(0);

        for (int z = 0; z < sdfVolumeTexture.depth; z++)
        {
            int zOffset = z * sdfVolumeTexture.height * sdfVolumeTexture.width;
            for (int y = 0; y < sdfVolumeTexture.height; y++)
            {
                int yOffset = y * sdfVolumeTexture.width;
                for (int x = 0; x < sdfVolumeTexture.width; x++)
                {
                    // This only works because we're a unit cube with our lower front left at 0,0,0
                    // We'll need a smarter transform later to get cell position
                    Vector3 samplePosition = new(
                        x / (sdfVolumeTexture.width - 1.0f),
                        y / (sdfVolumeTexture.height - 1.0f),
                        z / (sdfVolumeTexture.depth - 1.0f));

                    float oldDistance = ByteToDistance(data[x + yOffset + zOffset]);
                    (bool shouldWrite, float newDistance) = distanceFunc(samplePosition, oldDistance);
                    if (shouldWrite) {
                        data[x + yOffset + zOffset] = DistanceToByte(newDistance);
                    }
                }
            }
        }
        sdfVolumeTexture.SetPixelData<byte>(data, 0);
        sdfVolumeTexture.Apply();
    }

    private static void InitSdfVolumeTexture(Texture3D sdfVolumeTexture, float initDistance)
    {
        UpdateSdf(sdfVolumeTexture, (samplePosition, oldDistance) =>
        {
            return (true, initDistance);
        });
    }

    public static void BlitSphereToSdfVolumeTexture(Texture3D sdfVolumeTexture, Matrix4x4 trs)
    {
        BlitShapeToSdfVolumeTexture(sdfVolumeTexture, trs, UnitSphereDistance);
    }

    public static void BlitBoxToSdfVolumeTexture(Texture3D sdfVolumeTexture, Matrix4x4 trs)
    {
        BlitShapeToSdfVolumeTexture(sdfVolumeTexture, trs, UnitCubeDistance);
    }

    // Naive implementation! Could be improved by breaking the texture up into cells and only updating cells that overlap with the sphere
    // Warning! Do not use non-uniform scale vectors: At least according to Inigo, they can't generate correct SDF:
    // https://iquilezles.org/articles/distfunctions/#:~:text=with%20uniform%20scaling.-,Non%20uniform%20scaling,-is%20not%20possible
    private static void BlitShapeToSdfVolumeTexture(Texture3D sdfVolumeTexture, Matrix4x4 objectTrs, Func<Vector3, float> shapeDistanceFunction)
    {
        UpdateSdf(sdfVolumeTexture, (samplePosition, oldDistance) =>
        {
            Vector3 samplePositionInObjectSpace = WorldToObjectSpace(samplePosition, objectTrs);
            float objectSpaceDistance = shapeDistanceFunction(samplePositionInObjectSpace);
            float newDistance = MinimumObjectToWorldScaleFactor(objectTrs) * objectSpaceDistance;
            // return SmoothMinCircular(newDistance, oldDistance, 0.01f);
            return (newDistance < oldDistance, newDistance);
        });
    }

    private static float SmoothMinCircular(float a, float b, float k)
    {
        const float sqrt_of_one_half = 0.7071067812f;
        const float kNorm = 1.0f / (1.0f - sqrt_of_one_half);
        k *= kNorm;
        float h = Mathf.Max(k - Mathf.Abs(a - b), 0.0f) / k;
        return Mathf.Min(a, b) - k * 0.5f * (1.0f + h - Mathf.Sqrt(1.0f - h * (h - 2.0f)));
    }

    private static Vector4 WorldToObjectSpace(Vector3 position, Matrix4x4 objectTrs)
    {
        Vector4 homogeneousPoint = position;
        homogeneousPoint.w = 1;
        return objectTrs.inverse * homogeneousPoint;
    }

    // Assumes a uniform scale (see warning above)
    private static float MinimumObjectToWorldScaleFactor(Matrix4x4 objectTrs)
    {
        return (objectTrs * Vector3.right).magnitude;
    }

    private static float UnitSphereDistance(Vector3 samplePoint)
    {
        return SphereDistance(samplePoint, 0.5f);
    }

    private static float SphereDistance(Vector3 samplePoint, float radius)
    {
        return samplePoint.magnitude - radius;
    }

    private static float UnitCubeDistance(Vector3 samplePoint)
    {
        return BoxDistance(samplePoint, Vector3.one * 0.5f);
    }

    private static float BoxDistance(Vector3 samplePoint, Vector3 size)
    {
        return Vector3.Max(VectorUtils.Abs(samplePoint) - size, Vector3.zero).magnitude;
    }

    private static byte DistanceToByte(float distance)
    {
        return (byte)((distance * 0.5f + 0.5f) * 255); // [-1, 1] => [0, 255]
    }

    private static float ByteToDistance(byte alpha)
    {
        return (alpha / 255.0f) * 2 - 1; // [0, 255] => [-1, 1]
    }

}
