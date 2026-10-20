extends Control

## UI 总装容器与事件总线：解耦汇聚 6 大界面，向上与 Host/Main 提供纯数据通信。
signal ui_event(event: Dictionary)
signal step_dialogue
signal dialogue_option_selected(choice_id: String)

const Sound := preload("res://scenes/Shared/Audio/Sound.gd")
const SETTINGS_SCENE := preload("res://scenes/UI/SettingsUI.tscn")
const HUD_SCENE := preload("res://scenes/UI/FieldHUD.tscn")
const INVESTIGATION_SCENE := preload("res://scenes/UI/InvestigationUI.tscn")
const DIALOGUE_SCENE := preload("res://scenes/UI/DialogueUI.tscn")

var settings_ui: CanvasLayer
var hud_ui: CanvasLayer
var investigation_ui: CanvasLayer
var dialogue_ui: CanvasLayer

var snapshot: Dictionary = {}
var _closing := false
var _trace_display: Dictionary = {}

var hud: CanvasLayer:
	get:
		return hud_ui

var fader: ColorRect:
	get:
		return dialogue_ui.get_fader() if is_instance_valid(dialogue_ui) else null

var ui_busy: bool:
	get:
		return is_busy()

func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_instantiate_subsystems()

func _instantiate_subsystems() -> void:
	hud_ui = HUD_SCENE.instantiate()
	add_child(hud_ui)
	hud_ui.settings_requested.connect(func(): open_settings())

	dialogue_ui = DIALOGUE_SCENE.instantiate()
	add_child(dialogue_ui)
	dialogue_ui.dialogue_step.connect(func(): step_dialogue.emit())
	dialogue_ui.choice_selected.connect(func(cid: String):
		dialogue_option_selected.emit(cid)
		ui_event.emit({"kind": "choice", "id": cid})
	)
	dialogue_ui.dialogue_finished.connect(func(_last_choice: String):
		step_dialogue.emit()
	)

	investigation_ui = INVESTIGATION_SCENE.instantiate()
	add_child(investigation_ui)
	investigation_ui.action_chosen.connect(func(aid: String):
		ui_event.emit({"kind": "card_action", "id": aid})
	)

	settings_ui = SETTINGS_SCENE.instantiate()
	add_child(settings_ui)

func begin_shutdown() -> void:
	_closing = true

func is_busy() -> bool:
	if is_instance_valid(dialogue_ui) and dialogue_ui.is_dialogue_active():
		return true
	if is_instance_valid(investigation_ui) and investigation_ui.visible:
		return true
	if is_instance_valid(settings_ui) and settings_ui.visible:
		return true
	return false

func apply_snapshot(data: Dictionary) -> void:
	snapshot = data.duplicate(true)
	if is_instance_valid(dialogue_ui):
		dialogue_ui.configure_cast(snapshot.get("cast", {}))
	if is_instance_valid(hud_ui):
		hud_ui.apply_snapshot(snapshot)

func present(ev: Dictionary) -> Dictionary:
	if _closing or ev.is_empty(): return {}
	_trace_display = ev
	match str(ev.get("kind", "")):
		"lines":
			if is_instance_valid(dialogue_ui):
				dialogue_ui.start_dialogue(ev)
				var choice = await dialogue_ui.dialogue_finished
				return {"trace_id": str(ev.get("trace_id", "")), "choice": choice}
			return {}
		"aside":
			show_aside(str(ev.get("text", "")), float(ev.get("seconds", 2.5)))
		"flash", "title":
			show_flash(str(ev.get("text", "")), float(ev.get("seconds", 1.8)))
		"hint":
			set_hint(str(ev.get("text", "")))
		"card":
			open_card(ev)
		"ending":
			open_ending(ev)
		"music":
			Sound.music(str(ev.get("id", "")), float(ev.get("fade", 1.6)))
		"sound":
			Sound.sfx(str(ev.get("id", "")), float(ev.get("volume", 0.0)))
		"duck":
			Sound.duck(bool(ev.get("on", false)))
		"close", "interaction_closed":
			close_modal()
	return {}

func spatial_choices(input: Dictionary) -> void:
	if is_instance_valid(investigation_ui):
		investigation_ui.show_spatial_choices(input)

func close_modal() -> void:
	if is_instance_valid(investigation_ui) and investigation_ui.visible:
		investigation_ui.close()
	if is_instance_valid(settings_ui) and settings_ui.visible:
		settings_ui.close()

func open_notebook() -> void:
	if is_instance_valid(investigation_ui):
		investigation_ui.open_notebook(snapshot.get("notebook", {}))

func open_manuscript(_announce := true) -> void:
	if is_instance_valid(investigation_ui):
		investigation_ui.open_manuscript(snapshot.get("replay", {}))

func open_card(data: Dictionary) -> void:
	if is_instance_valid(investigation_ui):
		investigation_ui.open_card(data)

func open_campus_map() -> void:
	if is_instance_valid(investigation_ui):
		investigation_ui.open_campus_map(snapshot.get("map", {}))

func open_ending(data: Dictionary) -> void:
	if is_instance_valid(investigation_ui):
		investigation_ui.open_ending(data)

func open_settings(_page := "rules") -> void:
	if is_instance_valid(settings_ui):
		settings_ui.open()

func show_hover(text: String, pos: Vector2) -> void:
	if is_instance_valid(hud_ui):
		hud_ui.show_hover(text, pos)

func hide_hover() -> void:
	if is_instance_valid(hud_ui):
		hud_ui.hide_hover()

func set_hint(text: String) -> void:
	if is_instance_valid(hud_ui):
		hud_ui.set_hint(text)

func show_aside(text: String, duration := 2.5) -> void:
	if is_instance_valid(dialogue_ui):
		dialogue_ui.show_aside(text, duration)

func show_flash(text: String, duration := 1.8) -> void:
	if is_instance_valid(dialogue_ui):
		dialogue_ui.show_flash(text, duration)

func _unhandled_input(event: InputEvent) -> void:
	if _closing or not event.is_pressed() or event.is_echo(): return
	if event.is_action_pressed("cancel"):
		if is_busy():
			close_modal()
			get_viewport().set_input_as_handled()
		else:
			open_settings()
			get_viewport().set_input_as_handled()
		return
	if is_busy(): return
	if event.is_action_pressed("notebook"):
		open_notebook()
		get_viewport().set_input_as_handled()
	elif event.is_action_pressed("manuscript"):
		open_manuscript()
		get_viewport().set_input_as_handled()
	elif event.is_action_pressed("campus_map"):
		open_campus_map()
		get_viewport().set_input_as_handled()
	elif event.is_action_pressed("wait"):
		ui_event.emit({"kind": "action", "act": {"cmd": "stay"}})
		get_viewport().set_input_as_handled()
	elif event.is_action_pressed("settings"):
		open_settings()
		get_viewport().set_input_as_handled()

func view_trace_context() -> Dictionary:
	if is_instance_valid(settings_ui) and settings_ui.visible: return {"panel": "settings"}
	if is_instance_valid(investigation_ui) and investigation_ui.visible: return {"panel": "investigation"}
	if is_instance_valid(dialogue_ui) and dialogue_ui.is_dialogue_active(): return {"panel": "dialogue"}
	return {"panel": "field"}
