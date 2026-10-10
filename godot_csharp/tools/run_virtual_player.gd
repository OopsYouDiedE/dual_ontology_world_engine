extends SceneTree
## Trusted headless host. Reuses production field navigation, collisions, sight,
## stage acknowledgements and continuous GameSession; never loads a probe.
const EngineScript := preload("res://scenes/Host/GameEngine.cs")
const FieldScript := preload("res://scenes/Shared/Field/WorldView.gd")
const Navigation := preload("res://scenes/Shared/FieldNavigation.gd")
const ManuscriptPanel := preload("res://scenes/UI/Panels/Manuscript/ReplayManuscript.tscn")
const CampusPanel := preload("res://scenes/UI/Panels/CampusMap/CampusMap.tscn")
const MOVE_KEYS := {"W": KEY_W, "A": KEY_A, "S": KEY_S, "D": KEY_D}

var engine: Node
var field: Node2D
var output_file: FileAccess
var queue: Array = []
var draining := false
var stopping := false
var modal := false
var panel := ""
var replay_run_label := ""
var snapshot: Dictionary = {}
var seed := 0
var target_days := 3
var selection_limit := 10000
var observation_limit := 20000
var wall_limit := 600.0
var idle_limit := 30.0
var started := 0
var progressed := 0
var sequence := 0
var seen_public_states := {}
var last_time := ""
var pressed_move_keys: Array[int] = []
var keyboard_timer: SceneTreeTimer
var keyboard_start := Vector2.ZERO
var keyboard_names: Array = []
var keyboard_release_at := 0

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	var settings := {}
	for argument in OS.get_cmdline_user_args():
		var pieces := argument.split("=", true, 1)
		if pieces.size() != 2 or not pieces[0] in ["--seed", "--days", "--max-selections", "--max-observations", "--max-seconds", "--no-progress-seconds", "--declarations", "--output"] or settings.has(pieces[0]):
			push_error("Invalid or duplicate virtual-player argument: " + argument)
			quit(2)
			return
		settings[pieces[0]] = pieces[1]
	for key in ["--seed", "--days", "--max-selections", "--max-observations"]:
		if settings.has(key) and not str(settings[key]).is_valid_int():
			push_error("Expected an integer: " + key)
			quit(2)
			return
	for key in ["--max-seconds", "--no-progress-seconds"]:
		if settings.has(key) and (not str(settings[key]).is_valid_float() or not is_finite(float(settings[key]))):
			push_error("Expected a finite number: " + key)
			quit(2)
			return
	seed = int(settings.get("--seed", "0"))
	if seed < -2147483648 or seed > 2147483647:
		push_error("Seed must be a signed 32-bit integer (xorshift32-v1 tie stream).")
		quit(2)
		return
	target_days = int(settings.get("--days", "3"))
	selection_limit = int(settings.get("--max-selections", "10000"))
	observation_limit = int(settings.get("--max-observations", "20000"))
	wall_limit = float(settings.get("--max-seconds", "600"))
	idle_limit = float(settings.get("--no-progress-seconds", "30"))
	if not settings.has("--declarations") or not settings.has("--output") or target_days < 1 or selection_limit < 0 or observation_limit < 1 or wall_limit <= 0 or idle_limit <= 0:
		push_error("Required: --declarations=<compiled story> --output=<new JSONL>; budgets must be nonnegative/positive.")
		quit(2)
		return
	var path: String = settings["--output"]
	if FileAccess.file_exists(path):
		push_error("Refusing to overwrite an existing trajectory: " + path)
		quit(2)
		return
	DirAccess.make_dir_recursive_absolute(path.get_base_dir())
	output_file = FileAccess.open(path, FileAccess.WRITE)
	if output_file == null:
		push_error("Could not open trajectory output")
		quit(2)
		return
	started = Time.get_ticks_msec()
	progressed = started
	var shared_panel := ManuscriptPanel.instantiate()
	replay_run_label = shared_panel.get_node("LeftPage/Scroll/Column/Actions/RunPlayback").text
	shared_panel.free()
	engine = EngineScript.new()
	root.add_child(engine)
	engine.ConfigureAgent(FileAccess.get_file_as_string(settings["--declarations"]), seed, selection_limit, observation_limit)
	_record("start", {"seed": seed, "rng": "xorshift32-v1", "target_days": target_days, "max_selections": selection_limit, "max_observations": observation_limit, "max_seconds": wall_limit, "no_progress_seconds": idle_limit})
	_send({"kind": "start"})
	if not engine.AgentEnabled():
		_stop("agent_not_declared")
		return
	while not stopping:
		await create_timer(0.1).timeout
		if stopping: return
		var now := Time.get_ticks_msec()
		if float(now - started) / 1000.0 >= wall_limit:
			_stop("wall_budget")
			return
		var time: Dictionary = JSON.parse_string(engine.AgentTimeJson())
		var time_key := JSON.stringify(time)
		if time_key != last_time:
			last_time = time_key
			_record("player_time", time)
		if int(time.get("Day", 0)) >= target_days:
			_stop("target_day_reached")
			return
		if float(now - progressed) / 1000.0 >= idle_limit:
			_stop("no_public_progress")
			return
		if draining: continue
		if not pressed_move_keys.is_empty(): continue
		var observation: Dictionary = JSON.parse_string(engine.AgentObservationJson())
		if bool(observation.get("Complete", false)):
			_stop("story_complete")
			return
		if panel != "":
			var boundary_stop: String = engine.AgentObservePanel(panel, replay_run_label)
			if boundary_stop != "":
				_stop(boundary_stop)
				return
			observation = JSON.parse_string(engine.AgentObservationJson())
			_observe(observation)
		elif is_instance_valid(field) and not modal and field.field_active() and field.player_controlled() and field.click_path.is_empty() and not field.player.travelling:
			var boundary_stop: String = engine.AgentObserveField(_field_offers())
			if boundary_stop != "":
				_stop(boundary_stop)
				return
			observation = JSON.parse_string(engine.AgentObservationJson())
			_observe(observation)
		if observation.get("Candidates", []).is_empty():
			# Pending stage walks and sleeps must receive real physics acknowledgements.
			# An idle controllable field with no offers is a real candidate gap.
			if not modal and is_instance_valid(field) and field.field_active() and field.player_controlled() and field.click_path.is_empty() and not field.player.travelling:
				_stop("candidate_missing")
				return
			continue
		if engine.AgentSelections() >= selection_limit:
			_stop("selection_budget")
			return
		var decision: Dictionary = JSON.parse_string(engine.AgentDecideJson())
		_record("decision", decision)
		if decision.get("Token") == null:
			_stop(str(decision.get("Reason", "agent_cannot_assess")))
			return
		_send({"kind": "select_candidate", "token": decision.Token})

