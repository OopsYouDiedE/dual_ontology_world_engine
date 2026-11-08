#!/usr/bin/env python3
"""Convert published Godot campaign rooms and candidate manifests into standard

ArchBuilder web editor scenes (YAML and rooms_catalog.json).
Follows lowpoly_chapters_v3 architectural and coordinate contracts:
- Godot coordinates: +X right, +Y up, -Z back/depth (0 at cutaway, -depth at rear wall).
- L-shaped cutaway walls:
    Segment 0 (Left wall):  [-w/2, 0, 0] -> [-w/2, 0, -d], len = d
    Segment 1 (Rear wall):  [-w/2, 0, -d] -> [ w/2, 0, -d], len = w
"""

import csv
import json
import math
import os
import re
from pathlib import Path

BASE_DIR = Path(__file__).resolve().parent.parent.parent
ROOMS_DIR = BASE_DIR / "assets" / "scenes" / "campaign_rooms" / "rooms"
COMPONENTS_DIR = BASE_DIR / "assets" / "scenes" / "campaign_rooms" / "components"
PUBLICATION_FILE = BASE_DIR / "assets" / "scenes" / "campaign_rooms" / "publication.json"
CATALOG_CSV_FILE = Path(__file__).resolve().parent / "scene_catalog.csv"
F01_MANIFEST_FILE = Path(__file__).resolve().parent / "f01_manifest.json"
OUTPUT_DIR = Path(__file__).resolve().parent / "rooms_yaml"
OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

# Load scene_catalog.csv metadata
catalog_meta = {}
if CATALOG_CSV_FILE.exists():
    raw_bytes = CATALOG_CSV_FILE.read_bytes()
    # Handle UTF-16 LE from PowerShell or UTF-8
    if raw_bytes.startswith(b'\xff\xfe') or raw_bytes.startswith(b'\xfe\xff'):
        csv_text = raw_bytes.decode('utf-16')
    else:
        csv_text = raw_bytes.decode('utf-8-sig', errors='replace')
    reader = csv.DictReader(csv_text.splitlines())
    for row in reader:
            sid = row.get("scene_id", "").strip()
            if not sid:
                continue
            dim_str = row.get("尺寸_米_宽x深x高", "").strip()
            w, d, h = 8.0, 7.0, 3.6
            if dim_str:
                parts = dim_str.lower().replace("m", "").split("x")
                if len(parts) == 3:
                    try:
                        w, d, h = float(parts[0]), float(parts[1]), float(parts[2])
                    except ValueError:
                        pass
            catalog_meta[sid] = {
                "name": row.get("中文名称", sid),
                "category": row.get("分组", "室内"),
                "type": row.get("类型", "room"),
                "width": w,
                "depth": d,
                "height": h,
                "chapters": row.get("章节覆盖", ""),
            }

# Load courtyard prototype rooms
COURTYARD_JSON = BASE_DIR / "tools" / "building" / "designs" / "courtyard_prototype_rooms.json"
if COURTYARD_JSON.exists():
    with open(COURTYARD_JSON, "r", encoding="utf-8") as f:
        cdata = json.load(f)
        for cr in cdata.get("rooms", []):
            cid = cr.get("id")
            catalog_meta[cid] = {
                "name": cr.get("label", cid),
                "category": "四院原型",
                "type": "prototype",
                "width": 7.8,
                "depth": 6.7,
                "height": 3.6,
                "floor": cr.get("floor", "1F"),
                "chapters": "2"
            }
component_name_map = {}
if PUBLICATION_FILE.exists():
    with open(PUBLICATION_FILE, "r", encoding="utf-8") as f:
        pub_data = json.load(f)
        for comp_id, comp_info in pub_data.get("components", {}).items():
            sources = comp_info.get("sources", [])
            if sources:
                src_stem = Path(sources[0]).stem
                component_name_map[comp_id] = src_stem


