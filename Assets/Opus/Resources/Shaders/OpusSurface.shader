Shader "Opus/Surface"
{
    // Hand-painted surface: a triplanar pattern needing no UVs (in metric object space; for statically merged
    // objects this is world space), large-scale tone variation, grime blotches near the ground and a contact shadow.
    // The pattern texture is a tone multiplier (0.5 = the colour as is); the light cues (joint shadow, shingle lip) are painted in.
    // _OPUS_UVMAP (textured Blender models): no pattern, grime or tone; colour, normal, AO and roughness come from the model's
    // UV-mapped atlas textures (baked in Blender).
    // _OPUS_TILED (buildings): the model's UVs are in metres along each piece's grain; _BaseMap is a tiling tone texture
    // (0.5 = the colour as is) and _BumpMap its normal map, repeated every _Tile metres; AO and the per-piece tone come from
    // the vertex colour (_OPUS_HARD). Sharp at any distance and with no atlas seams to bleed.
    Properties
    {
        _BaseColor("Color", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _BaseMap("Pattern (tone multiplier)", 2D) = "gray" {}
        [NoScaleOffset] _GrimeMap("Grime (R) and tone (G)", 2D) = "black" {}
        _Tile("Pattern repeat (m)", Float) = 1
        _Strength("Pattern strength", Range(0, 1.5)) = 1
        _SlopeAlign("Top projection follows the slope", Float) = 0
        _Grime("Grime", Range(0, 1)) = 0.5
        _GrimeHeight("Grime height (m)", Float) = 0.9
        _Tone("Large tone variation", Range(0, 0.3)) = 0.08
        _AO("Ground proximity shadow", Range(0, 1)) = 1
        _Wrap("Soft light", Range(0, 1)) = 0.45
        _RimStrength("Rim light", Range(0, 1)) = 0.04
        // Hard surface (Blender models; 0 = the soft painted light of the garden props)
        _ShadeDepth("Shadow depth (neutral)", Range(0, 1)) = 0
        _Gloss("Shine", Range(0, 1)) = 0
        _SpecPower("Highlight sharpness", Float) = 32
        _Reflect("Rim reflection", Range(0, 1)) = 0
        _VertexAO("Baked AO (vertex colour R)", Range(0, 1)) = 0
        // Textured models (_OPUS_UVMAP): _BaseMap atlas colour
        [NoScaleOffset] _BumpMap("Normal (atlas)", 2D) = "bump" {}
        [NoScaleOffset] _OrmMap("AO (R) roughness (G) metal (B)", 2D) = "white" {}
        _BumpScale("Normal strength (tiled)", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _Tile;
            half _Strength;
            half _SlopeAlign;
            half _Grime;
            half _GrimeHeight;
            half _Tone;
            half _AO;
            half _Wrap;
            half _RimStrength;
            half _ShadeDepth;
            half _Gloss;
            half _SpecPower;
            half _Reflect;
            half _VertexAO;
            half _BumpScale;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            // Hard surface (Blender models): without this keyword the garden props compile exactly with the old code
            #pragma multi_compile_local _ _OPUS_HARD
            #pragma multi_compile_local _ _OPUS_UVMAP _OPUS_TILED

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "OpusCommon.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_GrimeMap);
            SAMPLER(sampler_GrimeMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);
            TEXTURE2D(_OrmMap);
            SAMPLER(sampler_OrmMap);

            // Grime colour multiplier: a brownish darkening (the stains on the shed wall in the reference)
            static const half3 GrimeTint = half3(0.44h, 0.37h, 0.3h);
            static const float GrimeTile = 2.5;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            #if defined(_OPUS_HARD)
                half4 color : COLOR;   // baked AO (R)
            #endif
            #if defined(_OPUS_UVMAP) || defined(_OPUS_TILED)
                float2 uv : TEXCOORD0;
                float4 tangentOS : TANGENT;
            #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float3 positionTP : TEXCOORD2; // metric object space (scaled)
                half3 normalTP : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            #if defined(_OPUS_HARD)
                half ao : TEXCOORD5;
            #endif
            #if defined(_OPUS_UVMAP) || defined(_OPUS_TILED)
                float2 uv : TEXCOORD6;
                half4 tangentWS : TEXCOORD7;
            #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);

                // World lengths of the object axes: a board given as a unit cube scaled up tiles in real metres.
                float4x4 m = GetObjectToWorldMatrix();
                float3 scale = float3(length(m._m00_m10_m20), length(m._m01_m11_m21), length(m._m02_m12_m22));
                output.positionTP = input.positionOS.xyz * scale;
                output.normalTP = normalize(input.normalOS / max(scale, 1e-4));
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
            #if defined(_OPUS_HARD)
                output.ao = lerp(1.0h, input.color.r, _VertexAO);
            #endif
            #if defined(_OPUS_UVMAP) || defined(_OPUS_TILED)
                output.uv = input.uv;
                VertexNormalInputs frame = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.tangentWS = half4(frame.tangentWS, input.tangentOS.w * GetOddNegativeScale());
            #endif
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 normalWS = normalize(input.normalWS);
            #if defined(_OPUS_UVMAP)
                // Textured model: atlas colour, tangent-space normal (OpenGL +Y), AO and roughness map
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                half3 orm = SAMPLE_TEXTURE2D(_OrmMap, sampler_OrmMap, input.uv).rgb;
                half3 tangentNormal = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv));
                half3 tangentWS = normalize(input.tangentWS.xyz);
                half3 bitangentWS = input.tangentWS.w * cross(normalWS, tangentWS);
                normalWS = normalize(tangentNormal.x * tangentWS + tangentNormal.y * bitangentWS + tangentNormal.z * normalWS);
                half height = (half)input.positionWS.y;
                albedo *= lerp(1.0h - 0.28h * _AO, 1.0h, smoothstep(0.0h, 0.35h, height));
                albedo *= orm.r;
                // Roughness -> finishes (SurfaceLibrary.ModelFinishes: matte 0.85 does not shine, glossy 0.22, metallic)
                half shine = saturate((0.85h - orm.g) / 0.63h);
                half gloss = lerp(shine * shine * 0.55h, 0.8h, orm.b);
                half power = lerp(lerp(8.0h, 40.0h, shine), 70.0h, orm.b);
                half reflectAmount = lerp(gloss * 0.36h, 0.45h, orm.b);
                half3 color = OpusShadeHard(albedo, normalWS, input.positionWS, _Wrap, _ShadeDepth, gloss * orm.r, power,
                    reflectAmount * orm.r);
            #elif defined(_OPUS_TILED)
                // Building: tiling tone and normal along each piece's grain; AO and the per-piece tone from the vertex colour
                float2 tuv = input.uv / _Tile;
                half tone = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, tuv).r;
                half3 albedo = _BaseColor.rgb * lerp(1.0h, tone * 2.0h, _Strength);
                half3 tangentNormal = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, tuv), _BumpScale);
                half3 tangentWS = normalize(input.tangentWS.xyz);
                half3 bitangentWS = input.tangentWS.w * cross(normalWS, tangentWS);
                normalWS = normalize(tangentNormal.x * tangentWS + tangentNormal.y * bitangentWS + tangentNormal.z * normalWS);
                half height = (half)input.positionWS.y;
                albedo *= lerp(1.0h - 0.28h * _AO, 1.0h, smoothstep(0.0h, 0.35h, height));
            #if defined(_OPUS_HARD)
                albedo *= input.ao;
                half3 color = OpusShadeHard(albedo, normalWS, input.positionWS, _Wrap, _ShadeDepth, _Gloss * input.ao,
                    _SpecPower, _Reflect * input.ao);
            #else
                half3 color = OpusShade(albedo, normalWS, input.positionWS, _Wrap);
            #endif
            #else
                half3 n = normalize(input.normalTP);
                half3 n2 = n * n;
                half3 w = n2 * n2 * n2;
                w /= max(w.x + w.y + w.z, 1e-4h);

                float3 p = input.positionTP / _Tile;
                float2 uvX = p.zy;
                float2 uvZ = p.xy;
                float2 uvY = p.xz;
                // Roof: the v axis of the top projection always runs up the slope, the u axis parallel to the ridge (shingle rows aligned)
                float2 h = n.xz;
                float hl = length(h);
                if (_SlopeAlign > 0.5h && hl > 0.08)
                {
                    float2 up = -h / hl;
                    uvY = float2(dot(p.xz, float2(up.y, -up.x)), dot(p.xz, up));
                }

                half3 pattern = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvX).rgb * w.x
                              + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvY).rgb * w.y
                              + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvZ).rgb * w.z;
                half3 albedo = _BaseColor.rgb * lerp(1.0h, pattern * 2.0h, _Strength);

                float3 g = input.positionTP / GrimeTile;
                half2 grimeTone = SAMPLE_TEXTURE2D(_GrimeMap, sampler_GrimeMap, g.zy).rg * w.x
                                + SAMPLE_TEXTURE2D(_GrimeMap, sampler_GrimeMap, g.xz).rg * w.y
                                + SAMPLE_TEXTURE2D(_GrimeMap, sampler_GrimeMap, g.xy).rg * w.z;
                albedo *= 1.0h + (grimeTone.y - 0.5h) * 2.0h * _Tone;

                // Grime: sharp-edged blotches at the bottom (thinning towards the grime height, very light wear
                // higher up) + a continuous darkening at the very bottom
                half height = (half)input.positionWS.y;
                half gh = max(_GrimeHeight, 0.01h);
                half lower = 1.0h - smoothstep(gh * 0.35h, gh, height);
                half splotch = grimeTone.x * max(lower, 0.12h);
                half baseBand = saturate(1.0h - height / (gh * 0.4h));
                half grime = saturate(max(splotch, baseBand * baseBand * 0.6h)) * _Grime;
                albedo = lerp(albedo, albedo * GrimeTint, grime);

                // Contact shadow: surfaces closer than 35 cm to the ground get darker
                albedo *= lerp(1.0h - 0.28h * _AO, 1.0h, smoothstep(0.0h, 0.35h, height));
            #if defined(_OPUS_HARD)
                // Blender models: baked AO (recesses, joints, under lips) + hard-surface light
                albedo *= input.ao;
                half3 color = OpusShadeHard(albedo, normalWS, input.positionWS, _Wrap, _ShadeDepth, _Gloss * input.ao,
                    _SpecPower, _Reflect * input.ao);
            #else
                half3 color = OpusShade(albedo, normalWS, input.positionWS, _Wrap);
            #endif
            #endif
                half3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(1.0h - saturate(dot(normalWS, viewDir)), 3.0h);
                color += _OpusSkyAmbient.rgb * rim * _RimStrength;

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 ShadowVert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                return ApplyShadowClamping(positionCS);
            }

            half4 ShadowFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 DepthVert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS.xyz);
            }

            half DepthFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
