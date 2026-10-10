extends SceneTree
## Neutral scene-tree contract. No chapter setup, authored map, story or receipt fixture.
const CampusMap := preload("res://scenes/Chapter2/Field/CampusMapView.gd")
const WorldField := preload("res://scenes/Shared/Field/WorldView.gd")
const StoryActor := preload("res://scenes/Story/StoryActor.gd")
const Navigation := preload("res://scenes/Shared/FieldNavigation.gd")
const AgentPilot := preload("res://scenes/Host/AgentPilot.gd")

class NeutralSeatEngine extends Node:
	var closed := false
	func AgentTimeJson() -> String: return "{}"
	func AgentSelections() -> int: return 0
	func CloseAgent() -> void: closed = true

class NeutralSeatHost extends Node:
	var field: Node

class KeyObserver extends Node:
	var edges: Array[Dictionary] = []
	func _input(event: InputEvent) -> void:
		if event is InputEventKey and event.physical_keycode == KEY_D:
			edges.append({"pressed": event.pressed, "wall_ms": Time.get_ticks_msec()})
	func duration() -> int:
		return int(edges[-1].wall_ms) - int(edges[0].wall_ms) if edges.size() >= 2 else -1

class MotionObserver extends Node:
	var actor: CharacterBody2D
	var positions: Array[Vector2] = []
	var colliders: Dictionary = {}
	func _physics_process(_delta: float) -> void:
		positions.append(actor.position)
		for index in actor.get_slide_collision_count():
			var collision := actor.get_slide_collision(index)
			var collider := collision.get_collider() as Node
			if collider != null: colliders[str(collider.name)] = true
	func reset() -> void:
		positions.clear()
		colliders.clear()

var failed := false
var field: Node2D
var adapter: Node2D
var player: CharacterBody2D
var observer: MotionObserver
var blocked_events: Array[Dictionary] = []
var rollback_before_trigger := Vector2(INF, INF)
var position_at_trigger := Vector2(INF, INF)

func check(condition: bool, label: String) -> void:
	print(("PASS " if condition else "FAIL ") + label)
	if not condition: failed = true

func _initialize() -> void:
	_run.call_deferred()

func _rectangle(parent: Node, node_name: String, point: Vector2, size: Vector2) -> CollisionShape2D:
	var node := CollisionShape2D.new()
	node.name = node_name
	node.position = point
	var rectangle := RectangleShape2D.new()
	rectangle.size = size
	node.shape = rectangle
	parent.add_child(node)
	return node

func _exit(parent: Node, node_name: String, point: Vector2, target: String) -> Area2D:
	var area := Area2D.new()
	area.name = node_name
	area.position = point
	area.collision_layer = 0
	area.collision_mask = 2
	area.set_meta("target", target)
	area.set_meta("spawn", "neutral")
	_rectangle(area, "Shape", Vector2(8, 0), Vector2(32, 128))
	parent.add_child(area)
	return area

func _neutral_frames() -> SpriteFrames:
	var frames := SpriteFrames.new()
	var texture := GradientTexture2D.new()
	texture.width = 1
	texture.height = 1
	texture.gradient = Gradient.new()
	for action in ["idle", "walk"]:
		for direction in ["front", "back", "left"]:
			var animation: String = action + "_" + direction
			frames.add_animation(animation)
			frames.add_frame(animation, texture)
	return frames

