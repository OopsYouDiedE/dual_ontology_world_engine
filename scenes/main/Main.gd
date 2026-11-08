extends Node

## 游戏唯一根场景：持有 SessionBridge / DvPresentationContent 网关，
## 协调书房舞台（StudyDeskStage3D）、标题（TitleUI）、书本检视器（BookUI）与现场（CampaignRoomHost）。
## UI 只通过本文件的会话接口提交操作；存档页的读写见 save_slots() 一组函数。

signal shutdown_completed
signal mode_changed(new_mode: String)

const SESSION_BRIDGE_SCRIPT := preload("res://scenes/main/SessionBridge.cs")
const PRESENTATION_CONTENT_SCRIPT := preload("res://scenes/main/DvPresentationContent.cs")
const STUDY_STAGE_SCENE := preload("res://scenes/components/world/StudyDeskStage3D.tscn")
const TITLE_UI_SCENE := preload("res://scenes/UIpanel/TitleUI/TitleUI.tscn")
const BOOK_UI_SCENE := preload("res://scenes/UIpanel/BookUI/BookUI.tscn")
const FIELD_GUI_SCENE := preload("res://scenes/UIpanel/GUI/FieldGUI.tscn")
const LIVE_BUILDER_SCENE := preload("res://tools/godot_architect/LiveBuilderOverlay.tscn")
const CAMPAIGN_ROOM_HOST_SCENE := preload("res://scenes/components/characters/CampaignRoomHost.tscn")
const Sound := preload("res://scenes/components/shared/Sound.gd")
const MANUAL_SLOTS := 6
const SLOT_NAMES := ["一", "二", "三", "四", "五", "六"]

@export var default_scenario_path := "res://assets/data/scenarios/campaign.dv"
@export var autoplay_title := true

var session_bridge  # Untyped: C# script class; typed declaration fails static check during editor import.
var presentation_content  # Untyped: C# script class; see session_bridge.

var stage: Node3D
var title_ui: Control
var book_ui: Control
var field_gui: Control
var live_builder: CanvasLayer
var ui_layer: CanvasLayer

var current_mode := "title" # "title", "book", "field"
var return_mode_after_book := "title"
var session_ready := false

class HostFlowState:
	var started := false
	var opening := false
	var draining := false
	var completion_pending := false
	var background_execution := false
	var reading := false
	var book_reading := false
	var view_faulted := false
	var opening_failed := false
	var shutdown_finished := false
	var player_options_open := false
	var closing := false
	func blocks_input() -> bool:
		return false

var flow := HostFlowState.new()
var _dv_intent: Array = []
var _world_ui_paused := false
var _session_generation := 0
var field: Node = null
var native_character_host: Node:
	get: return field

func native_character_mode() -> bool:
	return is_instance_valid(field)

func native_character_observation() -> Dictionary:
	return field.observation() if is_instance_valid(field) and field.has_method("observation") else {"ready": false}

func native_character_physical() -> Dictionary:
	return field.physical_observation() if is_instance_valid(field) and field.has_method("physical_observation") else {}

func _ready() -> void:
	name = "Main"
	process_mode = Node.PROCESS_MODE_ALWAYS
	_initialize_gateways()
	_initialize_presentation()
	if autoplay_title:
		enter_title(true)

func _initialize_gateways() -> void:
	session_bridge = SESSION_BRIDGE_SCRIPT.new()
	session_bridge.name = "SessionBridge"
	add_child(session_bridge)
	presentation_content = PRESENTATION_CONTENT_SCRIPT.new()
	session_bridge.TraceClosed.connect(_on_session_trace_closed)

func _initialize_presentation() -> void:
	stage = STUDY_STAGE_SCENE.instantiate()
	add_child(stage)
	ui_layer = CanvasLayer.new()
	ui_layer.name = "UILayer"
	add_child(ui_layer)
	field_gui = FIELD_GUI_SCENE.instantiate()
	ui_layer.add_child(field_gui)
	field_gui.hide()
	book_ui = BOOK_UI_SCENE.instantiate()
	ui_layer.add_child(book_ui)
	book_ui.configure(stage, self)
	book_ui.closed.connect(_on_book_closed)
	title_ui = TITLE_UI_SCENE.instantiate()
	ui_layer.add_child(title_ui)
	title_ui.new_game_requested.connect(start_new_game)
	title_ui.continue_requested.connect(continue_game)
	title_ui.book_requested.connect(open_book)
	title_ui.quit_requested.connect(_quit)
	var settings := get_node_or_null("/root/GameSettings")
	if settings != null: settings.settings_requested.connect(_on_settings_requested)
	live_builder = LIVE_BUILDER_SCENE.instantiate()
	add_child(live_builder)

# ================================================================ gateway ===

func submit_session_action(action: Dictionary) -> Array:
	if not is_instance_valid(session_bridge): return []
	var response: Dictionary = session_bridge.Dispatch(action)
	return response.get("events", [])

