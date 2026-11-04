extends RefCounted
## Visual furniture in metres. Origin is floor centre; forward is -Z.
## Seat and desktop heights refer to their top surface. No physics or placement.

const SURFACE = preload("res://scenes/components/rendering/StylizedSurface.gdshader")


const DETAILS = preload("res://scenes/components/world/FurnitureDetailGeometry.gd")
## Generated parts are immutable after creation, so equal finishes and equal
## part sizes share one resource across every room.
static var _materials: Dictionary = {}
static var _box_meshes: Dictionary = {}

static func create(kind: String) -> Node3D:
	var root: Node3D = _create_base(kind)
	var size := Vector3(.48,.88,.50)
	if kind == "desk": size=Vector3(1.60,.76,.80)
	elif kind == "bench": size=Vector3(1.80,.90,.45)
	return DETAILS.new().upgrade(root,kind,size)

static func create_sized(kind: String, size: Vector3, seat_height_m: float = -1.0) -> Node3D:
	var root: Node3D = _create_sized_base(kind,size,seat_height_m)
	# Decorative/evidence props retain their authored geometry and patterns.
	if kind not in ["bed","cabinet","nightstand","desk","chair","wardrobe","sofa","coffeetable","table","counter","lamp"]:
		return root
	return DETAILS.new().upgrade(root,kind,size)

static func _create_base(kind: String) -> Node3D:
	if kind not in ["chair", "desk", "bench"]:
		push_error("FrameworkFurniture: unknown furniture kind '%s'." % kind)
		return null
	var scene = load("res://scenes/components/world/FrameworkFurniture.tscn")
	var root: Node3D = scene.instantiate()
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
static func _create_sized_base(kind: String, size: Vector3, seat_height_m: float = -1.0) -> Node3D:
	var valid_kinds := [
		"bed", "cabinet", "nightstand", "desk", "chair", "counter", "stove", "sink", "table",
		"wardrobe", "washstand", "sofa", "coffeetable", "lamp", "book", "paper",
		"evidence", "carpet", "curtains", "painting", "doorplate", "pillow", "quilt"
	]
	if kind not in valid_kinds:
		push_error("FrameworkFurniture: unknown sized furniture kind '%s'." % kind)
		return null
	var small_items := ["book", "paper", "evidence", "doorplate", "lamp", "pillow", "quilt", "painting", "carpet", "curtains"]
	if kind not in small_items:
		if size.x < (0.24 if kind in ["cabinet", "nightstand"] else 0.35) or size.y < 0.20 or size.z < 0.20:
			push_error("FrameworkFurniture: furniture dimensions are too small for structural members.")
			return null
	var scene = load("res://scenes/components/world/FrameworkFurniture.tscn")
	var root: Node3D = scene.instantiate()
	root.name = "Framework" + kind.capitalize()
	var frame := _wood(Color("493426"))
	var boards := _wood(Color("886344"))
	match kind:
		"bed":
			_bed(root, size, frame, boards)
		"nightstand":
			_nightstand(root, size, frame, boards)
		"desk":
			_desk_sized(root, size, frame, boards)
		"chair":
			_chair_sized(root, size, frame, boards, seat_height_m)
		"cabinet":
			_cabinet(root, size, frame, boards)
		"wardrobe":
			_wardrobe(root, size, frame, boards)
		"washstand":
			_washstand(root, size, frame, boards)
		"sofa":
			_sofa(root, size, frame, boards)
		"coffeetable":
			_coffeetable(root, size, frame, boards)
		"lamp":
			_lamp(root, size)
		"book":
			_book(root, size)
		"paper":
			_paper(root, size)
		"evidence":
			_evidence(root, size)
		"carpet":
			_carpet(root, size)
		"curtains":
			_curtains(root, size)
		"painting":
			_painting(root, size)
		"doorplate":
			_doorplate(root, size)
		"pillow":
			_pillow(root, size)
		"quilt":
			_quilt(root, size)
		"counter", "table":
			_worktable(root, size, frame, boards)
		"stove":
			_stove(root, size)
		"sink":
			_sink(root, size)
	return root


static func _solid(color: Color, metallic: float = 0.0) -> ShaderMaterial:
	var key := "solid|%s|%.3f" % [color.to_html(), metallic]
	if _materials.has(key): return _materials[key]
	var material := ShaderMaterial.new()
	material.shader = SURFACE
	material.set_shader_parameter("pigment", color)
	material.set_shader_parameter("surface_roughness", 0.90)
	material.set_shader_parameter("surface_metallic", metallic)
	_materials[key] = material
	return material


