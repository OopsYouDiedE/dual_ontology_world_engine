"""Compare the published geometry contract with every declared Godot map source.

This is a static inventory and topology check. It does not run collision motion,
navigation, a DV session, access rules, or action receipts.
"""
from __future__ import annotations

import argparse
from collections import deque
from itertools import combinations
import json
import math
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'mcp/asset_pipeline'))
from scene_geometry import OUTDOOR_FIELDS, outdoor_metadata, scene_exits, scene_point, scene_source, vector

GAME = ROOT / "godot_csharp"
CONTRACT = GAME / "Contracts/geometry_contract.json"
GROUPS = {"sites": "Sites", "stations": "Stations", "stands": "Stands", "spawns": "Spawns"}


def equal_point(left, right):
    return len(left) == len(right) == 2 and all(math.isclose(a, b, abs_tol=0.001) for a, b in zip(left, right))


def room_at(item, point):
    rooms = item["rooms"]
    if len(rooms) == 1:
        return rooms[0]["id"]
    x, y = point
    return next((room["id"] for room in rooms if any(
        rx <= x < rx + width and ry <= y < ry + height
        for rx, ry, width, height in room["rects"])), "")


def doorway_components(rows, item, cell_size):
    unseen = {(x, y) for y, row in enumerate(rows) for x, mark in enumerate(row) if mark == "D"}
    result = []
    while unseen:
        start = min(unseen)
        unseen.remove(start)
        pending, component, rooms = deque([start]), {start}, set()
        while pending:
            x, y = pending.popleft()
            for adjacent in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                ax, ay = adjacent
                if adjacent in unseen:
                    unseen.remove(adjacent)
                    component.add(adjacent)
                    pending.append(adjacent)
                elif 0 <= ay < len(rows) and 0 <= ax < len(rows[ay]) and rows[ay][ax] in ".S":
                    room = room_at(item, [(ax + 0.5) * cell_size, (ay + 0.5) * cell_size])
                    if room:
                        rooms.add(room)
        result.append((component, rooms))
    return result


