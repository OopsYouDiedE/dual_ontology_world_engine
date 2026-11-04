extends Node3D
## Owns geometry, room clipping and objective aperture navigation. Main supplies ports and projections.
const GEOMETRY = preload("res://scenes/components/maps/GeometryCatalog.gd")
const DV_MAP = preload("res://scenes/components/world/LegacyDvMap.gd")
const DV_BUILDING = preload("res://scenes/components/architecture/BuildingFramework.gd")
var map_id := ""
var execution_only := false
var definitions: Dictionary = {}
var rooms: Dictionary = {}
var _dv_buildings: Dictionary = {}
var _legacy_definitions_loaded := false
var _surface_records: Array[Dictionary] = []
var _registered_roots: Dictionary = {}
var _links: Dictionary = {}
var _opening_specs: Dictionary = {}
var _observer_rects := PackedVector4Array()
var _shell_rects := PackedVector4Array()
var _slices := PackedVector4Array()
var _slice_axes := PackedVector2Array()
var _slice_heights := PackedVector2Array()
var _observer := Vector3.ZERO
var _floor_y := 0.0
var _deduction := false
var _shader_cache: Dictionary = {}
var _wall_margin := 0.0
var _opening_normals: Dictionary = {}
var _wall_axes: Dictionary = {}
var _opening_owners: Dictionary = {}
var _cutaway_height := 1.15
var _cap_floor := INF
var _cap_nodes: Array[MeshInstance3D] = []
var _boundary_cap_cache: Dictionary = {}
var _boundary_cap_key := ""
var _object_bounds: Dictionary = {}
var _moving_solid_records: Dictionary = {}
var _opening_roots: Dictionary = {}
var _opening_maps: Dictionary = {}
var _transition_maps: Array[String] = []
var _authored_regions: Array[Rect2] = []
var _material_view: Dictionary = {}
var material_parameter_writes := 0
var material_view_changed_parameters := 0
var visible_surface_count := 0
var _visible_surfaces: Array[Dictionary] = []
var _culling_key := ""
var _culling_dirty := true
var _culled_surface_count := 0
var _eye_image := Image.create(1, 1, false, Image.FORMAT_RGBAF)
var _eye_texture := ImageTexture.create_from_image(_eye_image)
var _published_eye := Vector3.INF
var observer_texture_updates := 0
var _observer_region_key := ""
var _observer_openings: Array[Dictionary] = []
var _observer_door_states: Dictionary = {}
var observer_update_us := 0
var observer_update_max_us := 0
var _cap_jobs: Array[Dictionary] = []
var _cap_generation := 0
var _horizontal_cap_seen: Dictionary = {}
var cap_build_max_us := 0

static var _prepared_room_resources: Dictionary = {}
static var room_load_max_us := 0
static var room_instantiate_max_us := 0
static var room_registration_max_us := 0

static func prepare_room_resources(source_map: String, identities: Array, tree: SceneTree, cancelled: Callable) -> bool:
	if DV_MAP.supports(source_map): return true
	var reader = preload("res://scenes/main/DvPresentationContent.cs").new()
	var legacy: Dictionary = reader.ReadRecord("res://assets/data/world3d.dv", "layouts", "world3d")
	var paths: Array[String] = []
	for identity: String in identities:
		var path := str(legacy.get("rooms", {}).get(identity, {}).get("scene", ""))
		if not path.is_empty() and path not in paths: paths.append(path)
	if paths.is_empty(): return true
	var resources: Dictionary = await preload("res://scenes/components/world/FieldResources.gd").wait_for(paths, tree, cancelled)
	if resources.is_empty(): return false
	_prepared_room_resources.merge(resources, true)
	while _prepared_room_resources.size() > 16: _prepared_room_resources.erase(_prepared_room_resources.keys()[0])
	return not cancelled.call()

func prepare_rooms(identities: Array, cancelled: Callable) -> bool:
	if not await prepare_room_resources(map_id, identities, get_tree(), cancelled): return false
	for identity in identities:
		if cancelled.call(): return false
		if rooms.has(identity): continue
		if ensure_room(identity).is_empty(): return false
		await get_tree().physics_frame
	return not cancelled.call()

func _ensure_legacy_definitions() -> void:
	if _legacy_definitions_loaded: return
	_legacy_definitions_loaded = true
	var reader = preload("res://scenes/main/DvPresentationContent.cs").new()
	var legacy: Dictionary = reader.ReadRecord("res://assets/data/world3d.dv", "layouts", "world3d")
	if legacy.is_empty() or not legacy.has("rooms"):
		push_error("Legacy room catalogue is unavailable for a non-DV map.")
		return
	definitions = legacy

func ensure_room(identity: String) -> Dictionary:
	if rooms.has(identity): return rooms[identity]
	if DV_MAP.supports(map_id):
		return _ensure_dv_room(identity)
	_ensure_legacy_definitions()
	var spec: Dictionary = definitions.get("rooms", {}).get(identity, {})
	if spec.is_empty(): return {}
	var measured := Time.get_ticks_usec()
	var packed := load(str(spec.scene)) as PackedScene
	room_load_max_us = maxi(room_load_max_us, Time.get_ticks_usec()-measured)
	if packed == null: return {}
	measured = Time.get_ticks_usec()
	var scene := packed.instantiate() as Node3D
	room_instantiate_max_us = maxi(room_instantiate_max_us, Time.get_ticks_usec()-measured)
	if scene == null: return {}
	var authored_environment := scene.get_node_or_null("WorldEnvironment") as WorldEnvironment
	if authored_environment != null and authored_environment.environment != null:
		scene.set_meta("presentation_environment", authored_environment.environment.duplicate(true))
	# Each room retains its metre-scale authored geometry in an isolated physical zone.
	scene.position = Vector3((rooms.size() % 8) * 512.0, 0, (rooms.size() / 8) * 512.0)
	if bool(spec.get("mirror_x", false)): scene.scale.x = -1.0
	_strip_presentation(scene)
	add_child(scene)
	if bool(spec.get("mirror_x", false)): _bake_mirrored_collisions(scene)
	var floor_bounds: Array[AABB] = []
	_floor_bounds(scene, Transform3D.IDENTITY, floor_bounds)
	var footprint := Rect2(-4,-4,8,8)
	if not floor_bounds.is_empty():
		var bounds: AABB = floor_bounds[0]
		for item in floor_bounds: bounds = bounds.merge(item)
		footprint = Rect2(bounds.position.x - scene.position.x, bounds.position.z - scene.position.z, bounds.size.x, bounds.size.z)
	var record := {"root":scene,"footprint":footprint,"grid":null}
	rooms[identity] = record
	measured = Time.get_ticks_usec()
	_register_root(record.root)
	room_registration_max_us = maxi(room_registration_max_us, Time.get_ticks_usec()-measured)
	scene.visible = false
	return record

