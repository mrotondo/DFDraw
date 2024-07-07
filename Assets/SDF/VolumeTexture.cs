using Unity.Collections;
using UnityEngine;
using static UnityEngine.Mathf;

namespace SDF
{
    public class VolumeTexture
    {
        private readonly Texture3D _sdfVolumeTexture;

        public VolumeTexture(int size)
        {
            // var texture = new RenderTexture(size, size, 0, RenderTextureFormat.R8)
            // {
            //     enableRandomWrite = true,
            //     dimension = TextureDimension.Tex3D,
            //     volumeDepth = size,
            //     wrapMode = TextureWrapMode.Clamp,
            //     filterMode = FilterMode.Bilinear
            // };
            // texture.Create();

            _sdfVolumeTexture = new Texture3D(size, size, size, TextureFormat.Alpha8, false, true)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Clear(1.0f);
        }

        public void ConfigureRenderer(DFRenderer renderer)
        {
            renderer.SdfVolumeTexture = _sdfVolumeTexture;
        }

        public delegate (bool, float) DistanceFunc(Vector3 samplePosition, float oldDistance);

        // TODO: Put this in a compute shader and see what happens
        // This currently relies on the fact we're a unit cube with our lower front left at 0,0,0
        private static void Update(Texture3D sdfVolumeTexture, Bounds bounds, DistanceFunc distanceFunc)
        {
            NativeArray<byte> data = sdfVolumeTexture.GetPixelData<byte>(0);

            Vector3Int textureSize = new(sdfVolumeTexture.width, sdfVolumeTexture.height, sdfVolumeTexture.depth);
            Vector3 min = Vector3.Scale(bounds.min, textureSize);
            Vector3 max = Vector3.Scale(bounds.max, textureSize);

            // assumes cubic sdf volume texture
            int minUpdateHalfSize = Mathf.CeilToInt(textureSize.x * 0.025f);

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

        public void UpdateArea(Vector3 centroid, float approximateBoundingRadius, DistanceFunc distanceFunc)
        {
            Update(_sdfVolumeTexture, new(centroid, Vector3.one * approximateBoundingRadius), distanceFunc);
        }

        public void UpdateEntire(DistanceFunc distanceFunc)
        {
            Update(_sdfVolumeTexture, new(Vector3.one * 0.5f, Vector3.one), distanceFunc);
        }

        private void Clear(float clearDistance)
        {
            UpdateEntire((samplePosition, oldDistance) =>
            {
                return (true, clearDistance);
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