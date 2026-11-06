extends Node3D
## Formal room presentation for the authored five-day Core campaign.
## Core owns facts, decisions and work; Godot owns navigation in the one loaded room.
## Doorways hand the remaining logical journey to Core under a short visual cover.
const BRIDGE = preload("res://scenes/main/SessionBridge.cs")
const READER = preload("res://scenes/main/DvPresentationContent.cs")
const BODY = preload("res://scenes/components/characters/StoryBody3D.gd")
const TRANSITION = preload("res://scenes/components/characters/RoomTransition.tscn")

var bridge
var body: CharacterBody3D
var camera: Camera3D
var ready_for_play := false
var last_failure := ""
var phase := "loading"
var definition: Dictionary = {}
var settings: Dictionary = {}
var state: Dictionary = {}
var native_book_ui: CanvasLayer
var player_paused := false
var room_id := ""
var controller_ai := false
var time_scale := 1.0
var feedback := ""
var _receipt_feedback_headline := ""
var _receipt_completion_text := ""
var _receipt_feedback_pending := false
var visited: Dictionary = {}
var journal: Array = []
var journal_dropped_total := 0
var selected_focus := ""
var _room: Node3D
var _region: NavigationRegion3D
var _navigation_map := RID()
var _room_ground_anchor := Vector3(INF, INF, INF)
var _components: Dictionary = {}
var _models: Dictionary = {}
var _actors: Dictionary = {}
var _bodies: Dictionary = {}
var _actions: Dictionary = {}
var _commands: Dictionary = {}
var _npc_paths: Dictionary = {}
var _npc_work: Dictionary = {}
var _npc_escorts: Dictionary = {}
var _path := PackedVector3Array()
var _pending_action := ""
var _pending_evidence: Array[String] = []
var _pending_deduction := false
var _pending_target := Vector3.ZERO
var _movement_checkpoint := Vector3.ZERO
var _movement_stalled := 0.0
var _last_ui := 0.0
var _last_room_refresh := 0.0
var _last_day := 0
var _finished := false
var _closed := false
var _room_loading := false
var _known_before: Dictionary = {}
var _restored_position: Variant
var _camera_yaw := 0.46
var _camera_size := 12.0
var _processed_receipts := 0
var _step_count := 0
var _simulation_usec := 0
var _started_usec := 0
var _last_visual_action := ""
var _source_path := ""
var _navigation_load: Dictionary = {}
var _previous_3d_scale := -1.0
var _autosave_pending := false
var _last_saved_time := -1.0
var _transition: CanvasLayer
var _transition_busy := false
var _portal: Dictionary = {}
var _navigation: Dictionary = {}
var _navigation_history: Array = []
var _arrival_queue: Dictionary = {}
var _physical_clock_pending := false
var _manual_arrived := false
var _manual_navigation: Dictionary = {}

func configure(path: String, restored: Dictionary = {}) -> void:
	_source_path = path
	bridge = BRIDGE.new()
	add_child(bridge)
	definition = bridge.Open(path)
	var presentation: Dictionary = definition.get("presentation", {})
	var reader = READER.new()
	var record: Dictionary = reader.ReadRecord(str(presentation.get("source", "res://assets/data/presentation/campaign_rooms.dv")), "data", str(presentation.get("definition", "campaign.rooms")))
	settings = record.get("value", {})
	if settings.is_empty():
		last_failure = "五日场景映射无法读取。"
		return
	for argument in OS.get_cmdline_user_args():
		if argument == "--dv-controller=ai": controller_ai = true
	for actor: Dictionary in definition.get("actors", []):
		_actors[str(actor.id)] = actor
		for action: Dictionary in actor.get("actions", []): _actions[str(action.id)] = action
	if not _actors.has(_viewer()):
		last_failure = "正式内容没有侦探角色。"
		return
	if not restored.is_empty():
		if int(restored.get("version", 0)) != 2 or not restored.get("core") is String:
			last_failure = "存档格式无效。"
			return
		var error: String = bridge.PrepareCheckpoint(str(restored.core), false)
		if not error.is_empty() or not bridge.CommitPreparedCheckpoint():
			last_failure = "存档无法恢复：" + error
			return
		var view: Dictionary = restored.get("campaign_view", {})
		visited = view.get("visited", {}).duplicate(true)
		journal = view.get("journal", []).duplicate(true)
		journal_dropped_total = int(view.get("journal_dropped_total", 0))
		_navigation_history = view.get("navigation_history", []).duplicate(true)
		_restored_position = view.get("position")
	bridge.SetPlayerAutonomy(controller_ai)
	time_scale = float(settings.get("auto_speed", 128.0) if controller_ai else settings.get("player_speed", 1.0))
	if not restored.is_empty() and not controller_ai:
		var restored_view: Dictionary = restored.get("campaign_view", {})
		player_paused = bool(restored_view.get("paused", false))
		time_scale = clampf(float(restored_view.get("speed", time_scale)), 1.0, 1024.0)
	var art: Dictionary = reader.ReadRecord(str(settings.model_source), "layouts", str(settings.model_definition))
	_models = art.get("models", {})
	if _models.is_empty():
		last_failure = "人物模型定义无法读取。"
		return
	if RenderingServer.get_video_adapter_name().to_lower().contains("llvmpipe"):
		_previous_3d_scale = get_viewport().scaling_3d_scale
		get_viewport().scaling_3d_scale = minf(_previous_3d_scale, float(settings.get("software_render_scale", 0.6)))
	add_child(preload("res://scenes/components/rendering/ObserverEnvironment3D.gd").new())
	_transition = TRANSITION.instantiate()
	add_child(_transition)
	camera = Camera3D.new()
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.near = 0.05
	camera.far = 120
	add_child(camera)
	camera.current = true
	state = bridge.Snapshot()
	_known_before = _knowledge_index()
	var initial_room := _actor_room(_viewer())
	if not settings.rooms.has(initial_room):
		last_failure = "缺少正式房间映射：" + initial_room
		return
	await _load_room(initial_room)
	if not last_failure.is_empty(): return
	native_book_ui = preload("res://scenes/UIpanel/BookShell/BookShellUI.tscn").instantiate()
	add_child(native_book_ui)
	native_book_ui.configure(self)
	_started_usec = Time.get_ticks_usec()
	ready_for_play = true
	phase = "idle"
	_refresh(true)

func _viewer() -> String:
	return str(definition.get("viewer", "detective"))

func _actor_visible(actor: String) -> bool:
	return not bool(_actors.get(actor, {}).get("hidden", false)) or room_id in settings.get("visible_hidden_rooms", {}).get(actor, [])

func _actor_room(actor: String) -> String:
	var fact: Dictionary = bridge.PhysicalFact(actor, "state", "room")
	return str(fact.get("Value", ""))

func _load_room(next_room: String, from_room: String = "") -> void:
	_room_loading = true
	phase = "loading"
	bridge.ConfigureVisibleRoom("")
	_path.clear()
	_pending_action = ""
	_navigation.clear()
	_arrival_queue.clear()
	_manual_arrived = false
	_manual_navigation.clear()
	_npc_paths.clear()
	_npc_work.clear()
	_npc_escorts.clear()
	_room_ground_anchor = Vector3(INF, INF, INF)
	for actor_body in _bodies.values():
		if is_instance_valid(actor_body): actor_body.free()
	_bodies.clear()
	body = null
	_components.clear()
	if is_instance_valid(_region): _region.free()
	if _navigation_map.is_valid():
		NavigationServer3D.free_rid(_navigation_map)
		_navigation_map = RID()
	if is_instance_valid(_room): _room.free()
	var spec: Dictionary = settings.rooms.get(next_room, {})
	var resource := str(spec.get("scene", ""))
	if spec.is_empty() or not ResourceLoader.exists(resource):
		last_failure = "房间资源未就绪：" + next_room + " / " + resource
		_room_loading = false
		return
	var packed = load(resource) as PackedScene
	if packed == null:
		last_failure = "房间场景无法载入：" + next_room
		_room_loading = false
		return
	_room = packed.instantiate() as Node3D
	_room.set_meta("campaign_room", next_room)
	_room.set_meta("binding_source", "res://assets/data/presentation/campaign_rooms.dv")
	add_child(_room)
	room_id = next_room
	visited[room_id] = true
	var instances := _room.get_node_or_null("Instances")
	if instances != null:
		_install_room_extras(instances, spec)
		for component in instances.get_children():
			if not component is Node3D: continue
			var id := str(component.get("instance_id"))
			_components[id] = component
			_add_pick_surface(component)
			for collider in component.find_children("*", "CollisionObject3D", true, false): collider.set_meta("campaign_component", id)
		_install_interaction_slots(spec)
	await get_tree().physics_frame
	_bake_navigation(spec)
	await get_tree().physics_frame
	await get_tree().physics_frame
	# Godot can build navigation map iterations asynchronously. Finish this
	# room's one-time synchronization before querying a spawn or saved position.
	NavigationServer3D.map_force_update(_region.get_navigation_map())
	# A room switch can happen during the physics signal itself; the queued
	# region attachment then reaches the server at the following process frame.
	# Never project the spawn against an empty, first map iteration.
	var probe := Vector3(0.0, 0.0, -float(spec.depth) * 0.5)
	for sync_attempt in range(4):
		if NavigationServer3D.map_get_closest_point_owner(_region.get_navigation_map(), probe).is_valid(): break
		await get_tree().process_frame
		NavigationServer3D.map_force_update(_region.get_navigation_map())
	if not NavigationServer3D.map_get_closest_point_owner(_region.get_navigation_map(), probe).is_valid():
		last_failure = "房间导航尚未完成登记：" + room_id
		_room_loading = false
		return
	_navigation_load = {"iteration": NavigationServer3D.map_get_iteration_id(_region.get_navigation_map()), "restored": _restored_position}
	if _region.navigation_mesh.get_polygon_count() == 0:
		last_failure = "房间没有可行走地面：" + room_id
		_room_loading = false
		return
	if not _set_room_ground_anchor(spec):
		_room_loading = false
		return
	body = _spawn_actor(_viewer(), 0)
	_navigation_load["initial_position"] = [body.position.x, body.position.y, body.position.z] if body != null else []
	if body == null:
		_room_loading = false
		return
	if _restored_position is Array and _restored_position.size() == 3:
		var saved := Vector3(float(_restored_position[0]), float(_restored_position[1]), float(_restored_position[2]))
		var safe := _floor_point(saved)
		_navigation_load["saved_projection"] = [safe.x, safe.y, safe.z]
		if safe.distance_to(saved) < 0.6: body.position = safe
		_restored_position = null
	elif not from_room.is_empty():
		_place_at_entrance(from_room)
		if not last_failure.is_empty():
			_room_loading = false
			return
	bridge.ConfigureVisibleRoom(next_room)
	_sync_actors()
	if not last_failure.is_empty():
		_room_loading = false
		return
	# Offscreen work has already started in Core. Display its existing position
	# at the real fixture without restarting or completing that work.
	for actor in _bodies:
		if actor != _viewer(): _place_existing_work(actor)
	_report_resident_positions()
	_camera_size = float(spec.get("camera_size", maxf(float(spec.width), float(spec.depth)) * 1.05))
	_update_camera()
	selected_focus = ""
	_room_loading = false
	phase = "idle"

