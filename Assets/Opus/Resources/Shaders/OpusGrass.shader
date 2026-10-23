Shader "Opus/Grass"
{
    // One instance per tuft (GrassField.cs, Graphics.RenderMeshPrimitives). Blade shape, wind, pushing
    // and colour are all here; the light is computed per vertex (enough at blade scale and very cheap).
    Properties
    {
        _Wrap("Soft light", Range(0, 1)) = 0.6
        _Translucency("Translucency", Range(0, 1)) = 0.4
        _RootDarkness("Root darkness", Range(0, 1)) = 0.3
        _StripeContrast("Stripe contrast", Range(0, 0.5)) = 0.16
        _Exposure("Brightness", Range(0.5, 1.5)) = 0.88
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "OpusLawn.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half _Wrap;
            half _Translucency;
            half _RootDarkness;
            half _StripeContrast;
            half _Exposure;
        CBUFFER_END

        float4 _ChunkData;       // start X, start Z, seed, max tufts
        float4 _OpusGrassParams; // long height, mown height, blade width, density
        float4 _OpusGrassLod;    // full density distance, far distance, far fraction, chunk size
        float4 _OpusWind;        // direction X, direction Z, strength, speed
        float4 _OpusPushers[4];  // x, z, radius, strength
        float _OpusPusherCount;
        TEXTURE2D(_OpusWindNoise);
        SAMPLER(sampler_OpusWindNoise);

        struct GrassAttributes
        {
            float3 positionOS : POSITION;
            float2 uv : TEXCOORD0;      // across the width, along the length
            float4 blade : TEXCOORD1;   // direction, height multiplier, natural bend, width multiplier
            float4 extra : TEXCOORD2;   // tone offset
            uint instanceID : SV_InstanceID;
        };

        struct Blade
        {
            float3 positionWS;
            half3 normalWS;
            half t;
            half height01;
            half dryness;
            half gust;
            half hue;
            half stripe;   // mowing stripe tone (-1 dark .. 1 light)
            half2 wet;     // x wetness, y greening (0..1)
            float2 rootXZ;
            float2 lawnUV;
            half4 state;
            uint patch;
            bool visible;
        };

        uint OpusHash(uint v)
        {
            uint state = v * 747796405u + 2891336453u;
            uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
            return (word >> 22u) ^ word;
        }

        float OpusRand(uint v)
        {
            return OpusHash(v) * (1.0 / 4294967296.0);
        }

        Blade BuildBlade(GrassAttributes input)
        {
            Blade b = (Blade)0;
            uint id = input.instanceID;
            uint seed = (uint)_ChunkData.z;

            // R2 low-discrepancy sequence: every prefix is evenly spread, drawing the first K instances by distance gives a gapless LOD.
            float2 shift = float2(OpusRand(seed * 9781u), OpusRand(seed * 6271u + 1u));
            float2 r2 = frac(shift + float2(0.7548776662, 0.5698402910) * (float)id);
            float2 tuft = _ChunkData.xy + r2 * _OpusGrassLod.w;

            float rotation = OpusRand(OpusHash(id) ^ (seed * 2654435761u)) * TWO_PI;
            float s, c;
            sincos(rotation, s, c);
            float2 root = tuft + float2(input.positionOS.x * c - input.positionOS.z * s, input.positionOS.x * s + input.positionOS.z * c);

            float2 uv = LawnUV(root);
            uint patch = InsideLawn(uv) ? LawnPatchAt(uv) : 0u;

            // Density by distance: tufts whose index passes the threshold shrink away.
            float distance = length(root - _WorldSpaceCameraPos.xz);
            float keep = _OpusGrassParams.w * lerp(1.0, _OpusGrassLod.z, smoothstep(_OpusGrassLod.x, _OpusGrassLod.y, distance));
            float rank = ((float)id + 0.5) / _ChunkData.w;
            float fade = saturate((keep - rank) / 0.03);
            if (patch == 0u || fade <= 0.0)
                return b;

            half4 state = LawnStateAt(uv);
            float heightScale = LawnPalette(patch, 0).a * 2.0;
            float tall = _OpusGrassParams.x * heightScale * input.blade.y;
            float cut = _OpusGrassParams.y * lerp(0.75, 1.25, saturate((input.blade.y - 0.65) * 2.0));
            float height = lerp(cut, tall, state.r) * fade;

            // Mowing direction (~0 on a never-mown cell): short grass leans that way and sets the stripe tone.
            float shortness = 1.0 - state.r;
            float2 mowRaw = state.ba * 2.0 - 1.0;
            float mowStrength = saturate(length(mowRaw) * 1.4);
            float2 mowDir = mowStrength > 0.01 ? normalize(mowRaw) : float2(0.0, 0.0);

            float t = input.uv.y;
            float side = input.uv.x - 0.5;
            float angle = input.blade.x * TWO_PI + rotation;
            float2 facing;
            sincos(angle, facing.y, facing.x);
            float2 across = float2(-facing.y, facing.x);
            // Full blade: tapers late towards the tip.
            float width = _OpusGrassParams.z * input.blade.w * (1.0 - 0.85 * t * t) * saturate(fade * 2.0);

            // The bend is an "angle vector" (radians, directional). Components:
            float2 windDir = _OpusWind.xy;
            float time = _Time.y;
            // 1) Combing: a common direction changing slowly at a large scale → a brushed, wavy look
            float comb = SAMPLE_TEXTURE2D_LOD(_OpusWindNoise, sampler_OpusWindNoise, root * 0.018 + 0.37, 0).r;
            float combAngle = atan2(windDir.y, windDir.x) + (comb - 0.5) * 2.4;
            float2 combDir = float2(cos(combAngle), sin(combAngle));
            // 2) Wind: bands sweeping the garden + noise; 3) blade flutter; 4) the blade's own small bend
            float band = sin(dot(root, windDir) * 0.5 - time * _OpusWind.w) * 0.5 + 0.5;
            float noise = SAMPLE_TEXTURE2D_LOD(_OpusWindNoise, sampler_OpusWindNoise, root * 0.045 - windDir * time * 0.035, 0).r;
            float gust = saturate(band * 0.65 + noise * 0.75 - 0.3);
            float flutter = sin(time * 4.7 + input.extra.x * 31.0 + dot(root, float2(1.3, 2.1))) * 0.06;
            float2 lean = combDir * (0.32 * (1.0 - 0.7 * shortness))
                        + facing * input.blade.z * 0.22
                        + windDir * (gust * _OpusWind.z * (1.0 - 0.6 * shortness))
                        + across * flutter
                        + mowDir * (0.85 * shortness * mowStrength);

            // Pushing: the mower housing and the player's feet push the grass outwards; the blades under the housing get crushed.
            float squash = 1.0;
            [unroll]
            for (int k = 0; k < 4; k++)
            {
                if ((float)k < _OpusPusherCount)
                {
                    float4 pusher = _OpusPushers[k];
                    float2 away = root - pusher.xy;
                    float len = max(length(away), 1e-4);
                    float influence = saturate(1.0 - len / pusher.z) * pusher.w;
                    lean += (away / len) * influence * 1.1;
                    // The inner zone (~65% of the radius, on the mower the housing itself) is laid almost flat.
                    squash = min(squash, 1.0 - smoothstep(0.0, 0.35, influence) * 0.88);
                }
            }

            // A length-preserving arc: the total angle theta bends evenly along the blade.
            float leanLength = length(lean);
            float theta = min(leanLength, 1.3);
            float2 leanDir = leanLength > 1e-4 ? lean / leanLength : facing;
            float bladeLength = height * squash;
            float a = theta * t;
            float horizontal, vertical;
            if (theta < 1e-3)
            {
                horizontal = 0.5 * bladeLength * theta * t * t;
                vertical = bladeLength * t;
            }
            else
            {
                horizontal = bladeLength * (1.0 - cos(a)) / theta;
                vertical = bladeLength * sin(a) / theta;
            }

            float2 offset = leanDir * horizontal;
            b.positionWS = float3(root.x + across.x * side * width + offset.x, vertical, root.y + across.y * side * width + offset.y);
            float sa = sin(a);
            b.normalWS = normalize(float3(leanDir.x * sa * 0.6 + facing.x * 0.3, 1.0, leanDir.y * sa * 0.6 + facing.y * 0.3));
            b.t = t;
            b.height01 = state.r;
            b.dryness = state.g;
            b.gust = gust;
            b.hue = input.extra.x;
            // Grass leaning away from the viewer looks light, grass leaning towards the viewer dark → stripes.
            float2 toBlade = root - _WorldSpaceCameraPos.xz;
            b.stripe = dot(mowDir, toBlade / max(length(toBlade), 1e-3)) * mowStrength * shortness;
            b.wet = LawnWetAt(uv);
            b.rootXZ = root;
            b.lawnUV = uv;
            b.state = state;
            b.patch = patch;
            b.visible = true;
            return b;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex GrassVert
            #pragma fragment GrassFrag
            // The shadow is sampled in the vertex shader; screen-space shadows cannot be used there.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "OpusCommon.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 color : COLOR;
                half fogFactor : TEXCOORD0;
            };

            Varyings GrassVert(GrassAttributes input)
            {
                Varyings output = (Varyings)0;
                Blade b = BuildBlade(input);
                if (!b.visible)
                {
                    output.positionCS = float4(2.0, 2.0, 2.0, 1.0); // outside the clip space
                    return output;
                }

                half3 albedo = LawnApplyWet(LawnBladeColor(b.patch, b.t, b.dryness, b.height01, b.wet.y), b.wet.x);
                albedo *= 1.0h + (b.hue - 0.5h) * 0.14h;
                albedo += LawnPalette(b.patch, 2).rgb * (b.gust * 0.12h * b.t); // shine in the gust band

                float4 shadowCoord = TransformWorldToShadowCoord(b.positionWS);
                Light light = GetMainLight(shadowCoord);
                half ndl = dot(b.normalWS, light.direction);
                half diffuse = saturate((ndl + _Wrap) / (1.0h + _Wrap));
                half lit = diffuse * light.shadowAttenuation;
                half3 viewDir = normalize(_WorldSpaceCameraPos - b.positionWS);
                half back = pow(saturate(dot(viewDir, -light.direction)), 3.0h) * _Translucency * b.t * light.shadowAttenuation;

                half3 color = lerp(albedo * _OpusShadowTint.rgb, albedo * light.color, lit);
                color += albedo * OpusAmbient(b.normalWS) * 0.25h;
                color += albedo * light.color * back;
                // Root darkening only in long grass (the shadow of dense blades); none on the mown carpet.
                color *= lerp(1.0h - _RootDarkness * b.height01, 1.0h, pow(saturate(b.t), 0.7h));
                color *= 1.0h + _StripeContrast * b.stripe;
                color *= _Exposure; // the light made the blades ~1.3x brighter than the palette; by the reference measurement
                color = LawnOverlay(color, b.rootXZ, b.lawnUV, b.patch, b.state, b.t);

                output.positionCS = TransformWorldToHClip(b.positionWS);
                output.color = color;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 GrassFrag(Varyings input) : SV_Target
            {
                return half4(MixFog(input.color, input.fogFactor), 1.0h);
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
            #pragma target 4.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            float4 DepthVert(GrassAttributes input) : SV_POSITION
            {
                Blade b = BuildBlade(input);
                return b.visible ? TransformWorldToHClip(b.positionWS) : float4(2.0, 2.0, 2.0, 1.0);
            }

            half DepthFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
