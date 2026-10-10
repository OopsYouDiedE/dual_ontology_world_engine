extends Node2D
## 通用世界现场：玩家行走（WASD / 点击 / 走近即开）、悬停与最近交互点、人物、视线、走位与镜头。
## 地图差异由地图适配器提供（几何、房间、锚点、交互点位置、道具状态、昨夜回放）；
## 游戏内容只来自快照与 stage 事件。现场的输入一律以 field_event 发出，由宿主转给 C#。
## 接口与分层见设计第四部分 §2。

const ACTOR := preload("res://scenes/Story/StoryActor.gd")
const FieldNavigation := preload("res://scenes/Shared/FieldNavigation.gd")
const FieldVocabulary := preload("res://scenes/Shared/Field/FieldVocabulary.gd")
const SightMaskScene := preload("res://scenes/UI/Panels/Field/SightMask.tscn")
const HintOverlayScene := preload("res://scenes/UI/Panels/Field/HintOverlay.tscn")
const PIXEL_FRAMES := "res://ShareAsset/Internal/Characters/PixelCast/%s.tres"
const ANIME_FRAMES := "res://ShareAsset/Internal/Characters/AnimeCast/%s.tres"
const CLICK_STOP_RADIUS := 8.0
const HUD_BAR_HEIGHT := 60.0
const CLICK_REPATH_DELAY := 0.15
const CLICK_MAX_BLOCKED_REPATHS := 4
const MAKE_WAY_COOLDOWN_MS := 1000
const APPROACH := [Vector2.UP, Vector2.DOWN, Vector2.LEFT, Vector2.RIGHT, Vector2(-1, -1), Vector2(1, -1), Vector2(-1, 1), Vector2(1, 1)]
const NEAR_SITE_RADIUS := 85.0
const DAYPART_TINT := {"dawn": Color(1.0, 0.96, 0.9), "morning": Color(1, 1, 1), "noon": Color(1, 1, 0.97),
	"afternoon": Color(1.0, 0.95, 0.86), "evening": Color(0.96, 0.82, 0.7), "night": Color(0.45, 0.5, 0.68)}

signal field_event(event: Dictionary)
signal hover_changed(text: String, screen_position: Vector2)
signal hint_changed(text: String)
signal closing

## 地图适配器（Node2D，含 objects_root、几何与锚点）。
var adapter: Node2D
## 宿主注入：界面是否占用（对白、弹窗）；以及界面的淡入淡出。
var ui_busy := func() -> bool: return false
var fader: Object

var player: CharacterBody2D
var actors: Dictionary = {}          # key → CharacterBody2D
var camera: Camera2D
var sight: Node2D
var hints: Node2D
var tint: CanvasModulate
var follow_camera := true

var snapshot: Dictionary = {}
var sites: Dictionary = {}           # id → {title, point, reach, box?, marker?, person?, new}
var nearest := ""
var hovered := ""
var _hover_text := ""
var _hint_text := ""
var _was_active := false
var _active_since := 0
var _closing := false
var _court_layout := false
var _applying_scene := false

var click_path := PackedVector2Array()
var click_waypoint := 0
var click_target := ""
var _click_last := Vector2(INF, INF)
var _click_stall := 0.0
var _click_repaths := 0
var _click_anchor := Vector2(INF, INF)

var _motion: Dictionary = {}         # instance id → generation
var _daily: Dictionary = {}          # who → job
var _paused_walks: Dictionary = {}   # who → remaining path
var _make_way_after: Dictionary = {} # instance id → next allowed sidestep (ticks msec)

# ============================================================== lifecycle ===

func setup(map_adapter: Node2D) -> void:
	adapter = map_adapter
	add_child(adapter)
	move_child(adapter, 0)
	adapter.field = self
	camera = Camera2D.new()
	camera.zoom = Vector2(2, 2)
	add_child(camera)
	camera.make_current()
	tint = CanvasModulate.new()
	tint.color = Color.WHITE
	add_child(tint)
	sight = SightMaskScene.instantiate()
	sight.name = "Sight"
	sight.z_index = 40
	sight.host = adapter
	add_child(sight)
	hints = HintOverlayScene.instantiate()
	hints.z_index = 50
	add_child(hints)
	hints.set("source", self)
	adapter.setup()
	for key in adapter.fixed_actors():
		actors[key] = adapter.fixed_actors()[key]
	player = actors.get("detective")
	if player == null:
		player = _make_actor("detective", "detective")
	camera.position = adapter.camera_point(player.position)
	camera.position_smoothing_enabled = adapter.camera_smoothing()
	_after_map_load()

func begin_shutdown() -> void:
	if _closing: return
	_closing = true
	closing.emit()
	for actor in actors.values():
		if is_instance_valid(actor):
			_cancel_motion(actor)
	if adapter and adapter.has_method("begin_shutdown"): adapter.begin_shutdown()
	_cancel_click()

func _exit_tree() -> void:
	begin_shutdown()

# ================================================================ snapshot ===

func apply_snapshot(value: Dictionary) -> void:
	snapshot = value
	if is_instance_valid(tint):
		tint.color = DAYPART_TINT.get(str(value.get("daypart", "")), Color.WHITE) if adapter.uses_daypart_tint() else Color.WHITE
	if adapter.has_method("apply_snapshot"): adapter.apply_snapshot(value)
	if _court_layout: _place_court()
	elif adapter.fixed_actors().is_empty(): _spawn_people(true)
	_build_sites()

func field_active() -> bool:
	return not _closing and bool(snapshot.get("field_enabled", false)) and not ui_busy.call() and not adapter.switching()

func player_controlled() -> bool:
	return bool(snapshot.get("player_control", true))

## 随每个输入附上的几何事实。
func context() -> Dictionary:
	var facts := {}
	for key in actors:
		var actor: CharacterBody2D = actors[key]
		if not is_instance_valid(actor): continue
		var near := []
		for id in sites:
			if not sites[id].has("person") and actor.position.distance_to(sites[id].point) <= NEAR_SITE_RADIUS:
				near.append(id)
		if adapter.has_method("near_doorway") and adapter.near_doorway(actor.position): near.append("doorway")
		facts[key] = {
			"room": adapter.room_of(actor.position), "entered": adapter.entered_room(actor.position),
			"room_name": adapter.room_name(actor.position), "visible": actor == player or (actor.visible and in_sight(actor.position)),
			"moving": actor.travelling and not _daily.has(key), "distance": actor.position.distance_to(player.position),
			"near_sites": near,
		}
	return {"field_active": field_active(), "actors": facts, "map": adapter.map_key(), "site_rooms": _site_rooms()}

