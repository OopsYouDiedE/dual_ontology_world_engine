@tool
extends Control

## UI presentation and one modal owner; only pure data crosses the Host boundary.
signal ui_event(event: Dictionary)
signal step_dialogue
signal casebook_reading_changed(active: bool)

const Sound := preload("res://scenes/components/shared/Sound.gd")
const Transition := preload("res://scenes/UIpanel/CasebookUI/UITransition.gd")
const UIInput := preload("res://scenes/UIpanel/UIInput.gd")
const CASEBOOK_SCRIPT := preload("res://scenes/UIpanel/CasebookUI.gd")
const CASEBOOK_DATA := preload("res://scenes/UIpanel/CasebookUI/CasebookData.gd")
const HUD_SCENE := preload("res://scenes/UIpanel/FieldHUD.tscn")
const INVESTIGATION_SCENE := preload("res://scenes/UIpanel/EvidenceArchiveUI/InvestigationUI.tscn")
const DIALOGUE_SCENE := preload("res://scenes/UIpanel/DialogueUI.tscn")
const INTERACTION_MENU_SCENE := preload("res://scenes/UIpanel/InteractionMenu.tscn")
const ACTION_QUEUE_SCENE := preload("res://scenes/UIpanel/FieldHUD/CurrentActionUI.tscn")
const DEDUCTION_SCENE := preload("res://scenes/UIpanel/DeductionInterface.tscn")
const EVIDENCE_INSPECT_SCENE := preload("res://scenes/UIpanel/EvidenceArchiveUI/EvidenceInspectUI.tscn")
const INVENTORY_SCENE := preload("res://scenes/UIpanel/EvidenceArchiveUI/InventoryUI.tscn")
const COLLABORATION_SCENE := preload("res://scenes/UIpanel/EventScheduleUI/CollaborationUI.tscn")
const ALERT_SCENE := preload("res://scenes/UIpanel/AlertUI.tscn")
const TEXT_COMPARISON_SCENE := preload("res://scenes/UIpanel/EvidenceArchiveUI/TextComparisonUI.tscn")
const CONVENTION_SCENE := preload("res://scenes/UIpanel/ConventionUI.tscn")
const SNACKBAR_SCRIPT := preload("res://scenes/UIpanel/TooltipGuideUI/Snackbar.gd")

var settings_ui: CanvasLayer
var hud_ui: CanvasLayer
var investigation_ui: CanvasLayer
var dialogue_ui: CanvasLayer
var interaction_menu: Control
var action_queue_panel: Control
var deduction_interface: Control
var evidence_inspect_ui: Control
var inventory_ui: Control
var collaboration_ui: Control
var alert_ui: Control
var text_comparison_ui: Control
var convention_ui: Control
var global_snackbar: PanelContainer
var snapshot: Dictionary = {}
var casebook: RefCounted = CASEBOOK_DATA.new()
## The host can prepare and draw this UI under a title without accepting input.
var host_input_blocked: bool = false
var start_covered: bool = false
var _closing: bool = false
var _trace_display: Dictionary = {}
var _pending_modal: Dictionary = {}
var _modal_cycle: bool = false
var _modal_kind: String = ""
var _active_modal: Dictionary = {}
var _modal_stack: Array[Dictionary] = []
var _return_focus: WeakRef
var _spatial_context: Dictionary = {}
var _pending_action: Dictionary = {}
var _player_options: Array = []
var _screen_motion = Transition.new()
var _archive_result: Dictionary = {}
var _archive_lines: Array = []
var _presentation_music_token: int = 0
var _host_input_enabled: bool = true
var _known_rooms: Dictionary = {}
var _known_connections: Dictionary = {}
var _casebook_trace_data: Dictionary = {}
var _casebook_trace_dirty: bool = true
var _replay_mode: bool = false
var _replay_context_serial: int = 0
var _book_knowledge_signature: int = 0
var _book_viewer: String = ""
var _inspection_active: bool = false
var _returning_title: bool = false

@onready var _screen_layer: CanvasLayer = $TransitionLayer
@onready var _screen_fader: ColorRect = $TransitionLayer/ScreenFader

var hud: CanvasLayer:
	get: return hud_ui

var fader: ColorRect:
	get: return _screen_fader

var ui_busy: bool:
	get: return is_busy()

