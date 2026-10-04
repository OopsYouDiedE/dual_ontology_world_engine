extends Node2D
## 《安眠者有福》第一章《门的这一侧》：生活翼现场、调查、重演入口与始业之答。
## 推演在 C#：AwakeningEngine → NightReplay（Core 的 Observation / DecisionSystem / SocialSystem / SimulationSession）。
## 本脚本是宿主：地图、移动、相机、界面、对白与流程。

const Content := preload("res://scenes/Awakening/AwakeningContent.gd")
const FieldNavigation := preload("res://scenes/Awakening/FieldNavigation.gd")
const ChapterUI := preload("res://scenes/Shared/UI/ChapterUI.gd")
const Paper := preload("res://scenes/Shared/UI/Paper.gd")
const Sound := preload("res://scenes/Shared/Audio/Sound.gd")
const ReplayManuscript := preload("res://scenes/Shared/UI/ReplayManuscript.gd")
const ReplayStage := preload("res://scenes/Awakening/ReplayStage.gd")
const TraceDecals := preload("res://scenes/Awakening/TraceDecals.gd")
const EngineScript := preload("res://scenes/Awakening/AwakeningEngine.cs")
const ScriptActors := preload("res://scenes/Shared/Story/ScriptActors.gd")
## 乙区地图（tools/build_awakening_map.py 生成）：地面格、家具足迹、书架积灰、壁饰心声。
const LAYOUT: Resource = preload("res://scenes/Awakening/map_layout.tres")
var STORY: Resource
const ART := "res://ShareAsset/Internal/Props/AwakeningEvidence/"
const PORTRAITS := {
	"侦探": "res://ShareAsset/Internal/Characters/FirstAwakeningPortraits/detective.png",
	"工程师": "res://ShareAsset/Internal/Characters/FirstAwakeningPortraits/engineer.png",
	"厨师": "res://ShareAsset/Internal/Characters/FirstAwakeningPortraits/chef.png",
	"音乐家": "res://ShareAsset/Internal/Characters/FirstAwakeningPortraits/musician.png",
}

const CLICK_STOP_RADIUS := 8.0
const HUD_BAR_HEIGHT := 60.0
const CLICK_REPATH_DELAY := 0.15
const CLICK_MAX_BLOCKED_REPATHS := 4
const APPROACH_DIRECTIONS := [Vector2.UP, Vector2.DOWN, Vector2.LEFT, Vector2.RIGHT, Vector2(-1, -1), Vector2(1, -1), Vector2(-1, 1), Vector2(1, 1)]
## 调查阶段同伴各在一处。
const POSTS := {"huo": Vector2(500, 132), "shen": Vector2(470, 652), "su": Vector2(232, 352)}
## 三位同伴各自的旧物，与它在地图上的贴图节点。
const OWN_OBJECTS := {"huo": "E7", "su": "E5", "shen": "E10"}
const OBJECT_SPRITES := {"E7": "Slate", "E5": "Fork", "E10": "Jar"}
## R-06、R-07 里本人的生活痕迹（不编号的旁证）。
const LIFE_TRACES := {
	"L6": {"point": Vector2(992, 512), "reach": 52.0},
	"L7": {"point": Vector2(652, 596), "reach": 52.0},
}


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

# ------------------------------------------------------------------ story ---
var engine: Node = EngineScript.new()
var script_actors: Node
var dialogue: CanvasLayer
var chapter_ui: Node
var busy_label: Label
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
var furniture_spoken: Dictionary:
	get: return engine.Progress("furniture_spoken")
	set(value): engine.ReplaceProgress("furniture_spoken", value)
var dialogue_closed_ms := -100000

var stage: String:
	get: return engine.Progress("state")["stage"]
	set(value): engine.SetProgress("state", "stage", value)
var detective_name: String:
	get: return engine.Progress("state")["detective_name"]
	set(value): engine.SetProgress("state", "detective_name", value)
var examined: Dictionary:
	get: return engine.Progress("examined")
var experiments: Dictionary:
	get: return engine.Progress("experiments")
var helped: Dictionary:
	get: return engine.Progress("helped")
var recalls: Dictionary:
	get: return engine.Progress("recalls")
var recall_marks: Dictionary:
	get: return engine.Progress("recall_marks")
	set(value): engine.ReplaceProgress("recall_marks", value)
var nudged: Dictionary:
	get: return engine.Progress("nudged")
	set(value): engine.ReplaceProgress("nudged", value)
var flags: Dictionary:
	get: return engine.Progress("flags")
## 同伴自己去碰旧物的过程：who → {state: going/holding/done, alone, choice, thought, fragment, told, noticed}
var life: Dictionary:
	get: return engine.Progress("life")
	set(value): engine.ReplaceProgress("life", value)
## 已对旧物拿定主意的同伴（碰过或不碰），回忆按钮不再出现。
var decided: Dictionary:
	get: return engine.Progress("decided")
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

# ============================================================== lifecycle ===

func _ready() -> void:
	TranslationServer.set_locale("zh_CN")
	texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	get_window().content_scale_size = Vector2i(1280, 992)
	get_window().content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	get_window().content_scale_stretch = Window.CONTENT_SCALE_STRETCH_INTEGER
	get_window().size = Vector2i(1280, 992)
	get_window().title = "安眠者有福 · 门的这一侧"
	engine.name = "Engine"
	add_child(engine)
	STORY = DialogueManager.create_resource_from_text(engine.DialogueText())
	Content.apply_content(engine.Content())
	script_actors = ScriptActors.new()
	add_child(script_actors)
	_build_geometry()
	_build_characters()
	_build_props()
	_build_sites()
	_build_ui()
	var hints: Node2D = get_node_or_null("InteractionHints")
	if hints: hints.set("source", self)
	dialogue = $Dialogue
	chapter_ui.configure_dialogue(dialogue, PORTRAITS)
	Sound.music("door_side", 2.5)
	camera = $Camera2D
	camera.position = player.position
	camera.make_current()
	if not has_meta("skip_intro"):
		call_deferred("_begin")

func _begin() -> void:
	await _present_investigation(engine.BeginWake())

