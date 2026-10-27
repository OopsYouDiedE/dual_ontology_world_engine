extends RefCounted

const GROUND_SHADER = preload("res://rendering/shaders/surfaces/outdoor_ground.gdshader")
const GRASS_SHADER = preload("res://rendering/shaders/surfaces/community_grass.gdshader")
const PAVING_SHADER = preload("res://rendering/shaders/surfaces/outdoor_paving.gdshader")
const PERSISTENT_MULTI = preload("res://rendering/PersistentMultiMesh.gd")
const PATCH_SIZE := 16.0
const MAX_SHADER_SEGMENTS := 48

## Coordinates in size, buildings, routes and focus are metres in root's local XZ plane.
static func build(kit: Node3D, root: Node3D, size: Vector2, config: Dictionary, buildings: Array[Rect2]) -> Dictionary:
	var segments: Array[Dictionary] = _route_segments(config.get("routes", []))
	var ground_material := ShaderMaterial.new()
	ground_material.shader = GROUND_SHADER
	ground_material.set_shader_parameter("grass_color", _color(config, "grass_color", "586b3b"))
	ground_material.set_shader_parameter("soil_color", _color(config, "soil_color", "504632"))
	ground_material.set_shader_parameter("stone_color", _color(config, "stone_color", "89877a"))
	var shader_routes := PackedVector4Array()
	var shader_widths := PackedFloat32Array()
	for segment in segments:
		if shader_routes.size() >= MAX_SHADER_SEGMENTS:
			break
		var a: Vector2 = segment["a"]
		var b: Vector2 = segment["b"]
		var world_a := root.global_transform * Vector3(a.x, 0.0, a.y)
		var world_b := root.global_transform * Vector3(b.x, 0.0, b.y)
		shader_routes.append(Vector4(world_a.x, world_a.z, world_b.x, world_b.z))
		shader_widths.append(segment["width"])
	var active_route_count := shader_routes.size()
	while shader_routes.size() < MAX_SHADER_SEGMENTS:
		shader_routes.append(Vector4.ZERO)
		shader_widths.append(0.0)
	ground_material.set_shader_parameter("route_count", active_route_count)
	ground_material.set_shader_parameter("routes", shader_routes)
	ground_material.set_shader_parameter("route_widths", shader_widths)

	var terrain := _terrain_mesh(size, config, buildings)
	var ground := MeshInstance3D.new()
	ground.name = "ContinuousGround"
	ground.mesh = terrain["mesh"]
	ground.material_override = ground_material
	root.add_child(ground)

	var floor_body := StaticBody3D.new()
	floor_body.name = "LandscapeFloor"
	floor_body.collision_layer = 1
	floor_body.collision_mask = 0
	floor_body.set_meta("collision_category", "floor")
	root.add_child(floor_body)
	var floor_shape := CollisionShape3D.new()
	var floor_surface := ConcavePolygonShape3D.new()
	floor_surface.backface_collision = true
	floor_surface.set_faces(terrain["faces"])
	floor_shape.shape = floor_surface
	floor_body.add_child(floor_shape)

	_build_paving(root, segments, _color(config, "stone_color", "89877a"), config, buildings)
	var grass_count := _build_grass(root, size, config, buildings, segments)
	return {"grass_instances": grass_count, "route_segments": segments.size()}

