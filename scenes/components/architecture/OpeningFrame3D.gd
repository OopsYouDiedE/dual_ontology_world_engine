@tool
extends Node3D
## Opening plane is local XY; its lower-left corner is local origin.
## Width and height include frame members; the clear width is width - 2*frame.
## Flat threshold is guaranteed when sill=false for seamless walking (Task 2.13, 2.16).
const PhysicsLayers = preload("res://scenes/components/world/PhysicsLayers.gd")

@export_range(0.2, 20.0, 0.05) var frame_width: float = 1.2:
	set(v):
		frame_width = v
		if Engine.is_editor_hint() and is_inside_tree():
			rebuild_frame()

@export_range(0.2, 20.0, 0.05) var frame_height: float = 2.2:
	set(v):
		frame_height = v
		if Engine.is_editor_hint() and is_inside_tree():
			rebuild_frame()

@export_range(0.01, 2.0, 0.01) var depth: float = 0.2:
	set(v):
		depth = v
		if Engine.is_editor_hint() and is_inside_tree():
			rebuild_frame()

@export_range(0.01, 1.0, 0.01) var frame_thickness: float = 0.1:
	set(v):
		frame_thickness = v
		if Engine.is_editor_hint() and is_inside_tree():
			rebuild_frame()

@export var has_sill: bool = false:
	set(v):
		has_sill = v
		if Engine.is_editor_hint() and is_inside_tree():
			rebuild_frame()

@export var frame_material: Material:
	set(v):
		frame_material = v
		if Engine.is_editor_hint() and is_inside_tree():
			rebuild_frame()

@export var glass_material: Material:
	set(v):
		glass_material = v
		if Engine.is_editor_hint() and is_inside_tree():
			rebuild_frame()

@export_range(0.0, 0.5, 0.005) var glass_thickness: float = 0.0:
	set(v):
		glass_thickness = v
		if Engine.is_editor_hint() and is_inside_tree():
			rebuild_frame()

func _ready() -> void:
	if Engine.is_editor_hint():
		rebuild_frame()

func rebuild_frame() -> void:
	if frame_width <= 2.0 * frame_thickness or frame_height <= 2.0 * frame_thickness or frame_thickness <= 0.0 or depth <= 0.0:
		return
	if glass_material != null and glass_thickness <= 0.0:
		glass_thickness = 0.02

	var old_visuals = get_node_or_null("VisualRoot")
	if old_visuals != null:
		old_visuals.name = "OldVisuals"
		old_visuals.queue_free()

	var old_body = get_node_or_null("CollisionRoot")
	if old_body != null:
		old_body.name = "OldCollision"
		old_body.queue_free()

	var visuals := Node3D.new()
	visuals.name = "VisualRoot"
	add_child(visuals)

	var body := StaticBody3D.new()
	body.name = "CollisionRoot"
	body.collision_layer = PhysicsLayers.SOLID
	body.collision_mask = 0
	add_child(body)

	# Left, right and top members deduct opening clear area (Task 2.13)
	_part(visuals, body, "Left", Vector3(frame_thickness / 2.0, frame_height / 2.0, 0.0), Vector3(frame_thickness, frame_height, depth), frame_material)
	_part(visuals, body, "Right", Vector3(frame_width - frame_thickness / 2.0, frame_height / 2.0, 0.0), Vector3(frame_thickness, frame_height, depth), frame_material)
	_part(visuals, body, "Top", Vector3(frame_width / 2.0, frame_height - frame_thickness / 2.0, 0.0), Vector3(frame_width - 2.0 * frame_thickness, frame_thickness, depth), frame_material)

	if has_sill:
		# Raised sill for windows or exterior barriers (Task 2.16)
		_part(visuals, body, "Sill", Vector3(frame_width / 2.0, frame_thickness / 2.0, 0.0), Vector3(frame_width - 2.0 * frame_thickness, frame_thickness, depth), frame_material)

	if glass_material != null and glass_thickness > 0.0:
		var bottom := frame_thickness if has_sill else 0.0
		_part(visuals, body, "Glass", Vector3(frame_width / 2.0, (frame_height - frame_thickness + bottom) / 2.0, 0.0), Vector3(frame_width - 2.0 * frame_thickness, frame_height - frame_thickness - bottom, glass_thickness), glass_material)

	if not has_sill and glass_material == null:
		for side in ["Left", "Right"]:
			var shape: CollisionShape3D = body.get_node_or_null(side)
			if shape != null and shape.shape is BoxShape3D:
				var inset := minf(frame_thickness * 0.4, 0.06)
				var jamb: BoxShape3D = shape.shape
				jamb.size.x = maxf(0.01, frame_thickness - inset)
				shape.position.x += -inset * 0.5 if side == "Left" else inset * 0.5

static func create(id: String, width: float, height: float, p_depth: float, frame: float, p_frame_material: Material, p_glass_material: Material = null, p_glass_thickness: float = 0.0, sill: bool = false) -> Node3D:
	if id.is_empty() or not is_finite(width) or not is_finite(height) or not is_finite(p_depth) or not is_finite(frame) or not is_finite(p_glass_thickness) or width <= 2.0 * frame or height <= 2.0 * frame or frame <= 0.0 or p_depth <= 0.0 or p_glass_thickness < 0.0 or p_glass_thickness > p_depth or (p_glass_material != null and p_glass_thickness <= 0.0):
		push_error("OpeningFrame3D " + id + ": invalid-parameters")
		return null
	var scene = load("res://scenes/components/architecture/OpeningFrame3D.tscn")
	var root: Node3D = scene.instantiate()
	root.name = "WorldObject3D"
	root.set_meta("world_object_id", id)
	root.set_meta("kind", "door_frame" if not sill and p_glass_material == null else "window_frame")
	root.set_meta("clear_width", width - 2.0 * frame)
	root.set_meta("clear_height", height - (2.0 * frame if sill else frame))
	root.set_meta("frame_thickness", frame)
	root.set_meta("depth", p_depth)
	root.set_meta("has_sill", sill)

	root.frame_width = width
	root.frame_height = height
	root.depth = p_depth
	root.frame_thickness = frame
	root.has_sill = sill
	root.frame_material = p_frame_material
	root.glass_material = p_glass_material
	root.glass_thickness = p_glass_thickness
	root.rebuild_frame()
	return root

static func _part(visuals: Node3D, body: StaticBody3D, part_name: String, position: Vector3, size: Vector3, material: Material) -> void:
	var visible := MeshInstance3D.new()
	visible.name = part_name
	var mesh := BoxMesh.new()
	mesh.size = size
	visible.mesh = mesh
	visible.position = position
	visible.material_override = material
	visuals.add_child(visible)
	var collision := CollisionShape3D.new()
	collision.name = part_name
	var box := BoxShape3D.new()
	box.size = size
	collision.shape = box
	collision.position = position
	body.add_child(collision)