# ================================================================= geometry ==

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
	player.position = Vector2(982, 144)
	player.pose("idle", "front")
	player.sit_at("front", 4.0)
	# 醒来时同伴各在自己房里（霍铎 R-06、沈鼎 R-07、苏泠 R-08）；相遇时才到活动厅。
	actor_of("huo").position = Vector2(930, 590)
	actor_of("shen").position = Vector2(706, 590)
	actor_of("su").position = Vector2(706, 210)
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
	# [节点名, 贴图, 画面上的脚底位置, 遮挡排序用的 Y（放在桌面上的物件跟随桌子的前沿排序）]
	for spec in [["NoticeFrame", "notice_frame.png", Vector2(400, 80), 96.0], ["Slate", "slate.png", Vector2(500, 240), 272.0], ["Score", "score.png", Vector2(652, 174), 193.0],
			["Cups", "cups.png", Vector2(352, 522), 540.0], ["Jar", "jar.png", Vector2(512, 486), 540.0],
			["Fork", "fork.png", Vector2(186, 438), 438.0], ["Wedge", "wedge.png", Vector2(270, 440), 440.0],
			["OilHook", "oil_hook.png", Vector2(992, 504), 516.0], ["Whetstone", "whetstone.png", Vector2(652, 594), 617.0]]:
		if not ResourceLoader.exists(ART + spec[1]):
			continue
		var sprite := Sprite2D.new()
		sprite.name = spec[0]
		sprite.texture = load(ART + spec[1])
		var visual: Vector2 = spec[2]
		sprite.position = Vector2(visual.x, spec[3])
		sprite.offset = Vector2(-sprite.texture.get_width() / 2.0, visual.y - float(spec[3]) - sprite.texture.get_height())
		sprite.centered = false
		$ObjectsAndPeople.add_child(sprite)

func _build_sites() -> void:
	for id in Content.SITES:
		var site: Dictionary = Content.SITES[id].duplicate()
		sites[id] = site
	for id in LIFE_TRACES:
		sites[id] = {"trace": true, "point": LIFE_TRACES[id].point, "reach": LIFE_TRACES[id].reach, "title": engine.LifeTrace(id).title}
	for key in ["huo", "shen", "su"]:
		sites[key] = {"person": key, "title": Content.PERSON_NAMES[key], "reach": 44.0}
	# 活动厅公告框：恶魔自立的学园守则四条。
	sites["BOARD"] = {"board": true, "point": Vector2(400, 108), "reach": 52.0, "title": "公告框"}

# ======================================================================= UI ==

func _build_ui() -> void:
	chapter_ui = ChapterUI.new()
	add_child(chapter_ui)
	chapter_ui.build_hud($FieldUI)
	ui = chapter_ui.ui
	hud_title = chapter_ui.hud_title
	hud_hint = chapter_ui.hud_hint
	hud_keys = chapter_ui.hud_keys
	hover_tip = chapter_ui.hover_tip
	aside = chapter_ui.aside
	modal_layer = chapter_ui.modal_layer
	busy_label = chapter_ui.busy_label
	stage_node = ReplayStage.new()
	stage_node.name = "ReplayStage"
	stage_node.host = self
	stage_node.z_index = 60
	add_child(stage_node)
	_refresh_hud()

func _refresh_hud() -> void:
	if not is_instance_valid(hud_title):
		return
	hud_title.text = "生活翼 · " + _room_name(player.position)
	hud_keys.text = chapter_ui.key_hint(nearest != "", stage == "investigate", replay_open, stage in ["roam", "investigate"])

## 乙区（第二部分 §11 第 7 条）：西端门厅（北厕、南储物），活动厅与备餐间，中廊两侧各三间房，外端两间空着。
func _room_name(p: Vector2) -> String:
	if p.x < 160:
		return "起居厅"
	if p.x < 304:
		return "厕" if p.y < 256 else ("储物间" if p.y >= 448 else "门厅")
	if p.x < 584:
		return "活动厅" if p.y < 416 else "备餐间"
	if p.y >= 304 and p.y < 448:
		return "走廊"
	var column := 0 if p.x < 816 else (1 if p.x < 1040 else 2)
	if p.y < 304:
		return ["R-08", "R-05", "空房"][column]
	return ["R-07", "R-06", "空房"][column]

func field_active() -> bool:
	return stage in ["roam", "investigate"] and modal_kind == "" and not dialogue.active and not dialogue.history_panel.visible and not story_lock and not ended

func site_visible(id: String) -> bool:
	if not sites.has(id):
		return false
	return engine.SiteVisible(stage, id, _room_key(player.position), ("foyer" if id in ["E1", "E4"] else _room_key(site_point(id))))

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
	match id:
		"E1": return Rect2(128, 344, 20, 80)
		"E8": return Rect2(484, 40, 32, 46)
		"E7": return Rect2(486, 226, 28, 20)
		"E6": return Rect2(638, 158, 30, 18)
		"E9": return Rect2(332, 508, 40, 18)
		"E10": return Rect2(502, 466, 22, 22)
		"E12": return Rect2(996, 198, 26, 54)
		"E11a", "E11b", "E11c", "E11d": return Rect2(site.point + Vector2(-22, -48), Vector2(44, 62))
		"E2": return Rect2(258, 428, 26, 16)
		"E3": return Rect2(154, 398, 14, 22)
		"E4": return Rect2(148, 352, 10, 64)
		"E5": return Rect2(176, 428, 22, 14)
		"E8j": return Rect2(157, 318, 16, 22)
		"E8r": return Rect2(583, 184, 18, 24)
		"BOARD": return Rect2(388, 46, 26, 36)
		"L6": return Rect2(980, 484, 24, 30)
		"L7": return Rect2(638, 580, 28, 20)
	return Rect2(site.point + Vector2(-14, -22), Vector2(28, 26))

# ================================================================ movement ===

func _physics_process(delta: float) -> void:
	if not is_instance_valid(player):
		return
	if follow_camera and camera:
		camera.position = Vector2(clampf(player.position.x, 320, 960), clampf(player.position.y - 24, 248, 456)).round()
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
	if not _script_presenting and field_active() and engine.PollScript():
		_refresh_script_actors()
		_present_script()
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
	if event.keycode == KEY_H and dialogue.history_panel.visible:
		dialogue.toggle_history()
		get_viewport().set_input_as_handled()
		return
	if ended:
		if event.keycode == KEY_R:
			get_tree().reload_current_scene()
		elif event.keycode == KEY_C:
			get_tree().change_scene_to_file("res://scenes/Chapter2/Game/Chapter2Game.tscn")
		return
	if modal_kind == "card":
		# 任意键返回现场。
		close_modal()
		get_viewport().set_input_as_handled()
		return
	if modal_kind != "":
		if chapter_ui.closes_modal(event.keycode, modal_kind):
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
	elif event.keycode == KEY_TAB and stage == "investigate":
		open_notebook()
		get_viewport().set_input_as_handled()
	elif event.keycode == KEY_R and replay_open:
		open_manuscript()
		get_viewport().set_input_as_handled()
	elif event.keycode == KEY_H:
		dialogue.toggle_history()
		get_viewport().set_input_as_handled()
	elif event.keycode == KEY_Q:
		open_actions()
		get_viewport().set_input_as_handled()

