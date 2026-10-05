"""Independent camera/geometry audit and three renders; never saves the input blend."""
import bpy, os, json, hashlib, struct, sys, time
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
SOURCE = os.path.abspath(os.path.join(HERE, '../../remake-chapter2/campus-site'))
FINAL = os.path.join(HERE, 'final')
os.makedirs(FINAL, exist_ok=True)

def sha(path):
    with open(path, 'rb') as stream:
        return hashlib.sha256(stream.read()).hexdigest()

def meshes():
    bpy.context.view_layer.update()
    result = {}
    for ob in bpy.context.scene.objects:
        if ob.type != 'MESH':
            continue
        h = hashlib.sha256()
        for row in ob.matrix_world:
            h.update(struct.pack('<4f', *row))
        for vertex in ob.data.vertices:
            h.update(struct.pack('<3f', *vertex.co))
        for polygon in ob.data.polygons:
            h.update(struct.pack('<I', len(polygon.vertices)))
            h.update(struct.pack('<' + 'I' * len(polygon.vertices), *polygon.vertices))
            h.update(struct.pack('<I', polygon.material_index))
        for material in ob.data.materials:
            h.update(material.name.encode('utf-8'))
        # bmesh deletion can reorder indices without changing the physical geometry.
        canonical = hashlib.sha256()
        for row in ob.matrix_world:
            canonical.update(struct.pack('<4f', *row))
        coordinates = [struct.pack('<3f', *vertex.co) for vertex in ob.data.vertices]
        for coordinate in sorted(coordinates):
            canonical.update(coordinate)
        faces = []
        for polygon in ob.data.polygons:
            points = [coordinates[index] for index in polygon.vertices]
            rotations = [b''.join(points[index:]+points[:index]) for index in range(len(points))]
            faces.append(struct.pack('<II', len(points), polygon.material_index)+min(rotations))
        for face in sorted(faces):
            canonical.update(face)
        for material in ob.data.materials:
            canonical.update(material.name.encode('utf-8'))
        result[ob.name] = dict(sha256=h.hexdigest(),canonical_sha256=canonical.hexdigest(), vertices=len(ob.data.vertices), polygons=len(ob.data.polygons))
    return result

started = time.time()
parameters = json.load(open(os.path.join(HERE, 'camera_parameters.json'), encoding='utf-8'))
terrain = np.load(os.path.join(HERE, 'terrain.npz'))
xs, ys, zs = terrain['xs'], terrain['ys'], terrain['z']
scene_data = json.load(open(os.path.join(SOURCE, 'scene.json'), encoding='utf-8'))
original_meshes = meshes()
source_blend_sha = sha(os.path.join(HERE, 'camera_tests.blend'))
audit = dict(
    context='Independent fresh-context code/data and image verification; no design documents or prior evaluations read.',
    source_files={name: sha(os.path.join(SOURCE, name)) for name in ('build_3d.py', 'scene.json')},
    terrain_sha256=sha(os.path.join(HERE, 'terrain.npz')),
    input_blend_sha256=source_blend_sha,
    parameter_scene_hash_matches=parameters['scene_sha256'] == sha(os.path.join(SOURCE, 'scene.json')),
    parameter_terrain_hash_matches=parameters['terrain_sha256'] == sha(os.path.join(HERE, 'terrain.npz')),
    cameras=[], final_renders=[])

def ground(x, y):
    fx = np.interp(x, xs, np.arange(len(xs)))
    fy = np.interp(y, ys, np.arange(len(ys)))
    i, j = int(min(np.floor(fy), len(ys)-2)), int(min(np.floor(fx), len(xs)-2))
    dy, dx = fy-i, fx-j
    z = zs[i:i+2, j:j+2]
    return float((z[0,0]*(1-dx)+z[0,1]*dx)*(1-dy)+(z[1,0]*(1-dx)+z[1,1]*dx)*dy)