func _ready() -> void:
	if Engine.is_editor_hint():
		add_child(HUD_SCENE.instantiate())
		return
	process_mode = Node.PROCESS_MODE_ALWAYS
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_screen_motion.configure(self, _screen_layer, _screen_fader, null, start_covered, 1.0)
	_screen_motion.completed.connect(_on_screen_transition_completed)
	hud_ui = HUD_SCENE.instantiate()
	add_child(hud_ui)
	hud_ui.settings_requested.connect(func():
		if _host_input_enabled: open_settings()
	)
	hud_ui.book_open_requested.connect(func():
		if _host_input_enabled: open_notebook()
	)
	hud_ui.action_cancelled.connect(_on_action_cancel_requested)
	hud_ui.perception_inspect_requested.connect(_on_perception_inspect)
	dialogue_ui = DIALOGUE_SCENE.instantiate()
	add_child(dialogue_ui)
	dialogue_ui.dialogue_step.connect(func():
		if not _closing: step_dialogue.emit()
	)
	dialogue_ui.dialogue_finished.connect(func(_last_choice: String):
		if not _closing and not _inspection_active:
			step_dialogue.emit()
			_flush_pending_action()
	)
	dialogue_ui.history_closed.connect(_activate_pending_modal)
	dialogue_ui.line_read.connect(_on_dialogue_line_read)
	dialogue_ui.inspection_closed.connect(_on_perception_closed)
	dialogue_ui.evidence_presented.connect(_on_evidence_presented)
	investigation_ui = INVESTIGATION_SCENE.instantiate()
	add_child(investigation_ui)
	investigation_ui.restore_previous_focus = false
	investigation_ui.action_chosen.connect(_on_investigation_action)
	investigation_ui.reasoning_requested.connect(func(request: Dictionary, serial: int):
		if not _closing: ui_event.emit({"kind": "case_reasoning_ui", "request": request, "serial": serial})
	)
	investigation_ui.closed.connect(_on_modal_closed)
	settings_ui = CASEBOOK_SCRIPT.new()
	settings_ui.archive = casebook
	settings_ui.pause_world = false
	settings_ui.replay_mode = _replay_mode
	add_child(settings_ui)
	settings_ui.restore_previous_focus = false
	settings_ui.closed.connect(_on_modal_closed)
	settings_ui.reopen_requested.connect(open_settings)
	settings_ui.reading_changed.connect(func(active: bool): casebook_reading_changed.emit(active))
	settings_ui.ui_event.connect(_on_book_event)
	_configure_book({}, true)
	casebook.changed.connect(func(): _casebook_trace_dirty = true)
	
	# Instantiate M3 components (InteractionMenu, ActionQueuePanel, DeductionInterface)
	interaction_menu = INTERACTION_MENU_SCENE.instantiate()
	add_child(interaction_menu)
	interaction_menu.action_selected.connect(_on_interaction_action_selected)
	interaction_menu.menu_closed.connect(_on_modal_closed)
	interaction_menu.blocked_feedback.connect(_on_blocked_feedback)
	interaction_menu.cost_preview_requested.connect(func(current: int, cost: int): hud_ui.show_cost_preview(current, cost))
	interaction_menu.cost_preview_cleared.connect(func(): hud_ui.hide_cost_preview())
	interaction_menu.pocket_item_used.connect(_on_pocket_item_used)
	interaction_menu.word_magic_applied.connect(_on_word_magic_applied)
	interaction_menu.parameter_action_requested.connect(_on_parameter_action_requested)
	
	action_queue_panel = ACTION_QUEUE_SCENE.instantiate()
	add_child(action_queue_panel)
	action_queue_panel.action_cancel_requested.connect(_on_action_cancel_requested)
	
	deduction_interface = DEDUCTION_SCENE.instantiate()
	add_child(deduction_interface)
	deduction_interface.deduction_submitted.connect(_on_deduction_submitted)
	deduction_interface.deduction_closed.connect(_on_modal_closed)
	
	evidence_inspect_ui = EVIDENCE_INSPECT_SCENE.instantiate()
	add_child(evidence_inspect_ui)
	evidence_inspect_ui.closed.connect(_on_evidence_inspect_closed)
	
	inventory_ui = INVENTORY_SCENE.instantiate()
	add_child(inventory_ui)
	inventory_ui.inventory_closed.connect(_on_modal_closed)
	inventory_ui.handover_requested.connect(_on_inventory_handover)
	inventory_ui.item_inspect_requested.connect(_on_inventory_item_inspect)
	
	collaboration_ui = COLLABORATION_SCENE.instantiate()
	add_child(collaboration_ui)
	collaboration_ui.invite_dispatched.connect(_on_collaboration_invite)
	collaboration_ui.arrangement_cancelled.connect(_on_collaboration_cancel)
	collaboration_ui.collaboration_closed.connect(_on_modal_closed)
	
	alert_ui = ALERT_SCENE.instantiate()
	add_child(alert_ui)
	alert_ui.evacuate_requested.connect(_on_hazard_evacuate)
	alert_ui.resolve_requested.connect(_on_hazard_resolve)
	alert_ui.alert_dismissed.connect(_on_hazard_dismissed)
	
	text_comparison_ui = TEXT_COMPARISON_SCENE.instantiate()
	add_child(text_comparison_ui)
	text_comparison_ui.comparison_closed.connect(_on_modal_closed)
	
	convention_ui = CONVENTION_SCENE.instantiate()
	add_child(convention_ui)
	convention_ui.opinion_updated.connect(_on_convention_opinion)
	convention_ui.convention_signed.connect(_on_convention_signed)
	convention_ui.convention_closed.connect(_on_modal_closed)
	
	global_snackbar = SNACKBAR_SCRIPT.new()
	global_snackbar.name = "GlobalSnackbar"
	global_snackbar.anchors_preset = Control.PRESET_BOTTOM_WIDE
	global_snackbar.anchor_top = 1.0
	global_snackbar.anchor_bottom = 1.0
	global_snackbar.offset_left = 120.0
	global_snackbar.offset_right = -120.0
	global_snackbar.offset_top = -68.0
	global_snackbar.offset_bottom = -20.0
	global_snackbar.grow_horizontal = Control.GROW_DIRECTION_BOTH
	global_snackbar.grow_vertical = Control.GROW_DIRECTION_BEGIN
	add_child(global_snackbar)
	
	var settings: Node = get_node_or_null("/root/GameSettings")
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
	if is_instance_valid(interaction_menu): interaction_menu.close_menu()
	if is_instance_valid(deduction_interface): deduction_interface.exit_deduction()
	if is_instance_valid(evidence_inspect_ui): evidence_inspect_ui.close_inspect()
	if is_instance_valid(inventory_ui): inventory_ui.close_inventory()
	if is_instance_valid(collaboration_ui): collaboration_ui.close_collaboration()
	if is_instance_valid(alert_ui): alert_ui.dismiss_alert()
	if is_instance_valid(text_comparison_ui): text_comparison_ui.close_comparison()
	if is_instance_valid(convention_ui): convention_ui.close_convention()
	if is_instance_valid(global_snackbar): global_snackbar.dismiss()