func open_actions() -> void:
	var items := []
	if nearest != "":
		var id := nearest
		items.append([str(sites[id].title), func() -> void:
			close_modal()
			_complete_click_interaction(id)])
	if stage == "investigate":
		items.append(["笔记本（Tab）", open_notebook])
	if replay_open:
		items.append(["重演手稿（R）", open_manuscript])
	items.append(["回看（H）", func() -> void:
		close_modal()
		dialogue.toggle_history()])
	close_modal()
	modal_kind = "menu"
	_cancel_click_navigation()
	chapter_ui.menu("行动", items, "点击移动与交互；" + chapter_ui.key_hint(nearest != "", stage == "investigate", replay_open, true), close_modal)
	modal_root = chapter_ui.modal_root

## 普通家具不是证物：点到时只给一句极短的心声，同一件只说一次，不打断移动。
const FURNITURE_ASIDES := {
	"Bed": "床。被子叠过。", "Nightstand": "床头柜。灯油还有。", "Wardrobe": "衣柜。几件换洗衣服。",
	"Desk": "书桌。抽屉是空的。", "DeskChair": "椅子。", "LinenChest": "箱子里是被褥。", "Fern": "一盆蕨。",
	"Dresser": "梳妆台。一只白瓷小碟。", "PaperBasket": "纸篓。", "WallClock": "挂钟。还在走。",
	"StudyBookcase": "书架。都是寻常的书。", "StudyChair": "椅子。", "StudyLamp": "落地灯。", "ReadingChair": "扶手椅。",
	"ReadingTable": "小几。", "Globe": "地球仪。转得动。", "StudySofa": "沙发。", "StudyFern": "花架。",
	"StudyConsole": "边桌。", "Sideboard": "碗柜。碗碟摆得整整齐齐。", "Range": "灶。是冷的。", "Worktable": "案板。刀收在架上。",
	"CupTable": "小圆桌。", "PantryLamp": "落地灯。", "PantryFern": "一盆蕨。", "PantryBarrel": "水桶。满的。",
	"PantryCrate": "存粮。", "PantrySack": "面粉。", "CorridorConsole": "边桌。", "CorridorPlant": "花架。",
	"HallLamp": "落地灯。", "CoatStand": "衣架。", "Bookcase": "书架。灰很厚。", "StudyTable": "长桌。桌面擦得很干净。",
	"Washstand": "水槽。", "SheetedBed": "床上罩着防尘布。灰落得很匀。", "EmptyBookcase": "书架。灰落得很匀，哪儿都一样厚。",
	"EmptyWardrobe": "衣柜。空的。", "Basin": "洗手盆。", "StoreCrate": "空箱子。", "StoreSack": "面粉。", "StoreBarrel": "水桶。满的。",
}

func _furniture_aside(pos: Vector2) -> void:
	for node in $ObjectsAndPeople.get_children():
		if node is not Sprite2D or furniture_spoken.has(node.name):
			continue
		var key := ""
		for prefix in FURNITURE_ASIDES:
			if str(node.name).begins_with(prefix) and prefix.length() > key.length():
				key = prefix
		if key == "":
			continue
		var sprite := node as Sprite2D
		if sprite.texture == null:
			continue
		var rect := Rect2(sprite.position + sprite.offset, sprite.texture.get_size())
		if rect.has_point(pos):
			engine.SetProgress("furniture_spoken", node.name, true)
			show_aside(FURNITURE_ASIDES[key])
			return
	# 画在墙上、地上的东西（窗、拖鞋、地毯）不是节点：地图生成时记下区域，按区域给一句心声。
	for index in layout.asides.size():
		var decor: Array = layout.asides[index]
		var area := Rect2(decor[0][0], decor[0][1], decor[0][2], decor[0][3])
		if area.has_point(pos) and not furniture_spoken.has("decor%d" % index):
			engine.SetProgress("furniture_spoken", "decor%d" % index, true)
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
	return stage in ["investigate", "playback", "answer", "finale"]

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
	actor.speed = previous

## 沿路走；被家具的碰撞卡住（导航网格与碰撞体略有出入）时，最后几步直接到位，不让人原地踏步。
func _follow(actor: CharacterBody2D, route: PackedVector2Array) -> void:
	actor.follow_path(route)
	var last := actor.position
	var still := 0
	while actor.travelling:
		await get_tree().physics_frame
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
	var room := _room_key(player.position)
	var action: String = engine.RoomTrigger(room)
	if action == "meeting":
		_meeting()
		return
	if action == "investigate":
		if room != current_room:
			current_room = room
			_refresh_script_actors()
			_on_room_changed(room)
		var entry: String = engine.RoomEntry(room)
		if entry != "":
			call_deferred("_nudge_room", entry)
			return

var visited_rooms: Dictionary:
	get: return engine.Progress("visited_rooms")
	set(value): engine.ReplaceProgress("visited_rooms", value)

func _room_key(p: Vector2) -> String:
	return {"R-05": "R05", "R-06": "R06", "R-07": "R07", "R-08": "R08", "走廊": "corridor", "活动厅": "hall", "备餐间": "pantry", "门厅": "foyer", "空房": "empty", "起居厅": "lounge", "厕": "toilet", "储物间": "store"}.get(_room_name(p), "")

func _nudge_room(room: String) -> void:
	await say(room)

func _nudge(id: String) -> void:
	await say("nudge_" + id)

# ================================================================ dialogue ===

func say(title: String) -> void:
	if dialogue.active:
		await dialogue.finished
	_cancel_click_navigation()
	story_lock = true
	dialogue.panel.show()
	dialogue.start(STORY, title, self)
	await dialogue.finished
	dialogue.panel.hide()
	story_lock = false
	dialogue_closed_ms = Time.get_ticks_msec()
	_refresh_hud()

func _localized_speaker(canonical: String) -> String:
	match canonical:
		"侦探": return detective_name
		"侦探（心声）": return detective_name + "（心声）"
	return canonical

func dialogue_has_modal_input() -> bool:
	return modal_kind == "name"

