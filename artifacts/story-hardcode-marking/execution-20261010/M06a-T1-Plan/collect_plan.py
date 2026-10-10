"""Read-only source/configuration inventory. No imports of project modules, generators or tests.

Only writes the JSON plan/evidence in this script's directory. Text values are hashed,
not reproduced in this artifact; the verbatim production migration belongs in .story.
"""
import ast
import hashlib
import json
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parents[4]
OUT = pathlib.Path(__file__).resolve().parent
CONTROL_HEAD = "232bb99aab6d0caac9dcd14edbead0f27347aefc"
SCRIPT = "godot_csharp/scenes/Awakening/Script"
STORY = SCRIPT + "/story"
MAP = "godot_csharp/scenes/Awakening/map_layout.tres"
ROOM_SOURCE = "godot_csharp/tools/chapter2_art/rooms.py"
MANIFEST = "godot_csharp/ShareAsset/Internal/Props/Chapter2/manifest.json"
INSIDE = ["living_hall", "zone_jia", "zone_yi", "zone_bing", "zone_ding", "central_hall",
          "lecture", "archive", "counterweight", "resonance", "clinic", "logistics", "granary", "greenhouse"]
COMPILER = [SCRIPT + "/" + name for name in ["generated_awakening_dialogue.json", "generated_awakening_content.json",
    "generated_awakening_layout.json", "generated_chapter_01_world.json", "Chapter1CompiledDialogue.cs"]]
COMPILER += ["godot_csharp/scenes/Chapter2/Script/generated_chapter_02.json", SCRIPT + "/generated_lines_zh.csv"]


def digest(data):
    return hashlib.sha256(data).hexdigest()


def text_digest(value):
    return digest(value.encode("utf-8"))


def info(path):
    p = ROOT / path
    if not p.exists():
        return {"path": path, "exists": False}
    b = p.read_bytes()
    return {"path": path, "exists": True, "bytes": len(b), "sha256": digest(b)}


def dump(name, data):
    (OUT / name).write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")


# Configuration only. No compiled story JSON is parsed or project Python executed.
layout = json.loads((ROOT / MAP).read_text(encoding="utf-8").split("metadata/data = ", 1)[1])
manifest = json.loads((ROOT / MANIFEST).read_text(encoding="utf-8"))
source_text = (ROOT / ROOM_SOURCE).read_text(encoding="utf-8")
tree = ast.parse(source_text)
labels = {}
titles = {}
title_values = {}
zone_values = {}
for assignment in [n for n in tree.body if isinstance(n, ast.Assign)]:
    if any(isinstance(t, ast.Name) and t.id == 'ZONE_KEY' for t in assignment.targets):
        zone_values = {value: label for label, value in ast.literal_eval(assignment.value).items()}
zone_suffix = next(n.value for n in ast.walk(tree) if isinstance(n, ast.Constant) and n.lineno == 145 and isinstance(n.value, str))
for function in [n for n in tree.body if isinstance(n, ast.FunctionDef)]:
    for node in ast.walk(function):
        if isinstance(node, ast.Dict):
            d = {k.value: v for k, v in zip(node.keys, node.values) if isinstance(k, ast.Constant)}
            if {"id", "label"} <= d.keys() and isinstance(d["id"], ast.Constant) and isinstance(d["label"], ast.Constant):
                labels[function.name, d["id"].value, d["label"].value] = d["label"].lineno
for node in ast.walk(tree):
    if isinstance(node, ast.Call) and isinstance(node.func, ast.Name) and node.func.id == "dict":
        d = {k.arg: k.value for k in node.keywords}
        if "key" in d and "title" in d and isinstance(d["key"], ast.Constant):
            titles[d["key"].value] = d["title"].lineno
            if isinstance(d["title"], ast.Constant):
                title_values[d["key"].value] = d["title"].value

