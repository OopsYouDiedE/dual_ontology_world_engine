extends Node2D
## 《安眠者有福》第一章《门的这一侧》：生活翼现场、调查、重演入口与始业之答。
## 推演在 C#：AwakeningEngine → NightReplay（Core 的 Observation / DecisionSystem / SocialSystem / SimulationSession）。
## 本脚本是宿主：地图、移动、相机、界面、对白与流程。

const Content := preload("res://scenes/Awakening/AwakeningContent.gd")
const FieldNavigation := preload("res://scenes/Awakening/FieldNavigation.gd")
const Paper := preload("res://scenes/Awakening/Paper.gd")
const ReplayManuscript := preload("res://scenes/Awakening/ReplayManuscript.gd")
const ReplayStage := preload("res://scenes/Awakening/ReplayStage.gd")
const TraceDecals := preload("res://scenes/Awakening/TraceDecals.gd")
const EngineScript := preload("res://scenes/Awakening/AwakeningEngine.cs")
const LAYOUT: Resource = preload("res://ShareAsset/Internal/Props/FirstAwakeningInterior/layout.tres")
const STORY: Resource = preload("res://scenes/Awakening/awakening.dialogue")
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
const POSTS := {"huo": Vector2(458, 586), "shen": Vector2(984, 726), "su": Vector2(688, 910)}
const RECALL_OBJECTS := ["E1", "E5", "E7", "E10"]
const HELP_NOTES := {
	"E4": "沈鼎：“这布是我拧的，这手法我认得。”",
	"E5": "苏泠用音叉轻敲凹痕：音色与敲桌面不同。“这里贴着空管。”",
	"E6": "苏泠读谱：“男声，很低。前两个字太轻，我听不出。”",
	"E7": "霍铎辨认：“是我的字。⊥ 是闩，✕ 是禁止……◎ 我不确定。”放大镜图样，他认不出指谁。",
	"E9": "沈鼎：“我泡茶按人放糖。”——“可我怎么会知道谁不吃糖？”",
	"E10": "沈鼎：“是我的刻法。一天一道。”",
	"E11": "三人与你比过手：半枚手印，比四个人的手都窄。",
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
var engine: Node
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

var stage := "wake"
var detective_name := "侦探"
var examined: Dictionary = {}
var experiments: Dictionary = {}
var helped: Dictionary = {}
var recalls: Dictionary = {}       # person → fragment
var recall_marks: Dictionary = {}  # 变量序号(1..6) → 回忆者
var nudged: Dictionary = {}
var flags: Dictionary = {}
var replay_open := false
var replay_vars := [-1, -1, -1, -1, -1, 0]
var replay_result: Dictionary = {}
var last_run_vars: Array = []
var replay_count := 0
var full_match_seen := false
var last_sound: Array = []
var answer_state: Dictionary = {}
var ended := false

# ============================================================== lifecycle ===

func _ready() -> void:
	TranslationServer.set_locale("zh_CN")
	texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	get_window().content_scale_size = Vector2i(1280, 992)
	get_window().content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	get_window().content_scale_stretch = Window.CONTENT_SCALE_STRETCH_INTEGER
	get_window().size = Vector2i(1280, 992)
	get_window().title = "安眠者有福 · 门的这一侧"
	engine = EngineScript.new()
	engine.name = "Engine"
	add_child(engine)
	_build_geometry()
	_build_characters()
	_build_props()
	_build_sites()
	_build_ui()
	var hints: Node2D = get_node_or_null("InteractionHints")
	if hints: hints.set("source", self)
	dialogue = $Dialogue
	dialogue.layer = 20
	dialogue.use_compact_layout()
	dialogue.single_press_keyboard_continue = true
	dialogue.apply_locale_font(Paper.FONT, null)
	dialogue.set_character_portraits(PORTRAITS)
	dialogue.panel.hide()
	var music: AudioStreamPlayer = get_node_or_null("ReasoningMusic")
	if music and music.stream:
		var loop_stream := music.stream.duplicate() as AudioStreamMP3
		loop_stream.loop = true
		music.stream = loop_stream
		music.volume_db = -14.0
		music.play()
	camera = $Camera2D
	camera.position = player.position
	camera.make_current()
	if not has_meta("skip_intro"):
		call_deferred("_begin")

func _begin() -> void:
	stage = "wake"
	story_lock = true
	await say("s1_wake")
	stage = "roam"
	story_lock = false
	_refresh_hud()

# ================================================================= geometry ==

func _build_geometry() -> void:
	layout = LAYOUT.data
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
	player.position = Vector2(154, 144)
	player.pose("idle", "front")
	player.sit_at("front", 4.0)
	# 醒来时同伴各在自己房里；相遇时才到活动厅。
	actor_of("huo").position = Vector2(480, 200)
	actor_of("shen").position = Vector2(800, 200)
	actor_of("su").position = Vector2(1120, 200)
	for key in ["huo", "shen", "su"]:
		actor_of(key).pose("idle", "front")

func actor_of(key: String) -> CharacterBody2D:
	return actors[Content.ACTOR_NODES[key]]

func _build_props() -> void:
	decals = TraceDecals.new()
	decals.name = "TraceDecals"
	decals.host = self
	decals.z_index = 1
	add_child(decals)
	move_child(decals, $ObjectsAndPeople.get_index())
	var door := $ObjectsAndPeople/MechanicalIsolationDoor
	var face := door.get_node("DoorFace") as Sprite2D
	if ResourceLoader.exists(ART + "isolation_door.png"):
		face.texture = load(ART + "isolation_door.png")
		(door.get_node("DoorGap") as Sprite2D).texture = load(ART + "isolation_door_open.png")
		door.get_node("InnerBolt").hide()
	var tube := $ObjectsAndPeople/CopperTransmitter as Sprite2D
	if ResourceLoader.exists(ART + "speaking_tube.png"):
		tube.texture = load(ART + "speaking_tube.png")
		tube.offset = Vector2(-14, -44)
	# [节点名, 贴图, 画面上的脚底位置, 遮挡排序用的 Y（放在桌面上的物件跟随桌子的前沿排序）]
	for spec in [["Slate", "slate.png", Vector2(372, 650), 682.0], ["Score", "score.png", Vector2(1022, 160), 180.0],
			["Cups", "cups.png", Vector2(862, 548), 572.0], ["Jar", "jar.png", Vector2(1128, 516), 572.0],
			["Fork", "fork.png", Vector2(714, 954), 954.0], ["Wedge", "wedge.png", Vector2(530, 952), 952.0]]:
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
	for key in ["huo", "shen", "su"]:
		sites[key] = {"person": key, "title": Content.PERSON_NAMES[key], "reach": 44.0}

# ======================================================================= UI ==

func _build_ui() -> void:
	ui = $FieldUI
	hud_title = $FieldUI/Title
	hud_hint = $FieldUI/Hint
	$FieldUI/Time.hide()
	for label in [hud_title, hud_hint]:
		label.add_theme_font_override("font", Paper.FONT)
	hud_keys = Label.new()
	hud_keys.position = Vector2(700, 20)
	hud_keys.size = Vector2(556, 30)
	hud_keys.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	hud_keys.add_theme_font_override("font", Paper.FONT)
	hud_keys.add_theme_font_size_override("font_size", 17)
	hud_keys.add_theme_color_override("font_color", Color("e8d6a7"))
	hud_keys.mouse_filter = Control.MOUSE_FILTER_IGNORE
	ui.add_child(hud_keys)
	modal_layer = CanvasLayer.new()
	modal_layer.layer = 15
	add_child(modal_layer)
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
	var keys := []
	if stage in ["roam", "investigate"]:
		keys.append("E / 点击  检视")
	if stage == "investigate":
		keys.append("Tab  笔记本")
	if replay_open:
		keys.append("R  重演")
	hud_keys.text = "　　".join(keys)

func _room_name(p: Vector2) -> String:
	if p.y < 300:
		var index := clampi(int(p.x / 320.0), 0, 3)
		return ["R-05", "R-06", "R-07", "R-08"][index]
	if p.y < 470:
		return "走廊"
	if p.y < 830:
		return "活动厅" if p.x < 640 else "备餐间"
	return "门厅"

func field_active() -> bool:
	return stage in ["roam", "investigate"] and modal_kind == "" and not dialogue.active and not story_lock and not ended

func site_visible(id: String) -> bool:
	if not sites.has(id):
		return false
	var site: Dictionary = sites[id]
	if stage == "roam":
		return id in ["E12", "E11a"]
	if stage != "investigate":
		return false
	if site.has("person"):
		return true
	return true

func site_is_new(id: String) -> bool:
	var site: Dictionary = sites[id]
	if site.has("person"):
		return false
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
		"E1": return Rect2(592, 960, 104, 46)
		"E8": return Rect2(482, 486, 32, 46)
		"E7": return Rect2(358, 632, 28, 20)
		"E6": return Rect2(1008, 146, 30, 18)
		"E9": return Rect2(842, 534, 40, 18)
		"E10": return Rect2(1118, 498, 22, 22)
		"E12": return Rect2(256, 228, 26, 54)
		"E11a", "E11b", "E11c", "E11d": return Rect2(site.point + Vector2(-22, -48), Vector2(44, 62))
		"E2": return Rect2(516, 934, 28, 22)
		"E3": return Rect2(652, 942, 24, 16)
		"E4": return Rect2(598, 952, 58, 10)
		"E5": return Rect2(702, 940, 24, 16)
		"E8j": return Rect2(566, 954, 16, 18)
	return Rect2(site.point + Vector2(-14, -22), Vector2(28, 26))

# ================================================================ movement ===

func _physics_process(delta: float) -> void:
	if not is_instance_valid(player):
		return
	if follow_camera and camera:
		camera.position = Vector2(clampf(player.position.x, 320, 960), clampf(player.position.y - 24, 248, 776)).round()
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
		if player.position.distance_to(site_point(target_id)) <= float(sites[target_id].reach) + 4.0:
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
		_move_to_click(destination)
	else:
		_approach_site(site_id)
	get_viewport().set_input_as_handled()

func _unhandled_input(event: InputEvent) -> void:
	if event is not InputEventKey or not event.pressed or event.echo:
		return
	if ended:
		if event.keycode == KEY_R:
			get_tree().reload_current_scene()
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
		_complete_click_interaction(nearest)
		get_viewport().set_input_as_handled()
	elif event.keycode == KEY_TAB and stage == "investigate":
		open_notebook()
		get_viewport().set_input_as_handled()
	elif event.keycode == KEY_R and replay_open:
		open_manuscript()
		get_viewport().set_input_as_handled()

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
	if player.position.distance_to(point) <= reach:
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
	return stage in ["investigate", "answer", "finale"]

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
	for other in actors.values():
		if other != walker and is_instance_valid(other):
			obstacles.append(Rect2(other.position.x - 8, other.position.y - 8, 16, 16))
	return obstacles

func _route(from: Vector2, to: Vector2, walker: CharacterBody2D = null) -> PackedVector2Array:
	var who := walker if walker else player
	var grid := FieldNavigation.build_route_grid(floors, _navigation_obstacles(who), Vector2i(layout.rows[0].length(), layout.rows.size()))
	return FieldNavigation.route_on_grid(from, to, grid)

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
	await actor.follow_path(route)
	actor.speed = previous

# ================================================================ triggers ===

func _check_triggers() -> void:
	if story_lock or dialogue.active or modal_kind != "":
		return
	if stage == "roam" and player.position.y > 470 and player.position.x < 608:
		_meeting()
		return
	if stage == "investigate":
		for id in RECALL_OBJECTS:
			if nudged.has(id) or not examined.has(id):
				continue
			var site_id: String = id
			if player.position.distance_to(site_point(site_id)) < 64.0:
				nudged[id] = true
				call_deferred("_nudge", id)
				return

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
func set_flag(name: String) -> void: flags[name] = true
func clear_flag(name: String) -> void: flags.erase(name)
func recalled(who: String) -> bool: return recalls.has(who)
func hooks(who: String) -> int: return int(engine.Hooks(who))
func total_hooks() -> int: return int(engine.TotalHooks())
func matched() -> bool: return full_match_seen
func replayed() -> bool: return replay_count > 0
func fragment(who: String) -> String: return str(recalls.get(who, ""))
func heard(who: String) -> String:
	for h in last_sound:
		if h.who == who:
			return str(h.text) + ("——像是从门外来的。" if h.outside else "")
	return ""
func heard_where(who: String) -> String:
	for h in last_sound:
		if h.who == who:
			return {"hall": "铜口旁", "foyer": "门边", "pantry": "备餐间", "R05": "R-05"}.get(h.room, "")
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
	await say("s2_meet")
	await _ask_name()
	await say("s2_tea")
	await say("s3_rewards")
	await say("s4_question")
	if flags.has("rest_chosen"):
		await _rest()
		return
	stage = "investigate"
	for key in POSTS:
		walk_actor(key, POSTS[key])
	await say("s5_begin")
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
	var root := Control.new()
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.theme = Paper.theme()
	modal_layer.add_child(root)
	modal_root = root
	var dim := ColorRect.new()
	dim.color = Color(0.03, 0.03, 0.05, 0.55)
	dim.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.add_child(dim)
	var panel := PanelContainer.new()
	panel.add_theme_stylebox_override("panel", Paper.sheet())
	panel.position = (Vector2(1280, 992) - size) / 2.0
	panel.size = size
	panel.custom_minimum_size = size
	root.add_child(panel)
	var column := VBoxContainer.new()
	column.add_theme_constant_override("separation", 12)
	panel.add_child(column)
	if not title.is_empty():
		column.add_child(Paper.label("[b]" + title + "[/b]", 25))
		column.add_child(Paper.rule())
	var scroll := ScrollContainer.new()
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	column.add_child(scroll)
	var body := VBoxContainer.new()
	body.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	body.add_theme_constant_override("separation", 10)
	scroll.add_child(body)
	root.set_meta("column", column)
	return body

func add_footer(actions: Array) -> HBoxContainer:
	var column: VBoxContainer = modal_root.get_meta("column")
	column.add_child(Paper.rule())
	var footer := HBoxContainer.new()
	footer.add_theme_constant_override("separation", 12)
	footer.alignment = BoxContainer.ALIGNMENT_END
	column.add_child(footer)
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
	if not sites.has(id):
		return
	var site: Dictionary = sites[id]
	if site.has("person"):
		await talk(site.person)
		return
	if id == "E8":
		open_tube_menu()
		return
	open_card(site.evidence)

func examine(id: String) -> void:
	if examined.has(id):
		return
	examined[id] = true
	engine.Examine("detective", id)
	if id == "E1" and not replay_open:
		call_deferred("_unlock_replay")

func open_card(id: String) -> void:
	examine(id)
	var ev: Dictionary = Content.EVIDENCE[id]
	var body := open_modal(ev.name, Vector2(900, 660), "card")
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 22)
	body.add_child(row)
	var picture_path := ART + "card_" + str(ev.picture) + ".png"
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
	text.add_theme_constant_override("separation", 10)
	row.add_child(text)
	var facts := str(ev.facts)
	if id == "E12" and examined.has("E7"):
		facts += "\n这个圆框带短柄的样子，与石板第二行的小图相同。"
	text.add_child(Paper.label(facts, 20))
	text.add_child(Paper.label("来源：" + str(ev.source), 16, Paper.MUTED))
	var notes := _card_notes(id)
	if not notes.is_empty():
		body.add_child(Paper.rule())
		for note in notes:
			body.add_child(Paper.label(note, 18, Paper.SEAL if note.begins_with("回忆") else Paper.INK))
	var actions := []
	if ev.has("experiment"):
		actions.append([str(ev.experiment) + ("（再做一次）" if experiments.has(id) and id == "E8" else ""), _on_experiment.bind(id), experiments.has(id) and id != "E8"])
	if ev.has("help") and not helped.has(id):
		actions.append(["请%s看看" % Content.PERSON_NAMES[ev.help], _on_help.bind(id)])
	if ev.has("recall") and not recalls.has(ev.recall):
		var who: String = ev.recall
		actions.append(["触碰插闩，试着回想" if who == "detective" else "请%s触碰它，回想" % Content.PERSON_NAMES[who], _on_recall.bind(id)])
	actions.append(["返回现场", close_modal])
	add_footer(actions)

