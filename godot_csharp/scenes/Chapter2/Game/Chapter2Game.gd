extends "res://scenes/Chapter2/Field/Chapter2Field.gd"
## 《安眠者有福》第二章：与第一章同一套玩法（设计总纲 §四；第三部分 §4—§8、§15）。
## 操作：点击或 WASD 走动，悬停描出可交互的物件与人；E / 空格 / Enter 检视或交谈；Tab 笔记本；R 重演手稿；
## M 学园图（直接去往某处）；T 让这一阵过去。读不耗时；写（请人亲眼看、讲规矩、请人做事、帮人、分口粮）做完即过去一阵。
## 人物推演在 C#：Chapter2Engine → Prototype.Game（Core 的 Observation / DecisionSystem / SocialSystem / CTB）。
## 本脚本是宿主：地图、移动、界面、对白与流程。

const Paper := preload("res://scenes/Shared/UI/Paper.gd")
const Sound := preload("res://scenes/Shared/Audio/Sound.gd")
const FieldNavigation := preload("res://scenes/Shared/FieldNavigation.gd")
const HintOverlay := preload("res://scenes/Shared/UI/HintOverlay.gd")
const ReplayManuscript := preload("res://scenes/Shared/ReplayManuscript.gd")
const CampusMap := preload("res://scenes/Chapter2/Game/CampusMap.gd")
const SightMask := preload("res://scenes/Shared/SightMask.gd")
const EngineScript := preload("res://scenes/Chapter2/Game/Chapter2Engine.cs")
const DIALOGUE_SCENE := preload("res://scenes/Shared/UI/ChapterDialogue.tscn")
const STORY: Resource = preload("res://scenes/Chapter2/Game/chapter2.dialogue")
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
const SLOT_NAMES := ["清晨", "上午", "午间", "下午", "傍晚", "夜里"]
## 各时段的配乐：清晨与上午是船歌式日常变奏，午间丝竹式，下午木卡姆式，傍晚拉格式，夜里八音盒。
const SLOT_MUSIC := ["day", "day", "day_noon", "day_afternoon", "day_evening", "night"]
const SLOT_TINT := [Color(1.0, 0.96, 0.9), Color(1, 1, 1), Color(1, 1, 0.97), Color(1.0, 0.95, 0.86), Color(0.96, 0.82, 0.7), Color(0.45, 0.5, 0.68)]

## 地图上可检视之处。thing：引擎里的物件（细看即亲见）；lore：只看的卡片；kind：宿主自有的交互。
const SITE_DEFS := {
	"logistics": {
		"ledger": {"title": "台账", "thing": "台账"},
		"ration_notice": {"title": "配给台公告", "thing": "配给台公告"},
		"ration_counter": {"title": "配给台", "kind": "meal"},
		"watch_point": {"title": "守夜点", "kind": "watch"},
	},
	"granary": {
		"latch": {"title": "粮仓门闩", "thing": "粮仓"},
		"stock": {"title": "存粮", "thing": "粮仓"},
	},
	"clinic": {
		"empty_bowl": {"title": "莫兰床边的碗", "thing": "碗"},
		"reagent": {"title": "试剂柜", "lore": "白漆试剂柜，标签写着“白塔”。有一格空着。\n茉拉把柜门关得很紧，不让人碰。"},
	},
	"central_hall": {
		"central_notice": {"title": "中央厅公告板", "thing": "中央厅公告"},
		"mouthpiece": {"title": "铜口", "kind": "tube"},
		"relay": {"title": "传声中继", "lore": "一根铜柱，管道从四面汇进来。\n要开名录库的门，这里得有人按拍子转发信号。"},
		"watch_point": {"title": "守夜点", "kind": "watch"},
	},
	"lecture": {
		"blackboard": {"title": "黑板", "thing": "黑板"},
		"timetable": {"title": "讲堂门外的公告", "thing": "讲堂公告"},
	},
	"archive": {
		"lock_core": {"title": "名录库之门", "thing": "名录库之门"},
		"seal_boxes": {"title": "封存匣", "lore": "门缝里看得见架子。十六只封存匣排在四座架上，每只贴着名字。\n你的名字也在其中一只上。"},
		"name_warning": {"title": "门边的铜牌", "lore": "铜牌上刻着一行字：\n入夜后，名录库中若有人以你之名唤你，勿应。"},
		"red_ink": {"title": "规程页的红墨批注", "lore": "文献规程页的页边，一行红墨批注：“授钥：雀。”字迹端正，是导师的手。\n库门锁芯与那把钥匙的齿上，都没有撬过的痕迹。"},
		"watch_point": {"title": "守夜点", "kind": "watch"},
	},
	"living_hall": {
		"hall_notice": {"title": "起居厅公告板", "thing": "生活翼公告"},
		"fireplace": {"title": "壁炉", "lore": "壁炉还温着。炉台上摆着十六只杯子，样式各不一样。"},
		"watch_point": {"title": "守夜点", "kind": "watch"},
	},
	"zone_yi": {"zone_rules": {"title": "公告框（学园守则）", "kind": "school_rules"}},
	"zone_jia": {"zone_rules": {"title": "甲区公告", "thing": "文契公告"}},
	"zone_bing": {"zone_rules": {"title": "丙区公告", "thing": "星仪公告"}},
	"zone_ding": {"zone_rules": {"title": "丁区公告", "thing": "后勤公告"}},
	"resonance": {
		"step_marks": {"title": "沉静室门内的步位标线", "lore": "门内地上的步位标线，一道不缺，一步也没有踩乱。"},
		"silent_door": {"title": "沉静室的门", "lore": "厚木门，内侧有一道浅浅的指痕。门是从外面关上的。"},
	},
	"greenhouse": {
		"valve": {"title": "阀门", "lore": "温室的水压阀门。锈住了。"},
		"gauge": {"title": "压力表", "lore": "压力表的指针贴着红线。没有人在修。"},
	},
	"campus_s": {
		"oath_court": {"title": "誓约庭", "lore": "铺石顺坡，设了几级台阶。站在这里，谁站在谁身边，一眼看得清。"},
		"gate": {"title": "门楼", "lore": "门楼外是去港口的路。沿海崖有一段塌了。"},
		"drying_yard": {"title": "晾晒场", "lore": "绳上晾着几件衬衫。地上是泥。"},
		"spring": {"title": "汲水泉", "lore": "泉水照常在流。"},
	},
}
## 地图里没有标记、由宿主补上的地点。
const EXTRA_SITES := {
	"zone_yi": {"bed": {"title": "你的床（R-05）", "kind": "bed", "point": Vector2(858, 196)}},
	"zone_jia": {"litter": {"title": "雀儿房里的废纸篓", "thing": "文契废纸篓", "point": Vector2(440, 240)}},
}
const REQUESTS := [["修粮仓的门闩", "RepairLatch"], ["去台账上补记", "Inform"], ["给饿着的人送一份口粮", "FeedWeak"], ["去诊疗所照看病人", "Care"], ["夜里守着名录库", "GuardArchive"]]
const FURNITURE := {
	"Bed": "床。", "Wardrobe": "衣柜。", "Crate": "箱子。", "Nightstand": "床头柜。", "Desk": "书桌。", "Chair": "椅子。", "Coat": "衣架。",
	"StudyTable": "长桌。", "HallBookcase": "书架。", "HallLamp": "落地灯。", "Armchair": "扶手椅。", "Range": "灶。",
	"Sideboard": "碗柜。", "Worktable": "案板。", "Barrel": "水桶。满的。", "Washstand": "水槽。", "CoatStand": "衣架。", "ShoeBench": "鞋凳。",
	"Store": "存货。", "ToolBench": "工具台。", "InkShelf": "墨架。墨块码得整整齐齐。", "Hood": "烟罩。", "DishRack": "碗架。", "Pump": "压水机。",
	"Sacks": "面粉。", "Table": "长桌。", "Bench": "长凳。",
}

