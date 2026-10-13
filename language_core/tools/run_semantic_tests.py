"""Compile and run the semantic counterexample regression test suite."""
import os
from pathlib import Path
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[2]
LANGUAGE = ROOT / 'language_core'
SCRATCH = ROOT / 'scratch' / 'semantic-tests'

def main():
    if SCRATCH.exists():
        shutil.rmtree(SCRATCH)
    SCRATCH.mkdir(parents=True)

    seed = LANGUAGE / 'compiler/seed/Seed.csproj'
    env = {**os.environ, 'DOTNET_ROLL_FORWARD': 'Major'}
    
    subprocess.run(['dotnet', 'build', str(seed), '-c', 'Release', '--disable-build-servers'],
                   cwd=ROOT, env=env, check=True)
    
    # Bundle core + session + test
    core_modules = sorted((LANGUAGE / 'core').glob('*.dv'))
    session_modules = sorted((LANGUAGE / 'verification/session').glob('*.dv'))
    test_module = LANGUAGE / 'tools/21-semantic-counterexamples.dv'
    
    bundle_files = core_modules + session_modules + [test_module]
    bundle_text = '\n'.join(p.read_text(encoding='utf-8') for p in bundle_files) + '\n(main (main))\n'
    
    bundle_path = SCRATCH / 'test_bundle.dv'
    bundle_path.write_text(bundle_text, encoding='utf-8')
    
    out_cs = SCRATCH / 'Program.cs'
    compiler_dll = ROOT / 'scratch' / 'test-build-3' / 'compiler-c3' / 'bin' / 'Release' / 'net8.0' / 'Program.dll'
    subprocess.run(['dotnet', str(compiler_dll), str(bundle_path), str(out_cs)],
                   cwd=ROOT, env=env, check=True)
    
    shutil.copyfile(LANGUAGE / 'compiler/Runtime.cs', SCRATCH / 'Runtime.cs')
    (SCRATCH / 'Program.csproj').write_text(
        '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
        '<OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework>'
        '<RollForward>Major</RollForward>'
        '<ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>'
        '<TreatWarningsAsErrors>true</TreatWarningsAsErrors>'
        '<UseSharedCompilation>false</UseSharedCompilation>'
        '</PropertyGroup></Project>',
        encoding='utf-8'
    )
    
    subprocess.run(['dotnet', 'build', str(SCRATCH / 'Program.csproj'), '-c', 'Release', '--disable-build-servers'],
                   cwd=ROOT, env=env, check=True)
    
    exe = SCRATCH / 'bin/Release/net8.0/Program.dll'
    res = subprocess.run(['dotnet', str(exe)], cwd=ROOT, env=env, capture_output=True, text=True)
    print("STDOUT:")
    print(res.stdout)
    if res.stderr:
        print("STDERR:")
        print(res.stderr)
    if res.returncode != 0:
        raise RuntimeError(f"Semantic tests failed with exit code {res.returncode}")
    print("All semantic property regression tests PASSED!")

if __name__ == '__main__':
    main()