func _card_notes(id: String) -> Array:
	var notes := []
	var ev: Dictionary = Content.EVIDENCE[id]
	if experiments.has(id):
		if id == "E8":
			notes.append("声路试验：" + "；".join(last_sound.map(func(h): return "%s（%s）听到 %s%s" % [Content.PERSON_NAMES[h.who], heard_where(h.who), h.text, "，像在门外" if h.outside else ""])))
		elif id == "E11":
			notes.append(HELP_NOTES.E11)
		else:
			notes.append("试验：" + str(ev.experiment_result).replace("\n", ""))
	if helped.has(id) and HELP_NOTES.has(id):
		notes.append(HELP_NOTES[id])
	if ev.has("recall") and recalls.has(ev.recall):
		notes.append("回忆（%s）：%s" % [Content.PERSON_NAMES[ev.recall], recalls[ev.recall]])
	return notes

func _on_experiment(id: String) -> void:
	close_modal()
	match id:
		"E8":
			await sound_test()
		"E11":
			for who in ["huo", "shen", "su"]:
				await _summon(who)
			await say("help_E11")
			experiments[id] = true
		"E3":
			await say("exp_E3")
			experiments[id] = true
			if not flags.has("reversal_one"):
				flags["reversal_one"] = true
				await say("s6_reversal")
		_:
			await say("exp_" + id)
			experiments[id] = true
	open_card(id)

