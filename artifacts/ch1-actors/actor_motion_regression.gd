extends SceneTree
## Independent, synthetic physics regression. No gameplay or UI acceptance.
const ActorScript = preload("res://scenes/Story/StoryActor.gd")
const WorldScript = preload("res://scenes/Shared/Field/WorldView.gd")

class Adapter extends Node2D:
	var grid: AStarGrid2D
	func nav_grid(_actor: CharacterBody2D) -> AStarGrid2D: return grid
	func walkable(_point: Vector2, _actor: CharacterBody2D) -> bool: return true
	func switching() -> bool: return false
	func anchor(_key: String): return null

var checks := 0
var failures := 0
var area: Node2D
var view: Node2D
var adapter: Adapter
var replies: Array[Dictionary] = []
var waiter: Dictionary = {}

func _initialize() -> void:
	_run.call_deferred()

func check(ok: bool, label: String, detail = "") -> void:
	checks += 1
	if not ok: failures += 1
	print(("PASS " if ok else "FAIL ") + label + (" | " + str(detail) if str(detail) != "" else ""))

func frames() -> SpriteFrames:
	var value := SpriteFrames.new()
	value.remove_animation("default")
	var picture := Image.create(1, 1, false, Image.FORMAT_RGBA8)
	picture.fill(Color.WHITE)
	var texture := ImageTexture.create_from_image(picture)
	for action in ["idle", "walk"]:
		for direction in ["front", "back", "left"]:
			value.add_animation(action + "_" + direction)
			value.add_frame(action + "_" + direction, texture)
	return value

func actor_at(point: Vector2) -> CharacterBody2D:
	var actor := ActorScript.new()
	actor.standard = frames()
	actor.position = point
	actor.speed = 180.0
	area.add_child(actor)
	return actor

func wall_at(rect: Rect2) -> StaticBody2D:
	var wall := StaticBody2D.new()
	wall.collision_layer = 1
	var shape := CollisionShape2D.new()
	var box := RectangleShape2D.new()
	box.size = rect.size
	shape.shape = box
	wall.position = rect.get_center()
	wall.add_child(shape)
	area.add_child(wall)
	return wall

func open_grid() -> AStarGrid2D:
	var grid := AStarGrid2D.new()
	grid.region = Rect2i(0, 0, 24, 24)
	grid.cell_size = Vector2(16, 16)
	grid.offset = Vector2(8, 8)
	grid.diagonal_mode = AStarGrid2D.DIAGONAL_MODE_NEVER
	grid.update()
	return grid

func start_actor_wait(actor: CharacterBody2D, route: PackedVector2Array, state: Dictionary) -> void:
	state.result = await actor.follow_path(route)
	state.done = true

func start_follow_wait(actor: CharacterBody2D, route: PackedVector2Array, key: String, state: Dictionary) -> void:
	state.result = await view._follow(actor, route, key)
	state.done = true

func until_done(state: Dictionary, limit := 240) -> bool:
	for _frame in limit:
		await physics_frame
		if state.get("done", false): return true
	return false

func until_reply(id: String, limit := 240) -> Dictionary:
	for _frame in limit:
		for reply in replies:
			if reply.presentation_id == id: return reply
		await physics_frame
	return {}

func wait_frames(count: int) -> void:
	for _frame in count: await physics_frame

