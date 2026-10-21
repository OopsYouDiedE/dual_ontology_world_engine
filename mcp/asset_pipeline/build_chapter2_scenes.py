"""Build every chapter-2 location: painted maps, sprites and editable Godot scenes.

Run from the repository root:  python mcp/asset_pipeline/build_chapter2_scenes.py
Then import in Godot:          godot --headless --editor --path godot_csharp --import

Writes
  godot_csharp/ShareAsset/Internal/Props/Chapter2/rooms/<key>.png      painted architecture / outdoor ground (1x)
  godot_csharp/ShareAsset/Internal/Props/Chapter2/props/<name>.png     chapter-2 sprites (1x)
  godot_csharp/scenes/Chapter2/Maps/<key>.tscn                         one scene per location
  godot_csharp/ShareAsset/Internal/Props/Chapter2/manifest.json        sizes, anchors, exits
Chapter-1 furniture is referenced from FirstAwakeningInterior, not duplicated.
Interior exits name the outdoors as "campus"; they are pointed at the outdoor region that holds
their spawn point (outdoor.py cuts the outdoors into six regions).
Shared drawing recipes are in tools/first_awakening_art and tools/chapter2_art.
Default labels come from the published manifest. Full outdoor reconstruction additionally
requires --site-source; original artwork and terrain inputs are never synthesized as fallback.
"""
from __future__ import annotations

import hashlib
import json
import re
import struct
import sys
from pathlib import Path

GAME = (Path(__file__).resolve().parents[2] / "godot_csharp")
sys.path.insert(0, str(Path(__file__).resolve().parent))

from chapter2_art import rooms  # noqa: E402
from map_text import parse_json, publish, read_json, replace_json_strings, replace_spans, require_strings  # noqa: E402

OUT = GAME / "ShareAsset/Internal/Props/Chapter2"
CH1 = "res://ShareAsset/Internal/Props/FirstAwakeningInterior"
SCENES = GAME / "scenes/Chapter2/Maps"
CELL = 32
TEXT_SOURCE = OUT / "manifest.json"


def title_key(key):
    return f"资源.地图.{key}.标题"


def label_key(key, site):
    return f"资源.场点.{key}.{site}.标签"


def load_room_text(path=TEXT_SOURCE):
    """Read existing published map labels or an explicit flat text-binding input.

    The default manifest preserves the current asset wording. It is not a DV
    gameplay authority and does not recreate the retired story compiler.
    """
    path = Path(path)
    if not path.is_file():
        raise FileNotFoundError(f"Map text input is missing: {path}. Supply --text-source with the published manifest or an explicit text-binding table.")
    source = read_json(path)
    if not isinstance(source, dict):
        raise ValueError("Map text input must be a manifest or a text-binding object")
    if isinstance(source.get("rooms"), dict):
        text = {}
        for key in rooms.TEXT_SITES:
            entry = source["rooms"].get(key)
            if not isinstance(entry, dict):
                raise ValueError(f"Missing published map label source: {key}")
            indices = site_indices(entry.get("sites"), key)
            text[title_key(key)] = entry.get("title")
            for site, index in indices.items():
                text[label_key(key, site)] = entry["sites"][index].get("label")
    else:
        text = source.get("text", source)
    keys = [k for key, sites in rooms.TEXT_SITES.items()
            for k in (title_key(key), *(label_key(key, site) for site in sites))]
    return require_strings(text, keys, ("资源.地图.", "资源.场点."))


def validate_maps(keys):
    if not keys or len(set(keys)) != len(keys) or any(key not in rooms.TEXT_SITES for key in keys):
        raise ValueError("Map batch must contain distinct known interior keys")


def site_indices(sites, key):
    if not isinstance(sites, list) or any(not isinstance(s, dict) or not isinstance(s.get("id"), str) for s in sites):
        raise ValueError(f"Invalid sites: {key}")
    ids = [s["id"] for s in sites if not s.get("geometry_only", False)]
    if len(set(ids)) != len(ids) or set(ids) != set(rooms.TEXT_SITES[key]):
        raise ValueError(f"Duplicate, missing or unknown site: {key}")
    return {site["id"]: i for i, site in enumerate(sites) if not site.get("geometry_only", False)}