func _on_help(id: String) -> void:
	close_modal()
	var who: String = Content.EVIDENCE[id].help
	await _summon(who)
	await say("help_" + id)
	helped[id] = true
	open_card(id)

func _summon(who: String) -> void:
	var actor := actor_of(who)
	if actor.position.distance_to(player.position) > 90:
		for offset in [Vector2(30, 4), Vector2(-30, 4), Vector2(0, -28), Vector2(48, -10), Vector2(-48, -10), Vector2(0, 30)]:
			var spot: Vector2 = player.position + offset
			var taken := false
			for other in actors.values():
				if other != actor and other.position.distance_to(spot) < 18:
					taken = true
			if _walkable(spot) and not taken:
				actor.position = spot
				break
	actor.face_point(player.position)
	player.face_point(actor.position)

func _on_recall(id: String) -> void:
	close_modal()
	var who: String = Content.EVIDENCE[id].recall
	if who != "detective":
		await _summon(who)
	flags.erase("recall_yes")
	await say("recall_ask_" + who)
	if flags.has("recall_yes"):
		var memory: Dictionary = engine.Recall(id)
		if not memory.is_empty():
			recalls[who] = memory.fragment
			for variable in memory.confirms:
				recall_marks[int(variable)] = who
			await say("recall_" + who)
	open_card(id)