func _send(input: Dictionary, from_field := false) -> void:
	if stopping: return
	var message := input.duplicate(true)
	if is_instance_valid(field):
		message.context = field.context()
		message.context.modal = ("replay" if panel == "manuscript" else panel) if panel != "" else ("agent" if modal else "")
	# The public trace excludes backstage context and presentation/source IDs.
	_record("input", {"kind": str(input.get("kind", "")), "token": input.get("token")})
	var events: Array = engine.DispatchField(message) if from_field else engine.Dispatch(message)
	queue.append_array(events)
	var observation = JSON.parse_string(engine.AgentObservationJson())
	if observation is Dictionary: _observe(observation)
	_drain()

func _observe(observation: Dictionary) -> void:
	var visible_observation := observation.duplicate(true)
	if is_instance_valid(field) and field.player != null:
		visible_observation["Position"] = [field.player.position.x, field.player.position.y]
	_record("observation", visible_observation)
	var public_state := {"lines": observation.get("Lines", []), "text": observation.get("Text", []), "complete": observation.get("Complete", false), "offers": []}
	for offer in observation.get("Candidates", []):
		public_state.offers.append({"label": offer.Label, "kind": offer.Kind, "new": offer.New})
	if is_instance_valid(field) and field.player != null:
		public_state.position = [snappedf(field.player.position.x, 8.0), snappedf(field.player.position.y, 8.0)]
	var visible := JSON.stringify(public_state).sha256_text()
	if not seen_public_states.has(visible):
		seen_public_states[visible] = true
		progressed = Time.get_ticks_msec()