static func _bed(root: Node3D, s: Vector3, frame: Material, boards: Material) -> void:
	var mattress := _solid(Color("c6bda7"))
	var linen := _solid(Color("dfd6bf"))
	var leg_height := s.y - 0.17
	for x in [-1, 1]:
		for z in [-1, 1]:
			_box(root, "BedLeg_%s_%s" % [x, z], Vector3(0.065, leg_height, 0.065), Vector3(x * (s.x * 0.5 - 0.07), leg_height * 0.5, z * (s.z * 0.5 - 0.07)), frame)
	# Bed underside is blocked from passage (Task 3.01)
	_box(root, "UnderBedSolid", Vector3(s.x - 0.14, leg_height, s.z - 0.14), Vector3(0, leg_height * 0.5, 0), frame)
	_box(root, "BedFrame", Vector3(s.x, 0.07, s.z), Vector3(0, s.y - 0.135, 0), frame)
	_box(root, "Mattress", Vector3(s.x - 0.10, 0.10, s.z - 0.10), Vector3(0, s.y - 0.05, 0), mattress)
	_box(root, "Pillow", Vector3(minf(0.36, s.x * 0.25), 0.065, minf(0.64, s.z - 0.16)), Vector3(-s.x * 0.5 + minf(0.30, s.x * 0.3), s.y + 0.0325, 0), linen)
	_box(root, "Headboard", Vector3(0.055, 0.48, s.z), Vector3(-s.x * 0.5 + 0.0275, s.y + 0.11, 0), boards)

static func _nightstand(root: Node3D, s: Vector3, frame: Material, boards: Material) -> void:
	var hardware := _solid(Color("4d4c45"), 0.45)
	var leg_h := 0.10
	var body_h := s.y - leg_h - 0.03
	for x in [-1, 1]:
		for z in [-1, 1]:
			_box(root, "Leg_%s_%s" % [x, z], Vector3(0.045, leg_h, 0.045), Vector3(x * (s.x * 0.5 - 0.035), leg_h * 0.5, z * (s.z * 0.5 - 0.035)), frame)
	_box(root, "Body", Vector3(s.x - 0.02, body_h, s.z - 0.02), Vector3(0, leg_h + body_h * 0.5, 0), boards)
	_box(root, "Top", Vector3(s.x, 0.03, s.z), Vector3(0, s.y - 0.015, 0), frame)
	_box(root, "DrawerFront", Vector3(s.x - 0.06, body_h * 0.45, 0.02), Vector3(0, leg_h + body_h * 0.72, -s.z * 0.5 + 0.01), boards)
	_box(root, "DrawerHandle", Vector3(0.08, 0.02, 0.025), Vector3(0, leg_h + body_h * 0.72, -s.z * 0.5 - 0.015), hardware)

static func _desk_sized(root: Node3D, s: Vector3, frame: Material, boards: Material) -> void:
	var hardware := _solid(Color("4d4c45"), 0.45)
	var thickness := 0.045
	var leg := 0.055
	var underside := s.y - thickness
	var x := s.x * 0.5 - leg * 0.5 - 0.025
	var z := s.z * 0.5 - leg * 0.5 - 0.025
	_box(root, "Desktop", Vector3(s.x, thickness, s.z), Vector3(0, s.y - thickness * 0.5, 0), boards)
	for side in [-1, 1]:
		var label := "Left" if side < 0 else "Right"
		for end in [-1, 1]:
			var end_label := "Front" if end < 0 else "Rear"
			_box(root, label + end_label + "Leg", Vector3(leg, underside, leg), Vector3(side * x, underside * 0.5, end * z), frame)
		_box(root, label + "Apron", Vector3(leg, 0.08, 2.0 * z), Vector3(side * x, underside - 0.04, 0), frame)
		_box(root, label + "Stretcher", Vector3(leg, leg, 2.0 * z), Vector3(side * x, 0.20, 0), frame)
	_box(root, "RearApron", Vector3(2.0 * x, 0.08, leg), Vector3(0, underside - 0.04, z), frame)
	_box(root, "CrossStretcher", Vector3(2.0 * x, leg, leg), Vector3(0, 0.20, 0), frame)
	var drawer_w := minf(0.55, s.x * 0.45)
	_box(root, "DrawerFront", Vector3(drawer_w, 0.09, 0.02), Vector3(0, underside - 0.045, -z + 0.01), boards)
	_box(root, "DrawerHandle", Vector3(0.08, 0.02, 0.025), Vector3(0, underside - 0.045, -z - 0.015), hardware)

