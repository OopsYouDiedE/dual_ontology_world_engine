"""Neutral acquisition compiler contracts and a removed-evidence story copy."""
import argparse
import copy
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from story_base import StoryError
from story_chapter1 import load
from story_knowledge import validate_knowledge

FIXTURE = Path(__file__).resolve().parents[2] / 'scenes/Shared/check/KnowledgeCheck/Fixtures/acquisition.story'


def source(text):
    folder = Path(__file__).resolve().parents[3] / 'tmp'
    folder.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(mode='w', encoding='utf-8', suffix='.story', dir=folder, delete=False) as file:
        file.write(text)
        path = Path(file.name)
    try:
        return load(path)
    finally:
        path.unlink()


def parse(text):
    return source(text).RULES_DATA['KNOWLEDGE']


def remove_evidence(text):
    lines, deleting = [], False
    for line in text.splitlines():
        if line.startswith('证物 item '):
            deleting = True
            continue
        if deleting and line[:1].isspace(): continue
        deleting = False
        if '卡片 | item' not in line: lines.append(line)
    return '\n'.join(lines) + '\n'


def validate(data, evidence=('item',), **overrides):
    args = dict(evidence=set(evidence), statements={'claim'}, variables={},
                line_speakers={'memory': 'observer'}, choices={'recall'}, sites=set())
    args.update(overrides)
    validate_knowledge(data, **args)


class KnowledgeTests(unittest.TestCase):
    def setUp(self):
        self.text = FIXTURE.read_text(encoding='utf-8')
        self.data = parse(self.text)

    def test_no_truth_in_declaration(self):
        validate(self.data)
        self.assertEqual(set(self.data), {'propositions', 'bindings', 'implications'})
        self.assertTrue(all('truth' not in b for b in self.data['bindings']))

    def test_delete_evidence_in_story_copy(self):
        deleted = source(remove_evidence(self.text))
        missing = deleted.RULES_DATA['KNOWLEDGE']
        validate(missing, evidence=())
        self.assertEqual(len(source(self.text).EVIDENCE), 1)
        self.assertEqual(deleted.EVIDENCE, [])
        self.assertEqual(self.data['implications'], missing['implications'])
        self.assertEqual([b['source'] for b in missing['bindings']], ['memory', 'proposal', 'proposal'])

    def test_reject_undeclared_proposition(self):
        self.data['bindings'][0]['propositions'] = ['missing']
        with self.assertRaises(StoryError): validate(self.data)

    def test_reject_deleted_object_stale_binding(self):
        with self.assertRaises(StoryError): validate(self.data, evidence=())

    def test_reject_speaker_substitution(self):
        with self.assertRaises(StoryError): validate(self.data, line_speakers={'memory': 'other'})

    def test_reject_duplicate_binding(self):
        self.data['bindings'].append(copy.deepcopy(self.data['bindings'][0]))
        with self.assertRaises(StoryError): validate(self.data)

    def test_reject_proposal_as_fact(self):
        with self.assertRaises(StoryError): parse(self.text.replace('陈述 | claim | 候选', '陈述 | claim | 亲见'))

    def test_reject_memory_without_recall_act(self):
        with self.assertRaises(StoryError): parse(self.text.replace('observer | 回想', 'observer | 陈述'))

    def test_reject_missing_replay_value(self):
        self.data['bindings'][-1].update(kind='variable', key='v:2')
        with self.assertRaises(StoryError): validate(self.data, variables={'v': 2})

    def test_reject_missing_site(self):
        self.data['bindings'][-1].update(kind='field', key='unknown')
        with self.assertRaises(StoryError): validate(self.data)

    def test_localization_keeps_wiring(self):
        translated = parse(self.text.replace('neutral observation', 'translated observation'))
        self.assertEqual(self.data['bindings'], translated['bindings'])
        self.assertEqual(self.data['implications'], translated['implications'])


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--prepare-fixture', type=Path)
    args, remaining = parser.parse_known_args()
    result = unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(KnowledgeTests))
    if not result.wasSuccessful(): sys.exit(1)
    if args.prepare_fixture:
        target = args.prepare_fixture.resolve()
        if target.exists(): raise RuntimeError('Refusing to overwrite a fixture directory')
        target.mkdir(parents=True)
        full = FIXTURE.read_text(encoding='utf-8')
        missing = remove_evidence(full)
        for name, text in [('full', full), ('missing', missing)]:
            (target / (name + '.story')).write_text(text, encoding='utf-8')
            compiled = source(text)
            data = compiled.RULES_DATA['KNOWLEDGE']
            validate(data, evidence={e.id for e in compiled.EVIDENCE})
            (target / (name + '.json')).write_text(json.dumps({'KNOWLEDGE': data}, ensure_ascii=False), encoding='utf-8')
