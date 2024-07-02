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

        BlitSphereToSdfVolumeTexture(texture, new Vector3(0.5f, 0.5f, 0.5f), 0.25f);
        BlitSphereToSdfVolumeTexture(texture, new Vector3(0.7f, 0.7f, 0.7f), 0.2f);
        BlitSphereToSdfVolumeTexture(texture, new Vector3(0.2f, 0.2f, 0.2f), 0.1f);

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
    public static void BlitSphereToSdfVolumeTexture(Texture3D sdfVolumeTexture, Vector3 sphereCenter, float sphereRadius)
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
                    Vector3 position = new(
                        x / (sdfVolumeTexture.width - 1.0f),
                        y / (sdfVolumeTexture.height - 1.0f),
                        z / (sdfVolumeTexture.depth - 1.0f));
                    float sphereDistance = Vector3.Distance(position, sphereCenter) - sphereRadius;

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

    private static byte DistanceToByte(float distance)
    {
        return (byte)((distance * 0.5f + 0.5f) * 255); // [-1, 1] => [0, 255]
    }

    private static float ByteToDistance(byte alpha)
    {
        return (alpha / 255.0f) * 2 - 1; // [0, 255] => [-1, 1]
    }

}
