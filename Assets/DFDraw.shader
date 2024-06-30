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

            struct lineSegment 
            {
                float3 pA;
                float3 pB;
            };

            struct ray
            {
                float3 origin;
                float3 direction;
            };

            float4 _CamPosition;
            float _VerticalFieldOfView;
            float _NearClipDistance;
            float _FarClipDistance;
            float _AspectRatio;
            float4 _CamRight;
            float4 _CamUp;
            float4 _CamForward;
            float4x4 _CamInverseProjectionMatrix;

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float _SampleDistance;
            float4x4 _BoxInverseTransform;

            ray frustumLineSegment(float verticalFieldOfView,
                float3 camPosition,
                float nearClipDistance,
                float farClipDistance,
                float aspectRatio,
                float3 right,
                float3 up,
                float3 forward,
                float2 uv)
            {
            float sizeFar = farClipDistance * tan(verticalFieldOfView / 2);
            float sizeNear = nearClipDistance * tan(verticalFieldOfView / 2);

            float2 normalizedUV = uv * 2 - 1;
            float2 aspectUV = normalizedUV * float2(aspectRatio, 1);

            float3 pointNear = camPosition 
                + forward * nearClipDistance
                + right * sizeNear * aspectUV.x
                + up * sizeNear * aspectUV.y; 

            float3 pointFar = camPosition 
                + forward * farClipDistance
                + right * sizeFar * aspectUV.x
                + up * sizeFar * aspectUV.y;

            lineSegment segment;
            segment.pA = pointNear;
            segment.pB = pointFar;

            return segment;
            }
            
            float3 pointOnLineSegment(lineSegment segment, float normalizedDistance)
            {
                return segment.pA + normalizedDistance * (segment.pB - segment.pA);
            }

            float boxDistance(float3 samplePoint, float3 size)
			{
				return length(max(abs(samplePoint) - size, 0.0));
			}

            float3 transform(float3 samplePoint, float4x4 inverseTransform)
			{
				float4 heterogenousSamplePoint = float4(samplePoint.x, samplePoint.y, samplePoint.z, 1.0);
				return mul(inverseTransform, heterogenousSamplePoint).xyz;
			}

			float sceneDistance(float3 samplePoint)
			{

				return boxDistance(transform(samplePoint, _BoxInverseTransform), 0.5);
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
                lineSegment segment = frustumLineSegment(_VerticalFieldOfView,
                                                         _CamPosition,
                                                         _NearClipDistance,
                                                         _FarClipDistance,
                                                         _AspectRatio,
                                                         _CamRight.xyz,
                                                         _CamUp.xyz,
                                                         _CamForward.xyz,
                                                         i.uv);

                float3 samplePoint = pointOnLineSegment(segment, _SampleDistance);
                float distance = sceneDistance(samplePoint);

                // fixed4 col = fixed4(_SampleDistance, _SampleDistance, _SampleDistance, 1);
                fixed4 col = fixed4(distance, distance, distance, 1);
                // fixed4 col = tex2D(_MainTex, i.uv);

                return col;
            }
            ENDCG
        }
    }
}