def map_prop_kind(node_name, prefab_name=""):
    """Map a Godot instance name / prefab name to the editor prop kind."""
    combined = f"{node_name}_{prefab_name}".lower()

    if "bed" in combined:
        return "bed"
    if any(k in combined for k in ["wardrobe", "cabinet", "cupboard", "lockers"]):
        return "cabinet"
    if "wash_table" in combined or "sink" in combined or "basin" in combined:
        return "wash_table"
    if any(k in combined for k in ["reception_desk", "control_console", "worktop", "counter"]):
        return "reception_desk"
    if "piano" in combined:
        return "upright_piano"
    if "bookshelf" in combined or "shelf" in combined or "rack" in combined:
        return "bookshelf"
    if "bench" in combined:
        return "bench"
    if "chair" in combined or "stool" in combined:
        return "chair"
    if "table" in combined or "nightstand" in combined or "desk" in combined:
        return "table"
    if "wall_lantern" in combined or "sconce" in combined:
        return "wall_lantern"
    if "pendant_lamp" in combined or "chandelier" in combined or "ceiling_lamp" in combined:
        return "pendant_lamp"
    if "banker_lamp" in combined or "lamp" in combined:
        return "banker_lamp"
    if "screen" in combined or "partition" in combined:
        return "screen"
    if "painting" in combined or "portrait" in combined or "schematic" in combined or "board" in combined:
        return "painting"
    if "rug" in combined or "carpet" in combined:
        return "rug"
    if "pedestal" in combined or "pedestal" in prefab_name.lower():
        return "pedestal"
    if "chest" in combined or "trunk" in combined:
        return "chest"
    if "plant" in combined or "planter" in combined or "flower" in combined:
        return "planter"
    if "lectern" in combined or "podium" in combined:
        return "lectern"
    if "chalkboard" in combined or "blackboard" in combined:
        return "chalkboard"
    if any(k in combined for k in ["crate", "barrel", "sacks", "box", "pallet", "chest", "bottle", "book", "accessory"]):
        return "crates"
    return "crates"


def determine_floor_style(room_id, category, r_type):
    """Determine architectural floor texture style."""
    rid = room_id.upper()
    if any(k in rid for k in ["R01", "R02", "R03", "R04", "R05", "R06", "R07", "R08", "R09", "R10", "R11", "R12", "R13", "R14", "R15", "R16"]):
        return "parquet_wood"
    if "DINING" in rid or "LIVING" in rid or "LECTURE" in rid:
        return "parquet_wood"
    if "CLINIC" in rid or "B-102" in rid or "BATH" in rid or "-W" in rid or "WASH" in rid:
        return "octagonal_tile"
    if "B1" in rid or "B2" in rid or "SERVICE" in rid or "STAIR" in rid:
        return "stone_slab"
    if "F01" in rid or "HALL" in rid:
        return "octagonal_tile"
    return "terrazzo_tile"


def determine_wall_style(room_id, category):
    rid = room_id.upper()
    if "B1" in rid or "B2" in rid or "SERVICE" in rid:
        return "brick_dado"
    if "F01" in rid:
        return "stone"
    return "classical"


