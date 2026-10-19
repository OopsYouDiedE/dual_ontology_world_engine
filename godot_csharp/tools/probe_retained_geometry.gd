extends SceneTree
## Engineering geometry fixture: no GameEngine, DV session, plot or player inputs.
const FIELD := preload("res://scenes/Shared/Field/WorldView.gd")
const EXECUTOR := preload("res://scenes/Host/DvFieldExecutor.gd")
const RESOURCE := "res://scenes/Chapter2/Field/CampusMapView.tscn"
var directory := ""
var bridge: Node
var archive: Node

func _initialize() -> void:
	root.position = Vector2i(10000, 10000)
	_run.call_deferred()

func _state(who: String, sprite: String, anchor_name := "") -> Dictionary:
	return {"resource": RESOURCE, "map": "living_hall", "execution_room": "living_hall:hall",
		"spawn": "east_south", "initial_anchor": anchor_name, "sites": {}, "access": {"maps": {}},
		"geometry_bindings": {"living_hall": {"sites": [{"id": "fixture_start", "point": [460, 736]}]}},
		"presentation": {"player_actor": who, "player_sprite": sprite},
		"people": [{"key": who, "map": "living_hall", "execution_room": "living_hall:hall",
			"initial_anchor": anchor_name, "spawn": "east_south", "sprite": sprite}]}

func _write(value: Dictionary, name := "geometry.dvlog") -> void:
	archive.WriteRecord(directory.path_join(name), JSON.stringify(value))

func _picture(name: String) -> void:
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(directory.path_join(name))

func _body_hits(field: Node2D, point: Vector2) -> Array:
	var query := PhysicsPointQueryParameters2D.new()
	query.position = point + Vector2(0, -6)
	query.collision_mask = 2
	var result: Array = []
	for hit in field.get_world_2d().direct_space_state.intersect_point(query):
		var collider: Node = hit.collider
		result.append({"instance": collider.get_instance_id(), "proxy_owner": collider.get_meta("retained_occupancy", 0)})
	return result

func _run() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--out="): directory = argument.substr(6)
	if directory == "" or DirAccess.dir_exists_absolute(directory):
		push_error("Require a new --out evidence directory")
		quit(2)
		return
	DirAccess.make_dir_recursive_absolute(directory)
	# Bound a fixture failure too; never leave an unattended engine running.
	create_timer(40.0).timeout.connect(func(): quit(3))
	archive = load("res://scenes/Host/FrameArchive.cs").new()
	root.add_child(archive)
	bridge = EXECUTOR.new()
	root.add_child(bridge)
	var state := _state("fixture_a", "architect", "site:fixture_start")
	var field: Node2D = FIELD.new()
	field.execution_only = true
	bridge.configure_visible(field, state)
	root.add_child(field)
	var adapter: Node2D = load(RESOURCE).instantiate()
	adapter.presentation = state.presentation
	field.setup(adapter)
	field.apply_snapshot(state)
	if field.player == null:
		_write({"error": "fixture_a_not_materialized"})
		quit(2)
		return
	field.camera.enabled = true
	var other_field: Node2D = bridge._hidden_field("fixture_b", _state("fixture_b", "archaeologist"))
	if other_field == null:
		_write({"error": "fixture_b_not_materialized"})
		quit(2)
		return
	bridge.sync_obstacles(field)
	await physics_frame
	var actor: CharacterBody2D = field.player
	var other: CharacterBody2D = other_field.player
	var other_start := other.position
	var start := actor.position
	var occupied: bool = field.position_occupied(actor, other.position)
	var grid: AStarGrid2D = adapter.nav_grid(actor)
	var other_cell := Vector2i(((other.position - grid.offset) / grid.cell_size).round())
	adapter._route_target_next = "living_hall"
	var occupied_entry_allowed: bool = adapter.valid_transition("living_hall", "east_south", Vector2.ZERO, false)
	adapter._route_target_next = ""
	# Deliberately request a straight physical path into another retained body's feet.
	# This is a collision probe, not an accepted formal action or a game trajectory.
	actor.follow_path(PackedVector2Array([other.position]))
	while actor.travelling: await physics_frame
	var stop := actor.position
	var collision_owner := 0
	for i in actor.get_slide_collision_count():
		var collider = actor.get_slide_collision(i).get_collider()
		if collider is Node and collider.has_meta("retained_occupancy"):
			collision_owner = int(collider.get_meta("retained_occupancy"))
	var collision_result: Dictionary = actor.travel_result.duplicate()
	var received: Dictionary = {}
	field.field_event.connect(func(event: Dictionary):
		if str(event.get("kind", "")) == "daily": received.merge(event, true))
	field.move_to_station("living_hall:hall", "living_hall:hall", "fixture_a", 1, "geometry_room_arrival")
	while received.is_empty(): await process_frame
	# Move the real offscreen body without a snapshot; occupancy must follow it.
	other.follow_path(PackedVector2Array([other.position + Vector2.UP * 32.0]))
	while other.travelling: await physics_frame
	await physics_frame
	await process_frame
	var live_proxy_point := Vector2(INF, INF)
	for node in adapter.objects_root().get_children():
		if int(node.get_meta("retained_occupancy", 0)) == other.get_instance_id(): live_proxy_point = node.position
	var live_position_occupied: bool = field.position_occupied(actor, other.position)
	var old_position_occupied: bool = field.position_occupied(actor, other_start)
	var old_position_colliders := _body_hits(field, other_start)
	var live_position_colliders := _body_hits(field, other.position)
	# Render the same retained body at its endpoint, not a duplicate or a snapped pose.
	bridge.acquire_actor("fixture_b", "", field)
	bridge.sync_obstacles(field)
	await physics_frame
	await process_frame
	var handoff_colliders := _body_hits(field, other.position)
	await _picture("room-arrival.png")
	var edge: Dictionary = adapter._next_edge("central_hall")
	_write({"scope": "engineering geometry fixture; no formal DV session", "occupied": occupied,
		"occupied_grid_solid": grid.is_point_solid(other_cell), "occupied_entry_allowed": occupied_entry_allowed,
		"start": [start.x, start.y], "collision_stop": [stop.x, stop.y], "collision_result": collision_result,
		"collision_owner": collision_owner, "other_instance_id": other.get_instance_id(),
		"other_position_at_collision": [other_start.x, other_start.y], "room_arrival": received,
		"live_body_position": [other.position.x, other.position.y], "live_proxy_position": [live_proxy_point.x, live_proxy_point.y],
		"live_position_occupied": live_position_occupied, "old_position_occupied": old_position_occupied,
		"old_position_colliders": old_position_colliders, "live_position_colliders": live_position_colliders,
		"handoff_colliders": handoff_colliders,
		"first_outdoor_edge": {"to": edge.get("to", ""), "point": str(edge.get("point", ""))}})
	# One physical interior/outdoor doorway, without plot state or a formal receipt.
	received.clear()
	var next_map := str(edge.get("to", ""))
	if next_map != "":
		field.execute_move({"map": next_map, "room": next_map + ":" + next_map,
			"execution_room": next_map + ":" + next_map}, "fixture_a", 2, "geometry_doorway")
		while received.is_empty(): await process_frame
		await _picture("outdoor-arrival.png")
		_write({"scope": "engineering doorway fixture; no formal DV session", "receipt": received}, "doorway.dvlog")
	bridge.begin_shutdown()
	field.begin_shutdown()
	quit()
