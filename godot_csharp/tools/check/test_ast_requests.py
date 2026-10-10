"""S09 compiler contracts and isolated story extensions for AstRequestCheck.

These are mechanism fixtures. They do not test UI, narrative quality or play time.
"""
import json
import re
import sys
import unittest
from pathlib import Path

TOOLS = Path(__file__).resolve().parents[1]
ROOT = TOOLS.parents[1]
sys.path.insert(0, str(TOOLS))
import compile_story
import story_ast
import story_script
from story_base import StoryError

OUTPUT = ROOT / "tmp/s09-ast-check"


def expanded_source():
    text = "\n".join("    " * line.indent + line.text
                     for line in story_script.expand_includes(compile_story.SOURCE)) + "\n"
    return text.split("\n运行\n", 1)[0] + "\n"


def insert_before(text, marker, addition):
    if text.count(marker) != 1:
        raise AssertionError("Expected one fixture insertion point: " + marker)
    return text.replace(marker, addition + marker, 1)


def append_to_block(text, heading, addition):
    marker = '\n    ' + heading + '\n'
    if text.count(marker) != 1:
        raise AssertionError('Expected one fixture block: ' + heading)
    end = text.index(marker) + len(marker)
    while end < len(text):
        line_end = text.find('\n', end)
        if line_end < 0:
            line_end = len(text)
        line = text[end:line_end]
        if line.strip() and not line.startswith('        '):
            break
        end = line_end + 1
    return text[:end] + addition + text[end:]


def fixture_text():
    text = expanded_source()
    # Extend the existing records instead of replacing any formal declarations.
    text = insert_before(text, "\n        actions:\n", '''
            FixtureSuccess: set("$actor", "FixtureSuccess", 1)
            FixtureDelayed: wait(2, seq(require(tag("$actor", "FixtureReady", 1)), set("$actor", "FixtureDelayedSuccess", 1)))
            FixtureImpossible: seq(require(false()), set("$actor", "FixtureImpossibleSuccess", 1))
            FixtureTarget: set("$target", "FixtureTargetSuccess", 1)
''')
    actions = []
    for action, procedure in (("FixtureSuccess", "FixtureSuccess"),
                              ("FixtureDelayed", "FixtureDelayed"),
                              ("FixtureImpossible", "FixtureImpossible")):
        actions.append(f'''            {action}:
                condition: tag("$recipient", "FixtureReady", 1)
                goal: true()
                effect: procedure("{procedure}")
                flags:
                cost: 0.01
                text: fixture.request
                actor: recipient
                target: none
''')
    actions.append('''            FixtureTarget:
                condition: tag("$recipient", "FixtureReady", 1)
                goal: true()
                effect: procedure("FixtureTarget")
                flags:
                cost: 0.01
                text: fixture.request
                actor: recipient
                target: kind("$target", "Body") 且 tag("$target", "FixtureTargetReady", 1)
                target_self: false
''')
    text = insert_before(text, '\n        actions:\n', '')
    text = text.replace('\n        actions:\n', '\n        actions:\n' + ''.join(actions), 1)
    text = append_to_block(text, '请求', '        - 中性附加请求 → @FixtureSuccess\n')
    text = insert_before(text, "\n        例外：\n", '''
            - 中性附加行为 | FixtureBehavior |
                rules:
                    0:
                        condition: param("subject") 且 param("condition")
                        mode: Prefer
                        effect: set("$actor", "FixtureCardSuccess", 1)
                        weight: 9
''')
    text += '''
续篇 chapter2
    文本
        fixture:
            request: neutral fixture request
    规矩 中性附加规则
        编号：FixtureRule
        立规者：恶魔
        内容：中性机制夹具
        标记：
        执行：
            0:
                condition: true()
                mode: Prefer
                effect: set("$actor", "FixtureRuleSuccess", 1)
                weight: 11
'''
    return text


def prepare(output=OUTPUT):
    output = Path(output)
    output.mkdir(parents=True, exist_ok=True)
    path = output / "extension.story"
    path.write_text(fixture_text(), encoding="utf-8")
    document = story_script.compile_story([path], {"awakening"}, section="chapter2")
    (output / "chapter2.json").write_text(json.dumps(document, ensure_ascii=False), encoding="utf-8")
    prepare_final_contracts(output / "final-contracts")
    prepare_receipt_contracts(output / "receipt-contracts")
    return path, document