def parse_tscn_instances(tscn_path):
    """Extract instances from a Godot .tscn file."""
    text = tscn_path.read_text(encoding="utf-8")

    # Extract ext_resources
    # [ext_resource type="PackedScene" path="res://assets/scenes/campaign_rooms/components/{hash}.tscn" id="6_prefab"]
    ext_resources = {}
    for m in re.finditer(r'\[ext_resource[^\]]+path="res://assets/scenes/campaign_rooms/components/([a-f0-9]+)\.tscn"[^\]]+id="([^"]+)"', text):
        comp_hash = m.group(1)
        res_id = m.group(2)
        prefab_name = component_name_map.get(comp_hash, comp_hash)
        ext_resources[res_id] = prefab_name

    # Extract nodes
    instances = []
    # Node pattern: [node name="..." parent="Instances" instance=ExtResource("...")]
    node_pattern = re.compile(
        r'\[node name="(?P<name>[^"]+)" parent="Instances" instance=ExtResource\("(?P<res>[^"]+)"\)\s*\](?P<body>.*?)(?=\n\[node |\Z)',
        re.DOTALL
    )

    for m in node_pattern.finditer(text):
        node_name = m.group("name")
        res_id = m.group("res")
        body = m.group("body")
        prefab_name = ext_resources.get(res_id, "")

        # Skip architecture / shells
        if "architecture" in node_name.lower() or "shell" in node_name.lower():
            continue

        # Extract transform
        x, y, z = 0.0, 0.0, 0.0
        yaw = 0.0

        # Case A: transform = Transform3D(b0, b1, b2, b3, b4, b5, b6, b7, b8, ox, oy, oz)
        t_match = re.search(r'transform\s*=\s*Transform3D\(\s*([^,\)]+),\s*([^,\)]+),\s*([^,\)]+),\s*([^,\)]+),\s*([^,\)]+),\s*([^,\)]+),\s*([^,\)]+),\s*([^,\)]+),\s*([^,\)]+),\s*([^,\)]+),\s*([^,\)]+),\s*([^,\)]+)\)', body)
        if t_match:
            vals = [float(v.strip()) for v in t_match.groups()]
            b0, b1, b2, b3, b4, b5, b6, b7, b8, ox, oy, oz = vals
            x, y, z = round(ox, 2), round(oy, 2), round(oz, 2)
            # Yaw from Godot basis:
            # Godot rotation around Y by angle theta:
            # [cos -sin]
            # [sin  cos] in X/Z
            yaw = round(math.degrees(math.atan2(b2, b8)))
        else:
            # Case B: position = Vector3(x, y, z) and optional rotation = Vector3(rx, ry, rz)
            p_match = re.search(r'position\s*=\s*Vector3\(\s*([^,\)]+),\s*([^,\)]+),\s*([^,\)]+)\)', body)
            if p_match:
                x = round(float(p_match.group(1).strip()), 2)
                y = round(float(p_match.group(2).strip()), 2)
                z = round(float(p_match.group(3).strip()), 2)
            r_match = re.search(r'rotation\s*=\s*Vector3\(\s*([^,\)]+),\s*([^,\)]+),\s*([^,\)]+)\)', body)
            if r_match:
                ry = float(r_match.group(2).strip())
                yaw = round(math.degrees(ry))

        # Check visibility
        v_match = re.search(r'visible\s*=\s*(false|true)', body)
        visible = True
        if v_match and v_match.group(1) == "false":
            visible = False

        instances.append({
            "name": node_name,
            "prefab": prefab_name,
            "x": x,
            "y": y,
            "z": z,
            "yaw": yaw,
            "visible": visible,
            "body": body
        })

    return instances