func _exit_tree() -> void:
	if Engine.is_editor_hint():
		return
	Sound.end_presentation_music(_presentation_music_token)
	_presentation_music_token = 0
	_screen_motion.dispose()

func pauses_world() -> bool:
	# Persistent HUD and selection availability do not open a reading layer.
	return _modal_cycle or not _pending_modal.is_empty() \
		or (is_instance_valid(dialogue_ui) and (dialogue_ui.is_dialogue_active() or dialogue_ui.is_history_active())) \
		or (is_instance_valid(investigation_ui) and investigation_ui.is_active()) \
		or (is_instance_valid(settings_ui) and settings_ui.is_active()) \
		or (is_instance_valid(interaction_menu) and interaction_menu.is_menu_open()) \
		or (is_instance_valid(deduction_interface) and deduction_interface.is_deduction_open()) \
		or (is_instance_valid(evidence_inspect_ui) and evidence_inspect_ui.is_inspect_open()) \
		or (is_instance_valid(inventory_ui) and inventory_ui.is_inventory_open()) \
		or (is_instance_valid(collaboration_ui) and collaboration_ui.is_collaboration_open()) \
		or (is_instance_valid(alert_ui) and alert_ui.is_alert_active()) \
		or (is_instance_valid(text_comparison_ui) and text_comparison_ui.is_comparison_open()) \
		or (is_instance_valid(convention_ui) and convention_ui.is_convention_open())

func is_busy() -> bool:
	return _closing or host_input_blocked or _screen_motion.is_active() or _modal_cycle or not _pending_modal.is_empty() \
		or (is_instance_valid(dialogue_ui) and dialogue_ui.is_dialogue_active()) \
		or (is_instance_valid(investigation_ui) and investigation_ui.is_active()) \
		or (is_instance_valid(settings_ui) and settings_ui.is_active()) \
		or (is_instance_valid(interaction_menu) and interaction_menu.is_menu_open()) \
		or (is_instance_valid(deduction_interface) and deduction_interface.is_deduction_open()) \
		or (is_instance_valid(evidence_inspect_ui) and evidence_inspect_ui.is_inspect_open()) \
		or (is_instance_valid(inventory_ui) and inventory_ui.is_inventory_open()) \
		or (is_instance_valid(collaboration_ui) and collaboration_ui.is_collaboration_open()) \
		or (is_instance_valid(text_comparison_ui) and text_comparison_ui.is_comparison_open()) \
		or (is_instance_valid(convention_ui) and convention_ui.is_convention_open())

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
	var old_room: String = str(snapshot.get("room", snapshot.get("current_room", "")))
	var new_room: String = str(data.get("room", data.get("current_room", "")))
	if not old_room.is_empty() and not new_room.is_empty() and old_room != new_room:
		if is_instance_valid(interaction_menu) and interaction_menu.is_menu_open():
			interaction_menu.close_menu()
		show_snackbar("抵达区域: " + new_room, "", 2.0)
	if str(snapshot.get("viewer", "")) != str(data.get("viewer", "")):
		_known_rooms.clear()
		_known_connections.clear()
	snapshot = data
	_learn_map()
	casebook.apply_snapshot(snapshot)
	_configure_book()
	if is_instance_valid(dialogue_ui): dialogue_ui.configure_cast(snapshot.get("cast", {}))
	if is_instance_valid(hud_ui): hud_ui.apply_snapshot(snapshot)
	if is_instance_valid(action_queue_panel):
		action_queue_panel.apply_queue_snapshot(snapshot.get("action_queue", {}))
	if not host_input_blocked and not _closing: Sound.scene_music(snapshot)

