using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEngine.Mathf;
using System.Linq;
using System;

namespace SDF
{
    public class VolumeTexture
    {
        private readonly uint _size;
        private readonly RenderTexture _sdfVolumeTexture;
        private readonly ComputeShader _updateSdfShader;

        private readonly uint _cellsPerDimension;
        private readonly uint _cellSize;
        private readonly uint _numCells;
        private readonly List<List<Sphere>> _sphereQueues;

        public uint ChunkSize = 4096;
        private readonly ComputeBuffer _sphereBuffer;
        private int _clearKernel;
        private int _blitSpheresKernel;

        public VolumeTexture(uint size, uint cellsPerDimension)
        {
            _size = size;
            _cellsPerDimension = cellsPerDimension;
            _numCells = cellsPerDimension * cellsPerDimension * cellsPerDimension;
            _cellSize = size / cellsPerDimension;

            _sdfVolumeTexture = new RenderTexture((int)size, (int)size, 0, RenderTextureFormat.R8)
            {
                enableRandomWrite = true,
                dimension = TextureDimension.Tex3D,
                volumeDepth = (int)size,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            _sdfVolumeTexture.Create();

            _sphereQueues = new();
            for (int i = 0; i < _numCells; i++)
            {
                _sphereQueues.Add(new());
            }

            _sphereBuffer = new((int)ChunkSize, 4 * 4);

            _updateSdfShader = StaticResourcesLoader.UpdateSdfShader;

            _clearKernel = _updateSdfShader.FindKernel("Clear");
            _updateSdfShader.SetTexture(_clearKernel, "SdfVolumeTexture", _sdfVolumeTexture);

            _blitSpheresKernel = _updateSdfShader.FindKernel("BlitSpheres");
            _updateSdfShader.SetTexture(_blitSpheresKernel, "SdfVolumeTexture", _sdfVolumeTexture);
            _updateSdfShader.SetBuffer(_blitSpheresKernel, "Spheres", _sphereBuffer);

            Clear(1.0f);
        }

        private Vector3Int MinTexelForCell(uint cellIndex)
        {
            uint cellsPerLayer = _cellsPerDimension * _cellsPerDimension;
            uint cellX = cellIndex % _cellsPerDimension;
            uint cellY = cellIndex % cellsPerLayer / _cellsPerDimension;
            uint cellZ = cellIndex / cellsPerLayer;
            return new Vector3Int((int)cellX, (int)cellY, (int)cellZ) * (int)_cellSize;
        }

        private uint CellIndexForPosition(Vector3 position)
        {

            uint cellX = (uint)Mathf.FloorToInt(position.x * _cellsPerDimension);
            uint cellY = (uint)Mathf.FloorToInt(position.y * _cellsPerDimension);
            uint cellZ = (uint)Mathf.FloorToInt(position.z * _cellsPerDimension);
            return cellZ * _cellsPerDimension * _cellsPerDimension + cellY * _cellsPerDimension + cellX;
        }

        public void ConfigureRenderer(DFRenderer renderer)
        {
            renderer.SdfVolumeTexture = _sdfVolumeTexture;
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

        public void EnqueueSphere(Vector3 position, float radius)
        {
            HashSet<uint> cellsAddedTo = new();  // This could be optimized for less alloc/gc
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        var positionOffset = new Vector3(x * radius, y * radius, z * radius);
                        uint cellIndex = CellIndexForPosition(position + positionOffset);
                        if (!cellsAddedTo.Contains(cellIndex) && cellIndex < _numCells)
                        {
                            var sphereQueue = _sphereQueues[(int)cellIndex];
                            sphereQueue.Add(new(position, radius));
                            cellsAddedTo.Add(cellIndex);
                        }
                    }
                }
            }
        }

        public void Clear(float clearDistance)
        {
            _updateSdfShader.SetFloat("ClearDistance", clearDistance);
            _updateSdfShader.GetKernelThreadGroupSizes(_clearKernel, out uint x, out uint y, out uint z);
            _updateSdfShader.Dispatch(_clearKernel, (int)(_size / x), (int)(_size / y), (int)(_size / z));
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