# ------------------------------------------------------------------ field ---
var engine: Node
var dialogue: CanvasLayer
var hints: Node2D
var grid: AStarGrid2D
var sites: Dictionary = {}
var actors: Dictionary = {}
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
var known_places: Dictionary = {"DormLife": true, "Commons": true}   # 侦探去过或听人提到过的地方（学园图只画这些）

# ------------------------------------------------------------------ story ---
var detective_name := "侦探"
var stage := "intro"
var flags: Dictionary = {}
var examined: Dictionary = {}       # 卡片 id → {title, text, place, thing?}
var people: Array = []
var talked: Dictionary = {}         # key → 最近一次问到的话
var journal: Array = []             # [[时段, 行...]]
var furniture_spoken: Dictionary = {}
var replay_open := false
var replay_vars := [-1, -1, -1, -1]
var replay_result: Dictionary = {}
var last_run_vars: Array = []
var replay_count := 0
var full_match_seen := false
var ended := false
var _modal_result: Variant = null
var _talk_choice := ""
var _wrong_name := ""
var _pending_lines: Array = []

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
	engine.Start()
	_load_chapter1()
	_build_ui()
	super()
	place_label.visible = false
	player.speed = 130.0
	player.collision_mask = 1
	tint = CanvasModulate.new()
	add_child(tint)
	sight = Node2D.new()
	sight.set_script(SightMask)
	sight.z_index = 40
	sight.host = self
	add_child(sight)
	_enter_sight()
	hints = Node2D.new()
	hints.set_script(HintOverlay)
	hints.z_index = 50
	add_child(hints)
	hints.set("source", self)
	dialogue = DIALOGUE_SCENE.instantiate()
	add_child(dialogue)
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
	Sound.music("day", 2.5)
	if not has_meta("skip_intro"):
		call_deferred("_begin")

## 第一章留下的：侦探的名字、各人的钩（跨章节持久状态）。
func _load_chapter1() -> void:
	if not FileAccess.file_exists("user://chapter1_end.json"):
		return
	var data = JSON.parse_string(FileAccess.get_file_as_string("user://chapter1_end.json"))
	if typeof(data) != TYPE_DICTIONARY:
		return
	detective_name = str(data.get("detective_name", "侦探"))
	var hooks = data.get("hooks", {})
	if typeof(hooks) == TYPE_DICTIONARY:
		for key in hooks:
			var who: String = {"huo": "Huo", "shen": "Shen", "su": "Su", "detective": "Det"}.get(str(key), "")
			if who != "":
				for i in int(hooks[key]):
					engine.SeedHook(who)

func _begin() -> void:
	story_lock = true
	await say("d2_arrival")
	# 始业日上午紧接第一章门开：集合与首课沿用内部首日的两个时段键。
	await _fade_to(_gather_day2)
	await _present(_pending_lines)
	await say("d2_gather")
	await _fade_to(_lesson_day2)
	await say("d2_lesson")
	await _present(_pending_lines)
	await say("d2_free")
	stage = "field"
	story_lock = false
	_refresh_hud()

func _gather_day2() -> void:
	_run({"cmd": "stay", "place": "Hall"}, false)
	load_map("central_hall", "south")

func _lesson_day2() -> void:
	_run({"cmd": "class"}, false)
	load_map("lecture", "from_central")

# ================================================================= the clock ==

func upcoming_day() -> int: return int(engine.NextDay())
func upcoming_slot() -> int: return int(engine.NextSlot())
func clock_text() -> String:
	return str(engine.ClockText())

