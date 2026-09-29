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

const FirstAwakeningStateScript = preload("res://scenes/FirstAwakening/FirstAwakeningState.gd")
const FirstAwakeningEnsembleScript = preload("res://scenes/FirstAwakening/FirstAwakeningEnsemble.gd")
const FirstAwakeningDeductionScript = preload("res://scenes/FirstAwakening/FirstAwakeningDeduction.gd")

var state: Resource = FirstAwakeningStateScript.new()
var ensemble: RefCounted = FirstAwakeningEnsembleScript.new()
var deduction: RefCounted = FirstAwakeningDeductionScript.new()

var floors: Dictionary = {}
var obstacle_rects: Array[Rect2] = []
var layout: Dictionary = {}
var sites: Dictionary = {}
var active_sites: Dictionary = {}
var evidence: Dictionary:
	get: return state.evidence
	set(v): state.evidence = v
var actors: Dictionary = {}
var npc_stage_seen: Dictionary = {}
@onready var scene_items: Node2D = $ObjectsAndPeople
var player: CharacterBody2D
var click_path := PackedVector2Array()
var click_waypoint := 0
var click_interaction_id := ""
var click_last_position := Vector2(INF, INF)
var click_stall_seconds := 0.0
var camera: Camera2D
var nearest := ""
var reachable: Array[String] = []
var cycle_offset := 0
var last_cell := Vector2i(-1, -1)
var last_proximity_position := Vector2(INF, INF)
var proximity_refresh_frames := 0
const PAIR_SITES := {
	"engineer": ["hinge", "weight", "trace", "door", "slit", "floor_wipe"],
	"cook": ["wash", "water", "rations", "basin_ring", "cloth_hang", "cold_stove"],
	"musician": ["study", "tear", "box_mark", "table_wedge", "chair_marks", "transmitter"]
}

var minute: int:
	get: return state.minute
	set(v): state.minute = v
var inner_bolt: bool:
	get: return state.inner_bolt
	set(v): state.inner_bolt = v
var viewing_outer_bar: bool:
	get: return state.viewing_outer_bar
	set(v): state.viewing_outer_bar = v
var broadcast_heard: bool:
	get: return state.broadcast_heard
	set(v): state.broadcast_heard = v
var meeting_done: bool:
	get: return state.meeting_done
	set(v): state.meeting_done = v
var captain: bool:
	get: return state.captain
	set(v): state.captain = v
var ended: bool:
	get: return state.ended
	set(v): state.ended = v
var ending_title: String:
	get: return state.ending_title
	set(v): state.ending_title = v
var disputed: Dictionary:
	get: return state.disputed
	set(v): state.disputed = v
var investigation_phase: String:
	get: return state.investigation_phase
	set(v): state.investigation_phase = v
var player_name: String:
	get: return state.player_name
	set(v): state.player_name = v
var name_entry: Control
var conversation_staged := false
var conversation_focus: String:
	get: return ensemble.conversation_focus
	set(v): ensemble.conversation_focus = v
const ARRIVAL_THRESHOLD := Rect2(224, 544, 64, 48)
var arrival_started := false
var ambient_elapsed: float:
	get: return ensemble.ambient_elapsed
	set(v): ensemble.ambient_elapsed = v
var ambient_speaker_index: int:
	get: return ensemble.ambient_speaker_index
	set(v): ensemble.ambient_speaker_index = v
var ambient_murmurs: Dictionary:
	get: return ensemble.ambient_murmurs
	set(v): ensemble.ambient_murmurs = v
var conversation_look_targets: Dictionary:
	get: return ensemble.conversation_look_targets
	set(v): ensemble.conversation_look_targets = v
var paired_companion: String:
	get: return state.paired_companion
	set(v): state.paired_companion = v
var npc_searches: Dictionary:
	get: return state.npc_searches
	set(v): state.npc_searches = v
var companion_route_cooldown := 0.0
var evidence_sources: Dictionary:
	get: return state.evidence_sources
	set(v): state.evidence_sources = v
var known_dorms: Dictionary:
	get: return state.known_dorms
	set(v): state.known_dorms = v
var deep_inspections: Dictionary:
	get: return state.deep_inspections
	set(v): state.deep_inspections = v
var ui: CanvasLayer
var modal: FieldModal
var modal_box: VBoxContainer
var hud_title: Label
var hud_hint: Label
var hud_time: Label
var modal_open := false
var mode := ""
var modal_locale_refresh: Callable
var language_selector: OptionButton
var active_locale_theme: Theme
var pending_close := false
var transmitter_rules: bool:
	get: return state.transmitter_rules
	set(v): state.transmitter_rules = v
var transmitter_connected: bool:
	get: return state.transmitter_connected
	set(v): state.transmitter_connected = v
var transmitter_answer_seen: bool:
	get: return state.transmitter_answer_seen
	set(v): state.transmitter_answer_seen = v
