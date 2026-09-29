"""Rebuild the First Awakening life wing from native 1x pixel recipes.

Writes the painted architecture, furniture sprites, a readable layout resource,
and the editable FirstAwakening scene. Nothing here resamples an
image: each pixel is placed by tools/first_awakening_art/ at game scale.

Run from the repository root:  python godot_csharp/tools/build_first_awakening_interior.py
"""
import json
import re
import sys
from pathlib import Path

GAME = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(GAME / "tools"))

from first_awakening_art import architecture, layout, placement, props  # noqa: E402

TARGET = GAME / "ShareAsset/Internal/Props/FirstAwakeningInterior"
SCENE = GAME / "scenes/FirstAwakening/FirstAwakening.tscn"
SITES = GAME / "scenes/FirstAwakening/sites.tres"


def _site_triggers() -> list[tuple[str, float, float, float]]:
    """Use the readable catalog as the sole source for scene trigger geometry."""
    result = []
    for line in SITES.read_text(encoding="utf-8").splitlines():
        match = re.search(r'\{"id": "([^"]+)".*"point": Vector2\(([^,]+), ([^)]+)\).*"reach": ([0-9.]+)', line)
        if match:
            result.append((match[1], float(match[2]), float(match[3]), float(match[4])))
    if not result or len({site[0] for site in result}) != len(result):
        raise ValueError(f"Investigation catalog must contain unique sites: {SITES}")
    return result


def _vec(x: float, y: float) -> str:
    return f"Vector2({x}, {y})"


def _rect_shape(lines: list[str], parent: str, name: str, rect: list[int], shape_id: str) -> None:
    x, y, w, h = rect
    lines.extend([
        f'[sub_resource type="RectangleShape2D" id="{shape_id}"]',
        f'size = {_vec(w, h)}',
        '',
    ])