## 结算下一个时段（侦探这一阵做的事 + 其余十五人依各自认知行动）。
func _run(act: Dictionary, reload := true) -> void:
	var title := clock_text()
	var lines: Array = engine.RunSlot(act)
	journal.append([title, lines])
	_pending_lines = lines
	_learn_places(lines)
	people = engine.People()
	tint.color = SLOT_TINT[upcoming_slot()] if not engine.Ended() else SLOT_TINT[0]
	Sound.sfx("bell_slot", -10.0)
	if not engine.Ended():
		Sound.music(SLOT_MUSIC[upcoming_slot()])
	if reload:
		var place := str(engine.DetPlace())
		if MAP_PLACE.get(String(map.name), "") != place and PLACE_MAP.has(place):
			load_map(PLACE_MAP[place], SPAWN_OF[PLACE_MAP[place]])
		else:
			_spawn_actors(true)

var _act: Dictionary = {}

func _run_pending() -> void:
	_run(_act)

## 听人提到的地方，记进学园图。
func _learn_places(lines: Array) -> void:
	for raw in lines:
		for key in CampusMap.NODES:
			var label: String = CampusMap.NODES[key].label
			if str(raw).contains(label) or (key == "ArchiveFront" and str(raw).contains("名录库")):
				known_places[key] = true

## 写，或让这一阵过去：结算、换场，再把看得见的结果交给玩家。
func commit(act: Dictionary) -> void:
	if story_lock or stage != "field":
		return
	var d := upcoming_day()
	var s := upcoming_slot()
	if s == 5:
		await say("d2_night_hint")
		return
	if d == 3 and s == 4:
		await _confront()
		return
	if s == 3 and bool(engine.DetHungry()) and str(act.get("cmd", "")) != "stay":
		show_aside("你饿得发虚。这个下午做不了要紧的事。（按 T 让它过去）")
		return
	# 饭点：没有别的事，就去后勤院领饭（始业日午间在场才看得见争执、才能调停）。
	if str(act.get("cmd", "")) == "stay" and s in [2, 4]:
		act = {"cmd": "collect"}
		if d == 2 and s == 2:
			story_lock = true
			await _fade_to(func(): load_map("logistics", "west"))
			flags.erase("mediate")
			await say("d2_noon_dispute")
			if flags.has("mediate"):
				act = {"cmd": "mediate"}
	story_lock = true
	_act = act
	await _fade_to(_run_pending)
	_flash_title(clock_text())
	await _present(_pending_lines)
	story_lock = false
	if upcoming_slot() == 5:
		show_aside("入夜了。回 R-05 睡下，或到守夜点守着。")
	elif upcoming_day() == 3 and upcoming_slot() == 4:
		show_aside("傍晚要对账了。准备好了，就到中央厅的铜口前（或按 T）。")
	_refresh_hud()

## 夜里：睡下或守夜，然后是次日清晨的固定场面。
func _night(act: Dictionary) -> void:
	story_lock = true
	_act = act
	await _fade_to(func(): _run(_act, false))
	await _present(_pending_lines)
	if engine.Ended():
		await _council()
		return
	if upcoming_day() == 3 and upcoming_slot() == 0:
		await say("d3_dawn")
		await _fade_to(_gather_day3)
		_flash_title("翌日 · 清晨")
		await _present(_pending_lines)
		if str(engine.AccusedName()) == "":
			await say("d3_no_accuse")
		await say("d3_after")
	story_lock = false
	_refresh_hud()

func _gather_day3() -> void:
	_run({"cmd": "stay", "place": "Hall"}, false)
	load_map("central_hall", "south")

# ================================================================= map & people ==

func load_map(key: String, spawn: String, at := Vector2.ZERO, use_at := false) -> void:
	_cancel_click()
	for a in actors.values():
		if is_instance_valid(a):
			a.queue_free()
	actors.clear()
	super(key, spawn, at, use_at)
	_build_grid()
	_enter_sight()
	if MAP_PLACE.has(key) and engine and engine.Started():
		engine.MoveDet(MAP_PLACE[key])
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
	var node := _map_node()
	if node != "":
		known_places[node] = true
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
			if str(p.place) == here and not (upcoming_slot() == 5 and bool(p.asleep)):
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
	var route := FieldNavigation.route_on_grid(actor.position, spot, grid) if grid else PackedVector2Array()
	if route.is_empty():
		actor.position = spot
	else:
		route.append(spot)
		await actor.follow_path(route)
	if is_instance_valid(actor):
		_pose(actor, frame)

func _walk_off(actor: CharacterBody2D, door: Vector2) -> void:
	var route := FieldNavigation.route_on_grid(actor.position, door, grid) if grid else PackedVector2Array()
	if not route.is_empty():
		await actor.follow_path(route)
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
	var defs: Dictionary = SITE_DEFS.get(key, {})
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
		sites[id] = EXTRA_SITES[key][id].duplicate()
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
	for node in map.get_node("Objects").get_children():
		if node is not Sprite2D or furniture_spoken.has(String(map.name) + String(node.name)):
			continue
		var sprite := node as Sprite2D
		if sprite.texture == null or node.has_meta("occluder"):
			continue
		var rect := sprite.get_rect()
		if not rect.has_point(sprite.to_local(pos)):
			continue
		var key := ""
		for prefix in FURNITURE:
			if str(node.name).contains(prefix) and prefix.length() > key.length():
				key = prefix
		if key != "":
			furniture_spoken[String(map.name) + String(node.name)] = true
			show_aside(FURNITURE[key])
			return