# Physics bodies must not inherit a reflection. Preserve every shape's world
# geometry while moving the reflection into its local shape coordinates.
func _bake_mirrored_collisions(root: Node3D) -> void:
	for body: CollisionObject3D in root.find_children("*", "CollisionObject3D", true, false):
		var transform := body.global_transform
		if transform.basis.determinant() >= 0.0: continue
		var shapes: Array[Dictionary] = []
		for child in body.get_children():
			if child is CollisionShape3D and child.shape != null:
				shapes.append({"node":child, "transform":child.global_transform})
		transform.basis.x = -transform.basis.x
		body.top_level = true
		body.global_transform = transform
		for entry: Dictionary in shapes:
			var node: CollisionShape3D = entry.node
			var shape_transform: Transform3D = entry.transform
			if shape_transform.basis.determinant() < 0.0:
				shape_transform.basis.x = -shape_transform.basis.x
				if node.shape is ConvexPolygonShape3D:
					var copy := node.shape.duplicate() as ConvexPolygonShape3D
					var points := copy.points
					for index in points.size(): points[index].x = -points[index].x
					copy.points = points
					node.shape = copy
				elif node.shape is ConcavePolygonShape3D:
					var copy := node.shape.duplicate() as ConcavePolygonShape3D
					var faces := copy.get_faces()
					for index in faces.size(): faces[index].x = -faces[index].x
					for index in range(0, faces.size(), 3):
						var swap := faces[index+1]
						faces[index+1] = faces[index+2]
						faces[index+2] = swap
					copy.set_faces(faces)
					node.shape = copy
				# Box, capsule, sphere and cylinder are symmetric about local X.
			node.global_transform = shape_transform

func navigation_geometry_key(identity: String) -> String:
	var root: Node3D = rooms[identity].root
	var values: Array = [root.get_instance_id(), _world_projection_identity.get("content_hash", "")]
	for building in _dv_buildings.values():
		for door in building.study_doors.values():
			values.append([door.global_transform, door.current_angle])
	return str(values)

func _ensure_dv_room(identity: String, requested_map: String = "") -> Dictionary:
	var source_map := map_id if requested_map.is_empty() else requested_map
	var spec: Dictionary = GEOMETRY.room_data(source_map, identity)
	if spec.is_empty(): return {}
	var building: Node3D = _dv_buildings.get(source_map)
	if not is_instance_valid(building):
		var source: Dictionary = DV_MAP.spatial_source(source_map)
		_cutaway_height = float(source.get("host_visual_profile", {}).get("cutaway_height_m", 1.15))
		var selected: Dictionary = DV_MAP.spatial_floor(source_map)
		if source.is_empty() or selected.is_empty(): return {}
		# Each legacy input map receives exactly its DV-authored floor,
		# with no fallback to the old per-room PackedScenes.
		var published: Dictionary = source.duplicate(false)
		published["floors"] = [selected]
		building = DV_BUILDING.new()
		building.name = "DvBuilding_" + source_map
		building.set_meta("storey_elevation",float(selected.z))
		add_child(building)
		building.visible = false
		building.call("build", published)
		_register_openings(selected, source_map, building)
		if not bool(building.get("build_completed")):
			push_error("Selected DV geometry failed to build in legacy Godot host: " + map_id)
			building.queue_free()
			return {}
		_dv_buildings[source_map] = building
	var raw := GEOMETRY.rect(spec.rect)
	var a: Vector3 = DV_MAP.pixel_to_godot(source_map, raw.position)
	var b: Vector3 = DV_MAP.pixel_to_godot(source_map, raw.end)
	var footprint := Rect2(Vector2(minf(a.x,b.x),minf(a.z,b.z)),
		Vector2(absf(b.x-a.x),absf(b.z-a.z)))
	var record := {"root":building,"footprint":footprint,"grid":null,"dv_authored":true,"map_id":source_map}
	rooms[identity] = record
	_register_root(record.root)
	return record

func prepare_floor_connection(connection: Dictionary) -> bool:
	for side: String in ["from", "to"]:
		var target_map := str(connection[side+"_map"])
		var target_room := str(connection[side+"_room"])
		if not DV_MAP.supports(target_map) or _ensure_dv_room(target_room,target_map).is_empty(): return false
	_transition_maps.assign([str(connection.from_map),str(connection.to_map)])
	return true

func finish_floor_connection() -> void:
	_transition_maps.clear()

## Task 4.11: Release visual world resources for inactive room while keeping Core state intact
func _strip_presentation(node: Node) -> void:
	for child in node.get_children():
		if child is Camera3D or child is WorldEnvironment or str(child.name) in ["CameraOcclusion", "RenderDetailController"] or child.has_meta("actor_id"):
			node.remove_child(child)
			child.free()
		else: _strip_presentation(child)

func _floor_bounds(node: Node, inherited: Transform3D, result: Array[AABB]) -> void:
	var transform := inherited
	if node is Node3D: transform = inherited * node.transform
	if node is MeshInstance3D and node.mesh != null:
		var bounds: AABB = transform * node.get_aabb()
		if bounds.size.x > 1.0 and bounds.size.z > 1.0 and bounds.position.y < 0.08 and bounds.end.y < 0.2:
			result.append(bounds)
	if node is GridMap and "floor" in str(node.name).to_lower() and node.mesh_library != null:
		for cell in node.get_used_cells():
			var item: int = node.get_cell_item(cell)
			var mesh: Mesh = node.mesh_library.get_item_mesh(item)
			if mesh != null:
				result.append(transform * Transform3D(Basis.IDENTITY,node.map_to_local(cell)) * node.mesh_library.get_item_mesh_transform(item) * mesh.get_aabb())
	for child in node.get_children(): _floor_bounds(child, transform, result)