def final_contract_text():
    text = fixture_text()
    actions = '''            FixtureDeclaredCost:
                condition: true()
                goal: true()
                effect: travel("$destination")
                flags:
                cost: 1.25
                travel_cost: 3.75
                text: fixture.request
                actor: recipient
                target: none
            FixturePhysical:
                condition: tag("$destination", "Passable", 1)
                condition_world: physical
                goal: true()
                effect: seq(travel("$destination"), set("$actor", "FixturePhysicalSuccess", 1))
                flags:
                cost: 0.01
                text: fixture.request
                actor: recipient
                target: none
            FixtureDeclaredFulfill:
                condition: tag("$recipient", "FixtureReady", 1)
                goal: true()
                effect: procedure("FixtureDelayed")
                flags:
                cost: 0.01
                text: fixture.request
                actor: recipient
                target: none
                promise: true
            FixtureOtherCompleted:
                condition: tag("$recipient", "FixtureReady", 1)
                goal: true()
                effect: procedure("FixtureDelayed")
                flags:
                cost: 0.01
                text: fixture.request
                actor: recipient
                target: none
                promise: true
'''
    text = text.replace('\n        actions:\n', '\n        actions:\n' + actions, 1)
    text, replacements = re.subn(r'(?m)^(            fulfill_action: )[^\n]+$',
                                 r'\g<1>FixtureDeclaredFulfill', text)
    if replacements != 1:
        raise AssertionError('Expected one declared fulfillment action')
    for scope in ('world', 'chapter', 'interaction'):
        text += f'''
续篇 chapter2
    规矩 中性元信息{scope}
        编号：FixtureMetadata_{scope}
        立规者：恶魔
        内容：neutral metadata fixture
        标记：
        有效期：{scope}
'''
    return text


def prepare_final_contracts(output):
    output = Path(output)
    output.mkdir(parents=True, exist_ok=True)
    path = output / 'extension.story'
    path.write_text(final_contract_text(), encoding='utf-8')
    document = story_script.compile_story([path], {'awakening'}, section='chapter2')
    (output / 'chapter2.json').write_text(json.dumps(document, ensure_ascii=False), encoding='utf-8')
    return path, document


def receipt_contract_text(weight=3):
    text = fixture_text()
    policy = '''
        receipt_attitudes:
'''
    for name, value in (('rules', weight), ('requests', weight + 4), ('mediation', weight + 2)):
        policy += f'''            {name}:
                accepted:
                    FixtureReceipt_{name}_accepted: {value}
                    Trust: {value}
                rejected:
                    FixtureReceipt_{name}_rejected: {-value}
                    Trust: {-value}
'''
    text, count = re.subn(r'\n        receipt_attitudes:\n.*?(?=\n        mediation:\n)',
                          policy.rstrip('\n'), text, flags=re.S)
    if count != 1:
        raise AssertionError('Expected one receipt attitude declaration')
    return text


def prepare_receipt_contracts(output):
    output = Path(output)
    output.mkdir(parents=True, exist_ok=True)
    for weight in (3, 9):
        path = output / f'weight-{weight}.story'
        path.write_text(receipt_contract_text(weight), encoding='utf-8')
        document = story_script.compile_story([path], {'awakening'}, section='chapter2')
        (output / f'weight-{weight}.json').write_text(json.dumps(document, ensure_ascii=False), encoding='utf-8')