func _make_neutral_tree() -> void:
	field = WorldField.new()
	field.name = "neutral_field"
	field.follow_camera = false
	field.snapshot = {"field_enabled": true, "player_control": true}
	field.set_process(false)
	field.set_process_unhandled_input(false)
	adapter = CampusMap.new()
	adapter.name = "neutral_adapter"
	adapter.field = field
	adapter.key = "neutral"
	field.adapter = adapter
	field.add_child(adapter)
	var map := Node2D.new()
	map.name = "neutral"
	map.set_meta("map_size", Vector2(384, 256))
	adapter.map = map
	adapter.add_child(map)
	var solids := StaticBody2D.new()
	solids.name = "Solids"
	solids.collision_layer = 1
	solids.collision_mask = 2
	map.add_child(solids)
	_rectangle(solids, "neutral_top", Vector2(192, -8), Vector2(416, 16))
	_rectangle(solids, "neutral_middle", Vector2(192, 128), Vector2(416, 16))
	_rectangle(solids, "neutral_bottom", Vector2(192, 264), Vector2(416, 16))
	_rectangle(solids, "neutral_left", Vector2(-8, 128), Vector2(16, 288))
	_rectangle(solids, "neutral_right", Vector2(392, 128), Vector2(16, 288))
	var exits := Node2D.new()
	exits.name = "Exits"
	map.add_child(exits)
	_exit(exits, "neutral_upper", Vector2(184, 64), "neutral_a")
	_exit(exits, "neutral_lower", Vector2(184, 200), "neutral_b")
	var objects := Node2D.new()
	objects.name = "Objects"
	map.add_child(objects)
	player = StoryActor.new()
	player.name = "neutral_player"
	player.standard = _neutral_frames()
	player.speed = 240.0
	player.position = Vector2(88, 72)
	field.player = player
	field.actors = {"neutral_player": player}
	field._player_key = "neutral_player"
	objects.add_child(player)
	root.add_child(field)
	observer = MotionObserver.new()
	observer.actor = player
	root.add_child(observer)
	field.field_event.connect(_on_field_event)

func _settle() -> void:
	await process_frame
	await physics_frame
	await physics_frame

func _access(a: bool, b: bool) -> void:
	adapter.apply_snapshot({"access": {"maps": {"neutral_a": a, "neutral_b": b}}})
	await _settle()

func _route(start: Vector2, finish: Vector2) -> PackedVector2Array:
	return Navigation.route_on_grid(start, finish, adapter.nav_grid(player))

func _walk_from(start: Vector2, label: String) -> Dictionary:
	# Positions are fixture preparation only; subsequent displacement is the formal
	# field input loop, StoryActor.move_input and the live physics server.
	player.position = start
	player.velocity = Vector2.ZERO
	field._cancel_click()
	await _settle()
	observer.reset()
	Input.action_press("move_right")
	await create_timer(1.1).timeout
	Input.action_release("move_right")
	await physics_frame
	var maximum_x := start.x
	for point in observer.positions: maximum_x = maxf(maximum_x, point.x)
	var result := {"start": [start.x, start.y], "end": [player.position.x, player.position.y],
		"maximum_x": maximum_x, "colliders": observer.colliders.keys()}
	print("OBSERVATION " + label + " " + JSON.stringify(result))
	return result

func _on_field_event(event: Dictionary) -> void:
	if event.get("kind", "") == "blocked_move": blocked_events.append(event.duplicate(true))

func _close_at_actual_entry(body: Node) -> void:
	if body != player: return
	rollback_before_trigger = adapter._last_allowed_position
	position_at_trigger = player.position
	adapter.apply_snapshot({"access": {"maps": {"neutral_a": false, "neutral_b": true}}})

func _stop_after_wall(pilot: Node, milliseconds: int) -> void:
	var deadline := Time.get_ticks_msec() + milliseconds
	while Time.get_ticks_msec() < deadline: await process_frame
	pilot.stop("neutral-stop")