anchors = ["window.EN", "window.R05", "window.R08", "window.hall", "rug.R08", "rug.R05", "rug.R07", "rug.R06", "slippers.R05"]
line_by_anchor = [196] * 4 + [199] * 4 + [200]
awakening_tree = ast.parse((ROOT / 'godot_csharp/tools/build_awakening_map.py').read_text(encoding='utf-8'))
literal_calls = {n.lineno: n for n in ast.walk(awakening_tree) if isinstance(n, ast.Call) and n.lineno in {196, 199, 200}}
aside_bindings = []
for index, (anchor, source_line) in enumerate(zip(anchors, line_by_anchor)):
    rect, value = layout["asides"][index]
    key = "map.awakening_yi." + anchor
    expression = literal_calls[source_line].args[0].elts[1]
    branch = 'body' if index in {0, 1, 2, 5, 7} else 'orelse'
    literal_node = getattr(expression, branch) if isinstance(expression, ast.IfExp) else expression
    source_literal_hash = text_digest(ast.literal_eval(literal_node))
    aside_bindings.append({
        "binding_id": key, "source_key": key, "table": "FURNITURE_ASIDES", "existing_output_ordinal": index,
        "text_origin": {"path": "godot_csharp/tools/build_awakening_map.py", "line": source_line,
                        "literal_branch": branch if source_line != 200 else "literal",
                        "source_literal_utf8_sha256": source_literal_hash,
                        "output_value_utf8_sha256": text_digest(value),
                        "source_literal_matches_existing_output": source_literal_hash == text_digest(value)},
        "author_target": {"path": STORY + "/第一章/热点与布局.story", "placement": "append new 家具旁白 block after existing EOF line 222"},
        "compiler_field": "generated_awakening_layout.json:furniture_asides[" + key + "]",
        "existing_output": "map_layout.tres:metadata/data.asides[" + str(index) + "][1]",
        "geometry_only": rect,
        "identity_rule": "explicit frozen source slot; never derive identity or choose text from x/y comparisons",
    })

room_names = {"R08": ("R08", 200), "R05": ("R05", 197), "EN": ("empty", 206),
              "R07": ("R07", 199), "R06": ("R06", 198), "ES": ("empty", 206)}
room_bindings = [{"binding_id": "map.awakening_yi.room." + room, "source_key": key, "table": "ROOM_NAMES",
                  "text_origin": {"path": STORY + "/第一章/热点与布局.story", "line": line},
                  "old_literal_origin": {"path": "godot_csharp/tools/build_awakening_map.py", "line": 44 if room in {"R08", "R05", "EN"} else 45},
                  "compiler_field": "generated_awakening_content.json:ROOM_NAMES[" + key + "]",
                  "existing_output": "map_layout.tres:metadata/data.rooms[" + room + "].label",
                  "output_value_utf8_sha256": text_digest(layout["rooms"][room]["label"])}
                 for room, (key, line) in room_names.items()]

chapter2_bindings = []
for room in INSIDE:
    data = manifest["rooms"][room]
    title_key = "资源.地图." + room + ".标题"
    source_title = zone_values[room] + zone_suffix if room.startswith('zone_') else title_values[room]
    chapter2_bindings.append({"binding_id": "chapter2.map." + room + ".title", "source_key": title_key,
        "text_origin": {"path": ROOM_SOURCE, "line": 145 if room.startswith("zone_") else titles[room],
                        "output_value_utf8_sha256": text_digest(data["title"]),
                        "source_literal_utf8_sha256": text_digest(source_title),
                        "source_literal_matches_existing_output": source_title == data["title"],
                        "expression": "existing zone title expression resolved for declared ZONE_KEY" if room.startswith("zone_") else "existing literal"},
        "author_target": {"path": STORY + "/续篇/07_家具与文本.story", "placement": "append new 文本 block inside existing 续篇 chapter2 after EOF line 418"},
        "compiler_field": "chapters[id=chapter2].design.text[" + title_key + "]",
        "existing_outputs": ["Maps/" + room + ".tscn:root.metadata/title", "manifest.json:rooms[" + room + "].title"]})
    for site in data["sites"]:
        site_key = "资源.场点." + room + "." + site["id"] + ".标签"
        chapter2_bindings.append({"binding_id": "chapter2.map." + room + ".site." + site["id"] + ".label", "source_key": site_key,
            "text_origin": {"path": ROOM_SOURCE, "line": labels.get(("zone" if room.startswith("zone_") else room, site["id"], site["label"])),
                            "output_value_utf8_sha256": text_digest(site["label"])},
            "author_target": {"path": STORY + "/续篇/07_家具与文本.story", "placement": "same new 文本 block"},
            "compiler_field": "chapters[id=chapter2].design.text[" + site_key + "]",
            "existing_outputs": ["Maps/" + room + ".tscn:Sites/" + site["id"] + ".metadata/label",
                                 "manifest.json:rooms[" + room + "].sites[id=" + site["id"] + "].label"],
            "geometry_only": site["point"]})