var preliminary_answer_attempted: bool:
	get: return state.preliminary_answer_attempted
	set(v): state.preliminary_answer_attempted = v
var bridge: Node
var bolt_visual: CanvasItem
var sim_actions: Array[String]:
	get: return state.sim_actions
	set(v): state.sim_actions = v
var sim_actor_actions: Dictionary:
	get: return state.sim_actor_actions
	set(v): state.sim_actor_actions = v
var mind_text: String:
	get: return state.mind_text
	set(v): state.mind_text = v
var mind_drafts: Dictionary:
	get: return state.mind_drafts
	set(v): state.mind_drafts = v
var mind_person: String:
	get: return state.mind_person
	set(v): state.mind_person = v
var mind_dirty: bool:
	get: return state.mind_dirty
	set(v): state.mind_dirty = v
var mind_note: String:
	get: return state.mind_note
	set(v): state.mind_note = v
var mind_trace: int:
	get: return state.mind_trace
	set(v): state.mind_trace = v
var sim_result: Dictionary:
	get: return state.sim_result
	set(v): state.sim_result = v
var sim_checks: Dictionary:
	get: return state.sim_checks
	set(v): state.sim_checks = v
var simulated_draft_snapshot: Dictionary:
	get: return state.simulated_draft_snapshot
	set(v): state.simulated_draft_snapshot = v
var pending_inspections: Dictionary:
	get: return state.pending_inspections
	set(v): state.pending_inspections = v
var conjectures: Dictionary:
	get: return state.conjectures
	set(v): state.conjectures = v
var checks_this_run: int:
	get: return state.checks_this_run
	set(v): state.checks_this_run = v
var replay_events: Array[String]:
	get: return state.replay_events
	set(v): state.replay_events = v
var replay_states: Array:
	get: return state.replay_states
	set(v): state.replay_states = v
var replay_pause_points: Dictionary:
	get: return state.replay_pause_points
	set(v): state.replay_pause_points = v
var replay_pause_display_points: Dictionary:
	get: return state.replay_pause_display_points
	set(v): state.replay_pause_display_points = v
var replay_pause_ids: Dictionary:
	get: return state.replay_pause_ids
	set(v): state.replay_pause_ids = v
var replay_index: int:
	get: return state.replay_index
	set(v): state.replay_index = v
var replay_playing: bool:
	get: return state.replay_playing
	set(v): state.replay_playing = v
var replay_fast: bool:
	get: return state.replay_fast
	set(v): state.replay_fast = v
var replay_accum: float:
	get: return state.replay_accum
	set(v): state.replay_accum = v
var replay_stage: Control
var replay_status: Label
var replay_feedback: VBoxContainer
var replay_reached: Dictionary:
	get: return state.replay_reached
	set(v): state.replay_reached = v
var reasoning_music: AudioStreamPlayer
var reasoning_music_enabled := true
var reasoning_music_tween: Tween
var answer_slots: Dictionary:
	get: return state.answer_slots
	set(v): state.answer_slots = v
var answer_slot_display_ids: Dictionary:
	get: return state.answer_slot_display_ids
	set(v): state.answer_slot_display_ids = v
var dialogue: CanvasLayer
var dialogue_after := ""
var meeting_introduction_seen: bool:
	get: return state.meeting_introduction_seen
	set(v): state.meeting_introduction_seen = v
var mind_introduction_seen: bool:
	get: return state.mind_introduction_seen
	set(v): state.mind_introduction_seen = v

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
	if canonical_name == "侦探" and player_name != "侦探": return player_name
	if canonical_name == "侦探（心声）" and player_name != "侦探": return player_name + "（" + tr("FA_UI_272") + "）"
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
			if control == name_entry or name_entry.is_ancestor_of(control): continue
			control.theme = theme
		if is_instance_valid(modal):
			modal.theme = theme
	if is_instance_valid(dialogue):
		dialogue.call("apply_locale_font", font, theme)
	if is_instance_valid(name_entry): name_entry.apply_locale_theme()

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
	ensemble.setup(self, actors, player)
	deduction.setup(self, state)
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
	_place_party_at_table()
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
		actors[id].arrived.connect(_refresh_conversation_facing)
	player = actors["侦探"]
	# A local copy preserves the shared walk cadence outside this scene.
	player.standard = player.standard.duplicate()
	for direction_name in ["front", "back", "left"]:
		player.standard.set_animation_speed("walk_" + direction_name, 7.5)
	player.sprite.sprite_frames = player.standard
	player.pose("idle", "front")
	player.sit_at("front", 4.0)
	actors["工程师"].pose("idle", "right")
	actors["厨师"].pose("idle", "back")
	actors["音乐家"].pose("idle", "left")

func _leave_bed() -> void:
	player.position = $WakePosition.position
	player.stand_up()

func _isolation_door() -> void:
	bolt_visual = scene_items.get_node("MechanicalIsolationDoor/InnerBolt")

