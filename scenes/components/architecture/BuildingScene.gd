@tool
extends Node3D
## Runtime geometry projection. This component has no Core/session reference.
signal interaction_requested(building_id: String, portal_id: String)
signal project_rebuilt(source_hash: String)
signal furniture_requested(building_id: String, object_id: String, slot_id: String)
const FURNITURE := preload("res://scenes/components/world/FurnitureView.gd")
const FACING := preload("res://scenes/components/world/MeshFacing.gd")
var furnishings: Dictionary = {}
var furniture_bindings: Dictionary = {}
const PROJECT_RESOURCE := preload("res://scenes/components/architecture/BuildingProjectResource.gd")
const DOOR := preload("res://scenes/components/architecture/BuildingDoor.gd")
const ARCH_DOOR := preload("res://scenes/components/architecture/ArchitecturalDoor.gd")
@export var project_resource: Resource
var portals: Dictionary = {}
var topology: Dictionary = {}
var build_error := ""
var _generated: Node3D
var _materials: Dictionary = {}
static var _model_cache: Dictionary = {}
static var _geometry_cache: Dictionary = {}
var model_cache_hits := 0
var geometry_cache_hits := 0

func _ready() -> void:
	# Authoring can assemble off-tree before attaching this view.
	if project_resource != null and not is_instance_valid(_generated):
		rebuild(project_resource)

static func release_preview_caches() -> void:
	# Called only by the terminating native authoring job after its view is freed.
	_model_cache.clear()
	_geometry_cache.clear()
	FACING._cache.clear()

func rebuild(source: Resource) -> bool:
	if not source is PROJECT_RESOURCE or not source.is_valid_package():
		build_error = "Unsupported building project resource"
		push_error(build_error)
		return false
	var next := Node3D.new()
	next.name = "GeneratedStructure"
	var next_portals := {}
	var next_topology := {}
	var next_furniture := {}
	var next_bindings := {}
	var data: Dictionary = source.compiled
	_materials.clear()
	model_cache_hits=0
	geometry_cache_hits=0
	for material_id: String in data.materials:
		_materials[material_id] = _material(data.materials[material_id], material_id)
	for building: Dictionary in data.buildings:
		var part := Node3D.new()
		part.name = str(building.id)
		part.position = _v3(building.origin)
		part.rotation_degrees.y = float(building.rotation_degrees)
		part.set_meta("building_id", building.id)
		next.add_child(part)
		assemble_geometry(part, building.geometry, next_portals, str(building.id))
		next_topology[str(building.id)] = building.topology.duplicate(true)
		for room: Dictionary in building.topology.rooms:
			var marker := Marker3D.new()
			marker.name = "Room_" + str(room.id).validate_node_name()
			marker.position = _v3(room.center)
			marker.set_meta("room_id", room.id)
			marker.set_meta("title", room.name)
			part.add_child(marker)
		for item: Dictionary in building.get("furnishings", []):
			var furniture: Node3D = FURNITURE.new()
			if not furniture.configure(item.get("runtime_definition", item.definition_data), str(item.id)):
				next.free()
				build_error = "Invalid furniture " + str(item.id)
				return false
			furniture.name = str(item.id).validate_node_name()
			furniture.position = _v3(item.position)
			furniture.rotation_degrees.y = float(item.rotation_degrees)
			furniture.set_meta("floor_index", int(item.floor_index))
			part.add_child(furniture)
			furniture.apply_projection({"state": item.state, "revision": 0})
			furniture.interaction_requested.connect(_furniture_request.bind(str(building.id)))
			var key := str(building.id) + "/" + str(item.id)
			next_furniture[key] = furniture
			next_bindings[key] = item.get("interaction_bindings", {}).duplicate(true)
		for instance: Dictionary in building.get("instances", []):
			var packed := load(str(instance.resource)) as PackedScene
			if packed == null:
				build_error = "Cannot load authored model " + str(instance.resource)
				next.free()
				push_error(build_error)
				return false
			var cache_key := str(instance.resource)+":"+str(FileAccess.get_modified_time(str(instance.resource)))+":"+JSON.stringify(instance.get("exclusions",[])).sha256_text()
			var cached: PackedScene = _model_cache.get(cache_key)
			var model := (cached if cached!=null else packed).instantiate() as Node3D
			if model == null:
				build_error = "Building model must have a Node3D root"
				next.free()
				return false
			if cached!=null:
				model_cache_hits+=1
			else:
				var excluded := _prune_instance(model, instance.get("exclusions", []))
				model.set_meta("excluded_nodes", excluded)
				_correct_model_facing(model)
				if not model.get_meta("static_unit_baked",false): _batch_static_model(model)
				_assign_owners(model,model)
				var saved := PackedScene.new()
				if saved.pack(model)==OK:
					if _model_cache.size()>=24: _model_cache.erase(_model_cache.keys()[0])
					_model_cache[cache_key]=saved
			model.name = str(instance.id).validate_node_name()
			model.position = _v3(instance.position)
			model.rotation_degrees = _v3(instance.rotation_degrees)
			model.scale = _v3(instance.scale)
			if instance.has("floor_index"):
				model.set_meta("floor_index", int(instance.floor_index))
			part.add_child(model)
	var connections := Node3D.new()
	connections.name = "Connections"
	next.add_child(connections)
	assemble_geometry(connections, data.connections, next_portals, "connections")
	for entry: Dictionary in data.get("reflection_probes", []):
		var probe := ReflectionProbe.new()
		probe.name = str(entry.get("id", "InteriorReflection")).validate_node_name()
		probe.position = _v3(entry.position)
		probe.size = _v3(entry.size)
		probe.update_mode = ReflectionProbe.UPDATE_ONCE
		probe.box_projection = true
		probe.interior = true
		probe.enable_shadows = false
		probe.ambient_mode = ReflectionProbe.AMBIENT_DISABLED
		probe.intensity = clampf(float(entry.get("intensity", 0.85)), 0.0, 2.0)
		probe.max_distance = 45.0
		probe.set_meta("authored_reflection_probe", true)
		next.add_child(probe)
	# Only replace nodes owned by this compiler. Hand-authored children are retained.
	if is_instance_valid(_generated):
		remove_child(_generated)
		_generated.queue_free()
	_generated = next
	project_resource = source
	portals = next_portals
	furnishings = next_furniture
	furniture_bindings = next_bindings
	topology = next_topology
	add_child(next)
	build_error = ""
	project_rebuilt.emit(str(source.source_hash))
	return true

