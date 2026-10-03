extends Node
## Walk the planned body to a reachable operation mark before committing.
var host: Node
var busy := false
var walker: CharacterBody2D
var cancelled := false
var cancel_result := {}

func perform(operation: String) -> Dictionary:
	if busy: return {"ok":false, "message":"人物正在完成当前操作。"}
	var plan: Dictionary = host.red.call("PrepareSafeAction", operation)
	if not plan.get("ok", false): return plan
	var actor_key := ""
	for key in host.STORY_ACTORS:
		if str(host.STORY_ACTORS[key]) == str(plan.get("actor", "")):
			actor_key = str(key)
			break
	walker = host.actors.get(actor_key)
	var site := str(plan.get("site", ""))
	if not is_instance_valid(walker) or not host.sites.has(site):
		return _fail("现场没有对应的人物或操作位置。")
	if walker.travelling: return _fail(actor_key + "正在执行其他移动。")
	var origin: Vector2 = walker.position
	var release := str(plan.get("operation", operation)).get_slice(":", 0) == "release"
	if not release:
		var movable: Dictionary = host.story_can_move(actor_key)
		if not movable.get("ok", false): return _fail(str(movable.get("message", "人物当前不能离开岗位。")))
	cancelled = false
	cancel_result = {}
	busy = true
	host._cancel_click_navigation()
	var page_title := "触物与具名转述" if operation.begins_with("recall_") else "凭证与约定交付 · 本人核验" if operation.begins_with("reward:") else "空载试件 · 人物操作"
	host._open_modal(page_title, "red")
	host._label(host.modal_box, actor_key + ("原位放开试柄。" if release else "正在前往" + host._site_display_title(site) + "。抵达后再操作。"), 20)
	host.modal.add_footer([["取消本次走位", cancel]])
	if not release:
		var mark: Dictionary = _operation_mark(site)
		if not mark.get("ok", false):
			busy = false
			return _fail("操作位置暂时没有可走通的路线，请让出通道后再试。")
		var reached: bool = await host._walk_actor_to(walker, mark.point)
		if cancelled:
			busy = false
			return cancel_result
		if not reached:
			busy = false
			return _fail("人物没有抵达操作位置，本次操作未提交。")
	if cancelled:
		busy = false
		return cancel_result
	walker.face_point(host._site_point(site))
	walker.pose("idle")
	var result: Dictionary = host.red.call("CommitSafeAction")
	if not result.get("ok", false): host.red.call("CancelPreparedAction")
	busy = false
	# A companion who only came to check a delivery steps back to where they were,
	# so the party does not pile up at the cabinet.
	if operation.begins_with("reward:") and walker != host.player and result.get("ok", false):
		host._walk_actor_to(walker, origin)
	walker = null
	return result

func _operation_mark(site: String) -> Dictionary:
	var point: Vector2 = host._site_point(site)
	var obstacles: Array[Rect2] = host._navigation_obstacles(walker.position, walker)
	var grid: AStarGrid2D = host.FieldNavigation.build_route_grid(host.floors, obstacles, Vector2i(host.layout.rows[0].length(), host.layout.rows.size()))
	var best := {}
	var best_cost := INF
	# Existing half-cell navigation centres around the real object footprint.
	# The closest usable mark is preferred; actor travel distance breaks ties.
	var reach := minf(float(host.sites[site].reach), 56.0)
	var center := Vector2i(point / 16.0)
	for y in range(-4, 5):
		for x in range(-4, 5):
			var cell := center + Vector2i(x, y)
			if not grid.region.has_point(cell) or grid.is_point_solid(cell): continue
			var candidate: Vector2 = grid.get_point_position(cell)
			var proximity := candidate.distance_to(point)
			if proximity < 20.0 or proximity > reach: continue
			var path: PackedVector2Array = host.FieldNavigation.route_on_grid(walker.position, candidate, grid)
			if path.is_empty(): continue
			var distance := walker.position.distance_to(path[0])
			for index in range(1, path.size()): distance += path[index - 1].distance_to(path[index])
			# Stand on the visible side of wall-backed objects, never behind them.
			var behind := 1000000.0 if candidate.y < point.y - 4.0 else 0.0
			var cost := proximity * 1000.0 + distance + behind
			if cost < best_cost:
				best_cost = cost
				best = {"ok":true, "point":candidate}
	return best

func cancel() -> void:
	if not busy: return
	cancelled = true
	cancel_result = host.red.call("CancelPreparedAction") if is_instance_valid(host.red) else {}
	cancel_result["ok"] = false
	cancel_result["message"] = "本次走位已取消，尚未抵达的操作没有提交。"
	if is_instance_valid(walker):
		walker.travelling = false
		walker.velocity = Vector2.ZERO
		walker.pose("idle")
		walker.arrived.emit()

func _fail(message: String) -> Dictionary:
	var result: Dictionary = host.red.call("CancelPreparedAction")
	result["ok"] = false
	result["message"] = message
	walker = null
	return result
