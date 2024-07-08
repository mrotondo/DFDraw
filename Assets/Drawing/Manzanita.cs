using System.Collections.Generic;
using SDF;
using UnityEngine;

public class Manzanita : MonoBehaviour
{
    public uint SdfVolumeSideLength = 128;
    private VolumeTexture _sdfVolumeTexture;

    public Vector3 BasePosition = new(0.5f, 0.0f, 0.5f);
    public float GrowthRate = 0.5f; // world units / second
    public Vector3 InitialGrowthDirection = new(0f, 1f, 0f);
    public float InitialRadius = 0.2f;  // world units
    public float RadiusGrowthRate = 0.9f; // growth rate / second
    public float BranchLength = 0.2f;  // world units
    public float BranchAngleRange = 45f;  // degrees
    public float BranchLengthChangeFactor = 0.9f; // ratio / branch
    public float BranchRadiusChangeFactor = 0.8f; // ratio / branch
    public float BranchAngleRangeChangeFactor = 0.8f; // ratio / branch
    public int MaxBranchDepth = 5;

    private List<Branch> _growingBranches;

    void Start()
    {
        _sdfVolumeTexture = new VolumeTexture(SdfVolumeSideLength);
        _sdfVolumeTexture.ConfigureRenderer(GetComponent<DFRenderer>());

        _growingBranches = new List<Branch>() {
            new(BasePosition, GrowthRate, InitialGrowthDirection, InitialRadius, RadiusGrowthRate, BranchLength, BranchAngleRange, 0)
        };
    }

    void Update()
    {
        List<Branch> finishedBranches = new();
        List<Branch> newBranches = new();
        foreach (var branch in _growingBranches)
        {
            branch.GrowAndRender(_sdfVolumeTexture);
            if (branch.ReadyToBranch() && branch.Depth < MaxBranchDepth)
            {
                newBranches.AddRange(branch.CreateBranches(BranchRadiusChangeFactor, BranchLengthChangeFactor, BranchAngleRangeChangeFactor, MaxBranchDepth));
            }
        }
        _growingBranches.RemoveAll(branch => branch.ReadyToBranch());
        _growingBranches.AddRange(newBranches);

        _sdfVolumeTexture.Render();
    }

    private class Branch
    {
        private readonly Vector3 _initialPosition;
        private Vector3 _position;
        private readonly float _growthRate;
        private readonly Vector3 _growthDirection;
        private float _radius;
        private readonly float _radiusGrowthRate;
        private readonly float _maxLength;
        private readonly float _branchAngleRange;
        public readonly int Depth;
        private readonly Marker _marker;
        private readonly System.Random _random;

        public Branch(Vector3 position, float growthRate, Vector3 growthDirection, float radius, float radiusGrowthRate, float maxLength, float branchAngleRange, int depth)
        {
            _initialPosition = _position = position;
            _growthRate = growthRate;
            _growthDirection = growthDirection;
            _radius = radius;
            _radiusGrowthRate = radiusGrowthRate;
            _maxLength = maxLength;
            _branchAngleRange = branchAngleRange;
            Depth = depth;
            _marker = new(position, Quaternion.identity, radius);
            _random = new System.Random();
        }

        private float Length()
        {
            return Vector3.Distance(_initialPosition, _position);
        }

        public bool ReadyToBranch()
        {
            return Length() > _maxLength;
        }

        public List<Branch> CreateBranches(float branchRadiusChangeFactor, float branchLengthChangeFactor, float branchAngleRangeChangeFactor, int maxBranchDepth)
        {
            List<Branch> branches = new();
            float t = (float)Depth / maxBranchDepth;
            int numBranches = _random.Next(Mathf.FloorToInt(Mathf.Lerp(3,1,t)), Mathf.FloorToInt(Mathf.Lerp(5,2,t)));
            for (int i = 0; i < numBranches; i++)
            {
                var circlePosition = Random.insideUnitCircle * Mathf.Tan(_branchAngleRange * Mathf.Deg2Rad);
                var circlePositionProjectedUpwards = new Vector3(circlePosition.x, 1, circlePosition.y);
                var rotation = Quaternion.FromToRotation(Vector3.up, circlePositionProjectedUpwards);
                var newGrowthDirection = rotation * _growthDirection;

                branches.Add(new(
                    _position,
                    _growthRate,
                    newGrowthDirection,
                    _radius * branchRadiusChangeFactor,
                    _radiusGrowthRate,
                    _maxLength * branchLengthChangeFactor,
                    _branchAngleRange * branchAngleRangeChangeFactor,
                    Depth + 1));
            }
            return branches;
        }

        public void GrowAndRender(VolumeTexture sdfVolumeTexture)
        {
            _position += _growthDirection * (_growthRate * Time.deltaTime);
            _radius *= 1 + (_radiusGrowthRate - 1) * Time.deltaTime;
            _marker.MarkTo(_position, Quaternion.identity, _radius);
            _marker.Render(sdfVolumeTexture);
        }
    }
}
