extends "res://scenes/Chapter2/Field/Chapter2Field.gd"
## 第二章 Godot 宿主：场景几何、输入与语义事件显示。
## 操作：点击或 WASD 走动，悬停描出可交互的物件与人；E / 空格 / Enter 检视或交谈；Tab 笔记本；R 重演手稿；
## M 学园图（直接去往某处）；T 让这一阵过去。读不耗时；写（请人亲眼看、讲规矩、请人做事、帮人、分口粮）做完即过去一阵。
## Chapter2Engine.Dispatch 连接无 Godot 的章节会话。
## 剧情候选、行动与章节推进由 C# 返回，宿主不核验剧情条件。

const UI_SCENES := {
	"AnswerHeader": preload("res://scenes/Chapter2/UI/AnswerHeader.tscn"),
	"AnswerList": preload("res://scenes/Chapter2/UI/AnswerList.tscn"),
	"ChoiceOption": preload("res://scenes/Chapter2/UI/ChoiceOption.tscn"),
	"ChoiceList": preload("res://scenes/Chapter2/UI/ChoiceList.tscn"),
	"Confirm": preload("res://scenes/Chapter2/UI/Confirm.tscn"),
	"Ending": preload("res://scenes/Chapter2/UI/Ending.tscn"),
	"EvidenceCard": preload("res://scenes/Chapter2/UI/EvidenceCard.tscn"),
	"FlashTitle": preload("res://scenes/Chapter2/UI/FlashTitle.tscn"),
	"Footer": preload("res://scenes/Chapter2/UI/Footer.tscn"),
	"Notebook": preload("res://scenes/Chapter2/UI/Notebook.tscn"),
}

const Paper := preload("res://scenes/Shared/UI/Paper.gd")
const Sound := preload("res://scenes/Shared/Audio/Sound.gd")
const FieldNavigation := preload("res://scenes/Shared/FieldNavigation.gd")
const HintOverlay := preload("res://scenes/Shared/UI/HintOverlay.tscn")
const ReplayManuscript := preload("res://scenes/Shared/ReplayManuscript.tscn")
const CampusMap := preload("res://scenes/Chapter2/Game/CampusMap.gd")
const CAMPUS_MAP_SCENE := preload("res://scenes/Chapter2/Game/CampusMap.tscn")
const SightMask := preload("res://scenes/Shared/SightMask.tscn")
const PresentationWait := preload("res://scenes/Shared/PresentationWait.gd")
const EventView := preload("res://scenes/Chapter2/Game/Chapter2EventView.gd")
const EngineScript := preload("res://scenes/Chapter2/Game/Chapter2Engine.cs")
const FRAMES_DIR := "res://ShareAsset/Internal/Characters/AnimeCast/%s.tres"

const SPRITES := {
	"Det": "detective", "Huo": "engineer", "Shen": "chef", "Su": "musician", "Lucian": "poet", "Gu": "archaeologist",
	"Quer": "thief", "Ward": "lawyer", "Selma": "naturalist", "Ruan": "chessplayer", "Elena": "dancer", "Moran": "painter",
	"Veronica": "chemist", "Bertrand": "marksman", "Margaret": "architect", "Mora": "doctor",
}
const NAMES := {
	"Huo": "霍铎", "Shen": "沈鼎", "Su": "苏泠", "Lucian": "卢西安", "Gu": "顾平章", "Quer": "雀儿", "Ward": "沃德", "Selma": "赛尔玛",
	"Ruan": "阮清越", "Elena": "伊莲娜", "Moran": "莫兰", "Veronica": "维罗妮卡", "Bertrand": "贝尔特朗", "Margaret": "玛格丽特", "Mora": "茉拉",
}
## 引擎的地点 → 人物站在哪张地图上。
const PLACE_MAP := {
	"Hall": "central_hall", "Lecture": "lecture", "Logistics": "logistics", "Clinic": "clinic", "Greenhouse": "greenhouse",
	"ArchiveFront": "archive", "Oath": "campus_s", "DormLife": "zone_yi", "DormW": "zone_jia", "DormS": "zone_bing", "DormL": "zone_ding",
}
## 地图 → 侦探在引擎里的地点（室外过道不改）。
const MAP_PLACE := {
	"central_hall": "Hall", "lecture": "Lecture", "logistics": "Logistics", "granary": "Logistics", "clinic": "Clinic",
	"greenhouse": "Greenhouse", "archive": "ArchiveFront", "counterweight": "ArchiveFront", "campus_s": "Oath", "living_hall": "DormLife",
	"zone_yi": "DormLife", "zone_jia": "DormW", "zone_bing": "DormS", "zone_ding": "DormL",
}
const SPAWN_OF := {
	"central_hall": "south", "lecture": "from_central", "logistics": "west", "granary": "south", "clinic": "south", "greenhouse": "south",
	"archive": "south", "campus_s": "gate_inside", "living_hall": "north_door", "zone_yi": "from_hall", "zone_jia": "from_hall",
	"zone_bing": "from_hall", "zone_ding": "from_hall", "resonance": "south", "counterweight": "from_stairs",
}
## 各时段的配乐：清晨与上午是船歌式日常变奏，午间丝竹式，下午木卡姆式，傍晚拉格式，夜里八音盒。
const SLOT_TINT := [Color(1.0, 0.96, 0.9), Color(1, 1, 1), Color(1, 1, 0.97), Color(1.0, 0.95, 0.86), Color(0.96, 0.82, 0.7), Color(0.45, 0.5, 0.68)]

