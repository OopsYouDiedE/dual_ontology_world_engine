"""Non-destructive, parameter-addressed Blender / Godot asset export.

The source .blend stays assembled. Each tagged component is exported once at
its own origin, then a Godot scene places independent prefab instances using
the same source transforms. Static architecture is exported separately.

Example (run inside Blender, after constructing the room)::

    tag_component(chair, 'chair_w052_d053_h098', 'chair',
                  {'width': .52, 'depth': .53, 'height': .98},
                  instance_id='lobby_chair_01')
    result = export_room_bundle(
        'output/godot', components=[chair, door, table],
        static_objects=[floor, walls, stairs, rails],
        res_base='res://tools/building/candidates/lowpoly_hall')

Coordinates: Blender (X,Y,Z) -> Godot (X,Z,-Y). glTF converts the mesh once;
instance matrices are C @ source_world @ C^-1. A Blender +Z yaw therefore
becomes a Godot +Y yaw of the SAME sign. Component front -Y becomes +Z.
Generated scripts expose visual/metadata hooks only. Authoritative actions,
occupancy, portal transitions and room loading remain the project's job.
"""
from __future__ import annotations

from collections import Counter
from contextlib import contextmanager
import hashlib
import json
import math
from pathlib import Path
import re
import struct
import uuid

import bpy
from mathutils import Matrix, Vector


SCHEMA_VERSION = 1
BLENDER_TO_GODOT = Matrix(((1, 0, 0, 0), (0, 0, 1, 0),
                           (0, -1, 0, 0), (0, 0, 0, 1)))
_C_INV = BLENDER_TO_GODOT.inverted()
_SAFE_ID = re.compile(r"^[A-Za-z_][A-Za-z0-9_-]*$")
_GEOMETRY_TYPES = {'MESH', 'CURVE', 'SURFACE', 'FONT'}


def _json(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True,
                      separators=(',', ':'), allow_nan=False)


def _plain(value):
    """Turn Blender ID properties and vectors into JSON-safe plain values."""
    if value is None or isinstance(value, (str, bool, int, float)):
        return value
    if hasattr(value, 'items'):
        return {str(k): _plain(v) for k, v in value.items()}
    return [_plain(v) for v in value]


def _identifier(value, label='identifier'):
    value = str(value)
    if not _SAFE_ID.fullmatch(value):
        raise ValueError('%s must be an ASCII filename identifier: %r' % (label, value))
    return value


def _node_name(value):
    value = re.sub(r'[^A-Za-z0-9_]', '_', str(value)).strip('_')
    return ('N_' if value[:1].isdigit() else '') + (value or 'Node')


def make_asset_id(kind, parameters):
    """A stable asset key; include seed/shape/material variants in parameters."""
    digest = hashlib.sha256(_json(parameters).encode('utf8')).hexdigest()[:12]
    return _identifier('%s_%s' % (_node_name(kind).lower(), digest), 'asset_id')


def tag_component(root, asset_id, kind, parameters=None, instance_id=None,
                  interactive=True, anchors=None, entity_id='', portal_id='',
                  collision_boxes=None):
    """Tag an existing component; geometry remains completely untouched.

    anchors is a mapping of names to local Blender positions, or dictionaries
    containing position/rotation_z/pose/clearance_radius_m. Existing generator
    handle, passage and light anchors are also recorded without reparenting.
    collision_boxes is optional explicit geometry, never an inferred solid
    bounding box: [{name, center:[x,y,z], size:[x,y,z]}], in root-local metres.
    A box may have parent_node equal to a source hinge/mesh name, in which case
    center/size are relative to that node. Runtime attaches it to that node.
    """
    root['asset_id'] = _identifier(asset_id, 'asset_id')
    root['component_kind'] = str(kind)
    root['asset_parameters_json'] = _json(parameters or {})
    root['instance_id'] = _identifier(instance_id or _node_name(root.name), 'instance_id')
    root['interactive'] = bool(interactive)
    root['entity_id'] = str(entity_id)
    root['portal_id'] = str(portal_id)
    if anchors is not None:
        root['component_anchors_json'] = _json(anchors)
    if collision_boxes is not None:
        root['collision_boxes_json'] = _json(collision_boxes)
    return root


def godot_matrix(blender_matrix):
    """Convert a node transform (not a camera's intrinsic optical axes)."""
    return BLENDER_TO_GODOT @ Matrix(blender_matrix) @ _C_INV


def _rows(matrix):
    return [[float(v) for v in row] for row in matrix]


def _num(value):
    value = float(value)
    if not math.isfinite(value):
        raise ValueError('non-finite transform')
    return format(0.0 if abs(value) < 5e-10 else value, '.9g')


def _transform_text(matrix):
    # Godot text resources serialize basis COLUMNS, then translation.
    return 'Transform3D(%s)' % ', '.join(_num(matrix[r][c])
                                        for c in range(4) for r in range(3))


