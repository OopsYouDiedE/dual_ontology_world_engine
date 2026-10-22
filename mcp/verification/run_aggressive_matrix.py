"""Run a serial, explicitly authorized matrix; does not authorize sessions by itself."""
import argparse
import hashlib
import json
import os
import sys
import time
from pathlib import Path

from binary_evidence import records
from aggressive_report import record, summarize
from matrix_lifecycle import FLAGS, TOOL_FLAGS, CASE_NAMES, clean_case, managed_run

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'artifacts' / 'perf-opt' / 'aggressive'
OPTIMIZATIONS = FLAGS + TOOL_FLAGS
CASES = tuple(zip(CASE_NAMES, ('Standard',) * 5 + ('Foolish', 'Standard'),
                  ('zone_yi', 'zone_jia', 'zone_bing', 'zone_ding', 'none', 'zone_yi', 'zone_yi')))


def publish(path, value):
    temporary = path.with_suffix(path.suffix + '.partial')
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')
    temporary.replace(path)


def production_options(mode):
    if mode not in ('baseline', 'precise-baseline'):
        return ['--selective-planning', 'on']
    result = ['--selective-planning', 'off' if mode == 'precise-baseline' else 'on']
    for name in OPTIMIZATIONS:
        result += ['--opt-' + name, 'off']
    return result


def case_command(destination, profile, zone, options, reuse, timeout, causal_timeout):
    command = [sys.executable, str(ROOT / 'mcp/verification/run_debug.py'),
               '--out', str(destination), '--timeout', str(timeout),
               '--detective-profile', profile, '--detective-zone', zone,
               '--causal-timeout', str(causal_timeout), *options]
    if reuse:
        command += ['--reuse-program', str(reuse)]
    return command


def run_case(name, profile, zone, options, reuse, causal_timeout, *, timeout=300,
             launcher_timeout=1320, cleanup_wait=300, lock=None):
    case_started = time.monotonic()
    destination = clean_case(OUTPUT, name, reuse, timeout=cleanup_wait)
    cleanup_seconds = time.monotonic() - case_started
    command = case_command(destination, profile, zone, options, reuse, timeout, causal_timeout)
    state = {'phase': 'running', 'scenario': name, 'matrix_pid': os.getpid(),
             'command': command, 'session_limit_seconds': timeout, 'parallel_sessions': 1}
    publish(OUTPUT / 'matrix-state.json', state)
    def launched(pid):
        state['owned_gate_pid'] = pid
        publish(OUTPUT / 'matrix-state.json', state)
        if lock:
            publish(lock, state)
    started = time.monotonic()
    with (OUTPUT / (name + '-launcher.txt')).open('wb') as log:
        exit_code = managed_run(command, cwd=ROOT, stdout=log, timeout=launcher_timeout, on_start=launched)
    command_elapsed = time.monotonic() - started
    item = summarize(destination, OUTPUT)
    item.update(launcher_exit=exit_code, total_wall_seconds=time.monotonic() - case_started,
                command_wall_seconds=command_elapsed, cleanup_seconds=cleanup_seconds,
                result_sha256=hashlib.sha256((destination / 'result.dvlog').read_bytes()).hexdigest() if (destination / 'result.dvlog').is_file() else None,
                command=command, independently_reviewed=False,
                session_limit_seconds=timeout,
                optimization_flags=list(OPTIMIZATIONS),
                comparison_conditions=f'{len(OPTIMIZATIONS)}-registered-flags-off-selective' if name.startswith('baseline-') else
                                      f'{len(OPTIMIZATIONS)}-registered-flags-off-precise-separate-policy' if name.startswith('precise-') else
                                      'all-on-or-single-ablation-selective')
    publish(OUTPUT / (name + '-measurement.json'), item)
    publish(OUTPUT / 'matrix-state.json', {'phase': 'case-complete', 'scenario': name,
                                          'execution_seconds': item.get('execution_seconds'),
                                          'evidence_complete': item.get('evidence_complete'),
                                          'launcher_exit': exit_code})
    print(json.dumps({key: item.get(key) for key in (
        'scenario', 'execution_seconds', 'timeout', 'evidence_complete', 'end_game_time',
        'source_consistent', 'source_unchanged', 'closing_observation', 'launcher_exit')},
        ensure_ascii=False), flush=True)
    return item


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--mode', choices=('validation', 'ablations', 'baseline', 'precise-baseline'), default='validation')
    parser.add_argument('--case', choices=CASE_NAMES)
    parser.add_argument('--reuse-program', type=Path)
    parser.add_argument('--timeout', type=int, default=300, help='Authorized session process window, excluding compilation and postprocessing')
    parser.add_argument('--launcher-timeout', type=int, help='Outer owned process-tree deadline; failure retains the matrix lock')
    parser.add_argument('--cleanup-wait', type=int, default=300, help='Wait for prior case processes and registered readers before cleanup')
    parser.add_argument('--causal-timeout', type=int, default=1,
                        help='Independent offline checker budget; timeout remains explicitly incomplete')
    args = parser.parse_args()
    if min(args.timeout, args.causal_timeout, args.cleanup_wait) < 1 or (args.launcher_timeout is not None and args.launcher_timeout < 1):
        parser.error('All windows must be positive')
    launcher_timeout = args.launcher_timeout or max(900, args.timeout + args.causal_timeout + 1020)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    lock = OUTPUT / 'matrix-running.lock'
    with lock.open('x', encoding='utf-8') as stream:
        json.dump({'phase': 'starting', 'matrix_pid': os.getpid(), 'mode': args.mode}, stream)
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
                               production_options('ablations') + ['--opt-' + option, 'off']) for option in OPTIMIZATIONS]
        else:
            selected = [case for case in CASES if args.case is None or case[0] == args.case]
            prefix = 'baseline-' if args.mode == 'baseline' else 'precise-' if args.mode == 'precise-baseline' else ''
            specifications = [(prefix + name, profile, zone, production_options(args.mode))
                              for name, profile, zone in selected]
        for name, profile, zone, options in specifications:
            item = run_case(name, profile, zone, options, reuse, args.causal_timeout,
                            timeout=args.timeout, launcher_timeout=launcher_timeout,
                            cleanup_wait=args.cleanup_wait, lock=lock)
            results.append(item)
            publish(OUTPUT / (args.mode + '-measurements.json'), results)
            if item.get('source_consistent') is False or item.get('source_unchanged') is False:
                raise RuntimeError('Sources changed; stop before mixing model versions')
            if reuse is None and (OUTPUT / name / 'compiled-program.json').is_file():
                reuse = OUTPUT / name / 'program'
        publish(OUTPUT / 'matrix-state.json', {'phase': 'complete', 'mode': args.mode,
                                              'scenarios': [item['scenario'] for item in results]})
    except BaseException as error:
        # A timeout, interrupt, reader/cleanup failure or unknown exit state must
        # never turn into permission to start another matrix concurrently.
        failure = {'phase': 'failed-lock-retained', 'matrix_pid': os.getpid(),
                   'error': type(error).__name__ + ': ' + str(error),
                   'recovery': 'Inspect case processes/readers and partial evidence before manually removing this lock.'}
        publish(lock, failure)
        publish(OUTPUT / 'matrix-state.json', failure)
        raise
    else:
        lock.unlink()


if __name__ == '__main__':
    main()
