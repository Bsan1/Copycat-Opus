#ifndef OPUS_LAWN_INCLUDED
#define OPUS_LAWN_INCLUDED

// Shared access to the lawn state textures (LawnState.cs sets them as globals).
// _OpusLawnState: r = height (1 long, 0 mown), g = dryness, b = mowing direction
// _OpusPatchMap:  r = patch id / 255 (0 = no grass), point sampling
// _OpusPatchPalette: x = patch, y = row (0 base + height multiplier/2, 1 middle, 2 tip, 3 mown)

TEXTURE2D(_OpusLawnState);
SAMPLER(sampler_OpusLawnState);
TEXTURE2D(_OpusLawnWet);        // r = wetness (1 when watered, fades over time)
SAMPLER(sampler_OpusLawnWet);
TEXTURE2D(_OpusPatchMap);
TEXTURE2D(_OpusPatchPalette);

float4 _OpusLawnRect;   // minX, minZ, width, depth (m)
float4 _OpusLawnCells;  // cell X, cell Z, cell size (m)
half4 _OpusDryBase;
half4 _OpusDryMid;
half4 _OpusDryTip;
half4 _OpusFreshBase;   // the colours a watered dry spot greens up to
half4 _OpusFreshMid;
half4 _OpusFreshTip;
half _OpusGreening;

float2 LawnUV(float2 xz)
{
    return (xz - _OpusLawnRect.xy) / _OpusLawnRect.zw;
}

bool InsideLawn(float2 uv)
{
    return uv.x >= 0.0 && uv.y >= 0.0 && uv.x < 1.0 && uv.y < 1.0;
}

uint LawnPatchAt(float2 uv)
{
    int2 cell = clamp((int2)floor(uv * _OpusLawnCells.xy), int2(0, 0), (int2)_OpusLawnCells.xy - 1);
    return (uint)round(LOAD_TEXTURE2D_LOD(_OpusPatchMap, cell, 0).r * 255.0);
}

half4 LawnPalette(uint patch, uint row)
{
    return LOAD_TEXTURE2D_LOD(_OpusPatchPalette, int2(patch, row), 0);
}

half4 LawnStateAt(float2 uv)
{
    return SAMPLE_TEXTURE2D_LOD(_OpusLawnState, sampler_OpusLawnState, uv, 0);
}

// --- Marks on the lawn (LawnOverlay.cs) ---
TEXTURE2D(_OpusPatchEdge);      // r = border cells of the active patch
SAMPLER(sampler_OpusPatchEdge);
float4 _OpusEdgeInfo;           // centre x, centre z, -, visibility
half4 _OpusEdgeColor;
float4 _OpusHighlight;          // origin x, origin z, start time, active
half4 _OpusHighlightColor;
float4 _OpusFinish;             // patch, start time, centre x, centre z
float4 _OpusFinishInfo;         // wave speed, sparkle duration
TEXTURE2D(_OpusPatternGuide);   // r: pattern guide (1 band "A", 0.5 band "B", 0 none), point sampling
float4 _OpusPatternInfo;        // patch, strength

float LawnHash(float2 p)
{
    return frac(sin(dot(p, float2(41.3, 289.1))) * 43758.5453);
}

