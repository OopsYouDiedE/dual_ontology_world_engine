extends Node
signal shutdown_completed
## 单一窗口：把输入交给 C#，把输出交给视图或 GUI，不持有剧情与章节规则。

const EngineScript := preload("res://scenes/Host/GameEngine.cs")
const UI_SCENE := preload("res://scenes/UI/UISystem.tscn")
const TITLE_SCENE := preload("res://scenes/UI/TitleUI.tscn")
const FIELD_SCRIPT := preload("res://scenes/Shared/Field/WorldView.gd")
const FIELD_EXECUTOR := DvFieldExecutorInner
const VIEW_TRACE := PlayerViewTraceInner
const DIAGNOSTICS := RuntimeDiagnosticsInner
const CAMPUS_MAP_VIEW := preload("res://scenes/Chapter2/Field/CampusMapView.tscn")

@export_enum("player", "ai") var controller := "player"
@export var campaign_options: Dictionary = {}
@export var viewer := ""
@export_range(0.25, 8.0, 0.25) var playback_speed := 1.0
var field_executor: Node
var _started := false
var view_trace: Node
var engine: Node
var ui: Node
var field: Node2D
var title_layer: CanvasLayer
var _queue: Array = []
var _draining := false
var _closing := false
var _dv_intent: Dictionary = {}
var _dv_serial := 0
var _view_resource := ""
var _interaction_id := ""
var _interaction_site := ""
var _reading := false
var _background_execution := false
var _pending_adapter: Node2D

func _timing(stage: String, details: Dictionary = {}) -> void:
	engine.RecordTiming({"stage": stage, "interaction_id": _interaction_id, "details": details})

func _ready() -> void:
	get_tree().auto_accept_quit = false
	add_to_group("lifecycle_hosts")
	TranslationServer.set_locale("zh_CN")
	get_window().title = "安眠者有福"
	engine = EngineScript.new()
	engine.name = "Engine"
	add_child(engine)
	engine.EventsReady.connect(_dv_events)
	engine.TraceClosed.connect(func(): shutdown_completed.emit())
	view_trace = VIEW_TRACE.new()
	view_trace.name = "PlayerViewTrace"
	add_child(view_trace)
	view_trace.configure(self, engine.RecordVisible, engine.RecordInput)
	var diagnostics := DIAGNOSTICS.new()
	add_child(diagnostics)
	diagnostics.configure(self)
	field_executor = FIELD_EXECUTOR.new()
	field_executor.name = "DvFieldExecutor"
	add_child(field_executor)
	_home()
	if "--dv-auto-start" in OS.get_cmdline_user_args(): dispatch.call_deferred({"kind": "start"})

func _dispose_views() -> void:
	if is_instance_valid(field):
		if is_instance_valid(field_executor): field_executor.retain_field(field)
		field.begin_shutdown()
		remove_child(field)
		field.queue_free()
	field = null
	if is_instance_valid(ui):
		ui.begin_shutdown()
		remove_child(ui)
		ui.queue_free()
	ui = null
	if is_instance_valid(title_layer):
		remove_child(title_layer)
		title_layer.queue_free()
	title_layer = null

func _home() -> void:
	_dispose_views()
	if is_instance_valid(field_executor): field_executor.reset()
	title_layer = CanvasLayer.new()
	title_layer.layer = 30
	add_child(title_layer)
	var title: Control = TITLE_SCENE.instantiate()
	title.mode = "title"
	title_layer.add_child(title)
	title.ui_event.connect(dispatch)

func _view(ev: Dictionary, begin := true) -> void:
	_timing("view_build_begin")
	var resource: PackedScene = CAMPUS_MAP_VIEW if str(ev.resource) == "res://scenes/Chapter2/Field/CampusMapView.tscn" else (load(str(ev.resource)) as PackedScene)
	if resource == null:
		push_error("无法加载世界视图：" + str(ev.resource))
		return
	# Production view events already carry the worker's completed data graph.
	# Keep Snapshot() only for callers using the earlier host interface.
	var initial: Dictionary = ev.get("snapshot", {})
	if initial.is_empty(): initial = ev if ev.has("viewer") else engine.Snapshot()
	var adapter: Node2D = resource.instantiate()
	_pending_adapter = adapter
	if adapter.get("presentation") != null and initial.has("presentation"):
		adapter.presentation = initial.presentation
	_timing("view_prepare_begin", {"map": initial.get("map", "")})
	var prepared := true
	if adapter.has_method("prepare_map"):
		prepared = await adapter.prepare_map(str(initial.get("map", "")), initial, get_tree())
	_timing("view_prepare_end", {"ready": prepared})
	if not prepared or _closing:
		if adapter.has_method("begin_shutdown"): adapter.begin_shutdown()
		adapter.free()
		_pending_adapter = null
		if not _closing: _show_error("无法准备世界地图：" + str(initial.get("map", "")))
		return
	# Resource preparation leaves the current screen drawn and responsive.
	# Retire it only after its own exit transition has completed.
	if is_instance_valid(title_layer):
		var title := title_layer.get_child(0)
		if title.has_method("transition_out"): await title.transition_out()
	elif is_instance_valid(ui) and ui.has_method("transition_out"):
		await ui.transition_out()
	if _closing:
		if adapter.has_method("begin_shutdown"): adapter.begin_shutdown()
		adapter.free()
		_pending_adapter = null
		return
	_dispose_views()
	_view_resource = str(ev.resource)
	ui = UI_SCENE.instantiate()
	ui.name = "UI"
	add_child(ui)
	ui.ui_event.connect(dispatch)
	field = Node2D.new()
	field.set_script(FIELD_SCRIPT)
	field.name = "WorldView"
	field.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	add_child(field)
	field.ui_busy = ui.is_busy
	field.fader = ui
	field.field_event.connect(_field_input)
	field.hover_changed.connect(ui.show_hover)
	field.hint_changed.connect(ui.set_hint)
	field_executor.configure_visible(field, initial)
	_pending_adapter = null
	field.setup(adapter)
	field_executor.bind_visible(field, initial)
	ui.apply_snapshot(initial)
	_timing("view_build_end")
	if ui.has_method("transition_in"): await ui.transition_in()
	# 等场景加入树并应用快照后才开始脚本；这也是可见视图的普通握手。
	if begin and not _closing: dispatch.call_deferred({"kind": "view_ready"})