func _keyboard_deadline() -> void:
	var pilot := AgentPilot.new()
	var seat := NeutralSeatHost.new()
	var engine := NeutralSeatEngine.new()
	var keys := KeyObserver.new()
	seat.field = field
	pilot.host = seat
	pilot.engine = engine
	root.add_child(seat)
	root.add_child(engine)
	root.add_child(keys)
	root.add_child(pilot)
	await _access(true, true)
	player.position = Vector2(88, 200)
	player.velocity = Vector2.ZERO
	await _settle()
	var old_scale := Engine.time_scale
	Engine.time_scale = 4.0
	await process_frame
	# Simulate a slow preceding frame; no physics steps are manufactured.
	OS.delay_msec(240)
	var started := Time.get_ticks_msec()
	await pilot._keyboard_move(["D"])
	var elapsed := Time.get_ticks_msec() - started
	# parse_input_event queues the release for the next normal input flush.
	# Preserve callback timing separately and inspect the delivered edge after drain.
	var pressed_at_return := Input.is_physical_key_pressed(KEY_D)
	await _settle()
	var held := keys.duration()
	print("OBSERVATION keyboard-wall-deadline " + JSON.stringify({"elapsed_wall_ms": elapsed, "key_held_wall_ms": held,
		"edges": keys.edges, "pressed_at_callback_return": pressed_at_return,
		"time_scale": Engine.time_scale, "end_position": [player.position.x, player.position.y]}))
	check(keys.edges.size() == 2 and bool(keys.edges[0].pressed) and not bool(keys.edges[-1].pressed),
		"pilot dispatches one real physical key press and matching release")
	check(elapsed >= 350 and elapsed <= 500 and held >= 350 and held <= 500,
		"busy preceding frame and accelerated engine retain the real 350ms key hold")
	check(player.position.x > 176.0 and not Input.is_physical_key_pressed(KEY_D) and pilot._pressed.is_empty(),
		"wall-clock key input moves through the formal field and leaves no held key")
	Engine.time_scale = old_scale
	player.position = Vector2(88, 200)
	player.velocity = Vector2.ZERO
	await _settle()
	keys.edges.clear()
	started = Time.get_ticks_msec()
	_stop_after_wall(pilot, 100)
	await pilot._keyboard_move(["D"])
	elapsed = Time.get_ticks_msec() - started
	pressed_at_return = Input.is_physical_key_pressed(KEY_D)
	await _settle()
	held = keys.duration()
	print("OBSERVATION keyboard-stop " + JSON.stringify({"elapsed_wall_ms": elapsed, "key_held_wall_ms": held,
		"edges": keys.edges, "pressed_at_callback_return": pressed_at_return,
		"engine_closed": engine.closed, "end_position": [player.position.x, player.position.y]}))
	check(elapsed >= 100 and elapsed < 350 and held >= 100 and held < 350,
		"active stop releases the key before its normal wall-clock deadline")
	check(pilot.stopping and engine.closed and not Input.is_physical_key_pressed(KEY_D) and pilot._pressed.is_empty(),
		"stop closes the seat and clears physical key state")
	pilot.queue_free()
	keys.queue_free()
	seat.queue_free()
	engine.queue_free()
	await process_frame

