extends Node
## 单一窗口：把输入交给 C#，把输出交给视图或 GUI，不持有剧情与章节规则。

const EngineScript := preload("res://scenes/BasicUISystem/GameEngine.cs")
const UI_SCENE := preload("res://scenes/Shared/UI/UISystem.tscn")
const TITLE_SCENE := preload("res://scenes/Title/Title.tscn")
const FIELD_SCRIPT := preload("res://scenes/Shared/Field/WorldView.gd")
const VIEW_TRACE := preload("res://scenes/BasicUISystem/PlayerViewTrace.gd")
const FIELD_EVENTS := ["scene", "stage"]

var view_trace: Node
var engine: Node
var ui: Node
var field: Node2D
var title_layer: CanvasLayer
var _queue: Array = []
var _draining := false
var _closing := false

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
	field.field_event.connect(dispatch)
	field.hover_changed.connect(ui.show_hover)
	field.hint_changed.connect(ui.set_hint)
	field.setup(resource.instantiate())
	if str(ev.get("map", "")) != "": field.load_map(str(ev.map), str(ev.get("spawn", "")))
	_apply(engine.Snapshot())
	# 等场景加入树并应用快照后才开始脚本；这也是可见视图的普通握手。
	dispatch.call_deferred({"kind": "view_ready"})

func dispatch(input: Dictionary) -> void:
	if _closing: return
	if str(input.get("kind", "")) == "quit":
		engine.RecordInput({"device": "ui_semantic", "kind": "quit"})
		begin_shutdown()
		get_tree().quit()
		return
	var message: Dictionary = input.duplicate(true)
	var context: Dictionary = field.context() if is_instance_valid(field) else {}
	context.modal = ui.modal_kind if is_instance_valid(ui) else ""
	message.context = context
	_queue.append_array(engine.Dispatch(message))
	await _drain()

func _drain() -> void:
	if _draining or _closing: return
	_draining = true
	while not _queue.is_empty() and not _closing:
		var ev: Dictionary = _queue.pop_front()
		var kind := str(ev.get("kind", ""))
		if kind == "home":
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
			if kind == "lines": ui.apply_snapshot(engine.Snapshot())
			var reply: Dictionary = await ui.present(ev)
			if not reply.is_empty() and not _closing: dispatch(reply)
		elif kind == "error":
			push_error(str(ev.message))
	_draining = false

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
