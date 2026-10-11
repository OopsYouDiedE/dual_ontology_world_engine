"""Reproduce C0 -> C1 -> C2 -> C3 without an interpreter in generated stages."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[2]
COMPILER = ROOT / 'language_core/compiler'

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--out', required=True)
    args = parser.parse_args()
    out = (ROOT / args.out).resolve()
    out.mkdir(parents=True, exist_ok=False)
    sources = [p for p in COMPILER.rglob('*') if p.is_file() and not {'bin', 'obj'}.intersection(p.parts)]
    sources += [Path(__file__).resolve()]
    # C0 links the seed reader/value project; freeze its complete source inputs too.
    for folder in ('Kernel', 'bootstrap'):
        sources += [p for p in (ROOT / 'language_core' / folder).rglob('*') if p.is_file() and not {'bin', 'obj'}.intersection(p.parts)]
    before = {str(p.relative_to(ROOT)): sha(p) for p in sources}
    (out / 'sources.json').write_text(json.dumps(before, indent=2), encoding='utf-8')
    with zipfile.ZipFile(out / 'sources.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
        for source in sources:
            archive.write(source, source.relative_to(ROOT).as_posix())
    commands = []

    def run(label, command, expected=0):
        result = subprocess.run([str(x) for x in command], cwd=ROOT, capture_output=True, timeout=180)
        (out / (label + '.stdout.txt')).write_bytes(result.stdout)
        (out / (label + '.stderr.txt')).write_bytes(result.stderr)
        commands.append({'label': label, 'command': [str(x) for x in command], 'exit': result.returncode})
        (out / 'commands.json').write_text(json.dumps(commands, indent=2), encoding='utf-8')
        if result.returncode != expected:
            raise RuntimeError(f'{label}: exit {result.returncode}; see raw output in {out}')
        return result.stdout

    def build(name):
        folder = out / name
        shutil.copyfile(COMPILER / 'Runtime.cs', folder / 'Runtime.cs')
        (folder / 'Program.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors><UseSharedCompilation>false</UseSharedCompilation></PropertyGroup></Project>', encoding='utf-8')
        run(name + '-build', ['dotnet', 'build', folder / 'Program.csproj', '-c', 'Release', '--disable-build-servers'])
        return folder / 'bin/Release/net8.0/Program.dll'

    run('toolchain', ['dotnet', '--version'])
    run('runtimes', ['dotnet', '--list-runtimes'])
    run('seed-build', ['dotnet', 'build', COMPILER / 'seed/Seed.csproj', '-c', 'Release', '--disable-build-servers'])
    previous = COMPILER / 'seed/bin/Release/net8.0/Seed.dll'
    stages = {}
    for name in ('c1', 'c2', 'c3'):
        folder = out / name
        folder.mkdir()
        run(name + '-generate', ['dotnet', previous, COMPILER / 'compiler.dv', folder / 'Program.cs'])
        previous = build(name)
        deps = json.loads(previous.with_suffix('.deps.json').read_text(encoding='utf-8'))
        if set(deps['libraries']) != {'Program/1.0.0'}:
            raise RuntimeError('Generated stage has an unexpected assembly dependency')
        stages[name] = {'source': sha(folder / 'Program.cs'), 'assembly': sha(previous)}
    if stages['c2']['source'] != stages['c3']['source']:
        raise RuntimeError('C2/C3 generated source differs')
    # Fresh source, not the compiler itself: executes recursive and mutable computation.
    sample = '(defn sum (n) (if (rt_lt n 1) 0 (rt_add n (sum (rt_sub n 1)))))\n(main (let (i 0 total 0) (do (while (rt_lt i 5) (do (set total (rt_add total i)) (set i (rt_add i 1)))) (rt_print (rt_add total (sum 10))))))\n'
    sample_path = out / 'sample.dv'
    sample_path.write_text(sample, encoding='utf-8')
    (out / 'sample').mkdir()
    run('sample-generate', ['dotnet', previous, sample_path, out / 'sample/Program.cs'])
    executable = build('sample')
    result = run('sample-execute', ['dotnet', executable])
    if result.decode().strip() != '65':
        raise RuntimeError('Sample semantic mismatch')
    unicode_text = 'A\u0085B\u2028C\u2029D'
    unicode_source = out / 'unicode.dv'
    unicode_source.write_text('(main (rt_print "' + unicode_text + '"))', encoding='utf-8')
    (out / 'unicode').mkdir()
    run('unicode-generate', ['dotnet', previous, unicode_source, out / 'unicode/Program.cs'])
    unicode_exe = build('unicode')
    unicode_result = run('unicode-execute', ['dotnet', unicode_exe]).decode('utf-8')
    if unicode_result.rstrip('\r\n') != unicode_text:
        raise RuntimeError('Unicode string changed during compilation')
    # The compiler must react to new compiler source, rather than replay a fixed artifact.
    mutated = out / 'mutated-compiler.dv'
    mutated.write_text((COMPILER / 'compiler.dv').read_text(encoding='utf-8').replace('class DVProgram', 'class DVChanged'), encoding='utf-8')
    (out / 'mutated').mkdir()
    run('mutation-generate', ['dotnet', previous, mutated, out / 'mutated/Program.cs'])
    mutated_exe = build('mutated')
    run('mutation-compile-sample', ['dotnet', mutated_exe, sample_path, out / 'mutated-sample.cs'])
    if 'class DVChanged' not in (out / 'mutated-sample.cs').read_text(encoding='utf-8'):
        raise RuntimeError('Compiler source mutation did not affect new compilation')
    invalid = {
        'unbound': '(main missing)',
        'arity': '(main (rt_add 1))',
        'duplicate': '(defn a () 1) (defn a () 2) (main 0)',
        'scope': '(main (do (let (x 1) x) x))',
        'escape': '(main "\\q")',
        'unfinished': '(main (rt_print "x")',
        'foreign': '(main (rt_eval "anything"))',
        'reserved-runtime': '(defn rt_custom () 1) (main (rt_custom))',
        'shadow': '(main (let (x 1) (let (x 2) x)))',
    }
    for label, source in invalid.items():
        path = out / (label + '.dv')
        target = out / (label + '.cs')
        path.write_text(source, encoding='utf-8')
        run('diagnostic-' + label, ['dotnet', previous, path, target], expected=1)
        if target.exists():
            raise RuntimeError('Rejected source emitted a target')
    after = {str(p.relative_to(ROOT)): sha(p) for p in sources}
    if before != after:
        raise RuntimeError('Compiler source changed during run')
    (out / 'result.json').write_text(json.dumps({
        'verdict': 'bootstrap_compiler_self_hosted',
        'scope': 'DV named-function dynamic-value bootstrap subset -> C#',
        'stages': stages,
        'criteria': {
            'same_compiler_source_compiled_by_c0_c1_c2': sha(COMPILER / 'compiler.dv'),
            'c1_c2_c3_built_and_c3_executed': True,
            'c2_c3_generated_source_byte_equal': True,
            'generated_stages_have_no_seed_or_interpreter_assembly_dependency': True,
            'fresh_program_compiled_and_executed': result.decode(),
            'compiler_source_change_affects_new_output': 'DVChanged',
            'unicode_preserved_through_compilation_and_execution': True,
            'invalid_sources_rejected_without_target_files': list(invalid),
            'frozen_sources_unchanged': True,
        },
        'full_language_specification_complete': False,
        'remaining': ['static types', 'capability checking', 'target-neutral IR', 'modules and closures', 'inference library compilation migration'],
    }, indent=2), encoding='utf-8')
    print(out)

if __name__ == '__main__':
    try:
        main()
    except Exception as exc:
        print(str(exc), file=sys.stderr)
        sys.exit(1)