static func _chair_sized(root: Node3D, s: Vector3, frame: Material, boards: Material, seat_height_m: float = -1.0) -> void:
	_seating(root, s.x, s.z, s.y, 0.045, frame, boards, seat_height_m)


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
	var key := "wood|%s" % color.to_html()
	if _materials.has(key): return _materials[key]
	var material := ShaderMaterial.new()
	material.shader = SURFACE
	material.set_shader_parameter("pigment", color)
	material.set_shader_parameter("surface_roughness", 0.88)
	material.set_shader_parameter("surface_metallic", 0.0)
	material.set_shader_parameter("wood", true)
	_materials[key] = material
	return material


static func _box(parent: Node3D, label: String, size: Vector3, centre: Vector3, material: Material) -> void:
	var key := "%.4f|%.4f|%.4f" % [size.x, size.y, size.z]
	var mesh: BoxMesh = _box_meshes.get(key)
	if mesh == null:
		mesh = BoxMesh.new()
		mesh.size = size
		_box_meshes[key] = mesh
	var part := MeshInstance3D.new()
	part.name = label
	part.mesh = mesh
	part.position = centre
	part.material_override = material
	parent.add_child(part)


static func _seating(root: Node3D, width: float, depth: float, height: float, leg: float, frame: Material, boards: Material, declared_seat_height: float = -1.0) -> void:
	var seat_height := declared_seat_height if declared_seat_height > 0.0 else 0.46
	if seat_height < 0.20 or seat_height > height - 0.10:
		push_error("Chair seat height is outside its structural bounds")
		return
	var surface := Marker3D.new()
	surface.name = "SeatSurface"
	surface.position.y = seat_height
	surface.set_meta("surface_size_m", Vector2(width, depth))
	surface.set_meta("surface_kind", "seat")
	root.add_child(surface)
	root.set_meta("seat_height_m", seat_height)
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

# -------------------------------------------------------------
# Tasks 3.02, 3.03, 3.07 - 3.18 Standard Bedroom Furnishings
# -------------------------------------------------------------

static func _wardrobe(root: Node3D, s: Vector3, frame: Material, boards: Material) -> void:
	var hardware := _solid(Color("4d4c45"), 0.45)
	_box(root, "Plinth", Vector3(s.x - 0.04, 0.08, s.z - 0.04), Vector3(0, 0.04, 0), frame)
	_box(root, "Body", Vector3(s.x, s.y - 0.12, s.z - 0.03), Vector3(0, 0.08 + (s.y - 0.12) * 0.5, 0.015), boards)
	_box(root, "TopCornice", Vector3(s.x + 0.04, 0.04, s.z + 0.02), Vector3(0, s.y - 0.02, 0), frame)
	for side in [-1, 1]:
		var side_name := "Left" if side < 0 else "Right"
		_box(root, "Door_%s" % side_name, Vector3(s.x * 0.5 - 0.015, s.y - 0.16, 0.025), Vector3(side * s.x * 0.25, 0.08 + (s.y - 0.16) * 0.5, -s.z * 0.5 + 0.0125), boards)
		_box(root, "Handle_%s" % side_name, Vector3(0.018, minf(0.14, s.y * 0.15), 0.025), Vector3(side * 0.04, s.y * 0.52, -s.z * 0.5 - 0.015), hardware)