func _register_root(root: Node3D) -> void:
	if _registered_roots.has(root.get_instance_id()): return
	_registered_roots[root.get_instance_id()] = true
	if execution_only: return # Physics/interaction geometry is retained; no observer materials or cutaway meshes.
	_cap_floor = INF
	for mesh: MeshInstance3D in root.find_children("*", "MeshInstance3D", true, false):
		assert(mesh.mesh != null, "Architectural mesh has no geometry")
		var kind := str(mesh.get_meta("kind", mesh.get_parent().get_meta("kind", "")))
		for surface in range(mesh.mesh.get_surface_count()):
			var original := mesh.get_active_material(surface)
			assert(original != null, "Architectural surface has no material")
			var clipped := _clip_material(original)
			clipped.set_shader_parameter("manuscript_storey_base",float(root.get_meta("storey_elevation",-1000000.0)))
			clipped.set_shader_parameter("manuscript_wall", kind in ["wall", "wall_lower", "wall_upper", "wall_component"] or _has_wall_owner(mesh))
			clipped.set_shader_parameter("manuscript_structure", _has_boundary_owner(mesh))
			clipped.set_shader_parameter("manuscript_slice_floor", kind == "floor")
			var solid_owner := _solid_owner(mesh)
			var bounds_key := solid_owner.get_instance_id()
			if not _object_bounds.has(bounds_key):
				var bounds: AABB = mesh.global_transform * mesh.get_aabb()
				for part: MeshInstance3D in solid_owner.find_children("*", "MeshInstance3D", true, false):
					if part.mesh != null: bounds = bounds.merge(part.global_transform * part.get_aabb())
				_object_bounds[bounds_key] = bounds
			var solid_bounds: AABB = _object_bounds[bounds_key]
			clipped.set_shader_parameter("manuscript_object_min", solid_bounds.position)
			clipped.set_shader_parameter("manuscript_object_max", solid_bounds.end)
			if str(solid_owner.get_meta("kind", "")) == "furniture":
				if not _moving_solid_records.has(bounds_key):
					_moving_solid_records[bounds_key] = {"owner":weakref(solid_owner),"transform":solid_owner.global_transform,"materials":[]}
				_moving_solid_records[bounds_key].materials.append(clipped)
			# Static room boundaries can span a purely semantic open-floor division.
			# Furnishings and moving door leaves still use the observer's own region.
			clipped.set_shader_parameter("manuscript_static_shell", kind in ["floor", "wall", "wall_lower", "wall_upper", "wall_component", "ceiling", "roof", "door_frame"] or _has_wall_owner(mesh) or _has_static_frame_owner(mesh))
			var extent: Vector3 = (mesh.global_transform * mesh.get_aabb()).size
			var measured_axis := _authored_wall_axis(mesh) if _has_wall_owner(mesh) else (Vector2.RIGHT if extent.x >= extent.z else Vector2.DOWN)
			var wall_axis: Vector2 = _wall_axes.get(str(mesh.get_meta("world_object_id", "")), measured_axis)
			var thickness := extent.z if absf(wall_axis.x)>0.7 else extent.x
			if _has_wall_owner(mesh) and thickness <= 1.0: _wall_margin = maxf(_wall_margin,thickness+0.04)
			clipped.set_shader_parameter("manuscript_wall_margin", thickness + 0.04 if _has_wall_owner(mesh) else _wall_margin)
			clipped.set_shader_parameter("manuscript_wall_axis", wall_axis)
			var doorway := _doorway_owner(mesh)
			var opening_spec: Dictionary = _opening_owners.get(str(doorway.get_meta("world_object_id", "")), {}) if doorway != null else {}
			clipped.set_shader_parameter("manuscript_doorway", not opening_spec.is_empty())
			if not opening_spec.is_empty():
				var opening_rect: Array = opening_spec.rect
				var anchor := Vector3((opening_rect[0] + opening_rect[2]) * 0.5, float(opening_spec.bottom), -(opening_rect[1] + opening_rect[3]) * 0.5)
				var normal: Vector2 = _opening_normals.get(str(opening_spec.id), Vector2.RIGHT)
				clipped.set_shader_parameter("manuscript_doorway_anchor", anchor)
				clipped.set_shader_parameter("manuscript_wall_axis", Vector2(-normal.y, normal.x))
				clipped.set_shader_parameter("manuscript_wall_margin", minf(float(opening_rect[2]) - float(opening_rect[0]), float(opening_rect[3]) - float(opening_rect[1])) + 0.04)
			clipped.set_shader_parameter("manuscript_ceiling", kind in ["ceiling", "roof"] or _has_ceiling_owner(mesh))
			mesh.set_surface_override_material(surface, clipped)
			_surface_records.append({"mesh": weakref(mesh), "surface": surface, "material": clipped, "original": original})
		mesh.material_override = null

func _has_wall_owner(node: Node) -> bool:
	while node != self:
		if node.has_meta("wall_component"): return true
		if str(node.get_meta("kind", "")) in ["wall", "wall_lower", "wall_upper", "wall_component"]: return true
		node = node.get_parent()
	return false

func _solid_owner(mesh: MeshInstance3D) -> Node3D:
	var node: Node = mesh
	while node != self:
		if str(node.get_meta("kind", "")) in ["furniture", "door_panel", "door_frame", "stairs", "stair_landing"]: return node as Node3D
		node = node.get_parent()
	return mesh

func _authored_wall_axis(mesh: MeshInstance3D) -> Vector2:
	var bounds: AABB = mesh.global_transform * mesh.get_aabb()
	var result := Vector2.RIGHT if bounds.size.x >= bounds.size.z else Vector2.DOWN
	var contact := 0.0
	for region: Rect2 in _authored_regions:
		var along_z := minf(bounds.end.z,region.end.y)-maxf(bounds.position.z,region.position.y)
		if along_z>contact and (absf(bounds.end.x-region.position.x)<0.025 or absf(bounds.position.x-region.end.x)<0.025):
			contact = along_z
			result = Vector2.DOWN
		var along_x := minf(bounds.end.x,region.end.x)-maxf(bounds.position.x,region.position.x)
		if along_x>contact and (absf(bounds.end.z-region.position.y)<0.025 or absf(bounds.position.z-region.end.y)<0.025):
			contact = along_x
			result = Vector2.RIGHT
	return result

func _doorway_owner(node: Node) -> Node3D:
	while node != self:
		if str(node.get_meta("kind", "")) in ["door_panel", "door_frame"]: return node as Node3D
		node = node.get_parent()
	return null

func _has_static_frame_owner(node: Node) -> bool:
	while node != self:
		if str(node.get_meta("kind", "")) == "door_panel": return false
		if str(node.get_meta("kind", "")) == "door_frame": return true
		node = node.get_parent()
	return false

func _has_boundary_owner(node: Node) -> bool:
	while node != self:
		if str(node.get_meta("kind", "")) in ["door_frame", "door_panel", "wall", "wall_lower", "wall_upper", "wall_component"]: return true
		node = node.get_parent()
	return false

func _has_ceiling_owner(node: Node) -> bool:
	while node != self:
		if node.has_meta("ceiling_component"): return true
		if str(node.get_meta("kind", "")) in ["ceiling", "roof"]: return true
		node = node.get_parent()
	return false