func _site_rooms() -> Dictionary:
	var rooms := {}
	for id in sites:
		if not sites[id].has("person"):
			rooms[id] = adapter.room_of(sites[id].point)
	return rooms

# =================================================================== sites ===

func _build_sites() -> void:
	sites.clear()
	var defs: Dictionary = snapshot.get("sites", {})
	var geometry: Dictionary = adapter.site_geometry(defs)
	for id in geometry:
		var def: Dictionary = defs.get(id, {})
		if def.is_empty() and not geometry[id].get("fallback", false): continue
		if not bool(def.get("enabled", true)): continue
		var site: Dictionary = geometry[id].duplicate()
		site.title = str(def.get("title", site.get("title", id)))
		site.new = bool(def.get("new", false))
		sites[id] = site
	for person in snapshot.get("people", []):
		var key := str(person.key)
		if not actors.has(key) or key == "detective": continue
		var site_id := "person:" + key
		if defs.has(site_id) and not bool(defs[site_id].get("enabled", true)): continue
		if not defs.has(site_id) and not adapter.fixed_actors().is_empty(): continue
		sites[site_id] = {"person": key, "title": str(person.get("title", person.get("name", key))), "reach": 44.0, "new": false}
	# 快照可能移除上一帧的人物或热点；同步最近交互点，悬停在下一帧按鼠标重新选取。
	if not sites.has(hovered): hovered = ""
	_pick_nearest()

func site_visible(id: String) -> bool:
	if not sites.has(id): return false
	if sites[id].has("person"):
		var actor: CharacterBody2D = actors.get(sites[id].person)
		return is_instance_valid(actor) and actor.visible
	# 挂在墙上的物件（隔离门、门框接头）的点落在墙格里：从它朝向的房间看得见。
	var point: Vector2 = sites[id].point
	return in_sight(point) or in_sight(point.move_toward(player.position, 16.0))

func site_is_new(id: String) -> bool:
	return sites.has(id) and bool(sites[id].get("new", false))

func site_point(id: String) -> Vector2:
	var site: Dictionary = sites[id]
	return actors[site.person].position if site.has("person") else site.point

func site_marker(id: String) -> Vector2:
	if sites[id].has("person"): return site_point(id) + Vector2(0, -50)
	return sites[id].get("marker", sites[id].point + Vector2(0, -14))

func site_box(id: String) -> Rect2:
	var p := site_point(id)
	if sites[id].has("person"): return Rect2(p + Vector2(-12, -46), Vector2(24, 48))
	return sites[id].get("box", Rect2(p + Vector2(-14, -22), Vector2(28, 26)))

func in_sight(point: Vector2) -> bool:
	return sight == null or sight.can_see(adapter.sight_point(point))

## 玩家画面记录：只列当前入镜且在视线内的物件；遮挡范围使用 viewport 屏幕坐标。
func player_visible_objects(occluded_regions: Array[Rect2] = []) -> Array[String]:
	var result: Array[String] = []
	if not is_inside_tree() or not is_instance_valid(adapter) or not is_instance_valid(player): return result
	var objects: Node = adapter.objects_root()
	if not is_instance_valid(objects): return result
	var counts := {}
	var represented_sites := {}
	var sprites: Array[Sprite2D] = []
	_player_object_sprites(objects, sprites)
	for sprite in sprites:
		if sprite.texture == null or str(sprite.name) == "BedQuiltForeground": continue
		if not _player_object_drawn(sprite): continue
		var exposed := _player_object_exposed(sprite.get_global_transform_with_canvas() * sprite.get_rect(), occluded_regions)
		if exposed.is_empty(): continue
		var site_id := _player_object_site(sprite)
		var has_site: bool = site_id != "" and sites.has(site_id) and not sites[site_id].has("person")
		var visible_site: bool = has_site and site_visible(site_id)
		var point := to_local(sprite.global_position)
		# 室内家具仍按所属房间判断，避免高家具的顶端跨墙泄漏另一房间。
		var object_in_sight: bool = in_sight(point) or visible_site
		if not adapter.sight_terrain_fog():
			object_in_sight = _player_object_exposed_in_sight(exposed)
		if not object_in_sight: continue
		# STORY-HARDCODE[CSH-033][schema][production]: 特定章节纹理到油灯/柜体可见名称的绑定；不是剧情因果规则。
		# nightstand 原生 20×36 纹理同时画了柜体和油灯，分别按像素外观判断入镜。
		if sprite.texture.resource_path == "res://ShareAsset/Internal/Props/FirstAwakeningInterior/nightstand.png" and sprite.get_rect().size == Vector2(20, 36):
			var texture_origin := sprite.get_rect().position
			var lamp_rect := Rect2(texture_origin + Vector2(6, 4), Vector2(8, 13))
			var cabinet_rect := Rect2(texture_origin + Vector2(1, 16), Vector2(18, 20))
			if not _player_object_exposed(sprite.get_global_transform_with_canvas() * lamp_rect, occluded_regions).is_empty():
				var lamp_title := FieldVocabulary.word("床头油灯")
				counts[lamp_title] = int(counts.get(lamp_title, 0)) + 1
			if _player_object_exposed(sprite.get_global_transform_with_canvas() * cabinet_rect, occluded_regions).is_empty(): continue
		var title := _player_object_readable(str(sites[site_id].title)) if visible_site else _player_object_title(sprite)
		if title == "": title = _player_object_title(sprite)
		if str(sprite.name) == "DoorFace": title += FieldVocabulary.word("门关")
		elif str(sprite.name) == "DoorGap": title += FieldVocabulary.word("门开")
		counts[title] = int(counts.get(title, 0)) + 1
		if visible_site: represented_sites[site_id] = true
	_player_object_floor_decor(counts, occluded_regions)
	# 宿舍门的门扇由 _draw 绘制，没有 Sprite2D；只读取可见门扇的开合外观。
	for node in objects.get_children():
		if node is not Node2D or not node.has_method("door_click_rect") or not _player_object_drawn(node): continue
		var door := node as Node2D
		var openness := float(door.get("openness"))
		var leaf_texture := door.get("left") as Texture2D
		if leaf_texture == null: continue
		var width := roundf(32.0 * (1.0 - openness) + 5.0 * openness)
		var height := float(leaf_texture.get_height())
		var door_transform := door.get_global_transform_with_canvas()
		var left_exposed := _player_object_exposed(door_transform * Rect2(-32, -height, width, height), occluded_regions)
		var right_exposed := _player_object_exposed(door_transform * Rect2(32 - width, -height, width, height), occluded_regions)
		if left_exposed.is_empty() and right_exposed.is_empty(): continue
		# 门道和墙面没有室内地板迷雾；按门扇真正露出的像素判断，不以逻辑房间排除。
		if not _player_object_unshaded(left_exposed) and not _player_object_unshaded(right_exposed): continue
		var title := FieldVocabulary.word("宿舍门开") if openness > 0.05 else FieldVocabulary.word("宿舍门关")
		counts[title] = int(counts.get(title, 0)) + 1
	# 痕迹和墙面热点未必是独立精灵，沿用已开放热点当前显示的标题。
	for id in sites:
		if sites[id].has("person") or represented_sites.has(id) or not site_visible(id): continue
		var hidden := false
		if "spatial" in adapter:
			var props: Dictionary = adapter.get("spatial").get("objects", {})
			if props.has(id):
				var prop := objects.get_node_or_null(str(props[id].get("sprite", ""))) as CanvasItem
				hidden = prop == null or not _player_object_drawn(prop)
		if hidden: continue
		if _player_object_exposed(get_global_transform_with_canvas() * site_box(id), occluded_regions).is_empty(): continue
		var title := _player_object_readable(str(sites[id].title))
		if title == str(id): title = FieldVocabulary.word("未命名")
		if title != "" and not counts.has(title): counts[title] = 1
	var titles: Array = counts.keys()
	titles.sort()
	for title in titles:
		result.append(str(title) + (" × %d" % int(counts[title]) if int(counts[title]) > 1 else ""))
	return result