def _vec_text(vector):
    return 'Vector3(%s)' % ', '.join(_num(v) for v in vector)


def _string(value):
    return json.dumps(str(value), ensure_ascii=False)


def _props(obj, key, default):
    raw = obj.get(key)
    if raw is None:
        return default
    return json.loads(raw) if isinstance(raw, str) else _plain(raw)


def _walk(roots, include_hidden=False):
    seen = set()
    result = []

    def visit(obj):
        if obj in seen:
            return
        seen.add(obj)
        if (obj.hide_render and not include_hidden) or obj.get('export_exclude', False):
            return
        result.append(obj)
        for child in sorted(obj.children, key=lambda x: x.name):
            visit(child)

    for obj in roots:
        visit(obj)
    return result


def _root_list(objects):
    objects = list(dict.fromkeys(objects))
    selected = set(objects)
    result = []
    for obj in objects:
        parent = obj.parent
        while parent is not None and parent not in selected:
            parent = parent.parent
        if parent is None:
            result.append(obj)
    return result


def _material_copy(source, created):
    """Explicit PBR translation; Cycles Backfacing/LightPath Mix is not glTF."""
    material = bpy.data.materials.new('__LPX_PBR_' + source.name)
    created.append(material)
    material.use_nodes = True
    material.use_backface_culling = True
    material.diffuse_color = tuple(source.diffuse_color)
    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new('ShaderNodeOutputMaterial')
    bsdf = nodes.new('ShaderNodeBsdfPrincipled')
    bsdf.inputs['Base Color'].default_value = tuple(source.diffuse_color)
    original = next((n for n in source.node_tree.nodes
                     if n.type == 'BSDF_PRINCIPLED'), None) if source.use_nodes else None
    for name in ('Base Color', 'Roughness', 'Metallic', 'IOR', 'Alpha',
                 'Emission Color', 'Emission Strength'):
        if original is not None and name in original.inputs and name in bsdf.inputs:
            socket = original.inputs[name]
            if socket.is_linked:
                raise ValueError('Exporter requires solid values, not textures: %s/%s' %
                                 (source.name, name))
            value = socket.default_value
            bsdf.inputs[name].default_value = tuple(value) if hasattr(value, '__len__') else value
    # The pane is deliberately coloured and opaque to the camera. Its mesh gets
    # cast_shadow OFF in Godot, matching the source's shadow-transparent shader.
    bsdf.inputs['Alpha'].default_value = 1.0
    material.node_tree.links.new(bsdf.outputs['BSDF'], output.inputs['Surface'])
    return material