func _unlock_replay() -> void:
	if replay_open:
		return
	replay_open = true
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
		body.add_child(Paper.label("[b]" + place + "[/b]", 21, Paper.SEAL))
		var grid := GridContainer.new()
		grid.columns = 3
		grid.add_theme_constant_override("h_separation", 10)
		grid.add_theme_constant_override("v_separation", 8)
		body.add_child(grid)
		for id in ids:
			var mark := ""
			if experiments.has(id) or helped.has(id):
				mark = "　·已试"
			grid.add_child(Paper.button(str(Content.EVIDENCE[id].name) + mark, open_card.bind(id), 18))
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
	await stage_node.play(result.frames)
	stage = "investigate"
	story_lock = false
	await after_replay(result)

func after_replay(result: Dictionary) -> void:
	close_modal()
	if bool(result.full_match) and not full_match_seen:
		full_match_seen = true
		for who in ["huo", "shen", "su"]:
			await _summon(who)
		await say("s8_reversal")
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
	body.add_child(Paper.label("请铜口说一句话。你和三位同伴分别站在铜口旁、门边、备餐间、R-05 听。", 19))
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
	var places := [["foyer", "门边"], ["hall", "铜口旁"], ["pantry", "备餐间"], ["R05", "R-05"]]
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
	# 同伴补上其余位置：苏泠优先门边，霍铎优先铜口旁，沈鼎优先备餐间。
	var free := ["hall", "foyer", "pantry", "R05"]
	free.erase(_sound_choice[1])
	var positions := {"detective": _sound_choice[1]}
	for pair in [["su", "foyer"], ["huo", "hall"], ["shen", "pantry"]]:
		var spot: String = pair[1] if free.has(pair[1]) else free[0]
		positions[pair[0]] = spot
		free.erase(spot)
	last_sound = engine.SoundTest(_sound_choice[0], positions)
	experiments["E8"] = true
	set_meta("sound_sentence", sentences[_sound_choice[0]])
	await say("sound_test")
	open_card("E8")

