"""Offline shallow projections of the sealed host DAG; original evidence stays untouched."""
import collections
import functools
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(ROOT / 'language_core/tools'))
from binary_evidence import RecordWriter
from raw_dag import RawDagIndex

run = Path(sys.argv[1]).resolve()
with RawDagIndex(run / 'host-session/session.dvdag') as index:
    index.node = functools.lru_cache(maxsize=30000)(index.node)

    def project(value, depth=0):
        if not isinstance(value, dict) or 'node' not in value:
            return value
        if depth >= 5:
            return {'original_node': value['node']}
        items = index.node(value['node'])
        pairs = []
        for item in items:
            if isinstance(item, dict) and 'node' in item:
                pair = index.node(item['node'])
                if len(pair) == 2 and isinstance(pair[0], str):
                    pairs.append(pair)
        if items and len(pairs) == len(items):
            return {key: ({'original_node': val['node']} if key in
                    {'mind', 'mind_before', 'mind_after', 'world', 'world_before', 'world_after', 'predicted'}
                    and isinstance(val, dict) and 'node' in val else project(val, depth + 1))
                    for key, val in pairs}
        return [project(item, depth + 1) for item in items]

    root = index.select(checkpoint='final')['node']
    refs = index.node(index.record_field(root, 'events')['node'])
    types = collections.Counter()
    selected = []
    plans = []
    target = {'listener_parse', 'speech_received_pending', 'public_topic_heard',
              'mind_simulation', 'hypothesis_adopted', 'hypothesis_revised',
              'answer_submitted', 'answer_accepted', 'question_transition',
              'host_completion'}
    for ref in refs:
        node = ref['node']
        kind = index.record_field(node, 'type')
        actor = index.record_field(node, 'actor')
        types[kind] += 1
        if (actor == 'detective' and kind in target) or kind == 'answer_accepted':
            selected.append({key: project(index.record_field(node, key))
                             for key in ('id', 'type', 'actor', 'time', 'parents', 'data')})
        if actor == 'detective' and kind in {'planning_stop', 'planning_choice'}:
            plans.append({key: project(index.record_field(node, key))
                          for key in ('id', 'type', 'actor', 'time', 'parents', 'data')})
    result = {'dag_validation': index.summary,
              'scope': 'Derived shallow projections of sealed final.events, not incremental event-commit index. Deep original values stay in the unmodified source DAG, referenced by original_node.',
              'event_type_counts': dict(types), 'selected_events': selected,
              'detective_planning_events': plans}
    destination = run / 'investigation-shallow-events.dvlog'
    if destination.exists():
        raise SystemExit('Refusing to overwrite evidence')
    with RecordWriter(destination) as writer:
        writer.append(result)
    print('seal', index.summary['complete_sealing'], 'events', sum(types.values()),
          'types', dict(types), 'selected', len(selected), 'plans', len(plans))