def write_scene(data: dict) -> None:
    """Serialize fixed scene topology; gameplay state stays in GDScript."""
    sprite_canvases = props.build()
    sprites = {item["sprite"] for item in data["furniture"]}
    sprites.update({"life_wing", "isolation_gate", "isolation_gate_open", "inner_bolt", "stop_base", "bed_0"})
    resources = [
        ('Script', 'res://scenes/FirstAwakening/FirstAwakening.gd', 'main'),
        ('Script', 'res://scenes/FirstAwakening/RoomDetails.gd', 'details'),
        ('Script', 'res://scenes/Story/StoryActor.gd', 'actor'),
        ('Script', 'res://scenes/FirstAwakening/RoomDoor.gd', 'room_door'),
        ('Script', 'res://scenes/FirstAwakening/HintOverlay.gd', 'hints'),
        ('Script', 'res://scenes/FirstAwakening/InvestigationSite.gd', 'investigation_site'),
        ('Script', 'res://scenes/FirstAwakening/FieldModal.gd', 'field_modal'),
        ('PackedScene', 'res://scenes/FirstAwakening/FirstAwakeningDialogue.tscn', 'dialogue'),
        ('Script', 'res://scenes/FirstAwakening/HallReplayBridge.cs', 'bridge'),
        ('AudioStream', 'res://ShareAsset/Internal/Props/first_awakening_reasoning_piano.mp3', 'reasoning_music'),
        ('Texture2D', 'res://ShareAsset/Internal/Props/FirstAwakeningInterior/mind-ui-dossier.png', 'paper_card'),
    ]
    resources += [('Texture2D', f'res://ShareAsset/Internal/Props/FirstAwakeningInterior/{name}.png', name) for name in sorted(sprites)]
    resources += [('SpriteFrames', f'res://ShareAsset/Internal/Characters/animation_v2/{name}.tres', name + '_frames') for name in ('detective', 'engineer', 'chef', 'musician')]
    lines = ['[gd_scene format=3]', '']
    lines += [f'[ext_resource type="{kind}" path="{path}" id="{key}"]' for kind, path, key in resources]
    lines += ['', '[sub_resource type="AtlasTexture" id="bed_quilt"]', 'atlas = ExtResource("bed_0")']
    # The first 30 pixels are behind the sleeper; the remainder is foreground.
    bed_height = sprite_canvases['bed_0'].h
    bed_width = sprite_canvases['bed_0'].w
    lines += [f'region = Rect2(0, 30, {bed_width}, {bed_height - 30})', '']
    lines += [
        '[sub_resource type="RectangleShape2D" id="dorm_leaf"]',
        'size = Vector2(64, 8)', '',
        '[sub_resource type="RectangleShape2D" id="actor_foot"]',
        'size = Vector2(16, 12)', '',
        '[sub_resource type="Gradient" id="light_falloff"]',
        'offsets = PackedFloat32Array(0, 0.45, 1)',
        'colors = PackedColorArray(1, 1, 1, 0.85, 1, 1, 1, 0.3, 1, 1, 1, 0)', '',
        '[sub_resource type="Gradient" id="dust_fade"]',
        'offsets = PackedFloat32Array(0, 0.5, 1)',
        'colors = PackedColorArray(1, 0.97, 0.9, 0, 1, 0.97, 0.9, 0.55, 1, 0.97, 0.9, 0)', '',
    ]
    site_triggers = _site_triggers()
    for radius in sorted({reach for _, _, _, reach in site_triggers}):
        lines += [f'[sub_resource type="CircleShape2D" id="site_reach_{int(radius)}"]',
                  f'radius = {radius}', '']
    light_sizes = sorted({int(light['size']) for light in data['lights']} | {120})
    for diameter in light_sizes:
        lines += [f'[sub_resource type="GradientTexture2D" id="light_{diameter}"]',
                  'gradient = SubResource("light_falloff")',
                  f'width = {diameter}', f'height = {diameter}',
                  'fill = 1', 'fill_from = Vector2(0.5, 0.5)', 'fill_to = Vector2(1, 0.5)', '']
    lines += [
        '[sub_resource type="Gradient" id="hud_vignette_gradient"]',
        'offsets = PackedFloat32Array(0, 0.55, 1)',
        'colors = PackedColorArray(0.03, 0.02, 0.05, 0, 0.03, 0.02, 0.05, 0, 0.03, 0.02, 0.05, 0.42)', '',
        '[sub_resource type="GradientTexture2D" id="hud_vignette"]',
        'gradient = SubResource("hud_vignette_gradient")',
        'fill = 1', 'fill_from = Vector2(0.5, 0.5)', 'fill_to = Vector2(1.05, 1.05)', '',
        '[sub_resource type="Gradient" id="hud_top_gradient"]',
        'colors = PackedColorArray(0.03, 0.02, 0.05, 0.62, 0.03, 0.02, 0.05, 0)', '',
        '[sub_resource type="Gradient" id="hud_bottom_gradient"]',
        'colors = PackedColorArray(0.03, 0.02, 0.05, 0, 0.03, 0.02, 0.05, 0.62)', '',
        '[sub_resource type="GradientTexture2D" id="hud_top"]',
        'gradient = SubResource("hud_top_gradient")', 'width = 4', 'height = 64',
        'fill_from = Vector2(0, 0)', 'fill_to = Vector2(0, 1)', '',
        '[sub_resource type="GradientTexture2D" id="hud_bottom"]',
        'gradient = SubResource("hud_bottom_gradient")', 'width = 4', 'height = 64',
        'fill_from = Vector2(0, 0)', 'fill_to = Vector2(0, 1)', '',
        '[sub_resource type="StyleBoxTexture" id="modal_panel"]',
        'texture = ExtResource("paper_card")',
        'axis_stretch_horizontal = 1',
        'axis_stretch_vertical = 1',
        'texture_margin_left = 8.0',
        'texture_margin_top = 8.0',
        'texture_margin_right = 8.0',
        'texture_margin_bottom = 8.0',
        'content_margin_left = 40.0',
        'content_margin_top = 32.0',
        'content_margin_right = 40.0',
        'content_margin_bottom = 32.0',
        '',
        '[sub_resource type="StyleBoxEmpty" id="flow_panel"]',
        '',
        '[sub_resource type="StyleBoxTexture" id="answer_panel"]',
        'texture = ExtResource("paper_card")',
        'axis_stretch_horizontal = 1',
        'axis_stretch_vertical = 1',
        'texture_margin_left = 8.0',
        'texture_margin_top = 8.0',
        'texture_margin_right = 8.0',
        'texture_margin_bottom = 8.0',
        'content_margin_left = 48.0',
        'content_margin_top = 40.0',
        'content_margin_right = 48.0',
        'content_margin_bottom = 40.0',
        '',
    ]
    for index, rect in enumerate(data['solids']):
        _rect_shape(lines, 'WallSolids', str(index), rect, f'wall_{index}')
    for index, item in enumerate(data['furniture']):
        if item['footprint']:
            _rect_shape(lines, 'FurnitureSolids', str(index), item['footprint'], f'foot_{index}')
    _rect_shape(lines, 'FurnitureSolids', 'stop', [data['stop']['now'][0] - 10, data['stop']['now'][1] - 8, 20, 8], 'stop_foot')
    lines += [
        '[node name="FirstAwakening" type="Node2D"]',
        'texture_filter = 1',
        'script = ExtResource("main")', '',
        '[node name="WakePosition" type="Marker2D" parent="."]',
        'position = Vector2(154, 184)', '',
        '[node name="MeetingSpots" type="Node2D" parent="."]', '',
        '[node name="Engineer" type="Marker2D" parent="MeetingSpots"]',
        'position = Vector2(232, 680)', '',
        '[node name="Chef" type="Marker2D" parent="MeetingSpots"]',
        'position = Vector2(320, 760)', '',
        '[node name="Musician" type="Marker2D" parent="MeetingSpots"]',
        'position = Vector2(408, 680)', '',
        '[node name="Detective" type="Marker2D" parent="MeetingSpots"]',
        'position = Vector2(320, 600)', '',
        '[node name="Darkness" type="ColorRect" parent="."]',
        'z_index = -10',
        'offset_left = -640.0', 'offset_top = -512.0',
        'offset_right = 1920.0', 'offset_bottom = 1536.0',
        'color = Color(0.0313726, 0.0431373, 0.054902, 1)',
        'mouse_filter = 2', '',
        '[node name="Ambient" type="CanvasModulate" parent="."]',
        'color = Color(0.615686, 0.596078, 0.678431, 1)', '',
        '[node name="Lighting" type="Node2D" parent="."]', '',
    ]
    for index, light in enumerate(data['lights']):
        tint = light['tint']
        color = tuple(int(tint[i:i + 2], 16) / 255 for i in (0, 2, 4))
        lines += [f'[node name="Lamp{index}" type="PointLight2D" parent="Lighting"]',
                  f'position = {_vec(*light["at"])}',
                  f'color = Color({color[0]:.6f}, {color[1]:.6f}, {color[2]:.6f}, 1)',
                  f'energy = {light["energy"]}', f'texture = SubResource("light_{light["size"]}")']
        if color[0] > color[2]:
            lines.append(f'metadata/base_energy = {light["energy"]}')
        lines.append('')
    for index, sconce in enumerate(data['sconces']):
        lines += [f'[node name="Sconce{index}" type="PointLight2D" parent="Lighting"]',
                  f'position = {_vec(sconce["x"], sconce["y"])}',
                  'color = Color(1, 0.760784, 0.478431, 1)',
                  'energy = 0.55', 'texture = SubResource("light_120")',
                  'metadata/base_energy = 0.55', '']
    for index, window in enumerate(data['windows']):
        lines += [f'[node name="WindowDust{index}" type="CPUParticles2D" parent="Lighting"]',
                  f'position = {_vec(window["x"], window["y"] + 90)}',
                  'z_index = 5', 'amount = 10', 'lifetime = 7.0',
                  'preprocess = 7.0', 'emission_shape = 3',
                  f'emission_rect_extents = {_vec(window["width"] * 0.6, 24)}',
                  'direction = Vector2(0.3, -1)', 'spread = 40.0',
                  'gravity = Vector2(0, 0)', 'initial_velocity_min = 1.5',
                  'initial_velocity_max = 4.0', 'color_ramp = SubResource("dust_fade")', '']
    lines += [
        '[node name="LifeWingArchitecture" type="Sprite2D" parent="."]',
        'texture = ExtResource("life_wing")', 'centered = false', '',
        '[node name="WallSolids" type="StaticBody2D" parent="."]', '',
    ]
    for index, (x, y, w, h) in enumerate(data['solids']):
        lines += [f'[node name="CollisionShape2D{index}" type="CollisionShape2D" parent="WallSolids"]',
                  f'position = {_vec(x + w / 2, y + h / 2)}', f'shape = SubResource("wall_{index}")', '']
    lines += ['[node name="FurnitureSolids" type="Node2D" parent="."]', '']
    for index, item in enumerate(data['furniture']):
        if not item['footprint']:
            continue
        x, y, w, h = item['footprint']
        lines += [f'[node name="{item["name"]}" type="StaticBody2D" parent="FurnitureSolids"]',
                  f'position = {_vec(x + w / 2, y + h / 2)}', '',
                  f'[node name="Shape" type="CollisionShape2D" parent="FurnitureSolids/{item["name"]}"]',
                  f'shape = SubResource("foot_{index}")', '']
    sx, sy = data['stop']['now']
    lines += [f'[node name="StopBase" type="StaticBody2D" parent="FurnitureSolids"]',
              f'position = {_vec(sx, sy - 4)}', '',
              '[node name="Shape" type="CollisionShape2D" parent="FurnitureSolids/StopBase"]',
              'shape = SubResource("stop_foot")', '',
              '[node name="RoomDetails" type="Node2D" parent="."]',
              'script = ExtResource("details")', '',
              '[node name="HallTraceDecals" type="Node2D" parent="."]',
              'script = ExtResource("details")', 'kind = "traces"', '',
              '[node name="ObjectsAndPeople" type="Node2D" parent="."]',
              'y_sort_enabled = true', '']
    for item in data['furniture']:
        x, y = item['position']
        if item['name'] == 'Bed0':
            y -= 40
        # Sprite artwork is bottom-centre anchored, matching the previous builder.
        canvas = sprite_canvases[item['sprite']]
        ox, oy = -(canvas.w // 2), -canvas.h + (40 if item['name'] == 'Bed0' else 0)
        lines += [f'[node name="{item["name"]}" type="Sprite2D" parent="ObjectsAndPeople"]',
                  f'position = {_vec(x, y)}', f'offset = {_vec(ox, oy)}',
                  'centered = false', f'texture = ExtResource("{item["sprite"]}")', '']
    gx, gy = data['gate']['position']
    lines += ['[node name="MechanicalIsolationDoor" type="Node2D" parent="ObjectsAndPeople"]',
              f'position = {_vec(gx, gy)}', '',
              '[node name="DoorFace" type="Sprite2D" parent="ObjectsAndPeople/MechanicalIsolationDoor"]',
              'centered = false', f'offset = {_vec(-sprite_canvases["isolation_gate"].w / 2, 0)}',
              'texture = ExtResource("isolation_gate")', '',
              '[node name="DoorGap" type="Sprite2D" parent="ObjectsAndPeople/MechanicalIsolationDoor"]',
              'visible = false', 'centered = false', f'offset = {_vec(-sprite_canvases["isolation_gate_open"].w / 2, 0)}',
              'texture = ExtResource("isolation_gate_open")', '',
              '[node name="InnerBolt" type="Sprite2D" parent="ObjectsAndPeople/MechanicalIsolationDoor"]',
              f'position = {_vec(data["gate"]["bolt"][0] - gx, data["gate"]["bolt"][1] - gy)}',
              f'offset = {_vec(-sprite_canvases["inner_bolt"].w / 2, -sprite_canvases["inner_bolt"].h / 2)}',
              'centered = false', 'texture = ExtResource("inner_bolt")', '',
              '[node name="StopBase" type="Sprite2D" parent="ObjectsAndPeople"]',
              f'position = {_vec(sx, sy)}',
              f'offset = {_vec(-sprite_canvases["stop_base"].w // 2, -sprite_canvases["stop_base"].h)}',
              'centered = false', 'texture = ExtResource("stop_base")', '']
    for name, x, y in [('侦探', 154, 144), ('工程师', 480, 416), ('厨师', 983, 728), ('音乐家', 706, 944)]:
        sheet = {'侦探': 'detective', '工程师': 'engineer', '厨师': 'chef', '音乐家': 'musician'}[name]
        lines += [f'[node name="{name}" type="CharacterBody2D" parent="ObjectsAndPeople"]',
                  f'position = {_vec(x, y)}', 'script = ExtResource("actor")',
                  f'standard = ExtResource("{sheet}_frames")', '',
                  f'[node name="CollisionShape2D" type="CollisionShape2D" parent="ObjectsAndPeople/{name}"]',
                  'position = Vector2(0, -6)', 'shape = SubResource("actor_foot")', '',
                  f'[node name="ContactShadow" type="Polygon2D" parent="ObjectsAndPeople/{name}"]',
                  'color = Color(0.06, 0.05, 0.04, 0.28)',
                  'polygon = PackedVector2Array(-9, -2, -6, -4, 6, -4, 9, -2, 9, 0, 6, 2, -6, 2, -9, 0)', '',
                  f'[node name="Sprite" type="AnimatedSprite2D" parent="ObjectsAndPeople/{name}"]',
                  'position = Vector2(0, -23)', 'texture_filter = 1',
                  f'sprite_frames = ExtResource("{sheet}_frames")', '']
    lines += ['[node name="BedQuiltForeground" type="Sprite2D" parent="ObjectsAndPeople"]',
              f'position = {_vec(154, 166)}',
              f'offset = {_vec(-(bed_width // 2), -(bed_height - 30))}',
              'centered = false', 'texture = SubResource("bed_quilt")', '']
    for index, at in enumerate(data['dorm_doors']):
        name = f'DormitoryDoor{index + 1}'
        owner = ('detective', 'engineer', 'cook', 'musician')[index]
        lines += [f'[node name="{name}" type="Node2D" parent="ObjectsAndPeople"]',
                  f'position = {_vec(*at)}', 'script = ExtResource("room_door")',
                  f'dorm_owner = "{owner}"', f'interior_bounds = Rect2({layout.DORM_X[index] * layout.CELL}, 32, 256, 256)', '',
                  f'[node name="Body" type="StaticBody2D" parent="ObjectsAndPeople/{name}"]', '',
                  f'[node name="LeafCollision" type="CollisionShape2D" parent="ObjectsAndPeople/{name}/Body"]',
                  'position = Vector2(0, -4)', 'shape = SubResource("dorm_leaf")', '']
    lines += [
        '[node name="InvestigationSites" type="Node2D" parent="."]', '',
    ]
    for site_id, x, y, reach in site_triggers:
        actor = {'engineer': '工程师', 'chef': '厨师', 'musician': '音乐家'}.get(site_id)
        parent = f'ObjectsAndPeople/{actor}' if actor else 'InvestigationSites'
        at = _vec(0, 0) if actor else _vec(x, y)
        node_path = f'{parent}/{site_id}'
        lines += [f'[node name="{site_id}" type="Area2D" parent="{parent}"]',
                  f'position = {at}', 'collision_layer = 0', 'collision_mask = 2',
                  'monitorable = false', 'script = ExtResource("investigation_site")',
                  f'site_id = "{site_id}"', '',
                  f'[node name="CollisionShape2D" type="CollisionShape2D" parent="{node_path}"]',
                  f'shape = SubResource("site_reach_{int(reach)}")', '']
    lines += [
        '[node name="InteractionHints" type="Node2D" parent="."]',
        'z_index = 50', 'script = ExtResource("hints")', '',
        '[node name="Dialogue" parent="." instance=ExtResource("dialogue")]', '',
        '[node name="HallReplayBridge" type="Node" parent="."]',
        'script = ExtResource("bridge")', '',
        '[node name="ReasoningMusic" type="AudioStreamPlayer" parent="."]',
        'stream = ExtResource("reasoning_music")',
        'volume_db = -48.0', '',
        '[node name="Camera2D" type="Camera2D" parent="."]',
        'zoom = Vector2(2, 2)', '',
        '[node name="FieldUI" type="CanvasLayer" parent="."]',
        'layer = 10', '',
        '[node name="Vignette" type="TextureRect" parent="FieldUI"]',
        'offset_right = 1280.0', 'offset_bottom = 992.0',
        'mouse_filter = 2', 'texture = SubResource("hud_vignette")', '',
        '[node name="TopShade" type="TextureRect" parent="FieldUI"]',
        'offset_right = 1280.0', 'offset_bottom = 84.0',
        'mouse_filter = 2', 'texture = SubResource("hud_top")', '',
        '[node name="BottomShade" type="TextureRect" parent="FieldUI"]',
        'offset_top = 916.0', 'offset_right = 1280.0', 'offset_bottom = 992.0',
        'mouse_filter = 2', 'texture = SubResource("hud_bottom")', '',
        '[node name="Title" type="Label" parent="FieldUI"]',
        'offset_left = 24.0', 'offset_top = 18.0',
        'offset_right = 784.0', 'offset_bottom = 52.0',
        'theme_override_colors/font_color = Color(0.909804, 0.839216, 0.654902, 1)',
        'theme_override_font_sizes/font_size = 22', 'text = "FA_UI_050"', '',
        '[node name="Time" type="Label" parent="FieldUI"]',
        'offset_left = 1150.0', 'offset_top = 18.0',
        'offset_right = 1255.0', 'offset_bottom = 52.0',
        'theme_override_colors/font_color = Color(0.909804, 0.839216, 0.654902, 1)',
        'theme_override_font_sizes/font_size = 22', 'text = "07:20"', '',
        '[node name="Language" type="OptionButton" parent="FieldUI"]',
        'offset_left = 946.0', 'offset_top = 15.0',
        'offset_right = 1122.0', 'offset_bottom = 53.0', '',
        '[node name="Hint" type="Label" parent="FieldUI"]',
        'offset_left = 340.0',
        'offset_top = 940.0',
        'offset_right = 940.0',
        'offset_bottom = 970.0',
        'mouse_filter = 2',
        'theme_override_colors/font_color = Color(0.94, 0.91, 0.83, 0.92)',
        'theme_override_colors/font_shadow_color = Color(0.03, 0.02, 0.05, 0.6)',
        'theme_override_constants/shadow_offset_y = 1',
        'theme_override_font_sizes/font_size = 18',
        'horizontal_alignment = 1',
        '',
        '[node name="InvestigationPanel" type="PanelContainer" parent="FieldUI"]',
        'script = ExtResource("field_modal")',
        'regular_bounds = Rect2(300, 112, 680, 768)',
        'detail_bounds = Rect2(270, 96, 740, 800)',
        'answer_bounds = Rect2(260, 96, 760, 800)',
        'flow_bounds = Rect2(0, 0, 1280, 992)',
        'regular_panel_style = SubResource("modal_panel")',
        'flow_panel_style = SubResource("flow_panel")',
        'answer_panel_style = SubResource("answer_panel")',
        'visible = false', 'offset_left = 94.0', 'offset_top = 72.0',
        'offset_right = 1186.0', 'offset_bottom = 922.0',
        'theme_override_styles/panel = SubResource("modal_panel")', '',
        '[node name="ModalScroll" type="ScrollContainer" parent="FieldUI/InvestigationPanel"]',
        'layout_mode = 2', 'horizontal_scroll_mode = 0', '',
        '[node name="ModalBox" type="VBoxContainer" parent="FieldUI/InvestigationPanel/ModalScroll"]',
        'layout_mode = 2', 'size_flags_horizontal = 3',
        'theme_override_constants/separation = 12', '',
    ]
    SCENE.write_text('\n'.join(lines), encoding='utf-8')


def write_layout_resource(data: dict) -> None:
    text = '\n'.join([
        '[gd_resource type="Resource" script_class="FirstAwakeningLayout" format=3]', '',
        '[ext_resource type="Script" path="res://scenes/FirstAwakening/LayoutResource.gd" id="layout"]', '',
        '[resource]', 'script = ExtResource("layout")',
        'data = ' + json.dumps(data, ensure_ascii=False, indent=1), '',
    ])
    (TARGET / 'layout.tres').write_text(text, encoding='utf-8')


def collision_rects() -> list[list[int]]:
    """Merge every non-walkable cell of the map into row runs."""
    rects = []
    for y in range(layout.H):
        x = 0
        while x < layout.W:
            if layout.walkable(x, y):
                x += 1
                continue
            start = x
            while x < layout.W and not layout.walkable(x, y):
                x += 1
            rects.append([start * layout.CELL, y * layout.CELL, (x - start) * layout.CELL, layout.CELL])
    return rects


def render() -> tuple[dict, dict, dict]:
    """Render in memory so CLI and MCP share one recipe and can verify before writing."""
    wing, anchors = architecture.build()
    architecture.bake_floor_work(wing, placement.FURNITURE, placement.RUGS, placement.SHOES)
    sprites = props.build()
    data = {
        "cell": layout.CELL,
        "size": [layout.W, layout.H],
        "rows": layout.ROWS,
        "solids": collision_rects(),
        "furniture": placement.FURNITURE,
        "gate": placement.GATE,
        "stop": placement.STOP,
        "lights": placement.LIGHTS,
        "windows": anchors["windows"],
        "sconces": anchors["sconces"],
        "dorm_doors": [[x + 32, 384] for x in architecture.DORM_DOORS],
    }
    manifest = {
        "process": "native 1x pixel recipes in tools/first_awakening_art; no resampling, no generated source images",
        "entry": "tools/build_first_awakening_interior.py",
        "anchor": "bottom-centre of each sprite is its floor contact point",
        "architecture": {"file": "life_wing.png", "size": [wing.w, wing.h]},
        "sprites": {name: {"size": [c.w, c.h]} for name, c in sprites.items()},
    }
    return {"life_wing": wing, **sprites}, data, manifest


def main() -> None:
    canvases, data, manifest = render()
    TARGET.mkdir(parents=True, exist_ok=True)
    if '--scene-only' not in sys.argv:
        for name, canvas in canvases.items():
            canvas.save(TARGET / f"{name}.png")
    (TARGET / "layout.json").write_text(json.dumps(data, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    (TARGET / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    write_layout_resource(data)
    write_scene(data)
    print(json.dumps({"sprites": len(canvases) - 1, "solids": len(data["solids"]), "furniture": len(placement.FURNITURE)}))


if __name__ == "__main__":
    main()
