extends RefCounted
## Metre-scale exterior shell. The host alone validates passage and interactions.
const PREFABS = preload("res://scenes/components/prefabs/PrefabLibrary.gd")
const WALL_THICKNESS := 0.28
const PLATFORM_HEIGHT := 0.36

static func build(kit: Node3D, parent: Node3D, spec: Dictionary) -> Node3D:
	var width: float = maxf(4.0, float(spec.get("width", 9.2)))
	var depth: float = maxf(4.0, float(spec.get("depth", 8.0)))
	var storeys: int = maxi(1, int(spec.get("storeys", 1)))
	var height: float = maxf(float(storeys) * 2.8, float(spec.get("height", 3.4)))
	var level_height: float = height / storeys
	var door_width: float = clampf(float(spec.get("door_width", 1.3)), 0.9, width - 2.0)
	var door_height: float = clampf(float(spec.get("door_height", 2.4)), 1.9, level_height - PLATFORM_HEIGHT - 0.22)
	var window_width: float = clampf(float(spec.get("window_width", 1.18)), 0.5, minf(width, depth) * 0.25)
	var window_height: float = clampf(float(spec.get("window_height", 1.3)), 0.55, level_height - 1.15)
	var window_spacing: float = maxf(0.42, float(spec.get("window_spacing", 1.35)))
	var roof_rise: float = maxf(0.5, float(spec.get("roof_rise", 1.35)))
	var wall_color: String = str(spec.get("wall_color", "b8b1a3"))
	var stone_color: String = str(spec.get("stone_color", "77756d"))
	var wood_color: String = str(spec.get("wood_color", "493a30"))
	var roof_color: String = str(spec.get("roof_color", "514b48"))
	var wall_override: Material = spec.get("wall_material") as Material
	var roof_override: Material = spec.get("roof_material") as Material
	var key := "exterior_architecture_v1|%s" % str([
		width, depth, height, storeys, door_width, door_height, window_width,
		window_height, window_spacing, roof_rise, wall_color, stone_color,
		wood_color, roof_color, str(spec.get("material_key", "")),
		_material_key(wall_override), _material_key(roof_override)
	])
	var instance: Node3D = PREFABS.instantiate(key, &"wall", func() -> Node3D:
		var old_root: Node3D = kit.content_root
		var root := Node3D.new()
		root.name = "ExteriorArchitecture"
		kit.content_root = root
		_author(kit, root, width, depth, height, storeys, door_width, door_height,
			window_width, window_height, window_spacing, roof_rise, wall_color,
			stone_color, wood_color, roof_color, wall_override, roof_override)
		kit.content_root = old_root
		return root)
	if instance == null: return null
	instance.name = str(spec.get("name", "ExteriorArchitecture"))
	parent.add_child(instance)
	return instance

static func _material_key(mat: Material) -> String:
	if mat == null: return "default"
	if mat is ShaderMaterial:
		var shader_material := mat as ShaderMaterial
		var shader_id := "none"
		if shader_material.shader != null:
			shader_id = shader_material.shader.resource_path
			if shader_id.is_empty(): shader_id = shader_material.shader.code.sha256_text()
		return str([shader_id, shader_material.get_shader_parameter("pigment"),
			shader_material.get_shader_parameter("surface_roughness"),
			shader_material.get_shader_parameter("surface_metallic")])
	if not mat.resource_path.is_empty(): return mat.resource_path
	if mat is StandardMaterial3D:
		var standard := mat as StandardMaterial3D
		return str([standard.albedo_color, standard.roughness, standard.metallic])
	return mat.get_class()

