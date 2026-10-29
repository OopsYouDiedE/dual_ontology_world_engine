extends Node3D
## Openable window sash generator with dedicated frame, glass, hinge and handle (Task 2.17, 2.18).
## Origin is at hinge pivot. Glass is a separate translucent material with solid collision
## when closed to block walking through or camera penetration.
## Window opening angle is strictly clamped with zero drift across multiple cycles.
const PhysicsLayers = preload("res://geometry/PhysicsLayers.gd")

var sash_width: float = 0.8
var sash_height: float = 1.0
var sash_thickness: float = 0.04
var current_angle: float = 0.0
var max_angle: float = 75.0
var min_angle: float = 0.0
var hinge_node: Node3D
var glass_body: StaticBody3D

static func create(id: String, width: float, height: float, thickness: float, max_open_angle: float, min_open_angle: float, frame_material: Material, glass_material: Material = null) -> Node3D:
	if id.is_empty() or not is_finite(width) or not is_finite(height) or not is_finite(thickness) or not is_finite(max_open_angle) or not is_finite(min_open_angle) or width <= 0.0 or height <= 0.0 or thickness <= 0.0:
		push_error("HingedWindow3D " + id + ": invalid-parameters")
		return null
	
	var script = load("res://geometry/generators/HingedWindow3D.gd")
	var root: Node3D = script.new()
	root.name = "WorldObject3D"
	root.set_meta("world_object_id", id)
	root.set_meta("kind", "openable_window")
	
	root.sash_width = width
	root.sash_height = height
	root.sash_thickness = thickness
	root.max_angle = max_open_angle
	root.min_angle = min_open_angle
	
	# HingeRoot pivot
	var hinge := Node3D.new()
	hinge.name = "HingeRoot"
	root.add_child(hinge)
	root.hinge_node = hinge
	
	var visuals := Node3D.new()
	visuals.name = "VisualRoot"
	hinge.add_child(visuals)
	
	# Default materials if null
	var f_mat: Material = frame_material
	if f_mat == null:
		var std_f := StandardMaterial3D.new()
		std_f.albedo_color = Color("4f3a2c")
		f_mat = std_f
	
	var g_mat: Material = glass_material
	if g_mat == null:
		var std_g := StandardMaterial3D.new()
		std_g.albedo_color = Color(0.72, 0.85, 0.94, 0.38)
		std_g.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		std_g.roughness = 0.12
		std_g.metallic = 0.05
		g_mat = std_g
	
	var border := 0.05
	# Outer sash frame (4 sides)
	_part(visuals, "Frame_Bottom", Vector3(width * 0.5, border * 0.5, 0.0), Vector3(width, border, thickness), f_mat)
	_part(visuals, "Frame_Top", Vector3(width * 0.5, height - border * 0.5, 0.0), Vector3(width, border, thickness), f_mat)
	_part(visuals, "Frame_HingeSide", Vector3(border * 0.5, height * 0.5, 0.0), Vector3(border, height - 2.0 * border, thickness), f_mat)
	_part(visuals, "Frame_LockSide", Vector3(width - border * 0.5, height * 0.5, 0.0), Vector3(border, height - 2.0 * border, thickness), f_mat)
	
	# Window Glass (Separate transparent material, Task 2.17)
	var glass := MeshInstance3D.new()
	glass.name = "SashGlass"
	var glass_mesh := BoxMesh.new()
	glass_mesh.size = Vector3(width - 2.0 * border, height - 2.0 * border, thickness * 0.35)
	glass.mesh = glass_mesh
	glass.position = Vector3(width * 0.5, height * 0.5, 0.0)
	glass.material_override = g_mat
	visuals.add_child(glass)
	
	# Window Handle & Latch on lock side
	var handle := MeshInstance3D.new()
	handle.name = "WindowHandle"
	var h_mesh := BoxMesh.new()
	h_mesh.size = Vector3(0.02, 0.10, 0.035)
	handle.mesh = h_mesh
	var brass := StandardMaterial3D.new()
	brass.albedo_color = Color("d4af37")
	brass.metallic = 0.85
	brass.roughness = 0.25
	handle.material_override = brass
	handle.position = Vector3(width - border * 0.6, height * 0.5, thickness * 0.5 + 0.015)
	visuals.add_child(handle)
	
	# CollisionRoot for Window (solid collision blocks character & camera, Task 2.17)
	var body := StaticBody3D.new()
	body.name = "CollisionRoot"
	body.collision_layer = PhysicsLayers.SOLID
	body.collision_mask = 0
	hinge.add_child(body)
	root.glass_body = body
	
	var col := CollisionShape3D.new()
	col.name = "WindowSashCollision"
	var box_shape := BoxShape3D.new()
	box_shape.size = Vector3(width, height, thickness)
	col.shape = box_shape
	col.position = Vector3(width * 0.5, height * 0.5, 0.0)
	body.add_child(col)
	
	# Interaction Slots (Task 2.18)
	var slots := Node3D.new()
	slots.name = "InteractionSlots"
	root.add_child(slots)
	
	var approach := Marker3D.new()
	approach.name = "Approach"
	approach.position = Vector3(width * 0.5, 0.0, 0.65)
	slots.add_child(approach)
	
	var stand := Marker3D.new()
	stand.name = "Stand"
	stand.position = Vector3(width * 0.5, 0.0, 0.45)
	slots.add_child(stand)
	
	var contact := Marker3D.new()
	contact.name = "Contact"
	contact.position = Vector3(width - border * 0.6, height * 0.5, 0.0)
	slots.add_child(contact)
	
	return root

static func _part(parent: Node3D, part_name: String, pos: Vector3, sz: Vector3, mat: Material) -> void:
	var m := MeshInstance3D.new()
	m.name = part_name
	var mesh := BoxMesh.new()
	mesh.size = sz
	m.mesh = mesh
	m.position = pos
	m.material_override = mat
	parent.add_child(m)

func set_angle_checked(angle_deg: float) -> bool:
	var target := clampf(angle_deg, minf(min_angle, max_angle), maxf(min_angle, max_angle))
	current_angle = target
	if hinge_node != null:
		hinge_node.rotation_degrees.y = target
	return true

func is_open() -> bool:
	return absf(current_angle) > 5.0

func open_ratio() -> float:
	var span := absf(max_angle - min_angle)
	if span < 0.001: return 0.0
	return clampf(absf(current_angle - min_angle) / span, 0.0, 1.0)

func can_operate_from_position(world_pos: Vector3, reach_m: float = 1.25) -> bool:
	var local_pos := to_local(world_pos) if is_inside_tree() else (transform.affine_inverse() * world_pos)
	var target := Vector3(sash_width * 0.5, sash_height * 0.5, 0.0)
	return local_pos.distance_to(target) <= reach_m
