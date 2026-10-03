extends "res://scenes/FirstAwakening/FirstAwakening.gd"
## Chapter coordinator: field input, dialogue lifecycle and module composition.
const ObservationReader = preload("res://scenes/Story/ObservationReader.gd")
const Content = preload("res://scenes/FirstAwakening/RedTokenContent.gd")
const RED_DIALOGUE = preload("res://scenes/FirstAwakening/red_token.dialogue")
const RedBridge = preload("res://scenes/FirstAwakening/RedTokenBridge.cs")
const EvidenceDetail = preload("res://scenes/FirstAwakening/RedTokenEvidenceView.gd")
const EvidenceNotebook = preload("res://scenes/FirstAwakening/EvidenceNotebook.tscn")
const DefenseView = preload("res://scenes/FirstAwakening/RedTokenDefenseView.gd")
const Director = preload("res://scenes/Story/StoryDirector.gd")
const RED_LAYOUT: Resource = preload("res://ShareAsset/Internal/Props/FirstAwakeningInterior/red_mechanisms.tres")
const DETECTIVE_COAT: SpriteFrames = preload("res://ShareAsset/Internal/Characters/animation_v2/detective.tres")
const DETECTIVE_SHIRT: SpriteFrames = preload("res://ShareAsset/Internal/Characters/animation_v2/detective_shirt.tres")
const COAT_STAND_EMPTY: Texture2D = preload("res://ShareAsset/Internal/Props/FirstAwakeningInterior/coat_stand.png")
const COAT_STAND_COAT: Texture2D = preload("res://ShareAsset/Internal/Props/FirstAwakeningInterior/coat_stand_coat.png")
## Authored conversations reached by inspecting a site for the first time.
const INSPECTION_SCENES := {"E15":["S05"], "E05":["S06"], "E06":["S06"], "E07":["S07"], "E08":["S08"], "E09":["S08"], "E02":["S11"], "E12":["S13"], "E13":["S18"], "E14":["S19"], "E09.deep":["S15"], "E16":["S22", "S23"]}
var red_defense_view = DefenseView.new()
var red_performance: Node
var red: Node
var round_number := 1
var drafts := [{}, {}, {}]
var verified := [false, false, false]
var red_seen := {}
var red_companions_met := false
var receipt_checked := false
var red_after := ""
var red_dialogue_queue: Array[String] = []
var red_exit_open := false
var red_details := {}
var red_notebook: Control
var red_notebook_box: VBoxContainer
var replay_summary: RichTextLabel
var deduction_view: Control
var red_visuals: Node2D
var safe_done := {}
var red_interaction_results := {}
var red_feedback := {}
var red_notes := {}
var last_recorded_evidence := ""
var red_note_id := ""
var red_note_history_start := 0
var red_inspection_loading := false
var red_defense_answer := {}
var red_defense_section := "open"
var red_defense_hints := false
var red_opening_active := false
var coat_worn := false
## Conversations wait here until the player is back in the field; each plays once.
var story_backlog: Array[String] = []
var story_played := {}
var departure_explained := false
signal player_name_confirmed
var player_name := ""
var name_entry_active := false
var name_entry: LineEdit
var name_entry_error: RichTextLabel
var room_seen := {}
var story_director: Node
var story_ok := true
var story_message := ""
var story_has_beats := false
var story_safe_holder: String:
	get: return str(red.call("State").get("safe_holder", "")) if is_instance_valid(red) else ""
const STORY_ACTORS := {"侦探":"侦探", "工程师":"工程师霍铎", "厨师":"主厨沈鼎", "音乐家":"音乐家苏泠"}
## The four contract holders, in the order the cabinet lists their deliveries.
const PARTY := ["侦探", "工程师霍铎", "主厨沈鼎", "音乐家苏泠"]
## Records the hypothesis notebook needs before it opens.
const REASONING_RECORDS := ["E01", "E05", "E06", "E07", "E08", "E09", "E15"]
const FieldKeyHints = preload("res://scenes/FirstAwakening/FieldKeyHints.gd")
var key_hints: HBoxContainer
var restart_pending := false
var hint_text_shown := ""
## A blocked move explains itself on the prompt pill for a moment before proximity hints resume.
var field_notice := ""
var field_notice_until := 0

func _field_notice(text: String, seconds := 2.5) -> void:
	field_notice = text
	field_notice_until = Time.get_ticks_msec() + int(seconds * 1000.0)
	if hud_hint: hud_hint.text = text
var room_sites = Content.ROOM_SITES.duplicate(true)

var red_sites = Content.RED_SITES.duplicate(true)

var fields = Content.FIELDS.duplicate(true)

const WORDS = Content.WORDS

var investigation_phase: String = "prologue"
var conversation_staged: bool = false
var common_room_entry_busy := false
var copper_introduction_complete: bool:
	get: return is_instance_valid(red) and bool(red.call("State").get("copper_introduction_received", false))
var previous_entry_position := Vector2.ZERO
var replay_states: Array = []
var replay_events: Array[String] = []
var replay_index: int = 0
var replay_accum: float = 0.0
var replay_fast: bool = false
var replay_playing: bool = false
var replay_stage: Node

func _setup_case_runtime() -> void:
	red_defense_view.setup(self)
	red = RedBridge.new()
	add_child(red)
	red_performance = preload("res://scenes/FirstAwakening/RedTokenPerformance.gd").new()
	add_child(red_performance)
	red_performance.host = self
	# The inherited scene retains the legacy node for old-case checks.
	# Red Token owns its own session and does not initialize the old deduction UI.

func _load_sites() -> void:
	sites.clear()
	for id in red_sites:
		var item: Array = red_sites[id]
		if RED_LAYOUT.data.sites.has(id):
			var at: Array = RED_LAYOUT.data.sites[id]
			item[1] = Vector2(at[0],at[1])
		sites[id] = {"id":id,"title":item[0],"title_id":item[0],"point":item[1],"reach":72.0,"diagram":"","description_id":""}
	for id in room_sites:
		var item: Array = room_sites[id]
		sites[id] = {"id":id,"title":item[0],"title_id":item[0],"point":item[1],"reach":48.0,"diagram":"","description_id":""}
	sites["companions"] = {"id":"companions","title":"同伴与凭证","title_id":"同伴与凭证","point":Vector2(280,556),"reach":96.0}

func _start_case() -> void:
	investigation_phase = "prologue"
	conversation_staged = false
	common_room_entry_busy = false
	previous_entry_position = player.position
	# Preserve the existing party placement and seated opening.
	_place_party_at_table()
	_dress_detective(false)
	$HallTraceDecals.hide()
	get_window().title = "安眠者有福 · 未归的红筹 · Demo"
	_build_field_hud()
	red_visuals = preload("res://scenes/FirstAwakening/RedTokenMechanisms.gd").new()
	red_visuals.source = self
	scene_items.add_child(red_visuals)
	story_director = Director.new()
	add_child(story_director)
	story_director.setup(self, actors, RED_DIALOGUE)
	story_director.busy_changed.connect(func(value: bool):
		story_lock = value or common_room_entry_busy
		if value: _cancel_click_navigation())
	if is_instance_valid(dialogue) and dialogue.has_method("clear_history"):
		dialogue.clear_history()
	if is_instance_valid(dialogue) and dialogue.has_method("set_character_portraits"):
		dialogue.set_character_portraits({
			"侦探": "res://ShareAsset/Internal/Characters/FirstAwakeningPortraits/detective.png",
			"工程师": "res://ShareAsset/Internal/Characters/FirstAwakeningPortraits/engineer.png",
			"厨师": "res://ShareAsset/Internal/Characters/FirstAwakeningPortraits/chef.png",
			"音乐家": "res://ShareAsset/Internal/Characters/FirstAwakeningPortraits/musician.png",
		})
	_begin_dialogue("opening")

