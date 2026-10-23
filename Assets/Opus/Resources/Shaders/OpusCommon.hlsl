#ifndef OPUS_COMMON_INCLUDED
#define OPUS_COMMON_INCLUDED

// Shared stylised light: soft "wrap" diffuse light, lavender-tinted shadows,
// sky/ground hemisphere ambient light. The colours come in as globals from Atmosphere.cs.

half4 _OpusSkyAmbient;
half4 _OpusGroundAmbient;
half4 _OpusShadowTint;

half3 OpusAmbient(half3 normalWS)
{
    half t = normalWS.y * 0.5h + 0.5h;
    return lerp(_OpusGroundAmbient.rgb, _OpusSkyAmbient.rgb, t);
}

// albedo: surface colour, wrap: 0 = hard Lambert, 1 = fully soft
half3 OpusShade(half3 albedo, half3 normalWS, float3 positionWS, half wrap)
{
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    Light mainLight = GetMainLight(shadowCoord);

    half ndl = dot(normalWS, mainLight.direction);
    half diffuse = saturate((ndl + wrap) / (1.0h + wrap));
    half lit = diffuse * mainLight.shadowAttenuation;

    half3 litColor = albedo * mainLight.color;
    half3 shadowColor = albedo * _OpusShadowTint.rgb;
    half3 color = lerp(shadowColor, litColor, lit);
    color += albedo * OpusAmbient(normalWS) * 0.25h;
    return color;
}

// Hard surface (painted metal, plastic; the Blender models): the shadow side drops to a dark neutral instead of lavender (the form reads),
// a sun highlight and sky reflection at the edges (thin bright lines on rounded edges).
// depth 0 = same as OpusShade; gloss 0 = no highlight.
static const half3 OpusNeutralShadow = half3(0.56h, 0.53h, 0.55h);

half3 OpusShadeHard(half3 albedo, half3 normalWS, float3 positionWS, half wrap, half depth, half gloss, half power,
    half reflectAmount)
{
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    Light mainLight = GetMainLight(shadowCoord);

    half ndl = dot(normalWS, mainLight.direction);
    half diffuse = saturate((ndl + wrap) / (1.0h + wrap));
    half lit = diffuse * mainLight.shadowAttenuation;

    half3 shadowTint = lerp(_OpusShadowTint.rgb, OpusNeutralShadow, depth);
    half3 color = lerp(albedo * shadowTint, albedo * mainLight.color, lit);
    color += albedo * OpusAmbient(normalWS) * (0.25h * (1.0h - 0.6h * depth));

    half3 viewDir = normalize(GetWorldSpaceViewDir(positionWS));
    half3 halfDir = normalize(mainLight.direction + viewDir);
    half spec = pow(saturate(dot(normalWS, halfDir)), power) * gloss * mainLight.shadowAttenuation * saturate(ndl * 4.0h);
    color += mainLight.color * spec;
    half fresnel = pow(1.0h - saturate(dot(normalWS, viewDir)), 4.0h);
    color += OpusAmbient(reflect(-viewDir, normalWS)) * fresnel * reflectAmount;
    return color;
}

#endif