func _clip_material(original: Material) -> ShaderMaterial:
	var result := ShaderMaterial.new()
	var code: String
	if original is ShaderMaterial:
		code = original.shader.code
	else:
		assert(original is StandardMaterial3D, "Unsupported architectural surface material")
		code = "shader_type spatial; uniform vec4 pigment : source_color; uniform sampler2D grain : source_color; uniform bool has_grain; uniform float roughness; uniform float metal; void fragment() { ALBEDO = pigment.rgb; if (has_grain) { ALBEDO *= texture(grain, UV).rgb; } ROUGHNESS = roughness; METALLIC = metal; }"
	var fragment := code.find("void fragment()")
	assert(fragment >= 0, "Architectural shader requires a fragment stage")
	var opening := code.find("{", fragment)
	var cursor := opening + 1
	var nesting := 1
	while nesting > 0:
		if code[cursor] == "{": nesting += 1
		elif code[cursor] == "}": nesting -= 1
		cursor += 1
	var finish := "\nvec3 manuscript_p = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz; float manuscript_visibility = manuscript_known(manuscript_p); if (manuscript_storey_base > manuscript_eye.y + 0.1 || (manuscript_cap ? !manuscript_cut(manuscript_p + vec3(0.0, 0.01, 0.0)) : manuscript_cut(manuscript_p)) || manuscript_visibility <= 0.001) { discard; } ALBEDO *= manuscript_visibility; EMISSION *= manuscript_visibility; SPECULAR *= manuscript_visibility;\n"
	code = code.insert(cursor - 1, finish)
	var header_end := code.find(";", code.find("render_mode")) if code.contains("render_mode") else code.find(";", code.find("shader_type"))
	code = code.insert(header_end + 1, "\n#include \"res://scenes/components/rendering/room_visibility.gdshaderinc\"\n")
	if not _shader_cache.has(code):
		var shader := Shader.new()
		shader.code = code
		_shader_cache[code] = shader
	result.shader = _shader_cache[code]
	if original is ShaderMaterial:
		for parameter in original.shader.get_shader_uniform_list():
			result.set_shader_parameter(parameter.name, original.get_shader_parameter(parameter.name))
	else:
		result.set_shader_parameter("pigment", original.albedo_color)
		result.set_shader_parameter("grain", original.albedo_texture)
		result.set_shader_parameter("has_grain", original.albedo_texture != null)
		result.set_shader_parameter("roughness", original.roughness)
		result.set_shader_parameter("metal", original.metallic)
	return result

func _register_openings(floor: Dictionary, source_map: String, building: Node3D) -> void:
	for room: Dictionary in GEOMETRY.map_data(source_map).rooms:
		if bool(room.get("fallback_corridor",false)): continue
		for raw: Array in room.rects:
			var rect := GEOMETRY.rect(raw)
			var a := DV_MAP.pixel_to_godot(source_map,rect.position)
			var b := DV_MAP.pixel_to_godot(source_map,rect.end)
			_authored_regions.append(Rect2(Vector2(minf(a.x,b.x),minf(a.z,b.z)),Vector2(absf(b.x-a.x),absf(b.z-a.z))))
	for wall: Dictionary in floor.get("wall_segments", []):
		_wall_margin = maxf(_wall_margin, float(wall.thickness) + 0.04)
		var along := Vector2(float(wall.end[0]) - float(wall.start[0]), -(float(wall.end[1]) - float(wall.start[1]))).normalized()
		_wall_axes[str(wall.id)] = along
		for opening_id in wall.openings: _opening_normals[str(opening_id)] = Vector2(-along.y, along.x)
	for opening: Dictionary in floor.openings:
		_opening_specs[str(opening.id)] = opening
		_opening_roots[str(opening.id)] = building
		_opening_maps[str(opening.id)] = source_map
		if opening.has("frame"): _opening_owners[str(opening.frame.id)] = opening
		if opening.has("door"): _opening_owners[str(opening.door.id)] = opening
		for leaf: Dictionary in opening.get("additional_doors", []): _opening_owners[str(leaf.id)] = opening
	for edge: Dictionary in GEOMETRY.map_data(source_map).doors:
		var key := str(edge.opening_ref) if edge.has("opening_ref") else str(edge.from) + ">" + str(edge.to)
		if _links.has(key): continue
		var portal := DV_MAP.pixel_to_godot(source_map, GEOMETRY.point(edge.point))
		var a := GEOMETRY.rect(GEOMETRY.room_data(source_map, str(edge.from)).rect).get_center()
		var b := GEOMETRY.rect(GEOMETRY.room_data(source_map, str(edge.to)).rect).get_center()
		var direction := (DV_MAP.pixel_to_godot(source_map, b) - DV_MAP.pixel_to_godot(source_map, a)).normalized()
		var link := NavigationLink3D.new()
		link.start_position = portal - direction * 0.65
		link.end_position = portal + direction * 0.65
		link.bidirectional = true
		link.enabled = false
		link.set_meta("edge", edge)
		add_child(link)
		_links[key] = link

func passage_enabled(edge: Dictionary) -> bool:
	if not edge.has("opening_ref"): return true # Authored shared open floor.
	var opening: Dictionary = _opening_specs[str(edge.opening_ref)]
	if not opening.has("door"): return true
	var leaves: Array = [opening.door]
	leaves.append_array(opening.get("additional_doors", []))
	for spec: Dictionary in leaves:
		var leaf: Node3D = _opening_roots[str(opening.id)].study_doors[str(spec.id)]
		if leaf.is_locked or leaf.moving: return false
		if absf(leaf.current_angle) < minf(70.0, absf(float(spec.open_angle)) * 0.85): return false
	return true

func apply_observer(identity: String, feet: Vector3, states: Dictionary, eye: Vector3) -> void:
	var started := Time.get_ticks_usec()
	_observer = eye
	# Section meshes are fixed to the authored storey, not regenerated for
	# each centimetre climbed by the observer on a ramp.
	_floor_y = float(DV_MAP.spatial_floor(map_id).z) if DV_MAP.supports(map_id) else feet.y
	_slices.clear()
	_slice_axes.clear()
	_slice_heights.clear()
	var region_key := str([map_id, identity, _registered_roots.size(), _world_projection_identity.get("content_hash", "")])
	if region_key != _observer_region_key:
		_observer_region_key = region_key
		_observer_rects.clear()
		for value: Array in GEOMETRY.room_data(map_id, identity).rects:
			var rect := GEOMETRY.rect(value)
			var a: Vector3 = DV_MAP.pixel_to_godot(map_id, rect.position) if DV_MAP.supports(map_id) else rooms[identity].root.position + Vector3(rect.position.x / 32.0, 0, rect.position.y / 32.0)
			var b: Vector3 = DV_MAP.pixel_to_godot(map_id, rect.end) if DV_MAP.supports(map_id) else rooms[identity].root.position + Vector3(rect.end.x / 32.0, 0, rect.end.y / 32.0)
			_observer_rects.append(Vector4(minf(a.x,b.x), minf(a.z,b.z), maxf(a.x,b.x), maxf(a.z,b.z)))
		assert(_observer_rects.size() <= 32, "Room clipping capacity exceeded")
		_shell_rects = _continuous_shell_rects(identity)
		_observer_openings.clear()
		for opening: Dictionary in _opening_specs.values():
			if str(_opening_maps.get(str(opening.id), map_id)) != map_id: continue
			var rect: Array = opening.rect
			var portal := preload("res://scenes/components/world/DesignCoordinates.gd").to_godot(Vector3((rect[0]+rect[2])*0.5, (rect[1]+rect[3])*0.5, opening.bottom))
			for r in _observer_rects:
				if portal.x >= r.x-0.3 and portal.x <= r.z+0.3 and portal.z >= r.y-0.3 and portal.z <= r.w+0.3 and minf(minf(absf(portal.x-r.x),absf(portal.x-r.z)),minf(absf(portal.z-r.y),absf(portal.z-r.w))) < 0.45:
					_observer_openings.append({"opening": opening, "portal": portal})
					break
	if not is_same(states, _observer_door_states):
		_observer_door_states = states
		for root in _dv_buildings.values():
			for id: String in root.study_doors:
				if states.has(id): root.study_doors[id].is_locked = bool(states[id].locked)
	for link: NavigationLink3D in _links.values(): link.enabled = passage_enabled(link.get_meta("edge"))
	for entry: Dictionary in _observer_openings:
		var opening: Dictionary = entry.opening
		var rect: Array = opening.rect
		var portal: Vector3 = entry.portal
		var delta := Vector2(portal.x-eye.x, portal.z-eye.z)
		if opening.has("door"):
			var leaf: Node3D = _opening_roots[str(opening.id)].study_doors[str(opening.door.id)]
			if absf(leaf.current_angle) < 20.0: continue
		elif states.has(str(opening.id)):
			if not bool(states[str(opening.id)].open): continue
		else: continue # An unseen window state never creates a view.
		var axis: Vector2 = _opening_normals.get(str(opening.id), Vector2.RIGHT if float(rect[2])-float(rect[0]) < float(rect[3])-float(rect[1]) else Vector2.DOWN)
		if axis.dot(delta) < 0.0: axis = -axis
		_slices.append(Vector4(portal.x, portal.z, minf(0.65,maxf(rect[2]-rect[0],rect[3]-rect[1])*0.5),1.8))
		_slice_axes.append(axis)
		_slice_heights.append(Vector2(float(opening.bottom), float(opening.top)))
	assert(_slices.size() <= 16, "Aperture clipping capacity exceeded")
	for key: String in _dv_buildings: _dv_buildings[key].visible = not execution_only and (key == map_id or _transition_maps.has(key))
	for key: String in rooms:
		if not rooms[key].get("dv_authored", false): rooms[key].root.visible = not execution_only and key == identity
	_publish_material_view()
	observer_update_us = Time.get_ticks_usec() - started
	observer_update_max_us = maxi(observer_update_max_us, observer_update_us)

