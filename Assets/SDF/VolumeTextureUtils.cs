using System;
using Unity.Collections;
using UnityEngine;

namespace SDF
{
    public static class VolumeTexture
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

        // TODO: Naive implementation! Could be improved by breaking the texture up into cells and only updating cells that overlap with the sphere
        public static void UpdateSdf(Texture3D sdfVolumeTexture, Func<Vector3, float, (bool, float)> distanceFunc)
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
                        if (shouldWrite)
                        {
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

        private static byte DistanceToByte(float distance)
        {
            return (byte)((distance * 0.5f + 0.5f) * 255); // [-1, 1] => [0, 255]
        }

        private static float ByteToDistance(byte alpha)
        {
            return (alpha / 255.0f) * 2 - 1; // [0, 255] => [-1, 1]
        }
    }
}