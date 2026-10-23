Shader "Opus/GroundRing"
{
    // The dashed target ring on the ground. So it does not get lost in the grass the vertices are pulled towards the camera
    // by _Pull: the screen position does not change, but in depth it passes in front of the grass (it stays behind nearby objects).
    Properties
    {
        _Color("Color", Color) = (0.93, 0.95, 0.96, 1)
        _Opacity("Opacity", Range(0, 1)) = 0.6
        _Dashes("Dash count", Float) = 26
        _Pull("Pull towards camera (m)", Float) = 0.55
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" }
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

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Opacity;
                half _Dashes;
                half _Pull;
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
                half fogFactor : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 toCamera = _WorldSpaceCameraPos - positionWS;
                float distance = length(toCamera);
                positionWS += toCamera / max(distance, 1e-3) * min(_Pull, distance * 0.5);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half dash = frac(input.uv.x * _Dashes);
                // _Dashes 0: a solid thin line (water ripples)
                half alpha = _Dashes > 0.5h ? smoothstep(0.0h, 0.06h, dash) * (1.0h - smoothstep(0.5h, 0.56h, dash)) : 1.0h;
                half across = abs(input.uv.y * 2.0h - 1.0h);
                alpha *= 1.0h - across * across * across * across;
                alpha *= _Opacity * _Color.a;
                half3 color = MixFog(_Color.rgb, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