func _build_field_hud() -> void:
	# Keycaps replace the plain legend; the proximity prompt sits on a pill.
	hud_time.hide()
	key_hints = FieldKeyHints.new()
	key_hints.position = Vector2(380, 16)
	key_hints.size = Vector2(546, 36)
	ui.add_child(key_hints)
	ui.move_child(key_hints, hud_time.get_index())
	key_hints.theme = active_locale_theme
	var pill := StyleBoxFlat.new()
	pill.bg_color = Color(0.06, 0.08, 0.07, 0.8)
	pill.border_color = Color(0.71, 0.6, 0.41, 0.6)
	pill.set_border_width_all(1)
	pill.set_corner_radius_all(16)
	pill.content_margin_left = 20
	pill.content_margin_right = 20
	pill.content_margin_top = 5
	pill.content_margin_bottom = 6
	hud_hint.add_theme_stylebox_override("normal", pill)
	hud_hint.add_theme_constant_override("shadow_offset_y", 0)

func _fit_hint_pill() -> void:
	if hud_hint.text == hint_text_shown: return
	hint_text_shown = hud_hint.text
	hud_hint.size = Vector2.ZERO
	hud_hint.position = Vector2(roundf(640.0 - hud_hint.size.x / 2.0), 966.0 - hud_hint.size.y)

func reasoning_records_found() -> int:
	return REASONING_RECORDS.filter(func(id): return red_notes.has(id) and red_seen.has(id)).size()

func steady_investigation_hints() -> bool:
	return true

func dialogue_search_inspectables() -> void:
	red.call("SearchOpeningRoom")
	_pick_nearest()

func inspection_hint_visible(id: String) -> bool:
	return is_instance_valid(red) and id in red.call("DiscoveredSites") and _site_in_hint_room(id)

func _site_in_hint_room(id: String) -> bool:
	if room_sites.has(id) or id == "E07":
		return is_instance_valid(player) and scene_items.get_node("DormitoryDoor1").contains_private_point(player.position)
	return true

func can_leave_detective_room() -> bool:
	return not name_entry_active and is_instance_valid(red) and bool(red.call("State").get("can_leave_room", false))

func _room_departure_hint() -> String:
	return "先看看门是否能开，或核对衣袋里的凭据，再决定出去。"

func _move_to_click(destination: Vector2) -> void:
	for rect in _closed_room_rects(player):
		if rect.grow(8.0).has_point(destination):
			_cancel_click_navigation()
			_field_notice("门关着，那是别人的房间。")
			return
	if story_safe_holder == "侦探":
		_cancel_click_navigation()
		_field_notice("我还在持柄，先松手再离开岗位。")
		return
	var threshold: Rect2 = scene_items.get_node("DormitoryDoor1").threshold_collision_rect()
	if not can_leave_detective_room() and player.position.y < threshold.end.y and destination.y >= threshold.position.y:
		_cancel_click_navigation()
		_field_notice(_room_departure_hint())
		return
	super._move_to_click(destination)

func _build_geometry() -> void:
	super._build_geometry()
	for item in layout.furniture:
		if item.name in RED_LAYOUT.data.replaced and item.footprint != null:
			var footprint: Array = item.footprint
			obstacle_rects.erase(Rect2(footprint[0],footprint[1],footprint[2],footprint[3]))
	for item in RED_LAYOUT.data.fixtures:
		if item.footprint != null:
			var r: Array = item.footprint
			obstacle_rects.append(Rect2(r[0],r[1],r[2],r[3]))

func _begin_dialogue(title: String, _after := "", _keep_scene := false) -> void:
	if dialogue == null: return
	if dialogue.active or story_lock or (is_instance_valid(red_performance) and red_performance.busy): return
	if title == "S02" and not conversation_staged:
		await _stage_common_room_entry()
		return
	if title == "S02" and red_companions_met and not copper_introduction_complete:
		title = "COPPER_INTRO"
	story_ok = true
	story_message = ""
	story_has_beats = false
	if title in ["opening", "S01"]: red_opening_active = true
	if is_instance_valid(red_notebook): red_notebook.hide()
	if modal: modal.hide()
	modal_open = false
	mode = ""
	dialogue.panel.show()
	dialogue.heading.text = "未归的红筹 · 现场"
	dialogue.single_press_keyboard_continue = true
	dialogue.start(RED_DIALOGUE, "S01" if title == "opening" else title, self)

func dialogue_wake_look(direction: String) -> void:
	if red_opening_active and direction in ["front", "back", "left", "right"]:
		player.sit_at(direction, 4.0)

func _localized_speaker(canonical_name: String) -> String:
	if canonical_name in ["侦探", "侦探（心声）"]:
		return "？？？" if player_name.is_empty() else player_name
	return super._localized_speaker(canonical_name)

func dialogue_has_modal_input() -> bool:
	return name_entry_active

func dialogue_request_name() -> void:
	if not player_name.is_empty(): return
	if name_entry_active:
		await player_name_confirmed
		return
	name_entry_active = true
	var language_was_disabled: bool = language_selector.disabled
	language_selector.get_popup().hide()
	language_selector.disabled = true
	_cancel_click_navigation()
	player.move_input(Vector2.ZERO)
	var dialogue_was_visible: bool = dialogue.panel.visible
	dialogue.panel.hide()
	_open_modal("票据上的姓名", "name")
	modal.compact_name_card()
	modal.add_label(modal_box, "衣袋里的登船凭据，边角有海水浸过的黄斑。姓名栏写着你的名字：")
	name_entry = LineEdit.new()
	name_entry.placeholder_text = "输入姓名"
	name_entry.custom_minimum_size = Vector2(0, 44)
	modal.paper_field(name_entry)
	modal_box.add_child(name_entry)
	name_entry_error = modal.add_label(modal_box, "", 18)
	name_entry_error.hide()
	name_entry.text_submitted.connect(func(_text: String): _confirm_player_name())
	var confirm := modal.add_footer([["确认姓名", _confirm_player_name]])
	confirm.focus_mode = Control.FOCUS_NONE
	name_entry.grab_focus()
	await player_name_confirmed
	language_selector.disabled = language_was_disabled
	_close_modal()
	if dialogue_was_visible: dialogue.panel.show()

func _confirm_player_name() -> void:
	if not name_entry_active: return
	var entered := name_entry.text.strip_edges()
	if entered.is_empty():
		name_entry_error.text = "姓名不能为空，请输入票据上的姓名。"
		name_entry_error.show()
		name_entry.grab_focus()
		return
	var identity: Dictionary = red.call("IdentifyInvestigator", entered)
	if not identity.get("ok", false):
		name_entry_error.text = str(identity.get("message", "尚未核对本人票据。"))
		name_entry_error.show()
		return
	player_name = entered
	name_entry_active = false
	name_entry.release_focus()
	player_name_confirmed.emit()

func story_beat(id: String) -> void:
	story_has_beats = true
	var result: Dictionary = await story_director.play(id)
	story_ok = result.get("ok", false)
	story_message = str(result.get("message", ""))

func story_wait() -> void:
	var result: Dictionary = await story_director.join()
	story_ok = result.get("ok", false)
	story_message = str(result.get("message", ""))

func story_require(requirement: Dictionary) -> Dictionary:
	var known: Array = red.call("State").get("known", [])
	for id in requirement.get("known", []):
		if id not in known: return {"ok":false, "message":"尚未取得现场材料：" + str(id)}
	return {"ok":true}

func story_can_move(actor: String) -> Dictionary:
	if actor == story_safe_holder:
		return {"ok":false, "message":actor + "仍在持续持柄，请先松开试柄。"}
	if actor == str(red.call("State").get("holder", "")):
		return {"ok":false, "message":actor + "仍占用现场持续岗位，请先完成确认或取消。"}
	return {"ok":true}

func story_commit(command: Dictionary) -> Dictionary:
	var kind := str(command.get("kind", ""))
	var result: Dictionary
	var actor := str(command.get("actor", ""))
	if kind in ["safe", "perform"] and not STORY_ACTORS.has(actor):
		return {"ok":false, "message":"没有对应的机制行动者：" + actor}
	match kind:
		"safe":
			var operation := str(command.get("operation", ""))
			if operation not in ["reward", "press", "release", "gap", "pipe", "close", "red", "handle", "push_red", "push_handle", "white"]:
				return {"ok":false, "message":"没有该安全试件动作。"}
			result = red.call("SafeTest", operation + ":" + str(STORY_ACTORS[actor]))
			if result.get("ok", false):
				var key := "reward:" + str(STORY_ACTORS[actor]) if operation == "reward" else operation
				safe_done[key] = true
			if is_instance_valid(red_visuals): red_visuals.test_state = result.get("safe_state", {})
		"inspect":
			var id := str(command.get("id", ""))
			result = red.call("Inspect", id)
			if result.get("ok", false): _accept_red_inspection(id, result)
		"perform":
			result = red.call("Perform", STORY_ACTORS[actor], str(command.get("action", "")), str(command.get("station", "")))
		"cancel":
			result = red.call("Cancel")
			if is_instance_valid(red_visuals): red_visuals.test_state = result.get("safe_state", {})
		_:
			return {"ok":false, "message":"剧情机制命令不受支持。"}
	return result

