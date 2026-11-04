extends RefCounted
## DV-authored exterior finishes; semantic locations and permissions stay in Core.
const LANDSCAPE = preload("res://scenes/components/world/ExteriorLandscape.gd")
const ARCHITECTURE = preload("res://scenes/components/architecture/ExteriorArchitecture.gd")
const PREFABS = preload("res://scenes/components/prefabs/PrefabLibrary.gd")
const IMPORTED = preload("res://scenes/components/prefabs/ImportedAssets.gd")
const PLASTER = preload("res://assets/materials/surfaces/exterior_plaster.gdshader")
const ROOF = preload("res://assets/materials/surfaces/exterior_roof_tiles.gdshader")
static var _data: Dictionary = {}

static func definition() -> Dictionary:
	if _data.is_empty():
		_data = preload("res://scenes/main/DvPresentationContent.cs").new().ReadRecord("res://assets/data/outdoor_art.dv", "layouts", "exterior_finish")
	return _data

static func handles(id: String) -> bool:
	return definition().get("maps", {}).has(id)

static func build(kit: Node3D, id: String, entry: Dictionary) -> Dictionary:
	var config := definition().duplicate(true)
	config.merge(config.maps[id], true)
	var scale: float = config.units_per_pixel
	var size := Vector2(float(entry.size_px[0]), float(entry.size_px[1])) * scale
	var focus := _point(config.focus_px, size, scale)
	config.focus = Vector2(focus.x, focus.z)
	for landform: Dictionary in config.get("landforms", []):
		landform.center = Vector2(float(landform.center[0]), float(landform.center[1]))
		landform.radius = Vector2(float(landform.radius[0]), float(landform.radius[1]))
	if config.has("terrain_grid"):
		config.terrain_grid.origin = Vector2(float(config.terrain_grid.origin[0]), float(config.terrain_grid.origin[1]))
		config.terrain_grid.step = Vector2(float(config.terrain_grid.step[0]), float(config.terrain_grid.step[1]))
	var routes: Array = []
	for declaration in config.routes:
		var points: Array = []
		for raw in declaration.points_px:
			var p := _point(raw, size, scale)
			points.append(Vector2(p.x, p.z))
		routes.append({"points": points, "width": declaration.width, "kind": declaration.kind})
	config.routes = routes
	var buildings: Array[Rect2] = []
	for item in entry.objects:
		if not item.has("footprint"): continue
		var center := _point(item.footprint.at, size, scale)
		var dimensions := Vector2(float(item.footprint.size[0]), float(item.footprint.size[1])) * scale
		buildings.append(Rect2(Vector2(center.x, center.z) - dimensions * 0.5, dimensions))
	var statistics := LANDSCAPE.build(kit, kit.content_root, size, config, buildings)
	for item in entry.objects:
		if item.representation == "building-3d":
			_building(kit, item, size, scale, config)
		elif item.representation == "vegetation-3d":
			var at := _point(item.at, size, scale)
			at.y = LANDSCAPE.height_at(Vector2(at.x, at.z), config, buildings)
			var height := clampf(float(item.image_size[1]) * scale, 0.65, 6.5)
			var shrub: bool = str(item.texture).contains("hydrangea")
			var tree: Node3D
			if shrub and Vector2(at.x, at.z).distance_to(config.focus) < float(config.grass_radius) + 8.0:
				tree = _detailed_shrub(kit, at, height, str(item.name))
			else:
				tree = IMPORTED.vegetation(kit.content_root, at, height, shrub, str(item.name))
			if tree != null:
				tree.set_meta("source_prop", item.name)
				tree.set_meta("representation", "e-detailed-foliage" if shrub else "approved-cc0-3d")
	# Unsupported old raster art is recorded, rather than substituted with new facts.
	var deferred: Array[String] = []
	for item in entry.objects:
		if item.representation == "retained-raster": deferred.append(str(item.name))
	kit.content_root.set_meta("deferred_legacy_visuals", deferred)
	for prop in config.garden:
		var at := _point(prop.at_px, size, scale)
		at.y = LANDSCAPE.height_at(Vector2(at.x, at.z), config, buildings)
		_garden_prop(kit, prop, at)
	kit.content_root.set_meta("exterior_finish", "e-detail-v1")
	kit.content_root.set_meta("source_layout", "res://assets/data/outdoor_art.dv")
	kit.content_root.set_meta("landscape_statistics", statistics)
	var offset: Array = config.camera_offset
	var target := focus + Vector3(0, float(config.get("camera_target_height", 1.2)), 0)
	var detail: Dictionary = config.get("detail_camera", {})
	var detail_target := _point(detail.get("focus_px", config.focus_px), size, scale) + Vector3(0, float(detail.get("target_height", 0.6)), 0)
	return {"size": size, "camera_size": float(config.camera_size), "camera_target": target,
		"camera_position": target + Vector3(float(offset[0]), float(offset[1]), float(offset[2])),
		"detail_camera_size": float(detail.get("size", 14.0)), "detail_camera_target": detail_target,
		"detail_camera_position": detail_target + Vector3(float(offset[0]), float(offset[1]), float(offset[2])),
		"counts": statistics, "limits": ["Art reconstruction; formal 3D traversal pending", "Unmigrated legacy visuals recorded in scene metadata"], "source": "res://assets/data/outdoor_art.dv"}