## 地图上可检视之处。thing：引擎里的物件（细看即亲见）；lore：只看的卡片；kind：宿主自有的交互。
## 地图里没有标记、由宿主补上的地点。
const EXTRA_SITES := {
	"zone_yi": {"bed": {"point": Vector2(858, 196)}},
	"zone_jia": {"litter": {"point": Vector2(440, 240)}},
}
# ------------------------------------------------------------------ field ---
var engine: Node
var dialogue: CanvasLayer
var hints: Node2D
var grid: AStarGrid2D
var sites: Dictionary = {}
var actors: Dictionary = {}
var _walking_actors: Array[CharacterBody2D] = []
var nearest := ""
var hovered := ""
var click_path := PackedVector2Array()
var click_waypoint := 0
var click_target := ""
var story_lock := false
var tint: CanvasModulate
var sight: Node2D
var _walls: Dictionary = {}           # 地图 → {挡视线的格}
var _seen: Dictionary = {}            # 地图 → {看见过的格}
var known_places: Dictionary = {}   # 侦探去过或听人提到过的地方（学园图只画这些）

# ------------------------------------------------------------------ story ---
var detective_name := "侦探"
var stage := "intro"
var examined: Dictionary = {}       # 卡片 id → {title, text, place, thing?}
var people: Array = []
var talked: Dictionary = {}         # key → 最近一次问到的话
var journal: Array = []             # [[时段, 行...]]
var replay_open := false
var replay_vars := [-1, -1, -1, -1]
var replay_result: Dictionary = {}
var last_run_vars: Array = []
var ended := false
var startup_error := ""

# ------------------------------------------------------------------ ui ---
var ui: CanvasLayer
var hud_title: Label
var hud_keys: Label
var hud_hint: Label
var hover_tip: Label
var aside: Label
var aside_until := 0
var fade: ColorRect
var modal_layer: CanvasLayer
var modal_root: Control
var modal_kind := ""

# ============================================================== lifecycle ===

func _ready() -> void:
	TranslationServer.set_locale("zh_CN")
	get_window().content_scale_size = Vector2i(1280, 992)
	get_window().content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	get_window().content_scale_stretch = Window.CONTENT_SCALE_STRETCH_INTEGER
	get_window().size = Vector2i(1280, 992)
	get_window().title = "安眠者有福 · 第二章"
	start_map = "living_hall"
	start_spawn = "east_south"
	engine = EngineScript.new()
	engine.name = "Engine"
	add_child(engine)
	_build_ui()
	var started: Dictionary = engine.TryStart()
	if not bool(started.ok):
		startup_error = str(started.error)
		set_process(false)
		set_physics_process(false)
		set_process_input(false)
		set_process_unhandled_input(false)
		var body := open_modal("第二章尚未开启", Vector2(760, 340), "startup")
		body.add_child(Paper.label(startup_error, 20))
		add_footer([["返回标题", func(): get_tree().change_scene_to_file("res://scenes/Title/Title.tscn")]])
		return
	_apply_snapshot(engine.Snapshot())
	super()
	place_label.visible = false
	player.speed = 130.0
	player.collision_mask = 1
	tint = CanvasModulate.new()
	add_child(tint)
	sight = SightMask.instantiate()
	sight.z_index = 40
	sight.host = self
	add_child(sight)
	_enter_sight()
	hints = HintOverlay.instantiate()
	hints.z_index = 50
	add_child(hints)
	hints.set("source", self)
	dialogue = $Dialogue
	dialogue.layer = 20
	dialogue.use_compact_layout()
	dialogue.single_press_keyboard_continue = true
	dialogue.dim_visited_responses = true
	dialogue.apply_locale_font(Paper.font(), null)
	var portraits := {"侦探": "detective", "工程师": "engineer", "厨师": "chef", "音乐家": "musician"}
	for key in NAMES:
		if not key in ["Huo", "Shen", "Su"]:
			portraits[NAMES[key]] = SPRITES[key]
	dialogue.set_dialogue_cast(portraits)
	dialogue.panel.hide()
	event_view = EventView.new()
	event_view.host = self
	add_child(event_view)
	add_to_group("lifecycle_hosts")
	Sound.music("day", 2.5)
	if not has_meta("skip_intro"):
		call_deferred("_begin")

var event_view: Node
var _event_queue: Array = []
var _draining_events := false
var _closing := false
var _applying_scene := false
var _court_layout := false
var _fade_tweens: Array[Tween] = []
var _playback_view: Control
var _replay_playing := false
signal closing

func _begin() -> void:
	await _dispatch({"kind": "begin"})

func upcoming_day() -> int: return int(engine.NextDay())
func upcoming_slot() -> int: return int(engine.NextSlot())
func clock_text() -> String: return str(engine.ClockText())

func _apply_snapshot(value: Dictionary) -> void:
	for key in ["stage", "ended", "replay_open", "detective_name", "people", "known_places", "examined", "talked", "journal", "replay_result"]:
		if value.has(key): set(key, value[key])
	if is_instance_valid(tint): tint.color = SLOT_TINT[int(engine.SlotIndex())]
	_refresh_hud()

func _dispatch(input: Dictionary) -> void:
	if _closing: return
	var visible: Array = []
	var nearby: Array = []
	for key in actors:
		var actor: CharacterBody2D = actors[key]
		if not is_instance_valid(actor) or not actor.visible: continue
		visible.append(str(key))
		if actor.position.distance_to(player.position) < 220.0: nearby.append(str(key))
	input["visible_people"] = visible
	input["nearby_people"] = nearby
	_event_queue.append_array(engine.Dispatch(input))
	await _drain_events()

