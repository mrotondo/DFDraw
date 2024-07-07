Shader "Unlit/DFDraw"
{
    Properties
    {
        _SdfVolumeTexture ("SDF Volume Texture", 3D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            // Upgrade NOTE: excluded shader from DX11; has structs without semantics (struct v2f members worldSpacePos)
            #pragma exclude_renderers d3d11
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            struct Ray
            {
                float3 origin;
                float3 direction;
            };

            struct AABB
            {
                float3 minBound;
                float3 maxBound;
            };

            struct RayAABBIntersectionResult
            {
                bool hit;
                float tMin;
                float tMax;
            };

            struct RayMarchResult
            {
                float distance;
                float steps;
                float length;
            };

            float4 _CamPosition;
            float _VerticalFieldOfView;
            float _FarClipDistance;
            float _AspectRatio;
            float4 _CamRight;
            float4 _CamUp;
            float4 _CamForward;
            float4x4 _CamInverseProjectionMatrix;

            sampler3D _SdfVolumeTexture;
            float4 _SdfVolumeTexture_ST;

            float _DistanceThreshold;
            int _MaxSteps;
            float _MaxMarchLength;

            Ray fragmentRay(float verticalFieldOfView,
                float3 camPosition,
                float farClipDistance,
                float aspectRatio,
                float3 right,
                float3 up,
                float3 forward,
                float2 uv)
            {
                float sizeFar = farClipDistance * tan(verticalFieldOfView / 2);

                float2 normalizedUV = uv * 2 - 1;
                float2 aspectUV = normalizedUV * float2(aspectRatio, 1);

                float3 pointFar = camPosition 
                    + forward * farClipDistance
                    + right * sizeFar * aspectUV.x
                    + up * sizeFar * aspectUV.y; 

                Ray ray;
                ray.origin = camPosition;
                ray.direction = normalize(pointFar - camPosition);
                return ray;
            }

            RayAABBIntersectionResult aabbIntersection(Ray ray, AABB boundingBox) {
                float3 inverse_dir = 1.0 / ray.direction;
                float3 tbot = inverse_dir * (boundingBox.minBound - ray.origin);
                float3 ttop = inverse_dir * (boundingBox.maxBound - ray.origin);
                float3 tmin = min(ttop, tbot);
                float3 tmax = max(ttop, tbot);
                float2 traverse = max(tmin.xx, tmin.yz);
                float traverselow = max(traverse.x, traverse.y);
                traverse = min(tmax.xx, tmax.yz);
                float traversehi = min(traverse.x, traverse.y);

                RayAABBIntersectionResult result;
                result.hit = traversehi > max(traverselow, 0.0);
                result.tMin = traverselow;
                result.tMax = traversehi;
                return result;
            }
            
            float3 pointOnRay(Ray ray, float distance)
            {
                return ray.origin + distance * ray.direction;
            }

            float alphaToDistance(float alpha) {
                return alpha * 2 - 1;
            }

            // needs to eventually handle positions that aren't in [(0,0,0), (1,1,1)]
            float volumeTextureDistance(float3 samplePoint, sampler3D volumeTexture)
            {
                return alphaToDistance(tex3D(volumeTexture, samplePoint).r);
            }

            float3 volumeTextureNormal(float3 samplePoint, sampler3D volumeTexture)
            {
                float3 xEpsilon = float3(0.01, 0.0, 0.0);
                float3 yEpsilon = float3(0.0, 0.01, 0.0);
                float3 zEpsilon = float3(0.0, 0.0, 0.01);
                float3 gradient = float3(
                    volumeTextureDistance(samplePoint + xEpsilon, volumeTexture) - volumeTextureDistance(samplePoint - xEpsilon, volumeTexture),
                    volumeTextureDistance(samplePoint + yEpsilon, volumeTexture) - volumeTextureDistance(samplePoint - yEpsilon, volumeTexture),
                    volumeTextureDistance(samplePoint + zEpsilon, volumeTexture) - volumeTextureDistance(samplePoint - zEpsilon, volumeTexture));
                return normalize(gradient) * 0.5 + 0.5;
            }

            RayMarchResult volumeTextureMarch(Ray ray, sampler3D volumeTexture)
            {
                float marchLength = 0;
                int steps = 0;
                float3 samplePoint = pointOnRay(ray, marchLength);
                float distance = volumeTextureDistance(samplePoint, volumeTexture);
                [loop]
                while (abs(distance) > _DistanceThreshold
                       && steps < _MaxSteps
                       && marchLength < _MaxMarchLength) {
                    marchLength += abs(distance);
                    samplePoint = pointOnRay(ray, marchLength);
                    distance = volumeTextureDistance(samplePoint, volumeTexture);
                    steps++;
                }
                RayMarchResult result;
                result.distance = distance;
                result.steps = steps;
                result.length = marchLength;
                return result;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _SdfVolumeTexture);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col;

                Ray boundingBoxRay = fragmentRay(_VerticalFieldOfView,
                                                 _CamPosition,
                                                 _FarClipDistance,
                                                 _AspectRatio,
                                                 _CamRight.xyz,
                                                 _CamUp.xyz,
                                                 _CamForward.xyz,
                                                 i.uv);

                AABB boundingBox;
                boundingBox.minBound = float3(0, 0, 0);
                boundingBox.maxBound = float3(1, 1, 1);

                RayAABBIntersectionResult intersection = aabbIntersection(boundingBoxRay, boundingBox);
                if (intersection.hit) {
                    float3 box_hit_position = pointOnRay(boundingBoxRay, intersection.tMin);
                    Ray marchingRay;
                    marchingRay.origin = box_hit_position;
                    marchingRay.direction = boundingBoxRay.direction;

                    RayMarchResult result = volumeTextureMarch(marchingRay, _SdfVolumeTexture);
                    float distance = result.distance;
                    if (distance < _DistanceThreshold) {
                        float normalizedLength = result.length / _MaxMarchLength;
                        float normalizedSteps = result.steps / _MaxSteps;
                        float3 position = pointOnRay(marchingRay, result.length);
                        float3 normal = volumeTextureNormal(position, _SdfVolumeTexture);
                        fixed3 gray = normalizedSteps;
                        col = fixed4(gray, 1);
                    } else {
                        col = fixed4(0.9, 0.9, 0.9, 1);
                    }
                } else {
                    col = fixed4(1, 1, 1, 1);
                }

                return col;
            }
            ENDCG
        }
    }
}