// Onto the lit colour: the orange dashed border, the show-remaining wave, the finish wave and its sparkle.
// t: position along the blade (constant for the ground).
half3 LawnOverlay(half3 color, float2 xz, float2 uv, uint patch, half4 state, half t)
{
    float now = _Time.y;

    if (_OpusEdgeInfo.w > 0.001)
    {
        // r: border strip, g: dash (from the walking distance along the border; LawnOverlay.MarkDashes)
        half2 edge = SAMPLE_TEXTURE2D_LOD(_OpusPatchEdge, sampler_OpusPatchEdge, uv, 0).rg;
        half lineMask = smoothstep(0.3h, 0.55h, edge.r) * smoothstep(0.35h, 0.6h, edge.g);
        color = lerp(color, _OpusEdgeColor.rgb, lineMask * _OpusEdgeInfo.w);
    }

    // Pattern task: the requested bands are lightly tinted over the long grass (the real stripes show once mown)
    if (_OpusPatternInfo.y > 0.001 && patch == (uint)round(_OpusPatternInfo.x))
    {
        int2 cell = clamp((int2)floor(uv * _OpusLawnCells.xy), int2(0, 0), (int2)_OpusLawnCells.xy - 1);
        half guide = LOAD_TEXTURE2D_LOD(_OpusPatternGuide, cell, 0).r;
        if (guide > 0.25h)
        {
            half band = guide > 0.75h ? 1.0h : -1.0h;
            half tallness = smoothstep(0.3h, 0.8h, state.r) * _OpusPatternInfo.y;
            color *= 1.0h + band * 0.085h * tallness;
            color = lerp(color, _OpusHighlightColor.rgb, (band > 0.0h ? 0.1h : 0.0h) * tallness);
        }
    }

    float sinceHighlight = now - _OpusHighlight.z;
    // w: highlighted patch (-1 = all, 0 = off)
    bool highlightHere = _OpusHighlight.w < -0.5 || (_OpusHighlight.w > 0.5 && patch == (uint)round(_OpusHighlight.w));
    if (highlightHere && sinceHighlight < 2.6)
    {
        float local = sinceHighlight - length(xz - _OpusHighlight.xy) / 16.0;
        half envelope = smoothstep(0.0, 0.15, local) * (1.0 - smoothstep(1.7, 2.4, local));
        half needsWork = saturate(step(0.5h, state.r) + step(0.3h, state.g));
        half pulse = 0.85h + 0.15h * sin(local * 12.0);
        color = lerp(color, _OpusHighlightColor.rgb * 1.25h, envelope * needsWork * 0.8h * pulse);
    }

    float sinceFinish = now - _OpusFinish.y;
    if (patch == (uint)round(_OpusFinish.x) && sinceFinish < 8.0)
    {
        float local = sinceFinish - length(xz - _OpusFinish.zw) / _OpusFinishInfo.x;
        half band = exp(-(local / 0.12) * (local / 0.12));
        half glow = local > 0.0 ? saturate(1.0 - local / _OpusFinishInfo.y) : 0.0h;
        half sparkle = step(0.93h, LawnHash(floor(xz * 6.0) + floor(sinceFinish * 8.0))) * glow;
        color = lerp(color, _OpusHighlightColor.rgb * 1.3h, band * 0.85h);
        color += half3(1.0h, 0.93h, 0.6h) * (glow * 0.22h + sparkle * 0.6h) * (0.5h + 0.5h * t);
    }
    return color;
}

// x = wetness (fades), y = greening (permanent)
half2 LawnWetAt(float2 uv)
{
    return SAMPLE_TEXTURE2D_LOD(_OpusLawnWet, sampler_OpusLawnWet, uv, 0).rg;
}

// Wet grass: darker and greener (in the reference the area around a watered spot looks like a dark green shadow).
half3 LawnApplyWet(half3 albedo, half wet)
{
    return lerp(albedo, albedo * half3(0.55h, 0.7h, 0.5h), wet * 0.85h);
}

// Colour from base to tip: base → middle → tip; dryness pulls towards straw tones, shortness towards the mown colour.
half3 LawnBladeColor(uint patch, half t, half dryness, half height01, half greening)
{
    half3 baseColor = LawnPalette(patch, 0).rgb;
    half3 midColor = LawnPalette(patch, 1).rgb;
    half3 tipColor = LawnPalette(patch, 2).rgb;
    half3 cutColor = LawnPalette(patch, 3).rgb;

    // A watered dry spot turns fresh green
    half fresh = greening * _OpusGreening;
    baseColor = lerp(baseColor, _OpusFreshBase.rgb, fresh);
    midColor = lerp(midColor, _OpusFreshMid.rgb, fresh);
    tipColor = lerp(tipColor, _OpusFreshTip.rgb, fresh);

    baseColor = lerp(baseColor, _OpusDryBase.rgb, dryness);
    midColor = lerp(midColor, _OpusDryMid.rgb, dryness);
    tipColor = lerp(tipColor, _OpusDryTip.rgb, dryness);

    half3 tall = t < 0.5h
        ? lerp(baseColor, midColor, smoothstep(0.0h, 0.5h, t))
        : lerp(midColor, tipColor, smoothstep(0.5h, 1.0h, t));
    // In the reference mown grass is lighter and brighter than long grass (a lime green carpet).
    half3 cut = lerp(lerp(cutColor * 0.9h, cutColor * 1.06h, t), _OpusDryMid.rgb, dryness * 0.8h);
    return lerp(cut, tall, smoothstep(0.15h, 0.75h, height01));
}

#endif
