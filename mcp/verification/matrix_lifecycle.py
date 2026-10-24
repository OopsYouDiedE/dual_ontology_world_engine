"""OS leases, bounded cleanup and process-tree ownership for the fixed matrix task."""
from contextlib import contextmanager
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import threading
import time

CASE_NAMES = ('S1', 'S2-jia', 'S2-bing', 'S2-ding', 'S3-none', 'S4-foolish', 'S1-repeat')
FLAGS = ('readview', 'plancache', 'lazytext', 'recordcache', 'predicate',
         'predicatecache', 'declcache', 'gc', 'appendshare', 'cow', 'mask',
         'lazyinternal', 'dirty', 'blackbox', 'replaycache', 'runtimespeed',
         'storyflow', 'stateindex')
FLAG_DEFAULTS = {name: 'on' for name in FLAGS}
TOOL_FLAGS = ('evidence-index', 'evidence-stream', 'causal-index', 'causalbuildcache')
ALLOWED = {*CASE_NAMES, *('baseline-' + x for x in CASE_NAMES),
           *('ablation-' + x for x in FLAGS + TOOL_FLAGS)}
_held = {}


def case_location(path):
    if path is None:
        return None
    path = Path(path).resolve()
    for ancestor in (path, *path.parents):
        if (ancestor.name == 'aggressive' and ancestor.parent.name == 'perf-opt'
                and ancestor.parent.parent.name == 'artifacts'):
            parts = path.relative_to(ancestor).parts
            if parts and parts[0] in ALLOWED:
                return ancestor, parts[0]
    return None