terrain_object = bpy.data.objects['地形']
for record in parameters['cameras']:
    ob = bpy.data.objects.get(record['name'])
    assert ob and ob.type == 'CAMERA', record['name']
    loc = ob.matrix_world.translation
    u, v = record['local_uv']
    xy = np.array(scene_data['origin']) + np.array(scene_data['eu'])*u + np.array(scene_data['ev'])*v
    terrain_z = ground(loc.x, loc.y)
    hit, hit_loc, _, _ = terrain_object.ray_cast(Vector((loc.x, loc.y, 10000)), Vector((0,0,-1)))
    direction = ob.matrix_world.to_quaternion() @ Vector((0,0,-1))
    target_direction = (Vector(record['target'])-loc).normalized()
    checked = dict(name=ob.name, location=list(loc), lens_mm=ob.data.lens,
        max_location_error_m=max(abs(a-b) for a,b in zip(loc,record['location'])),
        local_uv_position_error_m=float(np.linalg.norm(xy-np.array(loc[:2]))),
        target_direction_error=(direction-target_direction).length,
        bilinear_ground_z_m=terrain_z, eye_height_bilinear_m=loc.z-terrain_z,
        eye_height_expected_m=record['eye_above_ground_m'],
        height_error_m=loc.z-terrain_z-record['eye_above_ground_m'],
        mesh_ground_raycast_hit=hit, mesh_ground_z_m=hit_loc.z if hit else None,
        eye_height_mesh_m=loc.z-hit_loc.z if hit else None,
        clip_start_m=ob.data.clip_start, clip_end_m=ob.data.clip_end)
    checked['passed'] = checked['max_location_error_m'] < 0.0001 and abs(ob.data.lens-record['lens_mm']) < 0.0001 and abs(checked['height_error_m']) < 0.0001 and checked['target_direction_error'] < 0.0001
    audit['cameras'].append(checked)

scn = bpy.context.scene
scn.render.engine = 'BLENDER_EEVEE_NEXT'
scn.eevee.taa_render_samples = 64
scn.render.resolution_x, scn.render.resolution_y = 1920, 1080
scn.render.resolution_percentage = 100
scn.render.image_settings.file_format = 'PNG'
for name in ('02_court_eye', '03_court_6m', '05_south_25m'):
    path = os.path.join(FINAL, name+'.png')
    scn.camera = bpy.data.objects[name]
    scn.render.filepath = path
    before = time.time()
    if '--audit-only' not in sys.argv:
        if os.path.exists(path):
            raise RuntimeError('Refusing to overwrite existing final render: '+path)
        bpy.ops.render.render(write_still=True)
    with open(path, 'rb') as stream:
        header = stream.read(24)
    width, height = struct.unpack('>II', header[16:24])
    audit['final_renders'].append(dict(name=name,path=os.path.relpath(path,HERE).replace('\\','/'),sha256=sha(path),resolution=[width,height],engine=scn.render.engine,samples=scn.eevee.taa_render_samples,lens_mm=scn.camera.data.lens,location=list(scn.camera.location),render_seconds=time.time()-before if '--audit-only' not in sys.argv else None,reused_existing_independent_render='--audit-only' in sys.argv))

# Rebuild using the unchanged source geometry definitions and precisely the same terrain.
# The source's final save/render loop is excluded; no source files are changed.
source = open(os.path.join(SOURCE, 'build_3d.py'), encoding='utf-8').read().split("if '--save' in argv:")[0]
source = source.replace("T = np.load(os.path.join(HERE, 'terrain.npz'))", "T = np.load("+repr(os.path.join(HERE, 'terrain.npz'))+")")
ns = {'__file__': os.path.join(SOURCE,'build_3d.py'), '__name__': 'independent_source'}
saved_argv = sys.argv[:]
sys.argv = [sys.argv[0]]
exec(compile(source, ns['__file__'], 'exec'),ns)
sys.argv = saved_argv
reference_meshes = meshes()
canonical_equal = lambda name: original_meshes[name]['canonical_sha256'] == reference_meshes[name]['canonical_sha256']
geometry_equal = original_meshes.keys() == reference_meshes.keys() and all(canonical_equal(name) for name in original_meshes)
audit['geometry'] = dict(meshes_in_input=len(original_meshes),meshes_in_source_rebuild=len(reference_meshes),
    exact_geometry_transform_and_material_slot_match=geometry_equal,
    raw_vertex_index_order_match=original_meshes==reference_meshes,
    added_meshes=sorted(set(original_meshes)-set(reference_meshes)),
    missing_meshes=sorted(set(reference_meshes)-set(original_meshes)),
    changed_meshes=[name for name in original_meshes.keys() & reference_meshes.keys() if not canonical_equal(name)],
    index_order_only_differences=[name for name in original_meshes.keys() & reference_meshes.keys() if original_meshes[name]!=reference_meshes[name] and canonical_equal(name)],
    input_signatures=original_meshes,source_rebuild_signatures=reference_meshes)
audit['input_blend_unchanged'] = source_blend_sha == sha(os.path.join(HERE, 'camera_tests.blend'))
audit['elapsed_seconds'] = time.time()-started
json.dump(audit,open(os.path.join(HERE,'independent-camera-audit.json'),'w',encoding='utf-8'),ensure_ascii=False,indent=2)
print('INDEPENDENT AUDIT COMPLETE', 'geometry_match=', audit['geometry']['exact_geometry_transform_and_material_slot_match'], 'camera_passes=', all(c['passed'] for c in audit['cameras']), 'elapsed=', audit['elapsed_seconds'])
