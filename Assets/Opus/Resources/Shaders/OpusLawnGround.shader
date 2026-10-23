Shader "Opus/LawnGround"
{
    // The ground under the grass: carries the root tone of the same patch colours so dark soil does not show between
    // the blades. Soil colour on grassless cells (under obstacles).
    Properties
    {
        _OutsideColor("Grassless ground", Color) = (0.42, 0.36, 0.27, 1)
        _Wrap("Soft light", Range(0, 1)) = 0.5
        _RootTone("Root tone", Range(0, 1)) = 0.2
        _StripeContrast("Stripe contrast", Range(0, 0.5)) = 0.16
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "OpusLawn.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _OutsideColor;
            half _Wrap;
            half _RootTone;
            half _StripeContrast;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "OpusCommon.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = LawnUV(input.positionWS.xz);
                half3 albedo = _OutsideColor.rgb;
                uint patch = 0u;
                half4 state = half4(0, 0, 0.5h, 0.5h);
                if (InsideLawn(uv))
                {
                    patch = LawnPatchAt(uv);
                    if (patch > 0u)
                    {
                        state = LawnStateAt(uv);
                        half2 wetData = LawnWetAt(uv);
                        albedo = LawnBladeColor(patch, _RootTone, state.g, state.r, wetData.y) * lerp(0.95h, 0.85h, state.r);
                        albedo = LawnApplyWet(albedo, wetData.x);
                        // The stripe tone on the mown ground too (same formula as in the grass shader)
                        float2 mowRaw = state.ba * 2.0 - 1.0;
                        float mowStrength = saturate(length(mowRaw) * 1.4);
                        if (mowStrength > 0.01)
                        {
                            float2 toPoint = input.positionWS.xz - _WorldSpaceCameraPos.xz;
                            half stripe = dot(normalize(mowRaw), toPoint / max(length(toPoint), 1e-3)) * mowStrength * (1.0h - state.r);
                            albedo *= 1.0h + _StripeContrast * stripe;
                        }
                    }
                }
                half3 color = OpusShade(albedo, normalize(input.normalWS), input.positionWS, _Wrap) * (patch > 0u ? 0.88h : 1.0h);
                if (patch > 0u)
                    color = LawnOverlay(color, input.positionWS.xz, uv, patch, state, _RootTone);
                return half4(MixFog(color, input.fogFactor), 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            float4 DepthVert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half DepthFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
