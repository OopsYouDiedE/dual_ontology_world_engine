extends Node2D
## 第一章乙区地图的适配器：几何（地面格、家具足迹、宿舍门）、房间区域、锚点、交互点位置、
## 道具状态（隔离门、旧物、痕迹）、日常站点与昨夜回放。剧情、文字与流程不在这里，全部来自章节事件。
## 地图节点由 tools/build_awakening_map.py 生成；本脚本只读生成的布局数据。

const FieldNavigation := preload("res://scenes/Shared/FieldNavigation.gd")
const ReplayStage := preload("res://scenes/Awakening/ReplayStage.gd")
const TraceDecals := preload("res://scenes/Awakening/TraceDecals.gd")
const LAYOUT: Resource = preload("res://scenes/Awakening/map_layout.tres")
const SPATIAL := "res://scenes/Awakening/Script/generated_awakening_layout.json"
const CONTENT := "res://scenes/Awakening/Script/generated_awakening_content.json"
const ART := "res://ShareAsset/Internal/Props/AwakeningEvidence/"
## 人物键 → 场景里的人物节点名。
# STORY-HARDCODE[AWK-059][schema][production]: 本章人物键与正式现场角色节点名称固定绑定。
const ACTOR_NODES := {"detective": "侦探", "huo": "工程师", "shen": "厨师", "su": "音乐家"}

var field: Node2D
var layout: Dictionary = {}
var spatial: Dictionary = {}
var regions: Array = []
var room_names: Dictionary = {}
var floors: Dictionary = {}
var obstacle_rects: Array[Rect2] = []
var decals: Node2D
var stage_node: Node2D
## RoomDoor 读取：按节点名索引的人物、玩家与宿舍门权限。
var actors: Dictionary = {}
var player: CharacterBody2D
var _seen: Dictionary = {}
var _ink_grid: AStarGrid2D
var _dorms_open := false
var _isolation_open := false

func setup() -> void:
	layout = LAYOUT.get_meta("data")
	spatial = JSON.parse_string(FileAccess.get_file_as_string(SPATIAL))
	var content: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(CONTENT))
	regions = content.ROOM_REGIONS
	room_names = content.ROOM_NAMES
	for y in layout.rows.size():
		var row: String = layout.rows[y]
		for x in row.length():
			if row.substr(x, 1) in [".", "D"]:
				floors[Vector2i(x, y)] = true
	for item in layout.furniture:
		if item.footprint != null:
			var raw: Array = item.footprint
			obstacle_rects.append(Rect2(raw[0], raw[1], raw[2], raw[3]))
	for key in ACTOR_NODES:
		var actor: CharacterBody2D = $ObjectsAndPeople.get_node(ACTOR_NODES[key])
		actors[ACTOR_NODES[key]] = actor
		actor.collision_mask = 1
	player = actors["侦探"]
	player.standard = player.standard.duplicate()
	for direction in ["front", "back", "left"]:
		player.standard.set_animation_speed("walk_" + direction, 7.5)
	player.sprite.sprite_frames = player.standard
	_build_props()
	stage_node = ReplayStage.new()
	stage_node.name = "ReplayStage"
	stage_node.host = self
	stage_node.z_index = 60
	add_child(stage_node)

func begin_shutdown() -> void:
	if is_instance_valid(stage_node): stage_node.cancel_playback()

func fixed_actors() -> Dictionary:
	var result := {}
	for key in ACTOR_NODES: result[key] = actors[ACTOR_NODES[key]]
	return result

func actor_of(key: String) -> CharacterBody2D: return actors[ACTOR_NODES[key]]
func objects_root() -> Node2D: return $ObjectsAndPeople
func map_key() -> String: return "awakening"
func load_map(_key: String, _spawn: String) -> void: pass
func spawn_point(_spawn: String) -> Vector2: return player.position
func stands() -> Array: return []
func door_point(fallback: Vector2) -> Vector2: return fallback
func switching() -> bool: return false
func uses_daypart_tint() -> bool: return false
func camera_smoothing() -> bool: return false
func camera_limits() -> Rect2: return Rect2(-100000, -100000, 200000, 200000)
func camera_point(p: Vector2) -> Vector2: return Vector2(clampf(p.x, 320, 960), clampf(p.y - 24, 248, 456)).round()
func physics_update(_delta: float) -> void: pass
func seen_cells() -> Dictionary: return _seen

func apply_snapshot(snapshot: Dictionary) -> void:
	_dorms_open = bool(snapshot.get("access", {}).get("dorms", false))
	var opened := bool(snapshot.get("access", {}).get("isolation_open", false))
	if opened != _isolation_open: set_prop("isolation_door", "open" if opened else "closed")
	$ObjectsAndPeople/MechanicalIsolationDoor/DemonPrints.visible = bool(snapshot.get("access", {}).get("door_prints", false))

func near_doorway(point: Vector2) -> bool:
	var doorway = anchor("doorway")
	return doorway != null and point.distance_to(doorway) <= float(spatial.doorway_reach)

