extends Control

## UI presentation and one modal owner; only pure data crosses the Host boundary.
signal ui_event(event: Dictionary)
signal step_dialogue
signal dialogue_option_selected(choice_id: String)
signal casebook_reading_changed(active: bool)

const Sound := preload("res://scenes/Shared/Audio/Sound.gd")
const Transition := preload("res://scenes/UI/UITransition.gd")
const CASEBOOK_SCRIPT := preload("res://scenes/UI/CasebookUI.gd")
const CASEBOOK_DATA := preload("res://scenes/UI/CasebookData.gd")
const HUD_SCENE := preload("res://scenes/UI/FieldHUD.tscn")
const INVESTIGATION_SCENE := preload("res://scenes/UI/InvestigationUI.tscn")
const DIALOGUE_SCENE := preload("res://scenes/UI/DialogueUI.tscn")

var settings_ui: CanvasLayer
var hud_ui: CanvasLayer
var investigation_ui: CanvasLayer
var dialogue_ui: CanvasLayer
var snapshot: Dictionary = {}
var casebook: RefCounted = CASEBOOK_DATA.new()
## The host can prepare and draw this UI under a title without accepting input.
var host_input_blocked := false
var start_covered := false
var _closing := false
var _trace_display: Dictionary = {}
var _pending_modal: Dictionary = {}
var _modal_cycle := false
var _modal_kind := ""
var _active_modal: Dictionary = {}
var _modal_stack: Array[Dictionary] = []
var _return_focus: WeakRef
var _spatial_context: Dictionary = {}
var _pending_action: Dictionary = {}
var _screen_motion = Transition.new()
var _archive_result: Dictionary = {}
var _archive_lines: Array = []
var _presentation_music_token := 0

@onready var _screen_layer: CanvasLayer = $TransitionLayer
@onready var _screen_fader: ColorRect = $TransitionLayer/ScreenFader

var hud: CanvasLayer:
	get: return hud_ui

var fader: ColorRect:
	get: return _screen_fader

var ui_busy: bool:
	get: return is_busy()

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_screen_motion.configure(self, _screen_layer, _screen_fader, null, start_covered, 1.0)
	_screen_motion.completed.connect(_on_screen_transition_completed)
	hud_ui = HUD_SCENE.instantiate()
	add_child(hud_ui)
	hud_ui.settings_requested.connect(open_settings)
	dialogue_ui = DIALOGUE_SCENE.instantiate()
	add_child(dialogue_ui)
	dialogue_ui.dialogue_step.connect(func():
		if not _closing: step_dialogue.emit()
	)
	dialogue_ui.choice_selected.connect(func(cid: String):
		if _closing: return
		dialogue_option_selected.emit(cid)
		ui_event.emit({"kind": "choice", "id": cid})
	)
	dialogue_ui.dialogue_finished.connect(func(_last_choice: String):
		if not _closing:
			step_dialogue.emit()
			_flush_pending_action()
	)
	dialogue_ui.history_closed.connect(_activate_pending_modal)
	dialogue_ui.line_read.connect(_on_dialogue_line_read)
	investigation_ui = INVESTIGATION_SCENE.instantiate()
	add_child(investigation_ui)
	investigation_ui.restore_previous_focus = false
	investigation_ui.action_chosen.connect(_on_investigation_action)
	investigation_ui.closed.connect(_on_modal_closed)
	settings_ui = CASEBOOK_SCRIPT.new()
	settings_ui.archive = casebook
	add_child(settings_ui)
	settings_ui.restore_previous_focus = false
	settings_ui.closed.connect(_on_modal_closed)
	settings_ui.reopen_requested.connect(open_settings)
	settings_ui.reading_changed.connect(func(active: bool): casebook_reading_changed.emit(active))
	settings_ui.ui_event.connect(_on_book_event)
	var settings := get_node_or_null("/root/GameSettings")
	if settings != null: settings.setting_changed.connect(_on_setting_changed)
	_sync_overlay_input()

func set_host_input_blocked(blocked: bool) -> void:
	host_input_blocked = blocked
	if is_node_ready(): _sync_overlay_input()

func prepare_for_reveal() -> bool:
	if _closing: return false
	if not await dialogue_ui.wait_for_cast_prepared(): return false
	if _closing or not is_inside_tree(): return false
	hud_ui.finish_entrance()
	return true