func _assign_owners(node: Node, root: Node) -> void:
	for child in node.get_children():
		child.owner=root
		_assign_owners(child,root)

func _correct_model_facing(node: Node) -> void:
	if node is MeshInstance3D and node.mesh!=null:
		var corrected: Dictionary=FACING.correct(node.mesh)
		node.mesh=corrected.mesh
		node.set_meta("winding_faces_corrected",corrected.flipped)
	for child in node.get_children(): _correct_model_facing(child)

func _model_bounds(node: Node, parent_transform: Transform3D) -> Variant:
	var transform := parent_transform
	if node is Node3D: transform = parent_transform * node.transform
	var result: Variant = null
	if node is MeshInstance3D and node.mesh != null: result = transform * node.get_aabb()
	for child in node.get_children():
		var bounds: Variant = _model_bounds(child, transform)
		if bounds != null: result = bounds if result == null else (result as AABB).merge(bounds)
	return result

func _prune_instance(node: Node, rules: Array, parent_transform := Transform3D.IDENTITY) -> int:
	var removed := 0
	var transform := parent_transform
	if node is Node3D: transform = parent_transform * node.transform
	for child in node.get_children():
		var exclude := false
		for rule: Dictionary in rules:
			if str(child.name) in rule.get("names", []): exclude = true
			var child_kind := "ceiling" if child.has_meta("ceiling_component") else str(child.get_meta("kind", ""))
			if child_kind in rule.get("kinds", []):
				var bounds: Variant = _model_bounds(child, transform)
				var box: Array = rule.get("bounds", [])
				if bounds != null and box.size() == 6:
					var target := AABB(Vector3(box[0],box[1],box[2]),Vector3(box[3]-box[0],box[4]-box[1],box[5]-box[2])).grow(0.03)
					if target.encloses(bounds): exclude = true
		if exclude:
			node.remove_child(child)
			child.free()
			removed += 1
		else:
			removed += _prune_instance(child, rules, transform)
	return removed

func _batch_static_model(model: Node3D) -> void:
	# Batch after exclusions, in model coordinates. Physics children remain intact.
	# Keep visibility roles separate so cutaway and floor selection still work.
	var groups := {}
	_collect_static_meshes(model, Transform3D.IDENTITY, groups)
	var before := 0
	var after := 0
	for key: String in groups:
		var entries: Array = groups[key]
		before += entries.size()
		if entries.size() < 2:
			after += entries.size()
			continue
		var surface := SurfaceTool.new()
		surface.begin(Mesh.PRIMITIVE_TRIANGLES)
		for entry: Dictionary in entries:
			var mesh_node: MeshInstance3D = entry.node
			surface.append_from(mesh_node.mesh, 0, entry.transform)
		surface.index()
		surface.optimize_indices_for_cache()
		var batch := MeshInstance3D.new()
		batch.name = "StaticBatch_%d" % after
		batch.mesh = surface.commit()
		batch.material_override = entries[0].material
		batch.set_meta("kind", entries[0].kind)
		batch.set_meta("role", entries[0].role)
		model.add_child(batch)
		for entry: Dictionary in entries:
			var mesh_node: MeshInstance3D = entry.node
			if mesh_node.get_child_count() == 0:
				mesh_node.get_parent().remove_child(mesh_node)
				mesh_node.free()
			else:
				mesh_node.mesh = null
		after += 1
	model.set_meta("static_meshes_before", before)
	model.set_meta("static_meshes_after", after)