func _place_at_entrance(from_room: String) -> void:
	var spec: Dictionary = settings.rooms[room_id]
	var mapping: Dictionary = spec.get("entry_from", {})
	var entry: Dictionary = mapping.get(from_room, mapping.get("*", {}))
	var node: Node3D = _components.get(str(entry.get("component", "")), _exit_node())
	var slot := str(entry.get("slot", "Front"))
	var actual_slot := ""
	var point := body.position
	if not is_instance_valid(node) or node == body:
		last_failure = "目标房间没有可用的入口门：" + room_id
		return
	if is_instance_valid(node):
		var marker = node.get_interaction_slot(slot) if node.has_method("get_interaction_slot") else null
		point = _reachable_interaction_point(node, marker, body.position) if marker is Marker3D else _target_point_for_node(node)
		var safe := _floor_point(point)
		var route := _route(body.position, safe)
		if safe.distance_to(point) > 0.3 or (body.position.distance_to(safe) > 0.35 and (route.is_empty() or route[route.size() - 1].distance_to(safe) > 0.35)):
			last_failure = "入口未连接到房间通路：" + room_id
			return
		if node.has_method("get_interaction_slot"):
			for candidate_slot in [slot, "Front", "Back"]:
				var candidate = node.get_interaction_slot(candidate_slot)
				if candidate is Marker3D:
					var requested := Vector3(candidate.global_position.x, 0.0, candidate.global_position.z)
					if _floor_point(requested).distance_to(safe) < 0.025 and _floor_point(requested).distance_to(requested) <= 0.3:
						actual_slot = candidate_slot
						break
		body.position = safe
		body.face_world_direction(Vector3(0, 0, -float(spec.depth) * 0.5) - safe)
	_navigation_load["entrance"] = {"from_room": from_room, "component": str(node.get("instance_id")), "requested_slot": slot, "slot": actual_slot, "position": _vector(body.position), "topology_ref": spec.get("topology_ref", "")}

func _place_existing_work(actor: String) -> void:
	var activity: Dictionary = bridge.ActorActivity(actor)
	if str(activity.get("phase", "")) != "working" or str(activity.get("authority", "")) != "core": return
	var action: Dictionary = _actions.get(str(activity.get("action", "")), {})
	var target := _navigation_target(action, actor)
	if target.x == INF: return
	var actor_body: CharacterBody3D = _bodies[actor]
	var floor := _floor_point(target)
	if floor.distance_to(target) <= 0.4 and _path_connects(_route(actor_body.position, floor), actor_body.position, floor):
		actor_body.position = floor

func _report_resident_positions() -> void:
	for actor in _bodies:
		var actor_body: CharacterBody3D = _bodies[actor]
		bridge.ReportActorPlacement(str(actor), actor_body.position.x, actor_body.position.y, actor_body.position.z, actor_body.rotation.y, room_id)

func _vector(value: Vector3) -> Array:
	return [value.x, value.y, value.z]

func _install_room_extras(instances: Node, spec: Dictionary) -> void:
	for placement: Dictionary in spec.get("extra_components", []):
		var source := str(placement.get("source_scene", ""))
		if not ResourceLoader.exists(source):
			last_failure = "已声明家具资源未就绪：" + source
			continue
		var donor: Node = (load(source) as PackedScene).instantiate()
		var donor_instances := donor.get_node_or_null("Instances")
		if donor_instances != null:
			for candidate in donor_instances.get_children():
				if str(candidate.get("component_kind")) != str(placement.get("source_kind", "")): continue
				var added := candidate.duplicate() as Node3D
				added.set("instance_id", str(placement.id))
				var p: Array = placement.get("position", [0, 0, 0])
				added.position = Vector3(float(p[0]), float(p[1]), float(p[2]))
				added.rotation = Vector3(0, deg_to_rad(float(placement.get("yaw", 0))), 0)
				instances.add_child(added)
				break
		donor.free()

func _install_interaction_slots(spec: Dictionary) -> void:
	for placement: Dictionary in spec.get("interaction_slots", []):
		var component = _components.get(str(placement.get("component", "")))
		if not is_instance_valid(component):
			last_failure = "交互站位缺少家具：" + str(placement.get("component", ""))
			continue
		var marker = component.get_interaction_slot(str(placement.get("slot", "Front")))
		var point: Array = placement.get("position", [])
		if marker is Marker3D and point.size() == 3:
			marker.position = Vector3(float(point[0]), float(point[1]), float(point[2]))
		else: last_failure = "家具交互站位声明无效：" + str(placement.get("component", ""))

func _add_pick_surface(component: Node3D) -> void:
	# Interaction picking follows the imported mesh bounds. This separate layer
	# neither blocks walking nor contributes fabricated furniture to navigation.
	if not component.visible or str(component.get("component_kind")) in ["architecture", "complete_shell"]: return
	var bounds := AABB()
	var has_bounds := false
	for mesh: MeshInstance3D in component.find_children("*", "MeshInstance3D", true, false):
		if mesh.mesh == null: continue
		var relative: Transform3D = component.global_transform.affine_inverse() * mesh.global_transform
		var box: AABB = relative * mesh.get_aabb()
		bounds = bounds.merge(box) if has_bounds else box
		has_bounds = true
	if not has_bounds or bounds.size.length_squared() < 0.0001: return
	var pick_body := StaticBody3D.new()
	pick_body.name = "CampaignPickSurface"
	pick_body.collision_layer = 4
	pick_body.collision_mask = 0
	component.add_child(pick_body)
	var shape := CollisionShape3D.new()
	var box_shape := BoxShape3D.new()
	box_shape.size = Vector3(maxf(0.10, bounds.size.x), maxf(0.10, bounds.size.y), maxf(0.10, bounds.size.z))
	shape.shape = box_shape
	shape.position = bounds.get_center()
	pick_body.add_child(shape)

func _bake_navigation(spec: Dictionary) -> void:
	_region = NavigationRegion3D.new()
	var mesh := NavigationMesh.new()
	mesh.agent_radius = 0.23
	mesh.agent_height = float(settings.get("actor_height", 1.32))
	mesh.agent_max_climb = 0.24
	mesh.agent_max_slope = 45
	mesh.cell_size = 0.12
	mesh.cell_height = 0.08
	mesh.geometry_parsed_geometry_type = NavigationMesh.PARSED_GEOMETRY_STATIC_COLLIDERS
	mesh.geometry_collision_mask = 1
	mesh.filter_baking_aabb = AABB(Vector3(-float(spec.width) / 2.0 + 0.08, -0.35, -float(spec.depth) + 0.08), Vector3(float(spec.width) - 0.16, float(spec.get("navigation_height", 1.8)), float(spec.depth) - 0.16))
	var geometry := NavigationMeshSourceGeometryData3D.new()
	NavigationServer3D.parse_source_geometry_data(mesh, geometry, _room)
	NavigationServer3D.bake_from_source_geometry_data(mesh, geometry)
	_region.navigation_mesh = mesh
	# This streamed room owns its path map. The title's separate 3D viewport
	# must not determine when room navigation becomes queryable.
	_navigation_map = NavigationServer3D.map_create()
	NavigationServer3D.map_set_active(_navigation_map, true)
	NavigationServer3D.map_set_cell_size(_navigation_map, mesh.cell_size)
	NavigationServer3D.map_set_cell_height(_navigation_map, mesh.cell_height)
	NavigationServer3D.map_set_use_async_iterations(_navigation_map, false)
	_region.set_navigation_map(_navigation_map)
	add_child(_region)

func _spawn_actor(actor: String, index: int) -> CharacterBody3D:
	var actor_definition: Dictionary = _actors[actor]
	var key := str(settings.get("actor_models", {}).get(actor, actor_definition.get("model_key", actor)))
	if not _models.has(key):
		last_failure = "人物缺少已声明模型：" + actor + " / " + key
		return null
	var model: Dictionary = _models[key].duplicate(true)
	model.character_id = actor
	var actor_body: CharacterBody3D = BODY.new()
	add_child(actor_body)
	if not actor_body.configure(model, float(model.get("height", settings.get("actor_height", 1.32))), float(settings.get("collision_radius", 0.18))):
		actor_body.queue_free()
		last_failure = "人物模型无法加载：" + actor
		return null
	actor_body.set_meta("campaign_actor", actor)
	actor_body.collision_mask = 1
	actor_body.visible = _actor_visible(actor)
	if not actor_body.visible: actor_body.collision_layer = 0
	var spec: Dictionary = settings.rooms[room_id]
	var spawn: Array = spec.get("spawn", [0.0, 0.0, -1.0])
	var point := Vector3(float(spawn[0]), float(spawn[1]), float(spawn[2]))
	if index > 0:
		var width := float(spec.width)
		point = Vector3(clampf((float(index % 5) - 2.0) * 1.0, -width / 2.0 + 0.75, width / 2.0 - 0.75), 0.0, -1.0 - float(index / 5) * 0.85)
	var grounded := _spawn_floor_point(point)
	if grounded.x == INF:
		actor_body.queue_free()
		last_failure = "人物没有连接到房门的地面站位：" + actor + " / " + room_id
		return null
	actor_body.position = grounded
	actor_body.play_action("idle")
	_bodies[actor] = actor_body
	if _actor_visible(actor):
		var spawns: Array = _navigation_load.get("resident_spawns", [])
		spawns.append({"actor": actor, "requested": _vector(point), "position": _vector(grounded), "connected_to_entrance": true})
		_navigation_load["resident_spawns"] = spawns
	var label := Label3D.new()
	label.name = "CampaignActorName"
	label.text = str(actor_definition.get("name", actor))
	var label_font := str(settings.get("label_font", ""))
	if not label_font.is_empty() and ResourceLoader.exists(label_font): label.font = load(label_font)
	label.position.y = float(model.get("height", settings.get("actor_height", 1.32))) + 0.24
	label.font_size = 36
	label.pixel_size = 0.006
	label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	label.no_depth_test = true
	label.modulate = Color("e7cfa5") if actor == _viewer() else Color("efe7d5")
	actor_body.add_child(label)
	return actor_body