# 对白条件与插值（.dialogue 中调用）
func seen(id: String) -> bool: return examined.has(id)
func tested(id: String) -> bool: return experiments.has(id)
func asked(id: String) -> bool: return helped.has(id)
func flag(name: String) -> bool: return flags.has(name)
func set_flag(name: String) -> void: engine.SetProgress("flags", name, true)
func clear_flag(name: String) -> void: engine.RemoveProgress("flags", name)
func recalled(who: String) -> bool: return recalls.has(who)
## 恶魔朝某人的欲望说一句真话（.dialogue 中 do amplify(...)）：以 Corruption 来源写入此人认知。
func amplify(who: String, words: String) -> void: engine.Amplify(who, words)
## 恶魔对某位同伴低语（.dialogue 中 do whisper(...)）：写入他的认知后，他自己决定要不要去拿旧物。
func whisper(who: String, words: String) -> void:
	engine.Whisper(who, words)
	engine.SetProgress("nudged", OWN_OBJECTS[who], true)
	call_deferred("_consider", who)
func alone_choice(who: String) -> String: return str(life.get(who, {}).get("choice", ""))
func life_thought(who: String) -> String: return str(life.get(who, {}).get("thought", ""))
func share_recall(who: String) -> void:
	_apply_recall(OWN_OBJECTS[who], who)
	engine.SetDetail("life", who, "told", true)
func compared(who: String) -> bool: return soles_compared.has(who)
func v6_open() -> bool:
	return engine.V6Open()
func hooks(who: String) -> int: return int(engine.Hooks(who))
func total_hooks() -> int: return int(engine.TotalHooks())
func matched() -> bool: return full_match_seen
func replayed() -> bool: return replay_count > 0
func fragment(who: String) -> String: return str(recalls.get(who, ""))
func heard(who: String) -> String:
	for h in last_sound:
		if h.who == who:
			return str(h.text) + ("　" + str(h.direction) if str(h.direction) != "" else "")
	return ""
func heard_where(who: String) -> String:
	for h in last_sound:
		if h.who == who:
			return {"hall": "铜口旁", "foyer": "门边", "pantry": "备餐间", "R05": "R-05", "R08": "R-08"}.get(h.room, "")
	return ""
## 恶魔对有钩之人改用更亲近的称呼。
func addr(who: String) -> String:
	var close := hooks(who) > 0
	match who:
		"detective": return "我的侦探" if close else "侦探先生"
		"huo": return "霍铎" if close else "霍先生"
		"shen": return "沈鼎" if close else "沈师傅"
		"su": return "泠" if close else "苏小姐"
	return ""

# ================================================================= meeting ===

func _meeting() -> void:
	stage = "meeting"
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
	player.pose("idle", "front")
	var plan: int = engine.BeginMeeting()
	while true:
		var command: String = engine.NextMeeting(plan)
		if command == "":
			break
		match command:
			"@name": await _ask_name()
			"@board": await _present_board()
			"@rest":
				await _rest()
				engine.Presented(plan)
				engine.NextMeeting(plan)
				return
			"@investigate":
				stage = "investigate"
				for key in POSTS:
					walk_actor(key, POSTS[key])
			_: await say(command)
		engine.Presented(plan)
	_refresh_hud()

var _name_field: LineEdit

func _ask_name() -> void:
	var body := open_modal("报上名字", Vector2(560, 300), "name")
	body.add_child(Paper.label("三个人在等你开口。登船之前，你叫什么？", 19))
	_name_field = LineEdit.new()
	_name_field.placeholder_text = "你的名字"
	_name_field.max_length = 8
	_name_field.custom_minimum_size = Vector2(0, 46)
	_name_field.add_theme_font_size_override("font_size", 21)
	body.add_child(_name_field)
	_modal_result = null
	_name_field.text_submitted.connect(func(_t: String) -> void: _confirm_name())
	add_footer([["就叫这个", _confirm_name]])
	_name_field.grab_focus.call_deferred()
	while _modal_result == null:
		await get_tree().process_frame

func _confirm_name() -> void:
	var text := _name_field.text.strip_edges()
	detective_name = text if not text.is_empty() else "侦探"
	_modal_choose(true)

# =================================================================== modal ===

func open_modal(title: String, size: Vector2, kind: String) -> VBoxContainer:
	close_modal()
	modal_kind = kind
	_cancel_click_navigation()
	var body: VBoxContainer = chapter_ui.open_modal(title, size, kind)
	modal_root = chapter_ui.modal_root
	return body

func add_footer(actions: Array) -> HBoxContainer:
	return chapter_ui.add_footer(actions)

func close_modal() -> void:
	chapter_ui.close_modal()
	modal_root = null
	if modal_kind == "replay" and is_instance_valid(manuscript):
		manuscript = null
	modal_kind = ""

# ==================================================================== cards ===

func interact(id: String) -> void:
	if not site_visible(id):
		return
	var site: Dictionary = sites[id]
	if site.has("person"):
		await talk(site.person)
		return
	if id == "E8":
		open_tube_menu()
		return
	if site.has("board"):
		open_board()
		return
	if site.has("trace"):
		open_trace(id)
		return
	open_card(site.evidence)

## R-06、R-07 的生活痕迹：只看，不编号，不进重演对照。
func open_trace(id: String) -> void:
	engine.SetProgress("examined", id, true)
	var trace: Dictionary = engine.LifeTrace(id)
	var body := open_modal(trace.title, Vector2(820, 460), "card")
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 22)
	body.add_child(row)
	var picture_path := ART + str(trace.picture) + ".png"
	if ResourceLoader.exists(picture_path):
		var picture := TextureRect.new()
		picture.texture = load(picture_path)
		picture.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		picture.custom_minimum_size = Vector2(288, 216)
		picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		row.add_child(picture)
	var text := VBoxContainer.new()
	text.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(text)
	text.add_child(Paper.label(str(trace.facts), 20))
	text.add_child(Paper.label("来源：" + str(trace.room), 16, Paper.MUTED))
	add_footer([["返回现场", close_modal]])

func examine(id: String) -> void:
	for event in engine.ExamineContent(id):
		match str(event):
			"evidence": Sound.sfx("evidence", -3.0)
			"unlock": call_deferred("_unlock_replay")
			"v6": call_deferred("_v6_doubt")

## 细看了两件相熟旁证：疑问由玩家的发现提出，重演手稿多出第六个问题。
func _v6_doubt() -> void:
	while story_lock or dialogue.active:
		await get_tree().process_frame
	await say("v6_doubt")