func _unhandled_input(event: InputEvent) -> void:
	if event is not InputEventKey or not event.pressed or event.echo:
		return
	if ended:
		if event.keycode in [KEY_ENTER, KEY_SPACE]:
			get_tree().change_scene_to_file("res://scenes/Title/Title.tscn")
		return
	if modal_kind == "card":
		close_modal()
		get_viewport().set_input_as_handled()
		return
	if modal_kind != "":
		if event.keycode == KEY_ESCAPE or (event.keycode == KEY_TAB and modal_kind == "notebook") or (event.keycode == KEY_R and modal_kind == "replay") or (event.keycode == KEY_M and modal_kind == "map"):
			close_modal()
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
	if not sites.has(id):
		return
	var site: Dictionary = sites[id]
	if site.has("person"):
		await talk(str(site.person))
		return
	match str(site.get("kind", "")):
		"bed":
			if upcoming_slot() != 5:
				show_aside("还不到睡的时候。")
				return
			if await confirm("睡下", "在这里睡到天亮？"):
				await _night({"cmd": "sleep"})
			return
		"watch":
			var place: String = MAP_PLACE.get(String(map.name), "")
			if upcoming_slot() != 5:
				show_aside("守夜点。入夜后，可以在这里守着。")
				return
			if await confirm("守夜", "在这里守一夜？只看得见身影，听得见动静。"):
				await _night({"cmd": "watch", "place": place})
			return
		"meal":
			if upcoming_slot() in [2, 4]:
				commit({"cmd": "stay", "place": "Logistics"})
			else:
				show_aside("配给台。午间和傍晚开粮。")
			return
		"tube":
			if upcoming_day() == 3 and upcoming_slot() == 4:
				await _confront()
			else:
				open_lore(id, "铜口", "中央厅的铜口，比寝区里的大。铜牌刻着 ◎。")
			return
		"school_rules":
			open_lore(id, "学园守则", "框里压着一张字迹端正的纸。\n1. 封存之物，可凭对应之物寻回；寻回者，自负其果。\n2. 饮水、炉火、照明照常供给；物资由诸位自行分配。\n3. 外闩所封之处，余患未清，清后即启。\n4. 我所立之规，皆张贴于此。")
			return
	if site.has("thing"):
		open_thing(id)
	else:
		open_lore(id, str(site.title), str(site.get("lore", "")))

func _place_title() -> String:
	return str(map.get_meta("title", map.name)) if map else ""

func open_lore(id: String, title: String, text: String) -> void:
	examined[_card_id(id)] = {"title": title, "text": text, "place": _place_title()}
	_show_card(_card_id(id))

## 细看引擎里的物件：侦探亲见，同一处的痕迹一并看清。
func open_thing(id: String) -> void:
	var site: Dictionary = sites[id]
	var text := str(engine.Examine(str(site.thing)))
	if not examined.has(_card_id(id)):
		Sound.sfx("evidence", -3.0)
	examined[_card_id(id)] = {"title": str(site.title), "text": text, "place": _place_title(), "thing": str(site.thing)}
	_show_card(_card_id(id))
	if str(site.thing) == "台账" and upcoming_day() >= 3 and not replay_open:
		replay_open = true
		while modal_kind != "":
			await get_tree().process_frame
		await say("replay_unlock")
		_refresh_hud()

func _show_card(card: String) -> void:
	var c: Dictionary = examined[card]
	var body := open_modal(str(c.title), Vector2(860, clampi(300 + 30 * str(c.text).length() / 34, 340, 640)), "card")
	body.add_child(Paper.label(str(c.text), 20))
	body.add_child(Paper.label("来源：" + str(c.place), 16, Paper.MUTED))
	var actions := []
	# 写：请在场的人亲眼看看（要花上一阵）。
	if c.has("thing") and stage == "field":
		for k in actors:
			if is_instance_valid(actors[k]) and actors[k].visible and actors[k].position.distance_to(player.position) < 220:
				actions.append(["请%s看看（要花上一阵）" % NAMES[k], _show_to.bind(k, str(c.thing))])
				if actions.size() >= 3:
					break
	actions.append(["返回现场", close_modal])
	add_footer(actions)

func _show_to(who: String, thing: String) -> void:
	close_modal()
	await commit({"cmd": "escort", "who": who, "thing": thing})

# ==================================================================== talk ===

