"""Build a continuous skinned E character from its original modelling geometry.

Blender authoring pipeline, not a gameplay test. Joint locations and appearance
come from the exported DV recipe. Optional licensed motion is retargeted in rest space.
"""
import argparse
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree


def vector(point):
    return Vector((point[0], -point[2], point[1]))


def segment_distance(point, start, end):
    direction = end-start
    t = max(0.0, min(1.0, (point-start).dot(direction)/direction.length_squared))
    return (point-(start+direction*t)).length


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", required=True)
    parser.add_argument("--recipe", required=True)
    parser.add_argument("--identity", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--motion")
    parser.add_argument("--voxel", type=float, default=0.007)
    parser.add_argument("--triangles", type=int, default=28000)
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:])
    recipe = json.loads(Path(args.recipe).read_text(encoding="utf-8"))
    specs = recipe["body_template"]["bones"]
    output = Path(args.output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.gltf(filepath=str(Path(args.source).resolve()))
    garments = [obj for obj in bpy.context.scene.objects if obj.type == "MESH" and obj.name.startswith("ART_GARMENT_")]
    parts = [obj for obj in bpy.context.scene.objects if obj.type == "MESH" and obj not in garments]
    for obj in parts:
        points = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
        low = Vector(tuple(min(point[i] for point in points) for i in range(3)))
        high = Vector(tuple(max(point[i] for point in points) for i in range(3)))
        center = (low+high)*0.5
        if center.z > 0.86: region = 1
        elif center.z < 0.34 and abs(center.x)>0.05 and high.x-low.x<0.25: region = 5 if center.x<0 else 6
        elif abs(center.x)>0.17 and 0.37<center.z<0.82 and high.x-low.x<0.29: region = 3 if center.x<0 else 4
        else: region = 2
        attribute = obj.data.attributes.new("art_region","INT","FACE")
        for value in attribute.data: value.value = region
    bpy.ops.object.select_all(action="DESELECT")
    for obj in parts: obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    body = bpy.context.object
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for side in [-1,1]:
        bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=8,radius=1,location=(side*0.16,0,0.755))
        shoulder = bpy.context.object
        shoulder.scale = (0.085,0.09,0.10)
        attribute = shoulder.data.attributes.new("art_region","INT","FACE")
        for value in attribute.data: value.value = 3 if side<0 else 4
        shoulder.data.materials.append(body.data.materials[0])
        body.select_set(True)
        bpy.context.view_layer.objects.active = body
        bpy.ops.object.join()
    # The old display had no neck surface. Add actual geometry before union.
    bpy.ops.mesh.primitive_cylinder_add(vertices=20, radius=0.075, depth=0.14, location=(0,0,0.86))
    neck = bpy.context.object
    neck_material = bpy.data.materials.new("NeckSkin")
    neck_material.diffuse_color = (0.584,0.296,0.150,1)
    neck.data.materials.append(neck_material)
    attribute = neck.data.attributes.new("art_region","INT","FACE")
    for value in attribute.data: value.value = 2
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    original = body.data.copy()
    original_vertices = [vertex.co.copy() for vertex in original.vertices]
    original_faces = [tuple(poly.vertices) for poly in original.polygons]
    bvh = BVHTree.FromPolygons(original_vertices, original_faces)
    material_indices = [poly.material_index for poly in original.polygons]
    regions = [value.value for value in original.attributes["art_region"].data]
    remesh = body.modifiers.new("ContinuousSurface", "REMESH")
    remesh.mode = "VOXEL"
    remesh.voxel_size = args.voxel
    remesh.use_smooth_shade = True
    bpy.ops.object.modifier_apply(modifier=remesh.name)
    triangles = sum(len(poly.vertices)-2 for poly in body.data.polygons)
    if triangles > args.triangles:
        simplify = body.modifiers.new("GameTopology", "DECIMATE")
        simplify.ratio = args.triangles/triangles
        simplify.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=simplify.name)
    for polygon in body.data.polygons:
        hit = bvh.find_nearest(polygon.center)
        if hit[2] is not None: polygon.material_index = material_indices[hit[2]]
        polygon.use_smooth = True
    body.name = args.identity+"_Surface"
    armature = bpy.data.armatures.new("E_ThreeHead_Rig")
    rig = bpy.data.objects.new("Rig", armature)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    body.select_set(False)
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for name, spec in specs.items():
        bone = armature.edit_bones.new(name)
        bone.head, bone.tail = vector(spec["from"]), vector(spec["to"])
    for name, spec in specs.items():
        if spec["parent"]: armature.edit_bones[name].parent = armature.edit_bones[spec["parent"]]
    bpy.ops.object.mode_set(mode="OBJECT")
    groups = {name:body.vertex_groups.new(name=name) for name in specs if name != "root"}
    segments = [(name,vector(spec["from"]),vector(spec["to"])) for name,spec in specs.items() if name != "root"]
    for vertex in body.data.vertices:
        hit = bvh.find_nearest(vertex.co)
        region = regions[hit[2]]
        if region == 1:
            groups["head"].add([vertex.index],1.0,"REPLACE")
            continue
        if region in [3,4]:
            side = "l" if region == 3 else "r"
            allowed = {"upperarm."+side,"lowerarm."+side,"wrist."+side,"hand."+side}
            if vertex.co.z > 0.73: allowed.add("chest")
        elif region in [5,6]:
            side = "l" if region == 5 else "r"
            allowed = {"upperleg."+side,"lowerleg."+side,"foot."+side,"toes."+side}
            if vertex.co.z > 0.4: allowed.add("hips")
        else:
            allowed = {"hips"} if vertex.co.z < 0.55 else {"hips","spine","chest","neck","head"}
        nearest = sorted((segment_distance(vertex.co,start,end),name) for name,start,end in segments if name in allowed)[:4]
        values = [(name,1.0/(distance+0.025)**4) for distance,name in nearest]
        total = sum(value for _,value in values)
        for name,value in values: groups[name].add([vertex.index],value/total,"REPLACE")
    adjacency = [set() for _ in body.data.vertices]
    for edge in body.data.edges:
        a,b = edge.vertices
        adjacency[a].add(b)
        adjacency[b].add(a)
    weights = [{g.group:g.weight for g in vertex.groups} for vertex in body.data.vertices]
    for _ in range(8):
        smoothed = []
        for index,nearby in enumerate(adjacency):
            if body.data.vertices[index].co.z>0.91 or not nearby:
                smoothed.append(weights[index])
                continue
            values = {key:value*0.6 for key,value in weights[index].items()}
            for other in nearby:
                for key,value in weights[other].items(): values[key] = values.get(key,0)+value*0.4/len(nearby)
            top = sorted(values.items(),key=lambda item:item[1],reverse=True)[:4]
            total = sum(value for _,value in top)
            smoothed.append({key:value/total for key,value in top})
        weights = smoothed
    for group in body.vertex_groups: group.remove(list(range(len(body.data.vertices))))
    for index,values in enumerate(weights):
        for group,value in values.items(): body.vertex_groups[group].add([index],value,"REPLACE")
    modifier = body.modifiers.new("CharacterSkin", "ARMATURE")
    modifier.object = rig
    body.parent = rig
    for garment in garments:
        bpy.ops.object.select_all(action="DESELECT")
        garment.select_set(True)
        bpy.context.view_layer.objects.active = garment
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
        group = garment.vertex_groups.new(name="hips")
        group.add(list(range(len(garment.data.vertices))),1.0,"REPLACE")
        modifier = garment.modifiers.new("GarmentSkin","ARMATURE")
        modifier.object = rig
        garment.parent = rig
    motion_rig = None
    exported_actions = []
    if args.motion:
        previous = set(bpy.context.scene.objects)
        bpy.ops.import_scene.gltf(filepath=str(Path(args.motion).resolve()))
        imported = set(bpy.context.scene.objects)-previous
        motion_rig = next(obj for obj in imported if obj.type == "ARMATURE")
        for track in list(motion_rig.animation_data.nla_tracks): motion_rig.animation_data.nla_tracks.remove(track)
        clips = ["Idle","Walking_A","Walking_B","Interact","PickUp","Sit_Chair_Down","Sit_Chair_Idle","Sit_Chair_Pose","Sit_Chair_StandUp"]
        actions = list(bpy.data.actions)
        motion_rig.animation_data.action = next(action for action in actions if action.name.startswith("Idle_"))
        bpy.context.scene.frame_set(0)
        deps = bpy.context.evaluated_depsgraph_get()
        source_points = [obj.evaluated_get(deps).matrix_world @ vertex.co
                         for obj in imported if obj.type == "MESH" and obj.vertex_groups
                         for vertex in obj.evaluated_get(deps).data.vertices]
        motion_height = max(point.z for point in source_points)-min(point.z for point in source_points)
        motion_scale = recipe["body_template"]["nominal_height_m"]/motion_height
        reference_rotations = {bone.name:bone.matrix.to_3x3().copy() for bone in motion_rig.pose.bones}
        reference_locations = {bone.name:bone.location.copy() for bone in motion_rig.pose.bones}
        rig.animation_data_create()
        order = sorted(rig.pose.bones, key=lambda bone:len(bone.parent_recursive))
        for clip in clips:
            candidates = [action for action in actions if action.name.startswith(clip+"_")]
            if not candidates: raise ValueError("Missing licensed motion clip: "+clip)
            action = candidates[0]
            motion_rig.animation_data.action = action
            result = bpy.data.actions.new(clip)
            rig.animation_data.action = result
            start,end = map(int, action.frame_range)
            for frame in range(start,end+1):
                bpy.context.scene.frame_set(frame)
                rotations = {}
                deltas = {}
                for bone in order:
                    rest = bone.bone.matrix_local.to_3x3()
                    source = motion_rig.pose.bones.get(bone.name)
                    if source:
                        source_rest = source.bone.matrix_local.to_3x3()
                        delta = source.matrix.to_3x3() @ reference_rotations[bone.name].inverted()
                        bone.location = rest.inverted() @ (source_rest @ (source.location-reference_locations[bone.name]))*motion_scale
                    else:
                        delta = deltas.get(bone.parent.name,Matrix.Identity(3)) if bone.parent else Matrix.Identity(3)
                        bone.location = Vector((0,0,0))
                    desired = delta @ rest
                    parent_rest = bone.parent.bone.matrix_local.to_3x3() if bone.parent else Matrix.Identity(3)
                    parent_pose = rotations[bone.parent.name] if bone.parent else Matrix.Identity(3)
                    basis = rest.inverted() @ parent_rest @ parent_pose.inverted() @ desired
                    bone.rotation_mode = "QUATERNION"
                    bone.rotation_quaternion = basis.to_quaternion()
                    bone.keyframe_insert("rotation_quaternion",frame=frame-start,group=bone.name)
                    bone.keyframe_insert("location",frame=frame-start,group=bone.name)
                    rotations[bone.name], deltas[bone.name] = desired,delta
            exported_actions.append(result)
        for obj in imported: bpy.data.objects.remove(obj,do_unlink=True)
        motion_rig = None
        rig.animation_data.action = exported_actions[0]
        for action in list(bpy.data.actions):
            if action not in exported_actions: bpy.data.actions.remove(action)
    bpy.context.scene.frame_set(0)
    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    rig.select_set(True)
    for garment in garments: garment.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.gltf(filepath=str(output/(args.identity+".glb")),export_format="GLB",use_selection=True,
                              export_animations=bool(exported_actions),export_animation_mode="ACTIONS")
    bpy.ops.wm.save_as_mainfile(filepath=str(output/(args.identity+".blend")))
    report = {"identity":args.identity,"source":"original E geometry","status":"skin-prototype-pending-art-review",
              "triangles":sum(len(poly.vertices)-2 for poly in body.data.polygons),"vertices":len(body.data.vertices),
              "bones":len(armature.bones),"actions":[action.name for action in exported_actions],
              "body_surface":"voxel-union; no runtime segmented limbs","weight_method":"source-surface regions and rest-space bone blending"}
    (output/"inspection.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
    print(json.dumps(report))


if __name__ == "__main__": main()