func open_card(id: String, brief := false) -> void:
	examine(id)
	if engine.NudgeCard(id):
		# 恶魔在玩家第一次面对这件物件的回忆选项之前说一句，朝向此人的欲望；每件只说一次。
		close_modal()
		await say("nudge_" + id)
	var ev: Dictionary = Content.EVIDENCE[id]
	var body := open_modal(ev.name, Vector2(900, 660 if not brief else 420), "card")
	if brief:
		# 请人看过之后，只给新得到的那一句，不重显整张原文。
		for note in _card_notes(id):
			body.add_child(Paper.label(note, 19, Paper.SEAL if note.begins_with("回忆") else Paper.INK))
		add_footer([["看整张证物卡", open_card.bind(id)], ["返回现场", close_modal]])
		return
	var picture_path := ART + "card_" + str(ev.picture) + ".png"
	var picture: Texture2D = load(picture_path) if ResourceLoader.exists(picture_path) else null
	var text: VBoxContainer = chapter_ui.card_body(body, picture)
	var facts: String = engine.CardFacts(id)
	text.add_child(Paper.label(facts, 20))
	text.add_child(Paper.label("来源：" + str(ev.source), 16, Paper.MUTED))
	var notes := _card_notes(id)
	if not notes.is_empty():
		body.add_child(Paper.rule())
		for note in notes:
			body.add_child(Paper.label(note, 18, Paper.SEAL if note.begins_with("回忆") else Paper.INK))
	var actions := []
	if ev.has("experiment"):
		actions.append([str(ev.experiment) + ("（再做一次）" if experiments.has(id) and id == "E8" else ""), _on_experiment.bind(id), not engine.CanExperiment(id)])
	if ev.has("help") and engine.CanHelp(id):
		actions.append(["请%s看看" % Content.PERSON_NAMES[ev.help], _on_help.bind(id)])
	var going: bool = str(life.get(str(ev.get("recall", "")), {}).get("state", "")) in ["going", "holding"]
	if ev.has("recall") and engine.CanRecall(ev.recall, going):
		var who: String = ev.recall
		actions.append(["触碰插闩，试着回想（守则第 1 条）" if who == "detective" else "请%s触碰它，回想（守则第 1 条）" % Content.PERSON_NAMES[who], _on_recall.bind(id)])
	actions.append(["返回现场", close_modal])
	add_footer(actions)

func _card_notes(id: String) -> Array:
	return engine.CardNotes(id)

func _on_experiment(id: String) -> void:
	close_modal()
	var who := await _pick_sole() if id == "E3" else ""
	await _present_investigation(engine.BeginExperiment(id, who))
	open_card(id)

func _on_help(id: String) -> void:
	close_modal()
	await _present_investigation(engine.BeginHelp(id))
	open_card(id, true)

func _present_investigation(plan: int) -> void:
	while true:
		var command: String = engine.NextMeeting(plan)
		if command == "":
			return
		if command.begins_with("@summon:"):
			await _summon(command.trim_prefix("@summon:"))
		elif command.begins_with("@sfx:"):
			var parts := command.split(":")
			Sound.sfx(parts[1], float(parts[2]) if parts.size() > 2 else -4.0)
		elif command.begins_with("@music:"):
			var parts := command.split(":")
			Sound.music(parts[1], float(parts[2]) if parts.size() > 2 else 0.0)
		elif command.begins_with("@finish:"):
			_finish_reach(command.trim_prefix("@finish:"))
		elif command.begins_with("@aside:"):
			show_aside(command.trim_prefix("@aside:"))
		elif command.begins_with("@consider:"):
			_consider(command.trim_prefix("@consider:"))
		elif command == "@sound":
			await sound_test()
		elif command == "@answerpick":
			engine.ReplaceProgress("answer_pick", await _answer_pick())
		elif command == "@finale":
			await finale()
		elif command == "@finale_prepare":
			_finale_prepare()
		elif command == "@door_open":
			_open_door_picture()
		elif command == "@redraw":
			decals.queue_redraw()
		elif command == "@su_approach":
			await walk_actor("su", player.position + Vector2(34, 6))
		elif command == "@lock":
			story_lock = true
		elif command == "@unlock":
			story_lock = false
		elif command == "@save:false":
			_save_chapter_end(false)
		elif command == "@save:true":
			_save_chapter_end(true)
		elif command == "@ending:complete":
			_ending_card("门的这一侧 · 完", "门开了。门外是起居厅。")
		elif command == "@ending:rest":
			_ending_card("门的这一侧 · 歇息", "你们留在生活翼。门一直闩着。")
		elif command == "@hud":
			_refresh_hud()
		else:
			await say(command)
		engine.Presented(plan)

func _pick_sole() -> String:
	var body := open_modal("比对鞋底", Vector2(620, 450), "menu")
	body.add_child(Paper.label("灰上那枚马球靴印。拿谁的鞋底来比？", 19))
	_modal_result = null
	var list := VBoxContainer.new()
	list.add_theme_constant_override("separation", 10)
	body.add_child(list)
	for pair in [["detective", "你自己的靴底"], ["huo", "霍铎的鞋"], ["shen", "沈鼎的鞋"], ["su", "苏泠的鞋"]]:
		list.add_child(Paper.button(pair[1], _modal_choose.bind(pair[0])))
	add_footer([["算了", _modal_choose.bind("")]])
	while _modal_result == null:
		await get_tree().process_frame
	return str(_modal_result)

func _summon(who: String) -> void:
	var actor := actor_of(who)
	var guard := 0
	while str(life.get(who, {}).get("state", "")) in ["going", "holding"] and guard < 900:
		guard += 1
		await get_tree().process_frame
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
					actor.speed = previous
				break
	actor.face_point(player.position)
	player.face_point(actor.position)

## 恶魔走近旧物时对本人说的、朝向其欲望的真话（Corruption 来源写入此人认知）。

var whispered: Dictionary:
	get: return engine.Progress("whispered")
	set(value): engine.ReplaceProgress("whispered", value)
var recall_decision: Dictionary:
	get: return engine.Progress("recall_decision")
	set(value): engine.ReplaceProgress("recall_decision", value)
var refusals: Dictionary:
	get: return engine.Progress("refusals")
	set(value): engine.ReplaceProgress("refusals", value)

func told_rule(who: String) -> bool:
	return bool(engine.KnowsRule1(who))

func recall_thought() -> String:
	return str(recall_decision.get("thought", ""))

func _on_recall(id: String) -> void:
	close_modal()
	await _present_investigation(engine.BeginRecall(id))
	open_card(id)