## 热点与跨房间进入DV；房间内坐标接近由现场消费，不进入此入口。
func dispatch(input: Dictionary) -> void:
	if _closing: return
	if str(input.get("kind", "")) == "spatial_select":
		if is_instance_valid(field) and is_instance_valid(field.spatial): field.spatial.select(str(input.choice_id), str(input.site))
		return
	if str(input.get("kind", "")) == "quit":
		engine.RecordInput({"device": "ui_semantic", "kind": "quit"})
		begin_shutdown.call_deferred()
		await shutdown_completed
		get_tree().quit()
		return
	if str(input.get("kind", "")) == "start":
		if _started: return
		var options := _session_options()
		if options.is_empty(): return
		engine.ConfigureSession(options)
		_started = true
	engine.Dispatch(input)

func _session_options() -> Dictionary:
	var speed := playback_speed
	var options := {"controller": controller, "campaign": campaign_options.duplicate(true), "audit": false}
	if viewer != "": options.viewer = viewer
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--dv-speed="):
			var value := arg.trim_prefix("--dv-speed=")
			if not value.is_valid_float():
				_show_error("Invalid --dv-speed: " + value)
				return {}
			speed = float(value)
		elif arg == "--dv-audit": options.audit = true
		elif arg.begins_with("--dv-controller="): options.controller = arg.trim_prefix("--dv-controller=")
		elif arg.begins_with("--dv-viewer="): options.viewer = arg.trim_prefix("--dv-viewer=")
		elif arg.begins_with("--dv-profile="): options.campaign.profile = arg.trim_prefix("--dv-profile=")
		elif arg.begins_with("--dv-zone="): options.campaign.detective_zone = arg.trim_prefix("--dv-zone=")
		elif arg.begins_with("--dv-seed="):
			var value := arg.trim_prefix("--dv-seed=")
			if not value.is_valid_int():
				_show_error("Invalid --dv-seed: " + value)
				return {}
			options.campaign.seed = int(value)
	if str(options.controller) not in ["player", "ai"]:
		_show_error("Invalid --dv-controller: " + str(options.controller))
		return {}
	if not is_finite(speed) or speed < 0.25 or speed > 8.0:
		_show_error("Playback speed must be between 0.25 and 8")
		return {}
	Engine.time_scale = speed
	Engine.physics_ticks_per_second = ceili(60.0 * maxf(1.0, speed))
	Engine.max_physics_steps_per_frame = ceili(8.0 * maxf(1.0, speed))
	engine.RecordInput({"device": "host_configuration", "controller": str(options.controller), "playback_speed": speed, "physics_ticks_per_second": Engine.physics_ticks_per_second, "max_physics_steps_per_frame": Engine.max_physics_steps_per_frame})
	return options

func _show_error(message: String) -> void:
	push_error(message)
	var dialog := AcceptDialog.new()
	dialog.title = "会话错误"
	dialog.dialog_text = message
	add_child(dialog)
	dialog.confirmed.connect(dialog.queue_free)
	dialog.popup_centered(Vector2i(640, 240))

func _field_input(input: Dictionary) -> void:
	match str(input.get("kind", "")):
		"timing": engine.RecordTiming(input)
		"select":
			_interaction_id = str(input.interaction_id)
			_interaction_site = str(input.site)
			dispatch(input)
		"spatial_choices":
			_interaction_id = str(input.interaction_id)
			_timing("local_choices_ready")
			ui.spatial_choices(input)

func _accept_execution(input: Dictionary) -> void:
	if _closing or _dv_intent.is_empty(): return
	var action: Dictionary = _dv_intent.action
	var destination: Dictionary = _dv_intent.get("destination", {})
	var arrived := str(input.get("kind", "")) == "daily" and str(input.get("status", "")) == "arrived"
	arrived = arrived and int(input.get("serial", -1)) == _dv_serial
	arrived = arrived and str(input.get("who", "")) == str(_dv_intent.actor) and str(input.get("action", "")) == str(action.id)
	arrived = arrived and str(input.get("room", "")) == str(destination.get("execution_room", destination.get("room", "")))
	var travel: Dictionary = action.get("travel", {})
	if travel.has("station"):
		arrived = arrived and str(input.get("station", "")) == str(travel.station)
		arrived = arrived and input.has("arrival_error") and float(input.get("arrival_error", 5.0)) <= 4.0
	_dv_complete("applied" if arrived else "rejected", input)

func _drain() -> void:
	if _draining or _closing: return
	_draining = true
	while not _queue.is_empty() and not _closing:
		var ev: Dictionary = _queue.pop_front()
		# A completed reading can expose an older queued observer snapshot while
		# an off-screen actor still owns an executing body. Let that real movement
		# finish before snapshot binding can acquire/reparent the same body.
		if str(ev.get("kind", "")) in ["state", "view"]:
			while _background_execution and not _closing:
				await get_tree().process_frame
			if _closing: break
		match str(ev.get("kind", "")):
			"dv_action": await _dv_execute(ev.intent, ev.get("execution_snapshot", {}))
			"dv_present":
				var result: Dictionary = ev.presentation
				var lines := _presentation_lines(result)
				if not lines.is_empty():
					_reading = true
					# Start the ordinary serialized Prepare while this frozen text is read.
					# Remote NPCs still execute real Godot movement and return Complete.
					ev.advance_requested = true
					dispatch.call_deferred({"kind": "dv_next", "schedule": "reading-background"})
					_timing("presentation_begin", {"execution_event": result.get("execution_event", "")})
					await ui.present({"kind": "lines", "lines": lines, "trace_id": str(result.get("execution_event", "")), "presentation_id": str(result.get("execution_event", ""))})
					_reading = false
					_timing("reading_finished", {"execution_event": result.get("execution_event", "")})
				if not bool(ev.get("advance_requested", false)):
					dispatch.call_deferred({"kind": "dv_next"})
			"view": await _view(ev)
			"state": await _apply(ev.snapshot)
			"error": _show_error(str(ev.message))
			_: push_error("Unknown DV host event: " + str(ev.get("kind", "")))
	_draining = false