func talk(key: String) -> void:
	var actor: CharacterBody2D = actors[key]
	actor.face_point(player.position)
	player.face_point(actor.position)
	var name: String = NAMES[key]
	var doing := str(person(key).get("doing", ""))
	var first := true
	while true:
		_talk_choice = ""
		var text := "~ talk\n"
		if first:
			text += "（%s%s。）\n" % [name, ("刚才" + doing) if doing != "" else "在这里"]
		else:
			text += "侦探（心声）: 还要跟%s说什么？\n" % name
		first = false
		text += "- 问问%s知道些什么\n\tdo talk_pick(\"ask\")\n" % name
		text += "- 请%s跟你去看一样东西（要花上一阵）\n\tdo talk_pick(\"escort\")\n" % name
		text += "- 跟%s讲一条规矩（要花上一阵）\n\tdo talk_pick(\"rule\")\n" % name
		text += "- 请%s做一件事（要花上一阵）\n\tdo talk_pick(\"request\")\n" % name
		text += "- 帮%s干一阵活（要花上一阵）\n\tdo talk_pick(\"help\")\n" % name
		if bool(engine.DetFed()):
			text += "- 把你今天的口粮分给%s（要花上一阵）\n\tdo talk_pick(\"share\")\n" % name
		text += "- 没什么了\n=> END\n"
		await say_text(text)
		match _talk_choice:
			"ask":
				var reply: Dictionary = engine.AskPresentation(key)
				var line := _clean(str(reply.text))
				talked[key] = line
				await say_text("~ talk\n%s: %s [#emotion=%s]\n=> END\n" % [name, line, str(reply.emotion)])
				continue
			"escort":
				var things := []
				for card in examined:
					if examined[card].has("thing") and not things.has(examined[card].thing):
						things.append(examined[card].thing)
				if things.is_empty():
					show_aside("你还没细看过什么可以带他去看的东西。")
					continue
				var options := []
				for t in things:
					options.append([t, t])
				var pick = await pick_from("请%s跟你去看什么" % name, options)
				if pick == null:
					continue
				await commit({"cmd": "escort", "who": key, "thing": str(pick)})
			"rule":
				var options := []
				for r in engine.KnownRules():
					if bool(r.tellable):
						options.append(["「%s」（%s）" % [r.text, r.source], {"cmd": "tell_rule", "who": key, "rule": int(r.id)}])
				var wordings: Array = engine.Wordings("case")
				for i in wordings.size():
					options.append(["「%s」（你自己的写法）" % wordings[i], {"cmd": "tell_wording", "who": key, "wording": i}])
				var pick = await pick_from("跟%s讲哪一条" % name, options)
				if pick == null:
					continue
				await commit(pick)
			"request":
				var options := []
				for r in REQUESTS:
					options.append([r[0], r[1]])
				var pick = await pick_from("请%s做什么" % name, options)
				if pick == null:
					continue
				await commit({"cmd": "request", "who": key, "job": str(pick)})
			"help":
				await commit({"cmd": "help", "who": key})
			"share":
				await commit({"cmd": "share", "who": key})
		return

func talk_pick(choice: String) -> void:
	_talk_choice = choice

# ================================================================ dialogue ===

func say(title: String) -> void:
	await _run_dialogue(STORY, title)

## 推演或宿主拼出的对白：编成临时的对白资源，走同一个对白框。标题一律写成第一行的“~ 名字”。
func say_text(text: String) -> void:
	var resource: Resource = get_node("/root/DialogueManager").create_resource_from_text(text)
	var title := text.get_slice("\n", 0).trim_prefix("~ ").strip_edges()
	await _run_dialogue(resource, title)

func _run_dialogue(resource: Resource, title: String) -> void:
	while dialogue.active:
		await dialogue.finished
	_cancel_click()
	var was := story_lock
	story_lock = true
	dialogue.panel.show()
	dialogue.start(resource, title, self)
	await dialogue.finished
	dialogue.panel.hide()
	story_lock = was
	_refresh_hud()

func _localized_speaker(canonical: String) -> String:
	match canonical:
		"侦探": return detective_name
		"侦探（心声）": return detective_name + "（心声）"
	return canonical

func dialogue_has_modal_input() -> bool:
	return false

func lesson_answer(precise: bool) -> void:
	engine.LessonAnswer(precise)

func set_flag(name: String) -> void: flags[name] = true
func flag(name: String) -> bool: return flags.has(name)
func wrong_name() -> String: return _wrong_name

## 推演吐出的文字：去掉格式，交给对白框（广播由铜口说出，其余是旁白；转述最多带两条，全部记进笔记本）。
func _clean(text: String) -> String:
	return text.replace(": ", "：").replace("\n", " ").replace("{", "（").replace("}", "）").strip_edges()

func _present(lines: Array) -> void:
	var out := ""
	var rumors := 0
	for raw in lines:
		var line := _clean(str(raw))
		if line == "":
			continue
		if line.begins_with("【广播】"):
			out += "铜口: %s\n" % line.trim_prefix("【广播】")
			continue
		var said := line.find("说：")
		if said > 0 and said < 8 and not line.begins_with("你"):
			rumors += 1
			if rumors > 2:
				continue
			line = "听说——" + line
		if line.begins_with("（") and line.ends_with("）"):
			out += line + "\n"
		else:
			out += "（%s）\n" % line
	if out == "":
		return
	await say_text("~ feed\n" + out + "=> END\n")

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
	var footer := HFlowContainer.new()
	footer.add_theme_constant_override("h_separation", 12)
	footer.add_theme_constant_override("v_separation", 8)
	footer.alignment = FlowContainer.ALIGNMENT_END
	column.add_child(footer)
	for action in actions:
		var b := Paper.button(action[0], action[1])
		if action.size() > 2 and action[2]:
			b.disabled = true
		footer.add_child(b)

func close_modal() -> void:
	if is_instance_valid(modal_root):
		modal_root.queue_free()
	modal_root = null
	modal_kind = ""

func _modal_choose(value: Variant) -> void:
	_modal_result = value
	close_modal()

## 从一列选项里选一项；options = [[文字, 值], ...]。返回值，或 null（算了）。
func pick_from(title: String, options: Array) -> Variant:
	var body := open_modal(title, Vector2(860, mini(800, 200 + 56 * options.size())), "menu")
	_modal_result = null
	for o in options:
		var b := Paper.button(str(o[0]), _modal_choose.bind([o[1]]), 18)
		b.alignment = HORIZONTAL_ALIGNMENT_LEFT
		b.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		body.add_child(b)
	add_footer([["算了", _modal_choose.bind([null])]])
	while _modal_result == null:
		await get_tree().process_frame
	return _modal_result[0]

func confirm(title: String, text: String) -> bool:
	var body := open_modal(title, Vector2(620, 300), "menu")
	body.add_child(Paper.label(text, 19))
	_modal_result = null
	add_footer([["算了", _modal_choose.bind(false)], ["好", _modal_choose.bind(true)]])
	while _modal_result == null:
		await get_tree().process_frame
	return bool(_modal_result)

# ======================================================================= UI ==

