extends Node
## Room-local locomotion is host geometry. Only interaction/door requests select DV actions.
const NAV := preload("res://scenes/Shared/FieldNavigation.gd")
var field: Node2D
var options: Array = []
var submitted := false
var _route := PackedVector2Array()
var _site := ""
var _sequence := 0
var _walking := false
var _walk_id := ""
var _progress := Vector2.ZERO
var _stuck := 0.0

func set_options(value: Array) -> void:
	options = value
	submitted = false

func enabled() -> bool:
	return is_instance_valid(field) and field.field_active() and bool(field.snapshot.get("player_control", false)) and not submitted and not field._daily.has(field.player.name)

func _stamp(stage: String, id: String, data: Dictionary = {}) -> void:
	field.field_event.emit({"kind": "timing", "stage": stage, "interaction_id": id, "details": data})

func _id() -> String:
	_sequence += 1
	return "spatial-%d-%d" % [Time.get_ticks_usec(), _sequence]

func _stop_walk(reason: String) -> void:
	_route.clear()
	_site = ""
	if _walking:
		_stamp("local_motion_end", _walk_id, {"reason": reason, "game_time": field.snapshot.get("game_time", 0), "position": [field.player.position.x, field.player.position.y]})
	_walking = false
	if is_instance_valid(field.player) and not field.player.travelling: field.player.pose("idle")

func _begin_walk() -> void:
	if _walking: return
	if field.player.seated: field.player.stand_up()
	_walking = true
	_progress = field.player.position
	_stuck = 0.0
	_walk_id = _id()
	_stamp("local_motion_begin", _walk_id, {"game_time": field.snapshot.get("game_time", 0), "position": [field.player.position.x, field.player.position.y]})

func _physics_process(delta: float) -> void:
	if not enabled():
		if _walking: _stop_walk("input-blocked")
		return
	var direction := Input.get_vector("move_left", "move_right", "move_up", "move_down")
	if direction != Vector2.ZERO:
		_route.clear()
		_site = ""
		_begin_walk()
		_step(direction * field.player.speed * delta, delta)
	elif not _route.is_empty():
		_begin_walk()
		var difference: Vector2 = _route[0] - field.player.position
		if difference.length() <= 1.0:
			_route.remove_at(0)
			if _route.is_empty():
				var site := _site
				_stop_walk("arrived")
				if site != "": _interact(site)
		else:
			_step(difference.limit_length(field.player.speed * delta), delta)
			if field.player.position.distance_to(_progress) >= 0.5:
				_progress = field.player.position
				_stuck = 0.0
			else:
				_stuck += delta
				if _stuck >= 0.5: _stop_walk("blocked")
	elif _walking:
		_stop_walk("released")

func _step(motion: Vector2, delta: float) -> void:
	var next: Vector2 = field.player.position + motion
	var room := str(field.snapshot.get("execution_room", field.snapshot.get("room", "")))
	var next_room: String = field.adapter.room_of(next)
	var exit_map: String = field.adapter.manual_exit_target(next) if field.adapter.has_method("manual_exit_target") else ""
	if next_room != room or exit_map != "":
		if not field.adapter.walkable(next, field.player): return
		_stop_walk("doorway")
		for option in options:
			var binding: Dictionary = option.get("interaction", {})
			var destination: Dictionary = binding.get("destination", {})
			if str(binding.get("kind", "")) == "doorway" and ((exit_map != "" and str(destination.get("map", "")) == exit_map) or (exit_map == "" and str(destination.get("map", "")) == field.adapter.map_key() and str(destination.get("room", "")) == next_room)):
				_submit(option, "", "doorway")
				return
		return
	field.player.velocity = motion / maxf(delta, 0.000001)
	field.player.face_vector(motion)
	field.player.pose("walk")
	field.player.move_and_slide()
	field.player.velocity = Vector2.ZERO

func _unhandled_input(event: InputEvent) -> void:
	if not enabled() or event.is_echo(): return
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
		var site := str(field.hovered)
		if site != "": _approach(site)
		else: _navigate(field.get_global_mouse_position())
		get_viewport().set_input_as_handled()
	elif event.is_action_pressed("interact") or event.is_action_pressed("dialogue_continue"):
		var site := str(field.nearest)
		if site != "": _interact(site)
		get_viewport().set_input_as_handled()
	elif event.is_action_pressed("manuscript") or event.is_action_pressed("wait"):
		_show_self_choices()
		get_viewport().set_input_as_handled()