func _player_object_sprites(node: Node, found: Array[Sprite2D]) -> void:
	for child in node.get_children():
		if child is CharacterBody2D or child is Control or child is CanvasLayer: continue
		if child is Sprite2D: found.append(child)
		_player_object_sprites(child, found)

func _player_object_floor_decor(counts: Dictionary, occluded_regions: Array[Rect2]) -> void:
	if adapter.map_key() != "awakening" or not ("layout" in adapter): return
	var architecture := adapter.get_node_or_null("LifeWingArchitecture") as Sprite2D
	if architecture == null or architecture.texture == null or not _player_object_drawn(architecture): return
	var architecture_rect: Rect2 = architecture.get_global_transform_with_canvas() * architecture.get_rect()
	var map_layout: Dictionary = adapter.get("layout")
	# 此表的地毯矩形由真实 floor_work 烘焙区域生成，只取物件名，不读取踩痕旁白。
	for decor in map_layout.get("asides", []):
		if decor is not Array or decor.size() < 2 or not str(decor[1]).begins_with(FieldVocabulary.word("地毯旁白前缀")): continue
		var raw: Array = decor[0]
		if raw.size() != 4: continue
		var decor_rect := Rect2(float(raw[0]), float(raw[1]), float(raw[2]), float(raw[3]))
		if not in_sight(decor_rect.get_center()): continue
		var screen_rect: Rect2 = (adapter.get_global_transform_with_canvas() * decor_rect).intersection(architecture_rect)
		var exposed := _player_object_exposed(screen_rect, occluded_regions)
		if not exposed.is_empty() and _player_object_exposed_in_sight(exposed):
			counts[FieldVocabulary.word("地毯")] = int(counts.get(FieldVocabulary.word("地毯"), 0)) + 1
	# sconces 锚点为墙格顶端 +36；原生烘焙灯具本体位于锚点(-3,-23)至(4,-6)。
	for anchor in map_layout.get("sconces", []):
		if anchor is not Array or anchor.size() != 2: continue
		var lamp_rect := Rect2(float(anchor[0]) - 3.0, float(anchor[1]) - 23.0, 7.0, 17.0)
		var screen_rect: Rect2 = (adapter.get_global_transform_with_canvas() * lamp_rect).intersection(architecture_rect)
		var exposed := _player_object_exposed(screen_rect, occluded_regions)
		if _player_object_unshaded(exposed): counts[FieldVocabulary.word("壁灯")] = int(counts.get(FieldVocabulary.word("壁灯"), 0)) + 1

func _player_object_unshaded(pieces: Array[Rect2]) -> bool:
	var inverse := get_global_transform_with_canvas().affine_inverse()
	for piece in pieces:
		var world_rect: Rect2 = inverse * piece
		for y in range(floori(world_rect.position.y / 32.0), ceili(world_rect.end.y / 32.0)):
			for x in range(floori(world_rect.position.x / 32.0), ceili(world_rect.end.x / 32.0)):
				var cell := Vector2i(x, y)
				var fragment := world_rect.intersection(Rect2(Vector2(cell) * 32.0, Vector2(32, 32)))
				if fragment.has_area() and (sight == null or not adapter.sight_terrain_fog() or sight.terrain_shade(cell).a < 0.99): return true
	return false

func _player_object_drawn(item: CanvasItem) -> bool:
	if not item.is_visible_in_tree() or item.self_modulate.a <= 0.01: return false
	var ancestor: Node = item
	var alpha := item.self_modulate.a
	while ancestor is CanvasItem:
		alpha *= (ancestor as CanvasItem).modulate.a
		ancestor = ancestor.get_parent()
	return alpha > 0.01

## 顺次扣除所有遮挡矩形，联合遮挡也不能留下已完全盖住的物件。
func _player_object_exposed(screen_rect: Rect2, occluded_regions: Array[Rect2]) -> Array[Rect2]:
	var pieces: Array[Rect2] = []
	var clipped := screen_rect.intersection(get_viewport().get_visible_rect())
	if not clipped.has_area(): return pieces
	pieces.append(clipped)
	for cover in occluded_regions:
		var remaining: Array[Rect2] = []
		for piece in pieces:
			var overlap := piece.intersection(cover)
			if not overlap.has_area():
				remaining.append(piece)
				continue
			for fragment in [
				Rect2(piece.position, Vector2(piece.size.x, overlap.position.y - piece.position.y)),
				Rect2(Vector2(piece.position.x, overlap.end.y), Vector2(piece.size.x, piece.end.y - overlap.end.y)),
				Rect2(Vector2(piece.position.x, overlap.position.y), Vector2(overlap.position.x - piece.position.x, overlap.size.y)),
				Rect2(Vector2(overlap.end.x, overlap.position.y), Vector2(piece.end.x - overlap.end.x, overlap.size.y))]:
				if fragment.has_area(): remaining.append(fragment)
		pieces = remaining
		if pieces.is_empty(): break
	return pieces

