"""Finite cache-control mechanism check; never opens or advances a session."""
import subprocess
import tempfile
from pathlib import Path
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[2]

def main():
    assembly = ROOT / 'dv_core/base/bin/Release/net8.0/DuelView.Core.dll'
    if not assembly.is_file():
        raise SystemExit('Build LanguageCore in Release before this finite check')
    session = ROOT / 'dv_core/host_bridge/bin/Release/net8.0/DuelView.Session.dll'
    if not session.is_file():
        raise SystemExit('Build LanguageCore in Release before this finite check')
    scratch = ROOT / '.scratch'
    scratch.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='cache-controls-', dir=scratch) as folder:
        work = Path(folder)
        (work / 'Program.cs').write_bytes(Path(__file__).with_name('cache_controls_check.cs').read_bytes())
        project = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><Reference Include="DuelView.Core"><HintPath>' + escape(str(assembly)) + '</HintPath></Reference><Reference Include="DuelView.Session"><HintPath>' + escape(str(session)) + '</HintPath></Reference></ItemGroup></Project>'
        (work / 'Check.csproj').write_text(project, encoding='utf-8')
        subprocess.run(['dotnet', 'build', str(work / 'Check.csproj'), '-c', 'Release', '--disable-build-servers', '-p:NuGetAudit=false'], check=True, timeout=120)
        subprocess.run(['dotnet', str(work / 'bin/Release/net8.0/Check.dll')], check=True, timeout=30)

if __name__ == '__main__':
    main()
