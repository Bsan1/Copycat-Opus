Shader "Opus/WaterPuff"
{
    // The puffs of the hose water (WaterSpray): a camera-facing quad with a soft circle with lumpy edges inside. White on top,
    // light blue underneath (like the water in the video); stacked puffs read as a puffy, cloud-like jet.
    // uv: corner (-1..1) + 4 × seed (in x); uv2: (size m, opacity).
    Properties
    {
        _TopColor("Top", Color) = (0.80, 0.88, 0.96, 1)
        _BottomColor("Bottom", Color) = (0.62, 0.74, 0.88, 1)
        _Lumps("Clump", Range(0, 0.4)) = 0.2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "OpusCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _BottomColor;
                half _Lumps;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;   // centre of the puff (world)
                float2 uv : TEXCOORD0;
                float2 data : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 corner : TEXCOORD0;
                float seed : TEXCOORD1;
                half alpha : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float seed = floor((input.uv.x + 2.0) * 0.25);
                float2 corner = float2(input.uv.x - seed * 4.0, input.uv.y);
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float3 positionWS = input.positionOS.xyz + (right * corner.x + up * corner.y) * input.data.x;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.corner = corner;
                output.seed = seed;
                output.alpha = input.data.y;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 c = input.corner;
                float d = length(c);
                // Lumpy edge: a few bumps by angle (different on every puff)
                float a = atan2(c.y, c.x);
                float lumps = sin(a * 3.0 + input.seed * 1.7) * 0.6 + sin(a * 5.0 - input.seed * 2.3) * 0.4;
                float radius = 0.8 + lumps * _Lumps * 0.5;
                half alpha = 1.0h - smoothstep(radius - 0.06, radius, d);
                alpha *= input.alpha;
                clip(alpha - 0.004h);

                // Light from above: white on top, bluish underneath; slightly blue towards the edge
                half t = saturate(c.y * 0.5h + 0.55h);
                half3 color = lerp(_BottomColor.rgb, _TopColor.rgb, t * t * (3.0h - 2.0h * t));
                color = lerp(color, _BottomColor.rgb, smoothstep(0.45, radius, d) * 0.35h);
                Light light = GetMainLight();
                color *= lerp(half3(1, 1, 1), light.color, 0.35h);
                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