func _drain_events() -> void:
	if _draining_events or _closing: return
	_draining_events = true
	story_lock = true
	while not _event_queue.is_empty() and not _closing:
		var packet: Dictionary = _event_queue.pop_front()
		if str(packet.kind) == "state":
			_apply_snapshot(packet.snapshot)
			if _court_layout: _place_court_actors()
			else: _spawn_actors(true)
		else:
			await event_view.execute(packet)
			if _closing: break
			if str(packet.kind) == "lines":
				_event_queue.append_array(engine.Dispatch({"kind": "presented", "presentation_id": str(packet.presentation_id)}))
	story_lock = false
	_draining_events = false
	_refresh_hud()

func _choose_event(id: String, token: String) -> void:
	close_modal()
	await _dispatch({"kind": "select", "choice_id": id, "token": token})

func commit(act: Dictionary) -> void:
	if not field_active(): return
	await _dispatch({"kind": "action", "act": act})

func _wait_signal(source: Signal) -> void:
	if _closing: return
	await PresentationWait.until(source, closing)

func begin_shutdown() -> void:
	if _closing: return
	_closing = true
	if is_instance_valid(event_view): event_view.begin_shutdown()
	if is_instance_valid(_playback_view): _playback_view.cancel_playback()
	closing.emit()
	for tween in _fade_tweens:
		if is_instance_valid(tween): tween.kill()
	_fade_tweens.clear()
	_event_queue.clear()
	var stopping: Array = actors.values() + _walking_actors
	for actor in stopping:
		if is_instance_valid(actor):
			actor.travelling = false
			actor.velocity = Vector2.ZERO
			actor.arrived.emit()
	_cancel_click()

func _exit_tree() -> void:
	begin_shutdown()

# ================================================================= map & people ==

func load_semantic_scene(packet: Dictionary) -> void:
	_court_layout = str(packet.get("mode", "")) == "court"
	_applying_scene = true
	await _fade_to(func(): load_map(str(packet.map), str(packet.get("spawn", ""))))
	_applying_scene = false

func load_map(key: String, spawn: String, at := Vector2.ZERO, use_at := false) -> void:
	_cancel_click()
	for moving in _walking_actors.duplicate():
		if is_instance_valid(moving):
			moving.travelling = false
			moving.velocity = Vector2.ZERO
			moving.arrived.emit()
	for a in actors.values():
		if is_instance_valid(a):
			a.queue_free()
	actors.clear()
	super(key, spawn, at, use_at)
	_build_grid()
	_enter_sight()
	if not _applying_scene and MAP_PLACE.has(key) and engine and engine.Started():
		_event_queue.append_array(engine.Dispatch({"kind": "move", "place": MAP_PLACE[key], "map": key, "node": _map_node()}))
	_spawn_actors(false)
	_refresh_hud()

## 碰撞体（墙与家具）栅格化为寻路网格：半格精度，与第一章的规则相同（人物脚底留出余量）。每张图只建一次。
var _grids: Dictionary = {}

func _build_grid() -> void:
	var key := String(map.name)
	if _grids.has(key):
		grid = _grids[key]
		return
	var size: Vector2 = map.get_meta("map_size", Vector2(1024, 1024))
	grid = AStarGrid2D.new()
	grid.region = Rect2i(0, 0, int(size.x / 16.0), int(size.y / 16.0))
	grid.cell_size = Vector2(16, 16)
	grid.offset = Vector2(8, 8)
	grid.diagonal_mode = AStarGrid2D.DIAGONAL_MODE_NEVER
	grid.update()
	_grids[key] = grid
	var solids := map.get_node_or_null("Solids")
	if solids == null:
		return
	for shape_node in solids.get_children():
		var cs := shape_node as CollisionShape2D
		if cs == null or not (cs.shape is RectangleShape2D):
			continue
		var half: Vector2 = (cs.shape as RectangleShape2D).size / 2.0
		var rect := Rect2(cs.position - half, half * 2.0).grow_individual(10, 1, 10, 14)
		# 中心落在矩形里的格子：x 从 ceil((左-8)/16) 到 floor((右-8)/16)。
		var x0 := maxi(0, int(ceil((rect.position.x - 8.0) / 16.0)))
		var y0 := maxi(0, int(ceil((rect.position.y - 8.0) / 16.0)))
		var x1 := mini(grid.region.size.x - 1, int(floor((rect.end.x - 8.0) / 16.0)))
		var y1 := mini(grid.region.size.y - 1, int(floor((rect.end.y - 8.0) / 16.0)))
		if x1 >= x0 and y1 >= y0:
			grid.fill_solid_region(Rect2i(x0, y0, x1 - x0 + 1, y1 - y0 + 1), true)

## 室外楼体挡人物视线；室内按当前房间显示，访问记忆按地图永久保留。
func _enter_sight() -> void:
	if sight == null or map == null:
		return
	var key := String(map.name)
	if map.has_meta("outdoor_core") and not _walls.has(key):
		var walls := {}
		var outdoor := map.has_meta("outdoor_core")
		var solids := map.get_node_or_null("Solids")
		if solids:
			for shape_node in solids.get_children():
				var cs := shape_node as CollisionShape2D
				if cs == null or not (cs.shape is RectangleShape2D):
					continue
				var size: Vector2 = (cs.shape as RectangleShape2D).size
				var corner: Vector2 = cs.position - size / 2.0
				var wall := fmod(corner.x, 32.0) == 0.0 and fmod(corner.y, 32.0) == 0.0 and fmod(size.x, 32.0) == 0.0 and fmod(size.y, 32.0) == 0.0
				if not wall and not (outdoor and size.x * size.y >= 4096.0):
					continue
				for y in range(int(ceil((corner.y - 16.0) / 32.0)), int(floor((corner.y + size.y - 16.0) / 32.0)) + 1):
					for x in range(int(ceil((corner.x - 16.0) / 32.0)), int(floor((corner.x + size.x - 16.0) / 32.0)) + 1):
						walls[Vector2i(x, y)] = true
		_walls[key] = walls
	if not _seen.has(key):
		_seen[key] = {}
	sight.reset()
	sight.seen_cells = _seen[key]

