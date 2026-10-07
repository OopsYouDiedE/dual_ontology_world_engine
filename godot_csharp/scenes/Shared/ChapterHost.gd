extends Node
## 章节主代码（所有章节同一份）：只接收与转发。
## 界面（ChapterUI）与现场（ChapterField）发出的事件 → 附上现场几何 → ChapterEngine.Dispatch；
## 章节返回的事件 → 现场（scene、stage、地图上的回放）或界面（其余一切）；快照同时交给两者。
## 章节差异只在场景里设置：chapter_id、地图适配器与起始地图。

const EngineScript := preload("res://scenes/Shared/ChapterEngine.cs")
const Sound := preload("res://scenes/Shared/Audio/Sound.gd")
## 章末去处：章节给出目标键，这里换到对应场景。
const DESTINATIONS := {
	"chapter1": "res://scenes/Awakening/Chapter1.tscn", "chapter2": "res://scenes/Chapter2/Chapter2.tscn",
	"title": "res://scenes/Title/Title.tscn",
}
const FIELD_EVENTS := ["scene", "stage"]

@export var chapter_id := "chapter1"
@export var map_adapter: PackedScene
@export var start_map := ""
@export var start_spawn := ""

@onready var ui: Node = $UI
@onready var field: Node2D = $Field
var engine: Node
var _queue: Array = []
var _draining := false
var _closing := false

func _ready() -> void:
	add_to_group("lifecycle_hosts")
	TranslationServer.set_locale("zh_CN")
	get_window().content_scale_size = Vector2i(1280, 992)
	get_window().content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	get_window().content_scale_stretch = Window.CONTENT_SCALE_STRETCH_INTEGER
	get_window().size = Vector2i(1280, 992)
	engine = EngineScript.new()
	engine.name = "Engine"
	add_child(engine)
	field.ui_busy = ui.is_busy
	field.fader = ui.hud
	field.setup(map_adapter.instantiate())
	field.field_event.connect(dispatch)
	field.hover_changed.connect(ui.show_hover)
	field.hint_changed.connect(ui.set_hint)
	ui.ui_event.connect(_on_ui_event)
	var opened: Dictionary = engine.Open(chapter_id)
	if not bool(opened.ok):
		_startup_failed(str(opened.error))
		return
	if start_map != "": field.load_map(start_map, start_spawn)
	_apply(engine.Snapshot())
	if not has_meta("skip_intro"): dispatch.call_deferred({"kind": "begin"})

func _startup_failed(message: String) -> void:
	set_physics_process(false)
	field.process_mode = Node.PROCESS_MODE_DISABLED
	ui.present({"kind": "ending", "title": "章节尚未开启", "outcome": message, "rows": [],
		"actions": [{"id": "title", "label": "返回标题", "key": "Enter"}]})

func begin_shutdown() -> void:
	if _closing: return
	_closing = true
	_queue.clear()
	if is_instance_valid(field): field.begin_shutdown()
	if is_instance_valid(ui): ui.begin_shutdown()

func _exit_tree() -> void:
	begin_shutdown()

## 界面事件：换场景与赶路由宿主或现场完成，其余交给章节。
func _on_ui_event(ev: Dictionary) -> void:
	match str(ev.kind):
		"navigate": _navigate(str(ev.to))
		"travel": await field.travel_to_node(ev.node)
		_: dispatch(ev)

func dispatch(input: Dictionary) -> void:
	if _closing or not engine.Started(): return
	var context: Dictionary = field.context()
	context.modal = ui.modal_kind
	input.context = context
	_queue.append_array(engine.Dispatch(input))
	await _drain()

func _drain() -> void:
	if _draining or _closing: return
	_draining = true
	while not _queue.is_empty() and not _closing:
		var ev: Dictionary = _queue.pop_front()
		var kind := str(ev.kind)
		if kind == "state":
			_apply(ev.snapshot)
		elif kind in FIELD_EVENTS:
			if kind == "scene": await field.execute(ev)
			else: field.execute(ev)
		elif kind == "replay_play" and str(ev.get("mode", "")) == "stage":
			await field.execute(ev)
		else:
			var reply: Dictionary = await ui.present(ev)
			if not reply.is_empty() and not _closing:
				var context: Dictionary = field.context()
				context.modal = ui.modal_kind
				reply.context = context
				_queue.append_array(engine.Dispatch(reply))
	_draining = false

func _apply(snapshot: Dictionary) -> void:
	ui.apply_snapshot(snapshot)
	field.apply_snapshot(snapshot)

func _navigate(to: String) -> void:
	begin_shutdown()
	if to == "restart": get_tree().reload_current_scene()
	else: get_tree().change_scene_to_file(DESTINATIONS.get(to, DESTINATIONS.title))
