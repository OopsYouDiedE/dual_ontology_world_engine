"""Compiler contracts for declarative environmental updates and work receipts."""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import story_ast
from story_base import StoryError


def program():
    return {
        'procedures': {'work': 'seq(occupy("station", "Occupied", 1), wait(2, set("$actor", "Done", 1)))'},
        'actions': {},
        'laws': {'decay': {'hook': 'end', 'once': 'day', 'condition': 'tag("store", "Units", 1)',
                           'effect': 'seq(spend("store", "Units", 1), set("item", "Result", 1))'}},
        'recipes': {'work': {'stations': 'first、second', 'actor_tags': 'AtFirst、AtSecond', 'duration': 1.5,
                             'condition': 'true()', 'effect': 'change("item", "Result", 1)'}},
    }


class Contracts(unittest.TestCase):
    def test_existing_effect_ast_and_numeric_reads(self):
        data = program()
        data['laws']['decay']['effect'] = 'spend("store", "Units", min(value("store", "Units"), add(1, 2)))'
        result = story_ast.program(data)
        self.assertEqual(result['laws']['decay']['effect']['name'], 'spend')
        self.assertEqual(result['procedures']['work']['args'][0]['name'], 'occupy')
        self.assertEqual(result['recipes']['work']['stations'], ['first', 'second'])

    def test_personal_operations_cannot_hide_in_nested_laws_or_recipes(self):
        for operation in ('travel("route")', 'tell("person", "line", true())',
                          'transport("person", "route")', 'think(true())', 'procedure("work")'):
            for section in ('laws', 'recipes'):
                with self.subTest(operation=operation, section=section):
                    data = program()
                    key = 'decay' if section == 'laws' else 'work'
                    data[section][key]['effect'] = 'seq(set("item", "Result", 1), seq(when(true(), ' + operation + ')))'
                    with self.assertRaises(StoryError): story_ast.program(data)

    def test_work_requires_distinct_stations_tags_and_positive_time(self):
        for field, value in [('stations', 'same、same'), ('actor_tags', 'Same、Same'), ('duration', 0),
                             ('duration', -1), ('stations', 'only')]:
            with self.subTest(field=field, value=value):
                data = program(); data['recipes']['work'][field] = value
                with self.assertRaises(StoryError): story_ast.program(data)

    def test_unknown_lifecycle_collection_and_fields_are_rejected(self):
        for field, value in [('once', 'forever'), ('hook', 'mystery'), ('each', 'outsiders'), ('script', 'line')]:
            with self.subTest(field=field):
                data = program(); data['laws']['decay'][field] = value
                with self.assertRaises(StoryError): story_ast.program(data)

    def test_integer_types_and_belief_policies_are_strict(self):
        for expression in ('spend("store", "Units", true())', 'spend("store", "Units", 0.5)',
                           'spend("store", "Units", unknown(1))'):
            with self.subTest(expression=expression):
                with self.assertRaises(StoryError): story_ast.expression(expression, 'e')
        for key in ('body_defaults', 'constants'):
            data = program(); data[key] = {'Units': True}
            with self.assertRaises(StoryError): story_ast.program(data)
        data = program(); data['belief_parameters'] = {'known': {'rule': 'neutral', 'accepted': 'yes'}}
        with self.assertRaises(StoryError): story_ast.program(data)


if __name__ == '__main__': unittest.main()