func sight_origin() -> Vector2: return player.position + Vector2(0, -8)
func sight_entry_origin() -> Vector2: return player.position
func sight_interior(cell: Vector2i) -> bool:
	var rows: Array = map.get_meta("terrain_rows", [])
	if cell.y < 0 or cell.y >= rows.size():
		return false
	var row: String = rows[cell.y]
	# S 是房间内部台阶；门道 D、墙顶 #、墙面 W/w 与 G 均不触发进入。
	return cell.x >= 0 and cell.x < row.length() and row.substr(cell.x, 1) in [".", "S"]

func sight_size() -> Vector2i:
	var size: Vector2 = map.get_meta("map_size", Vector2(1024, 1024))
	return Vector2i(ceili(size.x / 32.0), ceili(size.y / 32.0))
func sight_blocks(cell: Vector2i) -> bool: return _walls.get(String(map.name), {}).has(cell)
func sight_terrain_fog() -> bool: return not map.has_meta("outdoor_core")

func sight_region(cell: Vector2i) -> String:
	var key := String(map.name)
	# 分区坐标直接对应 chapter2_art/rooms.py 的原生房间格；东区为水平镜像。
	if key in ["zone_yi", "zone_jia", "zone_ding", "zone_bing"]:
		var x := 35 - cell.x if key in ["zone_yi", "zone_ding"] else cell.x
		if x >= 30:
			return "wc" if cell.y < 7 else ("store" if cell.y >= 14 else "foyer")
		if x >= 22:
			return "hall" if cell.y < 14 else "prep"
		if cell.y >= 9 and cell.y < 14:
			return "corridor"
		return ("north_" if cell.y < 9 else "south_") + str(clampi(floori(float(x) / 7.0), 0, 2))
	match key:
		"living_hall": return "bath" if cell.y >= 32 else "hall"
		"archive":
			if cell.x >= 16: return "works"
			return "vault" if cell.y < 9 else "foyer"
		"resonance": return "silent" if cell.x >= 22 else "hall"
		"clinic": return "safety" if cell.x >= 20 else "ward"
		"logistics": return "kitchen" if cell.x < 13 else "dining"
	return key
func in_sight(point: Vector2) -> bool:
	return sight == null or sight.can_see(point if sight_terrain_fog() else point + Vector2(0, -8))

func _walkable(p: Vector2) -> bool:
	var c := Vector2i(p / 16.0)
	return grid != null and grid.region.has_point(c) and not grid.is_point_solid(c)

## 人物各在自己的去处：此处的人站到各自的位置，带着刚才在做的事。animate 时，新来的从门口走进来，离开的走向门口。
func _spawn_actors(animate: bool) -> void:
	if map == null:
		return
	var here := String(MAP_PLACE.get(String(map.name), ""))
	var shown := {}
	if here != "" and not (String(map.name) in ["living_hall", "granary", "counterweight"]):
		for p in people:
			if str(p.place) == here and bool(p.get("present", true)):
				shown[str(p.key)] = p
	var stands: Array = []
	var stand_node := map.get_node_or_null("Stands")
	if stand_node:
		for m in stand_node.get_children():
			stands.append((m as Marker2D).position)
	var door: Vector2 = player.position
	var spawns := map.get_node_or_null("Spawns")
	if spawns and spawns.get_child_count() > 0:
		door = (spawns.get_child(0) as Marker2D).position
	for key in actors.keys():
		if not shown.has(key):
			var leaving: CharacterBody2D = actors[key]
			actors.erase(key)
			if animate and is_instance_valid(leaving):
				_walk_off(leaving, door)
			elif is_instance_valid(leaving):
				leaving.queue_free()
	var index := 0
	for key in shown:
		var p: Dictionary = shown[key]
		var spot: Vector2 = stands[(absi(key.hash()) + index) % stands.size()] if not stands.is_empty() else player.position + Vector2(40 * index, 30)
		for other in actors.values():
			if other.position.distance_to(spot) < 20:
				spot += Vector2(26, 8)
		index += 1
		var actor: CharacterBody2D = actors.get(key)
		if actor == null:
			actor = _make_actor(key)
			if animate:
				actor.position = door
				_walk_on(actor, spot, str(p.frame))
			else:
				actor.position = spot
				_pose(actor, str(p.frame))
		else:
			_pose(actor, str(p.frame))
	_build_sites()

func _make_actor(key: String) -> CharacterBody2D:
	var actor := CharacterBody2D.new()
	actor.set_script(ACTOR)
	var path := FRAMES_DIR % SPRITES.get(key, "detective")
	actor.standard = load(path) if ResourceLoader.exists(path) else FRAMES
	actor.name = key
	map.get_node("Objects").add_child(actor)
	actor.collision_mask = 1
	actor.speed = 110.0
	actors[key] = actor
	return actor