def convert_room(room_id, tscn_path):
    """Convert a published room .tscn into standard ArchBuilder YAML/JSON dictionary."""
    meta = catalog_meta.get(room_id, {})
    name = meta.get("name", room_id)
    w = meta.get("width", 7.8)
    d = meta.get("depth", 6.7)
    h = meta.get("height", 3.6)
    category = meta.get("category", "住宅与生活配套")
    floor_str = meta.get("floor") or "1F"
    if not meta.get("floor"):
        if "2f" in name.lower() or room_id.startswith("R") or "main_2f" in name:
            floor_str = "2F"
        elif "b1" in room_id.lower() or "b1" in name.lower():
            floor_str = "B1"
        elif "b2" in room_id.lower() or "b2" in name.lower():
            floor_str = "B2"

    raw_instances = parse_tscn_instances(tscn_path)

    # Separate openings (doors and windows) from other props
    openings = []
    props = []
    stairs = []

    half_w = round(w / 2.0, 2)
    depth_val = round(d, 2)

    for inst in raw_instances:
        if not inst["visible"]:
            continue
        nm = inst["name"].lower()
        pf = inst["prefab"].lower()
        x, y, z, yaw = inst["x"], inst["y"], inst["z"], inst["yaw"]

        # Check if this instance is a door or window
        is_door = "door" in nm or "door" in pf
        is_window = "window" in nm or "window" in pf

        if is_door:
            # Determine which wall segment it belongs to:
            # Left wall (Segment 0): x is approximately -half_w (within 0.3m)
            # Rear wall (Segment 1): z is approximately -depth_val (within 0.3m)
            # Front wall: z is near 0
            if abs(x - (-half_w)) <= 0.4:
                # Left wall
                seg = 0
                center = round(-z, 2)
            elif abs(z - (-depth_val)) <= 0.4:
                # Rear wall
                seg = 1
                center = round(x + half_w, 2)
            else:
                # Fallback to rear wall projection
                seg = 1
                center = round(x + half_w, 2)

            is_double = "double" in nm or "double" in pf or ("width" in inst["body"] and float(re.search(r'width\s*=\s*([0-9.]+)', inst["body"]).group(1)) > 1.6)
            is_arch = "arch" in nm or "arch" in pf

            op_type = "double_door" if is_double else ("arched_closed_door" if is_arch else "closed_door")
            op_w = 2.4 if is_double else 1.2
            op_h = 2.65 if is_double else 2.4

            openings.append({
                "id": f"op_{inst['name']}",
                "segment": seg,
                "center": center,
                "width": op_w,
                "height": op_h,
                "bottom": 0.0,
                "type": op_type
            })
            continue

        if is_window:
            if abs(x - (-half_w)) <= 0.4:
                seg = 0
                center = round(-z, 2)
            else:
                seg = 1
                center = round(x + half_w, 2)

            is_rect = "rect" in nm or "rect" in pf
            op_type = "rectangular_window" if is_rect else "arched_window"
            openings.append({
                "id": f"op_{inst['name']}",
                "segment": seg,
                "center": center,
                "width": 1.4,
                "height": 2.2,
                "bottom": round(y, 2) if y > 0.1 else 0.95,
                "type": op_type,
                "rise": 0.4
            })
            continue

        # Non-opening prop
        kind = map_prop_kind(inst["name"], inst["prefab"])
        params = {}
        if kind == "wall_lantern":
            params["lamp_energy"] = 15
            params["lamp_height"] = round(y, 2)
        elif kind == "banker_lamp":
            params["lamp_energy"] = 8
            params["mount_y"] = round(y, 2)
        elif kind == "painting":
            params["mount_y"] = round(y, 2)
        elif kind == "planter":
            params["plant_w"] = 0.65
            params["plant_h"] = 0.85
            params["flower_count"] = 7
        elif kind == "pedestal":
            params["width"] = 0.52
            params["height"] = 0.75
        elif kind == "chest":
            params["chest_w"] = 0.95
            params["chest_d"] = 0.55
            params["chest_h"] = 0.50

        props.append({
            "id": f"prop_{inst['name']}",
            "name": inst["name"],
            "kind": kind,
            "x": x,
            "y": y,
            "z": z,
            "yaw": yaw,
            "params": params
        })

    floor_style = determine_floor_style(room_id, category, meta.get("type", ""))
    wall_style = determine_wall_style(room_id, category)

    floor_poly = [
        [-half_w, 0.0],
        [-half_w, -depth_val],
        [half_w, -depth_val],
        [half_w, 0.0]
    ]

    wall_points = [
        [-half_w, 0.0, 0.0],
        [-half_w, 0.0, -depth_val],
        [half_w, 0.0, -depth_val]
    ]

    doc = {
        "id": room_id,
        "name": name,
        "floor": floor_str,
        "activeLayerId": f"layer_{room_id}",
        "layers": [
            {
                "id": f"layer_{room_id}",
                "name": f"{floor_str} {name} 地面",
                "y": 0.0,
                "style": floor_style,
                "polygons": [floor_poly]
            }
        ],
        "walls": [
            {
                "id": f"wall_{room_id}",
                "name": "主外墙柱 (L型剖面外墙)",
                "closed": False,
                "height": round(h, 2),
                "thickness": 0.28,
                "baseY": 0.0,
                "style": wall_style,
                "points": wall_points,
                "openings": openings
            }
        ],
        "stairs": stairs,
        "railings": [],
        "props": props
    }

    return doc