def scene_sites(room):
    """Text-bound sites and label-free geometry anchors share the authored scene."""
    return [*room.get("sites", []), *({**site, "label": "", "geometry_only": True}
                                    for site in room.get("geometry_sites", []))]


def geometry_site_ids(key):
    return {site["id"] for room in rooms.all_rooms() if room["key"] == key
            for site in room.get("geometry_sites", [])}


def resolve_room_text(room, text):
    key = room["key"]
    validate_maps([key])
    site_indices(room.get("sites"), key)
    if room.get("title_key") != title_key(key):
        raise ValueError(f"Invalid title binding: {key}")
    result = {k: v for k, v in room.items() if k != "title_key"}
    result["title"] = text[room["title_key"]]
    result["sites"] = []
    for site in room["sites"]:
        if site.get("label_key") != label_key(key, site["id"]):
            raise ValueError(f"Invalid label binding: {key}/{site['id']}")
        result["sites"].append({**{k: v for k, v in site.items() if k != "label_key"}, "label": text[site["label_key"]]})
    return result


def scene_text(source, key, text):
    """Edit only the root title and explicit Sites children, retaining node order."""
    headers = list(re.finditer(r"(?m)^\[[^\r\n]*\][ \t]*\r?$", source))
    nodes = {}
    for i, header in enumerate(headers):
        if not header.group().startswith("[node "):
            continue
        attributes = dict(re.findall(r'(\w+)="([^"]*)"', header.group()))
        name, parent = attributes.get("name"), attributes.get("parent")
        if name is None or (parent is None and name != key):
            raise ValueError(f"Unexpected root node: {key}")
        path = name if parent in (None, ".") else f"{parent}/{name}"
        if path in nodes:
            raise ValueError(f"Duplicate scene node: {path}")
        end = headers[i + 1].start() if i + 1 < len(headers) else len(source)
        nodes[path] = (attributes, header.end(), end)
    roots = [path for path, (attrs, _, _) in nodes.items() if "parent" not in attrs]
    sites = {path.removeprefix("Sites/") for path in nodes if path.startswith("Sites/")}
    if roots != [key] or "Sites" not in nodes or sites != set(rooms.TEXT_SITES[key]) | geometry_site_ids(key):
        raise ValueError(f"Missing or unknown scene root/site: {key}")
    if nodes["Sites"][0].get("parent") != "." or nodes["Sites"][0].get("type") != "Node2D":
        raise ValueError(f"Invalid Sites container: {key}")
    edits = []
    bindings = [(key, "title", title_key(key))] + [(f"Sites/{site}", "label", label_key(key, site)) for site in rooms.TEXT_SITES[key]]
    for node, field, binding in bindings:
        attrs, start, end = nodes[node]
        if field == "label" and attrs.get("type") != "Marker2D":
            raise ValueError(f"Invalid site node type: {node}")
        block = source[start:end]
        properties = list(re.finditer(rf"(?m)^metadata/{field} = ([^\r\n]*)", block))
        if len(properties) != 1:
            raise ValueError(f"Missing or duplicate metadata/{field}: {node}")
        prop = properties[0]
        value_source = prop.group(1).rstrip(" \t")
        old = parse_json(value_source)
        if not isinstance(old, str):
            raise ValueError(f"Non-string metadata/{field}: {node}")
        if old != text[binding]:
            offset = start + prop.start(1)
            edits.append((offset, offset + len(value_source), json.dumps(text[binding], ensure_ascii=False)))
    return replace_spans(source, edits)


def update_text_only(keys, text_source=TEXT_SOURCE, scenes=SCENES, manifest_path=OUT / "manifest.json"):
    validate_maps(keys)
    text = load_room_text(text_source)
    manifest_source = manifest_path.read_bytes().decode("utf-8")
    manifest = parse_json(manifest_source)
    entries = manifest.get("rooms") if isinstance(manifest, dict) else None
    if not isinstance(entries, dict):
        raise ValueError("Manifest rooms must be an object")
    replacements, candidates = {}, {}
    for key in keys:
        entry = entries.get(key)
        if not isinstance(entry, dict):
            raise ValueError(f"Missing manifest room: {key}")
        indices = site_indices(entry.get("sites"), key)
        replacements[("rooms", key, "title")] = text[title_key(key)]
        for site, i in indices.items():
            replacements[("rooms", key, "sites", i, "label")] = text[label_key(key, site)]
        path = scenes / f"{key}.tscn"
        candidates[path] = scene_text(path.read_bytes().decode("utf-8"), key, text).encode("utf-8")
    candidates[manifest_path] = replace_json_strings(manifest_source, replacements).encode("utf-8")
    return publish(candidates)


