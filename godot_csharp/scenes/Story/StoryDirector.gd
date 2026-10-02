extends Node
## Visual blocking requests a host command only after every authored actor reaches its mark.
signal busy_changed(value: bool)
signal beat_finished(id: String, result: Dictionary)
const Score = preload("res://scenes/Story/StoryScore.gd")
var host: Node
var cast := {}
var beats := {}
var busy := false
var last_result := {"ok": true, "message": ""}
var _generation := 0
var _walkers: Array[Node] = []

func setup(scene: Node, actors: Dictionary, resource: Resource) -> void:
	host = scene
	cast = actors
	var score := Score.parse(str(resource.get("raw_text")))
	beats.clear()
	if not score.errors.is_empty():
		last_result = {"ok": false, "message": "\n".join(score.errors)}
		push_error(last_result.message)
		return
	for beat in score.beats: beats[str(beat.id)] = beat

func play(id: String) -> Dictionary:
	# One cue joins the previous cue, including a cue that let dialogue continue.
	var joining_previous := busy
	var previous: Dictionary = await join()
	if joining_previous and not previous.get("ok", false): return previous
	if not beats.has(id): return _failure("没有剧情编排节点：" + id)
	var beat: Dictionary = beats[id]
	var preflight := _preflight(beat)
	if not preflight.ok: return preflight
	_generation += 1
	var generation := _generation
	last_result = {"ok": true, "message": "", "beat": id}
	_set_busy(true)
	_run(beat, generation)
	if beat.get("wait", true): return await join()
	if not busy: return last_result
	return {"ok": true, "pending": busy, "beat": id}

func join() -> Dictionary:
	while busy and is_inside_tree(): await get_tree().physics_frame
	return last_result

func cancel() -> void:
	_generation += 1
	for actor in _walkers:
		if is_instance_valid(actor):
			actor.travelling = false
			actor.velocity = Vector2.ZERO
			actor.pose("idle")
			actor.arrived.emit()
	# Keep the input lock until pending tracks unwind and restore their speed.
	last_result = {"ok": false, "message": "剧情编排已取消；已提交的机制结果保留。"}

func _preflight(beat: Dictionary) -> Dictionary:
	var errors := Score.validate(beat)
	if not errors.is_empty(): return _failure("\n".join(errors))
	if not is_instance_valid(host): return _failure("剧情宿主已释放。")
	var requirement: Dictionary = beat.get("require", {})
	if not requirement.is_empty():
		var result: Dictionary = host.call("story_require", requirement)
		if not result.get("ok", false): return _failure(str(result.get("message", "剧情前提未满足。")))
	var tracks: Dictionary = beat.get("tracks", {})
	for name in tracks:
		if not cast.has(name) or not is_instance_valid(cast[name]): return _failure("找不到演员：" + str(name))
		var actor: Node = cast[name]
		var track: Dictionary = tracks[name]
		if actor.travelling: return _failure("演员正在执行其他移动：" + str(name))
		if actor.seated and not track.get("stand", false) and track.get("path", []).is_empty() and not track.has("seat") and track.get("pose", "idle") != "idle":
			return _failure("%s 仍坐着；请先编排起立再播放该姿态。" % name)
		if not track.get("path", []).is_empty():
			var result: Dictionary = host.call("story_can_move", str(name))
			if not result.get("ok", false): return _failure(str(result.get("message", "演员仍在占用岗位。")))
		var direction := str(track.get("face", actor.facing))
		var suffix := "left" if direction == "right" else direction
		var action := "sit" if track.has("seat") else str(track.get("pose", "idle"))
		if actor.standard == null or not actor.standard.has_animation(action + "_" + suffix):
			return _failure("%s 缺少动画 %s_%s，编排未开始。" % [name, action, suffix])
	return {"ok": true}

func _run(beat: Dictionary, generation: int) -> void:
	var tracks: Dictionary = beat.get("tracks", {})
	var group := {"remaining": tracks.size(), "errors": []}
	_walkers.clear()
	for name in tracks:
		_walkers.append(cast[name])
		_run_track(str(name), tracks[name], generation, group)
	while group.remaining > 0 and is_inside_tree(): await get_tree().physics_frame
	_walkers.clear()
	if generation == _generation:
		if not group.errors.is_empty():
			last_result = {"ok": false, "message": "\n".join(group.errors), "beat": beat.id}
		elif beat.has("mechanism"):
			# The callback is a host whitelist, never an arbitrary evaluator or property write.
			last_result = host.call("story_commit", beat.mechanism)
			last_result["beat"] = beat.id
		else:
			last_result = {"ok": true, "message": "演员已完成编排。", "beat": beat.id}
	_set_busy(false)
	beat_finished.emit(str(beat.id), last_result)

func _run_track(name: String, track: Dictionary, generation: int, group: Dictionary) -> void:
	var actor: Node = cast[name]
	var original_speed: float = actor.speed
	actor.speed = float(track.get("speed", original_speed))
	if track.get("stand", false): actor.stand_up()
	for point in track.get("path", []):
		if generation != _generation: break
		var destination := Vector2(float(point[0]), float(point[1]))
		var reached: bool = await host.call("_walk_actor_to", actor, destination)
		if not reached:
			group.errors.append("%s 未抵达 %s；机制未提交。" % [name, str(destination)])
			break
	if is_instance_valid(actor):
		actor.speed = original_speed
		if generation == _generation and group.errors.is_empty():
			var direction := str(track.get("face", actor.facing))
			if track.has("seat"):
				actor.sit_at(direction, float(track.seat))
			elif actor.seated:
				actor.sit_at(direction, actor.seat_height)
			else:
				actor.pose(str(track.get("pose", "idle")), direction)
	group.remaining -= 1

func _failure(message: String) -> Dictionary:
	last_result = {"ok": false, "message": message}
	return last_result

func _set_busy(value: bool) -> void:
	busy = value
	busy_changed.emit(value)
