using System.Collections.Generic;
using SDF;
using UnityEngine;

public class PineTree : MonoBehaviour
{
    public uint SdfVolumeSideLength = 128;
    public uint SdfVolumeNumCellsPerDimension;

    private VolumeTexture _sdfVolumeTexture;

    public Vector3 BasePosition = new(0.5f, 0.0f, 0.5f);
    public float GrowthRate = 0.5f; // world units / second
    public Vector3 InitialGrowthDirection = new(0f, 1f, 0f);
    public float InitialRadius = 0.2f;  // world units
    public float RadiusGrowthRate = 0.9f; // growth rate / second
    public float TrunkLength = 0.8f;

    public float BranchTimeInterval = 0.5f; // seconds between branches

    public float BranchLength = 0.2f;  // world units
    public float BranchAngleRange = 45f;  // degrees
    public float BranchLengthChangeFactor = 0.9f; // ratio / branch
    public float BranchRadiusChangeFactor = 0.8f; // ratio / branch
    public float BranchAngleRangeChangeFactor = 0.8f; // ratio / branch
    private System.Random _random;
    public int MaxBranchDepth = 5;

    private PineTreeTrunk _trunk;
    private List<PineTreeBranch> _growingBranches;

    void Start()
    {
        _sdfVolumeTexture = new VolumeTexture(SdfVolumeSideLength, SdfVolumeNumCellsPerDimension);
        _sdfVolumeTexture.ConfigureRenderer(GetComponent<DFRenderer>());

        _random = new System.Random();

        _trunk = new(_sdfVolumeTexture, BasePosition, GrowthRate, InitialGrowthDirection, InitialRadius, RadiusGrowthRate, TrunkLength, BranchTimeInterval);

        _growingBranches = new List<PineTreeBranch>();
    }

    void Update()
    {
        if (!_trunk.ReadyToEnd())
        {
            _trunk.GrowAndRender(_sdfVolumeTexture);
        }

        List<PineTreeBranch> finishedBranches = new();

        foreach (var branch in _growingBranches)
        {
            branch.GrowAndRender(_sdfVolumeTexture);
        }
        _growingBranches.RemoveAll(branch => branch.ReadyToEnd());
        if (_trunk.ReadyToBranch())
        {
            _growingBranches.AddRange(_trunk.CreateBranches(_random, _sdfVolumeTexture, BranchRadiusChangeFactor, BranchLengthChangeFactor));
        }

        _sdfVolumeTexture.Render();
    }

    public class PineTreeTrunk
    {
        private readonly Vector3 _initialPosition;
        private Vector3 _position;
        private readonly float _growthRate;
        private readonly Vector3 _growthDirection;
        private float _radius;
        private readonly float _radiusGrowthRate;
        private readonly float _maxLength;
        private readonly Marker _marker;

        private Color _color = new(0.4f, 0.2f, 0.05f);

        private float _branchTimeInterval;
        private float _timeAcc;

        public PineTreeTrunk(VolumeTexture sdfVolumeTexture, Vector3 position, float growthRate, Vector3 growthDirection, float radius, float radiusGrowthRate, float maxLength, float branchTimeInterval)
        {
            _initialPosition = _position = position;
            _growthRate = growthRate;
            _growthDirection = growthDirection;
            _radius = radius;
            _radiusGrowthRate = radiusGrowthRate;
            _maxLength = maxLength;
            _branchTimeInterval = branchTimeInterval;
            _marker = new(sdfVolumeTexture, position, Quaternion.identity, radius, _color);
        }

        private float Length()
        {
            return Vector3.Distance(_initialPosition, _position);
        }

        public bool ReadyToBranch()
        {
            return _timeAcc > _branchTimeInterval;
        }

        public bool ReadyToEnd()
        {
            return Length() > _maxLength;
        }

        public IEnumerable<PineTreeBranch> CreateBranches(System.Random random, VolumeTexture sdfVolumeTexture, float branchRadiusChangeFactor, float branchLengthChangeFactor)
        {
            _timeAcc = 0;

            var circlePosition = Random.insideUnitCircle.normalized;
            var newGrowthDirection = new Vector3(circlePosition.x, -0.5f, circlePosition.y);

            yield return new(
                sdfVolumeTexture,
                _position,
                _growthRate,
                newGrowthDirection,
                _radius * branchRadiusChangeFactor,
                _radiusGrowthRate,
                _maxLength * branchLengthChangeFactor * (0.2f + (_maxLength - Length()) / _maxLength));
        }

        public void GrowAndRender(VolumeTexture sdfVolumeTexture)
        {
            _timeAcc += Time.deltaTime;

            _position += _growthDirection * (_growthRate * Time.deltaTime);
            _radius *= 1 + (_radiusGrowthRate - 1) * Time.deltaTime;
            _marker.MarkTo(sdfVolumeTexture, _position, Quaternion.identity, _radius, _color);
        }
    }

    public class PineTreeBranch
    {
        private readonly Vector3 _initialPosition;
        private Vector3 _position;
        private readonly float _growthRate;
        private readonly Vector3 _growthDirection;
        private float _radius;
        private readonly float _radiusGrowthRate;
        private readonly float _maxLength;
        private readonly Marker _marker;

        private readonly Color _color = new(0.1f, 0.6f, 0.2f);

        public PineTreeBranch(VolumeTexture sdfVolumeTexture, Vector3 position, float growthRate, Vector3 growthDirection, float radius, float radiusGrowthRate, float maxLength)
        {
            _initialPosition = _position = position;
            _growthRate = growthRate;
            _growthDirection = growthDirection;
            _radius = radius;
            _radiusGrowthRate = radiusGrowthRate;
            _maxLength = maxLength * Random.Range(0.7f, 1.3f);
            _marker = new(sdfVolumeTexture, position, Quaternion.identity, radius, _color);
        }

        private float Length()
        {
            return Vector3.Distance(_initialPosition, _position);
        }

        public bool ReadyToEnd()
        {
            return Length() > _maxLength;
        }

        public void GrowAndRender(VolumeTexture sdfVolumeTexture)
        {
            _position += _growthDirection * (_growthRate * Time.deltaTime);
            _radius *= 1 + (_radiusGrowthRate - 1) * Time.deltaTime;
            _marker.MarkTo(sdfVolumeTexture, _position, Quaternion.identity, _radius, _color);
        }
    }

}