def update_sites_only(keys, text_source=TEXT_SOURCE, scenes=SCENES, manifest_path=OUT / "manifest.json"):
    validate_maps(keys)
    text_table = load_room_text(text_source)
    manifest = read_json(manifest_path)
    authored = {room["key"]: resolve_room_text(room, text_table) for room in rooms.all_rooms()}
    candidates = {}
    for key in keys:
        room = authored[key]
        if not isinstance(manifest.get("rooms", {}).get(key), dict):
            raise ValueError(f"Missing manifest room: {key}")
        path = scenes / f"{key}.tscn"
        source = path.read_bytes().decode("utf-8")
        if source.count('[node name="Sites"') != 1:
            raise ValueError(f"Missing or duplicate Sites container: {key}")
        prefix = source.split('[node name="Sites"', 1)[0]
        lines = ['[node name="Sites" type="Node2D" parent="."]', '']
        for site in scene_sites(room):
            lines += [f'[node name="{site["id"]}" type="Marker2D" parent="Sites"]',
                      f'position = Vector2({site["point"][0]}, {site["point"][1]})',
                      f'metadata/label = {json.dumps(site["label"], ensure_ascii=False)}', '']
            if site.get("geometry_only", False): lines.insert(len(lines) - 1, 'metadata/geometry_only = true')
        candidates[path] = (prefix + "\n".join(lines)).encode("utf-8")
        manifest["rooms"][key]["sites"] = scene_sites(room)
    candidates[manifest_path] = json.dumps(manifest, ensure_ascii=False, indent=1).encode("utf-8")
    return publish(candidates)


def register_external_sprites(room, sprite_paths, sizes):
    """Read explicitly authored existing PNGs without painting or copying assets."""
    for item in room["furniture"]:
        if "texture" not in item:
            continue
        texture = item["texture"]
        if not isinstance(texture, str) or not texture.startswith("res://"):
            raise ValueError(f"Expected a project texture path: {item['name']}")
        data = (GAME / texture.removeprefix("res://")).read_bytes()
        if data[:8] != b"\x89PNG\r\n\x1a\n":
            raise ValueError(f"Expected an existing PNG: {texture}")
        width, height = struct.unpack(">II", data[16:24])
        region = item.get("texture_region")
        if region is not None:
            x, y, rw, rh = region
            if min(x, y) < 0 or min(rw, rh) <= 0 or x + rw > width or y + rh > height:
                raise ValueError(f"Texture region outside source: {item['name']}")
            width, height = rw, rh
        sprite_paths[item["sprite"]] = texture
        sizes[item["sprite"]] = (width, height)


def existing_outdoor_spawns(manifest, scenes):
    """Resolve overlapping outdoor tiles by their actual, non-overlapping core bounds."""
    result = {}
    for key, room in manifest["rooms"].items():
        source = (scenes / f"{key}.tscn").read_text(encoding="utf-8")
        core = re.search(r"^metadata/outdoor_core = Rect2\(([^)]+)\)$", source, re.M)
        origin = re.search(r"^metadata/outdoor_origin = Vector2\(([^)]+)\)$", source, re.M)
        if core is None or origin is None:
            continue
        left, top, width, height = map(float, core[1].split(","))
        ox, oy = map(float, origin[1].split(","))
        for spawn, (x, y) in room["spawns"].items():
            if left <= x + ox < left + width and top <= y + oy < top + height:
                if spawn in result:
                    raise ValueError(f"Ambiguous outdoor spawn: {spawn}")
                if f'[node name="{spawn}" type="Marker2D" parent="Spawns"]' not in source:
                    raise ValueError(f"Missing outdoor spawn marker: {key}/{spawn}")
                result[spawn] = key
    return result


def resolve_outdoor_exits(room, spawn_region):
    # Copy recipe dictionaries: regenerating one room must not mutate other recipes.
    room["exits"] = [{**exit, "target": spawn_region[exit["spawn"]] if exit["target"] == "campus" else exit["target"]}
                     for exit in room["exits"]]