func _sync_actors() -> void:
	for actor in _bodies.keys():
		if actor != _viewer() and _actor_room(actor) != room_id:
			_bodies[actor].queue_free()
			_bodies.erase(actor)
			_npc_paths.erase(actor)
			_npc_work.erase(actor)
	var index := 1
	for actor in _actors:
		if actor == _viewer() or _actor_room(actor) != room_id: continue
		# Hidden residents still execute real room physics. Their invisible body
		# has no pick surface and is excluded from every public projection.
		if not _bodies.has(actor):
			var arriving := _spawn_actor(actor, index)
			if arriving != null and not _room_loading:
				var entrance := _target_point_for_node(_exit_node(), actor)
				if _floor_point(entrance).distance_to(entrance) <= 0.3: arriving.position = _floor_point(entrance)
				_place_existing_work(actor)
				bridge.ReportActorPlacement(str(actor), arriving.position.x, arriving.position.y, arriving.position.z, arriving.rotation.y, room_id)
		index += 1
	_update_actor_labels()

func _update_actor_labels() -> void:
	if not is_instance_valid(body): return
	var nearby: Array = []
	for actor in _bodies:
		if actor != _viewer() and _actor_visible(actor): nearby.append({"id": actor, "distance": body.position.distance_to(_bodies[actor].position)})
	nearby.sort_custom(func(a, b): return float(a.distance) < float(b.distance))
	var shown := {_viewer(): true}
	for entry in nearby.slice(0, 2):
		if float(entry.distance) <= 2.2: shown[str(entry.id)] = true
	var focused := selected_focus.split(".")[0]
	if _bodies.has(focused): shown[focused] = true
	for actor in _bodies:
		var label := _bodies[actor].get_node_or_null("CampaignActorName") as Label3D
		if label != null: label.visible = _actor_visible(actor) and shown.has(actor)

func _physics_process(delta: float) -> void:
	if not ready_for_play or _closed or _room_loading or not last_failure.is_empty(): return
	if _transition_busy:
		if str(_portal.get("stage", "")) == "travelling": _step_portal()
		_update_camera()
		return
	if player_paused and _physical_clock_pending:
		# Pausing stops new movement; already executed motion still owns time.
		_advance_clock(0.0, true)
		_physical_clock_pending = float(bridge.PendingPhysicalSeconds()) > 0.000001
		if not _physical_clock_pending: _finish_physical_arrivals()
	if not _finished and not player_paused:
		if _physical_clock_pending:
			_advance_clock(0.0, true)
			_physical_clock_pending = float(bridge.PendingPhysicalSeconds()) > 0.000001
			if not _physical_clock_pending: _finish_physical_arrivals()
		else:
			_prepare_navigation()
			var physical := bool(bridge.HasHostNavigation()) or _manual_motion_requested()
			_move_player(delta)
			_move_npcs(delta)
			if physical:
				_advance_clock(delta * _navigation_factor(), true)
				_physical_clock_pending = float(bridge.PendingPhysicalSeconds()) > 0.000001
				if not _physical_clock_pending: _finish_physical_arrivals()
			else:
				_advance_clock(delta * time_scale, false)
		if not _transition_busy: _detect_portal()
		if not _finished and _completion_met():
			bridge.ClearPendingTime()
			_finished = true
	else:
		for actor_body in _bodies.values(): actor_body.stop_and_hold_position()
	if _transition_busy: return
	_update_camera()
	_last_room_refresh += delta
	if _last_room_refresh >= 0.15:
		_last_room_refresh = 0.0
		var actual_room := _actor_room(_viewer())
		if actual_room != room_id:
			if not settings.rooms.has(actual_room): last_failure = "未绑定的正式目的地：" + actual_room; return
			# An escort can move the detective as another actor's real effect.
			_start_portal({"execution": 0, "to_room": actual_room, "remaining_seconds": 0.0})
			return
		_sync_actors()
	_last_ui += delta
	if _last_ui >= 0.25:
		_last_ui = 0.0
		_refresh(false)
		if _autosave_pending and not controller_ai and _pending_action.is_empty() and _activity().is_empty() and _actor_room(_viewer()) == room_id:
			_write_progress(true)

func _navigation_factor() -> float:
	return minf(5.0, maxf(1.0, sqrt(time_scale)))

func _manual_motion_requested() -> bool:
	if controller_ai or not is_instance_valid(body): return false
	if not _path.is_empty() or _manual_arrived: return true
	if not _activity().is_empty() or (is_instance_valid(native_book_ui) and native_book_ui.book_open()): return false
	return Input.get_vector("move_left", "move_right", "move_up", "move_down").length_squared() > 0.01

func _advance_clock(input_seconds: float, physical: bool) -> void:
	var started := Time.get_ticks_usec()
	var max_slices := int(settings.get("maximum_slices_per_frame", 160))
	for index in range(max_slices):
		var update: Dictionary = bridge.StepPhysics(input_seconds if index == 0 else 0.0) if physical else bridge.Step(input_seconds if index == 0 else 0.0, false, false)
		_step_count += 1
		_consume_update(update)
		var campaign: Dictionary = bridge.CampaignProgress()
		var window_ended := bool(campaign.get("window_completed", false))
		var legacy_cutoff := str(campaign.get("mode", "facts")) != "observation_window" and float(update.get("game_seconds", 0.0)) >= float(definition.get("run", {}).get("end_minutes", INF)) * 60.0
		if window_ended or legacy_cutoff:
			bridge.ClearPendingTime()
			for drain in range(24): _consume_update(bridge.Step(0.0, false, false))
			_finished = true
			break
		# A newly dispatched visible action must get an actual path before time
		# can advance. Physical seconds already earned are drained separately.
		if not physical and bool(update.get("has_host_navigation", false)): break
		if not _physical_clock_pending and not physical:
			var activity: Dictionary = bridge.ActorActivity(_viewer())
			if str(activity.get("phase", "")) == "transit" and str(activity.get("to_room", room_id)) != room_id: break
		if index > 0 and not bool(update.get("decision_pending", false)) and float(bridge.PendingSimulationSeconds()) <= 0.000001 and update.get("commands", []).is_empty() and update.get("receipts", []).is_empty(): break
		if Time.get_ticks_usec() - started >= int(settings.get("frame_simulation_budget_usec", 9000)): break
	_simulation_usec += Time.get_ticks_usec() - started

func _navigation_target(action: Dictionary, actor: String) -> Vector3:
	var actor_body: CharacterBody3D = _bodies[actor]
	var escort := str(action.get("escort", ""))
	if not escort.is_empty():
		return _target_point(escort, actor) if _bodies.has(escort) else Vector3(INF, INF, INF)
	if str(action.get("kind", "")) in ["walk", "travel"]:
		return _target_point_for_node(_exit_node(), actor)
	var focus := str(action.get("focus", actor))
	if bool(_binding(focus).get("self", false)) or _private_cognitive_action(str(action.get("id", ""))): return actor_body.position
	if not is_instance_valid(_target_node(focus)): return Vector3(INF, INF, INF)
	return _target_point(focus, actor)

func _path_length(from: Vector3, path: PackedVector3Array, target: Vector3) -> float:
	var result := 0.0
	var prior := from
	for point in path:
		result += prior.distance_to(point)
		prior = point
	return result + prior.distance_to(target)

func _prepare_navigation() -> void:
	for actor in _bodies:
		var activity: Dictionary = bridge.ActorActivity(actor)
		if not bool(activity.get("requires_arrival", false)):
			_navigation.erase(actor)
			_arrival_queue.erase(actor)
			continue
		var execution := int(activity.get("execution", 0))
		if int(_navigation.get(actor, {}).get("execution", 0)) == execution: continue
		var actor_body: CharacterBody3D = _bodies[actor]
		var action: Dictionary = _actions.get(str(activity.get("action", "")), {})
		var target := _navigation_target(action, actor)
		if target.x == INF:
			bridge.Fail(execution, "target_not_present")
			_record_navigation({"actor": actor, "execution": execution, "action": str(activity.get("action", "")), "focus": str(action.get("focus", "")), "room": room_id, "result": "target_not_present", "game_seconds": state.get("game_seconds", 0.0)})
			if actor == _viewer(): feedback = "现场暂时没有对应人物或物件：" + focus_label(str(action.get("focus", "")))
			continue
		var path := _route(actor_body.position, target)
		if actor_body.position.distance_to(target) > 0.35 and (path.is_empty() or path[path.size() - 1].distance_to(target) > 0.35):
			# A blocked real route fails honestly, without an arrival callback.
			bridge.Fail(execution, "navigation_unreachable")
			if actor == _viewer(): feedback = "到达目标的通路被挡住了，请换一条通路。"
			_record_navigation({"actor": actor, "execution": execution, "action": str(activity.get("action", "")), "room": room_id, "result": "unreachable", "game_seconds": state.get("game_seconds", 0.0)})
			continue
		var distance := _path_length(actor_body.position, path, target)
		if not bridge.SetNavigationTarget(execution, room_id, target.x, target.y, target.z, distance, float(settings.get("walking_speed", 2.4))): continue
		actor_body.interrupt_action()
		_navigation[actor] = {"execution": execution, "action": str(activity.get("action", "")), "path": path, "target": target, "start_position": _vector(actor_body.position), "start_game_seconds": state.get("game_seconds", 0.0), "path_length": distance, "travelled": 0.0, "physical_seconds": 0.0, "checkpoint": actor_body.position, "stalled": 0.0, "escort": str(action.get("escort", "")), "stage": "contact" if not str(action.get("escort", "")).is_empty() else "target"}

func _finish_physical_arrivals() -> void:
	for actor in _arrival_queue.keys():
		if not _navigation.has(actor) or not _bodies.has(actor): continue
		var nav: Dictionary = _navigation[actor]
		var actor_body: CharacterBody3D = _bodies[actor]
		if not bridge.Arrive(int(nav.execution), actor_body.position.x, actor_body.position.y, actor_body.position.z):
			var current: Dictionary = bridge.ActorActivity(actor)
			if bool(current.get("requires_arrival", false)):
				bridge.Fail(int(nav.execution), "arrival_rejected")
				if actor == _viewer(): feedback = "走近时现场条件发生了变化，请重新选择行动。"
			continue
		_record_navigation({"actor": actor, "execution": nav.execution, "action": nav.action, "room": room_id, "start_position": nav.start_position, "end_position": _vector(actor_body.position), "target": _vector(nav.target), "path_length": nav.path_length, "travelled": nav.travelled, "physical_seconds": nav.physical_seconds, "start_game_seconds": nav.start_game_seconds, "end_game_seconds": state.get("game_seconds", 0.0), "result": "arrived", "authority": "godot"})
		_navigation.erase(actor)
	_arrival_queue.clear()
	if _manual_arrived:
		_manual_arrived = false
		if not _manual_navigation.is_empty():
			_manual_navigation["end_game_seconds"] = state.get("game_seconds", 0.0)
			_manual_navigation["end_position"] = _vector(body.position)
			_manual_navigation["result"] = "arrived"
			_record_navigation(_manual_navigation)
			_manual_navigation = {}
		if not _pending_action.is_empty():
			var error := _submit_pending()
			if not error.is_empty(): feedback = error
		else: phase = "idle"