## IDs name the actual execution and emitted field; source_refs retain authored provenance.
## Emitted line ordinals are not claimed to be original declaration line numbers.
func _presentation_line(result: Dictionary, text: String, field_name: String, index: int, paragraph: int) -> Dictionary:
	var event := str(result.get("execution_event", ""))
	return {"text": text, "speaker": str(result.get("actor", "")), "speaker_name": str(result.get("speaker_name", "")),
		"trace_id": event, "execution_event": event, "source_id": str(result.get("action_id", "")),
		"line_id": "%s:%s:%d:%d" % [event, field_name, index, paragraph], "source_field": field_name,
		"emitted_line_index": index, "paragraph_index": paragraph, "source_refs": result.get("source_refs", [])}

func _presentation_lines(result: Dictionary) -> Array:
	var lines: Array = []
	if bool(result.get("visible", false)):
		if str(result.get("surface", "")) != "": lines.append(_presentation_line(result, str(result.surface), "surface", 0, 0))
		var index := 0
		for text in result.get("lines", []):
			var paragraph := 0
			for part in str(text).split("\n", false):
				lines.append(_presentation_line(result, part, "presentation.lines", index, paragraph))
				paragraph += 1
			index += 1
	var judgment = result.get("judgment", false)
	if judgment is Dictionary:
		var line := _presentation_line(result, "本次答辩已获接受。" if str(judgment.status) == "answer_accepted" else "本次答辩未获接受。", "judgment.status", 0, 0)
		line.speaker = ""
		line.judgment_event = str(judgment.get("event_id", ""))
		lines.append(line)
	return lines

func _apply(snapshot: Dictionary) -> void:
	_timing("ui_apply_begin")
	if _view_resource != "" and str(snapshot.resource) != _view_resource:
		await _view(snapshot, false)
		_timing("ui_apply_end")
		return
	if is_instance_valid(ui): ui.apply_snapshot(snapshot)
	if is_instance_valid(field): field.apply_snapshot(snapshot)
	_timing("ui_apply_end")

func begin_shutdown() -> void:
	if _closing: return
	_closing = true
	_reading = false
	if is_instance_valid(view_trace): view_trace.begin_shutdown()
	_queue.clear()
	if is_instance_valid(field_executor): field_executor.begin_shutdown()
	if is_instance_valid(_pending_adapter) and _pending_adapter.has_method("begin_shutdown"):
		_pending_adapter.begin_shutdown()
	if is_instance_valid(field): field.begin_shutdown()
	if is_instance_valid(ui): ui.begin_shutdown()
	if is_instance_valid(engine): engine.CloseTrace()

func _exit_tree() -> void:
	begin_shutdown()

func _dv_events(events: Array) -> void:
	if _closing: return
	for ev in events:
		if _reading and str(ev.get("kind", "")) == "dv_action" and _remote_npc_intent(ev):
			_execute_reading_npc(ev)
		elif _reading and str(ev.get("kind", "")) == "dv_present" and _presentation_lines(ev.presentation).is_empty():
			# Empty presentation has no reading work. Remember that its next cue was
			# already sent so the foreground drain cannot prepare the actor twice.
			ev.advance_requested = true
			_queue.append(ev)
			dispatch.call_deferred({"kind": "dv_next", "schedule": "reading-background"})
		else:
			_queue.append(ev)
	_drain.call_deferred()

func _remote_npc_intent(ev: Dictionary) -> bool:
	if _background_execution or not is_instance_valid(field): return false
	var intent: Dictionary = ev.get("intent", {})
	if str(intent.get("status", "")) != "prepared": return false
	var viewer := str(field.snapshot.get("viewer", ""))
	if str(intent.get("actor", "")) == viewer: return false
	var current_room := str(field.snapshot.get("execution_room", ""))
	var source: Dictionary = ev.get("execution_snapshot", {})
	var source_room := str(source.get("execution_room", ""))
	if source_room == "" or source_room == current_room: return false
	var destination: Dictionary = intent.get("destination", {})
	var destination_room := str(destination.get("execution_room", destination.get("room", "")))
	return destination_room != current_room

func _execute_reading_npc(ev: Dictionary) -> void:
	_background_execution = true
	_timing("reading_npc_execution_begin", {"actor": ev.intent.actor, "action_event": ev.intent.action_event})
	await _dv_execute(ev.intent, ev.get("execution_snapshot", {}))
	_timing("reading_npc_execution_end", {"actor": ev.intent.actor, "action_event": ev.intent.action_event})
	_background_execution = false

