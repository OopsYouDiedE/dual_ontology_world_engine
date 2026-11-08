"""Publish only the campaign's referenced rooms, with shared immutable assets.

This is an authoring command, not a gameplay host. It copies no screenshots,
Blender files, candidate history, or unused rooms. Every published scene keeps
its own paired script. Identical GLBs and identical prefab definitions share
one resource, while authored instance transforms remain unchanged.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
import sys
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = "tools/building/candidates/lowpoly_chapters_v3/"
DESTINATION = "assets/scenes/campaign_rooms/"
PRESENTATION = "assets/data/presentation/campaign_rooms.dv"
COMPOSITOR = "tools/building/compose_courtyard_rooms.py"
RESOURCE = re.compile(r'\[ext_resource\s+type="([^"]+)"\s+path="([^"]+)"[^\]]*\]')


def read_presentation() -> tuple[dict, str]:
    source = (ROOT / PRESENTATION).read_text(encoding="utf-8")
    marker = re.search(r'\bvalue\s*=\s*|"value"\s*:\s*', source)
    if marker is None:
        raise ValueError("Campaign presentation has no value record")
    return json.JSONDecoder().raw_decode(source[marker.end():])[0], source[:marker.end()]


def local(resource: str) -> Path:
    if not resource.startswith("res://"):
        raise ValueError("Expected a project resource: " + resource)
    path = (ROOT / resource[6:]).resolve()
    if not path.is_relative_to(ROOT):
        raise ValueError("Resource escapes project: " + resource)
    return path


def sha256(content: bytes) -> str:
    return hashlib.sha256(content).hexdigest()


def resource_closure(roots: list[str]) -> dict[str, bytes]:
    result: dict[str, bytes] = {}
    pending = list(roots)
    while pending:
        resource = pending.pop()
        if resource in result:
            continue
        if not resource.startswith("res://" + SOURCE):
            raise ValueError("Candidate dependency outside the declared source: " + resource)
        path = local(resource)
        if not path.is_file():
            raise FileNotFoundError("Missing required campaign source: " + str(path))
        content = path.read_bytes()
        result[resource] = content
        if path.suffix == ".tscn":
            pending.extend(match[1] for match in RESOURCE.findall(content.decode("utf-8")))
        elif path.suffix not in {".gd", ".glb"}:
            raise ValueError("Unexpected campaign dependency: " + resource)
    return result


def existing_models(revision: str | None) -> dict[str, str]:
    """Reuse only content that already exists unchanged in the chosen git tree."""
    if revision is None:
        return {}
    listing = subprocess.check_output(["git", "ls-tree", "-r", "-z", revision], cwd=ROOT)
    algorithm = subprocess.check_output(["git", "rev-parse", "--show-object-format"], cwd=ROOT).decode().strip()
    result: dict[str, str] = {}
    for entry in listing.split(b"\0"):
        if not entry:
            continue
        metadata, filename = entry.split(b"\t", 1)
        relative = filename.decode("utf-8")
        if not relative.endswith(".glb"):
            continue
        path = ROOT / relative
        if not path.is_file():
            continue
        content = path.read_bytes()
        expected = metadata.split()[2].decode()
        actual = hashlib.new(algorithm, b"blob " + str(len(content)).encode() + b"\0" + content).hexdigest()
        if actual == expected:
            result.setdefault(sha256(content), "res://" + relative)
    return result


def published_text(source: str, resource_map: dict[str, str]) -> str:
    for original, replacement in resource_map.items():
        if original in source:
            source = source.replace(original, replacement)
    source = re.sub(r'^metadata/manifest\s*=.*\n?', "", source, flags=re.MULTILINE)
    source = source.replace("metadata/candidate_only = true", "metadata/candidate_only = false")
    source = source.replace('metadata/core_binding = "pending"', 'metadata/core_binding = "campaign.rooms"')
    return source


def put(resource: str, content: bytes, outputs: set[str]) -> None:
    path = local(resource)
    if not path.is_relative_to(ROOT / DESTINATION):
        raise ValueError("Publisher may only write its own output directory")
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.is_file() or path.read_bytes() != content:
        path.write_bytes(content)
    outputs.add(resource)


def composition_rooms(presentation: dict) -> dict[str, dict]:
    return {identity: room for identity, room in presentation["rooms"].items()
            if room.get("source_composition")}


def validate_outputs(outputs: set[str]) -> None:
    for resource in outputs:
        path = local(resource)
        if not path.is_file():
            raise FileNotFoundError("Missing published output: " + resource)
        if resource.endswith(".tscn"):
            for _, dependency in RESOURCE.findall(path.read_text(encoding="utf-8")):
                if not local(dependency).is_file():
                    raise FileNotFoundError("Unresolved published dependency: " + dependency)
            if not local(resource.removesuffix(".tscn") + ".gd").is_file():
                raise ValueError("Missing dedicated scene script: " + resource)


def merge_compositions(presentation: dict, report: dict) -> dict:
    """Publish compositions without treating their existing scenes as candidates.

    A composition reuses the manifest's shared immutable components. Its own
    layout and generator are the sources, and its room pairs are new outputs.
    The original candidate-closure statistics remain separately attributable.
    """
    rooms = composition_rooms(presentation)
    grouped = defaultdict(dict)
    for identity, room in rooms.items():
        grouped[room["source_composition"]][identity] = room
    previous_compositions = report.get("composition_sources", {})
    previous_ids = {identity for source in previous_compositions.values() for identity in source["rooms"]}
    if previous_ids - rooms.keys():
        raise ValueError("Removing composed rooms requires an explicit full publication, not an additive refresh")
    legacy_stats = report.get("source_statistics", {}).get("candidate_closure", {
        "source": SOURCE, "files": report["source_files"], "bytes": report["source_bytes"]})
    shared_components = {value["resource"] for value in report["components"].values()}
    shared_models = {value["resource"] for value in report["models"].values()}
    outputs = set(report["outputs"])
    sources, source_inputs = {}, {}
    for source, declared_rooms in sorted(grouped.items()):
        layout_path = local(source)
        layout = json.loads(layout_path.read_text(encoding="utf-8"))
        if layout.get("schema") != "campaign-room-composition-v1":
            raise ValueError("Unsupported composition declaration: " + source)
        defined_ids = {room["id"] for room in layout["rooms"]}
        if defined_ids != declared_rooms.keys():
            raise ValueError("Composition must publish exactly its registered rooms: " + source)
        expected = set()
        for identity, room in declared_rooms.items():
            scene = "res://" + DESTINATION + "rooms/" + identity + "/" + identity + ".tscn"
            if room["scene"] != scene:
                raise ValueError("Composed room path disagrees with its declaration: " + identity)
            expected.update([scene, scene.removesuffix(".tscn") + ".gd"])
        generated = subprocess.run(
            [sys.executable, str(ROOT / COMPOSITOR), "--layout", str(layout_path), "--publish"],
            cwd=ROOT, check=True, capture_output=True, text=True)
        receipt = json.loads(generated.stdout)
        authoring_path = Path(receipt["report"]).resolve()
        if not authoring_path.is_relative_to(ROOT / "tools/building/candidates"):
            raise ValueError("Composition returned an unexpected report path")
        authoring = json.loads(authoring_path.read_text(encoding="utf-8"))
        if (authoring["mode"] != "published" or authoring["layout"] != source
                or authoring["layout_sha256"] != sha256(layout_path.read_bytes())
                or set(authoring["outputs"]) != expected):
            raise ValueError("Composition output does not match the requested source")
        for dependency in authoring["dependencies"].values():
            if dependency["resource"] not in shared_components or dependency["model"] not in shared_models:
                raise ValueError("Composition introduced an unpublished shared dependency")
            for resource_key, hash_key in [("resource", "component_sha256"), ("model", "model_sha256"), ("script", "script_sha256")]:
                if sha256(local(dependency[resource_key]).read_bytes()) != dependency[hash_key]:
                    raise ValueError("Shared composition dependency changed during publication")
        for resource, information in authoring["outputs"].items():
            content = local(resource).read_bytes()
            if len(content) != information["bytes"] or sha256(content) != information["sha256"]:
                raise ValueError("Composed output changed during publication: " + resource)
        validate_outputs(expected)
        outputs.update(expected)
        generator_uri = "res://" + COMPOSITOR
        source_inputs[source] = {"bytes": layout_path.stat().st_size, "sha256": sha256(layout_path.read_bytes())}
        source_inputs[generator_uri] = {"bytes": (ROOT / COMPOSITOR).stat().st_size,
                                        "sha256": sha256((ROOT / COMPOSITOR).read_bytes())}
        sources[source] = {"rooms": sorted(declared_rooms), "source_sha256": source_inputs[source]["sha256"],
                           "generator": generator_uri, "generator_sha256": source_inputs[generator_uri]["sha256"],
                           "source_architecture": layout["source_architecture"],
                           "authoring_report": "res://" + authoring_path.relative_to(ROOT).as_posix(),
                           "outputs": sorted(expected), "new_model_bytes": 0}
        for identity, room in declared_rooms.items():
            report["rooms"][identity] = {"source": source[6:], "resource": room["scene"],
                                         "source_kind": "composition", "source_composition": source,
                                         "composition_room": identity, "generator": generator_uri,
                                         "source_architecture": layout["source_architecture"]}
    if set(report["rooms"]) != set(presentation["rooms"]):
        raise ValueError("Publication room set differs from the registered presentation")
    report["source_statistics"] = {"candidate_closure": legacy_stats,
                                    "composition_inputs": {"files": len(source_inputs),
                                                           "bytes": sum(v["bytes"] for v in source_inputs.values())}}
    report["composition_sources"] = sources
    report["composition_inputs"] = source_inputs
    report["source_files"] = legacy_stats["files"] + len(source_inputs)
    report["source_bytes"] = legacy_stats["bytes"] + sum(v["bytes"] for v in source_inputs.values())
    report["logical_rooms"] = len(presentation["rooms"])
    room_scenes = {room["scene"] for room in presentation["rooms"].values()}
    room_scenes.update(component["source_scene"] for room in presentation["rooms"].values()
                       for component in room.get("extra_components", []))
    report["room_scenes"] = len(room_scenes)
    report["outputs"] = sorted(outputs)
    report["published_bytes"] = sum(local(path).stat().st_size for path in outputs)
    return report


def refresh_compositions() -> dict:
    """Update composed room outputs and the manifest, without reading old candidates."""
    presentation, _ = read_presentation()
    presentation_before = (ROOT / PRESENTATION).read_bytes()
    manifest_path = ROOT / DESTINATION / "publication.json"
    report = json.loads(manifest_path.read_text(encoding="utf-8"))
    composed = composition_rooms(presentation)
    previous_legacy = {identity: room for identity, room in report["rooms"].items()
                       if room.get("source_kind") != "composition"}
    current_legacy = {identity: room for identity, room in presentation["rooms"].items() if identity not in composed}
    if set(previous_legacy) != set(current_legacy):
        raise ValueError("Legacy room changes require a full candidate publication")
    for identity, room in current_legacy.items():
        if room["scene"] != previous_legacy[identity]["resource"]:
            raise ValueError("Legacy scene reference changed: " + identity)
    owned = {"res://" + DESTINATION + "rooms/" + identity + "/" + identity + suffix
             for identity in composed for suffix in (".tscn", ".gd")}
    preserved = {resource: sha256(local(resource).read_bytes())
                 for resource in report["outputs"] if resource not in owned}
    report = merge_compositions(presentation, report)
    validate_outputs(set(report["outputs"]))
    for resource, digest in preserved.items():
        if sha256(local(resource).read_bytes()) != digest:
            raise ValueError("Additive publication changed a pre-existing shared or legacy output: " + resource)
    if (ROOT / PRESENTATION).read_bytes() != presentation_before:
        raise ValueError("Presentation changed concurrently; retry against its final declaration")
    manifest_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    summary = publication_summary(report)
    summary["mode"] = "compositions_only"
    summary["preserved_output_files"] = len(preserved)
    summary["preserved_outputs_sha256"] = sha256(json.dumps(preserved, sort_keys=True, separators=(",", ":")).encode())
    return summary


def publication_summary(report: dict) -> dict:
    return {key: value for key, value in report.items()
            if key not in {"rooms", "models", "components", "outputs", "composition_sources", "composition_inputs"}}


def publish(revision: str | None, prune: bool) -> dict:
    presentation, prefix = read_presentation()
    room_sources: dict[str, str] = {}
    roots: set[str] = set()
    for identity, room in presentation["rooms"].items():
        if room.get("source_composition"):
            continue
        source = room.get("source_candidate_scene", room["scene"])
        room_sources[identity] = source
        roots.add(source)
        for component in room.get("extra_components", []):
            roots.add(component.get("source_candidate_scene", component["source_scene"]))
    closure = resource_closure(sorted(roots))
    reusable = existing_models(revision)
    resource_map: dict[str, str] = {}
    outputs: set[str] = set()
    models: dict[str, dict] = {}
    components: dict[str, dict] = {}
    originals = defaultdict(list)
    for resource, content in closure.items():
        if resource.endswith(".glb"):
            digest = sha256(content)
            destination = reusable.get(digest, "res://" + DESTINATION + "models/" + digest + ".glb")
            resource_map[resource] = destination
            originals[digest].append(resource[6:])
            if digest not in models:
                reused = digest in reusable
                if not reused:
                    put(destination, content, outputs)
                models[digest] = {"resource": destination, "bytes": len(content), "reused_from_revision": revision if reused else None}
    # A prefab signature includes its collision/interaction definition, its
    # model references, and its paired script. Room placements never collapse.
    for resource in sorted(closure):
        if not resource.endswith(".tscn") or resource in roots:
            continue
        source = closure[resource].decode("utf-8")
        scripts = [path for kind, path in RESOURCE.findall(source) if kind == "Script"]
        if len(scripts) != 1 or not scripts[0].endswith(".gd"):
            raise ValueError("Every prefab requires one exclusive paired script: " + resource)
        script = scripts[0]
        normalized = published_text(source, resource_map).replace(script, "__PAIRED_SCRIPT__")
        digest = sha256(normalized.encode() + b"\0" + closure[script])
        base = "res://" + DESTINATION + "components/" + digest
        resource_map[resource] = base + ".tscn"
        resource_map[script] = base + ".gd"
        put(base + ".tscn", normalized.replace("__PAIRED_SCRIPT__", base + ".gd").encode(), outputs)
        put(base + ".gd", closure[script], outputs)
        components.setdefault(digest, {"resource": base + ".tscn", "sources": []})["sources"].append(resource[6:])
    for resource in sorted(roots):
        name = Path(resource).stem
        resource_map[resource] = "res://" + DESTINATION + "rooms/" + name + "/" + name + ".tscn"
        source = closure[resource].decode("utf-8")
        scripts = [path for kind, path in RESOURCE.findall(source) if kind == "Script"]
        if len(scripts) != 1:
            raise ValueError("Every room requires one paired script: " + resource)
        resource_map[scripts[0]] = "res://" + DESTINATION + "rooms/" + name + "/" + name + ".gd"
    for resource in sorted(roots):
        source = closure[resource].decode("utf-8")
        scripts = [path for kind, path in RESOURCE.findall(source) if kind == "Script"]
        put(resource_map[resource], published_text(source, resource_map).encode(), outputs)
        put(resource_map[scripts[0]], closure[scripts[0]], outputs)
    for identity, room in presentation["rooms"].items():
        if room.get("source_composition"):
            continue
        room["source_candidate_scene"] = room_sources[identity]
        room["scene"] = resource_map[room_sources[identity]]
        for component in room.get("extra_components", []):
            source = component.get("source_candidate_scene", component["source_scene"])
            component["source_candidate_scene"] = source
            component["source_scene"] = resource_map[source]
    validate_outputs(outputs)
    manifest_path = ROOT / DESTINATION / "publication.json"
    for digest in models:
        models[digest]["source_candidates"] = sorted(originals[digest])
    report = {"schema": "campaign-asset-publication-v1", "source": SOURCE,
              "logical_rooms": len(room_sources), "room_scenes": len(roots),
              "source_files": len(closure), "source_bytes": sum(map(len, closure.values())),
              "unique_models": len(models), "unique_prefabs": len(components),
              "new_model_bytes": sum(value["bytes"] for value in models.values() if value["reused_from_revision"] is None),
              "published_bytes": sum(local(path).stat().st_size for path in outputs),
              "reused_model_count": sum(value["reused_from_revision"] is not None for value in models.values()),
              "rooms": {identity: {"source": source[6:], "resource": resource_map[source]} for identity, source in room_sources.items()},
              "models": models, "components": components, "outputs": sorted(outputs)}
    report = merge_compositions(presentation, report)
    if prune and manifest_path.is_file():
        previous = json.loads(manifest_path.read_text())
        for resource in previous.get("outputs", []):
            path = local(resource)
            if resource not in report["outputs"] and path.is_relative_to(ROOT / DESTINATION) and path.is_file():
                path.unlink()
    manifest_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (ROOT / PRESENTATION).write_text(prefix + json.dumps(presentation, ensure_ascii=False, indent=2) + "; }\n", encoding="utf-8")
    return publication_summary(report)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--reuse-revision", help="Git revision whose unchanged GLBs may be reused directly")
    parser.add_argument("--prune", action="store_true", help="Remove only obsolete files named in this publisher's previous manifest")
    parser.add_argument("--compositions-only", action="store_true", help="Publish registered source_composition rooms and refresh the manifest without rewriting legacy rooms or presentation")
    args = parser.parse_args()
    if args.compositions_only and (args.reuse_revision or args.prune):
        parser.error("--compositions-only cannot change legacy reuse or pruning policy")
    print(json.dumps(refresh_compositions() if args.compositions_only else publish(args.reuse_revision, args.prune),
                     ensure_ascii=False, indent=2))
