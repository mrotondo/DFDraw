using System;
using TreeEditor;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class SdfVolumeTextureUtils
{
    public static Texture3D CreateCubeSdfVolumeTexture(int size)
    {
        var texture = new Texture3D(size, size, size, TextureFormat.Alpha8, true)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        InitSdfVolumeTexture(texture, 1.0f);

        BlitSphereToSdfVolumeTexture(texture, Matrix4x4.TRS(Vector3.one * 0.5f, Quaternion.identity, Vector3.one * 0.5f));
        BlitSphereToSdfVolumeTexture(texture, Matrix4x4.TRS(Vector3.one * 0.7f, Quaternion.identity, Vector3.one * 0.4f));
        BlitSphereToSdfVolumeTexture(texture, Matrix4x4.TRS(Vector3.one * 0.2f, Quaternion.identity, Vector3.one * 0.2f));

        return texture;
    }

    private static void InitSdfVolumeTexture(Texture3D sdfVolumeTexture, float initDistance)
    {
        byte[] data = new byte[sdfVolumeTexture.width * sdfVolumeTexture.height * sdfVolumeTexture.depth];

        for (int z = 0; z < sdfVolumeTexture.depth; z++)
        {
            int zOffset = z * sdfVolumeTexture.height * sdfVolumeTexture.width;
            for (int y = 0; y < sdfVolumeTexture.height; y++)
            {
                int yOffset = y * sdfVolumeTexture.width;
                for (int x = 0; x < sdfVolumeTexture.width; x++)
                {
                    data[x + yOffset + zOffset] = DistanceToByte(initDistance);
                }
            }
        }
        sdfVolumeTexture.SetPixelData<byte>(data, 0);
        sdfVolumeTexture.Apply();
    }

    // Naive implementation! Could be improved by breaking the texture up into cells and only updating cells that overlap with the sphere
    public static void BlitSphereToSdfVolumeTexture(Texture3D sdfVolumeTexture, Matrix4x4 sphereTrs)
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

                    Vector3 samplePositionInObjectSpace = WorldToObjectSpace(samplePosition, sphereTrs);
                    float unscaledSphereDistance = SphereDistance(samplePositionInObjectSpace, 0.5f);
                    float sphereDistance = SafeScaleFactor(sphereTrs) * unscaledSphereDistance;

                    float oldDistance = ByteToDistance(data[x + yOffset + zOffset]);
                    if (sphereDistance < oldDistance)
                    {
                        data[x + yOffset + zOffset] = DistanceToByte(sphereDistance);
                    }
                }
            }
        }
        sdfVolumeTexture.SetPixelData<byte>(data, 0);
        sdfVolumeTexture.Apply();
    }

    private static Vector4 WorldToObjectSpace(Vector3 position, Matrix4x4 objectTrs)
    {
        Vector4 homogeneousPoint = position;
        homogeneousPoint.w = 1;
        return objectTrs.inverse * homogeneousPoint;
    }

    private static float SafeScaleFactor(Matrix4x4 objectTrs)
    {
        Vector3 x = objectTrs * Vector3.right;
        Vector3 y = objectTrs * Vector3.up;
        Vector3 z = objectTrs * Vector3.forward;
        return Math.Min(x.magnitude, Math.Min(y.magnitude, z.magnitude));
    }

    private static float SphereDistance(Vector3 samplePoint, float radius)
    {
        return samplePoint.magnitude - radius;
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
