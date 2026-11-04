extends Node
## Presentation-only wall fade for the streamed character session.
## Each wall receives its own material; collision and other solids stay intact.

var _camera: Camera3D
var _body: Node3D
var _settings: Node
var _walls: Array[Dictionary] = []
var _seen: Dictionary = {}
var _opaque_shader: Shader
var _alpha_shader: Shader
var _mode := "dither"
var _room_manifest: Dictionary = {}
var _front_x := 1.0
var _front_z := 1.0

func configure(camera: Camera3D, body: Node3D, manifest_path: String) -> void:
	_room_manifest = JSON.parse_string(FileAccess.get_file_as_string(manifest_path))
	_camera = camera
	_body = body
	_opaque_shader = load("res://scenes/components/rendering/ArchitecturalSurface.gdshader")
	_alpha_shader = Shader.new()
	_alpha_shader.code = _opaque_shader.code.replace("// Material categories", "uniform bool occlusion_secondary = false;\n// Material categories").replace("if (occlusion_alpha && opacity < 0.999) discard; // BASE_ALPHA_CUT", "").replace("// ALPHA_VARIANT", "ALPHA = occlusion_secondary ? clamp((opacity - 0.22) / 0.78, 0.0, 1.0) : 0.28;")
	_settings = get_node_or_null("/root/GameSettings")
	if _settings != null:
		_mode = str(_settings.get_value("graphics", "wall_transparency"))
		_settings.setting_changed.connect(_setting_changed)

func _setting_changed(section: String, key: String, value: Variant) -> void:
	if section != "graphics" or key != "wall_transparency": return
	_mode = str(value)
	# update_view selects alpha only for walls near the camera-to-person ray.

func _apply_mode(material: ShaderMaterial, mode: String) -> void:
	material.shader = _alpha_shader if mode == "alpha" else _opaque_shader
	material.set_shader_parameter("occlusion_alpha", mode == "alpha")

func collect(root: Node) -> void:
	_collect(root, false)

func _collect(node: Node, wall: bool) -> void:
	if node.has_meta("wall_occlusion_piece"): return
	var kind := str(node.get_meta("kind", ""))
	if kind in ["furniture", "door_panel", "door_frame", "floor", "ceiling", "roof"]: wall = false
	if bool(node.get_meta("wall_component", false)) or kind in ["wall", "wall_lower", "wall_upper", "wall_component"]: wall = true
	if wall and node is MeshInstance3D and node.mesh != null and not _seen.has(node.get_instance_id()):
		_seen[node.get_instance_id()] = true
		var override_material: Material = node.material_override
		for index in node.mesh.get_surface_count():
			var source: Material = node.get_active_material(index)
			if source is ShaderMaterial and source.shader == _opaque_shader:
				var material := source.duplicate() as ShaderMaterial
				_apply_mode(material, "dither")
				material.set_shader_parameter("overhead_cutaway", false)
				material.set_shader_parameter("occlusion_enabled", true)
				material.set_shader_parameter("occlusion_strength", 0.78)
				node.set_surface_override_material(index, material)
				_walls.append({"mesh": weakref(node), "id": node.get_instance_id(), "material": material, "mode": "dither"})
		if override_material is ShaderMaterial and override_material.shader == _opaque_shader:
			node.material_override = null
	for child in node.get_children(): _collect(child, wall)

func update_view() -> void:
	if not is_instance_valid(_camera) or not is_instance_valid(_body): return
	var retained: Array[Dictionary] = []
	var focus := _body.global_position + Vector3.UP * 0.7
	var room_bounds := _current_room_bounds()
	var toward := _camera.global_position - _body.global_position
	var horizontal := Vector2(toward.x,toward.z).normalized()
	if absf(horizontal.x)>0.14: _front_x=signf(horizontal.x)
	if absf(horizontal.y)>0.14: _front_z=signf(horizontal.y)
	var nearest_id: int=-1
	var nearest_distance:=INF
	for record in _walls:
		var mesh := record.mesh.get_ref() as MeshInstance3D
		if not is_instance_valid(mesh):
			_seen.erase(record.id)
			continue
		# Only rendered wall meshes crossing the actual camera-to-person corridor count.
		var bounds: AABB = (mesh.global_transform * mesh.get_aabb()).grow(0.12 if record.mode == "alpha" else 0.04)
		var head := _body.global_position + Vector3.UP * 1.2
		var crosses: bool = mesh.layers!=0 and mesh.is_visible_in_tree() and (bounds.intersects_segment(_camera.global_position, focus) or bounds.intersects_segment(_camera.global_position, head))
		record["crosses"]=crosses
		record["front_wall"]=_is_front_wall(mesh.global_transform * mesh.get_aabb(),room_bounds)
		if crosses:
			var hit: Variant=bounds.intersects_ray(_camera.global_position,focus-_camera.global_position)
			if hit==null: hit=bounds.intersects_ray(_camera.global_position,head-_camera.global_position)
			if hit is Vector3:
				var distance: float=_camera.global_position.distance_squared_to(hit)
				if distance<nearest_distance:
					nearest_distance=distance
					nearest_id=int(record.id)
		retained.append(record)
	for record in retained:
		var material: ShaderMaterial = record.material
		var crosses: bool=bool(record.crosses)
		var mode := "dither" if _mode == "dither" else "alpha" if crosses else "opaque"
		if record.mode != mode:
			_apply_mode(material, mode)
			record.mode = mode
		if mode=="alpha": material.set_shader_parameter("occlusion_secondary",int(record.id)!=nearest_id)
		material.set_shader_parameter("room_front_wall_hidden",bool(record.front_wall))
		material.set_shader_parameter("occlusion_enabled", mode != "opaque")
		material.set_shader_parameter("occlusion_focus", _body.global_position + Vector3.UP * 0.7)
		material.set_shader_parameter("occlusion_camera", _camera.global_position)
	_walls = retained

func _current_room_bounds() -> AABB:
	var result := AABB()
	var best := INF
	var pos := _body.global_position
	for id: String in _room_manifest.rooms:
		var box: Array=_room_manifest.rooms[id]
		var elevation: float=float(_room_manifest.room_elevations.get(id,_room_manifest.elevation))
		if absf(pos.y-elevation)>0.6: continue
		var low := Vector3(float(box[0]),elevation,-float(box[3]))
		var high := Vector3(float(box[2]),elevation+float(_room_manifest.room_clear_heights.get(id,_room_manifest.clear_height)),-float(box[1]))
		if pos.x<low.x-0.3 or pos.x>high.x+0.3 or pos.z<low.z-0.3 or pos.z>high.z+0.3: continue
		var area: float=(high.x-low.x)*(high.z-low.z)
		if area<best:
			best=area
			result=AABB(low,high-low)
	return result

func _is_front_wall(wall: AABB, room: AABB) -> bool:
	if room.size==Vector3.ZERO: return false
	var center := wall.get_center()
	if center.y<room.position.y or center.y>room.end.y: return false
	var x: float=room.end.x if _front_x>0 else room.position.x
	var z: float=room.end.z if _front_z>0 else room.position.z
	var x_side: bool=wall.size.x<0.8 and absf(center.x-x)<0.5 and wall.end.z>=room.position.z-0.35 and wall.position.z<=room.end.z+0.35
	var z_side: bool=wall.size.z<0.8 and absf(center.z-z)<0.5 and wall.end.x>=room.position.x-0.35 and wall.position.x<=room.end.x+0.35
	return x_side or z_side