## terrain_grid.heights is indexed [z][x], with origin and step in local XZ metres.
static func height_at(point: Vector2, config: Dictionary, buildings: Array[Rect2]) -> float:
	var grid: Dictionary = config.get("terrain_grid", {})
	var raw_rows: Array = grid.get("heights", [])
	if raw_rows.is_empty() or raw_rows[0].size() == 0:
		return 0.0
	var origin: Vector2 = grid.get("origin", Vector2.ZERO)
	var spacing: Vector2 = grid.get("step", Vector2.ONE)
	var x := clampf((point.x - origin.x) / maxf(absf(spacing.x), 0.001), 0.0, float(raw_rows[0].size() - 1))
	var z := clampf((point.y - origin.y) / maxf(absf(spacing.y), 0.001), 0.0, float(raw_rows.size() - 1))
	var x0 := floori(x)
	var z0 := floori(z)
	var x1 := mini(x0 + 1, raw_rows[0].size() - 1)
	var z1 := mini(z0 + 1, raw_rows.size() - 1)
	var tx := x - float(x0)
	var tz := z - float(z0)
	var h0 := lerpf(float(raw_rows[z0][x0]), float(raw_rows[z0][x1]), tx)
	var h1 := lerpf(float(raw_rows[z1][x0]), float(raw_rows[z1][x1]), tx)
	var height := lerpf(h0, h1, tz)
	for landform: Dictionary in config.get("landforms", []):
		var center: Vector2 = landform.center
		var radius: Vector2 = landform.radius
		var local := (point - center) / radius
		var falloff := 1.0 - smoothstep(0.0, 1.0, local.length())
		height += float(landform.height) * falloff
	for building in buildings:
		var footprint := building.grow(0.9)
		if footprint.has_point(point):
			return 0.0
		var closest := Vector2(clampf(point.x, footprint.position.x, footprint.end.x), clampf(point.y, footprint.position.y, footprint.end.y))
		height *= smoothstep(0.0, 2.0, point.distance_to(closest))
	return height

static func _terrain_mesh(size: Vector2, config: Dictionary, buildings: Array[Rect2]) -> Dictionary:
	var grid_spacing := maxf(0.25, float(config.get("terrain_mesh_spacing", 0.75)))
	var nx := maxi(1, ceili(size.x / grid_spacing))
	var nz := maxi(1, ceili(size.y / grid_spacing))
	var positions := PackedVector3Array()
	var normals := PackedVector3Array()
	var uvs := PackedVector2Array()
	var indices := PackedInt32Array()
	var faces := PackedVector3Array()
	for z in range(nz + 1):
		for x in range(nx + 1):
			var p := Vector2(-size.x * 0.5 + size.x * float(x) / float(nx), -size.y * 0.5 + size.y * float(z) / float(nz))
			positions.append(Vector3(p.x, height_at(p, config, buildings), p.y))
			normals.append(Vector3.ZERO)
			uvs.append(Vector2(float(x) / float(nx), float(z) / float(nz)))
	for z in range(nz):
		for x in range(nx):
			var a := z * (nx + 1) + x
			var b := a + 1
			var c := a + nx + 1
			var d := c + 1
			for tri in [PackedInt32Array([a, c, b]), PackedInt32Array([b, c, d])]:
				indices.append_array(PackedInt32Array([tri[0], tri[2], tri[1]]))
				var va: Vector3 = positions[tri[0]]
				var vb: Vector3 = positions[tri[1]]
				var vc: Vector3 = positions[tri[2]]
				var n := (vb - va).cross(vc - va)
				for index in tri:
					normals[index] = normals[index] + n
				faces.append_array(PackedVector3Array([va, vc, vb]))
	for i in range(normals.size()):
		normals[i] = normals[i].normalized()
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = positions
	arrays[Mesh.ARRAY_NORMAL] = normals
	arrays[Mesh.ARRAY_TEX_UV] = uvs
	arrays[Mesh.ARRAY_INDEX] = indices
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	return {"mesh": mesh, "faces": faces}

static func _route_segments(routes: Array) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	for route in routes:
		if not route is Dictionary:
			continue
		var points: Array = route.get("points", [])
		var width := maxf(float(route.get("width", 1.0)), 0.05)
		var kind := str(route.get("kind", "soil"))
		for i in range(points.size() - 1):
			var a: Vector2 = points[i]
			var b: Vector2 = points[i + 1]
			if a.distance_squared_to(b) > 0.0001:
				result.append({"a": a, "b": b, "width": width, "kind": kind})
	return result

