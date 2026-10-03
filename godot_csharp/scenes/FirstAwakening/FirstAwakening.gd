extends Node2D
## First Awakening: 32px host field for characters, navigation, camera and dialogue.

const FieldNavigation = preload("res://scenes/FirstAwakening/FieldNavigation.gd")
const FieldModal = preload("res://scenes/FirstAwakening/FieldModal.gd")
const LAYOUT: Resource = preload("res://ShareAsset/Internal/Props/FirstAwakeningInterior/layout.tres")
const ZHAOHUA_FONT: Font = preload("res://ShareAsset/External/ZhaohuaTypeWriter/ZhaohuaTypeWriter.ttf")
const NEW_TEGOMIN_FONT: Font = preload("res://ShareAsset/External/newtegomin/NewTegomin-Regular.ttf")
const FIELD_THEME_ZH: Theme = preload("res://scenes/FirstAwakening/field_theme_zh.tres")
const FIELD_THEME_OTHER: Theme = preload("res://scenes/FirstAwakening/field_theme_other.tres")

const CLICK_STOP_RADIUS := 8.0
const HUD_BAR_HEIGHT := 60.0
const CLICK_REPATH_DELAY := 0.15
const APPROACH_DIRECTIONS := [
	Vector2.UP, Vector2.DOWN, Vector2.LEFT, Vector2.RIGHT,
	Vector2(-1, -1), Vector2(1, -1), Vector2(-1, 1), Vector2(1, 1)
]

var floors: Dictionary = {}
var obstacle_rects: Array[Rect2] = []
var layout: Dictionary = {}
var sites: Dictionary = {}
var active_sites: Dictionary = {}
class StateShim extends RefCounted:
	var evidence: Dictionary = {}
	## Replays step through committed actions, not wall-clock time.
	var event_mode := true

var state := StateShim.new()
var evidence: Dictionary:
	get: return state.evidence
	set(v): state.evidence = v
var evidence_sources: Dictionary = {}
## Dormitory interiors the replay may show uncovered; only the detective's own room is known.
var known_dorms: Dictionary = {"detective": true}
var actors: Dictionary = {}
var player: CharacterBody2D
var camera: Camera2D

@onready var scene_items: Node2D = $ObjectsAndPeople

var click_path := PackedVector2Array()
var click_waypoint := 0
var click_interaction_id := ""
var click_last_position := Vector2(INF, INF)
var click_stall_seconds := 0.0
## Repaths without real progress; a route that keeps failing is dropped instead of looping.
var click_blocked_repaths := 0
var click_progress_anchor := Vector2(INF, INF)
const CLICK_MAX_BLOCKED_REPATHS := 4

var nearest := ""
var reachable: Array[String] = []
var cycle_offset := 0
var last_cell := Vector2i(-1, -1)
var last_proximity_position := Vector2(INF, INF)
var proximity_refresh_frames := 0

var paired_companion: String = ""
var ended: bool = false
var story_lock: bool = false

var ui: CanvasLayer
var modal: FieldModal
var modal_box: VBoxContainer
var hud_title: Label
var hud_hint: Label
var hud_time: Label
var modal_open: bool = false
var mode: String = ""
var modal_locale_refresh: Callable
var language_selector: OptionButton
var active_locale_theme: Theme

var dialogue: CanvasLayer
var bolt_visual: CanvasItem
var minute: int = 420

func _notification(what: int) -> void:
	if what != NOTIFICATION_TRANSLATION_CHANGED:
		return
	if is_instance_valid(player):
		_pick_nearest()
		_update_status()
	if is_instance_valid(language_selector):
		_refresh_language_selector()
	_apply_locale_theme()
	if modal_open and modal_locale_refresh.is_valid():
		call_deferred("_refresh_modal_locale")
	if is_instance_valid(dialogue) and dialogue.active and dialogue.line != null:
		dialogue.speaker.text = _localized_speaker(dialogue.line.character)
		dialogue.body.text = dialogue.line.text

func _refresh_modal_locale() -> void:
	if modal_open and modal_locale_refresh.is_valid():
		modal_locale_refresh.call()

