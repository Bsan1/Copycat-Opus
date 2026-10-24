"""Current reference-backed 3D style checks and informational image measurements.

Uses schema 3. Technical checks and authored colour targets do not certify visual style.
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
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
STYLE = os.path.join(ROOT, "tools", "blender", "style", "style.json")
DETAIL_SIZE = 256
HEX = re.compile(r"^[A-Za-z]+_([0-9A-Fa-f]{6})(?:_x[0-9.]+)?_[a-z]+$")
HEX_COLOUR = re.compile(r"^#[0-9A-Fa-f]{6}$")
TEMPLATES = {"i": [(0, 18)], "V": [(0, 93.6)], "L": [(0, 18), (90, 79.2)], "I": [(0, 18), (180, 18)],
             "T": [(0, 180)], "Y": [(0, 93.6), (180, 18)], "X": [(0, 93.6), (180, 93.6)]}


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


def spec(model):
    with open(os.path.join(ROOT, "Assets", "Opus", "ModelSpecs", model + ".json"), encoding="utf-8") as f:
        return json.load(f)


def built_models():
    return sorted(os.path.basename(p)[:-5] for p in glob.glob(os.path.join(ROOT, "Assets", "Opus", "ModelSpecs", "*.json")))


def load_style():
    if not os.path.exists(STYLE):
        return {}
    with open(STYLE, encoding="utf-8") as f:
        return json.load(f)


def validate_colour_roles(style):
    """Targets must be explicit authored values, not implicit image-wide thresholds."""
    roles = style.get("colour_roles", [])
    if not isinstance(roles, list):
        return ["colour_roles must be a list"]
    errors, seen = [], set()
    for role in roles:
        if not isinstance(role, dict):
            errors.append("colour role must be an object")
            continue
        rid = role.get("id")
        if not isinstance(rid, str) or not rid or rid in seen:
            errors.append(f"colour role: invalid or duplicate id {rid!r}")
        seen.add(str(rid))
        for key in ("scope", "materials", "models", "note_contains"):
            values = role.get(key, [])
            if not isinstance(values, list) or any(not isinstance(v, str) or not v for v in values):
                errors.append(f"{rid}: {key} must be a list of nonempty strings")
        if not role.get("scope"):
            errors.append(f"{rid}: colour role scope required")
        palette = role.get("palette")
        if not isinstance(palette, list) or not palette or any(not isinstance(c, str) or not HEX_COLOUR.fullmatch(c) for c in palette):
            errors.append(f"{rid}: palette must contain #RRGGBB targets")
        tolerance = role.get("tolerance_delta_e")
        if isinstance(tolerance, bool) or not isinstance(tolerance, (int, float)) or not math.isfinite(tolerance) or tolerance <= 0:
            errors.append(f"{rid}: positive finite tolerance_delta_e required")
        if role.get("status") not in ("draft", "active") or not role.get("rationale"):
            errors.append(f"{rid}: target status and authored rationale required")
        if role.get("status") == "active" and not role.get("decision"):
            errors.append(f"{rid}: active colour target needs an explicit decision")
    return errors


def validate(style):
    if style.get("schema") != 3:
        return ["schema 3 required; migrate legacy style data before using this tool"]
    import StyleGuide
    return StyleGuide.validate_project(ROOT) + validate_colour_roles(style)


def extract(images):
    import StyleGuide
    result = StyleGuide.measure(ROOT, images or None)
    print("Measurements are informational per-image observations; guide rules were not changed.")
    return result


def model_class(model, style):
    return next((c for c, models in style.get("classes", {}).items() if model in models), None)


def colour_check(model, style):
    """Compare authored base colours to role targets; never infer albedo from a lit image.

    Explicit surface.style_role wins over selector inference. Unnamed/ambiguous roles and source
    textures without an authored base colour stay unassessed, even if other surfaces match.
    """
    errors = validate_colour_roles(style)
    if errors:
        raise ValueError("; ".join(errors))
    cls = model_class(model, style)
    roles = [r for r in style.get("colour_roles", [])
             if ("*" in r["scope"] or cls in r["scope"])
             and (not r.get("models") or model in r["models"])]
    result = {"status": "not_configured" if not roles else "unassessed", "coverage": "none",
              "method": "authored base-colour targets; CIELAB D65 Delta E 76; advisory",
              "checked": [], "unassessed": [], "warnings": []}
    for surface in spec(model).get("surfaces", []):
        name, note = surface.get("name", ""), surface.get("note", "")
        reason = None
        if "style exception" in note.lower():
            reason = "documented style exception: " + note
        kind = surface.get("kind") or name.split("_")[0]
        if surface.get("style_role"):
            candidates = [r for r in roles if r["id"] == surface["style_role"]]
        else:
            candidates = [r for r in roles if (not r.get("materials") or kind in r["materials"])
                          and (not r.get("note_contains") or any(t.lower() in note.lower() for t in r["note_contains"]))]
        match = HEX.match(name)
        colour = surface.get("base_colour") or ("#" + match.group(1) if match else None)
        if not reason and len(candidates) != 1:
            reason = "no applicable colour role" if not candidates else "ambiguous colour roles; assign surface.style_role"
        if not reason and (not isinstance(colour, str) or not HEX_COLOUR.fullmatch(colour)):
            reason = "no authored base colour in spec; inspect the source texture/render visually"
        if reason:
            result["unassessed"].append({"surface": name, "reason": reason})
            continue
        role = candidates[0]
        rgb = lambda c: [int(c[i:i + 2], 16) for i in (1, 3, 5)]
        lab = srgb_to_lab(rgb(colour))
        distances = [float(np.linalg.norm(lab - srgb_to_lab(rgb(c)))) for c in role["palette"]]
        index = int(np.argmin(distances))
        delta, target = distances[index], role["palette"][index]
        within = delta <= role["tolerance_delta_e"]
        result["checked"].append({"surface": name, "role": role["id"], "colour": colour.upper(),
                                  "nearest": target, "delta_e": round(delta, 2),
                                  "tolerance_delta_e": role["tolerance_delta_e"], "within_target": within,
                                  "target_status": role["status"]})
        if not within:
            result["warnings"].append(f"{role.get('rule', 'colour.role_palette')}: {name} ({role['id']}) "
                                      f"Delta E {delta:.1f} from {target}, above authored tolerance "
                                      f"{role['tolerance_delta_e']}; {role['status']} target, visual review required")
    if result["checked"]:
        result["status"] = "advisory"
        result["coverage"] = "partial" if result["unassessed"] else "complete"
    return result


def rules_check(model, style):
    """Technical finish/budget rules and explicit bevel presence policy. No universal bevel width band."""
    s = spec(model)
    found = []
    allowed = style.get("finish_by_kind", {})
    known_finishes = {finish for values in allowed.values() for finish in values}
    unclassified = []
    for surface in s.get("surfaces", []):
        name = surface.get("name", "")
        if "style exception" in surface.get("note", "").lower():
            continue
        kind = surface.get("kind") or name.split("_")[0]
        finish = surface.get("finish") or name.rsplit("_", 1)[-1]
        if kind in allowed and finish not in allowed[kind]:
            found.append(("finish.real_material", f"{name}: {kind} finish {finish}; use {', '.join(allowed[kind])}"))
        elif known_finishes and finish not in known_finishes:
            found.append(("finish.real_material", f"{name}: unknown finish {finish}"))
        elif kind not in allowed:
            unclassified.append(name)
    cls = model_class(model, style)
    st = style.get("stylisation", {})
    budget = st.get("budgets_tris", {}).get(cls)
    tris = sum(o.get("triangles", 0) for o in s.get("objects", []))
    if budget and tris > budget[1]:
        found.append(("detail.budgets", f"{tris:,} triangles, above the {cls} budget {budget[1]:,}"))
    policy = st.get("bevel_policy", {})
    required = cls in policy.get("require_on_classes", [])
    skip = {o["name"] for o in s.get("objects", []) if o.get("foliage")}
    skip.update(part for part, reason in (s.get("unchamfered") or {}).items() if str(reason).strip())
    for part, width in (s.get("bevels_mm") or {}).items():
        if part in skip:
            continue
        if width is None and required:
            found.append(("edges.chamfer", f"{part}: no edge treatment or documented no_chamfer reason; choose by part scale and reference"))
        elif width is not None and policy.get("require_positive_width") and (
                isinstance(width, bool) or not isinstance(width, (int, float)) or not math.isfinite(width) or width <= 0):
            found.append(("edges.chamfer", f"{part}: bevel width must be positive and finite, got {width}"))
    rules = {r["id"]: r for r in style.get("rules", []) if r.get("status") != "retired"}
    found = [(rule, text) for rule, text in found if rule in rules]
    enforce = {rid: ("error" if r.get("origin") == "technical" and r.get("status") == "active"
                    and r.get("enforce") == "error" else "warn") for rid, r in rules.items()}
    problems = [f"{rule}: {text}" for rule, text in found if enforce[rule] == "error"]
    warnings = [f"{rule}: {text}" for rule, text in found if enforce[rule] == "warn"]
    return problems, warnings, {"class": cls, "triangles": tris, "finish_unclassified": unclassified}


def report(models, as_json):
    style = load_style()
    problems = validate(style)
    if problems:
        print("style.json: " + "; ".join(problems))
        return 1
    results = {}
    for model in [m for m in built_models() if not models or m in models]:
        if model in style.get("exempt", {}):
            results[model] = {"exempt": style["exempt"][model]}
            continue
        problems, warnings, info = rules_check(model, style)
        colour = colour_check(model, style)
        results[model] = {"outside": problems, "warnings": warnings + colour["warnings"], "colour_check": colour, **info}
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
        for model, row in results.items():
            if "exempt" in row:
                print(f"{model:12} exempt   {row['exempt']}")
                continue
            colour = row["colour_check"]
            print(f"{model:12} {'ok' if not row['outside'] else 'FAIL'}  technical rules; "
                  f"asset review: {row['asset_review']['text']}; visual style: {row['visual_style']}"
                  f"\n    colour: {colour['status']}, coverage {colour['coverage']} "
                  f"({len(colour['checked'])} checked, {len(colour['unassessed'])} unassessed surfaces)"
                  + "".join(f"\n    {problem}" for problem in row["outside"])
                  + "".join(f"\n    warning: {warning}" for warning in row["warnings"]))
    return 1 if any(row.get("outside") for row in results.values()) else 0


def brief(name, cls=None):
    import StyleGuide
    result = StyleGuide.brief(ROOT, name, cls=cls)
    print(f"brief: {result['path']}")
    return result


def bible():
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
    try:
        if a.action == "brief":
            if len(a.args) != 1:
                parser.error("brief NAME --class CLASS")
            brief(a.args[0], a.cls)
        elif a.action == "extract":
            extract(a.args)
        elif a.action == "report":
            return report(a.args, a.json)
        elif a.action == "validate":
            problems = validate(load_style())
            print("style.json: " + ("ok" if not problems else "; ".join(problems)))
            return 1 if problems else 0
        else:
            bible()
    except (ValueError, OSError) as exc:
        parser.exit(1, f"Style {a.action}: {exc}\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