func _collect_static_meshes(node: Node, parent_transform: Transform3D, groups: Dictionary) -> void:
	var transform := parent_transform
	if node is Node3D: transform = parent_transform * node.transform
	if node is MeshInstance3D and node.mesh != null and node.mesh.get_surface_count() == 1:
		var kind := str(node.get_meta("kind", ""))
		var role := str(node.get_meta("role", "structure"))
		var material: Material = node.get_active_material(0)
		if material != null and kind not in ["door_panel", "door_handle", "furniture"]:
			# Bound batches spatially; whole-campus batches defeat frustum culling.
			var center: Vector3 = transform * node.get_aabb().get_center()
			# SurfaceTool append must keep indexed and non-indexed meshes separate.
			# Mixing them leaves non-indexed faces outside the final index stream.
			# Preserve other vertex channels as well (foliage uses COLOR and UV2).
			var format: int = 0
			var arrays: Array = node.mesh.surface_get_arrays(0)
			for channel in arrays.size():
				if arrays[channel] != null and arrays[channel].size() > 0:
					format |= 1 << channel
			var key := "%s|%s|%d|%d|%d|%d" % [kind, role, material.get_instance_id(), floori(center.x / 16.0), floori(center.z / 16.0), format]
			if not groups.has(key): groups[key] = []
			groups[key].append({"node":node,"transform":transform,"material":material,"kind":kind,"role":role})
	for child in node.get_children(): _collect_static_meshes(child, transform, groups)

func _material(spec: Dictionary, identity: String) -> Material:
	if spec.has("resource"):
		var authored := load(str(spec.resource)) as Material
		if authored != null:
			return authored
	if identity == "glass" or float(spec.get("opacity", 1.0)) < 1.0:
		var glass := StandardMaterial3D.new()
		glass.albedo_color = Color(str(spec.get("color", "#badce1")))
		glass.albedo_color.a = float(spec.get("opacity", 0.28))
		glass.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		glass.cull_mode = BaseMaterial3D.CULL_BACK
		glass.roughness = float(spec.get("roughness", 0.12))
		glass.metallic_specular = 0.65
		return glass
	var mat := ShaderMaterial.new()
	mat.shader = preload("res://scenes/components/rendering/ArchitecturalSurface.gdshader")
	mat.set_shader_parameter("tint", Color(str(spec.get("color", "#eee9dc"))))
	mat.set_shader_parameter("roughness_value", float(spec.get("roughness", 0.85)))
	var pattern = {"oak":1, "walnut":1, "tile":2, "stone":3, "sage":4, "sand":4, "rose":4, "blue":4, "graphite":4, "ivory":5, "carpet":6, "check":7}.get(identity, 0)
	mat.set_shader_parameter("pattern", int(spec.get("pattern", pattern)))
	for layer in ["base", "detail", "mask"]:
		var path := str(spec.get(layer + "_texture", ""))
		if not path.is_empty() and path.begins_with("res://"):
			var texture := load(path) as Texture2D
			if texture != null:
				mat.set_shader_parameter(layer + "_texture", texture)
				mat.set_shader_parameter("use_" + layer + "_texture", true)
	var scale_value: Array = spec.get("texture_scale", [1.0, 1.0])
	if scale_value.size() == 2:
		mat.set_shader_parameter("texture_scale", Vector2(float(scale_value[0]), float(scale_value[1])))
	mat.set_shader_parameter("detail_blend", clampf(float(spec.get("detail_blend", 0.5)), 0.0, 1.0))
	return mat