func _build_ui() -> void:
	ui = CanvasLayer.new()
	ui.layer = 10
	add_child(ui)
	var bar := ColorRect.new()
	bar.color = Color(0.05, 0.06, 0.06, 0.72)
	bar.size = Vector2(1280, 56)
	bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	ui.add_child(bar)
	hud_title = _hud_label(Vector2(20, 14), Vector2(560, 30), 20, HORIZONTAL_ALIGNMENT_LEFT)
	hud_keys = _hud_label(Vector2(520, 16), Vector2(740, 30), 16, HORIZONTAL_ALIGNMENT_RIGHT)
	hud_hint = _hud_label(Vector2(340, 920), Vector2(600, 30), 18, HORIZONTAL_ALIGNMENT_CENTER)
	hover_tip = Label.new()
	hover_tip.add_theme_font_override("font", Paper.font())
	hover_tip.add_theme_font_size_override("font_size", 17)
	hover_tip.add_theme_color_override("font_color", Color("f1e4bd"))
	var tip := StyleBoxFlat.new()
	tip.bg_color = Color(0.07, 0.07, 0.08, 0.86)
	tip.border_color = Color(0.83, 0.74, 0.52, 0.7)
	tip.set_border_width_all(1)
	tip.set_content_margin_all(5)
	hover_tip.add_theme_stylebox_override("normal", tip)
	hover_tip.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hover_tip.hide()
	ui.add_child(hover_tip)
	aside = _hud_label(Vector2(140, 870), Vector2(1000, 30), 19, HORIZONTAL_ALIGNMENT_CENTER)
	aside.hide()
	fade = ColorRect.new()
	fade.color = Color(0, 0, 0, 0)
	fade.size = Vector2(1280, 992)
	fade.mouse_filter = Control.MOUSE_FILTER_IGNORE
	ui.add_child(fade)
	modal_layer = CanvasLayer.new()
	modal_layer.layer = 15
	add_child(modal_layer)

func _hud_label(at: Vector2, size: Vector2, font_size: int, align: HorizontalAlignment) -> Label:
	var l := Label.new()
	l.position = at
	l.size = size
	l.horizontal_alignment = align
	l.add_theme_font_override("font", Paper.font())
	l.add_theme_font_size_override("font_size", font_size)
	l.add_theme_color_override("font_color", Color("f1e4bd"))
	l.add_theme_color_override("font_outline_color", Color(0.04, 0.03, 0.05))
	l.add_theme_constant_override("outline_size", 5)
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	ui.add_child(l)
	return l

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
	var tw := create_tween()
	tw.tween_property(fade, "color:a", 1.0, 0.35)
	await tw.finished
	await get_tree().process_frame
	work.call()
	await get_tree().process_frame
	var back := create_tween()
	back.tween_property(fade, "color:a", 0.0, 0.4)
	await back.finished

func _flash_title(text: String) -> void:
	var l := Paper.label("[center]" + text + "[/center]", 32, Color(0.97, 0.93, 0.84))
	l.custom_minimum_size = Vector2(600, 0)
	l.position = Vector2(340, 400)
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	ui.add_child(l)
	var tw := create_tween()
	tw.tween_property(l, "modulate:a", 0.0, 1.6).set_delay(0.9)
	tw.tween_callback(l.queue_free)

# ============================================================== campus map ===

var _travel: Dictionary = {}

func open_campus_map() -> void:
	open_modal("学园图（点一处，直接去那里；走路不过时辰）", Vector2(1080, 820), "map")
	var column: VBoxContainer = modal_root.get_meta("column")
	var view: Control = CampusMap.new()
	view.here = _map_node()
	view.known = known_places
	column.add_child(view)
	view.picked.connect(_travel_to)
	add_footer([["合上（M）", close_modal]])

func _travel_to(node: String) -> void:
	close_modal()
	_travel = CampusMap.NODES[node]
	await _fade_to(func(): load_map(str(_travel.map), str(_travel.spawn)))

func _map_node() -> String:
	for key in CampusMap.NODES:
		if str(CampusMap.NODES[key].map) == String(map.name):
			return key
	return ""

# ================================================================ notebook ===

func open_notebook() -> void:
	var body := open_modal("笔记本", Vector2(1040, 820), "notebook")
	body.add_child(Paper.label("[b]看过的[/b]", 21, Paper.SEAL))
	if examined.is_empty():
		body.add_child(Paper.label("还没有记下任何东西。", 18, Paper.MUTED))
	var grid_box := GridContainer.new()
	grid_box.columns = 3
	grid_box.add_theme_constant_override("h_separation", 10)
	grid_box.add_theme_constant_override("v_separation", 8)
	body.add_child(grid_box)
	for card in examined:
		grid_box.add_child(Paper.button("%s · %s" % [examined[card].place, examined[card].title], _show_card.bind(card), 16))
	var rules: Array = engine.KnownRules()
	if not rules.is_empty():
		body.add_child(Paper.rule())
		body.add_child(Paper.label("[b]读过、听过的规矩[/b]", 21, Paper.SEAL))
		for r in rules:
			body.add_child(Paper.label("「%s」——%s%s" % [r.text, r.source, ("，贴在" + str(r.posted)) if str(r.posted) != "" else ""], 17))
	if not talked.is_empty():
		body.add_child(Paper.rule())
		body.add_child(Paper.label("[b]人[/b]（你亲耳听到的）", 21, Paper.SEAL))
		for key in talked:
			body.add_child(Paper.label("%s：%s" % [NAMES[key], talked[key]], 17))
	if not journal.is_empty():
		body.add_child(Paper.rule())
		body.add_child(Paper.label("[b]这几日[/b]", 21, Paper.SEAL))
		for i in range(journal.size() - 1, -1, -1):
			var entry: Array = journal[i]
			body.add_child(Paper.label("[b]%s[/b]" % entry[0], 17, Paper.MUTED))
			for line in entry[1]:
				body.add_child(Paper.label(_clean(str(line)), 16))
	var footer := []
	if replay_open:
		footer.append(["重演手稿（R）", open_manuscript])
	footer.append(["合上（Tab）", close_modal])
	add_footer(footer)