func sound_sentence() -> String:
	return str(get_meta("sound_sentence", ""))

# ==================================================================== talk ===

func talk(who: String) -> void:
	actor_of(who).face_point(player.position)
	player.face_point(actor_of(who).position)
	await say("talk_" + who)

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
	flags.erase("rest_chosen")
	await say("rest_confirm")
	if flags.has("rest_chosen"):
		await _rest()

# ================================================================== answer ===

func statements() -> Array:
	var list := []
	if flags.has("reversal_one") or (not replay_result.is_empty() and bool(replay_result.get("detective_bolted", false))):
		list.append("me")
	for i in 6:
		if int(replay_vars[i]) >= 0 and replay_count > 0:
			list.append("V%d_%d" % [i + 1, int(replay_vars[i])])
	return list

func answer() -> void:
	stage = "answer"
	answer_state = {"step": 1, "said_trust": false, "shown_familiar": false}
	await say("answer_open")
	while answer_state.step <= 4:
		await say("answer_q%d" % answer_state.step)
		var pick: Dictionary = await _answer_pick()
		if pick.is_empty():
			await say("answer_pause")
			stage = "investigate"
			_refresh_hud()
			return
		var verdict := _judge(answer_state.step, pick)
		await say(verdict)
		if verdict == "answer_ok%d" % answer_state.step:
			answer_state.step += 1
	# 第 5 步：反问，只在重演全部吻合（V6=早已相熟）之后出现，由玩家主动发起。
	if full_match_seen:
		flags.erase("reverse_yes")
		await say("answer_offer_reverse")
		if flags.has("reverse_yes"):
			while true:
				var pick: Dictionary = await _answer_pick(true)
				if pick.is_empty():
					break
				if pick.kind == "show" and pick.id in ["E10", "E11"]:
					flags["asked_reverse"] = true
					await say("answer_reverse")
					break
				await say("answer_reverse_wrong")
	await finale()