static func _build_paving(root: Node3D, segments: Array[Dictionary], stone_color: Color, config: Dictionary, buildings: Array[Rect2]) -> void:
	var paving_material := ShaderMaterial.new()
	paving_material.shader = PAVING_SHADER
	paving_material.set_shader_parameter("stone_color", stone_color)
	var curb_material := StandardMaterial3D.new()
	curb_material.albedo_color = stone_color.darkened(0.13)
	curb_material.roughness = 0.96
	var curb_mesh := BoxMesh.new()
	curb_mesh.size = Vector3(0.11, 0.09, 0.47)
	curb_mesh.material = curb_material
	var curb_transforms: Array[Transform3D] = []
	for segment in segments:
		if segment["kind"] != "paving":
			continue
		var a: Vector2 = segment["a"]
		var b: Vector2 = segment["b"]
		var delta := b - a
		var length := delta.length()
		var paving := MeshInstance3D.new()
		paving.name = "PavingSegment"
		paving.mesh = _paving_mesh(a, b, float(segment["width"]), config, buildings)
		paving.material_override = paving_material
		root.add_child(paving)
		var direction := delta / length
		var across := Vector2(direction.y, -direction.x)
		var blocks := maxi(1, ceili(length / 0.5))
		for side in [-1.0, 1.0]:
			for i in range(blocks):
				var along := minf(length, (float(i) + 0.5) * length / float(blocks))
				var p: Vector2 = a + direction * along + across * side * (float(segment["width"]) * 0.5 + 0.055)
				var ahead: Vector2 = a + direction * minf(length, along + 0.23) + across * side * (float(segment["width"]) * 0.5 + 0.055)
				var behind: Vector2 = a + direction * maxf(0.0, along - 0.23) + across * side * (float(segment["width"]) * 0.5 + 0.055)
				var tangent := Vector3(ahead.x - behind.x, height_at(ahead, config, buildings) - height_at(behind, config, buildings), ahead.y - behind.y).normalized()
				var sideways := Vector3(across.x, 0.0, across.y)
				var upward := tangent.cross(sideways).normalized()
				var basis := Basis(sideways, upward, tangent)
				curb_transforms.append(Transform3D(basis, Vector3(p.x, height_at(p, config, buildings) + 0.045, p.y)))
	if curb_transforms.is_empty():
		return
	var multi := MultiMesh.new()
	multi.transform_format = MultiMesh.TRANSFORM_3D
	multi.mesh = curb_mesh
	multi.instance_count = curb_transforms.size()
	for i in range(curb_transforms.size()):
		multi.set_instance_transform(i, curb_transforms[i])
	var curbs := PERSISTENT_MULTI.new()
	curbs.name = "PavingStoneEdges"
	curbs.multimesh = multi
	curbs.saved_transforms = curb_transforms
	root.add_child(curbs)

static func _paving_mesh(a: Vector2, b: Vector2, width: float, config: Dictionary, buildings: Array[Rect2]) -> ArrayMesh:
	var length := a.distance_to(b)
	var subdivisions := maxi(1, ceili(length / 0.5))
	var direction := (b - a) / length
	var across := Vector2(direction.y, -direction.x)
	var vertices := PackedVector3Array()
	var normals := PackedVector3Array()
	var uvs := PackedVector2Array()
	var indices := PackedInt32Array()
	for i in range(subdivisions + 1):
		var center := a.lerp(b, float(i) / float(subdivisions))
		for side in [-1.0, 1.0]:
			var p: Vector2 = center + across * side * width * 0.5
			vertices.append(Vector3(p.x, height_at(p, config, buildings) + 0.012, p.y))
			normals.append(Vector3.ZERO)
			uvs.append(Vector2((side + 1.0) * 0.5, float(i) / float(subdivisions)))
	for i in range(subdivisions):
		var j := i * 2
		var tri := PackedInt32Array([j, j + 2, j + 1, j + 1, j + 2, j + 3])
		indices.append_array(PackedInt32Array([tri[0],tri[2],tri[1],tri[3],tri[5],tri[4]]))
		for k in range(0, 6, 3):
			var p0: Vector3 = vertices[tri[k]]
			var p1: Vector3 = vertices[tri[k + 1]]
			var p2: Vector3 = vertices[tri[k + 2]]
			var n := (p1 - p0).cross(p2 - p0)
			for t in range(3):
				var index := tri[k + t]
				normals[index] = normals[index] + n
	for i in range(normals.size()):
		normals[i] = normals[i].normalized()
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = vertices
	arrays[Mesh.ARRAY_NORMAL] = normals
	arrays[Mesh.ARRAY_TEX_UV] = uvs
	arrays[Mesh.ARRAY_INDEX] = indices
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	return mesh