func _dv_execute(intent: Dictionary, execution_snapshot: Dictionary) -> void:
	if str(intent.get("status", "")) == "awaiting_input":
		field.spatial.set_options(intent.options)
		_timing("player_options_ready", {"options": intent.options.size()})
		return
	if str(intent.get("status", "")) != "prepared": return
	_dv_intent = intent
	_dv_serial += 1
	var action: Dictionary = intent.action
	_timing("execution_begin", {"action_event": intent.action_event, "actor": intent.actor})
	if str(action.kind) == "move" or action.get("travel", {}).has("station"):
		var receipt: Dictionary = await field_executor.execute(intent, field, execution_snapshot, _dv_serial)
		_accept_execution(receipt)
	else:
		var contact := true
		if _interaction_site != "" and _interaction_site != "self" and str(intent.actor) == str(field.snapshot.get("viewer", "")):
			contact = field.spatial._in_reach(_interaction_site)
		_dv_complete("applied" if contact else "rejected", {"executor": "dv-world", "site": _interaction_site, "contact": contact, "context": field.context()})

func _dv_complete(outcome: String, execution: Dictionary) -> void:
	_timing("execution_end", {"outcome": outcome, "action_event": _dv_intent.action_event})
	var receipt := {"action_event": _dv_intent.action_event, "outcome": outcome, "actual": _dv_intent.execution_cost, "executor": "godot", "field": execution}
	_dv_intent = {}
	_interaction_site = ""
	dispatch.call_deferred({"kind": "dv_complete", "receipt": receipt})

func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_CLOSE_REQUEST and not _closing:
		begin_shutdown.call_deferred()
		await shutdown_completed
		get_tree().quit()

# ==============================================================================
# 宿主内聚执行支持类 (Host Subsystems Inner Classes)
# ==============================================================================

class RuntimeDiagnosticsInner extends Node:
	var host: Node
	var previous_draw := 0
	var locations: Dictionary = {}
	var first_visible: Dictionary = {}
	var process_begin := 0
	var render_begin := 0
	var last_input_state: Dictionary = {}
	const FRAME_GAP_US := 50000

	func configure(value: Node) -> void:
		host = value
		process_priority = -10000
		RenderingServer.frame_pre_draw.connect(_before_render)
		RenderingServer.frame_post_draw.connect(_drawn)

	func _process(_delta: float) -> void:
		if is_instance_valid(host): process_begin = host.engine.TimingClockUs()

	func _before_render() -> void:
		if is_instance_valid(host): render_begin = host.engine.TimingClockUs()

	func _drawn() -> void:
		if not is_instance_valid(host) or host._closing: return
		var now: int = host.engine.TimingClockUs()
		if previous_draw > 0 and now - previous_draw >= FRAME_GAP_US:
			host._timing("frame_gap", {"begin_us": previous_draw, "end_us": now, "duration_us": now - previous_draw, "threshold_us": FRAME_GAP_US, "process_begin_us": process_begin, "render_begin_us": render_begin})
		previous_draw = now
		if not is_instance_valid(host.field): return
		var field: Node2D = host.field
		if not is_instance_valid(field.adapter): return
		var spatial: Node = field.spatial
		var input_state := {"map": str(field.adapter.map_key()), "room": str(field.snapshot.get("execution_room", "")),
			"ui_busy": is_instance_valid(host.ui) and host.ui.is_busy(), "reading": host._reading,
			"background_execution": host._background_execution, "draining": host._draining,
			"queued_events": host._queue.size(), "map_switching": field.adapter.switching(),
			"submitted": is_instance_valid(spatial) and spatial.submitted,
			"options": spatial.options.size() if is_instance_valid(spatial) else 0,
			"input_enabled": spatial.enabled() if is_instance_valid(spatial) else false,
			"intent_actor": str(host._dv_intent.get("actor", "")), "intent_action": str(host._dv_intent.get("action", {}).get("id", "")),
			"physical_jobs": field._daily.keys()}
		if input_state != last_input_state:
			last_input_state = input_state.duplicate(true)
			host._timing("input_gate_state", input_state)
		var viewport_rect := field.get_viewport_rect()
		for key in field.actors:
			var actor = field.actors[key]
			if not is_instance_valid(actor): continue
			var room := str(field.adapter.entered_room(actor.position)) if field.adapter.has_method("entered_room") else ""
			var location := str(field.adapter.map_key()) + "/" + room
			if room != "" and locations.get(key, "") != location:
				locations[key] = location
				host._timing("physical_location", {"actor": key, "location": location, "position": [actor.position.x, actor.position.y], "initial": not first_visible.has("located:" + str(key))})
				first_visible["located:" + str(key)] = true
			var screen: Vector2 = actor.get_global_transform_with_canvas().origin
			if actor.is_visible_in_tree() and viewport_rect.has_point(screen):
				var visible_key := str(key) + "@" + location
				if not first_visible.has(visible_key):
					first_visible[visible_key] = true
					host._timing("actor_in_viewport", {"actor": key, "location": location, "screen": [screen.x, screen.y], "scope": "visible node and viewport bounds; occlusion/recognition require PNG review"})

