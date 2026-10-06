extends Node2D
## 《安眠者有福》第一章《门的这一侧》：生活翼现场、调查、重演入口与始业之答。
## 推演在 C#：AwakeningEngine → NightReplay（Core 的 Observation / DecisionSystem / SocialSystem / SimulationSession）。
## 本脚本是宿主：地图、移动、相机、界面、对白与流程。

const Content := preload("res://scenes/Awakening/AwakeningContent.gd")
const FieldNavigation := preload("res://scenes/Shared/FieldNavigation.gd")
const UI_SCENES := {
	"AnswerHeader": preload("res://scenes/Awakening/UI/AnswerHeader.tscn"),
	"AnswerList": preload("res://scenes/Awakening/UI/AnswerList.tscn"),
	"Board": preload("res://scenes/Awakening/UI/Board.tscn"),
	"Comparisons": preload("res://scenes/Awakening/UI/Comparisons.tscn"),
	"Ending": preload("res://scenes/Awakening/UI/Ending.tscn"),
	"EvidenceCard": preload("res://scenes/Awakening/UI/EvidenceCard.tscn"),
	"Footer": preload("res://scenes/Awakening/UI/Footer.tscn"),
	"NameEntry": preload("res://scenes/Awakening/UI/NameEntry.tscn"),
	"Notebook": preload("res://scenes/Awakening/UI/Notebook.tscn"),
	"NotebookSection": preload("res://scenes/Awakening/UI/NotebookSection.tscn"),
	"SoleChoice": preload("res://scenes/Awakening/UI/SoleChoice.tscn"),
	"SoundExperiment": preload("res://scenes/Awakening/UI/SoundExperiment.tscn"),
	"SoundResults": preload("res://scenes/Awakening/UI/SoundResults.tscn"),
	"TraceCard": preload("res://scenes/Awakening/UI/TraceCard.tscn"),
	"TubeMenu": preload("res://scenes/Awakening/UI/TubeMenu.tscn"),
}

const Paper := preload("res://scenes/Shared/UI/Paper.gd")
const Sound := preload("res://scenes/Shared/Audio/Sound.gd")
const ReplayManuscript := preload("res://scenes/Shared/ReplayManuscript.tscn")
const SightMask := preload("res://scenes/Shared/SightMask.tscn")
const ReplayStage := preload("res://scenes/Awakening/ReplayStage.gd")
const TraceDecals := preload("res://scenes/Awakening/TraceDecals.gd")
const EngineScript := preload("res://scenes/Awakening/AwakeningEngine.cs")
## 乙区地图（tools/build_awakening_map.py 生成）：地面格、家具足迹、书架积灰、壁饰心声。
const LAYOUT: Resource = preload("res://scenes/Awakening/map_layout.tres")
const ART := "res://ShareAsset/Internal/Props/AwakeningEvidence/"
const CLICK_STOP_RADIUS := 8.0
const HUD_BAR_HEIGHT := 60.0
const CLICK_REPATH_DELAY := 0.15
const CLICK_MAX_BLOCKED_REPATHS := 4
const APPROACH_DIRECTIONS := [Vector2.UP, Vector2.DOWN, Vector2.LEFT, Vector2.RIGHT, Vector2(-1, -1), Vector2(1, -1), Vector2(-1, 1), Vector2(1, 1)]
# ------------------------------------------------------------------ field ---
var floors: Dictionary = {}
var obstacle_rects: Array[Rect2] = []
var layout: Dictionary = {}
var sites: Dictionary = {}
var actors: Dictionary = {}
var player: CharacterBody2D
var camera: Camera2D
var click_path := PackedVector2Array()
var click_waypoint := 0
var click_interaction_id := ""
var click_last_position := Vector2(INF, INF)
var click_stall_seconds := 0.0
var click_blocked_repaths := 0
var click_progress_anchor := Vector2(INF, INF)
var nearest := ""
var hovered := ""
var story_lock := false
var follow_camera := true
var sight: Node2D

# ------------------------------------------------------------------ story ---
var engine: Node = EngineScript.new()
var dialogue: CanvasLayer
var ui: CanvasLayer
var hud_title: Label
var hud_hint: Label
var hud_keys: Label
var modal_layer: CanvasLayer
var modal_root: Control
var modal_kind := ""
var manuscript: Control
var stage_node: Node2D
var decals: Node2D
var hover_tip: Label
var aside: Label
var aside_until := 0
var furniture_spoken: Dictionary = {}
var dialogue_closed_ms := -100000

var stage: String:
	get: return engine.Progress("state")["stage"]
	set(value): engine.SetProgress("state", "stage", value)
var detective_name: String:
	get: return engine.Progress("state")["detective_name"]
	set(value): engine.SetProgress("state", "detective_name", value)
var examined: Dictionary:
	get: return engine.Progress("examined")
	set(value): engine.ReplaceProgress("examined", value)
var experiments: Dictionary:
	get: return engine.Progress("experiments")
	set(value): engine.ReplaceProgress("experiments", value)
var helped: Dictionary:
	get: return engine.Progress("helped")
	set(value): engine.ReplaceProgress("helped", value)
var recalls: Dictionary:
	get: return engine.Progress("recalls")
	set(value): engine.ReplaceProgress("recalls", value)
var recall_marks: Dictionary:
	get: return engine.Progress("recall_marks")
	set(value): engine.ReplaceProgress("recall_marks", value)
var nudged: Dictionary:
	get: return engine.Progress("nudged")
	set(value): engine.ReplaceProgress("nudged", value)
var flags: Dictionary:
	get: return engine.Progress("flags")
	set(value): engine.ReplaceProgress("flags", value)
## 同伴自己去碰旧物的过程：who → {state: going/holding/done, alone, choice, thought, fragment, told, noticed}
var life: Dictionary:
	get: return engine.Progress("life")
	set(value): engine.ReplaceProgress("life", value)
## 已对旧物拿定主意的同伴（碰过或不碰），回忆按钮不再出现。
var decided: Dictionary:
	get: return engine.Progress("decided")
	set(value): engine.ReplaceProgress("decided", value)
var hand_invitations: Dictionary = {} # 仅由当地谈话邀请；真实走路，受阻即停。
var soles_compared: Dictionary:
	get: return engine.Progress("soles_compared")
	set(value): engine.ReplaceProgress("soles_compared", value)
var sound_rounds: int:
	get: return engine.Progress("state")["sound_rounds"]
	set(value): engine.SetProgress("state", "sound_rounds", value)
var last_unheard_ms: int:
	get: return engine.Progress("state")["last_unheard_ms"]
	set(value): engine.SetProgress("state", "last_unheard_ms", value)
var current_room := ""
var replay_open: bool:
	get: return engine.Progress("state")["replay_open"]
	set(value): engine.SetProgress("state", "replay_open", value)
var replay_vars: Array:
	get: return engine.Progress("state")["replay_vars"]
	set(value): engine.SetProgress("state", "replay_vars", value)
var replay_result: Dictionary:
	get: return engine.Progress("replay_result")
	set(value): engine.ReplaceProgress("replay_result", value)
var last_run_vars: Array:
	get: return engine.Progress("state")["last_run_vars"]
	set(value): engine.SetProgress("state", "last_run_vars", value)
var replay_count: int:
	get: return engine.Progress("state")["replay_count"]
	set(value): engine.SetProgress("state", "replay_count", value)
var full_match_seen: bool:
	get: return engine.Progress("state")["full_match_seen"]
	set(value): engine.SetProgress("state", "full_match_seen", value)
var last_sound: Array:
	get: return engine.Progress("state")["last_sound"]
	set(value): engine.SetProgress("state", "last_sound", value)
var answer_state: Dictionary:
	get: return engine.Progress("answer_state")
	set(value): engine.ReplaceProgress("answer_state", value)
var ended: bool:
	get: return engine.Progress("state")["ended"]
	set(value): engine.SetProgress("state", "ended", value)

## Race each external wait against host shutdown and disconnect both subscriptions.
signal closing
var _closing := false
class CancellableWait extends RefCounted:
	signal resumed
	var source: Signal
	var cancellation: Signal
	var done := false
	func finish() -> void:
		if done: return
		done = true
		if source.is_connected(finish): source.disconnect(finish)
		if cancellation.is_connected(finish): cancellation.disconnect(finish)
		resumed.emit()

func _wait_signal(source: Signal) -> void:
	await _wait_signal_or(source, closing)

func _wait_signal_or(source: Signal, cancellation: Signal) -> void:
	if _closing: return
	var wait := CancellableWait.new()
	wait.source = source
	wait.cancellation = cancellation
	source.connect(wait.finish)
	cancellation.connect(wait.finish)
	await wait.resumed

