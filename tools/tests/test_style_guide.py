"""Reference provenance, bounded context and incremental updates, without Blender/Unity."""
import copy
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import StyleGuide as G


class GuideTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        image = self.root / 'refs/source.png'
        image.parent.mkdir(parents=True)
        Image.new('RGB', (100, 80), '#648255').save(image)
        self.refs = {'schema': 1, 'references': [
            {'id': 'r_shape', 'path': 'refs/source.png', 'sha256': G.file_hash(image), 'crop': [0, 0, 90, 70],
             'roles': ['shape'], 'scope': ['*'], 'label': 'Biçim', 'group': 'source-a',
             'observation': 'Büyük, okunur şekiller.', 'limitations': 'Gizli yüzeyler görünmez.'},
            {'id': 'r_wood', 'path': 'refs/source.png', 'sha256': G.file_hash(image), 'crop': [10, 10, 50, 50],
             'roles': ['material'], 'scope': ['building'], 'label': 'Ahşap', 'group': 'source-b',
             'observation': 'Geniş doku değişimi.', 'limitations': 'Işık taban renge karışır.'}]}
        self.style = {'schema': 3, 'style_version': '1.1', 'name': 'Test bahçesi',
                      'summary': 'Sade, okunabilir bahçe objeleri.', 'stylisation': {},
                      'classes': {'machine': ['mower'], 'building': ['house'], 'tree': ['tree']},
                      'rules': [self.rule('shape.read', 'shape', ['*'], 'r_shape'),
                                self.rule('material.wood', 'material', ['building'], 'r_wood')],
                      'guideline': {'revision': 1, 'status': 'draft', 'overview': ['Hedef görünüm.'],
                                    'uncertainties': ['Görünmeyen yüzeylerin görünümü.'],
                                    'examples': [], 'history': [],
                                    'cost': {'max_reference_images': 2, 'max_brief_chars': 7000}}}
        self.save()

    @staticmethod
    def rule(rid, aspect, scope, ref):
        return {'id': rid, 'title': rid, 'text': 'Ana biçimi okunur tut.', 'scope': scope,
                'aspect': aspect, 'origin': 'observation', 'status': 'draft', 'confidence': 'medium',
                'kind': 'tunable', 'enforce': 'warn', 'check': 'visual review', 'materials': [],
                'evidence': [{'reference': ref, 'aspect': aspect}], 'rationale': 'Görsel kanıt.',
                'instructions': ['İşlevsel biçimi açıkça göster.'], 'avoid': ['Eşit yoğunlukta küçük detay.']}

    def save(self):
        G.write_json(self.root / G.STYLE_PATH, self.style)
        G.write_json(self.root / G.REF_PATH, self.refs)

    def test_palette_only_evidence_cannot_drive_shape_even_as_interpretation(self):
        rule = self.style['rules'][0]
        self.refs['references'][0]['roles'] = ['colour']
        self.assertIn('not intended for shape', ';'.join(G.validate_data(self.style, self.refs)))
        rule['origin'] = 'interpretation'
        rule['evidence'][0]['aspect'] = 'colour'
        self.assertIn('evidence aspect differs', ';'.join(G.validate_data(self.style, self.refs)))

    def test_changed_source_requires_inspection_instead_of_silent_reuse(self):
        Image.new('RGB', (100, 80), '#dd2211').save(self.root / 'refs/source.png')
        errors = G.validate_project(self.root)
        self.assertIn('source changed', ';'.join(errors))
        with self.assertRaises(ValueError):
            G.brief(self.root, 'mower')

    def test_crop_must_stay_inside_source(self):
        self.refs['references'][0]['crop'] = [90, 0, 50, 40]
        self.assertIn('crop exceeds image', ';'.join(G.validate_data(self.style, self.refs, self.root)))

    def test_reference_paths_cannot_escape_project(self):
        self.refs['references'][0]['path'] = '../outside.png'
        self.assertIn('Path must stay', ';'.join(G.validate_data(self.style, self.refs, self.root)))

    def test_brief_selects_class_rules_and_bounded_images(self):
        result = G.brief(self.root, 'mower')
        self.assertEqual(result['rule_ids'], ['shape.read'])
        self.assertEqual(result['reference_ids'], ['r_shape'])
        self.assertEqual(result['stats']['image_count'], 1)
        self.assertNotIn('material.wood', result['text'])
        self.assertLessEqual(len(result['text']), 7000)
        self.assertTrue(Path(result['board']).exists())
        self.assertEqual(G.brief(self.root, 'house')['rule_ids'], ['shape.read', 'material.wood'])

    def test_material_filter_preserves_common_rules(self):
        self.style['rules'][1]['materials'] = ['Wood']
        self.save()
        self.assertEqual(G.brief(self.root, 'house', materials=['Metal'])['rule_ids'], ['shape.read'])
        self.assertEqual(len(G.brief(self.root, 'house', materials=['Wood'])['rule_ids']), 2)

    def test_unknown_model_requires_class_and_excess_context_fails_explicitly(self):
        with self.assertRaises(ValueError):
            G.brief(self.root, 'new_pump')
        self.assertEqual(G.brief(self.root, 'new_pump', 'machine')['rule_ids'], ['shape.read'])
        self.style['rules'][0]['text'] = 'x' * 7200
        self.save()
        with self.assertRaisesRegex(ValueError, 'budget'):
            G.brief(self.root, 'mower')

    def test_build_has_no_process_calls_and_unchanged_outputs_keep_mtimes(self):
        with patch('subprocess.Popen', side_effect=AssertionError('No renderer, engine or model call')):
            first = G.build(self.root)
            report = Path(first['files']['report'])
            stamp = report.stat().st_mtime_ns
            second = G.build(self.root)
        self.assertTrue(first['changed'])
        self.assertFalse(second['changed'])
        self.assertEqual(stamp, report.stat().st_mtime_ns)
        for rule in self.style['rules']:
            self.assertIn(rule['id'], report.read_text(encoding='utf-8'))
            self.assertIn(rule['id'], Path(first['files']['guide']).read_text(encoding='utf-8'))
        self.assertEqual(first['ai_calls'], 0)

    def test_editorial_changes_do_not_invalidate_model_rules(self):
        before = G.snapshot_data(self.style, self.refs)
        self.style['guideline']['revision'] += 1
        self.style['rules'][0]['rationale'] = 'Daha anlaşılır açıklama.'
        self.style['rules'][0]['title'] = 'Yeni başlık'
        result = G.impact_data(before, G.snapshot_data(self.style, self.refs))
        self.assertEqual(result['changed_rules'], [])
        self.assertEqual(result['affected_models'], [])
        self.assertEqual(result['automatic_builds'], 0)

    def test_targeted_feedback_changes_only_relevant_models_and_preserves_old_detail(self):
        source_hash = G.file_hash(self.root / G.REF_PATH)
        before_shape = copy.deepcopy(self.style['rules'][0])
        with patch('subprocess.Popen', side_effect=AssertionError('No build')):
            result = G.revise(self.root, 'material.wood', 'Ahşapta geniş renk farkları kullan.',
                              'Kullanıcı ahşabın daha sakin görünmesini istedi.')
        self.assertEqual(result['affected_models'], ['house'])
        self.assertEqual(result['changed_rules'], ['material.wood'])
        current, _ = G.load(self.root)
        self.assertEqual(current['rules'][0], before_shape)
        self.assertEqual(current['rules'][1]['origin'], 'user_decision')
        self.assertIn('previous_guidance', current['rules'][1])
        self.assertEqual(current['rules'][1]['evidence'], [])
        self.assertEqual(current['rules'][1]['previous_guidance']['evidence'][0]['reference'], 'r_wood')
        self.assertEqual(current['style_version'], '1.1')
        self.assertEqual(source_hash, G.file_hash(self.root / G.REF_PATH))

    def test_rule_removal_and_scope_change_include_old_scope(self):
        before = G.snapshot_data(self.style, self.refs)
        self.style['rules'][1]['scope'] = ['tree']
        changed = G.impact_data(before, G.snapshot_data(self.style, self.refs))
        self.assertEqual(changed['affected_models'], ['house', 'tree'])
        self.style['rules'][1]['status'] = 'retired'
        removed = G.impact_data(before, G.snapshot_data(self.style, self.refs))
        self.assertEqual(removed['changed_rules'], ['material.wood'])

    def test_technical_budget_change_is_not_mistaken_for_editorial_change(self):
        rule = self.style['rules'][1]
        rule.update(id='detail.budgets', origin='technical', decision='Existing authored budget', evidence=[])
        self.style['stylisation']['budgets_tris'] = {'building': [0, 1000]}
        before = G.snapshot_data(self.style, self.refs)
        self.style['stylisation']['budgets_tris']['building'][1] = 2000
        self.assertEqual(G.impact_data(before, G.snapshot_data(self.style, self.refs))['changed_rules'], ['detail.budgets'])

    def example(self, status='baseline'):
        G.write_json(self.root / 'Assets/Opus/ModelSpecs/mower.json', {'asset': 'asset-v1', 'structure': 's1'})
        return {'id': 'mower', 'model': 'mower', 'asset': 'asset-v1', 'image': 'refs/source.png',
                'sha256': G.file_hash(self.root / 'refs/source.png'), 'scope': ['machine'], 'status': status, 'note': 'Example'}

    def test_baseline_never_auto_promotes_when_model_is_approved(self):
        example = self.example()
        G.write_json(self.root / 'tools/blender/approvals.json', {'mower': {'asset': 'asset-v1'}})
        self.assertEqual(G.example_state(self.root, example), 'baseline')

    def test_stale_approval_and_changed_example_image_cannot_define_style(self):
        example = self.example('approved')
        G.write_json(self.root / 'tools/blender/approvals.json', {'mower': {'asset': 'old'}})
        self.assertEqual(G.example_state(self.root, example), 'outdated')
        example['sha256'] = '0' * 64
        self.assertEqual(G.example_state(self.root, example), 'unavailable')

    def test_measurements_do_not_mutate_rules_or_create_albedo_target(self):
        before = G.file_hash(self.root / G.STYLE_PATH)
        measured = G.measure(self.root)
        self.assertEqual(before, G.file_hash(self.root / G.STYLE_PATH))
        self.assertEqual(len(measured['images']), 2)
        self.assertNotIn('shadow_shift_a', measured['images'][0]['values'])
        self.assertNotIn('albedo', measured)
        with patch('Style.measure', side_effect=AssertionError('Expected cached image evidence')):
            self.assertEqual(G.measure(self.root), measured)

    def test_new_reference_is_pending_evidence_not_an_automatic_rule_change(self):
        before = G.file_hash(self.root / G.STYLE_PATH)
        result = G.add_reference(self.root, self.root / 'refs/source.png', 'new_palette', ['colour'], ['*'])
        self.assertEqual(result['rules_changed'], 0)
        self.assertEqual(before, G.file_hash(self.root / G.STYLE_PATH))
        self.assertTrue(result['needs_visual_interpretation'])


if __name__ == '__main__':
    unittest.main()
