extends SceneTree

const SCENE_PATH := "res://scenes/FirstAwakening/FirstAwakening.tscn"
const REPORT_PATH := "res://../artifacts/first-awakening/party-movement-diagnostic.json"
const DESTINATIONS := {
	"工程师": Vector2(238, 704),
	"厨师": Vector2(394, 717),
	"音乐家": Vector2(481, 710),
}
const SAMPLE_INTERVAL := 0.5
const RUN_SECONDS := 18.0
const TIME_SCALE := 1.0

func _initialize() -> void:
	call_deferred("run")

func _new_room(label: String) -> Node2D:
	var room: Node2D = load(SCENE_PATH).instantiate()
	room.name = label
	root.add_child(room)
	room.dialogue.active = false
	room.dialogue.panel.hide()
	room.call("_close_modal")
	return room

func _start_group(room: Node2D, disable_peer_collision := false, chef_only := false) -> Dictionary:
	var actors: Dictionary = room.get("actors")
	var routes := {}
	for id in DESTINATIONS:
		var actor: CharacterBody2D = actors[id]
		var route: PackedVector2Array = room.call("_route", actor.position, DESTINATIONS[id])
		routes[id] = []
		for point in route:
			routes[id].append([point.x, point.y])
		if chef_only and id != "厨师":
			actor.collision_layer = 0
			actor.collision_mask = 0
		elif disable_peer_collision:
			# Static geometry is on layer 1; actors are on layer 2.
			actor.collision_mask = 1
	if chef_only:
		var chef: CharacterBody2D = actors["厨师"]
		var path: PackedVector2Array = room.call("_route", chef.position, DESTINATIONS["厨师"])
		if path.size() > 1:
			chef.follow_path(path)
	else:
		room.call("_gather_party")
	return routes

func _run_case(case_name: String, disable_peer_collision := false, chef_only := false) -> Dictionary:
	var room := _new_room(case_name)
	var routes := _start_group(room, disable_peer_collision, chef_only)
	var samples := []
	var steps := int(RUN_SECONDS / SAMPLE_INTERVAL)
	for step in range(steps + 1):
		var actors: Dictionary = room.get("actors")
		var snapshot := {"seconds": step * SAMPLE_INTERVAL}
		for id in DESTINATIONS:
			snapshot[id] = _actor_state(actors[id])
		samples.append(snapshot)
		if step < steps:
			await create_timer(SAMPLE_INTERVAL).timeout
	var actors: Dictionary = room.get("actors")
	var end_states := {}
	for id in DESTINATIONS:
		var actor: CharacterBody2D = actors[id]
		var state := _actor_state(actor)
		state["distance_to_destination"] = actor.position.distance_to(DESTINATIONS[id])
		state["nearby_obstacle_rects"] = _nearby_obstacles(room, actor)
		end_states[id] = state
	var output := {"routes": routes, "samples": samples, "end_states": end_states}
	room.queue_free()
	await process_frame
	return output

func _actor_state(actor: CharacterBody2D) -> Dictionary:
	var blockers := []
	for i in actor.get_slide_collision_count():
		var hit := actor.get_slide_collision(i)
		var collider := hit.get_collider()
		blockers.append({
			"name": str(collider.name) if is_instance_valid(collider) else "freed",
			"type": collider.get_class() if is_instance_valid(collider) else "freed",
			"normal": [hit.get_normal().x, hit.get_normal().y],
		})
	return {
		"position": [actor.position.x, actor.position.y],
		"target": [actor.target.x, actor.target.y],
		"waypoint": actor.waypoint,
		"waypoint_count": actor.path.size(),
		"travelling": actor.travelling,
		"slide_collisions": blockers,
	}

func _nearby_obstacles(room: Node2D, actor: CharacterBody2D) -> Array:
	var nearby := []
	for i in room.obstacle_rects.size():
		var rect: Rect2 = room.obstacle_rects[i]
		var distance := Vector2(
			clampf(actor.position.x, rect.position.x, rect.end.x),
			clampf(actor.position.y, rect.position.y, rect.end.y)
		).distance_to(actor.position)
		if distance <= 80.0:
			nearby.append({
				"obstacle_index": i,
				"rect": [rect.position.x, rect.position.y, rect.size.x, rect.size.y],
				"distance_from_feet": distance,
			})
	return nearby

func run() -> void:
	var output := {
		"run_seconds": RUN_SECONDS,
		"sample_interval_seconds": SAMPLE_INTERVAL,
		"engine_time_scale": TIME_SCALE,
		"destinations": {},
		"cases": {},
		"caveat": "Diagnostic captures runtime positions and CharacterBody slide contacts; it does not infer route success from AStar paths alone.",
	}
	Engine.time_scale = TIME_SCALE
	for id in DESTINATIONS:
		var destination: Vector2 = DESTINATIONS[id]
		output.destinations[id] = [destination.x, destination.y]
	output.cases.parallel_actual_collisions = await _run_case("parallel_actual_collisions")
	output.cases.chef_alone = await _run_case("chef_alone", false, true)
	output.cases.parallel_without_actor_collisions = await _run_case("parallel_without_actor_collisions", true)
	Engine.time_scale = 1.0

	var file := FileAccess.open(REPORT_PATH, FileAccess.WRITE)
	if file == null:
		push_error("Could not write movement diagnostic: " + REPORT_PATH)
		quit(1)
		return
	file.store_string(JSON.stringify(output, "\t"))
	file.close()
	print("PARTY ROUTE DIAGNOSTIC: " + REPORT_PATH)
	quit()
