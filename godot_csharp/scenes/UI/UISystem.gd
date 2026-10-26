extends Control

## UI presentation and one modal owner; only pure data crosses the Host boundary.
signal ui_event(event: Dictionary)
signal step_dialogue
signal casebook_reading_changed(active: bool)

const Sound := preload("res://scenes/Shared/Audio/Sound.gd")
const Transition := preload("res://scenes/UI/UITransition.gd")
const UIInput := preload("res://scenes/Shared/Input/UIInput.gd")
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
var _player_options: Array = []
var _screen_motion = Transition.new()
var _archive_result: Dictionary = {}
var _archive_lines: Array = []
var _presentation_music_token := 0
var _host_input_enabled := true
var _known_rooms: Dictionary = {}
var _known_connections: Dictionary = {}
var _casebook_trace_data: Dictionary = {}
var _casebook_trace_dirty := true
var _replay_mode := false
var _replay_context_serial := 0

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
	hud_ui.settings_requested.connect(func():
		if _host_input_enabled: open_settings()
	)
	dialogue_ui = DIALOGUE_SCENE.instantiate()
	add_child(dialogue_ui)
	dialogue_ui.dialogue_step.connect(func():
		if not _closing: step_dialogue.emit()
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
	settings_ui.pause_world = not _replay_mode
	settings_ui.replay_mode = _replay_mode
	add_child(settings_ui)
	settings_ui.restore_previous_focus = false
	settings_ui.closed.connect(_on_modal_closed)
	settings_ui.reopen_requested.connect(open_settings)
	settings_ui.reading_changed.connect(func(active: bool): casebook_reading_changed.emit(active))
	settings_ui.ui_event.connect(_on_book_event)
	casebook.changed.connect(func(): _casebook_trace_dirty = true)
	var settings := get_node_or_null("/root/GameSettings")
	if settings != null: settings.setting_changed.connect(_on_setting_changed)
	_sync_overlay_input()

func set_host_input_blocked(blocked: bool) -> void:
	host_input_blocked = blocked
	if is_node_ready():
		_sync_overlay_input()
		if not blocked and not _closing: Sound.scene_music(snapshot)

func prepare_for_reveal() -> bool:
	if _closing: return false
	if not await dialogue_ui.wait_for_cast_prepared(): return false
	if _closing or not is_inside_tree(): return false
	hud_ui.finish_entrance()
	return true
func set_host_input_enabled(enabled: bool) -> void:
	# Main can prepare this UI behind the opening page. It may render, but must
	# not capture the title's input or expose a second active menu beforehand.
	_host_input_enabled = enabled
	if is_node_ready(): _sync_overlay_input()

func prepare_shutdown_drain() -> void:
	# Retain page/transition resources until in-flight field work has settled.
	set_host_input_blocked(true)
	settings_ui.release_world_pause()

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
	if str(snapshot.get("viewer", "")) != str(data.get("viewer", "")):
		_known_rooms.clear()
		_known_connections.clear()
	snapshot = data
	_learn_map(data.get("pending", {}).get("options", []))
	casebook.apply_snapshot(snapshot)
	if is_instance_valid(dialogue_ui): dialogue_ui.configure_cast(snapshot.get("cast", {}))
	if is_instance_valid(hud_ui): hud_ui.apply_snapshot(snapshot)
	if not host_input_blocked and not _closing: Sound.scene_music(snapshot)

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

func _request_modal(kind: String, data: Dictionary = {}, view_state: Dictionary = {}) -> void:
	if _closing or host_input_blocked or not _host_input_enabled or not _pending_action.is_empty(): return
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
	if not view_state.is_empty(): _pending_modal["view_state"] = view_state
	_sync_overlay_input()
	if settings_ui.is_active():
		if kind in ["settings", "notebook", "card", "bookmarks"]:
			_pending_modal.clear()
			_modal_kind = kind
			_active_modal = {"kind": kind, "data": data}
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
		"manuscript":
			_spatial_context = {"site": "self", "options": data.get("options", [])}
			investigation_ui.open_manuscript(data)
		"campus_map": investigation_ui.open_campus_map(data)
		"ending": investigation_ui.open_ending(data)
		"spatial":
			_spatial_context = data
			investigation_ui.show_spatial_choices(data)
	if request.has("view_state") and _modal_kind not in ["settings", "notebook", "bookmarks", "card"]:
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
	var screen_blocked: bool = host_input_blocked or not _host_input_enabled or _screen_motion.is_active()
	dialogue_ui.set_modal_blocked(_modal_cycle or screen_blocked)
	hud_ui.set_overlay_blocked(_modal_cycle or screen_blocked)
	settings_ui.set_external_blocked(screen_blocked)
	investigation_ui.set_external_blocked(screen_blocked)

func _on_investigation_action(id: String) -> void:
	if _closing or not _pending_action.is_empty(): return
	var action_id := id
	if id.begins_with("{"):
		var envelope: Variant = JSON.parse_string(id)
		if envelope is not Dictionary or not envelope.get("action_id") is String or not envelope.get("hypothesis_id") is String: return
		action_id = str(envelope.action_id)
	_pending_modal.clear()
	_modal_stack.clear()
	if _modal_kind in ["spatial", "manuscript"]:
		# Use only the site and currently offered IDs from the real field event.
		# WorldView.spatial.select rechecks the current qualification and contact.
		for option: Dictionary in _spatial_context.get("options", []):
			if str(option.get("id", "")) == action_id and bool(option.get("available", true)):
				_pending_action = {"kind": "spatial_select", "choice_id": id, "site": str(_spatial_context.get("site", ""))}
				return

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

func set_player_options(options: Array) -> void:
	# IDs come from the current Prepare result. Opening a page never creates
	# actions or admits a hypothesis; the field checks selection again.
	_player_options = options.duplicate(true)
	_learn_map(_player_options)

func open_manuscript(_announce := true) -> void:
	var data: Dictionary = snapshot.get("replay", {}).duplicate(true)
	data.personal_notes = casebook.notes.duplicate(true)
	var choices: Array = []
	for option: Dictionary in _player_options:
		var binding: Dictionary = option.get("interaction", {})
		if str(binding.get("kind", "")) == "self" and str(binding.get("purpose", "")) != "wait": choices.append(option)
	data.options = choices
	_request_modal("manuscript", data)

func open_card(data: Dictionary) -> void:
	var entry_id: String = casebook.record_card(data)
	_request_modal("card", {"entry_id": entry_id})

func _learn_map(choices: Array = []) -> void:
	var map_id := str(snapshot.get("map", ""))
	var room_id := str(snapshot.get("execution_room", ""))
	if map_id.is_empty() or room_id.is_empty(): return
	if not _known_rooms.has(map_id): _known_rooms[map_id] = {}
	_known_rooms[map_id][room_id] = {"label": str(snapshot.get("hud", {}).get("room", room_id)), "visited": true}
	for room: Dictionary in snapshot.get("map_knowledge", {}).get("rooms", []):
		var known_map := str(room.map)
		if not _known_rooms.has(known_map): _known_rooms[known_map] = {}
		var known_id := str(room.room)
		var visited := bool(_known_rooms[known_map].get(known_id, {}).get("visited", false))
		_known_rooms[known_map][known_id] = {"label": str(room.label), "visited": visited}
	for door: Dictionary in snapshot.get("map_knowledge", {}).get("doors", []):
		var from: Dictionary = door.from
		var to: Dictionary = door.to
		var target := str(to.room) if str(to.map) == str(from.map) else str(to.map)
		_known_connections[str(from.map) + ":" + str(from.room) + ">" + target] = {"map": str(from.map), "from": str(from.room), "to": target, "label": str(from.label) + " → " + str(to.label)}
	for option: Dictionary in choices:
		var interaction: Dictionary = option.get("interaction", {})
		if str(interaction.get("kind", "")) != "doorway" or not bool(option.get("available", true)): continue
		var destination: Dictionary = interaction.destination
		var next_map := str(destination.map)
		var next_room := str(destination.get("execution_room", destination.get("room", "")))
		if not _known_rooms.has(next_map): _known_rooms[next_map] = {}
		if not _known_rooms[next_map].has(next_room):
			_known_rooms[next_map][next_room] = {"label": str(option.get("label", "")), "visited": false}
		var to := next_room if next_map == map_id else next_map
		_known_connections[map_id + ":" + room_id + ">" + to] = {"map": map_id, "from": room_id, "to": to, "label": str(option.get("label", ""))}

func open_campus_map() -> void:
	_learn_map(_player_options)
	var map_id := str(snapshot.get("map", ""))
	var connections: Array = []
	for connection: Dictionary in _known_connections.values():
		if str(connection.map) == map_id: connections.append(connection)
	_request_modal("campus_map", {"id": map_id, "current_room": str(snapshot.get("execution_room", "")),
		"current_label": str(snapshot.get("hud", {}).get("room", "")),
		"rooms": _known_rooms.get(map_id, {}).duplicate(true), "connections": connections})

func open_ending(data: Dictionary) -> void:
	_request_modal("ending", data)

func open_settings(_page := "rules") -> void:
	_request_modal("settings")

func _book_page(kind: String, data: Dictionary) -> String:
	if data.has("page"): return str(data.page)
	match kind:
		"settings": return "settings"
		"bookmarks": return "load"
		"card": return "entry:" + str(data.get("entry_id", ""))
	return "index"

func _on_book_event(event: Dictionary) -> void:
	match str(event.get("kind", "")):
		"bookmark_save", "bookmark_load": ui_event.emit(event)
		"open_reasoning": open_manuscript()

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
	if host_input_blocked or not _host_input_enabled: return
	if _closing or _screen_motion.is_active():
		get_viewport().set_input_as_handled()
		return
	# Children receive _input first: the book, modal sheet or history closes one
	# level and consumes the event. Capture the remaining back input before GUI
	# Controls can swallow a right-click and before the field can see it.
	if UIInput.is_back(event, false):
		if UIInput.is_back(event):
			if _modal_cycle: close_modal()
			elif not dialogue_ui.is_dialogue_active(): open_settings()
		get_viewport().set_input_as_handled()

func _unhandled_input(event: InputEvent) -> void:
	if _closing or host_input_blocked or not _host_input_enabled or not event.is_pressed() or event.is_echo(): return
	if UIInput.is_back(event):
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
		ui_event.emit({"kind": "request_wait"})
		get_viewport().set_input_as_handled()

func view_trace_context() -> Dictionary:
	if _casebook_trace_dirty and (settings_ui.is_active() or investigation_ui.is_active()):
		_casebook_trace_data = casebook.export_data()
		_casebook_trace_dirty = false
	if settings_ui.is_active():
		var context: Dictionary = settings_ui.book.page_trace_context()
		context.transition = settings_ui.transition_state()
		context.modal_kind = _modal_kind
		context.modal_data = _active_modal.get("data", {}).duplicate(true)
		context.casebook = _casebook_trace_data
		context.book_state = settings_ui.book.capture_replay_state()
		return context
	if investigation_ui.is_active():
		return {"panel": "spatial" if _modal_kind == "spatial" else "investigation", "page": _modal_kind,
			"presentation_id": str(_spatial_context.get("interaction_id", "")), "transition": investigation_ui.transition_state(),
			"modal_kind": _modal_kind, "modal_data": _active_modal.get("data", {}).duplicate(true),
			"view_state": investigation_ui.capture_view_state(), "casebook": _casebook_trace_data}
	if dialogue_ui.is_dialogue_active():
		return {"panel": "dialogue", "presentation_id": str(_trace_display.get("presentation_id", ""))}
	return {"panel": "field", "modal_pending": not _pending_modal.is_empty(), "screen_transition": _screen_motion.is_active()}

func set_replay_mode(enabled: bool) -> void:
	_replay_mode = enabled
	if is_instance_valid(settings_ui):
		settings_ui.pause_world = not enabled
		settings_ui.replay_mode = enabled

func restore_replay_context(context: Dictionary) -> void:
	set_replay_mode(true)
	_replay_context_serial += 1
	var serial := _replay_context_serial
	if context.get("casebook") is Dictionary:
		casebook.restore_data(context.casebook, str(snapshot.get("viewer", "")))
	var panel := str(context.get("panel", "field"))
	if panel == "casebook":
		var page := str(context.get("page", "index"))
		if not settings_ui.is_open() or str(settings_ui.book.page_trace_context().get("page", "")) != page:
			_request_modal(str(context.get("modal_kind", "notebook")), {"page": page})
		await get_tree().process_frame
		while serial == _replay_context_serial and not _closing and (not settings_ui.is_open() or settings_ui.transition_state() == "turning"):
			await get_tree().process_frame
		if serial == _replay_context_serial and not _closing:
			await settings_ui.book.restore_replay_state(context.get("book_state", {"page": page}))
	elif panel in ["investigation", "spatial"]:
		var kind := str(context.get("modal_kind", context.get("page", "spatial")))
		var data: Dictionary = context.get("modal_data", {})
		var view_state: Dictionary = context.get("view_state", {})
		if investigation_ui.is_open() and _modal_kind == kind and _active_modal.get("data", {}) == data:
			investigation_ui.restore_view_state(view_state)
		else:
			_request_modal(kind, data, view_state)
	elif panel in ["field", "dialogue"]:
		close_modal(false)