static func _point(raw: Array, size: Vector2, scale: float) -> Vector3:
	return Vector3(float(raw[0]) * scale - size.x * 0.5, 0, float(raw[1]) * scale - size.y * 0.5)

static func _building(kit: Node3D, item: Dictionary, size: Vector2, scale: float, config: Dictionary) -> void:
	var dimensions := Vector2(float(item.footprint.size[0]), float(item.footprint.size[1])) * scale
	var spec := {"name": item.name, "width": dimensions.x, "depth": dimensions.y, "height": item.height,
		"storeys": maxi(1, int(round(float(item.height) / 5.0))), "roof_rise": minf(dimensions.x, dimensions.y) * 0.24,
		"wall_color": config.wall_color, "stone_color": config.foundation_color, "wood_color": config.wood_color, "roof_color": config.roof_color,
		"door_width": config.door_width, "door_height": config.door_height, "window_width": config.window_width,
		"window_height": config.window_height, "window_spacing": config.window_spacing}
	var plaster := ShaderMaterial.new()
	plaster.shader = PLASTER
	plaster.set_shader_parameter("pigment", Color(str(config.wall_color)))
	var roof := ShaderMaterial.new()
	roof.shader = ROOF
	roof.set_shader_parameter("pigment", Color(str(config.roof_color)))
	spec.wall_material = plaster
	spec.roof_material = roof
	spec.material_key = "e-exterior-plaster/" + str(config.wall_color) + "/" + str(config.roof_color)
	var model := ARCHITECTURE.build(kit, kit.content_root, spec)
	if model == null: return
	model.position = _point(item.footprint.at, size, scale)
	model.set_meta("source_prop", item.name)
	model.set_meta("source_collision", item.footprint.node)

static func _garden_prop(kit: Node3D, prop: Dictionary, at: Vector3) -> void:
	var kind := str(prop.kind)
	if kind == "bench":
		var first: int = kit.content_root.get_child_count()
		kit._bench(at, float(prop.width))
		if kit.content_root.get_child_count() > first:
			kit.content_root.get_child(first).rotation.y = float(prop.yaw)
		return
	var category: StringName = &"furniture" if kind == "planter" else &"decoration"
	var instance := PREFABS.place_helper(kit, "exterior-garden-v2/" + kind + "/" + str(prop.width), at, kind.capitalize(), category, func(): _garden_geometry(kit, kind, float(prop.width)))
	if instance != null: instance.rotation.y = float(prop.yaw)