dump("bindings.json", {"status": "PROPOSED_NOT_A_LEASE", "classification": "DETECTION_ONLY",
    "literal_storage": ".story migration only; artifacts identify values by source location and UTF-8 hash",
    "T1a_asides": aside_bindings, "T1a_room_labels": room_bindings, "T1b_interior_text": chapter2_bindings})

sources = ["AGENTS.md", "godot_csharp/AGENTS.md", "godot_csharp/tools/build_awakening_map.py",
    "godot_csharp/tools/build_chapter2_scenes.py", "godot_csharp/tools/compile_story.py", "godot_csharp/tools/story_base.py",
    "godot_csharp/tools/story_chapter1.py", "godot_csharp/tools/story_design.py", "godot_csharp/tools/story_script.py", ROOM_SOURCE,
    STORY + "/第一章/热点与布局.story", STORY + "/第一章/提示与站位.story", STORY + "/生态/物件名.story", STORY + "/续篇/07_家具与文本.story"]
configs = [MAP, "godot_csharp/scenes/Awakening/Awakening.tscn", MANIFEST]
configs += ["godot_csharp/scenes/Chapter2/Maps/" + room + ".tscn" for room in sorted(manifest["rooms"])]
refs = {"godot_csharp/ShareAsset/Internal/Props/AwakeningEvidence/life_wing_yi.png"}
map_structure = {}
for path in configs:
    if not path.endswith(".tscn"):
        continue
    text = (ROOT / path).read_text(encoding="utf-8")
    for res in re.findall(r'^\[ext_resource .*?path="(res://[^"\n]+)"', text, flags=re.M):
        refs.add("godot_csharp/" + res.removeprefix("res://"))
    stripped = re.sub(r'^metadata/(?:title|label) = .*$', '', text, flags=re.M)
    site_nodes = re.findall(r'^\[node name="([^"]+)" type="Marker2D" parent="Sites"\]', text, flags=re.M)
    map_structure[path] = {"all_non_title_label_text_utf8_sha256": text_digest(stripped), "site_ids": site_nodes,
        "has_sites_segment": bool(re.search(r'^\[node name="Sites".*', text, flags=re.M)),
        "node_count": len(re.findall(r'^\[node ', text, flags=re.M))}

geometry = {k: v for k, v in layout.items() if k not in {"asides", "rooms"}}
geometry["asides"] = [x[0] for x in layout["asides"]]
geometry["rooms"] = {k: {a: b for a, b in v.items() if a != "label"} for k, v in layout["rooms"].items()}
manifest_structure = json.loads(json.dumps(manifest))
for data in manifest_structure["rooms"].values():
    data.pop("title", None)
    for site in data.get("sites", []):
        site.pop("label", None)

dump("baseline-evidence.json", {"status": "READ_ONLY_OBSERVATION_NOT_ACCEPTANCE", "classification": "DETECTION_ONLY",
    "baseline_head": CONTROL_HEAD, "baseline_provenance": "provided by controller; this agent did not invoke Git",
    "source_hashes": [info(p) for p in sources], "configuration_hashes": [info(p) for p in configs],
    "compiler_outputs_hash_only_not_parsed": [info(p) for p in COMPILER],
    "referenced_resources_hash_only_not_viewed": [info(p) for p in sorted(refs)],
    "map_structure": map_structure,
    "awakening_geometry_canonical_sha256": text_digest(json.dumps(geometry, ensure_ascii=False, sort_keys=True, separators=(",", ":"))),
    "chapter2_non_title_label_manifest_canonical_sha256": text_digest(json.dumps(manifest_structure, ensure_ascii=False, sort_keys=True, separators=(",", ":"))),
    "unexecuted": ["project module imports", "generators", "compiler", "builds", "tests", "Godot", "images", "compiled story JSON browsing", "Git"],
    "source_config_discrepancy": {"status": "STATIC_DERIVATION_ONLY_NOT_GENERATOR_RESULT",
        "existing_window_centers_x": [x[0] for x in layout["windows"]],
        "source_expression_centers_x": [1184, 960, 736, 448],
        "derivation": "rooms.py:122,125,165-177 zone_yi width 36*32; build_awakening_map.py:117-118 applies +128",
        "disposition": "T1a text-only retains current geometry and cannot silently absorb full regeneration"}})

