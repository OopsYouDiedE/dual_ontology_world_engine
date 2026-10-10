"""Completion/crash policy unit regression; does not replace real Godot checks."""
from contextlib import redirect_stdout
import io
from pathlib import Path
import subprocess
import unittest
from unittest.mock import patch
import uuid

import verify


class CompletionTests(unittest.TestCase):
    def test_both_real_summary_formats_and_failure_records(self):
        self.assertTrue(verify.check_completed("flow", "TOTAL 136 checks, 0 failed\n"))
        self.assertTrue(verify.check_completed("flow2", "TOTAL 109 checks, 109 passed, 0 failed\n"))
        self.assertTrue(verify.check_completed("flow", "TOTAL 136 checks, 0 failed\r\n"))
        self.assertTrue(verify.check_completed("flow2", "TOTAL 109 checks, 109 passed, 0 failed\r\n"))
        for output in ("TOTAL 0 checks, 0 failed", "TOTAL 109 checks, 108 passed, 0 failed", "PASS incomplete"):
            self.assertFalse(verify.check_completed("flow2", output))
        failed = "TOTAL 109 checks, 108 passed, 1 failed"
        self.assertTrue(verify.check_completed("flow2", failed))
        self.assertFalse(verify.check_succeeded("flow2", failed))

    def test_save_needs_write_read_and_outer_completion(self):
        complete = "WRITE COMPLETE\nREAD COMPLETE\nSESSION SAVE HEADLESS COMPLETE: 0\n"
        self.assertTrue(verify.check_succeeded("save", complete))
        for missing in ("WRITE COMPLETE", "READ COMPLETE", "SESSION SAVE HEADLESS COMPLETE: 0"):
            self.assertFalse(verify.check_succeeded("save", complete.replace(missing, "")))

    def test_complete_failed_checks_never_retry_or_pass_even_with_zero_exit(self):
        for code in (0, 1):
            ok, _, _, calls = self.run_fixture([subprocess.CompletedProcess([], code, b"TOTAL 136 checks, 2 failed\r\n", b"")])
            self.assertFalse(ok)
            self.assertEqual(calls, 1)

    def run_fixture(self, results):
        directory = verify.ROOT / "tmp" / "core-save-verify-tests" / uuid.uuid4().hex
        with patch.object(verify.subprocess, "run", side_effect=results) as invoked, redirect_stdout(io.StringIO()):
            ok, detail = verify.run("flow", ["fixture"], 1, True, directory)
        return ok, detail, directory, invoked.call_count

    def test_completed_access_violation_uses_output(self):
        for code in (-1073741819, 3221225477):
            with self.subTest(code=code):
                ok, detail, _, calls = self.run_fixture([subprocess.CompletedProcess([], code, b"TOTAL 136 checks, 0 failed\n", b"")])
                self.assertTrue(ok)
                self.assertIn("0xc0000005", detail)
                self.assertEqual(calls, 1)

    def test_other_exit_or_engine_error_still_fails(self):
        for code, stderr in ((1, b""), (0, b"ERROR: fixture engine failure\n"),
                             (-1073741819, b"SCRIPT ERROR: fixture\n")):
            with self.subTest(code=code, stderr=stderr):
                ok, _, _, calls = self.run_fixture([subprocess.CompletedProcess([], code, b"TOTAL 136 checks, 0 failed\n", stderr)])
                self.assertFalse(ok)
                self.assertEqual(calls, 1)

    def test_missing_completion_retries_once_and_preserves_first_bytes(self):
        first = b"PASS incomplete\r\n"
        final = b"TOTAL 136 checks, 0 failed\n"
        ok, _, directory, calls = self.run_fixture([
            subprocess.CompletedProcess([], -1073741819, first, b""),
            subprocess.CompletedProcess([], 0, final, b"")])
        self.assertTrue(ok)
        self.assertEqual(calls, 2)
        self.assertEqual((directory / "flow.stdout.log").read_bytes(), first)
        self.assertEqual((directory / "retry" / "flow.stdout.log").read_bytes(), final)

    def test_second_incomplete_result_fails_even_with_zero_exit(self):
        ok, _, _, calls = self.run_fixture([subprocess.CompletedProcess([], 0, b"PASS incomplete\n", b"")] * 2)
        self.assertFalse(ok)
        self.assertEqual(calls, 2)


if __name__ == "__main__":
    unittest.main()