func assemble_geometry(parent: Node3D, geometry: Dictionary, doors: Dictionary, building_id: String) -> void:
	var floors := {}
	var bodies := {}
	for entry: Dictionary in geometry.meshes:
		var level := int(entry.get("level", -1))
		var floor: Node3D = _floor(parent, floors, level)
		var visual := MeshInstance3D.new()
		visual.name = str(entry.key).validate_node_name()
		visual.set_meta("role", entry.get("role", "structure"))
		visual.set_meta("source_id", entry.get("source_id", ""))
		visual.set_meta("building_id", building_id)
		var signature := (JSON.stringify(entry.positions)+JSON.stringify(entry.normals)).sha256_text()
		var mesh: ArrayMesh=_geometry_cache.get(signature)
		if mesh==null:
			var vertices := _vectors(entry.positions)
			var normals := _vectors(entry.normals)
			var uv := PackedVector2Array()
			for i in vertices.size():
				var p := vertices[i]
				var normal := normals[i]
				uv.append(Vector2(p.x, p.z) if absf(normal.y) > 0.7 else Vector2(p.z, p.y) if absf(normal.x) > 0.7 else Vector2(p.x, p.y))
			var arrays := []
			arrays.resize(Mesh.ARRAY_MAX)
			arrays[Mesh.ARRAY_VERTEX] = vertices
			arrays[Mesh.ARRAY_NORMAL] = normals
			arrays[Mesh.ARRAY_TEX_UV] = uv
			mesh=ArrayMesh.new()
			mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
			# Generate tangent bases once for each distinct geometry, not each edit.
			var surface := SurfaceTool.new()
			surface.create_from(mesh, 0);surface.generate_tangents();surface.index();surface.optimize_indices_for_cache()
			mesh = surface.commit()
			mesh = FACING.correct(mesh).mesh
			if _geometry_cache.size()>=128: _geometry_cache.erase(_geometry_cache.keys()[0])
			_geometry_cache[signature]=mesh
		else: geometry_cache_hits+=1
		visual.mesh = mesh
		visual.material_override = _materials.get(str(entry.material))
		floor.add_child(visual)
	for shape: Dictionary in geometry.colliders:
		var level := int(shape.get("level", -1))
		var floor: Node3D = _floor(parent, floors, level)
		if not bodies.has(level):
			var body := StaticBody3D.new()
			body.name = "StructureCollision"
			body.collision_layer = 1
			body.collision_mask = 0
			floor.add_child(body)
			bodies[level] = body
		var node := CollisionShape3D.new()
		node.name = str(shape.id).validate_node_name()
		node.set_meta("source_id", shape.get("source_id", ""))
		if shape.kind == "box":
			var box := BoxShape3D.new()
			box.size = _v3(shape.size)
			node.shape = box
			node.position = _v3(shape.center)
			node.rotation.y = -float(shape.angle)
		else:
			var convex := ConvexPolygonShape3D.new()
			var points := PackedVector3Array()
			for point: Array in shape.points:
				points.append(_v3(point))
			convex.points = points
			node.shape = convex
		bodies[level].add_child(node)
	for item: Dictionary in geometry.get("doors", []):
		var portal: Node3D = ARCH_DOOR.new()
		portal.name = "Door_" + str(item.id).validate_node_name()
		portal.configure(item, _materials)
		portal.position = _v3(item.position)
		portal.rotation.y = -float(item.angle)
		portal.set_meta("building_id", building_id)
		portal.interaction_requested.connect(func(id: String): interaction_requested.emit(building_id, id))
		_floor(parent, floors, int(item.level)).add_child(portal)
		doors[building_id + "/" + str(item.id)] = portal

func _floor(parent: Node3D, floors: Dictionary, level: int) -> Node3D:
	if not floors.has(level):
		var floor := Node3D.new()
		floor.name = "Floor_" + str(level) if level >= 0 else "SiteStructure"
		floor.set_meta("floor_index", level)
		parent.add_child(floor)
		floors[level] = floor
	return floors[level]

func _v3(values: Array) -> Vector3:
	return Vector3(float(values[0]), float(values[1]), float(values[2]))

func _vectors(values: Array) -> PackedVector3Array:
	var output := PackedVector3Array()
	for i in range(0, values.size(), 3):
		output.append(Vector3(float(values[i]), float(values[i + 1]), float(values[i + 2])))
	return output

func apply_door_projection(states: Dictionary) -> void:
	for key: String in states:
		if portals.has(key):
			portals[key].apply_state(states[key])

func visible_floor(index: int) -> void:
	if not is_instance_valid(_generated):
		return
	for part: Node in _generated.get_children():
		var levels: Array=topology.get(str(part.name),{}).get("floors",[])
		var selected: Dictionary=levels[index] if index>=0 and index<levels.size() else {}
		for floor: Node in part.get_children():
			if floor.has_meta("floor_index") and floor is Node3D:
				var fi: int=int(floor.get_meta("floor_index"))
				if index<0 or fi<0 or selected.is_empty(): floor.visible=true
				elif fi<levels.size():
					var item: Dictionary=levels[fi]
					floor.visible=float(item.elevation)<=float(selected.elevation)+.01 or (item.get("kind","")=="half" and item.get("parent_floor","")==selected.id)

func _furniture_request(object_id: String, slot_id: String, building_id: String) -> void:
	furniture_requested.emit(building_id, object_id, slot_id)

func apply_furniture_projection(states: Dictionary) -> void:
	for key: String in states:
		if furnishings.has(key):
			furnishings[key].apply_projection(states[key])
