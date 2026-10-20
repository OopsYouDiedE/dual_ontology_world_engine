"""Inspect existing Godot UI scenes and measure text with the actual engine fonts."""
from __future__ import annotations

import json
import os
import shutil
import subprocess

import workflow as w


def ui_design_contract() -> dict:
    """Return current authoring inputs and the limits of these engineering tools."""
    return dict(
        source='docs/design-standard.md',
        scenes='godot_csharp/scenes/UI',
        themes='godot_csharp/tools/build_ui_theme.py',
        rules=[
            'Use the current design standard and shared Theme resources.',
            'Inspect resolved styles, keyboard focus, containment and real font metrics.',
            'Preserve dialogue order and actor knowledge when adjusting presentation.'
        ],
        limits=[
            'Scene inspection is UI engineering evidence; it does not open a DV session.',
            'Text measurement reports physical fit, not narrative or artistic quality.',
            'This service does not generate the retired FirstAwakening UI theme.'
        ])


def _engine(request):
    executable=os.environ.get('GODOT_BIN') or shutil.which('godot')
    if not executable: raise ValueError('Set GODOT_BIN to the project Godot 4.6 .NET executable')
    w.REPORT.mkdir(parents=True,exist_ok=True)
    request_path=w.REPORT/'ui-request.json'
    report_path=w.REPORT/'ui-engine-report.json'
    w.save_json(request_path,request)
    if report_path.exists(): report_path.unlink()
    args=[executable,'--headless','--path',str(w.GAME),'--log-file',str(w.REPORT/'ui-engine.log'),
          '--script','res://addons/rpg_asset_adapter/mcp/ui_inspect.gd','--',str(request_path)]
    result=subprocess.run(args,capture_output=True,text=True,encoding='utf-8',errors='replace',timeout=90)
    log=result.stdout+'\n'+result.stderr
    (w.REPORT/'ui-engine-output.log').write_text(log,encoding='utf-8')
    diagnostics=[line for line in log.splitlines() if line.startswith('ERROR:')]
    fatal=[line for line in diagnostics if line!='ERROR: Failed to read the root certificate store.']
    if result.returncode or fatal or 'SCRIPT ERROR:' in log or not report_path.exists():
        raise ValueError('Godot UI check failed: '+log[-5000:])
    report=json.loads(report_path.read_text(encoding='utf-8'))
    report['engine_log']=str(w.REPORT/'ui-engine-output.log')
    report['environment_diagnostics']=diagnostics
    return report


def inspect_godot_ui(viewport: list[int] | None = None,
                     scene: str = "res://scenes/UI/UISystem.tscn") -> dict:
    """Inspect an existing UI scene in Godot; does not open a DV game session."""
    size = viewport or [1280, 992]
    if len(size) != 2 or any(type(v) is not int for v in size) or not (960 <= size[0] <= 2560 and 640 <= size[1] <= 1440):
        raise ValueError('viewport must be integer [width,height] in 960..2560 by 640..1440')
    path = w.within(w.read_path(scene), w.GAME / 'scenes/UI')
    if path.suffix != '.tscn' or not path.is_file():
        raise ValueError('scene must name an existing .tscn below scenes/UI')
    return _engine(dict(mode='scene', scene=w.res(path), viewport=size))

def measure_dialogue_text(texts: list[str], width: int = 1088, height: int = 110,
                          font_size: int = 23, font_path: str | None = None) -> dict:
    """Measure CJK wrapping with real Godot TextParagraph; this is physical fit, not literary quality."""
    if not 1<=len(texts)<=200 or any(not isinstance(t,str) or len(t)>10000 for t in texts):
        raise ValueError('Provide 1..200 texts, each at most 10000 characters')
    if any(type(v) is not int for v in (width,height,font_size)) or not 1<=width<=4096 or not 1<=height<=4096 or not 8<=font_size<=96:
        raise ValueError('width/height 1..4096, font_size 8..96')
    if font_path:
        path=w.within(w.read_path(font_path),w.GAME/'ShareAsset')
        if path.suffix.lower() not in ('.ttf','.otf','.tres') or not path.is_file(): raise ValueError('font_path must be a runtime font resource')
        font_path=w.res(path)
    items=[dict(text=t,width=width,height=height,font_size=font_size) for t in texts]
    if font_path:
        for item in items: item['font_path']=font_path
    return _engine(dict(mode='text',items=items))
