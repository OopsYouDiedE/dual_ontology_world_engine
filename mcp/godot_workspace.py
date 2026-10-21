"""Launch development tools against linked production assets in a temporary project."""
from __future__ import annotations

import argparse
import atexit
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]
GAME = ROOT / 'godot_csharp'
_workspaces = []


def _link(source: Path, target: Path):
    target.parent.mkdir(parents=True, exist_ok=True)
    if os.name == 'nt':
        subprocess.run(['cmd', '/c', 'mklink', '/J', str(target), str(source)],
                       check=True, capture_output=True)
    else:
        target.symlink_to(source, target_is_directory=True)


def prepare(csharp=False, editor=False):
    """Use the actual source folders; tool assemblies remain outside the game project."""
    scratch = ROOT / '.scratch'
    scratch.mkdir(exist_ok=True)
    temporary = tempfile.TemporaryDirectory(prefix='godot-tools-', dir=scratch)
    _workspaces.append(temporary)
    work = Path(temporary.name)
    for name in ('scenes', 'ShareAsset', 'Contracts'):
        _link(GAME / name, work / name)
    _link(ROOT / 'mcp/verification', work / 'tools')
    _link(ROOT / 'mcp/asset_pipeline', work / 'asset_pipeline')
    _link(ROOT / 'mcp/asset_pipeline/sources', work / 'AssetsSource')
    _link(ROOT / 'mcp/sandboxes', work / 'sandboxes')
    _link(ROOT / 'mcp/rpg_adapter', work / 'addons/rpg_asset_adapter')
    _link(ROOT / 'mcp/editor_lsp/godot', work / 'addons/dv_checker')
    shutil.copyfile(GAME / 'default_bus_layout.tres', work / 'default_bus_layout.tres')
    source = (GAME / 'project.godot').read_text(encoding='utf-8')
    source = source.replace('project/assembly_name="DualOntologyWorldEngine"',
                            'project/assembly_name="DuelView.Tools"')
    if not csharp:
        source = source.replace('PackedStringArray("4.6", "C#")', 'PackedStringArray("4.6")')
        source = source.replace('run/main_scene="res://scenes/Host/Main.tscn"',
                                'run/main_scene="res://sandboxes/MapSandbox.tscn"')
        source = source.replace('[dotnet]\n\nproject/assembly_name="DuelView.Tools"\n', '')
    if editor:
        source = source.replace('enabled=PackedStringArray()',
            'enabled=PackedStringArray("res://addons/dv_checker/plugin.cfg", "res://addons/rpg_asset_adapter/plugin.cfg")')
    source += '\n[duelview_tools]\nrepository="' + ROOT.as_posix() + '"\n'
    (work / 'project.godot').write_text(source, encoding='utf-8')
    if csharp:
        project = f'''<Project Sdk="Godot.NET.Sdk/4.6.1">
  <PropertyGroup><TargetFramework>net8.0</TargetFramework><EnableDynamicLoading>true</EnableDynamicLoading>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup><Compile Include="scenes/**/*.cs" /><Compile Include="tools/FrameArchive.cs" />
    <ProjectReference Include="{(ROOT / 'dv_core/host_bridge/DuelView.Session.csproj').as_posix()}" />
  </ItemGroup>
</Project>'''
        (work / 'DuelView.Tools.csproj').write_text(project, encoding='utf-8')
        shutil.copyfile(GAME / 'NuGet.Config', work / 'NuGet.Config')
    return work


def cleanup():
    while _workspaces:
        _workspaces.pop().cleanup()


atexit.register(cleanup)


def build(work: Path):
    subprocess.run(['dotnet', 'build', str(work / 'DuelView.Tools.csproj'),
                    '--disable-build-servers', '-p:NuGetAudit=false'], check=True)


def import_assets(work: Path, godot, log_path: Path | None = None):
    """Populate this workspace's import cache before opening its scene or tool."""
    command = [str(godot), '--headless', '--editor', '--path', str(work), '--import', '--quit']
    if log_path is not None:
        command += ['--log-file', str(log_path)]
    subprocess.run(command, cwd=ROOT, check=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--godot', default=os.environ.get('GODOT') or os.environ.get('GODOT_BIN') or shutil.which('godot'))
    parser.add_argument('--sandbox', action='store_true')
    parser.add_argument('--editor', action='store_true')
    parser.add_argument('--script')
    parser.add_argument('--csharp', action='store_true')
    parser.add_argument('--headless', action='store_true')
    parser.add_argument('arguments', nargs=argparse.REMAINDER)
    args = parser.parse_args()
    if not args.godot:
        parser.error('Supply --godot or set GODOT to the installed Godot executable')
    work = prepare(csharp=args.csharp, editor=args.editor)
    if args.csharp:
        build(work)
    import_assets(work, args.godot)
    command = [args.godot, '--path', str(work)]
    if args.headless:
        command.append('--headless')
    if args.editor:
        command.append('--editor')
    if args.script:
        command += ['--script', args.script]
    elif args.sandbox:
        command.append('res://sandboxes/MapSandbox.tscn')
    if args.arguments:
        command += args.arguments if args.arguments[0] == '--' else ['--', *args.arguments]
    return subprocess.call(command, cwd=ROOT)


if __name__ == '__main__':
    raise SystemExit(main())