func begin_shutdown() -> void:
	if _closing: return
	_closing = true
	if is_instance_valid(dialogue): dialogue.cancel_presentation()
	if is_instance_valid(stage_node): stage_node.cancel_playback()
	closing.emit()
	for actor in actors.values():
		if not is_instance_valid(actor): continue
		actor.travelling = false
		actor.velocity = Vector2.ZERO
		actor.arrived.emit()
	hand_invitations.clear()
	if is_instance_valid(modal_root): close_modal()

func _exit_tree() -> void:
	begin_shutdown()

# ============================================================== lifecycle ===

func _ready() -> void:
	add_to_group("lifecycle_hosts")
	TranslationServer.set_locale("zh_CN")
	texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	get_window().content_scale_size = Vector2i(1280, 992)
	get_window().content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	get_window().content_scale_stretch = Window.CONTENT_SCALE_STRETCH_INTEGER
	get_window().size = Vector2i(1280, 992)
	get_window().title = Content.text("窗口标题")
	engine.name = "Engine"
	add_child(engine)
	Content.apply_content(engine.Content())
	_build_geometry()
	_build_characters()
	_build_props()
	_build_sites()
	_build_ui()
	# 视野（设计总纲 §四.6）：墙挡视线，视线外暗下去，视线外的人不显示。
	sight = SightMask.instantiate()
	sight.name = "Sight"
	sight.z_index = 40
	sight.host = self
	add_child(sight)
	var hints: Node2D = get_node_or_null("InteractionHints")
	if hints: hints.set("source", self)
	dialogue = $Dialogue
	dialogue.layer = 20
	dialogue.use_compact_layout()
	dialogue.single_press_keyboard_continue = true
	dialogue.dim_visited_responses = true
	dialogue.apply_locale_font(Paper.font(), null)
	dialogue.set_dialogue_cast(Content.DIALOGUE_CAST)
	dialogue.panel.hide()
	Sound.music("door_side", 2.5)
	camera = $Camera2D
	camera.position = player.position
	camera.make_current()
	if not has_meta("skip_intro"):
		call_deferred("_begin")

func _begin() -> void:
	if _closing: return
	await _play_plan(engine.BeginWake())
	if _closing: return

func _build_geometry() -> void:
	layout = LAYOUT.get_meta("data")
	for y in layout.rows.size():
		var row: String = layout.rows[y]
		for x in row.length():
			if row.substr(x, 1) in [".", "D"]:
				floors[Vector2i(x, y)] = true
	for item in layout.furniture:
		if item.footprint != null:
			var raw: Array = item.footprint
			obstacle_rects.append(Rect2(raw[0], raw[1], raw[2], raw[3]))

func _build_characters() -> void:
	var people: Node2D = $ObjectsAndPeople
	for key in Content.ACTOR_NODES:
		actors[Content.ACTOR_NODES[key]] = people.get_node(Content.ACTOR_NODES[key])
	player = actors["侦探"]
	player.standard = player.standard.duplicate()
	for direction_name in ["front", "back", "left"]:
		player.standard.set_animation_speed("walk_" + direction_name, 7.5)
	player.sprite.sprite_frames = player.standard
	player.position = Content.STAGING["起点·detective"]
	player.pose("idle", "front")
	player.sit_at("front", 4.0)
	# 醒来时同伴各在自己房里（霍铎 R-06、沈鼎 R-07、苏泠 R-08）；相遇时才到活动厅。
	for key in ["huo", "shen", "su"]:
		actor_of(key).position = Content.STAGING["起点·" + key]
	for key in ["huo", "shen", "su"]:
		actor_of(key).pose("idle", "front")
	# 人与人之间不互相阻挡：只与墙和家具碰撞。被挤到时，同伴自己让开（见 _make_way）。
	for actor in actors.values():
		actor.collision_mask = 1

func actor_of(key: String) -> CharacterBody2D:
	return actors[Content.ACTOR_NODES[key]]

func _build_props() -> void:
	decals = TraceDecals.new()
	decals.name = "TraceDecals"
	decals.host = self
	decals.z_index = 1
	add_child(decals)
	move_child(decals, $ObjectsAndPeople.get_index())
	var tube := $ObjectsAndPeople/CopperTransmitter as Sprite2D
	if ResourceLoader.exists(ART + "speaking_tube.png"):
		tube.texture = load(ART + "speaking_tube.png")
		tube.offset = Vector2(-14, -44)
	for spec in Content.PROPS:
		if not ResourceLoader.exists(ART + str(spec.texture)): continue
		var sprite := Sprite2D.new()
		sprite.name = str(spec.node)
		sprite.texture = load(ART + str(spec.texture))
		var visual := Vector2(float(spec.x), float(spec.y))
		sprite.position = Vector2(visual.x, float(spec.z_y))
		sprite.offset = Vector2(-sprite.texture.get_width() / 2.0, visual.y - float(spec.z_y) - sprite.texture.get_height())
		sprite.centered = false
		$ObjectsAndPeople.add_child(sprite)

func _build_sites() -> void:
	for id in Content.SITES:
		var site: Dictionary = Content.SITES[id].duplicate()
		sites[id] = site
	for id in Content.LIFE_TRACES:
		sites[id] = {"trace": true, "point": Content.LIFE_TRACES[id].point, "reach": Content.LIFE_TRACES[id].reach, "title": Content.LIFE_TRACES[id].title}
	for id in Content.EXTRA_SITES:
		sites[id] = Content.EXTRA_SITES[id].duplicate()

# ======================================================================= UI ==

func _build_ui() -> void:
	ui = $FieldUI
	hud_title = $FieldUI/Title
	hud_hint = $FieldUI/Hint
	hud_keys = $FieldUI/Keys
	hover_tip = $FieldUI/HoverTip
	aside = $FieldUI/Aside
	modal_layer = $ModalLayer
	stage_node = ReplayStage.new()
	stage_node.name = "ReplayStage"
	stage_node.host = self
	stage_node.z_index = 60
	add_child(stage_node)
	_refresh_hud()

func _ui_content(template: String, parent: Node) -> Control:
	var view: Control = UI_SCENES[template].instantiate()
	parent.add_child(view)
	return view

func _ui_action(button: Button, action: Callable) -> void:
	button.pressed.connect(func(): Sound.sfx("ui_click", -4.0))
	button.pressed.connect(action)

func _refresh_hud() -> void:
	if not is_instance_valid(hud_title):
		return
	hud_title.text = Content.text("界面·地点前缀") + " " + _room_name(player.position)
	var keys := []
	if Content.STAGES[stage].examine:
		keys.append(Content.text("界面·键位·检视"))
	if Content.STAGES[stage].notebook:
		keys.append(Content.text("界面·键位·笔记本"))
	if replay_open:
		keys.append(Content.text("界面·键位·重演"))
	hud_keys.text = "　　".join(keys)

## 脚下是哪个房间：区域表写在 chapter_01.story 的“房间区域”。
func _region_at(p: Vector2) -> Dictionary:
	for region in Content.ROOM_REGIONS:
		var rect: Array = region.rect
		if (rect[0] == null or p.x >= float(rect[0])) and (rect[1] == null or p.y >= float(rect[1])) \
				and (rect[2] == null or p.x < float(rect[2])) and (rect[3] == null or p.y < float(rect[3])):
			return region
	return {"name": "", "region": ""}

func _room_name(p: Vector2) -> String:
	return str(_region_at(p).name)

func sight_origin() -> Vector2: return player.position + Vector2(0, -8)
func sight_entry_origin() -> Vector2: return player.position
func sight_size() -> Vector2i: return Vector2i(layout.rows[0].length(), layout.rows.size())
func sight_interior(cell: Vector2i) -> bool:
	if cell.y < 0 or cell.y >= layout.rows.size():
		return false
	var row: String = layout.rows[cell.y]
	return cell.x >= 0 and cell.x < row.length() and row.substr(cell.x, 1) == "."

func sight_region(cell: Vector2i) -> String:
	# 同名的房间（两间空房）在区域表里各有区域名，各自揭示。
	return str(_region_at(Vector2(cell * 32) + Vector2(16, 16)).region)

func in_sight(point: Vector2) -> bool: return sight == null or sight.can_see(point)

func field_active() -> bool:
	return Content.STAGES[stage].walk and modal_kind == "" and not dialogue.active and not story_lock and not ended

func site_visible(id: String) -> bool:
	if not sites.has(id):
		return false
	var site: Dictionary = sites[id]
	if not engine.SiteVisible(stage, id, _room_key(player.position), _room_key(site_point(id))):
		return false
	if site.has("person"):
		return actor_of(site.person).visible
	return in_sight(site_point(id))

func site_is_new(id: String) -> bool:
	var site: Dictionary = sites[id]
	if site.has("person"):
		return false
	if site.has("trace"):
		return not examined.has(id)
	if site.has("board"):
		return not flags.has("board_read")
	return not examined.has(site.evidence)

func site_point(id: String) -> Vector2:
	var site: Dictionary = sites[id]
	if site.has("person"):
		return actor_of(site.person).position
	return site.point

