extends Node2D
## First Awakening: a single connected 32 px map. The feet are the actor origins.

const EvidenceDiagram = preload("res://scenes/FirstAwakening/EvidenceDiagram.gd")
const FieldNavigation = preload("res://scenes/FirstAwakening/FieldNavigation.gd")
const FieldModal = preload("res://scenes/FirstAwakening/FieldModal.gd")
const DIALOGUE = preload("res://scenes/FirstAwakening/first_awakening.dialogue")
const LAYOUT: Resource = preload("res://ShareAsset/Internal/Props/FirstAwakeningInterior/layout.tres")
const SITE_CATALOG: Resource = preload("res://scenes/FirstAwakening/sites.tres")
const ANSWER_CATALOG: Resource = preload("res://scenes/FirstAwakening/answers.tres")
const INVESTIGATION_CONTENT: Resource = preload("res://scenes/FirstAwakening/investigation_content.tres")
const AnswerRules = preload("res://scenes/FirstAwakening/AnswerRules.gd")
const ZHAOHUA_FONT: Font = preload("res://ShareAsset/External/ZhaohuaTypeWriter/ZhaohuaTypeWriter.ttf")
const NEW_TEGOMIN_FONT: Font = preload("res://ShareAsset/External/newtegomin/NewTegomin-Regular.ttf")
const FIELD_THEME_ZH: Theme = preload("res://scenes/FirstAwakening/field_theme_zh.tres")
const FIELD_THEME_OTHER: Theme = preload("res://scenes/FirstAwakening/field_theme_other.tres")
const CLICK_STOP_RADIUS := 8.0
const APPROACH_DIRECTIONS := [Vector2.UP, Vector2.DOWN, Vector2.LEFT, Vector2.RIGHT, Vector2(-1, -1), Vector2(1, -1), Vector2(-1, 1), Vector2(1, 1)]

var floors: Dictionary = {}
var obstacle_rects: Array[Rect2] = []
var layout: Dictionary = {}
var sites: Dictionary = {}
var active_sites: Dictionary = {}
var evidence: Dictionary = {}
var actors: Dictionary = {}
var npc_stage_seen: Dictionary = {}
@onready var scene_items: Node2D = $ObjectsAndPeople
var player: CharacterBody2D
var click_path := PackedVector2Array()
var click_waypoint := 0
var click_interaction_id := ""
var camera: Camera2D
var nearest := ""
var reachable: Array[String] = []
var cycle_offset := 0
var last_cell := Vector2i(-1, -1)
var last_proximity_position := Vector2(INF, INF)
var proximity_refresh_frames := 0
var minute := 7 * 60 + 20
var inner_bolt := true
var viewing_outer_bar := false
var broadcast_heard := false
var meeting_done := false
var captain := false
var ended := false
var ending_title := ""
var disputed := {"prison": false, "defense": false, "escape": false}
var neutral_actions_unlocked := false
var ui: CanvasLayer
var modal: FieldModal
var modal_box: VBoxContainer
var hud_title: Label
var hud_hint: Label
var hud_lead: Label
var hud_time: Label
var modal_open := false
var mode := ""
var modal_locale_refresh: Callable
var language_selector: OptionButton
var active_locale_theme: Theme
var pending_close := false
var transmitter_rules := false
var transmitter_connected := false
var transmitter_answer_seen := false
var preliminary_answer_attempted := false
var bridge: Node
var bolt_visual: CanvasItem
var sim_actions: Array[String] = []
var sim_actor_actions: Dictionary = {}
var mind_text := ""
var mind_drafts: Dictionary = {}
var mind_person := "侦探"
var mind_dirty := false
var mind_note := ""
var mind_trace := 0
var sim_result: Dictionary = {}
var sim_checks: Dictionary = {}
var pending_inspections: Dictionary = {}
var conjectures: Dictionary = {}
var checks_this_run := 0
var replay_events: Array[String] = []
var replay_states: Array = []
var replay_pause_points: Dictionary = {}
var replay_pause_display_points: Dictionary = {}
var replay_pause_ids: Dictionary = {}
var replay_index := 0
var replay_playing := false
var replay_fast := false
var replay_accum := 0.0
var replay_stage: Control
var replay_status: Label
var replay_feedback: VBoxContainer
var replay_reached: Dictionary = {}
var reasoning_music: AudioStreamPlayer
var reasoning_music_enabled := true
var reasoning_music_tween: Tween
var answer_slots := {"place": "", "door": "", "outer": ""}
var answer_slot_display_ids := {"place": "", "door": "", "outer": ""}
var dialogue: CanvasLayer
var dialogue_after := ""
var meeting_introduction_seen := false
var mind_introduction_seen := false

func _notification(what: int) -> void:
	if what != NOTIFICATION_TRANSLATION_CHANGED:
		return
	if is_instance_valid(player):
		_pick_nearest()
		_update_status()
	if is_instance_valid(language_selector):
		_refresh_language_selector()
	_apply_locale_theme()
	if not sim_result.is_empty() and not sim_actions.is_empty():
		call_deferred("_refresh_replay_locale")
	if modal_open and modal_locale_refresh.is_valid():
		call_deferred("_refresh_modal_locale")
	if is_instance_valid(dialogue) and dialogue.active and dialogue.line != null:
		dialogue.speaker.text = _localized_speaker(dialogue.line.character)
		dialogue.body.text = dialogue.line.text

func _refresh_modal_locale() -> void:
	if modal_open and modal_locale_refresh.is_valid():
		modal_locale_refresh.call()

func _refresh_replay_locale() -> void:
	if sim_result.is_empty() or sim_actions.is_empty():
		return
	var old_index := replay_index
	var was_playing := replay_playing
	var program := _compile_program()
	if program.states.is_empty():
		return
	replay_states = program.states
	sim_result = replay_states.back()
	replay_events = program.events
	replay_index = clampi(old_index, 0, replay_events.size())
	replay_playing = was_playing
	_build_replay_pause_points()
	_refresh_replay_status()
	_refresh_replay_feedback()