func _player_object_exposed_in_sight(pieces: Array[Rect2]) -> bool:
	var inverse := get_global_transform_with_canvas().affine_inverse()
	for piece in pieces:
		var world_rect: Rect2 = inverse * piece
		if in_sight(world_rect.get_center()): return true
		for y in range(floori(world_rect.position.y / 32.0), ceili(world_rect.end.y / 32.0)):
			for x in range(floori(world_rect.position.x / 32.0), ceili(world_rect.end.x / 32.0)):
				var cell_piece := world_rect.intersection(Rect2(Vector2(x, y) * 32.0, Vector2(32, 32)))
				if cell_piece.has_area() and in_sight(cell_piece.get_center()): return true
	return false

func _player_object_site(sprite: Sprite2D) -> String:
	return str(FieldVocabulary.table("SPRITE_SITES").get(str(sprite.name), "")) if adapter.map_key() == "awakening" else ""

func _player_object_title(node: Node) -> String:
	for key in ["player_visible_name", "title", "label", "text"]:
		var value = node.get_meta(key, "")
		if value is String:
			var title := _player_object_readable(value)
			if title != "": return title
	# 同样叫 Bed、Desk、Frame 或 Stand 的地图精灵可能绘制不同物件，依据实际纹理区分。
	if node is Sprite2D and node.texture != null:
		var texture_name: String = node.texture.resource_path.get_file().get_basename()
		var texture_names := FieldVocabulary.table("TEXTURE_NAMES")
		if texture_names.has(texture_name): return str(texture_names[texture_name])
		var prefixes := FieldVocabulary.table("TEXTURE_PREFIX_NAMES")
		for prefix in prefixes:
			if texture_name.begins_with(str(prefix)): return str(prefixes[prefix])
	# 只映射物件的外观名称，不使用家具旁白中关于抽屉、柜内或证物内容的事实。
	var names := FieldVocabulary.table("OBJECT_NAMES")
	var node_name := str(node.name)
	var match_name := node_name.to_lower()
	var best := ""
	for key in names:
		if match_name.contains(str(key).to_lower()) and str(key).length() > best.length(): best = str(key)
	if best != "": return str(names[best])
	# 地图已有中文名称可以保留，移除复制实例序号；英文调试名不作为玩家标题。
	var chinese := false
	for index in node_name.length():
		var code := node_name.unicode_at(index)
		if code >= 0x4e00 and code <= 0x9fff: chinese = true
	if chinese:
		while node_name.length() > 0 and (node_name.right(1).is_valid_int() or node_name.ends_with("_")): node_name = node_name.left(-1)
		return node_name
	return FieldVocabulary.word("未命名")

func _player_object_readable(value: String) -> String:
	var title := value.strip_edges()
	if title.begins_with("res://") or title.begins_with("user://") or title.contains("/") or title.contains("\\"): return ""
	return title

# ================================================================ physics ===

func _physics_process(delta: float) -> void:
	if _closing or player == null: return
	if follow_camera:
		camera.position = adapter.camera_point(player.position)
	adapter.physics_update(delta)
	_update_daily(delta)
	_update_paused_walks()
	_make_way()
	_pick_nearest()
	if bool(snapshot.get("realtime", false)):
		field_event.emit({"kind": "tick", "delta": delta})
	if not player_controlled():
		_cancel_click()
		return
	if not field_active():
		_cancel_click()
		if not player.travelling:
			player.velocity = Vector2.ZERO
			if not player.seated: player.pose("idle")
		return
	var direction := Vector2(
		float(Input.is_action_pressed("move_right")) - float(Input.is_action_pressed("move_left")),
		float(Input.is_action_pressed("move_down")) - float(Input.is_action_pressed("move_up")))
	var scale := 1.0
	if direction != Vector2.ZERO:
		_cancel_click()
		if player.seated: _leave_seat()
	elif click_target != "" and sites.has(click_target):
		if player.position.distance_to(site_point(click_target)) <= float(sites[click_target].reach) + 6.0:
			var id := click_target
			_cancel_click()
			player.move_input(Vector2.ZERO)
			interact(id)
			return
	if direction == Vector2.ZERO and not click_path.is_empty():
		_click_stall = _click_stall + delta if player.position.distance_to(_click_last) < 0.25 else 0.0
		_click_last = player.position
		if player.position.distance_to(_click_anchor) > 12.0:
			_click_anchor = player.position
			_click_repaths = 0
		if _click_stall >= CLICK_REPATH_DELAY:
			var pending := click_target
			var destination := click_path[-1]
			_click_stall = 0.0
			_click_repaths += 1
			if _click_repaths > CLICK_MAX_BLOCKED_REPATHS:
				_cancel_click()
				return
			if pending == "": _route_click(destination)
			else: _approach(pending)
			return
		while click_waypoint < click_path.size() and player.position.distance_to(click_path[click_waypoint]) <= (CLICK_STOP_RADIUS if click_waypoint == click_path.size() - 1 else 0.01):
			click_waypoint += 1
		if click_waypoint >= click_path.size():
			var target := click_target
			_cancel_click()
			if target != "" and sites.has(target) and player.position.distance_to(site_point(target)) <= float(sites[target].reach) + 8.0:
				interact(target)
				return
		else:
			var remaining := click_path[click_waypoint] - player.position
			direction = remaining.normalized()
			scale = minf(1.0, remaining.length() / maxf(player.speed * delta, 0.00001))
	if not player.seated:
		player.move_input(direction.normalized() * scale)

func _process(_delta: float) -> void:
	if _closing or player == null: return
	var fog: bool = adapter.sight_terrain_fog()
	var objects: Node = adapter.objects_root()
	if fog and objects:
		for node in objects.get_children():
			if node is Sprite2D:
				node.self_modulate.a = 1.0 if sight.terrain_known(node.position) else 0.0
	for key in actors:
		var actor: CharacterBody2D = actors[key]
		if is_instance_valid(actor) and actor != player:
			actor.visible = in_sight(actor.position)
	var active := field_active()
	if active and not _was_active: _active_since = Time.get_ticks_msec()
	_was_active = active
	hovered = ""
	if active:
		var mouse := get_global_mouse_position()
		var best := INF
		for id in sites:
			if site_visible(id) and site_box(id).grow(3).has_point(mouse):
				var d := site_box(id).get_center().distance_squared_to(mouse)
				if d < best:
					best = d
					hovered = id
	var hover_text := str(sites[hovered].title) if sites.has(hovered) else ""
	if active and hover_text == "" and objects:
		var hits: Dictionary = adapter.furniture_hits(get_global_mouse_position())
		for object_name in hits.get("nodes", []):
			var furniture := objects.get_node_or_null(str(object_name)) as Sprite2D
			if furniture and _player_object_drawn(furniture) and in_sight(furniture.position):
				hover_text = _player_object_title(furniture)
				break
	if hover_text != _hover_text or hover_text != "":
		_hover_text = hover_text
		hover_changed.emit(hover_text, get_viewport().get_mouse_position())
	var focus := hovered if sites.has(hovered) else nearest
	var hint := "" if not active or not sites.has(focus) else str(sites[focus].title) + ("　［E］" if focus == nearest else "")
	if hint != _hint_text:
		_hint_text = hint
		hint_changed.emit(hint)

