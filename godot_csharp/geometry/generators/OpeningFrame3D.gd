extends RefCounted
## Opening plane is local XY; its lower-left corner is local origin.
## Width and height include frame members; the clear width is width - 2*frame.
## Flat threshold is guaranteed when sill=false for seamless walking (Task 2.13, 2.16).
const PhysicsLayers = preload("res://geometry/PhysicsLayers.gd")

static func create(id: String, width: float, height: float, depth: float, frame: float, frame_material: Material, glass_material: Material = null, glass_thickness: float = 0.0, sill: bool = false) -> Node3D:
	if id.is_empty() or not is_finite(width) or not is_finite(height) or not is_finite(depth) or not is_finite(frame) or not is_finite(glass_thickness) or width <= 2.0*frame or height <= 2.0*frame or frame <= 0.0 or depth <= 0.0 or glass_thickness < 0.0 or glass_thickness > depth or (glass_material != null and glass_thickness <= 0.0):
		push_error("OpeningFrame3D " + id + ": invalid-parameters")
		return null
	var root := Node3D.new()
	root.name = "WorldObject3D"
	root.set_meta("world_object_id", id)
	root.set_meta("kind", "door_frame" if not sill and glass_material == null else "window_frame")
	root.set_meta("clear_width", width - 2.0 * frame)
	root.set_meta("clear_height", height - (2.0 * frame if sill else frame))
	root.set_meta("frame_thickness", frame)
	root.set_meta("depth", depth)
	root.set_meta("has_sill", sill)
	var visuals := Node3D.new()
	visuals.name = "VisualRoot"
	root.add_child(visuals)
	var body := StaticBody3D.new()
	body.name = "CollisionRoot"
	body.collision_layer = PhysicsLayers.SOLID
	body.collision_mask = 0
	root.add_child(body)
	# Left, right and top members deduct opening clear area (Task 2.13)
	_part(visuals, body, "Left", Vector3(frame / 2.0, height / 2.0, 0.0), Vector3(frame, height, depth), frame_material)
	_part(visuals, body, "Right", Vector3(width - frame / 2.0, height / 2.0, 0.0), Vector3(frame, height, depth), frame_material)
	_part(visuals, body, "Top", Vector3(width / 2.0, height - frame / 2.0, 0.0), Vector3(width - 2.0 * frame, frame, depth), frame_material)
	if sill:
		# Raised sill for windows or exterior barriers (Task 2.16)
		_part(visuals, body, "Sill", Vector3(width / 2.0, frame / 2.0, 0.0), Vector3(width - 2.0 * frame, frame, depth), frame_material)
	if glass_material != null:
		var bottom := frame if sill else 0.0
		_part(visuals, body, "Glass", Vector3(width / 2.0, (height - frame + bottom) / 2.0, 0.0), Vector3(width - 2.0 * frame, height - frame - bottom, glass_thickness), glass_material)
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

