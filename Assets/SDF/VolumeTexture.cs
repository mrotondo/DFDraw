using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System;

namespace SDF
{
    public class VolumeTexture
    {
        private readonly uint _size;
        private readonly RenderTexture _sdfVolumeTexture;
        private readonly RenderTexture _colorVolumeTexture;
        private readonly ComputeShader _updateSdfShader;

        private readonly uint _cellsPerDimension;
        private readonly uint _cellsPerLayer;
        private readonly uint _cellSize;
        private readonly uint _numCells;
        private readonly List<Sphere>[] _sphereQueues;

        public const uint ChunkSize = 1024;
        private readonly ComputeBuffer _sphereBuffer;
        private readonly int _clearKernel;
        private readonly int _blitSpheresKernel;

        private struct Sphere
        {
            public Vector3 position;
            public float radius;
            public Vector3 color;
            float unusedPadding;
        }

        public VolumeTexture(uint size, uint cellsPerDimension)
        {
            _size = size;
            _cellsPerDimension = cellsPerDimension;
            _cellsPerLayer = cellsPerDimension * cellsPerDimension;
            _numCells = cellsPerDimension * cellsPerDimension * cellsPerDimension;
            _cellSize = size / cellsPerDimension;

            _sdfVolumeTexture = new RenderTexture((int)size, (int)size, 0, RenderTextureFormat.RFloat)
            {
                enableRandomWrite = true,
                dimension = TextureDimension.Tex3D,
                volumeDepth = (int)size,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            _sdfVolumeTexture.Create();

            _colorVolumeTexture = new RenderTexture((int)size, (int)size, 0, RenderTextureFormat.RGB111110Float)
            {
                enableRandomWrite = true,
                dimension = TextureDimension.Tex3D,
                volumeDepth = (int)size,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            _colorVolumeTexture.Create();

            _sphereQueues = new List<Sphere>[_numCells];
            for (int i = 0; i < _numCells; i++)
            {
                _sphereQueues[i] = new();
            }

            int sizeOfSphereInBytes = 8 * 4;
            _sphereBuffer = new((int)ChunkSize, sizeOfSphereInBytes);

            _updateSdfShader = StaticResourcesLoader.UpdateSdfShader;

            _clearKernel = _updateSdfShader.FindKernel("Clear");
            _updateSdfShader.SetTexture(_clearKernel, "SdfVolumeTexture", _sdfVolumeTexture);
            _updateSdfShader.SetTexture(_clearKernel, "ColorVolumeTexture", _colorVolumeTexture);

            _blitSpheresKernel = _updateSdfShader.FindKernel("BlitSpheres");
            _updateSdfShader.SetTexture(_blitSpheresKernel, "SdfVolumeTexture", _sdfVolumeTexture);
            _updateSdfShader.SetTexture(_blitSpheresKernel, "ColorVolumeTexture", _colorVolumeTexture);
            _updateSdfShader.SetBuffer(_blitSpheresKernel, "Spheres", _sphereBuffer);

            Clear(1.0f, Color.red);
        }

        private Vector3Int MinTexelForCell(uint cellIndex)
        {
            uint cellX = cellIndex % _cellsPerDimension;
            uint cellY = cellIndex % _cellsPerLayer / _cellsPerDimension;
            uint cellZ = cellIndex / _cellsPerLayer;
            return new Vector3Int((int)cellX, (int)cellY, (int)cellZ) * (int)_cellSize;
        }

        private (int, int, int) CellIndexForPosition(Vector3 position)
        {
            return ((int)(position.x * _cellsPerDimension),
                    (int)(position.y * _cellsPerDimension),
                    (int)(position.z * _cellsPerDimension));
        }

        public void ConfigureRenderer(DFRenderer renderer)
        {
            renderer.SdfVolumeTexture = _sdfVolumeTexture;
            renderer.ColorVolumeTexture = _colorVolumeTexture;
        }

        public void Render()
        {
            for (uint i = 0; i < _numCells; i++)
            {
                RenderCell(i);
            }
        }

        private void RenderCell(uint cellIndex)
        {
            // TODO: Revisit if cell size ever becomes not divisible by thread group size
            _updateSdfShader.GetKernelThreadGroupSizes(_blitSpheresKernel, out uint x, out uint y, out uint z);
            int xThreadGroups = (int)(_cellSize / x);
            int yThreadGroups = (int)(_cellSize / y);
            int zThreadGroups = (int)(_cellSize / z);

            var sphereQueue = _sphereQueues[(int)cellIndex];
            Vector3Int cellTexelOffset = MinTexelForCell(cellIndex);
            _updateSdfShader.SetInts("TexelOffset", new int[] { cellTexelOffset.x, cellTexelOffset.y, cellTexelOffset.z, 0 });

            for (uint i = 0; i < Mathf.CeilToInt((float)sphereQueue.Count / ChunkSize); i++)
            {
                uint startIndex = i * ChunkSize;
                uint numSpheres = Math.Min(ChunkSize, (uint)sphereQueue.Count - startIndex);

                _updateSdfShader.SetInt("NumSpheres", (int)numSpheres);
                _sphereBuffer.SetData<Sphere>(sphereQueue, (int)startIndex, 0, (int)numSpheres);
                _updateSdfShader.Dispatch(_blitSpheresKernel, xThreadGroups, yThreadGroups, zThreadGroups);
            }

            sphereQueue.Clear();
        }

        public void EnqueueSphere(Vector3 position, float radius, Color color)
        {
            (int cellX, int cellY, int cellZ) = CellIndexForPosition(position);
            uint sphereQueueIndex = (uint)(cellZ * _cellsPerLayer + cellY * _cellsPerDimension + cellX);

            Sphere sphere = new() { position = position, radius = radius, color = new Vector3(color.r, color.g, color.b) };

            if (sphereQueueIndex >= 0 && sphereQueueIndex < _numCells)
            {
                var sphereQueue = _sphereQueues[(int)sphereQueueIndex];
                sphereQueue.Add(sphere);
                if (sphereQueue.Count == ChunkSize)
                {
                    RenderCell(sphereQueueIndex);
                }
            }

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        int offsetX = cellX + x;
                        int offsetY = cellY + y;
                        int offsetZ = cellZ + z;
                        uint offsetSphereQueueIndex = (uint)(offsetZ * _cellsPerLayer + offsetY * _cellsPerDimension + offsetX);
                        if (offsetSphereQueueIndex >= 0 && offsetSphereQueueIndex < _numCells)
                        {
                            var sphereQueue = _sphereQueues[(int)offsetSphereQueueIndex];
                            sphereQueue.Add(sphere);
                            if (sphereQueue.Count == ChunkSize)
                            {
                                RenderCell(offsetSphereQueueIndex);
                            }
                        }
                    }
                }
            }
        }

        public void Clear(float clearDistance, Color clearColor)
        {
            _updateSdfShader.SetFloat("ClearDistance", clearDistance);
            _updateSdfShader.SetVector("ClearColor", clearColor);
            _updateSdfShader.GetKernelThreadGroupSizes(_clearKernel, out uint x, out uint y, out uint z);
            _updateSdfShader.Dispatch(_clearKernel, (int)(_size / x), (int)(_size / y), (int)(_size / z));
        }
    }
}