func _localized_speaker(canonical_name: String) -> String:
	var ids: Dictionary = INVESTIGATION_CONTENT.speaker_name_ids
	return tr(str(ids[canonical_name])) if ids.has(canonical_name) else canonical_name

func _refresh_language_selector() -> void:
	var names := [tr("FA_UI_021"), "English", tr("FA_UI_111")]
	for index in names.size():
		language_selector.set_item_text(index, names[index])
	language_selector.select(["zh_CN", "en", "ja"].find(GameLocale.current_language()))

func _apply_locale_theme() -> void:
	var font: Font = ZHAOHUA_FONT if GameLocale.current_language() == "zh_CN" else NEW_TEGOMIN_FONT
	var theme := FIELD_THEME_ZH if GameLocale.current_language() == "zh_CN" else FIELD_THEME_OTHER
	active_locale_theme = theme
	if is_instance_valid(ui):
		for control in ui.find_children("*", "Control", true, false):
			control.theme = theme
		if is_instance_valid(modal):
			modal.theme = theme
	if is_instance_valid(dialogue):
		dialogue.call("apply_locale_font", font, theme)

func _on_language_selected(index: int) -> void:
	GameLocale.set_language(["zh_CN", "en", "ja"][index])

func _localized_join(items: Array, separator := "、") -> String:
	var localized := PackedStringArray()
	for item in items:
		localized.append(tr(str(item)))
	if separator == "、" and GameLocale.current_language() == "en":
		separator = ", "
	return separator.join(localized)

func _translate_display_id_sequence(value: String) -> String:
	var localized := PackedStringArray()
	for id in value.split("|"):
		if id == "LIST":
			localized.append(", " if GameLocale.current_language() == "en" else "、")
		else:
			localized.append(tr(id))
	return "".join(localized)

func _ready() -> void:
	texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	get_window().content_scale_size = Vector2i(1280, 992)
	get_window().content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	get_window().content_scale_stretch = Window.CONTENT_SCALE_STRETCH_INTEGER
	get_window().size = Vector2i(1280, 992)
	get_window().title = tr("FA_UI_078")
	_build_geometry()
	_load_sites()
	_isolation_door()
	_build_characters()
	var hints: Node2D = $InteractionHints
	hints.set("source", self)
	_build_ui()
	dialogue = $Dialogue
	dialogue.layer = ui.layer + 1
	_apply_locale_theme()
	dialogue.use_compact_layout()
	dialogue.heading.text = tr("FA_UI_050")
	dialogue.panel.hide()
	dialogue.line_shown.connect(_on_dialogue_line_shown)
	dialogue.finished.connect(_on_dialogue_finished)
	bridge = $HallReplayBridge
	reasoning_music = $ReasoningMusic
	var loop_stream := reasoning_music.stream.duplicate() as AudioStreamMP3
	loop_stream.loop = true
	reasoning_music.stream = loop_stream
	reasoning_music.volume_db = -12.0
	reasoning_music.bus = &"Master"
	reasoning_music.play()
	camera = $Camera2D
	camera.position = player.position
	camera.make_current()
	_begin_dialogue("opening")

func _build_geometry() -> void:
	layout = LAYOUT.data
	for y in layout.rows.size():
		var row: String = layout.rows[y]
		for x in row.length():
			var mark := row.substr(x, 1)
			var cell := Vector2i(x, y)
			if mark in [".", "D"]:
				floors[cell] = true
	for item in layout.furniture:
		if item.footprint != null:
			var raw: Array = item.footprint
			obstacle_rects.append(Rect2(raw[0], raw[1], raw[2], raw[3]))
	obstacle_rects.append(Rect2(layout.stop.now[0] - 10, layout.stop.now[1] - 8, 20, 8))

func _load_sites() -> void:
	for entry in SITE_CATALOG.entries:
		sites[entry.id] = entry

func _site_entered(id: String) -> void:
	active_sites[id] = true
	if is_instance_valid(player):
		_pick_nearest()

func _site_exited(id: String) -> void:
	active_sites.erase(id)
	if is_instance_valid(player):
		_pick_nearest()

func _build_characters() -> void:
	for id in ["侦探", "工程师", "厨师", "音乐家"]:
		actors[id] = scene_items.get_node(id)
	player = actors["侦探"]
	# A local copy preserves the shared walk cadence outside this scene.
	player.standard = player.standard.duplicate()
	for direction_name in ["front", "back", "left"]:
		player.standard.set_animation_speed("walk_" + direction_name, 7.5)
	player.sprite.sprite_frames = player.standard
	player.pose("idle", "front")
	player.sit_at("front", 4.0)
	actors["工程师"].pose("idle", "left")
	actors["厨师"].pose("idle", "left")
	actors["音乐家"].pose("idle", "back")

func _leave_bed() -> void:
	player.position = $WakePosition.position
	player.stand_up()

func _isolation_door() -> void:
	bolt_visual = scene_items.get_node("MechanicalIsolationDoor/InnerBolt")