func _walk_on(actor: CharacterBody2D, spot: Vector2, frame: String) -> void:
	if _closing: return
	_walking_actors.append(actor)
	var route := FieldNavigation.route_on_grid(actor.position, spot, grid) if grid else PackedVector2Array()
	if route.is_empty():
		actor.position = spot
	else:
		route.append(spot)
		await actor.follow_path(route)
	_walking_actors.erase(actor)
	if not _closing and is_instance_valid(actor):
		_pose(actor, frame)

func _walk_off(actor: CharacterBody2D, door: Vector2) -> void:
	if _closing: return
	_walking_actors.append(actor)
	var route := FieldNavigation.route_on_grid(actor.position, door, grid) if grid else PackedVector2Array()
	if not route.is_empty():
		await actor.follow_path(route)
	_walking_actors.erase(actor)
	if is_instance_valid(actor):
		actor.queue_free()

## 动作帧类别（推演给出）落到图集动画。
func _pose(actor: CharacterBody2D, frame: String) -> void:
	actor.pose({"操作": "interact", "递物": "interact", "拉拽": "interact", "交谈": "talk", "蹲查": "inspect"}.get(frame, "idle"), "front")

func person(key: String) -> Dictionary:
	for p in people:
		if str(p.key) == key:
			return p
	return {}

# ================================================================= sites ===

func _build_sites() -> void:
	sites.clear()
	if map == null:
		return
	var key := String(map.name)
	var defs: Dictionary = engine.SiteDefinitions(key)
	var marker_root := map.get_node_or_null("Sites")
	if marker_root:
		for m in marker_root.get_children():
			var id := String(m.name)
			var def: Dictionary = defs.get(id, {}).duplicate()
			if def.is_empty():
				if id in ["transmitter", "isolation_gate", "lane", "harbor", "stele", "collapse", "counterweight"]:
					continue
				def = {"title": str(m.get_meta("label", id)), "lore": str(m.get_meta("label", id)) + "。"}
			def["point"] = (m as Marker2D).position
			sites[id] = def
	for id in EXTRA_SITES.get(key, {}):
		sites[id] = defs[id].duplicate()
		sites[id].merge(EXTRA_SITES[key][id], true)
	for id in sites:
		sites[id]["reach"] = 46.0
	for k in actors:
		var p := person(k)
		var doing := str(p.get("doing", ""))
		sites["person:" + k] = {"person": k, "title": str(NAMES.get(k, k)) + ("（" + _short(doing) + "）" if doing != "" else ""), "reach": 44.0}

func _short(text: String) -> String:
	return text if text.length() <= 14 else text.substr(0, 14) + "…"

func field_active() -> bool:
	return stage == "field" and modal_kind == "" and dialogue != null and not dialogue.active and not story_lock and not ended and not switching

func site_visible(id: String) -> bool:
	if not sites.has(id):
		return false
	if sites[id].has("person"):
		return actors.has(sites[id].person) and is_instance_valid(actors[sites[id].person]) and actors[sites[id].person].visible
	return in_sight(sites[id].point)

func site_is_new(id: String) -> bool:
	var site: Dictionary = sites[id]
	if site.has("person") or site.has("kind"):
		return false
	return not examined.has(_card_id(id))

func site_point(id: String) -> Vector2:
	var site: Dictionary = sites[id]
	if site.has("person"):
		return actors[site.person].position
	return site.point

func site_marker(id: String) -> Vector2:
	return site_point(id) + (Vector2(0, -50) if sites[id].has("person") else Vector2(0, -16))

func site_box(id: String) -> Rect2:
	var p := site_point(id)
	if sites[id].has("person"):
		return Rect2(p + Vector2(-12, -46), Vector2(24, 48))
	return Rect2(p + Vector2(-16, -26), Vector2(32, 30))

func _card_id(id: String) -> String:
	return String(map.name) + "/" + id

# ================================================================ movement ===

func _physics_process(delta: float) -> void:
	if not armed and not _inside_exit():
		armed = true
	_check_region()
	_fade_occluders()
	_pick_nearest()
	if not field_active():
		_cancel_click()
		player.velocity = Vector2.ZERO
		if not player.seated:
			player.pose("idle")
		return
	var direction := Vector2(
		float(Input.is_physical_key_pressed(KEY_D) or Input.is_physical_key_pressed(KEY_RIGHT)) - float(Input.is_physical_key_pressed(KEY_A) or Input.is_physical_key_pressed(KEY_LEFT)),
		float(Input.is_physical_key_pressed(KEY_S) or Input.is_physical_key_pressed(KEY_DOWN)) - float(Input.is_physical_key_pressed(KEY_W) or Input.is_physical_key_pressed(KEY_UP)))
	if direction != Vector2.ZERO:
		_cancel_click()
		player.move_input(direction.normalized())
		return
	if click_target != "" and sites.has(click_target) and player.position.distance_to(site_point(click_target)) <= float(sites[click_target].reach) + 6.0:
		var id := click_target
		_cancel_click()
		player.move_input(Vector2.ZERO)
		interact(id)
		return
	if click_waypoint < click_path.size():
		var remaining := click_path[click_waypoint] - player.position
		if remaining.length() <= 4.0:
			click_waypoint += 1
			if click_waypoint >= click_path.size():
				_cancel_click()
				player.move_input(Vector2.ZERO)
			return
		player.move_input(remaining.normalized() * minf(1.0, remaining.length() / maxf(player.speed * delta, 0.0001)))
	else:
		player.move_input(Vector2.ZERO)

func _cancel_click() -> void:
	click_path = PackedVector2Array()
	click_waypoint = 0
	click_target = ""