func observe_session() -> Dictionary:
	if not is_instance_valid(session_bridge): return {}
	return session_bridge.Snapshot()

func read_presentation_record(path: String, category: String, identity: String) -> Dictionary:
	if presentation_content == null: return {}
	return presentation_content.ReadRecord(path, category, identity)

func validate_prefab_placement(prefab_path: String, position: Vector3, rotation: Vector3, context: Dictionary = {}) -> Dictionary:
	if not is_instance_valid(session_bridge): return {"valid": false, "reason": "no_session"}
	return session_bridge.ValidatePrefabPlacement(prefab_path, position, rotation, context)

func dispatch(operation: Dictionary) -> void:
	match str(operation.get("kind", "")):
		"start": start_new_game()
		"load": continue_game()
		"action": submit_session_action(operation)

func session_is_idle() -> bool:
	if current_mode == "field" and is_instance_valid(field):
		return bool(field.get("ready_for_play"))
	return true

func _formal_selection_ready() -> bool:
	return false

func wait_for_shutdown() -> void:
	flow.closing = true
	if is_instance_valid(field_gui): field_gui.release()
	if is_instance_valid(field) and field.has_method("shutdown"): field.shutdown()
	shutdown_completed.emit()
	flow.shutdown_finished = true

# ============================================================ navigation ===

func enter_title(first := false) -> void:
	current_mode = "title"
	return_mode_after_book = "title"
	flow.reading = false
	if is_instance_valid(field_gui): field_gui.release()
	stage.set_active(true)
	title_ui.show_title(has_progress(), first)
	Sound.music("main_theme", 3.0 if first else 1.6)
	mode_changed.emit(current_mode)

func start_new_game() -> void:
	if flow.started: return
	_open_field({})

func continue_game() -> void:
	var latest := ""
	var latest_time := -1
	for slot: Dictionary in save_slots():
		if bool(slot.exists) and int(slot.saved_at) > latest_time:
			latest = str(slot.id)
			latest_time = int(slot.saved_at)
	if latest.is_empty(): return
	load_slot(latest)

## 打开书本检视器到指定页（gallery / save / settings）。已打开时翻到该页。
func open_book(page_id := "gallery") -> void:
	if current_mode == "book":
		book_ui.goto_page(page_id)
		return
	if book_ui.busy() or current_mode not in ["title", "field"]: return
	return_mode_after_book = current_mode
	if current_mode == "title":
		title_ui.hide_title()
	else:
		_suspend_field(true)
		stage.snap_closed()
		stage.set_active(true)
	if is_instance_valid(field_gui):
		field_gui.set_suppressed(true)
	current_mode = "book"
	flow.reading = true
	book_ui.open(page_id, return_mode_after_book == "field")
	mode_changed.emit(current_mode)

func close_book() -> void:
	book_ui.close()

func _on_book_closed() -> void:
	flow.reading = false
	if return_mode_after_book == "field" and is_instance_valid(field):
		stage.set_active(false)
		_suspend_field(false)
		if is_instance_valid(field_gui):
			field_gui.set_suppressed(false)
		current_mode = "field"
		mode_changed.emit(current_mode)
	else:
		if is_instance_valid(field_gui):
			field_gui.release()
		enter_title()

func _on_settings_requested() -> void:
	if current_mode == "book":
		if book_ui.page_id() == "settings": close_book()
		else: book_ui.goto_page("settings")
	else:
		open_book("settings")

func _open_field(restored: Dictionary) -> void:
	if is_instance_valid(field):
		if is_instance_valid(field_gui):
			field_gui.release()
		field.shutdown()
		field.queue_free()
		field = null
	book_ui.close_immediately()
	title_ui.hide_title(0.2)
	stage.set_active(false)
	flow.started = true
	flow.reading = false
	session_bridge.Open(default_scenario_path)
	session_ready = true
	field = CAMPAIGN_ROOM_HOST_SCENE.instantiate()
	add_child(field)
	move_child(field, 0)
	field.configure(default_scenario_path, restored)
	if is_instance_valid(field_gui):
		field_gui.configure(field)
	current_mode = "field"
	return_mode_after_book = "field"
	Sound.stop_music(1.2)
	mode_changed.emit(current_mode)

## 打开书本时冻结现场：停止处理与输入，书房镜头接管画面。
func _suspend_field(suspended: bool) -> void:
	if not is_instance_valid(field): return
	field.process_mode = Node.PROCESS_MODE_DISABLED if suspended else Node.PROCESS_MODE_INHERIT
	field.visible = not suspended
	if not suspended and is_instance_valid(field.get("camera")): field.camera.make_current()

# ================================================================= input ===

func _input(event: InputEvent) -> void:
	if current_mode == "book" and book_ui.route_input(event):
		get_viewport().set_input_as_handled()

