"""Generate and inspect a native-art UI preview; this is not production UI acceptance."""
from __future__ import annotations

import hashlib
import io
import json
import os
import re
from pathlib import Path
import shutil
import subprocess
import sys

from PIL import Image, ImageDraw
import workflow as w
from pixel_handoff import _make_aseprite

SOURCE = w.GAME/'AssetsSource/Internal/Props/FirstAwakeningInterior'
OUTPUT = w.GAME/'ShareAsset/Internal/Props/FirstAwakeningInterior'
SCENE = w.GAME/'scenes/Shared/UI/NativeArtPreview.tscn'
MANIFEST = SOURCE/'mind-ui.json'
sys.path.insert(0, str(w.GAME/'tools'))
from first_awakening_art import mind_ui as native_ui
COLORS = native_ui.PAPER_COLORS
RECIPE_HASHES = {Path(path): hashlib.sha256(Path(path).read_bytes()).hexdigest()
                 for path in (__file__, native_ui.__file__)}


def ui_design_contract() -> dict:
    """Return verified Godot 4.6 UI rules and current project evidence, not universal style bans."""
    return dict(palette=COLORS, project_grid=32,
        source='docs/design-standard.md',
        rules=['Hand-placed native pixels on paper; 640x496 notebook displayed at 2x. No generated image or resampling.',
            'StyleBoxTexture: tile both axes only with periodic source strips; explicit texture and content margins.',
            'Button: normal/hover/pressed/hover_pressed/disabled plus transparent pixel focus overlay.',
            'Pressed top +1, bottom -1 with constant sum; align_to_largest_stylebox=0.',
            'StyleBoxFlat fallback: zero corner radii, integer 1..2 borders, anti_aliasing=false.',
            'Keep visible keyboard focus; do not replace it with StyleBoxEmpty.',
            'Avoid pure white default text; measure foreground/background contrast, never optimize for illegibility.',
            'ColorRect is allowed for actual overlays; do not build a panel background from redundant nodes.',
            'Three lines by 24 characters is a tunable editorial budget; use engine font measurement for physical fit.'],
        limits=['Scene preview is a theme fixture, not playable case evidence.',
            'Theme styles do not override local theme overrides; runtime adoption must remove those deliberately.',
            'Do not change Core rules, dialogue control flow or actor knowledge to satisfy a visual checker.'])


def _color(value):
    rgb=tuple(int(value[i:i+2],16)/255 for i in (1,3,5))
    return 'Color(%s, %s, %s, 1)' % tuple(f'{v:.6f}' for v in rgb)


def _valid_ids(text):
    return re.sub(r'(id="|Resource\(")([a-z-]+)(")',lambda m:m[1]+m[2].replace('-','_')+m[3],text)


def _slice(kind):
    return native_ui.paper_slice(kind)


def _resources():
    kinds=('dossier','frame','editor','button-normal','button-hover','button-pressed','button-disabled','focus')
    textures='\n'.join(f'[ext_resource type="Texture2D" path="{w.res(OUTPUT/("mind-ui-"+kind+".png"))}" id="{kind}"]' for kind in kinds)
    styles=[]
    for kind in kinds:
        cut=4 if kind.startswith('button') or kind=='editor' else 8
        margins=[14,7,14,7] if kind.startswith('button') else [24,24,24,24] if kind=='dossier' else [12,12,12,12] if kind=='frame' else [6,6,6,6]
        if kind=='button-pressed': margins[1]+=1; margins[3]-=1
        style=f'[sub_resource type="StyleBoxTexture" id="{kind}"]\ntexture = ExtResource("{kind}")\naxis_stretch_horizontal = 1\naxis_stretch_vertical = 1\n'
        for side,margin in zip(('left','top','right','bottom'),margins):
            style+=f'texture_margin_{side} = {cut}.0\ncontent_margin_{side} = {margin}.0\n'
        if kind=='focus': style+='draw_center = false\n'
        styles.append(style)
    styles.append('[sub_resource type="SystemFont" id="font"]\nfont_names = PackedStringArray("SimSun", "Noto Serif CJK SC", "Times New Roman")\n')
    theme='default_font = SubResource("font")\ndefault_font_size = 20\n'
    for control in ('Label','Button','TextEdit'):
        theme+=f'{control}/colors/font_color = {_color(COLORS["text"])}\n'
    for state in ('normal','hover','pressed','disabled','hover_pressed','focus'):
        resource='focus' if state=='focus' else 'button-pressed' if state=='hover_pressed' else 'button-'+state
        theme+=f'Button/styles/{state} = SubResource("{resource}")\n'
        if state!='normal': theme+=f'Button/colors/font_{state}_color = {_color(COLORS["muted"] if state=="disabled" else COLORS["text"])}\n'
    theme+='Button/constants/align_to_largest_stylebox = 0\n'
    theme+='PanelContainer/styles/panel = SubResource("frame")\nPanel/styles/panel = SubResource("frame")\n'
    theme+='Dossier/base_type = &"PanelContainer"\nDossier/styles/panel = SubResource("dossier")\n'
    for state in ('normal','read_only'): theme+=f'TextEdit/styles/{state} = SubResource("editor")\n'
    theme+='TextEdit/styles/focus = SubResource("focus")\n'
    for key,value in dict(caret_color='text',font_placeholder_color='muted',font_readonly_color='muted',selection_color='edge').items():
        theme+=f'TextEdit/colors/{key} = {_color(COLORS[value])}\n'
    theme+='HSeparator/styles/separator = SubResource("button-disabled")\nHSeparator/constants/separation = 8\n'
    for control in ('LineEdit', 'OptionButton'):
        theme+=f'{control}/colors/font_color = {_color(COLORS["text"])}\n'
        for state in ('normal','hover','pressed','focus'):
            resource='focus' if state=='focus' else 'button-'+state
            theme+=f'{control}/styles/{state} = SubResource("{resource}")\n'
    theme+=f'LineEdit/colors/caret_color = {_color(COLORS["text"])}\n'
    theme+=f'LineEdit/colors/font_placeholder_color = {_color(COLORS["muted"])}\n'
    theme+=f'TextEdit/colors/font_selected_color = {_color(COLORS["text"])}\n'
    theme+=f'TextEdit/colors/selection_color = {_color(COLORS["hover"])}\n'
    return textures,'\n'.join(styles),theme


