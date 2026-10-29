extends RefCounted
## Visual furniture in metres. Origin is floor centre; forward is -Z.
## Seat and desktop heights refer to their top surface. No physics or placement.

const SURFACE = preload("res://rendering/rpg/StylizedSurface.gdshader")


static func create(kind: String) -> Node3D:
	if kind not in ["chair", "desk", "bench"]:
		push_error("FrameworkFurniture: unknown furniture kind '%s'." % kind)
		return null
	var root := Node3D.new()
	root.name = "Framework" + kind.capitalize()
	var frame := _wood(Color("493426"))
	var boards := _wood(Color("886344"))
	match kind:
		"chair":
			_seating(root, 0.48, 0.50, 0.88, 0.045, frame, boards)
		"desk":
			_desk(root, frame, boards)
		"bench":
			_seating(root, 1.80, 0.45, 0.90, 0.055, frame, boards)
	return root


## Size is X width, Y work/bed surface height, Z depth; bed headboard rises 0.35 m.
## Dimensions come from DV. Fixed section sizes are never stretched with the root.
static func create_sized(kind: String, size: Vector3) -> Node3D:
	if kind not in ["bed", "cabinet", "counter", "stove", "sink", "table"]:
		push_error("FrameworkFurniture: unknown sized furniture kind '%s'." % kind)
		return null
	if size.x < (0.24 if kind=="cabinet" else 0.40) or size.y < 0.30 or size.z < 0.35:
		push_error("FrameworkFurniture: furniture dimensions are too small for structural members.")
		return null
	var root := Node3D.new()
	root.name = "Framework" + kind.capitalize()
	var frame := _wood(Color("493426"))
	var boards := _wood(Color("886344"))
	match kind:
		"bed":
			_bed(root, size, frame, boards)
		"cabinet":
			_cabinet(root, size, frame, boards)
		"counter", "table":
			_worktable(root, size, frame, boards)
		"stove":
			_stove(root, size)
		"sink":
			_sink(root, size)
	return root


static func _solid(color: Color, metallic: float = 0.0) -> ShaderMaterial:
	var material := ShaderMaterial.new()
	material.shader = SURFACE
	material.set_shader_parameter("pigment", color)
	material.set_shader_parameter("surface_roughness", 0.90)
	material.set_shader_parameter("surface_metallic", metallic)
	return material


static func _bed(root: Node3D, s: Vector3, frame: Material, boards: Material) -> void:
	var mattress := _solid(Color("c6bda7"))
	var linen := _solid(Color("dfd6bf"))
	var leg_height := s.y - 0.17
	for x in [-1, 1]:
		for z in [-1, 1]:
			_box(root, "BedLeg_%s_%s" % [x, z], Vector3(0.065, leg_height, 0.065), Vector3(x * (s.x * 0.5 - 0.07), leg_height * 0.5, z * (s.z * 0.5 - 0.07)), frame)
	_box(root, "BedFrame", Vector3(s.x, 0.07, s.z), Vector3(0, s.y - 0.135, 0), frame)
	_box(root, "Mattress", Vector3(s.x - 0.10, 0.10, s.z - 0.10), Vector3(0, s.y - 0.05, 0), mattress)
	_box(root, "Pillow", Vector3(minf(0.36, s.x * 0.25), 0.065, minf(0.64, s.z - 0.16)), Vector3(-s.x * 0.5 + minf(0.30, s.x * 0.3), s.y + 0.0325, 0), linen)
	_box(root, "Headboard", Vector3(0.055, 0.48, s.z), Vector3(-s.x * 0.5 + 0.0275, s.y + 0.11, 0), boards)