func _narrow_route() -> void:
	var neutral := Node2D.new()
	neutral.position = Vector2(600, 0)
	var solid := StaticBody2D.new()
	solid.collision_layer = 1
	solid.collision_mask = 2
	neutral.add_child(solid)
	_rectangle(solid, "left", Vector2(44, 128), Vector2(88, 96))
	_rectangle(solid, "right", Vector2(248, 128), Vector2(272, 96))
	var walker := StoryActor.new()
	walker.name = "neutral_narrow_walker"
	walker.standard = _neutral_frames()
	walker.position = Vector2(100, 44)
	walker.speed = 240.0
	neutral.add_child(walker)
	root.add_child(neutral)
	await _settle()
	var floors := {}
	for y in 8:
		for x in 12: floors[Vector2i(x, y)] = true
	var obstacles: Array[Rect2] = [Rect2(0, 80, 88, 96), Rect2(112, 80, 272, 96)]
	var fine := Navigation.build_route_grid(floors, obstacles, Vector2i(12, 8))
	var route := Navigation.route_on_grid(Vector2(100, 44), Vector2(100, 212), fine)
	check(fine.cell_size == Vector2(8, 8) and not route.is_empty(), "fine grid preserves the neutral 24px narrow passage")
	var coarse := AStarGrid2D.new()
	coarse.region = Rect2i(0, 0, 24, 16)
	coarse.cell_size = Vector2(16, 16)
	coarse.offset = Vector2(8, 8)
	coarse.diagonal_mode = AStarGrid2D.DIAGONAL_MODE_NEVER
	coarse.update()
	for y in 16:
		for x in 24:
			var cell := Vector2i(x, y)
			coarse.set_point_solid(cell, not Navigation.walkable(coarse.get_point_position(cell), floors, obstacles))
	check(Navigation.route_on_grid(Vector2(100, 44), Vector2(100, 212), coarse).is_empty(),
		"coarse centre sampling loses that physically viable narrow passage")
	if not route.is_empty():
		check(Array(route).all(func(point: Vector2) -> bool: return Navigation.walkable(point, floors, obstacles)),
			"narrow route retains the original conservative footprint predicate")
		var result: Dictionary = await walker.follow_path(route)
		print("OBSERVATION physical-narrow-route " + JSON.stringify({"result": result,
			"final_position": [walker.position.x, walker.position.y], "route": Array(route)}))
		check(bool(result.ok) and walker.position.distance_to(Vector2(100, 212)) <= 0.1,
			"original StoryActor collision body physically completes the fine-grid route")
	walker.position = Vector2(100, 128)
	walker.velocity = Vector2.ZERO
	await _settle()
	var hit: Dictionary = await walker.walk_to(Vector2(60, 128))
	print("OBSERVATION physical-narrow-wall " + JSON.stringify({"result": hit, "final_position": [walker.position.x, walker.position.y]}))
	check(not bool(hit.ok) and walker.position.x >= 95.0 and walker.position.x < 100.0,
		"narrow route keeps the original body collision against adjacent solids")
	check(not Navigation.walkable(Vector2(84, 128), floors, obstacles), "fine grid does not make a neighbouring wall traversable")
	neutral.queue_free()
	await process_frame