static func _washstand(root: Node3D, s: Vector3, frame: Material, boards: Material) -> void:
	var stone := _solid(Color("dcd8cd"))
	var ceramic := _solid(Color("eae6db"), 0.2)
	var leg := 0.045
	var x := s.x * 0.5 - leg * 0.5 - 0.02
	var z := s.z * 0.5 - leg * 0.5 - 0.02
	for side in [-1, 1]:
		for end in [-1, 1]:
			_box(root, "Leg_%s_%s" % [side, end], Vector3(leg, s.y - 0.12, leg), Vector3(side * x, (s.y - 0.12) * 0.5, end * z), frame)
		_box(root, "Rail_%s" % side, Vector3(leg, 0.04, 2.0 * z), Vector3(side * x, 0.18, 0), frame)
	_box(root, "LowerShelf", Vector3(s.x - 0.06, 0.02, s.z - 0.06), Vector3(0, 0.19, 0), boards)
	_box(root, "StandTop", Vector3(s.x, 0.035, s.z), Vector3(0, s.y - 0.10, 0), boards)
	_box(root, "BasinRing", Vector3(minf(0.42, s.x * 0.7), 0.09, minf(0.42, s.z * 0.7)), Vector3(0, s.y - 0.045, 0), stone)
	_box(root, "BasinHollow", Vector3(minf(0.32, s.x * 0.55), 0.02, minf(0.32, s.z * 0.55)), Vector3(0, s.y - 0.07, 0), ceramic)
	_box(root, "Pitcher", Vector3(0.14, 0.22, 0.14), Vector3(s.x * 0.28, s.y + 0.02, 0), ceramic)
	_box(root, "TowelBar", Vector3(s.x * 0.8, 0.02, 0.02), Vector3(0, s.y - 0.12, -s.z * 0.5 - 0.02), frame)

static func _sofa(root: Node3D, s: Vector3, frame: Material, boards: Material) -> void:
	var cushion_mat := _solid(Color("4a5c68"))
	var seat_h := 0.44
	_box(root, "SofaPlinth", Vector3(s.x - 0.04, 0.10, s.z - 0.04), Vector3(0, 0.05, 0), frame)
	_box(root, "BackRest", Vector3(s.x - 0.04, s.y - 0.35, 0.16), Vector3(0, 0.35 + (s.y - 0.35) * 0.5, s.z * 0.5 - 0.08), cushion_mat)
	for side in [-1, 1]:
		_box(root, "Arm_%s" % side, Vector3(0.14, 0.24, s.z), Vector3(side * (s.x * 0.5 - 0.07), seat_h + 0.10, 0), boards)
	var seat_w := (s.x - 0.32) * 0.5
	_box(root, "Seat_Left", Vector3(seat_w, 0.14, s.z - 0.20), Vector3(-seat_w * 0.5 - 0.01, seat_h - 0.07, -0.04), cushion_mat)
	_box(root, "Seat_Right", Vector3(seat_w, 0.14, s.z - 0.20), Vector3(seat_w * 0.5 + 0.01, seat_h - 0.07, -0.04), cushion_mat)
	# Multi-seat slots (Task 3.09)
	var slot_l := Marker3D.new()
	slot_l.name = "Sit_Left"
	slot_l.position = Vector3(-seat_w * 0.5 - 0.01, seat_h, 0.0)
	root.add_child(slot_l)
	var slot_r := Marker3D.new()
	slot_r.name = "Sit_Right"
	slot_r.position = Vector3(seat_w * 0.5 + 0.01, seat_h, 0.0)
	root.add_child(slot_r)

static func _coffeetable(root: Node3D, s: Vector3, frame: Material, boards: Material) -> void:
	var leg := 0.045
	var x := s.x * 0.5 - leg * 0.5 - 0.02
	var z := s.z * 0.5 - leg * 0.5 - 0.02
	for side in [-1, 1]:
		for end in [-1, 1]:
			_box(root, "Leg_%s_%s" % [side, end], Vector3(leg, s.y - 0.035, leg), Vector3(side * x, (s.y - 0.035) * 0.5, end * z), frame)
		_box(root, "Apron_%s" % side, Vector3(leg, 0.04, 2.0 * z), Vector3(side * x, s.y - 0.055, 0), frame)
	_box(root, "Tabletop", Vector3(s.x, 0.035, s.z), Vector3(0, s.y - 0.0175, 0), boards)
	_box(root, "LowerShelf", Vector3(s.x - 0.08, 0.02, s.z - 0.08), Vector3(0, 0.12, 0), boards)

static func _lamp(root: Node3D, s: Vector3) -> void:
	var brass := _solid(Color("c4a24d"), 0.75)
	var shade_mat := _solid(Color("fff8e7"))
	var light_h := maxf(s.y, 0.45)
	_box(root, "LampBase", Vector3(0.16, 0.025, 0.16), Vector3(0, 0.0125, 0), brass)
	_box(root, "LampStem", Vector3(0.025, light_h - 0.16, 0.025), Vector3(0, (light_h - 0.16) * 0.5 + 0.025, 0), brass)
	_box(root, "Lampshade", Vector3(0.24, 0.14, 0.24), Vector3(0, light_h - 0.07, 0), shade_mat)
	# OmniLight3D (Task 3.11)
	var light := OmniLight3D.new()
	light.name = "LampLight"
	light.light_color = Color("fff5df")
	light.light_energy = 1.4
	light.omni_range = 6.5
	light.position = Vector3(0, light_h - 0.07, 0)
	root.add_child(light)