func _record_navigation(record: Dictionary) -> void:
	if str(record.get("actor", "")) != _viewer() and not _actor_visible(str(record.get("actor", ""))): return
	if str(record.get("actor", "")) != _viewer() and record.has("action"):
		var id := str(record.action)
		record["action"] = "thinking" if _private_cognitive_action(id) else str(_actions.get(id, {}).get("kind", "interact"))
	_navigation_history.append(record.duplicate(true))
	while _navigation_history.size() > 1024: _navigation_history.pop_front()

func _detect_portal() -> void:
	var activity: Dictionary = bridge.ActorActivity(_viewer())
	if str(activity.get("phase", "")) == "transit" and str(activity.get("to_room", room_id)) != room_id:
		_start_portal(activity)

func _start_portal(activity: Dictionary) -> void:
	if _transition_busy or _closed: return
	_transition_busy = true
	phase = "transition"
	var target := str(activity.get("to_room", _actor_room(_viewer())))
	_portal = {"execution": int(activity.get("execution", 0)), "from": room_id, "to": target, "stage": "covering", "start_game_seconds": state.get("game_seconds", 0.0), "duration": float(activity.get("remaining_seconds", 0.0)), "completed": _actor_room(_viewer()) == target, "committed": _actor_room(_viewer()) == target}
	_path.clear()
	_pending_action = ""
	if is_instance_valid(native_book_ui): native_book_ui.close_book()
	for actor_body in _bodies.values(): actor_body.stop_and_hold_position()
	await _transition.cover(room_label(room_id), room_label(target))
	if _closed: return
	# All physical seconds were consumed before Arrive. The unloaded portion
	# can now run in Core without counting the renderer's fade/load time.
	bridge.ClearPendingTime()
	bridge.ConfigureVisibleRoom("")
	_portal["stage"] = "travelling"
	_transition.set_progress(0.15, "沿通路前行")

func _step_portal() -> void:
	if bool(_portal.get("completed", false)):
		_complete_portal()
		return
	var started := Time.get_ticks_usec()
	for index in range(int(settings.get("maximum_slices_per_frame", 160))):
		var activity: Dictionary = bridge.ActorActivity(_viewer())
		var remaining := float(activity.get("remaining_seconds", 0.0))
		if remaining <= 0.000001: bridge.ClearPendingTime()
		var input_seconds := minf(20.0, remaining) if float(bridge.PendingSimulationSeconds()) <= 0.000001 else 0.0
		var update: Dictionary = bridge.StepTransit(input_seconds)
		_step_count += 1
		_consume_update(update)
		if bool(_portal.get("completed", false)):
			bridge.ClearPendingTime()
			break
		var duration := maxf(0.001, float(_portal.get("duration", 0.0)))
		_transition.set_progress(0.15 + 0.6 * clampf(1.0 - remaining / duration, 0.0, 1.0), "沿通路前行")
		if activity.is_empty():
			_portal["completed"] = true
			_portal["committed"] = _actor_room(_viewer()) == str(_portal.to)
			break
		if Time.get_ticks_usec() - started >= int(settings.get("frame_simulation_budget_usec", 9000)): break
	_simulation_usec += Time.get_ticks_usec() - started
	if bool(_portal.get("completed", false)): _complete_portal()

func _complete_portal() -> void:
	if str(_portal.get("stage", "")) != "travelling": return
	_portal["stage"] = "loading"
	var target := _actor_room(_viewer())
	if not settings.rooms.has(target):
		last_failure = "未绑定的正式目的地：" + target
		return
	var origin := str(_portal.get("from", room_id))
	_transition.set_progress(0.8, "走入" + room_label(target))
	if target != room_id:
		await _load_room(target, origin)
	else:
		bridge.ConfigureVisibleRoom(room_id)
	if _closed: return
	_record_navigation({"actor": _viewer(), "execution": _portal.get("execution", 0), "from_room": origin, "to_room": target, "start_game_seconds": _portal.get("start_game_seconds", 0.0), "end_game_seconds": state.get("game_seconds", 0.0), "result": "transferred" if bool(_portal.get("committed", false)) else "cancelled", "authority": "core", "loaded_room_count": _loaded_room_count()})
	_refresh(true)
	_portal["stage"] = "revealing"
	await _transition.reveal()
	if _closed: return
	_portal = {}
	_transition_busy = false
	phase = "idle"

func _loaded_room_count() -> int:
	var count := 0
	for child in get_children():
		if child.has_meta("campaign_room"): count += 1
	return count

func _consume_update(update: Dictionary) -> void:
	state["game_seconds"] = update.get("game_seconds", state.get("game_seconds", 0.0))
	for command: Dictionary in update.get("commands", []):
		if str(command.get("kind", "")) == "execute":
			_commands[str(command.actor)] = command
		elif str(command.get("kind", "")) == "cancel":
			_commands.erase(str(command.get("actor", "")))
	for receipt: Dictionary in update.get("receipts", []):
		_processed_receipts += 1
		var actor := str(receipt.get("Actor", ""))
		var action := str(receipt.get("Action", ""))
		var committed := bool(receipt.get("Committed", false))
		var interruption_label := ""
		var interruption = receipt.get("Interruption")
		if actor == _viewer() and not committed and interruption is Dictionary:
			interruption_label = str(interruption.get("Label", ""))
		var narrative := _received_narrative(receipt)
		var completion := _narrative_text(narrative)
		# New receipts carry the exact words and audience from completion time.
		# Only old receipts may resolve the detective's own legacy text here.
		if not receipt.has("Narrative") and actor == _viewer() and committed:
			completion = bridge.CompletionText(action)
		if _transition_busy and actor == _viewer() and int(receipt.get("Execution", -1)) == (int(_portal.get("execution", 0)) & 0xffffffff):
			_portal["completed"] = true
			_portal["committed"] = bool(receipt.get("Committed", false))
		_commands.erase(actor)
		var private_thought := actor != _viewer() and _private_cognitive_action(action)
		var visible_here := _bodies.has(actor) and _actor_room(actor) == room_id and _actor_visible(actor)
		if (not private_thought and (actor == _viewer() or visible_here)) or not narrative.is_empty():
			var public_kind := str(_actions.get(action, {}).get("kind", "interact"))
			var public_actor := actor if actor == _viewer() or visible_here else ""
			var actor_name := str(_actors.get(public_actor, {}).get("name", "现场人物"))
			if actor != _viewer() and not narrative.is_empty():
				actor_name = _narrative_speaker_label(narrative)
				public_kind = "talk"
			var record := {"time": float(receipt.get("Time", state.game_seconds)) / 60.0, "execution": int(receipt.get("Execution", 0)), "actor": public_actor, "actor_name": actor_name, "action": action if actor == _viewer() else public_kind, "kind": public_kind, "label": str(_actions.get(action, {}).get("label", action)) if actor == _viewer() else ("听到发言" if not narrative.is_empty() else _public_action_label(action)), "committed": committed, "reason": str(receipt.get("Reason", "")) if actor == _viewer() else "", "interruption_label": interruption_label, "completion_text": completion, "narrative": narrative, "narrative_source_ids": _narrative_source_ids(narrative), "intent": receipt.get("Intent") if actor == _viewer() else null}
			journal.append(record)
			while journal.size() > 8192:
				journal.pop_front()
				journal_dropped_total += 1
		if actor == _viewer():
			if not controller_ai: _autosave_pending = true
			var outcome := "已完成" if committed else "未完成：" + failure_label(str(receipt.get("Reason", "")))
			if not interruption_label.is_empty(): outcome = "现场机会变化，行动未完成：" + interruption_label
			_receipt_feedback_headline = str(_actions.get(action, {}).get("label", action)) + " · " + outcome
			_receipt_completion_text = completion
			_receipt_feedback_pending = true
			feedback = _receipt_feedback_headline
			if not completion.is_empty(): feedback += "\n" + completion
		elif not narrative.is_empty():
			_receipt_feedback_headline = _narrative_speaker_label(narrative) + " · 发言"
			_receipt_completion_text = completion
			_receipt_feedback_pending = true
			feedback = _receipt_feedback_headline + "\n" + completion

func _received_narrative(receipt: Dictionary) -> Array:
	var result: Array = []
	var entries: Variant = receipt.get("Narrative", [])
	if not entries is Array: return result
	for entry in entries:
		if not entry is Dictionary or str(entry.get("Text", "")).is_empty(): continue
		var recipients: Variant = entry.get("Recipients", [])
		if not recipients is Array or not recipients.has(_viewer()): continue
		result.append(entry.duplicate(true))
	return result

func _narrative_text(entries: Array) -> String:
	var lines: Array[String] = []
	for entry: Dictionary in entries:
		var text := str(entry.get("Text", ""))
		var speaker := str(entry.get("SpeakerLabel", ""))
		if str(entry.get("Kind", "")) == "speech" and not speaker.is_empty() and not text.begins_with(speaker + "：") and not text.begins_with(speaker + ":"):
			text = speaker + "：" + text
		if not text.is_empty() and not lines.has(text): lines.append(text)
	return "\n".join(lines)

func _narrative_speaker_label(entries: Array) -> String:
	var names: Array[String] = []
	for entry: Dictionary in entries:
		var name := str(entry.get("SpeakerLabel", ""))
		if not name.is_empty() and not names.has(name): names.append(name)
	return "、".join(names) if not names.is_empty() else "现场声音"

func _narrative_source_ids(entries: Array) -> Array:
	var result: Array = []
	for entry: Dictionary in entries:
		var source_id := str(entry.get("EvidenceId", ""))
		if not source_id.is_empty() and not result.has(source_id): result.append(source_id)
	return result