func _follow_companion() -> void:
	if not paired_companion.is_empty() and not story_can_move(paired_companion).get("ok", false): return
	super._follow_companion()

func _on_dialogue_finished() -> void:
	if story_has_beats and is_instance_valid(story_director):
		var result: Dictionary = await story_director.join()
		if not result.get("ok", true):
			story_ok = false
			story_message = str(result.get("message", ""))
	if not story_ok:
		if not copper_introduction_complete: conversation_staged = false
		red_opening_active = false
		dialogue.panel.hide()
		_show_message("剧情编排未完成", story_message + "\n已提交的机制结果保留；本段未完成的演出不算抵达。")
		return
	if red_opening_active:
		red_opening_active = false
		_leave_bed()
	var inspected := red_note_id
	if red_note_id != "":
		red_notes[red_note_id] = dialogue.history.slice(red_note_history_start)
		if room_sites.has(red_note_id) and (red_note_id != "ROOM_COAT" or not player_name.is_empty()):
			if not _accept_room_inspection(red_note_id):
				red_note_id = ""
				dialogue.panel.hide()
				return
		red_note_id = ""
		_update_status()
	dialogue.panel.hide()
	if inspected != "" and not ended:
		_open_recorded_evidence(inspected, false)
	elif red_after != "":
		var next := red_after
		red_after = ""
		_begin_dialogue(next)
	elif not red_dialogue_queue.is_empty():
		_begin_dialogue(red_dialogue_queue.pop_front())
	elif not story_backlog.is_empty() and not ended:
		_flush_story.call_deferred()
	elif ended:
		_open_modal("本 Demo 完成 · 证物与未知保留", "red")
		_label(modal_box,"红筹案已结，本区交接记录留存。外闩已升起，四人带着证物走向门厅；封存的疑问将带入主楼。下一章尚未开放。",21)
		modal.add_footer([["重新开始",func(): get_tree().reload_current_scene()]])

func dialogue_confirm_red_companions() -> void:
	# S02 has completed its introductions and, when available, actual signature checks.
	# Refresh only the saved E14 record; its first reading remains in dialogue history.
	if not story_ok: return
	var introduced: Dictionary = red.call("IntroduceCompanions")
	if not introduced.get("ok", false):
		story_ok = false
		story_message = str(introduced.get("message", "同伴介绍尚未完成。"))
		return
	investigation_phase = "investigation"
	_pick_nearest()
	_update_status()
	if red_companions_met: return
	red_companions_met = true
	if not red_notes.has("E14"): return
	var observation_line: Variant = await dialogue.manager.get_next_dialogue_line(RED_DIALOGUE, "OBS_E14", [self])
	if observation_line == null: return
	var speaker_name := str(dialogue.call("_localized_speaker", observation_line.character))
	red_notes["E14"] = [speaker_name + "\n" + str(observation_line.text)]

func dialogue_complete_copper_introduction() -> void:
	if not story_ok or copper_introduction_complete: return
	var result: Dictionary = red.call("ReceiveCopperIntroduction")
	if not result.get("ok", false):
		story_ok = false
		story_message = str(result.get("message", "尚未听完铜口的介绍。"))
		return
	_pick_nearest()
	_update_status()

func _face_companions_to_detective() -> void:
	for actor_name in ["工程师", "厨师", "音乐家"]:
		var actor: CharacterBody2D = actors[actor_name]
		var direction := player.position - actor.position
		if direction.length_squared() <= 1.0: continue
		actor.face_vector(direction)
		if actor.seated:
			actor.sit_at(actor.facing, actor.seat_height)
		else:
			actor.pose("idle", actor.facing)

func _stage_common_room_entry() -> void:
	if common_room_entry_busy or ended: return
	if red_companions_met and copper_introduction_complete: return
	if dialogue == null or dialogue.active or story_lock or red_performance.busy: return
	common_room_entry_busy = true
	story_lock = true
	if modal_open: _close_modal()
	_cancel_click_navigation()
	player.move_input(Vector2.ZERO)
	_face_companions_to_detective()
	var movable: Dictionary = story_can_move("侦探")
	var reached := false
	if movable.get("ok", false):
		reached = await _walk_actor_to(player, $MeetingSpots/Detective.position)
	if not is_inside_tree(): return
	common_room_entry_busy = false
	story_lock = is_instance_valid(story_director) and story_director.busy
	previous_entry_position = player.position
	if not reached:
		var reason := str(movable.get("message", ""))
		_show_message("尚未走到桌边", reason + "\n暂时没能走到桌边。可以靠近桌边，或与同伴交互，再继续介绍。")
		return
	_face_companions_to_detective()
	player.face_point($MeetingSpots/Chef.position)
	conversation_staged = true
	_begin_dialogue("S02")

func dialogue_open_red_exit() -> void:
	# Called from S28 after Halvard pulls the inner bolt, so the door opens on cue.
	_open_red_exit()

func _open_red_exit() -> void:
	if red_exit_open: return
	red_exit_open = true
	paired_companion = ""
	click_path.clear()
	var door := scene_items.get_node("MechanicalIsolationDoor")
	door.get_node("DoorFace").hide()
	door.get_node("DoorGap").show()
	bolt_visual.hide()
	var sign := PanelContainer.new()
	sign.name = "RedExitPassSign"
	sign.position = Vector2(704,970)
	sign.custom_minimum_size = Vector2(64,28)
	var paper := StyleBoxFlat.new()
	paper.bg_color = Color("214d3b")
	paper.content_margin_left = 12
	paper.content_margin_right = 12
	sign.add_theme_stylebox_override("panel",paper)
	sign.theme = active_locale_theme
	scene_items.add_child(sign)
	var pass_label := _label(sign,"通行",20,Color("f4ecd4"))
	pass_label.autowrap_mode = TextServer.AUTOWRAP_OFF
	pass_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	# Per-person exit paths now belong to S28 in the dialogue score.

func _physics_process(delta: float) -> void:
	if is_instance_valid(player) and not modal_open and not story_lock and dialogue != null and not dialogue.active:
		if story_safe_holder == "侦探":
			_cancel_click_navigation()
			player.move_input(Vector2.ZERO)
			_pick_nearest()
			_update_status()
			return
		var direction := Vector2(float(Input.is_physical_key_pressed(KEY_D) or Input.is_physical_key_pressed(KEY_RIGHT)) - float(Input.is_physical_key_pressed(KEY_A) or Input.is_physical_key_pressed(KEY_LEFT)), float(Input.is_physical_key_pressed(KEY_S) or Input.is_physical_key_pressed(KEY_DOWN)) - float(Input.is_physical_key_pressed(KEY_W) or Input.is_physical_key_pressed(KEY_UP)))
		var threshold: Rect2 = scene_items.get_node("DormitoryDoor1").threshold_collision_rect()
		var destination: Vector2 = player.position + direction.normalized() * player.speed * delta
		if not can_leave_detective_room() and direction.y > 0 and player.position.y < threshold.end.y and destination.y >= threshold.position.y:
			_cancel_click_navigation()
			player.move_input(Vector2.ZERO)
			_pick_nearest()
			_update_status()
			_field_notice(_room_departure_hint())
			return
	super._physics_process(delta)
	if not is_instance_valid(player): return
	var from := previous_entry_position
	var to := player.position
	previous_entry_position = to
	if red_companions_met and copper_introduction_complete: return
	if not can_leave_detective_room() or modal_open or dialogue == null or dialogue.active or story_lock or ended: return
	# Room entry brings attention and a walk to the table, without searching items.
	var cell_size := float(layout.cell)
	var threshold_y := 17.0 * cell_size
	var crossed_entry := false
	if from.y < threshold_y and to.y >= threshold_y:
		var crossing_x := lerpf(from.x, to.x, (threshold_y - from.y) / (to.y - from.y))
		crossed_entry = (crossing_x >= 7.0 * cell_size and crossing_x <= 9.0 * cell_size) or (crossing_x >= 31.0 * cell_size and crossing_x <= 33.0 * cell_size)
	var table_point: Vector2 = $MeetingSpots/Detective.position
	var approached_table := from.distance_to(table_point) > 64.0 and to.distance_to(table_point) <= 64.0
	if crossed_entry or approached_table: _stage_common_room_entry()