static func _build_grass(root: Node3D, size: Vector2, config: Dictionary, buildings: Array[Rect2], segments: Array[Dictionary]) -> int:
	var density := maxf(float(config.get("grass_density", 7.0)), 0.0)
	if density <= 0.0:
		return 0
	var height_range: Array = config.get("grass_height", [0.12, 0.22])
	var min_height := maxf(float(height_range[0]), 0.01)
	var max_height := maxf(float(height_range[1]), min_height)
	var radius := maxf(float(config.get("grass_radius", 28.0)), 0.0)
	var focus: Vector2 = config.get("focus", Vector2.ZERO)
	var seed_value := int(config.get("seed", 1))
	var grass_material := ShaderMaterial.new()
	grass_material.shader = GRASS_SHADER
	grass_material.set_shader_parameter("top_color", _color(config, "top_color", "819355"))
	grass_material.set_shader_parameter("bottom_color", _color(config, "bottom_color", "40542b"))
	var wind_noise := FastNoiseLite.new()
	wind_noise.seed = seed_value
	wind_noise.frequency = 0.035
	var wind_texture := NoiseTexture2D.new()
	wind_texture.width = 128
	wind_texture.height = 128
	wind_texture.seamless = true
	wind_texture.noise = wind_noise
	grass_material.set_shader_parameter("wind_noise", wind_texture)
	grass_material.set_shader_parameter("wind_strength", float(config.get("wind_strength", 0.045)))
	grass_material.set_shader_parameter("wind_noise_size", float(config.get("wind_noise_size", 0.075)))
	grass_material.set_shader_parameter("wind_noise_speed", float(config.get("wind_noise_speed", 0.035)))
	var blade_mesh := _grass_mesh()
	blade_mesh.surface_set_material(0, grass_material)
	var step := sqrt(1.0 / density)
	var count := 0
	var x_min := maxi(floori(-size.x * 0.5 / PATCH_SIZE), floori((focus.x - radius) / PATCH_SIZE))
	var x_max := mini(ceili(size.x * 0.5 / PATCH_SIZE), ceili((focus.x + radius) / PATCH_SIZE))
	var z_min := maxi(floori(-size.y * 0.5 / PATCH_SIZE), floori((focus.y - radius) / PATCH_SIZE))
	var z_max := mini(ceili(size.y * 0.5 / PATCH_SIZE), ceili((focus.y + radius) / PATCH_SIZE))
	for px in range(x_min, x_max):
		for pz in range(z_min, z_max):
			var transforms: Array[Transform3D] = []
			var tints: Array[Color] = []
			var lowest := INF
			var highest := -INF
			var rng := RandomNumberGenerator.new()
			rng.seed = absi(seed_value + px * 92821 + pz * 68917)
			var cell_x_min := ceili(float(px) * PATCH_SIZE / step)
			var cell_x_max := ceili(float(px + 1) * PATCH_SIZE / step)
			var cell_z_min := ceili(float(pz) * PATCH_SIZE / step)
			var cell_z_max := ceili(float(pz + 1) * PATCH_SIZE / step)
			for gx in range(cell_x_min, cell_x_max):
				for gz in range(cell_z_min, cell_z_max):
					var p := Vector2((float(gx) + rng.randf()) * step, (float(gz) + rng.randf()) * step)
					if p.x < -size.x * 0.5 or p.x > size.x * 0.5 or p.y < -size.y * 0.5 or p.y > size.y * 0.5:
						continue
					var distance := p.distance_to(focus)
					if distance >= radius:
						continue
					var acceptance := 1.0 - smoothstep(maxf(0.0, radius - 3.0), radius, distance)
					acceptance *= _grass_clearance(p, size, buildings, segments)
					if rng.randf() > acceptance:
						continue
					var height := rng.randf_range(min_height, max_height)
					var terrain_height := height_at(p, config, buildings)
					var rotation := Basis(Vector3.UP, rng.randf_range(0.0, TAU))
					transforms.append(Transform3D(rotation.scaled(Vector3.ONE * height), Vector3(p.x, terrain_height, p.y)))
					tints.append(Color(rng.randf(), 0.0, 0.0, 1.0))
					lowest = minf(lowest, terrain_height)
					highest = maxf(highest, terrain_height + height)
			if transforms.is_empty():
				continue
			var multi := MultiMesh.new()
			multi.transform_format = MultiMesh.TRANSFORM_3D
			multi.use_custom_data = true
			multi.mesh = blade_mesh
			multi.instance_count = transforms.size()
			for i in range(transforms.size()):
				multi.set_instance_transform(i, transforms[i])
				multi.set_instance_custom_data(i, tints[i])
			var patch := PERSISTENT_MULTI.new()
			patch.name = "GrassPatch_%d_%d" % [px, pz]
			patch.multimesh = multi
			patch.saved_transforms = transforms
			patch.saved_custom_data = PackedColorArray(tints)
			patch.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
			patch.custom_aabb = AABB(Vector3(px * PATCH_SIZE - 0.2, lowest - 0.15, pz * PATCH_SIZE - 0.2), Vector3(PATCH_SIZE + 0.4, highest - lowest + 0.35, PATCH_SIZE + 0.4))
			root.add_child(patch)
			count += transforms.size()
	return count