func _physics_process(_delta: float) -> void:
	if not is_instance_valid(player):
		return
	if camera:
		camera.position = Vector2(clampf(player.position.x, 320, 960), clampf(player.position.y - 24, 248, 776)).round()
	# Area2D overlap notifications trail direct position changes by a physics tick.
	if player.position.distance_to(last_proximity_position) > 16.0:
		proximity_refresh_frames = 16
	last_proximity_position = player.position
	var cell := Vector2i(player.position / 16.0)
	if cell != last_cell:
		last_cell = cell
		cycle_offset = 0
	_pick_nearest()
	proximity_refresh_frames = maxi(0, proximity_refresh_frames - 1)
	_update_status()
	if modal_open or dialogue.active or ended:
		if not player.seated: player.move_input(Vector2.ZERO)
		return
	var direction := Vector2(float(Input.is_physical_key_pressed(KEY_D) or Input.is_physical_key_pressed(KEY_RIGHT)) - float(Input.is_physical_key_pressed(KEY_A) or Input.is_physical_key_pressed(KEY_LEFT)), float(Input.is_physical_key_pressed(KEY_S) or Input.is_physical_key_pressed(KEY_DOWN)) - float(Input.is_physical_key_pressed(KEY_W) or Input.is_physical_key_pressed(KEY_UP)))
	if direction.length_squared() > 0:
		click_path.clear()
		click_interaction_id = ""
	if player.seated and direction.length_squared() > 0:
		_leave_bed()
	if direction == Vector2.ZERO and click_interaction_id != "":
		var target_id := click_interaction_id
		if active_sites.has(target_id) or player.position.distance_to(_site_point(target_id)) <= float(sites[target_id].reach) + 4.0:
			click_path.clear()
			click_interaction_id = ""
			player.move_input(Vector2.ZERO)
			_interact(target_id)
			return
	if direction == Vector2.ZERO and not click_path.is_empty():
		while click_waypoint < click_path.size() and player.position.distance_to(click_path[click_waypoint]) <= CLICK_STOP_RADIUS:
			click_waypoint += 1
		if click_waypoint >= click_path.size():
			click_path.clear()
			if click_interaction_id != "":
				var target_id := click_interaction_id
				click_interaction_id = ""
				player.move_input(Vector2.ZERO)
				if active_sites.has(target_id) or player.position.distance_to(_site_point(target_id)) <= float(sites[target_id].reach) + 8.0:
					_interact(target_id)
					return
		else:
			direction = player.position.direction_to(click_path[click_waypoint])
	if not player.seated: player.move_input(direction.normalized())

func _pick_nearest() -> void:
	var facing := {"front": Vector2.DOWN, "back": Vector2.UP, "left": Vector2.LEFT, "right": Vector2.RIGHT}.get(player.facing, Vector2.DOWN) as Vector2
	var scored: Array = []
	var candidates: Array = sites.keys() if proximity_refresh_frames > 0 else active_sites.keys()
	for id in candidates:
		var site: Dictionary = sites[id]
		var target: Vector2 = actors[site.title].position if actors.has(site.title) else site.point
		var offset := target - player.position
		var distance := offset.length()
		if distance > site.reach:
			continue
		# Prefer what the detective faces, then what is still unexamined.
		var ahead := 1.0 if distance < 4.0 else offset.normalized().dot(facing)
		var score: float = distance * (1.0 if _has_new_at(id) else 1.4) * (1.35 - 0.35 * ahead)
		scored.append([score, id])
	scored.sort_custom(func(a, b): return a[0] < b[0])
	reachable.clear()
	for entry in scored:
		reachable.append(entry[1])
	if reachable.is_empty():
		nearest = ""
		cycle_offset = 0
	else:
		nearest = reachable[cycle_offset % reachable.size()]
	if hud_hint:
		if nearest == "":
			hud_hint.text = tr("FA_UI_011")
		else:
			var others := tr("FA_UI_002") % [cycle_offset % reachable.size() + 1, reachable.size()] if reachable.size() > 1 else ""
			hud_hint.text = tr("FA_UI_190") + tr(sites[nearest].title_id) + others + tr("FA_UI_001")

func _process(delta: float) -> void:
	hud_hint.visible = not modal_open and not dialogue.active
	if is_instance_valid(hud_lead):
		hud_lead.visible = not modal_open and not dialogue.active
	if is_instance_valid(replay_stage) and modal_open and mode == "flow" and replay_pause_points.has(replay_index):
		replay_stage.queue_redraw()
	if not replay_playing or not modal_open or mode != "flow":
		return
	replay_accum += delta
	var duration := 0.65 / 4.0 if replay_fast else 0.65
	if is_instance_valid(replay_stage):
		replay_stage.set("progress", clampf(replay_accum / duration, 0.0, 1.0))
		replay_stage.queue_redraw()
	if replay_accum < duration:
		return
	replay_accum = 0
	_replay_step(1)
	if replay_index >= replay_events.size():
		replay_playing = false

func _input(event: InputEvent) -> void:
	if dialogue != null and dialogue.active:
		return
	if event is not InputEventMouseButton or not event.pressed or event.button_index != MOUSE_BUTTON_LEFT or modal_open or ended:
		return
	if language_selector.get_global_rect().has_point(event.position) or language_selector.get_popup().visible:
		return
	var destination: Vector2 = get_global_transform_with_canvas().affine_inverse() * event.position
	var site_id := _clicked_site(destination)
	if site_id == "":
		click_interaction_id = ""
		_move_to_click(destination)
	else:
		_approach_site(site_id)
	get_viewport().set_input_as_handled()

func _unhandled_input(event: InputEvent) -> void:
	if dialogue != null and dialogue.active:
		return
	if event is InputEventMouseButton and event.pressed and modal_open and mode == "detail":
		if event.button_index not in [MOUSE_BUTTON_WHEEL_UP, MOUSE_BUTTON_WHEEL_DOWN] and not Rect2(modal.position, modal.size).has_point(event.position):
			_close_modal()
		return
	if event is not InputEventKey or not event.pressed or event.echo:
		return
	if ended and event.keycode == KEY_R:
		get_tree().reload_current_scene()
		return
	if modal_open:
		if event.keycode == KEY_ESCAPE:
			_close_modal()
		elif mode == "detail" and event.keycode not in [KEY_UP, KEY_DOWN, KEY_PAGEUP, KEY_PAGEDOWN]:
			_close_modal()
		return
	if ended:
		if event.keycode == KEY_R:
			get_tree().reload_current_scene()
		return
	if event.keycode in [KEY_E, KEY_ENTER] and nearest != "":
		_interact(nearest)
	elif event.keycode == KEY_TAB and reachable.size() > 1:
		cycle_offset += 1
		_pick_nearest()
	elif event.keycode == KEY_J:
		_open_journal()
	elif event.keycode == KEY_F:
		_request_flow()