def validate_links(manifest):
    spawns = {key: set(room["spawns"]) for key, room in manifest["rooms"].items()}
    bad = [(key, e["name"], e["target"], e["spawn"]) for key, room in manifest["rooms"].items() for e in room["exits"]
           if e["target"] not in spawns or e["spawn"] not in spawns[e["target"]]]
    if bad:
        raise ValueError(f"Invalid map links: {bad}")


def update_geometry_only(keys, text_source=TEXT_SOURCE, scenes=SCENES, manifest_path=OUT / "manifest.json", repaint=False):
    """Regenerate selected interiors; optionally repaint their baked floor shadows."""
    validate_maps(keys)
    text_table = load_room_text(text_source)
    manifest = read_json(manifest_path)
    spawn_region = existing_outdoor_spawns(manifest, scenes)
    authored = {room["key"]: resolve_room_text(room, text_table) for room in rooms.all_rooms()}
    fps = {**CH1_FP, **{name: entry.get("footprint") for name, entry in manifest.get("props", {}).items()}}
    candidates = {}
    for key in keys:
        room = authored[key]
        resolve_outdoor_exits(room, spawn_region)
        room["furniture"] = resolve(room["furniture"], fps)
        paths, sizes = {}, {}
        for item in room["furniture"]:
            if "texture" in item:
                continue
            name = item["sprite"]
            texture = f"res://ShareAsset/Internal/Props/Chapter2/props/{name}.png" if name in manifest.get("props", {}) else f"{CH1}/{name}.png"
            data = (GAME / texture.removeprefix("res://")).read_bytes()
            if data[:8] != b"\x89PNG\r\n\x1a\n":
                raise ValueError(f"Expected an existing PNG: {texture}")
            paths[name] = texture
            sizes[name] = struct.unpack(">II", data[16:24])
        register_external_sprites(room, paths, sizes)
        candidates[scenes / f"{key}.tscn"] = tscn(room, f"res://ShareAsset/Internal/Props/Chapter2/rooms/{key}.png", paths, sizes).encode("utf-8")
        if repaint:
            import io
            from chapter2_art import painter
            canvas, _ = painter.paint(room)
            buffer = io.BytesIO()
            canvas.image().save(buffer, format="PNG")
            candidates[OUT / "rooms" / f"{key}.png"] = buffer.getvalue()
        manifest["rooms"][key]["sites"] = scene_sites(room)
        manifest["rooms"][key]["exits"] = room["exits"]
        manifest["rooms"][key]["spawns"] = room["spawns"]
        manifest["rooms"][key]["geometry_objects"] = [item for item in room["furniture"] if "texture" in item]
    validate_links(manifest)
    candidates[manifest_path] = json.dumps(manifest, ensure_ascii=False, indent=1).encode("utf-8")
    return publish(candidates)


def update_stands_only(keys, scenes=SCENES, manifest_path=OUT / "manifest.json"):
    """Append capacity markers without rewriting published art, geometry or text."""
    validate_maps(keys)
    manifest = read_json(manifest_path)
    fps = {**CH1_FP, **{name: entry.get("footprint") for name, entry in manifest.get("props", {}).items()}}
    authored = {room["key"]: room for room in rooms.all_rooms()}
    candidates = {}
    for key in keys:
        room = authored[key]
        room["furniture"] = resolve(room["furniture"], fps)
        points = stands(room)
        previous = stands({**room, "stand_capacity": {}})
        path = scenes / f"{key}.tscn"
        source = path.read_bytes().decode("utf-8")
        markers = []
        for match in re.finditer(r'(?ms)^\[node\b.*?(?=^\[|\Z)', source):
            header = match[0].splitlines()[0]
            if 'parent="Stands"' not in header:
                continue
            name = re.search(r'name="([^"]+)"', header)
            position = re.search(r'^position = Vector2\(([-\d.]+), ([-\d.]+)\)\r?$', match[0], re.M)
            if 'type="Marker2D"' not in header or name is None or position is None:
                raise ValueError(f"Unsupported published Stand: {key}")
            markers.append((name[1], [float(position[1]), float(position[2])], match.end()))
        published = [(name, point) for name, point, _ in markers]
        expected = [(f"s{i:02d}", point) for i, point in enumerate(points)]
        if published == expected:
            continue
        old = [(f"s{i:02d}", point) for i, point in enumerate(previous)]
        if not markers or published != old or points[:len(previous)] != previous:
            raise ValueError(f"Published Stands differ from their source recipe: {key}; retain the scene for review")
        newline = "\r\n" if "\r\n" in source else "\n"
        added = "".join(newline.join([
            f'[node name="s{i:02d}" type="Marker2D" parent="Stands"]',
            f'position = Vector2({x}, {y})', "", "",
        ]) for i, (x, y) in enumerate(points[len(previous):], len(previous)))
        end = markers[-1][2]
        candidates[path] = (source[:end] + added + source[end:]).encode("utf-8")
    return publish(candidates)

