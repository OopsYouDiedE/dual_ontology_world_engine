"""Finite synthetic evidence/file/process checks. Never builds or starts a DV session."""
import copy
import hashlib
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

import aggressive_report as report
import compiled_program as compiled
import matrix_lifecycle as lifecycle
import run_aggressive_matrix as matrix
from binary_evidence import MAGIC, encode, write_frame, RecordWriter, records
from raw_dag import RawDagIndex


def binary_journal(variant='normal'):
    payloads = [{'kind': 'header', 'format': 'dv-raw-dag-v2'}]
    nodes = 0
    def node(items):
        nonlocal nodes
        values = [{'node': node(x)} if isinstance(x, list) else x for x in items]
        identity = nodes
        nodes += 1
        payloads.append({'kind': 'node', 'node_id': identity, 'items': values})
        return identity
    empty = node([])
    payloads.append({'kind': 'checkpoint', 'name': 'initial', 'root': empty, 'events': 0})
    fields = [['id', 'e0'], ['time', {'rational': '1/3'}], ['type', 'host_completion'],
              ['actor', 'a'], ['parents', ['e-before']], ['data', [['action_event', 'action0']]]]
    if variant == 'duplicates':
        fields = [['id', 'discard'], ['actor', 'first'], [False, 'ignored']] + fields + [['actor', 'last']]
    if variant == 'missing':
        fields = [['id', 'e0']]
    if variant == 'nested':
        fields[1] = ['time', [1, 2]]
        fields[2] = ['type', ['unusual']]
        fields[3] = ['actor', False]
    if variant == 'invalid-rational':
        fields[1] = ['time', {'rational': '2/6'}]
    root = node(fields)
    payloads.append({'kind': 'event', 'root': root, 'ordinal': 0, 'event_id': 'e0', 'process_elapsed_ms': 12.5})
    if variant == 'bad-id':
        payloads[-1]['event_id'] = 'other'
    if variant == 'negative-elapsed':
        payloads[-1]['process_elapsed_ms'] = -1
    if variant == 'duplicate-event':
        payloads.append({'kind': 'event', 'root': root, 'ordinal': 1, 'event_id': 'e0'})
    payloads += [{'kind': 'checkpoint', 'name': 'final', 'root': root, 'events': 1},
                 {'kind': 'seal', 'events': 1, 'nodes': nodes, 'final_checkpoint': True}]
    if variant == 'bad-seal':
        payloads[-1]['nodes'] += 1
    if variant == 'forward-ref':
        payloads[1]['items'] = [{'node': 999}]
    if variant == 'after-seal':
        payloads.append({'kind': 'node', 'node_id': nodes, 'items': []})
    out, previous = io.BytesIO(), '0' * 64
    out.write(MAGIC)
    for sequence, payload in enumerate(payloads):
        checksum = hashlib.sha256(previous.encode() + b'\n' + str(sequence).encode() + b'\n' + encode(payload)).hexdigest()
        write_frame(out, {'seq': sequence, 'prev': previous, 'payload': payload,
                          'sha256': 'f' * 64 if variant == 'bad-chain' and sequence == len(payloads) - 1 else checksum})
        previous = checksum
    data = out.getvalue()
    if variant == 'truncated':
        data = data[:-7]
    if variant == 'frame-checksum':
        data = data[:-1] + bytes([data[-1] ^ 1])
    return data


