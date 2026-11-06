"""Render an existing skinned character's authored clips for asset review in Blender."""
import argparse
from pathlib import Path
import sys
import bpy
from mathutils import Vector

parser = argparse.ArgumentParser()
parser.add_argument("--source", required=True)
parser.add_argument("--output", required=True)
parser.add_argument("--clips", nargs=3, default=["idle", "walk", "sit"])
args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
output = Path(args.output).resolve()
output.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(Path(args.source).resolve()))
rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"
          and not obj.name.startswith("Icosphere")]
for obj in bpy.context.scene.objects:
    if obj.type == "MESH" and obj not in meshes: obj.hide_render = True
rig.animation_data_create()
for track in list(rig.animation_data.nla_tracks): rig.animation_data.nla_tracks.remove(track)
points = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
low, high = min(p.z for p in points), max(p.z for p in points)
height = high - low
center = Vector((0, 0, (low + high)/2))
scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.samples = 24
scene.render.resolution_x, scene.render.resolution_y = 600, 750
scene.render.resolution_percentage = 100
scene.world.color = (0.18,0.18,0.18)
bpy.ops.object.camera_add(location=center+Vector((2,-4,1.5))*height)
camera = bpy.context.object
camera.rotation_euler = (center-camera.location).to_track_quat("-Z","Y").to_euler()
camera.data.type, camera.data.ortho_scale = "ORTHO", height*1.55
scene.camera = camera
for at, energy in [((2,-3,4),200),((-3,-1,2),120)]:
    bpy.ops.object.light_add(type="AREA", location=Vector(at)*height)
    bpy.context.object.data.energy = energy
    bpy.context.object.data.size = height*3
for clip, progress in zip(args.clips, [0, 0.3, 1]):
    actions = [action for action in bpy.data.actions if action.name == clip or action.name.startswith(clip+"_")]
    if not actions: continue
    action = actions[0]
    rig.animation_data.action = action
    scene.frame_set(int(action.frame_range[0] + progress*(action.frame_range[1]-action.frame_range[0])))
    scene.render.filepath = str(output/(clip+".png"))
    bpy.ops.render.render(write_still=True)