## RoomDoor：宿舍门是否对玩家开放（主人自己总能进）。
func can_access_dorm(_owner: String) -> bool: return _dorms_open

# ------------------------------------------------------------------ props ---

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
	for spec in spatial.props:
		var texture_path := str(spec.texture)
		if not texture_path.begins_with("res://"): texture_path = ART + texture_path
		if not ResourceLoader.exists(texture_path): continue
		var sprite := Sprite2D.new()
		sprite.name = str(spec.node)
		sprite.texture = load(texture_path)
		var visual := Vector2(float(spec.x), float(spec.y))
		sprite.position = Vector2(visual.x, float(spec.z_y))
		sprite.offset = Vector2(-sprite.texture.get_width() / 2.0, visual.y - float(spec.z_y) - sprite.texture.get_height())
		sprite.centered = false
		$ObjectsAndPeople.add_child(sprite)
		if str(spec.node) == "Piano":
			# 琴身的地面足迹与导航使用同一矩形，上方绘画由场景 Y 排序遮挡。
			var footprint := Rect2(visual + Vector2(-40, -14), Vector2(80, 14))
			var body := StaticBody2D.new()
			body.name = "Piano"
			body.position = footprint.get_center()
			var shape := RectangleShape2D.new()
			shape.size = footprint.size
			var collision := CollisionShape2D.new()
			collision.shape = shape
			body.add_child(collision)
			$FurnitureSolids.add_child(body)
			obstacle_rects.append(footprint)

## 剧情给出的道具状态：隔离门开启、旧物被拿起与放回（放回后摆得和原来不一样）、痕迹重绘。
func set_prop(id: String, state: String) -> void:
	match id:
		"isolation_door":
			_isolation_open = state == "open"
			var door := $ObjectsAndPeople/MechanicalIsolationDoor
			for node in ["DoorFace", "LoungeCover", "LoungeCoverInner"]: door.get_node(node).visible = state != "open"
			door.get_node("DoorGap").visible = state == "open"
			var prints := door.get_node_or_null("DemonPrints")
			if prints and state != "open": prints.visible = false
		"traces":
			decals.queue_redraw()
		_:
			var object: Dictionary = spatial.objects.get(id, {})
			var sprite := $ObjectsAndPeople.get_node_or_null(str(object.get("sprite", "")))
			if sprite == null: return
			sprite.visible = state != "taken"
			if state == "returned" and not sprite.has_meta("moved"):
				sprite.set_meta("moved", true)
				sprite.position.x += 3

# --------------------------------------------------------------- geometry ---

func _region_at(p: Vector2) -> Dictionary:
	for region in regions:
		var rect: Array = region.rect
		if (rect[0] == null or p.x >= float(rect[0])) and (rect[1] == null or p.y >= float(rect[1])) \
				and (rect[2] == null or p.x < float(rect[2])) and (rect[3] == null or p.y < float(rect[3])):
			return region
	return {"name": "", "region": ""}

func room_name(p: Vector2) -> String: return str(_region_at(p).name)

func room_of(p: Vector2) -> String:
	var name := room_name(p)
	for key in room_names:
		if room_names[key] == name: return key
	return ""

## 剧情到场按脚下真实室内地板判定，门槛与墙不算进入。
func entered_room(p: Vector2) -> String:
	return room_of(p) if sight_interior(Vector2i(p / 32.0)) else ""

func _closed_room_rects(walker: CharacterBody2D) -> Array[Rect2]:
	var rects: Array[Rect2] = []
	for door in $ObjectsAndPeople.get_children():
		if not door.has_method("access_allowed") or door.access_allowed(): continue
		if is_instance_valid(walker) and door.owner_actor() == walker: continue
		rects.append(door.threshold_collision_rect())
		rects.append(door.interior_bounds)
	return rects

func walkable(point: Vector2, walker: CharacterBody2D = null) -> bool:
	if not floors.has(Vector2i(point / 32.0)): return false
	for rect in obstacle_rects + _closed_room_rects(walker if walker else player):
		if rect.grow(8.0).has_point(point): return false
	return true

func _size() -> Vector2i: return Vector2i(layout.rows[0].length(), layout.rows.size())

func nav_grid(walker: CharacterBody2D) -> AStarGrid2D:
	var obstacles := obstacle_rects.duplicate()
	obstacles.append_array(_closed_room_rects(walker))
	return FieldNavigation.build_route_grid(floors, obstacles, _size())

## 回放墨影的走位：只避家具与墙，不受宿舍门与人物阻挡。
func ink_route(from: Vector2, to: Vector2) -> PackedVector2Array:
	if _ink_grid == null: _ink_grid = FieldNavigation.build_route_grid(floors, obstacle_rects, _size())
	var route := FieldNavigation.route_on_grid(from, to, _ink_grid)
	if route.is_empty(): return PackedVector2Array([to])
	route.append(to)
	return route

# ----------------------------------------------------------------- sight ---