class PlayerViewTraceInner extends Node:
	var host: Node
	var record_visible: Callable
	var record_input: Callable
	var _last_frame := ""
	var _last_data: Dictionary = {}
	var _wired: Dictionary = {}
	var _control_context: Dictionary = {}
	var _closing := false
	var _timed_presentations: Dictionary = {}

	func configure(owner_node: Node, visible_sink: Callable, input_sink: Callable) -> void:
		host = owner_node
		record_visible = visible_sink
		record_input = input_sink
		RenderingServer.frame_post_draw.connect(_after_draw)

	func begin_shutdown() -> void:
		if _closing: return
		_closing = true
		if RenderingServer.frame_post_draw.is_connected(_after_draw):
			RenderingServer.frame_post_draw.disconnect(_after_draw)

	func _exit_tree() -> void:
		begin_shutdown()

	func _input(event: InputEvent) -> void:
		if _closing or not record_input.is_valid(): return
		var data: Dictionary = {}
		if event is InputEventKey and not event.echo:
			data = {"device": "keyboard", "key": OS.get_keycode_string(event.keycode), "pressed": event.pressed,
				"unicode": event.unicode, "shift": event.shift_pressed, "ctrl": event.ctrl_pressed}
		elif event is InputEventMouseButton:
			data = {"device": "mouse", "button": event.button_index, "pressed": event.pressed,
				"position": [event.position.x, event.position.y], "double_click": event.double_click}
		if not data.is_empty():
			data.merge(_context())
			record_input.call(data)

	func _context() -> Dictionary:
		if is_instance_valid(host.ui): return host.ui.view_trace_context()
		return {"panel": "title"}

	func _activation(control: Control, path: String) -> void:
		if _closing or not is_instance_valid(control): return
		var data: Dictionary = _control_context.get(control.get_instance_id(), {}).duplicate()
		data.merge({"device": "control_signal", "path": path,
			"text": _plain_text(control), "pressed": true})
		record_input.call(data)

	func _wire(control: Control) -> void:
		var id := control.get_instance_id()
		_control_context[id] = _context()
		if _wired.has(id): return
		_wired[id] = true
		var path := str(host.get_path_to(control))
		if control is BaseButton: control.pressed.connect(_activation.bind(control, path))
		elif control is LineEdit:
			control.text_changed.connect(func(value: String) -> void:
				if _closing: return
				var data := _context()
				data.merge({"device": "text_control", "path": path, "value": value})
				record_input.call(data))

	func _after_draw() -> void:
		if _closing or not is_instance_valid(host): return
		host._timing("visible_trace_begin")
		_collect_after_draw()
		host._timing("visible_trace_end")

	func _collect_after_draw() -> void:
		var items: Array = []
		var viewport_rect: Rect2 = host.get_viewport().get_visible_rect()
		if is_instance_valid(host.title_layer):
			_scan(host.title_layer, viewport_rect, items)
		elif is_instance_valid(host.ui):
			_scan(host.ui, viewport_rect, items)
		var data := _context()
		data["event"] = "visible_frame"
		data["view_schema"] = 2
		data["texts"] = items
		data["objects"] = _scene_objects()
		if data == _last_data: return
		_last_data = data.duplicate(true)
		var signature := JSON.stringify(data)
		if signature == _last_frame: return
		_last_frame = signature
		data["frame"] = Engine.get_frames_drawn()
		record_visible.call(data)
		var presentation := str(data.get("presentation_id", ""))
		if presentation != "" and str(data.get("panel", "")) in ["dialogue", "spatial"] and not _timed_presentations.has(presentation):
			_timed_presentations[presentation] = true
			host._timing("local_choices_frame" if str(data.get("panel", "")) == "spatial" else "first_frame_drawn", {"presentation_id": presentation, "frame": data.frame})

	func _scene_objects() -> Array:
		if not is_instance_valid(host.field): return []
		var covered: Array[Rect2] = []
		if is_instance_valid(host.ui):
			for child in host.ui.get_children():
				if child is CanvasLayer and child.visible:
					for sub in child.get_children():
						if sub is Control and sub.is_visible_in_tree() and sub.modulate.a > 0.001:
							covered.append(sub.get_global_rect())
		return host.field.player_visible_objects(covered)

	func _scan(node: Node, clip: Rect2, items: Array, covered: Array[Rect2] = []) -> void:
		if node is CanvasLayer and not node.visible: return
		if node is CanvasItem and (not node.is_visible_in_tree() or node.modulate.a <= 0.001): return
		var child_clip := clip
		if node is Control:
			var control: Control = node
			var bounds := control.get_global_rect()
			var visible_bounds := bounds.intersection(clip)
			if not visible_bounds.has_area(): return
			_wire(control)
			var text := _plain_text(control)
			if text != "":
				var item := {"text": text, "content_clipped": false}
				if control.has_meta("player_source"): item.merge(control.get_meta("player_source"))
				if control is RichTextLabel:
					item["visible_characters"] = control.visible_characters
					item["complete"] = control.visible_characters < 0 or control.visible_characters >= control.get_total_character_count()
				items.append(item)
			if control.clip_contents or control is ScrollContainer: child_clip = visible_bounds
		for child in node.get_children(): _scan(child, child_clip, items, covered)

	func _plain_text(control: Control) -> String:
		if control is RichTextLabel: return control.get_parsed_text()
		if control is Label or control is Button: return control.atr(control.text)
		if control is LineEdit or control is TextEdit:
			return control.text if control.text != "" else control.atr(control.placeholder_text)
		return ""