func site_marker(id: String) -> Vector2:
	var site: Dictionary = sites[id]
	if site.has("person"):
		return actor_of(site.person).position + Vector2(0, -50)
	return site.point + Vector2(0, -14)

func site_box(id: String) -> Rect2:
	var site: Dictionary = sites[id]
	if site.has("person"):
		return Rect2(actor_of(site.person).position + Vector2(-12, -46), Vector2(24, 48))
	if Content.SITE_BOXES.has(id): return Content.SITE_BOXES[id]
	if id.begins_with("E11"): return Rect2(site.point + Vector2(-22, -48), Vector2(44, 62))
	return Rect2(site.point + Vector2(-14, -22), Vector2(28, 26))

# ================================================================ movement ===

func _physics_process(delta: float) -> void:
	if _closing: return
	if not is_instance_valid(player):
		return
	if follow_camera and camera:
		camera.position = Vector2(clampf(player.position.x, 320, 960), clampf(player.position.y - 24, 248, 456)).round()
	_update_hand_invitations(delta)
	_make_way()
	_pick_nearest()
	_check_triggers()
	if not field_active():
		_cancel_click_navigation()
		if not player.travelling:
			player.velocity = Vector2.ZERO
		return
	var direction := Vector2(
		float(Input.is_physical_key_pressed(KEY_D) or Input.is_physical_key_pressed(KEY_RIGHT)) - float(Input.is_physical_key_pressed(KEY_A) or Input.is_physical_key_pressed(KEY_LEFT)),
		float(Input.is_physical_key_pressed(KEY_S) or Input.is_physical_key_pressed(KEY_DOWN)) - float(Input.is_physical_key_pressed(KEY_W) or Input.is_physical_key_pressed(KEY_UP)))
	var movement_scale := 1.0
	if direction.length_squared() > 0:
		_cancel_click_navigation()
	if player.seated and direction.length_squared() > 0:
		_leave_bed()
	if direction == Vector2.ZERO and click_interaction_id != "":
		var target_id := click_interaction_id
		if player.position.distance_to(site_point(target_id)) <= float(sites[target_id].reach) + 6.0:
			_complete_click_interaction(target_id)
			return
	if direction == Vector2.ZERO and not click_path.is_empty():
		click_stall_seconds = click_stall_seconds + delta if player.position.distance_to(click_last_position) < 0.25 else 0.0
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
				if player.position.distance_to(site_point(target_id)) <= float(sites[target_id].reach) + 8.0:
					_complete_click_interaction(target_id)
					return
		else:
			var remaining := click_path[click_waypoint] - player.position
			direction = remaining.normalized()
			movement_scale = minf(1.0, remaining.length() / maxf(player.speed * delta, 0.00001))
	if not player.seated:
		player.move_input(direction.normalized() * movement_scale)

func _process(_delta: float) -> void:
	if _closing: return
	# 视线外的同伴不显示：他在做什么，侦探看不见（回放的墨影是假设，不受此限）。
	for key in ["huo", "shen", "su"]:
		actor_of(key).visible = in_sight(actor_of(key).position)
	for node in $ObjectsAndPeople.get_children():
		if node is Sprite2D:
			node.self_modulate.a = 1.0 if sight.terrain_known(node.position) else 0.0
	if hud_hint:
		hud_hint.visible = field_active()
		if field_active():
			var focus := hovered if hovered != "" else nearest
			hud_hint.text = "" if focus == "" else str(sites[focus].title) + ("　［E］" if focus == nearest else "")
	hovered = ""
	if field_active():
		var mouse := get_global_mouse_position()
		var best := INF
		for id in sites:
			if not site_visible(id):
				continue
			var box := site_box(id).grow(3)
			if box.has_point(mouse):
				var distance := box.get_center().distance_squared_to(mouse)
				if distance < best:
					best = distance
					hovered = id
	if is_instance_valid(hover_tip):
		hover_tip.visible = hovered != "" and field_active()
		if hover_tip.visible:
			hover_tip.text = str(sites[hovered].title)
			hover_tip.reset_size()
			var at := get_viewport().get_mouse_position() + Vector2(16, -34)
			hover_tip.position = Vector2(clampf(at.x, 8, 1272 - hover_tip.size.x), clampf(at.y, 64, 960))
	if is_instance_valid(aside) and aside.visible and Time.get_ticks_msec() > aside_until:
		aside.hide()
	if Engine.get_process_frames() % 20 == 0:
		_refresh_hud()

func _pick_nearest() -> void:
	nearest = ""
	if not field_active():
		return
	var best := INF
	for id in sites:
		if not site_visible(id):
			continue
		var distance := player.position.distance_to(site_point(id))
		if distance <= float(sites[id].reach) and distance < best:
			best = distance
			nearest = id

func _input(event: InputEvent) -> void:
	if not field_active():
		return
	if event is not InputEventMouseButton or not event.pressed or event.button_index != MOUSE_BUTTON_LEFT:
		return
	if event.position.y < HUD_BAR_HEIGHT:
		get_viewport().set_input_as_handled()
		return
	click_blocked_repaths = 0
	click_progress_anchor = player.position
	_cancel_click_navigation()
	var destination: Vector2 = get_global_transform_with_canvas().affine_inverse() * event.position
	var site_id := _clicked_site(destination)
	if site_id == "":
		_furniture_aside(destination)
		_move_to_click(destination)
	else:
		_approach_site(site_id)
	get_viewport().set_input_as_handled()

func _unhandled_input(event: InputEvent) -> void:
	if event is not InputEventKey or not event.pressed or event.echo:
		return
	if ended:
		if event.keycode == KEY_R:
			begin_shutdown()
			get_tree().reload_current_scene()
		elif event.keycode == KEY_C and flags.has("door_open"):
			begin_shutdown()
			get_tree().change_scene_to_file("res://scenes/Chapter2/Game/Chapter2Game.tscn")
		return
	if modal_kind == "card":
		# 任意键返回现场。
		close_modal()
		get_viewport().set_input_as_handled()
		return
	if modal_kind != "":
		if event.keycode == KEY_ESCAPE or (event.keycode == KEY_TAB and modal_kind == "notebook") or (event.keycode == KEY_R and modal_kind == "replay"):
			close_modal()
			get_viewport().set_input_as_handled()
		return
	if not field_active():
		return
	if event.keycode in [KEY_E, KEY_SPACE, KEY_ENTER] and nearest != "":
		# 对话刚结束时多按的一下，只是在推进对白，不当作新的交互。
		if Time.get_ticks_msec() - dialogue_closed_ms < 600:
			get_viewport().set_input_as_handled()
			return
		_complete_click_interaction(nearest)
		get_viewport().set_input_as_handled()
	elif event.keycode == KEY_TAB and Content.STAGES[stage].notebook:
		open_notebook()
		get_viewport().set_input_as_handled()
	elif event.keycode == KEY_R and replay_open:
		open_manuscript()
		get_viewport().set_input_as_handled()

## 普通家具不是证物：点到时只给一句极短的心声，同一件只说一次，不打断移动。
func _furniture_aside(pos: Vector2) -> void:
	for node in $ObjectsAndPeople.get_children():
		if node is not Sprite2D or furniture_spoken.has(node.name):
			continue
		var key := ""
		for prefix in Content.FURNITURE_ASIDES:
			if str(node.name).begins_with(prefix) and prefix.length() > key.length():
				key = prefix
		if key == "":
			continue
		var sprite := node as Sprite2D
		if sprite.texture == null:
			continue
		var rect := Rect2(sprite.position + sprite.offset, sprite.texture.get_size())
		if rect.has_point(pos):
			furniture_spoken[node.name] = true
			show_aside(Content.FURNITURE_ASIDES[key])
			return
	# 画在墙上、地上的东西（窗、拖鞋、地毯）不是节点：地图生成时记下区域，按区域给一句心声。
	for index in layout.asides.size():
		var decor: Array = layout.asides[index]
		var area := Rect2(decor[0][0], decor[0][1], decor[0][2], decor[0][3])
		if area.has_point(pos) and not furniture_spoken.has("decor%d" % index):
			furniture_spoken["decor%d" % index] = true
			show_aside(decor[1])
			return

func show_aside(text: String) -> void:
	aside.text = text
	aside.show()
	aside_until = Time.get_ticks_msec() + 2600

func _clicked_site(pos: Vector2) -> String:
	var chosen := ""
	var best := INF
	for id in sites:
		if not site_visible(id):
			continue
		var box := site_box(id).grow(4)
		if box.has_point(pos):
			var distance := box.get_center().distance_squared_to(pos)
			if distance < best:
				best = distance
				chosen = id
	return chosen

func _cancel_click_navigation() -> void:
	click_path.clear()
	click_waypoint = 0
	click_interaction_id = ""
	click_last_position = Vector2(INF, INF)
	click_stall_seconds = 0.0

func _complete_click_interaction(id: String) -> void:
	_cancel_click_navigation()
	player.move_input(Vector2.ZERO)
	interact(id)