static func _grass_clearance(p: Vector2, size: Vector2, buildings: Array[Rect2], segments: Array[Dictionary]) -> float:
	var clearance := minf(minf(p.x + size.x * 0.5, size.x * 0.5 - p.x), minf(p.y + size.y * 0.5, size.y * 0.5 - p.y))
	var acceptance := smoothstep(0.0, 0.6, clearance)
	for building in buildings:
		var area := building.grow(0.65)
		if area.has_point(p):
			return 0.0
		var nearest := Vector2(clampf(p.x, area.position.x, area.end.x), clampf(p.y, area.position.y, area.end.y))
		acceptance = minf(acceptance, smoothstep(0.0, 0.4, p.distance_to(nearest)))
	for segment in segments:
		var a: Vector2 = segment["a"]
		var b: Vector2 = segment["b"]
		var t := clampf((p - a).dot(b - a) / maxf(a.distance_squared_to(b), 0.0001), 0.0, 1.0)
		var edge_distance := p.distance_to(a.lerp(b, t)) - float(segment["width"]) * 0.5 - 0.05
		if edge_distance <= 0.0:
			return 0.0
		acceptance = minf(acceptance, smoothstep(0.0, 0.4, edge_distance))
	return acceptance

static func _grass_mesh() -> ArrayMesh:
	var vertices := PackedVector3Array()
	var normals := PackedVector3Array()
	var uvs := PackedVector2Array()
	var indices := PackedInt32Array()
	for leaf in range(4):
		var angle := float(leaf) * TAU / 4.0 + 0.18
		var outward := Vector3(cos(angle), 0.0, sin(angle))
		var sideways := Vector3(-outward.z, 0.0, outward.x)
		for level in range(5):
			var t := float(level) / 4.0
			var center := outward * (0.05 + 0.22 * t * t) + Vector3.UP * t
			var half_width := (0.075 * (1.0 - t) + 0.002) * (0.6 + 0.4 * sin(t * PI))
			for side in [-1.0, 1.0]:
				vertices.append(center + sideways * half_width * side)
				normals.append(outward)
				uvs.append(Vector2((side + 1.0) * 0.5, 1.0 - t))
		for level in range(4):
			var base := leaf * 10 + level * 2
			indices.append_array(PackedInt32Array([base, base + 2, base + 1, base + 1, base + 2, base + 3]))
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = vertices
	arrays[Mesh.ARRAY_NORMAL] = normals
	arrays[Mesh.ARRAY_TEX_UV] = uvs
	arrays[Mesh.ARRAY_INDEX] = indices
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	return mesh

static func _color(config: Dictionary, key: String, fallback: String) -> Color:
	return Color(str(config.get(key, fallback)))