func _pick_nearest() -> void:
	nearest = ""
	if not field_active():
		return
	var best := INF
	for id in sites:
		if not site_visible(id):
			continue
		var d := player.position.distance_to(site_point(id))
		if d <= float(sites[id].reach) and d < best:
			best = d
			nearest = id

func _process(_delta: float) -> void:
	# 家具整件随房间记忆显示，避免高柜上半部从始终明亮的墙格泄露。
	if sight != null and map != null and sight_terrain_fog():
		var objects := map.get_node_or_null("Objects")
		if objects != null:
			for object in objects.get_children():
				if object is Sprite2D:
					object.self_modulate.a = 1.0 if sight.terrain_known(object.position) else 0.0
	# 视线外的人不显示：他在做什么，侦探看不见。
	for a in actors.values():
		if is_instance_valid(a):
			a.visible = in_sight(a.position)
	hovered = ""
	if field_active():
		var mouse := get_global_mouse_position()
		var best := INF
		for id in sites:
			if site_visible(id) and site_box(id).grow(3).has_point(mouse):
				var d := site_box(id).get_center().distance_squared_to(mouse)
				if d < best:
					best = d
					hovered = id
	if is_instance_valid(hover_tip):
		hover_tip.visible = hovered != "" and field_active()
		if hover_tip.visible:
			hover_tip.text = str(sites[hovered].title)
			hover_tip.reset_size()
			var at := get_viewport().get_mouse_position() + Vector2(16, -34)
			hover_tip.position = Vector2(clampf(at.x, 8, 1272 - hover_tip.size.x), clampf(at.y, 64, 960))
	if is_instance_valid(hud_hint):
		var focus := hovered if hovered != "" else nearest
		hud_hint.visible = field_active() and focus != ""
		if hud_hint.visible:
			hud_hint.text = str(sites[focus].title) + ("　［E］" if focus == nearest else "")
	if is_instance_valid(aside) and aside.visible and Time.get_ticks_msec() > aside_until:
		aside.hide()

func _input(event: InputEvent) -> void:
	if not field_active():
		return
	if event is not InputEventMouseButton or not event.pressed or event.button_index != MOUSE_BUTTON_LEFT:
		return
	if event.position.y < 60:
		return
	var at := get_global_mouse_position()
	_cancel_click()
	var chosen := ""
	var best := INF
	for id in sites:
		if site_visible(id) and site_box(id).grow(4).has_point(at):
			var d := site_box(id).get_center().distance_squared_to(at)
			if d < best:
				best = d
				chosen = id
	if chosen != "":
		if player.position.distance_to(site_point(chosen)) <= float(sites[chosen].reach) + 6.0:
			interact(chosen)
		else:
			_approach(chosen)
	else:
		_furniture_aside(at)
		_route_to(at)
	get_viewport().set_input_as_handled()

func _route_to(destination: Vector2) -> void:
	if grid == null:
		return
	var route := FieldNavigation.route_on_grid(player.position, destination, grid)
	if route.is_empty():
		return
	click_path = route
	if _walkable(destination):
		click_path.append(destination)
	click_waypoint = 0

func _approach(id: String) -> void:
	var point := site_point(id)
	var reach := float(sites[id].reach)
	var best := PackedVector2Array()
	var best_len := INF
	for heading in [Vector2.DOWN, Vector2.UP, Vector2.LEFT, Vector2.RIGHT, Vector2(-1, 1), Vector2(1, 1)]:
		var candidate: Vector2 = point + heading.normalized() * (reach - 12.0)
		if not _walkable(candidate):
			continue
		var route := FieldNavigation.route_on_grid(player.position, candidate, grid)
		if route.is_empty():
			continue
		if route.size() < best_len:
			best_len = route.size()
			best = route
			best.append(candidate)
	if best.is_empty():
		return
	click_path = best
	click_waypoint = 0
	click_target = id

func _furniture_aside(pos: Vector2) -> void:
	var hit_names: Array = []
	for node in map.get_node("Objects").get_children():
		if node is not Sprite2D: continue
		var sprite := node as Sprite2D
		if sprite.texture == null or node.has_meta("occluder"): continue
		if sprite.get_rect().has_point(sprite.to_local(pos)):
			hit_names.append(String(node.name))
	if not hit_names.is_empty():
		_dispatch({"kind": "furniture", "map": String(map.name), "nodes": hit_names})

func _unhandled_input(event: InputEvent) -> void:
	if event is not InputEventKey or not event.pressed or event.echo:
		return
	if ended:
		if event.keycode in [KEY_ENTER, KEY_SPACE]:
			begin_shutdown()
			get_tree().change_scene_to_file("res://scenes/Title/Title.tscn")
		return
	if modal_kind == "card":
		_dismiss_modal()
		get_viewport().set_input_as_handled()
		return
	if modal_kind != "":
		if event.keycode == KEY_ESCAPE or (event.keycode == KEY_TAB and modal_kind == "notebook") or (event.keycode == KEY_R and modal_kind == "replay") or (event.keycode == KEY_M and modal_kind == "map"):
			_dismiss_modal()
			get_viewport().set_input_as_handled()
		return
	if not field_active():
		return
	match event.keycode:
		KEY_E, KEY_SPACE, KEY_ENTER:
			if nearest != "":
				interact(nearest)
		KEY_TAB:
			open_notebook()
		KEY_R:
			if replay_open:
				open_manuscript()
		KEY_M:
			open_campus_map()
		KEY_T:
			commit({"cmd": "stay", "place": MAP_PLACE.get(String(map.name), "")})
		_:
			return
	get_viewport().set_input_as_handled()

