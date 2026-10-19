extends Node
signal shutdown_completed
## 单一窗口：把输入交给 C#，把输出交给视图或 GUI，不持有剧情与章节规则。

const EngineScript := preload("res://scenes/Host/GameEngine.cs")
const UI_SCENE := preload("res://scenes/UI/UISystem.tscn")
const TITLE_SCENE := preload("res://scenes/UI/Panels/Title/Title.tscn")
const FIELD_SCRIPT := preload("res://scenes/Shared/Field/WorldView.gd")
const FIELD_EXECUTOR := preload("res://scenes/Host/DvFieldExecutor.gd")
const VIEW_TRACE := preload("res://scenes/Host/PlayerViewTrace.gd")

@export_enum("player", "ai") var controller := "player"
@export var campaign_options: Dictionary = {}
@export var viewer := ""
@export_range(0.25, 8.0, 0.25) var playback_speed := 4.0
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
	title_layer.add_child(title)
	title.ui_event.connect(dispatch)

func _view(ev: Dictionary, begin := true) -> void:
	_view_resource = str(ev.resource)
	var resource: PackedScene = load(str(ev.resource)) as PackedScene
	if resource == null:
		push_error("无法加载世界视图：" + str(ev.resource))
		return
	_dispose_views()
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
	field.fader = ui.hud
	field.field_event.connect(_field_input)
	field.hover_changed.connect(ui.show_hover)
	field.hint_changed.connect(ui.set_hint)
	var adapter: Node2D = resource.instantiate()
	var initial: Dictionary = engine.Snapshot()
	if adapter.get("presentation") != null and initial.has("presentation"):
		adapter.presentation = initial.presentation
	field_executor.configure_visible(field, initial)
	field.setup(adapter)
	field_executor.bind_visible(field, initial)
	_apply(initial)
	# 等场景加入树并应用快照后才开始脚本；这也是可见视图的普通握手。
	if begin: dispatch.call_deferred({"kind": "view_ready"})

## 输入只进入 DV 会话的统一调度入口。
func dispatch(input: Dictionary) -> void:
	if _closing: return
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
	var options := {"controller": controller, "campaign": campaign_options.duplicate(true)}
	if viewer != "": options.viewer = viewer
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--dv-speed="):
			var value := arg.trim_prefix("--dv-speed=")
			if not value.is_valid_float():
				_show_error("Invalid --dv-speed: " + value)
				return {}
			speed = float(value)
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
	engine.RecordInput({"device": "host_configuration", "controller": str(options.controller), "playback_speed": speed, "physics_ticks_per_second": Engine.physics_ticks_per_second})
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
	# Prepared movement is consumed only by the executor's awaited receipt.
	if str(input.get("kind", "")) != "daily": return

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
		match str(ev.get("kind", "")):
			"dv_action": await _dv_execute(ev.intent, ev.get("execution_snapshot", {}))
			"dv_present":
				var result: Dictionary = ev.presentation
				var lines := _presentation_lines(result)
				if not lines.is_empty():
					await ui.present({"kind": "lines", "lines": lines, "trace_id": str(result.get("execution_event", "")), "presentation_id": str(result.get("execution_event", ""))})
				dispatch.call_deferred({"kind": "dv_next"})
			"view": _view(ev)
			"state": _apply(ev.snapshot)
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
	if _view_resource != "" and str(snapshot.resource) != _view_resource:
		_view(snapshot, false)
		return
	if is_instance_valid(ui): ui.apply_snapshot(snapshot)
	if is_instance_valid(field): field.apply_snapshot(snapshot)

func begin_shutdown() -> void:
	if _closing: return
	_closing = true
	if is_instance_valid(view_trace): view_trace.begin_shutdown()
	_queue.clear()
	if is_instance_valid(field_executor): field_executor.begin_shutdown()
	if is_instance_valid(field): field.begin_shutdown()
	if is_instance_valid(ui): ui.begin_shutdown()
	if is_instance_valid(engine): engine.CloseTrace()

func _exit_tree() -> void:
	begin_shutdown()

func _dv_events(events: Array) -> void:
	_queue.append_array(events)
	_drain.call_deferred()

func _dv_execute(intent: Dictionary, execution_snapshot: Dictionary) -> void:
	if str(intent.get("status", "")) == "awaiting_input":
		ui.present({"kind": "candidates", "title": "接下来做什么", "options": intent.options})
		return
	if str(intent.get("status", "")) != "prepared": return
	_dv_intent = intent
	_dv_serial += 1
	var action: Dictionary = intent.action
	if str(action.kind) == "move" or action.get("travel", {}).has("station"):
		var receipt: Dictionary = await field_executor.execute(intent, field, execution_snapshot, _dv_serial)
		_accept_execution(receipt)
	else:
		_dv_complete("applied", {"executor": "dv-world", "context": field.context()})

func _dv_complete(outcome: String, execution: Dictionary) -> void:
	var receipt := {"action_event": _dv_intent.action_event, "outcome": outcome, "actual": _dv_intent.execution_cost, "executor": "godot", "field": execution}
	_dv_intent = {}
	dispatch.call_deferred({"kind": "dv_complete", "receipt": receipt})

func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_CLOSE_REQUEST and not _closing:
		begin_shutdown.call_deferred()
		await shutdown_completed
		get_tree().quit()