@contextmanager
def _temporary_asset(roots, prototype=None):
    """Copy evaluated geometry + hierarchy, export without touching source IDs.

    Copies share no mesh/material data with the working scene. Scene/selection
    are restored even on exporter errors. Blender 4.2 and 4.5 are supported via
    the operator RNA rather than a version-specific argument list.
    """
    context = bpy.context
    if context.mode != 'OBJECT':
        raise RuntimeError('Run export in Object mode; source mode will not be changed.')
    context.view_layer.update()
    depsgraph = context.evaluated_depsgraph_get()
    # Tagged hidden components still need complete reusable assets. Their
    # preview visibility is an instance property, not a geometry deletion.
    sources = _walk(roots, include_hidden=prototype is not None)
    supported = [o for o in sources if o.type in _GEOMETRY_TYPES or o.type == 'EMPTY']
    if not any(o.type in _GEOMETRY_TYPES for o in supported):
        raise ValueError('Asset has no renderable geometry.')
    original_scene = context.scene
    selected = list(context.selected_objects)
    active = context.view_layer.objects.active
    created_meshes, created_materials, created_objects = [], [], []
    material_map = {}
    temp_scene = bpy.data.scenes.new('__LPX_EXPORT_' + uuid.uuid4().hex[:8])
    prefix = '__LPX_' + uuid.uuid4().hex[:8] + '_'
    name_map, source_names, clones = {}, {}, {}
    used_names = Counter()
    allocated_names = {'Asset'}
    root_inverse = prototype.matrix_world.inverted() if prototype else Matrix.Identity(4)
    bounds_min = Vector((math.inf, math.inf, math.inf))
    bounds_max = Vector((-math.inf, -math.inf, -math.inf))
    shadowless, hinges, source_anchors, light_sources = [], [], [], []
    root_node = None
    try:
        root_node = bpy.data.objects.new(prefix + 'Asset', None)
        created_objects.append(root_node)
        temp_scene.collection.objects.link(root_node)
        root_node.matrix_world = Matrix.Identity(4)
        name_map[root_node.name] = 'Asset'
        if prototype is not None and prototype.type == 'EMPTY':
            clones[prototype] = root_node
            source_names[prototype.name] = 'Asset'
        for source in supported:
            if source == prototype and source.type == 'EMPTY':
                continue
            base = source.name
            if prototype and base.startswith(prototype.name + '_'):
                base = base[len(prototype.name) + 1:]
            base = _node_name(base)
            used_names[base] += 1
            canonical = base if used_names[base] == 1 else '%s_%03d' % (base, used_names[base])
            while canonical in allocated_names:
                used_names[base] += 1
                canonical = '%s_%03d' % (base, used_names[base])
            allocated_names.add(canonical)
            data = None
            if source.type in _GEOMETRY_TYPES:
                evaluated = source.evaluated_get(depsgraph)
                data = bpy.data.meshes.new_from_object(evaluated, preserve_all_data_layers=True,
                                                      depsgraph=depsgraph)
                created_meshes.append(data)
                name_map[data.name] = canonical + '_Mesh'
                materials = [slot.material for slot in evaluated.material_slots]
                if len(data.polygons) and (not materials or any(m is None for m in materials)):
                    raise ValueError('Every exported mesh/material slot must have a solid material: ' + source.name)
                data.materials.clear()
                for mat in materials:
                    if mat not in material_map:
                        material_map[mat] = _material_copy(mat, created_materials)
                    data.materials.append(material_map[mat])
                local_world = root_inverse @ source.matrix_world
                for vertex in data.vertices:
                    point = local_world @ vertex.co
                    for axis in range(3):
                        bounds_min[axis] = min(bounds_min[axis], point[axis])
                        bounds_max[axis] = max(bounds_max[axis], point[axis])
                if (not getattr(source, 'visible_shadow', True) or
                        any(m and m.get('pane_model') for m in materials)):
                    shadowless.append(canonical)
            clone = bpy.data.objects.new(prefix + canonical, data)
            created_objects.append(clone)
            temp_scene.collection.objects.link(clone)
            clones[source] = clone
            source_names[source.name] = canonical
            name_map[clone.name] = canonical
        # Set hierarchy and LOCAL matrices directly. This avoids stale world
        # matrices while parenting a chain before a dependency graph update.
        for source, clone in clones.items():
            if clone == root_node:
                continue
            parent = source.parent
            while parent is not None and parent not in clones:
                parent = parent.parent
            clone.parent = clones[parent] if parent in clones else root_node
            if parent in clones:
                clone.matrix_basis = parent.matrix_world.inverted() @ source.matrix_world
            else:
                clone.matrix_basis = root_inverse @ source.matrix_world
            clone.matrix_parent_inverse = Matrix.Identity(4)
            canonical = source_names[source.name]
            if source.get('kind') == 'door_hinge':
                hinges.append({'name': canonical, 'source_name': source.name,
                               'open_angle_degrees': float(source.get('open_angle_degrees', 0)),
                               'local_matrix_godot': _rows(godot_matrix(clone.matrix_basis))})
            if source.type == 'EMPTY' and any(s in source.name.lower()
                                            for s in ('anchor', 'stand_', 'passage_')):
                source_anchors.append({'name': canonical, 'source_name': source.name,
                                       'parent_node': name_map[clone.parent.name],
                                       'matrix_blender': _rows(root_inverse @ source.matrix_world),
                                       'matrix_godot': _rows(godot_matrix(root_inverse @ source.matrix_world))})
        for source in sources:
            if source.type == 'LIGHT':
                data = source.data
                light_sources.append({'name': source.name, 'type': data.type,
                                      'blender_energy': float(data.energy),
                                      'color_linear': list(data.color),
                                      'size': float(getattr(data, 'size', 0)),
                                      'matrix_blender': _rows(root_inverse @ source.matrix_world),
                                      'imported': False,
                                      'note': 'Create lights in the room rig; watts are not Godot energy multipliers.'})
        for source_mat, clone_mat in material_map.items():
            name_map[clone_mat.name] = source_mat.name
        with context.temp_override(scene=temp_scene, view_layer=temp_scene.view_layers[0]):
            for obj in temp_scene.objects:
                obj.select_set(True)
            context.view_layer.objects.active = root_node
            context.view_layer.update()
            yield {'scene': temp_scene, 'name_map': name_map, 'source_names': source_names,
                   'shadowless_nodes': shadowless, 'hinges': hinges,
                   'source_anchors': source_anchors, 'light_sources': light_sources,
                   'bounds_blender': {'min': list(bounds_min), 'max': list(bounds_max)},
                   'source_objects': len(supported), 'copied_meshes': len(created_meshes)}
    finally:
        for obj in reversed(created_objects):
            if obj.name in bpy.data.objects:
                bpy.data.objects.remove(obj, do_unlink=True)
        for mesh in created_meshes:
            if mesh.name in bpy.data.meshes:
                bpy.data.meshes.remove(mesh)
        for material in created_materials:
            if material.name in bpy.data.materials:
                bpy.data.materials.remove(material)
        if temp_scene.name in bpy.data.scenes:
            bpy.data.scenes.remove(temp_scene)
        # temp_override restores the context. Restore selection defensively;
        # glTF export is allowed to touch selection even in another scene.
        if context.scene == original_scene:
            for obj in context.selected_objects:
                obj.select_set(False)
            for obj in selected:
                if obj.name in context.view_layer.objects:
                    obj.select_set(True)
            if active and active.name in context.view_layer.objects:
                context.view_layer.objects.active = active