func _localized_speaker(canonical_name: String) -> String:
	return canonical_name

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
	_setup_case_runtime()
	var hints: Node2D = get_node_or_null("InteractionHints")
	if hints: hints.set("source", self)
	_build_ui()
	dialogue = $Dialogue
	dialogue.layer = ui.layer + 1
	_apply_locale_theme()
	dialogue.use_compact_layout()
	dialogue.heading.text = tr("FA_UI_050")
	dialogue.panel.hide()
	dialogue.line_shown.connect(_on_dialogue_line_shown)
	dialogue.finished.connect(_on_dialogue_finished)
	var reasoning_music: AudioStreamPlayer = get_node_or_null("ReasoningMusic")
	if reasoning_music and reasoning_music.stream:
		var loop_stream := reasoning_music.stream.duplicate() as AudioStreamMP3
		loop_stream.loop = true
		reasoning_music.stream = loop_stream
		reasoning_music.volume_db = -12.0
		reasoning_music.bus = &"Master"
		reasoning_music.play()
	camera = $Camera2D
	camera.position = player.position
	camera.make_current()
	_start_case()

func _setup_case_runtime() -> void:
	pass

func _start_case() -> void:
	pass

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
	pass

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
	player.standard = player.standard.duplicate()
	for direction_name in ["front", "back", "left"]:
		player.standard.set_animation_speed("walk_" + direction_name, 7.5)
	player.sprite.sprite_frames = player.standard
	player.pose("idle", "front")
	player.sit_at("front", 4.0)
	actors["工程师"].pose("idle", "right")
	actors["厨师"].pose("idle", "back")
	actors["音乐家"].pose("idle", "left")

func _place_party_at_table() -> void:
	actors["工程师"].position = $MeetingSpots/Engineer.position
	actors["厨师"].position = $MeetingSpots/Chef.position
	actors["音乐家"].position = $MeetingSpots/Musician.position
	actors["工程师"].sit_at("right", 0.0)
	actors["厨师"].sit_at("back", 0.0)
	actors["音乐家"].sit_at("left", 0.0)

func _leave_bed() -> void:
	player.position = $WakePosition.position
	player.stand_up()

func _isolation_door() -> void:
	var door_node := scene_items.get_node_or_null("MechanicalIsolationDoor")
	if door_node and door_node.has_node("InnerBolt"):
		bolt_visual = door_node.get_node("InnerBolt")

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

func _physics_process(_delta: float) -> void:
	if not is_instance_valid(player):
		return
	if camera:
		camera.position = Vector2(clampf(player.position.x, 320, 960), clampf(player.position.y - 24, 248, 776)).round()
	if player.position.distance_to(last_proximity_position) > 16.0:
		proximity_refresh_frames = 16
	last_proximity_position = player.position
	var cell := Vector2i(player.position / 16.0)
	if cell != last_cell:
		last_cell = cell
		cycle_offset = 0
	_pick_nearest()
	if hud_hint and not click_interaction_id.is_empty() and sites.has(click_interaction_id):
		hud_hint.text = "→ " + str(sites[click_interaction_id].get("title", ""))
	proximity_refresh_frames = maxi(0, proximity_refresh_frames - 1)
	_update_status()
	if modal_open or (dialogue != null and dialogue.active) or ended or story_lock:
		_cancel_click_navigation()
		if not player.travelling: player.velocity = Vector2.ZERO
		return
	_follow_companion()
	var direction := Vector2(
		float(Input.is_physical_key_pressed(KEY_D) or Input.is_physical_key_pressed(KEY_RIGHT)) - float(Input.is_physical_key_pressed(KEY_A) or Input.is_physical_key_pressed(KEY_LEFT)),
		float(Input.is_physical_key_pressed(KEY_S) or Input.is_physical_key_pressed(KEY_DOWN)) - float(Input.is_physical_key_pressed(KEY_W) or Input.is_physical_key_pressed(KEY_UP))
	)
	var movement_scale := 1.0
	if direction.length_squared() > 0:
		_cancel_click_navigation()
	if player.seated and direction.length_squared() > 0:
		_leave_bed()
	if direction == Vector2.ZERO and click_interaction_id != "":
		var target_id := click_interaction_id
		if active_sites.has(target_id) or player.position.distance_to(_site_point(target_id)) <= float(sites[target_id].reach) + 4.0:
			_complete_click_interaction(target_id)
			return
	if direction == Vector2.ZERO and not click_path.is_empty():
		click_stall_seconds = click_stall_seconds + _delta if player.position.distance_to(click_last_position) < 0.25 else 0.0
		click_last_position = player.position
		if player.position.distance_to(click_progress_anchor) > 12.0:
			click_progress_anchor = player.position
			click_blocked_repaths = 0
		if click_stall_seconds >= CLICK_REPATH_DELAY:
			var pending_id := click_interaction_id
			var destination := click_path[-1]
			click_stall_seconds = 0.0
			click_blocked_repaths += 1
			if click_blocked_repaths > CLICK_MAX_BLOCKED_REPATHS:
				# Something the grid cannot see is in the way; stop rather than push forever.
				_cancel_click_navigation()
				return
			if pending_id.is_empty(): _move_to_click(destination)
			else: _approach_site(pending_id)
			return
		while click_waypoint < click_path.size() and player.position.distance_to(click_path[click_waypoint]) <= (CLICK_STOP_RADIUS if click_waypoint == click_path.size() - 1 else 0.01):
			click_waypoint += 1
		if click_waypoint >= click_path.size():
			click_path.clear()
			if click_interaction_id != "":
				var target_id := click_interaction_id
				_cancel_click_navigation()
				if active_sites.has(target_id) or player.position.distance_to(_site_point(target_id)) <= float(sites[target_id].reach) + 8.0:
					_complete_click_interaction(target_id)
					return
		else:
			var remaining := click_path[click_waypoint] - player.position
			direction = remaining.normalized()
			movement_scale = minf(1.0, remaining.length() / maxf(player.speed * _delta, 0.00001))
	if not player.seated: player.move_input(direction.normalized() * movement_scale)

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
		var ahead := 1.0 if distance < 4.0 else offset.normalized().dot(facing)
		var score: float = distance * (1.35 - 0.35 * ahead)
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
		hud_hint.text = "" if nearest == "" else str(sites[nearest].get("title", ""))

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