func begin_shutdown() -> void:
	if _closing: return
	_closing = true
	Sound.end_presentation_music(_presentation_music_token)
	_presentation_music_token = 0
	_pending_modal.clear()
	_modal_stack.clear()
	_active_modal.clear()
	_pending_action.clear()
	_return_focus = null
	_screen_motion.dispose()
	settings_ui.begin_shutdown()
	investigation_ui.begin_shutdown()
	dialogue_ui.begin_shutdown()
	hud_ui.begin_shutdown()

func _exit_tree() -> void:
	Sound.end_presentation_music(_presentation_music_token)
	_presentation_music_token = 0
	_screen_motion.dispose()

func is_busy() -> bool:
	return _closing or host_input_blocked or _screen_motion.is_active() or _modal_cycle or not _pending_modal.is_empty() \
		or (is_instance_valid(dialogue_ui) and dialogue_ui.is_dialogue_active()) \
		or (is_instance_valid(investigation_ui) and investigation_ui.is_active()) \
		or (is_instance_valid(settings_ui) and settings_ui.is_active())

## These methods only cover/uncover the screen. They never step or replace text.
func fade_out() -> bool:
	if _closing: return false
	var request_id: int = _screen_motion.request(true, true, 0.16)
	_sync_overlay_input()
	return await _screen_motion.wait_for(request_id)

func fade_in() -> bool:
	if _closing: return false
	var request_id: int = _screen_motion.request(false, true, 0.20)
	_sync_overlay_input()
	return await _screen_motion.wait_for(request_id)

func transition_out() -> bool:
	return await fade_out()

func transition_in() -> bool:
	if _closing: return false
	if not _screen_motion.is_active(): _screen_motion.request(true, false)
	return await fade_in()

func _on_screen_transition_completed(_request_id: int, shown: bool, cancelled: bool) -> void:
	if _closing or cancelled: return
	_sync_overlay_input()
	if not shown: _activate_pending_modal()

func _on_setting_changed(section: String, key: String, value: Variant) -> void:
	if section == "accessibility" and key == "reduce_motion" and bool(value): _screen_motion.finish_current()

func apply_snapshot(data: Dictionary) -> void:
	# Main publishes completed projections. UI reads them without mutating or
	# repeatedly deep-copying the whole snapshot for a handful of display fields.
	snapshot = data
	casebook.apply_snapshot(snapshot)
	if is_instance_valid(dialogue_ui): dialogue_ui.configure_cast(snapshot.get("cast", {}))
	if is_instance_valid(hud_ui): hud_ui.apply_snapshot(snapshot)
	if not _closing: Sound.scene_music(snapshot)

func present(ev: Dictionary) -> Dictionary:
	if _closing or ev.is_empty(): return {}
	_trace_display = ev
	match str(ev.get("kind", "")):
		"lines":
			_presentation_music_token = Sound.begin_presentation_music(_archive_result)
			var music_token := _presentation_music_token
			var result: Dictionary = await dialogue_ui.play_dialogue(ev)
			Sound.end_presentation_music(music_token)
			if _presentation_music_token == music_token: _presentation_music_token = 0
			result["trace_id"] = str(ev.get("trace_id", ""))
			return result
		"aside": show_aside(str(ev.get("text", "")), float(ev.get("seconds", 2.5)))
		"flash", "title": show_flash(str(ev.get("text", "")), float(ev.get("seconds", 1.8)))
		"hint": set_hint(str(ev.get("text", "")))
		"card": open_card(ev)
		"ending": open_ending(ev)
		"music": Sound.music(str(ev.get("id", "")), float(ev.get("fade", 1.6)))
		"sound": Sound.sfx(str(ev.get("id", "")), float(ev.get("volume", 0.0)))
		"duck": Sound.duck(bool(ev.get("on", false)))
		"close", "interaction_closed": close_modal(false)
	return {}