# ================================================================= interact ===

func interact(id: String) -> void:
	if not sites.has(id) or not site_visible(id): return
	if player.position.distance_to(site_point(id)) > float(sites[id].reach) + 6.0: return
	var site: Dictionary = sites[id]
	if site.has("person"):
		var actor: CharacterBody2D = actors[site.person]
		actor.face_point(player.position)
		player.face_point(actor.position)
	await _dispatch({"kind": "interact", "map": String(map.name), "site": id, "target": str(site.get("person", ""))})

func _place_title() -> String:
	return str(map.get_meta("title", map.name)) if map else ""

func _show_card(card: String) -> void:
	await _dispatch({"kind": "card", "card_id": card})

func _localized_speaker(canonical: String) -> String:
	match canonical:
		"侦探": return detective_name
		"侦探（心声）": return detective_name + "（心声）"
	return canonical

func dialogue_has_modal_input() -> bool:
	return false

# =================================================================== modal ===

func open_modal(title: String, size: Vector2, kind: String) -> VBoxContainer:
	close_modal()
	modal_kind = kind
	_cancel_click()
	if kind != "ending":
		Sound.sfx("page", -6.0)
	modal_root = Paper.modal(modal_layer, title, size)
	return modal_root.get_meta("body")

func add_footer(actions: Array) -> void:
	var column: VBoxContainer = modal_root.get_meta("column")
	column.add_child(Paper.rule())
	var footer := _ui_content("Footer", column) as HFlowContainer
	for action in actions:
		var b := Paper.button(action[0], action[1])
		if action.size() > 2 and action[2]:
			b.disabled = true
		footer.add_child(b)

func _dismiss_modal() -> void:
	if modal_kind in ["card", "menu"]:
		if not event_view.close_option.is_empty():
			var option: Dictionary = event_view.close_option
			_choose_event(str(option.id), str(option.token))
		return
	close_modal()

func close_modal() -> void:
	if is_instance_valid(modal_root):
		modal_root.queue_free()
	modal_root = null
	modal_kind = ""

# ======================================================================= UI ==

func _build_ui() -> void:
	ui = $GameplayHUD
	hud_title = $GameplayHUD/Title
	hud_keys = $GameplayHUD/Keys
	hud_hint = $GameplayHUD/Hint
	hover_tip = $GameplayHUD/HoverTip
	aside = $GameplayHUD/Aside
	fade = $GameplayHUD/Fade
	modal_layer = $ModalLayer

func _ui_content(template: String, parent: Node) -> Control:
	var view: Control = UI_SCENES[template].instantiate()
	parent.add_child(view)
	return view

func _refresh_hud() -> void:
	if not is_instance_valid(hud_title) or map == null or engine == null:
		return
	hud_title.text = "%s　%s" % [clock_text(), _place_title()]
	var keys := ["E / 点击  检视", "Tab  笔记本"]
	if replay_open:
		keys.append("R  重演")
	keys.append("M  学园图")
	keys.append("T  过一阵")
	hud_keys.text = "　　".join(keys)

func show_aside(text: String) -> void:
	aside.text = text
	aside.show()
	aside_until = Time.get_ticks_msec() + 3200

func _fade_to(work: Callable) -> void:
	if _closing: return
	var tw := create_tween()
	tw.tween_property(fade, "color:a", 1.0, 0.35)
	_fade_tweens.append(tw)
	await _wait_signal(tw.finished)
	_fade_tweens.erase(tw)
	if _closing:
		tw.kill()
		return
	await _wait_signal(get_tree().process_frame)
	if _closing: return
	work.call()
	await _wait_signal(get_tree().process_frame)
	if _closing: return
	var back := create_tween()
	back.tween_property(fade, "color:a", 0.0, 0.4)
	_fade_tweens.append(back)
	await _wait_signal(back.finished)
	_fade_tweens.erase(back)
	if _closing: back.kill()

func _flash_title(text: String) -> void:
	var view := _ui_content("FlashTitle", ui)
	view.get_node("Text").text = "[center]" + text + "[/center]"
	var tw := create_tween()
	tw.tween_property(view, "modulate:a", 0.0, 1.6).set_delay(0.9)
	tw.tween_callback(view.queue_free)


# ============================================================== campus map ===

var _travel: Dictionary = {}

func open_campus_map() -> void:
	open_modal("学园图（点一处，直接去那里；走路不过时辰）", Vector2(1080, 820), "map")
	var column: VBoxContainer = modal_root.get_meta("column")
	var view: Control = CAMPUS_MAP_SCENE.instantiate()
	view.here = _map_node()
	view.known = known_places
	column.add_child(view)
	view.picked.connect(_travel_to)
	add_footer([["合上（M）", close_modal]])

func _travel_to(node: String) -> void:
	close_modal()
	_travel = CampusMap.NODES[node]
	await _fade_to(func(): load_map(str(_travel.map), str(_travel.spawn)))
	if not _closing: await _drain_events()

func _map_node() -> String:
	for key in CampusMap.NODES:
		if str(CampusMap.NODES[key].map) == String(map.name):
			return key
	return ""

# ================================================================ notebook ===