func _navigate(point: Vector2) -> void:
	_stop_walk("new-target")
	var destination_room: String = field.adapter.room_of(point)
	var own_room := str(field.snapshot.get("execution_room", ""))
	if not field.in_sight(point):
		var doorway_visible := false
		for direction in field.APPROACH:
			if field.in_sight(point + direction.normalized() * 32.0): doorway_visible = true
		var admitted := false
		for option in options:
			var binding: Dictionary = option.get("interaction", {})
			if str(binding.get("kind", "")) == "doorway" and str(binding.get("destination", {}).get("room", "")) == destination_room: admitted = true
		if not doorway_visible or not admitted: return
	if not field.adapter.walkable(point, field.player): return
	_route = NAV.route_on_grid(field.player.position, point, _local_grid(destination_room if destination_room != own_room else ""))
	if not _route.is_empty(): _route.append(point)

func _local_grid(destination_room := "") -> AStarGrid2D:
	var grid: AStarGrid2D = field.adapter.nav_grid(field.player)
	var room := str(field.snapshot.get("execution_room", ""))
	for y in range(grid.region.position.y, grid.region.end.y):
		for x in range(grid.region.position.x, grid.region.end.x):
			var cell := Vector2i(x, y)
			var candidate_room: String = field.adapter.room_of(grid.get_point_position(cell))
			if candidate_room != room and candidate_room != destination_room: grid.set_point_solid(cell)
	return grid

func _approach(site: String) -> void:
	if not field.site_visible(site): return
	if _in_reach(site):
		_interact(site)
		return
	var center: Vector2 = field.site_point(site)
	var station := {"point": center, "radius": float(field.sites[site].reach) - 4.0}
	var grid: AStarGrid2D = _local_grid()
	var best := INF
	var chosen := PackedVector2Array()
	for point in field._station_approaches(station, grid):
		if field.adapter.entered_room(point) != str(field.snapshot.get("execution_room", "")) or not field.adapter.walkable(point, field.player): continue
		if field.position_occupied(field.player, point): continue
		var route := NAV.route_on_grid(field.player.position, point, grid)
		if route.is_empty(): continue
		var distance: float = field._route_cost(field.player.position, route, point)
		if distance < best:
			best = distance
			chosen = route
			chosen.append(point)
	_stop_walk("new-target")
	_route = chosen
	_site = site

func _in_reach(site: String) -> bool:
	return field.sites.has(site) and field.site_visible(site) and field.player.position.distance_to(field.site_point(site)) <= float(field.sites[site].reach)

func choices_for(site: String) -> Array:
	var result: Array = []
	for option in options:
		var binding: Dictionary = option.get("interaction", {})
		if (str(binding.get("kind", "")) == "site" and str(binding.get("site", "")) == site) or (str(binding.get("kind", "")) == "person" and site == "person:" + str(binding.get("person", ""))): result.append(option)
	return result

func _interact(site: String) -> void:
	if not _in_reach(site): return
	_stop_walk("interaction")
	var choices := choices_for(site)
	if choices.size() == 1: _submit(choices[0], site, "hotspot")
	elif choices.size() > 1:
		var id := _id()
		_stamp("input_request", id, {"site": site, "request": "local-choices"})
		field.field_event.emit({"kind": "spatial_choices", "interaction_id": id, "site": site, "title": field.sites[site].title, "options": choices, "screen_position": field.get_global_transform_with_canvas() * field.site_point(site)})

func _show_self_choices() -> void:
	var choices: Array = []
	for option in options:
		if str(option.get("interaction", {}).get("kind", "")) == "self": choices.append(option)
	if choices.is_empty(): return
	var id := _id()
	_stamp("input_request", id, {"site": "self", "request": "local-choices"})
	field.field_event.emit({"kind": "spatial_choices", "interaction_id": id, "site": "self", "title": "随身手稿", "options": choices, "screen_position": field.get_global_transform_with_canvas() * field.player.position})

func select(id: String, site: String) -> void:
	if submitted or (site != "self" and not _in_reach(site)): return
	for option in options:
		if str(option.id) == id and ((site == "self" and str(option.get("interaction", {}).get("kind", "")) == "self") or option in choices_for(site)):
			_submit(option, site, "local-choice")
			return

func _submit(option: Dictionary, site: String, source: String) -> void:
	var id := _id()
	_stamp("input_request", id, {"choice_id": option.id, "site": site, "source": source, "game_time": field.snapshot.get("game_time", 0)})
	submitted = true
	field.field_event.emit({"kind": "select", "choice_id": option.id, "site": site, "interaction_id": id, "context": field.context()})