# Only a DV-authored open-floor adjacency joins shells. A doorway never joins
# unknown interiors, and the observer/contents rectangles remain unchanged.
func _continuous_shell_rects(identity: String) -> PackedVector4Array:
	var result := _observer_rects.duplicate()
	if not DV_MAP.supports(map_id): return result
	var connected := [identity]
	var pending := [identity]
	var edges: Array = GEOMETRY.map_data(map_id).get("doors", [])
	while not pending.is_empty():
		var current: String = pending.pop_front()
		for edge: Dictionary in edges:
			if edge.has("opening_ref") or str(edge.get("source", "")) != "DV semantic-region shared boundary": continue
			var next := str(edge.to) if str(edge.from) == current else str(edge.from) if str(edge.to) == current else ""
			if next.is_empty() or connected.has(next): continue
			connected.append(next)
			pending.append(next)
			for raw: Array in GEOMETRY.room_data(map_id, next).get("rects", []):
				var area := GEOMETRY.rect(raw)
				var a := DV_MAP.pixel_to_godot(map_id, area.position)
				var b := DV_MAP.pixel_to_godot(map_id, area.end)
				result.append(Vector4(minf(a.x,b.x),minf(a.z,b.z),maxf(a.x,b.x),maxf(a.z,b.z)))
	assert(result.size() <= 32, "Continuous shell clipping capacity exceeded")
	return result

func set_deduction(active: bool) -> void:
	_deduction = active
	_publish_material_view()

func _publish_material_view() -> void:
	if _published_eye != _observer:
		_published_eye = _observer
		_eye_image.set_pixel(0, 0, Color(_observer.x, _observer.y, _observer.z, 1.0))
		_eye_texture.update(_eye_image)
		observer_texture_updates += 1
	_refresh_moving_solid_bounds()
	if not is_equal_approx(_cap_floor, _floor_y):
		_refresh_cutaway_caps()
	_refresh_boundary_caps()
	_build_cap_slice()
	var rects := _observer_rects.duplicate()
	rects.resize(32)
	var shell_rects := _shell_rects.duplicate()
	shell_rects.resize(32)
	var slices := _slices.duplicate()
	slices.resize(16)
	var axes := _slice_axes.duplicate()
	axes.resize(16)
	var heights := _slice_heights.duplicate()
	heights.resize(16)
	var view := {"manuscript_cutaway_height": _cutaway_height,
		"manuscript_room_count": _observer_rects.size(), "manuscript_rooms": rects,
		"manuscript_shell_count": _shell_rects.size(), "manuscript_shells": shell_rects,
		"manuscript_slice_count": _slices.size(), "manuscript_slices": slices,
		"manuscript_axes": axes, "manuscript_slice_heights": heights,
		"manuscript_eye_data": _eye_texture,
		"manuscript_floor": _floor_y, "manuscript_deduction": _deduction}
	var changed := {}
	for parameter in view:
		if not _material_view.has(parameter) or _material_view[parameter] != view[parameter]:
			changed[parameter] = view[parameter]
	_material_view = view
	material_view_changed_parameters = changed.size()
	var eye_cell := Vector2(_observer.x, _observer.z).snapped(Vector2.ONE * 0.5)
	var culling_key := str([_observer_rects, _shell_rects, _slices, _slice_axes, eye_cell if not _slices.is_empty() else Vector2.ZERO])
	var full_cull := culling_key != _culling_key or _culling_dirty
	var culling_changed := full_cull or _culled_surface_count != _surface_records.size()
	if culling_changed:
		_culling_key = culling_key
		_culling_dirty = false
		_update_surface_culling(eye_cell, 0 if full_cull else _culled_surface_count)
		_culled_surface_count = _surface_records.size()
	if changed.is_empty() and not culling_changed: return
	for record: Dictionary in _visible_surfaces:
		if not is_instance_valid(record.mesh.get_ref()): continue
		var material: ShaderMaterial = record.material
		var updates: Dictionary = changed if record.get("view_initialized", false) else view
		for parameter in updates:
			material.set_shader_parameter(parameter, updates[parameter])
			material_parameter_writes += 1
		record["view_initialized"] = true