func _leave_bed() -> void:
	player.position = $WakePosition.position
	player.stand_up()

func _move_to_click(destination: Vector2) -> void:
	_cancel_click_navigation()
	if not _walkable(destination):
		return
	if player.seated:
		_leave_bed()
	if player.position.distance_to(destination) <= CLICK_STOP_RADIUS:
		return
	var route := _route(player.position, destination)
	if route.is_empty():
		return
	click_path = route
	click_path.append(destination)
	click_waypoint = 0

func _approach_site(id: String) -> void:
	_cancel_click_navigation()
	var point := site_point(id)
	var reach := float(sites[id].reach)
	# 与行走中“到了就打开”的判定一致：站在够得着的地方，再点一次就直接打开，不绕到别处。
	if player.position.distance_to(point) <= reach + 6.0:
		_complete_click_interaction(id)
		return
	if player.seated:
		_leave_bed()
	var best_path := PackedVector2Array()
	var best_destination := Vector2.ZERO
	var best_cost := INF
	var grid := FieldNavigation.build_route_grid(floors, _navigation_obstacles(player), Vector2i(layout.rows[0].length(), layout.rows.size()))
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
	if not floors.has(Vector2i(point / 32.0)):
		return false
	for rect in obstacle_rects + _closed_room_rects(player):
		if rect.grow(8.0).has_point(point):
			return false
	return true

func can_access_dorm(_owner: String) -> bool:
	return Content.STAGES[stage].dorms

func _closed_room_rects(walker: CharacterBody2D) -> Array[Rect2]:
	var rects: Array[Rect2] = []
	for door in $ObjectsAndPeople.get_children():
		if not door.has_method("access_allowed") or door.access_allowed():
			continue
		if is_instance_valid(walker) and door.owner_actor() == walker:
			continue
		rects.append(door.threshold_collision_rect())
		rects.append(door.interior_bounds)
	return rects

func _navigation_obstacles(walker: CharacterBody2D) -> Array[Rect2]:
	var obstacles := obstacle_rects.duplicate()
	obstacles.append_array(_closed_room_rects(walker))
	return obstacles

func _route(from: Vector2, to: Vector2, walker: CharacterBody2D = null) -> PackedVector2Array:
	var who := walker if walker else player
	var grid := FieldNavigation.build_route_grid(floors, _navigation_obstacles(who), Vector2i(layout.rows[0].length(), layout.rows.size()))
	return FieldNavigation.route_on_grid(from, to, grid)

## 回放墨影的走位：只避家具与墙，不受宿舍门与人物阻挡；网格只建一次。
var _ink_grid: AStarGrid2D

func ink_route(from: Vector2, to: Vector2) -> PackedVector2Array:
	if _ink_grid == null:
		_ink_grid = FieldNavigation.build_route_grid(floors, obstacle_rects, Vector2i(layout.rows[0].length(), layout.rows.size()))
	var route := FieldNavigation.route_on_grid(from, to, _ink_grid)
	if route.is_empty():
		return PackedVector2Array([to])
	route.append(to)
	return route

## 把某人走到某处（场面调度用）；走不通就直接放到那里。
func walk_actor(key: String, point: Vector2, speed := 150.0) -> void:
	if _closing: return
	var actor := actor_of(key)
	if actor.seated:
		actor.stand_up()
	var route := _route(actor.position, point, actor)
	if route.is_empty() or actor.position.distance_to(point) < 4:
		actor.position = point
		return
	route.append(point)
	var previous: float = actor.speed
	actor.speed = speed
	await _follow(actor, route)
	if _closing: return
	actor.speed = previous

## 沿路走；被家具的碰撞卡住（导航网格与碰撞体略有出入）时，最后几步直接到位，不让人原地踏步。
func _follow(actor: CharacterBody2D, route: PackedVector2Array) -> void:
	if _closing: return
	actor.follow_path(route)
	var last := actor.position
	var still := 0
	while actor.travelling:
		await _wait_signal(get_tree().physics_frame)
		if _closing: return
		if actor.position.distance_to(last) < 0.2:
			still += 1
			if still > 20:
				actor.position = route[route.size() - 1]
				actor.travelling = false
				actor.velocity = Vector2.ZERO
				actor.pose("idle")
				actor.arrived.emit()
				break
		else:
			still = 0
		last = actor.position

# ================================================================ triggers ===

func _check_triggers() -> void:
	if story_lock or dialogue.active or modal_kind != "":
		return
	var room := _entered_room(player.position)
	if room == "":
		return
	var trigger := str(engine.RoomTrigger(room))
	if trigger == "meeting":
		_meeting()
		return
	if trigger == "room" and room != current_room:
		current_room = room
		_on_room_changed(room)
		engine.RoomEntry(room)

var visited_rooms: Dictionary:
	get: return engine.Progress("visited_rooms")
	set(value): engine.ReplaceProgress("visited_rooms", value)

func _room_key(p: Vector2) -> String:
	var name := _room_name(p)
	for key in Content.ROOM_NAMES:
		if Content.ROOM_NAMES[key] == name:
			return key
	return ""

## 剧情到场按脚下真实室内地板判定，门槛与墙不算进入。
func _entered_room(point: Vector2) -> String:
	if not sight_interior(Vector2i(point / 32.0)):
		return ""
	return _room_key(point)

# ================================================================ dialogue ===

func say(title: String) -> void:
	if _closing: return
	if dialogue.active:
		await _wait_signal(dialogue.finished)
		if _closing: return
	_cancel_click_navigation()
	story_lock = true
	dialogue.panel.show()
	dialogue.start_packet(engine, title, self)
	await _wait_signal(dialogue.finished)
	if _closing: return
	dialogue.panel.hide()
	story_lock = false
	dialogue_closed_ms = Time.get_ticks_msec()
	_refresh_hud()

## C# plans choose chapter events; the host executes their spatial and UI presentations.
func _play_plan(key: int) -> void:
	if _closing: return
	while true:
		var event := str(engine.NextMeeting(key))
		if event == "": break
		if event.begins_with("@"):
			var parts := event.substr(1).split(":")
			var args: Array = []
			for i in range(1, parts.size()): args.append(parts[i])
			await present_dialogue_command(parts[0], args)
			if _closing: return
		else:
			await say(event)
			if _closing: return
		engine.Presented(key)

func present_dialogue_command(kind: String, args: Array) -> void:
	if _closing: return
	match kind:
		"card":
			await open_card(str(args[0]))
			if _closing: return
		"talk":
			await talk(str(args[0]))
			if _closing: return
		"field_board": open_board()
		"trace": open_trace(str(args[0]))
		"tube": open_tube_menu()
		"experiment":
			await _on_experiment(str(args[0]))
			if _closing: return
		"help":
			await _on_help(str(args[0]))
			if _closing: return
		"recall":
			await _on_recall(str(args[0]))
			if _closing: return
		"compare":
			await _compare_hand(str(args[0]))
			if _closing: return
		"close": close_modal()
		"name":
			await _ask_name()
			if _closing: return
		"board":
			await _present_board()
			if _closing: return
		"rest":
			await _rest()
			if _closing: return
		"posts":
			for who in Content.POSTS: walk_actor(who, Content.POSTS[who])
		"summon":
			await _summon(str(args[0]))
			if _closing: return
		"consider": call_deferred("_consider", str(args[0]))
		"finish":
			await _finish_reach(str(args[0]))
			if _closing: return
		"sound":
			await sound_test()
			if _closing: return
		"aside": show_aside(":".join(args))
		"music": Sound.music(str(args[0]), float(args[1]) if args.size() > 1 else 2.5)
		"sfx": Sound.sfx(str(args[0]), float(args[1]) if args.size() > 1 else -4.0)
		"lock": story_lock = true
		"unlock": story_lock = false
		"hud": _refresh_hud()
		"answerpick":
			var pick: Dictionary = await _answer_pick()
			if _closing: return
			engine.ReplaceProgress("answer_pick", pick)
		"finale":
			await finale()
			if _closing: return
		"finale_prepare":
			story_lock = true
			follow_camera = false
			player.position = Content.STAGING["终局站位·侦探"]
			player.pose("idle", "left")
			camera.position = Content.STAGING["终局镜头"]
			_refresh_hud()
		"door_open":
			Sound.sfx("door_unbar")
			Sound.music("main_theme", 4.0)
			var door := $ObjectsAndPeople/MechanicalIsolationDoor
			for node in ["DoorFace", "LoungeCover", "LoungeCoverInner"]: door.get_node(node).hide()
			door.get_node("DoorGap").show()
			var prints := door.get_node_or_null("DemonPrints")
			if prints: prints.show()
		"redraw": decals.queue_redraw()
		"su_approach":
			await walk_actor("su", player.position + Vector2(34, 6))
			if _closing: return
		"save": _save_chapter_end(str(args[0]) == "true")
		"ending": _ending_card(str(args[0]))
		_: push_error("Unknown chapter presentation: " + kind)

