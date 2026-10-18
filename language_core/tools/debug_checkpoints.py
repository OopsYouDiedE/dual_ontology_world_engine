"""Read-only export and validation of committed real-completion stage checkpoints."""
from pathlib import Path
from fractions import Fraction
import argparse
import hashlib
import io
import json
import os
from raw_dag import RawDagIndex

STAGES = {'stage-day3': 2880, 'stage-day5': 5760}


def sha_file(path):
    digest = hashlib.sha256()
    with Path(path).open('rb') as source:
        while block := source.read(1024 * 1024):
            digest.update(block)
    return digest.hexdigest()


def record(value):
    if isinstance(value, list):
        if value and all(isinstance(p, list) and len(p) == 2 and isinstance(p[0], str) for p in value):
            return {p[0]: record(p[1]) for p in value}
        return [record(p) for p in value]
    return value


def small(index, name, path):
    output = io.StringIO()
    index.write_value(index.select(checkpoint=name, path=path), output, 'trace-json')
    return record(json.loads(output.getvalue()))


def checkpoint_metadata(index, name):
    if name not in STAGES:
        raise ValueError('Only actual stage-day3/stage-day5 completion checkpoints may resume')
    row = index.db.execute('SELECT root,events,seq FROM checkpoints WHERE name=? ORDER BY seq DESC LIMIT 1', (name,)).fetchone()
    if row is None:
        raise ValueError('Requested stage checkpoint has no validated commit')
    meta = {key: small(index, name, [key]) for key in ('format', 'after_complete', 'boundary', 'before_time', 'game_time', 'steps', 'executed', 'completion', 'completed_action_event', 'last_event', 'inherited_event_count', 'debug_config', 'run_config', 'provenance')}
    if meta['format'] != 'dv-debug-stage-v1' or meta['after_complete'] is not True:
        raise ValueError('Checkpoint was not sealed after a real Complete')
    if meta['boundary'] != STAGES[name] or not Fraction(str(meta['before_time'])) < STAGES[name] <= Fraction(str(meta['game_time'])):
        raise ValueError('Checkpoint does not cross its declared absolute day boundary')
    if small(index, name, ['state', 'physical', 'time']) != meta['game_time']:
        raise ValueError('Checkpoint clock differs from saved physical state')
    # Complete may remove pending entirely; both forms match DV's dv_find(..., false).
    # Inspect only this optional field; missing mandatory fields still fail below.
    saved_state = index.select(checkpoint=name, path=['state'])
    pending = index.record_field(saved_state['node'], 'pending')
    if pending is not None and pending is not False:
        raise ValueError('Checkpoint still awaits execution completion')
    if small(index, name, ['state', 'story', 'run_config']) != meta['run_config']:
        raise ValueError('Stored session configuration differs from checkpoint declaration')
    if meta['provenance'].get('format') != 'dv-debug-provenance-v1' or meta['provenance'].get('run_config') != meta['run_config']:
        raise ValueError('Checkpoint source/configuration provenance is missing or inconsistent')
    selected = index.select(checkpoint=name, path=['state', 'events'])
    event_refs = index.node(selected['node'])
    inherited = meta['inherited_event_count']
    if type(inherited) is not int or inherited < 0 or len(event_refs) - inherited != row[1] or not event_refs:
        raise ValueError('Saved event extent differs from committed raw-event extent')
    last_event_ref = event_refs[-1]
    if not isinstance(last_event_ref, dict) or index.record_field(last_event_ref['node'], 'id') != meta['last_event']:
        raise ValueError('Saved final event does not match checkpoint evidence')
    completion = meta['completion']
    def event_value(event, path):
        output = io.StringIO()
        index.write_value(index.select(event=event, path=path), output, 'trace-json')
        return record(json.loads(output.getvalue()))
    host_id, execution_id = completion['host_event'], completion['execution_event']
    positions = {}
    for event in (host_id, execution_id, meta['last_event']):
        found = index.db.execute('SELECT ordinal FROM events WHERE id=?', (event,)).fetchone()
        if found is None:
            raise ValueError('Checkpoint lacks a committed host/execution/final event')
        positions[event] = found[0]
    if not positions[host_id] < positions[execution_id] <= positions[meta['last_event']]:
        raise ValueError('Checkpoint execution provenance has an invalid event order')
    if event_value(host_id, ['type']) != 'host_completion' or event_value(execution_id, ['type']) != 'action_execution':
        raise ValueError('Checkpoint has no actual host/execution event pair')
    if event_value(execution_id, ['parents']) != [host_id]:
        raise ValueError('Execution event does not descend from the declared host receipt')
    if event_value(host_id, ['actor']) != completion['actor'] or event_value(execution_id, ['actor']) != completion['actor']:
        raise ValueError('Checkpoint execution actor differs from host receipt')
    if completion['action_event'] != meta['completed_action_event'] or event_value(host_id, ['data', 'action_event']) != meta['completed_action_event']:
        raise ValueError('Checkpoint action token does not match the actual host receipt')
    if event_value(host_id, ['data', 'executor']) != 'formal-model' or event_value(execution_id, ['data', 'receipt', 'execution', 'status']) != completion['status']:
        raise ValueError('Checkpoint execution receipt differs from recorded Complete')
    if small(index, name, ['state', 'executed']) != meta['executed'] or Fraction(str(meta['executed'])) <= 0:
        raise ValueError('Saved completion extent is missing or inconsistent')
    if index.summary['source_changed_during_scan']:
        raise ValueError('Raw source changed during validation')
    return {**meta, 'checkpoint': name, 'commit_frame': row[2], 'committed_events': row[1], 'state_event_extent': len(event_refs)}


