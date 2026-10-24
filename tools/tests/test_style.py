"""Current style checks: authored colour roles, technical policy and per-image evidence."""
import copy
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest import mock

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import Style as S


def role(**changes):
    value = {"id": "rubber.dark", "scope": ["machine"], "materials": ["Rubber"],
             "palette": ["#303030"], "tolerance_delta_e": 12, "status": "draft",
             "rationale": "Authored working target; not recovered screenshot albedo."}
    value.update(changes)
    return value


RULES = {"schema": 3, "classes": {"machine": ["m"]},
         "finish_by_kind": {"Rubber": ["matte"], "Plastic": ["matte", "satin", "glossy"]},
         "stylisation": {"budgets_tris": {"machine": [100, 1000]},
                         "bevel_policy": {"require_on_classes": ["machine"], "require_positive_width": True}},
         "rules": [{"id": rid, "origin": "technical", "status": "draft", "enforce": "warn"}
                   for rid in ("finish.real_material", "detail.budgets", "edges.chamfer")]}


class MeasurementTests(unittest.TestCase):
    def test_known_colours(self):
        np.testing.assert_allclose(S.srgb_to_lab([255, 255, 255]), [100.0, 0.0, 0.0], atol=0.05)
        np.testing.assert_allclose(S.srgb_to_lab([255, 0, 0]), [53.24, 80.09, 67.20], atol=0.05)
        self.assertEqual(list(S.lab_to_srgb(S.srgb_to_lab([194, 31, 34]))), [194, 31, 34])

    def test_two_opposite_hues_fit_a_two_sector_template(self):
        image = np.zeros((64, 64, 3), dtype=float)
        image[:, :32], image[:, 32:] = (200, 40, 40), (40, 180, 190)
        result = S.harmony(image)
        self.assertIn(result["template"], ("I", "X", "Y", "T"))
        self.assertLess(result["fit_deg"], 2.0)

    def test_noise_has_more_fine_detail_than_a_smooth_gradient(self):
        noise = np.random.default_rng(0).normal(50, 20, (128, 128))
        y, x = np.mgrid[0:128, 0:128]
        smooth = 50 + 30 * np.sin(x / 20.0) * np.cos(y / 25.0)
        self.assertLess(S.fourier_slope(noise), 0.5)
        self.assertGreater(S.fourier_slope(smooth), 1.5)

    def test_per_image_measurements_are_descriptive_and_finite(self):
        image = Image.fromarray(np.random.default_rng(1).integers(0, 256, (80, 120, 3), dtype=np.uint8))
        result = S.measure(image)
        for field in ("lightness_median", "chroma_median", "contrast_rms", "edge_density", "busyness", "fourier_slope"):
            self.assertTrue(np.isfinite(result[field]))
        self.assertAlmostEqual(sum(result[k] for k in ("notan_dark", "notan_mid", "notan_light")), 1.0, places=6)


class ProjectTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        patch = mock.patch.object(S, "ROOT", str(self.root))
        patch.start()
        self.addCleanup(patch.stop)
        self.style = copy.deepcopy(RULES)

    def model(self, surfaces=(), triangles=500, bevels=None, unchamfered=None, foliage=False):
        path = self.root / "Assets/Opus/ModelSpecs/m.json"
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps({"surfaces": list(surfaces),
                                  "objects": [{"name": "Body", "triangles": triangles, "foliage": foliage}],
                                  "bevels_mm": bevels or {}, "unchamfered": unchamfered or {}}), encoding="utf-8")

    def test_authored_role_flags_changed_colour_without_changing_target(self):
        self.style["colour_roles"] = [role()]
        self.model([{"name": "Rubber_303030_matte"}])
        match = S.colour_check("m", self.style)
        self.assertEqual(match["coverage"], "complete")
        self.assertTrue(match["checked"][0]["within_target"])
        self.model([{"name": "Rubber_00FF00_matte"}])
        mismatch = S.colour_check("m", self.style)
        self.assertEqual(mismatch["status"], "advisory")
        self.assertFalse(mismatch["checked"][0]["within_target"])
        self.assertTrue(mismatch["warnings"])
        self.assertEqual(self.style["colour_roles"][0]["palette"], ["#303030"])

    def test_model_note_and_material_selectors_are_independent_of_hex(self):
        self.style["colour_roles"] = [role(id="paint.red", materials=["Metal"], models=["m"],
                                                note_contains=["Housing:", "Rear flap:"], palette=["#C21F22"])]
        self.model([{"name": "Metal_00FF00_glossy", "note": "Housing: painted cover"},
                    {"name": "Metal_808080_satin", "note": "Unpainted handle"}])
        result = S.colour_check("m", self.style)
        self.assertEqual(len(result["checked"]), 1)
        self.assertFalse(result["checked"][0]["within_target"])
        self.assertEqual(result["coverage"], "partial")

    def test_unknown_texture_colours_and_unmatched_materials_stay_unassessed(self):
        self.style["colour_roles"] = [role()]
        self.model([{"name": "Source_black_matte", "style_role": "rubber.dark"},
                    {"name": "Metal_303030_matte"}])
        result = S.colour_check("m", self.style)
        self.assertEqual(result["status"], "unassessed")
        self.assertEqual(result["checked"], [])
        self.assertEqual(len(result["unassessed"]), 2)
        self.model([{"name": "Source_black_matte", "style_role": "rubber.dark", "base_colour": "#303030"}])
        self.assertTrue(S.colour_check("m", self.style)["checked"][0]["within_target"])

    def test_ambiguous_roles_require_explicit_assignment(self):
        self.style["colour_roles"] = [role(), role(id="rubber.light", palette=["#BBBBBB"])]
        self.model([{"name": "Rubber_303030_matte"}])
        self.assertEqual(S.colour_check("m", self.style)["checked"], [])
        self.model([{"name": "Rubber_303030_matte", "style_role": "rubber.dark"}])
        self.assertEqual(S.colour_check("m", self.style)["checked"][0]["role"], "rubber.dark")

    def test_scope_and_documented_exceptions_do_not_report_a_false_pass(self):
        self.style["colour_roles"] = [role(scope=["building"])]
        self.model([{"name": "Rubber_303030_matte"}])
        self.assertEqual(S.colour_check("m", self.style)["status"], "not_configured")
        self.style["colour_roles"][0]["scope"] = ["machine"]
        self.model([{"name": "Rubber_00FF00_matte", "note": "Style exception: luminous locator"}])
        result = S.colour_check("m", self.style)
        self.assertEqual(result["checked"], [])
        self.assertIn("exception", result["unassessed"][0]["reason"])

    def test_invalid_colour_target_cannot_silently_pass(self):
        for bad in (role(palette=["green"]), role(tolerance_delta_e=float("nan")), role(status="active")):
            with self.subTest(target=bad):
                self.assertTrue(S.validate_colour_roles({"colour_roles": [bad]}))
                with self.assertRaises(ValueError):
                    S.colour_check("m", {"colour_roles": [bad]})

    def test_finish_uses_actual_finish_and_reports_unknown_material_kind(self):
        self.model([{"name": "Rubber_303030_matte", "finish": "glossy"},
                    {"name": "Source_unknown_matte", "finish": "matte"}])
        problems, warnings, info = S.rules_check("m", self.style)
        self.assertEqual(problems, [])
        self.assertIn("finish.real_material", warnings[0])
        self.assertEqual(info["finish_unclassified"], ["Source_unknown_matte"])

    def test_positive_bevels_have_no_universal_width_band(self):
        self.model(bevels={"SmallCap": 0.8, "LargeBody": 4.0})
        self.assertEqual(S.rules_check("m", self.style)[:2], ([], []))

    def test_missing_and_invalid_bevels_follow_explicit_policy_and_exemptions(self):
        self.model(bevels={"Body": None, "Cap": 0, "Lip": float("nan"), "Cord": None},
                   unchamfered={"Cord": "round cord needs no bevel"})
        warnings = S.rules_check("m", self.style)[1]
        self.assertEqual(len(warnings), 3)
        self.model(bevels={"Body": None}, foliage=True)
        self.assertEqual(S.rules_check("m", self.style)[:2], ([], []))
        self.model(bevels={"Body": None})
        self.style["stylisation"].pop("bevel_policy")
        self.assertEqual(S.rules_check("m", self.style)[:2], ([], []))

    def test_draft_limits_warn_active_technical_limits_can_fail_and_retired_rules_do_not_apply(self):
        self.model(triangles=1500)
        budget = self.style["rules"][1]
        budget["enforce"] = "error"
        self.assertEqual(S.rules_check("m", self.style)[0], [])
        self.assertTrue(S.rules_check("m", self.style)[1])
        budget["status"] = "active"
        self.assertTrue(S.rules_check("m", self.style)[0])
        budget["status"] = "retired"
        self.assertEqual(S.rules_check("m", self.style)[:2], ([], []))

    def test_low_triangle_count_is_not_a_style_defect(self):
        self.model(triangles=10)
        self.assertEqual(S.rules_check("m", self.style)[:2], ([], []))

    def test_current_commands_delegate_and_legacy_schema_is_rejected(self):
        import StyleGuide
        with mock.patch.object(StyleGuide, "measure", return_value={"images": []}) as measure, \
                mock.patch.object(StyleGuide, "brief", return_value={"path": "brief.md"}) as brief, \
                mock.patch.object(StyleGuide, "validate_project", return_value=[]) as validate:
            S.extract(["source.png"])
            S.brief("m", "machine")
            S.validate({"schema": 3})
            measure.assert_called_once_with(str(self.root), ["source.png"])
            brief.assert_called_once_with(str(self.root), "m", cls="machine")
            validate.assert_called_once_with(str(self.root))
        self.assertTrue(S.validate({"schema": 2}))

    def test_report_separates_advisory_colour_coverage_from_asset_and_visual_approval(self):
        import Review
        self.style["colour_roles"] = [role()]
        self.model([{"name": "Rubber_00FF00_matte"}, {"name": "Source_bark_matte"}])
        output = io.StringIO()
        with mock.patch.object(S, "load_style", return_value=self.style), \
                mock.patch.object(S, "validate", return_value=[]), \
                mock.patch.object(Review, "state", return_value={"text": "approved", "clean": True}), \
                mock.patch("sys.stdout", output):
            self.assertEqual(S.report(["m"], True), 0)
        row = json.loads(output.getvalue())["m"]
        self.assertEqual(row["asset_review"]["text"], "approved")
        self.assertEqual(row["visual_style"], "not assessed by numeric checks")
        self.assertEqual(row["colour_check"]["coverage"], "partial")
        self.assertTrue(row["warnings"])


if __name__ == "__main__":
    unittest.main()
