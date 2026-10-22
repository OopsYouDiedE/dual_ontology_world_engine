"""Finite tool fixtures; fake compiler outputs, no build or formal session."""
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import compiled_program as compiled
import run_debug as runner


class CausalBuildCache(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='causal-cache-')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.language, self.verification = self.root / 'language', self.root / 'verification'
        runtime = self.language / 'compiler/Runtime.cs'
        bridge = self.verification / 'CausalCheckBridge.cs'
        entry = self.language / 'checks/70-checks.dv'
        for path in (runtime, bridge, entry):
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(path.name, encoding='utf-8')
        self.hashes = {str(p): compiled.digest(p) for p in (runtime, bridge, entry)}
        self.normalized = {str(entry): self.hashes[str(entry)]}
        self.calls = []
        self.stack = [patch.object(runner, 'LANGUAGE', self.language),
                      patch.object(runner, 'VERIFICATION', self.verification),
                      patch.object(runner, 'compiler_fingerprint', return_value='compiler'),
                      patch.object(runner.subprocess, 'run', side_effect=self.fake_run)]
        for context in self.stack:
            context.start()
            self.addCleanup(context.stop)

    def fake_run(self, command, **kwargs):
        self.calls.append(command)
        if '--out' in command:
            target = Path(command[command.index('--out') + 1])
            (target / 'source-manifest.json').write_text(json.dumps({
                'modules': [{'path': p, 'sha256': h} for p, h in self.normalized.items()],
                'profile_instrumented': False, 'compiler': 'compiler'}), encoding='utf-8')
            (target / 'DVProgram.cs').write_text('generated', encoding='utf-8')
        elif 'build' in command:
            target = Path(command[2]).parent / compiled.BINARY_ROOT
            target.mkdir(parents=True)
            for name in ('Program.dll', 'Program.deps.json', 'Program.runtimeconfig.json'):
                (target / name).write_bytes(name.encode())
        else:
            raise subprocess.TimeoutExpired(command, kwargs['timeout'])
        return subprocess.CompletedProcess(command, 0)

    def invoke(self, name, reuse=None, enabled='on'):
        out = self.root / name
        out.mkdir()
        (out / 'source-hashes.json').write_text(json.dumps(self.hashes), encoding='utf-8')
        result = {'raw_sha256': 'raw', 'raw_path': 'raw.dvdag', 'run_config': {}}
        with self.assertRaises(subprocess.TimeoutExpired):
            runner.causal_check(out, result, self.hashes, self.normalized, {}, 1,
                                causalbuildcache=enabled, reuse_program=reuse)
        self.assertTrue(result['causal_stages']['build_complete'])
        self.assertEqual(result['causal_stages']['timeout_stage'], 'execution')
        self.assertIn('execution_elapsed_seconds', result['causal_stages'])
        return out, result

    def test_reuse_still_executes_and_records_timeout(self):
        first, built = self.invoke('first')
        self.assertEqual(len(self.calls), 3)
        second, reused = self.invoke('second', first / 'program')
        self.assertEqual(len(self.calls), 4)  # checker executes; neither build command runs
        self.assertEqual(reused['causal_stages']['build_mode'], 'verified-reuse')
        self.assertEqual(compiled.files(first / 'causal-program', True),
                         compiled.files(second / 'causal-program', True))
        self.invoke('disabled', first / 'program', 'off')
        self.assertEqual(len(self.calls), 7)

    def test_missing_manifest_builds_and_wrong_entry_rejects(self):
        first, _ = self.invoke('first', self.root / 'unknown/program')
        path = first / 'compiled-causal-program.json'
        value = json.loads(path.read_text())
        value['entry_kind'] = 'fresh-debug'
        path.write_text(json.dumps(value), encoding='utf-8')
        out = self.root / 'rejected'
        out.mkdir()
        result = {}
        with self.assertRaises(ValueError):
            runner.causal_check(out, result, self.hashes, self.normalized, {}, 1,
                                reuse_program=first / 'program')
        self.assertEqual(len(self.calls), 3)
        self.assertEqual(result['causal_stages']['build_mode'], 'reuse-validation')

    def test_changed_inputs_and_binary_reject(self):
        first, _ = self.invoke('first')
        for field in ('hashes', 'binary'):
            with self.subTest(field=field):
                if field == 'hashes':
                    self.hashes['new-source'] = 'changed'
                else:
                    (first / 'causal-program' / compiled.BINARY_ROOT / 'Program.dll').write_bytes(b'tamper')
                out = self.root / field
                out.mkdir()
                with self.assertRaises(ValueError):
                    runner.causal_check(out, {}, self.hashes, self.normalized, {}, 1,
                                        reuse_program=first / 'program')
                self.hashes.pop('new-source', None)
        self.assertEqual(len(self.calls), 3)

    def test_build_timeout_is_not_execution_timeout(self):
        with patch.object(runner.subprocess, 'run', side_effect=subprocess.TimeoutExpired('build', 240)):
            out = self.root / 'timeout'
            out.mkdir()
            result = {}
            with self.assertRaises(subprocess.TimeoutExpired):
                runner.causal_check(out, result, self.hashes, self.normalized, {}, 1)
        self.assertEqual(result['causal_stages']['timeout_stage'], 'dv-generation')
        self.assertIn('build_elapsed_seconds', result['causal_stages'])
        self.assertNotIn('execution_elapsed_seconds', result['causal_stages'])


if __name__ == '__main__':
    unittest.main()