func _resolve_recall(id: String, who: String) -> void:
	await _present_investigation(engine.BeginResolveRecall(id, who))

# ============================================================ companions' own reach ===
## 恶魔低语之后，同伴依自己的认知决定要不要走过去拿起旧物（MorningWorld.Intend）；
## 走到之后伸手由 ActionRuntime 结算（MorningWorld.Reach）；碰不碰仍由他自己决定。
## 侦探在场可以劝阻、说明守则第 1 条或任其自便；不在场，只留下痕迹和他事后的神色。

func _consider(who: String) -> void:
	var intent: Dictionary = engine.Consider(who)
	if bool(intent.get("reach", false)):
		_go_reach(who, str(intent.get("thought", "")))

func _go_reach(who: String, thought: String) -> void:
	engine.BeginReach(who, thought)
	var obj: String = OWN_OBJECTS[who]
	await get_tree().create_timer(0.6).timeout
	while dialogue.active and _detective_present(who):
		await get_tree().process_frame
	var actor := actor_of(who)
	var spot := _spot_near(site_point(obj), float(sites[obj].reach) - 14.0, actor)
	await walk_actor(who, spot, 110.0)
	actor.face_point(site_point(obj))
	engine.Locate(who, _room_key(actor.position))
	if not bool(engine.Reach(who)):
		return
	_show_object(obj, false)
	if _detective_present(who):
		await _present_reach(who)
	else:
		await _alone_reach(who)

func _detective_present(who: String) -> bool:
	var actor := actor_of(who)
	return _room_name(player.position) == _room_name(actor.position) and player.position.distance_to(actor.position) < 360.0

func _present_reach(who: String) -> void:
	while dialogue.active or story_lock or (modal_kind != "" and modal_kind != "card") or stage != "investigate":
		await get_tree().process_frame
	if not _detective_present(who):
		await _alone_reach(who)
		return
	close_modal()
	actor_of(who).face_point(player.position)
	player.face_point(actor_of(who).position)
	await _present_investigation(engine.BeginPresentReach(who))

func _alone_reach(who: String) -> void:
	engine.PrepareAlone(who)
	await get_tree().create_timer(1.2).timeout
	engine.CompleteAlone(who)
	_finish_reach(who)

func _finish_reach(who: String) -> void:
	var obj: String = OWN_OBJECTS[who]
	engine.PutBack(who)
	_show_object(obj, true)
	if _detective_present(who):
		engine.SetDetail("life", who, "noticed", true)
	await walk_actor(who, POSTS[who], 110.0)
	engine.Locate(who, _room_key(actor_of(who).position))

func _show_object(obj: String, visible_now: bool) -> void:
	var sprite := $ObjectsAndPeople.get_node_or_null(OBJECT_SPRITES.get(obj, ""))
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
	for who in ["huo", "su", "shen"]:
		engine.Locate(who, _room_key(actor_of(who).position))
	await _present_investigation(engine.BeginRoomChanged(room, Time.get_ticks_msec()))

## 被挤到时让开：玩家走路贴上来，站着的同伴往旁边挪一步。
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

func talk_first(who: String) -> bool:
	return bool(life.get(who, {}).get("alone", false)) and not bool(life.get(who, {}).get("told", false))

func _apply_recall(id: String, who: String) -> void:
	engine.ApplyRecall(id, who)

## S3 广播结束：镜头带过公告框，学园守则四条完整呈现一次（任意键翻过）。读不读，由玩家决定；这里只是让它被看见过一次。
func _present_board() -> void:
	follow_camera = false
	var from := camera.position
	var tween := create_tween()
	tween.tween_property(camera, "position", Vector2(420, 248), 0.8)
	await tween.finished
	open_board()
	while modal_kind != "":
		await get_tree().process_frame
	var back := create_tween()
	back.tween_property(camera, "position", from, 0.5)
	await back.finished
	follow_camera = true

func open_board() -> void:
	engine.SetProgress("flags", "board_read", true)
	var body := open_modal("活动厅公告框", Vector2(860, 620), "card")
	body.add_child(Paper.label("框里压着一张新抄的纸，字迹端正，墨色很新。题头写着：学园守则。", 18, Paper.MUTED))
	for clause in engine.PostedRules():
		body.add_child(Paper.label(str(clause), 19))
	add_footer([["返回现场", close_modal]])

func _unlock_replay() -> void:
	if not engine.UnlockReplay():
		return
	while story_lock or dialogue.active or modal_kind != "":
		await get_tree().process_frame
	await say("replay_unlock")
	_refresh_hud()

# ================================================================ notebook ===

func open_notebook() -> void:
	var body := open_modal("笔记本", Vector2(1000, 780), "notebook")
	if examined.is_empty():
		body.add_child(Paper.label("还没有记下任何东西。", 19, Paper.MUTED))
	for place in Content.PLACES:
		var ids := []
		for id in Content.EVIDENCE:
			if examined.has(id) and Content.EVIDENCE[id].place == place:
				ids.append(id)
		if ids.is_empty():
			continue
		var entries := []
		for id in ids:
			var mark := ""
			if experiments.has(id) or helped.has(id):
				mark = "　·已试"
			entries.append([str(Content.EVIDENCE[id].name) + mark, open_card.bind(id)])
		chapter_ui.notebook_section(body, place, entries)
	if flags.has("board_read"):
		body.add_child(Paper.label("[b]其他[/b]", 21, Paper.SEAL))
		var other := Paper.button("活动厅公告框（学园守则）", open_board, 18)
		other.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
		body.add_child(other)
	if not recalls.is_empty():
		body.add_child(Paper.rule())
		body.add_child(Paper.label("[b]回忆所得[/b]", 21, Paper.SEAL))
		for who in recalls:
			body.add_child(Paper.label("%s：%s" % [Content.PERSON_NAMES[who], recalls[who]], 18))
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
	manuscript = ReplayManuscript.new()
	manuscript.host = self
	manuscript.size_flags_vertical = Control.SIZE_EXPAND_FILL
	column.add_child(manuscript)

func replay_ui_data() -> Dictionary:
	var marks := {}
	for index in recall_marks:
		marks[str(index)] = Content.PERSON_NAMES[recall_marks[index]]
	return {"variables": Content.VARIABLES, "count": Content.VARIABLES.size() if v6_open() else Content.VARIABLES.size() - 1, "marks": marks, "fresh": [5] if not flags.has("v6_seen") else [], "full_match_text": "全部吻合。", "ready_hint": "还有变量没有设定。", "show_empty_finding": true}