def _read_glb(path):
    raw = Path(path).read_bytes()
    magic, version, length = struct.unpack_from('<4sII', raw, 0)
    if magic != b'glTF' or version != 2 or length != len(raw):
        raise ValueError('Invalid glTF 2.0 binary: %s' % path)
    chunks, cursor = [], 12
    while cursor < length:
        size, kind = struct.unpack_from('<I4s', raw, cursor)
        cursor += 8
        chunks.append((kind, raw[cursor:cursor + size]))
        cursor += size
    document = json.loads(next(data for kind, data in chunks if kind == b'JSON'))
    return document, chunks


def _finish_glb(path, name_map):
    document, chunks = _read_glb(path)
    for key in ('nodes', 'materials', 'meshes'):
        for item in document.get(key, []):
            if item.get('name') in name_map:
                item['name'] = name_map[item['name']]
    for scene in document.get('scenes', []):
        scene['name'] = 'AssetScene'
    payload = _json(document).encode('utf8')
    payload += b' ' * (-len(payload) % 4)
    rewritten = [(b'JSON', payload)] + [(kind, data) for kind, data in chunks if kind != b'JSON']
    body = b''.join(struct.pack('<I4s', len(data), kind) + data for kind, data in rewritten)
    Path(path).write_bytes(struct.pack('<4sII', b'glTF', 2, 12 + len(body)) + body)
    triangles = 0
    primitives = 0
    for mesh in document.get('meshes', []):
        for primitive in mesh.get('primitives', []):
            primitives += 1
            if primitive.get('mode', 4) != 4:
                continue
            index = primitive.get('indices', primitive.get('attributes', {}).get('POSITION'))
            triangles += document['accessors'][index]['count'] // 3
    materials = document.get('materials', [])
    if any(m.get('doubleSided', False) for m in materials):
        raise ValueError('Material unexpectedly disables backface culling.')
    if not materials:
        raise ValueError('Exported GLB has no materials.')
    return {'nodes': len(document.get('nodes', [])), 'meshes': len(document.get('meshes', [])),
            'primitives': primitives, 'triangles': triangles, 'materials': len(materials),
            'bytes': Path(path).stat().st_size,
            'sha256': hashlib.sha256(Path(path).read_bytes()).hexdigest(),
            'backface_culling': True,
            'material_colors': {m.get('name', str(i)):
                                m.get('pbrMetallicRoughness', {}).get('baseColorFactor', [1, 1, 1, 1])
                                for i, m in enumerate(materials)}}


def _export_glb(path, roots, prototype=None):
    with _temporary_asset(roots, prototype) as info:
        options = dict(filepath=str(path), export_format='GLB', use_selection=True,
                       use_visible=False, use_renderable=False, use_active_collection=False,
                       use_active_scene=True, export_yup=True, export_apply=False,
                       export_materials='EXPORT', export_extras=False,
                       export_animations=False, export_lights=False, export_cameras=False,
                       export_texcoords=False, export_normals=True,
                       export_tangents=False, export_skins=False,
                       export_draco_mesh_compression_enable=False)
        available = {p.identifier for p in bpy.ops.export_scene.gltf.get_rna_type().properties}
        options = {key: value for key, value in options.items() if key in available}
        result = bpy.ops.export_scene.gltf(**options)
        if 'FINISHED' not in result:
            raise RuntimeError('glTF export failed: %s' % result)
        info = {key: value for key, value in info.items() if key != 'scene'}
        info['stats'] = _finish_glb(path, info.pop('name_map'))
    return info


def _marker(name, position, *, rotation_z=0, pose='stand', clearance=.3):
    matrix = Matrix.Translation(Vector(position)) @ Matrix.Rotation(rotation_z, 4, 'Z')
    return {'name': _node_name(name), 'matrix_blender': _rows(matrix),
            'matrix_godot': _rows(godot_matrix(matrix)), 'pose': pose,
            'clearance_radius_m': float(clearance), 'availability': 'host_validation_required'}