def _scene_text(textures,styles,theme):
    header='[gd_scene load_steps=19 format=3]\n\n'+textures+'\n\n'+styles+'\n[sub_resource type="Theme" id="theme"]\n'+theme
    # Nodes deliberately use normal Godot layout containers, no background ColorRect.
    return header+'''
[node name="MindThemePreview" type="CanvasLayer"]

[node name="Root" type="PanelContainer" parent="."]
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
grow_horizontal = 2
grow_vertical = 2
texture_filter = 1
theme = SubResource("theme")
theme_override_styles/panel = SubResource("frame")

[node name="Margin" type="MarginContainer" parent="Root"]
layout_mode = 2
theme_override_constants/margin_left = 12
theme_override_constants/margin_top = 12
theme_override_constants/margin_right = 12
theme_override_constants/margin_bottom = 12

[node name="Columns" type="HBoxContainer" parent="Root/Margin"]
layout_mode = 2
theme_override_constants/separation = 24

[node name="Review" type="PanelContainer" parent="Root/Margin/Columns"]
layout_mode = 2
size_flags_horizontal = 3
size_flags_stretch_ratio = 1.1
theme_override_styles/panel = SubResource("frame")

[node name="Stack" type="VBoxContainer" parent="Root/Margin/Columns/Review"]
layout_mode = 2
theme_override_constants/separation = 16

[node name="Title" type="Label" parent="Root/Margin/Columns/Review/Stack"]
layout_mode = 2
text = "推演板 · 样式验收"
theme_override_font_sizes/font_size = 26

[node name="Observation" type="Label" parent="Root/Margin/Columns/Review/Stack"]
layout_mode = 2
text = "已知观察、提出的假设与重演结果分别记录。\n此页只验收样式，不提供案件结论。"
autowrap_mode = 3

[node name="Space" type="Control" parent="Root/Margin/Columns/Review/Stack"]
layout_mode = 2
size_flags_vertical = 3

[node name="Controls" type="HFlowContainer" parent="Root/Margin/Columns/Review/Stack"]
layout_mode = 2
theme_override_constants/h_separation = 12
theme_override_constants/v_separation = 12

[node name="Play" type="Button" parent="Root/Margin/Columns/Review/Stack/Controls"]
layout_mode = 2
text = "播放 / 暂停"
toggle_mode = true
theme_override_styles/normal = SubResource("button-normal")
theme_override_styles/hover = SubResource("button-hover")
theme_override_styles/pressed = SubResource("button-pressed")
theme_override_styles/focus = SubResource("focus")

[node name="View" type="Button" parent="Root/Margin/Columns/Review/Stack/Controls"]
layout_mode = 2
text = "门厅视角"

[node name="Disabled" type="Button" parent="Root/Margin/Columns/Review/Stack/Controls"]
layout_mode = 2
text = "尚未核对"
disabled = true

[node name="Dossier" type="PanelContainer" parent="Root/Margin/Columns"]
layout_mode = 2
size_flags_horizontal = 3
theme_type_variation = &"Dossier"
theme_override_styles/panel = SubResource("dossier")

[node name="Stack" type="VBoxContainer" parent="Root/Margin/Columns/Dossier"]
layout_mode = 2
theme_override_constants/separation = 16

[node name="Title" type="Label" parent="Root/Margin/Columns/Dossier/Stack"]
layout_mode = 2
text = "行为卷宗"
theme_override_font_sizes/font_size = 26

[node name="Rule" type="HSeparator" parent="Root/Margin/Columns/Dossier/Stack"]
layout_mode = 2

[node name="Draft" type="TextEdit" parent="Root/Margin/Columns/Dossier/Stack"]
layout_mode = 2
custom_minimum_size = Vector2(280, 320)
size_flags_vertical = 3
placeholder_text = "写下人物准备采取的行动……"
wrap_mode = 1

[node name="Actions" type="HFlowContainer" parent="Root/Margin/Columns/Dossier/Stack"]
layout_mode = 2
theme_override_constants/h_separation = 12

[node name="Words" type="Button" parent="Root/Margin/Columns/Dossier/Stack/Actions"]
layout_mode = 2
text = "词汇"

[node name="Run" type="Button" parent="Root/Margin/Columns/Dossier/Stack/Actions"]
layout_mode = 2
text = "从头重演"
'''


