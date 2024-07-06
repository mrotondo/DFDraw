using UnityEngine;

public static class VectorUtils
{
    public static Vector3 Abs(Vector3 v)
    {
        return new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}