func _process(delta: float) -> void:
	if hud_hint:
		hud_hint.visible = not modal_open and not dialogue.active and not hud_hint.text.is_empty()
		_fit_hint_pill()
	if is_instance_valid(key_hints): key_hints.visible = not (modal_open and mode == "rednotebook")
	if replay_playing and is_instance_valid(replay_stage):
		replay_accum += delta
		var beat := 0.65 / (4.0 if replay_fast else 1.0)
		while replay_accum >= beat and replay_index < replay_events.size():
			replay_accum -= beat
			replay_index += 1
		if replay_index >= replay_events.size(): replay_playing = false
		replay_stage.frame = replay_index
		replay_stage.progress = replay_accum / beat if replay_playing else 0.0

func _site_accessible(id: String) -> bool:
	if not can_leave_detective_room() and not bool(red.call("State").get("companions_met", false)): return sites.has(id) and (room_sites.has(id) or id == "E07")
	if id == "E16": return red_seen.has("E13") and red_seen.has("E15")
	return sites.has(id)

func _pick_nearest() -> void:
	if not is_instance_valid(player) or not is_instance_valid(red): return
	var scores: Array = []
	# Walking refreshes range within remembered targets; it never searches the room.
	for id in red.call("DiscoveredSites"):
		if not sites.has(id): continue
		if id != "companions": _refresh_site_title(id)
		if not _site_accessible(id) or not _site_in_hint_room(id): continue
		var distance: float = player.position.distance_to(_site_point(id))
		if distance <= float(sites[id].reach): scores.append([distance,id])
	scores.sort_custom(func(a,b): return a[0] < b[0])
	reachable.clear()
	for candidate in scores: reachable.append(candidate[1])
	nearest = "" if reachable.is_empty() else reachable[cycle_offset % reachable.size()]
	if hud_hint: hud_hint.text = "" if nearest == "" else "E 检视 · " + str(sites[nearest].title)
	if hud_hint and reachable.size() > 1: hud_hint.text += "　　Tab 换目标 %d/%d" % [cycle_offset % reachable.size() + 1, reachable.size()]
	if hud_hint and inspection_hint_visible("ROOM_DOOR") and not can_leave_detective_room() and player.position.distance_to(_site_point("ROOM_DOOR")) <= 64.0:
		hud_hint.text = "E 检视 · 房门　" + _room_departure_hint()
	if hud_hint and Time.get_ticks_msec() < field_notice_until: hud_hint.text = field_notice

func _approach_site(id: String) -> void:
	# A deliberate mouse target remains available before a search; use its known name.
	if id != "companions": _refresh_site_title(id)
	super._approach_site(id)

func _has_new_at(id: String) -> bool:
	return not room_seen.has(id) if room_sites.has(id) else not red_seen.has(id)

func _update_status() -> void:
	var count := 0
	for id in red_sites:
		if red_seen.has(id): count += 1
	if hud_title: hud_title.text = "未归的红筹"
	if not is_instance_valid(key_hints): return
	var entries: Array = [["J", "证物"]]
	if can_use_mind_reasoning(): entries.append(["F", "假说"])
	elif investigation_phase == "investigation": entries.append(["", "推演材料 %d / %d" % [reasoning_records_found(), REASONING_RECORDS.size()]])
	entries.append(["R", "重启"])
	key_hints.show_entries(entries)

func can_use_mind_reasoning() -> bool:
	return reasoning_records_found() == REASONING_RECORDS.size()

func _apply_locale_theme() -> void:
	super._apply_locale_theme()
	active_locale_theme = preload("res://scenes/FirstAwakening/field_theme_zh.tres")
	if is_instance_valid(key_hints): key_hints.theme = active_locale_theme
	if is_instance_valid(dialogue): dialogue.apply_locale_font(preload("res://ShareAsset/External/ZhaohuaTypeWriter/ZhaohuaTypeWriter.ttf"),active_locale_theme)
	if is_instance_valid(modal): modal.theme = active_locale_theme

func _on_language_selected(index: int) -> void:
	var showing_evidence := modal_open and modal.current_mode == "evidence"
	super._on_language_selected(index)
	if index != 0 and not showing_evidence:
		_show_message("Translation coverage / 翻訳範囲","The new Red Token case is currently available in Chinese. Language selection still applies to shared controls and name entry.\n新しい事件本文は現在中国語のみです。共有操作と姓名入力の言語設定は保持されます。\n本轮新案未提供完整英日译文，旧案译文不会用作新案对白。")

func _open_modal(title: String, next_mode: String) -> void:
	restart_pending = false
	_restore_red_scroll()
	modal_locale_refresh = Callable()
	if is_instance_valid(red_notebook): red_notebook.hide()
	replay_playing = false
	mode = next_mode
	modal_open = true
	modal.open_content(title,next_mode,active_locale_theme)

func _interact(id: String) -> void:
	if not _site_accessible(id): return
	click_path.clear()
	if room_sites.has(id):
		player.face_point(_site_point(id))
		_open_room_interaction(id)
		return
	if id == "companions":
		if not conversation_staged:
			_stage_common_room_entry()
			return
		_open_modal("四人的凭证与同行", "red")
		_button(modal_box,"核对来意与姓名",func(): _begin_dialogue("S02"))
		for actor_name in ["工程师","厨师","音乐家"]:
			_button(modal_box,"邀请%s同行" % actor_name,func(): _invite_red_companion(actor_name))
		_button(modal_box,"停止同行",_stop_red_companion)
		return
	_open_red_interaction(id)

func _invite_red_companion(actor_name: String) -> void:
	var result: Dictionary = red.call("FollowCompanion", STORY_ACTORS[actor_name])
	if not result.get("ok", false):
		_show_message("同行请求", str(result.get("message", "对方暂未接受请求。")))
		return
	paired_companion = actor_name
	_close_modal()

func _stop_red_companion() -> void:
	red.call("StopFollowing")
	paired_companion = ""
	_close_modal()

func _open_room_interaction(id: String) -> void:
	_inspect_room(id)

func _inspect_room(id: String) -> void:
	if red_inspection_loading: return
	if red_notes.has(id) and room_seen.has(id):
		_open_recorded_evidence(id, false)
		return
	await _show_red_inspection_record(id, id)

func _return_to_inspection(id: String) -> void:
	if room_sites.has(id): _open_room_interaction(id)
	else: _open_red_interaction("E09" if id.begins_with("E09.") else id)