## 底部两个按钮：陈述 / 出示。返回 {kind, id}；空字典表示暂时不答。
var _answer_list: VBoxContainer

func _answer_pick(reverse := false) -> Dictionary:
	_modal_result = null
	var body := open_modal("始业之答" if not reverse else "反问", Vector2(860, 640), "answer")
	body.add_child(Paper.label("铜口在等你回答。陈述一句，或出示一件东西。" if not reverse else "你说：“你说我们‘初涉斯地’。”——拿出一件东西。", 20))
	_answer_list = VBoxContainer.new()
	_answer_list.add_theme_constant_override("separation", 8)
	body.add_child(_answer_list)
	var footer_actions := []
	if not reverse:
		footer_actions.append(["陈述", _answer_show_list.bind("say")])
	footer_actions.append(["出示", _answer_show_list.bind("show")])
	footer_actions.append(["暂且不答" if not reverse else "算了", _modal_choose.bind({})])
	add_footer(footer_actions)
	if reverse:
		_answer_show_list("show")
	while _modal_result == null:
		await get_tree().process_frame
	return _modal_result

func _answer_show_list(kind: String) -> void:
	for child in _answer_list.get_children():
		child.queue_free()
	if kind == "say":
		var options := statements()
		if options.is_empty():
			_answer_list.add_child(Paper.label("你还没有可以陈述的结论。重演一次，设定过的处境就能说出口。", 18, Paper.MUTED))
		for key in options:
			_answer_list.add_child(Paper.button(Content.STATEMENTS[key], _modal_choose.bind({"kind": "say", "id": key}), 19))
	else:
		if examined.is_empty():
			_answer_list.add_child(Paper.label("笔记本里还没有东西。", 18, Paper.MUTED))
		for id in Content.EVIDENCE:
			if examined.has(id):
				_answer_list.add_child(Paper.button(Content.EVIDENCE[id].name, _modal_choose.bind({"kind": "show", "id": id}), 19))

