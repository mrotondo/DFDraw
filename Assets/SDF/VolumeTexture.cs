using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEngine.Mathf;

namespace SDF
{
    public class VolumeTexture
    {
        private readonly uint _size;
        private readonly RenderTexture _sdfVolumeTexture;
        private readonly ComputeShader _updateSdfShader;

        public VolumeTexture(uint size)
        {
            _size = size;

            _sdfVolumeTexture = new RenderTexture((int)size, (int)size, 0, RenderTextureFormat.R8)
            {
                enableRandomWrite = true,
                dimension = TextureDimension.Tex3D,
                volumeDepth = (int)size,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            _sdfVolumeTexture.Create();

            _updateSdfShader = StaticResourcesLoader.UpdateSdfShader;
            int clearKernel = _updateSdfShader.FindKernel("Clear");
            _updateSdfShader.SetTexture(clearKernel, "SdfVolumeTexture", _sdfVolumeTexture);
            int blitSphereKernel = _updateSdfShader.FindKernel("BlitSphere");
            _updateSdfShader.SetTexture(blitSphereKernel, "SdfVolumeTexture", _sdfVolumeTexture);

            Clear(1.0f);
        }

        public void ConfigureRenderer(DFRenderer renderer)
        {
            renderer.SdfVolumeTexture = _sdfVolumeTexture;
        }

        public void BlitSphere(Vector3 position, float radius)
        {
            int blitSphereKernel = _updateSdfShader.FindKernel("BlitSphere");
            _updateSdfShader.SetVector("SpherePosition", position);
            _updateSdfShader.SetFloat("SphereRadius", radius);
            _updateSdfShader.GetKernelThreadGroupSizes(blitSphereKernel, out uint x, out uint y, out uint z);
            _updateSdfShader.Dispatch(blitSphereKernel, (int)(_size / x), (int)(_size / y), (int)(_size / z));
        }

        public void Clear(float clearDistance)
        {
            int clearKernel = _updateSdfShader.FindKernel("Clear");
            _updateSdfShader.SetFloat("ClearDistance", clearDistance);
            _updateSdfShader.GetKernelThreadGroupSizes(clearKernel, out uint x, out uint y, out uint z);
            _updateSdfShader.Dispatch(clearKernel, (int)(_size / x), (int)(_size / y), (int)(_size / z));
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