def build_f01_scene():
    """Build the Grand Main Hall F01 scene from manifest specification."""
    w, d, h = 18.0, 17.0, 8.8
    half_w = 9.0
    depth_val = 17.0

    layers = [
        {
            "id": "layer_F01_ground",
            "name": "1F 主大厅 地面",
            "y": 0.0,
            "style": "octagonal_tile",
            "polygons": [
                [[-9.0, 0.0], [-9.0, -17.0], [9.0, -17.0], [9.0, 0.0]]
            ]
        },
        {
            "id": "layer_F01_middle_landing",
            "name": "1.5F 梯间主平台",
            "y": 2.0,
            "style": "stone_slab",
            "polygons": [
                [[-2.0, -11.3], [-2.0, -13.6], [2.0, -13.6], [2.0, -11.3]]
            ]
        },
        {
            "id": "layer_F01_gallery",
            "name": "2F 连续U型回廊",
            "y": 4.0,
            "style": "parquet_wood",
            "polygons": [
                # West Gallery
                [[-9.0, -3.0], [-9.0, -14.2], [-6.5, -14.2], [-6.5, -3.0]],
                # East Gallery
                [[6.5, -3.0], [6.5, -14.2], [9.0, -14.2], [9.0, -3.0]],
                # Rear Gallery
                [[-9.0, -14.2], [-9.0, -17.0], [9.0, -17.0], [9.0, -14.2]]
            ]
        }
    ]

    stairs = [
        {
            "id": "stair_F01_central_flight",
            "name": "主居中双宽大理石直跑梯",
            "x": 0.0,
            "y": 0.0,
            "z": -7.1,
            "rise": 2.0,
            "run": 4.2,
            "risers": 12,
            "width": 3.4,
            "nosing": 0.03,
            "railingPos": "both",
            "yaw": 90
        },
        {
            "id": "stair_F01_west_branch",
            "name": "西侧二层回廊衔接分叉梯",
            "x": -2.0,
            "y": 2.0,
            "z": -12.4,
            "rise": 2.0,
            "run": 4.5,
            "risers": 12,
            "width": 2.0,
            "nosing": 0.03,
            "railingPos": "both",
            "yaw": 180
        },
        {
            "id": "stair_F01_east_branch",
            "name": "东侧二层回廊衔接分叉梯",
            "x": 2.0,
            "y": 2.0,
            "z": -12.4,
            "rise": 2.0,
            "run": 4.5,
            "risers": 12,
            "width": 2.0,
            "nosing": 0.03,
            "railingPos": "both",
            "yaw": 0
        }
    ]

    railings = [
        {
            "id": "rail_F01_west_gallery",
            "style": "turned_wood",
            "height": 1.0,
            "baseY": 4.0,
            "points": [
                [-6.5, 4.0, -3.0],
                [-6.5, 4.0, -11.4]
            ]
        },
        {
            "id": "rail_F01_east_gallery",
            "style": "turned_wood",
            "height": 1.0,
            "baseY": 4.0,
            "points": [
                [6.5, 4.0, -3.0],
                [6.5, 4.0, -11.4]
            ]
        },
        {
            "id": "rail_F01_rear_gallery",
            "style": "turned_wood",
            "height": 1.0,
            "baseY": 4.0,
            "points": [
                [-6.5, 4.0, -13.4],
                [-6.5, 4.0, -14.2],
                [6.5, 4.0, -14.2],
                [6.5, 4.0, -13.4]
            ]
        }
    ]

    openings = [
        # Rear wall (Segment 1): starts at -9.0, ends at +9.0, len = 18.0.
        # Door centers in Blender: sign * (w/2 - 2.65) = sign * 6.35. Godot X = +/-6.35.
        # Center on segment = X + 9.0 = 9.0 +/- 6.35 = 2.65 and 15.35.
        {"id": "op_F01_Ground_Rear_West", "segment": 1, "center": 2.65, "width": 1.5, "height": 2.8, "bottom": 0.0, "type": "closed_door"},
        {"id": "op_F01_Ground_Rear_East", "segment": 1, "center": 15.35, "width": 1.5, "height": 2.8, "bottom": 0.0, "type": "closed_door"},
        {"id": "op_F01_Upper_Rear_West", "segment": 1, "center": 2.65, "width": 1.5, "height": 2.8, "bottom": 4.0, "type": "closed_door"},
        {"id": "op_F01_Upper_Rear_East", "segment": 1, "center": 15.35, "width": 1.5, "height": 2.8, "bottom": 4.0, "type": "closed_door"},
        # Rear wall arched windows at x = -3.35, 0, +3.35 -> center = 5.65, 9.0, 12.35
        {"id": "op_F01_Rear_Window_W", "segment": 1, "center": 5.65, "width": 1.7, "height": 3.55, "bottom": 4.42, "type": "arched_window", "rise": 0.85},
        {"id": "op_F01_Rear_Window_C", "segment": 1, "center": 9.0, "width": 2.05, "height": 3.55, "bottom": 4.42, "type": "arched_window", "rise": 1.02},
        {"id": "op_F01_Rear_Window_E", "segment": 1, "center": 12.35, "width": 1.7, "height": 3.55, "bottom": 4.42, "type": "arched_window", "rise": 0.85},
        # West side wall (Segment 0): len = 17.0. Door at y = 7.9 -> center = 7.9
        {"id": "op_F01_Ground_Side_West", "segment": 0, "center": 7.9, "width": 1.45, "height": 2.75, "bottom": 0.0, "type": "closed_door"},
        {"id": "op_F01_Upper_Side_West", "segment": 0, "center": 7.9, "width": 1.45, "height": 2.75, "bottom": 4.0, "type": "closed_door"},
        {"id": "op_F01_West_Window_Lower", "segment": 0, "center": 12.05, "width": 2.0, "height": 2.65, "bottom": 0.65, "type": "arched_window", "rise": 0.90},
        {"id": "op_F01_West_Window_Upper", "segment": 0, "center": 12.05, "width": 2.35, "height": 3.45, "bottom": 4.45, "type": "arched_window", "rise": 1.04}
    ]

    wall_points = [
        [-9.0, 0.0, 0.0],
        [-9.0, 0.0, -17.0],
        [9.0, 0.0, -17.0]
    ]

    # Props from f01_manifest.json
    props = []
    if F01_MANIFEST_FILE.exists():
        raw_b = F01_MANIFEST_FILE.read_bytes()
        if raw_b.startswith(b'\xff\xfe') or raw_b.startswith(b'\xfe\xff'):
            m_text = raw_b.decode('utf-16')
        else:
            m_text = raw_b.decode('utf-8-sig', errors='replace')
        f01_data = json.loads(m_text)
        for inst in f01_data.get("instances", []):
                # Skip doors or windows which are already in openings
                kind_str = inst.get("kind", "")
                iid = inst.get("instance_id", "")
                if "door" in kind_str or "door" in iid.lower():
                    continue
                if "window" in kind_str or "window" in iid.lower():
                    continue
                if any(k in iid.lower() or k in kind_str.lower() for k in ["architecture", "enclosure", "ground", "gallery", "stair"]):
                    continue

                mat = inst.get("matrix_godot", [])
                if len(mat) == 4:
                    x = round(mat[0][3], 2)
                    y = round(mat[1][3], 2)
                    z = round(mat[2][3], 2)
                    b00 = mat[0][0]
                    b02 = mat[0][2]
                    b22 = mat[2][2]
                    yaw = round(math.degrees(math.atan2(b02, b22)))
                else:
                    x, y, z, yaw = 0.0, 0.0, 0.0, 0

                kind = map_prop_kind(iid, kind_str)
                params = {}
                if kind == "wall_lantern":
                    params["lamp_energy"] = 20
                    params["lamp_height"] = y
                elif kind == "pendant_lamp" or kind_str == "chandelier":
                    params["lamp_energy"] = 100
                    params["drop_length"] = 1.85
                    kind = "pendant_lamp"
                elif kind == "painting":
                    params["frame_w"] = 2.8
                    params["frame_h"] = 1.65
                    params["mount_y"] = y
                elif kind == "reception_desk":
                    params["desk_w"] = 2.5
                    params["desk_h"] = 1.08
                elif kind == "planter":
                    params["plant_w"] = 0.75
                    params["plant_h"] = 0.90
                    params["flower_count"] = 7
                elif kind == "pedestal":
                    params["width"] = 0.55
                    params["height"] = 0.70

                props.append({
                    "id": f"prop_{iid}",
                    "name": iid,
                    "kind": kind,
                    "x": x,
                    "y": y,
                    "z": z,
                    "yaw": yaw,
                    "params": params
                })

    doc = {
        "id": "F01",
        "name": "入口双分叉梯大厅与连续二层回廊",
        "floor": "1F",
        "activeLayerId": "layer_F01_ground",
        "layers": layers,
        "walls": [
            {
                "id": "wall_F01",
                "name": "大厅古典石砌外墙",
                "closed": False,
                "height": 8.8,
                "thickness": 0.38,
                "baseY": 0.0,
                "style": "stone",
                "points": wall_points,
                "openings": openings
            }
        ],
        "stairs": stairs,
        "railings": railings,
        "props": props
    }

    return doc