func _process(_delta: float) -> void:
	if hud_hint:
		hud_hint.visible = not modal_open and (dialogue == null or not dialogue.active)

func _input(event: InputEvent) -> void:
	if story_lock or (dialogue != null and dialogue.active):
		return
	if event is not InputEventMouseButton or not event.pressed or event.button_index != MOUSE_BUTTON_LEFT or modal_open or ended:
		return
	if language_selector.get_global_rect().has_point(event.position) or language_selector.get_popup().visible:
		return
	# The top bar is interface, not floor: clicks there never walk the detective.
	if event.position.y < HUD_BAR_HEIGHT:
		get_viewport().set_input_as_handled()
		return
	click_blocked_repaths = 0
	click_progress_anchor = player.position
	_cancel_click_navigation()
	var destination: Vector2 = get_global_transform_with_canvas().affine_inverse() * event.position
	var site_id := _clicked_site(destination)
	if site_id == "":
		_move_to_click(destination)
	else:
		_approach_site(site_id)
	get_viewport().set_input_as_handled()

func _unhandled_input(event: InputEvent) -> void:
	if story_lock or (dialogue != null and dialogue.active):
		return
	if modal_open:
		if event is InputEventKey and event.pressed and event.keycode == KEY_ESCAPE:
			_close_modal()
		return
	if ended and event is InputEventKey and event.pressed and event.keycode == KEY_R:
		get_tree().reload_current_scene()
		return
	if event is InputEventKey and event.pressed and not event.echo:
		if event.keycode in [KEY_E, KEY_ENTER] and nearest != "":
			_complete_click_interaction(nearest)
		elif event.keycode == KEY_TAB and reachable.size() > 1:
			cycle_offset += 1
			_pick_nearest()

func _cancel_click_navigation() -> void:
	click_path.clear()
	click_waypoint = 0
	click_interaction_id = ""
	click_last_position = Vector2(INF, INF)
	click_stall_seconds = 0.0
	if is_instance_valid(player) and not player.travelling:
		player.velocity = Vector2.ZERO

func _complete_click_interaction(id: String) -> void:
	_cancel_click_navigation()
	player.move_input(Vector2.ZERO)
	_interact(id)

func _interact(_id: String) -> void:
	pass

func _move_to_click(destination: Vector2) -> void:
	_cancel_click_navigation()
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

func _clicked_site(pos: Vector2) -> String:
	var visible_sites := {}
	for id in sites:
		if _site_accessible(id): visible_sites[id] = sites[id]
	return FieldNavigation.clicked_site(pos, visible_sites, actors)