func _request_modal(kind: String, data: Dictionary = {}) -> void:
	if _closing or host_input_blocked or not _pending_action.is_empty(): return
	# Settings is a child reading layer. Preserve the sheet beneath it, including
	# its reading position; returning must not dispatch or re-run any world action.
	if kind == "settings" and _modal_kind != "settings" and investigation_ui.is_open() and not _active_modal.is_empty():
		var previous := _active_modal.duplicate(false)
		previous["view_state"] = investigation_ui.capture_view_state()
		_modal_stack.append(previous)
	elif kind != "settings":
		# Explicit navigation replaces the current reading route. A manuscript
		# opened from the book must not reveal an obsolete copy when it closes.
		_modal_stack.clear()
	if not _modal_cycle:
		var focused := get_viewport().gui_get_focus_owner()
		_return_focus = weakref(focused) if focused != null else null
	_modal_cycle = true
	_pending_modal = {"kind": kind, "data": data}
	_sync_overlay_input()
	if settings_ui.is_active():
		if kind in ["settings", "notebook", "card", "bookmarks"]:
			_pending_modal.clear()
			_modal_kind = kind
			settings_ui.open(_book_page(kind, data))
		else:
			settings_ui.close()
	elif investigation_ui.is_active():
		investigation_ui.close()
	else:
		_activate_pending_modal()

func _activate_pending_modal() -> void:
	if _closing or host_input_blocked or _screen_motion.is_active(): return
	if settings_ui.is_active() or investigation_ui.is_active(): return
	if _pending_modal.is_empty():
		_release_modal()
		return
	if dialogue_ui.is_history_active():
		dialogue_ui.close_history()
		return
	var request := _pending_modal
	_pending_modal = {}
	_modal_kind = str(request.kind)
	_active_modal = request
	var data: Dictionary = request.data
	_spatial_context = {}
	match _modal_kind:
		"settings", "notebook", "bookmarks", "card": settings_ui.open(_book_page(_modal_kind, data))
		"manuscript": investigation_ui.open_manuscript(data)
		"campus_map": investigation_ui.open_campus_map(data)
		"ending": investigation_ui.open_ending(data)
		"spatial":
			_spatial_context = data
			investigation_ui.show_spatial_choices(data)
	if request.has("view_state") and _modal_kind != "settings":
		investigation_ui.restore_view_state(request.view_state)
	_sync_overlay_input()

func _on_modal_closed() -> void:
	if _closing: return
	_spatial_context = {}
	if _pending_modal.is_empty() and not _modal_stack.is_empty() and _pending_action.is_empty():
		_pending_modal = _modal_stack.pop_back()
		if str(_pending_modal.get("kind", "")) == "manuscript":
			# A note written in the child book is immediately visible on return.
			var restored_data: Dictionary = _pending_modal.data.duplicate(true)
			restored_data.personal_notes = casebook.notes.duplicate(true)
			_pending_modal.data = restored_data
	_activate_pending_modal()

func _release_modal() -> void:
	if _screen_motion.is_active() or settings_ui.is_active() or investigation_ui.is_active() or not _pending_modal.is_empty(): return
	_modal_cycle = false
	_modal_kind = ""
	_active_modal.clear()
	_sync_overlay_input()
	var restored := false
	if _return_focus != null:
		var control := _return_focus.get_ref() as Control
		if Transition.can_focus(control):
			control.grab_focus()
			restored = true
	_return_focus = null
	if not restored: dialogue_ui.restore_focus()
	_flush_pending_action()

func _sync_overlay_input() -> void:
	var screen_blocked: bool = host_input_blocked or _screen_motion.is_active()
	dialogue_ui.set_modal_blocked(_modal_cycle or screen_blocked)
	hud_ui.set_overlay_blocked(_modal_cycle or screen_blocked)
	settings_ui.set_external_blocked(screen_blocked)
	investigation_ui.set_external_blocked(screen_blocked)

func _on_investigation_action(id: String) -> void:
	if _closing or not _pending_action.is_empty(): return
	_pending_modal.clear()
	_modal_stack.clear()
	if _modal_kind == "spatial":
		# Use only the site and currently offered IDs from the real field event.
		# WorldView.spatial.select rechecks the current qualification and contact.
		for option: Dictionary in _spatial_context.get("options", []):
			if str(option.get("id", "")) == id and bool(option.get("available", true)):
				_pending_action = {"kind": "spatial_select", "choice_id": id, "site": str(_spatial_context.get("site", ""))}
				return
	else:
		_pending_action = {"kind": "card_action", "id": id}

func _flush_pending_action() -> void:
	if _closing or host_input_blocked or _pending_action.is_empty() or _modal_cycle or _screen_motion.is_active(): return
	if str(_pending_action.kind) == "spatial_select" and dialogue_ui.is_dialogue_active(): return
	# The field rechecks ui_busy. Release the departing modal before dispatching
	# its single captured choice, so the legitimate selection is not rejected.
	var action := _pending_action
	_pending_action = {}
	ui_event.emit(action)

