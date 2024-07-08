using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEngine.Mathf;
using System.Linq;

namespace SDF
{
    public class VolumeTexture
    {
        private readonly uint _size;
        private readonly RenderTexture _sdfVolumeTexture;
        private readonly ComputeShader _updateSdfShader;

        private readonly List<Sphere> _sphereQueue;
        private readonly ComputeBuffer _sphereBuffer;

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

            _sphereQueue = new();
            _sphereBuffer = new(4096, 4 * 4);

            _updateSdfShader = StaticResourcesLoader.UpdateSdfShader;
            int clearKernel = _updateSdfShader.FindKernel("Clear");
            _updateSdfShader.SetTexture(clearKernel, "SdfVolumeTexture", _sdfVolumeTexture);
            int blitSphereKernel = _updateSdfShader.FindKernel("BlitSphere");
            _updateSdfShader.SetTexture(blitSphereKernel, "SdfVolumeTexture", _sdfVolumeTexture);
            int blitSpheresKernel = _updateSdfShader.FindKernel("BlitSpheres");
            _updateSdfShader.SetTexture(blitSpheresKernel, "SdfVolumeTexture", _sdfVolumeTexture);
            _updateSdfShader.SetBuffer(blitSpheresKernel, "Spheres", _sphereBuffer);

            Clear(1.0f);
        }

        public void ConfigureRenderer(DFRenderer renderer)
        {
            renderer.SdfVolumeTexture = _sdfVolumeTexture;
        }

        public void Render()
        {
            int blitSpheresKernel = _updateSdfShader.FindKernel("BlitSpheres");

            _updateSdfShader.SetInt("NumSpheres", _sphereQueue.Count);
            _sphereBuffer.SetData<Sphere>(_sphereQueue);
            _updateSdfShader.GetKernelThreadGroupSizes(blitSpheresKernel, out uint x, out uint y, out uint z);
            _updateSdfShader.Dispatch(blitSpheresKernel, (int)(_size / x), (int)(_size / y), (int)(_size / z));

            _sphereQueue.Clear();
        }

        public void EnqueueSphere(Vector3 position, float radius)
        {
            _sphereQueue.Add(new(position, radius));
        }

        public void Clear(float clearDistance)
        {
            int clearKernel = _updateSdfShader.FindKernel("Clear");
            _updateSdfShader.SetFloat("ClearDistance", clearDistance);
            _updateSdfShader.GetKernelThreadGroupSizes(clearKernel, out uint x, out uint y, out uint z);
            _updateSdfShader.Dispatch(clearKernel, (int)(_size / x), (int)(_size / y), (int)(_size / z));
        }

        public struct Sphere
        {
            public Vector3 Position;
            public float Radius;

            public Sphere(Vector3 position, float radius)
            {
                Position = position;
                Radius = radius;
            }
        }
    }
}