func _open_red_interaction(id: String, keep_scroll := false) -> void:
	if not red_sites.has(id): return
	if id == "E07" or _inspect_only(id):
		_inspect_red(id)
		return
	var scroll: ScrollContainer = modal_box.get_parent()
	var scroll_position := scroll.scroll_vertical if keep_scroll else 0
	var result: Dictionary = red_interaction_results.get(id, {})
	var trial: Dictionary = red_visuals.test_state if is_instance_valid(red_visuals) else result.get("safe_state", {})
	_open_modal(_site_display_title(id), "red")
	_device_preview(modal_box,id,trial)
	if not result.is_empty():
		# A successful reward message is represented by that person's single
		# delivery below; failures and other safe actions keep their result text.
		var reward_result: bool = id == "E01" and result.get("ok", false)
		if not reward_result: _label(modal_box,str(result.get("message","试件未动作。")),18)
		_red_safe_status(id,trial)
	_button(modal_box,"检视",func(): _inspect_red(id))
	if id == "E01":
		var known: Array = red.call("State").get("known", [])
		if "E13" in known and "E15" in known:
			var query := _button(modal_box, "就现场材料向接待者提问" + (" · 已检视，可复核" if "E16" in known else ""), _open_red_inquiry)
			query.custom_minimum_size.y = 54
			query.size_flags_horizontal = Control.SIZE_EXPAND_FILL
			query.add_theme_font_size_override("font_size", 22)
		else:
			_label(modal_box, "铜口位于领取柜上方；现实质询须先检视归还匣与椅上红筹的现场材料。", 18)
		var deliveries: Dictionary = Content.DELIVERIES
		var pending := PARTY.filter(func(person): return not safe_done.get("reward:" + person, false))
		if pending.size() > 1:
			# One request walks each remaining holder to the cabinet in turn.
			var batch := _button(modal_box, "四人依次核验交付（剩余 %d 人）" % pending.size(), _verify_all_deliveries)
			_style_primary(batch)
		var grid := _button_grid()
		for person in PARTY:
			var verified_person: bool = safe_done.get("reward:" + person, false)
			_button(grid,"%s · %s" % [person, "已核验" if verified_person else "核验交付"],func(): _safe_red("reward:"+person,"本人凭证与约定交付"))
		for person in PARTY:
			if safe_done.get("reward:" + person, false):
				_label(modal_box, "[color=#344c39]%s[/color]　%s" % [person, str(deliveries[person])], 17)
		_label(modal_box, "四人各自核验可信凭证与约定交付后，自愿决定留下参加。", 18)
		_button(modal_box,"听取安全与去留说明",func():
			departure_explained = true
			_begin_dialogue("S04"))
		if departure_explained:
			_button(modal_box,"暂不参加考题，结束本 Demo",_safe_exit)
	var knowledge: Dictionary = red.call("SiteKnowledge", id)
	if knowledge.get("noticed", false):
		var safe_grid := _button_grid()
		for option in Content.SAFE_INTERACTIONS.get(id, []):
			_red_safe_button(option[0],option[1],option[2],safe_grid)
		for option in Content.FOLLOWUP_INSPECTIONS.get(id, []):
			_button(modal_box,option[1],func(): _inspect_red(option[0]))
	elif Content.SAFE_INTERACTIONS.has(id):
		_label(modal_box,"先近身查看物件，再提出具体的操作请求。",18)
	if id in ["E10", "E11"]:
		_label(modal_box, "触物入口原在试柄旁；记录的是两人的转述，内容以检视后证物页为准。", 18)
	# Every object page keeps its exits fixed below the scrolling body.
	_pin_red_footer([["返回现场",_close_modal],["提交答辩",_open_red_defense]] if id == "E01" else [["返回现场",_close_modal]])
	if keep_scroll: scroll.set_deferred("scroll_vertical",scroll_position)

## Objects whose page would only offer "检视" open their record directly.
func _inspect_only(id: String) -> bool:
	if id in ["E01", "E10", "E11"] or red_interaction_results.has(id): return false
	return not Content.SAFE_INTERACTIONS.has(id) and not Content.FOLLOWUP_INSPECTIONS.has(id)

func _button_grid(parent: Node = null) -> GridContainer:
	var grid := GridContainer.new()
	grid.columns = 2
	grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	grid.add_theme_constant_override("h_separation", 10)
	grid.add_theme_constant_override("v_separation", 8)
	(parent if parent != null else modal_box).add_child(grid)
	return grid

func _red_safe_button(operation: String, caption: String, title: String, parent: Node = null) -> void:
	var mark := " · 已核验" if safe_done.get(operation,false) else ""
	_button(parent if parent != null else modal_box,caption + mark,func(): _safe_red(operation,title))

func _safe_red(operation: String, _title: String) -> void:
	if story_lock or red_performance.busy: return
	var result: Dictionary = await red_performance.perform(operation)
	if not is_inside_tree() or ended: return
	if result.get("ok",false): safe_done[operation] = true
	if is_instance_valid(red_visuals) and result.has("safe_state"): red_visuals.test_state = result.safe_state
	if safe_done.has("press") and safe_done.has("release"): _queue_story("S12")
	if safe_done.has("gap") and safe_done.has("pipe"): _queue_story("S14")
	if PARTY.all(func(person): return safe_done.has("reward:" + person)): _queue_story("S03")
	var id: String = Content.safe_interaction_site(operation)
	red_interaction_results[id] = result
	_open_red_interaction(id,true)

func _verify_all_deliveries() -> void:
	for person in PARTY:
		if safe_done.get("reward:" + person, false): continue
		await _safe_red("reward:" + person, "本人凭证与约定交付")
		# A cancelled walk or refused check stops the queue on the page that explains it.
		if not safe_done.get("reward:" + person, false) or not is_inside_tree() or ended: return

func _walk_actor_to(actor: CharacterBody2D, destination: Vector2) -> bool:
	if actor.travelling: return false
	if actor.position.distance_to(destination) <= 2.0: return true
	var path := _route(actor.position, destination, actor)
	if path.is_empty(): return false
	path.append(destination)
	actor.stand_up()
	actor.follow_path(path)
	var last_progress: Vector2 = actor.position
	var stalled := 0.0
	while is_instance_valid(actor) and actor.travelling:
		await get_tree().physics_frame
		if not is_instance_valid(actor): return false
		if actor.position.distance_to(last_progress) >= 0.25:
			last_progress = actor.position
			stalled = 0.0
		else:
			stalled += get_physics_process_delta_time()
		if stalled >= 2.0:
			actor.travelling = false
			actor.path.clear()
			actor.velocity = Vector2.ZERO
			actor.pose("idle")
			actor.arrived.emit()
			return false
	return is_instance_valid(actor) and actor.position.distance_to(destination) <= 2.0

func _site_display_title(id: String) -> String:
	var knowledge: Dictionary = red.call("SiteKnowledge", id)
	if knowledge.get("noticed", false):
		if room_sites.has(id): return str(room_sites[id][0])
		if red_sites.has(id): return str(red_sites[id][0])
	return str(knowledge.get("name", "物件"))

func _refresh_site_title(id: String) -> void:
	if not sites.has(id): return
	var title := _site_display_title(id)
	sites[id].title = title
	sites[id].title_id = title

func _accept_room_inspection(id: String) -> bool:
	var result: Dictionary = red.call("Inspect", id)
	if not result.get("ok", false):
		_show_message("材料尚未成立", str(result.get("message", "请先核对材料")))
		return false
	room_seen[id] = true
	_refresh_site_title(id)
	if id == "ROOM_COAT": _dress_detective(true)
	return true

func _red_safe_status(id: String, trial: Dictionary) -> void:
	if id == "E01":
		_label(modal_box,"凭证与约定交付：已核验 %d / 4 人。" % int(trial.get("rewards_verified", 0)),18)
	elif id == "E03":
		_label(modal_box,"空载试柄当前%s。" % ("按住，安全回路保持" if trial.get("pressed", false) else "已松开，弹簧回位停流"),18)
	elif id == "E04":
		_label(modal_box,"本次声路试件：门缝声%s；管壁振颤声%s。" % ["有" if trial.get("gap_sound", false) else "无", "有" if trial.get("pipe_sound", false) else "无"],18)
	elif id == "E13":
		_label(modal_box,"筹槽%s；柄槽%s；白片%s。试件只核验交接机构，不证明排灰完成。" % ["已接收" if trial.get("red_received", false) else "未接收", "已接收" if trial.get("handle_received", false) else "未接收", "已领取" if safe_done.get("white", false) else "可领取" if trial.get("white", false) else "尚未解锁"],18)