def _interaction_markers(root, bounds, kind, interactive):
    lo, hi = Vector(bounds['min']), Vector(bounds['max'])
    center = (lo + hi) * .5
    markers = []
    if interactive:
        # Match the current project's InteractionSlots Front/Back convention.
        markers += [_marker('Front', (center.x, lo.y - .45, 0), rotation_z=math.pi),
                    _marker('Back', (center.x, hi.y + .45, 0)),
                    _marker('ContactTarget', (center.x, center.y, min(hi.z, 1.15)),
                            pose='contact', clearance=0)]
        if kind in ('chair', 'bench'):
            height = float(root.get('seat_height', .44))
            markers.append(_marker('Seat', (0, 0, height), pose='sit'))
        elif kind in ('table', 'reception_desk'):
            height = float(root.get('height', hi.z))
            markers.append(_marker('Surface', (0, 0, height), pose='surface', clearance=0))
    explicit = _props(root, 'component_anchors_json', {})
    by_name = {m['name']: m for m in markers}
    for name, value in explicit.items():
        values = value if isinstance(value, dict) else {'position': value}
        by_name[_node_name(name)] = _marker(name, values.get('position', (0, 0, 0)),
                                           rotation_z=float(values.get('rotation_z', 0)),
                                           pose=values.get('pose', 'stand'),
                                           clearance=values.get('clearance_radius_m', .3))
    return list(by_name.values())


def _collision_descriptors(root, source_names):
    result = []
    for i, box in enumerate(_props(root, 'collision_boxes_json', [])):
        size = [float(v) for v in box['size']]
        if len(size) != 3 or min(size) <= 0:
            raise ValueError('Collision box needs three positive size dimensions.')
        center = Vector(box.get('center', (0, 0, 0)))
        parent = box.get('parent_node', '')
        if parent and parent not in source_names:
            raise ValueError('Unknown collision parent_node: ' + parent)
        result.append({'name': _node_name(box.get('name', 'Shape_%02d' % i)),
                       'position': list(BLENDER_TO_GODOT.to_3x3() @ center),
                       'size': [size[0], size[2], size[1]],
                       'parent_node': source_names.get(parent, '')})
    return result


_PREFAB_SCRIPT = '''# Generated visual candidate. This file belongs only to this prefab scene.
extends Node3D

signal interaction_requested(entity_id: String, action_id: String, slot_id: String)

@export var asset_id: String = ""
@export var instance_id: String = ""
@export var entity_id: String = ""
@export var portal_id: String = ""
@export var component_kind: String = ""
@export var interactive: bool = false
@export var shadowless_nodes: PackedStringArray = PackedStringArray()
@export var hinge_names: PackedStringArray = PackedStringArray()
@export var hinge_open_degrees: PackedFloat32Array = PackedFloat32Array()
@export var collision_boxes_json: String = "[]"
@export_enum("none", "trimesh") var static_collision: String = "none"

var _hinges: Array[Node3D] = []
var _rest_bases: Array[Basis] = []
var _collision_shapes: Array[CollisionShape3D] = []
var _initialized: bool = false

func _ready() -> void:
    _initialize_visuals()

func _initialize_visuals() -> void:
    if _initialized:
        return
    var visual := get_node_or_null("Visual")
    if visual == null:
        return
    _initialized = true
    for node_name in shadowless_nodes:
        var mesh := visual.find_child(node_name, true, false) as GeometryInstance3D
        if mesh != null:
            mesh.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
    for node_name in hinge_names:
        var hinge := visual.find_child(node_name, true, false) as Node3D
        _hinges.append(hinge)
        _rest_bases.append(hinge.basis if hinge != null else Basis.IDENTITY)
    # The project's importer skips furniture from static batching. Preserve
    # movable hinge/handle hierarchy even if an upstream scene walks meshes.
    if interactive:
        for mesh in visual.find_children("*", "MeshInstance3D", true, false):
            mesh.set_meta("kind", "furniture")
    var boxes: Variant = JSON.parse_string(collision_boxes_json)
    if boxes is Array:
        for value: Variant in boxes:
            if not value is Dictionary:
                continue
            var spec: Dictionary = value
            var parent_node: Node3D = self
            var requested_parent: String = str(spec.get("parent_node", ""))
            if not requested_parent.is_empty() and requested_parent != "Asset":
                parent_node = visual.find_child(requested_parent, true, false) as Node3D
            if parent_node == null:
                continue
            var body := StaticBody3D.new()
            body.name = "Solid_" + str(spec.get("name", "Shape"))
            body.collision_layer = 1
            body.collision_mask = 0
            parent_node.add_child(body)
            var shape := CollisionShape3D.new()
            shape.name = "Shape"
            var box_shape := BoxShape3D.new()
            var dimensions: Array = spec["size"]
            box_shape.size = Vector3(float(dimensions[0]), float(dimensions[1]), float(dimensions[2]))
            shape.shape = box_shape
            var center: Array = spec["position"]
            shape.position = Vector3(float(center[0]), float(center[1]), float(center[2]))
            body.add_child(shape)
            _collision_shapes.append(shape)
    if static_collision == "trimesh":
        _build_static_collision(visual)

func _build_static_collision(visual: Node) -> void:
    # One exact concave collider for evaluated architecture. Openings, stair
    # treads and the void below the gallery remain open; no giant bounding box.
    var faces := PackedVector3Array()
    for child in visual.find_children("*", "MeshInstance3D", true, false):
        var mesh_instance := child as MeshInstance3D
        if mesh_instance.mesh == null or shadowless_nodes.has(str(mesh_instance.name)):
            continue
        var source_faces: PackedVector3Array = mesh_instance.mesh.get_faces()
        var relative: Transform3D = _local_transform_to_root(mesh_instance)
        faces.append_array(relative * source_faces)
    if faces.is_empty():
        return
    var body := StaticBody3D.new()
    body.name = "Solid"
    body.collision_layer = 1
    body.collision_mask = 0
    add_child(body)
    var shape := CollisionShape3D.new()
    shape.name = "ArchitectureShape"
    var concave := ConcavePolygonShape3D.new()
    concave.set_faces(faces)
    shape.shape = concave
    body.add_child(shape)
    _collision_shapes.append(shape)

func _local_transform_to_root(descendant: Node3D) -> Transform3D:
    # Native authoring previews can prepare scenes outside the SceneTree.
    # Multiply only the descendant-to-self chain, excluding self's transform
    # and every outer host transform. This is valid both on and off the tree.
    var relative := Transform3D.IDENTITY
    var cursor: Node = descendant
    while cursor != null and cursor != self:
        if cursor is Node3D:
            relative = (cursor as Node3D).transform * relative
        cursor = cursor.get_parent()
    return relative

func request_interaction(action_id: String, slot_id: String = "Front") -> void:
    # IDs must come from the current Core projection/spatial.options.
    # Emitting this signal neither grants permission nor changes world state.
    if interactive and not entity_id.is_empty() and not action_id.is_empty():
        interaction_requested.emit(entity_id, action_id, slot_id)

func get_interaction_slot(slot_id: String) -> Marker3D:
    return get_node_or_null("InteractionSlots/" + slot_id) as Marker3D

func apply_visual_open_fraction(fraction: float) -> void:
    # Call only after an authoritative host projection; no portal traversal.
    _initialize_visuals()
    var amount := clampf(fraction, 0.0, 1.0)
    for index in _hinges.size():
        var hinge := _hinges[index]
        if hinge != null and index < hinge_open_degrees.size():
            hinge.basis = _rest_bases[index] * Basis(Vector3.UP, deg_to_rad(hinge_open_degrees[index]) * amount)

func set_projected_collision_enabled(enabled: bool) -> void:
    # The host decides when a projected portal becomes physically passable.
    _initialize_visuals()
    for shape in _collision_shapes:
        shape.set_deferred("disabled", not enabled)
'''


