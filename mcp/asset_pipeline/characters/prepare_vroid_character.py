"""Import a VRoid VRM into Blender without changing body proportions or skinning.

Permanent asset-authoring entry point, not a gameplay test. The editable blend,
GLB and inspection report are candidates until reviewed in the real Godot host.
Existing project clothing is brought in as reference geometry, never auto-adopted.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import struct
import sys
import tempfile

import bpy


def read_vrm(path):
    data = path.read_bytes()
    if len(data) < 20:
        raise ValueError("Expected a binary VRM/glTF file")
    magic, version, length = struct.unpack_from("<4sII", data)
    if magic != b"glTF" or version != 2 or length != len(data):
        raise ValueError("Invalid glTF 2 binary header")
    chunk_length, chunk_type = struct.unpack_from("<II", data, 12)
    if chunk_type != 0x4E4F534A or 20 + chunk_length > len(data):
        raise ValueError("Missing glTF JSON chunk")
    document = json.loads(data[20:20 + chunk_length])
    extensions = document.get("extensions", {})
    if "VRMC_vrm" in extensions:
        vrm = extensions["VRMC_vrm"]
        human = vrm.get("humanoid", {}).get("humanBones", {})
        human = {name: value["node"] for name, value in human.items()}
        version = "1.0"
    elif "VRM" in extensions:
        vrm = extensions["VRM"]
        human = {item["bone"]: item["node"]
                 for item in vrm.get("humanoid", {}).get("humanBones", [])}
        version = "0.x"
    else:
        raise ValueError("Source has no VRM humanoid metadata")
    required = {"hips", "spine", "head", "leftUpperArm", "rightUpperArm",
                "leftLowerArm", "rightLowerArm", "leftUpperLeg", "rightUpperLeg",
                "leftLowerLeg", "rightLowerLeg", "leftFoot", "rightFoot"}
    missing = sorted(required - human.keys())
    if missing:
        raise ValueError(f"Missing humanoid bones: {missing}")
    for name, index in human.items():
        if not isinstance(index, int) or not 0 <= index < len(document.get("nodes", [])):
            raise ValueError(f"Invalid humanoid node: {name}")
    return data, {"vrm_version": version, "meta": vrm.get("meta", {}),
                  "humanoid_nodes": {name: {"index": index,
                      "name": document["nodes"][index].get("name", "")}
                      for name, index in human.items()}}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--height", type=float, required=True,
                        help="Explicit overall height in metres; uniform scaling only")
    parser.add_argument("--clothing-reference", help="Existing E character GLB, reference only")
    parser.add_argument("--skeleton-reference", help="Approved master inspection.json; require identical rest skeleton")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    if not math.isfinite(args.height) or args.height <= 0:
        raise ValueError("Height must be finite and positive")
    source, output = Path(args.source).resolve(), Path(args.output).resolve()
    data, metadata = read_vrm(source)
    output.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    # Blender's native glTF importer reads VRM's glTF skin and morph data. A .glb
    # suffix is used only for format dispatch; the source bytes remain unchanged.
    with tempfile.NamedTemporaryFile(suffix=".glb", delete=False) as handle:
        handle.write(data)
        import_path = Path(handle.name)
    try:
        bpy.ops.import_scene.gltf(filepath=str(import_path))
    finally:
        import_path.unlink(missing_ok=True)
    rigs = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if len(rigs) != 1:
        raise ValueError(f"Expected one armature, found {len(rigs)}")
    rig = rigs[0]
    # Names alone do not establish compatibility. Matrix, parent, axis and bone
    # lengths are all part of the shared rest skeleton contract.
    skeleton = [{"name": bone.name,
                 "parent": bone.parent.name if bone.parent else None,
                 "length": round(bone.length, 7),
                 "rest_matrix": [round(value, 7) for row in bone.matrix_local for value in row]}
                for bone in sorted(rig.data.bones, key=lambda item: item.name)]
    signature = hashlib.sha256(json.dumps(skeleton, sort_keys=True).encode()).hexdigest()
    if args.skeleton_reference:
        master = json.loads(Path(args.skeleton_reference).read_text(encoding="utf-8-sig"))
        if master.get("skeleton_signature") != signature:
            raise ValueError("Rest skeleton differs from master; retarget before sharing animations")
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    deform_bones = {bone.name for bone in rig.data.bones}
    inspection = []
    for obj in meshes:
        modifiers = [mod for mod in obj.modifiers
                     if mod.type == "ARMATURE" and mod.object == rig]
        if not modifiers:
            raise ValueError(f"Unbound mesh requires manual review: {obj.name}")
        groups = {group.index for group in obj.vertex_groups if group.name in deform_bones}
        unweighted = sum(not any(g.group in groups and g.weight > 0 for g in vertex.groups)
                         for vertex in obj.data.vertices)
        if unweighted:
            raise ValueError(f"{obj.name}: {unweighted} vertices have no bone weights")
        inspection.append({"name": obj.name, "vertices": len(obj.data.vertices),
                           "triangles": sum(len(p.vertices)-2 for p in obj.data.polygons),
                           "unweighted_vertices": unweighted,
                           "shape_keys": list(obj.data.shape_keys.key_blocks.keys())
                               if obj.data.shape_keys else []})
    if not meshes:
        raise ValueError("VRM contains no character mesh")
    points = [obj.matrix_world @ vertex.co for obj in meshes for vertex in obj.data.vertices]
    low, high = min(p.z for p in points), max(p.z for p in points)
    if high-low < 0.001:
        raise ValueError("Character bounds have no usable height")
    root = bpy.data.objects.new("VRoidCharacter", None)
    bpy.context.scene.collection.objects.link(root)
    # Keep the complete imported hierarchy, including expression meshes.
    for obj in list(bpy.context.scene.objects):
        if obj != root and obj.parent is None:
            obj.parent = root
    factor = args.height/(high-low)
    root.scale = (factor, factor, factor)
    root.location.z = -low*factor
    report = {"status": "imported-candidate-not-approved",
              "source": str(source), "source_sha256": hashlib.sha256(data).hexdigest(),
              **metadata, "bones": len(rig.data.bones), "meshes": inspection,
              "skeleton_signature": signature, "skeleton": skeleton,
              "bounds_height_before_m": high-low, "height_with_hair_m": args.height,
              "uniform_scale": factor, "head_ratio": "requires-chin-and-crown-measurement",
              "materials": "source-materials-require-project-shader-conversion",
              "expressions": "morph-targets-preserved-VRM-bindings-require-review",
              "animations": "none-added", "clothing": "existing-project-design-required"}
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.gltf(filepath=str(output/"character_candidate.glb"),
                              export_format="GLB", use_selection=True,
                              export_animations=False)
    if args.clothing_reference:
        reference = Path(args.clothing_reference).resolve()
        previous = set(bpy.context.scene.objects)
        bpy.ops.import_scene.gltf(filepath=str(reference))
        collection = bpy.data.collections.new("E_Clothing_Reference_ONLY")
        bpy.context.scene.collection.children.link(collection)
        for obj in set(bpy.context.scene.objects)-previous:
            for owner in list(obj.users_collection):
                owner.objects.unlink(obj)
            collection.objects.link(obj)
        collection.hide_render = True
        collection.hide_viewport = True
        report["clothing_reference"] = {"source": str(reference),
            "sha256": hashlib.sha256(reference.read_bytes()).hexdigest(),
            "status": "hidden-reference-not-exported-or-weighted"}
    bpy.ops.wm.save_as_mainfile(filepath=str(output/"character_working.blend"))
    (output/"inspection.json").write_text(json.dumps(report, ensure_ascii=False, indent=2),
                                         encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False))


if __name__ == "__main__":
    main()
