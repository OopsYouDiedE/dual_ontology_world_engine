@tool
extends "res://scenes/components/architecture/BuildingDoor.gd"
## Generated door assembly. Only accepted projections change gameplay presentation.
var door_type := "single"
var door_width := 1.0
var leaves: Array[Node3D] = []
var _leaf_origins: Array[Vector3] = []
var _leaf_signs: Array[float] = []
var _shapes: Array[CollisionShape3D] = []

func configure(item: Dictionary, materials: Dictionary) -> void:
	portal_id = str(item.id)
	door_type = str(item.get("door_type", "single"))
	door_width = float(item.width)
	opening_degrees = float(item.get("opening_degrees", 95.0))
	set_meta("portal_id", portal_id)
	var material: Material = materials.get(str(item.get("leaf_material", "glass" if door_type == "glass" else "door")))
	var count := 2 if door_type == "double" else 1
	# Physical reveals: the leaf must not occupy the jamb/head members.
	var frame_reveal := 0.04
	var usable_width := maxf(0.1,door_width-frame_reveal*2.0)
	for i in count:
		var leaf := Node3D.new()
		leaf.name = "Leaf" if i == 0 else "LeafRight"
		leaf.position.x = door_width-frame_reveal if i == 1 else frame_reveal
		add_child(leaf)
		leaves.append(leaf)
		_leaf_origins.append(leaf.position)
		_leaf_signs.append(-1.0 if i == 0 else 1.0)
		var width := usable_width / count
		var center := Vector3(width * (0.5 if i == 0 else -0.5), float(item.height) * 0.5, 0)
		var mesh := MeshInstance3D.new()
		mesh.name = "Mesh"
		var cube := BoxMesh.new()
		cube.size = Vector3(width - 0.012, maxf(0.1,float(item.height)-0.012), 0.065)
		mesh.mesh = cube
		mesh.position = center
		mesh.material_override = material
		leaf.add_child(mesh)
		var body := StaticBody3D.new()
		body.name = "Body"
		body.collision_layer = 1
		body.collision_mask = 0
		leaf.add_child(body)
		var shape := CollisionShape3D.new()
		shape.name = "Shape"
		var box := BoxShape3D.new()
		box.size = cube.size
		shape.shape = box
		shape.position = center
		body.add_child(shape)
		_shapes.append(shape)
		# A slender frame leaves the glazing visible and shares its actual leaf motion.
		if door_type == "glass" or str(item.get("leaf_material", "")) == "glass":
			for edge in [Vector4(-width / 2, 0, 0.045, float(item.height)), Vector4(width / 2, 0, 0.045, float(item.height)), Vector4(0, -float(item.height) / 2, width, 0.05), Vector4(0, float(item.height) / 2, width, 0.05)]:
				var frame := MeshInstance3D.new()
				var part := BoxMesh.new()
				part.size = Vector3(edge.z, edge.w, 0.08)
				frame.mesh = part
				frame.position = center + Vector3(edge.x, edge.y, 0)
				frame.material_override = materials.get("frame")
				leaf.add_child(frame)
		var handle := MeshInstance3D.new()
		var sphere := SphereMesh.new()
		sphere.radius = 0.035
		sphere.height = 0.07
		sphere.radial_segments = 12
		sphere.rings = 6
		handle.mesh = sphere
		handle.position = Vector3(width * (0.85 if i == 0 else -0.85), minf(float(item.height) * 0.45, 1.05), -0.065)
		handle.material_override = materials.get("rail")
		leaf.add_child(handle)

func apply_state(value: Dictionary, immediate := false) -> void:
	if str(value.get("portal_id", portal_id)) != portal_id:
		return
	var rev := int(value.get("revision", _revision + 1))
	if rev <= _revision:
		return
	_revision = rev
	_opened = bool(value.get("open", false))
	_locked = bool(value.get("locked", false))
	if _transition != null:
		_transition.kill()
	if not immediate and animation_seconds > 0 and is_inside_tree():
		_transition = create_tween().set_parallel(true)
	for i in leaves.size():
		var leaf := leaves[i]
		var target := _leaf_origins[i]
		var angle := _leaf_signs[i] * deg_to_rad(opening_degrees) if _opened else 0.0
		if door_type == "sliding":
			target.x += door_width + 0.04 if _opened else 0.0
			angle = 0.0
		_shapes[i].set_deferred("disabled", _opened)
		if immediate or animation_seconds <= 0 or not is_inside_tree():
			leaf.position = target
			leaf.rotation.y = angle
		else:
			_transition.tween_property(leaf, "position", target, animation_seconds)
			_transition.tween_property(leaf, "rotation:y", angle, animation_seconds)
	if immediate or animation_seconds <= 0 or not is_inside_tree():
		_finish_pose(rev)
	else:
		_transition.chain().tween_callback(_finish_pose.bind(rev))

func _finish_pose(rev: int) -> void:
	if rev != _revision:
		return
	# Only a closed door leaf blocks physics; Core still authorizes passage.
	pose_settled.emit(portal_id, _opened)