func _move_to_click(destination: Vector2) -> void:
	if not _walkable(destination):
		return
	if player.seated:
		_leave_bed()
	if player.position.distance_to(destination) <= CLICK_STOP_RADIUS:
		click_path.clear()
		return
	var route := _route(player.position, destination)
	if route.is_empty():
		return
	click_path = route
	click_path.append(destination)
	click_waypoint = 0

func _site_point(id: String) -> Vector2:
	return FieldNavigation.site_point(id, sites, actors)

func _clicked_site(position: Vector2) -> String:
	return FieldNavigation.clicked_site(position, sites, actors)

func _approach_site(id: String) -> void:
	click_path.clear()
	click_interaction_id = ""
	var point := _site_point(id)
	var reach := float(sites[id].reach)
	if player.position.distance_to(point) <= reach:
		click_path.clear()
		click_interaction_id = ""
		player.move_input(Vector2.ZERO)
		_interact(id)
		return
	if player.seated:
		_leave_bed()
	var best_path := PackedVector2Array()
	var best_destination := Vector2.ZERO
	var best_cost := INF
	for fraction in [1.0, 0.55]:
		for heading in APPROACH_DIRECTIONS:
			var candidate: Vector2 = point + heading.normalized() * (reach - CLICK_STOP_RADIUS - 2.0) * fraction
			if not _walkable(candidate):
				continue
			var route := _route(player.position, candidate)
			if route.is_empty():
				continue
			var cost := player.position.distance_to(route[0]) + route[route.size() - 1].distance_to(candidate)
			for index in range(1, route.size()):
				cost += route[index - 1].distance_to(route[index])
			if cost < best_cost:
				best_cost = cost
				best_path = route
				best_destination = candidate
	if best_path.is_empty():
		return
	click_path = best_path
	click_path.append(best_destination)
	click_waypoint = 0
	click_interaction_id = id

func _advance(amount := 5) -> void:
	minute += amount
	if hud_time:
		hud_time.text = "%02d:%02d" % [int(minute / 60) % 24, minute % 60]

func _interact(id: String) -> void:
	click_path.clear()
	click_interaction_id = ""
	if player.seated:
		_leave_bed()
	var target_point := _site_point(id) if sites.has(id) else Vector2.ZERO
	if sites.has(id) and actors.has(sites[id].title):
		var actor := actors[sites[id].title] as CharacterBody2D
		target_point = actor.position
		actor.face_point(player.position)
	if is_instance_valid(player) and target_point != Vector2.ZERO:
		player.face_point(target_point)
	if id == "meeting":
		_open_meeting()
		return
	if id == "transmitter":
		_open_transmitter()
		return
	if id == "door":
		_open_door()
		return
	if id in ["engineer", "chef", "musician"]:
		_talk_npc(id)
		return
	var site: Dictionary = sites[id]
	if not evidence.has(id):
		evidence[id] = true
		if not pending_inspections.has(id):
			_advance()
	if _resolve_pending_inspection(id):
		return
	_show_detail(id, site.title_id, site.description_id, site.diagram)
	_update_status()

func _npc_stage() -> int:
	return 2 if captain else 1 if broadcast_heard else 0

func _has_new_at(id: String) -> bool:
	if ended: return false
	if pending_inspections.has(id) and not mind_dirty and checks_this_run < 3:
		return true
	if id in ["engineer", "chef", "musician"]:
		return int(npc_stage_seen.get(id, -1)) < _npc_stage()
	if id == "transmitter":
		return not broadcast_heard or (captain and not transmitter_answer_seen)
	if id == "door":
		# Opening the panel does not consume its unread sub-interactions.
		for part in ["door", "slit", "hinge", "weight"]:
			if not evidence.has(part): return true
		return false
	if id == "meeting":
		if not broadcast_heard or captain: return false
		for required in INVESTIGATION_CONTENT.discussion_evidence:
			if not evidence.has(required): return false
		if not meeting_introduction_seen: return true
		for hypothesis in disputed:
			if not disputed[hypothesis] and _can_challenge(hypothesis): return true
		return false
	return not evidence.has(id)

func _can_challenge(hypothesis: String) -> bool:
	match hypothesis:
		"prison": return evidence.has("door") and (evidence.has("drawer") or evidence.has("study"))
		"defense": return evidence.has("hinge") and evidence.has("weight")
		"escape": return evidence.has("paper") and evidence.has("tear") and evidence.has("slit")
	return false

func _talk_npc(id: String) -> void:
	_begin_dialogue(id + "_entry")

func dialogue_mark_met(id: String) -> void:
	var stage := _npc_stage()
	var first_in_stage := int(npc_stage_seen.get(id, -1)) < stage
	if first_in_stage:
		npc_stage_seen[id] = stage
		_advance()
		evidence[id] = true

func _begin_dialogue(title: String, after := "", keep_scene := false) -> void:
	if modal_open and not keep_scene:
		modal.hide()
		modal_open = false
		mode = ""
	dialogue_after = after
	dialogue.panel.show()
	dialogue.heading.text = tr("FA_UI_049") + tr("FA_UI_030" if title.begins_with("broadcast") or title.begins_with("transmitter") or title.begins_with("reject") or title.begins_with("ending") else "FA_UI_132")
	dialogue.start(DIALOGUE, title, self)

