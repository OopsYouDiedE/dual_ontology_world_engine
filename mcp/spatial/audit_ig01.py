#!/usr/bin/env python3
"""Repeatable IG-01 pre-assembly audit. Read-only except for its own evidence report.

The report separates source-level checks from CLI and Godot receipts. It never
promotes static inspection into runtime acceptance or generates mock receipts.
Run from anywhere: py mcp/spatial/audit_ig01.py [--output PATH] [--strict].
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import pathlib
import subprocess
from datetime import datetime, timezone

ROOT = pathlib.Path(__file__).resolve().parents[2]
CONTENT = ROOT / "dv_core" / "content"
DEFAULT_REPORT = ROOT / "artifacts" / "acceptance-20261029" / "ig01-static-audit.json"
TASKS = ("1.1", "1.2", "1.3", "1.4", "1.5", "1.6")


def digest(path: pathlib.Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def dv_value(path: pathlib.Path) -> dict:
    source = path.read_text(encoding="utf-8-sig")
    marker = "value ="
    index = source.find(marker)
    if index < 0:
        raise ValueError(f"{path.name}: missing DV value")
    value, consumed = json.JSONDecoder().raw_decode(source[index + len(marker):].lstrip())
    if not isinstance(value, dict):
        raise ValueError(f"{path.name}: DV value must be a record")
    return value


def git(*arguments: str) -> str:
    try:
        result = subprocess.run(["git", "-C", str(ROOT), *arguments],
                                capture_output=True, text=True, encoding="utf-8",
                                errors="replace", check=True, timeout=15)
        return result.stdout.strip()
    except (OSError, subprocess.SubprocessError):
        return ""


def max_error(a: list, b: list) -> float:
    return max(abs(float(x) - float(y)) for x, y in zip(a, b))


def coordinates(source: dict, datum: list) -> dict:
    probes = source.get("coordinate_probes", [])
    results = []
    for probe in probes:
        local = probe["local"]
        world = [local[i] + datum[i] for i in range(3)]
        godot = [world[0], world[2], -world[1]]
        recovered_world = [godot[0], -godot[2], godot[1]]
        recovered_local = [recovered_world[i] - datum[i] for i in range(3)]
        errors = {
            "world_m": max_error(world, probe["world"]),
            "godot_m": max_error(godot, probe["godot_world"]),
            "round_trip_m": max_error(local, recovered_local),
        }
        results.append({"id": probe.get("id", ""), "errors": errors, "local": local})
    planar = [x["local"] for x in results]
    noncollinear = False
    for i in range(len(planar)):
        for j in range(i + 1, len(planar)):
            for k in range(j + 1, len(planar)):
                a, b, c = planar[i], planar[j], planar[k]
                cross = (b[0]-a[0]) * (c[1]-a[1]) - (b[1]-a[1]) * (c[0]-a[0])
                if abs(cross) > 1e-8:
                    noncollinear = True
    vertical = len({round(float(x[2]), 5) for x in planar}) > 1
    max_round_trip = max((r["errors"]["round_trip_m"] for r in results), default=math.inf)
    passed = (len(probes) >= 4 and noncollinear and vertical and
              all(max(r["errors"].values()) <= 0.001 for r in results))
    return {
        "datum_design_m": datum,
        "probe_count": len(results),
        "noncollinear_planar": noncollinear,
        "vertical_difference": vertical,
        "maximum_round_trip_m": max_round_trip,
        "arithmetic_passed": passed,
        "scope": "DV declaration arithmetic only; not Godot collisions, heading, or formal host",
        "probes": results,
    }


def catalogue(source: dict) -> dict:
    ids: dict[str, str] = {}
    duplicates: list[dict] = []
    missing: list[dict] = []
    category_counts: dict[str, int] = {}
    absent_wall_refs = 0
    unconfigured = 0
    status_counts: dict[str, int] = {}
    room_refs = {}
    for floor in source.get("floors", []):
        floor_id = floor["id"]
        if floor_id in ids:
            duplicates.append({"id": floor_id, "paths": [ids[floor_id], "floors"]})
        else:
            ids[floor_id] = "floors"
        room_refs[floor_id] = {x["id"] for x in floor.get("rooms", [])}
        for key in ("rooms", "pieces", "openings", "furniture", "colliders", "wall_segments"):
            items = floor.get(key, [])
            category_counts[key] = category_counts.get(key, 0) + len(items)
            for i, item in enumerate(items):
                loc = f"{floor_id}/{key}/{i}"
                identifier = item.get("id")
                if not isinstance(identifier, str) or not identifier:
                    missing.append({"path": loc, "field": "id"})
                elif identifier in ids:
                    duplicates.append({"id": identifier, "paths": [ids[identifier], loc]})
                else:
                    ids[identifier] = loc
                if key == "openings" and not item.get("wall_ref"):
                    absent_wall_refs += 1
                if key == "furniture":
                    room = item.get("room")
                    if room not in room_refs[floor_id]:
                        missing.append({"path": loc, "field": "room", "value": room})
                    status = item.get("interaction", {}).get("status", "missing")
                    status_counts[status] = status_counts.get(status, 0) + 1
                    unconfigured += status != "ready"
                if key == "openings":
                    for nested in ("frame", "door"):
                        child = item.get(nested)
                        if child is None:
                            continue
                        child_id = child.get("id") if isinstance(child, dict) else None
                        child_path = f"{loc}/{nested}"
                        if not isinstance(child_id, str) or not child_id:
                            missing.append({"path": child_path, "field": "id"})
                        elif child_id in ids:
                            duplicates.append({"id": child_id, "paths": [ids[child_id], child_path]})
                        else:
                            ids[child_id] = child_path
    for key in ("effective_stairs", "voids", "surface_materials",
                "spatial_anchors", "isolation_gate_candidates", "coordinate_probes"):
        items = source.get(key, [])
        category_counts[key] = len(items)
        for index, item in enumerate(items):
            location = f"{key}/{index}"
            identifier = item.get("id") if isinstance(item, dict) else None
            if not isinstance(identifier, str) or not identifier:
                missing.append({"path": location, "field": "id"})
            elif identifier in ids:
                duplicates.append({"id": identifier, "paths": [ids[identifier], location]})
            else:
                ids[identifier] = location
    paths = source.get("railing_assembly_candidate", {}).get("paths", [])
    category_counts["railing_paths"] = len(paths)
    for index, item in enumerate(paths):
        identifier = item.get("id") if isinstance(item, dict) else None
        location = f"railing_assembly_candidate/paths/{index}"
        if not isinstance(identifier, str) or not identifier:
            missing.append({"path": location, "field": "id"})
        elif identifier in ids:
            duplicates.append({"id": identifier, "paths": [ids[identifier], location]})
        else:
            ids[identifier] = location
    return {
        "counts": category_counts,
        "distinct_id_count": len(ids),
        "duplicate_ids": duplicates[:50],
        "duplicate_id_count": len(duplicates),
        "missing_id_or_room_refs": missing[:50],
        "missing_id_or_room_ref_count": len(missing),
        "openings_missing_wall_ref": absent_wall_refs,
        "interactions_not_ready": unconfigured,
        "interaction_statuses": status_counts,
    }


def inspect(root: pathlib.Path) -> dict:
    pack_path = CONTENT / "00-pack.data.dv"
    building_path = CONTENT / "95-configurable-building.data.dv"
    datum_path = CONTENT / "90-shared_building.data.dv"
    pack = dv_value(pack_path)
    building = dv_value(building_path)
    datum_data = dv_value(datum_path)
    datum_reference = building.get("world_datum_ref", {})
    datum = datum_data.get("coordinate_origin_world", [])
    manifest_selected = pack.get("spatial_definition")
    published = set(pack.get("definitions", []))
    manifest_valid = (manifest_selected == "space.configurable_building" and
                      manifest_selected in published and
                      datum_reference == {"definition": "space.shared_building",
                                          "field": "coordinate_origin_world",
                                          "file": "90-shared_building.data.dv"} and
                      datum_reference["definition"] in published and len(datum) == 3)
    catalog = catalogue(building)
    baselines = coordinates(building, datum) if manifest_valid else {"arithmetic_passed": False}
    world_stage = (root / "godot_csharp/scenes/World/Runtime/WorldStage3D.gd").read_text(encoding="utf-8-sig")
    main = (root / "godot_csharp/scenes/Host/Main.gd").read_text(encoding="utf-8-sig")
    object_node = (root / "godot_csharp/geometry/WorldObject3D.gd").read_text(encoding="utf-8-sig")
    bridge = (root / "godot_csharp/scenes/Host/SpatialActionBridge.gd").read_text(encoding="utf-8-sig")
    engine = (root / "godot_csharp/scenes/Host/GameEngine.cs").read_text(encoding="utf-8-sig")
    session_source = (root / "dv_core/Session/Session.cs").read_text(encoding="utf-8-sig")
    native_binding_fields = ("content_hash", "state_version")
    bridge_seals_binding = all(f'"{name}"' in bridge for name in native_binding_fields)
    session_checks_binding = all(f'"{name}"' in session_source for name in native_binding_fields)
    geometry = json.loads((root / "godot_csharp/Contracts/geometry_contract.json").read_text(encoding="utf-8-sig"))
    host_places = dv_value(CONTENT / "host-runtime.data.dv")
    declared_maps = {place["map"] for place in host_places.values() if isinstance(place, dict) and isinstance(place.get("map"), str)}
    geometry_maps = set(geometry["maps"])
    # The old Node2D host now obtains two main-building maps from DV, not from
    # the historical static geometry_contract.json. Count them only when
    # the runtime bridge and both real scenes are present.
    geometry_source = (root / "godot_csharp/scenes/Host/GeometryCatalog.gd").read_text(encoding="utf-8-sig")
    legacy_bridge = (root / "godot_csharp/geometry/LegacyDvMap.gd").read_text(encoding="utf-8-sig")
    compatibility_maps = {"main_1f", "main_2f"} if (
        "LEGACY_DV_MAP.map_data(map_id)" in geometry_source and
        "ReadPublishedData(SOURCE_FILE, SOURCE_ID)" in legacy_bridge and
        "_ensure_dv_room(identity)" in world_stage and
        all((root / f"godot_csharp/scenes/World/Maps/{name}.tscn").is_file()
            for name in ("main_1f", "main_2f"))
    ) else set()
    missing_formal_maps = sorted(declared_maps - geometry_maps - compatibility_maps)
    ready_block = world_stage.split("func _ready() -> void:", 1)[1].split("func _ensure_legacy_definitions", 1)[0]
    legacy_loader = world_stage.split("func _ensure_legacy_definitions", 1)[1].split("func ensure_room", 1)[0] if "func _ensure_legacy_definitions" in world_stage else ""
    new_map_branch = "if DV_MAP.supports(field.adapter.map_key())" in world_stage and "return _ensure_dv_room(identity)" in world_stage
    old_scene = 'ReadRecord("res://Content/world3d.dv"' in ready_block
    legacy_load_is_guarded = bool(legacy_loader and new_map_branch and
                                  'ReadRecord("res://Content/world3d.dv"' in legacy_loader and
                                  "_ensure_legacy_definitions()" in world_stage.split("func ensure_room", 1)[1].split("func _ensure_dv_room", 1)[0])
    old_mapping = "GEOMETRY.room_data(" in world_stage and "footprint" in world_stage and not new_map_branch
    old_host = 'FIELD_SCRIPT := preload("res://scenes/World/Runtime/WorldView.gd")' in main
    projection_source = (root / "godot_csharp/geometry/LegacyDvMap.gd").read_text(encoding="utf-8-sig")
    geometry_catalog = (root / "godot_csharp/scenes/Host/GeometryCatalog.gd").read_text(encoding="utf-8-sig")
    projection_scenes_missing = [name for name in ("main_1f", "main_2f")
                                 if not (root / f"godot_csharp/scenes/World/Maps/{name}.tscn").is_file()]
    logical_projection_is_dv_derived = all(token in projection_source for token in
        ('SOURCE_FILE := "95-configurable-building.data.dv"', 'SOURCE_ID := "space.configurable_building"',
         "func _terrain_rows(", 'piece.get("kind", "") != "floor"')) and 'layout.set_meta("terrain_rows", item.terrain_rows)' in geometry_catalog and not projection_scenes_missing
    actor_profile_is_dv_owned = ('ReadPublishedData("95-configurable-building.data.dv", "space.configurable_building")' in world_stage
                                 and '"host_actor_profile"' in world_stage)
    required_nodes = ("VisualRoot", "CollisionRoot", "InteractionArea",
                      "InteractionSlots", "StateVisuals", "LabelAnchor")
    nodes_present = all(f'"{name}"' in object_node for name in required_nodes)
    integration = {
        "formal_host_loads_legacy_world3d_in_ready": old_scene,
        "legacy_world3d_load_is_confined_to_non_dv_maps": legacy_load_is_guarded,
        "formal_host_uses_2d_logical_adapter": old_host,
        "logical_projection_is_dv_derived": logical_projection_is_dv_derived,
        "projection_scenes_missing": projection_scenes_missing,
        "formal_host_uses_old_rectangle_mapping": old_mapping,
        "actor_physics_profile_is_dv_owned": actor_profile_is_dv_owned,
        "spatial_node_structure_declared": nodes_present,
        "spatial_bridge_declared": "func execute(" in bridge,
        "host_bridges_prepared_actions": "spatial_action_bridge.execute(" in main,
        "host_checks_completion_event_duplicates": "completedEvents.Contains(actionEvent)" in engine,
        "native_bridge_seals_content_and_state": bridge_seals_binding,
        "core_session_checks_native_binding": session_checks_binding,
        "formal_declared_map_count": len(declared_maps),
        "formal_contract_map_count": len(geometry_maps),
        "legacy_dv_compatibility_maps": sorted(compatibility_maps),
        "formal_maps_absent_from_contract": missing_formal_maps,
    }
    checks = {
        "1.1": {
            "static_criteria_passed": bool(manifest_valid and
                                           catalog["duplicate_id_count"] == 0 and
                                           catalog["missing_id_or_room_ref_count"] == 0 and
                                           catalog["openings_missing_wall_ref"] == 0),
            "reason": "Some openings have no explicit wall reference; complete reverse-reference enforcement is still pending.",
        },
        "1.2": {
            "static_criteria_passed": bool(baselines["arithmetic_passed"] and
                                           logical_projection_is_dv_derived and not old_mapping),
            "reason": "DV baseline arithmetic passes; the formal map's logical grid is projected from the same DV floor pieces and the 3D transform uses DesignCoordinates.",
        },
        "1.3": {
            "static_criteria_passed": bool(not old_scene and legacy_load_is_guarded and
                                           logical_projection_is_dv_derived and actor_profile_is_dv_owned and
                                           not old_mapping and not missing_formal_maps),
            "reason": "The selected 95 DV supplies the formal 3D building, actor profile, room bounds and projected walkability. The historical room catalogue is lazy-loaded only for maps outside the selected DV building.",
        },
        "1.4": {
            "static_criteria_passed": nodes_present and catalog["interactions_not_ready"] == 0,
            "reason": "WorldObject3D structure exists, but DV interactive instances are not ready.",
        },
        "1.5": {
            "static_criteria_passed": bool(integration["spatial_bridge_declared"] and
                                           integration["host_bridges_prepared_actions"] and
                                           integration["host_checks_completion_event_duplicates"] and
                                           integration["native_bridge_seals_content_and_state"] and
                                           integration["core_session_checks_native_binding"]),
            "reason": "Static hash/version sealing and event guards exist; runtime cancellation and resource idempotence remain unverified.",
        },
        "1.6": {
            "static_criteria_passed": True,
            "reason": "Repeatable source-audit ledger is generated; CLI and Godot receipts still require independent recording.",
        },
    }
    commit = git("rev-parse", "--short=12", "HEAD")
    return {
        "schema": "ig01-static-audit-v1",
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "root": str(root),
        "git": {"head": commit, "branch": git("branch", "--show-current"),
                "dirty": bool(git("status", "--porcelain"))},
        "source_files_sha256": {
            str(path.relative_to(root)).replace("\\", "/"): digest(path)
            for path in (pack_path, building_path, datum_path)
        },
        "campaign_pack_content_hash": None,
        "campaign_pack_hash_note": "Only a real DV session may supply the complete compiled content hash.",
        "content_manifest": {"selected_spatial_definition": manifest_selected,
                             "selected_present": manifest_valid,
                             "definitions_count": len(published)},
        "catalogue": catalog,
        "coordinates": baselines,
        "formal_host": integration,
        "tasks": {
            task: {**checks[task], "acceptance": "not_verified",
                   "static_evidence": str(DEFAULT_REPORT.relative_to(root)).replace("\\", "/"),
                   "cli_receipt": None, "godot_receipt": None}
            for task in TASKS
        },
        "delivery_gate_passed": False,
        "boundary": "This tool does not run CLI or Godot or modify DV/engine state.",
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=pathlib.Path, default=DEFAULT_REPORT)
    parser.add_argument("--strict", action="store_true", help="Return 1 until IG-01 static gate passes")
    args = parser.parse_args()
    report = inspect(ROOT)
    evidence = str(args.output.resolve())
    operations = {
        "1.1": "parse selected spatial DV; enumerate IDs, room references, opening-wall references",
        "1.2": "compare DV reference probes with design-to-Godot and inverse transforms",
        "1.3": "inspect formal host entrypoint, legacy scene reads and rectangle mapping",
        "1.4": "inspect DV object readiness and required WorldObject3D nodes",
        "1.5": "inspect Core-prepared spatial bridge and duplicate completion guard",
        "1.6": "generate acceptance ledger separating static, CLI and Godot evidence",
    }
    for task, item in report["tasks"].items():
        item["static_receipt"] = {
            "version": report["git"]["head"],
            "content_hash": None,
            "source_sha256": report["source_files_sha256"],
            "host": "source-audit",
            "decision_maker": "deterministic-static-check",
            "test_method": "static-source-analysis",
            "preconditions": "current tracked and untracked working files readable; no runtime session started",
            "operation": operations[task],
            "result": "static-pass" if item["static_criteria_passed"] else "static-blocked",
            "detail": item["reason"],
            "evidence_path": evidence,
        }
        item["cli"] = {"status": "not_verified", "receipt_path": None}
        item["godot"] = {"status": "not_verified", "receipt_path": None}
    # Read existing formal host evidence. Never infer acceptance or silently
    # reuse an older run as proof of the current working-tree revision.
    cli_file = ROOT / "artifacts/acceptance-20261029/ig01-cli-live-observation.json"
    godot_file = ROOT / "artifacts/acceptance-20261029/ig01-godot-formal-observation.json"
    if cli_file.is_file():
        cli = json.loads(cli_file.read_text(encoding="utf-8"))
        observed = {
            "status": "observed-limited-scope",
            "version": cli.get("open", {}).get("version"),
            "content_hash": cli.get("open", {}).get("content_hash"),
            "host": "cli",
            "decision_maker": cli.get("decision_maker", "unknown"),
            "test_method": "official live CLI input",
            "preconditions": "new game from CLI; complete content bundle",
            "operation": "open then select the actually offered detective.opening.wake",
            "result": cli.get("steps", []),
            "receipt_path": str(cli_file),
            "recorded_epoch": cli.get("started_epoch"),
            "current_source_verified": False,
        }
        for task in ("1.1", "1.6"):
            report["tasks"][task]["cli"] = observed
    if godot_file.is_file():
        godot = json.loads(godot_file.read_text(encoding="utf-8"))
        observed = {
            "status": "observed-failed",
            "version": godot.get("post_start", {}).get("state", {}).get("state_version"),
            "content_hash": godot.get("post_start", {}).get("state", {}).get("content_hash"),
            "host": "godot",
            "decision_maker": "agent",
            "test_method": "official live Godot input",
            "preconditions": "title screen; new game selected",
            "operation": "start and observe until load completes or faults",
            "result": {"faulted": godot.get("post_start", {}).get("faulted"),
                       "errors": godot.get("post_start", {}).get("host_errors")},
            "receipt_path": str(godot_file),
            "screenshot_path": godot.get("screenshot_path"),
            "recorded_epoch": godot.get("started_epoch"),
            "current_source_verified": False,
        }
        for task in ("1.3", "1.6"):
            report["tasks"][task]["godot"] = observed
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"IG-01 audit: {args.output}")
    print(f"IDs: {report['catalogue']['distinct_id_count']}, duplicate: {report['catalogue']['duplicate_id_count']}, "
          f"openings without wall_ref: {report['catalogue']['openings_missing_wall_ref']}, "
          f"interactions not ready: {report['catalogue']['interactions_not_ready']}")
    print(f"4-point/vertical coordinate baseline: {report['coordinates']['arithmetic_passed']}")
    for task, value in report["tasks"].items():
        print(f"{task}: static={value['static_criteria_passed']}; acceptance={value['acceptance']}")
    return 1 if args.strict and not all(v["static_criteria_passed"] for v in report["tasks"].values()) else 0


if __name__ == "__main__":
    raise SystemExit(main())