def _write_prefab(output, res_base, asset):
    asset_id = asset['asset_id']
    folder = output / 'prefabs'
    scene_path = folder / (asset_id + '.tscn')
    script_path = folder / (asset_id + '.gd')
    script_path.write_text(_PREFAB_SCRIPT, encoding='utf8')
    names = ', '.join(_string(n) for n in asset['shadowless_nodes'])
    hinges = ', '.join(_string(h['name']) for h in asset['hinges'])
    angles = ', '.join(_num(h['open_angle_degrees']) for h in asset['hinges'])
    category = 'furniture' if asset['interactive'] else 'decoration'
    lines = ['[gd_scene load_steps=3 format=3]', '',
             '[ext_resource type="Script" path=%s id="1_script"]' %
             _string(res_base + '/prefabs/' + asset_id + '.gd'),
             '[ext_resource type="PackedScene" path=%s id="2_model"]' %
             _string(res_base + '/models/' + asset_id + '.glb'), '',
             '[node name=%s type="Node3D"]' % _string(_node_name(asset_id)),
             'script = ExtResource("1_script")',
             'asset_id = ' + _string(asset_id),
             'component_kind = ' + _string(asset['kind']),
             'interactive = ' + str(asset['interactive']).lower(),
             'shadowless_nodes = PackedStringArray(%s)' % names,
             'hinge_names = PackedStringArray(%s)' % hinges,
             'hinge_open_degrees = PackedFloat32Array(%s)' % angles,
             'collision_boxes_json = ' + _string(_json(asset['collision_boxes'])),
             'static_collision = ' + _string(asset.get('static_collision', 'none')),
             'metadata/world_category = ' + _string(category),
             'metadata/core_binding = "pending"',
             'metadata/front_axis = "+Z"',
             'metadata/static_unit_baked = true',
             'metadata/asset_parameters_json = ' + _string(_json(asset['parameters'])), '',
             '[node name="Visual" parent="." instance=ExtResource("2_model")]', '',
             '[node name="InteractionSlots" type="Node3D" parent="."]']
    for marker in asset['interaction_markers']:
        lines += ['', '[node name=%s type="Marker3D" parent="InteractionSlots"]' %
                  _string(marker['name']),
                  'transform = ' + _transform_text(marker['matrix_godot']),
                  'metadata/pose = ' + _string(marker['pose']),
                  'metadata/clearance_radius_m = ' + _num(marker['clearance_radius_m']),
                  'metadata/availability = "host_validation_required"']
    scene_path.write_text('\n'.join(lines) + '\n', encoding='utf8')
    return str(scene_path), str(script_path)