@contextmanager
def case_guard(path, exclusive=False, timeout=300):
    location = case_location(path)
    if location is None:
        yield
        return
    root, name = location
    guards = root / '.matrix-guards'
    guards.mkdir(parents=True, exist_ok=True)
    lock = guards / (name + '.lock')
    key = (str(lock.resolve()), threading.get_ident())
    if key in _held:
        if exclusive and not _held[key][1]:
            raise RuntimeError('Cannot upgrade an active evidence reader to cleanup')
        yield
        return
    stream = lock.open('a+b')
    stream.seek(0, 2)
    if not stream.tell():
        stream.write(b'0')
        stream.flush()
    started = time.monotonic()
    try:
        while True:
            try:
                stream.seek(0)
                if os.name == 'nt':
                    import msvcrt
                    # Windows byte locks are exclusive: same-case readers are
                    # serialized, and cleanup waits for their complete lifetime.
                    msvcrt.locking(stream.fileno(), msvcrt.LK_NBLCK, 1)
                else:
                    import fcntl
                    fcntl.flock(stream, (fcntl.LOCK_EX if exclusive else fcntl.LOCK_SH) | fcntl.LOCK_NB)
                break
            except OSError:
                if time.monotonic() - started >= timeout:
                    raise TimeoutError('Evidence runner/reader lease still active: ' + name)
                time.sleep(0.1)
        _held[key] = (stream, exclusive)
        try:
            yield
        finally:
            del _held[key]
            stream.seek(0)
            if os.name == 'nt':
                import msvcrt
                msvcrt.locking(stream.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                import fcntl
                fcntl.flock(stream, fcntl.LOCK_UN)
    finally:
        stream.close()


def strict_destination(root, name):
    root = Path(root).resolve()
    destination = root / name
    if (name not in ALLOWED or destination.resolve().parent != root or destination.is_symlink()
            or destination.name != name):
        raise ValueError('Output is not an authorized fixed scenario directory')
    return destination


def case_processes(destination):
    if os.name != 'nt':
        raise RuntimeError('Legacy process inventory requires Windows; refusing unverified cleanup')
    command = '[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false); Get-CimInstance Win32_Process | Select-Object ProcessId,CommandLine | ConvertTo-Json -Compress'
    result = subprocess.run(['powershell', '-NoProfile', '-NonInteractive', '-Command', command],
                            check=True, capture_output=True, text=True, encoding='utf-8', timeout=30)
    rows = json.loads(result.stdout or '[]')
    if isinstance(rows, dict):
        rows = [rows]
    needle = str(Path(destination).resolve()).replace('/', '\\').casefold()
    matches = []
    for row in rows:
        text = (row.get('CommandLine') or '').replace('/', '\\').casefold()
        start = text.find(needle)
        end = start + len(needle)
        if (row['ProcessId'] != os.getpid() and start >= 0 and
                (end == len(text) or text[end] in '\\"\' \t')):
            matches.append(row['ProcessId'])
    return matches


def clean_case(root, name, reuse=None, timeout=300):
    destination = strict_destination(root, name)
    if reuse is not None and Path(reuse).resolve().is_relative_to(destination.resolve()):
        raise ValueError('Cleanup would delete the reusable program')
    started = time.monotonic()
    while True:
        while case_processes(destination):
            if time.monotonic() - started >= timeout:
                raise TimeoutError('Prior case runner/reader processes have not exited: ' + name)
            time.sleep(0.2)
        remaining = max(0, timeout - (time.monotonic() - started))
        with case_guard(destination, exclusive=True, timeout=remaining):
            # A newly arriving registered reader may be waiting on this guard.
            # Release and retry rather than holding cleanup while waiting for it.
            if case_processes(destination):
                if time.monotonic() - started >= timeout:
                    raise TimeoutError('Case process appeared before cleanup: ' + name)
                continue
            if destination.exists():
                # One native shell validates and deletes. No enumerated paths are
                # passed to cmd/batch or another shell for filesystem operations.
                script = r'''
    $ErrorActionPreference = 'Stop'
    $taskRoot = [IO.Path]::GetFullPath($env:DV_MATRIX_CLEAN_ROOT)
    $taskTarget = [IO.Path]::GetFullPath($env:DV_MATRIX_CLEAN_TARGET)
    if ([IO.Directory]::GetParent($taskTarget).FullName -ne $taskRoot) { throw 'Cleanup target escaped task root' }
    $taskItem = Get-Item -LiteralPath $taskTarget -Force
    if ($taskItem.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Cleanup target is a reparse point' }
    if (Get-ChildItem -LiteralPath $taskTarget -Force -Recurse | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw 'Cleanup subtree contains a reparse point' }
    Remove-Item -LiteralPath $taskTarget -Recurse -Force
    '''
                env = os.environ.copy()
                env.update(DV_MATRIX_CLEAN_ROOT=str(Path(root).resolve()),
                           DV_MATRIX_CLEAN_TARGET=str(destination.resolve()))
                subprocess.run(['powershell', '-NoProfile', '-NonInteractive', '-Command', script],
                               env=env, check=True, timeout=timeout)
            return destination


class ProcessTreeError(RuntimeError):
    pass


class WindowsJob:
    def __init__(self):
        import ctypes as c
        from ctypes import wintypes as w
        self.c, self.w = c, w
        self.api = c.WinDLL('kernel32', use_last_error=True)
        class Basic(c.Structure):
            _fields_ = [('process_time', c.c_longlong), ('job_time', c.c_longlong),
                        ('flags', w.DWORD), ('minimum', c.c_size_t), ('maximum', c.c_size_t),
                        ('active', w.DWORD), ('affinity', c.c_size_t),
                        ('priority', w.DWORD), ('scheduling', w.DWORD)]
        class IO(c.Structure):
            _fields_ = [(name, c.c_ulonglong) for name in ('read', 'write', 'other', 'read_bytes', 'write_bytes', 'other_bytes')]
        class Extended(c.Structure):
            _fields_ = [('basic', Basic), ('io', IO), ('process_memory', c.c_size_t),
                        ('job_memory', c.c_size_t), ('peak_process', c.c_size_t), ('peak_job', c.c_size_t)]
        class Accounting(c.Structure):
            _fields_ = [(name, c.c_longlong) for name in ('user', 'kernel', 'period_user', 'period_kernel')] + [
                ('faults', w.DWORD), ('total', w.DWORD), ('active', w.DWORD), ('terminated', w.DWORD)]
        self.Accounting = Accounting
        self.api.CreateJobObjectW.argtypes = [c.c_void_p, w.LPCWSTR]
        self.api.CreateJobObjectW.restype = w.HANDLE
        self.api.SetInformationJobObject.argtypes = [w.HANDLE, c.c_int, c.c_void_p, w.DWORD]
        self.api.AssignProcessToJobObject.argtypes = [w.HANDLE, w.HANDLE]
        self.api.QueryInformationJobObject.argtypes = [w.HANDLE, c.c_int, c.c_void_p, w.DWORD, c.c_void_p]
        self.api.TerminateJobObject.argtypes = [w.HANDLE, w.UINT]
        self.api.CloseHandle.argtypes = [w.HANDLE]
        self.handle = self.api.CreateJobObjectW(None, None)
        if not self.handle:
            raise c.WinError(c.get_last_error())
        info = Extended()
        info.basic.flags = 0x2000  # JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
        if not self.api.SetInformationJobObject(self.handle, 9, c.byref(info), c.sizeof(info)):
            self.close()
            raise c.WinError(c.get_last_error())

    def assign(self, process):
        if not self.api.AssignProcessToJobObject(self.handle, self.w.HANDLE(int(process._handle))):
            raise self.c.WinError(self.c.get_last_error())

    def active(self):
        info = self.Accounting()
        if not self.api.QueryInformationJobObject(self.handle, 1, self.c.byref(info), self.c.sizeof(info), None):
            raise self.c.WinError(self.c.get_last_error())
        return info.active

    def terminate(self):
        if not self.api.TerminateJobObject(self.handle, 1):
            raise self.c.WinError(self.c.get_last_error())

    def close(self):
        if self.handle:
            self.api.CloseHandle(self.handle)
            self.handle = None


def managed_run(command, *, cwd, stdout, timeout, on_start=None):
    job = WindowsJob() if os.name == 'nt' else None
    process = None
    try:
        # The child cannot spawn the launcher until its parent assigns the Job.
        gate = [sys.executable, str(Path(__file__).resolve()), '--gated-launch', json.dumps(command)]
        process = subprocess.Popen(gate, cwd=cwd, stdin=subprocess.PIPE, stdout=stdout,
                                   stderr=subprocess.STDOUT, start_new_session=os.name != 'nt')
        if job:
            try:
                job.assign(process)
            except BaseException:
                process.kill()
                raise
        if on_start:
            on_start(process.pid)
        process.stdin.write(b'go\n')
        process.stdin.close()
        try:
            result = process.wait(timeout=timeout)
        except subprocess.TimeoutExpired as error:
            raise ProcessTreeError('Outer launcher deadline expired; owned process tree terminated') from error
        deadline = time.monotonic() + 1
        while job and job.active() and time.monotonic() < deadline:
            time.sleep(0.02)
        if job and job.active():
            raise ProcessTreeError('Descendants outlived launcher; owned process tree terminated')
        return result
    finally:
        if process is None and job:
            job.close()
        if process is not None:
            if job:
                try:
                    if job.active():
                        job.terminate()
                    deadline = time.monotonic() + 10
                    while job.active():
                        if time.monotonic() >= deadline:
                            raise ProcessTreeError('Could not confirm owned process tree exit')
                        time.sleep(0.05)
                finally:
                    job.close()
            elif process.poll() is None:
                os.killpg(process.pid, signal.SIGKILL)
            if process.poll() is None:
                process.wait(timeout=10)


if __name__ == '__main__':
    if len(sys.argv) != 3 or sys.argv[1] != '--gated-launch':
        raise SystemExit('Internal process gate only')
    if sys.stdin.buffer.readline() != b'go\n':
        raise SystemExit('Parent did not authorize process launch')
    raise SystemExit(subprocess.call(json.loads(sys.argv[2])))