func _drain() -> void:
	if draining or stopping: return
	draining = true
	while not queue.is_empty() and not stopping:
		var ev: Dictionary = queue.pop_front()
		match str(ev.get("kind", "")):
			"strategy_action", "strategy_deviation", "case_progress", "object_changed", "world_resolution":
				_record(str(ev.kind), ev)
			"view":
				panel = ""
				modal = false
				if is_instance_valid(field):
					field.begin_shutdown()
					field.queue_free()
				field = Node2D.new()
				field.set_script(FieldScript)
				root.add_child(field)
				field.ui_busy = func() -> bool: return modal
				field.field_event.connect(func(input: Dictionary): _send(input, true))
				field.setup(load(str(ev.resource)).instantiate())
				if str(ev.get("map", "")) != "": field.load_map(str(ev.map), str(ev.get("spawn", "")))
				field.apply_snapshot(engine.Snapshot())
				_send({"kind": "view_ready"})
			"state":
				snapshot = ev.snapshot
				if is_instance_valid(field): field.apply_snapshot(snapshot)
			"scene":
				await field.execute(ev)
			"stage":
				field.execute(ev)
			"replay_play":
				if str(ev.get("mode", "")) == "stage": await field.execute(ev)
				elif str(ev.get("mode", "")) == "campus":
					panel = ""
					modal = true
					var playback := CampusPanel.instantiate()
					root.add_child(playback)
					playback.mode = "replay"
					playback.show_data(ev.get("campus", snapshot.get("campus", {})))
					await playback.play(ev.get("steps", []), bool(ev.get("full_match", false)))
					playback.queue_free()
					modal = false
					_send({"kind": "presented", "presentation_id": str(ev.presentation_id)})
			"manuscript":
				panel = "manuscript"
				modal = true
				_send({"kind": "opened", "panel": "manuscript"}, true)
			"player_panel":
				panel = str(ev.panel)
				modal = panel != ""
			"travel":
				panel = ""
				modal = false
				await field.travel_to_node(ev.node)
			"player_field":
				panel = ""
				modal = false
				var input: Dictionary = ev.input
				if str(input.action) == "interact": field._approach(str(input.site))
				elif str(input.action) == "move":
					if input.has("keys"): _keyboard_move(input.keys)
					else: field._route_click(Vector2(float(input.point[0]), float(input.point[1])))
				elif str(input.action) == "wait": await create_timer(1.0).timeout
			"lines", "card", "form", "candidates", "ending":
				panel = ""
				modal = true
			"close":
				panel = ""
				modal = false
			"error":
				_record("feedback", {"message": str(ev.get("message", ""))})
				_stop("engine_error")
			"agent_stopped":
				_stop(str(ev.reason))
	# Local form edits retain their form; consumed continuations release its modal.
	if stopping:
		draining = false
		return
	var observation = JSON.parse_string(engine.AgentObservationJson())
	if panel == "" and observation is Dictionary and observation.get("Candidates", []).is_empty(): modal = false
	draining = false

func _field_offers() -> Array:
	var offers := []
	for id in field.sites:
		if not field.site_visible(id): continue
		offers.append({"label": str(field.sites[id].title), "kind": "interact", "new": field.site_is_new(id), "input": {"kind": "field_action", "action": "interact", "site": id}})
	var grid: AStarGrid2D = field.adapter.nav_grid(field.player)
	for heading in [Vector2.UP, Vector2.DOWN, Vector2.LEFT, Vector2.RIGHT, Vector2(-1, -1), Vector2(1, -1), Vector2(-1, 1), Vector2(1, 1)]:
		var destination: Vector2 = field.player.position + heading.normalized() * 64.0
		if not field.in_sight(destination) or not field.adapter.walkable(destination, field.player): continue
		if Navigation.route_on_grid(field.player.position, destination, grid).is_empty(): continue
		offers.append({"label": "%s %d,%d" % [field.adapter.room_name(destination), destination.x, destination.y], "kind": "move", "new": false, "input": {"kind": "field_action", "action": "move", "point": [destination.x, destination.y]}})
	# Keyboard walking is a public input even when its future landing room is unseen.
	# Do not use a hidden destination or bypass the field's normal collisions/control locks.
	for keys in [["W"], ["S"], ["A"], ["D"], ["W", "A"], ["W", "D"], ["S", "A"], ["S", "D"]]:
		offers.append({"label": "%s %d,%d" % ["+".join(keys), field.player.position.x, field.player.position.y], "kind": "move", "new": false, "input": {"kind": "field_action", "action": "move", "keys": keys}})
	if bool(snapshot.get("controls", {}).get("wait", false)):
		offers.append({"label": "T", "kind": "wait", "new": false, "input": {"kind": "action", "act": {"cmd": "stay"}}})
	else:
		# Literal inactivity: one real second of physics, without an authored wait command or clock injection.
		offers.append({"label": "", "kind": "wait", "new": false, "input": {"kind": "field_action", "action": "wait"}})
	for shortcut in [{"key": "Tab", "panel": "notebook"}, {"key": "R", "panel": "manuscript"}, {"key": "M", "panel": "campus"}]:
		if bool(snapshot.get("controls", {}).get(shortcut.panel, false)):
			offers.append({"label": shortcut.key, "kind": "menu", "new": false, "input": {"kind": "player_panel", "panel": shortcut.panel}})
	return offers