func failure_label(reason: String) -> String:
	return str({"deduction_unproved": "所选证据不足以推出结论", "independent_opinion_rejected": "对方评估后仍有顾虑", "suggestion_rejected": "对方评估后仍有顾虑", "evidence_missing": "尚未出示必要证据", "objective_condition_failed": "现场条件已经变化，行动未能完成", "target_not_present": "目标人物或物件已不在当前现场", "navigation_unreachable": "当前没有通往目标的可行走通路", "arrival_rejected": "到达后未能满足现场交互条件", "navigation_path_lost": "接近期间通路发生变化", "navigation_stalled": "实际走动被阻挡，需要换个接近方向", "escort_target_missing": "同行者已不在当前现场", "escort_exit_unreachable": "同行者无法沿当前通路走到门边", "escort_route_blocked": "同行者在实际走动中受阻"}.get(reason, reason))

func _refresh(force: bool) -> void:
	if not is_instance_valid(bridge): return
	state = bridge.Snapshot()
	state["mechanic_history"] = state.get("mechanic_history", []).filter(func(record): return str(record.get("actor", "")) == _viewer() or str(record.get("kind", "")) == "world_event")
	for key in ["deduction", "opinion", "incantation"]:
		var result = state.get(key)
		if result is Dictionary and str(result.get("actor", "")) != _viewer(): state[key] = {}
	state.erase("recent")
	if bool(state.get("campaign_progress", {}).get("completed", false)): _finished = true
	state["active"] = not _finished
	state["day"] = int(float(state.get("game_seconds", 0.0)) / 86400.0) + 1
	state["room"] = room_id
	state["location_title"] = room_label(room_id)
	state["controller"] = "ai" if controller_ai else "player"
	state["campaign_completed"] = bool(state.get("campaign_progress", {}).get("completed", false))
	state["campaign_complete"] = state.campaign_completed
	var known := _knowledge_index()
	var gained: Array = []
	for key in known:
		if (not _known_before.has(key) or str(_known_before[key]) != str(known[key])) and not str(key).begins_with("event:"):
			if known[key] is String:
				var value := str(known[key])
				gained.append(room_label(value) if settings.rooms.has(value) else value)
	if not gained.is_empty():
		var heading := feedback.split("\n")[0]
		if heading.is_empty() or (not _receipt_feedback_pending and not _receipt_feedback_headline.is_empty() and feedback.begins_with(_receipt_feedback_headline)):
			heading = "新的见闻"
		if _receipt_feedback_pending and not _receipt_feedback_headline.is_empty():
			heading = _receipt_feedback_headline
			if not _receipt_completion_text.is_empty():
				feedback = heading + "\n" + _receipt_completion_text
			else: feedback = heading + "\n" + _brief_gained(gained)
		else: feedback = heading + "\n" + _brief_gained(gained)
	_receipt_feedback_pending = false
	_known_before = known
	if is_instance_valid(native_book_ui): native_book_ui.refresh(force)

func _brief_gained(gained: Array) -> String:
	var lines: Array[String] = []
	for value in gained.slice(0, 2):
		var plain := str(value).replace("\n", " ")
		lines.append(plain if plain.length() <= 72 else plain.left(71) + "…")
	return "\n".join(lines)

func _completion_met() -> bool:
	# Core owns the completion contract. An observation window ending says
	# nothing about whether an emergent outcome occurred in that window.
	return bool(bridge.CampaignProgress().get("completed", false))

func _knowledge_index() -> Dictionary:
	var result: Dictionary = {}
	for item: Dictionary in state.get("knowledge", []):
		var key: Dictionary = item.get("key", {})
		var fact: Dictionary = item.get("fact", {})
		if not bool(fact.get("Known", true)) or bool(fact.get("Conflicted", false)): continue
		result[str(key.get("Object", "")) + "." + str(key.get("Facet", "")) + "." + str(key.get("Field", ""))] = fact.get("Value")
	return result

func room_label(id: String) -> String:
	return str(settings.get("rooms", {}).get(id, {}).get("label", definition.get("room_labels", {}).get(id, id)))

func _category(action: Dictionary) -> String:
	if action.has("deduction") or str(action.get("kind", "")) == "deduce": return "deduction"
	if action.has("incantation") or action.has("utterance") or str(action.get("kind", "")) in ["incantation", "utterance", "spell"]: return "incantation"
	if action.has("suggestion") or action.has("opinion") or action.has("persuasion") or str(action.get("kind", "")) in ["persuade", "suggest"]: return "opinion"
	return "action"

func _private_cognitive_action(id: String) -> bool:
	var action: Dictionary = _actions.get(id, {})
	return action.has("strategy") or action.has("deduction") or str(action.get("kind", "")) in ["strategy", "deduce"]

func visible_activity(actor: String) -> String:
	var id := str(bridge.ActorActivity(actor).get("action", ""))
	if actor == _viewer() or id.is_empty(): return id
	return "thinking" if _private_cognitive_action(id) else str(_actions.get(id, {}).get("kind", "interact"))

func _public_action_label(id: String) -> String:
	var action: Dictionary = _actions.get(id, {})
	if action.has("public_label"): return str(action.public_label)
	if _private_cognitive_action(id): return "整理思绪"
	return str({"walk": "走动", "talk": "交谈", "read": "阅读", "inspect": "观察现场", "interact": "处理手边事务", "rest": "休息", "sleep": "休息"}.get(str(action.get("kind", id)), "处理手边事务"))

func visible_activity_label(actor: String) -> String:
	var activity: Dictionary = bridge.ActorActivity(actor)
	var id := str(activity.get("action", ""))
	if id.is_empty(): return "观察周围"
	if bool(activity.get("requires_arrival", false)): return "正在走近：" + (str(_actions.get(id, {}).get("label", "目标")) if actor == _viewer() else "手边事务")
	return str(_actions.get(id, {}).get("label", "观察周围")) if actor == _viewer() else _public_action_label(id)

func visible_progress(actor: String) -> Dictionary:
	var progress: Dictionary = bridge.WorkProgress(actor)
	if actor != _viewer():
		progress.erase("action")
		if visible_activity(actor) == "thinking": return {}
	return progress

func available_actions() -> Array:
	var result: Array = []
	if _finished or not ready_for_play or _transition_busy or _room_loading: return result
	for action: Dictionary in _actors.get(_viewer(), {}).get("actions", []):
		if not bridge.Available(str(action.id)): continue
		var focus := str(action.get("focus", _viewer()))
		result.append({"id": str(action.id), "label": str(action.get("label", action.id)), "focus": focus, "kind": str(action.get("kind", "interact")), "category": _category(action), "minutes": float(action.get("duration_seconds", 60.0)) / 60.0, "target": focus_label(focus), "reason": str(action.get("reason", "")), "escort": str(action.get("escort", ""))})
	return result

func visible_targets() -> Array:
	var result: Array = []
	var grouped: Dictionary = {}
	for action: Dictionary in available_actions():
		var focus := str(action.focus)
		if not grouped.has(focus): grouped[focus] = {"id": focus, "label": focus_label(focus), "actions": []}
		grouped[focus].actions.append(action)
	# Presence is visible even before a conversational topic is known. Do not
	# expose a person's private plan or manufacture a usable interaction.
	for actor in _bodies:
		if actor == _viewer() or not _actor_visible(actor): continue
		if not grouped.has(actor): grouped[actor] = {"id": actor, "label": focus_label(actor), "actions": []}
	for focus in grouped: result.append(grouped[focus])
	return result

func _binding(focus: String) -> Dictionary:
	var bindings: Dictionary = settings.get("focus_bindings", {})
	if bindings.has(focus): return bindings[focus]
	for prefix in settings.get("focus_prefixes", []):
		if focus.begins_with(str(prefix.prefix)): return prefix
	return {}

func focus_label(focus: String) -> String:
	if focus.begins_with("@component:"):
		var component = _components.get(focus.trim_prefix("@component:"))
		if is_instance_valid(component): return str(settings.get("kind_labels", {}).get(str(component.get("component_kind")), "现场物件"))
	var binding := _binding(focus)
	if binding.has("label"): return str(binding.label)
	var actor := focus.split(".")[0]
	if _actors.has(actor): return str(_actors[actor].get("name", actor)) + (" · 随身物" if focus.contains(".") else "")
	if settings.rooms.has(focus): return room_label(focus)
	for fixture: Dictionary in definition.get("fixtures", []):
		if str(fixture.get("id", "")) == focus: return str(fixture.get("label", focus))
	return str(settings.get("kind_labels", {}).get(focus, "现场物件"))

func action_matches_focus(action: Dictionary, focus: String) -> bool:
	if focus.begins_with("@component:"):
		return _action_target_node(action) == _components.get(focus.trim_prefix("@component:"))
	return str(action.focus) == focus or (not focus.contains(".") and str(action.focus).begins_with(focus + "."))

func _action_target_node(action: Dictionary) -> Node3D:
	if not str(action.get("escort", "")).is_empty(): return _target_node(str(action.escort))
	return _exit_node() if str(action.get("kind", "")) == "walk" else _target_node(str(action.get("focus", _viewer())))

func _target_node(focus: String) -> Node3D:
	var binding := _binding(focus)
	if not binding.has("kinds") and not binding.has("component"):
		var actor := focus.split(".")[0]
		if _bodies.has(actor): return _bodies[actor]
	if binding.has("component") and _components.has(str(binding.component)): return _components[str(binding.component)]
	for kind in binding.get("kinds", []):
		for component in _components.values():
			if str(component.get("component_kind")) == str(kind): return component
	if focus.ends_with("-bed"):
		for component in _components.values():
			if str(component.get("component_kind")) in ["bed", "medical_bed"]: return component
	for suffix in {"-basin": ["wash_table", "sink"], "-table": ["table", "work_counter"], "-stove": ["stove"], "-lectern": ["lectern"]}:
		if not focus.ends_with(suffix): continue
		for component in _components.values():
			if str(component.get("component_kind")) in {"-basin": ["wash_table", "sink"], "-table": ["table", "work_counter"], "-stove": ["stove"], "-lectern": ["lectern"]}[suffix]: return component
	if focus.ends_with("-door") or focus.ends_with("-gate") or settings.rooms.has(focus): return _exit_node()
	return body if bool(binding.get("self", false)) else null

func _exit_node() -> Node3D:
	for component in _components.values():
		if str(component.get("component_kind")) == "door" and component.visible: return component
	return body

func _target_point(focus: String, actor: String = "") -> Vector3:
	var node := _target_node(focus)
	if not is_instance_valid(node): return body.position
	if node == body and (actor.is_empty() or actor == _viewer()): return body.position
	if not actor.is_empty() and node == _bodies.get(actor): return node.global_position
	var from := body.global_position if actor.is_empty() or not _bodies.has(actor) else (_bodies[actor] as Node3D).global_position
	if node.has_method("get_interaction_slot"):
		var marker = node.get_interaction_slot(str(_binding(focus).get("slot", "Front")))
		if marker is Marker3D: return _reachable_interaction_point(node, marker, from)
	var p := node.global_position
	var direction := Vector3(from.x - p.x, 0.0, from.z - p.z).normalized()
	if direction.length_squared() < 0.1: direction = Vector3.FORWARD
	return _floor_point(Vector3(p.x, 0.0, p.z) + direction * float(_binding(focus).get("distance", 0.95)))

