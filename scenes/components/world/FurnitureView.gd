@tool
extends Node3D
## Geometry and potential affordances only. Main/Core owns claims, actions and state.
signal interaction_requested(object_id: String, slot_id: String)
const RESOURCE := preload("res://scenes/components/world/FurnitureResource.gd")
const DETAILS := preload("res://scenes/components/world/FurnitureDetailGeometry.gd")
@export var furniture_resource: Resource
@export var object_id := ""
var definition: Dictionary = {}
var state := "normal"
var revision := -1
var occupied_slots: Dictionary = {}
var _generated: Node3D
var _parts: Dictionary = {}
var _debug := false
var _markers: Node3D
var _details = DETAILS.new()

func _ready() -> void:
	if furniture_resource != null and definition.is_empty():
		configure(furniture_resource.definition, object_id)

func configure(value: Dictionary, identity := "") -> bool:
	var resource := RESOURCE.new()
	resource.definition = value.duplicate(true)
	if not resource.valid():
		return false
	definition = resource.definition
	furniture_resource = resource
	object_id = identity if not identity.is_empty() else str(definition.id)
	state = str(definition.default_state)
	revision = -1
	occupied_slots.clear()
	_rebuild()
	return true

func _v3(values: Array) -> Vector3:
	return Vector3(float(values[0]), float(values[1]), float(values[2]))

func _rebuild() -> void:
	_details.configure_style()
	if is_instance_valid(_generated):
		remove_child(_generated)
		_generated.queue_free()
	_parts.clear()
	_generated = Node3D.new()
	_generated.name = "GeneratedFurniture"
	add_child(_generated)
	var visuals := Node3D.new()
	visuals.name = "VisualRoot"
	_generated.add_child(visuals)
	if definition.has("visual_resource"):
		var scene := load(str(definition.visual_resource)) as PackedScene
		if scene != null:
			visuals.add_child(scene.instantiate())
	for part: Dictionary in definition.parts:
		var piece := Node3D.new()
		piece.name = str(part.id).validate_node_name()
		visuals.add_child(piece)
		var mesh := MeshInstance3D.new()
		mesh.name = "Mesh"
		var shape: Shape3D
		var size := _v3(part.size)
		if str(part.kind) == "cylinder":
			var cylinder := CylinderMesh.new()
			cylinder.top_radius = size.x * 0.5
			cylinder.bottom_radius = size.x * 0.5
			cylinder.height = size.y
			cylinder.radial_segments = 20
			mesh.mesh = cylinder
			mesh.scale.z = size.z / size.x
			var collision := CylinderShape3D.new()
			collision.radius = maxf(size.x, size.z) * 0.5
			collision.height = size.y
			shape = collision
		else:
			var cube := BoxMesh.new()
			cube.size = size
			mesh.mesh = cube
			var collision := BoxShape3D.new()
			collision.size = size
			shape = collision
		var material := StandardMaterial3D.new()
		material.albedo_color = Color(str(part.color))
		material.roughness = 0.8
		mesh.material_override = material
		if not definition.has("visual_resource"):
			# Refine only the displayed mesh. Part transforms and collision stay authoritative.
			mesh.name = str(part.id).validate_node_name()
			_details._refine(mesh)
			mesh.name = "Mesh"
		piece.add_child(mesh)
		var body := StaticBody3D.new()
		body.name = "Solid"
		body.collision_layer = 1
		body.collision_mask = 0
		piece.add_child(body)
		var collider := CollisionShape3D.new()
		collider.name = "Shape"
		collider.shape = shape
		body.add_child(collider)
		_parts[str(part.id)] = {"node": piece, "mesh": mesh, "shape": collider, "source": part}
	var slots := Node3D.new()
	slots.name = "InteractionSlots"
	_generated.add_child(slots)
	for slot: Dictionary in definition.interactions:
		var group := Node3D.new()
		group.name = str(slot.id).validate_node_name()
		group.set_meta("slot_id", slot.id)
		group.set_meta("pose", slot.pose)
		group.set_meta("resource_group", slot.group)
		group.set_meta("capacity", slot.capacity)
		slots.add_child(group)
		for key in ["approach", "stand", "contact", "face_target", "exit"]:
			var marker := Marker3D.new()
			marker.name = key
			marker.position = _v3(slot[key])
			group.add_child(marker)
	var surfaces := Node3D.new()
	surfaces.name = "PlacementSurfaces"
	_generated.add_child(surfaces)
	for surface: Dictionary in definition.surfaces:
		var marker := Marker3D.new()
		marker.name = str(surface.id).validate_node_name()
		marker.position = _v3(surface.position)
		marker.set_meta("definition", surface.duplicate(true))
		surfaces.add_child(marker)
		for slot: Dictionary in surface.slots:
			var point := Marker3D.new()
			point.name = str(slot.id).validate_node_name()
			point.position = Vector3(float(slot.point[0]), 0, float(slot.point[1]))
			marker.add_child(point)
	_apply_pose()
	show_authoring_markers(_debug)