func _keyboard_move(keys: Array) -> void:
	keyboard_start = field.player.position
	keyboard_names = keys.duplicate()
	for key in keys:
		var code: int = MOVE_KEYS[str(key)]
		var event := InputEventKey.new()
		event.keycode = code
		event.physical_keycode = code
		event.pressed = true
		pressed_move_keys.append(code)
		Input.parse_input_event(event)
	_record("keyboard_input", {"keys": keys, "pressed": true})
	keyboard_release_at = Time.get_ticks_msec() + 350
	keyboard_timer = create_timer(0.35)
	keyboard_timer.timeout.connect(_finish_keyboard_move, CONNECT_ONE_SHOT)

func _finish_keyboard_move() -> void:
	keyboard_timer = null
	if stopping: return
	var remaining := keyboard_release_at - Time.get_ticks_msec()
	if remaining > 0:
		# Scene timers consume frame delta; a busy prior frame must not shorten a new input.
		keyboard_timer = create_timer(float(remaining) / 1000.0)
		keyboard_timer.timeout.connect(_finish_keyboard_move, CONNECT_ONE_SHOT)
		return
	_release_move_keys()
	_record("keyboard_move", {"keys": keyboard_names, "start_position": [keyboard_start.x, keyboard_start.y], "end_position": [field.player.position.x, field.player.position.y]})

func _release_move_keys() -> void:
	if pressed_move_keys.is_empty(): return
	var keys: Array[String] = []
	for code in pressed_move_keys:
		keys.append(str(MOVE_KEYS.find_key(code)))
		var event := InputEventKey.new()
		event.keycode = code
		event.physical_keycode = code
		event.pressed = false
		Input.parse_input_event(event)
	pressed_move_keys.clear()
	_record("keyboard_input", {"keys": keys, "pressed": false})

func _record(kind: String, value) -> void:
	sequence += 1
	output_file.store_line(JSON.stringify({"sequence": sequence, "elapsed_ms": Time.get_ticks_msec() - started, "kind": kind, "value": value}))
	output_file.flush()

func _stop(reason: String) -> void:
	if stopping: return
	stopping = true
	if keyboard_timer != null:
		if keyboard_timer.timeout.is_connected(_finish_keyboard_move): keyboard_timer.timeout.disconnect(_finish_keyboard_move)
		keyboard_timer = null
	_release_move_keys()
	var summary := {"seed": seed, "target_days": target_days, "time": JSON.parse_string(engine.AgentTimeJson()) if engine else null, "reason": reason, "selections": engine.AgentSelections() if engine else 0, "elapsed_seconds": float(Time.get_ticks_msec() - started) / 1000.0}
	if is_instance_valid(field) and field.player != null:
		summary["visible_position"] = [field.player.position.x, field.player.position.y]
	_record("stop", summary)
	print(JSON.stringify(summary))
	if is_instance_valid(field): field.begin_shutdown()
	if engine:
		engine.CloseAgent()
		engine.CloseTrace()
	output_file.close()
	_finish_shutdown.call_deferred(1 if reason == "engine_error" else 0)

func _finish_shutdown(code: int) -> void:
	# An inactivity selection may still be awaiting its real one-second timer.
	# Let the suspended drain return before freeing its field and engine references.
	while draining:
		await process_frame
	await create_timer(0.15).timeout
	queue.clear()
	if is_instance_valid(field): field.queue_free()
	if is_instance_valid(engine): engine.queue_free()
	await process_frame
	await process_frame
	field = null
	engine = null
	quit(code)