# ================================================================== replay ===

const FINDING_TITLES := ["台账", "粮仓门闩", "诊疗所的碗", "甲区的废纸篓", "贝尔特朗夜巡所见"]

func open_manuscript() -> void:
	if not replay_open:
		return
	open_modal("", Vector2(1200, 900), "replay")
	var column: VBoxContainer = modal_root.get_meta("column")
	for child in column.get_children():
		child.queue_free()
	var m: Control = ReplayManuscript.new()
	m.host = self
	m.size_flags_vertical = Control.SIZE_EXPAND_FILL
	column.add_child(m)

func replay_title() -> String: return "那一夜重演"
func replay_hint() -> String: return "设定第 2 日夜里的处境与人心，让那一夜重演一次。谁做了什么，由各人自己的认知推出。"
func replay_questions() -> Array: return engine.CaseQuestions()
func replay_mark(_index: int) -> String: return ""
func replay_is_new(_index: int) -> bool: return false
func replay_opened() -> void: pass
func finding_title(finding: Dictionary) -> String:
	return FINDING_TITLES[int(finding.index)]
func finding_card(_finding: Dictionary) -> Callable: return Callable()

func run_replay(vars: Array) -> Dictionary:
	replay_vars = vars.duplicate()
	replay_result = engine.RunCase(vars)
	replay_count += 1
	return replay_result

func play_replay(result: Dictionary) -> void:
	close_modal()
	open_modal("那一夜（墨影回放）", Vector2(1080, 800), "playback")
	var column: VBoxContainer = modal_root.get_meta("column")
	var view: Control = CampusMap.new()
	view.mode = "replay"
	column.add_child(view)
	await get_tree().process_frame
	Sound.sfx("replay", -4.0)
	Sound.duck(true)
	await view.play(result.steps, bool(result.full_match))
	Sound.duck(false)
	await after_replay(result)

func after_replay(result: Dictionary) -> void:
	close_modal()
	if replay_count == 1 and not bool(result.full_match):
		await say("replay_first")
	if bool(result.full_match) and not full_match_seen:
		full_match_seen = true
		await say("replay_match")
	open_manuscript()

# ================================================================ confront ===

## 第 3 日傍晚的对账：一问一答，陈述与出示成对（与第一章始业之答同一套）。
const STEPS := [
	{"asker": "沃德", "q": "台账上的缺口，有一份，是谁取的？为何取？", "claim": 0},
	{"asker": "沃德", "q": "其余的呢？", "claim": 2},
	{"asker": "沃德", "q": "夜里有人动过粮仓的门。", "claim": 1},
	{"asker": "阮清越", "q": "那台账上，为何不见记录？", "claim": 3},
]

func _confront() -> void:
	if story_lock and stage != "field":
		return
	story_lock = true
	Sound.music("confrontation")
	await _fade_to(func(): load_map("central_hall", "south"))
	await say("d3_evening")
	await say("confront_begin")
	var pairs := []
	for step in STEPS:
		if not bool(engine.ClaimTrue(int(step.claim))):
			continue
		while true:
			var pick: Dictionary = await _confront_pick(step)
			if pick.is_empty():
				await say("confront_pause")
				break
			var said := int(pick.say)
			var shown := int(pick.show)
			if said == int(step.claim) and bool(engine.PairValid(said, shown)):
				Sound.sfx("answer_ok", -4.0)
				pairs.append([said, shown])
				await say_text("~ r\n（%s）\n=> END\n" % _clean(str(engine.Reaction(said))))
				break
			if said == 5 and bool(engine.PairValid(5, shown)):
				pairs.append([said, shown])
				await say("confront_frame")
				break
			Sound.sfx("answer_wrong", -3.0)
			if said != int(step.claim):
				await say("confront_wrong_say")
			else:
				var ev: Array = engine.Evidence()
				_wrong_name = str(ev[shown].name)
				await say("confront_wrong_show")
	await say("confront_rule")
	var wordings: Array = engine.Wordings("case")
	var options := [["不提新规矩", -1]]
	for i in wordings.size():
		options.append(["提出：「%s」" % wordings[i], i])
	var proposal = await pick_from("今后如何？", options)
	var common: bool = await confirm("共同规则", "再当众提议一条：「公开指控须附可检查的材料」？")
	_act = {"cmd": "confront", "pairs": pairs, "proposal": -1 if proposal == null else int(proposal), "common": common}
	await _fade_to(_run_pending)
	var shown_lines := []
	for line in _pending_lines:
		if not str(line).begins_with("你：“"):
			shown_lines.append(line)
	await _present(shown_lines)
	Sound.music("night", 3.0)
	await say("d3_whisper")
	story_lock = false
	show_aside("入夜了。回 R-05 睡下，或到守夜点守着。")
	_refresh_hud()

var _cf_choice := {}
var _cf_list: VBoxContainer
var _cf_summary: RichTextLabel