func _unhandled_input(event: InputEvent) -> void:
	if current_mode == "title" and title_ui.interactive and event is InputEventMouseButton \
			and event.pressed and event.button_index == MOUSE_BUTTON_LEFT and stage.pick_book(event.position):
		get_viewport().set_input_as_handled()
		open_book("gallery")
	elif current_mode == "field" and event.is_action_pressed("notebook"):
		get_viewport().set_input_as_handled()
		open_book("save")

func _process(_delta: float) -> void:
	if not stage.visible: return
	var viewport := get_viewport()
	var mouse := viewport.get_mouse_position()
	var size := viewport.get_visible_rect().size
	stage.set_pointer(mouse / size.max(Vector2.ONE) * 2.0 - Vector2.ONE)
	var hovering: bool = current_mode == "title" and title_ui.interactive and stage.pick_book(mouse)
	stage.set_hover(hovering)
	Input.set_default_cursor_shape(Input.CURSOR_POINTING_HAND if hovering else Input.CURSOR_ARROW)

# ================================================================= saves ===

## 自动记录 + MANUAL_SLOTS 页手记。每项：id、label、kind、exists、saved_at、place、game_time。
func save_slots() -> Array:
	var slots: Array = []
	var auto_path: String = session_bridge.SavedGameFile()
	var auto := {"id": "auto", "label": "自动记录", "kind": "auto", "exists": FileAccess.file_exists(auto_path), "saved_at": 0, "place": "—", "game_time": "—"}
	if auto.exists:
		auto.saved_at = FileAccess.get_modified_time(auto_path)
		var data: Variant = JSON.parse_string(FileAccess.get_file_as_string(auto_path))
		if data is Dictionary:
			auto.place = str(data.get("campaign_view", {}).get("room", "—"))
	slots.append(auto)
	for index in MANUAL_SLOTS:
		var id := str(index + 1)
		var slot := {"id": id, "label": "手记 " + SLOT_NAMES[index], "kind": "manual", "exists": false, "saved_at": 0, "place": "—", "game_time": "—"}
		var data: Variant = _read_json(_slot_path(id))
		if data is Dictionary and data.get("save") is Dictionary:
			var meta: Dictionary = data.get("meta", {})
			slot.exists = true
			slot.saved_at = int(meta.get("saved_at", 0))
			slot.place = str(meta.get("place", "—"))
			slot.game_time = str(meta.get("game_time", "—"))
		slots.append(slot)
	return slots

func has_progress() -> bool:
	for slot: Dictionary in save_slots():
		if bool(slot.exists): return true
	return false

func can_save() -> bool:
	return return_mode_after_book == "field" and is_instance_valid(field) and bool(field.get("ready_for_play"))

func save_to_slot(id: String) -> String:
	if id == "auto" or not id.is_valid_int(): return "自动记录由调查进程写入。"
	if not can_save(): return "调查中才能写入记录。"
	var checkpoint: Dictionary = field.host_checkpoint()
	if checkpoint.has("error"): return "写入失败：" + str(checkpoint.error)
	var seconds := float(field.get("state").get("game_seconds", 0.0))
	var meta := {"saved_at": int(Time.get_unix_time_from_system()), "place": str(field.room_label(str(field.get("room_id")))),
		"game_time": "第%d日 %02d:%02d" % [int(seconds / 86400.0) + 1, int(fmod(seconds, 86400.0) / 3600.0), int(fmod(seconds, 3600.0) / 60.0)]}
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(_slot_directory()))
	var file := FileAccess.open(_slot_path(id), FileAccess.WRITE)
	if file == null: return "写入失败：" + error_string(FileAccess.get_open_error())
	file.store_string(JSON.stringify({"meta": meta, "save": checkpoint}))
	file.close()
	return ""

func load_slot(id: String) -> String:
	var restored: Variant = null
	if id == "auto":
		restored = JSON.parse_string(session_bridge.ReadSavedGame())
	else:
		var data: Variant = _read_json(_slot_path(id))
		if data is Dictionary: restored = data.get("save")
	if not restored is Dictionary: return "这一页的记录无法读取。"
	_open_field(restored)
	return ""

func delete_slot(id: String) -> String:
	if id == "auto" or not id.is_valid_int(): return "自动记录不能撕去。"
	var path := ProjectSettings.globalize_path(_slot_path(id))
	if not FileAccess.file_exists(path): return ""
	var error := DirAccess.remove_absolute(path)
	return "" if error == OK else "撕去失败：" + error_string(error)

func _slot_directory() -> String:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--dv-save-slot="): return "user://sessions/%s/casebook" % argument.substr(15)
	return "user://casebook"

func _slot_path(id: String) -> String:
	return _slot_directory().path_join("slot-%s.json" % id)

static func _read_json(path: String) -> Variant:
	if not FileAccess.file_exists(path): return null
	return JSON.parse_string(FileAccess.get_file_as_string(path))

func _quit() -> void:
	var audio := get_node_or_null("/root/GameAudio")
	if audio != null: audio.quit_cleanly()
	else: get_tree().quit()

func _on_session_trace_closed() -> void:
	shutdown_completed.emit()