func _physics_process(_delta: float) -> void:
	if not is_instance_valid(player):
		return
	if investigation_phase == "arrival" and not arrival_started:
		_update_ambient_discussion(_delta)
	if not paired_companion.is_empty() and can_access_dorm(paired_companion):
		for door in scene_items.get_children():
			if door.has_method("access_allowed") and door.dorm_owner == paired_companion and door.interior_bounds.has_point(player.position):
				known_dorms[paired_companion] = true
	if camera:
		camera.position = Vector2(clampf(player.position.x, 320, 960), clampf(player.position.y - 24, 248, 776)).round()
		if investigation_phase == "arrival" and player.position.y >= 480 and player.position.x >= 224 and player.position.x <= 288:
			camera.position = Vector2(320, 640)
		if conversation_staged:
			var center := Vector2.ZERO
			for actor in actors.values(): center += actor.position
			center /= float(actors.size())
			camera.position = Vector2(clampf(center.x, 320, 960), center.y + (-96.0 if name_entry.visible else 64.0)).round()
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
		if not player.travelling: player.velocity = Vector2.ZERO
		return
	if investigation_phase == "arrival" and not arrival_started and ARRIVAL_THRESHOLD.has_point(player.position):
		click_path.clear()
		click_interaction_id = ""
		player.move_input(Vector2.ZERO)
		_begin_dialogue("activity_hall_opening")
		return
	_follow_companion()
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
		click_stall_seconds = click_stall_seconds + _delta if player.position.distance_to(click_last_position) < 0.25 else 0.0
		click_last_position = player.position
		if click_stall_seconds >= 0.5:
			var pending_id := click_interaction_id
			var destination := click_path[-1]
			click_stall_seconds = 0.0
			if pending_id.is_empty(): _move_to_click(destination)
			else: _approach_site(pending_id)
			return
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
		if not _site_accessible(id): continue
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
		hud_hint.text = "" if nearest == "" else tr(sites[nearest].title_id)

func _process(delta: float) -> void:
	hud_hint.visible = not modal_open and not dialogue.active
	if conversation_staged and dialogue.active and not dialogue.busy and dialogue.line != null and dialogue.body.visible_characters >= 0:
		var speaker: String = "厨师" if dialogue.line.character == "主厨" else dialogue.line.character
		if actors.has(speaker):
			var actor: CharacterBody2D = actors[speaker]
			if not actor.travelling and _conversation_actor_active(actor) and not actor.sprite.is_playing(): actor.pose("talk")
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
	for door in scene_items.get_children():
		if door.has_method("access_allowed") and not door.access_allowed() and (door.contains_private_point(destination) or door.door_click_rect().has_point(destination)):
			_show_message("FA_UI_362", "FA_UI_363")
			get_viewport().set_input_as_handled()
			return
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
	click_stall_seconds = 0.0
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
	var visible_sites := {}
	for id in sites:
		if _site_accessible(id): visible_sites[id] = sites[id]
	return FieldNavigation.clicked_site(position, visible_sites, actors)

func _approach_site(id: String) -> void:
	click_stall_seconds = 0.0
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
	if investigation_phase == "paired" and id in PAIR_SITES[paired_companion] and actors[_paired_actor()].position.distance_to(player.position) > 96:
		_show_message("FA_UI_364", "FA_UI_365")
		return
	if not _site_accessible(id):
		_show_message("FA_UI_366", "FA_UI_367")
		return
	_record_deep_inspection(id)
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
		evidence_sources[id] = "亲见"
		if not pending_inspections.has(id):
			_advance()
	if _resolve_pending_inspection(id):
		return
	_show_detail(id, site.title_id, site.description_id, site.diagram)
	_update_status()

func _npc_stage() -> int:
	return 2 if captain else 1 if broadcast_heard else 0

func _has_new_at(id: String) -> bool:
	if not _site_accessible(id): return false
	if ended: return false
	if pending_inspections.has(id) and not mind_dirty:
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
		if investigation_phase in ["arrival", "regroup", "paired"]: return true
		if not broadcast_heard or captain: return false
		for required in INVESTIGATION_CONTENT.discussion_evidence:
			if not evidence.has(required): return false
		if not meeting_introduction_seen: return true
		for hypothesis in disputed:
			if not disputed[hypothesis] and _can_challenge(hypothesis): return true
		return false
	return not evidence.has(id)

func _can_challenge(hypothesis: String) -> bool:
	for claim in INVESTIGATION_CONTENT.meeting_hypotheses:
		if claim.id == hypothesis:
			return AnswerRules.available({"requirements": claim}, evidence, {})
	return false

func _talk_npc(id: String) -> void:
	if investigation_phase == "arrival":
		_begin_dialogue("activity_hall_opening")
	else:
		dialogue_mark_met(id)
		_begin_dialogue("companion_" + id)