func _reachable_interaction_point(node: Node3D, preferred: Marker3D, from: Vector3) -> Vector3:
	# Generated furniture provides standing slots on both sides. A chair or
	# wall can obstruct one side; use only a real, reachable authored slot.
	var markers: Array[Marker3D] = [preferred]
	for name in ["Front", "Back"]:
		var alternate = node.get_interaction_slot(name)
		if alternate is Marker3D and alternate != preferred and str(alternate.get_meta("pose", "stand")) == "stand": markers.append(alternate)
	for marker in markers:
		var requested := Vector3(marker.global_position.x, 0.0, marker.global_position.z)
		var floor := _floor_point(requested)
		if floor.distance_to(requested) > 0.3: continue
		if from.distance_to(floor) <= float(settings.get("interaction_distance", 0.35)): return floor
		var route := _route(from, floor)
		if not route.is_empty() and route[route.size() - 1].distance_to(floor) <= float(settings.get("interaction_distance", 0.35)): return floor
	return Vector3(preferred.global_position.x, 0.0, preferred.global_position.z)

func _floor_point(point: Vector3) -> Vector3:
	if not is_instance_valid(_region): return point
	return NavigationServer3D.map_get_closest_point(_region.get_navigation_map(), point)

func _path_connects(path: PackedVector3Array, from: Vector3, to: Vector3) -> bool:
	if from.distance_to(to) <= 0.08: return true
	return not path.is_empty() and path[0].distance_to(from) <= 0.2 and path[path.size() - 1].distance_to(to) <= 0.2

func _set_room_ground_anchor(spec: Dictionary) -> bool:
	var spawn: Array = spec.get("spawn", [0.0, 0.0, -1.0])
	var requested := Vector3(float(spawn[0]), float(spawn[1]), float(spawn[2]))
	var ground := _floor_point(requested)
	# Furniture tops can be separate navigation islands. The authored floor
	# anchor must lead to a real door before it can seed resident placements.
	if absf(ground.y - requested.y) <= 0.3:
		for component in _components.values():
			if str(component.get("component_kind")) != "door" or not component.visible: continue
			for slot in ["Front", "Back"]:
				var marker = component.get_interaction_slot(slot) if component.has_method("get_interaction_slot") else null
				if not marker is Marker3D: continue
				var entry := Vector3(marker.global_position.x, requested.y, marker.global_position.z)
				var entry_floor := _floor_point(entry)
				if entry_floor.distance_to(entry) > 0.3: continue
				if not _path_connects(_route(ground, entry_floor), ground, entry_floor): continue
				_room_ground_anchor = ground
				_navigation_load["ground_anchor"] = {"position": _vector(ground), "entrance_component": str(component.get("instance_id")), "entrance_slot": slot, "entrance_position": _vector(entry_floor)}
				return true
	last_failure = "房间的出生地面未连接到实际房门：" + room_id
	return false

func _spawn_floor_point(requested: Vector3) -> Vector3:
	if _room_ground_anchor.x == INF: return Vector3(INF, INF, INF)
	var floor_y := float(settings.rooms[room_id].get("spawn", [0.0, 0.0, -1.0])[1])
	var projected := _floor_point(requested)
	if absf(projected.y - floor_y) <= 0.3 and _path_connects(_route(_room_ground_anchor, projected), _room_ground_anchor, projected): return projected
	# This runs only when a resident is first instantiated, never as a rescue
	# teleport during play. Search actual floor polygons near the desired spot.
	# Keep the full mesh: connected stairs and raised work areas remain usable.
	var best := _room_ground_anchor
	var best_distance := requested.distance_squared_to(best)
	for ring in range(1, 7):
		for direction in range(16):
			var angle := TAU * float(direction) / 16.0
			var probe := requested + Vector3(cos(angle), 0.0, sin(angle)) * (float(ring) * 0.5)
			var candidate := _floor_point(probe)
			if absf(candidate.y - floor_y) > 0.3: continue
			var distance := requested.distance_squared_to(candidate)
			if distance >= best_distance: continue
			if not _path_connects(_route(_room_ground_anchor, candidate), _room_ground_anchor, candidate): continue
			best = candidate
			best_distance = distance
	return best

func _route(from: Vector3, to: Vector3) -> PackedVector3Array:
	if not is_instance_valid(_region): return PackedVector3Array()
	return NavigationServer3D.map_get_path(_region.get_navigation_map(), from, _floor_point(to), true)

func request_action(action_id: String, evidence: Array[String] = [], deduction := false) -> String:
	if _transition_busy or _room_loading: return "正在穿过门扉，请稍候。"
	if _finished: return "五日流程已结束。"
	if controller_ai: return "当前由 AI 控制侦探；接管后可亲自行动。"
	if not _pending_action.is_empty() or not _activity().is_empty(): return "请等待当前行动完成，或先取消。"
	if not _actions.has(action_id) or not bridge.Available(action_id): return "当前认知或现场条件尚不允许这个行动。"
	var action: Dictionary = _actions[action_id]
	if _category(action) == "deduction" and not deduction:
		if is_instance_valid(native_book_ui): native_book_ui.present_deduction(action_id)
		return ""
	selected_focus = str(action.get("focus", _viewer()))
	if str(action.get("kind", "")) != "walk" and not is_instance_valid(_target_node(selected_focus)):
		return "现场没有找到对应人物或家具：" + focus_label(selected_focus)
	var target := _target_point(selected_focus)
	if str(action.get("kind", "")) == "walk" and str(action.get("escort", "")).is_empty():
		var door := _exit_node()
		if is_instance_valid(door): target = _target_point_for_node(door)
	_pending_action = action_id
	_pending_target = target
	_pending_evidence = evidence.duplicate()
	_pending_deduction = deduction
	if body.position.distance_to(target) <= float(settings.get("interaction_distance", 0.35)):
		return _submit_pending()
	_path = _route(body.position, target)
	if _path.is_empty() or _path[_path.size() - 1].distance_to(target) > float(settings.get("interaction_distance", 0.35)):
		_pending_action = ""
		_path.clear()
		return "到这件物品的通路被挡住了。"
	_movement_checkpoint = body.position
	_movement_stalled = 0.0
	_manual_arrived = false
	_manual_navigation = {"actor": _viewer(), "action": action_id, "room": room_id, "authority": "godot", "start_position": _vector(body.position), "target": _vector(target), "start_game_seconds": state.get("game_seconds", 0.0), "path_length": _path_length(body.position, _path, target), "travelled": 0.0, "physical_seconds": 0.0}
	phase = "walking"
	feedback = "正在走近：" + focus_label(selected_focus)
	return ""

func _target_point_for_node(node: Node3D, actor: String = "") -> Vector3:
	var actor_body: CharacterBody3D = _bodies.get(actor, body)
	if not is_instance_valid(node) or node == actor_body: return actor_body.position
	if node.has_method("get_interaction_slot"):
		var marker = node.get_interaction_slot("Front")
		if marker is Marker3D: return _reachable_interaction_point(node, marker, actor_body.global_position)
	var dir := Vector3(actor_body.position.x - node.position.x, 0, actor_body.position.z - node.position.z).normalized()
	return _floor_point(Vector3(node.position.x, 0, node.position.z) + dir * 0.8)

func _submit_pending() -> String:
	var id := _pending_action
	_pending_action = ""
	_path.clear()
	if body.position.distance_to(_pending_target) > float(settings.get("interaction_distance", 0.35)):
		return "尚未走到物件的交互位置。"
	if not bridge.Available(id): return "走近后，现场或人物意图已经变化。"
	var error: String
	if _pending_deduction:
		error = bridge.SubmitDeduction(id, _pending_evidence)
	else:
		error = bridge.Submit(id)
	_pending_deduction = false
	_pending_evidence.clear()
	if error.is_empty():
		feedback = str(_actions.get(id, {}).get("label", id)) + "…"
		phase = "working"
		if is_instance_valid(native_book_ui): native_book_ui.close_book()
	else: feedback = error; phase = "idle"
	return error

func _activity() -> Dictionary:
	return bridge.ActorActivity(_viewer()) if is_instance_valid(bridge) else {}

func _move_player(delta: float) -> void:
	if not is_instance_valid(body) or controller_ai: return
	if player_paused: body.stop_and_hold_position(); return
	if not _path.is_empty():
		while not _path.is_empty() and body.position.distance_to(_path[0]) < 0.12: _path.remove_at(0)
		if _path.is_empty():
			body.stop_and_hold_position()
			_manual_arrived = true
			return
		var prior := body.position
		body.step_towards(_path[0], delta, float(settings.get("walking_speed", 2.4)) * _navigation_factor())
		if not _manual_navigation.is_empty():
			_manual_navigation["travelled"] = float(_manual_navigation.get("travelled", 0.0)) + prior.distance_to(body.position)
			_manual_navigation["physical_seconds"] = float(_manual_navigation.get("physical_seconds", 0.0)) + delta * _navigation_factor()
		bridge.ReportPlacement(body.position.x, body.position.y, body.position.z, body.rotation.y, room_id)
		if body.position.distance_to(_movement_checkpoint) > 0.06: _movement_checkpoint = body.position; _movement_stalled = 0.0
		else: _movement_stalled += delta
		if _movement_stalled > 3.5:
			_path.clear(); _pending_action = ""; _manual_navigation.clear(); phase = "idle"; feedback = "通路被家具挡住；可以换一个接近方向。"
		return
	var activity: Dictionary = bridge.ActorActivity(_viewer())
	if not activity.is_empty():
		phase = "walking" if bool(activity.get("requires_arrival", false)) else "working"
		return
	if is_instance_valid(native_book_ui) and native_book_ui.book_open(): return
	var input := Input.get_vector("move_left", "move_right", "move_up", "move_down")
	if input.length_squared() > 0.01:
		var right := camera.global_basis.x
		var forward := -camera.global_basis.z
		right.y = 0; forward.y = 0
		var direction := (right.normalized() * input.x - forward.normalized() * input.y).normalized()
		body.velocity = direction * float(settings.get("walking_speed", 2.4)) * _navigation_factor()
		body.velocity.y = -1.0
		body.face_world_direction(direction)
		body.set_movement_speed(body.velocity.length())
		body.move_and_slide()
		bridge.ReportPlacement(body.position.x, body.position.y, body.position.z, body.rotation.y, room_id)
		phase = "walking"
	else:
		body.velocity = Vector3(0, -1, 0)
		body.move_and_slide()
		body.set_movement_speed(0.0)
		phase = "idle"