static func _author(kit: Node3D, root: Node3D, width: float, depth: float,
		height: float, storeys: int, door_width: float, door_height: float,
		window_width: float, window_height: float, window_spacing: float,
		roof_rise: float, wall_color: String, stone_color: String,
		wood_color: String, roof_color: String, wall_override: Material,
		roof_override: Material) -> void:
	var plaster: Material = wall_override if wall_override != null else kit.material("exterior_plaster", wall_color, 2, 0.91)
	var stone: Material = kit.material("exterior_stone", stone_color, 2, 0.86)
	var timber: Material = kit.material("exterior_wood", wood_color, 1, 0.58)
	var roofing: Material = roof_override if roof_override != null else kit.material("exterior_roof", roof_color, 2, 0.81)
	var copper: Material = kit.material("exterior_copper", "806951", 0, 0.46, 0.52)
	var glass: Material = kit.material("exterior_glass", "829b9d", 0, 0.24, 0.08)
	var detail := _decoration(root, "ExteriorDetails")
	var level_height := height / storeys
	for side in ["Front", "Rear", "Right", "Left"]:
		var wall := Node3D.new()
		wall.name = side + "Wall"
		root.add_child(wall)
		var length: float = width if side in ["Front", "Rear"] else depth
		match side:
			"Front": wall.position.z = depth * 0.5
			"Rear": wall.position.z = -depth * 0.5; wall.rotation.y = PI
			"Right": wall.position.x = width * 0.5; wall.rotation.y = PI * 0.5
			"Left": wall.position.x = -width * 0.5; wall.rotation.y = -PI * 0.5
		for floor_index in range(storeys):
			var low := floor_index * level_height
			var openings: Array[Dictionary] = []
			if side == "Front" and floor_index == 0:
				openings.append({"x": 0.0, "width": door_width, "bottom": PLATFORM_HEIGHT, "top": PLATFORM_HEIGHT + door_height, "kind": "door"})
			var centers := _window_centers(length, window_width, window_spacing, door_width if side == "Front" and floor_index == 0 else 0.0)
			for center in centers:
				openings.append({"x": center, "width": window_width,
					"bottom": low + maxf(0.83, (level_height - window_height) * 0.47),
					"top": low + maxf(0.83, (level_height - window_height) * 0.47) + window_height,
					"kind": "window"})
			openings.sort_custom(func(a: Dictionary, b: Dictionary) -> bool: return a.x < b.x)
			_wall_band(kit, wall, detail, length, low, low + level_height, openings,
				plaster, stone, timber, copper, glass)
		# A continuous cornice is broad enough to read at a distance.
		kit.box(wall, Vector3(0, height - 0.07, 0.17), Vector3(length + 0.2, 0.17, 0.46), stone, "Cornice")
	_roof(kit, root, detail, width, depth, height, roof_rise, plaster, roofing, copper)
	_corners(kit, detail, width, depth, height, stone)
	_entry(kit, root, detail, width, depth, door_width, door_height, stone, timber, copper)
	root.set_meta("exterior_shell_only", true)
	root.set_meta("passage_availability", "host_validation_required")

static func _window_centers(length: float, window_width: float, spacing: float, reserve_center: float) -> Array[float]:
	var result: Array[float] = []
	var margin := 0.85
	if reserve_center > 0.0:
		var inner := reserve_center * 0.5 + 0.6
		var usable := length * 0.5 - margin - inner
		var count := maxi(0, int(floor((usable + spacing) / (window_width + spacing))))
		if count == 0: return result
		var first := inner + (usable - count * window_width - (count - 1) * spacing) * 0.5 + window_width * 0.5
		for side in [-1, 1]:
			for index in range(count):
				result.append(side * (first + index * (window_width + spacing)))
	else:
		var usable := length - margin * 2.0
		var count := maxi(0, int(floor((usable + spacing) / (window_width + spacing))))
		if count == 0: return result
		var first := -length * 0.5 + margin + (usable - count * window_width - (count - 1) * spacing) * 0.5 + window_width * 0.5
		for index in range(count): result.append(first + index * (window_width + spacing))
	return result

static func _wall_band(kit: Node3D, wall: Node3D, detail: Node3D,
		length: float, low: float, high: float, openings: Array[Dictionary],
		plaster: Material, stone: Material, timber: Material, copper: Material,
		glass: Material) -> void:
	var cursor := -length * 0.5
	for opening in openings:
		var left: float = float(opening.x) - float(opening.width) * 0.5
		var right: float = float(opening.x) + float(opening.width) * 0.5
		_solid_span(kit, wall, cursor, left, low, high, plaster, stone)
		_solid_span(kit, wall, left, right, low, float(opening.bottom), plaster, stone)
		_solid_span(kit, wall, left, right, float(opening.top), high, plaster, stone)
		if opening.kind == "window":
			_window(kit, wall, float(opening.x), float(opening.bottom), float(opening.width),
				float(opening.top) - float(opening.bottom), timber, stone, glass)
		else:
			_door_reveal(kit, wall, float(opening.width), float(opening.bottom),
				float(opening.top), timber, stone)
		cursor = right
	_solid_span(kit, wall, cursor, length * 0.5, low, high, plaster, stone)

static func _solid_span(kit: Node3D, wall: Node3D, left: float, right: float,
		bottom: float, top: float, plaster: Material, stone: Material) -> void:
	if right - left < 0.015 or top - bottom < 0.015: return
	var x := (left + right) * 0.5
	kit.box(wall, Vector3(x, (top + bottom) * 0.5, 0),
		Vector3(right - left, top - bottom, WALL_THICKNESS), plaster, "Masonry")
	if bottom < 0.54:
		var cap := minf(top, 0.54)
		kit.box(wall, Vector3(x, (bottom + cap) * 0.5, 0.018),
			Vector3(right - left, cap - bottom, WALL_THICKNESS + 0.045), stone, "StonePlinth")
		var joints := _decoration(wall, "PlinthJoints")
		var joint_mat: Material = kit.material("exterior_mortar_joint", "5e5d58", 2, 0.92)
		if top >= 0.54:
			kit.box(joints, Vector3(x, 0.535, 0.184),
				Vector3(right - left, 0.012, 0.013), joint_mat, "BedJoint")
		var first_joint := int(ceil(left / 0.9))
		var last_joint := int(floor(right / 0.9))
		for index in range(first_joint, last_joint + 1):
			var joint_x := index * 0.9
			if joint_x <= left + 0.08 or joint_x >= right - 0.08: continue
			kit.box(joints, Vector3(joint_x, (bottom + cap) * 0.5, 0.184),
				Vector3(0.012, cap - bottom, 0.013), joint_mat, "HeadJoint")