func _device_preview(parent: Node, id: String, trial := {}) -> void:
	var detail_id: String = red_details.get(id,id)
	if id != "E13" or not red_interaction_results.has("E13"):
		if _add_red_detail(parent,detail_id): return
	var assets := {"E01":"reward_cabinet","E02":"assignment_board","E03":"spring_handle","E08":"cloth_clip","E09":"inspect_basin","E10":"handle_rack","E11":"assignment_board","E13":"inspect_box","E14":"sign_strip","E15":"red_token"}
	if not assets.has(id): return
	var asset: String = assets[id]
	if id == "E03" and trial.get("pressed",false): asset = "spring_handle_pressed"
	if id == "E13" and trial.get("red_received",false): asset = "inspect_box_received"
	if id == "E13" and safe_done.get("push_red",false): asset = "inspect_box_stamped"
	if id == "E13" and trial.get("white",false): asset = "inspect_box_white"
	var texture: Texture2D = load("res://ShareAsset/Internal/Props/FirstAwakeningInterior/red_"+asset+".png")
	var row := HBoxContainer.new()
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	parent.add_child(row)
	var view := TextureRect.new()
	view.texture = texture
	view.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	view.custom_minimum_size = texture.get_size() * (3.0 if id in ["E01","E09"] else 4.0)
	view.stretch_mode = TextureRect.STRETCH_SCALE
	view.mouse_filter = Control.MOUSE_FILTER_IGNORE
	row.add_child(view)
	if red_seen.has(id) and EvidenceDetail.has_detail(id):
		_button(parent,"查看已记录原物细节",func(): _open_recorded_evidence(id,false))

func _add_red_detail(parent: Node, id: String) -> bool:
	if not red_seen.has(id) or not EvidenceDetail.has_detail(id): return false
	var detail := EvidenceDetail.new()
	parent.add_child(detail)
	detail.setup(id)
	return true

func _inspect_red(id: String, from_inquiry := false) -> void:
	if red_inspection_loading: return
	if red_notes.has(id):
		_open_recorded_evidence(id, false)
		return
	if id == "E16" and not from_inquiry:
		_open_red_inquiry()
		return
	if id == "E12":
		for operation in ["recall_engineer", "recall_music"]:
			var contact: Dictionary = await red_performance.perform(operation)
			if not is_inside_tree() or ended: return
			if not contact.get("ok", false):
				_show_message("触物请求", str(contact.get("message", "本人尚未完成触物转述。")))
				return
	if id == "E16":
		var inquiry: Dictionary = red.call("Perform","侦探","open_copper","B")
		if not inquiry.get("ok", false):
			_show_message("质询尚未完成", str(inquiry.get("message", "没有取得纸带。")))
			return
	var result: Dictionary = red.call("Inspect",id)
	if not result.get("ok",false):
		_show_message("材料尚未成立",str(result.get("message","请先核对材料")))
		return
	_accept_red_inspection(id, result)
	var title: String = Content.observation_title(id)
	await _show_red_inspection_record(id, title)

func _accept_red_inspection(id: String, result: Dictionary) -> void:
	red_seen[id] = result
	_refresh_site_title(id)
	red_details["E09" if id.begins_with("E09.") else id] = id
	evidence[id] = true
	evidence_sources[id] = "具名转述" if id == "E12" else "亲见"
	if id == "E01": receipt_checked = true
	if id == "E09.deep": red_seen["E09"] = result; evidence["E09"] = true
	for title in INSPECTION_SCENES.get(id, []): _queue_story(title)

func _show_red_inspection_record(id: String, title: String) -> void:
	# Use the same dialogue resource and state evaluator as the spoken view.
	# Only the successfully inspected object's observation is read here.
	red_inspection_loading = true
	var record: Dictionary = await ObservationReader.read(dialogue, RED_DIALOGUE, title, self)
	red_inspection_loading = false
	if record.status == "interactive":
		red_note_id = id
		red_note_history_start = dialogue.history.size()
		_begin_dialogue(title)
		return
	if record.status != "complete":
		_show_message("检视尚未完成", "本次记录未能完整展开，请重新检视。" if record.status == "cycle" else "未取得本次材料记录，请重新检视。")
		return
	if room_sites.has(id) and not _accept_room_inspection(id): return
	var paragraphs: Array[String] = record.paragraphs
	var line_ids: Array[String] = record.line_ids
	red_notes[id] = paragraphs
	_update_status()
	if can_use_mind_reasoning(): _queue_story("S09")
	for index in paragraphs.size():
		dialogue.history.append(paragraphs[index])
		dialogue.history_line_ids.append(line_ids[index])
	_open_recorded_evidence(id, false)

func _open_journal() -> void:
	var records := _recorded_evidence_records()
	if records.is_empty():
		_show_message("证物页", "尚未完成任何检视记录。")
		return
	var selected_id := str(records[0][0])
	for record in records:
		if str(record[0]) == last_recorded_evidence:
			selected_id = last_recorded_evidence
			break
	_open_recorded_evidence(selected_id, false)

func _recorded_evidence_title(id: String) -> String:
	if room_sites.has(id): return str(room_sites[id][0])
	if red_sites.has(id): return str(red_sites[id][0])
	return "上游捕集片" if id == "E09.deep" else "同号转清与停流联"

func _recorded_evidence_records() -> Array:
	var records: Array = []
	for id in red_notes:
		if not room_seen.has(id) and not red_seen.has(id): continue
		if not room_sites.has(id) and not red_sites.has(id) and id not in ["E09.deep", "E09.completion"]: continue
		records.append([str(id), _recorded_evidence_title(str(id))])
	return records

func _open_recorded_evidence(id: String, return_to_notebook: bool) -> void:
	if not red_notes.has(id) or (not red_seen.has(id) and not room_seen.has(id)): return
	var records := _recorded_evidence_records()
	var book = modal.get_node_or_null("EvidenceNotebook") if modal.current_mode == "evidence" else null
	if not is_instance_valid(book):
		_open_modal("", "evidence")
		book = EvidenceNotebook.instantiate()
		modal.add_child(book)
	else:
		if is_instance_valid(red_notebook): red_notebook.hide()
		modal.show()
		modal_open = true
		mode = "evidence"
	var return_to_reasoning := return_to_notebook and can_use_mind_reasoning()
	var actions: Array = [[3, _open_red_notebook]] if return_to_reasoning else [[2, _close_modal]]
	if not return_to_reasoning and not room_sites.has(id) and id != "E07":
		actions.push_front([0, func(): _return_to_inspection(id)])
	book.configure(id, _recorded_evidence_title(id), red_notes[id], actions)
	book.set_records(records, id, func(next_id: String): _open_recorded_evidence(next_id, return_to_notebook))
	last_recorded_evidence = id
	modal_locale_refresh = func(): _open_recorded_evidence(id, return_to_notebook)

func _open_red_feedback() -> void:
	if not can_use_mind_reasoning(): return
	_open_modal("推演差异与已读材料", "red")
	_label(modal_box,str(red_feedback.get("message", "尚未执行推演。")),20)
	for message in red_feedback.get("conflicts", []):
		_label(modal_box,"冲突 · " + str(message),18)
	for message in red_feedback.get("unproven", []):
		_label(modal_box,"待证 · " + str(message),18)
	_label(modal_box,"查阅已经读过的材料",20)
	for id in red_notes:
		_button(modal_box,id + " · " + _recorded_evidence_title(id),func(): _open_recorded_evidence(id, true))
	_pin_red_footer([["返回推演",_open_red_notebook]])

func _request_flow() -> void:
	if not can_use_mind_reasoning(): return
	_open_red_round(round_number)

func _open_red_round(number: int) -> void:
	if not can_use_mind_reasoning(): return
	round_number = number
	_open_red_notebook()
	if is_instance_valid(deduction_view):
		deduction_view.current_mode = "motivation" if number == 2 else "sequence"
		deduction_view.refresh()

func _evaluate_red(number: int) -> void:
	var candidate := {}
	for fragment in drafts: candidate.merge(fragment,true)
	var result: Dictionary = red.call("EvaluateRound",number,candidate)
	red_feedback = result
	verified[number-1] = result.get("ok",false)
	if number == 1: _queue_story("S10")
	if number == 2 and verified[1]:
		_queue_story("S16")
		_queue_story("S17")
	if number == 3: _queue_story("S21" if verified[2] else "S20")
	set_meta("last_predictions", result.get("predictions", {}))
	if is_instance_valid(replay_summary):
		replay_summary.text = str(result.get("message","这一片段尚无材料可验证。"))
	_install_red_snapshots(result.get("snapshots",[]))
	if is_instance_valid(deduction_view):
		deduction_view.refresh()