def main():
    print("Starting conversion to ArchBuilder catalog...")
    catalog = []

    # 1. Add F01
    f01_doc = build_f01_scene()
    catalog.append(f01_doc)
    print(f"Generated F01 scene with {len(f01_doc['props'])} props, {len(f01_doc['stairs'])} stairs, {len(f01_doc['railings'])} railings.")

    # 2. Add published campaign rooms
    room_dirs = sorted([d for d in ROOMS_DIR.iterdir() if d.is_dir()])
    print(f"Found {len(room_dirs)} campaign room directories.")

    for rdir in room_dirs:
        rid = rdir.name
        tscn_file = rdir / f"{rid}.tscn"
        if not tscn_file.exists():
            print(f"Warning: {tscn_file} not found, skipping.")
            continue

        doc = convert_room(rid, tscn_file)
        catalog.append(doc)
        print(f"Generated {rid}: {doc['name']} - props: {len(doc['props'])}, openings: {len(doc['walls'][0]['openings'])}")

    # Sort catalog: F01 first, then R01-R16, then courtyard prototypes, then public/industrial
    def sort_key(item):
        iid = item["id"]
        if iid == "F01":
            return (0, 0, iid)
        if re.match(r"^R\d+", iid):
            return (1, int(re.search(r"\d+", iid).group(0)), iid)
        if any(iid.startswith(k) for k in ["B-102", "F-104", "S-102", "W-102"]):
            return (2, 0, iid)
        return (3, 0, iid)

    catalog.sort(key=sort_key)

    # Write individual YAML files
    import yaml
    for item in catalog:
        out_yaml = OUTPUT_DIR / f"{item['id']}.yaml"
        with open(out_yaml, "w", encoding="utf-8") as f:
            yaml.dump(item, f, allow_unicode=True, sort_keys=False)

    # Write combined YAML
    out_all_yaml = OUTPUT_DIR / "all_rooms.yaml"
    with open(out_all_yaml, "w", encoding="utf-8") as f:
        yaml.dump(catalog, f, allow_unicode=True, sort_keys=False)

    # Write combined rooms_catalog.json
    out_json = OUTPUT_DIR / "rooms_catalog.json"
    with open(out_json, "w", encoding="utf-8") as f:
        json.dump(catalog, f, ensure_ascii=False, indent=2)

    print(f"\nAll done! Successfully wrote {len(catalog)} rooms to {OUTPUT_DIR}/rooms_catalog.json and individual YAML files.")


if __name__ == "__main__":
    main()