func _localized_speaker(canonical: String) -> String:
	match canonical:
		"侦探": return detective_name
		"侦探（心声）": return detective_name + "（心声）"
	return canonical

func dialogue_has_modal_input() -> bool:
	return modal_kind == "name"

func heard_where(who: String) -> String:
	for h in last_sound:
		if h.who == who: return str(Content.ROOM_NAMES.get(h.room, h.room))
	return ""

# ================================================================= meeting ===

func _meeting() -> void:
	if _closing: return
	stage = Content.STAGE_EVENTS["相遇"]
	story_lock = true
	_cancel_click_navigation()
	player.move_input(Vector2.ZERO)
	var spots := {"huo": $MeetingSpots/Engineer.position, "shen": $MeetingSpots/Chef.position, "su": $MeetingSpots/Musician.position}
	for key in spots:
		actor_of(key).position = spots[key]
	actor_of("huo").pose("idle", "right")
	actor_of("shen").pose("idle", "back")
	actor_of("su").pose("idle", "left")
	await walk_actor("detective", $MeetingSpots/Detective.position, 120.0)
	if _closing: return
	player.pose("idle", "front")
	await _play_plan(engine.BeginMeeting())
	if _closing: return

var _name_field: LineEdit

func _ask_name() -> void:
	if _closing: return
	var body := open_modal(Content.text("报名·标题"), Vector2(560, 300), "name")
	var view := _ui_content("NameEntry", body)
	view.get_node("Prompt").text = Content.text("报名·提示")
	_name_field = view.get_node("Name")
	_name_field.placeholder_text = Content.text("报名·占位")
	_modal_result = null
	_name_field.text_submitted.connect(func(_t: String) -> void: _confirm_name())
	add_footer([[Content.text("报名·确认"), _confirm_name]])
	_name_field.grab_focus.call_deferred()
	while _modal_result == null:
		await _wait_signal(get_tree().process_frame)
		if _closing: return

func _confirm_name() -> void:
	var text := _name_field.text.strip_edges()
	detective_name = text if not text.is_empty() else Content.text("报名·默认名")
	_modal_choose(true)

# =================================================================== modal ===

func open_modal(title: String, size: Vector2, kind: String) -> VBoxContainer:
	close_modal()
	modal_kind = kind
	_cancel_click_navigation()
	if kind != "ending":
		Sound.sfx("page", -6.0)
	modal_root = Paper.modal(modal_layer, title, size)
	return modal_root.get_meta("body")

func add_footer(actions: Array) -> HBoxContainer:
	var column: VBoxContainer = modal_root.get_meta("column")
	column.add_child(Paper.rule())
	var footer := _ui_content("Footer", column) as HBoxContainer
	for action in actions:
		var b := Paper.button(action[0], action[1])
		if action.size() > 2 and action[2]:
			b.disabled = true
		footer.add_child(b)
	return footer

func close_modal() -> void:
	if is_instance_valid(modal_root):
		modal_root.queue_free()
	modal_root = null
	if modal_kind == "replay" and is_instance_valid(manuscript):
		manuscript = null
	modal_kind = ""

# ==================================================================== cards ===

func interact(id: String) -> void:
	if _closing: return
	if not sites.has(id):
		return
	await _present_field_packet(engine.Interact(id))
	if _closing: return

func _present_field_packet(packet: Dictionary) -> void:
	if _closing: return
	for command in packet.get("commands", []):
		await present_dialogue_command(str(command.kind), command.args)
		if _closing: return

func _geometric_card_people() -> Array:
	var people := []
	for who in ["detective", "huo", "shen", "su"]:
		engine.Locate(who, _room_key(actor_of(who).position))
		if _can_compare_hand(who): people.append(who)
	return people

func _choose_card(choice_id: String) -> void:
	if _closing: return
	# Recheck real shelf positions and travelling state at execution time.
	await _present_field_packet(engine.ChooseCard(choice_id, _geometric_card_people()))
	if _closing: return

## R-06、R-07 的生活痕迹：只看，不编号，不进重演对照。
func open_trace(id: String) -> void:
	engine.SetProgress("examined", id, true)
	var trace: Dictionary = engine.LifeTrace(id)
	var body := open_modal(trace.title, Vector2(820, 460), "card")
	var view := _ui_content("TraceCard", body)
	var picture := view.get_node("Row/Picture") as TextureRect
	var picture_path := ART + str(trace.picture) + ".png"
	picture.visible = ResourceLoader.exists(picture_path)
	if picture.visible:
		picture.texture = load(picture_path)
	view.get_node("Row/Texts/Facts").text = str(trace.facts)
	view.get_node("Row/Texts/Source").text = "来源：" + str(trace.room)
	add_footer([["返回现场", close_modal]])

func examine(id: String) -> void:
	for event in engine.ExamineContent(id):
		match str(event):
			"evidence": Sound.sfx("evidence", -3.0)
			"unlock": call_deferred("_unlock_replay")
			"v6": call_deferred("_v6_doubt")

func _v6_doubt() -> void:
	if _closing: return
	while story_lock or dialogue.active:
		await _wait_signal(get_tree().process_frame)
		if _closing: return
	await say("v6_doubt")
	if _closing: return

func open_card(id: String, brief := false) -> void:
	if _closing: return
	examine(id)
	if engine.NudgeCard(id):
		# 恶魔在玩家第一次面对这件物件的回忆选项之前说一句，朝向此人的欲望；每件只说一次。
		close_modal()
		await say("nudge_" + id)
		if _closing: return
	var packet: Dictionary = engine.CardPacket(id, _geometric_card_people())
	var card: Dictionary = packet.card
	var body := open_modal(card.title, Vector2(900, 660 if not brief else 420), "card")
	if brief:
		# 请人看过之后，只给新得到的那一句，不重显整张原文。
		for note in card.notes:
			body.add_child(Paper.label(note, 19, Paper.SEAL if note.begins_with("回忆") else Paper.INK))
		add_footer([["看整张证物卡", open_card.bind(id)], ["返回现场", close_modal]])
		return
	var view := _ui_content("EvidenceCard", body)
	var picture := view.get_node("Row/Picture") as TextureRect
	var picture_path := ART + "card_" + str(card.picture) + ".png"
	picture.visible = ResourceLoader.exists(picture_path)
	if picture.visible:
		picture.texture = load(picture_path)
	view.get_node("Row/Texts/Facts").text = str(card.facts)
	view.get_node("Row/Texts/Source").text = "来源：" + str(card.source)
	var notes: Array = card.notes
	if not notes.is_empty():
		body.add_child(Paper.rule())
		for note in notes:
			body.add_child(Paper.label(note, 18, Paper.SEAL if note.begins_with("回忆") else Paper.INK))
	var comparisons: VBoxContainer = null
	if id == "E11": comparisons = _ui_content("Comparisons", body) as VBoxContainer
	var footer := add_footer([])
	for choice in packet.choices:
		var button := Paper.button(str(choice.label), _choose_card.bind(str(choice.id)))
		button.disabled = not bool(choice.is_available)
		button.tooltip_text = str(choice.lock_hint)
		if comparisons != null and str(choice.id).contains(":compare:"):
			comparisons.add_child(button)
		else:
			footer.add_child(button)

func _on_experiment(id: String) -> void:
	if _closing: return
	close_modal()
	if id == "E11":
		await _compare_hand("detective")
		if _closing: return
		return
	var who := ""
	if id == "E3":
		who = await _pick_sole()
		if _closing: return
	await _play_plan(engine.BeginExperiment(id, who))
	if _closing: return
	open_card(id)

func _at_hand_shelf(actor: CharacterBody2D) -> bool:
	return _room_key(actor.position) == "R05" and actor.position.distance_to(site_point("E11a")) <= 85.0

func _hand_busy(who: String) -> bool:
	return actor_of(who).travelling or str(life.get(who, {}).get("state", "")) in ["going", "holding"]

func _can_compare_hand(who: String) -> bool:
	if not _at_hand_shelf(player): return false
	if who == "detective": return true
	var actor := actor_of(who)
	return _at_hand_shelf(actor) and actor.visible and in_sight(actor.position) and not _hand_busy(who)

func _compare_hand(who: String) -> void:
	if _closing: return
	close_modal()
	if not _can_compare_hand(who):
		show_aside(Content.text("提示·比对站位"))
		open_card("E11")
		return
	engine.Locate("detective", _room_key(player.position))
	engine.Locate(who, _room_key(actor_of(who).position))
	await _play_plan(engine.BeginExperiment("E11", who))
	if _closing: return
	open_card("E11")