dump("file-plan.json", {"schema": "M06a-T1-precise-plan/v1", "status": "PROPOSED_NOT_A_LEASE",
    "classification": "DETECTION_ONLY", "production_write_authorized": False, "baseline_head": CONTROL_HEAD,
    "scope": {"current": "first two chapters approximately 3-5 game days", "future": "40 game days namespace only, no duration/capacity claim"},
    "compiler_source_change": False, "runtime_core_audio_format_change": False,
    "T1a": {"independent_clone_and_single_writer_required": True,
        "source_write_paths": [STORY + "/第一章/热点与布局.story", "godot_csharp/tools/build_awakening_map.py", "godot_csharp/AGENTS.md"],
        "compiler_write_paths_exact": COMPILER,
        "compiler_expected_changed_paths": [SCRIPT + "/generated_awakening_layout.json"],
        "compiler_allowed_delta": "append exactly 9 FURNITURE_ASIDES keys at layout.furniture_asides; all preexisting keys/values unchanged",
        "compiler_must_remain_byte_identical": [p for p in COMPILER if p != SCRIPT + "/generated_awakening_layout.json"],
        "map_text_only_write_paths": [MAP],
        "map_expected_changed_paths": [], "map_must_remain_byte_identical": [MAP],
        "must_not_write": ["godot_csharp/scenes/Awakening/Awakening.tscn", "godot_csharp/ShareAsset/Internal/Props/AwakeningEvidence/life_wing_yi.png"],
        "text_migration_count": 9, "existing_room_label_binding_count": 6,
        "pipeline": ["official compile_story.py (all seven existing output paths)", "build_awakening_map.py --text-only (new restricted entry point)"]},
    "T1b": {"separate_lease_required": True,
        "requires_rebaseline_at_lease_after_T1a": True,
        "source_write_paths": [STORY + "/续篇/07_家具与文本.story", ROOM_SOURCE, "godot_csharp/tools/build_chapter2_scenes.py", "godot_csharp/AGENTS.md"],
        "compiler_write_paths_exact": COMPILER,
        "compiler_expected_changed_paths": ["godot_csharp/scenes/Chapter2/Script/generated_chapter_02.json"],
        "compiler_allowed_delta": "append exactly 54 scalar string keys in chapters[id=chapter2].design.text; no other fields change",
        "compiler_must_remain_byte_identical": [p for p in COMPILER if not p.endswith("generated_chapter_02.json")],
        "map_text_only_write_paths": ["godot_csharp/scenes/Chapter2/Maps/" + room + ".tscn" for room in INSIDE] + [MANIFEST],
        "map_expected_changed_paths": [],
        "map_must_remain_byte_identical": ["godot_csharp/scenes/Chapter2/Maps/" + room + ".tscn" for room in INSIDE] + [MANIFEST],
        "interior_room_keys": INSIDE, "room_title_count": 14, "site_label_count": 40,
        "must_not_write": ["godot_csharp/scenes/Chapter2/Maps/" + room + ".tscn" for room in manifest["rooms"] if room not in INSIDE],
        "all_assets_read_hash_only": True,
        "pipeline": ["official compile_story.py (all seven existing output paths)", "build_chapter2_scenes.py --text-only <reviewed interior keys> (new restricted entry point)"]},
    "global_byte_invariants": ["all PNG/audio/import assets", "all preexisting compiler data outside specified new text keys", "all scene/config node IDs, geometry, sprites, walls, collisions, exits, stands, lighting and particles"],
    "no_write_wildcards": True,
    "not_unlocked": ["T2 resource index", "T3 presentation/audio", "S18", "new DSL/public format", "Core", "outdoor recipes", "new narrative/content", "scene geometry regeneration"],
    "verification_role": "fresh independent test agent; implementer cannot execute acceptance; pressure separate from this static plan"})

print(json.dumps({"plan_artifacts": ["bindings.json", "baseline-evidence.json", "file-plan.json"],
    "binding_counts": [len(aside_bindings), len(room_bindings), sum('site' not in b['binding_id'] for b in chapter2_bindings),
                       sum('site' in b['binding_id'] for b in chapter2_bindings)],
    "configurations": len(configs), "referenced_resources_hashed": len(refs),
    "missing_source_line_refs": [b['binding_id'] for b in chapter2_bindings if not b['text_origin']['line']]}, ensure_ascii=False))
