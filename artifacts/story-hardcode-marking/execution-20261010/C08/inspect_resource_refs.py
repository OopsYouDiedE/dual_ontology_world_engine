"""C08 read-only structural resource inspection. Never invokes a generator.

Reads only the explicitly authorized sources and generated map configurations.
Reports IDs, paths, dimensions and hashes; does not export story prose.
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import re


REPO = Path(__file__).resolve().parents[4]
OUT = Path(__file__).resolve().parent
GAME = REPO / "godot_csharp"
SOURCES = [
    "artifacts/story-hardcode-marking/execution-20261010/F01/capability-manifest.json",
    "artifacts/story-hardcode-marking/execution-20261010/F01/REPORT.md",
    "artifacts/story-hardcode-marking/execution-20261010/F02/hardened_scope_guard.py",
    "godot_csharp/tools/build_awakening_map.py",
    "godot_csharp/tools/build_chapter2_scenes.py",
    "godot_csharp/tools/chapter2_art/rooms.py",
    "godot_csharp/scenes/UI/Panels/Dialogue/ChapterDialogue.gd",
    "godot_csharp/scenes/Awakening/Script/Chapter1Data.cs",
    "godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs",
    "godot_csharp/scenes/Awakening/Script/story/第一章/热点与布局.story",
    "godot_csharp/scenes/Awakening/Script/story/生态/物件名.story",
    "godot_csharp/scenes/Awakening/Script/story/续篇/07_家具与文本.story",
]
PARTIAL_SOURCES = {
    "godot_csharp/scenes/Shared/Audio/Sound.gd": "1-38",
    "godot_csharp/scenes/Host/GameEngine.cs": "1-30,54-126; resource inputs, presentation transport and conversion only",
    "godot_csharp/tools/story_base.py": "15-77; source origin and key-value/indent helpers",
    "godot_csharp/tools/story_chapter1.py": "70-145,400-418,1299-1319,1331-1364; resource DTO/table mapping and resource branches only",
    "godot_csharp/tools/story_design.py": "20-73,100-142,210-237,281-306,313-337; text resource compilation helpers only",
}


def fingerprint(path: Path) -> dict:
    data = path.read_bytes()
    return {"path": path.relative_to(REPO).as_posix(), "sha256": hashlib.sha256(data).hexdigest(),
            "bytes": len(data), "lines": len(data.splitlines())}


def resource_exists(value: str) -> bool:
    if not value.startswith("res://"):
        return False
    rel = value[6:]
    if any(part in {"", ".", ".."} for part in rel.split("/")) or "\\" in rel:
        raise ValueError("unsafe resource reference")
    return (GAME / rel).is_file()


def scene_refs(path: Path) -> dict:
    source = path.read_text(encoding="utf-8-sig")
    refs = []
    for header in re.findall(r'^\[ext_resource ([^\]]+)\]', source, flags=re.MULTILINE):
        attrs = dict(re.findall(r'(\w+)="([^"]+)"', header))
        if not {"type", "path", "id"} <= attrs.keys():
            raise ValueError("unparsed external resource header in " + str(path))
        refs.append({"kind": attrs["type"], "path": attrs["path"], "id": attrs["id"],
                     "exists": resource_exists(attrs["path"])})
    sprite_count = len(re.findall(r'\[node name="[^"]+" type="Sprite2D"', source))
    return {**fingerprint(path), "ext_resources": refs, "sprite_node_count": sprite_count,
            "site_marker_count": len(re.findall(r'\[node name="[^"]+" type="Marker2D" parent="Sites"\]', source)),
            "label_metadata_count": source.count("metadata/label ="),
            "title_metadata_count": source.count("metadata/title ="),
            "godot_serialization_format": re.search(r'^\[gd_scene [^\n]*format=(\d+)', source).group(1),
            "semantic_version_declared": "metadata/resource_contract_version" in source}


def main() -> None:
    report = {
        "schema": "static-resource-inspection/C08/v1", "classification": "DETECTION_ONLY",
        "baseline": "7489db56a714724eb7cb735958dd879dc662d238",
        "generators_invoked": False, "production_write_authorized": False,
        "semantics": "Static structure and filesystem existence only; no Godot loader or gameplay verification.",
        "source_evidence": [fingerprint(REPO / value) for value in SOURCES],
        "authorized_expansion": "Parent message approved map configurations and the five source resource/compiler bridges; no design document grant.",
        "expanded_source_evidence": [{**fingerprint(REPO / value), "content_read_ranges": ranges,
                                      "hash_scope": "full file bytes; content study limited to listed ranges"}
                                     for value, ranges in PARTIAL_SOURCES.items()],
        "configuration_read_scope": [],
    }
    layout = GAME / "scenes/Awakening/map_layout.tres"
    layout_data = json.loads(layout.read_text(encoding="utf-8-sig").split("metadata/data =", 1)[1])
    report["configuration_read_scope"].append(fingerprint(layout))
    report["awakening_layout"] = {
        "fields": sorted(layout_data), "furniture_count": len(layout_data["furniture"]),
        "room_ids": sorted(layout_data["rooms"]), "aside_count": len(layout_data["asides"]),
        "asides_are_literal_text": all(isinstance(item[1], str) for item in layout_data["asides"]),
        "version_declared": "format" in layout_data or "schema" in layout_data or "version" in layout_data,
        "furniture": [{key: item[key] for key in ("name", "sprite", "position", "footprint")}
                      for item in layout_data["furniture"]],
        "aside_rectangles": [item[0] for item in layout_data["asides"]],
    }
    awakening = scene_refs(GAME / "scenes/Awakening/Awakening.tscn")
    report["scene_configurations"] = [awakening]
    report["configuration_read_scope"].append({key: awakening[key] for key in ("path", "sha256", "bytes", "lines")})
    manifest_path = GAME / "ShareAsset/Internal/Props/Chapter2/manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    report["configuration_read_scope"].append(fingerprint(manifest_path))
    report["chapter2_manifest"] = {
        "top_level_fields": sorted(manifest), "room_count": len(manifest["rooms"]),
        "room_ids": sorted(manifest["rooms"]), "prop_count": len(manifest["props"]),
        "prop_ids": sorted(manifest["props"]),
        "version_declared": "format" in manifest or "schema" in manifest or "version" in manifest,
        "room_field_sets": sorted({tuple(sorted(room)) for room in manifest["rooms"].values()}),
        "exit_count": sum(len(room["exits"]) for room in manifest["rooms"].values()),
        "site_count": sum(len(room.get("sites", [])) for room in manifest["rooms"].values()),
    }
    bad_exits = []
    image_outputs = []
    for key, room in sorted(manifest["rooms"].items()):
        if not re.fullmatch(r"[a-z][a-z0-9_]*", key):
            raise ValueError("unsafe room id")
        for item in room["exits"]:
            target = manifest["rooms"].get(item["target"])
            if target is None or item["spawn"] not in target["spawns"]:
                bad_exits.append({"room": key, "exit": item["name"], "target": item["target"], "spawn": item["spawn"]})
        scene = scene_refs(GAME / "scenes/Chapter2/Maps" / f"{key}.tscn")
        report["scene_configurations"].append(scene)
        report["configuration_read_scope"].append({name: scene[name] for name in ("path", "sha256", "bytes", "lines")})
        resource = f"res://ShareAsset/Internal/Props/Chapter2/rooms/{key}.png"
        image_outputs.append({"path": resource, "exists": resource_exists(resource)})
    report["exit_reference_errors"] = bad_exits
    report["room_image_outputs"] = image_outputs
    refs = [item for scene in report["scene_configurations"] for item in scene["ext_resources"]]
    report["missing_ext_resources"] = sorted({item["path"] for item in refs if not item["exists"]})
    generated_props = sorted({item["path"] for item in refs
                              if item["path"].startswith("res://ShareAsset/Internal/Props/Chapter2/props/")})
    report["referenced_chapter2_prop_outputs"] = [{"path": value, "exists": resource_exists(value)}
                                                 for value in generated_props]
    report["referenced_chapter2_props_missing_manifest_row"] = [
        value for value in generated_props if Path(value).stem not in manifest["props"]]
    dialogue = (GAME / "scenes/UI/Panels/Dialogue/ChapterDialogue.gd").read_text(encoding="utf-8-sig")
    voice_paths = re.findall(r'preload\("(res://ShareAsset/Internal/Characters/voices/[^"]+)"\)', dialogue)
    report["dialogue_static_voices"] = [{"path": value, "exists": resource_exists(value)} for value in voice_paths]
    report["unverified"] = [
        "Sound.gd forwards the sfx key to GameAudio.play_sfx; actual GameAudio registry and clip resolution not reviewed.",
        "Actual cast snapshot and dynamic portrait existence; cast producer outside authorized source scope.",
        "Current generated story Content JSON file was not parsed; compile_story serialization and furniture translator not reviewed.",
        "Map runtime consumers and localization lookup behavior; not read here.",
    ]
    output = OUT / "resource-reference-check.json"
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    interior_ids = ["living_hall", "zone_jia", "zone_yi", "zone_bing", "zone_ding", "central_hall",
                    "lecture", "archive", "counterweight", "resonance", "clinic", "logistics", "granary", "greenhouse"]
    current_derived = [
        {"path": "godot_csharp/ShareAsset/Internal/Props/AwakeningEvidence/life_wing_yi.png", "producer": "godot_csharp/tools/build_awakening_map.py", "role": "architecture image; T1 requires byte preservation"},
        {"path": "godot_csharp/scenes/Awakening/map_layout.tres", "producer": "godot_csharp/tools/build_awakening_map.py", "role": "geometry and asides configuration"},
        {"path": "godot_csharp/scenes/Awakening/Awakening.tscn", "producer": "godot_csharp/tools/build_awakening_map.py", "role": "generated map node region in existing scene"},
        {"path": "godot_csharp/ShareAsset/Internal/Props/Chapter2/manifest.json", "producer": "godot_csharp/tools/build_chapter2_scenes.py", "role": "partial current asset/room manifest"},
    ]
    for key in sorted(manifest["rooms"]):
        current_derived.extend([
            {"path": f"godot_csharp/scenes/Chapter2/Maps/{key}.tscn", "producer": "godot_csharp/tools/build_chapter2_scenes.py", "role": "map configuration", "t1_interior_scope": key in interior_ids},
            {"path": f"godot_csharp/ShareAsset/Internal/Props/Chapter2/rooms/{key}.png", "producer": "godot_csharp/tools/build_chapter2_scenes.py", "role": "architecture/ground image; T1 requires byte preservation"},
        ])
    all_prop_resources = set(generated_props) | {f"res://ShareAsset/Internal/Props/Chapter2/props/{key}.png" for key in manifest["props"]}
    for value in sorted(all_prop_resources):
        current_derived.append({"path": "godot_csharp/" + value[6:], "producer": "godot_csharp/tools/build_chapter2_scenes.py",
                                "role": "chapter2 prop output; recipe module not studied; T1 requires byte preservation", "exists": resource_exists(value)})
    file_plan = {
        "schema": "resource-migration-file-plan/C08/v1", "status": "DRAFT_NOT_AN_ALLOWLIST",
        "classification": "DETECTION_ONLY", "production_write_authorized": False,
        "baseline": report["baseline"],
        "read_sources": report["source_evidence"] + report["expanded_source_evidence"],
        "T1_proposed_production_source_changes": [
            {"path": "godot_csharp/scenes/Awakening/Script/story/第一章/热点与布局.story", "purpose": "author-approved keyed map asides; reuse existing room-name author table"},
            {"path": "godot_csharp/scenes/Awakening/Script/story/续篇/07_家具与文本.story", "purpose": "author-approved resource title/site label text keys; existing 文本 syntax"},
            {"path": "godot_csharp/tools/build_awakening_map.py", "purpose": "resolve authored room labels/asides; preserve current output shape and geometry"},
            {"path": "godot_csharp/tools/chapter2_art/rooms.py", "purpose": "replace reviewed interior title/site prose with keyed references; preserve recipes"},
            {"path": "godot_csharp/tools/build_chapter2_scenes.py", "purpose": "resolve reviewed interior text keys into existing metadata/manifest shape"},
            {"path": "godot_csharp/AGENTS.md", "purpose": "synchronize implemented module workflow/contract changes if approved"},
        ],
        "T1_proposed_changed_derived_outputs": [
            "godot_csharp/scenes/Awakening/Script/generated_awakening_content.json",
            "godot_csharp/scenes/Chapter2/Script/generated_chapter_02.json",
            "godot_csharp/scenes/Awakening/map_layout.tres",
            "godot_csharp/scenes/Awakening/Awakening.tscn",
            "godot_csharp/ShareAsset/Internal/Props/Chapter2/manifest.json",
            *[f"godot_csharp/scenes/Chapter2/Maps/{key}.tscn" for key in sorted(interior_ids)],
        ],
        "compiler_output_caveat": "The two generated Content JSON names exist or are directly referenced by GameEngine, but compile_story output serialization was not reviewed. Its exact affected localization/line-map outputs must be authorized and frozen before a production lock; this is not claimed to be a complete compile_story output allowlist.",
        "current_map_generator_output_files_exact": current_derived,
        "current_dependency_resource_paths_exact": sorted({item["path"] for item in refs}),
        "current_voice_dependency_paths_exact": voice_paths,
        "unchanged_sources_in_T1": [
            "godot_csharp/scenes/Awakening/Script/story/生态/物件名.story",
            "godot_csharp/scenes/Awakening/Script/Chapter1Data.cs",
            "godot_csharp/scenes/Chapter2/Script/Chapter2Script.cs",
            "godot_csharp/scenes/UI/Panels/Dialogue/ChapterDialogue.gd",
            "godot_csharp/scenes/Shared/Audio/Sound.gd",
            "godot_csharp/scenes/Host/GameEngine.cs",
            "godot_csharp/tools/story_base.py",
            "godot_csharp/tools/story_chapter1.py",
            "godot_csharp/tools/story_design.py",
        ],
        "T2_proposed_new_file_names_not_approved": [
            {"path": "godot_csharp/tools/resource_index.py", "role": "pure emitter from formal parser DTO; no separate story parser"},
            {"path": "godot_csharp/scenes/Shared/Story/generated_resource_index.json", "role": "generated sidecar only; no v0 consumer switch"},
        ],
        "exact_read_candidates_before_any_implementation_lock": [
            {"path": "godot_csharp/tools/compile_story.py", "boundary": "resource/content serialization and exact generated output set; not read in C08"},
            {"path": "godot_csharp/tools/story_script.py", "boundary": "家具 author table translator only; not read in C08"},
            {"path": "godot_csharp/tools/chapter2_art/outdoor.py", "boundary": "six exterior map titles/eleven site author bindings and actual resource producer; not read in C08"},
            {"path": "godot_csharp/project.godot", "boundary": "GameAudio autoload source path only; registry file not guessed"},
            {"path": "godot_csharp/scenes/Shared/Story/InteractionPacket.cs", "boundary": "presentation packet producer/schema before T3; F01 evidence only"},
            {"path": "godot_csharp/scenes/Shared/Story/WorldBehavior.cs", "boundary": "F01 G05 field-copy preservation and presentation output before T3; F01 evidence only"},
            {"path": "godot_csharp/scenes/Shared/Game/GameSession.cs", "boundary": "presentation occurrence producer and transport before T3; F01 evidence only"},
        ],
        "T3_not_yet_lockable": "Exact audio registry and actual chapter packet producers must be resolved by narrowly authorized reads before deciding the source/import/consumer write set. C08 does not publish a speculative complete allowlist.",
        "scope_counts": {"current_chapter2_maps": len(manifest["rooms"]), "reviewed_interior_maps": len(interior_ids),
                         "reviewed_interior_sites": sum(len(manifest["rooms"][key]["sites"]) for key in interior_ids),
                         "remaining_exterior_maps": sorted(set(manifest["rooms"]) - set(interior_ids)),
                         "remaining_exterior_sites": sum(len(room["sites"]) for key, room in manifest["rooms"].items() if key not in interior_ids),
                         "exact_current_map_generator_outputs": len(current_derived)},
        "excluded_mutations": ["csharp/*", "docs/design-standard.md", "S18 and sealed evidence", "current expected/golden/automated capture artifacts", "manual generated file edits"],
    }
    (OUT / "file-plan.json").write_text(json.dumps(file_plan, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(json.dumps({"output": output.relative_to(REPO).as_posix(),
                      "rooms": report["chapter2_manifest"]["room_count"],
                      "sites": report["chapter2_manifest"]["site_count"],
                      "missing_ext_resources": report["missing_ext_resources"],
                      "exit_reference_errors": bad_exits,
                      "chapter2_props_without_manifest_rows": len(report["referenced_chapter2_props_missing_manifest_row"]),
                      "awakening_literal_asides": report["awakening_layout"]["aside_count"]}, ensure_ascii=False))


if __name__ == "__main__":
    main()