def build_diegetic_ui(replace: bool = False) -> dict:
    """Build native pixel nine-slices, editable sources, shared Theme and generic preview .tscn.
    Fixed registered asset family. Refuses to overwrite unowned or artist-edited files even with replace.
    No runtime scene logic or current dialogue is changed by this tool.
    """
    for path, loaded_hash in RECIPE_HASHES.items():
        if hashlib.sha256(path.read_bytes()).hexdigest() != loaded_hash:
            raise ValueError('UI recipe changed since this MCP process loaded it; restart before rebuilding: '+str(path))
    w.within(MANIFEST,SOURCE)
    textures,styles,theme=_resources()
    outputs={OUTPUT/'mind_theme.tres':_valid_ids('[gd_resource type="Theme" load_steps=18 format=3]\n\n'+textures+'\n\n'+styles+'\n[resource]\n'+theme).encode(),
             SCENE:_valid_ids(_scene_text(textures,styles,theme)).encode()}
    sheet=native_ui.notebook()
    sheet_stream=io.BytesIO(); sheet.save(sheet_stream,format='PNG')
    for folder in (SOURCE,OUTPUT): outputs[folder/'mind-notebook.png']=sheet_stream.getvalue()
    outputs[SOURCE/'mind-notebook.aseprite']=_make_aseprite([('paper',sheet)])
    specs=[]
    for kind in ('dossier','frame','editor','button-normal','button-hover','button-pressed','button-disabled','focus'):
        im=_slice(kind)
        stream=io.BytesIO(); im.save(stream,format='PNG')
        for folder in (SOURCE,OUTPUT): outputs[folder/('mind-ui-'+kind+'.png')]=stream.getvalue()
        outputs[SOURCE/('mind-ui-'+kind+'.aseprite')]=_make_aseprite([(kind,im)])
        cut=4 if kind.startswith('button') or kind=='editor' else 8
        padding=[14,7,14,7] if kind.startswith('button') else [24]*4 if kind=='dossier' else [12]*4 if kind=='frame' else [6]*4
        if kind=='button-pressed': padding=[14,8,14,6]
        specs.append(dict(name=kind,size=list(im.size),texture_margins=[cut]*4,
            content_margins=padding,margin_order=['left','top','right','bottom'],
            slice_lines_x=[cut,im.width-cut],slice_lines_y=[cut,im.height-cut],
            stretch='tile',texture_filter='nearest',artist_notes='Preserve corner silhouettes and periodic centre/edge strips.'))
    old=json.loads(MANIFEST.read_text(encoding='utf-8')) if MANIFEST.exists() else {}
    for path in outputs:
        root=SOURCE if path.is_relative_to(SOURCE) else OUTPUT if path.is_relative_to(OUTPUT) else SCENE.parent
        w.within(path,root)
        if path.exists():
            if not replace: raise ValueError('UI output exists; use replace=true for an intentional owned-file rebuild')
            if old.get('files',{}).get(str(path.relative_to(w.REPO)))!=hashlib.sha256(path.read_bytes()).hexdigest():
                raise ValueError('Unowned/artist-edited output preserved: '+str(path))
    for path,content in outputs.items():
        path.parent.mkdir(parents=True,exist_ok=True)
        path.write_bytes(content)
    manifest=dict(palette=COLORS,slices=specs,theme=w.res(OUTPUT/'mind_theme.tres'),preview=w.res(SCENE),
        recipe_hashes={str(p.relative_to(w.REPO)):digest for p,digest in RECIPE_HASHES.items()},
        notebook=dict(file='mind-notebook.png',native_size=[640,496],display_scale=2,
                      recipe='tools/first_awakening_art/mind_ui.py',process='native integer dots and lines, no resampling'),
        files={str(p.relative_to(w.REPO)):hashlib.sha256(content).hexdigest() for p,content in outputs.items()},
        acceptance='native-art preview only; inspect with Godot for asset and layout diagnostics. This does not certify or replace production in-game UI.')
    w.save_json(MANIFEST,manifest)
    return dict(theme=manifest['theme'],preview=manifest['preview'],slice_manifest=str(MANIFEST),slice_count=len(specs))


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


def inspect_godot_ui(viewport: list[int] | None = None) -> dict:
    """Load the generated native-art preview in Godot and inspect its styles/layout, not production UI."""
    size=viewport or [1280,992]
    if len(size)!=2 or any(type(v) is not int for v in size) or not (960<=size[0]<=2560 and 640<=size[1]<=1440):
        raise ValueError('viewport must be integer [width,height] in 960..2560 by 640..1440')
    return _engine(dict(mode='scene',scene=w.res(SCENE),viewport=size))


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
