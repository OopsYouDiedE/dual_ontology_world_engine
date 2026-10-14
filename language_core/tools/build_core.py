"""Compile the DV library with the DV compiler; no domain acceptance scenarios run."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[2]
LANGUAGE = ROOT / 'language_core'

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--out', required=True)
    args = parser.parse_args()
    out = (ROOT / args.out).resolve()
    out.mkdir(parents=True, exist_ok=False)
    inputs = [p for d in ('compiler', 'core') for p in (LANGUAGE / d).rglob('*')
              if p.is_file() and not {'bin', 'obj'}.intersection(p.parts)] + [Path(__file__).resolve()]
    hashes = {p.relative_to(ROOT).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in inputs}
    (out / 'sources.json').write_text(json.dumps(hashes, indent=2), encoding='utf-8')
    with zipfile.ZipFile(out / 'sources.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
        for p in inputs:
            archive.write(p, p.relative_to(ROOT).as_posix())
    commands = []

    def run(label, argv):
        result = subprocess.run([str(a) for a in argv], cwd=ROOT, capture_output=True, timeout=180)
        (out / (label + '.stdout.txt')).write_bytes(result.stdout)
        (out / (label + '.stderr.txt')).write_bytes(result.stderr)
        commands.append({'label': label, 'argv': [str(a) for a in argv], 'exit': result.returncode})
        (out / 'commands.json').write_text(json.dumps(commands, indent=2), encoding='utf-8')
        if result.returncode:
            raise RuntimeError(f'{label} failed; raw diagnostics: {out}')

    def project(folder):
        shutil.copyfile(LANGUAGE / 'compiler/Runtime.cs', folder / 'Runtime.cs')
        (folder / 'Program.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors><UseSharedCompilation>false</UseSharedCompilation></PropertyGroup></Project>', encoding='utf-8')
        run(folder.name + '-build', ['dotnet', 'build', folder / 'Program.csproj', '-c', 'Release', '--disable-build-servers'])
        return folder / 'bin/Release/net8.0/Program.dll'

    seed = LANGUAGE / 'compiler/seed/Seed.csproj'
    run('seed-build', ['dotnet', 'build', seed, '-c', 'Release', '--disable-build-servers'])
    compiler = out / 'compiler'
    compiler.mkdir()
    run('compiler-generate', ['dotnet', seed.parent / 'bin/Release/net8.0/Seed.dll', LANGUAGE / 'compiler/compiler.dv', compiler / 'Program.cs'])
    executable = project(compiler)
    compiler_sources = []
    for name in ('compiler-c2', 'compiler-c3'):
        stage = out / name
        stage.mkdir()
        run(name + '-generate', ['dotnet', executable, LANGUAGE / 'compiler/compiler.dv', stage / 'Program.cs'])
        executable = project(stage)
        compiler_sources.append((stage / 'Program.cs').read_bytes())
    if compiler_sources[0] != compiler_sources[1]:
        raise RuntimeError('Compiler generated-source fixed point changed')
    library = out / 'library'
    library.mkdir()
    bundle = library / 'library.dv'
    modules = sorted((LANGUAGE / 'core').glob('*.dv'))
    bundle.write_text('\n'.join(p.read_text(encoding='utf-8') for p in modules) + '\n(main false)\n', encoding='utf-8')
    run('library-generate', ['dotnet', executable, bundle, library / 'Program.cs'])
    project(library)
    inspector = out / 'inspector'
    inspector.mkdir()
    inspector_source = inspector / 'inspector.dv'
    inspector_source.write_text('\n'.join(p.read_text(encoding='utf-8') for p in modules) + '\n(main (dv_inspector_main))\n', encoding='utf-8')
    run('inspector-generate', ['dotnet', executable, inspector_source, inspector / 'Program.cs'])
    project(inspector)
    if hashes != {p.relative_to(ROOT).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in inputs}:
        raise RuntimeError('Sources changed while building')
    (out / 'result.json').write_text(json.dumps({'status': 'compiled', 'modules': [p.name for p in modules],
        'compiler': 'DV C3', 'compiler_fixed_point': True, 'compiler_source_sha256': hashlib.sha256(compiler_sources[1]).hexdigest(), 'domain_program_executed': False,
        'acceptance': 'Not performed; awaiting user acceptance-code proposal', 'sources_unchanged': True}, indent=2), encoding='utf-8')
    print(out)

if __name__ == '__main__':
    main()
