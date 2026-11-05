# Generated visual candidate. This file belongs only to this prefab scene.
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