static func _cabinet(root: Node3D, s: Vector3, frame: Material, boards: Material) -> void:
	var hardware := _solid(Color("4d4c45"), 0.45)
	_box(root, "Plinth", Vector3(s.x - 0.04, 0.08, s.z - 0.04), Vector3(0, 0.04, 0), frame)
	_box(root, "CabinetBody", Vector3(s.x, s.y - 0.11, s.z - 0.03), Vector3(0, 0.08 + (s.y - 0.11) * 0.5, 0.015), boards)
	_box(root, "Top", Vector3(s.x, 0.03, s.z), Vector3(0, s.y - 0.015, 0), frame)
	for side in [-1, 1]:
		_box(root, "Door_%s" % side, Vector3(s.x * 0.5 - 0.012, s.y - 0.14, 0.03), Vector3(side * s.x * 0.25, 0.09 + (s.y - 0.14) * 0.5, -s.z * 0.5 + 0.015), boards)
		_box(root, "Handle_%s" % side, Vector3(0.018, minf(0.12, s.y * 0.20), 0.025), Vector3(side * 0.035, s.y * 0.53, -s.z * 0.5 - 0.0125), hardware)


static func _worktable(root: Node3D, s: Vector3, frame: Material, boards: Material) -> void:
	var leg_height := s.y - 0.045
	var x := s.x * 0.5 - 0.055
	var z := s.z * 0.5 - 0.055
	_box(root, "Worktop", Vector3(s.x, 0.045, s.z), Vector3(0, s.y - 0.0225, 0), boards)
	for side in [-1, 1]:
		for end in [-1, 1]:
			_box(root, "Leg_%s_%s" % [side, end], Vector3(0.055, leg_height, 0.055), Vector3(side * x, leg_height * 0.5, end * z), frame)
		_box(root, "SideStretcher_%s" % side, Vector3(0.055, 0.055, 2.0 * z), Vector3(side * x, 0.16, 0), frame)
		_box(root, "Apron_%s" % side, Vector3(2.0 * x, 0.065, 0.045), Vector3(0, leg_height - 0.0325, side * z), frame)
	_box(root, "CrossStretcher", Vector3(2.0 * x, 0.055, 0.055), Vector3(0, 0.16, 0), frame)


static func _stove(root: Node3D, s: Vector3) -> void:
	var masonry := _solid(Color("777267"))
	var iron := _solid(Color("383a37"), 0.35)
	_box(root, "HearthBody", Vector3(s.x, s.y - 0.035, s.z), Vector3(0, (s.y - 0.035) * 0.5, 0), masonry)
	_box(root, "IronTop", Vector3(s.x, 0.025, s.z), Vector3(0, s.y - 0.0225, 0), iron)
	_box(root, "FireDoor", Vector3(s.x * 0.52, s.y * 0.42, 0.025), Vector3(0, s.y * 0.46, -s.z * 0.5 - 0.0125), iron)
	_box(root, "DoorLatch", Vector3(0.07, 0.018, 0.025), Vector3(s.x * 0.17, s.y * 0.46, -s.z * 0.5 - 0.0375), iron)
	for x in [-1, 1]:
		for z in [-1, 1]:
			var mesh := CylinderMesh.new()
			mesh.top_radius = minf(s.x, s.z) * 0.15
			mesh.bottom_radius = mesh.top_radius
			mesh.height = 0.01
			mesh.radial_segments = 16
			var ring := MeshInstance3D.new()
			ring.name = "Hob_%s_%s" % [x, z]
			ring.mesh = mesh
			ring.material_override = iron
			ring.position = Vector3(x * s.x * 0.25, s.y - 0.005, z * s.z * 0.25)
			root.add_child(ring)


static func _sink(root: Node3D, s: Vector3) -> void:
	var stone := _solid(Color("969387"))
	var support := _solid(Color("6d7169"))
	var bowl_height := minf(0.22, s.y * 0.40)
	var bottom := s.y - bowl_height
	_box(root, "Pedestal", Vector3(s.x - 0.14, bottom, s.z - 0.14), Vector3(0, bottom * 0.5, 0), support)
	_box(root, "BasinFloor", Vector3(s.x, 0.045, s.z), Vector3(0, bottom + 0.0225, 0), stone)
	for side in [-1, 1]:
		_box(root, "BasinSide_%s" % side, Vector3(0.055, bowl_height, s.z), Vector3(side * (s.x * 0.5 - 0.0275), bottom + bowl_height * 0.5, 0), stone)
		_box(root, "BasinEnd_%s" % side, Vector3(s.x - 0.11, bowl_height, 0.055), Vector3(0, bottom + bowl_height * 0.5, side * (s.z * 0.5 - 0.0275)), stone)