def _write_room(output, res_base, room_id, manifest):
    assets = manifest['assets']
    ext = {a['asset_id']: str(i + 2) + '_prefab' for i, a in enumerate(assets)}
    lines = ['[gd_scene load_steps=%d format=3]' % (2 + len(assets)), '',
             '[ext_resource type="Script" path=%s id="1_script"]' %
             _string(res_base + '/' + room_id + '.gd')]
    for asset in assets:
        lines.append('[ext_resource type="PackedScene" path=%s id=%s]' %
                     (_string(res_base + '/prefabs/' + asset['asset_id'] + '.tscn'),
                      _string(ext[asset['asset_id']])) )
    lines += ['', '[node name=%s type="Node3D"]' % _string(_node_name(room_id)),
              'script = ExtResource("1_script")',
              'metadata/room_id = ' + _string(room_id),
              'metadata/static_unit_baked = true',
              'metadata/candidate_only = true',
              'metadata/core_binding = "pending"',
              'metadata/manifest = ' + _string(res_base + '/manifest.json'),
              '', '[node name="Instances" type="Node3D" parent="."]']
    for instance in manifest['instances']:
        lines += ['', '[node name=%s parent="Instances" instance=ExtResource(%s)]' %
                  (_string(_node_name(instance['instance_id'])), _string(ext[instance['asset_id']])),
                  'transform = ' + _transform_text(instance['matrix_godot']),
                  'instance_id = ' + _string(instance['instance_id']),
                  'entity_id = ' + _string(instance['entity_id']),
                  'portal_id = ' + _string(instance['portal_id'])]
        if not instance.get('preview_visible', True):
            lines.append('visible = false')
    scene_path = output / (room_id + '.tscn')
    scene_path.write_text('\n'.join(lines) + '\n', encoding='utf8')
    script_path = output / (room_id + '.gd')
    script_path.write_text('''# Assembled visual candidate; no authoritative gameplay state lives here.
extends Node3D

func find_component(instance_id: String) -> Node3D:
    for component in $Instances.get_children():
        if component.get("instance_id") == instance_id:
            return component as Node3D
    return null
''', encoding='utf8')
    return str(scene_path), str(script_path)