static func _window(kit: Node3D, wall: Node3D, x: float, bottom: float,
		width: float, height: float, timber: Material, stone: Material,
		glass: Material) -> void:
	# The aperture stays empty through the masonry thickness. The glazing and
	# sash live in a decorative child, recessed behind the outer wall surface.
	var visual := _decoration(wall, "RecessedWindow")
	visual.position.x = x
	var frame_depth := 0.13
	for side in [-1, 1]:
		kit.box(visual, Vector3(side * (width * 0.5 - 0.045), bottom + height * 0.5, 0.01),
			Vector3(0.09, height, frame_depth), timber, "Jamb")
	for y in [bottom + 0.045, bottom + height - 0.045]:
		kit.box(visual, Vector3(0, y, 0.01), Vector3(width, 0.09, frame_depth), timber, "Rail")
	kit.box(visual, Vector3(0, bottom + height * 0.5, -0.08),
		Vector3(width - 0.15, height - 0.15, 0.035), glass, "Glazing")
	kit.box(visual, Vector3(0, bottom + height * 0.5, 0.025),
		Vector3(0.055, height - 0.08, 0.07), timber, "Mullion")
	kit.box(visual, Vector3(0, bottom + height * 0.52, 0.03),
		Vector3(width - 0.09, 0.055, 0.07), timber, "Transom")
	kit.box(visual, Vector3(0, bottom - 0.035, 0.23),
		Vector3(width + 0.28, 0.105, 0.37), stone, "Sill")
	kit.box(visual, Vector3(0, bottom + height + 0.07, 0.12),
		Vector3(width + 0.31, 0.15, 0.31), stone, "Lintel")

static func _door_reveal(kit: Node3D, wall: Node3D, width: float,
		bottom: float, top: float, timber: Material, stone: Material) -> void:
	var trim := _decoration(wall, "DoorSurround")
	for side in [-1, 1]:
		kit.box(trim, Vector3(side * (width * 0.5 + 0.065), (bottom + top) * 0.5, 0.16),
			Vector3(0.13, top - bottom, 0.25), stone, "Jamb")
	kit.box(trim, Vector3(0, top + 0.065, 0.16),
		Vector3(width + 0.28, 0.13, 0.27), stone, "Lintel")
	kit.box(trim, Vector3(0, bottom - 0.025, 0.2),
		Vector3(width + 0.12, 0.05, 0.42), stone, "Threshold")
	trim.set_meta("door_visual_only", true)

static func _roof(kit: Node3D, root: Node3D, detail: Node3D,
		width: float, depth: float, height: float, rise: float,
		plaster: Material, roofing: Material, copper: Material) -> void:
	var roof := Node3D.new()
	roof.name = "PitchedRoof"
	root.add_child(roof)
	var run := width * 0.5 + 0.38
	var angle := atan2(rise, run)
	var slope_length := sqrt(run * run + rise * rise)
	for side in [-1, 1]:
		var plane: MeshInstance3D = kit.box(roof, Vector3(side * run * 0.5, height + rise * 0.5, 0),
			Vector3(slope_length + 0.08, 0.16, depth + 0.75), roofing, "RoofPlane")
		plane.rotation.z = -side * angle
		kit.box(detail, Vector3(side * run, height - 0.035, 0),
			Vector3(0.11, 0.11, depth + 0.76), copper, "Gutter")
	kit.box(detail, Vector3(0, height + rise + 0.08, 0),
		Vector3(0.18, 0.15, depth + 0.83), roofing, "RidgeCap")
	_gable(roof, depth, width, height, rise, plaster)
	for side in [-1, 1]:
		var pipe: MeshInstance3D = kit.cylinder(detail,
			Vector3(side * (width * 0.5 + 0.11), height * 0.48, -depth * 0.5 - 0.12),
			0.045, height * 0.93, copper)
		pipe.name = "Downpipe"
		kit.box(detail, Vector3(side * (width * 0.5 + 0.11), height - 0.08, -depth * 0.5 - 0.12),
			Vector3(0.13, 0.11, 0.13), copper, "GutterOutlet")