func _invite_hand(who: String) -> void:
	var actor := actor_of(who)
	if not examined.has("E11") or not actor.visible or not in_sight(actor.position) or player.position.distance_to(actor.position) > 56.0:
		show_aside(Content.text("旁白·邀请距离"))
		return
	if _hand_busy(who) or hand_invitations.has(who):
		show_aside(Content.text("旁白·正忙", {"人名": Content.PERSON_NAMES[who]}))
		return
	if _at_hand_shelf(actor):
		engine.Locate(who, _room_key(actor.position))
		show_aside(Content.text("旁白·已在架沿", {"人名": Content.PERSON_NAMES[who]}))
		return
	var obstacles := _navigation_obstacles(actor)
	var grid := FieldNavigation.build_route_grid(floors, obstacles, Vector2i(layout.rows[0].length(), layout.rows.size()))
	var route := PackedVector2Array()
	var destination := Vector2.ZERO
	for offset in [Vector2(0, 40), Vector2(-32, 40), Vector2(32, 40), Vector2(-48, 56), Vector2(0, 72), Vector2(48, 56)]:
		var spot: Vector2 = site_point("E11a") + offset
		if not FieldNavigation.walkable(spot, floors, obstacles): continue
		var taken := player.position.distance_to(spot) < 24.0
		for other in actors.values():
			if other != actor and other.position.distance_to(spot) < 28.0: taken = true
		for invitation in hand_invitations.values():
			if invitation.destination.distance_to(spot) < 28.0: taken = true
		if taken: continue
		var candidate := FieldNavigation.route_on_grid(actor.position, spot, grid)
		if candidate.is_empty(): continue
		route = candidate
		destination = spot
		break
	if route.is_empty():
		show_aside(Content.text("旁白·路不通", {"人名": Content.PERSON_NAMES[who]}))
		return
	if actor.seated: actor.stand_up()
	route.append(destination)
	var room := _room_key(actor.position)
	engine.Locate(who, room)
	hand_invitations[who] = {"destination": destination, "last": actor.position, "still": 0.0, "room": room}
	actor.follow_path(route)
	show_aside(Content.text("旁白·正赶去", {"人名": Content.PERSON_NAMES[who]}))

func _update_hand_invitations(delta: float) -> void:
	for who in hand_invitations.keys():
		var actor := actor_of(who)
		var invitation: Dictionary = hand_invitations[who]
		var room := _room_key(actor.position)
		if room != str(invitation.room):
			engine.Locate(who, room)
			invitation.room = room
		if not actor.travelling:
			hand_invitations.erase(who)
			show_aside(Content.text("旁白·已到" if _at_hand_shelf(actor) else "旁白·停下", {"人名": Content.PERSON_NAMES[who]}))
			continue
		invitation.still = float(invitation.still) + delta if actor.position.distance_to(invitation.last) < 0.2 else 0.0
		invitation.last = actor.position
		if float(invitation.still) >= 1.0:
			actor.travelling = false
			actor.velocity = Vector2.ZERO
			actor.pose("idle")
			actor.arrived.emit()
			hand_invitations.erase(who)
			show_aside(Content.text("旁白·被挡", {"人名": Content.PERSON_NAMES[who]}))

func _on_help(id: String) -> void:
	if _closing: return
	close_modal()
	await _play_plan(engine.BeginHelp(id))
	if _closing: return
	open_card(id, true)

func _pick_sole() -> String:
	if _closing: return ""
	var body := open_modal(Content.text("鞋底·标题"), Vector2(620, 450), "menu")
	var view := _ui_content("SoleChoice", body)
	view.get_node("Prompt").text = Content.text("鞋底·提示")
	_modal_result = null
	var list := view.get_node("Choices") as VBoxContainer
	for key in Content.SOLE_TARGETS:
		list.add_child(Paper.button(str(Content.SOLE_TARGETS[key]), _modal_choose.bind(key)))
	add_footer([["算了", _modal_choose.bind("")]])
	while _modal_result == null:
		await _wait_signal(get_tree().process_frame)
		if _closing: return ""
	return str(_modal_result)

func _summon(who: String) -> void:
	if _closing: return
	var actor := actor_of(who)
	var guard := 0
	while str(life.get(who, {}).get("state", "")) in ["going", "holding"] and guard < 900:
		guard += 1
		await _wait_signal(get_tree().process_frame)
		if _closing: return
	if actor.position.distance_to(player.position) > 90:
		for offset in [Vector2(30, 4), Vector2(-30, 4), Vector2(0, -28), Vector2(48, -10), Vector2(-48, -10), Vector2(0, 30)]:
			var spot: Vector2 = player.position + offset
			var taken := false
			for other in actors.values():
				if other != actor and other.position.distance_to(spot) < 18:
					taken = true
			if _walkable(spot) and not taken:
				# 同伴真的走过来；路走不通才直接站到旁边。
				var route := _route(actor.position, spot, actor)
				if route.is_empty():
					actor.position = spot
				else:
					route.append(spot)
					var previous: float = actor.speed
					actor.speed = 240.0
					await _follow(actor, route)
					if _closing: return
					actor.speed = previous
				break
	actor.face_point(player.position)
	player.face_point(actor.position)

func _on_recall(id: String) -> void:
	if _closing: return
	close_modal()
	await _play_plan(engine.BeginRecall(id))
	if _closing: return
	open_card(id)

func _consider(who: String) -> void:
	if _closing: return
	if hand_invitations.has(who): return
	var intent: Dictionary = engine.Consider(who)
	if bool(intent.get("reach", false)):
		_go_reach(who, str(intent.get("thought", "")))

func _go_reach(who: String, thought: String) -> void:
	if _closing: return
	engine.BeginReach(who, thought)
	var obj: String = Content.OWN_OBJECTS[who]
	await _wait_signal(get_tree().create_timer(0.6).timeout)
	if _closing: return
	while dialogue.active and _detective_present(who):
		await _wait_signal(get_tree().process_frame)
		if _closing: return
	var actor := actor_of(who)
	var spot := _spot_near(site_point(obj), float(sites[obj].reach) - 14.0, actor)
	await walk_actor(who, spot, 110.0)
	if _closing: return
	actor.face_point(site_point(obj))
	engine.Locate(who, _room_key(actor.position))
	if not bool(engine.Reach(who)):
		return
	_show_object(obj, false)
	if _detective_present(who):
		await _present_reach(who)
		if _closing: return
	else:
		await _alone_reach(who)
		if _closing: return

func _detective_present(who: String) -> bool:
	var actor := actor_of(who)
	var room := _entered_room(player.position)
	return room != "" and room == _entered_room(actor.position) and in_sight(actor.position) and player.position.distance_to(actor.position) < 360.0

func _present_reach(who: String) -> void:
	if _closing: return
	while dialogue.active or story_lock or (modal_kind != "" and modal_kind != "card") or stage != "investigate":
		await _wait_signal(get_tree().process_frame)
		if _closing: return
	if not _detective_present(who):
		await _alone_reach(who)
		if _closing: return
		return
	close_modal()
	actor_of(who).face_point(player.position)
	player.face_point(actor_of(who).position)
	await _play_plan(engine.BeginPresentReach(who))
	if _closing: return

func _alone_reach(who: String) -> void:
	if _closing: return
	engine.PrepareAlone(who)
	await _wait_signal(get_tree().create_timer(1.2).timeout)
	if _closing: return
	engine.CompleteAlone(who)
	await _finish_reach(who)
	if _closing: return

func _finish_reach(who: String) -> void:
	if _closing: return
	var obj: String = Content.OWN_OBJECTS[who]
	engine.PutBack(who)
	_show_object(obj, true)
	if _detective_present(who):
		engine.SetDetail("life", who, "noticed", true)
	await walk_actor(who, Content.POSTS[who], 110.0)
	if _closing: return
	engine.Locate(who, _room_key(actor_of(who).position))

func _show_object(obj: String, visible_now: bool) -> void:
	var sprite := $ObjectsAndPeople.get_node_or_null(Content.OBJECT_SPRITES.get(obj, ""))
	if sprite == null:
		return
	sprite.visible = visible_now
	if visible_now and not sprite.has_meta("moved"):
		# 放回去了，摆得和原来不一样。
		sprite.set_meta("moved", true)
		sprite.position.x += 3

func _spot_near(point: Vector2, radius: float, walker: CharacterBody2D) -> Vector2:
	var best := walker.position
	var best_cost := INF
	for fraction in [1.0, 0.7]:
		for heading in APPROACH_DIRECTIONS:
			var candidate: Vector2 = point + heading.normalized() * radius * fraction
			if not _walkable(candidate):
				continue
			var route := _route(walker.position, candidate, walker)
			if route.is_empty():
				continue
			var cost := walker.position.distance_to(candidate)
			if cost < best_cost:
				best_cost = cost
				best = candidate
	return best

## 走进一个房间：不在场的同伴可能被恶魔低语过；碰过旧物的人，事后的神色会被看见。
func _on_room_changed(room: String) -> void:
	if _closing: return
	if room == "" or _entered_room(player.position) != room: return
	for who in ["detective", "huo", "su", "shen"]:
		engine.Locate(who, _room_key(actor_of(who).position))
	await _play_plan(engine.BeginRoomChanged(room, Time.get_ticks_msec()))
	if _closing: return

