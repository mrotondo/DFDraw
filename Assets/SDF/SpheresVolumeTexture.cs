using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System;
using Unity.Collections;

namespace SDF
{
    public class SpheresVolumeTexture
    {
        private readonly uint _size;
        private readonly RenderTexture _sdfVolumeTexture;
        private readonly RenderTexture _colorVolumeTexture;
        private readonly ComputeShader _updateSdfShader;

        private readonly uint _cellsPerDimension;
        private readonly uint _cellsPerLayer;
        private readonly uint _cellSize;
        private readonly uint _numCells;
        private readonly Sphere[][] _sphereQueues;
        private readonly uint[] _sphereCounts;

        public const uint ChunkSize = 1024;
        private ComputeBuffer _sphereBuffer;
        private readonly int _clearKernel;
        private readonly int _blitSpheresKernel;

        private struct Sphere
        {
            public Vector3 position;
            public float radius;
            public Vector3 color;
            float unusedPadding;
        }

        public SpheresVolumeTexture(uint size, uint cellsPerDimension)
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

            _sphereQueues = new Sphere[_numCells][];
            _sphereCounts = new uint[_numCells];
            for (int i = 0; i < _numCells; i++)
            {
                _sphereQueues[i] = new Sphere[ChunkSize];
            }

            int sizeOfSphereInBytes = 8 * 4;
            _sphereBuffer = new((int)ChunkSize, sizeOfSphereInBytes);

            _updateSdfShader = StaticResourcesLoader.SpheresVolumeTextureShader;

            _clearKernel = _updateSdfShader.FindKernel("Clear");
            _updateSdfShader.SetTexture(_clearKernel, "SdfVolumeTexture", _sdfVolumeTexture);
            _updateSdfShader.SetTexture(_clearKernel, "ColorVolumeTexture", _colorVolumeTexture);

            _blitSpheresKernel = _updateSdfShader.FindKernel("BlitSpheres");
            _updateSdfShader.SetTexture(_blitSpheresKernel, "SdfVolumeTexture", _sdfVolumeTexture);
            _updateSdfShader.SetTexture(_blitSpheresKernel, "ColorVolumeTexture", _colorVolumeTexture);
            _updateSdfShader.SetBuffer(_blitSpheresKernel, "Spheres", _sphereBuffer);

            Clear(1.0f, Color.red);
        }

        public void DisposeOfComputeBuffer()
        {
            _sphereBuffer.Dispose();
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

            var sphereQueue = _sphereQueues[cellIndex];
            var numSpheres = _sphereCounts[cellIndex];
            if (numSpheres > ChunkSize)
            {
                throw new Exception("Tried to render a cell with " + numSpheres + " spheres, more than Chunksize " + ChunkSize);
            }

            Vector3Int cellTexelOffset = MinTexelForCell(cellIndex);
            _updateSdfShader.SetInts("TexelOffset", new int[] { cellTexelOffset.x, cellTexelOffset.y, cellTexelOffset.z, 0 });
            _updateSdfShader.SetInt("NumSpheres", (int)numSpheres);
            _sphereBuffer.SetData<Sphere>(new NativeArray<Sphere>(sphereQueue, Allocator.Temp), 0, 0, (int)numSpheres);
            _updateSdfShader.Dispatch(_blitSpheresKernel, xThreadGroups, yThreadGroups, zThreadGroups);

            _sphereCounts[cellIndex] = 0;
        }

        public void EnqueueSphere(Vector3 position, float radius, Color color)
        {
            (int cellX, int cellY, int cellZ) = CellIndexForPosition(position);
            uint sphereQueueIndex = (uint)(cellZ * _cellsPerLayer + cellY * _cellsPerDimension + cellX);

            if (sphereQueueIndex >= 0 && sphereQueueIndex < _numCells)
            {
                AddSphere(sphereQueueIndex, position, radius, color);
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
                            AddSphere(offsetSphereQueueIndex, position, radius, color);
                        }
                    }
                }
            }
        }

        private void AddSphere(uint cellIndex, Vector3 position, float radius, Color color)
        {
            var numSpheres = _sphereCounts[cellIndex];
            var sphereQueue = _sphereQueues[cellIndex];
            sphereQueue[numSpheres].position = position;
            sphereQueue[numSpheres].radius = radius;
            sphereQueue[numSpheres].color.x = color.r;
            sphereQueue[numSpheres].color.y = color.g;
            sphereQueue[numSpheres].color.z = color.b;

            numSpheres += 1;
            _sphereCounts[cellIndex] = numSpheres;

            if (numSpheres == ChunkSize)
            {
                RenderCell(cellIndex);
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