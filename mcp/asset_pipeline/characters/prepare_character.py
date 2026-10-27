"""Blender authoring tool: optimize a skinned GLB without splitting its limbs.

Run with Blender --background --python this_file -- --source file.glb --output dir.
The output is an asset candidate, not a gameplay test or an approved character.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector, Quaternion


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--ratio", type=float, default=0.2)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    if not 0.05 <= args.ratio <= 1.0:
        raise ValueError("ratio must be between 0.05 and 1.0")
    source, output = Path(args.source).resolve(), Path(args.output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.gltf(filepath=str(source))
    rigs = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if len(rigs) != 1:
        raise ValueError("Expected one skinned character armature")
    rig = rigs[0]
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"
              and any(mod.type == "ARMATURE" and mod.object == rig for mod in obj.modifiers)]
    if not meshes:
        raise ValueError("No meshes bound to the armature")
    report = {"source_sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
              "bones": len(rig.data.bones), "meshes": [], "status": "candidate-not-approved"}
    for obj in meshes:
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        before = sum(len(poly.vertices) - 2 for poly in obj.data.polygons)
        # Welding coincident UV seams retains a continuous deforming body surface.
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.mesh.remove_doubles(threshold=0.000001)
        bpy.ops.object.mode_set(mode="OBJECT")
        if before > 10000:
            modifier = obj.modifiers.new("GameTopology", "DECIMATE")
            modifier.ratio = args.ratio
            modifier.use_collapse_triangulate = True
            bpy.ops.object.modifier_move_up(modifier=modifier.name)
            bpy.ops.object.modifier_apply(modifier=modifier.name)
        after = sum(len(poly.vertices) - 2 for poly in obj.data.polygons)
        unweighted = sum(not any(group.weight > 0 for group in vertex.groups) for vertex in obj.data.vertices)
        if unweighted:
            raise ValueError(f"{obj.name}: {unweighted} vertices lost skin weights")
        report["meshes"].append({"name": obj.name, "triangles_before": before,
                                 "triangles_after": after, "unweighted_vertices": unweighted})
    bpy.ops.object.select_all(action="DESELECT")
    for obj in [rig, *meshes]: obj.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(output / "body_candidate.glb"), export_format="GLB",
                              use_selection=True, export_animations=False)
    (output / "inspection.json").write_text(json.dumps(report, indent=2), encoding="utf-8")

    # Neutral clay material exposes silhouette and deformation without painted detail.
    clay = bpy.data.materials.new("InspectionClay")
    clay.diffuse_color = (0.38, 0.48, 0.51, 1.0)
    for obj in meshes:
        obj.data.materials.clear()
        obj.data.materials.append(clay)
        for polygon in obj.data.polygons: polygon.material_index = 0
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 24
    scene.render.resolution_x, scene.render.resolution_y = 700, 900
    scene.render.resolution_percentage = 100
    scene.world.color = (0.15, 0.15, 0.15)
    bpy.ops.object.camera_add(location=(2.8, -5.0, 2.0))
    camera = bpy.context.object
    camera.rotation_euler = (Vector((0, 0, 0.9)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type, camera.data.ortho_scale = "ORTHO", 2.3
    scene.camera = camera
    bpy.ops.object.light_add(type="AREA", location=(2, -3, 4))
    bpy.context.object.data.energy, bpy.context.object.data.shape, bpy.context.object.data.size = 500, "DISK", 3.0
    bpy.ops.object.light_add(type="AREA", location=(-3, -1, 2))
    bpy.context.object.data.energy, bpy.context.object.data.size = 250, 3.0
    scene.render.filepath = str(output / "neutral.png")
    bpy.ops.render.render(write_still=True)
    # Review pose only; no fabricated production animation is exported.
    for name, angle, axis in [("lowerarm01.L", -65, (1,0,0)), ("lowerleg01.R", 55, (1,0,0))]:
        bone = rig.pose.bones.get(name)
        if bone is None: continue
        rest = bone.bone.matrix_local.to_quaternion()
        bone.rotation_mode = "QUATERNION"
        bone.rotation_quaternion = rest.inverted() @ Quaternion(axis, math.radians(angle)) @ rest
    scene.render.filepath = str(output / "deformation.png")
    bpy.ops.render.render(write_still=True)
    print(json.dumps(report))


if __name__ == "__main__": main()