static func _garden_geometry(kit: Node3D, kind: String, width: float) -> void:
	var root: Node3D = kit.group(kind.capitalize(), Vector3.ZERO)
	var stone: Material = kit.material("garden_limestone", "999b84", 2, 0.88)
	var metal: Material = kit.material("garden_metal", "444d44", 0, 0.65, 0.55)
	if kind == "planter":
		kit.box(root, Vector3(0, 0.08, 0), Vector3(width + 0.10, 0.16, width + 0.10), stone)
		for side in [-1, 1]:
			kit.box(root, Vector3(side * width * 0.44, 0.34, 0), Vector3(width * 0.12, 0.52, width), stone)
			kit.box(root, Vector3(0, 0.34, side * width * 0.44), Vector3(width, 0.52, width * 0.12), stone)
		kit.box(root, Vector3(0, 0.46, 0), Vector3(width * 0.75, 0.05, width * 0.75), kit.material("planter_soil", "504a37", 2))
		_planter_foliage(kit, root)
	else:
		kit.cylinder(root, Vector3(0, 0.06, 0), 0.16, 0.12, metal)
		kit.cylinder(root, Vector3(0, 0.86, 0), 0.045, 1.66, metal)
		kit.box(root, Vector3(0, 1.80, 0), Vector3(0.28, 0.40, 0.28), kit.material("garden_lamp_glass", "e1d6ad", 0, 0.35))
		for x in [-0.15, 0.15]:
			for z in [-0.15, 0.15]: kit.cylinder(root, Vector3(x, 1.80, z), 0.012, 0.42, metal)
		kit.box(root, Vector3(0, 2.03, 0), Vector3(0.38, 0.06, 0.38), metal)
		kit.cylinder(root, Vector3(0, 2.10, 0), 0.045, 0.10, metal)
		var lamp := OmniLight3D.new()
		lamp.position.y = 1.8
		lamp.light_color = Color("ebd2a6")
		lamp.light_energy = 0.6
		lamp.omni_range = 4.0
		lamp.set_meta("normal_energy", 0.6)
		root.add_child(lamp)

static func _planter_foliage(kit: Node3D, parent: Node3D) -> void:
	var foliage := Node3D.new()
	foliage.name = "LayeredPlantFoliage"
	foliage.set_meta("world_category", &"decoration")
	parent.add_child(foliage)
	var stem: Material = kit.material("garden_plant_stem", "536247", 1, 0.94)
	var leaf_colors := ["536c45", "61794d", "6e8557"]
	for branch in range(7):
		var angle := float(branch) * TAU / 7.0
		var tip := Vector3(cos(angle) * 0.32, 1.02 + sin(angle * 2.0) * 0.10, sin(angle) * 0.32)
		var base := Vector3(0, 0.48, 0)
		kit.limb(foliage, base, tip, 0.009, stem)
		for level in range(5):
			var center := base.lerp(tip, 0.23 + level * 0.145)
			for side in [-1, 1]:
				var at: Vector3 = center + Vector3(-sin(angle), 0.01, cos(angle)) * side * 0.065
				var leaf: MeshInstance3D = kit.ellipsoid(foliage, at, Vector3(0.09, 0.028, 0.20 - level * 0.018), kit.material("garden_leaf_" + str((branch+level)%3), leaf_colors[(branch+level)%3], 3, 0.92))
				leaf.rotation.y = -angle + side * 0.7
				leaf.rotation.z = side * 0.24

static func _detailed_shrub(kit: Node3D, at: Vector3, source_height: float, title: String) -> Node3D:
	var instance := PREFABS.place_helper(kit, "exterior-ground-shrub-v1", at, title, &"decoration", func():
		var foliage := Node3D.new()
		foliage.name = "GroundShrub"
		foliage.position.y = -0.48
		kit.content_root.add_child(foliage)
		_planter_foliage(kit, foliage))
	if instance != null:
		instance.scale = Vector3.ONE * clampf(source_height * 0.55, 0.4, 1.15) / 0.65
	return instance
