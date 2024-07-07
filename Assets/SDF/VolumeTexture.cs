using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEngine.Mathf;

namespace SDF
{
    public static class VolumeTexture
    {
        public static Texture3D CreateCubeSdfVolumeTexture(int size)
        {
            // var texture = new RenderTexture(size, size, 0, RenderTextureFormat.R8);
            // texture.enableRandomWrite = true;
            // texture.dimension = TextureDimension.Tex3D;
            // texture.volumeDepth = size;
            // texture.Create();


            var texture = new Texture3D(size, size, size, TextureFormat.Alpha8, false, true)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            InitSdfVolumeTexture(texture, 1.0f);

            return texture;
        }

        public delegate (bool, float) DistanceFunc(Vector3 samplePosition, float oldDistance);

        // TODO: Put this in a compute shader and see what happens
        // This currently relies on the fact we're a unit cube with our lower front left at 0,0,0
        private static void UpdateSdf(Texture3D sdfVolumeTexture, Bounds bounds, DistanceFunc distanceFunc)
        {
            NativeArray<byte> data = sdfVolumeTexture.GetPixelData<byte>(0);

            Vector3Int textureSize = new(sdfVolumeTexture.width, sdfVolumeTexture.height, sdfVolumeTexture.depth);
            Vector3 min = Vector3.Scale(bounds.min, textureSize);
            Vector3 max = Vector3.Scale(bounds.max, textureSize);

            // assumes cubic sdf volume texture
            int minUpdateHalfSize = (int)(textureSize.x * 0.025f);

            for (int z = Max(0, FloorToInt(min.z) - minUpdateHalfSize); z < Min(textureSize.z, CeilToInt(max.z) + minUpdateHalfSize); z++)
            {
                int zOffset = z * sdfVolumeTexture.height * sdfVolumeTexture.width;
                for (int y = Max(0, FloorToInt(min.y) - minUpdateHalfSize); y < Min(textureSize.y, CeilToInt(max.y) + minUpdateHalfSize); y++)
                {
                    int yOffset = y * sdfVolumeTexture.width;
                    for (int x = Max(0, FloorToInt(min.x) - minUpdateHalfSize); x < Min(textureSize.x, CeilToInt(max.x) + minUpdateHalfSize); x++)
                    {
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

        public static void UpdateSdfStartingAt(
            Texture3D sdfVolumeTexture, Vector3 centroid, float approximateBoundingRadius, DistanceFunc distanceFunc)
        {
            UpdateSdf(sdfVolumeTexture, new(centroid, Vector3.one * approximateBoundingRadius), distanceFunc);
        }

        public static void UpdateEntireSdf(Texture3D sdfVolumeTexture, DistanceFunc distanceFunc)
        {
            UpdateSdf(sdfVolumeTexture, new(Vector3.one * 0.5f, Vector3.one), distanceFunc);
        }

        private static void InitSdfVolumeTexture(Texture3D sdfVolumeTexture, float initDistance)
        {
            UpdateEntireSdf(sdfVolumeTexture, (samplePosition, oldDistance) =>
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