# Chapter-1 sprite footprints (w, h) measured from its placement table.
CH1_FP = {
    "bed_0": (36, 64), "desk_0": (58, 26), "chair_back": (14, 10), "chair_front": (14, 10), "wardrobe": (40, 16),
    "nightstand": (18, 12), "bookcase_0": (40, 14), "bookcase_study": (40, 14), "bookcase_study_b": (40, 14),
    "coat_stand": (12, 8), "study_table": (132, 46), "sideboard": (82, 22), "range_stove": (42, 20), "worktable": (96, 30),
    "washstand": (48, 20), "side_table_plain": (24, 12), "armchair": (28, 16), "floor_lamp": (10, 8), "barrel": (18, 12),
    "crate": (22, 10), "sack": (14, 8), "plant_stand": (16, 8), "bench": (40, 10), "transmitter": None,
}


def sprites_and_footprints():
    from first_awakening_art import props as ch1_props
    from chapter2_art import props_archive, props_core, props_service

    canvases, fps = {}, dict(CH1_FP)
    ch1 = ch1_props.build()
    for mod in (props_core, props_archive, props_service):
        canvases.update(mod.build())
        fps.update(mod.FOOTPRINTS)
    return canvases, ch1, fps


def resolve(furniture, fps):
    out = []
    for f in furniture:
        f = dict(f)
        if "footprint" not in f:
            fp = fps.get(f["sprite"])
            x, y = f["position"]
            f["footprint"] = None if fp is None else [int(x - fp[0] / 2), int(y - fp[1]), int(fp[0]), int(fp[1])]
        out.append(f)
    return out