func _apply_pose() -> void:
	var changes: Dictionary = definition.states[state].get("parts", {})
	for id: String in _parts:
		var part: Dictionary = _parts[id].source.duplicate(true)
		part.merge(changes.get(id, {}), true)
		_parts[id].node.position = _v3(part.position)
		_parts[id].node.rotation_degrees.y = float(part.get("rotation_degrees", 0))
		_parts[id].mesh.visible = bool(part.visible) and not definition.has("visual_resource")
		_parts[id].shape.set_deferred("disabled", not bool(part.collision))

func apply_projection(value: Dictionary) -> bool:
	if str(value.get("object_id", object_id)) != object_id or not value.has("revision"):
		return false
	var next_revision := int(value.revision)
	var next_state := str(value.get("state", state))
	if next_revision <= revision or not definition.states.has(next_state) or not value.get("occupied_slots", {}) is Dictionary:
		return false
	revision = next_revision
	state = next_state
	occupied_slots = value.get("occupied_slots", {}).duplicate(true)
	_apply_pose()
	return true

func request_interaction(slot_id: String) -> void:
	for slot: Dictionary in definition.interactions:
		if str(slot.id) == slot_id and state in slot.active_states:
			interaction_requested.emit(object_id, slot_id)
			return

func potential_interactions() -> Array:
	var result := []
	for slot: Dictionary in definition.interactions:
		if state not in slot.active_states:
			continue
		var entry := slot.duplicate(true)
		for key in ["approach", "stand", "contact", "face_target", "exit"]:
			entry[key] = to_global(_v3(slot[key]))
		entry["object_id"] = object_id
		entry["availability"] = "requires_core_validation"
		result.append(entry)
	return result

func show_authoring_markers(enabled: bool) -> void:
	_debug = enabled
	if is_instance_valid(_markers):
		_markers.free()
	if not enabled or not is_instance_valid(_generated):
		return
	_markers = Node3D.new()
	_markers.name = "AuthoringMarkers"
	_generated.add_child(_markers)
	for slot: Dictionary in definition.interactions:
		_debug_box(_v3(slot.clearance.position), _v3(slot.clearance.size), Color(0.9, 0.55, 0.2, 0.15))
		_debug_box(_v3(slot.approach) + Vector3(0, 0.04, 0), Vector3(0.16, 0.08, 0.16), Color(0.2, 0.8, 0.7, 0.9))
		_debug_box(_v3(slot.contact), Vector3(0.09, 0.09, 0.09), Color(1, 0.7, 0.2, 0.9))
	for surface: Dictionary in definition.surfaces:
		_debug_box(_v3(surface.position), Vector3(float(surface.size[0]), 0.015, float(surface.size[1])), Color(0.2, 0.6, 1, 0.45))

func _debug_box(at: Vector3, size: Vector3, color: Color) -> void:
	var node := MeshInstance3D.new()
	var mesh := BoxMesh.new()
	mesh.size = size
	node.mesh = mesh
	node.position = at
	var mat := StandardMaterial3D.new()
	mat.albedo_color = color
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	node.material_override = mat
	_markers.add_child(node)