func _update_surface_culling(eye_cell: Vector2, first_record := 0) -> void:
	var aperture_regions: Array[Rect2] = []
	for index in _slices.size():
		var aperture := _slices[index]
		var normal := _slice_axes[index]
		var tangent := Vector2(-normal.y, normal.x)
		var portal := Vector2(aperture.x, aperture.y)
		# This envelope contains the aperture cone for every eye position in
		# the half-metre cell. Exact shader clipping still updates every frame.
		var offset := portal - eye_cell
		var depth := maxf(0.001, offset.dot(normal) - 0.354)
		var far_center := portal + normal * aperture.w
		var width := tangent.abs() * (aperture.z * 1.6 + (absf(offset.dot(tangent)) + 0.354) * aperture.w / depth)
		var start := portal.min(far_center) - width
		aperture_regions.append(Rect2(start, portal.max(far_center) + width - start).grow(0.02))
	if first_record == 0: _visible_surfaces.clear()
	for index in range(first_record, _surface_records.size()):
		var record: Dictionary = _surface_records[index]
		var mesh := record.mesh.get_ref() as MeshInstance3D
		if not is_instance_valid(mesh): continue
		var material: ShaderMaterial = record.material
		# Discard in a shader still submits and draws the entire storey. Cull
		# disjoint instances with conservative bounds, leaving physics intact.
		if not record.has("culling"):
			record["culling"] = {"layers": mesh.layers, "transform": mesh.global_transform,
				"bounds": mesh.global_transform * mesh.get_aabb(),
				"shell": bool(material.get_shader_parameter("manuscript_static_shell")),
				"margin": float(material.get_shader_parameter("manuscript_wall_margin")) + 0.02,
				"aperture": not bool(material.get_shader_parameter("manuscript_wall")) and not bool(material.get_shader_parameter("manuscript_structure")) and not bool(material.get_shader_parameter("manuscript_ceiling"))}
			if bool(material.get_shader_parameter("manuscript_doorway")):
				# A leaf may rotate between culling updates. Its whole sweep
				# stays in this conservative envelope, including paired leaves.
				record.culling.margin += (record.culling.bounds as AABB).size.length() * 2.0
		var culling: Dictionary = record.culling
		if mesh.global_transform != culling.transform:
			culling.transform = mesh.global_transform
			culling.bounds = mesh.global_transform * mesh.get_aabb()
		var bounds: AABB = culling.bounds
		var footprint := Rect2(bounds.position.x, bounds.position.z, bounds.size.x, bounds.size.z).grow(float(culling.margin))
		var displayed := false
		var regions: PackedVector4Array = _shell_rects if culling.shell else _observer_rects
		for region in regions:
			if footprint.intersects(Rect2(region.x, region.y, region.z-region.x, region.w-region.y), true):
				displayed = true
				break
		if not displayed and culling.aperture:
			for region in aperture_regions:
				if footprint.intersects(region, true):
					displayed = true
					break
		var layers: int = culling.layers if displayed else 0
		if mesh.layers != layers: mesh.layers = layers
		if not displayed:
			record["view_initialized"] = false
			continue
		_visible_surfaces.append(record)
	visible_surface_count = _visible_surfaces.size()

func _refresh_moving_solid_bounds() -> void:
	for key in _moving_solid_records:
		var record: Dictionary = _moving_solid_records[key]
		var owner := record.owner.get_ref() as Node3D
		if not is_instance_valid(owner) or owner.global_transform == record.transform: continue
		_culling_dirty = true
		var bounds := AABB()
		var has_bounds := false
		for part: MeshInstance3D in owner.find_children("*", "MeshInstance3D", true, false):
			if part.mesh == null: continue
			var part_bounds: AABB = part.global_transform * part.get_aabb()
			bounds = bounds.merge(part_bounds) if has_bounds else part_bounds
			has_bounds = true
		if not has_bounds: continue
		record.transform = owner.global_transform
		_object_bounds[key] = bounds
		for material: ShaderMaterial in record.materials:
			material.set_shader_parameter("manuscript_object_min",bounds.position)
			material.set_shader_parameter("manuscript_object_max",bounds.end)

func _refresh_cutaway_caps() -> void:
	_cap_floor = _floor_y
	_cap_generation += 1
	for cap: MeshInstance3D in _cap_nodes:
		if is_instance_valid(cap): cap.queue_free()
	_cap_nodes.clear()
	_horizontal_cap_seen.clear()
	_surface_records = _surface_records.filter(func(record: Dictionary) -> bool: return not record.get("cap", false) or record.get("boundary_cap", false))
	_culling_dirty = true
	for node: MeshInstance3D in _boundary_cap_cache.get(_boundary_cap_key, []):
		if is_instance_valid(node): node.visible = false
	_boundary_cap_key = ""

func _queue_visible_cutaway_caps() -> void:
	var seen: Dictionary = {}
	for record: Dictionary in _surface_records:
		if record.get("cap",false): continue
		var source := record.mesh.get_ref() as MeshInstance3D
		if not is_instance_valid(source) or seen.has(source.get_instance_id()): continue
		seen[source.get_instance_id()] = true
		if _horizontal_cap_seen.has(source.get_instance_id()): continue
		var material: ShaderMaterial = record.material
		if not bool(material.get_shader_parameter("manuscript_wall")) and not bool(material.get_shader_parameter("manuscript_doorway")): continue
		var bounds: AABB = source.global_transform * source.get_aabb()
		var elevation := _floor_y + _cutaway_height
		if elevation <= bounds.position.y + 0.0001 or elevation >= bounds.end.y - 0.0001: continue
		var footprint := Rect2(bounds.position.x, bounds.position.z, bounds.size.x, bounds.size.z).grow(float(material.get_shader_parameter("manuscript_wall_margin")) + 0.02)
		var touches_shell := false
		for region in _shell_rects:
			if footprint.intersects(Rect2(region.x, region.y, region.z-region.x, region.w-region.y), true):
				touches_shell = true
				break
		if not touches_shell: continue
		_horizontal_cap_seen[source.get_instance_id()] = true
		_cap_jobs.append({"record": record, "elevation": _floor_y + _cutaway_height,
			"axis": 1, "normal": Vector3.UP, "generation": _cap_generation, "boundary": ""})

func _refresh_boundary_caps() -> void:
	# Cache a region's cross sections on its first visit. Re-entering it only
	# changes visibility; the actual mesh is never recomputed every frame.
	var key := str([_registered_roots.size(), _floor_y, _shell_rects])
	if key == _boundary_cap_key: return
	for node: MeshInstance3D in _boundary_cap_cache.get(_boundary_cap_key, []):
		if is_instance_valid(node): node.visible = false
	_boundary_cap_key = key
	_queue_visible_cutaway_caps()
	if _boundary_cap_cache.has(key):
		for node: MeshInstance3D in _boundary_cap_cache[key]:
			if is_instance_valid(node): node.visible = true
		return
	var caps: Array[MeshInstance3D] = []
	var seen: Dictionary = {}
	for record: Dictionary in _surface_records:
		if record.get("cap",false): continue
		var source := record.mesh.get_ref() as MeshInstance3D
		if not is_instance_valid(source) or seen.has(source.get_instance_id()): continue
		seen[source.get_instance_id()] = true
		var material: ShaderMaterial = record.material
		if not bool(material.get_shader_parameter("manuscript_wall")): continue
		var bounds: AABB = source.global_transform * source.get_aabb()
		var footprint := Rect2(bounds.position.x, bounds.position.z, bounds.size.x, bounds.size.z).grow(float(material.get_shader_parameter("manuscript_wall_margin")) + 0.02)
		var touches_shell := false
		for region in _shell_rects:
			if footprint.intersects(Rect2(region.x, region.y, region.z-region.x, region.w-region.y), true):
				touches_shell = true
				break
		if not touches_shell: continue
		var along: Vector2 = material.get_shader_parameter("manuscript_wall_axis")
		var axis := 0 if absf(along.x) > 0.7 else 2
		var planes: Dictionary = {}
		for rect: Vector4 in _shell_rects:
			planes[rect.x if axis == 0 else rect.y] = -1.0
			planes[rect.z if axis == 0 else rect.w] = 1.0
		for coordinate: float in planes:
			if coordinate <= bounds.position[axis] + 0.0001 or coordinate >= bounds.end[axis] - 0.0001: continue
			var normal := Vector3.ZERO
			normal[axis] = planes[coordinate]
			_cap_jobs.append({"record": record, "elevation": coordinate, "axis": axis,
				"normal": normal, "generation": _cap_generation, "boundary": key})
	_boundary_cap_cache[key] = caps