func _approach_site(id: String) -> void:
	_cancel_click_navigation()
	if not _site_accessible(id):
		return
	var point := _site_point(id)
	var reach := float(sites[id].reach)
	if player.position.distance_to(point) <= reach:
		_complete_click_interaction(id)
		return
	if player.seated:
		_leave_bed()
	var best_path := PackedVector2Array()
	var best_destination := Vector2.ZERO
	var best_cost := INF
	var grid := FieldNavigation.build_route_grid(floors, _navigation_obstacles(player.position, player), Vector2i(layout.rows[0].length(), layout.rows.size()))
	for fraction in [1.0, 0.55]:
		for heading in APPROACH_DIRECTIONS:
			var candidate: Vector2 = point + heading.normalized() * (reach - CLICK_STOP_RADIUS - 2.0) * fraction
			if not _walkable(candidate):
				continue
			var route := FieldNavigation.route_on_grid(player.position, candidate, grid)
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

func _walkable(point: Vector2) -> bool:
	var cell := Vector2i(point / 32.0)
	if not floors.has(cell): return false
	for rect in obstacle_rects + _closed_room_rects(player):
		if rect.grow(8.0).has_point(point): return false
	return true

## Doorways and interiors of dormitories the walker may not enter; their leaves stay solid.
func _closed_room_rects(walker: CharacterBody2D) -> Array[Rect2]:
	var rects: Array[Rect2] = []
	if not is_instance_valid(scene_items): return rects
	for door in scene_items.get_children():
		if not door.has_method("access_allowed") or door.access_allowed(): continue
		if is_instance_valid(walker) and door.owner_actor() == walker: continue
		rects.append(door.threshold_collision_rect())
		rects.append(door.interior_bounds)
	return rects

func _navigation_obstacles(_walker_pos: Vector2, _walker: CharacterBody2D) -> Array[Rect2]:
	var obstacles := obstacle_rects.duplicate()
	obstacles.append_array(_closed_room_rects(_walker))
	for other_actor in actors.values():
		if other_actor != _walker and is_instance_valid(other_actor):
			obstacles.append(Rect2(other_actor.position.x - 8, other_actor.position.y - 8, 16, 16))
	return obstacles

func _route(from: Vector2, to: Vector2, actor: CharacterBody2D = null) -> PackedVector2Array:
	var walker := actor if actor else player
	var grid := FieldNavigation.build_route_grid(floors, _navigation_obstacles(from, walker), Vector2i(layout.rows[0].length(), layout.rows.size()))
	return FieldNavigation.route_on_grid(from, to, grid)

func _site_accessible(_id: String) -> bool:
	return true

func _follow_companion() -> void:
	if paired_companion == "": return
	var actor: CharacterBody2D = actors.get(paired_companion)
	if actor and actor.position.distance_to(player.position) > 96 and not actor.travelling:
		var destination := player.position + Vector2(32, 24)
		if _walkable(destination): actor.follow_path(_route(actor.position, destination, actor))

func _open_modal(title: String, next_mode: String) -> void:
	modal_locale_refresh = Callable()
	mode = next_mode
	modal_open = true
	modal.open_content(title, next_mode, active_locale_theme)

func _close_modal() -> void:
	modal_locale_refresh = Callable()
	modal.hide()
	modal_open = false
	mode = ""

func _show_message(title: String, words: String) -> void:
	_open_modal(title, "message")
	_label(modal_box, words, 21)
	modal.add_footer([["FA_UI_148", _close_modal]])

func _button(parent: Node, words: String, action: Callable, disabled := false) -> Button:
	var button: Button = Button.new()
	button.text = words
	button.disabled = disabled
	button.pressed.connect(action)
	parent.add_child(button)
	return button

func _label(parent: Node, words: String, font_size := 18, color := FieldModal.INK) -> RichTextLabel:
	var label := RichTextLabel.new()
	label.text = words
	label.bbcode_enabled = true
	label.fit_content = true
	label.scroll_active = false
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.add_theme_font_size_override("normal_font_size", font_size)
	label.add_theme_color_override("default_color", color)
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	parent.add_child(label)
	return label

func _begin_dialogue(_title: String, _after := "", _keep_scene := false) -> void:
	pass

func _on_dialogue_line_shown(_character: String) -> void:
	pass

func _on_dialogue_finished() -> void:
	pass