func _configure_book(route: Dictionary = {}, force: bool = false) -> void:
	if not is_instance_valid(settings_ui): return
	var data: Dictionary = snapshot.duplicate(false)
	var map_id: String = str(snapshot.get("map", ""))
	data["known_rooms"] = _known_rooms.get(map_id, {})
	var connections: Array = []
	for value: Dictionary in _known_connections.values():
		if str(value.get("from_map", "")) == map_id or str(value.get("to_map", "")) == map_id: connections.append(value)
	data["known_connections"] = connections
	data["allow_save"] = not _replay_mode and not str(snapshot.get("viewer", "")).is_empty()
	for key: String in ["entry_id", "related", "schedule_view", "convention"]:
		if route.has(key): data[key] = route[key]
	# Clock and animation snapshots must not rebuild the page's native controls.
	# The next tab always receives the latest projection, including its time.
	var signature: int = hash([data.get("viewer", ""), casebook.entries, data.get("casebook", {}),
		data.get("self", {}), data.get("protagonist", {}), data.get("inventory", {}),
		data.get("abnormals", []), data.get("active_contracts", []), data.get("appearance", ""),
		data.get("convention", {}), data.get("collaboration", {}), data.get("map_knowledge", {}),
		data.get("known_rooms", {}), data.get("native_save", {})])
	var viewer: String = str(data.get("viewer", ""))
	var same_viewer: bool = viewer == _book_viewer
	var editing_notes: bool = false
	if is_instance_valid(settings_ui.book) and settings_ui.is_active():
		editing_notes = str(settings_ui.book.page_trace_context().get("page", "")) == "B05"
	if not force and same_viewer and (signature == _book_knowledge_signature or editing_notes):
		settings_ui.snapshot = data
		if is_instance_valid(settings_ui.book): settings_ui.book.snapshot = data
	else:
		settings_ui.configure(data, casebook)
	_book_knowledge_signature = signature
	_book_viewer = viewer

func present(ev: Dictionary) -> Dictionary:
	if _closing or ev.is_empty(): return {}
	_trace_display = ev
	match str(ev.get("kind", "")):
		"lines":
			if _inspection_active:
				dialogue_ui.close_live_line()
				_inspection_active = false
			_presentation_music_token = Sound.begin_presentation_music(_archive_result)
			var music_token: int = _presentation_music_token
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
		"alert": show_alert(ev)
		"collaboration": open_collaboration(ev)
		"text_comparison", "compare_text": open_text_comparison(ev)
		"convention": open_convention(ev)
		"close", "interaction_closed": close_modal(false)
	return {}

func _request_modal(kind: String, data: Dictionary = {}, view_state: Dictionary = {}) -> void:
	if _closing or host_input_blocked or not _host_input_enabled or not _pending_action.is_empty(): return
	# Settings is a child reading layer. Preserve the sheet beneath it, including
	# its reading position; returning must not dispatch or re-run any world action.
	if kind == "settings" and _modal_kind != "settings" and investigation_ui.is_open() and not _active_modal.is_empty():
		var previous: Dictionary = _active_modal.duplicate(false)
		previous["view_state"] = investigation_ui.capture_view_state()
		_modal_stack.append(previous)
	elif kind != "settings":
		# Explicit navigation replaces the current reading route. A manuscript
		# opened from the book must not reveal an obsolete copy when it closes.
		_modal_stack.clear()
	if not _modal_cycle:
		var focused: Control = get_viewport().gui_get_focus_owner()
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
			_configure_book(data, true)
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
	var request: Dictionary = _pending_modal
	_pending_modal = {}
	_modal_kind = str(request.kind)
	_active_modal = request
	var data: Dictionary = request.data
	_spatial_context = {}
	match _modal_kind:
		"settings", "notebook", "bookmarks", "card":
			_configure_book(data, true)
			settings_ui.open(_book_page(_modal_kind, data))
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
	var restored: bool = false
	if _return_focus != null:
		var control: Control = _return_focus.get_ref() as Control
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
	if is_instance_valid(interaction_menu) and screen_blocked:
		interaction_menu.close_menu()
	if is_instance_valid(deduction_interface) and screen_blocked:
		deduction_interface.exit_deduction()
	if is_instance_valid(evidence_inspect_ui) and screen_blocked:
		evidence_inspect_ui.close_inspect()
	if is_instance_valid(inventory_ui) and screen_blocked:
		inventory_ui.close_inventory()
	if is_instance_valid(collaboration_ui) and screen_blocked:
		collaboration_ui.close_collaboration()
	if is_instance_valid(text_comparison_ui) and screen_blocked:
		text_comparison_ui.close_comparison()
	if is_instance_valid(convention_ui) and screen_blocked:
		convention_ui.close_convention()