static func _wood(color: Color) -> ShaderMaterial:
	var material := ShaderMaterial.new()
	material.shader = SURFACE
	material.set_shader_parameter("pigment", color)
	material.set_shader_parameter("surface_roughness", 0.88)
	material.set_shader_parameter("surface_metallic", 0.0)
	material.set_shader_parameter("wood", true)
	return material


static func _box(parent: Node3D, label: String, size: Vector3, centre: Vector3, material: Material) -> void:
	var mesh := BoxMesh.new()
	mesh.size = size
	var part := MeshInstance3D.new()
	part.name = label
	part.mesh = mesh
	part.position = centre
	part.material_override = material
	parent.add_child(part)


static func _seating(root: Node3D, width: float, depth: float, height: float, leg: float, frame: Material, boards: Material) -> void:
	var seat_height := 0.46
	var seat_thickness := 0.045
	var underside := seat_height - seat_thickness
	var x := width * 0.5 - leg * 0.5 - 0.015
	var z := depth * 0.5 - leg * 0.5 - 0.015
	_box(root, "Seat", Vector3(width, seat_thickness, depth), Vector3(0, seat_height - seat_thickness * 0.5, 0), boards)
	for side in [-1, 1]:
		var label := "Left" if side < 0 else "Right"
		_box(root, label + "FrontLeg", Vector3(leg, underside, leg), Vector3(side * x, underside * 0.5, -z), frame)
		# Rear legs continue as single solid back posts, with no visible joint gap.
		_box(root, label + "BackPost", Vector3(leg, height, leg), Vector3(side * x, height * 0.5, z), frame)
		_box(root, label + "SideStretcher", Vector3(leg, 0.045, 2.0 * z), Vector3(side * x, 0.19, 0), frame)
		_box(root, label + "SeatRail", Vector3(leg, 0.065, 2.0 * z), Vector3(side * x, underside - 0.0325, 0), frame)
	for end in [-1, 1]:
		var label := "Front" if end < 0 else "Rear"
		_box(root, label + "SeatRail", Vector3(2.0 * x, 0.065, leg), Vector3(0, underside - 0.0325, end * z), frame)
	_box(root, "LowerBackBoard", Vector3(2.0 * x, 0.10, 0.035), Vector3(0, 0.65, z), boards)
	_box(root, "UpperBackBoard", Vector3(2.0 * x, 0.10, 0.035), Vector3(0, height - 0.05, z), boards)
	_box(root, "CrossStretcher", Vector3(2.0 * x, 0.045, leg), Vector3(0, 0.19, 0), frame)


static func _desk(root: Node3D, frame: Material, boards: Material) -> void:
	var width := 1.20
	var depth := 0.60
	var height := 0.75
	var thickness := 0.045
	var leg := 0.055
	var underside := height - thickness
	var x := width * 0.5 - leg * 0.5 - 0.025
	var z := depth * 0.5 - leg * 0.5 - 0.025
	_box(root, "Desktop", Vector3(width, thickness, depth), Vector3(0, height - thickness * 0.5, 0), boards)
	for side in [-1, 1]:
		var label := "Left" if side < 0 else "Right"
		for end in [-1, 1]:
			var end_label := "Front" if end < 0 else "Rear"
			_box(root, label + end_label + "Leg", Vector3(leg, underside, leg), Vector3(side * x, underside * 0.5, end * z), frame)
		_box(root, label + "Apron", Vector3(leg, 0.08, 2.0 * z), Vector3(side * x, underside - 0.04, 0), frame)
		_box(root, label + "Stretcher", Vector3(leg, leg, 2.0 * z), Vector3(side * x, 0.20, 0), frame)
	for end in [-1, 1]:
		var label := "Front" if end < 0 else "Rear"
		_box(root, label + "Apron", Vector3(2.0 * x, 0.08, leg), Vector3(0, underside - 0.04, end * z), frame)
	_box(root, "CrossStretcher", Vector3(2.0 * x, leg, leg), Vector3(0, 0.20, 0), frame)