func dialogue_mark_met(id: String) -> void:
	var stage := _npc_stage()
	var first_in_stage := int(npc_stage_seen.get(id, -1)) < stage
	if first_in_stage:
		npc_stage_seen[id] = stage
		_advance()
		evidence[id] = true

func _begin_dialogue(title: String, after := "", keep_scene := false) -> void:
	conversation_staged = title in ["activity_hall_opening", "first_regrouping_meeting", "exchange_findings", "companion_engineer", "companion_chef", "companion_musician"] or title in ["transmitter_entry", "ending_passed", "meeting_nomination"] or after == "meeting_board"
	conversation_focus = ""
	conversation_look_targets.clear()
	if title == "activity_hall_opening":
		arrival_started = true
		for label in ambient_murmurs.values(): label.hide()
	if modal_open and not keep_scene:
		modal.hide()
		modal_open = false
		mode = ""
	dialogue_after = after
	dialogue.panel.show()
	dialogue.heading.text = tr("FA_UI_049") + tr("FA_UI_030" if title.begins_with("broadcast") or title.begins_with("transmitter") or title.begins_with("reject") or title.begins_with("ending") else "FA_UI_132")
	dialogue.start(DIALOGUE, title, self)

func _conversation_actor_active(actor: CharacterBody2D) -> bool:
	if npc_searches.has(actor.name): return false
	if npc_searches.has("other_pair") and actor != player and actor != actors[_paired_actor()]: return false
	return true

func _on_dialogue_line_shown(character: String) -> void:
	dialogue.speaker.text = _localized_speaker(character)
	if conversation_staged:
		ensemble.on_dialogue_line_shown(character, dialogue)

func _refresh_conversation_facing() -> void:
	if conversation_staged and dialogue.active and dialogue.line != null:
		_on_dialogue_line_shown(dialogue.line.character)

func dialogue_face_group(focus: String) -> void:
	ensemble.face_group(focus)

func dialogue_face_actor(who: String, target: String) -> void:
	ensemble.face_actor(who, target)

func dialogue_follow_speaker() -> void:
	ensemble.follow_speaker()

func _update_ambient_discussion(delta: float) -> void:
	ensemble.update_ambient_discussion(delta)

func dialogue_stage_arrival() -> void:
	await ensemble.stage_arrival()

func dialogue_request_name() -> void:
	dialogue.panel.hide()
	player_name = await name_entry.request_name()
	await dialogue._refresh_history_locale()
	dialogue.panel.show()

func _on_dialogue_finished() -> void:
	if conversation_staged:
		for actor in actors.values():
			if not actor.travelling and _conversation_actor_active(actor):
				actor.pose("idle")
	conversation_staged = false
	conversation_focus = ""
	dialogue.panel.hide()
	var after := dialogue_after
	dialogue_after = ""
	if after == "regroup":
		_gather_party()
	elif after == "meeting_board":
		_show_meeting_board()
	elif after == "nomination":
		_begin_dialogue("meeting_nomination")
	elif after == "answer":
		_open_answer()
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
	language_selector = $FieldUI/Language
	language_selector.add_item(tr("FA_UI_021"))
	language_selector.add_item("English")
	language_selector.add_item(tr("FA_UI_111"))
	language_selector.item_selected.connect(_on_language_selected)
	_refresh_language_selector()
	modal = $FieldUI/InvestigationPanel
	modal_box = $FieldUI/InvestigationPanel/ModalScroll/ModalBox
	name_entry = preload("res://scenes/FirstAwakening/NameEntry.tscn").instantiate()
	ui.add_child(name_entry)

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
	if investigation_phase == "dorm_search" and _own_dorm_complete():
		investigation_phase = "regroup"
		_begin_dialogue("dorm_investigation_finished")

func _show_message(title: String, words: String) -> void:
	modal_locale_refresh = Callable(self, "_show_message").bind(title, words)
	_open_modal(_translate_display_id_sequence(title), "message")
	var localized_parts := PackedStringArray()
	for part in words.split("\n\n"):
		localized_parts.append(_translate_display_id_sequence(part))
	_label(modal_box, "\n\n".join(localized_parts), 21)
	modal.add_footer([["FA_UI_148", _close_modal]])

func _show_detail(id: String, title: String, words: String, diagram: String) -> void:
	modal_locale_refresh = Callable(self, "_show_detail").bind(id, title, words, diagram)
	_open_modal(_translate_display_id_sequence(title), "detail")
	var localized_parts := PackedStringArray()
	for part in words.split("\n\n"):
		localized_parts.append(_translate_display_id_sequence(part))
	modal.add_detail("\n\n".join(localized_parts), diagram, _close_modal)

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

func _open_journal() -> void:
	modal_locale_refresh = Callable(self, "_open_journal")
	_open_modal("FA_UI_050", "journal")
	for category in INVESTIGATION_CONTENT.journal_categories:
		_label(modal_box, category.title_id, 17, modal.MUTED)
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
	if not pending_names.is_empty():
		_label(modal_box, tr("FA_UI_127") + _localized_join(pending_names), 17, modal.MUTED)
	modal.add_footer([["FA_UI_162", _close_modal]])