func _on_investigation_action(id: String) -> void:
	if _closing or not _pending_action.is_empty(): return
	var action_id: String = id
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
	if is_instance_valid(interaction_menu) and interaction_menu.is_menu_open(): return
	# The field rechecks ui_busy. Release the departing modal before dispatching
	# its single captured choice, so the legitimate selection is not rejected.
	var action: Dictionary = _pending_action
	_pending_action = {}
	ui_event.emit(action)

func spatial_choices(data: Dictionary) -> void:
	if is_instance_valid(interaction_menu) and data.has("options"):
		_spatial_context = data
		interaction_menu.open_for_target(data)
	else:
		_request_modal("spatial", data)

func _on_interaction_action_selected(action_id: String, target_id: String) -> void:
	if _closing or host_input_blocked or not _host_input_enabled or not _pending_action.is_empty(): return
	var expected_target: String = str(_spatial_context.get("site", _spatial_context.get("target_id", _spatial_context.get("id", ""))))
	if target_id != expected_target or _offered_action(action_id).is_empty():
		_on_blocked_feedback("当前无法执行")
		return
	_pending_modal.clear()
	_modal_stack.clear()
	_pending_action = {"kind": "spatial_select", "choice_id": action_id, "site": target_id}
	_flush_pending_action()

func _offered_records(data: Dictionary, depth: int = 0) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	if depth > 8 or not bool(data.get("available", true)): return result
	for key: String in ["options", "actions", "children", "items", "pocket", "word_options", "words", "variants"]:
		var values: Variant = data.get(key, [])
		if values is not Array: continue
		for value in values:
			if value is not Dictionary or not bool(value.get("available", true)): continue
			result.append(value)
			result.append_array(_offered_records(value, depth + 1))
	return result

func _offered_action(id: String) -> Dictionary:
	if id.is_empty(): return {}
	for value: Dictionary in _offered_records(_spatial_context):
		if str(value.get("action_id", value.get("id", ""))) == id: return value
	return {}

func _on_pocket_item_used(target_id: String, item_id: String) -> void:
	if item_id.is_empty(): return
	for value: Dictionary in _offered_records(_spatial_context):
		if str(value.get("item_id", value.get("id", ""))) != item_id: continue
		var action_id: String = str(value.get("action_id", ""))
		if not action_id.is_empty():
			_on_interaction_action_selected(action_id, target_id)
			return
	_on_blocked_feedback("这件物品暂不可用")

func _on_word_magic_applied(target_id: String, formula: String) -> void:
	if formula.is_empty(): return
	for value: Dictionary in _offered_records(_spatial_context):
		if str(value.get("formula", "")) != formula: continue
		var action_id: String = str(value.get("action_id", ""))
		if not action_id.is_empty():
			_on_interaction_action_selected(action_id, target_id)
			return
	_on_blocked_feedback("这道言灵暂不可施加")

func _on_parameter_action_requested(action_id: String, target_id: String, parameters: Dictionary) -> void:
	var action: Dictionary = _offered_action(action_id)
	if not action.is_empty():
		var variants: Array[Dictionary] = [action]
		variants.append_array(_offered_records(action))
		for value: Dictionary in variants:
			var supplied: Variant = value.get("parameter_values", value.get("params"))
			if supplied is Dictionary and supplied == parameters:
				var offered_id: String = str(value.get("action_id", value.get("id", "")))
				if not offered_id.is_empty():
					_on_interaction_action_selected(offered_id, target_id)
					return
	_on_blocked_feedback("当前参数不可执行")

func _on_blocked_feedback(reason: String) -> void:
	if is_instance_valid(hud_ui) and hud_ui.has_method("set_hint"):
		hud_ui.set_hint(reason)

func _on_action_cancel_requested(action_id: String) -> void:
	if _closing or host_input_blocked or action_id.is_empty(): return
	ui_event.emit({"kind": "action_cancel", "action_id": action_id})
	show_snackbar("正在取消", "", 1.6)

func _on_perception_inspect(record: Dictionary) -> void:
	if _closing or host_input_blocked or not _host_input_enabled or _modal_cycle: return
	if dialogue_ui.is_dialogue_active(): return
	var description: String = str(record.get("description", record.get("text", "")))
	if description.is_empty(): return
	var data: Dictionary = record.duplicate(true)
	data["text"] = description
	if not data.has("title"): data["title"] = str(record.get("label", "现场记录"))
	_inspection_active = true
	dialogue_ui.show_inspection(data)

