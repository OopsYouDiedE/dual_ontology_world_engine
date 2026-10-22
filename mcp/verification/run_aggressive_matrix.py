"""Run the authorized seven-case matrix or switch ablations on one frozen source tree."""
import argparse
import json
import os
import shutil
import subprocess
import sys
import time
from pathlib import Path

from binary_evidence import records
from aggressive_report import record, summarize

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'artifacts' / 'perf-opt' / 'aggressive'
OPTIMIZATIONS = ('readview', 'plancache', 'prune', 'lazytext', 'recordcache', 'predicate',
                 'predicatecache', 'declcache', 'gc', 'appendshare', 'cow', 'mask',
                 'lazyinternal', 'dirty', 'planreuse', 'blackbox')
CASES = (
    ('S1', 'Standard', 'zone_yi'),
    ('S2-jia', 'Standard', 'zone_jia'),
    ('S2-bing', 'Standard', 'zone_bing'),
    ('S2-ding', 'Standard', 'zone_ding'),
    ('S3-none', 'Standard', 'none'),
    ('S4-foolish', 'Foolish', 'zone_yi'),
    ('S1-repeat', 'Standard', 'zone_yi'),
)


def publish(path, value):
    temporary = path.with_suffix(path.suffix + '.partial')
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')
    temporary.replace(path)


def run_case(name, profile, zone, options, reuse, causal_timeout):
    destination = (OUTPUT / name).resolve()
    if destination.parent != OUTPUT.resolve() or destination.name not in {
            *(case[0] for case in CASES), *('baseline-' + case[0] for case in CASES),
            *('ablation-' + option for option in OPTIMIZATIONS)}:
        raise ValueError('Output is not an authorized fixed scenario directory')
    if destination.exists():
        shutil.rmtree(destination)
    command = [sys.executable, str(ROOT / 'mcp/verification/run_debug.py'),
               '--out', str(destination), '--timeout', '180',
               '--detective-profile', profile, '--detective-zone', zone,
               '--causal-timeout', str(causal_timeout), *options]
    if reuse:
        command += ['--reuse-program', str(reuse)]
    state = {'phase': 'running', 'scenario': name, 'pid': os.getpid(),
             'command': command, 'session_limit_seconds': 180, 'parallel_sessions': 1}
    publish(OUTPUT / 'matrix-state.json', state)
    started = time.monotonic()
    with (OUTPUT / (name + '-launcher.txt')).open('wb') as log:
        process = subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, timeout=900)
    item = summarize(destination, OUTPUT)
    item.update(launcher_exit=process.returncode, total_wall_seconds=time.monotonic() - started,
                command=command, independently_reviewed=False)
    publish(OUTPUT / (name + '-measurement.json'), item)
    publish(OUTPUT / 'matrix-state.json', {'phase': 'case-complete', 'scenario': name,
                                          'execution_seconds': item.get('execution_seconds'),
                                          'evidence_complete': item.get('evidence_complete'),
                                          'launcher_exit': process.returncode})
    print(json.dumps({key: item.get(key) for key in (
        'scenario', 'execution_seconds', 'timeout', 'evidence_complete', 'end_game_time',
        'source_consistent', 'source_unchanged', 'closing_observation', 'launcher_exit')},
        ensure_ascii=False), flush=True)
    return item


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--mode', choices=('validation', 'ablations', 'baseline'), default='validation')
    parser.add_argument('--case', choices=tuple(case[0] for case in CASES))
    parser.add_argument('--reuse-program', type=Path)
    parser.add_argument('--causal-timeout', type=int, default=1,
                        help='Independent offline checker budget; timeout remains explicitly incomplete')
    args = parser.parse_args()
    OUTPUT.mkdir(parents=True, exist_ok=True)
    lock = OUTPUT / 'matrix-running.lock'
    with lock.open('x', encoding='utf-8') as stream:
        stream.write(str(os.getpid()))
    try:
        results = []
        reuse = args.reuse_program.resolve() if args.reuse_program else None
        if args.mode == 'ablations':
            anchor = OUTPUT / 'S1'
            anchor_result = record(next(records(anchor / 'result.dvlog')))
            if not anchor_result.get('evidence_complete'):
                raise ValueError('Ablations require a complete all-on S1 anchor')
            reuse = reuse or anchor / 'program'
            specifications = [('ablation-' + option, 'Standard', 'zone_yi',
                               ['--opt-' + option, 'off']) for option in OPTIMIZATIONS]
        else:
            selected = [case for case in CASES if args.case is None or case[0] == args.case]
            options = []
            prefix = ''
            if args.mode == 'baseline':
                prefix = 'baseline-'
                options = ['--selective-planning', 'off']
                for name in OPTIMIZATIONS:
                    options += ['--opt-' + name, 'off']
            specifications = [(prefix + name, profile, zone, options) for name, profile, zone in selected]
        for name, profile, zone, options in specifications:
            item = run_case(name, profile, zone, options, reuse, args.causal_timeout)
            results.append(item)
            publish(OUTPUT / (args.mode + '-measurements.json'), results)
            if item.get('source_consistent') is False or item.get('source_unchanged') is False:
                raise RuntimeError('Sources changed; stop before mixing model versions')
            if reuse is None and (OUTPUT / name / 'compiled-program.json').is_file():
                reuse = OUTPUT / name / 'program'
        publish(OUTPUT / 'matrix-state.json', {'phase': 'complete', 'mode': args.mode,
                                              'scenarios': [item['scenario'] for item in results]})
    finally:
        lock.unlink(missing_ok=True)


if __name__ == '__main__':
    main()