func spatial_choices(data: Dictionary) -> void:
	_request_modal("spatial", data)

func close_modal(return_to_parent := true) -> void:
	_pending_modal.clear()
	if not return_to_parent: _modal_stack.clear()
	settings_ui.close()
	investigation_ui.close()
	if not settings_ui.is_active() and not investigation_ui.is_active(): _release_modal()

func open_notebook() -> void:
	_request_modal("notebook", snapshot.get("notebook", {}))

func open_manuscript(_announce := true) -> void:
	var data: Dictionary = snapshot.get("replay", {}).duplicate(true)
	data.personal_notes = casebook.notes.duplicate(true)
	_request_modal("manuscript", data)

func open_card(data: Dictionary) -> void:
	var entry_id: String = casebook.record_card(data)
	_request_modal("card", {"entry_id": entry_id})

func open_campus_map() -> void:
	var map_value: Variant = snapshot.get("map", {})
	var map_data: Dictionary = {}
	if map_value is Dictionary:
		map_data = map_value
	elif map_value is String or map_value is StringName:
		# The current snapshot's map is a location ID, not authored map prose.
		map_data = {"id": str(map_value)}
	_request_modal("campus_map", map_data)

func open_ending(data: Dictionary) -> void:
	_request_modal("ending", data)

func open_settings(_page := "rules") -> void:
	_request_modal("settings")

func _book_page(kind: String, data: Dictionary) -> String:
	match kind:
		"settings": return "settings"
		"bookmarks": return "load"
		"card": return "entry:" + str(data.get("entry_id", ""))
	return "index"

func _on_book_event(event: Dictionary) -> void:
	match str(event.get("kind", "")):
		"bookmark_save", "bookmark_load": ui_event.emit(event)
		"open_reasoning": open_manuscript()
		"card_action":
			_on_investigation_action(str(event.get("id", "")))
			settings_ui.close()

func show_bookmark_result(message: String, success := false) -> void:
	settings_ui.show_bookmark_result(message, success)

func set_bookmark_loading(blocked: bool) -> void:
	settings_ui.set_bookmark_loading(blocked)

func prepare_presentation(result: Dictionary) -> void:
	_archive_result = result
	_archive_lines = []

func _on_dialogue_line_read(line: Dictionary) -> void:
	if _archive_result.is_empty(): return
	_archive_lines.append(line.duplicate(true))
	casebook.record_presentation(_archive_result, _archive_lines)


func show_hover(text: String, pos: Vector2) -> void:
	if not _modal_cycle: hud_ui.show_hover(text, pos)

func hide_hover() -> void:
	hud_ui.hide_hover()

func set_hint(text: String) -> void:
	hud_ui.set_hint(text)

func show_aside(text: String, duration := 2.5) -> void:
	dialogue_ui.show_aside(text, duration)

func show_flash(text: String, duration := 1.8) -> void:
	dialogue_ui.show_flash(text, duration)

func _input(event: InputEvent) -> void:
	# Leave title input to the still-visible title; our controls are disabled.
	if host_input_blocked: return
	if _screen_motion.is_active() and event.is_pressed(): get_viewport().set_input_as_handled()

func _unhandled_input(event: InputEvent) -> void:
	if _closing or host_input_blocked or not event.is_pressed() or event.is_echo(): return
	if event.is_action_pressed("cancel") or event.is_action_pressed("ui_cancel"):
		if _modal_cycle: close_modal()
		elif not dialogue_ui.is_dialogue_active(): open_settings()
		get_viewport().set_input_as_handled()
		return
	if event.is_action_pressed("settings"):
		open_settings()
		get_viewport().set_input_as_handled()
		return
	if _modal_cycle:
		# A queued switch also owns input while the previous panel is closing.
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

func view_trace_context() -> Dictionary:
	if settings_ui.is_active(): return {"panel": "casebook", "page": str(settings_ui.book.get("_current_page")) if is_instance_valid(settings_ui.book) else _modal_kind, "transition": settings_ui.transition_state()}
	if investigation_ui.is_active(): return {"panel": "investigation", "page": _modal_kind, "transition": investigation_ui.transition_state()}
	if dialogue_ui.is_dialogue_active(): return {"panel": "dialogue"}
	return {"panel": "field", "modal_pending": not _pending_modal.is_empty(), "screen_transition": _screen_motion.is_active()}
