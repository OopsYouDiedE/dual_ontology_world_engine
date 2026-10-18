"""Prepared neutral engineering probe. Run only after the batch source freeze.

No session, scenario, world process or game entry is compiled/executed. Supply
--compiler-dll after the ordinary compiler bootstrap to invoke actual cs_string.
All outputs are created in a new --out directory. Existing evidence is read only.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time

ROOT = next(parent for parent in Path(__file__).resolve().parents if (parent / 'language_core/compiler/Runtime.cs').is_file())
sys.path.insert(0, str(ROOT / 'language_core/tools'))
from raw_dag import RawDagIndex, inspect_journal


PROJECT = '''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors><UseSharedCompilation>false</UseSharedCompilation></PropertyGroup></Project>'''

PROGRAM = r'''
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using DuelView.Compiled;

public static class Probe
{
    static List<object> Pair(string key, object value) => new() { key, value };
    static string Encode(object value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) { Write(writer, value); writer.Flush(); }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    static void Write(Utf8JsonWriter writer, object value)
    {
        switch (value)
        {
            case string text: writer.WriteStringValue(text); break;
            case double number: writer.WriteNumberValue(number); break;
            case Rational exact: writer.WriteStringValue(exact.ToString()); break;
            case bool flag: writer.WriteBooleanValue(flag); break;
            case List<object> list:
                writer.WriteStartArray(); foreach (var child in list) Write(writer, child); writer.WriteEndArray(); break;
            default: throw new Exception("Unexpected neutral value");
        }
    }
    public static int Main(string[] args)
    {
        var folder = args[0];
        var mode = args[1];
        var facts = new Dictionary<string, object>();
        var unicode = "中🙂e\u0301\t\r\n\"\\" + '\u0085' + '\u2028' + '\u2029';
        var shared = new List<object> { Pair("ratio", R.exact("2/3")), unicode, false, "2/3" };
        var original = new List<object> { Pair("id", "neutral-event"), Pair("type", "neutral"), Pair("actor", "neutral-owner"), Pair("time", R.exact("2252")), Pair("shared", shared), Pair("same", shared), Pair("history", new List<object>{"kept"}), Pair("simulations", new List<object>{"kept"}) };
        var frozen = R.snapshot(original);
        if (mode == "existing" || mode == "partial-existing")
        {
            var path = Path.Combine(folder, mode == "existing" ? "neutral.dag.jsonl" : "occupied.dag.jsonl");
            try { R.raw_open(path); facts["existing_destination_rejected"] = false; }
            catch (IOException error) { facts["existing_destination_rejected"] = true; facts["error"] = error.Message; }
        }
        else if (mode == "duplicate")
        {
            R.raw_open(Path.Combine(folder, "duplicate.dag.jsonl"));
            R.raw_event(frozen);
            try { R.raw_event(frozen); facts["duplicate_rejected"] = false; }
            catch (InvalidOperationException error) { facts["duplicate_rejected"] = true; facts["error"] = error.Message; }
            facts["explicit_abort_deactivated_recorder"] = R.Bool(R.raw_abort()) && !R.Bool(R.raw_event(frozen));
        }
        else
        {
            var before = Directory.GetFiles(folder).Order().ToArray();
            facts["disabled_hook_returned_false"] = !R.Bool(R.raw_event(frozen));
            facts["disabled_hook_created_no_files"] = before.SequenceEqual(Directory.GetFiles(folder).Order());
            var expected = Encode(frozen);
            File.WriteAllText(Path.Combine(folder, "expected.json"), expected, new UTF8Encoding(false));
            R.json_write(Path.Combine(folder, "actual.json"), frozen);
            R.json_write(Path.Combine(folder, "actual.json.gz"), frozen);
            facts["default_json_bytes_equal_reference_encoding"] = File.ReadAllBytes(Path.Combine(folder, "expected.json")).SequenceEqual(File.ReadAllBytes(Path.Combine(folder, "actual.json")));
            using (var gzip = new GZipStream(File.OpenRead(Path.Combine(folder, "actual.json.gz")), CompressionMode.Decompress))
            using (var reader = new StreamReader(gzip, Encoding.UTF8)) facts["gzip_restores_exact_json_text"] = reader.ReadToEnd() == expected;
            R.json_print(frozen);
            R.raw_open(Path.Combine(folder, "neutral.dag.jsonl"));
            R.raw_checkpoint("initial", frozen);
            R.raw_event(frozen);
            R.json_write(Path.Combine(folder, "reference.json"), R.raw_ref(shared));
            R.replace(shared, 1d, "changed-after-freeze");
            R.replace(R.Items(original[0]), 1d, "mutable-new-id");
            facts["frozen_event_unchanged_after_source_mutation"] = (string)R.json_stringify(frozen) == expected;
            R.json_write(Path.Combine(folder, "mutated-expected.json"), original);
            R.raw_finish(new List<object> { Pair("original", frozen), Pair("mutated", original) });
            facts["final_filename_published"] = File.Exists(Path.Combine(folder, "neutral.dag.jsonl"));
            facts["partial_absent_after_finish"] = !File.Exists(Path.Combine(folder, "neutral.dag.jsonl.partial"));
            facts["finish_restored_disabled_hook"] = !R.Bool(R.raw_event(frozen));
            facts["finish_deactivated_raw_ref"] = Equals(R.raw_ref(frozen), false);
            R.raw_open(Path.Combine(folder, "second.dag.jsonl"));
            R.raw_checkpoint("initial", frozen);
            R.raw_event(frozen);
            R.raw_finish(frozen);
            facts["same_process_second_recorder_finished"] = File.Exists(Path.Combine(folder, "second.dag.jsonl"));
            if (args.Length > 2 && args[2] != "")
            {
                // Both probe and compiler are named Program.dll. Keep their identities
                // in separate contexts so reflection cannot resolve the probe itself.
                var compilerContext = new AssemblyLoadContext("neutral-compiler-probe", isCollectible: true);
                try
                {
                    var assembly = compilerContext.LoadFromAssemblyPath(Path.GetFullPath(args[2]));
                    var methods = assembly.GetTypes().SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                        .Where(candidate => candidate.Name == "F_cs_string" && !candidate.IsGenericMethod && candidate.GetParameters() is var parameters && parameters.Length == 1 && parameters[0].ParameterType == typeof(object)).ToArray();
                    if (methods.Length != 1) throw new Exception($"Expected one compiled F_cs_string(object); found {methods.Length} in {assembly.Location}");
                    var method = methods[0];
                    var raw = "\"" + unicode.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
                    var expectedEscapes = raw.Replace("\u0085", "\\u0085").Replace("\u2028", "\\u2028").Replace("\u2029", "\\u2029");
                    facts["actual_compiler_cs_string_invoked"] = true;
                    facts["compiler_reflection_assembly"] = assembly.Location;
                    facts["compiler_reflection_type"] = method.DeclaringType?.FullName ?? "";
                    facts["compiler_reflection_method"] = method.Name;
                    facts["compiler_unicode_control_escape_text_unchanged"] = Equals(method.Invoke(null, new object[] { raw }), expectedEscapes);
                }
                finally { compilerContext.Unload(); }
            }
            else facts["actual_compiler_cs_string_invoked"] = false;
        }
        File.WriteAllText(Path.Combine(folder, mode + "-facts.json"), JsonSerializer.Serialize(facts, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        return 0;
    }
}
'''


def record(value):
    if isinstance(value, list):
        if value and all(isinstance(pair, list) and len(pair) == 2 and isinstance(pair[0], str) for pair in value):
            return {pair[0]: record(pair[1]) for pair in value}
        return [record(child) for child in value]
    return value


def recover(index, destination, value_format='trace-json', **selection):
    with destination.open('x', encoding='utf-8') as output:
        index.write_value(index.select(**selection), output, value_format)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--compiler-dll', type=Path)
    args = parser.parse_args()
    out = args.out.resolve()
    out.mkdir(parents=True, exist_ok=False)
    project = out / 'program'
    project.mkdir()
    runtime = ROOT / 'language_core/compiler/Runtime.cs'
    shutil.copyfile(runtime, project / 'Runtime.cs')
    (project / 'Program.cs').write_text(PROGRAM, encoding='utf-8')
    (project / 'Program.csproj').write_text(PROJECT, encoding='utf-8')
    env = os.environ.copy()
    env.setdefault('DOTNET_ROLL_FORWARD', 'Major')
    commands = []

    def run(label, command):
        start = time.monotonic()
        with (out / (label + '.stdout.txt')).open('wb') as stdout, (out / (label + '.stderr.txt')).open('wb') as stderr:
            process = subprocess.run(command, cwd=ROOT, env=env, stdout=stdout, stderr=stderr, timeout=180)
        commands.append({'label': label, 'command': list(map(str, command)), 'exit': process.returncode, 'elapsed_seconds': time.monotonic() - start})
        (out / 'commands.json').write_text(json.dumps(commands, ensure_ascii=False, indent=2), encoding='utf-8')
        if process.returncode:
            raise RuntimeError(f'Neutral engineering command {label} exited {process.returncode}; inspect raw logs')

    run('build', ['dotnet', 'build', str(project / 'Program.csproj'), '-c', 'Release', '--disable-build-servers', '-p:NuGetAudit=false'])
    dll = project / 'bin/Release/net8.0/Program.dll'
    run('write', ['dotnet', str(dll), str(out), 'write', str(args.compiler_dll.resolve()) if args.compiler_dll else ''])
    original = (out / 'neutral.dag.jsonl').read_bytes()
    run('existing', ['dotnet', str(dll), str(out), 'existing'])
    (out / 'occupied.dag.jsonl.partial').write_bytes(b'preserve-previous-partial')
    run('partial-existing', ['dotnet', str(dll), str(out), 'partial-existing'])
    run('duplicate', ['dotnet', str(dll), str(out), 'duplicate'])
    facts = {'scope': 'neutral serialization/compiler engineering only; no session or gameplay conclusions',
             'runtime_sha256': hashlib.sha256(runtime.read_bytes()).hexdigest(),
             'existing_raw_bytes_unchanged': (out / 'neutral.dag.jsonl').read_bytes() == original,
             'existing_partial_bytes_unchanged': (out / 'occupied.dag.jsonl.partial').read_bytes() == b'preserve-previous-partial'}
    facts['runtime_observations'] = {mode: json.loads((out / (mode + '-facts.json')).read_text(encoding='utf-8')) for mode in ('write', 'existing', 'partial-existing', 'duplicate')}
    with RawDagIndex(out / 'neutral.dag.jsonl') as index:
        facts['sealed_journal'] = index.summary
        recover(index, out / 'recovered-event.json', event='neutral-event')
        recover(index, out / 'recovered-event-tagged.json', event='neutral-event', value_format='dv-tagged')
        recover(index, out / 'recovered-initial.json', checkpoint='initial')
        recover(index, out / 'recovered-final-original.json', checkpoint='final', path=['original'])
        recover(index, out / 'recovered-final-mutated.json', checkpoint='final', path=['mutated'])
        root = index.select(event='neutral-event')['node']
        facts['shared_subtree_node_reused'] = index.record_field(root, 'shared') == index.record_field(root, 'same')
        reference = record(json.loads((out / 'reference.json').read_text(encoding='utf-8')))
        recover(index, out / 'recovered-reference.json', node=int(reference['node_id']))
    expected = json.loads((out / 'expected.json').read_text(encoding='utf-8'))
    for name in ('event', 'initial', 'final-original'):
        facts['recovered_' + name + '_equals_original_json_value'] = json.loads((out / f'recovered-{name}.json').read_text(encoding='utf-8')) == expected
    facts['final_mutated_equals_actual_mutated_value'] = json.loads((out / 'recovered-final-mutated.json').read_text(encoding='utf-8')) == json.loads((out / 'mutated-expected.json').read_text(encoding='utf-8'))
    facts['raw_ref_recovers_original_shared_value'] = json.loads((out / 'recovered-reference.json').read_text(encoding='utf-8')) == next(pair[1] for pair in expected if pair[0] == 'shared')
    tagged = json.loads((out / 'recovered-event-tagged.json').read_text(encoding='utf-8'))
    tagged_shared = next(pair[1] for pair in tagged if pair[0] == 'shared')
    facts['tagged_exact_number_type_retained'] = tagged_shared[0][1] == {'$dv_rational': '2/3'}
    facts['tagged_numeric_looking_text_remains_text'] = tagged_shared[3] == '2/3'
    facts['direct_stdout_json_bytes_equal_reference'] = (out / 'write.stdout.txt').read_bytes().rstrip(b'\r\n') == (out / 'expected.json').read_bytes()
    frames = original.splitlines(keepends=True)
    commit = next(i for i, line in enumerate(frames) if json.loads(line)['payload'].get('kind') == 'event')
    truncated = b''.join(frames[:commit + 1]) + frames[commit + 1][:max(1, len(frames[commit + 1]) // 2)]
    (out / 'truncated.dag.jsonl.partial').write_bytes(truncated)
    corrupted = bytearray(original)
    offset = original.rfind(b'"sha256":"') + len(b'"sha256":"')
    corrupted[offset] = ord('0') if corrupted[offset] != ord('0') else ord('1')
    (out / 'bad-tail.dag.jsonl.partial').write_bytes(corrupted)
    facts['truncated_prefix'] = inspect_journal(out / 'truncated.dag.jsonl.partial')
    facts['bad_checksum_prefix'] = inspect_journal(out / 'bad-tail.dag.jsonl.partial')
    facts['duplicate_prefix'] = inspect_journal(out / 'duplicate.dag.jsonl.partial')
    facts['unverified_scope'] = 'No timing improvement, gameplay behavior or narrative completion assessed'
    (out / 'engineering-observations.json').write_text(json.dumps(facts, ensure_ascii=False, indent=2), encoding='utf-8')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
