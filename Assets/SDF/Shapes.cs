using System;
using UnityEngine;

namespace SDF
{
    public static class Shapes
    {
        // Warning! Do not use non-uniform scale vectors: At least according to Inigo, they can't generate correct SDF:
        // https://iquilezles.org/articles/distfunctions/#:~:text=with%20uniform%20scaling.-,Non%20uniform%20scaling,-is%20not%20possible
        private static void BlitShape(VolumeTexture sdfVolumeTexture, Matrix4x4 objectTrs, Func<Vector3, float> shapeDistanceFunction)
        {
            Vector3 centroid = objectTrs * new Vector4(0, 0, 0, 1);
            float approximateBoundingRadius = (objectTrs * new Vector4(1, 0, 0, 0)).magnitude;
            sdfVolumeTexture.UpdateArea(centroid, approximateBoundingRadius, (samplePosition, oldDistance) =>
            {
                Vector3 samplePositionInObjectSpace = WorldToObjectSpace(samplePosition, objectTrs);
                float objectSpaceDistance = shapeDistanceFunction(samplePositionInObjectSpace);
                float newDistance = ObjectToWorldScaleFactor(objectTrs) * objectSpaceDistance;
                // return SmoothMinCircular(newDistance, oldDistance, 0.01f);
                return (newDistance < oldDistance, newDistance);
            });
        }

        private static float SmoothMinCircular(float a, float b, float k)
        {
            const float sqrt_of_one_half = 0.7071067812f;
            const float kNorm = 1.0f / (1.0f - sqrt_of_one_half);
            k *= kNorm;
            float h = Mathf.Max(k - Mathf.Abs(a - b), 0.0f) / k;
            return Mathf.Min(a, b) - k * 0.5f * (1.0f + h - Mathf.Sqrt(1.0f - h * (h - 2.0f)));
        }

        private static Vector4 WorldToObjectSpace(Vector3 position, Matrix4x4 objectTrs)
        {
            Vector4 homogeneousPoint = position;
            homogeneousPoint.w = 1;
            return objectTrs.inverse * homogeneousPoint;
        }

        // Assumes a uniform scale (see warning above)
        private static float ObjectToWorldScaleFactor(Matrix4x4 objectTrs)
        {
            return (objectTrs * Vector3.right).magnitude;
        }

        public static void BlitSphereToSdfVolumeTexture(VolumeTexture sdfVolumeTexture, Matrix4x4 trs)
        {
            BlitShape(sdfVolumeTexture, trs, UnitSphereDistance);
        }

        public static void BlitBoxToSdfVolumeTexture(VolumeTexture sdfVolumeTexture, Matrix4x4 trs)
        {
            BlitShape(sdfVolumeTexture, trs, UnitCubeDistance);
        }

        private static float UnitSphereDistance(Vector3 samplePoint)
        {
            return SphereDistance(samplePoint, 0.5f);
        }

        private static float SphereDistance(Vector3 samplePoint, float radius)
        {
            return samplePoint.magnitude - radius;
        }

        private static float UnitCubeDistance(Vector3 samplePoint)
        {
            return BoxDistance(samplePoint, Vector3.one * 0.5f);
        }

        private static float BoxDistance(Vector3 samplePoint, Vector3 size)
        {
            return Vector3.Max(VectorUtils.Abs(samplePoint) - size, Vector3.zero).magnitude;
        }
    }
}