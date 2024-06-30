Shader "Unlit/DFDraw"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _SampleDistance ("SampleDistance", Float) = 1
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

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float _SampleDistance;
            float _DistanceThreshold;
            int _MaxSteps;
            float _MaxMarchLength;

            float4x4 _BoxInverseTransform;
            float4x4 _BoxTransform;

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
            
            float3 pointOnRay(Ray ray, float distance)
            {
                return ray.origin + distance * ray.direction;
            }

            float sphereDistance(float3 samplePoint, float radius)
            {
                return length(samplePoint) - radius;
            }

            float boxDistance(float3 samplePoint, float3 size) {
                return length(max(abs(samplePoint) - size, 0.0));
            }

            float3 transform(float3 samplePoint, float4x4 inverseTransform) {
                float4 heterogenousSamplePoint = float4(samplePoint.x, samplePoint.y, samplePoint.z, 1.0);
                return mul(inverseTransform, heterogenousSamplePoint).xyz;
            }

            float safeScaleFactor(float4x4 transform) {
                float4 transformedPoint = mul(transform, float4(1, 1, 1, 0));
                return min(transformedPoint.x, min(transformedPoint.y, transformedPoint.z));
            }

            float sceneDistance(float3 samplePoint) {
                float unscaledDistance = boxDistance(transform(samplePoint, _BoxInverseTransform), 0.5);
                return unscaledDistance * safeScaleFactor(_BoxTransform);
            }

            RayMarchResult march(Ray ray) {
                float marchLength = 0;
                int steps = 0;
                float3 samplePoint = pointOnRay(ray, marchLength);
                float distance = sceneDistance(samplePoint);
                while (abs(distance) > _DistanceThreshold
                       && steps < _MaxSteps 
                       && marchLength < _MaxMarchLength) {
                    marchLength += abs(distance);
                    samplePoint = pointOnRay(ray, marchLength);
                    distance = sceneDistance(samplePoint);
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
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                Ray ray = fragmentRay(_VerticalFieldOfView,
                                      _CamPosition,
                                      _FarClipDistance,
                                      _AspectRatio,
                                      _CamRight.xyz,
                                      _CamUp.xyz,
                                      _CamForward.xyz,
                                      i.uv);

                // float3 samplePoint = pointOnRay(ray, _SampleDistance);
                // float distance = sceneDistance(samplePoint) / 10;
                // fixed4 col = fixed4(distance, distance, distance, 1);

                RayMarchResult result = march(ray);
                float distance = result.distance;
                float normalizedLength = result.length / _MaxMarchLength;
                float normalizedSteps = result.steps / _MaxSteps;
                fixed4 col = fixed4(normalizedSteps, normalizedSteps, normalizedSteps, 1);

                return col;
            }
            ENDCG
        }
    }
}