class DvFieldExecutorInner extends Node:
	const WORLD_VIEW := preload("res://scenes/Shared/Field/WorldView.gd")
	var _bodies: Dictionary = {}
	var _worlds: Dictionary = {}
	var _occupancy: Array[Dictionary] = []
	var _parking: Node2D
	var _preparing_adapters: Array[Node2D] = []
	var _closing := false
	var _busy := false
	var _viewer := ""

	func configure_visible(field: Node2D, initial: Dictionary) -> void:
		if not field.execution_only: _viewer = str(initial.get("presentation", {}).get("player_actor", ""))
		field.execution_bridge = self
		field.execution_resource = str(initial.get("resource", ""))
		field.execution_binding = initial
		field.snapshot = initial

	func bind_visible(field: Node2D, initial: Dictionary) -> void:
		field.apply_snapshot(initial)
		note_map(field)

	func _ensure_parking() -> void:
		if is_instance_valid(_parking): return
		var viewport := _new_viewport("RetainedBodies")
		_parking = Node2D.new()
		_parking.process_mode = Node.PROCESS_MODE_DISABLED
		_parking.hide()
		viewport.add_child(_parking)

	func _new_viewport(label: String) -> SubViewport:
		var viewport := SubViewport.new()
		viewport.name = label
		viewport.size = Vector2i(32, 32)
		viewport.disable_3d = true
		viewport.world_2d = World2D.new()
		viewport.render_target_update_mode = SubViewport.UPDATE_DISABLED
		add_child(viewport)
		return viewport

	func _actor_binding(who: String, state: Dictionary) -> Dictionary:
		var result: Dictionary = {}
		for person in state.get("people", []):
			if str(person.get("key", "")) == who:
				result = person.duplicate(true)
				break
		if not result.has("anchor") or str(result.get("anchor", "")) == "":
			result["anchor"] = str(result.get("initial_anchor", ""))
		var presentation: Dictionary = state.get("presentation", {})
		if str(presentation.get("player_actor", "")) == who:
			var init_a := str(state.get("initial_anchor", ""))
			var a := str(state.get("anchor", init_a))
			result.merge({"map": str(state.get("map", "")), "execution_room": str(state.get("execution_room", "")),
				"spawn": str(state.get("spawn", "")), "anchor": a,
				"sprite": str(presentation.get("player_sprite", ""))}, true)
		return result

	func _initial_point(who: String, field: Node2D, binding: Dictionary):
		var room := str(binding.get("execution_room", ""))
		if room == "" or str(binding.get("map", "")) != str(field.adapter.map_key()): return null
		var declared_anchor := str(binding.get("anchor", ""))
		if declared_anchor != "":
			var point = field.adapter.anchor(declared_anchor)
			if point == null or field.adapter.entered_room(point) != room or not field.adapter.walkable(point): return null
			return null if _initial_occupied(who, field, point) else point
		var candidates: Array = []
		for placement in field.execution_binding.get("initial_placements", []):
			if str(placement.get("actor", "")) == who:
				var point = field.adapter.anchor(str(placement.get("anchor", "")))
				if point != null: candidates.append(point)
		candidates.append_array(field.adapter.stands())
		var spawn := str(binding.get("spawn", ""))
		if spawn != "":
			var point = field.adapter.anchor(spawn)
			if point != null: candidates.append(point)
		var layout: Node = field.adapter.get("map")
		var spawns: Node = layout.get_node_or_null("Spawns") if is_instance_valid(layout) else null
		if spawns != null:
			for marker in spawns.get_children():
				if marker is Marker2D: candidates.append(marker.position)
		for candidate in candidates:
			var point: Vector2 = candidate
			if field.adapter.entered_room(point) != room or not field.adapter.walkable(point): continue
			if not _initial_occupied(who, field, point): return point
		return null

	func _initial_occupied(who: String, field: Node2D, point: Vector2) -> bool:
		for id in _bodies:
			var record: Dictionary = _bodies[id]
			var body: CharacterBody2D = record.body
			if id != who and is_instance_valid(body) and str(record.resource) == field.execution_resource and str(record.map) == field.adapter.map_key() and body.position.distance_to(point) < 24.0:
				return true
		return false

	func occupied_positions(walker: CharacterBody2D, resource: String, map_key: String) -> Array[Vector2]:
		var result: Array[Vector2] = []
		for record: Dictionary in _bodies.values():
			var body: CharacterBody2D = record.body
			if is_instance_valid(body) and body != walker and str(record.resource) == resource and str(record.map) == map_key:
				result.append(body.position)
		return result

	func occupied_at(walker: CharacterBody2D, resource: String, map_key: String, point: Vector2) -> bool:
		# Station searches ask this for many candidates. Do not allocate a full
		# positions array for every point; inspect the same retained bodies.
		for record: Dictionary in _bodies.values():
			var body: CharacterBody2D = record.body
			if not is_instance_valid(body) or body == walker or str(record.resource) != resource or str(record.map) != map_key: continue
			var separation := (body.position - point).abs()
			if separation.x < 19.0 and separation.y < 15.0: return true
		return false

	func sync_obstacles(field: Node2D) -> void:
		if _closing or not is_instance_valid(field) or not is_instance_valid(field.adapter): return
		var root: Node2D = field.adapter.objects_root()
		var retained: Dictionary = {}
		for i in range(_occupancy.size() - 1, -1, -1):
			var item: Dictionary = _occupancy[i]
			if item.field != field: continue
			var obstacle: StaticBody2D = item.obstacle
			var record: Dictionary = _bodies.get(item.who, {})
			var body: CharacterBody2D = item.body
			if not is_instance_valid(obstacle) or obstacle.get_parent() != root or record.is_empty() or not is_instance_valid(body) or record.body != body or body.get_parent() == root or str(record.resource) != field.execution_resource or str(record.map) != field.adapter.map_key():
				_remove_occupancy(i)
				continue
			retained[item.who] = item
		for who in _bodies:
			var record: Dictionary = _bodies[who]
			var body: CharacterBody2D = record.body
			if not is_instance_valid(body) or body.get_parent() == root: continue
			if str(record.resource) != field.execution_resource or str(record.map) != field.adapter.map_key(): continue
			if retained.has(who):
				_update_occupancy(retained[who])
				continue
			var obstacle := StaticBody2D.new()
			obstacle.set_meta("retained_occupancy", body.get_instance_id())
			obstacle.collision_layer = body.collision_layer
			obstacle.collision_mask = 0
			obstacle.transform = body.transform
			var shapes: Array[Dictionary] = []
			for child in body.get_children():
				if child is CollisionShape2D:
					var shape := CollisionShape2D.new()
					shape.shape = child.shape
					shape.transform = child.transform
					shape.disabled = child.disabled
					obstacle.add_child(shape)
					shapes.append({"source": child, "proxy": shape})
			root.add_child(obstacle)
			_occupancy.append({"obstacle": obstacle, "body": body, "field": field, "who": who, "shapes": shapes})

	func _remove_occupancy(index: int) -> void:
		var obstacle: StaticBody2D = _occupancy[index].obstacle
		if is_instance_valid(obstacle):
			obstacle.collision_layer = 0
			obstacle.queue_free()
		_occupancy.remove_at(index)

	func _update_occupancy(item: Dictionary) -> void:
		var obstacle: StaticBody2D = item.obstacle
		var body: CharacterBody2D = item.body
		obstacle.transform = body.transform
		obstacle.collision_layer = body.collision_layer
		for pair: Dictionary in item.shapes:
			var source: CollisionShape2D = pair.source
			var proxy: CollisionShape2D = pair.proxy
			if not is_instance_valid(proxy): continue
			if not is_instance_valid(source):
				proxy.disabled = true
				continue
			if proxy.shape != source.shape: proxy.shape = source.shape
			if proxy.transform != source.transform: proxy.transform = source.transform
			if proxy.disabled != source.disabled: proxy.disabled = source.disabled

	func _physics_process(_delta: float) -> void:
		if _closing: return
		for i in range(_occupancy.size() - 1, -1, -1):
			var item: Dictionary = _occupancy[i]
			var obstacle = item.obstacle
			if not is_instance_valid(obstacle) or not obstacle.is_inside_tree():
				_remove_occupancy(i)
				continue
			var body = item.body
			var field = item.field
			var record: Dictionary = _bodies.get(item.who, {}) if is_instance_valid(body) else {}
			if not is_instance_valid(field) or not is_instance_valid(field.adapter) or record.is_empty() or str(record.map) != field.adapter.map_key() or str(record.resource) != field.execution_resource or body.get_parent() == obstacle.get_parent():
				_remove_occupancy(i)
				continue
			_update_occupancy(item)

	func acquire_actor(who: String, sprite_key: String, field: Node2D) -> CharacterBody2D:
		if _closing: return null
		var body: CharacterBody2D
		var previous: Node2D
		if _bodies.has(who):
			var record: Dictionary = _bodies[who]
			body = record.body
			if not is_instance_valid(body) or str(record.resource) != field.execution_resource or str(record.map) != field.adapter.map_key(): return null
			previous = record.field
			if is_instance_valid(previous) and previous != field: _detach(who, previous)
		else:
			var binding := _actor_binding(who, field.execution_binding)
			var point = _initial_point(who, field, binding)
			if point == null or sprite_key == "": return null
			body = field.create_body(who, sprite_key)
			if body == null: return null
			body.speed = 130.0 if who == _viewer else 110.0
			body.position = point
			field._cancel_motion(body)
			_bodies[who] = {"body": body, "field": field, "map": str(field.adapter.map_key()), "resource": field.execution_resource}
		if body.get_parent() != field.adapter.objects_root():
			var actual := body.position
			if body.get_parent() != null: body.get_parent().remove_child(body)
			field.adapter.objects_root().add_child(body)
			body.position = actual
		body.process_mode = Node.PROCESS_MODE_INHERIT
		field.actors[who] = body
		if who == str(field.adapter.player_key()):
			field.player = body
			field.sight.set_process(not field.execution_only)
		_bodies[who].field = field
		if is_instance_valid(previous) and previous != field: sync_obstacles(previous)
		sync_obstacles(field)
		return body

	func _detach(who: String, field: Node2D) -> void:
		var body: CharacterBody2D = field.actors.get(who)
		if body == null: return
		field._daily_release(who)
		field._cancel_motion(body)
		field.actors.erase(who)
		if field.player == body:
			field.player = null
			field.sight.set_process(false)

	func park_actor(who: String, field: Node2D) -> void:
		var body: CharacterBody2D = field.actors.get(who)
		if body == null: return
		_ensure_parking()
		_bodies[who] = {"body": body, "field": null, "map": str(field.adapter.map_key()), "resource": field.execution_resource}
		_detach(who, field)
		var actual := body.position
		body.get_parent().remove_child(body)
		_parking.add_child(body)
		body.position = actual

	func retain_field(field: Node2D) -> void:
		if not is_instance_valid(field): return
		for who in field.actors.keys(): park_actor(str(who), field)

	func note_map(field: Node2D) -> void:
		for who in field.actors:
			_bodies[who] = {"body": field.actors[who], "field": field, "map": str(field.adapter.map_key()), "resource": field.execution_resource}

	func _hidden_field(who: String, state: Dictionary) -> Node2D:
		var field: Node2D = _worlds.get(who)
		var resource := str(state.get("resource", ""))
		var source_map := str(state.get("map", ""))
		if resource == "" or source_map == "": return null
		if is_instance_valid(field):
			if field.execution_resource != resource: return null
			if field.player == null:
				if field.adapter.has_method("prepare_map"):
					if not await field.adapter.prepare_map(source_map, state, get_tree()): return null
				if _closing or not is_instance_valid(field) or field._closing: return null
				if field.adapter.map_key() != source_map: field.adapter.load_map(source_map, str(state.get("spawn", "")))
				field.execution_binding = state
				var binding := _actor_binding(who, state)
				if acquire_actor(who, str(binding.get("sprite", "")), field) == null: return null
			field.apply_snapshot(state)
			return field
		var packed := CAMPUS_MAP_VIEW if resource == "res://scenes/Chapter2/Field/CampusMapView.tscn" else (load(resource) as PackedScene)
		if packed == null: return null
		var adapter := packed.instantiate() as Node2D
		if adapter == null: return null
		adapter.presentation = state.get("presentation", {})
		_preparing_adapters.append(adapter)
		var prepared := true
		if adapter.has_method("prepare_map"):
			prepared = await adapter.prepare_map(source_map, state, get_tree())
		_preparing_adapters.erase(adapter)
		if not prepared or _closing:
			if adapter.has_method("begin_shutdown"): adapter.begin_shutdown()
			adapter.free()
			return null
		var viewport := _new_viewport("ActorExecution")
		field = Node2D.new()
		field.set_script(WORLD_VIEW)
		field.execution_only = true
		configure_visible(field, state)
		field.hide()
		viewport.add_child(field)
		field.setup(adapter)
		field.apply_snapshot(state)
		_worlds[who] = field
		return field if field.player != null else null

	func _blocked(intent: Dictionary, serial: int, reason: String) -> Dictionary:
		var who := str(intent.get("actor", ""))
		var raw_act = intent.get("action", {})
		var act_id := str(raw_act.get("id", "")) if raw_act is Dictionary else ""
		var result := {"kind": "daily", "who": who, "serial": serial,
			"action": act_id, "status": "blocked", "room": "", "reason": reason}
		if _bodies.has(who):
			var body: CharacterBody2D = _bodies[who].body
			if is_instance_valid(body):
				result["map"] = str(_bodies[who].map)
				result["actual_position"] = [body.position.x, body.position.y]
		return result

	func execute(intent: Dictionary, visible_field: Node2D, execution_snapshot: Dictionary, serial: int) -> Dictionary:
		if _closing or _busy: return _blocked(intent, serial, "executor_unavailable")
		# Hold the same execution lock during asynchronous resource preparation.
		_busy = true
		var receipt := await _execute_pending(intent, visible_field, execution_snapshot, serial)
		_busy = false
		return receipt

	func _execute_pending(intent: Dictionary, visible_field: Node2D, execution_snapshot: Dictionary, serial: int) -> Dictionary:
		var who := str(intent.get("actor", ""))
		var raw_act = intent.get("action", {})
		var action := str(raw_act.get("id", "")) if raw_act is Dictionary else ""
		var destination: Dictionary = intent.get("destination", {})
		var source_map := str(execution_snapshot.get("map", ""))
		var source_room := str(execution_snapshot.get("execution_room", ""))
		if source_map == "" or source_room == "": return _blocked(intent, serial, "missing_source_geometry_binding")
		if _bodies.has(who) and (str(_bodies[who].map) != source_map or str(_bodies[who].resource) != str(execution_snapshot.get("resource", ""))):
			return _blocked(intent, serial, "retained_body_source_mismatch")
		var field: Node2D
		if is_instance_valid(visible_field) and visible_field.execution_resource == str(execution_snapshot.get("resource", "")) and visible_field.adapter.map_key() == source_map:
			var body: CharacterBody2D = visible_field.actors.get(who)
			if body != null and (body == visible_field.player or str(destination.get("map", "")) == source_map): field = visible_field
		if field == null: field = await _hidden_field(who, execution_snapshot)
		if _closing: return _blocked(intent, serial, "execution_interrupted")
		if field == null or not field.actors.has(who): return _blocked(intent, serial, "missing_actor_geometry_binding")
		var body: CharacterBody2D = field.actors[who]
		if field.adapter.departure_room(body.position) != source_room:
			return _blocked(intent, serial, "retained_body_room_mismatch")
		if field.adapter.has_method("apply_snapshot"): field.adapter.apply_snapshot(execution_snapshot)
		sync_obstacles(field)
		var source_point := body.position
		var source_geometry := "interior" if field.adapter.entered_room(source_point) == source_room else "source-side-doorway"
		var instance := body.get_instance_id()
		await get_tree().physics_frame
		if _closing or not is_instance_valid(field) or field._closing:
			return _blocked(intent, serial, "execution_interrupted")
		var received: Dictionary = {}
		var listener := func(event: Dictionary) -> void:
			if str(event.get("kind", "")) == "daily" and int(event.get("serial", -1)) == serial and str(event.get("who", "")) == who and str(event.get("action", "")) == action:
				received.merge(event, true)
		field.field_event.connect(listener)
		field.execute_move(destination, who, serial, action)
		while received.is_empty() and not _closing and is_instance_valid(field) and not field._closing:
			await get_tree().process_frame
		if is_instance_valid(field) and field.field_event.is_connected(listener): field.field_event.disconnect(listener)
		if received.is_empty(): return _blocked(intent, serial, "execution_interrupted")
		received["source_map"] = source_map
		received["source_room"] = source_room
		received["source_geometry"] = source_geometry
		received["source_position"] = [source_point.x, source_point.y]
		received["actor_instance_id"] = instance
		received["execution_world"] = "isolated-godot" if field.execution_only else "visible-godot"
		note_map(field)
		if str(received.get("status", "")) == "arrived":
			var expected_room := str(destination.get("execution_room", destination.get("room", "")))
			if str(received.get("room", "")) != expected_room or str(received.get("map", "")) != str(destination.get("map", "")):
				received.status = "blocked"
				received.reason = "arrival_identity_mismatch"
		var raw_travel = raw_act.get("travel", {}) if raw_act is Dictionary else {}
		if str(received.get("status", "")) == "arrived" and raw_travel is Dictionary and raw_travel.has("station"):
			var station := str(raw_travel.station)
			if str(received.get("station", "")) != station or not received.has("arrival_error") or float(received.arrival_error) > 4.0:
				received.status = "blocked"
				received.reason = "station_evidence_mismatch"
		if field != visible_field and is_instance_valid(visible_field) and field.execution_resource == visible_field.execution_resource and field.adapter.map_key() == visible_field.adapter.map_key():
			acquire_actor(who, "", visible_field)
		return received

	func begin_shutdown() -> void:
		if _closing: return
		_closing = true
		for adapter in _preparing_adapters:
			if is_instance_valid(adapter) and adapter.has_method("begin_shutdown"): adapter.begin_shutdown()
		for field in _worlds.values():
			if is_instance_valid(field): field.begin_shutdown()

	func reset() -> void:
		begin_shutdown()
		for viewport in get_children():
			remove_child(viewport)
			viewport.queue_free()
		_bodies.clear()
		_worlds.clear()
		_occupancy.clear()
		_parking = null
		_busy = false
		_closing = false
		_viewer = ""