def export_checkpoint(index, name, out):
    meta = checkpoint_metadata(index, name)
    out = Path(out).resolve()
    out.mkdir(parents=True, exist_ok=False)
    temporary = out / 'checkpoint.save.json.partial'
    target = out / 'checkpoint.save.json'
    with temporary.open('x', encoding='utf-8', newline='\n') as output:
        index.write_value(index.select(checkpoint=name), output, 'dv-tagged')
        output.write('\n')
    os.link(temporary, target)
    temporary.unlink()
    manifest = {'format': 'dv-debug-checkpoint-bundle-v1', 'checkpoint': name, 'save_file': target.name, 'save_sha256': sha_file(target), 'raw_source': str(index.source), 'raw_sha256': index.summary['source_sha256'], 'raw_validated_bytes': index.summary['validated_bytes'], 'raw_invalid_tail': index.summary['invalid_tail'], 'metadata': meta, 'scope': 'actual committed completion checkpoint; tagged Save values preserve exact rationals; no invented earlier state'}
    with (out / 'manifest.json').open('x', encoding='utf-8') as output:
        json.dump(manifest, output, ensure_ascii=False, indent=2)
        output.write('\n')
    return manifest


def validate_bundle(path):
    path = Path(path).resolve()
    bundle = json.loads(path.read_text(encoding='utf-8'))
    if bundle.get('format') != 'dv-debug-checkpoint-bundle-v1':
        raise ValueError('Unsupported checkpoint bundle')
    save = path.parent / bundle['save_file']
    if save.name != 'checkpoint.save.json' or save.resolve().parent != path.parent or not save.is_file() or sha_file(save) != bundle['save_sha256']:
        raise ValueError('Checkpoint tagged Save is missing, changed or outside its bundle')
    raw = Path(bundle['raw_source'])
    if not raw.is_file():
        raise ValueError('Original committed raw source is missing')
    # One validated raw scan; stream the Save re-expansion through a digest.
    class DigestSink:
        def __init__(self): self.digest = hashlib.sha256()
        def write(self, text): self.digest.update(text.encode('utf-8'))
    with RawDagIndex(raw) as index:
        if index.summary['source_sha256'] != bundle['raw_sha256']:
            raise ValueError('Original committed raw source changed')
        meta = checkpoint_metadata(index, bundle['checkpoint'])
        if meta != bundle['metadata']:
            raise ValueError('Bundle metadata does not match original committed checkpoint')
        sink = DigestSink()
        index.write_value(index.select(checkpoint=bundle['checkpoint']), sink, 'dv-tagged')
        sink.write('\n')
        if sink.digest.hexdigest() != bundle['save_sha256']:
            raise ValueError('Tagged Save does not equal the committed checkpoint value')
    hashes = meta['provenance'].get('source_hashes')
    if not isinstance(hashes, dict) or not hashes:
        raise ValueError('Checkpoint has no source fingerprint contract')
    for source, expected in hashes.items():
        if not Path(source).is_file() or sha_file(source) != expected:
            raise ValueError(f'Checkpoint source differs or is missing: {source}')
    return bundle, save


def export_available(source, destination, summary=None):
    if summary is not None and not any(item['name'] in STAGES for item in summary.get('checkpoint_commits', [])):
        return [{'checkpoint': name, 'available': False, 'reason': 'no committed stage in previously validated summary'} for name in STAGES]
    result = []
    with RawDagIndex(source) as index:
        for name in STAGES:
            if index.db.execute('SELECT 1 FROM checkpoints WHERE name=?', (name,)).fetchone():
                try:
                    bundle = export_checkpoint(index, name, Path(destination) / name)
                    result.append({'checkpoint': name, 'manifest': str(Path(destination) / name / 'manifest.json'), 'commit_frame': bundle['metadata']['commit_frame'], 'available': True})
                except Exception as error:
                    result.append({'checkpoint': name, 'available': False, 'error': str(error)})
            else:
                result.append({'checkpoint': name, 'available': False, 'reason': 'no actual committed boundary completion in this raw prefix'})
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    parser.add_argument('--checkpoint', choices=tuple(STAGES), required=True)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    with RawDagIndex(args.source) as index:
        manifest = export_checkpoint(index, args.checkpoint, args.out)
    print(json.dumps({'manifest': str(args.out.resolve() / 'manifest.json'), 'commit_frame': manifest['metadata']['commit_frame']}, ensure_ascii=False))


if __name__ == '__main__':
    main()