func _review_evidence(id: String) -> void:
	var site: Dictionary = sites[id]
	var source := tr("FA_UI_356") % player_name if evidence_sources.get(id, "亲见") == "亲见" else tr("FA_UI_357")
	_show_detail(id, site.title_id, source + "\n\n" + tr(site.description_id), site.diagram)

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
	_record_deep_inspection(part)
	evidence[part] = true
	evidence_sources[part] = "亲见"
	if part == "hinge": evidence["weight"] = true
	_advance()

func _open_transmitter() -> void:
	if investigation_phase == "arrival":
		_begin_dialogue("activity_hall_opening")
		return
	_begin_dialogue("transmitter_entry", "transmitter")

func dialogue_connect_transmitter() -> void:
	var operator: CharacterBody2D = actors["厨师"] if investigation_phase == "arrival" else player
	operator.face_point(sites["transmitter"].point)
	operator.pose("interact")
	await operator.sprite.animation_finished
	operator.pose("idle")
	transmitter_rules = true
	transmitter_connected = true
	if captain: transmitter_answer_seen = true
	_advance()

func dialogue_hear_broadcast() -> void:
	broadcast_heard = true
	evidence["transmitter"] = true
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
	_label(modal_box, tr("FA_UI_361") % player_name, 21)
	modal.add_footer([["FA_UI_169", func(): get_tree().reload_current_scene()]])

func _open_meeting() -> void:
	if player.position.distance_to(sites["meeting"].point) > 100:
		_show_message("FA_UI_063", "FA_UI_368")
		return
	if investigation_phase == "arrival":
		if ARRIVAL_THRESHOLD.has_point(player.position) and not arrival_started: _begin_dialogue("activity_hall_opening")
		return
	if investigation_phase == "dorm_search":
		_show_message("FA_UI_369", "FA_UI_370")
		return
	if investigation_phase == "regroup":
		_begin_dialogue("first_regrouping_meeting")
		return
	if investigation_phase == "paired":
		var missing: Array[String] = []
		for id in PAIR_SITES[paired_companion]:
			if not deep_inspections.has(id): missing.append(tr(sites[id].title_id))
		if not missing.is_empty():
			_show_message("FA_UI_364", tr("FA_UI_371") + _localized_join(missing))
			return
		_begin_dialogue("exchange_findings", "meeting_board")
		return
	_show_meeting_board()

func _show_meeting_board() -> void:
	modal_locale_refresh = Callable(self, "_show_meeting_board")
	_open_modal("FA_UI_064", "meeting")
	for claim in INVESTIGATION_CONTENT.meeting_hypotheses:
		var claim_button := _button(modal_box, claim.label_id, _challenge.bind(claim.id), disputed[claim.id])
		claim_button.tooltip_text = tr("FA_UI_372") + " · " + tr(claim.label_id)
	if _all_disputed():
		captain = true
	modal.add_footer([["FA_UI_162", _close_modal], ["FA_MIND_001", _request_flow]])
	_update_status()

func _walkable(center: Vector2) -> bool:
	return FieldNavigation.walkable(center, floors, _navigation_obstacles())

func _route(start: Vector2, finish: Vector2, mover: CharacterBody2D = null) -> PackedVector2Array:
	if mover == null: mover = player
	return FieldNavigation.route(start, finish, floors, _navigation_obstacles(start, mover), Vector2i(layout.rows[0].length(), layout.rows.size()))

func _walk_actor_to(actor: CharacterBody2D, destination: Vector2) -> bool:
	# Stage through real collision and replan if another actor blocks the route.
	actor.travelling = false
	if actor.seated: actor.stand_up()
	var route := _route(actor.position, destination, actor)
	if route.is_empty(): return false
	actor.follow_path(route)
	var previous := actor.position
	var stalled := 0.0
	var elapsed := 0.0
	while actor.travelling and elapsed < 45.0:
		await get_tree().physics_frame
		var delta := get_physics_process_delta_time()
		elapsed += delta
		stalled = stalled + delta if actor.position.distance_to(previous) < 0.25 else 0.0
		previous = actor.position
		if stalled > 0.5:
			var replacement := _route(actor.position, destination, actor)
			if not replacement.is_empty():
				actor.path = replacement
				actor.waypoint = 0
				actor.target = replacement[0]
			stalled = 0.0
	if actor.travelling:
		actor.travelling = false
		actor.velocity = Vector2.ZERO
		actor.pose("idle")
		actor.arrived.emit()
		push_warning("FirstAwakening staging blocked: " + actor.name + " at " + str(actor.position) + " target " + str(actor.target) + " toward " + str(destination))
		for index in actor.get_slide_collision_count(): print("STAGING COLLIDER ", actor.get_slide_collision(index).get_collider().get_path())
	return actor.position.distance_to(destination) < 28.0