func sight_origin() -> Vector2: return player.position + Vector2(0, -8)
func sight_entry_origin() -> Vector2: return player.position
func sight_size() -> Vector2i: return _size()
func sight_interior(cell: Vector2i) -> bool:
	if cell.y < 0 or cell.y >= layout.rows.size(): return false
	var row: String = layout.rows[cell.y]
	return cell.x >= 0 and cell.x < row.length() and row.substr(cell.x, 1) == "."
func sight_region(cell: Vector2i) -> String:
	# 同名的房间（两间空房）在区域表里各有区域名，各自揭示。
	return str(_region_at(Vector2(cell * 32) + Vector2(16, 16)).region)
func sight_blocks(_cell: Vector2i) -> bool: return false
func sight_terrain_fog() -> bool: return true
func sight_point(p: Vector2) -> Vector2: return p
## TraceDecals 读视线记忆。
var sight: Node2D:
	get: return field.sight if field else null

# ----------------------------------------------------------------- sites ---

func site_geometry(_defs: Dictionary) -> Dictionary:
	var result := {}
	var boxes: Dictionary = spatial.site_boxes
	for source in spatial.sites:
		var id := str(source.id)
		var point := Vector2(float(source.x), float(source.y))
		var box := Rect2(point + Vector2(-14, -22), Vector2(28, 26))
		if boxes.has(id): box = Rect2(float(boxes[id][0]), float(boxes[id][1]), float(boxes[id][2]), float(boxes[id][3]))
		elif id.begins_with("E11"): box = Rect2(point + Vector2(-22, -48), Vector2(44, 62))
		result[id] = {"point": point, "reach": float(source.reach), "box": box, "title": str(source.title)}
	for source in spatial.life_traces:
		var point := Vector2(float(source.x), float(source.y))
		result[str(source.id)] = {"point": point, "reach": float(source.reach), "title": str(source.title)}
	for id in spatial.extra_sites:
		var site: Dictionary = spatial.extra_sites[id]
		if str(site.kind) == "board":
			result[id] = {"point": Vector2(float(site.x), float(site.y)), "reach": float(site.reach), "title": str(site.title)}
	return result

func anchor(name: String):
	if spatial.staging.has(name): return Vector2(float(spatial.staging[name][0]), float(spatial.staging[name][1]))
	if name.begins_with("post:"):
		var post: Array = spatial.posts[name.substr(5)]
		return Vector2(float(post[0]), float(post[1]))
	if name.begins_with("meeting:"): return ($MeetingSpots.get_node(name.substr(8)) as Marker2D).position
	if name == "wake": return $WakePosition.position
	if name.begins_with("site:"):
		var geometry := site_geometry({})
		var id := name.substr(5)
		if geometry.has(id): return geometry[id].point
	return null

## 普通家具与画在墙上地上的装饰：点到哪一件（同一件只说一次由章节记）。
func furniture_hits(pos: Vector2) -> Dictionary:
	for node in $ObjectsAndPeople.get_children():
		if node is not Sprite2D: continue
		var sprite := node as Sprite2D
		if sprite.texture == null: continue
		if Rect2(sprite.position + sprite.offset, sprite.texture.get_size()).has_point(pos):
			var hit := false
			for prefix in spatial.furniture_asides:
				if str(node.name).begins_with(prefix): hit = true
			if hit: return {"nodes": [str(node.name)]}
	for index in layout.asides.size():
		var decor: Array = layout.asides[index]
		if Rect2(decor[0][0], decor[0][1], decor[0][2], decor[0][3]).has_point(pos):
			return {"nodes": ["decor%d" % index], "texts": {"decor%d" % index: decor[1]}}
	return {}

## 日常的去处：热点只供几何定位。
# STORY-HARDCODE[AWK-041][schema][production]: 日常行动目标映射到本章 piano/tube/sink/door/score 等现场物件。
func station(target: String, who: String) -> Dictionary:
	match target:
		"self":
			var start = anchor("起点·" + who)
			return {} if start == null else {"point": start, "radius": 12.0}
		"piano":
			var piano := $ObjectsAndPeople.get_node_or_null("Piano") as Node2D
			return {} if piano == null else {"point": piano.position + Vector2(0, 32), "focus": piano.position, "radius": 16.0}
	var site := {"tube": "E8", "sink": "E9", "door": "E8j", "score": "E6"}
	if site.has(target):
		var geometry := site_geometry({})
		if geometry.has(site[target]):
			return {"point": geometry[site[target]].point, "radius": float(geometry[site[target]].reach) - 10.0}
	var node := str({"table": "StudyTable", "prep": "Worktable"}.get(target, ""))
	var prop: Node2D = $ObjectsAndPeople.get_node_or_null(node) as Node2D if node != "" else null
	if prop != null: return {"point": prop.position + Vector2(0, -24), "radius": 78.0 if target == "table" else 62.0}
	return {}

# ----------------------------------------------------------------- replay ---

## 昨夜回放在真实地图上：现场让出镜头，界面条隐藏，看完回执。
func play_replay(ev: Dictionary) -> void:
	await stage_node.play(ev)

func set_bar_visible(value: bool) -> void:
	if field and field.fader and field.fader.has_method("set_bar_visible"): field.fader.set_bar_visible(value)