func _build_cap_slice() -> void:
	# Decorative cross sections share a bounded render-frame budget. Physics and
	# room visibility remain immediate; obsolete floor jobs never publish.
	var started := Time.get_ticks_usec()
	while not _cap_jobs.is_empty() and Time.get_ticks_usec() - started < 2000:
		var job: Dictionary = _cap_jobs.pop_front()
		var boundary := str(job.boundary)
		if boundary.is_empty() and int(job.generation) != _cap_generation: continue
		var record: Dictionary = job.record
		var source := record.mesh.get_ref() as MeshInstance3D
		if not is_instance_valid(source): continue
		var cap := preload("res://scenes/components/architecture/CutawayCap3D.gd").create(source, job.elevation, job.axis, job.normal)
		if cap == null: continue
		var material := (record.material as ShaderMaterial).duplicate() as ShaderMaterial
		if boundary.is_empty():
			material.set_shader_parameter("manuscript_cap", true)
			_cap_nodes.append(cap)
		else:
			material.set_shader_parameter("manuscript_boundary_cap", true)
			material.set_shader_parameter("manuscript_section_normal", job.normal)
			_boundary_cap_cache[boundary].append(cap)
			cap.visible = boundary == _boundary_cap_key
		cap.material_override = material
		_surface_records.append({"mesh": weakref(cap), "surface": 0, "material": material,
			"original": record.original, "cap": true, "boundary_cap": not boundary.is_empty()})
	cap_build_max_us = maxi(cap_build_max_us, Time.get_ticks_usec() - started)

func unload_room_visuals(identity: String) -> bool:
	if not rooms.has(identity):
		return false
	var record: Dictionary = rooms[identity]
	var root_node = record.get("root") as Node3D
	if root_node != null and is_instance_valid(root_node):
		root_node.visible = false
		record["visuals_unloaded"] = true
		return true
	return false

## Task 4.11: Reload visual resources upon re-entering room and re-synchronize with Core state
func reload_room_visuals(identity: String, core_state_override: Dictionary = {}) -> bool:
	if not rooms.has(identity):
		return false
	var record: Dictionary = rooms[identity]
	var root_node = record.get("root") as Node3D
	if root_node != null and is_instance_valid(root_node):
		root_node.visible = true
		record["visuals_unloaded"] = false
		if not core_state_override.is_empty():
			for obj_id in core_state_override:
				var state_val = core_state_override[obj_id]
				var obj = _find_world_object_recursive(root_node, str(obj_id))
				if obj != null and obj.has_method("set_angle_checked"):
					obj.call("set_angle_checked", float(state_val))
		return true
	return false

func _find_world_object_recursive(parent: Node, id_str: String) -> Node:
	if parent.has_meta("world_object_id") and str(parent.get_meta("world_object_id")) == id_str:
		return parent
	if str(parent.name) == id_str:
		return parent
	for child in parent.get_children():
		var found = _find_world_object_recursive(child, id_str)
		if found != null: return found
	return null

## Task 4.12: Debug visual overlay toggle

func adopted_material(mesh: MeshInstance3D, surface: int) -> Material:
	for record: Dictionary in _surface_records:
		if record.mesh.get_ref() == mesh and int(record.surface) == surface: return record.original
	return mesh.get_active_material(surface)

# Query ports return value snapshots; physical object handles stay owned by this map.
func has_room(identity: String) -> bool:
	return rooms.has(identity)

func room_view(identity: String) -> Dictionary:
	if not rooms.has(identity): return {}
	var record: Dictionary = rooms[identity]
	var root: Node3D = record.root
	if not is_instance_valid(root): return {}
	return {"dv_authored": bool(record.get("dv_authored", false)), "footprint": record.footprint,
		"origin": root.position, "space_id": root.get_instance_id()}

func logical_room_at(point: Vector2, query_map_id: String = "") -> String:
	return str(GEOMETRY.room_at(map_id if query_map_id.is_empty() else query_map_id, point).get("id", ""))

func logical_to_world(point: Vector2, identity: String) -> Vector3:
	var record := room_view(identity)
	if record.is_empty(): return Vector3.INF
	if record.dv_authored: return DV_MAP.pixel_to_godot(map_id, point)
	var logical := GEOMETRY.rect(GEOMETRY.room_data(map_id, identity).rect)
	var local: Vector2 = record.footprint.position + (point - logical.position) / logical.size * record.footprint.size
	var result: Vector3 = record.origin + Vector3(local.x, 0, local.y)
	var floor_y := sample_ground(result)
	if is_finite(floor_y): result.y = floor_y
	return result

func world_to_logical(point: Vector3, identity: String) -> Vector2:
	var record := room_view(identity)
	if record.is_empty(): return Vector2.INF
	if record.dv_authored:
		return DV_MAP.design_to_pixel(map_id, preload("res://scenes/components/world/DesignCoordinates.gd").to_design(point))
	var local: Vector3 = point - record.origin
	var logical := GEOMETRY.rect(GEOMETRY.room_data(map_id, identity).rect)
	return logical.position + (Vector2(local.x, local.z) - record.footprint.position) / record.footprint.size * logical.size

func sample_ground(point: Vector3) -> float:
	var layers = preload("res://scenes/components/world/PhysicsLayers.gd")
	var dv_floor := DV_MAP.supports(map_id)
	var mask: int = layers.FLOOR if dv_floor else layers.SOLID
	var ray := PhysicsRayQueryParameters3D.create(point + Vector3.UP * 2.0, point - Vector3.UP * 2.0, mask)
	var excluded: Array[RID] = []
	for attempt in 12:
		var hit := get_world_3d().direct_space_state.intersect_ray(ray)
		if hit.is_empty(): return INF
		var collider: Node3D = hit.collider
		if hit.normal.y > 0.7 and (bool(collider.collision_layer & layers.FLOOR) if dv_floor else (str(collider.get_meta("collision_category", "")) == "floor" or "floor" in str(collider.name).to_lower())):
			return float(hit.position.y)
		excluded.append(hit.rid)
		ray.exclude = excluded
	return INF

func register_spatial_objects(participants: Node) -> bool:
	for building in _dv_buildings.values():
		for node in building.find_children("*", "Node3D", true, false):
			var identity := str(node.get_meta("world_object_id", ""))
			if identity.is_empty() or not node.has_method("resolve_interaction_slot"): continue
			if participants.resolve_spatial_object(identity) == node: continue
			if not participants.register_spatial_object(identity, node): return false
	return true