func open_notebook() -> void:
	var body := open_modal("笔记本", Vector2(1040, 820), "notebook")
	var view := _ui_content("Notebook", body)
	view.get_node("Empty").visible = examined.is_empty()
	var grid_box := view.get_node("Evidence") as GridContainer
	for card in examined:
		grid_box.add_child(Paper.button("%s · %s" % [examined[card].place, examined[card].title], _show_card.bind(card), 16))
	var rules: Array = engine.KnownRules()
	view.get_node("Rules").visible = not rules.is_empty()
	for r in rules:
		view.get_node("Rules/Entries").add_child(Paper.label("「%s」——%s%s" % [r.text, r.source, ("，贴在" + str(r.posted)) if str(r.posted) != "" else ""], 17))
	view.get_node("People").visible = not talked.is_empty()
	for key in talked:
		view.get_node("People/Entries").add_child(Paper.label("%s：%s" % [NAMES[key], talked[key]], 17))
	view.get_node("Journal").visible = not journal.is_empty()
	for i in range(journal.size() - 1, -1, -1):
		var entry: Array = journal[i]
		var entries := view.get_node("Journal/Entries")
		entries.add_child(Paper.label("[b]%s[/b]" % entry[0], 17, Paper.MUTED))
		for line in entry[1]:
			entries.add_child(Paper.label(str(line), 16))
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
	var m: Control = ReplayManuscript.instantiate()
	m.host = self
	m.size_flags_vertical = Control.SIZE_EXPAND_FILL
	column.add_child(m)

func replay_title() -> String: return str(engine.ReplayMetadata().title)
func replay_hint() -> String: return str(engine.ReplayMetadata().hint)
func replay_questions() -> Array: return engine.CaseQuestions()
func replay_mark(_index: int) -> String: return ""
func replay_is_new(_index: int) -> bool: return false
func replay_opened() -> void: pass
func finding_title(finding: Dictionary) -> String:
	return str(finding.get("name", ""))
func finding_card(_finding: Dictionary) -> Callable: return Callable()

func run_replay(vars: Array) -> Dictionary:
	replay_vars = vars.duplicate()
	for packet in engine.Dispatch({"kind": "replay", "vars": vars}):
		if str(packet.kind) == "state": _apply_snapshot(packet.snapshot)
		else: _event_queue.append(packet)
	return replay_result

func play_replay(result: Dictionary) -> void:
	if _closing or _replay_playing: return
	_replay_playing = true
	close_modal()
	open_modal("那一夜（墨影回放）", Vector2(1080, 800), "playback")
	var column: VBoxContainer = modal_root.get_meta("column")
	var view: Control = CAMPUS_MAP_SCENE.instantiate()
	view.mode = "replay"
	_playback_view = view
	column.add_child(view)
	await _wait_signal(get_tree().process_frame)
	if _closing:
		_replay_playing = false
		return
	Sound.sfx("replay", -4.0)
	Sound.duck(true)
	await view.play(result.steps, bool(result.full_match))
	Sound.duck(false)
	_replay_playing = false
	if _closing or not is_instance_valid(view) or bool(view.cancelled): return
	await after_replay(result)

func after_replay(_result: Dictionary) -> void:
	close_modal()
	await _drain_events()
	if not _closing: open_manuscript()

func _place_court_actors() -> void:
	var court: Vector2 = sites.get("oath_court", {}).get("point", player.position)
	var i := 0
	for p in people:
		if not bool(p.get("court_present", false)):
			continue
		var actor: CharacterBody2D = actors.get(str(p.key))
		if not is_instance_valid(actor): actor = _make_actor(str(p.key))
		actor.position = court + Vector2((i % 5) * 34 - 68, (i / 5) * 34 - 30)
		i += 1
	player.position = court + Vector2(0, 110)
	_build_sites()

func _group_by_stance(stands: Dictionary) -> void:
	var court: Vector2 = sites.get("oath_court", {}).get("point", player.position)
	var groups := {}
	for key in stands:
		var g: String = stands[key]
		if not groups.has(g):
			groups[g] = []
		groups[g].append(key)
	var gi := 0
	for g in groups:
		var base: Vector2 = court + Vector2(-240 + gi * 160, -60)
		var j := 0
		for key in groups[g]:
			if actors.has(key):
				actors[key].walk_to(base + Vector2((j % 3) * 30, (j / 3) * 30))
			j += 1
		gi += 1

func _ending(result: Dictionary) -> void:
	Sound.music("main_theme", 4.0)
	var body := open_modal("第二章 · 完", Vector2(820, 680), "ending")
	var view := _ui_content("Ending", body)
	view.get_node("Archive").text = "名录库：" + str(result.result_text)
	view.get_node("Quer").text = str(result.quer)
	view.get_node("CaseRule").visible = str(result.case_rule) != ""
	view.get_node("CaseRule").text = "你在对账时提出的规矩：「%s」" % result.case_rule
	view.get_node("CommonRule").text = "第一条共同规则：" + ("成立" if bool(result.common_rule) else "没有成立")
	var hooked: Array = result.hooks.duplicate()
	if int(result.det_hooks) > 0:
		hooked.push_front("你自己")
	view.get_node("Hooks").text = "留在恶魔那里的钩：%s" % ("无" if hooked.is_empty() else "、".join(hooked))
	view.get_node("Kept").text = "你没有交出的：%s" % ("你自己" if int(result.det_hooks) == 0 else "无")
	for line in result.get("impressions", []):
		view.get_node("Impressions").add_child(Paper.label(str(line), 17))
	var file := FileAccess.open("user://chapter2_end.json", FileAccess.WRITE)
	if file:
		file.store_string(JSON.stringify({"detective_name": detective_name, "result": result.result, "quer": result.quer,
			"case_rule": result.case_rule, "common_rule": result.common_rule, "hooks": result.hooks, "det_hooks": result.det_hooks}, "\t"))
	ended = true
	stage = "ended"
	story_lock = false