func _gather_party() -> void:
	# Room searches finish before anyone reports what they found.
	while not npc_searches.is_empty():
		await get_tree().physics_frame
	await _walk_actor_to(player, $MeetingSpots/Detective.position)
	var destinations := {"工程师": $MeetingSpots/Engineer.position, "厨师": $MeetingSpots/Chef.position, "音乐家": $MeetingSpots/Musician.position}
	for id in destinations:
		await _walk_actor_to(actors[id], destinations[id])
	conversation_focus = ""

func dialogue_prepare_transmitter() -> void:
	await _walk_actor_to(player, Vector2(440, 624))
	await _walk_actor_to(actors["厨师"], Vector2(504, 584))

	dialogue_face_group("transmitter")

func dialogue_question_actor(name: String) -> void:
	var destination: Vector2 = {"工程师": Vector2(472, 584), "音乐家": Vector2(552, 608)}[name]
	await _walk_actor_to(actors[name], destination)
	dialogue_face_group("transmitter")

func dialogue_prepare_answer() -> void:
	# The delegate reaches the handle by player input; companions join him there.
	if not captain: return
	var destinations := {"工程师": Vector2(440, 624), "厨师": Vector2(552, 624), "音乐家": Vector2(584, 584)}
	for id in destinations:
		await _walk_actor_to(actors[id], destinations[id])
	dialogue_face_group("transmitter")

func dialogue_disconnect_transmitter() -> void:
	transmitter_connected = false

func _challenge(hypothesis: String) -> void:
	for claim in INVESTIGATION_CONTENT.meeting_hypotheses:
		if claim.id == hypothesis:
			_open_modal(claim.title_id, "meeting_evidence")
			_label(modal_box, tr(claim.claim_id), 19)
	var shelf := HFlowContainer.new()
	shelf.add_theme_constant_override("h_separation", 8)
	shelf.add_theme_constant_override("v_separation", 4)
	modal_box.add_child(shelf)
	for id in evidence:
		if sites.has(id) and id not in ["engineer", "chef", "musician", "meeting", "transmitter"]:
			_button(shelf, sites[id].title_id, _present_evidence.bind(hypothesis, id))
	modal.add_footer([["FA_UI_373", _show_meeting_board]])

func _present_evidence(hypothesis: String, id: String) -> void:
	for claim in INVESTIGATION_CONTENT.meeting_hypotheses:
		if claim.id != hypothesis: continue
		var related: bool = id in claim.get("evidence_all", []) or id in claim.get("evidence_any", [])
		if not related or not _can_challenge(hypothesis):
			_begin_dialogue("weak_evidence", "meeting_board")
			return
		if not _replay_submission_validation().ready:
			_show_message("FA_UI_374", "FA_UI_375")
			return
		_begin_dialogue("challenge_" + hypothesis, "meeting_board")
		return

func _all_disputed() -> bool:
	return state.all_disputed()

func dialogue_accept_challenge(hypothesis: String) -> void:
	disputed[hypothesis] = true
	_advance()
	if _all_disputed():
		captain = true
		dialogue_after = "nomination"
	_update_status()

func dialogue_weak_evidence() -> void:
	_advance(1)

func _request_flow() -> void:
	if not meeting_done:
		_show_message("FA_MIND_001", "FA_UI_376")
		return
	_open_flow()
	if not mind_introduction_seen:
		mind_introduction_seen = true
		modal.process_mode = Node.PROCESS_MODE_DISABLED
		_begin_dialogue("mind_intro", "mind_ready", true)

func _open_flow() -> void:
	if not meeting_done: return
	preload("res://scenes/FirstAwakening/MindPresentation.gd").open(self)

func _hypothesis_test(hypothesis: String) -> void:
	_show_hypothesis_probe(hypothesis)
	_update_status()

func _show_hypothesis_probe(hypothesis: String) -> void:
	if not meeting_done: return
	_open_modal("FA_UI_377", "message")
	_label(modal_box, bridge.ProbeHypothesis(hypothesis), 21)
	modal.add_footer([["FA_UI_378", _request_flow]])

func _run_replay() -> void:
	simulated_draft_snapshot.clear()
	var program := _compile_program()
	if program.has("error"):
		mind_note = str(program.error)
		_refresh_replay_feedback()
		return
	if program.states.is_empty():
		mind_note = "当前草稿没有可运行的行动。"
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
	simulated_draft_snapshot = _current_replay_input()
	_open_flow()

func _current_replay_input() -> Dictionary:
	return deduction.current_replay_input()

func _replay_submission_validation() -> Dictionary:
	return deduction.replay_submission_validation()

func _build_replay_pause_points() -> void:
	deduction.build_replay_pause_points()

func _compile_program() -> Dictionary:
	return deduction.compile_program(bridge)

func _replay_context() -> Dictionary:
	return deduction.replay_context()