func _load_competitor(kind: String) -> void:
	drafts = [
		{"cloth_before_flow":"yes" if kind == "H1" else "no","flow":"ash" if kind == "H1" else "water","wipe":"after" if kind == "H1" else "before","wedge":"dirty_then_wrap"},
		{"engineer_exception":"read","holder":"engineer","cook_filter":"yes" if kind == "H1" else "no","sound_gap":"quiet","sound_pipe":"ringing","release":"after_confirmation","completion_record":"actual" if kind == "H1" else "mistaken"},
		{"returned":"no" if kind == "H1" else "yes","take":"never" if kind == "H1" else "after_return","return_before_room":"no" if kind == "H1" else "yes","identity":"unknown"}
	]
	verified = [false,false,false]
	_evaluate_red(3)
	if is_instance_valid(replay_summary):
		replay_summary.text = "H1：排灰后清理，未交回红筹，带着未结工作返房。接收印与签条不相容。" if kind == "H1" else "H2：先擦地后清水洗布，误记完成，器具交回后开屉再取。上游捕集片灰印与此冲突。"

func _open_red_notebook() -> void:
	if not can_use_mind_reasoning(): return
	modal_locale_refresh = Callable()
	if not is_instance_valid(red_notebook):
		red_notebook = preload("res://scenes/FirstAwakening/MindNotebook.tscn").instantiate()
		for child in red_notebook.get_children():
			if child is CanvasItem: child.visible = child.name in ["Paper","StageMount","Transport"]
		ui.add_child(red_notebook)
		red_notebook.get_node("Transport").setup(self)
		replay_states = [{"ids":[],"closed":true,"inner_bolt":false,"outer_bar":false,"caption":"候选历史尚未提交"}]
		replay_stage = preload("res://scenes/FirstAwakening/ReplayStage.tscn").instantiate()
		replay_stage.set_script(preload("res://scenes/FirstAwakening/RedTokenReplayStage.gd"))
		replay_stage.source = self
		red_notebook.get_node("StageMount").add_child(replay_stage)
		
		# Unified Deduction View (Event Sequence & Motivation Deduction)
		deduction_view = preload("res://scenes/FirstAwakening/RedTokenDeductionView.gd").new()
		red_notebook.add_child(deduction_view)
		deduction_view.setup(self)
		deduction_view.return_requested.connect(_close_modal)
		replay_summary = deduction_view.finding_label
		red_notebook_box = deduction_view.right_content
	modal.hide()
	red_notebook.show()
	modal_open = true
	mode = "rednotebook"
	if is_instance_valid(deduction_view):
		deduction_view.refresh()

func _install_red_snapshots(snapshots: Array) -> void:
	replay_events.clear()
	replay_states.clear()
	for snapshot in snapshots:
		var visible: Dictionary = snapshot.duplicate(true)
		visible["valve_open"] = visible.get("flowing",false)
		for field in {"ids":[],"closed":true,"inner_bolt":false,"outer_bar":false}:
			if not visible.has(field): visible[field] = {"ids":[],"closed":true,"inner_bolt":false,"outer_bar":false}[field]
		replay_states.append(visible)
		if replay_states.size() > 1: replay_events.append(str(visible.get("caption","动作提交")))
	if replay_states.is_empty(): replay_states.append({"ids":[],"closed":true,"inner_bolt":false,"outer_bar":false})
	replay_index = 0
	replay_accum = 0.0
	replay_fast = false
	replay_playing = not replay_events.is_empty()
	replay_stage.events = replay_events
	replay_stage.frame = replay_index
	replay_stage.last_frame = -1
	replay_stage.progress = 0.0

func _replay_step(amount: int, _mark_reached := true) -> void:
	replay_index = clampi(replay_index + amount,0,replay_events.size())
	if is_instance_valid(replay_stage): replay_stage.frame = replay_index; replay_stage.progress = 0.0

func _start_replay(fast := false) -> void:
	replay_fast = fast
	if replay_index >= replay_events.size(): replay_index = 0
	replay_accum = 0.0
	replay_playing = not replay_events.is_empty()

func _open_red_actions() -> void:
	_open_modal("安全模型：身体、工具与持续岗位", "red")
	for operation in [["工程师读并接受具名特例","工程师霍铎","read_exception","B"],["工程师从器材架取柄","工程师霍铎","take_handle","B"],["主厨先夹滤布","主厨沈鼎","clip_filter","C"],["主厨确认夹牢","主厨沈鼎","confirm_filter","C"],["工程师持柄保持排灰","工程师霍铎","hold","B"],["主厨检查当班捕集灰","主厨沈鼎","capture","C"],["主厨盆侧观察出水转清并确认","主厨沈鼎","inspect_clear","C"],["工程师释放柄","工程师霍铎","release","B"],["工程师去门厅插内销","工程师霍铎","bolt","D"],["工程师交回柄","工程师霍铎","deposit_other","A"],["值守者交回红筹","工程师霍铎","deposit_red","A"]]:
		_button(modal_box,operation[0],func(): _perform_red(operation.slice(1)))
	_button(modal_box,"取消本次持续动作（保留已提交灰印）",func(): red.call("Cancel"); _show_message("持续动作已取消","水流停止，已留下的灰与已提交交接历史保留。"))
	var actions: Array = [["返回现场",_close_modal]]
	if can_use_mind_reasoning(): actions.push_front(["返回假说",_request_flow])
	_pin_red_footer(actions)

func _perform_red(pair: Array) -> void:
	var result: Dictionary = red.call("Perform",pair[0],pair[1],pair[2])
	_show_message("动作提交",str(result.get("message","本次动作没有提交。")))

func _restore_red_scroll() -> void:
	if not is_instance_valid(modal) or not modal.has_node("RedPageLayout"): return
	var layout: VBoxContainer = modal.get_node("RedPageLayout")
	var scroll: ScrollContainer = modal_box.get_parent()
	layout.remove_child(scroll)
	modal.remove_child(layout)
	modal.add_child(scroll)
	scroll.size_flags_vertical = Control.SIZE_FILL
	layout.queue_free()

func _pin_red_footer(actions: Array, status := "") -> void:
	# Only long Red Token pages use this wrapper. The body scrolls independently.
	var scroll: ScrollContainer = modal_box.get_parent()
	modal.remove_child(scroll)
	var layout := VBoxContainer.new()
	layout.name = "RedPageLayout"
	layout.custom_minimum_size.y = modal.regular_bounds.size.y - modal.get_theme_stylebox("panel").get_minimum_size().y
	layout.add_theme_constant_override("separation", 10)
	modal.add_child(layout)
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	layout.add_child(scroll)
	var footer := VBoxContainer.new()
	footer.name = "RedFixedFooter"
	layout.add_child(footer)
	var rule := ColorRect.new()
	rule.color = FieldModal.RULE
	rule.custom_minimum_size.y = 1
	footer.add_child(rule)
	if status != "": _label(footer, status, 17)
	var row := GridContainer.new()
	row.columns = 2
	row.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_theme_constant_override("h_separation", 12)
	row.add_theme_constant_override("v_separation", 8)
	footer.add_child(row)
	for action in actions:
		var button := _button(row, action[0], action[1])
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		if str(action[0]).begins_with("提交"): _style_primary(button)
	_layout_red_fixed_page.call_deferred()

func _style_primary(button: Button) -> void:
	# The one decisive action on a page reads as filled ink, not another paper tab.
	var ink := StyleBoxFlat.new()
	ink.bg_color = Color("344c39")
	ink.border_color = Color("213c27")
	ink.set_border_width_all(1)
	ink.border_width_bottom = 3
	ink.set_corner_radius_all(2)
	ink.set_content_margin_all(8)
	var hover := ink.duplicate() as StyleBoxFlat
	hover.bg_color = Color("3f5c45")
	var pressed := ink.duplicate() as StyleBoxFlat
	pressed.bg_color = Color("2a3e2e")
	pressed.border_width_bottom = 1
	for state in ["normal", "focus"]: button.add_theme_stylebox_override(state, ink)
	button.add_theme_stylebox_override("hover", hover)
	button.add_theme_stylebox_override("pressed", pressed)
	for key in ["font_color", "font_hover_color", "font_focus_color", "font_pressed_color"]:
		button.add_theme_color_override(key, Color("fff8e8"))