func replay_ui_evidence() -> Dictionary:
	return Content.EVIDENCE

func replay_ui_ready(selection: Array) -> bool:
	return not selection.has(-1)

func replay_ui_opened() -> void:
	if v6_open():
		engine.SetProgress("flags", "v6_seen", true)

func replay_ui_run(selection: Array, skip: bool) -> void:
	var result := run_replay(selection)
	last_run_vars = selection.duplicate()
	if skip:
		after_replay(result)
	else:
		play_replay(result)

func run_replay(vars: Array) -> Dictionary:
	replay_vars = vars.duplicate()
	var result: Dictionary = engine.RunReplay(vars, examined.keys())
	replay_result = result
	replay_count += 1
	return result

## 回放结束（或跳过）后回到手稿；跳过与看完的结算相同。
func play_replay(result: Dictionary) -> void:
	close_modal()
	stage = "playback"
	story_lock = true
	Sound.sfx("replay", -4.0)
	Sound.duck(true)
	await stage_node.play(result.frames)
	Sound.duck(false)
	stage = "investigate"
	story_lock = false
	await after_replay(result)

func after_replay(_result: Dictionary) -> void:
	close_modal()
	await _present_investigation(engine.BeginAfterReplay())
	open_manuscript()

# ================================================================ sound test ==

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
	var body := open_modal("声路试验", Vector2(820, 600), "sound")
	body.add_child(Paper.label("请铜口说一句话。你和三位同伴分别站在铜口旁、R-08、门边、备餐间、R-05 中的四处听。", 19))
	body.add_child(Paper.label("[b]让铜口说哪一句[/b]", 19, Paper.SEAL))
	var sentences: Array = engine.Sentences()
	_sound_choice = [0, "foyer"]
	_sound_buttons = {"sentence": [], "spot": []}
	var sentence_row := VBoxContainer.new()
	body.add_child(sentence_row)
	for i in sentences.size():
		var b := Paper.button("“%s”" % sentences[i], _sound_pick.bind("sentence", i, i))
		b.toggle_mode = true
		b.button_pressed = i == 0
		_sound_buttons.sentence.append(b)
		sentence_row.add_child(b)
	body.add_child(Paper.label("[b]你自己站在哪里听[/b]", 19, Paper.SEAL))
	var where_row := HBoxContainer.new()
	where_row.add_theme_constant_override("separation", 10)
	body.add_child(where_row)
	var places := [["foyer", "门边"], ["R08", "R-08"], ["hall", "铜口旁"], ["pantry", "备餐间"], ["R05", "R-05"]]
	for i in places.size():
		var b := Paper.button(places[i][1], _sound_pick.bind("spot", i, places[i][0]))
		b.toggle_mode = true
		b.button_pressed = i == 0
		_sound_buttons.spot.append(b)
		where_row.add_child(b)
	_modal_result = null
	add_footer([["算了", _modal_choose.bind(false)], ["开始", _modal_choose.bind(true)]])
	while _modal_result == null:
		await get_tree().process_frame
	if not _modal_result:
		return
	var positions: Dictionary = engine.SoundPositions(str(_sound_choice[1]))
	Sound.sfx("tuning_fork", -5.0)
	last_sound = engine.SoundTest(_sound_choice[0], positions)
	engine.SetProgress("experiments", "E8", true)
	set_meta("sound_sentence", sentences[_sound_choice[0]])
	sound_rounds += 1
	if sound_rounds == 1:
		await say("sound_test")
		open_card("E8")
	else:
		# 第二轮起不再逐人复述：一张小表汇总四处所闻。
		_sound_table()

func _sound_table() -> void:
	var body := open_modal("声路试验 · 第 %d 轮" % sound_rounds, Vector2(820, 470), "card")
	body.add_child(Paper.label("铜口说：“%s”" % sound_sentence(), 19))
	var grid := GridContainer.new()
	grid.columns = 4
	grid.add_theme_constant_override("h_separation", 22)
	grid.add_theme_constant_override("v_separation", 8)
	body.add_child(grid)
	for head in ["位置", "谁", "听到", "从哪边来"]:
		grid.add_child(Paper.label("[b]%s[/b]" % head, 18, Paper.SEAL))
	for h in last_sound:
		grid.add_child(Paper.label(heard_where(h.who), 18))
		grid.add_child(Paper.label(Content.PERSON_NAMES[h.who] if h.who != "detective" else "你", 18))
		grid.add_child(Paper.label(str(h.text), 18))
		grid.add_child(Paper.label(str(h.direction), 18))
	add_footer([["看证物卡", open_card.bind("E8")], ["返回现场", close_modal]])

func sound_sentence() -> String:
	return str(get_meta("sound_sentence", ""))

# ==================================================================== talk ===

func talk(who: String) -> void:
	actor_of(who).face_point(player.position)
	player.face_point(actor_of(who).position)
	await _present_investigation(engine.BeginTalk(who))

# ==================================================================== tube ===

func open_tube_menu() -> void:
	var body := open_modal("铜口", Vector2(620, 420), "menu")
	body.add_child(Paper.label("铜口静静地嵌在墙上。铜牌上的 ◎ 擦得发亮。", 19))
	var list := VBoxContainer.new()
	list.add_theme_constant_override("separation", 10)
	body.add_child(list)
	list.add_child(Paper.button("检视铜口与铜牌", open_card.bind("E8")))
	list.add_child(Paper.button("回答始业之问", _tube_answer))
	list.add_child(Paper.button("不作答，在生活翼歇息", _tube_rest))
	add_footer([["返回现场", close_modal]])

func _tube_answer() -> void:
	close_modal()
	answer()

func _tube_rest() -> void:
	close_modal()
	_confirm_rest()

func _confirm_rest() -> void:
	await _present_investigation(engine.BeginConfirmRest())

# ================================================================== answer ===

func statements() -> Array:
	return engine.Statements(replay_vars, replay_count, bool(replay_result.get("detective_bolted", false)))

func answer() -> void:
	await _present_investigation(engine.BeginAnswer())

## 第 1—4 步陈述与出示成对：先各选一项，再作答。返回 {say, show}；空字典表示暂时不答。
var _answer_list: VBoxContainer
var _answer_choice: Dictionary = {}
var _answer_summary: RichTextLabel
var _answer_submit: Button