func _confront_pick(step: Dictionary) -> Dictionary:
	_modal_result = null
	_cf_choice = {"say": -1, "show": -1}
	var body := open_modal("对账", Vector2(900, 820), "answer")
	var column: VBoxContainer = modal_root.get_meta("column")
	var header := VBoxContainer.new()
	header.add_theme_constant_override("separation", 6)
	column.add_child(header)
	column.move_child(header, 2)
	header.add_child(Paper.label("%s问：[b]%s[/b]" % [step.asker, step.q], 21, Paper.SEAL))
	header.add_child(Paper.label("陈述一句结论，再出示一件能支撑它的东西。", 18, Paper.MUTED))
	_cf_summary = Paper.label("", 18)
	header.add_child(_cf_summary)
	header.add_child(Paper.rule())
	_cf_list = VBoxContainer.new()
	_cf_list.add_theme_constant_override("separation", 8)
	body.add_child(_cf_list)
	add_footer([["陈述", _cf_show.bind("say")], ["出示", _cf_show.bind("show")], ["暂且不答", _modal_choose.bind({})], ["作答", _cf_submit]])
	_cf_show("say")
	_cf_refresh()
	while _modal_result == null:
		await get_tree().process_frame
	return _modal_result

func _cf_show(kind: String) -> void:
	for child in _cf_list.get_children():
		child.queue_free()
	if kind == "say":
		var claims: Array = engine.Claims()
		for i in claims.size():
			var b := Paper.button(str(claims[i]), _cf_select.bind("say", i), 19)
			b.toggle_mode = true
			b.button_pressed = int(_cf_choice.say) == i
			_cf_list.add_child(b)
	else:
		var any := false
		for e in engine.Evidence():
			if bool(e.have):
				any = true
				var b := Paper.button(str(e.name), _cf_select.bind("show", int(e.index)), 19)
				b.toggle_mode = true
				b.button_pressed = int(_cf_choice.show) == int(e.index)
				_cf_list.add_child(b)
		if not any:
			_cf_list.add_child(Paper.label("你手里还没有能出示的东西：去细看，去问人。", 18, Paper.MUTED))

func _cf_select(kind: String, value: int) -> void:
	_cf_choice[kind] = value
	_cf_refresh()

func _cf_refresh() -> void:
	var claims: Array = engine.Claims()
	var ev: Array = engine.Evidence()
	_cf_summary.text = "陈述：%s\n出示：%s" % [str(claims[int(_cf_choice.say)]) if int(_cf_choice.say) >= 0 else "（未选）", str(ev[int(_cf_choice.show)].name) if int(_cf_choice.show) >= 0 else "（未选）"]

func _cf_submit() -> void:
	if int(_cf_choice.say) < 0 or int(_cf_choice.show) < 0:
		return
	_modal_choose(_cf_choice.duplicate())

# ================================================================= council ===

## 第三日清晨，誓约庭：各人依立场站到一个方案旁边；侦探表态，人数最多且凑得齐三站的方案成立。
func _council() -> void:
	story_lock = true
	Sound.music("rite", 3.0)
	await _fade_to(_go_court)
	_flash_title("第三日 · 清晨")
	await say("d4_council")
	var wordings: Array = engine.Wordings("archive")
	var options := [["不提", -1]]
	for i in wordings.size():
		options.append(["当众提出：「%s」" % wordings[i], i])
	var proposal = await pick_from("你要当众提出一种写法吗？", options)
	var plans: Array = engine.CouncilPlans(-1 if proposal == null else int(proposal))
	var plan_options := []
	for i in plans.size():
		plan_options.append(["站到这一边：「%s」（%s）" % [plans[i].text, plans[i].author], i])
	plan_options.append(["不表态", -1])
	var back = await pick_from("名录库之议：你站在哪一边？", plan_options)
	var result: Dictionary = engine.Council(-1 if back == null else int(back))
	_group_by_stance(result.stands)
	var lines := []
	for line in result.log:
		if not str(line).contains("站到了") and not str(line).contains("匣子没有应"):
			lines.append(line)
	await _present(lines)
	if str(result.result) == "Sealed":
		await say("d4_sealed")
	else:
		await say("d4_box")
	_ending(result)

func _go_court() -> void:
	load_map("campus_s", "gate_inside")
	var court: Vector2 = sites.get("oath_court", {}).get("point", player.position)
	var i := 0
	for p in people:
		if bool(p.sick):
			continue
		var actor := _make_actor(str(p.key))
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
	body.add_child(Paper.label("名录库：" + str(result.result_text), 22))
	body.add_child(Paper.label(str(result.quer), 19))
	if str(result.case_rule) != "":
		body.add_child(Paper.label("你在对账时提出的规矩：「%s」" % result.case_rule, 18))
	body.add_child(Paper.label("第一条共同规则：" + ("成立" if bool(result.common_rule) else "没有成立"), 18))
	var hooked: Array = result.hooks.duplicate()
	if int(result.det_hooks) > 0:
		hooked.push_front("你自己")
	body.add_child(Paper.label("留在恶魔那里的钩：%s" % ("无" if hooked.is_empty() else "、".join(hooked)), 18, Paper.SEAL))
	body.add_child(Paper.label("你没有交出的：%s" % ("你自己" if int(result.det_hooks) == 0 else "无"), 18, Paper.MATCH))
	body.add_child(Paper.rule())
	body.add_child(Paper.label("[b]你在几个人眼里的样子[/b]（只写你见过的迹象）", 18, Paper.MUTED))
	for line in engine.Impressions():
		body.add_child(Paper.label(str(line), 17))
	body.add_child(Paper.label("按空格或 Enter 回到标题。", 16, Paper.MUTED))
	var file := FileAccess.open("user://chapter2_end.json", FileAccess.WRITE)
	if file:
		file.store_string(JSON.stringify({"detective_name": detective_name, "result": result.result, "quer": result.quer,
			"case_rule": result.case_rule, "common_rule": result.common_rule, "hooks": result.hooks, "det_hooks": result.det_hooks}, "\t"))
	ended = true
	stage = "ended"
	story_lock = false