func _make_way() -> void:
	if player.velocity.length() < 10.0:
		return
	for key in ["huo", "shen", "su"]:
		var actor := actor_of(key)
		if actor.travelling or actor.position.distance_to(player.position) > 20.0:
			continue
		if str(life.get(key, {}).get("state", "")) in ["going", "holding"]:
			continue
		var side := player.velocity.normalized().orthogonal() * 28.0
		for spot in [actor.position + side, actor.position - side]:
			if _walkable(spot):
				actor.walk_to(spot)
				break

func _present_board() -> void:
	if _closing: return
	follow_camera = false
	var from := camera.position
	var tween := create_tween()
	tween.tween_property(camera, "position", Content.STAGING["公告框镜头"], 0.8)
	await _wait_signal(tween.finished)
	if _closing: return
	open_board()
	while modal_kind != "":
		await _wait_signal(get_tree().process_frame)
		if _closing: return
	var back := create_tween()
	back.tween_property(camera, "position", from, 0.5)
	await _wait_signal(back.finished)
	if _closing: return
	follow_camera = true

func open_board() -> void:
	engine.SetProgress("flags", "board_read", true)
	var body := open_modal(Content.text("公告框·标题"), Vector2(860, 620), "card")
	var view := _ui_content("Board", body)
	view.get_node("Introduction").text = Content.text("公告框·引言")
	var clauses := view.get_node("Clauses") as VBoxContainer
	for clause in engine.PostedRules():
		clauses.add_child(Paper.label(str(clause), 19))
	add_footer([["返回现场", close_modal]])

func _unlock_replay() -> void:
	if _closing: return
	if not engine.UnlockReplay(): return
	while story_lock or dialogue.active or modal_kind != "":
		await _wait_signal(get_tree().process_frame)
		if _closing: return
	await say("replay_unlock")
	if _closing: return
	_refresh_hud()

func open_notebook() -> void:
	var body := open_modal("笔记本", Vector2(1000, 780), "notebook")
	var view := _ui_content("Notebook", body)
	view.get_node("Empty").text = Content.text("笔记·空")
	view.get_node("Empty").visible = examined.is_empty()
	for place in Content.PLACES:
		var ids := []
		for id in Content.EVIDENCE:
			if examined.has(id) and Content.EVIDENCE[id].place == place:
				ids.append(id)
		if ids.is_empty():
			continue
		var section := _ui_content("NotebookSection", view.get_node("Groups"))
		section.get_node("Place").text = "[b]" + place + "[/b]"
		var grid := section.get_node("Grid") as GridContainer
		for id in ids:
			var mark := ""
			if experiments.has(id) or helped.has(id):
				mark = "　·" + Content.text("笔记·已试")
			grid.add_child(Paper.button(str(Content.EVIDENCE[id].name) + mark, open_card.bind(id), 18))
	view.get_node("Groups").visible = view.get_node("Groups").get_child_count() > 0
	view.get_node("Others").visible = flags.has("board_read")
	view.get_node("Others/Board").text = Content.text("笔记·公告框")
	_ui_action(view.get_node("Others/Board"), open_board)
	view.get_node("Recalls/Heading").text = Content.text("笔记·回忆所得")
	view.get_node("Recalls").visible = not recalls.is_empty()
	for who in recalls:
		view.get_node("Recalls/Entries").add_child(Paper.label("%s：%s" % [Content.PERSON_NAMES[who], recalls[who]], 18))
	var footer := []
	if replay_open:
		footer.append(["重演手稿（R）", open_manuscript])
	footer.append(["合上（Tab）", close_modal])
	add_footer(footer)


# ================================================================== replay ===

func open_manuscript() -> void:
	if not replay_open:
		return
	open_modal("", Vector2(1200, 900), "replay")
	var column: VBoxContainer = modal_root.get_meta("column")
	for child in column.get_children():
		child.queue_free()
	manuscript = ReplayManuscript.instantiate()
	manuscript.host = self
	manuscript.size_flags_vertical = Control.SIZE_EXPAND_FILL
	column.add_child(manuscript)

## 重演手稿（两章共用组件）向宿主取的内容。
func replay_title() -> String: return Content.text("重演·标题")
func replay_hint() -> String: return Content.text("重演·提示")
## 第六个问题（昨夜之前，四人彼此……）起初不写出来：素不相识是没人说出口的默认。
## 重演里出现信任上的拒绝，或细看过两件相熟旁证之后，它才出现。
func replay_questions() -> Array:
	var count: int = Content.VARIABLES.size() if engine.V6Open() else Content.VARIABLES.size() - 1
	return Content.VARIABLES.slice(0, count)
func replay_mark(index: int) -> String:
	if recall_marks.has(index + 1):
		return "　[color=seal]%s[/color]" % Content.text("重演·回忆所证", {"人名": Content.PERSON_NAMES[recall_marks[index + 1]]})
	return ""
func replay_is_new(index: int) -> bool: return index == 5 and not flags.has("v6_seen")
func replay_opened() -> void:
	if engine.V6Open():
		engine.SetProgress("flags", "v6_seen", true)
func finding_title(finding: Dictionary) -> String:
	return Content.EVIDENCE[finding.id].name if str(finding.status) != "unvisited" else str(finding.place)
func finding_card(finding: Dictionary) -> Callable:
	return open_card.bind(str(finding.id))

func run_replay(vars: Array) -> Dictionary:
	replay_vars = vars.duplicate()
	var result: Dictionary = engine.RunReplay(vars, examined.keys())
	replay_result = result
	replay_count += 1
	return result

## 回放结束（或跳过）后回到手稿；跳过与看完的结算相同。
func play_replay(result: Dictionary) -> void:
	if _closing: return
	close_modal()
	stage = Content.STAGE_EVENTS["重演开始"]
	story_lock = true
	Sound.sfx("replay", -4.0)
	Sound.duck(true)
	await stage_node.play(result.frames)
	if _closing: return
	Sound.duck(false)
	stage = Content.STAGE_EVENTS["重演结束"]
	story_lock = false
	await after_replay(result)
	if _closing: return

func after_replay(_result: Dictionary) -> void:
	if _closing: return
	close_modal()
	await _play_plan(engine.BeginAfterReplay())
	if _closing: return
	open_manuscript()

var _sound_choice := [0, "foyer"]
var _sound_buttons := {"sentence": [], "spot": []}
var _modal_result: Variant = null

func _modal_choose(value: Variant) -> void:
	_modal_result = value
	close_modal()

func _sound_pick(kind: String, index: int, value: Variant) -> void:
	_sound_choice[0 if kind == "sentence" else 1] = value
	for k in _sound_buttons[kind].size():
		_sound_buttons[kind][k].button_pressed = k == index

func sound_test() -> void:
	if _closing: return
	var body := open_modal(Content.text("声路·标题"), Vector2(820, 600), "sound")
	var view := _ui_content("SoundExperiment", body)
	view.get_node("Introduction").text = Content.text("声路·说明")
	view.get_node("SentenceHeading").text = Content.text("声路·选句")
	view.get_node("PlaceHeading").text = Content.text("声路·选位")
	var sentences: Array = engine.Sentences()
	_sound_choice = [0, str(Content.SOUND_SPOTS.keys()[0])]
	_sound_buttons = {"sentence": [], "spot": []}
	var sentence_row := view.get_node("Sentences") as VBoxContainer
	for i in sentences.size():
		var b := Paper.button("“%s”" % sentences[i], _sound_pick.bind("sentence", i, i))
		b.toggle_mode = true
		b.button_pressed = i == 0
		_sound_buttons.sentence.append(b)
		sentence_row.add_child(b)
	var where_row := view.get_node("Places") as HBoxContainer
	var places: Array = Content.SOUND_SPOTS.keys()
	for i in places.size():
		var b := Paper.button(str(Content.SOUND_SPOTS[places[i]]), _sound_pick.bind("spot", i, places[i]))
		b.toggle_mode = true
		b.button_pressed = i == 0
		_sound_buttons.spot.append(b)
		where_row.add_child(b)
	_modal_result = null
	add_footer([["算了", _modal_choose.bind(false)], ["开始", _modal_choose.bind(true)]])
	while _modal_result == null:
		await _wait_signal(get_tree().process_frame)
		if _closing: return
	if not _modal_result:
		return
	var positions: Dictionary = engine.SoundPositions(_sound_choice[1])
	Sound.sfx("tuning_fork", -5.0)
	last_sound = engine.SoundTest(_sound_choice[0], positions)
	engine.SetProgress("experiments", "E8", true)
	engine.SetProgress("state", "sound_sentence", _sound_choice[0])
	sound_rounds += 1
	if sound_rounds == 1:
		await say("sound_test")
		if _closing: return
		open_card("E8")
	else:
		# 第二轮起不再逐人复述：一张小表汇总四处所闻。
		_sound_table()