func site_positions(identity: String, sprite_sites: Dictionary) -> Dictionary:
	var result := {}
	if not rooms.has(identity): return result
	var root: Node3D = rooms[identity].root
	for prop in sprite_sites:
		var node := root.find_child(str(prop), true, false) as Node3D
		if node != null: result[prop] = node.global_position
	return result

func visible_object_nodes(identity: String, camera: Camera3D, occluded_regions: Array[Rect2]) -> Array[Node3D]:
	var result: Array[Node3D] = []
	if not rooms.has(identity): return result
	var root: Node3D = rooms[identity].root
	for node: Node3D in root.find_children("*", "Node3D", true, false):
		if not node.has_meta("source_prop") or not node.is_visible_in_tree(): continue
		for mesh: MeshInstance3D in node.find_children("*", "MeshInstance3D", true, false):
			if not mesh.is_visible_in_tree(): continue
			var bounds: AABB = mesh.global_transform * mesh.get_aabb()
			var point := bounds.get_center()
			if not _solid_sample_is_displayed(mesh, point): continue
			if camera.is_position_behind(point): continue
			var screen := camera.unproject_position(point)
			if not get_viewport().get_visible_rect().has_point(screen): continue
			var covered := false
			for region in occluded_regions:
				if region.has_point(screen): covered = true
			if covered: continue
			var query := PhysicsRayQueryParameters3D.create(camera.global_position, point, 1)
			var hit := get_world_3d().direct_space_state.intersect_ray(query)
			if hit.is_empty() or node.is_ancestor_of(hit.collider) or node == hit.collider:
				result.append(node)
				break
	return result

func _solid_sample_is_displayed(mesh: MeshInstance3D, point: Vector3) -> bool:
	# The observation list must use the same region and whole-solid gate as
	# room_visibility. Node.visible alone cannot detect a discarded fragment.
	var material := mesh.get_active_material(0) as ShaderMaterial
	if material == null: return false
	if bool(material.get_shader_parameter("manuscript_ceiling")): return false
	var wall := bool(material.get_shader_parameter("manuscript_wall"))
	var structure := bool(material.get_shader_parameter("manuscript_structure"))
	var doorway := bool(material.get_shader_parameter("manuscript_doorway"))
	var margin := float(material.get_shader_parameter("manuscript_wall_margin")) if structure else 0.02
	var along: Vector2 = material.get_shader_parameter("manuscript_wall_axis")
	var margins := Vector2(margin,margin)
	if wall: margins = Vector2(0.0,margin) if absf(along.x)>0.7 else Vector2(margin,0.0)
	var regions := _shell_rects if bool(material.get_shader_parameter("manuscript_static_shell")) else _observer_rects
	var anchor: Vector3 = material.get_shader_parameter("manuscript_doorway_anchor")
	var boundary := anchor if doorway else point
	if (wall or doorway) and point.y > _floor_y+_cutaway_height:
		for r: Vector4 in regions:
			if absf(along.x)>0.7 and boundary.x>r.x-margin and boundary.x<r.z+margin and absf(boundary.z-r.w)<margin: return false
			if absf(along.y)>0.7 and boundary.z>r.y-margin and boundary.z<r.w+margin and absf(boundary.x-r.z)<margin: return false
	for r: Vector4 in regions:
		if doorway and anchor.x>=r.x-margin and anchor.z>=r.y-margin and anchor.x<=r.z+margin and anchor.z<=r.w+margin: return true
		if point.x>=r.x-margins.x and point.z>=r.y-margins.y and point.x<=r.z+margins.x and point.z<=r.w+margins.y: return true
	if wall or structure: return false
	var low: Vector3 = material.get_shader_parameter("manuscript_object_min")
	var high: Vector3 = material.get_shader_parameter("manuscript_object_max")
	for i in range(_slices.size()):
		var fade := 1.0
		for corner in range(8):
			var p := Vector3(low.x if (corner & 1)==0 else high.x,low.y if (corner & 2)==0 else high.y,low.z if (corner & 4)==0 else high.z)
			fade = minf(fade,_aperture_visibility(p,i))
		if fade>0.001: return true
	return false

func _aperture_visibility(point: Vector3, index: int) -> float:
	var aperture := _slices[index]
	var n := _slice_axes[index]
	var d := Vector2(point.x-aperture.x,point.z-aperture.y)
	var depth := d.dot(n)
	if depth<0.0 or depth>aperture.w: return 0.0
	var tangent := Vector2(-n.y,n.x)
	var from_eye := Vector2(aperture.x-_observer.x,aperture.y-_observer.z)
	var eye_depth := maxf(0.001,from_eye.dot(n))
	var aperture_y := lerpf(_observer.y,point.y,eye_depth/maxf(0.001,eye_depth+depth))
	var heights := _slice_heights[index]
	if aperture_y<heights.x-0.05 or aperture_y>heights.y+0.05: return 0.0
	var offset := from_eye.dot(tangent)*depth/eye_depth
	var width := minf(aperture.z*1.6,aperture.z*(1.0+depth/eye_depth))
	return (1.0-smoothstep(0.0,aperture.w,depth))*(1.0-smoothstep(width*0.65,width,absf(d.dot(tangent)-offset)))

func presentation_environment(identity: String) -> Environment:
	if not rooms.has(identity): return null
	var root: Node3D = rooms[identity].root
	return root.get_meta("presentation_environment") if root.has_meta("presentation_environment") else null

func apply_lighting(identity: String, snapshot: Dictionary) -> void:
	if not rooms.has(identity): return
	var root: Node3D = rooms[identity].root
	var lighting := root.get_node_or_null("WorldTimeLighting")
	if lighting != null: lighting.apply_snapshot(snapshot)

func doorways() -> Array:
	var result: Array = GEOMETRY.map_data(map_id).get("doors", []).duplicate(true)
	result.append_array(definitions.get("passages", {}).get(map_id, []).duplicate(true))
	return result.filter(func(door: Dictionary): return passage_enabled(door))

var _world_projection_identity: Dictionary = {}
var _visibility_projection_identity: Dictionary = {}
var _physical_door_states: Dictionary = {}

func _projection_identity(frame: Dictionary) -> Dictionary:
	return {"session_generation": frame.get("session_generation"), "map_id": frame.get("map_id"),
		"map_generation": frame.get("map_generation"), "content_hash": frame.get("content_hash"),
		"state_version": frame.get("state_version")}

func apply_world_projection(frame: Dictionary) -> void:
	_world_projection_identity = _projection_identity(frame)
	var physical: Dictionary = frame.get("physical", {})
	_physical_door_states = physical.get("building_doors", {}).duplicate(true)
	for root in _dv_buildings.values():
		for id: String in root.study_doors:
			if _physical_door_states.has(id): root.study_doors[id].is_locked = bool(_physical_door_states[id].locked)
	for link: NavigationLink3D in _links.values(): link.enabled = passage_enabled(link.get_meta("edge"))

func apply_visibility_projection(frame: Dictionary) -> void:
	_visibility_projection_identity = _projection_identity(frame)