func _run() -> void:
	area = Node2D.new()
	root.add_child(area)
	view = WorldScript.new()
	area.add_child(view)
	view.set_process(false)
	view.set_physics_process(false)
	view.set_process_input(false)
	view.set_process_unhandled_input(false)
	adapter = Adapter.new()
	adapter.grid = open_grid()
	view.adapter = adapter
	view.add_child(adapter)
	view.snapshot = {"field_enabled": true}
	view.field_event.connect(func(reply: Dictionary): replies.append(reply.duplicate(true)))
	var actor := actor_at(Vector2(88, 104))
	view.actors = {"walker": actor}
	view.player = actor
	var wall := wall_at(Rect2(144, 70, 16, 80))
	await wait_frames(2)
	var signals := {"count": 0}
	actor.arrived.connect(func(): signals.count += 1)
	actor.velocity = Vector2(90, 0)
	actor.pose("walk", "left")
	actor.stop_travel()
	check(actor.velocity == Vector2.ZERO and not actor.travelling and str(actor.sprite.animation).begins_with("idle_"), "stop without active path still clears velocity and walking pose")
	var state := {}
	start_actor_wait(actor, PackedVector2Array([Vector2(248, 104)]), state)
	check(await until_done(state), "blocked walk releases awaited coroutine")
	check(state.get("result", {}).get("ok", true) == false and state.get("result", {}).get("reason", "") == "stuck", "blocked walk reports stuck", state)
	check(not actor.travelling and actor.path.is_empty() and actor.velocity == Vector2.ZERO and str(actor.sprite.animation).begins_with("idle_"), "blocked walk ends idle")
	check(signals.count == 1 and actor.position.x <= 136.1, "blocked walk emits arrived once without crossing wall", actor.position)
	actor.position = Vector2(135, 104)
	await wait_frames(2)
	state = {}
	start_actor_wait(actor, PackedVector2Array([Vector2(138, 104)]), state)
	check(await until_done(state) and state.get("result", {}).get("reason", "") == "stuck" and actor.position.x <= 136.1, "short final step preserves collision", {"result": state, "position": actor.position})
	actor.position = Vector2(88, 104)
	await wait_frames(2)
	state = {}
	start_actor_wait(actor, PackedVector2Array([Vector2(88, 184), Vector2(248, 184), Vector2(248, 104)]), state)
	check(await until_done(state) and state.get("result", {}).get("ok", false) and actor.position.distance_to(Vector2(248, 104)) <= 0.1, "multisegment path arrives around physical obstacle", state)
	actor.position = Vector2(88, 104)
	adapter.grid = open_grid()
	for y in 24: adapter.grid.set_point_solid(Vector2i(10, y), true)
	await wait_frames(2)
	var before: Vector2 = actor.position
	view._stage({"op": "walk", "who": "walker", "to": {"point": [248, 104]}, "presentation_id": "no-route"})
	var reply := await until_reply("no-route")
	check(reply.get("ok", true) == false and reply.get("reason", "") == "no_route" and actor.position == before, "no-route stage ack with no displacement", reply)
	adapter.grid = open_grid()
	view._stage({"op": "walk", "who": "walker", "to": {"point": [248, 104]}, "presentation_id": "stage-stuck"})
	reply = await until_reply("stage-stuck")
	check(reply.get("ok", true) == false and reply.get("reason", "") == "stuck" and actor.position.x <= 136.1 and not actor.travelling, "stuck stage ack without teleport", {"reply": reply, "position": actor.position})
	actor.position = Vector2(135, 104)
	await wait_frames(2)
	view._stage({"op": "walk", "who": "walker", "to": {"point": [138, 104]}, "presentation_id": "stage-short"})
	reply = await until_reply("stage-short")
	check(reply.get("ok", true) == false and reply.get("reason", "") == "stuck" and actor.position.x <= 136.1, "short stage walk cannot bypass obstacle via early ack", {"reply": reply, "position": actor.position})
	wall.queue_free()
	actor.position = Vector2(88, 104)
	var other := actor_at(Vector2(280, 104))
	other.collision_layer = 0
	view.actors.other = other
	adapter.grid = open_grid()
	for y in 24: adapter.grid.set_point_solid(Vector2i(10, y), true)
	await wait_frames(2)
	view._stage({"op": "walk", "who": "walker", "to": {"near_actor": "other", "offsets": [[-32, 0], [-200, 0]]}, "presentation_id": "near-later"})
	reply = await until_reply("near-later")
	check(reply.get("ok", false) and actor.position.distance_to(Vector2(80, 104)) <= 0.1, "near_actor skips disconnected first candidate", {"reply": reply, "position": actor.position})
	adapter.grid = open_grid()
	actor.position = Vector2(88, 104)
	state = {}
	start_actor_wait(actor, PackedVector2Array([Vector2(248, 104)]), state)
	await wait_frames(4)
	actor.pause_travel()
	before = actor.position
	await wait_frames(40)
	check(not state.get("done", false) and actor.position == before and not actor.path.is_empty(), "actor pause preserves await and position")
	actor.resume_travel()
	check(await until_done(state) and state.get("result", {}).get("ok", false), "actor resume completes original await", state)
	actor.position = Vector2(88, 104)
	view.snapshot.field_enabled = true
	state = {}
	start_follow_wait(actor, PackedVector2Array([Vector2(248, 104)]), "walker", state)
	await wait_frames(4)
	view.snapshot.field_enabled = false
	await wait_frames(4)
	before = actor.position
	await wait_frames(40)
	check(not state.get("done", false) and actor.position == before and view._paused_walks.has("walker"), "world pause preserves ongoing route")
	view.snapshot.field_enabled = true
	check(await until_done(state) and state.get("result", {}).get("ok", false) and view._paused_walks.is_empty(), "world resume completes awaited route", state)
	actor.position = Vector2(88, 104)
	state = {}
	start_follow_wait(actor, PackedVector2Array([Vector2(248, 104)]), "walker", state)
	await wait_frames(4)
	view.snapshot.field_enabled = false
	await wait_frames(4)
	var cancelled_signal: int = signals.count
	view._cancel_motion(actor)
	check(await until_done(state) and state.get("result", {}).get("reason", "") == "cancelled" and actor.path.is_empty() and view._paused_walks.is_empty() and signals.count == cancelled_signal + 1, "paused cancellation releases signal and follow wait", state)
	view.snapshot.field_enabled = true
	actor.position = Vector2(88, 104)
	actor.pose("idle", "back")
	other.position = Vector2(88, 123)
	other.velocity = Vector2.UP * 90.0
	view.player = other
	view._make_way()
	await wait_frames(2)
	check(actor.travelling and view._make_way_after.has(actor.get_instance_id()), "make-way starts physical sidestep and sets cooldown")
	for _frame in 180:
		await physics_frame
		if not actor.travelling: break
	await wait_frames(3)
	check(actor.facing == "back" and actor.travel_result.ok and int(view._make_way_after.get(actor.get_instance_id(), 0)) > Time.get_ticks_msec(), "make-way restores original facing and completion cooldown", actor.travel_result)
	other.position = actor.position + Vector2(0, 19)
	var generation := int(view._motion.get(actor.get_instance_id(), 0))
	view._make_way()
	check(not actor.travelling and int(view._motion.get(actor.get_instance_id(), 0)) == generation, "make-way cooldown suppresses repeated sidestep")
	view._make_way_after.clear()
	actor.pose("idle", "back")
	view._step_aside(actor, PackedVector2Array([actor.position + Vector2(50, 0)]))
	await wait_frames(3)
	view._cancel_motion(actor)
	actor.pose("idle", "left")
	state = {}
	start_follow_wait(actor, PackedVector2Array([actor.position + Vector2(-24, 0)]), "", state)
	check(await until_done(state), "new generation motion completes")
	await wait_frames(3)
	check(actor.facing == "left", "cancelled sidestep cannot restore old facing over new motion", actor.facing)
	print("RESULT %d checks, %d failures" % [checks, failures])
	view.begin_shutdown()
	area.queue_free()
	await process_frame
	quit(0 if failures == 0 else 1)
