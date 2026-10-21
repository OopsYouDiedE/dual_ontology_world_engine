"""Launch development tools against linked production assets in a temporary project."""
from __future__ import annotations

import argparse
import atexit
from contextlib import contextmanager
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[1]
GAME = ROOT / 'godot_csharp'
_workspaces = {}


def _link(source: Path, target: Path):
    target.parent.mkdir(parents=True, exist_ok=True)
    if os.name == 'nt':
        subprocess.run(['cmd', '/c', 'mklink', '/J', str(target), str(source)],
                       check=True, capture_output=True)
    else:
        target.symlink_to(source, target_is_directory=True)


def _custom_user_data(name):
    if sys.platform == 'win32':
        base = Path(os.environ['APPDATA'])
    elif sys.platform == 'darwin':
        base = Path.home() / 'Library/Application Support'
    else:
        base = Path(os.environ.get('XDG_DATA_HOME', str(Path.home() / '.local/share')))
    return base / name


def prepare(csharp=False, editor=False, mock_ui=False, isolated_user_data=False):
    """Link runtime assets; only explicit C# tools receive a .NET project."""
    scratch = ROOT / '.scratch'
    scratch.mkdir(exist_ok=True)
    temporary = tempfile.TemporaryDirectory(prefix='godot-tools-', dir=scratch)
    work = Path(temporary.name)
    user_name = 'DuelView.Tools/' + work.name if isolated_user_data or mock_ui else None
    user_data = _custom_user_data(user_name) if user_name else None
    _workspaces[work] = (temporary, user_data)
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
        source = re.sub(r'\n\[dotnet\]\n.*?(?=\n\[|\Z)', '', source, flags=re.DOTALL)
        if not mock_ui:
            source = source.replace('run/main_scene="res://scenes/Host/Main.tscn"',
                                    'run/main_scene="res://sandboxes/MapSandbox.tscn"')
    if user_name:
        source = source.replace('[application]\n', '[application]\nconfig/use_custom_user_dir=true\n'
                                f'config/custom_user_dir="{user_name}"\n')
    if editor:
        source = source.replace('enabled=PackedStringArray()',
            'enabled=PackedStringArray("res://addons/dv_checker/plugin.cfg", "res://addons/rpg_asset_adapter/plugin.cfg")')
    source += '\n[duelview_tools]\nrepository="' + ROOT.as_posix() + '"\n'
    (work / 'project.godot').write_text(source, encoding='utf-8')
    if csharp:
        bridge = escape((ROOT / 'dv_core/host_bridge/DuelView.Session.csproj').as_posix())
        project = f'''<Project Sdk="Godot.NET.Sdk/4.6.1">
  <PropertyGroup><TargetFramework>net8.0</TargetFramework><EnableDynamicLoading>true</EnableDynamicLoading>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup><Compile Include="scenes/**/*.cs" /><Compile Include="tools/FrameArchive.cs" />
    <ProjectReference Include="{bridge}" />
  </ItemGroup>
</Project>'''
        (work / 'DuelView.Tools.csproj').write_text(project, encoding='utf-8')
        shutil.copyfile(GAME / 'NuGet.Config', work / 'NuGet.Config')
    return work


def cleanup(work: Path | None = None):
    selected = [work] if work is not None else list(_workspaces)
    for path in selected:
        temporary, user_data = _workspaces.pop(path)
        try:
            if user_data is not None and user_data.exists():
                shutil.rmtree(user_data)
        finally:
            temporary.cleanup()


@contextmanager
def workspace(**options):
    work = prepare(**options)
    try:
        yield work
    finally:
        cleanup(work)


atexit.register(cleanup)


def build(work: Path):
    subprocess.run(['dotnet', 'build', str(work / 'DuelView.Tools.csproj'),
                    '--disable-build-servers', '-p:NuGetAudit=false'],
                   stdout=sys.stderr, stderr=sys.stderr, check=True)


def import_assets(work: Path, godot, log_path: Path | None = None):
    """Populate this workspace's import cache before opening its scene or tool."""
    command = [str(godot), '--headless', '--editor', '--path', str(work), '--import', '--quit']
    if log_path is not None:
        command += ['--log-file', str(log_path)]
    subprocess.run(command, cwd=ROOT, stdout=sys.stderr, stderr=sys.stderr, check=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--godot', default=os.environ.get('GODOT') or os.environ.get('GODOT_BIN') or shutil.which('godot'))
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument('--sandbox', action='store_true')
    mode.add_argument('--editor', action='store_true')
    mode.add_argument('--mock-ui', action='store_true', help='Run Main with the pure-GDScript mock session')
    mode.add_argument('--script')
    parser.add_argument('--csharp', action='store_true')
    parser.add_argument('--headless', action='store_true')
    parser.add_argument('--isolated-user-data', action='store_true', help='Use disposable tool-owned preferences and saves')
    parser.add_argument('arguments', nargs=argparse.REMAINDER)
    args = parser.parse_args()
    if not args.godot:
        parser.error('Supply --godot or set GODOT to the installed Godot executable')
    if args.mock_ui and args.csharp:
        parser.error('--mock-ui uses a pure-GDScript project; select one workspace mode')
    with workspace(csharp=args.csharp, editor=args.editor, mock_ui=args.mock_ui,
                   isolated_user_data=args.isolated_user_data) as work:
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
        arguments = args.arguments[1:] if args.arguments[:1] == ['--'] else args.arguments
        if args.mock_ui:
            arguments = ['--dv-mock', *arguments]
        if arguments:
            command += ['--', *arguments]
        return subprocess.call(command, cwd=ROOT)


if __name__ == '__main__':
    raise SystemExit(main())