static func _book(root: Node3D, s: Vector3) -> void:
	var leather := _solid(Color("6a3528"))
	var pages := _solid(Color("f4eedb"))
	var w := maxf(s.x, 0.22)
	var h := maxf(s.y, 0.04)
	var d := maxf(s.z, 0.16)
	_box(root, "BookCover", Vector3(w, h, d), Vector3(0, h * 0.5, 0), leather)
	_box(root, "BookPages", Vector3(w - 0.015, h - 0.008, d - 0.01), Vector3(0.005, h * 0.5, 0), pages)

static func _paper(root: Node3D, s: Vector3) -> void:
	var paper_mat := _solid(Color("f9f6ec"))
	var w := maxf(s.x, 0.21)
	var d := maxf(s.z, 0.29)
	_box(root, "PaperSheet", Vector3(w, 0.003, d), Vector3(0, 0.0015, 0), paper_mat)

static func _evidence(root: Node3D, s: Vector3) -> void:
	var brass := _solid(Color("d4af37"), 0.85)
	_box(root, "EvidenceBody", Vector3(maxf(s.x, 0.08), maxf(s.y, 0.02), maxf(s.z, 0.08)), Vector3(0, 0.01, 0), brass)

static func _carpet(root: Node3D, s: Vector3) -> void:
	var rug_mat := _solid(Color("82433d"))
	_box(root, "RugSurface", Vector3(maxf(s.x, 1.2), 0.004, maxf(s.z, 1.8)), Vector3(0, 0.002, 0), rug_mat)

static func _curtains(root: Node3D, s: Vector3) -> void:
	var cloth := _solid(Color("734945"))
	var brass := _solid(Color("b39656"), 0.7)
	var w := maxf(s.x, 1.4)
	var h := maxf(s.y, 2.0)
	_box(root, "CurtainRod", Vector3(w + 0.16, 0.035, 0.035), Vector3(0, h + 0.02, 0), brass)
	_box(root, "CurtainLeft", Vector3(w * 0.25, h, 0.05), Vector3(-w * 0.38, h * 0.5, 0), cloth)
	_box(root, "CurtainRight", Vector3(w * 0.25, h, 0.05), Vector3(w * 0.38, h * 0.5, 0), cloth)

static func _painting(root: Node3D, s: Vector3) -> void:
	var frame_mat := _solid(Color("5c4033"))
	var canvas_mat := _solid(Color("9fa8a3"))
	var w := maxf(s.x, 0.8)
	var h := maxf(s.y, 0.6)
	_box(root, "Frame", Vector3(w, h, 0.03), Vector3(0, h * 0.5, 0), frame_mat)
	_box(root, "Canvas", Vector3(w - 0.08, h - 0.08, 0.01), Vector3(0, h * 0.5, -0.01), canvas_mat)

static func _doorplate(root: Node3D, s: Vector3) -> void:
	var brass := _solid(Color("c4a24d"), 0.8)
	_box(root, "Plaque", Vector3(maxf(s.x, 0.24), maxf(s.y, 0.10), 0.015), Vector3(0, 0.05, 0), brass)

static func _pillow(root: Node3D, s: Vector3) -> void:
	var linen := _solid(Color("dfd6bf"))
	var w := maxf(s.x, 0.5)
	var h := maxf(s.y, 0.10)
	var d := maxf(s.z, 0.35)
	_box(root, "PillowCushion", Vector3(w, h, d), Vector3(0, h * 0.5, 0), linen)
	var contact := Marker3D.new()
	contact.name = "HeadContact"
	contact.position = Vector3(0, h, 0)
	root.add_child(contact)

static func _quilt(root: Node3D, s: Vector3) -> void:
	var linen := _solid(Color("c9c0ac"))
	_box(root, "QuiltFold", Vector3(maxf(s.x, 1.1), maxf(s.y, 0.12), maxf(s.z, 1.2)), Vector3(0, 0.06, 0), linen)