func _pick_nearest() -> void:
	nearest = ""
	if not field_active(): return
	var best := INF
	for id in sites:
		if not site_visible(id): continue
		var d := player.position.distance_to(site_point(id))
		if d <= float(sites[id].reach) and d < best:
			best = d
			nearest = id

# ================================================================== input ===

func _input(event: InputEvent) -> void:
	if not field_active() or not player_controlled(): return
	if event is not InputEventMouseButton or not event.pressed or event.button_index != MOUSE_BUTTON_LEFT: return
	if event.position.y < HUD_BAR_HEIGHT:
		get_viewport().set_input_as_handled()
		return
	_click_repaths = 0
	_click_anchor = player.position
	_cancel_click()
	var at := get_global_mouse_position()
	var chosen := ""
	var best := INF
	for id in sites:
		if site_visible(id) and site_box(id).grow(4).has_point(at):
			var d := site_box(id).get_center().distance_squared_to(at)
			if d < best:
				best = d
				chosen = id
	if chosen == "":
		var hits: Dictionary = adapter.furniture_hits(at)
		if not hits.get("nodes", []).is_empty():
			field_event.emit({"kind": "furniture", "map": adapter.map_key(), "nodes": hits.nodes, "texts": hits.get("texts", {})})
		_route_click(at)
	else:
		_approach(chosen)
	get_viewport().set_input_as_handled()

func _unhandled_input(event: InputEvent) -> void:
	if event is not InputEventKey and event is not InputEventMouseButton: return
	if not event.is_pressed() or event.is_echo(): return
	if not field_active() or not player_controlled(): return
	if event.is_action_pressed("interact") and nearest != "":
		# 对白刚结束时多按的一下只是在推进对白。
		if Time.get_ticks_msec() - _active_since >= 600:
			_cancel_click()
			player.move_input(Vector2.ZERO)
			interact(nearest)
		get_viewport().set_input_as_handled()

func interact(id: String) -> void:
	if not sites.has(id) or not site_visible(id): return
	if player.position.distance_to(site_point(id)) > float(sites[id].reach) + 8.0: return
	var site: Dictionary = sites[id]
	if site.has("person"):
		var actor: CharacterBody2D = actors[site.person]
		actor.face_point(player.position)
		player.face_point(actor.position)
	field_event.emit({"kind": "interact", "map": adapter.map_key(), "site": id, "target": str(site.get("person", ""))})

func _cancel_click() -> void:
	click_path = PackedVector2Array()
	click_waypoint = 0
	click_target = ""
	_click_last = Vector2(INF, INF)
	_click_stall = 0.0

func _leave_seat() -> void:
	var wake = adapter.anchor("wake")
	if wake != null: player.position = wake
	player.stand_up()

func _route_click(destination: Vector2) -> void:
	_cancel_click()
	if not adapter.walkable(destination, player): return
	if player.seated: _leave_seat()
	if player.position.distance_to(destination) <= CLICK_STOP_RADIUS: return
	var route := FieldNavigation.route_on_grid(player.position, destination, adapter.nav_grid(player))
	if route.is_empty(): return
	click_path = route
	click_path.append(destination)
	click_waypoint = 0

## 走到够得着的地方再打开；已经够得着就直接打开。
func _approach(id: String) -> void:
	_cancel_click()
	var point := site_point(id)
	var reach := float(sites[id].reach)
	if player.position.distance_to(point) <= reach + 6.0:
		interact(id)
		return
	if player.seated: _leave_seat()
	var grid: AStarGrid2D = adapter.nav_grid(player)
	var best := PackedVector2Array()
	var best_destination := Vector2.ZERO
	var best_cost := INF
	for fraction in [1.0, 0.55]:
		for heading in APPROACH:
			var candidate: Vector2 = point + heading.normalized() * (reach - CLICK_STOP_RADIUS - 2.0) * fraction
			if not adapter.walkable(candidate, player): continue
			var route := FieldNavigation.route_on_grid(player.position, candidate, grid)
			if route.is_empty(): continue
			var cost := _route_cost(player.position, route, candidate)
			if cost < best_cost:
				best_cost = cost
				best = route
				best_destination = candidate
	if best.is_empty(): return
	click_path = best
	click_path.append(best_destination)
	click_waypoint = 0
	click_target = id

func _route_cost(from: Vector2, route: PackedVector2Array, to: Vector2) -> float:
	var cost := from.distance_to(route[0]) + route[route.size() - 1].distance_to(to)
	for i in range(1, route.size()): cost += route[i - 1].distance_to(route[i])
	return cost

# ================================================================ people ===

func _make_actor(key: String, sprite_key: String) -> CharacterBody2D:
	var actor := CharacterBody2D.new()
	actor.set_script(ACTOR)
	var path := PIXEL_FRAMES % sprite_key
	if not ResourceLoader.exists(path): path = ANIME_FRAMES % sprite_key
	if not ResourceLoader.exists(path): path = PIXEL_FRAMES % "detective"
	actor.standard = load(path)
	actor.name = key
	adapter.objects_root().add_child(actor)
	actor.collision_mask = 1
	actor.speed = 130.0 if key == "detective" else 110.0
	actors[key] = actor
	return actor