func _answer_pick() -> Dictionary:
	_modal_result = null
	_answer_choice = {"say": "", "show": ""}
	var body := open_modal("始业之答", Vector2(900, 820), "answer")
	# 题目与当前所选的陈述、出示固定在顶部；下面的列表单独滚动，不与题目叠压。
	var column: VBoxContainer = modal_root.get_meta("column")
	var header := VBoxContainer.new()
	header.add_theme_constant_override("separation", 6)
	column.add_child(header)
	column.move_child(header, 2)
	var questions := {1: "门，是谁闩上的？", 2: "门外，有人呼喊吗？", 3: "他喊的是什么？", 4: "闩门的人，为何不听门边那位女士的？"}
	header.add_child(Paper.label("铜口问：[b]%s[/b]" % questions.get(int(answer_state.get("step", 1)), ""), 21, Paper.SEAL))
	header.add_child(Paper.label("陈述一句结论，再出示一件能支撑它的东西。", 18, Paper.MUTED))
	_answer_summary = Paper.label("", 18)
	header.add_child(_answer_summary)
	header.add_child(Paper.rule())
	_answer_list = VBoxContainer.new()
	_answer_list.add_theme_constant_override("separation", 8)
	body.add_child(_answer_list)
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
		await get_tree().process_frame
	return _modal_result

func _answer_refresh() -> void:
	var said := str(_answer_choice.say)
	var shown := str(_answer_choice.show)
	var line: String = "陈述：" + str(Content.STATEMENTS[said] if said != "" else "（未选）") + "\n"
	line += "出示：" + (str(Content.EVIDENCE[shown].name) if shown != "" else "（未选）")
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
			_answer_list.add_child(Paper.label("你还没有可以陈述的结论。重演一次，设定过的处境就能说出口。", 18, Paper.MUTED))
		for key in options:
			var b := Paper.button(Content.STATEMENTS[key], _answer_select.bind("say", key), 19)
			b.toggle_mode = true
			b.button_pressed = _answer_choice.say == key
			_answer_list.add_child(b)
	else:
		if examined.is_empty():
			_answer_list.add_child(Paper.label("笔记本里还没有东西。", 18, Paper.MUTED))
		for id in Content.EVIDENCE:
			if examined.has(id):
				var b := Paper.button(Content.EVIDENCE[id].name, _answer_select.bind("show", id), 19)
				b.toggle_mode = true
				b.button_pressed = _answer_choice.show == id
				_answer_list.add_child(b)

func _judge(step: int, pick: Dictionary) -> String:
	return engine.Judge(step, str(pick.get("say", "")), str(pick.get("show", "")))

func wrong_name() -> String:
	return engine.WrongName()

# ================================================================== endings ==

func finale() -> void:
	await _present_investigation(engine.BeginFinale())

func _finale_prepare() -> void:
	story_lock = true
	follow_camera = false
	player.position = Vector2(196, 384)
	player.pose("idle", "left")
	camera.position = Vector2(320, 368)
	_refresh_hud()

func _open_door_picture() -> void:
	var door := $ObjectsAndPeople/MechanicalIsolationDoor
	Sound.sfx("door_unbar")
	Sound.music("main_theme", 4.0)
	door.get_node("DoorFace").hide()
	door.get_node("DoorGap").show()
	door.get_node("LoungeCover").hide()
	door.get_node("LoungeCoverInner").hide()
	# 恶魔须显形才能抬外闩：起居厅的烟灰上留下一行窄底皮鞋印。
	var prints := door.get_node_or_null("DemonPrints")
	if prints:
		prints.show()

func _rest() -> void:
	await _present_investigation(engine.BeginRest())

func _save_chapter_end(rested: bool) -> void:
	var data: Dictionary = engine.ChapterEnd(examined.keys(), full_match_seen, flags.has("asked_hand"), rested)
	data["detective_name"] = detective_name
	var file := FileAccess.open("user://chapter1_end.json", FileAccess.WRITE)
	if file:
		file.store_string(JSON.stringify(data, "\t"))

func _ending_card(title: String, line: String) -> void:
	ended = true
	var body := open_modal(title, Vector2(720, 420), "ending")
	body.add_child(Paper.label(line, 21))
	var hook_text := []
	for key in ["detective", "huo", "shen", "su"]:
		if hooks(key) > 0:
			hook_text.append(Content.PERSON_NAMES[key])
	body.add_child(Paper.label("留在恶魔那里的钩：%s" % ("无" if hook_text.is_empty() else "、".join(hook_text)), 18, Paper.SEAL))
	var kept := []
	for key in ["detective", "huo", "shen", "su"]:
		if hooks(key) == 0:
			kept.append(Content.PERSON_NAMES[key] if key != "detective" else "你自己")
	body.add_child(Paper.label("你没有交出的：%s" % ("无" if kept.is_empty() else "、".join(kept)), 18, Paper.MATCH))
	body.add_child(Paper.label("按 C 进入第二章；按 R 重新开始。", 17, Paper.MUTED))
	modal_kind = "ending"
	stage = "ended"

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
			for key in POSTS:
				actor_of(key).position = POSTS[key]
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

## 同一 C# 剧情运行器的宿主出口；保留 Paper、Sound 和原人物动作组件。
var _script_presenting := false

func run_script(path: String) -> void:
	engine.LoadScript(path)
	_refresh_script_actors()
	await _present_script()

func _present_script() -> void:
	_script_presenting = true
	while true:
		var event: Dictionary = engine.NextScript()
		_refresh_script_actors()
		match str(event.kind):
			"done":
				_script_presenting = false
				return
			"wait": await get_tree().process_frame
			"text", "choice", "image":
				_modal_result = null
				var body := open_modal("", Vector2(820, 520), "script")
				if event.kind == "image":
					var picture := TextureRect.new()
					picture.texture = load(str(event.content))
					picture.custom_minimum_size = Vector2(576, 360)
					picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
					picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
					body.add_child(picture)
				else:
					body.add_child(Paper.label(str(event.content), 20))
				var actions := []
				if event.kind == "choice":
					for option in event.options:
						actions.append([str(option.text), _modal_choose.bind(str(option.id))])
				else:
					actions.append(["继续", _modal_choose.bind(true)])
				add_footer(actions)
				while _modal_result == null:
					await get_tree().process_frame
				if event.kind == "choice":
					engine.ChooseScript(str(_modal_result))
				close_modal()
			"audio": Sound.music(str(event.content))
			"animation": actor_of(str(event.actor)).pose(str(event.content))

func _refresh_script_actors() -> void:
	script_actors.refresh(self, engine.ScriptCharacters(_room_key(player.position)), player.position)
