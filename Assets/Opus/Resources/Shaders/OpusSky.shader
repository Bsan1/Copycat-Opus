Shader "Opus/Sky"
{
    // Watercolour sky: lavender zenith → pink-white horizon, brush-marked clouds from domain-warped fBm noise
    // (the side facing the sun is light, a slight darkening at the edges like pooled paint),
    // paper texture and a fog band at the horizon. The clouds drift slowly.
    Properties
    {
        _ZenithColor("Zenith", Color) = (0.66, 0.61, 0.79, 1)
        _HorizonColor("Horizon", Color) = (0.9, 0.84, 0.85, 1)
        _GroundColor("Ground", Color) = (0.78, 0.73, 0.75, 1)
        _Curve("Transition curve", Range(0.2, 3)) = 0.6
        _SunGlow("Sun highlight", Color) = (1, 0.92, 0.8, 1)
        _SunGlowPower("Highlight tightness", Range(1, 64)) = 6
        _SunGlowStrength("Highlight strength", Range(0, 1)) = 0.25
        _CloudColor("Cloud", Color) = (1, 0.96, 0.97, 1)
        _CloudShadow("Cloud shadow", Color) = (0.78, 0.72, 0.84, 1)
        _CloudCover("Cloud threshold", Range(0, 1)) = 0.47
        _CloudSoftness("Cloud softness", Range(0.01, 0.5)) = 0.11
        _CloudScale("Cloud scale", Float) = 1.3
        _CloudSpeed("Cloud speed", Float) = 0.006
        _HazeColor("Horizon fog", Color) = (0.93, 0.89, 0.9, 1)
        _HazeHeight("Fog height", Range(0.01, 0.5)) = 0.07
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _HorizonColor;
                half4 _GroundColor;
                half _Curve;
                half4 _SunGlow;
                half _SunGlowPower;
                half _SunGlowStrength;
                half4 _CloudColor;
                half4 _CloudShadow;
                half _CloudCover;
                half _CloudSoftness;
                float _CloudScale;
                float _CloudSpeed;
                half4 _HazeColor;
                half _HazeHeight;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash(i), b = Hash(i + float2(1, 0)), c = Hash(i + float2(0, 1)), d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float sum = 0.0, amplitude = 0.5;
                [unroll]
                for (int i = 0; i < 5; i++)
                {
                    sum += Noise(p) * amplitude;
                    p = p * 2.03 + float2(17.1, 9.2);
                    amplitude *= 0.5;
                }
                return sum;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                half height = direction.y;
                half3 color;
                if (height >= 0.0h)
                    color = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(saturate(height), _Curve));
                else
                    color = lerp(_HorizonColor.rgb, _GroundColor.rgb, saturate(-height * 6.0h));

                float3 sunDirection = _MainLightPosition.xyz;
                half sun = pow(saturate(dot(direction, sunDirection)), _SunGlowPower);
                color += _SunGlow.rgb * sun * _SunGlowStrength;

                if (height > -0.02)
                {
                    // Projection onto the sky plane: towards the horizon the clouds get smaller and denser
                    float2 p = direction.xz / (height + 0.22) * _CloudScale;
                    p += _Time.y * _CloudSpeed * float2(1.0, 0.35);
                    // Domain warp: curly shapes like brush strokes
                    float2 warp = float2(Fbm(p * 0.6 + 3.1), Fbm(p * 0.6 + 8.7)) - 0.5;
                    float2 q = p + warp * 1.1;
                    float density = Fbm(q);
                    float detail = Fbm(q * 3.3 + 21.0);
                    density += (detail - 0.5) * 0.12;

                    half cover = smoothstep(_CloudCover, _CloudCover + _CloudSoftness, density);
                    // The side facing the sun is light: the density difference sampled towards the sun
                    float2 toSun = normalize(sunDirection.xz + 1e-4) * 0.22;
                    half shade = saturate((density - Fbm(q + toSun)) * 4.0 + 0.55);
                    half3 cloud = lerp(_CloudShadow.rgb, _CloudColor.rgb, shade);
                    // Watercolour: paint pooling at the edge (a thin dark ring), paper white inside
                    half rim = smoothstep(_CloudCover, _CloudCover + 0.04, density) * (1.0h - smoothstep(_CloudCover + 0.04, _CloudCover + 0.14, density));
                    cloud *= 1.0h - rim * 0.1h;
                    cover *= smoothstep(-0.02, 0.1, height); // blends into the fog at the horizon
                    color = lerp(color, cloud, cover * 0.92h);
                }

                // Horizon fog and paper texture
                half haze = exp(-abs(height) / _HazeHeight);
                color = lerp(color, _HazeColor.rgb, haze * 0.75h);
                float grain = Noise(direction.xy * 420.0 + direction.z * 173.0);
                color *= 1.0h + (grain - 0.5h) * 0.03h;
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