def stands(room, n=16):
    """Standing points for people: open floor cells away from walls, furniture and doorways, spread out."""
    from chapter2_art import painter

    rows = room["rows"]
    if not rows:
        return room.get("stands", [])
    blocked = set()
    for f in room["furniture"]:
        if f.get("footprint"):
            x, y, w, h = f["footprint"]
            for cy in range((y - 12) // CELL, (y + h + 12) // CELL + 1):
                for cx in range((x - 12) // CELL, (x + w + 12) // CELL + 1):
                    blocked.add((cx, cy))
    for e in room["exits"]:
        if "cells" in e:
            x0, y0, x1, y1 = e["cells"]
            for cy in range(y0 - 2, y1 + 2):
                for cx in range(x0 - 2, x1 + 2):
                    blocked.add((cx, cy))
    ok = []
    for y in range(1, len(rows) - 1):
        for x in range(1, len(rows[0]) - 1):
            if (x, y) in blocked:
                continue
            if all(rows[y + dy][x + dx] in painter.WALKABLE for dy in (-1, 0, 1) for dx in (-1, 0, 1)):
                ok.append((x, y))
    if not ok:
        if room.get("stand_capacity"):
            raise ValueError(f"No legal standing candidates for {room['key']}")
        return []
    cx = sum(p[0] for p in ok) / len(ok); cy = sum(p[1] for p in ok) / len(ok)
    pick = [min(ok, key=lambda p: (p[0] - cx) ** 2 + (p[1] - cy) ** 2)]
    while len(pick) < min(n, len(ok)):
        pick.append(max(ok, key=lambda p: min((p[0] - q[0]) ** 2 + (p[1] - q[1]) ** 2 for q in pick)))
    # Preserve the original markers and their order. Whole-building spreading
    # alone does not guarantee enough initial places in each occupied region.
    for name, capacity in room.get("stand_capacity", {}).items():
        bounds = [r[1:] for r in room.get("regions", []) if r[0] == name]
        if len(bounds) != 1 or not isinstance(capacity, int) or capacity < 0:
            raise ValueError(f"Invalid standing capacity: {room['key']}:{name}")
        x0, y0, x1, y1 = bounds[0]
        within = lambda p: x0 <= p[0] < x1 and y0 <= p[1] < y1
        while sum(within(p) for p in pick) < capacity:
            available = [p for p in ok if within(p) and p not in pick]
            if not available:
                raise ValueError(f"Insufficient legal standing candidates: {room['key']}:{name}")
            pick.append(max(available, key=lambda p: min((p[0] - q[0]) ** 2 + (p[1] - q[1]) ** 2 for q in pick)))
    return [[x * CELL + CELL // 2, y * CELL + CELL - 4] for x, y in pick]


def wall_rects(rows):
    """Solid rectangles for every non-walkable cell, merged into horizontal runs."""
    from chapter2_art import painter

    rects = []
    for y, row in enumerate(rows):
        x = 0
        while x < len(row):
            if row[x] in painter.WALKABLE:
                x += 1
                continue
            start = x
            while x < len(row) and row[x] not in painter.WALKABLE:
                x += 1
            rects.append([start * CELL, y * CELL, (x - start) * CELL, CELL])
    return rects


# ---------------------------------------------------------------- scenes ----

def tscn(room: dict, arch_path: str, sprite_paths: dict, sizes: dict) -> str:
    ext = [("Texture2D", arch_path, "arch")]
    used = sorted({f["sprite"] for f in room["furniture"]})
    for name in used:
        ext.append(("Texture2D", sprite_paths[name], "t_" + name))
    lines = [f'[gd_scene load_steps={len(ext) + 1} format=3]', '']
    lines += [f'[ext_resource type="{k}" path="{p}" id="{i}"]' for k, p, i in ext]
    lines.append('')
    shapes = []
    solids = wall_rects(room["rows"]) + [f["footprint"] for f in room["furniture"] if f.get("footprint")] + room.get("solids_px", [])
    for i, (x, y, w, h) in enumerate(solids):
        shapes.append((f"s{i}", x + w / 2, y + h / 2, w, h))
    exits = []
    for e in room["exits"]:
        if "rect_px" in e:
            x, y, w, h = e["rect_px"]
        else:
            x0, y0, x1, y1 = e["cells"]
            x, y, w, h = x0 * CELL, y0 * CELL, (x1 - x0) * CELL, (y1 - y0) * CELL
        exits.append((e, x + w / 2, y + h / 2, w, h))
    for sid, _, _, w, h in shapes:
        lines += [f'[sub_resource type="RectangleShape2D" id="{sid}"]', f'size = Vector2({w}, {h})', '']
    for i, (_, _, _, w, h) in enumerate(exits):
        lines += [f'[sub_resource type="RectangleShape2D" id="e{i}"]', f'size = Vector2({w}, {h})', '']
    W = len(room["rows"][0]) * CELL if "size_px" not in room else room["size_px"][0]
    H = len(room["rows"]) * CELL if "size_px" not in room else room["size_px"][1]
    lines += [f'[node name="{room["key"]}" type="Node2D"]', 'texture_filter = 1',
              f'metadata/title = {json.dumps(room["title"], ensure_ascii=False)}', f'metadata/map_size = Vector2({W}, {H})']
    if room["rows"]:
        lines.append(f'metadata/terrain_rows = {json.dumps(room["rows"])}')
    lights = []
    for decor in room.get("decor", []):
        if decor[0] in ("window", "sconce"):
            lights.append({"kind": decor[0], "point": [decor[1], decor[2] * CELL + (48 if decor[0] == "window" else 36)]})
    for prop in room.get("furniture", []):
        if prop["sprite"] in ("floor_lamp", "range_stove", "fireplace"):
            lights.append({"kind": "lamp", "point": [prop["position"][0], prop["position"][1] - 28]})
    lines.append(f'metadata/stage_lights = {json.dumps(lights)}')
    lines += [f'metadata/{k} = {v}' for k, v in room.get("meta", {}).items()]
    lines += ['',
              '[node name="Architecture" type="Sprite2D" parent="."]', 'z_index = -10', 'texture = ExtResource("arch")', 'centered = false', '',
              '[node name="Objects" type="Node2D" parent="."]', 'y_sort_enabled = true', '']
    for f in room["furniture"]:
        w, h = sizes[f["sprite"]]
        x, y = f["position"]
        ox, oy = f.get("draw_offset", [-(w // 2), -h])
        lines += [f'[node name="{f["name"]}" type="Sprite2D" parent="Objects"]', f'position = Vector2({x}, {y})',
                  f'texture = ExtResource("t_{f["sprite"]}")', 'centered = false', f'offset = Vector2({ox}, {oy})']
        if "texture_region" in f:
            rx, ry, rw, rh = f["texture_region"]
            lines += ['region_enabled = true', f'region_rect = Rect2({rx}, {ry}, {rw}, {rh})']
        for prop in ("rotation_degrees", "z_index"):
            if prop in f: lines.append(f'{prop} = {f[prop]}')
        if f.get("occluder"):
            lines.append('metadata/occluder = true')
        lines.append('')
    lines += ['[node name="Solids" type="StaticBody2D" parent="."]', 'collision_mask = 0', '']
    for sid, cx, cy, _, _ in shapes:
        lines += [f'[node name="{sid}" type="CollisionShape2D" parent="Solids"]', f'position = Vector2({cx}, {cy})', f'shape = SubResource("{sid}")', '']
    lines += ['[node name="Exits" type="Node2D" parent="."]', '']
    for i, (e, cx, cy, _, _) in enumerate(exits):
        lines += [f'[node name="{e["name"]}" type="Area2D" parent="Exits"]', f'position = Vector2({cx}, {cy})', 'collision_layer = 0',
                  'collision_mask = 2', f'metadata/target = "{e["target"]}"', f'metadata/spawn = "{e["spawn"]}"', '',
                  f'[node name="Shape" type="CollisionShape2D" parent="Exits/{e["name"]}"]', f'shape = SubResource("e{i}")', '']
    lines += ['[node name="Spawns" type="Node2D" parent="."]', '']
    for name, (x, y) in room["spawns"].items():
        lines += [f'[node name="{name}" type="Marker2D" parent="Spawns"]', f'position = Vector2({x}, {y})', '']
    lines += ['[node name="Stands" type="Node2D" parent="."]', '']
    for i, (x, y) in enumerate(stands(room)):
        lines += [f'[node name="s{i:02d}" type="Marker2D" parent="Stands"]', f'position = Vector2({x}, {y})', '']
    lines += ['[node name="Sites" type="Node2D" parent="."]', '']
    for s in scene_sites(room):
        lines += [f'[node name="{s["id"]}" type="Marker2D" parent="Sites"]', f'position = Vector2({s["point"][0]}, {s["point"][1]})',
                  f'metadata/label = {json.dumps(s["label"], ensure_ascii=False)}', '']
        if s.get("geometry_only", False): lines.insert(len(lines) - 1, 'metadata/geometry_only = true')
    return "\n".join(lines)


def main():
    import argparse
    parser = argparse.ArgumentParser()
    modes = parser.add_mutually_exclusive_group()
    modes.add_argument("--text-only", nargs="+", metavar="MAP", help="Update existing interior titles and labels without rebuilding geometry.")
    modes.add_argument("--sites-only", nargs="+", metavar="MAP", help="Regenerate marker geometry with published labels, using existing textures.")
    modes.add_argument("--stands-only", nargs="+", metavar="MAP", help="Append declared standing capacity while preserving all other published scene bytes.")
    modes.add_argument("--geometry-only", nargs="+", metavar="MAP", help="Regenerate selected interiors from recipes and existing PNGs; do not paint or import assets.")
    modes.add_argument("--interiors-only", nargs="+", metavar="MAP", help="Regenerate selected interiors and their baked floor shadows, without outdoors.")
    parser.add_argument("--text-source", type=Path, default=TEXT_SOURCE,
                        help="Published map manifest or explicit flat text-binding JSON; does not run a story compiler.")
    parser.add_argument("--site-source", type=Path,
                        help="Complete authoring source directory for full outdoor reconstruction (campus.py, render_site.py, site.json and their data).")
    args = parser.parse_args()
    if args.text_only:
        print(f"room text: {update_text_only(args.text_only, text_source=args.text_source)} file(s) changed")
        return
    if args.sites_only:
        print(f"generated sites: {update_sites_only(args.sites_only, text_source=args.text_source)} file(s) changed")
        return
    if args.stands_only:
        print(f"generated stands: {update_stands_only(args.stands_only)} file(s) changed")
        return
    if args.interiors_only:
        print(f"generated interiors: {update_geometry_only(args.interiors_only, text_source=args.text_source, repaint=True)} file(s) changed")
        return
    if args.geometry_only:
        print(f"generated geometry: {update_geometry_only(args.geometry_only, text_source=args.text_source)} file(s) changed")
        return
    from chapter2_art import outdoor, painter

    if args.site_source is not None:
        outdoor.SITE_DIR = args.site_source.resolve()
    outdoor.require_site_source()
    text_table = load_room_text(args.text_source)
    all_rooms = [resolve_room_text(room, text_table) for room in rooms.all_rooms()]
    canvases, ch1, fps = sprites_and_footprints()
    # Resolve every external source and the outdoor build before publishing any
    # runtime image. Missing terrain/original art must not leave partial outputs.
    sprite_paths, sizes = {}, {}
    for name, can in canvases.items():
        sprite_paths[name] = f"res://ShareAsset/Internal/Props/Chapter2/props/{name}.png"
        sizes[name] = (can.w, can.h)
    for name, can in ch1.items():
        if name not in sprite_paths:
            sprite_paths[name] = f"{CH1}/{name}.png"
            sizes[name] = (can.w, can.h)
    for room in all_rooms:
        register_external_sprites(room, sprite_paths, sizes)
    regions, spawn_region, outdoor_sprites = outdoor.build(sizes, fps)
    (OUT / "rooms").mkdir(parents=True, exist_ok=True)
    (OUT / "props").mkdir(parents=True, exist_ok=True)
    SCENES.mkdir(parents=True, exist_ok=True)
    manifest = {"source": "mcp/asset_pipeline/build_chapter2_scenes.py", "rooms": {}, "props": {}}
    for name, can in canvases.items():
        path = OUT / "props" / f"{name}.png"
        can.save(path)
        manifest["props"][name] = {"size": [can.w, can.h], "footprint": fps.get(name), "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}
    for name, can in outdoor_sprites.items():
        can.save(OUT / "props" / f"{name}.png")
        sprite_paths[name] = f"res://ShareAsset/Internal/Props/Chapter2/props/{name}.png"
        sizes[name] = (can.w, can.h)
    for room in all_rooms:
        resolve_outdoor_exits(room, spawn_region)
        room["furniture"] = resolve(room["furniture"], fps)
        register_external_sprites(room, sprite_paths, sizes)
        missing = {f["sprite"] for f in room["furniture"]} - set(sprite_paths)
        assert not missing, (room["key"], missing)
        arch, anchors = painter.paint(room)
        arch.save(OUT / "rooms" / f"{room['key']}.png")
        (SCENES / f"{room['key']}.tscn").write_text(
            tscn(room, f"res://ShareAsset/Internal/Props/Chapter2/rooms/{room['key']}.png", sprite_paths, sizes), encoding="utf-8")
        manifest["rooms"][room["key"]] = {"title": room["title"], "cells": [len(room["rows"][0]), len(room["rows"])],
                                          "exits": room["exits"], "spawns": room["spawns"], "sites": scene_sites(room),
                                          "geometry_objects": [item for item in room["furniture"] if "texture" in item]}
        print(room["key"], arch.w, arch.h, len(room["furniture"]))
    for room in regions:
        room["architecture"].save(OUT / "rooms" / f"{room['key']}.png")
        (SCENES / f"{room['key']}.tscn").write_text(
            tscn(room, f"res://ShareAsset/Internal/Props/Chapter2/rooms/{room['key']}.png", sprite_paths, sizes), encoding="utf-8")
        manifest["rooms"][room["key"]] = {"title": room["title"], "size_px": room["size_px"], "exits": room["exits"],
                                          "spawns": room["spawns"], "sites": room["sites"]}
        print(room["key"], *room["size_px"], len(room["furniture"]), len(room["solids_px"]), "solids")
    # link check: every exit's target room and spawn exist
    validate_links(manifest)
    (OUT / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=1), encoding="utf-8")
    print("ok", len(manifest["rooms"]), "rooms", len(canvases), "chapter-2 sprites")


if __name__ == "__main__":
    main()