func _on_dialogue_line_shown(_character: String) -> void:
	dialogue.layout_compact_line()

func _on_dialogue_finished() -> void:
	dialogue.panel.hide()
	var after := dialogue_after
	dialogue_after = ""
	if after == "meeting_board":
		_show_meeting_board()
	elif after == "nomination":
		_begin_dialogue("meeting_nomination")
	elif after == "answer":
		_open_answer()
	elif after == "exit":
		_begin_dialogue("exit_discussion")
	elif after == "depart":
		_finish("ending_exit")
	elif after == "ending":
		_show_ending_panel()
	elif after == "preliminary_done":
		transmitter_connected = false
		_update_status()
	elif after == "transmitter":
		transmitter_connected = false
	elif after == "mind_ready":
		modal.process_mode = Node.PROCESS_MODE_INHERIT

func dialogue_choose_answer() -> void:
	dialogue_after = "answer"

func dialogue_choose_exit() -> void:
	dialogue_after = "exit"

func dialogue_accept_exit() -> void:
	dialogue_after = "depart"

func _style(color: Color, border := Color("63706b")) -> StyleBoxFlat:
	return modal.style(color, border)

func _label(parent: Node, words: String, size := 20, color := Color("e6dfcc")) -> Label:
	return modal.add_label(parent, words, size, color)

func _button(parent: Node, words: String, action: Callable, disabled := false) -> Button:
	return modal.add_button(parent, words, action, disabled)

func _build_ui() -> void:
	ui = $FieldUI
	hud_title = $FieldUI/Title
	hud_time = $FieldUI/Time
	hud_hint = $FieldUI/Hint
	hud_lead = Label.new()
	hud_lead.name = "InvestigationLead"
	hud_lead.position = Vector2(24, 46)
	hud_lead.size = Vector2(900, 24)
	hud_lead.add_theme_font_size_override("font_size", 14)
	hud_lead.add_theme_color_override("font_color", Color("cfc4a6"))
	hud_lead.mouse_filter = Control.MOUSE_FILTER_IGNORE
	ui.add_child(hud_lead)
	language_selector = $FieldUI/Language
	language_selector.add_item(tr("FA_UI_021"))
	language_selector.add_item("English")
	language_selector.add_item(tr("FA_UI_111"))
	language_selector.item_selected.connect(_on_language_selected)
	_refresh_language_selector()
	modal = $FieldUI/InvestigationPanel
	modal_box = $FieldUI/InvestigationPanel/ModalScroll/ModalBox

func _open_modal(title: String, next_mode: String) -> void:
	if mode == "flow" and next_mode != "flow":
		replay_playing = false
		preload("res://scenes/FirstAwakening/MindPresentation.gd").return_to_field_music(self)
	mode = next_mode
	modal_open = true
	modal.open_content(title, next_mode, active_locale_theme)

func _close_modal() -> void:
	if mode == "flow":
		replay_playing = false
		preload("res://scenes/FirstAwakening/MindPresentation.gd").return_to_field_music(self)
	modal.hide()
	modal_open = false
	mode = ""
	transmitter_connected = false
	modal_locale_refresh = Callable()
	player.pose("idle")

func _show_message(title: String, words: String) -> void:
	modal_locale_refresh = Callable(self, "_show_message").bind(title, words)
	_open_modal(_translate_display_id_sequence(title), "message")
	var localized_parts := PackedStringArray()
	for part in words.split("\n\n"):
		localized_parts.append(_translate_display_id_sequence(part))
	_label(modal_box, "\n\n".join(localized_parts), 21)
	_button(modal_box, "FA_UI_148", _close_modal)

func _show_detail(id: String, title: String, words: String, diagram: String) -> void:
	modal_locale_refresh = Callable(self, "_show_detail").bind(id, title, words, diagram)
	_open_modal(_translate_display_id_sequence(title), "detail")
	var localized_parts := PackedStringArray()
	for part in words.split("\n\n"):
		localized_parts.append(_translate_display_id_sequence(part))
	modal.add_detail("\n\n".join(localized_parts), diagram, _close_modal)

func _get_investigation_lead() -> String:
	if ended:
		return "FA_LEAD_ENDED"
	if not broadcast_heard:
		return "FA_LEAD_BROADCAST"
	if not (evidence.has("door") and evidence.has("inner_bolt") and evidence.has("stop_base")):
		return "FA_LEAD_EXAMINE_DOOR"
	if not (disputed.prison and disputed.defense and disputed.escape):
		return "FA_LEAD_TALK_COMPANIONS"
	if not (sim_checks.has("门底遮蔽") and sim_checks.has("纸内转印") and sim_checks.has("双套锁止")):
		return "FA_LEAD_REPLAY_MIND"
	return "FA_LEAD_ROUNDTABLE"

func _update_status() -> void:
	if hud_title and is_instance_valid(player):
		var room := "FA_UI_132"
		if player.position.y < 300:
			room = "FA_UI_079"
		elif player.position.y < 470:
			room = "FA_UI_024"
		elif player.position.y < 816:
			room = "FA_UI_160" if player.position.x < 640 else "FA_UI_135"
		else:
			room = "FA_UI_177"
		hud_title.text = tr("FA_UI_049") + tr(room)
	if is_instance_valid(hud_lead):
		hud_lead.text = tr("FA_UI_LEAD_PREFIX") + tr(_get_investigation_lead())
		hud_lead.visible = not modal_open and not dialogue.active