def export_room_bundle(output_dir, *, components, static_objects=(),
                       res_base='res://tools/building/candidates/lowpoly_hall',
                       room_id='main_hall', metadata=None, static_collision='trimesh'):
    """Export reusable GLBs + dedicated prefab scripts + assembled candidate.

    Returns {manifest, paths, exportstats}. Use the SAME output folder as the
    destination res_base inside a Godot project. No project.godot/Main changes
    occur. Original objects/materials/transforms and selection survive export.
    Rendering lights/cameras are excluded; the parent room owns its light rig.
    """
    room_id = _identifier(room_id, 'room_id')
    if static_collision not in ('none', 'trimesh'):
        raise ValueError("static_collision must be 'none' or 'trimesh'.")
    res_base = str(res_base).rstrip('/')
    if not res_base.startswith('res://') or '..' in res_base.split('/'):
        raise ValueError('res_base must be an in-project res:// folder.')
    output = Path(output_dir).expanduser().resolve()
    output.mkdir(parents=True, exist_ok=True)
    (output / 'models').mkdir(exist_ok=True)
    (output / 'prefabs').mkdir(exist_ok=True)
    bpy.context.view_layer.update()
    components = list(components)
    if len(set(components)) != len(components):
        raise ValueError('A component root appears more than once.')
    selected_roots = set(components)
    for root in components:
        ancestor = root.parent
        while ancestor:
            if ancestor in selected_roots:
                raise ValueError('Component roots may not be nested: ' + root.name)
            ancestor = ancestor.parent
    static_roots = _root_list(static_objects)
    component_objects = set(_walk(components, include_hidden=True))
    if component_objects.intersection(_walk(static_roots)):
        raise ValueError('Static geometry and reusable components overlap.')
    manifest = {'schema_version': SCHEMA_VERSION, 'room_id': room_id,
                'status': 'candidate_unbound', 'blender_version': bpy.app.version_string,
                'coordinate_contract': {'blender': 'X right, Y back, Z up; front -Y',
                                        'godot': 'X right, Y up, Z front; prefab front +Z',
                                        'point_conversion': '(x,y,z) -> (x,z,-y)',
                                        'node_conversion': 'C * M_blender * inverse(C)',
                                        'yaw_conversion': 'Blender +Z -> Godot +Y, same sign',
                                        'matrix_storage': 'row-major 4x4; tscn uses basis columns',
                                        'geometry_conversion_count': 1},
                'metadata': metadata or {}, 'assets': [], 'instances': [],
                'limitations': ['Candidate visual assets are not automatically bound to Core.',
                                'Furniture uses explicit collision boxes; static architecture can use a concave trimesh.',
                                'Lighting is owned by the room rig, not embedded in GLBs.',
                                'Opaque stylized panes are visible but cast no Godot shadows.',
                                'No room switching, occupancy or action IDs are invented.']}
    by_asset, instance_ids, paths = {}, set(), []
    for root in components:
        asset_id = _identifier(root.get('asset_id', ''), 'asset_id on ' + root.name)
        instance_id = _identifier(root.get('instance_id', _node_name(root.name)), 'instance_id')
        if instance_id in instance_ids:
            raise ValueError('Duplicate instance_id: ' + instance_id)
        instance_ids.add(instance_id)
        kind = str(root.get('component_kind', root.get('asset_type', root.get('kind', 'component'))))
        parameters = _props(root, 'asset_parameters_json', {})
        interactive = bool(root.get('interactive', False))
        if asset_id in by_asset:
            existing = by_asset[asset_id]
            if (existing['parameters'] != parameters or existing['kind'] != kind or
                    existing['interactive'] != interactive):
                raise ValueError('asset_id reused for differing kind/parameters: ' + asset_id)
        else:
            model = output / 'models' / (asset_id + '.glb')
            info = _export_glb(model, [root], root)
            asset = {'asset_id': asset_id, 'kind': kind, 'parameters': parameters,
                     'interactive': interactive, 'model': 'models/' + asset_id + '.glb',
                     'prefab': 'prefabs/' + asset_id + '.tscn',
                     'prototype_source': root.name, **info}
            asset['interaction_markers'] = _interaction_markers(root, info['bounds_blender'], kind, interactive)
            asset['collision_boxes'] = _collision_descriptors(root, info['source_names'])
            asset['collision_status'] = 'explicit_boxes' if asset['collision_boxes'] else 'not_supplied'
            by_asset[asset_id] = asset
            manifest['assets'].append(asset)
            paths.extend([str(model), *_write_prefab(output, res_base, asset)])
        manifest['instances'].append({'instance_id': instance_id, 'asset_id': asset_id,
                                      'source_object': root.name, 'kind': kind,
                                      'entity_id': str(root.get('entity_id', '')),
                                      'portal_id': str(root.get('portal_id', '')),
                                      'preview_visible': bool(root.get('preview_visible', True)),
                                      'matrix_blender': _rows(root.matrix_world),
                                      'matrix_godot': _rows(godot_matrix(root.matrix_world))})
    if static_roots:
        asset_id = room_id + '_architecture'
        if asset_id in by_asset or asset_id in instance_ids:
            raise ValueError('Reserved architecture identifier is already used: ' + asset_id)
        model = output / 'models' / (asset_id + '.glb')
        info = _export_glb(model, static_roots)
        asset = {'asset_id': asset_id, 'kind': 'architecture', 'parameters': {},
                 'interactive': False, 'model': 'models/' + asset_id + '.glb',
                 'prefab': 'prefabs/' + asset_id + '.tscn', **info,
                 'interaction_markers': [], 'collision_boxes': [],
                 'static_collision': static_collision,
                 'collision_status': 'evaluated_trimesh' if static_collision == 'trimesh'
                                     else 'host_architecture_required'}
        manifest['assets'].insert(0, asset)
        manifest['instances'].insert(0, {'instance_id': asset_id, 'asset_id': asset_id,
                                        'source_object': '', 'kind': 'architecture',
                                        'entity_id': '', 'portal_id': '',
                                        'preview_visible': True,
                                        'matrix_blender': _rows(Matrix.Identity(4)),
                                        'matrix_godot': _rows(Matrix.Identity(4))})
        paths.extend([str(model), *_write_prefab(output, res_base, asset)])
    scene_path, script_path = _write_room(output, res_base, room_id, manifest)
    paths.extend([scene_path, script_path])
    stats = {'unique_assets': len(manifest['assets']), 'placed_instances': len(manifest['instances']),
             'unique_triangles': sum(a['stats']['triangles'] for a in manifest['assets']),
             'glb_bytes': sum(a['stats']['bytes'] for a in manifest['assets']),
             'interactive_instances': sum(1 for i in manifest['instances']
                                          if by_asset.get(i['asset_id'], {}).get('interactive'))}
    triangle_by_asset = {a['asset_id']: a['stats']['triangles'] for a in manifest['assets']}
    stats['placed_triangles'] = sum(triangle_by_asset[i['asset_id']] for i in manifest['instances'])
    manifest['exportstats'] = stats
    manifest['assembled_scene'] = room_id + '.tscn'
    manifest_path = output / 'manifest.json'
    manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False, allow_nan=False) + '\n', encoding='utf8')
    paths.append(str(manifest_path))
    return {'manifest': manifest, 'paths': {'root': str(output), 'scene': scene_path,
                                         'script': script_path, 'manifest': str(manifest_path),
                                         'files': paths}, 'exportstats': stats}
