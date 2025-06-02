Shader "Unlit/DFDraw"
{
    Properties
    {
        _SdfVolumeTexture ("SDF Volume Texture", 3D) = "white" {}
        _ColorVolumeTexture("Color Volume Texture", 3D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            #define MAX_RAYS 4

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

            struct WeightedRay
            {
                float weight;
                Ray ray;
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
            sampler3D _ColorVolumeTexture;
            float4 _ColorVolumeTexture_ST;

            float _DistanceThreshold;
            int _MaxSteps;
            float _MaxMarchLength;
            float _MaxStepLength;
            float _StepLengthInsideObjects;

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

            AABB renderVolumeBoundingBox()
            {
                AABB boundingBox;
                boundingBox.minBound = float3(0, 0, 0);
                boundingBox.maxBound = float3(1, 1, 1);
                return boundingBox;
            }

            // taken from somewhere
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

            float3 reflect(float3 v, float3 n)
            {
                float3 projectionOntoNormal = n * dot(v, n);
                return v - 2 * projectionOntoNormal;
            }
            
            float3 pointOnRay(Ray ray, float distance)
            {
                return ray.origin + distance * ray.direction;
            }

            float map(float value, float min1, float max1, float min2, float max2) {
                return min2 + (value - min1) * (max2 - min2) / (max1 - min1);
            }
            float3 map(float3 value, float min1, float max1, float min2, float max2) {
                return min2 + (value - min1) * (max2 - min2) / (max1 - min1);
            }

            // needs to eventually handle positions that aren't in [(0,0,0), (1,1,1)]
            float volumeTextureDistance(float3 samplePoint, sampler3D volumeTexture)
            {
                float compressedDistance = tex3D(volumeTexture, samplePoint).r;
                float decompressedDistance = map(compressedDistance, 0, 1, -1, 1);

                return decompressedDistance;
            }

            float3 volumeTextureColor(float3 samplePoint, sampler3D volumeTexture)
            {
                return tex3D(volumeTexture, samplePoint).rgb;    
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
                return normalize(gradient);
            }

            RayMarchResult volumeTextureMarchToSurface(Ray ray, sampler3D volumeTexture)
            {
                float marchLength = 0;
                int steps = 0;
                float3 samplePoint = pointOnRay(ray, marchLength);
                float distance = volumeTextureDistance(samplePoint, volumeTexture);
                [loop]
                while (abs(distance) > _DistanceThreshold
                       && steps < _MaxSteps
                       && marchLength < _MaxMarchLength) {
                    marchLength += min(_MaxStepLength, abs(distance));
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

            fixed4 surfaceColor(Ray marchingRay)
            {
                RayMarchResult result = volumeTextureMarchToSurface(marchingRay, _SdfVolumeTexture);
                
                float3 normalizedLightDirection = normalize(float3(-1, -1, -1));
                float normalizedLength = result.length / _MaxMarchLength;
                float normalizedSteps = result.steps / _MaxSteps;
                float distance = result.distance;
                fixed3 gray = normalizedSteps;
                if (distance < _DistanceThreshold) {
                    float3 position = pointOnRay(marchingRay, result.length);
                    float3 normal = volumeTextureNormal(position, _SdfVolumeTexture);
                    float3 normalColor = map(normal, -1, 1, 0, 1);
                    float3 color = volumeTextureColor(position, _ColorVolumeTexture);
                    return fixed4((dot(normal, -normalizedLightDirection) * 0.5 + 0.5) * color, 1);
                    // return fixed4(color, 1);
                    // return fixed4(normalColor, 1);
                } else {
                    return fixed4(0.1, 0.2, 0.3, 1);
                }
            }

            fixed4 accumulatedColor(Ray marchingRay, sampler3D volumeTexture)
            {
                fixed4 outputColor = fixed4(0, 0, 0, 1);

                float STEP_DISTANCE_INSIDE_OBJECTS = 0.001;
                float materialAbsorbtion = 0.1;

                int numRays = 1;
                WeightedRay weightedRays[MAX_RAYS];
                
                weightedRays[0].ray.origin = marchingRay.origin;
                weightedRays[0].ray.direction = marchingRay.direction;
                weightedRays[0].weight = 1;

                int rayBeingMarched = 0;
                [loop]
                while (rayBeingMarched < numRays)
                {
                    fixed3 rayColor = fixed3(0, 0, 0);
                    float remainingIntensity = 1;
                    float rayWeight = 1;

                    float marchLength = 0;
                    int steps = 0;
                    Ray ray = weightedRays[rayBeingMarched].ray;
                    float3 samplePoint = pointOnRay(ray, marchLength);
                    float distance = volumeTextureDistance(samplePoint, volumeTexture);
                    [loop]
                    while (steps < _MaxSteps
                           && marchLength < _MaxMarchLength
                           && remainingIntensity > 0) {
        
                        float3 position = pointOnRay(ray, marchLength);

                        if (distance > _DistanceThreshold) // not inside an object
                        {
                            marchLength += min(_MaxStepLength, abs(distance));
                        }
                        else if (abs(distance) <= _DistanceThreshold) // on an object
                        {
                            if (numRays < MAX_RAYS)
                            {
                                float3 normal = volumeTextureNormal(position, _SdfVolumeTexture);
                                Ray reflectedRay = { position, reflect(ray.direction, normal) };
                                
                                numRays += 1;
                                weightedRays[numRays].weight = rayWeight * 0.5;
                                weightedRays[numRays].ray.origin = reflectedRay.origin + reflectedRay.direction * _DistanceThreshold * 2;
                                weightedRays[numRays].ray.direction = reflectedRay.direction;

                                rayWeight *= 0.5;
                            }
                            marchLength += _DistanceThreshold * 2;
                        }
                        else // inside an object
                        {
                            // this should at some point take into account how long of a step we took through the material
                            float3 color = volumeTextureColor(position, _ColorVolumeTexture);
    
                            rayColor += color * materialAbsorbtion * remainingIntensity;
                            remainingIntensity -= materialAbsorbtion;
                            marchLength += _StepLengthInsideObjects;
                        }
                        
                        samplePoint = pointOnRay(ray, marchLength);
                        distance = volumeTextureDistance(samplePoint, volumeTexture);
                        steps++;
                    }
                    
                    outputColor += fixed4(rayColor * rayWeight, 0);
                    rayBeingMarched += 1;
                }

                return outputColor;
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

                Ray boundingBoxRay = fragmentRay(_VerticalFieldOfView, _CamPosition, _FarClipDistance, _AspectRatio, _CamRight.xyz, _CamUp.xyz, _CamForward.xyz, i.uv);
                AABB boundingBox = renderVolumeBoundingBox();

                RayAABBIntersectionResult intersection = aabbIntersection(boundingBoxRay, boundingBox);
                if (intersection.hit) {
                    float3 box_hit_position = pointOnRay(boundingBoxRay, intersection.tMin);
                    Ray marchingRay = { box_hit_position, boundingBoxRay.direction };

                    // col = surfaceColor(marchingRay);
                    col = accumulatedColor(marchingRay, _SdfVolumeTexture);
                } else {
                    col = fixed4(0, 0, 0, 1);
                }

                return col;
            }
            ENDCG
        }
    }
}