static func _gable(parent: Node3D, depth: float, width: float,
		height: float, rise: float, plaster: Material) -> void:
	var mesh := SurfaceTool.new()
	mesh.begin(Mesh.PRIMITIVE_TRIANGLES)
	var half_width := width * 0.5
	var half_depth := depth * 0.5
	var front_left := Vector3(-half_width, height, half_depth)
	var front_right := Vector3(half_width, height, half_depth)
	var front_top := Vector3(0, height + rise, half_depth)
	var back_left := Vector3(-half_width, height, -half_depth)
	var back_right := Vector3(half_width, height, -half_depth)
	var back_top := Vector3(0, height + rise, -half_depth)
	_triangle(mesh, front_left, front_right, front_top)
	_triangle(mesh, back_right, back_left, back_top)
	_quad(mesh, front_left, back_left, back_right, front_right)
	_quad(mesh, front_left, front_top, back_top, back_left)
	_quad(mesh, front_top, front_right, back_right, back_top)
	mesh.generate_normals()
	var instance := MeshInstance3D.new()
	instance.name = "SolidGable"
	instance.mesh = mesh.commit()
	instance.material_override = plaster
	parent.add_child(instance)

static func _triangle(surface: SurfaceTool, a: Vector3, b: Vector3, c: Vector3) -> void:
	surface.add_vertex(a)
	surface.add_vertex(b)
	surface.add_vertex(c)

static func _quad(surface: SurfaceTool, a: Vector3, b: Vector3, c: Vector3, d: Vector3) -> void:
	_triangle(surface, a, b, c)
	_triangle(surface, a, c, d)

static func _corners(kit: Node3D, detail: Node3D, width: float,
		depth: float, height: float, stone: Material) -> void:
	for sx in [-1, 1]:
		for sz in [-1, 1]:
			for course in range(int(ceil(height / 0.72))):
				var bottom := course * 0.72
				var top := minf(height - 0.17, bottom + 0.56)
				if top <= bottom: continue
				kit.box(detail, Vector3(sx * width * 0.5, (bottom + top) * 0.5, sz * depth * 0.5),
					Vector3(0.42, top - bottom, 0.42), stone, "CornerStone")

static func _entry(kit: Node3D, root: Node3D, detail: Node3D,
		width: float, depth: float, door_width: float, door_height: float,
		stone: Material, timber: Material, copper: Material) -> void:
	var entry := Node3D.new()
	entry.name = "EntryMasonry"
	root.add_child(entry)
	var front := depth * 0.5
	var stair_width := minf(width - 0.8, door_width + 0.9)
	kit.box(entry, Vector3(0, PLATFORM_HEIGHT * 0.5, front + 0.62),
		Vector3(stair_width, PLATFORM_HEIGHT, 1.25), stone, "Platform")
	for index in range(3):
		var rise := PLATFORM_HEIGHT * (3.0 - index) / 4.0
		kit.box(entry, Vector3(0, rise * 0.5, front + 1.42 + index * 0.36),
			Vector3(stair_width, rise, 0.36), stone, "Step%d" % (index + 1))
	var leaf := _decoration(detail, "VisibleClosedDoor")
	leaf.set_meta("door_visual_only", true)
	leaf.set_meta("passage_availability", "host_validation_required")
	kit.box(leaf, Vector3(0, PLATFORM_HEIGHT + door_height * 0.5, front + 0.055),
		Vector3(door_width - 0.11, door_height - 0.065, 0.085), timber, "DoorLeaf")
	for y in [PLATFORM_HEIGHT + door_height * 0.29, PLATFORM_HEIGHT + door_height * 0.71]:
		kit.box(leaf, Vector3(0, y, front + 0.106),
			Vector3(door_width - 0.31, 0.065, 0.025), timber, "DoorRail")
	kit.cylinder(leaf, Vector3(door_width * 0.34, PLATFORM_HEIGHT + door_height * 0.47,
		front + 0.13), 0.055, 0.045, copper).rotation.x = PI * 0.5
	var approach := _marker(root, "EntryApproach", Vector3(0, 0, front + 2.7))
	approach.rotation.y = PI
	_marker(root, "Threshold", Vector3(0, PLATFORM_HEIGHT, front + 0.04))
	_marker(root, "Inside", Vector3(0, PLATFORM_HEIGHT, front - 0.78))

static func _decoration(parent: Node3D, label: String) -> Node3D:
	var node := Node3D.new()
	node.name = label
	node.set_meta("world_category", &"decoration")
	parent.add_child(node)
	return node

static func _marker(parent: Node3D, label: String, at: Vector3) -> Marker3D:
	var marker := Marker3D.new()
	marker.name = label
	marker.position = at
	marker.set_meta("availability", "host_validation_required")
	marker.set_meta("geometry_candidate_only", true)
	parent.add_child(marker)
	return marker
