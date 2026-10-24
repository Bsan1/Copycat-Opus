"""Reference-driven 3D asset guidelines for Copycat Opus.

Schema 3 uses StyleGuide.py for the AI guide, visual report, scoped briefs and informational
per-image measurements. Technical checks and asset review state do not certify visual style.
Legacy schema-2 colour helpers remain for compatibility; their metrics are not validated style gates.

Commands: bible; brief NAME [--class CLASS]; extract [IMAGE ...]; report [MODEL ...] [--json]; validate.
"""
import argparse
import glob
import json
import math
import os
import re
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
STYLE = os.path.join(ROOT, "tools", "blender", "style", "style.json")
OUT = os.path.join(ROOT, "Builds", "Style")
BOARD = os.path.join(OUT, "style_board.png")
PALETTE_SIZE = 16
LIGHTNESS_PERCENTILES = (0.5, 99.5)
CHROMA_PERCENTILE = 99.5
CHROMA_MARGIN = 1.10
HUE_MIN_CHROMA = 8.0      # below this a colour's hue is noise: only the overall ceiling applies to it
HUE_NEIGHBOUR = 0.8       # a hue family the style never uses gets this share of its neighbours' ceiling (at least the floor)
# 24 October 2026, the near-miss test: the shed, house and tree with their colours 2.6 x more saturated stayed under the one
# overall chroma ceiling (it comes from the mower's red deck), so a neon-green tree passed. The style's greens reach 54:
# the ceiling is per hue family now.
DETAIL_SIZE = 256
HEX = re.compile(r"^[A-Za-z]+_([0-9A-Fa-f]{6})(?:_x[0-9.]+)?_[a-z]+$")
KINDS = ("invariant", "tunable")
ENFORCE = ("error", "warn")   # a rule's gate fails the run, or only reports
# Cohen-Or et al. 2006 harmonic templates: sectors as (centre offset in degrees, width in degrees)
TEMPLATES = {"i": [(0, 18)], "V": [(0, 93.6)], "L": [(0, 18), (90, 79.2)], "I": [(0, 18), (180, 18)],
             "T": [(0, 180)], "Y": [(0, 93.6), (180, 18)], "X": [(0, 93.6), (180, 93.6)]}
HUE_NAMES = ["red", "orange", "yellow", "lime", "green", "teal", "cyan", "azure", "blue", "violet", "purple", "pink"]


# ----------------------------------------------------------------------------------------------------------------------
# Colour spaces
# ----------------------------------------------------------------------------------------------------------------------

def _linear(rgb):
    c = np.asarray(rgb, dtype=float) / 255.0
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def srgb_to_lab(rgb):
    """rgb (..., 3) 0..255 -> CIELAB (D65)."""
    m = np.array([[0.4124564, 0.3575761, 0.1804375], [0.2126729, 0.7151522, 0.0721750], [0.0193339, 0.1191920, 0.9503041]])
    xyz = _linear(rgb) @ m.T / np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > (6 / 29) ** 3, np.cbrt(xyz), xyz / (3 * (6 / 29) ** 2) + 4 / 29)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], axis=-1)


def lab_to_srgb(lab):
    lab = np.asarray(lab, dtype=float)
    fy = (lab[..., 0] + 16) / 116
    f = np.stack([fy + lab[..., 1] / 500, fy, fy - lab[..., 2] / 200], axis=-1)
    xyz = np.where(f > 6 / 29, f ** 3, 3 * (6 / 29) ** 2 * (f - 4 / 29)) * np.array([0.95047, 1.0, 1.08883])
    m = np.array([[3.2404542, -1.5371385, -0.4985314], [-0.9692660, 1.8760108, 0.0415560], [0.0556434, -0.2040259, 1.0572252]])
    c = np.clip(xyz @ m.T, 0, 1)
    c = np.where(c <= 0.0031308, 12.92 * c, 1.055 * c ** (1 / 2.4) - 0.055)
    return np.round(c * 255).astype(int)


def srgb_to_oklab(rgb):
    """OKLab (Ottosson 2020): better hue uniformity than CIELAB, used for hue families."""
    lin = _linear(rgb)
    m1 = np.array([[0.4122214708, 0.5363325363, 0.0514459929], [0.2119034982, 0.6806995451, 0.1073969566],
                   [0.0883024619, 0.2817188376, 0.6299787005]])
    m2 = np.array([[0.2104542553, 0.7936177850, -0.0040720468], [1.9779984951, -2.4285922050, 0.4505937099],
                   [0.0259040371, 0.7827717662, -0.8086757660]])
    return np.cbrt(lin @ m1.T) @ m2.T


def chroma(lab):
    lab = np.asarray(lab, dtype=float)
    return np.hypot(lab[..., 1], lab[..., 2])


def hex_of(rgb):
    return "#%02X%02X%02X" % tuple(int(v) for v in rgb)