func _reveal_replay_point() -> void:
	deduction.reveal_replay_point()

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
	if mind_dirty or not replay_reached.has(title):
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
	var pending: Dictionary = pending_inspections[id]
	pending_inspections.erase(id)
	_verify_trace(pending.title, pending.required, id)
	return true

func _verify_trace(title: String, required: String, actual_site: String) -> void:
	if mind_dirty or sim_result.is_empty() or not replay_reached.has(title):
		return
	if not _trace_visible(required) or not evidence.has(actual_site):
		_show_message("FA_UI_033", "FA_UI_165")
		return
	var clue: Dictionary = {}
	for entry in INVESTIGATION_CONTENT.replay_clues:
		if entry.canonical == title:
			clue = entry
			break
	var lead_id := "FA_UI_121"
	if actual_site == "trace" and not evidence.has("paper"):
		lead_id = "FA_UI_043"
	var title_display_id: String = clue.get("pause_title_id", "FA_UI_121")
	_show_detail(actual_site, "FA_UI_034|" + title_display_id, lead_id + "\n\n" + sites[actual_site].description_id, sites[actual_site].diagram)
	_refresh_replay_feedback()
	_update_status()

func _open_answer() -> void:
	if not broadcast_heard:
		_show_message("FA_UI_082", "FA_UI_037")
		return
	modal_locale_refresh = Callable(self, "_open_answer")
	_open_modal("FA_UI_145", "answer")
	var replay_validation: Dictionary = _replay_submission_validation()
	if not replay_validation.ready:
		_label(modal_box, "\n".join(replay_validation.reasons), 17, modal.MUTED)
		_button(modal_box, "FA_UI_229", _request_flow).size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	for section in ANSWER_CATALOG.sections:
		var options := []
		for option in section.options:
			var refuted: bool = captain and str(option.get("outcome", "")).begins_with("reject_")
			if refuted and answer_slots[section.slot] == option.value:
				answer_slots[section.slot] = ""
				answer_slot_display_ids[section.slot] = ""
			options.append([option.value, not refuted and AnswerRules.available(option, evidence, {}), option.display_id])
		_answer_choice(section.slot, section.title_id, options)
	var submit := modal.add_footer([["FA_UI_162", _close_modal], ["FA_UI_100", _submit_answer]])
	submit.disabled = answer_slots.values().has("") or not captain or not replay_validation.ready

func _answer_choice(slot: String, title: String, options: Array) -> void:
	_label(modal_box,title,17,modal.MUTED)
	var menu := MenuButton.new()
	menu.text = tr(answer_slot_display_ids[slot]) if not answer_slot_display_ids[slot].is_empty() else tr("FA_UI_166")
	menu.alignment = HORIZONTAL_ALIGNMENT_LEFT
	menu.custom_minimum_size.y = 44
	menu.add_theme_font_size_override("font_size",21)
	modal.paper_field(menu)
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
	var replay_validation: Dictionary = _replay_submission_validation()
	if not replay_validation.ready:
		_show_message("FA_UI_386", "\n".join(replay_validation.reasons))
		return
	_advance()
	if answer_slots.values().has(""):
		_show_message("FA_UI_156", "FA_UI_016")
		return
	var resolution: String = AnswerRules.resolution(ANSWER_CATALOG.sections, answer_slots, evidence, {})
	if not captain and not resolution.begins_with("reject_"):
		resolution = "reject_unsupported"
	if resolution.begins_with("reject_"):
		_begin_dialogue(resolution, "answer")
	else:
		_finish(resolution)

func can_access_dorm(owner: String) -> bool:
	if not state.can_access_dorm(owner):
		return false
	if owner == "detective":
		return true
	return actors.has(_paired_actor()) and actors[_paired_actor()].position.distance_to(player.position) <= 96

func _site_accessible(id: String) -> bool:
	if not sites.has(id): return false
	var owner := "detective"
	for suffix in ["1", "2", "3"]:
		if id in ["room_" + suffix, "bed_" + suffix, "window_" + suffix, "desk_" + suffix]:
			owner = {"1": "engineer", "2": "cook", "3": "musician"}[suffix]
			if not can_access_dorm(owner): return false
	if investigation_phase == "arrival": return id in ["meeting", "transmitter", "engineer", "chef", "musician", "room_0"]
	if investigation_phase == "dorm_search": return id in ["window_0", "bed_0", "drawer", "paper", "room_0", "meeting"]
	return true

func _navigation_obstacles(mover_position := Vector2(INF, INF), mover: CharacterBody2D = null) -> Array[Rect2]:
	var result: Array[Rect2] = obstacle_rects.duplicate()
	for door in scene_items.get_children():
		if door.has_method("access_allowed") and not door.access_allowed():
			if mover != null and mover != player and door.owner_actor() == mover: continue
			result.append(Rect2(door.position + Vector2(-32, -8), Vector2(64, 8)))
	for actor in actors.values():
		if actor == mover or (mover == null and actor == player): continue
		result.append(Rect2(actor.position + Vector2(-8, -12), Vector2(16, 12)))
	return result