func _open_journal() -> void:
	modal_locale_refresh = Callable(self, "_open_journal")
	_open_modal("FA_UI_050", "journal")
	_label(modal_box, tr("FA_UI_054") % [int(minute / 60) % 24, minute % 60], 17)
	_label(modal_box, tr("FA_UI_LEAD_PREFIX") + tr(_get_investigation_lead()), 18, Color("dfc995"))
	for category in INVESTIGATION_CONTENT.journal_categories:
		_label(modal_box, category.title_id, 22, Color("d1bc90"))
		var found: Array[String] = []
		for id in category.site_ids:
			if evidence.has(id):
				found.append(tr(sites[id].title_id))
				_button(modal_box, sites[id].title_id, _review_evidence.bind(id))
		if found.is_empty():
			_label(modal_box, "FA_UI_083", 17)
	var pending_names: Array[String] = []
	for id in pending_inspections:
		pending_names.append(tr(sites[id].title_id))
	_label(modal_box, tr("FA_UI_127") + (_localized_join(pending_names) if not pending_names.is_empty() else tr("FA_UI_110")), 17, Color("dfc995"))
	_label(modal_box, tr("FA_UI_086") + (_localized_join(conjectures.values()) if not conjectures.is_empty() else tr("FA_UI_110")), 17, Color("bdd2a4"))
	_button(modal_box, "FA_UI_162", _close_modal)

func _review_evidence(id: String) -> void:
	var site: Dictionary = sites[id]
	_show_detail(id, site.title_id, site.description_id, site.diagram)

func _open_door() -> void:
	if not evidence.has("door"):
		evidence["door"] = true
		_advance()
	_begin_dialogue("door")
	_update_status()

func _toggle_bolt() -> void:
	inner_bolt = not inner_bolt
	bolt_visual.visible = inner_bolt
	_advance()

func dialogue_inspect_door(part: String) -> void:
	evidence[part] = true
	if part == "hinge": evidence["weight"] = true
	_advance()

func _open_transmitter() -> void:
	_begin_dialogue("transmitter_entry", "transmitter")

func dialogue_connect_transmitter() -> void:
	transmitter_rules = true
	transmitter_connected = true
	if captain: transmitter_answer_seen = true
	_advance()

func dialogue_hear_broadcast() -> void:
	broadcast_heard = true
	evidence["transmitter"] = true
	_gather_party()
	_update_status()

func _finish(dialogue_title: String) -> void:
	ended = true
	ending_title = dialogue_title
	if is_instance_valid(reasoning_music) and reasoning_music.playing:
		var tween := create_tween()
		tween.tween_property(reasoning_music, "volume_db", -40.0, 3.0)
	_begin_dialogue(dialogue_title, "ending")

func _show_ending_panel() -> void:
	if ending_title != "ending_exit":
		var gate: Node2D = scene_items.get_node("MechanicalIsolationDoor")
		gate.get_node("DoorFace").hide()
		gate.get_node("DoorGap").show()
		bolt_visual.hide()
	modal_locale_refresh = Callable(self, "_show_ending_panel")
	_open_modal("FA_UI_144", "ending")
	_label(modal_box, "FA_UI_114", 17)
	_button(modal_box, "FA_UI_169", func(): get_tree().reload_current_scene())

func _open_meeting() -> void:
	if not broadcast_heard:
		_show_message("FA_UI_137", "FA_UI_173")
		return
	if player.position.distance_to(sites["meeting"].point) > 75:
		_show_message("FA_UI_157", "FA_UI_029")
		return
	var missing: Array[String] = []
	for id in INVESTIGATION_CONTENT.discussion_evidence:
		if not evidence.has(id):
			missing.append(sites[id].title_id)
	if not missing.is_empty():
		_show_message("FA_UI_163", "FA_UI_041|LIST|" + "|LIST|".join(missing))
		return
	if not meeting_done:
		meeting_done = true
		_advance()
	if not meeting_introduction_seen:
		meeting_introduction_seen = true
		_begin_dialogue("meeting", "meeting_board")
		return
	_show_meeting_board()

func _show_meeting_board() -> void:
	modal_locale_refresh = Callable(self, "_show_meeting_board")
	_open_modal("FA_UI_064", "meeting")
	_label(modal_box, "FA_UI_080", 19)
	_button(modal_box, tr("FA_UI_014" if disputed.prison else "FA_UI_053") + tr("FA_UI_023"), func(): _challenge("prison"), not _can_challenge("prison"))
	_button(modal_box, tr("FA_UI_014" if disputed.defense else "FA_UI_053") + tr("FA_UI_076"), func(): _challenge("defense"), not _can_challenge("defense"))
	_button(modal_box, tr("FA_UI_014" if disputed.escape else "FA_UI_053") + tr("FA_UI_103"), func(): _challenge("escape"), not _can_challenge("escape"))
	if disputed.prison and disputed.defense and disputed.escape:
		captain = true
		_label(modal_box, "FA_UI_017", 20, Color("bdd2a4"))
		_button(modal_box, "FA_UI_229", _request_flow)
	else:
		_label(modal_box, "FA_UI_130", 16)
	_button(modal_box, "FA_UI_150", _close_modal)
	_update_status()

func _walkable(center: Vector2) -> bool:
	return FieldNavigation.walkable(center, floors, obstacle_rects)

func _route(start: Vector2, finish: Vector2) -> PackedVector2Array:
	return FieldNavigation.route(start, finish, floors, obstacle_rects, Vector2i(layout.rows[0].length(), layout.rows.size()))

func _gather_party() -> void:
	var destinations := {"工程师": $MeetingSpots/Engineer.position, "厨师": $MeetingSpots/Chef.position, "音乐家": $MeetingSpots/Musician.position}
	for id in destinations:
		var cast: CharacterBody2D = actors[id]
		var route := _route(cast.position, destinations[id])
		if route.size() > 1:
			cast.follow_path(route)

func _challenge(hypothesis: String) -> void:
	_begin_dialogue("present_" + hypothesis, "meeting_board")

func dialogue_accept_challenge(hypothesis: String) -> void:
	disputed[hypothesis] = true
	_advance()
	if disputed.prison and disputed.defense and disputed.escape:
		captain = true
		dialogue_after = "nomination"
	else:
		dialogue_after = "meeting_board"
	_update_status()