## 多地图章节：此处的人站到各自的位置。animate 时新来的从门口走进来，离开的走向门口。
func _spawn_people(animate: bool) -> void:
	var shown := {}
	for p in snapshot.get("people", []):
		if str(p.get("map", "")) == adapter.map_key() and bool(p.get("present", true)):
			shown[str(p.key)] = p
	var stands: Array = adapter.stands()
	var door: Vector2 = adapter.door_point(player.position)
	for key in actors.keys():
		if key == "detective" or shown.has(key): continue
		var leaving: CharacterBody2D = actors[key]
		actors.erase(key)
		if animate and is_instance_valid(leaving): _walk_off(leaving, door)
		elif is_instance_valid(leaving): leaving.queue_free()
	var index := 0
	for key in shown:
		var p: Dictionary = shown[key]
		var spot: Vector2 = stands[(absi(key.hash()) + index) % stands.size()] if not stands.is_empty() else player.position + Vector2(40 * index, 30)
		for other in actors.values():
			if other != player and other.position.distance_to(spot) < 20: spot += Vector2(26, 8)
		index += 1
		var actor: CharacterBody2D = actors.get(key)
		if actor == null:
			actor = _make_actor(key, str(p.get("sprite", "detective")))
			if animate:
				actor.position = door
				_walk_on(actor, spot, str(p.get("frame", "")))
			else:
				actor.position = spot
				_frame_pose(actor, str(p.get("frame", "")))
		else:
			_frame_pose(actor, str(p.get("frame", "")))

func _walk_on(actor: CharacterBody2D, spot: Vector2, frame: String) -> void:
	await _follow_route(actor, spot)
	if not _closing and is_instance_valid(actor): _frame_pose(actor, frame)

func _walk_off(actor: CharacterBody2D, door: Vector2) -> void:
	await _follow_route(actor, door)
	if is_instance_valid(actor): actor.queue_free()

func _frame_pose(actor: CharacterBody2D, frame: String) -> void:
	actor.pose({"操作": "interact", "递物": "interact", "拉拽": "interact", "交谈": "talk", "蹲查": "inspect"}.get(frame, "idle"), "front")

func _place_court() -> void:
	# STORY-HARDCODE[CSH-035][schema][production]: 专用盟誓庭锚点与侦探位置绑定；名单由外部快照驱动。
	var court: Vector2 = adapter.anchor("site:oath_court") if adapter.anchor("site:oath_court") != null else player.position
	var i := 0
	for p in snapshot.get("people", []):
		if not bool(p.get("court_present", false)): continue
		var actor: CharacterBody2D = actors.get(str(p.key))
		if not is_instance_valid(actor): actor = _make_actor(str(p.key), str(p.get("sprite", "detective")))
		actor.position = court + Vector2((i % 5) * 34 - 68, (i / 5) * 34 - 30)
		i += 1
	player.position = court + Vector2(0, 110)

## 有人挡在侦探的路上：没有被剧情占用的同伴自己让开。
func _make_way() -> void:
	if player.velocity.length() < 10.0: return
	var pinned: Array = snapshot.get("pinned", [])
	for key in actors:
		var actor: CharacterBody2D = actors[key]
		if actor == player or not is_instance_valid(actor) or _daily.has(key) or key in pinned: continue
		if actor.travelling or actor.position.distance_to(player.position) > 20.0: continue
		var id := actor.get_instance_id()
		if Time.get_ticks_msec() < int(_make_way_after.get(id, 0)): continue
		var side := player.velocity.normalized().orthogonal() * 28.0
		for spot in [actor.position + side, actor.position - side]:
			if not adapter.walkable(spot, actor): continue
			var route := FieldNavigation.route_on_grid(actor.position, spot, adapter.nav_grid(actor))
			if route.is_empty(): continue
			route.append(spot)
			_make_way_after[id] = Time.get_ticks_msec() + MAKE_WAY_COOLDOWN_MS
			_step_aside(actor, route)
			break

func _step_aside(actor: CharacterBody2D, route: PackedVector2Array) -> void:
	var facing: String = actor.facing
	var result: Dictionary = await _follow(actor, route)
	if _closing or not is_instance_valid(actor) or str(result.reason) == "cancelled": return
	actor.pose("idle", facing)
	_make_way_after[actor.get_instance_id()] = Time.get_ticks_msec() + MAKE_WAY_COOLDOWN_MS

# ================================================================== maps ===

## 换图（多地图章节）：玩家移进新图，人物按快照重新站位；玩家自己走过去的，告诉章节到了哪里。
func load_map(key: String, spawn: String, at := Vector2.ZERO, use_at := false) -> void:
	if not _applying_scene and adapter.has_method("can_enter") and not adapter.can_enter(key):
		adapter.blocked_entry()
		field_event.emit({"kind": "blocked_move", "map": key})
		return
	_cancel_click()
	for actor_key in actors.keys():
		if actor_key == "detective": continue
		var actor: CharacterBody2D = actors[actor_key]
		if is_instance_valid(actor):
			_cancel_motion(actor)
			actor.queue_free()
		actors.erase(actor_key)
	_daily.clear()
	_make_way_after.clear()
	player.get_parent().remove_child(player)
	add_child(player)
	adapter.load_map(key, spawn)
	remove_child(player)
	adapter.objects_root().add_child(player)
	player.position = at if use_at else adapter.spawn_point(spawn)
	_after_map_load()
	# 先按快照站好此处的人，再告诉章节到了哪里（回来的快照只让新来的人走进来）。
	if adapter.fixed_actors().is_empty(): _spawn_people(false)
	_build_sites()
	if not _applying_scene and adapter.map_key() != "":
		field_event.emit({"kind": "move", "map": adapter.map_key()})

func _after_map_load() -> void:
	sight.reset()
	sight.seen_cells = adapter.seen_cells()
	camera.position = adapter.camera_point(player.position)
	var limits: Rect2 = adapter.camera_limits()
	camera.limit_left = int(limits.position.x)
	camera.limit_top = int(limits.position.y)
	camera.limit_right = int(limits.end.x)
	camera.limit_bottom = int(limits.end.y)
	camera.reset_smoothing()

func travel_to_node(node: Dictionary) -> void:
	await _fade(func(): load_map(str(node.map), str(node.spawn)))

func _fade(work: Callable) -> void:
	if fader != null: await fader.fade_to(1.0, 0.35)
	if _closing: return
	work.call()
	await get_tree().process_frame
	if fader != null and not _closing: await fader.fade_to(0.0, 0.4)

# ============================================================ stage events ===

## 章节的空间指令。带 presentation_id 的在完成后回执（ok / reason）。
func execute(ev: Dictionary) -> void:
	match str(ev.kind):
		"scene":
			_court_layout = str(ev.get("mode", "")) == "court"
			_applying_scene = true
			await _fade(func(): load_map(str(ev.map), str(ev.get("spawn", ""))))
			_applying_scene = false
			if _court_layout: _place_court()
			_ack(ev, {})
		"replay_play":
			follow_camera = false
			await adapter.play_replay(ev)
			follow_camera = true
			_ack(ev, {})
		"stage":
			_stage(ev)

