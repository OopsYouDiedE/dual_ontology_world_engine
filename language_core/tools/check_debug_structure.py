"""Opt-in neutral codec/DAG engineering only. Never invokes a session loop."""
from pathlib import Path
import argparse
import ast
import hashlib
import json
import io
import shutil
import subprocess
import sys
import time
from dv_toolchain import ROOT, LANGUAGE, PROJECT, environment
from raw_dag import RawDagIndex
from binary_evidence import records
from debug_checkpoints import record
from dv_sources import sources
from debug_checkpoints import checkpoint_metadata

PROBE = r'''
using DuelView.Compiled;
internal static class DebugStructureProbe
{
    public static void Main(string[] args)
    {
        var expected = new List<object> { new List<object> { "exact", Rational.From("1/3") }, new List<object> { "text", "1/3" }, new List<object> { "number", 0.5d }, new List<object> { "flag", false } };
        R.WriteEvidenceFile(args[0] + ".dvsave", expected);
        var loaded = R.ReadEvidenceFile(args[0] + ".dvsave");
        R.raw_open(args[0]);
        R.raw_checkpoint("initial", expected);
        R.json_print(new List<object> { new List<object> { "scope", "neutral-binary-codec-only" }, new List<object> { "same_value", R.same_value(expected, loaded) }, new List<object> { "exact_type", R.record_find(loaded, "exact", false).GetType().FullName! }, new List<object> { "text_type", R.record_find(loaded, "text", false).GetType().FullName! } });
        R.raw_checkpoint("neutral-codec", loaded);
        R.raw_event(new List<object> { new List<object> { "id", "e0" }, new List<object> { "type", "neutral-codec" }, new List<object> { "actor", "" }, new List<object> { "time", 0.0 }, new List<object> { "data", loaded } });
        R.raw_finish(expected);
        R.json_print(new List<object> { new List<object> { "formal_session", "not-started" }, new List<object> { "stage_checkpoint", "not-created" }, new List<object> { "resume_loop", "not-called" } });
    }
}
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    out = args.out.resolve()
    out.mkdir(parents=True, exist_ok=False)
    report = {'scope': 'neutral-binary-codec-and-journal-only', 'formal_session': 'not-started', 'stages': [], 'complete': False}
    tracked = {path for path, _ in sources([LANGUAGE / 'game/debug-run.dv'])}
    tracked.update({LANGUAGE / 'compiler/Runtime.cs', LANGUAGE / 'compiler/compiler.dv', LANGUAGE / 'tools/DebugCheckpointResume.cs', LANGUAGE / 'tools/debug_checkpoints.py', LANGUAGE / 'tools/raw_dag.py', LANGUAGE / 'tools/binary_evidence.py', Path(__file__).resolve()})
    initial_hashes = {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in sorted(tracked)}
    report['source_sha256_before'] = initial_hashes
    def save():
        (out / 'engineering-result.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    def run(cmd, name):
        start = time.monotonic()
        stage = {'name': name, 'command': list(map(str, cmd))}
        report['stages'].append(stage)
        try:
            with (out / (name + '.stdout')).open('wb') as stdout, (out / (name + '.stderr')).open('wb') as stderr:
                result = subprocess.run(list(map(str, cmd)), cwd=ROOT, env=environment(), stdout=stdout, stderr=stderr, timeout=240)
            stage['exit_code'] = result.returncode
            if result.returncode:
                raise RuntimeError(f'{name} exited {result.returncode}')
        finally:
            stage['elapsed_seconds'] = time.monotonic() - start
            save()
    try:
        for name in ('run_debug.py', 'debug_checkpoints.py', 'resume_debug.py', 'check_debug_structure.py'):
            source = LANGUAGE / 'tools' / name
            ast.parse(source.read_text(encoding='utf-8-sig'), filename=str(source))
        program = out / 'program'
        run([sys.executable, LANGUAGE / 'tools/build_session.py', '--entry', LANGUAGE / 'game/debug-run.dv', '--out', program, '--main', 'false'], 'dv-generation-no-session-main')
        shutil.copyfile(LANGUAGE / 'compiler/Runtime.cs', program / 'Runtime.cs')
        shutil.copyfile(LANGUAGE / 'tools/DebugCheckpointResume.cs', program / 'DebugCheckpointResume.cs')
        (program / 'NeutralProbe.cs').write_text(PROBE, encoding='utf-8')
        project = PROJECT.replace('<OutputType>Exe</OutputType>', '<OutputType>Exe</OutputType><StartupObject>DebugStructureProbe</StartupObject>')
        (program / 'Program.csproj').write_text(project, encoding='utf-8')
        run(['dotnet', 'build', program / 'Program.csproj', '-c', 'Release', '--disable-build-servers', '-p:NuGetAudit=false'], 'neutral-build')
        journal = out / 'neutral-codec.dvdag'
        run(['dotnet', program / 'bin/Release/net8.0/Program.dll', journal], 'neutral-codec')
        packets = [record(value) for value in records(out / 'neutral-codec.stdout')]
        report['codec_observations'] = packets
        with RawDagIndex(journal) as index:
            report['raw_summary'] = index.summary
            with (out / 'neutral-binary.dvsave').open('xb') as output:
                index.write_binary(index.select(checkpoint='neutral-codec'), output)
            expanded = io.StringIO()
            index.write_value(index.select(checkpoint='neutral-codec'), expanded, 'dv-tagged')
            restored = json.loads(expanded.getvalue())
            expected = [['exact', {'$dv_rational': '1/3'}], ['text', '1/3'], ['number', 0.5], ['flag', False]]
            report['tagged_expected'] = expected
            report['tagged_actual'] = restored
            try:
                checkpoint_metadata(index, 'stage-day3')
            except ValueError as error:
                report['missing_stage_rejected'] = str(error)
            else:
                raise RuntimeError('Missing real stage checkpoint was accepted')
            if not packets[0]['same_value'] or expected != restored or not index.summary['complete_sealing']:
                raise RuntimeError('Concrete neutral codec/journal observations differ')
        report['complete'] = True
    except Exception as error:
        report['error'] = f'{type(error).__name__}: {error}'
    finally:
        report['source_sha256_after'] = {path: hashlib.sha256(Path(path).read_bytes()).hexdigest() if Path(path).is_file() else None for path in initial_hashes}
        report['source_unchanged'] = report['source_sha256_after'] == initial_hashes
        save()
    return 0 if report['complete'] and report['source_unchanged'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