func _sound_table() -> void:
	var body := open_modal(Content.text("声路·轮标题", {"轮": sound_rounds}), Vector2(820, 470), "card")
	var view := _ui_content("SoundResults", body)
	view.get_node("Sentence").text = Content.text("声路·铜口说", {"句": sound_sentence()})
	var grid := view.get_node("Grid") as GridContainer
	for h in last_sound:
		grid.add_child(Paper.label(heard_where(h.who), 18))
		grid.add_child(Paper.label(Content.PERSON_NAMES[h.who] if h.who != "detective" else Content.text("声路·你"), 18))
		grid.add_child(Paper.label(str(h.text), 18))
		grid.add_child(Paper.label(str(h.direction), 18))
	add_footer([["看证物卡", open_card.bind("E8")], ["返回现场", close_modal]])

func sound_sentence() -> String:
	return str(engine.Sentences()[int(engine.Progress("state").get("sound_sentence", 0))])

# ==================================================================== talk ===

func talk(who: String) -> void:
	if _closing: return
	actor_of(who).face_point(player.position)
	player.face_point(actor_of(who).position)
	engine.RemoveProgress("flags", "invite_hand_" + who)
	await _play_plan(engine.BeginTalk(who))
	if _closing: return
	if flags.has("invite_hand_" + who):
		engine.RemoveProgress("flags", "invite_hand_" + who)
		_invite_hand(who)

func open_tube_menu() -> void:
	var body := open_modal(Content.text("铜口·标题"), Vector2(620, 420), "menu")
	var view := _ui_content("TubeMenu", body)
	view.get_node("Introduction").text = Content.text("铜口·描述")
	view.get_node("Choices/Examine").text = Content.text("铜口·检视")
	view.get_node("Choices/Answer").text = Content.text("铜口·作答")
	view.get_node("Choices/Rest").text = Content.text("铜口·歇息")
	_ui_action(view.get_node("Choices/Examine"), open_card.bind("E8"))
	_ui_action(view.get_node("Choices/Answer"), _tube_answer)
	_ui_action(view.get_node("Choices/Rest"), _tube_rest)
	add_footer([["返回现场", close_modal]])

func _tube_answer() -> void:
	close_modal()
	answer()

func _tube_rest() -> void:
	close_modal()
	_confirm_rest()

func _confirm_rest() -> void:
	if _closing: return
	await _play_plan(engine.BeginConfirmRest())
	if _closing: return

func statements() -> Array:
	return engine.Statements(replay_vars, replay_count, bool(replay_result.get("detective_bolted", false)))

func answer() -> void:
	if _closing: return
	await _play_plan(engine.BeginAnswer())
	if _closing: return

var _answer_list: VBoxContainer
var _answer_choice: Dictionary = {}
var _answer_summary: RichTextLabel
var _answer_submit: Button

func _answer_pick() -> Dictionary:
	if _closing: return {}
	_modal_result = null
	_answer_choice = {"say": "", "show": ""}
	var body := open_modal(Content.text("答辩·标题"), Vector2(900, 820), "answer")
	# 题目与当前所选的陈述、出示固定在顶部；下面的列表单独滚动，不与题目叠压。
	var column: VBoxContainer = modal_root.get_meta("column")
	var header := _ui_content("AnswerHeader", column) as VBoxContainer
	column.move_child(header, 2)
	var verdict: Dictionary = Content.VERDICTS.get(str(int(answer_state.get("step", 1))), {})
	header.get_node("Question").text = Content.text("答辩·题头", {"问题": verdict.get("question", "")})
	header.get_node("Instructions").text = Content.text("答辩·说明")
	_answer_summary = header.get_node("Summary")
	_answer_list = _ui_content("AnswerList", body) as VBoxContainer
	var footer_actions := []
	footer_actions.append(["陈述", _answer_show_list.bind("say")])
	footer_actions.append(["出示", _answer_show_list.bind("show")])
	footer_actions.append(["暂且不答", _modal_choose.bind({})])
	footer_actions.append(["作答", _answer_submit_pressed])
	var footer := add_footer(footer_actions)
	_answer_submit = footer.get_child(footer.get_child_count() - 1)
	_answer_show_list("say")
	_answer_refresh()
	while _modal_result == null:
		await _wait_signal(get_tree().process_frame)
		if _closing: return {}
	return _modal_result

func _answer_refresh() -> void:
	var said := str(_answer_choice.say)
	var shown := str(_answer_choice.show)
	var line: String = Content.text("答辩·陈述行", {"内容": Content.STATEMENTS[said] if said != "" else Content.text("答辩·未选")}) + "\n"
	line += Content.text("答辩·出示行", {"内容": str(Content.EVIDENCE[shown].name) if shown != "" else Content.text("答辩·未选")})
	_answer_summary.text = line
	if is_instance_valid(_answer_submit):
		_answer_submit.disabled = shown == "" or said == ""

func _answer_select(kind: String, id: String) -> void:
	_answer_choice[kind] = id
	_answer_refresh()

func _answer_submit_pressed() -> void:
	if _answer_submit.disabled:
		return
	_modal_choose(_answer_choice.duplicate())

func _answer_show_list(kind: String) -> void:
	for child in _answer_list.get_children():
		child.queue_free()
	if kind == "say":
		var options := statements()
		if options.is_empty():
			_answer_list.add_child(Paper.label(Content.text("答辩·无陈述"), 18, Paper.MUTED))
		for key in options:
			var b := Paper.button(Content.STATEMENTS[key], _answer_select.bind("say", key), 19)
			b.toggle_mode = true
			b.button_pressed = _answer_choice.say == key
			_answer_list.add_child(b)
	else:
		if examined.is_empty():
			_answer_list.add_child(Paper.label(Content.text("答辩·无笔记"), 18, Paper.MUTED))
		for id in Content.EVIDENCE:
			if examined.has(id):
				var b := Paper.button(Content.EVIDENCE[id].name, _answer_select.bind("show", id), 19)
				b.toggle_mode = true
				b.button_pressed = _answer_choice.show == id
				_answer_list.add_child(b)

func finale() -> void:
	if _closing: return
	await _play_plan(engine.BeginFinale())
	if _closing: return

func _rest() -> void:
	if _closing: return
	await _play_plan(engine.BeginRest())
	if _closing: return

func _save_chapter_end(rested: bool) -> void:
	var data: Dictionary = engine.ChapterEnd(examined.keys(), full_match_seen, flags.has("asked_hand"), rested)
	data["detective_name"] = detective_name
	var file := FileAccess.open("user://chapter1_end.json", FileAccess.WRITE)
	if file:
		file.store_string(JSON.stringify(data, "\t"))

func _ending_card(kind: String) -> void:
	ended = true
	var body := open_modal(Content.text("结局·%s·标题" % kind), Vector2(720, 420), "ending")
	var view := _ui_content("Ending", body)
	view.get_node("Outcome").text = Content.text("结局·%s·正文" % kind)
	var hook_text := []
	for key in ["detective", "huo", "shen", "su"]:
		if int(engine.Hooks(key)) > 0:
			hook_text.append(Content.PERSON_NAMES[key])
	view.get_node("Hooks").text = Content.text("结局·钩", {"名单": Content.text("结局·无") if hook_text.is_empty() else "、".join(hook_text)})
	var kept := []
	for key in ["detective", "huo", "shen", "su"]:
		if int(engine.Hooks(key)) == 0:
			kept.append(Content.PERSON_NAMES[key] if key != "detective" else Content.text("结局·你自己"))
	view.get_node("Kept").text = Content.text("结局·未交", {"名单": Content.text("结局·无") if kept.is_empty() else "、".join(kept)})
	view.get_node("Continue").text = Content.text("结局·续章" if flags.has("door_open") else "结局·重开")
	modal_kind = "ending"
	stage = Content.STAGE_EVENTS["章末"]

# ============================================================ test support ===
## 流程跳转回归使用：直接把局面摆到某个状态，不走完整游玩。

func jump_to(state: String) -> void:
	close_modal()
	story_lock = false
	if player.seated:
		player.stand_up()
	match state:
		"wake":
			stage = "roam"
		"investigate":
			stage = "investigate"
			for key in Content.POSTS:
				actor_of(key).position = Content.POSTS[key]
			player.position = Vector2(230, 400)
		"reversal_one":
			jump_to("investigate")
			for id in ["E1", "E2", "E3"]:
				examine(id)
			replay_open = true
		"replay_match":
			jump_to("reversal_one")
			for id in Content.EVIDENCE:
				examine(id)
			engine.SetProgress("experiments", "E8", true)
			engine.SetProgress("flags", "reversal_one", true)
			run_replay([0, 0, 0, 0, 0, 1])
			full_match_seen = bool(replay_result.full_match)
		"answer_done":
			jump_to("replay_match")
			answer_state = {"step": 5, "said_trust": true, "shown_familiar": true}
			stage = "answer"
	_refresh_hud()