# ----------------------------------------------------------------------------------------------------------------------
# Measures (one image)
# ----------------------------------------------------------------------------------------------------------------------

def _hsv(rgb):
    c = np.asarray(rgb, dtype=float) / 255.0
    mx, mn = c.max(axis=-1), c.min(axis=-1)
    d = mx - mn
    s = np.where(mx > 0, d / np.maximum(mx, 1e-9), 0)
    r, g, b = c[..., 0], c[..., 1], c[..., 2]
    h = np.where(d == 0, 0, np.where(mx == r, ((g - b) / np.maximum(d, 1e-9)) % 6,
                                     np.where(mx == g, (b - r) / np.maximum(d, 1e-9) + 2, (r - g) / np.maximum(d, 1e-9) + 4)))
    return (h * 60) % 360, s


def harmony(rgb):
    """Best Cohen-Or template and its normalised fit (mean saturation-weighted hue distance to the template, degrees)."""
    h, s = _hsv(rgb.reshape(-1, 3))
    keep = s > 0.15
    h, s = h[keep], s[keep]
    if len(h) < 50:
        return {"template": "N", "fit_deg": 0.0}
    best = None
    for name, sectors in TEMPLATES.items():
        for alpha in range(0, 360, 3):
            dist = np.full(len(h), 180.0)
            for offset, width in sectors:
                centre = (alpha + offset) % 360
                d = np.abs((h - centre + 180) % 360 - 180) - width / 2
                dist = np.minimum(dist, np.maximum(d, 0))
            fit = float((dist * s).sum() / s.sum())
            if best is None or fit < best[2]:
                best = (name, alpha, fit)
    return {"template": best[0], "angle": best[1], "fit_deg": round(best[2], 2)}


def _resize(image):
    w, h = image.size
    scale = DETAIL_SIZE / max(w, h)
    return image.resize((max(8, round(w * scale)), max(8, round(h * scale))), Image.BICUBIC)