func _ack(ev: Dictionary, result: Dictionary) -> void:
	if _closing or not ev.has("presentation_id"): return
	var reply := {"kind": "presented", "presentation_id": str(ev.presentation_id), "ok": true}
	reply.merge(result, true)
	field_event.emit(reply)

func _actor(who: String) -> CharacterBody2D:
	return actors.get(who)

func _stage(ev: Dictionary) -> void:
	var who := str(ev.get("who", ""))
	var actor := _actor(who)
	match str(ev.op):
		"walk": _op_walk(ev)
		"spawn":
			if actor == null: actor = _make_actor(who, str(ev.sprite))
			var point = _target_point(ev.to, actor)
			if point == null:
				_ack(ev, {"ok": false, "reason": "missing_anchor"})
				return
			_cancel_motion(actor)
			actor.position = point
			actor.pose("idle", "front")
			_ack(ev, {})
		"place":
			if actor == null:
				_ack(ev, {"ok": false})
				return
			_cancel_motion(actor)
			var point = _target_point(ev.to, actor)
			if point != null: actor.position = point
			_ack(ev, {})
		"pose":
			if actor: actor.pose(str(ev.get("anim", "idle")), str(ev.get("facing", "")))
			_ack(ev, {})
		"face":
			var toward = _target_point({"actor": str(ev.toward)} if not str(ev.toward).begins_with("site:") else {"anchor": str(ev.toward)}, actor)
			if actor and toward != null: actor.face_point(toward)
			_ack(ev, {})
		"sit":
			if actor:
				actor.pose("idle", str(ev.get("facing", "front")))
				actor.sit_at(str(ev.get("facing", "front")), float(ev.get("lift", 0.0)))
			_ack(ev, {})
		"stand":
			if actor and actor.seated: actor.stand_up()
			_ack(ev, {})
		"halt":
			if actor:
				_cancel_motion(actor)
				_paused_walks.erase(who)
				actor.pose("idle")
			_ack(ev, {})
		"cancel_click":
			_cancel_click()
			player.move_input(Vector2.ZERO)
		"camera": _op_camera(ev)
		"prop":
			adapter.set_prop(str(ev.id), str(ev.state))
			_ack(ev, {})
		"group": _group(ev)
		"daily_go": _daily_go(ev)
		"daily_work":
			if _daily.has(who) and actor:
				_daily[who].working = true
				actor.speed = float(_daily[who].previous_speed)
				actor.face_point(_daily[who].focus)
				actor.pose({"check": "inspect", "work": "interact"}.get(str(ev.get("action", "rest")), "idle"))
		"daily_release": _daily_release(who)
		_: push_error("Unknown stage op: " + str(ev.op))

# ------------------------------------------------------------------ walk ---

func _target_point(to, walker: CharacterBody2D):
	if to is Dictionary:
		if to.has("point"): return Vector2(float(to.point[0]), float(to.point[1]))
		if to.has("anchor"): return adapter.anchor(str(to.anchor))
		if to.has("actor"):
			var other := _actor(str(to.actor))
			return other.position if other else null
	return null

func _op_walk(ev: Dictionary) -> void:
	var who := str(ev.who)
	var actor := _actor(who)
	if actor == null:
		_ack(ev, {"ok": false, "reason": "no_route"})
		return
	_daily_release(who)
	_cancel_motion(actor)
	if actor.seated: actor.stand_up()
	var to: Dictionary = ev.to
	var destination = null
	var route := PackedVector2Array()
	var grid: AStarGrid2D = adapter.nav_grid(actor)
	if to.has("near_site") or to.has("near_actor"):
		var base: Vector2 = site_point(str(to.near_site)) if to.has("near_site") and sites.has(str(to.near_site)) else (_actor(str(to.get("near_actor", ""))).position if to.has("near_actor") else adapter.anchor("site:" + str(to.near_site)))
		for offset in to.get("offsets", [[0, 0]]):
			var spot: Vector2 = base + Vector2(float(offset[0]), float(offset[1]))
			if not adapter.walkable(spot, actor): continue
			if bool(to.get("avoid", false)) and _taken(spot, actor): continue
			destination = spot
			route = FieldNavigation.route_on_grid(actor.position, spot, grid)
			if not route.is_empty(): break
	elif to.has("approach_site"):
		var site_id := str(to.approach_site)
		var point: Vector2 = site_point(site_id) if sites.has(site_id) else adapter.anchor("site:" + site_id)
		var reach: float = float(sites[site_id].reach) if sites.has(site_id) else 46.0
		destination = _spot_near(point, reach - float(to.get("inset", 0.0)), actor)
	else:
		destination = _target_point(to, actor)
	if destination == null:
		_ack(ev, {"ok": false, "reason": "no_route"})
		return
	if actor.position.distance_to(destination) <= ACTOR.ARRIVAL_RADIUS:
		_ack(ev, {"reason": "arrived"})
		return
	if route.is_empty():
		route = FieldNavigation.route_on_grid(actor.position, destination, grid)
	if route.is_empty():
		_ack(ev, {"ok": false, "reason": "no_route"})
		return
	route.append(destination)
	var previous_speed: float = actor.speed
	if float(ev.get("speed", 0.0)) > 0.0: actor.speed = float(ev.speed)
	var result: Dictionary = await _follow(actor, route, who if bool(ev.get("pausable", false)) else "")
	if is_instance_valid(actor): actor.speed = previous_speed
	_ack(ev, result)

func _taken(spot: Vector2, actor: CharacterBody2D) -> bool:
	if player != actor and player.position.distance_to(spot) < 24.0: return true
	for other in actors.values():
		if other != actor and is_instance_valid(other) and other.position.distance_to(spot) < 28.0: return true
	return false

func _spot_near(point: Vector2, radius: float, walker: CharacterBody2D):
	var best = null
	var best_cost := INF
	var grid: AStarGrid2D = adapter.nav_grid(walker)
	for fraction in [1.0, 0.7]:
		for heading in APPROACH:
			var candidate: Vector2 = point + heading.normalized() * radius * fraction
			if not adapter.walkable(candidate, walker): continue
			if FieldNavigation.route_on_grid(walker.position, candidate, grid).is_empty(): continue
			var cost := walker.position.distance_to(candidate)
			if cost < best_cost:
				best_cost = cost
				best = candidate
	return best