func _request_flow() -> void:
	_open_flow()
	if not mind_introduction_seen:
		mind_introduction_seen = true
		modal.process_mode = Node.PROCESS_MODE_DISABLED
		_begin_dialogue("mind_intro", "mind_ready", true)

func _open_flow() -> void:
	preload("res://scenes/FirstAwakening/MindPresentation.gd").open(self)

func _hypothesis_test(hypothesis: String) -> void:
	_advance()
	neutral_actions_unlocked = true
	_show_hypothesis_probe(hypothesis)
	_update_status()

func _show_hypothesis_probe(hypothesis: String) -> void:
	var is_defense := hypothesis == "defense"
	var probe_id := "InnerWedgeStopsOutsidePush" if is_defense else "OutsideActorSetBothLocks"
	var title_id := "FA_UI_051" if is_defense else "FA_UI_099"
	var suffix_id := "FA_UI_168" if is_defense else "FA_UI_036"
	modal_locale_refresh = Callable(self, "_show_hypothesis_probe").bind(hypothesis)
	_open_modal(tr(title_id), "message")
	_label(modal_box, bridge.Probe(probe_id) + "\n\n" + tr(suffix_id), 21)
	_button(modal_box, "FA_UI_148", _close_modal)

func _run_replay() -> void:
	var program := _compile_program()
	if program.has("error"):
		mind_note = str(program.error)
		_refresh_replay_feedback()
		return
	replay_states = program.states
	sim_result = replay_states.back()
	replay_events = program.events
	mind_dirty = false
	mind_note = ""
	replay_index = 0
	replay_playing = false
	replay_reached.clear()
	pending_inspections.clear()
	_build_replay_pause_points()
	checks_this_run = 0
	_open_flow()

func _build_replay_pause_points() -> void:
	replay_pause_points.clear()
	replay_pause_display_points.clear()
	replay_pause_ids.clear()
	var seen := {}
	for index in range(1,replay_states.size()):
		var ids: Array = replay_states[index].ids
		var titles: Array[String] = []
		var display_titles: Array[String] = []
		var point_ids: Array[String] = []
		for clue in INVESTIGATION_CONTENT.replay_clues:
			if seen.has(clue.id):
				continue
			var present := true
			for state_id in clue.state_ids:
				if not ids.has(state_id):
					present = false
			if present:
				seen[clue.id] = true
				point_ids.append(clue.id)
				titles.append(clue.pause_title)
				display_titles.append(tr(clue.pause_title_id))
		if not titles.is_empty():
			replay_pause_points[index] = " / ".join(titles)
			replay_pause_display_points[index] = " / ".join(display_titles)
			replay_pause_ids[index] = point_ids

func _compile_program() -> Dictionary:
	if not sim_actor_actions.is_empty():
		return bridge.CompileActorDrafts(sim_actor_actions, _replay_context())
	if sim_actions.is_empty() or not sim_actions[0].contains("|"):
		return bridge.Compile(sim_actions)
	return bridge.CompileDraft(sim_actions, _replay_context())

func _replay_context() -> Dictionary:
	var context := {}
	for id in sites:
		var site: Dictionary = sites[id]
		var talk: Array[String] = []
		var speaker: String = site.title
		if id == "transmitter": speaker = "铜口"
		if id in ["engineer", "chef", "musician", "transmitter", "meeting"]:
			for entry in dialogue.history:
				if entry.begins_with(speaker + "\n") or (id == "meeting" and meeting_introduction_seen): talk.append(entry)
		context[id] = {"title":site.title, "point":site.point,
			"description":site.description if evidence.has(id) else "",
			"known_dialogue":"\n".join(talk)}
	return context

func _reveal_replay_point() -> void:
	if not replay_pause_points.has(replay_index):
		return
	var point_ids: Array = replay_pause_ids[replay_index]
	for clue in INVESTIGATION_CONTENT.replay_clues:
		if point_ids.has(clue.id):
			replay_reached[clue.canonical] = true

func _refresh_replay_feedback() -> void:
	preload("res://scenes/FirstAwakening/MindPresentation.gd").refresh(self)

func _start_replay(fast: bool) -> void:
	if replay_events.is_empty(): return
	if replay_index >= replay_events.size():
		replay_index = 0
	replay_fast = fast
	replay_playing = true
	replay_accum = 0
	if is_instance_valid(replay_status):
		replay_status.text = tr("FA_UI_105")
	if is_instance_valid(replay_stage):
		replay_stage.set("frame", replay_index)
		replay_stage.set("progress", 0.0)
		replay_stage.set("highlight", "|".join(replay_pause_ids.get(replay_index, [])))
		replay_stage.queue_redraw()

func _replay_step(delta: int) -> void:
	replay_index = clampi(replay_index + delta, 0, replay_events.size())
	_reveal_replay_point()
	if replay_pause_points.has(replay_index) or replay_index >= replay_events.size() or (not replay_states.is_empty() and not replay_states[replay_index].get("succeeded",true)):
		replay_playing = false
	_refresh_replay_status()
	if is_instance_valid(replay_stage):
		replay_stage.set("frame", replay_index)
		replay_stage.set("progress", 0.0)
		replay_stage.set("highlight", "|".join(replay_pause_ids.get(replay_index, [])))
		replay_stage.queue_redraw()
	_refresh_replay_feedback()

func _refresh_replay_status() -> void:
	if is_instance_valid(replay_status):
		replay_status.text = (tr("FA_UI_126") % [replay_index, replay_events.size(), replay_pause_display_points.get(replay_index, "")]) if replay_pause_points.has(replay_index) else (tr("FA_UI_125") % [replay_index, replay_events.size()])