def fourier_slope(gray):
    """Slope of the radially averaged Fourier amplitude spectrum (log-log, radii 4..N/4): natural scenes about 1.2;
    shallower = more fine detail (Redies; Aesthetics Toolbox)."""
    g = gray - gray.mean()
    window = np.outer(np.hanning(g.shape[0]), np.hanning(g.shape[1]))
    amp = np.abs(np.fft.fftshift(np.fft.fft2(g * window)))
    cy, cx = np.array(amp.shape) // 2
    y, x = np.indices(amp.shape)
    r = np.hypot(y - cy, x - cx).astype(int)
    radii = np.arange(4, min(amp.shape) // 4)
    means = np.array([amp[r == k].mean() for k in radii])
    ok = means > 0
    slope = -np.polyfit(np.log(radii[ok]), np.log(means[ok]), 1)[0]
    return float(slope)


def measure(image):
    """Five axes of one image (sRGB PIL image): colour, value, hue, light, detail."""
    rgb = np.asarray(image.convert("RGB"), dtype=float)
    flat = rgb.reshape(-1, 3)
    lab = srgb_to_lab(flat)
    L, C = lab[:, 0], chroma(lab)
    ok = srgb_to_oklab(flat)
    ok_c = np.hypot(ok[:, 1], ok[:, 2])
    ok_h = (np.degrees(np.arctan2(ok[:, 2], ok[:, 1])) + 360) % 360
    hues = np.bincount(((ok_h + 15) % 360 // 30).astype(int), weights=ok_c, minlength=12)
    hues = hues / max(hues.sum(), 1e-9)
    dark, light = L <= np.percentile(L, 20), L >= np.percentile(L, 80)
    shift = lab[light].mean(axis=0)[1:] - lab[dark].mean(axis=0)[1:]   # (a, b) of the lights minus the shadows
    small = _resize(image.convert("RGB"))
    gray = srgb_to_lab(np.asarray(small, dtype=float))[..., 0]
    gy, gx = np.gradient(gray)
    grad = np.hypot(gx, gy)
    k = 5
    pad = np.pad(gray, k // 2, mode="edge")
    windows = np.lib.stride_tricks.sliding_window_view(pad, (k, k))
    return {
        "lightness_median": float(np.median(L)), "lightness_p10": float(np.percentile(L, 10)),
        "lightness_p90": float(np.percentile(L, 90)), "contrast_rms": float(L.std()),
        "notan_dark": float((L < 35).mean()), "notan_mid": float(((L >= 35) & (L < 70)).mean()), "notan_light": float((L >= 70).mean()),
        "chroma_median": float(np.median(C)), "chroma_p90": float(np.percentile(C, 90)),
        "chroma_dark": float(C[L < 35].mean()) if (L < 35).any() else 0.0,
        "chroma_mid": float(C[(L >= 35) & (L < 70)].mean()) if ((L >= 35) & (L < 70)).any() else 0.0,
        "chroma_light": float(C[L >= 70].mean()) if (L >= 70).any() else 0.0,
        "shadow_shift_a": float(shift[0]), "shadow_shift_b": float(shift[1]),
        "shadow_colour": hex_of(lab_to_srgb(lab[dark].mean(axis=0))), "light_colour": hex_of(lab_to_srgb(lab[light].mean(axis=0))),
        "fourier_slope": fourier_slope(gray), "edge_density": float((grad > 6.0).mean()),
        "busyness": float(windows.std(axis=(-1, -2)).mean()),
        "hue_families": [round(float(v), 4) for v in hues], "harmony": harmony(rgb),
    }


SCALARS = ["lightness_median", "lightness_p10", "lightness_p90", "contrast_rms", "notan_dark", "notan_mid", "notan_light",
           "chroma_median", "chroma_p90", "chroma_dark", "chroma_mid", "chroma_light", "shadow_shift_a", "shadow_shift_b",
           "fourier_slope", "edge_density", "busyness"]


def profile(per_image):
    """Median and IQR (p25-p75) of every scalar over the images, plus the summed hue families and harmony templates."""
    out = {}
    for key in SCALARS:
        v = np.array([m[key] for m in per_image])
        out[key] = {"median": round(float(np.median(v)), 3), "p25": round(float(np.percentile(v, 25)), 3),
                    "p75": round(float(np.percentile(v, 75)), 3), "p10": round(float(np.percentile(v, 10)), 3),
                    "p90": round(float(np.percentile(v, 90)), 3)}
    hues = np.mean([m["hue_families"] for m in per_image], axis=0)
    out["hue_families"] = {HUE_NAMES[i]: round(float(hues[i]), 3) for i in range(12)}
    templates = [m["harmony"]["template"] for m in per_image]
    out["harmony"] = {t: templates.count(t) for t in sorted(set(templates))}
    return out


def leave_one_out(per_image, band=(10, 90), margin=0.15):
    """Each image's scalars against the band of the other images (p10-p90, widened by 15 % of its width): the share of
    an image's measures inside. A low share marks an image that does not belong to the style, or too few sources."""
    rows = []
    for i, m in enumerate(per_image):
        others = [o for j, o in enumerate(per_image) if j != i]
        inside = 0
        for key in SCALARS:
            v = np.array([o[key] for o in others])
            lo, hi = np.percentile(v, band)
            pad = (hi - lo) * margin
            inside += lo - pad <= m[key] <= hi + pad
        rows.append(round(inside / len(SCALARS), 2))
    return rows


# ----------------------------------------------------------------------------------------------------------------------
# Palettes, sources, models
# ----------------------------------------------------------------------------------------------------------------------

def kmeans(points, k, seed=7, rounds=40):
    """Deterministic k-means (farthest-point start from a seeded first centre)."""
    rng = np.random.default_rng(seed)
    centres = [points[rng.integers(len(points))]]
    for _ in range(1, k):
        d = np.min(((points[:, None, :] - np.array(centres)[None]) ** 2).sum(-1), axis=1)
        centres.append(points[int(np.argmax(d))])
    centres = np.array(centres)
    labels = np.zeros(len(points), dtype=int)
    for _ in range(rounds):
        labels = np.argmin(((points[:, None, :] - centres[None]) ** 2).sum(-1), axis=1)
        moved = np.array([points[labels == i].mean(axis=0) if np.any(labels == i) else centres[i] for i in range(k)])
        if np.allclose(moved, centres):
            break
        centres = moved
    shares = np.bincount(labels, minlength=k) / len(points)
    order = np.argsort(-shares)
    return centres[order], shares[order]


def hue_bin(lab):
    """CIELAB hue family (12 of 30 degrees, family 0 centred on 0 degrees) of one or more Lab colours."""
    lab = np.asarray(lab, dtype=float)
    return (((np.degrees(np.arctan2(lab[..., 2], lab[..., 1])) + 375) % 360) // 30).astype(int)


def hue_ceilings(groups, floor):
    """The chroma ceiling per hue family: the highest the style's own colours reach in that family (p99 of each group of
    pixels, every authored colour; x CHROMA_MARGIN). A family the style never uses gets HUE_NEIGHBOUR of its neighbours'
    ceiling; none is below `floor` (muted colours of any hue are always allowed)."""
    own = np.zeros(12)
    for lab, exact in groups:
        lab = np.asarray(lab, dtype=float).reshape(-1, 3)
        if not len(lab):
            continue
        c, b = chroma(lab), hue_bin(lab)
        for family in range(12):
            hit = c[(b == family) & (c >= HUE_MIN_CHROMA)]
            if len(hit) and (exact or len(hit) >= 50):
                own[family] = max(own[family], float(hit.max() if exact else np.percentile(hit, 99)) * CHROMA_MARGIN)
    out = [max(own[f], HUE_NEIGHBOUR * max(own[(f - 1) % 12], own[(f + 1) % 12]), floor) for f in range(12)]
    return [round(float(v), 1) for v in out]


def gamut_and_palette(lab, exact=None):
    """Lightness range, chroma ceiling (+ margin), palette and accents; `exact`: authored Lab colours that must be inside."""
    lo, hi = np.percentile(lab[:, 0], LIGHTNESS_PERCENTILES)
    ceiling = float(np.percentile(chroma(lab), CHROMA_PERCENTILE))
    if exact is not None and len(exact):
        lo, hi = min(lo, float(exact[:, 0].min())), max(hi, float(exact[:, 0].max()))
        ceiling = max(ceiling, float(chroma(exact).max()))
    centres, shares = kmeans(lab, PALETTE_SIZE)
    gamut = {"lightness": [round(float(lo) - 1, 1), round(float(hi) + 1, 1)], "chroma_max": round(ceiling * CHROMA_MARGIN, 1),
             "chroma_median": round(float(np.median(chroma(lab))), 1)}
    palette = [{"hex": hex_of(lab_to_srgb(c)), "lab": [round(float(v), 1) for v in c], "share": round(float(sh), 3)}
               for c, sh in zip(centres, shares)]
    # Accents: small-share, high-chroma colours (the 5 % most saturated pixels, clustered)
    strong = lab[chroma(lab) >= np.percentile(chroma(lab), 95)]
    accents = []
    if len(strong) >= 20:
        ac, ash = kmeans(strong, 5, seed=11)
        accents = [{"hex": hex_of(lab_to_srgb(c)), "lab": [round(float(v), 1) for v in c]} for c in ac]
    return gamut, palette, accents


def reference_crops():
    """The reference crops the generators name (Builds/Models/*/views.json), each once."""
    seen, crops = set(), []
    for path in sorted(glob.glob(os.path.join(ROOT, "Builds", "Models", "*", "views.json"))):
        with open(path, encoding="utf-8") as f:
            for view in json.load(f)["views"]:
                if view.get("type") != "reference":
                    continue
                key = (view["path"], tuple(view["crop"]))
                if key not in seen and os.path.exists(os.path.join(ROOT, view["path"])):
                    seen.add(key)
                    crops.append(key)
    return crops


def load_sources(images):
    sources = [(p, None) for p in images] if images else reference_crops()
    out = []
    for path, crop in sources:
        image = Image.open(path if os.path.isabs(path) else os.path.join(ROOT, path)).convert("RGB")
        if crop:
            x, y, w, h = crop
            image = image.crop((x, y, x + w, y + h))
        out.append((f"{path}{' crop ' + str(list(crop)) if crop else ''}", image))
    return out


def spec(model):
    with open(os.path.join(ROOT, "Assets", "Opus", "ModelSpecs", model + ".json"), encoding="utf-8") as f:
        return json.load(f)


def built_models():
    return sorted(os.path.basename(p)[:-5] for p in glob.glob(os.path.join(ROOT, "Assets", "Opus", "ModelSpecs", "*.json")))


def surface_colours(s):
    out = []
    for surface in s.get("surfaces", []):
        m = HEX.match(surface["name"])
        if m:
            out.append((surface, srgb_to_lab([int(m.group(1)[i:i + 2], 16) for i in (0, 2, 4)])))
    return out


def atlas_pixels(s, count, rng):
    for key, rel in ((s.get("atlas") or {}).get("textures") or {}).items():
        if "color" in key and os.path.exists(os.path.join(ROOT, rel)):
            a = np.asarray(Image.open(os.path.join(ROOT, rel)).convert("RGB")).reshape(-1, 3)
            a = a[a.sum(axis=1) > 0]
            return a[rng.choice(len(a), size=min(len(a), count), replace=False)]
    return None


def model_colours(models):
    rng = np.random.default_rng(5)
    exact, pixels = [], []
    for model in models:
        s = spec(model)
        exact += [lab for surface, lab in surface_colours(s) if "style exception" not in surface.get("note", "").lower()]
        a = atlas_pixels(s, 8000, rng)
        if a is not None:
            pixels.append(srgb_to_lab(a))
    exact = np.array(exact) if exact else np.zeros((0, 3))
    return (np.concatenate([exact] + pixels) if pixels else exact), exact


def load_style():
    if not os.path.exists(STYLE):
        return {}
    with open(STYLE, encoding="utf-8") as f:
        return json.load(f)


def save_style(style):
    os.makedirs(os.path.dirname(STYLE), exist_ok=True)
    with open(STYLE, "w", encoding="utf-8", newline="\n") as f:
        json.dump(style, f, indent=2, ensure_ascii=False)
        f.write("\n")


def validate(style):
    """Validate the current project guide, or a legacy schema-2 rules document."""
    if style.get("schema", 2) >= 3:
        import StyleGuide
        return StyleGuide.validate_project(ROOT)
    problems = []
    ids = [r.get("id") for r in style.get("rules", [])]
    for r in style.get("rules", []):
        if not r.get("id") or not r.get("text"):
            problems.append(f"a rule without id or text: {r}")
        if r.get("kind") not in KINDS:
            problems.append(f"{r.get('id')}: kind must be one of {KINDS}")
        if not str(r.get("check", "")).strip():
            problems.append(f"{r.get('id')}: no check (name the gate, test or 'review' that enforces it)")
        if r.get("enforce", "error") not in ENFORCE:
            problems.append(f"{r.get('id')}: enforce must be one of {ENFORCE}")
    for miss in style.get("near_misses", []):
        if miss.get("rule") not in ids:
            problems.append(f"near miss {miss.get('variant')}: names no rule of the style ({miss.get('rule')})")
    duplicates = sorted({i for i in ids if ids.count(i) > 1})
    if duplicates:
        problems.append(f"rule ids used twice: {duplicates}")
    for key in ("style_version", "stylisation", "summary"):
        if key not in style:
            problems.append(f"no {key}")
    return problems


# ----------------------------------------------------------------------------------------------------------------------
# extract / report
# ----------------------------------------------------------------------------------------------------------------------

def extract(images):
    style = load_style()
    if style.get("schema", 2) >= 3:
        import StyleGuide
        result = StyleGuide.measure(ROOT, images or None)
        print("Measurements are informational per-image observations; guide rules were not changed.")
        return result
    problems = validate(style)
    if problems:
        sys.exit("style.json: " + "; ".join(problems))
    sources = load_sources(images)
    if len(sources) < 3:
        sys.exit("give at least 3 source images (more is better: 20-50 frames make a stable profile)")
    rng = np.random.default_rng(3)
    pixels, per_image = [], []
    for name, image in sources:
        a = np.asarray(image).reshape(-1, 3)
        pixels.append(a[rng.choice(len(a), size=min(len(a), 6000), replace=False)])
        m = measure(image)
        m["source"] = name
        per_image.append(m)
    lit_lab = srgb_to_lab(np.concatenate(pixels))
    lit_gamut, lit_palette, lit_accents = gamut_and_palette(lit_lab)
    approvals = os.path.join(ROOT, "tools", "blender", "approvals.json")
    approved = sorted(json.load(open(approvals, encoding="utf-8"))) if os.path.exists(approvals) else []
    built = built_models()
    defining = [m for m in approved if m in built] or [m for m in built if m != "calibration"]
    albedo_lab, exact = model_colours(defining)
    albedo_gamut, albedo_palette, albedo_accents = gamut_and_palette(albedo_lab, exact)
    for model in defining:   # a defining model passes its own style
        a = atlas_pixels(spec(model), 20000, np.random.default_rng(1))
        if a is None:
            continue
        lab = srgb_to_lab(a)
        p1, p99 = (float(v) for v in np.percentile(lab[:, 0], (1, 99)))
        albedo_gamut["lightness"] = [round(min(albedo_gamut["lightness"][0], p1 - 1), 1),
                                     round(max(albedo_gamut["lightness"][1], p99 + 1), 1)]
        albedo_gamut["chroma_max"] = round(max(albedo_gamut["chroma_max"], float(np.percentile(chroma(lab), 99)) * CHROMA_MARGIN), 1)
    groups = [(exact, True)] + [(srgb_to_lab(a), False) for a in
                                (atlas_pixels(spec(m), 20000, np.random.default_rng(1)) for m in defining) if a is not None]
    # The floor is the style's median chroma: with twice that (31), the shed's grey-blue siding 2.6 x more saturated (a
    # loud teal, chroma 22) still passed (24 October 2026, the near-miss page)
    albedo_gamut["chroma_floor"] = albedo_gamut["chroma_median"]
    albedo_gamut["chroma_by_hue"] = hue_ceilings(groups, floor=albedo_gamut["chroma_floor"])
    coverage = leave_one_out(per_image)
    style["measured"] = {
        "settings": {"colour": "CIELAB D65 (gamut, value), OKLab (hue)", "detail_resize": f"Pillow BICUBIC, {DETAIL_SIZE} px long side"},
        "lit": {"sources": [m["source"] for m in per_image], "pixels": int(len(lit_lab)), "gamut": lit_gamut,
                "palette": lit_palette, "accents": lit_accents, "profile": profile(per_image),
                "per_image": [{k: (round(v, 3) if isinstance(v, float) else v) for k, v in m.items()} for m in per_image],
                "leave_one_out": {"inside_share": coverage, "median": round(float(np.median(coverage)), 2)}},
        "albedo": {"defined_by": defining, "approved_only": bool(approved), "colours": int(len(albedo_lab)),
                   "gamut": albedo_gamut, "palette": albedo_palette, "accents": albedo_accents},
    }
    save_style(style)
    board(style, [image for _, image in sources])
    p = style["measured"]["lit"]["profile"]
    print(f"lit: lightness {lit_gamut['lightness']}, chroma max {lit_gamut['chroma_max']}; value median "
          f"{p['lightness_median']['median']}, contrast {p['contrast_rms']['median']}, fourier slope {p['fourier_slope']['median']}, "
          f"harmony {p['harmony']}")
    print(f"albedo: lightness {albedo_gamut['lightness']}, chroma max {albedo_gamut['chroma_max']} from {len(defining)} models "
          f"({'approved' if approved else 'all built, none approved yet'})")
    print(f"leave-one-out: median {style['measured']['lit']['leave_one_out']['median']} of an image's measures inside the "
          f"others' band; lowest {min(coverage)} ({per_image[int(np.argmin(coverage))]['source']})")


def board(style, thumbs):
    width, sw = 1600, 98
    rows = (len(thumbs) + 7) // 8
    image = Image.new("RGB", (width, 330 + rows * 200 + 20), (243, 236, 223))
    draw = ImageDraw.Draw(image)
    font = _font(14)
    for row, (kind, label) in enumerate((("lit", "On screen (the sources, lit)"), ("albedo", "Base colours (the style's models)"))):
        m = style["measured"][kind]
        g = m["gamut"]
        y = 8 + row * 160
        draw.text((10, y), f"{label}: lightness {g['lightness'][0]}-{g['lightness'][1]}, chroma at most {g['chroma_max']} "
                           f"(median {g['chroma_median']})", fill=(40, 36, 30), font=font)
        for i, entry in enumerate(m["palette"]):
            x = 10 + i * sw
            draw.rectangle([x, y + 24, x + sw - 8, y + 100], fill=entry["hex"])
            draw.text((x, y + 104), f"{entry['hex']} {entry['share'] * 100:.0f}%", fill=(40, 36, 30), font=font)
    for i, thumb in enumerate(thumbs):
        t = thumb.copy()
        t.thumbnail((190, 190))
        image.paste(t, (10 + (i % 8) * 198, 330 + (i // 8) * 200))
    os.makedirs(OUT, exist_ok=True)
    image.save(BOARD)


def check(model, style):
    """One model's base colours against the albedo envelope: (problems, nearest palette distance, atlas stats)."""
    if style.get("schema", 2) >= 3:
        # A lit screenshot does not define unlit albedo limits. Document revisions also do not invalidate meshes.
        return [], None, None
    g = style["measured"]["albedo"]["gamut"]
    lo, hi, cmax = g["lightness"][0], g["lightness"][1], g["chroma_max"]
    palette = np.array([p["lab"] for p in style["measured"]["albedo"]["palette"]])
    by_hue = g.get("chroma_by_hue")
    s = spec(model)
    problems, nearest = [], []
    for surface, lab in surface_colours(s):
        nearest.append(float(np.min(np.linalg.norm(palette - lab, axis=1))))
        out = []
        if chroma(lab) > cmax:
            out.append(f"chroma {chroma(lab):.0f} > {cmax}")
        elif by_hue and chroma(lab) >= HUE_MIN_CHROMA and chroma(lab) > by_hue[int(hue_bin(lab))]:
            out.append(f"chroma {chroma(lab):.0f} > {by_hue[int(hue_bin(lab))]}, the style's ceiling for this hue "
                       f"({int(hue_bin(lab)) * 30} deg)")
        if not lo <= lab[0] <= hi:
            out.append(f"lightness {lab[0]:.0f} outside {lo}-{hi}")
        if out and "style exception" not in surface.get("note", "").lower():
            problems.append(f"{surface['name']}: {', '.join(out)}")
    atlas = None
    a = atlas_pixels(s, 20000, np.random.default_rng(1))
    if a is not None:
        lab = srgb_to_lab(a)
        atlas = {"chroma_p99": round(float(np.percentile(chroma(lab), 99)), 1),
                 "lightness_p1_p99": [round(float(v), 1) for v in np.percentile(lab[:, 0], (1, 99))]}
        if atlas["chroma_p99"] > cmax:
            problems.append(f"atlas: 1 % of its pixels above chroma {atlas['chroma_p99']} > {cmax}")
        if atlas["lightness_p1_p99"][0] < lo or atlas["lightness_p1_p99"][1] > hi:
            problems.append(f"atlas: lightness {atlas['lightness_p1_p99']} outside {lo}-{hi}")
        if by_hue:
            c, b = chroma(lab), hue_bin(lab)
            for family in range(12):
                hit = c[(b == family) & (c >= HUE_MIN_CHROMA)]
                if len(hit) >= 200 and float(np.percentile(hit, 99)) > by_hue[family]:
                    problems.append(f"atlas: chroma {np.percentile(hit, 99):.0f} at {family * 30} deg, above the style's "
                                    f"{by_hue[family]} for this hue")
    version = s.get("style_version")
    if version and version != style.get("style_version"):
        problems.append(f"built for style {version}, the style is now {style.get('style_version')}: rebuild and re-check")
    return problems, (round(max(nearest), 1) if nearest else None), atlas



def model_class(model, style):
    return next((c for c, models in style.get("classes", {}).items() if model in models), None)


def rules_check(model, style):
    """The style's measurable rules beyond colour: (problems, warnings, info). Each finding belongs to a rule, and the
    rule's "enforce" decides whether it fails the run ("error") or is reported ("warn"). A new gate starts as "warn" and
    becomes "error" once its near misses prove it and the user has decided what to do with the assets it flags (the
    research report, step 6).
    - finish.real_material: the finish by kind (style.finish_by_kind): a Rubber surface "glossy", a Glass one "matte" ...;
    - detail.budgets: upper triangle limits only; a low polygon count is not a style defect;
    - edges.chamfer: for the classes in chamfer_applies_to, a hard part without a chamfer or outside stylisation.chamfer_mm."""
    s = spec(model)
    found = []   # (rule id, message)
    allowed = style.get("finish_by_kind", {})
    for surface in s.get("surfaces", []):
        if not HEX.match(surface["name"]) or "style exception" in surface.get("note", "").lower():
            continue
        kind, finish = surface["name"].split("_")[0], surface["name"].split("_")[-1]
        if kind in allowed and finish not in allowed[kind]:
            found.append(("finish.real_material",
                          f"{surface['name']}: a {kind} surface is {finish}; the style allows {', '.join(allowed[kind])}"))
    cls = model_class(model, style)
    st = style.get("stylisation", {})
    budget = st.get("budgets_tris", {}).get(cls)
    tris = sum(o.get("triangles", 0) for o in s.get("objects", []))
    warnings = []
    if budget:
        if tris > budget[1]:
            found.append(("detail.budgets", f"{tris:,} triangles, above the {cls} budget {budget[1]:,}"))
    if cls in st.get("chamfer_applies_to", []):
        band = st.get("chamfer_mm", {}).get("hard_parts")
        skip = {o["name"] for o in s.get("objects", []) if o.get("foliage")} | set(s.get("unchamfered") or {})
        for part, width in (s.get("bevels_mm") or {}).items():
            if part in skip:   # leaves, and parts the generator leaves unchamfered on purpose (part(no_chamfer=why))
                continue
            if width is None and band:
                found.append(("edges.chamfer", f"{part}: no chamfer (hard parts of a {cls} get {band[0]}-{band[1]} mm)"))
            elif band and not band[0] <= width <= band[1]:
                found.append(("edges.chamfer", f"{part}: chamfer {width} mm outside {band[0]}-{band[1]} mm"))
    rules = {r["id"]: r for r in style.get("rules", []) if r.get("status") != "retired"}
    found = [(rule, text) for rule, text in found if rule in rules]
    enforce = {rid: ("error" if r.get("origin") == "technical" and r.get("status") == "active"
                    and r.get("enforce") == "error" else "warn")
               if style.get("schema", 2) >= 3 else r.get("enforce", "error") for rid, r in rules.items()}
    problems = [f"{rule}: {text}" for rule, text in found if enforce.get(rule, "error") == "error"]
    warnings += [f"{rule}: {text}" for rule, text in found if enforce.get(rule, "error") == "warn"]
    return problems, warnings, {"class": cls, "triangles": tris}

def report(models, as_json):
    style = load_style()
    problems = validate(style)
    if problems:
        print("style.json: " + "; ".join(problems))
        return 1
    guide = style.get("schema", 2) >= 3
    if not guide and "albedo" not in style.get("measured", {}):
        sys.exit("no measured style: run py -3 tools/Style.py extract first")
    results = {}
    for model in [m for m in built_models() if not models or m in models]:
        if model in style.get("exempt", {}):
            if not as_json:
                print(f"{model:12} exempt   {style['exempt'][model]}")
            continue
        problems, distance, atlas = check(model, style)
        rule_problems, warnings, info = rules_check(model, style)
        results[model] = {"outside": problems + rule_problems, "warnings": warnings, "palette_distance_max": distance,
                          "atlas": atlas, **info}
        if guide:
            import Review
            original_root = Review.ROOT
            try:
                Review.ROOT = ROOT
                results[model]["asset_review"] = Review.state(model)
            finally:
                Review.ROOT = original_root
            results[model]["visual_style"] = "not assessed by numeric checks"
    if as_json:
        print(json.dumps(results))
    else:
        for model, r in results.items():
            if guide:
                print(f"{model:12} {'ok' if not r['outside'] else 'FAIL'}  technical rules; "
                      f"asset review: {r['asset_review']['text']}; visual style: {r['visual_style']}"
                      + "".join(f"\n    {o}" for o in r["outside"])
                      + "".join(f"\n    warning: {w}" for w in r["warnings"]))
                continue
            print(f"{model:12} {'ok' if not r['outside'] else 'OUTSIDE'}  farthest surface from the palette "
                  f"{r['palette_distance_max']}" + (f", atlas chroma p99 {r['atlas']['chroma_p99']}" if r["atlas"] else "")
                  + "".join(f"\n    {o}" for o in r["outside"]) + "".join(f"\n    warning: {w}" for w in r["warnings"]))
    return 1 if any(r["outside"] for r in results.values()) else 0



PAIR_SIDE = 480        # px: a pair's two frames are measured at this size, with the same filter (same settings)
PAIR_TOLERANCE = 0.5   # of the sources' p10-p90 width: how far a game shot may sit from its reference on one axis


def _pair_image(path):
    image = Image.open(path).convert("RGB")
    image.thumbnail((PAIR_SIDE, PAIR_SIDE), Image.BOX)
    return image


def pair_check(game, reference, prof):
    """One game shot against the reference frame it recreates, on every scalar axis: the difference, and whether it is
    beyond PAIR_TOLERANCE of the sources' own spread on that axis (the spread is what the style allows between images)."""
    g, r = measure(game), measure(reference)
    axes, outside = {}, []
    for key in SCALARS:
        width = prof[key]["p90"] - prof[key]["p10"]
        tolerance = max(width * PAIR_TOLERANCE, 1e-3)
        delta = g[key] - r[key]
        axes[key] = {"reference": round(r[key], 3), "game": round(g[key], 3), "delta": round(delta, 3),
                     "tolerance": round(tolerance, 3)}
        if abs(delta) > tolerance:
            outside.append(key)
    return axes, outside, g, r


def brief(name, cls=None):
    """The brief a new asset starts from (the research report, step 8): the style in one page, for its class."""
    style = load_style()
    if style.get("schema", 2) >= 3:
        import StyleGuide
        result = StyleGuide.brief(ROOT, name, cls=cls)
        print(f"brief: {result['path']}")
        return result
    cls = cls or "prop"
    st = style["stylisation"]
    albedo = style["measured"]["albedo"]
    floor = st["detail_floor_m"].get(cls, st["detail_floor_m"].get("prop"))
    lines = [f"# Brief: {name} ({cls})", "", f"**The style:** {style['summary']}", "",
             f"- Anchor: {st['anchor']}", f"- Tie-breaker: {st['tie_breaker']}",
             f"- Detail floor for a {cls}: {floor} m; smaller features are painted or left out",
             f"- Triangle budget: {st['budgets_tris'].get(cls, '(no budget for this class: add one)')}",
             f"- Chamfers on hard parts: {st['chamfer_mm']['hard_parts']} mm" + (" (checked)" if cls in st.get("chamfer_applies_to", []) else ""),
             f"- Base colours inside lightness {albedo['gamut']['lightness']} and chroma at most {albedo['gamut']['chroma_max']}; "
             f"palette: {' '.join(p['hex'] for p in albedo['palette'])}",
             "- Legacy chroma ceiling per CIELAB hue: " + ", ".join(f"{i * 30} deg {v:g}" for i, v in enumerate(albedo["gamut"].get("chroma_by_hue", []))),
             "- Finishes by kind: " + "; ".join(f"{k} {'/'.join(v)}" for k, v in style.get("finish_by_kind", {}).items()), "",
             "**Rules** (id, kind: what; checked by):", ""]
    lines += [f"- `{r['id']}` ({r['kind']}): {r['text']}; checked by {r['check']}" for r in style["rules"]]
    lines += ["", "Legacy schema-2 brief. Migrate the project to schema 3 for source-backed, scoped guidance. "
              "Technical checks and visual approval remain separate."]
    path = os.path.join(OUT, "briefs", name + ".md")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print(f"brief: {os.path.relpath(path, ROOT)}")

# ----------------------------------------------------------------------------------------------------------------------
# Art bible
# ----------------------------------------------------------------------------------------------------------------------

def _font(size):
    try:
        return ImageFont.truetype("arial.ttf", size)
    except OSError:
        return ImageFont.load_default()


def bible():
    """Build the synchronized AI guide and human visual report."""
    import StyleGuide
    result = StyleGuide.build(ROOT)
    print("Guideline artifacts: " + json.dumps(result, ensure_ascii=False))
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("action", choices=("extract", "report", "validate", "bible", "brief"))
    parser.add_argument("args", nargs="*")
    parser.add_argument("--json", action="store_true")
    parser.add_argument("--class", dest="cls", default=None)
    a = parser.parse_args()
    if a.action == "brief":
        if not a.args:
            sys.exit("brief NAME --class CLASS")
        try:
            brief(a.args[0], a.cls)
        except (ValueError, OSError) as exc:
            parser.exit(1, f"Style brief: {exc}\n")
        return
    if a.action == "extract":
        extract(a.args)
    elif a.action == "report":
        sys.exit(report(a.args, a.json))
    elif a.action == "validate":
        problems = validate(load_style())
        print("style.json: " + ("ok" if not problems else "; ".join(problems)))
        sys.exit(1 if problems else 0)
    else:
        bible()


if __name__ == "__main__":
    main()
