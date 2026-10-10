extends Node
## 单一窗口：把输入交给 C#，把输出交给视图或 GUI，不持有剧情与章节规则。

const EngineScript := preload("res://scenes/Host/GameEngine.cs")
const UI_SCENE := preload("res://scenes/UI/UISystem.tscn")
const TITLE_SCENE := preload("res://scenes/UI/Panels/Title/Title.tscn")
const FIELD_SCRIPT := preload("res://scenes/Shared/Field/WorldView.gd")
const VIEW_TRACE := preload("res://scenes/Host/PlayerViewTrace.gd")
const FIELD_EVENTS := ["scene", "stage"]
const MEASUREMENT_EVENTS := ["case_progress", "strategy_deviation", "strategy_action", "object_changed", "world_resolution"]

var view_trace: Node
var engine: Node
var ui: Node
var field: Node2D
var title_layer: CanvasLayer
var _queue: Array = []
var _draining := false
var _awaiting_ui := false
var _closing := false
## 坐在玩家席位上的 AI（为空时由真人操作）。
var pilot: Node

func _ready() -> void:
	add_to_group("lifecycle_hosts")
	TranslationServer.set_locale("zh_CN")
	get_window().title = "安眠者有福"
	engine = EngineScript.new()
	engine.name = "Engine"
	add_child(engine)
	view_trace = VIEW_TRACE.new()
	view_trace.name = "PlayerViewTrace"
	add_child(view_trace)
	view_trace.configure(self, engine.RecordVisible, engine.RecordInput)
	_home()

func _dispose_views() -> void:
	if is_instance_valid(field):
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
	title_layer = CanvasLayer.new()
	title_layer.layer = 30
	add_child(title_layer)
	var title: Control = TITLE_SCENE.instantiate()
	title_layer.add_child(title)
	title.ui_event.connect(dispatch)

func _view(ev: Dictionary) -> void:
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
	field.setup(resource.instantiate())
	if str(ev.get("map", "")) != "": field.load_map(str(ev.map), str(ev.get("spawn", "")))
	_apply(engine.Snapshot())
	# 等场景加入树并应用快照后才开始脚本；这也是可见视图的普通握手。
	dispatch.call_deferred({"kind": "view_ready"})

## AI 坐上玩家席位：该角色的决定只经 token 交回引擎，界面输入不再替它做选择。
func enable_pilot(seat: Node) -> void:
	pilot = seat
	add_child(pilot)
	pilot.attach(self)

func dispatch(input: Dictionary) -> void:
	if _closing: return
	if str(input.get("kind", "")) == "quit":
		engine.RecordInput({"device": "ui_semantic", "kind": "quit"})
		begin_shutdown()
		get_tree().quit()
		return
	if is_instance_valid(pilot) and _seat_input(input): return
	_send(input, false)

## 席位由 AI 坐着时的界面输入：读完台词的回执交给 AI 当作它的选择，
## 由引擎发起的面板回执照常交回，其余操作不代替角色做决定。
func _seat_input(input: Dictionary) -> bool:
	var kind := str(input.get("kind", ""))
	if kind == "presented" and pilot.take_ui_reply(input): return true
	if kind in ["presented", "opened", "start", "view_ready"]:
		_send(input, kind == "opened")
		return true
	return true

func _field_input(input: Dictionary) -> void:
	if _closing: return
	_send(input, is_instance_valid(pilot))

func submit_token(token: String) -> void:
	if _closing: return
	_send({"kind": "select_candidate", "token": token}, false)

func _send(input: Dictionary, from_field: bool) -> void:
	var message: Dictionary = input.duplicate(true)
	var context: Dictionary = field.context() if is_instance_valid(field) else {}
	context.modal = ui.modal_kind if is_instance_valid(ui) else ""
	message.context = context
	var events: Array = engine.DispatchField(message) if from_field else engine.Dispatch(message)
	if is_instance_valid(pilot):
		# A closed seat must stop at once, not wait behind a dialogue it can no longer answer.
		for ev in events:
			if str(ev.get("kind", "")) == "agent_stopped": pilot.stop(str(ev.reason))
	_queue.append_array(events)
	await _drain()

## 没有待呈现的事件，或界面正等本人读完/选择（其后的事件排在它之后）。
func settled() -> bool:
	return _awaiting_ui or (_queue.is_empty() and not _draining)

func _drain() -> void:
	if _draining or _closing: return
	_draining = true
	while not _queue.is_empty() and not _closing:
		var ev: Dictionary = _queue.pop_front()
		var kind := str(ev.get("kind", ""))
		if kind == "error" and is_instance_valid(pilot):
			pilot.recorded.emit("feedback", {"message": str(ev.get("message", ""))})
			pilot.stop("engine_error")
		if kind in MEASUREMENT_EVENTS:
			# Session tracing and the seat record retain these; the visual host presents their actual dialogue separately.
			if is_instance_valid(pilot): pilot.recorded.emit(kind, ev)
			continue
		elif kind == "player_field":
			if is_instance_valid(pilot): await pilot.perform_field(ev.input)
		elif kind == "player_panel":
			if is_instance_valid(ui): ui.open_panel(str(ev.panel))
		elif kind == "agent_stopped":
			if is_instance_valid(pilot): pilot.stop(str(ev.reason))
		elif kind == "home":
			_home()
		elif kind == "view":
			_view(ev)
		elif kind == "state":
			_apply(ev.snapshot)
		elif kind == "travel":
			if is_instance_valid(field): await field.travel_to_node(ev.node)
		elif kind in FIELD_EVENTS:
			if is_instance_valid(field):
				if kind == "scene": await field.execute(ev)
				else: field.execute(ev)
		elif kind == "replay_play" and str(ev.get("mode", "")) == "stage":
			if is_instance_valid(field): await field.execute(ev)
		elif is_instance_valid(ui):
			if is_instance_valid(pilot): pilot.recorded.emit("shown", _visible_part(ev))
			if kind == "lines": ui.apply_snapshot(engine.Snapshot())
			_awaiting_ui = true
			var reply: Dictionary = await ui.present(ev)
			_awaiting_ui = false
			if not reply.is_empty() and not _closing: dispatch(reply)
		elif kind == "error":
			push_error(str(ev.message))
	_draining = false

## 画面上实际显示给本人的内容（不含来源、呈现编号与 token）。
static func _visible_part(ev: Dictionary) -> Dictionary:
	var shown := {"kind": str(ev.get("kind", ""))}
	for key in ["title", "prompt", "intro", "facts", "source", "text", "outcome", "notes", "rows", "items", "caption"]:
		if ev.has(key): shown[key] = ev[key]
	if ev.has("lines"):
		shown.lines = []
		for line in ev.lines: shown.lines.append([str(line.get("speaker_name", line.get("speaker", ""))), str(line.get("text", ""))])
	for key in ["options", "choices", "actions"]:
		if ev.has(key):
			shown[key] = []
			for option in ev[key]: shown[key].append(str(option.get("label", "")))
	return shown

func _apply(snapshot: Dictionary) -> void:
	if is_instance_valid(ui): ui.apply_snapshot(snapshot)
	if is_instance_valid(field): field.apply_snapshot(snapshot)

func begin_shutdown() -> void:
	if _closing: return
	_closing = true
	if is_instance_valid(view_trace): view_trace.begin_shutdown()
	_queue.clear()
	if is_instance_valid(field): field.begin_shutdown()
	if is_instance_valid(ui): ui.begin_shutdown()
	if is_instance_valid(engine): engine.CloseTrace()

func _exit_tree() -> void:
	begin_shutdown()