class FiniteTools(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='finite-tool-hardening-')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def test_raw_dag_on_off_exact(self):
        valid = ('normal', 'duplicates', 'missing', 'nested')
        invalid = ('invalid-rational', 'bad-id', 'negative-elapsed', 'duplicate-event', 'bad-seal',
                   'forward-ref', 'after-seal', 'bad-chain', 'truncated', 'frame-checksum')
        for variant in valid + invalid:
            with self.subTest(variant=variant):
                source = self.root / (variant + '.dvdag')
                source.write_bytes(binary_journal(variant))
                values = []
                for optimize in (False, True):
                    with RawDagIndex(source, optimize=optimize) as index:
                        destination = self.root / (variant + str(optimize) + '.dvlog')
                        index.write_event_index(destination)
                        selected = io.BytesIO()
                        if index.summary['complete_sealing']:
                            index.write_binary(index.select(event='e0'), selected)
                        values.append((index.summary, destination.read_bytes(), selected.getvalue()))
                        if variant == 'normal':
                            event = next(records(destination))
                            self.assertEqual(event['parent_ids'], ['e-before'])
                            self.assertEqual(event['host_action_event'], 'action0')
                self.assertEqual(values[0], values[1])
                self.assertEqual(values[0][0]['complete_sealing'], variant in valid)

    def program(self):
        source = self.root / 'old' / 'program'
        binary = source / compiled.BINARY_ROOT
        binary.mkdir(parents=True)
        for name in ('Program.dll', 'Program.deps.json', 'Program.runtimeconfig.json'):
            (binary / name).write_bytes(name.encode())
        (source / 'Runtime.cs').write_text('runtime', encoding='utf-8')
        (source / 'Program.csproj').write_text('project', encoding='utf-8')
        (source / 'source-manifest.json').write_text(json.dumps({
            'modules': [{'path': 'entry.dv', 'sha256': 'entry'}],
            'profile_instrumented': False, 'compiler': 'compiler'}), encoding='utf-8')
        hashes, normalized = {'seed.cs': 'seed', 'entry.dv': 'entry'}, {'entry.dv': 'entry'}
        value = compiled.capture(source, hashes, False, normalized, 'compiler')
        (source.parent / 'compiled-program.json').write_text(json.dumps(value), encoding='utf-8')
        return source, value, hashes, normalized

    def test_reuse_rejects_incomplete_or_changed_inputs(self):
        source, value, hashes, normalized = self.program()
        for alteration in ('empty', 'omitted', 'profile', 'schema', 'seed', 'restore-entry', 'runtime-metadata'):
            with self.subTest(alteration=alteration):
                candidate = copy.deepcopy(value)
                if alteration == 'empty': candidate['binaries'] = {}
                if alteration == 'omitted': candidate['binaries'].pop(compiled.BINARY_ROOT + 'Program.dll')
                if alteration == 'profile': candidate['instrumented'] = True
                if alteration == 'schema': candidate['schema'] = 'dv-compiled-program-v1'
                if alteration == 'seed': candidate['source_hashes']['seed.cs'] = 'other'
                if alteration == 'restore-entry': candidate['entry_kind'] = 'checkpoint-resume'
                if alteration == 'runtime-metadata': candidate['runtime_switch_contract'] = {}
                with self.assertRaises(ValueError):
                    compiled.verify(source, candidate, hashes, False, normalized, 'compiler')
        with self.assertRaises(ValueError):
            compiled.verify(source, value, hashes, False, normalized, 'other-compiler')
        extra = source / compiled.BINARY_ROOT / 'unlisted.dll'
        extra.write_bytes(b'extra')
        with self.assertRaises(ValueError):
            compiled.verify(source, value, hashes, False, normalized, 'compiler')

    def test_reuse_copied_bytes_reverified(self):
        source, value, hashes, normalized = self.program()
        target = self.root / 'new' / 'program'
        compiled.copy_verified(source, target, hashes, False, normalized, 'compiler')
        self.assertEqual(compiled.files(target, True), value['binaries'])
        off = compiled.capture(target, hashes, False, normalized, 'compiler', runtime_config={'opt_runtimespeed': False})
        self.assertEqual(off['binaries'], value['binaries'])
        self.assertFalse(off['runtime_config_at_capture']['opt_runtimespeed'])
        original = compiled.shutil.copy2
        def corrupt(src, dst):
            result = original(src, dst)
            if Path(dst).name == 'Program.dll': Path(dst).write_bytes(b'changed during copy')
            return result
        with patch.object(compiled.shutil, 'copy2', side_effect=corrupt):
            with self.assertRaises(ValueError):
                compiled.copy_verified(source, self.root / 'raced' / 'program', hashes, False, normalized, 'compiler')

    def test_generator_profile_and_generated_sources_are_bound(self):
        source, value, hashes, normalized = self.program()
        manifest = source / 'source-manifest.json'
        original = json.loads(manifest.read_text())
        original['profile_instrumented'] = True
        manifest.write_text(json.dumps(original), encoding='utf-8')
        # Even rehashing the generated files cannot conceal a profile mismatch.
        value['program_sources'] = compiled.files(source, False)
        with self.assertRaises(ValueError):
            compiled.verify(source, value, hashes, False, normalized, 'compiler')

    def test_full_compiler_inputs_include_seed(self):
        names = {path.relative_to(compiled.LANGUAGE).as_posix() for path in compiled.compiler_inputs()}
        self.assertIn('compiler/seed/Program.cs', names)
        self.assertIn('compiler/seed/Seed.csproj', names)

    def report_fixture(self):
        directory = self.root / 'report'
        directory.mkdir()
        result = {'evidence_complete': True, 'execution_elapsed_seconds': 1,
                  'build_source_consistent': True, 'source_unchanged': True,
                  'run_config_matches': True, 'raw_sealing_complete': True,
                  'run_config': {'detective_zone': 'zone_yi'}}
        action = 'detective.c2.daily.closing.central_hall'
        completion = {'host_event': 'e1', 'execution_event': 'e2', 'action_event': 'action0',
                      'actor': 'detective', 'status': 'applied'}
        candidate = {'viewer': 'detective', 'action': action, 'execution_event': 'e2', 'completion': completion}
        events = [{'event_id': 'e1', 'type': 'host_completion', 'actor': 'detective', 'parents': ['e0']},
                  {'event_id': 'e2', 'type': 'action_execution', 'actor': 'detective', 'parents': ['e1'],
                   'action_id': action, 'action_kind': 'inspect', 'detail': {'status': 'applied'}}]
        index = [{'event_id': 'e1', 'type': 'host_completion', 'actor': 'detective', 'ordinal': 1,
                  'parent_ids': ['e0'], 'host_action_event': 'action0'},
                 {'event_id': 'e2', 'type': 'action_execution', 'actor': 'detective', 'ordinal': 2,
                  'parent_ids': ['e1'], 'game_time': 10, 'process_elapsed_ms': 1000}]
        return directory, result, candidate, events, index

    def test_closing_report_requires_actual_receipt_and_true_state(self):
        directory, result, candidate, events, index = self.report_fixture()
        for variant in ('valid', 'unknown-state', 'false-state', 'missing-index', 'bad-completion', 'wrong-actor', 'wrong-host-action', 'wrong-parent'):
            with self.subTest(variant=variant):
                claim, indexed = copy.deepcopy(candidate), copy.deepcopy(index)
                cell = {'known': True, 'conflicted': False, 'value': True}
                if variant == 'unknown-state': cell['known'] = False
                if variant == 'false-state': cell['value'] = False
                if variant == 'missing-index': indexed = []
                if variant == 'bad-completion': claim['completion'] = False
                if variant == 'wrong-actor': claim['completion']['actor'] = 'other'
                if variant == 'wrong-host-action': indexed[0]['host_action_event'] = 'action-other'
                if variant == 'wrong-parent': indexed[1]['parent_ids'] = ['e-other']
                for filename in ('result.dvlog', 'stdout.dvlog', 'raw-event-index.dvlog'):
                    (directory / filename).unlink(missing_ok=True)
                with RecordWriter(directory / 'result.dvlog') as output: output.append(result)
                with RecordWriter(directory / 'stdout.dvlog') as output:
                    output.append({'type': 'debug_events', 'batch': {'events': events}})
                    output.append({'type': 'debug_end', 'stop_evidence': {
                        'chapter_summary': {'council_closed': cell}, 'closing_observation': claim}})
                with RecordWriter(directory / 'raw-event-index.dvlog') as output:
                    for event in indexed: output.append(event)
                (directory / 'source-hashes.json').write_text(json.dumps({'35-campaign-schema.dv': 'schema'}), encoding='utf-8')
                summary = report.summarize(directory, self.root)
                self.assertEqual(summary['speed_target_met'], variant == 'valid')
                self.assertIn('35-campaign-schema.dv', summary['module_hashes_20_60'])
                self.assertNotIn('????', report.markdown({'scenarios': [summary]}))

    def test_registered_flags_are_available_without_starting_a_session(self):
        tool = Path(__file__).with_name('run_debug.py')
        result = subprocess.run([sys.executable, '-B', str(tool), '--help'],
                                capture_output=True, text=True, timeout=10)
        self.assertEqual(result.returncode, 0, result.stderr)
        for flag in lifecycle.FLAGS + lifecycle.TOOL_FLAGS:
            self.assertIn('--opt-' + flag, result.stdout)

    def test_matrix_windows_and_baseline_policy(self):
        options = matrix.production_options('baseline')
        self.assertEqual(len([x for x in options if x.startswith('--opt-')]), len(lifecycle.FLAGS + lifecycle.TOOL_FLAGS))
        self.assertIn('--opt-replaycache', options)
        self.assertIn('--opt-runtimespeed', options)
        self.assertEqual(options[:2], ['--selective-planning', 'on'])
        self.assertEqual(matrix.production_options('precise-baseline')[:2], ['--selective-planning', 'off'])
        command = matrix.case_command(Path('case'), 'Standard', 'zone_yi', options, None, 300, 1)
        self.assertEqual(command[command.index('--timeout') + 1], '300')
        with self.assertRaises(ValueError): lifecycle.strict_destination(self.root, '../outside')
        with self.assertRaises(ValueError): lifecycle.strict_destination(self.root, 'unknown')
        with self.assertRaises(ValueError): lifecycle.clean_case(self.root, 'S1', self.root / 'S1' / 'program')

    def test_matrix_failure_retains_lock(self):
        output = self.root / 'artifacts/perf-opt/aggressive'
        with patch.object(matrix, 'OUTPUT', output), patch.object(sys, 'argv', ['matrix', '--case', 'S1']), \
                patch.object(matrix, 'run_case', side_effect=lifecycle.ProcessTreeError('finite failure')):
            with self.assertRaises(lifecycle.ProcessTreeError): matrix.main()
            lock = output / 'matrix-running.lock'
            self.assertEqual(json.loads(lock.read_text())['phase'], 'failed-lock-retained')
            with self.assertRaises(FileExistsError): matrix.main()

    @unittest.skipUnless(os.name == 'nt', 'native Windows lifecycle')
    def test_windows_process_tree_timeout(self):
        marker = self.root / 'child-pid.txt'
        script = "import subprocess,sys,time;from pathlib import Path;p=subprocess.Popen([sys.executable,'-c','import time;time.sleep(60)']);Path(sys.argv[1]).write_text(str(p.pid));time.sleep(60)"
        with (self.root / 'process.log').open('wb') as log:
            with self.assertRaises(lifecycle.ProcessTreeError):
                lifecycle.managed_run([sys.executable, '-c', script, str(marker)], cwd=self.root, stdout=log, timeout=1)
        self.assertTrue(marker.is_file())
        import ctypes
        pid = int(marker.read_text())
        kernel = ctypes.WinDLL('kernel32', use_last_error=True)
        kernel.OpenProcess.restype = ctypes.c_void_p
        handle = kernel.OpenProcess(0x1000, False, pid)
        if handle:
            try:
                code = ctypes.c_ulong()
                kernel.GetExitCodeProcess.argtypes = [ctypes.c_void_p, ctypes.c_void_p]
                self.assertTrue(kernel.GetExitCodeProcess(handle, ctypes.byref(code)))
                self.assertNotEqual(code.value, 259)  # STILL_ACTIVE
            finally:
                kernel.CloseHandle.argtypes = [ctypes.c_void_p]
                kernel.CloseHandle(handle)

    @unittest.skipUnless(os.name == 'nt', 'native Windows lifecycle')
    def test_reader_lease_blocks_cleanup_then_native_cleanup(self):
        output = self.root / 'artifacts/perf-opt/aggressive'
        destination = output / 'S1'
        destination.mkdir(parents=True)
        (destination / 'old.txt').write_text('old', encoding='utf-8')
        script = "import sys,time;from pathlib import Path;sys.path.insert(0,sys.argv[1]);from matrix_lifecycle import case_guard;g=case_guard(Path(sys.argv[2]));g.__enter__();print('ready',flush=True);time.sleep(1);g.__exit__(None,None,None)"
        process = subprocess.Popen([sys.executable, '-c', script, str(Path(lifecycle.__file__).parent), str(destination)],
                                   stdout=subprocess.PIPE, text=True)
        try:
            self.assertEqual(process.stdout.readline().strip(), 'ready')
            with self.assertRaises(TimeoutError):
                with lifecycle.case_guard(destination, exclusive=True, timeout=0.1): pass
            self.assertTrue((destination / 'old.txt').is_file())
            process.wait(timeout=5)
            lifecycle.clean_case(output, 'S1', timeout=10)
            self.assertFalse(destination.exists())
        finally:
            if process.poll() is None: process.kill()
            process.wait()
            process.stdout.close()


if __name__ == '__main__':
    unittest.main()