class AstCompilerTests(unittest.TestCase):
    def test_receipt_attitudes_preserve_declared_integer_tags_and_reject_invalid_outcomes(self):
        policies = {'rules': {'accepted': {'FixtureReceipt': 3, 'Trust': 3},
                              'rejected': {'FixtureReceipt': -3}}}
        compiled = story_ast.program({'procedures': {}, 'actions': {}, 'receipt_attitudes': policies})
        self.assertEqual(compiled['receipt_attitudes'], policies)
        invalid = [None, [], {'rules': None}, {'rules': []}, {'rules': {'completed': {}}},
                   {'rules': {'Accepted': {}}}, {'rules': {'accepted': []}},
                   {'rules': {'accepted': {'': 1}}}]
        invalid += [{'rules': {'accepted': {'FixtureReceipt': value}}}
                    for value in (True, 1.5, '1', None)]
        for policy in invalid:
            with self.subTest(policy=policy), self.assertRaises(StoryError):
                story_ast.program({'procedures': {}, 'actions': {}, 'receipt_attitudes': policy})

    def test_story_receipt_label_and_weight_changes_survive_formal_compilation(self):
        prepare_receipt_contracts(OUTPUT / 'receipt-contracts')
        for weight in (3, 9):
            document = json.loads((OUTPUT / 'receipt-contracts' / f'weight-{weight}.json').read_text(encoding='utf-8'))
            policies = document['chapters'][0]['design']['execution']['receipt_attitudes']
            for name, value in (('rules', weight), ('requests', weight + 4), ('mediation', weight + 2)):
                self.assertEqual(policies[name]['accepted'], {f'FixtureReceipt_{name}_accepted': value, 'Trust': value})
                self.assertEqual(policies[name]['rejected'], {f'FixtureReceipt_{name}_rejected': -value, 'Trust': -value})

    def test_action_condition_world_accepts_declared_sources_and_rejects_unknown(self):
        row = {'condition': 'true()', 'goal': 'true()', 'effect': 'set("$actor", "FixtureValue", 1)',
               'cost': .01, 'text': 'fixture.request', 'actor': 'recipient', 'target': 'none'}
        for world in ('physical', 'subjective'):
            compiled = story_ast.program({'procedures': {}, 'actions': {'neutral': {**row, 'condition_world': world}}})
            self.assertEqual(compiled['actions']['neutral']['condition_world'], world)
        for world in ('absent', '', True, 1, None):
            with self.subTest(world=world), self.assertRaises(StoryError):
                story_ast.program({'procedures': {}, 'actions': {'neutral': {**row, 'condition_world': world}}})

    def test_metadata_only_rules_have_empty_ast_and_preserve_all_lifetimes(self):
        from types import SimpleNamespace
        from story_base import read_lines
        from story_design import nodes
        from story_rules import compile_rules
        ctx = SimpleNamespace(vocab=SimpleNamespace(mapping=lambda _: {}), person_enum={'甲': 'X'})
        for scope in (None, 'world', 'chapter', 'interaction'):
            with self.subTest(scope=scope):
                text = '规矩 中性\n    编号：FixtureMetadata\n    立规者：甲\n    内容：neutral metadata\n    标记：\n'
                if scope is not None:
                    text += f'    有效期：{scope}\n'
                lines = read_lines(text)
                entry = compile_rules([(lines[0], nodes(lines[1:]))], ctx)[0]
                self.assertEqual(entry, {'name': '中性', 'id': 'FixtureMetadata', 'text': 'neutral metadata',
                                         'flags': [], 'lifetime': scope or 'world', 'rules': [], 'author': 'X'})

    def test_story_copy_changes_fulfillment_id_and_keeps_metadata_rules_nonexecutable(self):
        _, document = prepare_final_contracts(OUTPUT / 'final-contracts')
        design = document['chapters'][0]['design']
        self.assertEqual(design['execution']['commitments']['fulfill_action'], 'FixtureDeclaredFulfill')
        self.assertTrue(design['execution']['actions']['FixtureDeclaredFulfill']['promise'])
        for scope in ('world', 'chapter', 'interaction'):
            rule = next(row for row in design['rules'] if row['id'] == 'FixtureMetadata_' + scope)
            self.assertEqual(rule['lifetime'], scope)
            self.assertEqual(rule['rules'], [])

    def test_conditions_and_effects_keep_typed_nodes_and_neutral_refs(self):
        condition = story_ast.expression('tag("$actor", "FixtureReady", 1) 且 非 false()', 'c')
        effect = story_ast.expression('wait(2.5, seq(require(true()), change("$actor", "FixtureValue", -2)))', 'e')
        self.assertEqual(condition["op"], "and")
        self.assertEqual(condition["args"][0]["args"][0]["value"], "$actor")
        self.assertEqual(effect["name"], "wait")
        self.assertEqual(effect["args"][0]["value"], 2.5)
        self.assertEqual(effect["args"][1]["args"][1]["args"][2]["value"], -2)
        history = story_ast.expression('observed("neutral", "FixtureObserved", 1, "Perception")', 'c')
        self.assertEqual(history['name'], 'observed')

    def test_unknown_nodes_wrong_types_and_wrong_arity_fail_explicitly(self):
        for source, kind in [('unimplemented("$actor")', 'e'), ('absent("$actor")', 'c'),
                             ('tag("$actor", "FixtureReady", true())', 'c'),
                             ('set("$actor", "FixtureReady", 1.5)', 'e'),
                             ('observed("neutral", "FixtureObserved", "1", "Perception")', 'c'),
                             ('travel("x", "y")', 'e'), ('true()', 'e'), ('set("x", "y", 1)', 'c')]:
            with self.subTest(source=source), self.assertRaises(StoryError):
                story_ast.expression(source, kind, 71)

    def test_rule_mode_weight_required_fields_and_select_are_checked(self):
        row = {'condition': 'true()', 'effect': 'set("$actor", "FixtureValue", 1)',
               'mode': 'Prefer', 'weight': 3}
        compiled = story_ast.rules({'neutral': row})
        self.assertEqual(compiled[0]['select']['name'], 'true')
        for replacement in ({'mode': 'Absent'}, {'weight': True}, {'weight': 1.5}, {'extra': 1}):
            with self.subTest(replacement=replacement), self.assertRaises(StoryError):
                story_ast.rules({'neutral': {**row, **replacement}})
        for missing in ('condition', 'effect', 'mode', 'weight'):
            invalid = dict(row)
            del invalid[missing]
            with self.subTest(missing=missing), self.assertRaises(StoryError):
                story_ast.rules({'neutral': invalid})

    def test_action_schema_rejects_unknown_ast_bad_actor_and_bad_cost(self):
        row = {'condition': 'true()', 'goal': 'true()', 'effect': 'set("$actor", "FixtureValue", 1)',
               'cost': .01, 'text': 'fixture.request', 'actor': 'recipient', 'target': 'none'}
        for replacement in ({'effect': 'unimplemented("$actor")'}, {'actor': 'absent'},
                            {'cost': -1}, {'cost': True}, {'extra': 1},
                            {'send_cost': -1}, {'send_cost': True}, {'travel_cost': -1},
                            {'travel_cost': '1'}, {'target_self': 0}, {'promise': 'true'}):
            with self.subTest(replacement=replacement), self.assertRaises(StoryError):
                story_ast.program({'procedures': {}, 'actions': {'neutral': {**row, **replacement}}})
        self.assertEqual(story_ast.program({'procedures': {}, 'actions': {'neutral': row}})
                         ['actions']['neutral']['effect']['name'], 'set')
        optional = {**row, 'send_cost': 0, 'travel_cost': 1.5, 'target_self': True, 'promise': False}
        compiled = story_ast.program({'procedures': {}, 'actions': {'neutral': optional}})['actions']['neutral']
        self.assertEqual({field: compiled[field] for field in ('send_cost', 'travel_cost', 'target_self', 'promise')},
                         {field: optional[field] for field in ('send_cost', 'travel_cost', 'target_self', 'promise')})

    def test_copy_extension_retains_existing_rules_cards_actions_and_source_bytes(self):
        sources = [compile_story.SOURCE] + list((compile_story.SOURCE.parent / 'story').rglob('*.story'))
        before = {p: p.read_bytes() for p in sources}
        base_path = OUTPUT / 'base.story'
        OUTPUT.mkdir(parents=True, exist_ok=True)
        base_path.write_text(expanded_source(), encoding='utf-8')
        base = story_script.compile_story([base_path], {'awakening'}, section='chapter2')['chapters'][0]
        _, document = prepare()
        extended = document['chapters'][0]
        old, new = base['design'], extended['design']
        self.assertEqual(new['rules'][:-1], old['rules'])
        self.assertEqual(new['rules'][-1]['id'], 'FixtureRule')
        self.assertEqual(new['rules'][-1]['rules'][0]['mode'], 'Prefer')
        self.assertEqual(new['card_kit']['behaviors'][:-1], old['card_kit']['behaviors'])
        self.assertEqual(new['card_kit']['behaviors'][-1]['id'], 'FixtureBehavior')
        for key, value in old['execution']['actions'].items():
            self.assertEqual(new['execution']['actions'][key], value)
        self.assertEqual(new['execution']['actions']['FixtureSuccess']['actor'], 'recipient')
        self.assertEqual(new['execution']['actions']['FixtureTarget']['target']['op'], 'and')
        self.assertEqual(extended['requests'][:-1], base['requests'])
        self.assertEqual(extended['requests'][-1]['id'], 'FixtureSuccess')
        self.assertEqual(before, {p: p.read_bytes() for p in sources})

    def test_unknown_ast_in_a_story_copy_is_a_compilation_failure(self):
        OUTPUT.mkdir(parents=True, exist_ok=True)
        path = OUTPUT / 'invalid.story'
        path.write_text(fixture_text().replace('FixtureSuccess: set("$actor", "FixtureSuccess", 1)',
                                              'FixtureSuccess: unimplemented("$actor")'), encoding='utf-8')
        with self.assertRaises((StoryError, story_script.ws.ScriptError)) as error:
            story_script.compile_story([path], {'awakening'}, section='chapter2')
        self.assertIn('unimplemented', str(error.exception))


if __name__ == '__main__':
    if '--prepare' in sys.argv:
        index = sys.argv.index('--prepare')
        destination = Path(sys.argv[index + 1]) if len(sys.argv) > index + 1 else OUTPUT
        prepare(destination)
        print(destination)
    else:
        unittest.main()
