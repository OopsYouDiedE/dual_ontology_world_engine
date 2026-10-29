extends Node3D
const PhysicsLayers = preload("res://geometry/PhysicsLayers.gd")
## Presentation only. The host must pass a C#-validated declaration.
## All coordinates supplied to this node are local Godot metres.

func build(spec: Dictionary, visual: Node3D) -> bool:
	if not spec.has_all(["id", "name", "interaction", "collision"]) or not spec.interaction is Dictionary or not spec.collision is bool:
		push_error("WorldObject3D: incomplete declaration")
		return false
	var interaction: Dictionary = spec.interaction
	if interaction.get("status", "") != "ready" or not interaction.has_all(["collision_shapes", "area_size", "area_center", "approach", "stand", "face_target", "contact", "label_position", "actions", "resources", "state_visuals"]):
		push_error("WorldObject3D: incomplete interaction")
		return false
	if str(spec.id).is_empty() or str(spec.name).is_empty() or not is_instance_valid(visual):
		push_error("WorldObject3D: invalid identity, name or visual")
		return false
	for key in ["actions", "resources", "state_visuals"]:
		if not interaction[key] is Array:
			push_error("WorldObject3D: invalid " + key)
			return false
	if interaction.actions.is_empty() or not interaction.collision_shapes is Array:
		push_error("WorldObject3D: invalid actions or collision shapes")
		return false
	if spec.collision and interaction.collision_shapes.is_empty():
		push_error("WorldObject3D: missing collision shapes")
		return false
	if not spec.collision and not interaction.collision_shapes.is_empty():
		push_error("WorldObject3D: collision disabled with shapes")
		return false
	for shape in interaction.collision_shapes:
		if not shape is Dictionary or not shape.has_all(["kind", "size", "center"]) or shape.kind != "box" or not _valid_size(shape.size) or not _valid_vector(shape.center):
			push_error("WorldObject3D: invalid collision shape")
			return false
	if not _valid_size(interaction.area_size):
		push_error("WorldObject3D: invalid area size")
		return false
	for key in ["area_center", "label_position", "approach", "stand", "face_target", "contact"]:
		if not _valid_vector(interaction[key]):
			push_error("WorldObject3D: invalid " + key)
			return false
	if get_child_count() != 0 or visual.get_parent() != null:
		push_error("WorldObject3D: already built or visual is owned")
		return false
	name = "WorldObject3D"
	set_meta("world_object_id", str(spec.id))
	var visual_root := Node3D.new()
	visual_root.name = "VisualRoot"
	add_child(visual_root)
	visual_root.add_child(visual)
	var collision_root := Node3D.new()
	collision_root.name = "CollisionRoot"
	add_child(collision_root)
	if spec.collision:
		var body := StaticBody3D.new()
		body.name = "StaticBody3D"
		body.collision_layer = PhysicsLayers.SOLID
		body.collision_mask = 0
		collision_root.add_child(body)
		for shape in interaction.collision_shapes: body.add_child(_box_shape(shape.size, shape.center))
	var area := Area3D.new()
	area.name = "InteractionArea"
	area.collision_layer = PhysicsLayers.INTERACTION
	area.collision_mask = PhysicsLayers.ACTOR
	add_child(area)
	area.add_child(_box_shape(interaction.area_size, interaction.area_center))
	var slots := Node3D.new()
	slots.name = "InteractionSlots"
	add_child(slots)
	for pair in [["approach", "Approach"], ["stand", "Stand"], ["face_target", "FaceTarget"], ["contact", "Contact"]]:
		var marker := Marker3D.new()
		marker.name = pair[1]
		marker.position = interaction[pair[0]]
		slots.add_child(marker)
	for opt_pair in [["sit", "Sit"], ["lie", "Lie"], ["exit", "Exit"], ["placement", "PlacementSurface"]]:
		if interaction.has(opt_pair[0]):
			var opt_marker := Marker3D.new()
			opt_marker.name = opt_pair[1]
			opt_marker.position = interaction[opt_pair[0]]
			slots.add_child(opt_marker)
	var states := Node3D.new()
	states.name = "StateVisuals"
	add_child(states)
	var label := Label3D.new()
	label.name = "LabelAnchor"
	# The physical definition name is not observer knowledge.
	label.text = ""
	label.position = interaction.label_position
	label.visible = false
	label.no_depth_test = false # Ensure depth test occludes label behind walls (Task 4.09)
	label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	label.outline_size = 4
	label.outline_modulate = Color(0, 0, 0, 0.85)
	add_child(label)
	return true