func _on_perception_closed(record: Dictionary) -> void:
	_inspection_active = false
	if _closing: return
	var data: Dictionary = record.duplicate(true)
	var heard: bool = str(data.get("kind", data.get("type", ""))) in ["heard", "sound", "offscreen"]
	if not data.has("category"): data["category"] = "testimony" if heard else "records"
	if not data.has("certainty"): data["certainty"] = "subjective_statement" if heard else "observation"
	casebook.record_card(data)
	dialogue_ui.show_archive_flight()
	_configure_book()
	_activate_pending_modal()
	_flush_pending_action()

func _on_evidence_presented(item_id: String, context: Dictionary) -> void:
	# The old host accepts prepared field choices, never an invented item action.
	var offered_id: String = ""
	for value in context.get("evidence_options", []):
		if value is Dictionary and str(value.get("item_id", "")) == item_id and bool(value.get("available", true)):
			offered_id = str(value.get("action_id", ""))
			break
	if offered_id.is_empty() or _offered_action(offered_id).is_empty():
		_on_blocked_feedback("此时无法出示")
		return
	_on_interaction_action_selected(offered_id, str(_spatial_context.get("site", "")))

func show_snackbar(message: String, action_text := "", duration := 3.5) -> void:
	if is_instance_valid(global_snackbar) and global_snackbar.has_method("show_message"):
		global_snackbar.show_message(message, action_text, duration)

func open_deduction(data: Dictionary) -> void:
	if is_instance_valid(deduction_interface):
		deduction_interface.open_deduction(data)

func _on_deduction_submitted(hypothesis_id: String, payload: Dictionary) -> void:
	if _closing: return
	# Zero physical side effects: only passes cognitive hypothesis payload
	ui_event.emit({"kind": "deduction_submit", "hypothesis_id": hypothesis_id, "payload": payload})

func open_evidence_inspect(data: Dictionary) -> void:
	if is_instance_valid(evidence_inspect_ui):
		evidence_inspect_ui.open_inspect(data)

func _on_evidence_inspect_closed(original_target: String) -> void:
	_on_modal_closed()
	if not original_target.is_empty():
		ui_event.emit({"kind": "focus_target", "target_id": original_target})

func open_inventory(data: Dictionary = {}) -> void:
	if is_instance_valid(inventory_ui):
		var inv_data: Dictionary = data if not data.is_empty() else snapshot.get("inventory", {})
		inventory_ui.open_inventory(inv_data)

func _on_inventory_handover(item_id: String, target_char: String) -> void:
	if _closing: return
	# Authoritative handover dispatched to Core; zero client-side duplication
	ui_event.emit({"kind": "item_handover", "item_id": item_id, "target": target_char})

func _on_inventory_item_inspect(item_id: String) -> void:
	if _closing: return
	var item_data: Dictionary = {"id": item_id, "title": item_id, "target_id": item_id}
	open_evidence_inspect(item_data)

func show_alert(data: Dictionary) -> void:
	if is_instance_valid(alert_ui):
		alert_ui.show_alert(data)

func _on_hazard_evacuate(hazard_id: String) -> void:
	if _closing: return
	ui_event.emit({"kind": "hazard_evacuate", "hazard_id": hazard_id})

func _on_hazard_resolve(hazard_id: String) -> void:
	if _closing: return
	ui_event.emit({"kind": "hazard_resolve", "hazard_id": hazard_id})

func _on_hazard_dismissed(hazard_id: String) -> void:
	if _closing: return
	ui_event.emit({"kind": "hazard_dismissed", "hazard_id": hazard_id})

func open_collaboration(data: Dictionary = {}) -> void:
	if is_instance_valid(collaboration_ui):
		var collab_data: Dictionary = data if not data.is_empty() else snapshot.get("collaboration", {})
		collaboration_ui.open_collaboration(collab_data)

func _on_collaboration_invite(participant: String, task: String, location: String) -> void:
	if _closing: return
	ui_event.emit({"kind": "collaboration_invite", "participant": participant, "task": task, "location": location})

func _on_collaboration_cancel(participant: String) -> void:
	if _closing: return
	ui_event.emit({"kind": "collaboration_cancel", "participant": participant})

func open_text_comparison(data: Dictionary) -> void:
	if is_instance_valid(text_comparison_ui):
		text_comparison_ui.open_comparison(data)

func open_convention(data: Dictionary = {}) -> void:
	var conv_data: Dictionary = data.duplicate(false) if not data.is_empty() else snapshot.get("convention", {})
	_request_modal("notebook", {"page": "B04", "convention": conv_data})

