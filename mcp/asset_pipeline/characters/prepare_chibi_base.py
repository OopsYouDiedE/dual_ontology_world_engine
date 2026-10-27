"""Prepare a licensed skinned chibi asset for character modelling.

Blender authoring tool: removes equipment, keeps skinning and authored actions,
and changes overall units uniformly. It never splits or stretches body segments.
"""
import argparse
import hashlib
import json
from pathlib import Path
import sys
import bpy

parser = argparse.ArgumentParser()
parser.add_argument("--source", required=True)
parser.add_argument("--output", required=True)
parser.add_argument("--height", type=float, default=1.32)
args = parser.parse_args(sys.argv[sys.argv.index("--")+1:])
source, output = Path(args.source).resolve(), Path(args.output).resolve()
output.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(source))
rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"
          and any(mod.type == "ARMATURE" and mod.object == rig for mod in obj.modifiers)]
if not meshes: raise ValueError("Character has no skinned surfaces")
removed = []
for obj in list(bpy.context.scene.objects):
    if obj != rig and obj not in meshes:
        removed.append(obj.name)
        bpy.data.objects.remove(obj, do_unlink=True)
rig.animation_data_create()
for track in list(rig.animation_data.nla_tracks): rig.animation_data.nla_tracks.remove(track)
idle = next(action for action in bpy.data.actions if action.name.startswith("Idle_"))
rig.animation_data.action = idle
bpy.context.scene.frame_set(0)
deps = bpy.context.evaluated_depsgraph_get()
points = [obj.evaluated_get(deps).matrix_world @ vertex.co
          for obj in meshes for vertex in obj.evaluated_get(deps).data.vertices]
bottom, top = min(p.z for p in points), max(p.z for p in points)
scale = args.height / (top-bottom)
root = bpy.data.objects.new("Character", None)
bpy.context.scene.collection.objects.link(root)
rig.parent = root
root.scale = (scale,scale,scale)
root.location.z = -bottom*scale
report = {"status":"modelling-base-not-final-character", "source_sha256":hashlib.sha256(source.read_bytes()).hexdigest(),
          "source_height":top-bottom, "uniform_scale":scale, "height":args.height,
          "bones":len(rig.data.bones), "removed_equipment":removed, "meshes":[]}
for obj in meshes:
    weighted = sum(bool(v.groups) for v in obj.data.vertices)
    blended = sum(sum(group.weight > 0.001 for group in v.groups)>1 for v in obj.data.vertices)
    report["meshes"].append({"name":obj.name,"vertices":len(obj.data.vertices),"weighted":weighted,"blended":blended})
    if weighted != len(obj.data.vertices): raise ValueError("Unweighted character vertices")
bpy.ops.object.select_all(action="DESELECT")
for obj in [root,rig,*meshes]: obj.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.gltf(filepath=str(output/"chibi_base.glb"),export_format="GLB",use_selection=True,
                          export_animations=True,export_animation_mode="ACTIONS")
bpy.ops.wm.save_as_mainfile(filepath=str(output/"chibi_base.blend"))
(output/"inspection.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
print(json.dumps(report))