func _judge(step: int, pick: Dictionary) -> String:
	var kind: String = pick.kind
	var id: String = pick.id
	match step:
		1:
			if kind == "say" and id == "me": return "answer_ok1"
			if kind == "show" and id == "E3": return "answer_ok1"
			if kind == "show" and id in ["E12", "E7"] and examined.has("E12") and examined.has("E7"): return "answer_ok1"
		2:
			if kind == "show" and id == "E8" and experiments.has("E8"): return "answer_ok2"
			if kind == "show" and id == "E5": return "answer_ok2"
			if kind == "say" and id == "V3_0": return "answer_ok2"
			if kind == "show" and id == "E8": return "answer_need_test"
		3:
			if kind == "show" and id in ["E7", "E6"]: return "answer_ok3"
			if kind == "say" and id == "V4_0": return "answer_ok3"
		4:
			if kind == "say" and id == "V5_0":
				answer_state.said_trust = true
			elif kind == "show" and id in ["E9", "E10", "E11", "E7"]:
				answer_state.shown_familiar = true
			else:
				return _wrong(step, kind, id)
			if answer_state.said_trust and answer_state.shown_familiar:
				return "answer_ok4"
			return "answer_half4_trust" if answer_state.said_trust else "answer_half4_shown"
	return _wrong(step, kind, id)

func _wrong(_step: int, kind: String, id: String) -> String:
	if kind == "show":
		set_meta("wrong_name", Content.EVIDENCE[id].name)
		return "answer_wrong_show"
	set_meta("wrong_name", Content.STATEMENTS[id])
	return "answer_wrong_say"

func wrong_name() -> String:
	return str(get_meta("wrong_name", ""))

# ================================================================== endings ==

func finale() -> void:
	stage = "finale"
	story_lock = true
	follow_camera = false
	camera.position = Vector2(644, 862)
	await say("s10_steps")
	var door := $ObjectsAndPeople/MechanicalIsolationDoor
	door.get_node("DoorFace").hide()
	door.get_node("DoorGap").show()
	flags["door_open"] = true
	decals.queue_redraw()
	await walk_actor("su", player.position + Vector2(30, 0))
	await say("s10_open")
	_save_chapter_end(false)
	_ending_card("门的这一侧 · 完", "主楼的门开了。")

func _rest() -> void:
	stage = "finale"
	story_lock = true
	await say("ending_rest")
	_save_chapter_end(true)
	_ending_card("门的这一侧 · 歇息", "你们留在生活翼。门一直闩着。")

func _save_chapter_end(rested: bool) -> void:
	var data: Dictionary = engine.ChapterEnd(examined.keys(), full_match_seen, flags.has("asked_reverse"), rested)
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
	body.add_child(Paper.label("按 R 重新开始。", 17, Paper.MUTED))
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
			player.position = Vector2(640, 920)
		"reversal_one":
			jump_to("investigate")
			for id in ["E1", "E2", "E3"]:
				examine(id)
			replay_open = true
		"replay_match":
			jump_to("reversal_one")
			for id in Content.EVIDENCE:
				examine(id)
			experiments["E8"] = true
			flags["reversal_one"] = true
			run_replay([0, 0, 0, 0, 0, 1])
			full_match_seen = bool(replay_result.full_match)
		"answer_step5":
			jump_to("replay_match")
			answer_state = {"step": 5, "said_trust": true, "shown_familiar": true}
			stage = "answer"
	_refresh_hud()
