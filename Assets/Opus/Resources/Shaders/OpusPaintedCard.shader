Shader "Opus/PaintedCard"
{
    // Distant painted tree cards: watercolour texture, soft edge (alpha-to-coverage with MSAA), a light directional
    // light and haze that grows with distance but never covers them completely (in the reference distant trees are pale but colourful).
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _Tint("Color", Color) = (1, 1, 1, 1)
        _Cutoff("Cutoff", Range(0, 1)) = 0.45
        _HazeStart("Haze start (m)", Float) = 12
        _HazeEnd("Haze end (m)", Float) = 85
        _HazeMax("Max haze", Range(0, 1)) = 0.72
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        AlphaToMask On

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Tint;
                half _Cutoff;
                float _HazeStart;
                float _HazeEnd;
                half _HazeMax;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 paint = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Tint;
                // A sharp transition around the cutoff: MSAA alpha-to-coverage softens the edge
                half alpha = saturate((paint.a - _Cutoff) / max(fwidth(paint.a), 1e-3) + 0.5h);
                clip(alpha - 0.01h);

                Light light = GetMainLight();
                half3 color = paint.rgb * lerp(0.82h, 1.0h, light.color.r);
                float distance = length(_WorldSpaceCameraPos - input.positionWS);
                half haze = smoothstep(_HazeStart, _HazeEnd, distance) * _HazeMax;
                color = lerp(color, unity_FogColor.rgb, haze);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
