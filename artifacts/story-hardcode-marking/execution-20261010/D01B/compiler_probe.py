"""DETECTION_ONLY: existing compiler on neutral in-memory source; no project story/design import."""
import importlib.util
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
path = ROOT / 'godot_csharp/tools/world_script.py'
spec = importlib.util.spec_from_file_location('d01b_existing_world_script', path)
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)

neutral = '''chapter(id="neutral")
空间 = room(id="room_a", name="中性空间")
甲 = object(id="object_a", name="样本甲", room=空间, aliases=["共同词"], state={"量": 1}, observable={"量": True})
'''
sources = {
    'unique_alias': neutral,
    'same_word_two_entities': neutral + '乙 = object(id="object_b", name="样本乙", room=空间, aliases=["共同词"])\n',
    'added_neutral_entity': neutral + '乙 = object(id="object_b", name="样本乙", room=空间, aliases=["第二词"])\n',
    'unknown_entity': neutral + '乙 = object(id="object_b", name="样本乙", room="未声明空间")\n',
    'unsupported_facet_property': neutral.replace('state={"量": 1}', 'properties={"facet": "侧面甲"}'),
}
results = []
for key, text in sources.items():
    source = (Path(__file__).resolve().parent / (key + '.neutral')).as_posix()
    try:
        doc = module.Compiler([Path(source)], texts={source: text}).parse()
        results.append({'id': key, 'status': 'COMPILED', 'entity_ids': [item['id'] for item in doc['chapters'][0]['entities']]})
    except module.ScriptError as exc:
        results.append({'id': key, 'status': 'REJECTED', 'message': str(exc), 'location': exc.source.json() if exc.source else None})
assert results[0]['status'] == 'COMPILED'
assert results[1]['status'] == 'REJECTED'
assert results[2]['status'] == 'COMPILED'
assert results[3]['status'] == 'REJECTED'
assert results[4]['status'] == 'REJECTED'
print(json.dumps({'schema': 'D01B-compiler-probe/v1', 'mode': 'DETECTION_ONLY', 'results': results}, ensure_ascii=False, indent=2))