def inspect_contract(contract_path: Path):
    contract = json.loads(contract_path.read_text(encoding="utf-8"))
    maps = contract["maps"]
    errors, coverage = [], {}

    def require(condition, message):
        if not condition:
            errors.append(message)

    require(contract["schema"] == "dv-geometry-v1", "Unsupported geometry schema")
    require("living_floor" in maps, "Production map living_floor is missing from the contract")
    for name, item in maps.items():
        prefix = name + ": "
        scene = GAME / item["scene"].removeprefix("res://")
        nodes, shapes = scene_source(scene)
        counts = {group: 0 for group in GROUPS}
        coverage[name] = counts
        require(equal_point(vector(nodes[""]["fields"]["metadata/map_size"]), item["size"]), prefix + "map size differs from scene")
        authored_outdoor = outdoor_metadata(nodes)
        require({field: item[field] for field in OUTDOOR_FIELDS if field in item} == authored_outdoor,
                prefix + "outdoor coordinate metadata differs from scene")
        room_ids = {room["id"] for room in item["rooms"]}
        require(len(room_ids) == len(item["rooms"]), prefix + "duplicate room IDs")
        require(bool(room_ids), prefix + "no room regions")
        for room in item["rooms"]:
            require(bool(room["rects"]), prefix + room["id"] + " has no region rectangles")
            for rect in room["rects"]:
                require(len(rect) == 4 and all(math.isfinite(n) for n in rect) and rect[2] > 0 and rect[3] > 0,
                        prefix + room["id"] + " has an invalid region rectangle")
        for group, scene_group in GROUPS.items():
            published = {marker["path"] for marker in item[group].values()}
            authored = {path for path, node in nodes.items()
                        if path.startswith(scene_group + "/") and node["type"] == "Marker2D"}
            require(published == authored, prefix + group + " inventory differs: missing=" + str(sorted(authored - published)) + ", stale=" + str(sorted(published - authored)))
            for marker_id, marker in item[group].items():
                label = prefix + group + "/" + marker_id
                path = marker["path"]
                if path not in nodes:
                    continue
                require(nodes[path]["type"] == "Marker2D", label + " is not a Marker2D")
                require(equal_point(scene_point(nodes, path), marker["point"]), label + " coordinates differ")
                require(marker["room"] == room_at(item, marker["point"]), label + " room differs from the published regions")
                if "label" in marker:
                    require(json.loads(nodes[path]["fields"].get("metadata/label", '""')) == marker["label"], label + " label differs")
                counts[group] += 1

        exits = {entry["id"]: entry for entry in item["exits"]}
        source_exits = {entry["id"]: entry for entry in scene_exits(nodes, shapes)}
        require(len(exits) == len(item["exits"]), prefix + "duplicate exit IDs")
        require(set(exits) == set(source_exits), prefix + "exit inventory differs from scene")
        for exit_id, entry in exits.items():
            label = prefix + "exit/" + exit_id
            if exit_id not in source_exits:
                continue
            authored = source_exits[exit_id]
            require(equal_point(authored['point'], entry['point']), label + " coordinates differ")
            for field in ('path', 'rect', 'target', 'spawn', 'internal_gate', 'gate'):
                require(entry.get(field) == authored.get(field), label + ' ' + field + ' differs')
            if entry['internal_gate']:
                require(bool(entry.get('gate')), label + ' internal gate has no access key')
            elif entry['target']:
                require(entry['target'] in maps, label + " target map is missing")
                if entry['target'] in maps:
                    require(entry['spawn'] in maps[entry['target']]['spawns'], label + " target spawn is missing")

        rows = json.loads(nodes[""]["fields"].get("metadata/terrain_rows", "[]"))
        components = doorway_components(rows, item, contract["cell_size"])
        declared_pairs = [set() for _ in components]
        door_ids = set()
        external_ids = set()
        for door in item["doors"]:
            label = prefix + "door/" + door["id"]
            require(door["id"] not in door_ids, label + " duplicates an ID")
            door_ids.add(door["id"])
            require(door["from"] in room_ids, label + " source room is missing")
            if "target_map" in door:
                external_ids.add(door["id"])
                entry = exits.get(door["id"])
                require(entry is not None, label + " has no matching scene exit")
                if entry is not None:
                    require(door["target_map"] == door["to"] == entry["target"], label + " target disagrees with exit")
                    require(equal_point(door["point"], entry["point"]), label + " point disagrees with exit")
                continue
            require(door["to"] in room_ids and door["from"] != door["to"], label + " destination room is missing or identical")
            cell = tuple(math.floor(n / contract["cell_size"]) for n in door["point"])
            index = next((i for i, (cells, _) in enumerate(components) if cell in cells), None)
            require(index is not None, label + " does not lie in an authored D doorway")
            if index is not None:
                pair = frozenset((door["from"], door["to"]))
                require(pair <= components[index][1], label + " rooms do not border its authored doorway")
                declared_pairs[index].add(pair)
        routed_exits = {key for key, entry in exits.items() if entry['target'] and not entry['internal_gate']}
        require(external_ids == routed_exits, prefix + "external door inventory differs from exits")
        for index, (_, neighbours) in enumerate(components):
            actual_pairs = {frozenset(pair) for pair in combinations(neighbours, 2)}
            require(actual_pairs == declared_pairs[index], prefix + "doorway component " + str(index) + " room connections differ")
        counts.update(rooms=len(room_ids), doors=len(door_ids), exits=len(exits))

    for name, additions in contract["additions"].items():
        require(name in maps, "Additions reference missing map " + name)
        if name not in maps:
            continue
        for group, point_field in (("sites", "point"), ("props", "position")):
            ids = set()
            for entry in additions[group]:
                label = name + ": additions/" + group + "/" + entry["id"]
                require(entry["id"] not in ids, label + " duplicates an ID")
                ids.add(entry["id"])
                point = entry[point_field]
                require(len(point) == 2 and all(math.isfinite(n) for n in point), label + " has invalid coordinates")
                require(all(0 <= point[i] <= maps[name]["size"][i] for i in range(2)), label + " lies outside the map")
                if group == "props":
                    require((GAME / entry["texture"].removeprefix("res://")).is_file(), label + " texture is missing")
                    rect = entry["footprint"]
                    require(len(rect) == 4 and rect[2] > 0 and rect[3] > 0, label + " footprint is invalid")
            coverage[name]["added_" + group] = len(ids)
    return {"scope": "static-contract-scene-inventory-and-door-topology", "contract": str(contract_path),
            "runtime_collision_motion": "not executed", "dv_session": "not executed",
            "coverage": coverage, "errors": errors, "passed": not errors}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--contract", type=Path, default=CONTRACT)
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    result = inspect_contract(args.contract)
    rendered = json.dumps(result, ensure_ascii=False, indent=2)
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(rendered + "\n", encoding="utf-8")
    print(rendered)
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())