func _animation(action: Dictionary) -> String:
	return str(settings.get("action_animations", {}).get(str(action.get("kind", "")), "interact"))

func _move_npcs(delta: float) -> void:
	var escorted: Dictionary = {}
	for nav_actor in _navigation:
		var active_nav: Dictionary = _navigation[nav_actor]
		if str(active_nav.get("stage", "")) == "exit": escorted[str(active_nav.get("escort", ""))] = nav_actor
	for actor in _bodies:
		if escorted.has(actor): continue
		var actor_body: CharacterBody3D = _bodies[actor]
		var activity: Dictionary = bridge.ActorActivity(actor)
		if activity.is_empty():
			if actor != _viewer() or controller_ai: actor_body.play_action("idle")
			continue
		var action_id := str(activity.get("action", ""))
		var action: Dictionary = _actions.get(action_id, {})
		if not bool(activity.get("requires_arrival", false)):
			if str(activity.get("phase", "")) == "working": actor_body.play_action(_animation(action))
			else: actor_body.stop_and_hold_position()
			continue
		if not _navigation.has(actor): continue
		var nav: Dictionary = _navigation[actor]
		if _arrival_queue.has(actor): actor_body.stop_and_hold_position(); continue
		var target: Vector3 = nav.target
		var focus_node := _target_node(str(action.get("focus", actor)))
		# A person can walk while we approach. Re-route against their actual
		# current position, and register the changed contact target honestly.
		if str(nav.get("stage", "")) != "exit" and focus_node is CharacterBody3D and focus_node != actor_body and (str(nav.get("stage", "")) == "contact" or str(action.get("kind", "")) not in ["walk", "travel"]):
			var current_target := _navigation_target(action, actor)
			if current_target.distance_to(target) > 0.45:
				target = current_target
				nav.target = target
				nav.path = _route(actor_body.position, target)
				bridge.SetNavigationTarget(int(nav.execution), room_id, target.x, target.y, target.z, _path_length(actor_body.position, nav.path, target), float(settings.get("walking_speed", 2.4)))
		var path: PackedVector3Array = nav.path
		while not path.is_empty() and actor_body.position.distance_to(path[0]) < 0.12: path.remove_at(0)
		nav.path = path
		var companion_ready := true
		if str(nav.get("stage", "")) == "exit":
			companion_ready = _follow_escort(actor, str(nav.get("escort", "")), delta)
			if not companion_ready:
				actor_body.stop_and_hold_position()
				nav.physical_seconds = float(nav.physical_seconds) + delta * _navigation_factor()
				continue
		if actor_body.position.distance_to(target) <= float(settings.get("interaction_distance", 0.35)):
			if str(nav.get("stage", "")) == "contact":
				_begin_escort_exit(actor, nav)
				continue
			bridge.ReportNavigationProgress(int(nav.execution), actor_body.position.x, actor_body.position.y, actor_body.position.z, 0.0)
			_arrival_queue[actor] = int(nav.execution)
			actor_body.stop_and_hold_position()
			continue
		if path.is_empty():
			bridge.Fail(int(nav.execution), "navigation_path_lost")
			if actor == _viewer(): feedback = "目标已经离开当前通路，请重新接近。"
			continue
		var prior := actor_body.position
		actor_body.step_towards(path[0], delta, float(settings.get("walking_speed", 2.4)) * _navigation_factor())
		nav.travelled = float(nav.travelled) + prior.distance_to(actor_body.position)
		nav.physical_seconds = float(nav.physical_seconds) + delta * _navigation_factor()
		bridge.ReportNavigationProgress(int(nav.execution), actor_body.position.x, actor_body.position.y, actor_body.position.z, _path_length(actor_body.position, path, target))
		if actor_body.position.distance_to(nav.checkpoint) > 0.06: nav.checkpoint = actor_body.position; nav.stalled = 0.0
		else: nav.stalled = float(nav.stalled) + delta
		if float(nav.stalled) > 3.5:
			bridge.Fail(int(nav.execution), "navigation_stalled")
			if actor == _viewer(): feedback = "通路被挡住；换个接近方向后可以再试。"
		if actor == _viewer(): phase = "walking"

func _begin_escort_exit(actor: String, nav: Dictionary) -> void:
	var actor_body: CharacterBody3D = _bodies[actor]
	var companion := str(nav.get("escort", ""))
	if not _bodies.has(companion): bridge.Fail(int(nav.execution), "escort_target_missing"); return
	var target := _target_point_for_node(_exit_node(), actor)
	var path := _route(actor_body.position, target)
	if path.is_empty() or path[path.size() - 1].distance_to(target) > 0.35:
		bridge.Fail(int(nav.execution), "escort_exit_unreachable")
		if actor == _viewer(): feedback = "同行者到门边的通路被挡住了。"
		return
	var distance := _path_length(actor_body.position, path, target)
	if not bridge.SetNavigationTarget(int(nav.execution), room_id, target.x, target.y, target.z, distance, float(settings.get("walking_speed", 2.4))): return
	nav.stage = "exit"
	nav.target = target
	nav.path = path
	nav.path_length = float(nav.path_length) + distance
	nav.stalled = 0.0
	(_bodies[companion] as CharacterBody3D).interrupt_action()

func _follow_escort(actor: String, escort: String, delta: float) -> bool:
	if not _bodies.has(escort): bridge.Fail(int(_navigation.get(actor, {}).get("execution", 0)), "escort_target_missing"); return false
	var leader: CharacterBody3D = _bodies[actor]
	var companion: CharacterBody3D = _bodies[escort]
	var nav: Dictionary = _navigation.get(actor, {})
	var target := _floor_point(leader.position + Vector3(0.42, 0.0, 0.0))
	var path := _route(companion.position, target)
	while not path.is_empty() and companion.position.distance_to(path[0]) < 0.12: path.remove_at(0)
	if not path.is_empty(): companion.step_towards(path[0], delta, float(settings.get("walking_speed", 2.4)) * _navigation_factor())
	else: companion.stop_and_hold_position()
	bridge.ReportActorPlacement(escort, companion.position.x, companion.position.y, companion.position.z, companion.rotation.y, room_id)
	var checkpoint: Vector3 = nav.get("escort_checkpoint", companion.position)
	if companion.position.distance_to(checkpoint) > 0.06:
		nav["escort_checkpoint"] = companion.position
		nav["escort_stalled"] = 0.0
	else:
		nav["escort_checkpoint"] = checkpoint
		nav["escort_stalled"] = float(nav.get("escort_stalled", 0.0)) + delta
	if companion.position.distance_to(leader.position) > 1.25 and float(nav.get("escort_stalled", 0.0)) > 3.5:
		bridge.Fail(int(nav.execution), "escort_route_blocked")
		_record_navigation({"actor": actor, "execution": nav.get("execution", 0), "room": room_id, "result": "escort_route_blocked", "game_seconds": state.get("game_seconds", 0.0)})
		if actor == _viewer(): feedback = "同行者的通路被挡住了，请换个位置再试。"
	return companion.position.distance_to(leader.position) <= 1.25

func _update_camera() -> void:
	if not is_instance_valid(camera) or room_id.is_empty(): return
	var center: Vector3
	if is_instance_valid(body):
		center = body.position + Vector3.UP * 0.8
	else:
		var spec: Dictionary = settings.rooms[room_id]
		center = Vector3(0.0, 0.65, -float(spec.depth) * 0.5)
	camera.size = _camera_size
	var viewport_size := get_viewport().get_visible_rect().size
	camera.h_offset = _camera_size * viewport_size.x / maxf(1.0, viewport_size.y) * 0.05
	camera.position = center + Vector3(sin(_camera_yaw) * 18.0, 20.0, cos(_camera_yaw) * 18.0)
	camera.look_at(center, Vector3.UP)

func _unhandled_input(event: InputEvent) -> void:
	if not ready_for_play or _room_loading or _transition_busy: return
	if event.is_action_pressed("notebook"):
		native_book_ui.toggle_book("evidence"); get_viewport().set_input_as_handled(); return
	if event.is_action_pressed("campus_map"):
		native_book_ui.toggle_book("map"); get_viewport().set_input_as_handled(); return
	if event.is_action_pressed("interact"):
		_select_nearest_target(); get_viewport().set_input_as_handled(); return
	if event.is_action_pressed("cancel"):
		if native_book_ui.book_open(): native_book_ui.close_book()
		else: _path.clear(); _pending_action = ""; selected_focus = ""
		get_viewport().set_input_as_handled(); return
	if event is InputEventMouseButton and event.pressed:
		if event.button_index == MOUSE_BUTTON_WHEEL_UP: _camera_size = maxf(5.0, _camera_size - 0.8)
		elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN: _camera_size = minf(24.0, _camera_size + 0.8)
		elif event.button_index == MOUSE_BUTTON_RIGHT:
			if native_book_ui.book_open(): native_book_ui.close_book()
			else: _path.clear(); _pending_action = ""; selected_focus = ""
		elif event.button_index == MOUSE_BUTTON_LEFT and not native_book_ui.book_open(): _pick(event.position)

func _select_nearest_target() -> void:
	var closest := 1.8
	var focus := ""
	for action: Dictionary in available_actions():
		var target := _target_node(str(action.focus))
		if not is_instance_valid(target) or target == body: continue
		var distance := body.position.distance_to(_target_point(str(action.focus)))
		if distance <= closest:
			closest = distance
			focus = str(action.focus)
	if focus.is_empty(): feedback = "走近已知的人物或物件，再按 E 查看行动。"
	else: select_focus(focus)

func _pick(point: Vector2) -> void:
	var origin := camera.project_ray_origin(point)
	var query := PhysicsRayQueryParameters3D.create(origin, origin + camera.project_ray_normal(point) * 150.0, 7)
	var hit: Dictionary = get_world_3d().direct_space_state.intersect_ray(query)
	if hit.is_empty(): return
	var node: Node = hit.collider
	while node != null:
		if node.has_meta("campaign_actor"):
			select_focus(str(node.get_meta("campaign_actor"))); return
		if node.has_meta("campaign_component"):
			var component = _components.get(str(node.get_meta("campaign_component")))
			if is_instance_valid(component) and str(component.get("component_kind")) in ["architecture", "complete_shell", "floor", "floor_slab"]: break
			for action: Dictionary in available_actions():
				if _action_target_node(action) == component: select_focus("@component:" + str(node.get_meta("campaign_component"))); return
			feedback = "目前没有与这件物品相关的已知行动。"
			return
		node = node.get_parent()
	if not controller_ai and _activity().is_empty():
		_path = _route(body.position, hit.position)
		_pending_action = ""
		_movement_checkpoint = body.position
		_movement_stalled = 0.0
		_begin_manual_walk_trace(_floor_point(hit.position))