func _next_motion(actor: CharacterBody2D) -> int:
	var id := actor.get_instance_id()
	_motion[id] = int(_motion.get(id, 0)) + 1
	return _motion[id]

func _cancel_motion(actor: CharacterBody2D) -> void:
	_next_motion(actor)
	for who in _paused_walks.keys():
		if actors.get(who) == actor: _paused_walks.erase(who)
	actor.stop_travel()

## 沿路走；受阻由角色停步并报告失败。可暂停的路线在现场不可用时停步。
func _follow(actor: CharacterBody2D, route: PackedVector2Array, pause_key := "") -> Dictionary:
	_cancel_motion(actor)
	var generation := _next_motion(actor)
	var id := actor.get_instance_id()
	actor.follow_path(route)
	while actor.travelling or _paused_walks.has(pause_key):
		await get_tree().physics_frame
		if _closing or not is_instance_valid(actor) or int(_motion.get(id, 0)) != generation:
			return {"ok": false, "reason": "cancelled"}
		if not actor.travelling and not _paused_walks.has(pause_key):
			break
		if pause_key != "" and not field_active():
			if actor.travelling:
				_paused_walks[pause_key] = actor.path.slice(actor.waypoint)
				actor.pause_travel()
			continue
		if _paused_walks.has(pause_key):
			actor.resume_travel()
			_paused_walks.erase(pause_key)
	return actor.travel_result

func _update_paused_walks() -> void:
	pass

func _follow_route(actor: CharacterBody2D, destination: Vector2) -> Dictionary:
	var route := FieldNavigation.route_on_grid(actor.position, destination, adapter.nav_grid(actor))
	if route.is_empty():
		_cancel_motion(actor)
		return {"ok": false, "reason": "no_route"}
	route.append(destination)
	return await _follow(actor, route)

# ---------------------------------------------------------------- camera ---

func _op_camera(ev: Dictionary) -> void:
	var to: Dictionary = ev.get("to", {})
	var seconds := float(ev.get("seconds", 0.5))
	# 设置里“减少动效”时镜头直接切过去，不平移。
	var settings := get_node_or_null("/root/GameSettings")
	if settings != null and bool(settings.get_value("accessibility", "reduce_motion")):
		seconds = 0.0
	var target: Vector2
	if bool(to.get("follow", false)):
		target = adapter.camera_point(player.position)
	else:
		follow_camera = false
		target = _target_point(to, player)
	if seconds <= 0.0:
		camera.position = target
	else:
		var tween := create_tween()
		tween.tween_property(camera, "position", target, seconds)
		await tween.finished
	if bool(to.get("follow", false)): follow_camera = true
	_ack(ev, {})

func _group(ev: Dictionary) -> void:
	var court = adapter.anchor(str(ev.get("anchor", "site:oath_court")))
	if court == null: court = player.position
	var groups := {}
	for key in ev.stands:
		var g: String = ev.stands[key]
		if not groups.has(g): groups[g] = []
		groups[g].append(key)
	var gi := 0
	for g in groups:
		var base: Vector2 = court + Vector2(-240 + gi * 160, -60)
		var j := 0
		for key in groups[g]:
			if actors.has(key): actors[key].walk_to(base + Vector2((j % 3) * 30, (j / 3) * 30))
			j += 1
		gi += 1
	_ack(ev, {})

# ----------------------------------------------------------------- daily ---

## 日常：章节给出去处（房间与站点），现场找一个够得着、不与别人重叠的落脚点走过去，到了或被挡都回报。
func _daily_go(ev: Dictionary) -> void:
	var who := str(ev.who)
	var actor := _actor(who)
	if actor == null: return
	var station: Dictionary = adapter.station(str(ev.target), who)
	if station.is_empty():
		_daily_report(who, int(ev.serial), "blocked", "")
		return
	var grid: AStarGrid2D = adapter.nav_grid(actor)
	var destination := Vector2.ZERO
	var best_route := PackedVector2Array()
	var best_cost := INF
	for fraction in [1.0, 0.75]:
		for heading in APPROACH:
			var point: Vector2 = station.point + heading.normalized() * float(station.radius) * fraction
			if adapter.entered_room(point) != str(ev.room) or not adapter.walkable(point, actor): continue
			var reserved := false
			for other in _daily.values():
				if point.distance_to(other.destination) < 28.0: reserved = true
			if reserved: continue
			var route := FieldNavigation.route_on_grid(actor.position, point, grid)
			if route.is_empty(): continue
			var cost := _route_cost(actor.position, route, point)
			if cost < best_cost:
				best_cost = cost
				destination = point
				best_route = route
	if best_route.is_empty():
		_daily_report(who, int(ev.serial), "blocked", "")
		return
	_cancel_motion(actor)
	if actor.seated: actor.stand_up()
	_daily[who] = {"serial": int(ev.serial), "room": str(ev.room), "action": str(ev.get("action", "rest")), "destination": destination,
		"focus": station.get("focus", station.point), "last": actor.position, "still": 0.0, "working": false, "previous_speed": actor.speed}
	actor.speed *= clampf(float(ev.get("speed", 1.0)), 0.5, 2.0)
	best_route.append(destination)
	actor.follow_path(best_route)

func _daily_release(who: String) -> void:
	if not _daily.has(who): return
	var job: Dictionary = _daily[who]
	var actor := _actor(who)
	_daily.erase(who)
	if actor == null: return
	_cancel_motion(actor)
	actor.speed = float(job.previous_speed)
	actor.pose("idle")

func _update_daily(delta: float) -> void:
	for who in _daily.keys():
		var job: Dictionary = _daily[who]
		var actor := _actor(who)
		if actor == null or bool(job.working) or bool(job.get("reported", false)): continue
		if not actor.travelling:
			var room: String = adapter.entered_room(actor.position)
			var arrived: bool = bool(actor.travel_result.ok) and actor.position.distance_to(job.destination) <= 4.0 and room == str(job.room)
			job.reported = true
			_daily_report(who, int(job.serial), "arrived" if arrived else "blocked", room, str(job.action))
			continue
		job.still = float(job.still) + delta if actor.position.distance_to(job.last) < 0.2 else 0.0
		job.last = actor.position
		if float(job.still) >= 1.0:
			job.reported = true
			_daily_report(who, int(job.serial), "blocked", adapter.entered_room(actor.position))

func _daily_report(who: String, serial: int, status: String, room: String, action := "rest") -> void:
	if status != "arrived": _daily_release(who)
	field_event.emit({"kind": "daily", "who": who, "serial": serial, "status": status, "room": room, "action": action})