func _trace_visible(required: String) -> bool:
	if replay_states.is_empty(): return false
	var ids: Array = replay_states[replay_index].ids
	var state: Dictionary = replay_states[replay_index]
	return ids.has(required) and (required != "inner-bolt" or (state.inner_bolt and state.outer_bar))

func _request_trace_check(title: String, required: String, actual_site: String) -> void:
	if mind_dirty or checks_this_run >= 3 or sim_checks.has(title) or not replay_reached.has(title):
		return
	if not _trace_visible(required):
		return
	if evidence.has(actual_site):
		_verify_trace(title, required, actual_site)
		return
	pending_inspections[actual_site] = {"title": title, "required": required}
	_show_message("FA_UI_042|" + sites[actual_site].title_id, "FA_UI_164")

func _resolve_pending_inspection(id: String) -> bool:
	if not pending_inspections.has(id):
		return false
	if checks_this_run >= 3:
		_show_message("FA_UI_115", "FA_UI_122")
		return true
	var pending: Dictionary = pending_inspections[id]
	pending_inspections.erase(id)
	_verify_trace(pending.title, pending.required, id)
	return true

func _verify_trace(title: String, required: String, actual_site: String) -> void:
	if mind_dirty or checks_this_run >= 3 or sim_result.is_empty() or not replay_reached.has(title) or sim_checks.has(title):
		return
	if not _trace_visible(required) or not evidence.has(actual_site):
		_show_message("FA_UI_033", "FA_UI_165")
		return
	checks_this_run += 1
	_advance(10)
	sim_checks[title] = true
	var clue: Dictionary = {}
	for entry in INVESTIGATION_CONTENT.replay_clues:
		if entry.canonical == title:
			clue = entry
			break
	if not clue.is_empty():
		conjectures[title] = clue.conjecture_id
	var lead_id := "FA_UI_121"
	if actual_site == "trace" and not evidence.has("paper"):
		lead_id = "FA_UI_043"
	var title_display_id: String = clue.get("pause_title_id", "FA_UI_121")
	_show_detail(actual_site, "FA_UI_034|" + title_display_id, lead_id + "\n\n" + sites[actual_site].description_id + "\n\nFA_UI_055", sites[actual_site].diagram)
	_refresh_replay_feedback()
	_update_status()

func _open_answer() -> void:
	if not broadcast_heard:
		_show_message("FA_UI_082", "FA_UI_037")
		return
	modal_locale_refresh = Callable(self, "_open_answer")
	_open_modal("FA_UI_145", "answer")
	if not captain:
		_label(modal_box, "FA_UI_230", 17, Color("dfc995"))
	elif not (sim_checks.has("门底遮蔽") and sim_checks.has("纸内转印") and sim_checks.has("双套锁止")):
		_label(modal_box, "FA_UI_228", 17, Color("dfc995"))
		_button(modal_box, "FA_UI_229", _request_flow)
	for section in ANSWER_CATALOG.sections:
		var options := []
		for option in section.options:
			var refuted: bool = captain and str(option.get("outcome", "")).begins_with("reject_")
			if refuted and answer_slots[section.slot] == option.value:
				answer_slots[section.slot] = ""
				answer_slot_display_ids[section.slot] = ""
			options.append([option.value, not refuted and AnswerRules.available(option, evidence, sim_checks), option.display_id])
		_answer_choice(section.slot, section.title_id, options)
	var footer := HBoxContainer.new()
	modal_box.add_child(footer)
	_button(footer,"FA_UI_162",_close_modal)
	var space := Control.new()
	space.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	footer.add_child(space)
	_button(footer,"FA_UI_100",_submit_answer,answer_slots.values().has(""))

func _answer_choice(slot: String, title: String, options: Array) -> void:
	_label(modal_box,title,18,Color("a1ae9f"))
	var menu := MenuButton.new()
	menu.text = tr(answer_slot_display_ids[slot]) if not answer_slot_display_ids[slot].is_empty() else tr("FA_UI_166")
	menu.alignment = HORIZONTAL_ALIGNMENT_LEFT
	menu.custom_minimum_size.y = 58
	menu.add_theme_font_size_override("font_size",22)
	menu.add_theme_color_override("font_color",Color("e6dfcc"))
	menu.add_theme_stylebox_override("normal",_style(Color("253d34"),Color("253d34")))
	var available: Array[String] = []
	for option in options:
		if option[1]:
			menu.get_popup().add_item(tr(option[2]))
			available.append(option[0])
	menu.get_popup().add_theme_font_size_override("font_size",20)
	menu.get_popup().index_pressed.connect(func(index: int): _set_slot(slot,available[index], _available_display_id(options, index)))
	modal_box.add_child(menu)

func _available_display_id(options: Array, selected_index: int) -> String:
	var index := 0
	for option in options:
		if not option[1]:
			continue
		if index == selected_index:
			return option[2]
		index += 1
	return "FA_UI_166"

func _set_slot(slot: String, value: String, display_id: String) -> void:
	answer_slots[slot] = value
	answer_slot_display_ids[slot] = display_id
	_open_answer()

func _submit_answer() -> void:
	if player.position.distance_to(sites["transmitter"].point) > 72:
		_show_message("FA_UI_158", "FA_UI_147")
		return
	if not transmitter_connected:
		_show_message("FA_UI_031", "FA_UI_094")
		return
	_advance()
	if answer_slots.place == "" or answer_slots.door == "" or answer_slots.outer == "":
		_show_message("FA_UI_156", "FA_UI_016")
		return
	var resolution: String = AnswerRules.resolution(ANSWER_CATALOG.sections, answer_slots, evidence, sim_checks)
	if not captain and not resolution.begins_with("reject_"):
		resolution = "reject_unsupported"
	if resolution.begins_with("reject_"):
		_begin_dialogue(resolution, "answer")
	else:
		_finish(resolution)