func _own_dorm_complete() -> bool:
	return state.is_own_dorm_complete()

func _place_party_at_table() -> void:
	ensemble.place_party_at_table()

func dialogue_start_dorm_search() -> void:
	broadcast_heard = true
	evidence["transmitter"] = true
	investigation_phase = "dorm_search"
	for name in ["工程师", "厨师", "音乐家"]:
		npc_searches[name] = true
		_search_npc_room(name)

func _search_npc_room(name: String) -> void:
	var actor: CharacterBody2D = actors[name]
	var room_x: float = {"工程师": 512.0, "厨师": 832.0, "音乐家": 1152.0}[name]
	await _walk_actor_to(actor, Vector2(room_x, 232))
	actor.face_point(Vector2(room_x + 66, 112))
	actor.pose("inspect")
	await actor.sprite.animation_finished
	actor.pose("idle")
	var common_point: Vector2 = {"工程师": Vector2(580, 910), "厨师": Vector2(900, 600), "音乐家": Vector2(400, 730)}[name]
	await _walk_actor_to(actor, common_point)
	actor.face_point({"工程师": Vector2(644, 954), "厨师": Vector2(882, 580), "音乐家": Vector2(320, 652)}[name])
	npc_searches.erase(name)

func dialogue_pair(owner: String) -> void:
	paired_companion = owner
	investigation_phase = "paired"
	deep_inspections.clear()
	npc_searches["other_pair"] = true
	_inspect_other_pair()

func _inspect_other_pair() -> void:
	var others: Array[CharacterBody2D] = []
	for name in ["工程师", "厨师", "音乐家"]:
		if name != _paired_actor(): others.append(actors[name])
	var locations: Array = {"engineer": ["pantry", "study"], "cook": ["study", "hall"], "musician": ["hall", "pantry"]}[paired_companion]
	var spots := {"pantry": [Vector2(904, 600), Vector2(952, 600)], "study": [Vector2(400, 730), Vector2(456, 730)], "hall": [Vector2(580, 910), Vector2(700, 910)]}
	var subjects := {"pantry": Vector2(882, 580), "study": Vector2(320, 652), "hall": Vector2(644, 954)}
	for location in locations:
		for index in others.size():
			await _walk_actor_to(others[index], spots[location][index])
			others[index].face_point(subjects[location])
		await get_tree().create_timer(0.8).timeout
	npc_searches.erase("other_pair")

func _paired_actor() -> String:
	return state.paired_actor()

func _follow_companion() -> void:
	if investigation_phase != "paired" or paired_companion.is_empty(): return
	var partner: CharacterBody2D = actors[_paired_actor()]
	if partner.position.distance_to(player.position) <= 70: return
	companion_route_cooldown -= get_physics_process_delta_time()
	var stale: bool = partner.travelling and ((not partner.path.is_empty() and partner.path[-1].distance_to(player.position) > 96) or not FieldNavigation.walkable(partner.target, floors, _navigation_obstacles(partner.position, partner)))
	if partner.travelling and not stale:
		if companion_route_cooldown <= 0:
			companion_route_cooldown = 0.75
			var shorter := _route(partner.position, partner.path[-1], partner)
			# A temporary blockage must not send the companion around the entire wing
			# after the nearby passage has become free again.
			if shorter.size() > 1 and shorter.size() + 4 < partner.path.size() - partner.waypoint:
				partner.follow_path(shorter)
		return
	var offsets: Array[Vector2] = [player.position.direction_to(partner.position)]
	offsets.append_array(APPROACH_DIRECTIONS)
	var best := PackedVector2Array()
	for offset in offsets:
		var destination := player.position + offset.normalized() * 48
		if not _walkable(destination): continue
		var route := _route(partner.position, destination, partner)
		if route.size() > 1 and (best.is_empty() or route.size() < best.size()): best = route
	if not best.is_empty():
		companion_route_cooldown = 0.75
		partner.follow_path(best)

func _record_deep_inspection(id: String) -> void:
	if investigation_phase == "paired" and id in PAIR_SITES[paired_companion]:
		state.record_deep_inspection(id)

func dialogue_exchange_findings() -> void:
	# Other pair's inspection is reported at the table, never passed off as sight.
	for owner in PAIR_SITES:
		if owner == paired_companion: continue
		for id in PAIR_SITES[owner]:
			if not evidence.has(id):
				evidence[id] = true
				evidence_sources[id] = "同伴勘验后转述"
	for id in ["stop", "floor_wipe"]:
		if not evidence.has(id):
			evidence[id] = true
			evidence_sources[id] = "同伴勘验后转述"
	meeting_done = true
	meeting_introduction_seen = true
	investigation_phase = "discussion"
