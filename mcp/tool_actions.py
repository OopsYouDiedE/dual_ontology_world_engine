"""On-demand implementations for the repository's reusable command tools."""
from __future__ import annotations

import importlib
import os
from pathlib import Path
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
ASSET_COMMANDS = {
    'living-floor': 'build_living_floor.py', 'chapter-maps': 'build_chapter2_scenes.py',
    'anime-cast': 'build_anime_cast.py', 'pixel-cast': 'build_pixel_cast.py',
    'dialogue-cast': 'build_dialogue_cast.py', 'character-atlas': 'build_character_atlas.py',
    'desk': 'build_painterly_desk.py', 'desk-surfaces': 'build_desk_surface_maps.py',
    'room-props': 'build_room_table_props.py', 'scene-surfaces': 'build_scene_surfaces.py',
    'materials': 'build_surface_materials.py', 'ui-theme': 'build_ui_theme.py',
    'ui-paper': 'build_ui_paper.py', 'ui-desk': 'build_ui_desk.py',
    'translations': 'build_mind_ui_translations.py', 'geometry-contract': 'export_geometry.py',
}
DIAGNOSTIC_COMMANDS = {
    'logic-session': 'run_debug.py', 'checkpoint': 'debug_checkpoints.py',
    'raw-dag': 'raw_dag.py', 'capture-save': 'run_saved_game_capture.py',
    'profile': 'profile_dv_capture.py', 'timing': 'summarize_interaction_timing.py',
    'hitches': 'report_runtime_hitches.py', 'encode': 'encode_dv_capture.py',
    'geometry': 'check_geometry_contracts.py', 'paths': 'audit_action_paths.py',
}


def _run(command, timeout=600):
    result = subprocess.run(command, cwd=ROOT, capture_output=True, text=True,
                            encoding='utf-8', errors='replace', timeout=timeout)
    if result.returncode:
        raise RuntimeError(result.stderr or result.stdout)
    return {'exit_code': result.returncode, 'stdout': result.stdout, 'stderr': result.stderr}


def assets(operation: str, arguments: list[str] | None = None):
    script = str(ROOT / 'mcp/asset_pipeline' / ASSET_COMMANDS[operation])
    if operation in ('desk', 'desk-surfaces'):
        blender = os.environ.get('BLENDER') or shutil.which('blender')
        if not blender:
            raise RuntimeError('Set BLENDER to the Blender executable for the desk geometry pipeline')
        return _run([blender, '--background', '--python', script, '--', *(arguments or [])], timeout=1800)
    return _run([sys.executable, script, *(arguments or [])])


def diagnostics(operation: str, arguments: list[str] | None = None):
    return _run([sys.executable, str(ROOT / 'mcp/verification' / DIAGNOSTIC_COMMANDS[operation]), *(arguments or [])])


def music(arguments: list[str] | None = None):
    return _run([sys.executable, str(ROOT / 'mcp/music_pipeline/build_soundtrack.py'), *(arguments or [])], timeout=3600)


def compiler_prewarm():
    sys.path.insert(0, str(ROOT / 'mcp/build'))
    toolchain = importlib.import_module('dv_toolchain')
    return {'compiler': str(toolchain.compiler())}


def cli(arguments: list[str] | None = None):
    return _run(['dotnet', 'run', '--project', str(ROOT / 'dv_core/cli/DvCli.csproj'), '--', *(arguments or [])])


def sandbox(godot: str = '', mode: str = 'map'):
    command = [sys.executable, str(ROOT / 'mcp/godot_workspace.py')]
    if godot:
        command += ['--godot', godot]
    command += ['--sandbox'] if mode == 'map' else ['--editor']
    process = subprocess.Popen(command, cwd=ROOT, stdin=subprocess.DEVNULL,
                               stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    return {'pid': process.pid, 'mode': mode, 'workspace_lifetime': 'until the Godot window closes'}


def adapter(operation: str, arguments: dict | None = None):
    owners = {
        'inspect_image': 'workflow', 'draw_asset': 'workflow', 'adapt_asset': 'workflow',
        'build_tiles': 'workflow', 'audit_layout': 'workflow', 'compose_map': 'workflow', 'export_godot': 'workflow',
        'tilemap_workflow': 'workflow',
        'native_art_catalog': 'native_art', 'read_native_recipe': 'native_art',
        'render_native_prop': 'native_art', 'render_native_material': 'native_art', 'native_art_contact_sheet': 'native_art',
        'inspect_pixel_style': 'pixel_handoff', 'quantize_asset': 'pixel_handoff',
        'layered_sprite': 'pixel_handoff', 'export_aseprite': 'pixel_handoff',
        'review_dialogue': 'production', 'create_plan': 'production', 'read_plan': 'production',
        'update_task': 'production', 'next_task': 'production',
        'ui_design_contract': 'ui_workflow', 'inspect_godot_ui': 'ui_workflow', 'measure_dialogue_text': 'ui_workflow',
    }
    sys.path.insert(0, str(ROOT / 'mcp/rpg_adapter/mcp'))
    function = getattr(importlib.import_module(owners[operation]), operation)
    return function(**(arguments or {}))