func _begin_manual_walk_trace(target: Vector3) -> void:
	_manual_arrived = false
	_manual_navigation = {"actor": _viewer(), "action": "walk", "room": room_id, "authority": "godot", "start_position": _vector(body.position), "target": _vector(target), "start_game_seconds": state.get("game_seconds", 0.0), "path_length": _path_length(body.position, _path, target), "travelled": 0.0, "physical_seconds": 0.0}

func select_focus(focus: String) -> void:
	selected_focus = focus
	if is_instance_valid(native_book_ui): native_book_ui.refresh(true)

func set_speed(value: float) -> void:
	if _transition_busy: return
	time_scale = clampf(value, 1.0, 1024.0)
	bridge.ClearFastForwardTime()

func set_paused(value: bool) -> void:
	if _transition_busy: return
	player_paused = value
	bridge.ClearFastForwardTime()

func set_control_ai(value: bool) -> void:
	if _transition_busy or _room_loading: return
	controller_ai = value
	bridge.SetPlayerAutonomy(value)
	_pending_action = ""; _path.clear(); _manual_navigation.clear(); _manual_arrived = false
	feedback = "侦探自主行动中。" if value else "已接管侦探。"

func deduction_preview(id: String) -> Dictionary:
	return bridge.PreviewDeduction(id)

func host_checkpoint() -> Dictionary:
	if _transition_busy or _room_loading or not is_instance_valid(body): return {"error": "正在穿过门扉，请抵达后保存。"}
	if _physical_clock_pending: return {"error": "正在记录刚才的移动，请稍后保存。"}
	var core: Dictionary = bridge.CaptureCheckpoint()
	if core.has("error"): return core
	return {"version": 2, "core": str(core.json), "campaign_view": {"visited": visited, "journal": journal, "journal_dropped_total": journal_dropped_total, "navigation_history": _navigation_history, "room": room_id, "position": _vector(body.position), "paused": player_paused, "speed": time_scale}}

func save_game() -> void:
	if not ready_for_play: return
	_write_progress(false)

func _write_progress(automatic: bool) -> void:
	_autosave_pending = false
	var saved := host_checkpoint()
	if saved.has("error"): feedback = "保存失败：" + str(saved.error); return
	var error: String = bridge.WriteSavedGame(JSON.stringify(saved))
	if error.is_empty():
		_last_saved_time = float(state.get("game_seconds", 0.0))
		if not automatic: feedback = "已保存当前进度。"
	else: feedback = "保存失败：" + error

func interface_observation() -> Dictionary:
	var progress: Dictionary = bridge.WorkProgress(_viewer())
	var campaign: Dictionary = state.get("campaign_progress", {})
	var campaign_phase: Dictionary = campaign.get("phase", {})
	var objective := str(campaign_phase.get("label", "自主安排")) if bool(campaign_phase.get("active", false)) and not bool(campaign_phase.get("task_guidance", true)) else str(definition.get("scenario_label", ""))
	var known_map: Array = []
	for id in visited:
		var spec: Dictionary = settings.rooms.get(id, {})
		known_map.append({"id": id, "label": room_label(id), "topology_ref": spec.get("topology_ref", ""), "position": spec.get("map_position", []), "floor": spec.get("floor", "")})
	var own_receipts: Array = journal.filter(func(record): return str(record.get("actor", "")) == _viewer())
	return {"choices": available_actions(), "targets": visible_targets(), "selected_focus": selected_focus, "objective": objective, "campaign_progress": campaign, "feedback": feedback, "paused": player_paused, "speed": time_scale, "last_saved_game_seconds": _last_saved_time, "player_action": str(progress.get("action", "")), "player_progress": progress, "pending_approach": _pending_action, "map": known_map, "content_path": _source_path, "player_paths": state.get("player_paths", {}), "active_intent": state.get("active_intent"), "recent_own_actions": own_receipts.slice(maxi(0, own_receipts.size() - 32)), "transition": _transition.snapshot() if is_instance_valid(_transition) else {}, "journal_coverage": {"count": journal.size(), "dropped": journal_dropped_total, "first_game_minute": journal[0].get("time") if not journal.is_empty() else null, "last_game_minute": journal[-1].get("time") if not journal.is_empty() else null}, "book": native_book_ui.book_state() if is_instance_valid(native_book_ui) else {}}

func observation() -> Dictionary:
	_refresh(false)
	var actors: Array = []
	for actor in _bodies:
		if not _actor_visible(actor): continue
		var actor_body: CharacterBody3D = _bodies[actor]
		var visible_id := visible_activity(actor)
		var motion: Dictionary = bridge.ActorActivity(actor)
		actors.append({"id": actor, "room": room_id, "position": _vector(actor_body.position), "action": visible_id, "progress": visible_progress(actor), "navigation": {"authority": motion.get("authority", "none"), "phase": motion.get("phase", "idle"), "requires_arrival": motion.get("requires_arrival", false)}})
	return {"ready": ready_for_play, "phase": phase, "last_failure": last_failure, "state": state, "interface": interface_observation() if ready_for_play else {}, "autonomous_actors": actors, "scene_resource": settings.rooms.get(room_id, {}).get("scene", ""), "room_binding": settings.rooms.get(room_id, {}), "navigation_polygons": _region.navigation_mesh.get_polygon_count() if is_instance_valid(_region) else 0, "execution_evidence": {"core_receipts": _processed_receipts, "core_steps": _step_count, "core_seconds": float(_simulation_usec) / 1000000.0, "wall_seconds": float(Time.get_ticks_usec() - _started_usec) / 1000000.0, "physical_completion_injected": false}}

func _selected_interaction_observation() -> Dictionary:
	# Read-only diagnostics for the same visible focus selected by the player.
	# This exposes actual authored markers and navigation results, never NPC
	# intentions or an API for changing placement, knowledge or arrival state.
	if selected_focus.is_empty() or not ready_for_play or _room_loading or not is_instance_valid(body) or not is_instance_valid(_region): return {}
	var node := _target_node(selected_focus)
	if not is_instance_valid(node): return {"focus": selected_focus, "target_present": false}
	var component_id := ""
	for id in _components:
		if _components[id] == node: component_id = str(id); break
	var target := _target_point(selected_focus)
	var path := _route(body.position, target)
	var markers: Array = []
	if node.has_method("get_interaction_slot"):
		for slot in ["Front", "Back"]:
			var marker = node.get_interaction_slot(slot)
			if not marker is Marker3D: continue
			var requested := Vector3(marker.global_position.x, 0.0, marker.global_position.z)
			var floor := _floor_point(requested)
			var marker_path := _route(body.position, floor)
			markers.append({"slot": slot, "local_position": _vector(marker.position), "global_position": _vector(marker.global_position), "floor_projection": _vector(floor), "projection_distance": floor.distance_to(requested), "path_points": marker_path.size(), "path_end": _vector(marker_path[marker_path.size() - 1]) if not marker_path.is_empty() else [], "path_end_distance": marker_path[marker_path.size() - 1].distance_to(floor) if not marker_path.is_empty() else null})
	return {"focus": selected_focus, "target_present": true, "component": component_id, "node_path": str(node.get_path()), "node_position": _vector(node.position), "node_global_position": _vector(node.global_position), "host_global_position": _vector(global_position), "actor_position": _vector(body.position), "actor_global_position": _vector(body.global_position), "binding": _binding(selected_focus), "computed_target": _vector(target), "target_distance": body.position.distance_to(target), "interaction_distance": float(settings.get("interaction_distance", 0.35)), "path_points": path.size(), "path_end": _vector(path[path.size() - 1]) if not path.is_empty() else [], "markers": markers}

func physical_observation() -> Dictionary:
	if not is_instance_valid(body): return {}
	return {"time_us": Time.get_ticks_usec(), "physics_frame": Engine.get_physics_frames(), "position": _vector(body.position), "room": room_id, "phase": phase, "on_floor": body.is_on_floor(), "speed_mps": body.velocity.length(), "animation": body.current_action, "game_seconds": state.get("game_seconds", 0.0), "navigation_load": _navigation_load, "loaded_room_count": _loaded_room_count(), "navigation_factor": _navigation_factor(), "pending_physical_seconds": bridge.PendingPhysicalSeconds(), "navigation_history": _navigation_history.slice(maxi(0, _navigation_history.size() - 32)), "selected_interaction": _selected_interaction_observation(), "transition": _transition.snapshot() if is_instance_valid(_transition) else {}}

func interface_command(request: Dictionary) -> String:
	if not ready_for_play: return "会话尚未就绪。"
	if _transition_busy or _room_loading: return "正在穿过门扉，请稍候。"
	var command := str(request.get("command", ""))
	if command == "pause": set_paused(bool(request.get("paused", false))); return ""
	if command == "save": save_game(); return ""
	if command == "cancel":
		_path.clear(); _pending_action = ""; _manual_navigation.clear(); _manual_arrived = false; bridge.Cancel(); return ""
	if command == "action": return request_action(str(request.get("action_id", "")))
	if command == "deduce":
		var evidence: Array[String] = []
		for id in request.get("evidence_ids", []): evidence.append(str(id))
		return request_action(str(request.get("action_id", "")), evidence, true)
	if command == "walk":
		if controller_ai or not _activity().is_empty(): return "当前无法手动行走。"
		var p: Array = request.get("position", [])
		if p.size() != 3: return "行走目标需三个坐标。"
		var target := Vector3(float(p[0]), float(p[1]), float(p[2]))
		if _floor_point(target).distance_to(target) > 0.5: return "目标不在当前房间的可行走地面。"
		_path = _route(body.position, target)
		_pending_action = ""; _movement_checkpoint = body.position; _movement_stalled = 0
		_begin_manual_walk_trace(target)
		return "" if not _path.is_empty() else "目标无法到达。"
	return "未支持的角色操作。"

func shutdown() -> void:
	_closed = true
	ready_for_play = false
	set_physics_process(false)
	_path.clear()
	for child in get_children(): child.queue_free()

func _exit_tree() -> void:
	if _previous_3d_scale > 0.0:
		get_viewport().scaling_3d_scale = _previous_3d_scale
	if _navigation_map.is_valid():
		NavigationServer3D.free_rid(_navigation_map)
		_navigation_map = RID()