func _run() -> void:
	_make_neutral_tree()
	await _keyboard_deadline()
	await _narrow_route()
	await _access(false, true)
	var solids: StaticBody2D = adapter.map.get_node("Solids")
	var upper: CollisionShape2D = solids.get_node("Access_neutral_upper")
	var lower: CollisionShape2D = solids.get_node("Access_neutral_lower")
	check(not upper.disabled and lower.disabled, "current destination access controls each physical barrier")
	check(upper.position == Vector2(192, 64) and upper.shape == adapter.map.get_node("Exits/neutral_upper/Shape").shape,
		"barrier reuses authored exit shape and local position")
	check(not adapter.walkable(Vector2(192, 72), player), "closed doorway is not a walkable navigation cell")
	check(_route(Vector2(88, 72), Vector2(296, 72)).is_empty(), "closed doorway has no route through the isolated upper corridor")
	var closed := await _walk_from(Vector2(88, 72), "closed-upper-keyboard")
	check(float(closed.maximum_x) < 176.0 and float(closed.maximum_x) > 140.0 and closed.colliders.has("Solids"),
		"keyboard movement reaches and physically collides with the closed doorway")
	field._route_click(Vector2(296, 72))
	check(field.click_path.is_empty(), "formal click routing refuses the closed doorway")
	check(not _route(Vector2(88, 200), Vector2(296, 200)).is_empty(), "closing the upper exit preserves the allowed lower route")
	var allowed := await _walk_from(Vector2(88, 200), "allowed-lower-keyboard")
	check(float(allowed.maximum_x) > 296.0, "closing another exit preserves actual movement through the allowed doorway")
	var upper_id := upper.get_instance_id()
	var lower_id := lower.get_instance_id()
	var solids_before: int = solids.get_child_count()
	var grid_before: AStarGrid2D = adapter.nav_grid(player)
	for _repeat in range(3): await _access(false, true)
	check(solids.get_child_count() == solids_before and upper.get_instance_id() == upper_id and lower.get_instance_id() == lower_id,
		"repeated identical snapshots do not stack collision barriers")
	check(adapter.nav_grid(player) == grid_before, "identical access snapshots preserve the existing navigation grid")
	await _access(true, false)
	check(upper.disabled and not lower.disabled and adapter.nav_grid(player) != grid_before,
		"changed access disables the opened barrier and rebuilds navigation")
	check(adapter.walkable(Vector2(192, 72), player) and not _route(Vector2(88, 72), Vector2(296, 72)).is_empty(),
		"opening the exit restores doorway walkability and a complete route")
	var opened := await _walk_from(Vector2(88, 72), "opened-upper-keyboard")
	check(float(opened.maximum_x) > 296.0, "opening the exit restores physical passage")
	check(_route(Vector2(88, 200), Vector2(296, 200)).is_empty(), "closing the other destination removes only its route")
	var lower_closed := await _walk_from(Vector2(88, 200), "closed-lower-keyboard")
	check(float(lower_closed.maximum_x) < 176.0 and lower_closed.colliders.has("Solids"), "the independently closed lower exit also blocks actual movement")
	await _access(false, true)
	check(not upper.disabled and lower.disabled and solids.get_child_count() == solids_before,
		"closing an opened exit restores its barrier without duplicate shapes")
	await _access(true, true)
	player.position = Vector2(88, 72)
	player.velocity = Vector2.ZERO
	await _settle()
	field._route_click(Vector2(296, 72))
	check(not field.click_path.is_empty(), "opening the exit restores formal click navigation")
	await create_timer(1.1).timeout
	check(player.position.x > 280.0 and field.click_path.is_empty(), "formal click movement crosses the opened exit and completes")
	# Close access inside the real Area2D entry callback. The pending map change
	# must be rejected by WorldView, without injecting a presented/arrival receipt.
	player.position = Vector2(88, 72)
	player.velocity = Vector2.ZERO
	await _settle()
	var upper_exit: Area2D = adapter.map.get_node("Exits/neutral_upper")
	upper_exit.body_entered.connect(_close_at_actual_entry)
	upper_exit.body_entered.connect(adapter._on_exit_entered.bind(upper_exit))
	upper_exit.body_exited.connect(adapter._on_exit_left)
	adapter.armed = true
	field._route_click(Vector2(296, 72))
	await create_timer(0.8).timeout
	print("OBSERVATION indoor-entry-race " + JSON.stringify({
		"position_at_trigger": [position_at_trigger.x, position_at_trigger.y],
		"last_allowed": [rollback_before_trigger.x, rollback_before_trigger.y],
		"after_rejection": [player.position.x, player.position.y],
		"map": adapter.map_key(), "blocked_events": blocked_events}))
	check(not adapter.map.has_meta("outdoor_core") and is_finite(rollback_before_trigger.x)
		and position_at_trigger.x > rollback_before_trigger.x, "indoor rejection follows actual entry movement from a remembered valid position")
	check(blocked_events.size() == 1 and blocked_events[0].get("map", "") == "neutral_a"
		and adapter.map_key() == "neutral" and not adapter.switching(), "denied deferred map change preserves the indoor map and releases switching state")
	check(player.position.is_equal_approx(rollback_before_trigger), "indoor blocked entry restores the last allowed physical position")
	check(field.click_path.is_empty() and field.click_target == "" and field.click_waypoint == 0,
		"denied map change cancels the active click path")
	var stopped_position := player.position
	await create_timer(0.3).timeout
	check(player.position.is_equal_approx(stopped_position), "cancelled click path cannot resume movement after rejection")
	Input.action_release("move_right")
	field.begin_shutdown()
	observer.queue_free()
	field.queue_free()
	await process_frame
	await process_frame
	print("CAMPUS ACCESS CHECK COMPLETE: " + ("FAIL" if failed else "PASS"))
	quit(1 if failed else 0)