func _layout_red_fixed_page() -> void:
	# Run after FieldModal's short-card fitting so long pages keep their bounds.
	await get_tree().process_frame
	await get_tree().process_frame
	if not modal.has_node("RedPageLayout"): return
	modal.position = modal.regular_bounds.position
	modal.size = modal.regular_bounds.size

func _red_defense_sections() -> Array:
	return Content.defense_sections()

func _red_defense_statements() -> Dictionary:
	return Content.defense_statements()

func _defense_option_text(options: Array, value: String) -> String:
	return red_defense_view._defense_option_text(options, value)

func _open_red_defense() -> void:
	red_defense_view._open_red_defense()

func _red_defense_status() -> String:
	return red_defense_view._red_defense_status()

func _style_red_statement(button: Button, selected: bool) -> void:
	red_defense_view._style_red_statement(button, selected)

func _defense_choice(key: String, options: Array, answer: Dictionary, parent: Node = null, caption := "") -> void:
	red_defense_view._defense_choice(key, options, answer, parent, caption)

func _open_red_inquiry() -> void:
	var known: Array = red.call("State").get("known", [])
	if "E16" in known:
		_open_recorded_evidence("E16", false)
		return
	if not ("E13" in known and "E15" in known):
		_show_message("现场材料未齐", "请先检视归还匣与椅上红筹，再就实际痕迹发问。")
		return
	_open_modal("铜口 · 就现场痕迹发问", "red")
	_label(modal_box, "归还匣与椅上的痕迹已经记下。选择你要请接待者核验的问题；他的回答仍须与原物、当班记录相对照。", 20)
	_button(modal_box, "归还匣的钢印留着同一缺角。\n“你能查到接收后，谁还接触过前屉吗？”", func(): _request_red_inquiry_record("归还匣接收后的接触记录"))
	_button(modal_box, "前封已断，拉手留下擦痕，椅上筹带较窄半手印。\n“这些是一次什么样的移物？请拿现场记录核对。”", func(): _request_red_inquiry_record("断封、拉手与椅上筹的移物记录"))
	if "E14" in known:
		_button(modal_box, "四人的返房签条已有记录。\n“能否查一查返房完成之后的巡检？”", func(): _request_red_inquiry_record("返房之后的巡检记录"))
	_pin_red_footer([["返回领取柜",func(): _open_red_interaction("E01")],["返回现场",_close_modal]])

func _request_red_inquiry_record(question: String) -> void:
	# Several material-based questions request the same real record. The existing
	# Inspect path owns availability and acquisition; no host-created evidence.
	dialogue.history.append(_localized_speaker("侦探") + "\n请求核验：" + question)
	dialogue.history_line_ids.append("")
	_inspect_red("E16", true)

func _safe_exit() -> void:
	if is_instance_valid(red_performance): red_performance.cancel()
	if is_instance_valid(story_director): story_director.cancel()
	red.call("Cancel")
	ended = true
	_open_modal("安全结束本 Demo", "red")
	_label(modal_box,"生活支路已停流，实验动力保持断联。饮水与食物已确认。四人可在安全起居处休息；本 Demo 在生活翼结束布置，无需认罪或过题。",21)
	modal.add_footer([["重新开始",func(): get_tree().reload_current_scene()]])

func _unhandled_input(event: InputEvent) -> void:
	if is_instance_valid(red_performance) and red_performance.busy:
		if event.is_action_pressed("ui_cancel"): red_performance.cancel()
		get_viewport().set_input_as_handled()
		return
	if name_entry_active:
		get_viewport().set_input_as_handled()
		return
	if story_lock: return
	if modal_open and modal.current_mode == "evidence" and not dialogue.active and event is InputEventKey and event.pressed and not event.echo:
		get_viewport().set_input_as_handled()
		if event.keycode in [KEY_SHIFT, KEY_CTRL, KEY_ALT, KEY_META]: return
		var book := modal.get_node_or_null("EvidenceNotebook")
		var turn := -1 if event.keycode in [KEY_LEFT, KEY_PAGEUP] else 1 if event.keycode in [KEY_RIGHT, KEY_PAGEDOWN] else 0
		# Arrow keys turn pages; any other key returns to the field as before.
		if turn != 0:
			if is_instance_valid(book): book.step(turn)
			return
		_close_modal()
		return
	if event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_R and not dialogue.active:
		# A stray R must not wipe the run: the first press asks, a second confirms.
		if restart_pending: _restart_case()
		else: _confirm_restart()
		get_viewport().set_input_as_handled()
		return
	if event is InputEventKey and event.pressed and not event.echo and not dialogue.active and not modal_open and not ended:
		if get_viewport().gui_get_focus_owner() is LineEdit: return
		if event.keycode == KEY_J:
			_open_journal()
			get_viewport().set_input_as_handled()
			return
		if event.keycode == KEY_F and can_use_mind_reasoning():
			_request_flow()
			get_viewport().set_input_as_handled()
			return
	super._unhandled_input(event)

func _confirm_restart() -> void:
	if ended:
		_restart_case()
		return
	_open_modal("重新开始？", "message")
	_label(modal_box, "当前调查进度不会保存，将回到醒来的房间。
再按一次 R 也可确认。", 21)
	modal.add_footer([["继续调查", _close_modal], ["重新开始", _restart_case]])
	restart_pending = true

func _restart_case() -> void:
	red.call("CancelPreparedAction")
	red.call("Cancel")
	get_tree().reload_current_scene()

func _close_modal() -> void:
	restart_pending = false
	if name_entry_active: return
	if is_instance_valid(red_performance) and red_performance.busy:
		red_performance.cancel()
		return
	modal_locale_refresh = Callable()
	modal.hide()
	if is_instance_valid(red_notebook): red_notebook.hide()
	replay_playing = false
	modal_open = false
	mode = ""
	_flush_story.call_deferred()

func _queue_story(title: String) -> void:
	if story_played.has(title) or title in story_backlog: return
	story_backlog.append(title)

func _flush_story() -> void:
	# Conversations need the companions present and never interrupt open pages.
	if story_backlog.is_empty() or ended or modal_open: return
	if not (red_companions_met and copper_introduction_complete): return
	if dialogue == null: return
	if story_lock or dialogue.active or (is_instance_valid(red_performance) and red_performance.busy):
		# A line or blocking is still finishing; try again next frame.
		if not get_tree().process_frame.is_connected(_flush_story):
			get_tree().process_frame.connect(_flush_story, CONNECT_ONE_SHOT)
		return
	var title: String = story_backlog.pop_front()
	story_played[title] = true
	_begin_dialogue(title)

func _dress_detective(coat_on: bool) -> void:
	# The coat hangs on the R-05 stand until the player takes it in ROOM_COAT.
	coat_worn = coat_on
	var frames: SpriteFrames = (DETECTIVE_COAT if coat_on else DETECTIVE_SHIRT).duplicate()
	for direction_name in ["front", "back", "left"]:
		frames.set_animation_speed("walk_" + direction_name, 7.5)
	var was_standard: bool = player.sprite.sprite_frames == player.standard
	var animation: StringName = player.sprite.animation
	var frame: int = player.sprite.frame
	var playing: bool = player.sprite.is_playing()
	player.standard = frames
	if was_standard:
		player.sprite.sprite_frames = frames
		player.sprite.play(animation)
		player.sprite.frame = frame
		if not playing: player.sprite.pause()
	var stand := scene_items.get_node_or_null("CoatStand0") as Sprite2D
	if is_instance_valid(stand): stand.texture = COAT_STAND_EMPTY if coat_on else COAT_STAND_COAT

func _exit_tree() -> void:
	if is_instance_valid(red_performance): red_performance.cancel()
	if is_instance_valid(story_director): story_director.cancel()
	if is_instance_valid(red): red.call("Cancel")

func _button(parent: Node, words: String, action: Callable, disabled := false) -> Button:
	var button := super._button(parent,words,action,disabled)
	button.clip_text = false
	button.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var paper := preload("res://scenes/FirstAwakening/paper_button_theme.tres")
	for state in ["normal","hover","pressed","disabled","focus"]:
		button.add_theme_stylebox_override(state,paper.get_stylebox(state,"Button"))
	return button