func _on_convention_opinion(clause_id: String, opinion: String) -> void:
	if _closing: return
	ui_event.emit({"kind": "convention_opinion", "clause_id": clause_id, "opinion": opinion})

func _on_convention_signed(viewer_id: String, version: String) -> void:
	if _closing: return
	ui_event.emit({"kind": "convention_signed", "viewer": viewer_id, "version": version})

func close_modal(return_to_parent := true) -> void:
	_pending_modal.clear()
	if not return_to_parent: _modal_stack.clear()
	settings_ui.close()
	investigation_ui.close()
	if not settings_ui.is_active() and not investigation_ui.is_active(): _release_modal()

func open_notebook() -> void:
	_request_modal("notebook", {"page": "B01"})

func set_player_options(options: Array) -> void:
	# IDs come from the current Prepare result. Opening a page never creates
	# actions or admits a hypothesis; the field checks selection again.
	_player_options = options.duplicate(true)
	_learn_map()

func open_manuscript(_announce := true) -> void:
	var data: Dictionary = snapshot.get("replay", {}).duplicate(true)
	data.personal_notes = casebook.notes.duplicate(true)
	var choices: Array = []
	for option: Dictionary in _player_options:
		var binding: Dictionary = option.get("interaction", {})
		if str(binding.get("kind", "")) == "self" and str(binding.get("purpose", "")) != "wait": choices.append(option)
	data.options = choices
	_request_modal("manuscript", data)

func case_reasoning_response(serial: int, request: Dictionary, response: Dictionary) -> void:
	if not _closing and is_instance_valid(investigation_ui): investigation_ui.reasoning_response(serial, request, response)

func open_card(data: Dictionary) -> void:
	var entry_id: String = casebook.record_card(data)
	_request_modal("card", {"entry_id": entry_id})

func _learn_map() -> void:
	# Rebuild from the observer projection, including only completed passages.
	# Offered choices and hypothetical playback do not add rooms or routes.
	_known_rooms.clear()
	_known_connections.clear()
	var map_id: String = str(snapshot.get("map", ""))
	var room_id: String = str(snapshot.get("execution_room", ""))
	if map_id.is_empty() or room_id.is_empty(): return
	_add_visited_room(map_id, room_id, str(snapshot.get("hud", {}).get("room", room_id)))
	var knowledge: Dictionary = snapshot.get("map_knowledge", {})
	for value in knowledge.get("doors", []):
		if not value is Dictionary: continue
		var door: Dictionary = value
		var actions: Variant = door.get("passage_action_ids", [])
		var observed: Variant = door.get("passage_observed", false)
		var passed: bool = observed is bool and observed and actions is Array and not actions.is_empty()
		if not passed: continue
		var from: Dictionary = door.get("from", {})
		var to: Dictionary = door.get("to", {})
		var from_map: String = str(from.get("map", ""))
		var from_room: String = str(from.get("room", ""))
		var to_map: String = str(to.get("map", ""))
		var to_room: String = str(to.get("room", ""))
		if from_map.is_empty() or from_room.is_empty() or to_map.is_empty() or to_room.is_empty(): continue
		var from_label: String = str(from.get("label", from_room))
		var to_label: String = str(to.get("label", to_room))
		_add_visited_room(from_map, from_room, from_label)
		_add_visited_room(to_map, to_room, to_label)
		var key: String = from_map + ":" + from_room + ">" + to_map + ":" + to_room
		var connection: Dictionary = {"from_map": from_map, "from_room": from_room, "to_map": to_map, "to_room": to_room, "label": from_label + " → " + to_label}
		_known_connections[key] = connection

func _add_visited_room(map_id: String, room_id: String, label: String) -> void:
	if not _known_rooms.has(map_id): _known_rooms[map_id] = {}
	_known_rooms[map_id][room_id] = {"label": label, "visited": true}

func open_campus_map() -> void:
	_learn_map()
	_request_modal("notebook", {"page": "B03", "schedule_view": "map"})

func open_ending(data: Dictionary) -> void:
	_request_modal("ending", data)

func open_settings(page: String = "B08") -> void:
	_request_modal("settings", {"page": "settings@1" if page == "settings@1" else "B08"})

func _book_page(kind: String, data: Dictionary) -> String:
	var page: String = str(data.get("page", ""))
	if kind == "card" or (page in ["B01", "evidence", "archive"] and not str(data.get("entry_id", "")).is_empty()):
		return "entry:" + str(data.get("entry_id", ""))
	if page.begins_with("entry:") or page == "settings@1": return page
	if page.length() == 3 and page.begins_with("B") and int(page.substr(1)) in range(1, 9): return page
	match page:
		"index", "evidence", "archive", "items", "people", "notebook": return "B01"
		"rules", "knowledge": return "B02"
		"events", "records", "schedule", "campus_map": return "B03"
		"convention", "contracts": return "B04"
		"hypotheses", "notes": return "B05"
		"gallery": return "B06"
		"load", "save", "bookmarks": return "B07"
		"settings": return "B08"
	if kind == "settings": return "B08"
	if kind == "bookmarks": return "B07"
	return "B01"

