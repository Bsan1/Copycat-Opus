"""Feedback keeps useful guidance and its full history while flagging stale evidence."""
import copy
import io
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import StyleGuide as G


class FeedbackTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.root = Path(temp.name)
        image = self.root / 'wood.png'
        Image.new('RGB', (40, 40), '#8e7958').save(image)
        self.refs = {'schema': 1, 'references': [
            {'id': rid, 'path': 'wood.png', 'sha256': G.file_hash(image), 'crop': crop,
             'roles': ['material'], 'scope': ['building'], 'label': rid,
             'observation': 'Broad wood grain.', 'limitations': 'Lighting affects the colour.'}
            for rid, crop in [('old_wood', [0, 0, 20, 40]), ('new_wood', [20, 0, 20, 40])]]}
        self.original = {
            'id': 'material.wood', 'title': 'Wood', 'text': 'Use broad wood variation.',
            'scope': ['building'], 'materials': ['Planks'], 'aspect': 'material',
            'origin': 'observation', 'status': 'draft', 'confidence': 'medium',
            'kind': 'tunable', 'enforce': 'warn', 'check': 'visual review',
            'evidence': [{'reference': 'old_wood', 'aspect': 'material', 'note': 'Visible grain.'}],
            'rationale': 'Keep boards readable at game-camera distance.',
            'instructions': ['Use broad grain changes.', 'Keep the board joints visible.'],
            'avoid': ['Uniform noise on every board.'],
        }
        self.style = {
            'schema': 3, 'style_version': '1.1', 'name': 'Test', 'summary': 'Readable forms.',
            'classes': {'building': ['house'], 'machine': ['mower']},
            'rules': [copy.deepcopy(self.original)],
            'guideline': {'revision': 1, 'status': 'draft', 'history': []},
        }
        G.write_json(self.root / G.STYLE_PATH, self.style)
        G.write_json(self.root / G.REF_PATH, self.refs)

    def current(self):
        return G.load(self.root)[0]

    def test_summary_only_preserves_details_and_marks_them_provisional(self):
        result = G.revise(self.root, 'material.wood', 'Make the grain quieter.',
                          'The user asked for quieter grain.')
        style = self.current()
        rule = style['rules'][0]
        for key in ('instructions', 'avoid', 'rationale'):
            self.assertEqual(rule[key], self.original[key])
        self.assertEqual(rule['evidence'][0]['reference'], 'old_wood')
        self.assertEqual(rule['evidence'][0]['status'], 'needs_review')
        self.assertEqual(rule['guidance_review']['status'], 'needs_review')
        self.assertEqual(set(rule['guidance_review']['fields']), {'instructions', 'avoid', 'rationale', 'evidence'})
        self.assertEqual(rule['status'], 'draft')
        self.assertEqual(rule['decision'], 'The user asked for quieter grain.')
        self.assertEqual(style['guideline']['history'][0]['before_rule'], self.original)
        self.assertEqual(style['guideline']['history'][0]['after_rule'], rule)
        self.assertEqual(result['affected_models'], ['house'])
        self.assertEqual(result['automatic_builds'], 0)
        self.assertEqual(G.read(self.root / G.REF_PATH), self.refs)

    def test_repeated_updates_never_overwrite_full_prior_revisions(self):
        G.revise(self.root, 'material.wood', 'Make the grain quieter.', 'Quieter grain requested.')
        first = self.current()
        G.revise(self.root, 'material.wood', 'Keep quiet grain and clearer joints.', 'Clearer joints requested.')
        second = self.current()
        history = second['guideline']['history']
        self.assertEqual(len(history), 2)
        self.assertEqual(history[0], first['guideline']['history'][0])
        self.assertEqual(history[0]['before_rule'], self.original)
        self.assertEqual(history[1]['before_rule'], first['rules'][0])
        self.assertEqual(history[1]['after_rule'], second['rules'][0])
        self.assertEqual(second['rules'][0]['instructions'], self.original['instructions'])
        self.assertEqual(second['rules'][0]['avoid'], self.original['avoid'])
        self.assertEqual(second['rules'][0]['evidence'][0]['reference'], 'old_wood')

    def test_structured_patch_reconciles_only_explicitly_reviewed_guidance(self):
        G.revise(self.root, 'material.wood', 'Make the grain quieter.', 'Quieter grain requested.')
        G.revise(self.root, 'material.wood', None, 'Quieter grain requested.', {
            'instructions': ['Use low-contrast broad grain.', self.original['instructions'][1]],
            'evidence': [{'reference': 'new_wood', 'aspect': 'material', 'status': 'supporting'}],
            'reviewed_fields': ['avoid', 'rationale'],
        })
        style = self.current()
        rule = style['rules'][0]
        self.assertEqual(rule['text'], 'Make the grain quieter.')
        self.assertEqual(rule['instructions'][1], self.original['instructions'][1])
        self.assertEqual(rule['avoid'], self.original['avoid'])
        self.assertEqual([e['reference'] for e in rule['evidence']], ['new_wood'])
        self.assertEqual(rule['guidance_review']['status'], 'reconciled')
        self.assertEqual(rule['guidance_review']['fields'], [])
        self.assertEqual(rule['status'], 'active')
        self.assertEqual(style['guideline']['history'][0]['before_rule']['evidence'][0]['reference'], 'old_wood')

    def test_partial_patch_keeps_unreviewed_evidence_pending(self):
        G.revise(self.root, 'material.wood', 'Make the grain quieter.', 'Quieter grain requested.', {
            'instructions': ['Use low-contrast broad grain.', self.original['instructions'][1]],
            'reviewed_fields': ['avoid', 'rationale'],
        })
        rule = self.current()['rules'][0]
        self.assertEqual(rule['guidance_review']['fields'], ['evidence'])
        self.assertEqual(rule['evidence'][0]['status'], 'needs_review')
        self.assertEqual(rule['status'], 'draft')

    def test_invalid_patch_does_not_mutate_rules_or_history(self):
        original_bytes = (self.root / G.STYLE_PATH).read_bytes()
        bad_patches = [
            {'status': 'active'}, {'instructions': 'wrong type'}, {'evidence': 'wrong type'},
            {'evidence': [{'reference': 'old_wood', 'aspect': 'shape'}]},
            {'evidence': [{'reference': 'old_wood', 'aspect': 'material', 'status': 'approved'}]},
            {'reviewed_fields': ['palette']}, {'scope': []}, {'text': ''},
        ]
        for change in bad_patches:
            with self.subTest(change=change), self.assertRaises(ValueError):
                G.revise(self.root, 'material.wood', None, 'Actual user note.', change)
            self.assertEqual((self.root / G.STYLE_PATH).read_bytes(), original_bytes)

    def test_cli_patch_can_explicitly_retire_evidence_and_preserve_other_fields(self):
        changes = self.root / 'patch.json'
        G.write_json(changes, {
            'text': 'Use quiet grain.',
            'instructions': ['Keep quiet broad grain.', self.original['instructions'][1]],
            'evidence': [dict(self.original['evidence'][0], status='superseded')],
            'reviewed_fields': ['avoid', 'rationale'],
        })
        with patch('sys.argv', ['StyleGuide.py', '--root', str(self.root), 'revise', 'material.wood',
                                '--patch', str(changes), '--note', 'Quiet grain requested.']), \
                patch('sys.stdout', io.StringIO()):
            self.assertEqual(G.main(), 0)
        rule = self.current()['rules'][0]
        self.assertEqual(rule['text'], 'Use quiet grain.')
        self.assertEqual(rule['avoid'], self.original['avoid'])
        self.assertEqual(rule['evidence'][0]['status'], 'superseded')
        self.assertEqual(rule['guidance_review']['status'], 'reconciled')


if __name__ == '__main__':
    unittest.main()