# Single and multi-slot occupancy tracking (Tasks 3.01, 3.06, 3.09)
var occupied_by: String = ""
var slot_occupancies: Dictionary = {}

func is_occupied() -> bool:
	return not occupied_by.is_empty() or not slot_occupancies.is_empty()

func is_slot_occupied(slot_name: String) -> bool:
	return slot_occupancies.has(slot_name) and not str(slot_occupancies[slot_name]).is_empty()

func occupy(actor_id: String) -> bool:
	if is_occupied() and occupied_by != actor_id:
		return false
	occupied_by = actor_id
	return true

func occupy_slot(slot_name: String, actor_id: String) -> bool:
	if is_slot_occupied(slot_name) and slot_occupancies[slot_name] != actor_id:
		return false
	slot_occupancies[slot_name] = actor_id
	return true

func release_reservation(actor = null) -> void:
	occupied_by = ""
	slot_occupancies.clear()

func release_slot(slot_name: String) -> void:
	slot_occupancies.erase(slot_name)

# Target selection highlighting (Task 4.09)
var is_highlighted := false
var _original_materials: Dictionary = {}

func set_highlight(highlight: bool, color: Color = Color("ffd54f")) -> void:
	is_highlighted = highlight
	var visual_root := get_node_or_null("VisualRoot")
	if visual_root == null: return
	_apply_highlight_recursive(visual_root, highlight, color)

func _apply_highlight_recursive(node: Node, highlight: bool, color: Color) -> void:
	if node is MeshInstance3D and node.mesh != null:
		var key := node.get_instance_id()
		if highlight:
			if not _original_materials.has(key):
				_original_materials[key] = node.material_override
			var mat := StandardMaterial3D.new()
			mat.albedo_color = color
			mat.emission_enabled = true
			mat.emission = color
			mat.emission_energy_multiplier = 0.35
			node.material_override = mat
		else:
			if _original_materials.has(key):
				node.material_override = _original_materials[key]
				_original_materials.erase(key)
	for child in node.get_children():
		_apply_highlight_recursive(child, highlight, color)

## The host supplies a current observer projection from Core; absence or an
## invalid projection clears the label rather than exposing physical content.
func set_observer_label(projection: Dictionary) -> bool:
	var label := get_node_or_null("LabelAnchor") as Label3D
	if label == null: return false
	label.text = ""
	label.visible = false
	if not projection.has_all(["object_id", "display_name", "visible"]) or projection.object_id != get_meta("world_object_id", "") or typeof(projection.visible) != TYPE_BOOL or not projection.display_name is String:
		return false
	if not projection.visible: return true
	if projection.display_name.is_empty(): return false
	label.text = projection.display_name
	label.visible = true
	label.no_depth_test = false
	if bool(projection.get("highlight", false)):
		set_highlight(true)
	else:
		set_highlight(false)
	return true

## Stable slot identifiers are part of the spatial interaction contract.
func resolve_interaction_slot(slot_id: String) -> Marker3D:
	var node_name := ""
	match slot_id:
		"approach": node_name = "Approach"
		"stand": node_name = "Stand"
		"face_target": node_name = "FaceTarget"
		"contact": node_name = "Contact"
		"sit": node_name = "Sit"
		"lie": node_name = "Lie"
		"exit": node_name = "Exit"
		"placement": node_name = "PlacementSurface"
		_: return null
	return get_node_or_null("InteractionSlots/" + node_name) as Marker3D

func _valid_vector(value: Variant) -> bool:
	return value is Vector3 and value.is_finite()

func _valid_size(value: Variant) -> bool:
	return _valid_vector(value) and value.x > 0.0 and value.y > 0.0 and value.z > 0.0

func _box_shape(size: Vector3, center: Vector3) -> CollisionShape3D:
	var shape := BoxShape3D.new()
	shape.size = size
	var node := CollisionShape3D.new()
	node.name = "CollisionShape3D"
	node.shape = shape
	node.position = center
	return node