func _on_book_event(event: Dictionary) -> void:
	if _closing or host_input_blocked or not _host_input_enabled: return
	match str(event.get("kind", "")):
		"bookmark_save", "bookmark_load":
			if _replay_mode: show_bookmark_result("回放中无法存取书签")
			else: ui_event.emit(event)
		"bookmark_overwrite": show_bookmark_result("暂不支持覆盖，请保存新书签")
		"bookmark_delete": show_bookmark_result("暂不支持删除这份书签")
		"return_title": _return_to_title()
		"book_page", "open_section": _request_modal("notebook", event.duplicate(false))
		"open_reasoning": open_manuscript()
		"convention_opinion", "convention_signed":
			if _replay_mode: show_snackbar("回放中无法签改公约", "", 2.0)
			else: ui_event.emit(event)

func _return_to_title() -> void:
	if _returning_title: return
	if _replay_mode:
		show_bookmark_result("请先结束回放")
		return
	var host: Node = get_parent()
	while host != null and not (host.has_method("wait_for_shutdown") and host.has_method("dispatch")):
		host = host.get_parent()
	if host == null:
		show_bookmark_result("暂时无法返回标题")
		return
	_returning_title = true
	var tree: SceneTree = get_tree()
	await host.call("wait_for_shutdown")
	if is_instance_valid(tree):
		var result: Error = tree.reload_current_scene()
		if result != OK: push_error("Could not return to the title scene: %s" % error_string(result))

func show_bookmark_result(message: String, success := false) -> void:
	settings_ui.show_bookmark_result(message, success)

func set_bookmark_loading(blocked: bool) -> void:
	settings_ui.set_bookmark_loading(blocked)

func prepare_presentation(result: Dictionary) -> void:
	_archive_result = result
	_archive_lines = []

func _on_dialogue_line_read(line: Dictionary) -> void:
	if _inspection_active or _archive_result.is_empty(): return
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
	if Engine.is_editor_hint():
		return
	if host_input_blocked or not _host_input_enabled: return
	if _closing or _screen_motion.is_active():
		get_viewport().set_input_as_handled()
		return
	# Children receive _input first: the book, modal sheet or history closes one
	# level and consumes the event. Capture the remaining back input before GUI
	# Controls can swallow a right-click and before the field can see it.
	if UIInput.is_back(event, false):
		if UIInput.is_back(event):
			if is_instance_valid(interaction_menu) and interaction_menu.is_menu_open(): interaction_menu.close_menu()
			elif _modal_cycle: close_modal()
			elif not dialogue_ui.is_dialogue_active(): open_notebook()
		get_viewport().set_input_as_handled()

func _unhandled_input(event: InputEvent) -> void:
	if Engine.is_editor_hint():
		return
	if _closing or host_input_blocked or not _host_input_enabled or not event.is_pressed() or event.is_echo(): return
	if UIInput.is_back(event):
		if _modal_cycle: close_modal()
		elif not dialogue_ui.is_dialogue_active(): open_notebook()
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
		settings_ui.pause_world = false
		settings_ui.replay_mode = enabled

func restore_replay_context(context: Dictionary) -> void:
	set_replay_mode(true)
	_replay_context_serial += 1
	var serial: int = _replay_context_serial
	if context.get("casebook") is Dictionary:
		casebook.restore_data(context.casebook, str(snapshot.get("viewer", "")))
	var panel: String = str(context.get("panel", "field"))
	if panel == "casebook":
		var page: String = str(context.get("page", "index"))
		if not settings_ui.is_open() or str(settings_ui.book.page_trace_context().get("page", "")) != page:
			_request_modal(str(context.get("modal_kind", "notebook")), {"page": page})
		await get_tree().process_frame
		while serial == _replay_context_serial and not _closing and (not settings_ui.is_open() or settings_ui.transition_state() == "turning"):
			await get_tree().process_frame
		if serial == _replay_context_serial and not _closing:
			await settings_ui.book.restore_replay_state(context.get("book_state", {"page": page}))
	elif panel in ["investigation", "spatial"]:
		var kind: String = str(context.get("modal_kind", context.get("page", "spatial")))
		var data: Dictionary = context.get("modal_data", {})
		var view_state: Dictionary = context.get("view_state", {})
		if investigation_ui.is_open() and _modal_kind == kind and _active_modal.get("data", {}) == data:
			investigation_ui.restore_view_state(view_state)
		else:
			_request_modal(kind, data, view_state)
	elif panel in ["field", "dialogue"]:
		close_modal(false)
