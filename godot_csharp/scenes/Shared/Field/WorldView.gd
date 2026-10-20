extends Node2D
## 通用世界现场：执行正式 DV 移动意图，承接人物、视线、悬停、碰撞与镜头。
## 地图适配器提供几何、房间、锚点和道具显示；完成回执经 field_event 交回宿主。
## 接口与分层见设计第四部分 §2。

const ACTOR := preload("res://scenes/Story/StoryActor.gd")
const FieldNavigation := FieldNavigationInner
const FieldVocabulary := FieldVocabularyInner
const Bindings := PresentationBindingsInner
const SightMaskScript := SightMaskInner
const HintOverlayScript := HintOverlayInner
const ROOM_STAGE := RoomStageInner
const SPATIAL_INPUT := SpatialInputInner
const PIXEL_FRAMES := "res://ShareAsset/Internal/Characters/PixelCast/%s.tres"
const ANIME_FRAMES := "res://ShareAsset/Internal/Characters/AnimeCast/%s.tres"
const APPROACH := [Vector2.UP, Vector2.DOWN, Vector2.LEFT, Vector2.RIGHT, Vector2(-1, -1), Vector2(1, -1), Vector2(-1, 1), Vector2(1, 1)]
const NEAR_SITE_RADIUS := 85.0
const DAYPART_TINT := {"dawn": Color("b7b8b8"), "morning": Color("d8d4cc"), "noon": Color("e4e0d8"),
	"afternoon": Color("d4cec4"), "evening": Color("96939a"), "night": Color("525866")}

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
var spatial: Node
var room_stage: Node2D
var follow_camera := true
## The host owns retained bodies; hidden fields run the same geometry without UI.
var execution_bridge: Node
var execution_only := false
var execution_binding: Dictionary = {}
var execution_resource := ""
var execution_transitions: Array = []

var snapshot: Dictionary = {}
var sites: Dictionary = {}           # id → {title, point, reach, box?, marker?, person?, new}
var nearest := ""
var hovered := ""
var _hover_text := ""
var _hint_text := ""
var _was_active := false
var _active_since := 0
var _closing := false
var _applying_scene := false
var _player_key := ""


var _motion: Dictionary = {}         # instance id → generation
var _daily: Dictionary = {}          # who → job
var _cached_object_sprites: Array[Sprite2D] = []
var _cached_object_map := ""

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
	sight = SightMaskScript.new()
	sight.name = "Sight"
	sight.z_index = 40
	sight.host = adapter
	sight.set_process(false)
	add_child(sight)
	hints = HintOverlayScript.new()
	hints.z_index = 50
	add_child(hints)
	hints.set("source", self)
	if not adapter.setup(): return
	if execution_bridge != null and str(execution_binding.get("map", "")) != "":
		adapter.load_map(str(execution_binding.map), str(execution_binding.get("spawn", "")))
	_player_key = adapter.player_key()
	for key in adapter.fixed_actors():
		actors[key] = adapter.fixed_actors()[key]
	player = actors.get(_player_key)
	if player == null:
		if not adapter.fixed_actors().is_empty():
			Bindings.fail(self, "Missing player actor binding: " + _player_key)
			return
		player = _make_actor(_player_key, adapter.player_sprite())
	if player == null: return
	sight.set_process(not execution_only)
	if execution_only:
		camera.enabled = false
		sight.set_process(false)
		sight.hide()
		hints.hide()
		set_process(false)
	camera.position = adapter.camera_point(player.position)
	camera.position_smoothing_enabled = adapter.camera_smoothing()
	_after_map_load()
	if not execution_only:
		spatial = SPATIAL_INPUT.new()
		spatial.name = "SpatialInput"
		spatial.field = self
		add_child(spatial)
		room_stage = ROOM_STAGE.new()
		room_stage.field = self
		add_child(room_stage)

func begin_shutdown() -> void:
	if _closing: return
	_closing = true
	closing.emit()
	for actor in actors.values():
		if is_instance_valid(actor):
			_cancel_motion(actor)
	if adapter and adapter.has_method("begin_shutdown"): adapter.begin_shutdown()

func _exit_tree() -> void:
	begin_shutdown()

# ================================================================ snapshot ===

func apply_snapshot(value: Dictionary) -> void:
	snapshot = value
	execution_binding = value
	if is_instance_valid(tint):
		var daylight: Color = DAYPART_TINT.get(str(value.get("daypart", "")), Color.WHITE)
		tint.color = daylight * Color(0.62, 0.66, 0.76) if adapter.sight_terrain_fog() else daylight
	if adapter.has_method("apply_snapshot"): adapter.apply_snapshot(value)
	if not execution_only and adapter.fixed_actors().is_empty(): _spawn_people()
	if execution_bridge != null: execution_bridge.sync_obstacles(self)
	_build_sites()

func field_active() -> bool:
	return not execution_only and player != null and not _closing and bool(snapshot.get("field_enabled", false)) and not ui_busy.call() and not adapter.switching()

func context() -> Dictionary:
	if player == null:
		return {"field_active": false, "actors": {}, "map": adapter.map_key(), "site_rooms": _site_rooms()}
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
	var raw_defs = snapshot.get("sites", {})
	var defs := {}
	if raw_defs is Dictionary:
		defs = raw_defs
	elif raw_defs is Array:
		for item in raw_defs:
			if item is Dictionary and item.has("id"):
				defs[str(item.id)] = item
			elif item is Array and item.size() == 2 and item[0] is String:
				defs[item[0]] = item[1]
	var geometry: Dictionary = adapter.site_geometry(defs)
	for id in geometry:
		var def: Dictionary = defs.get(id, {})
		if def.is_empty() and not geometry[id].get("fallback", false): continue
		if not bool(def.get("enabled", true)): continue
		var site: Dictionary = geometry[id].duplicate()
		site.title = str(def.get("title", site.get("title", "")))
		if site.title == "" or str(site.title) == str(id):
			var names := FieldVocabulary.table("OBJECT_NAMES")
			for key in names:
				if str(key).to_lower() == str(id).to_lower(): site.title = str(names[key])
		if site.title == "" or str(site.title) == str(id): continue
		site.new = bool(def.get("new", false))
		sites[id] = site
	for person in snapshot.get("people", []):
		var key := str(person.key)
		if not actors.has(key) or key == _player_key: continue
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
	if _cached_object_map != adapter.map_key() or _cached_object_sprites.is_empty():
		_cached_object_sprites.clear()
		_player_object_sprites(objects, _cached_object_sprites)
		_cached_object_map = adapter.map_key()
	for sprite in _cached_object_sprites:
		if not is_instance_valid(sprite) or sprite.texture == null or str(sprite.name) == "BedQuiltForeground": continue
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
		var parts := FieldVocabulary.sprite_parts(sprite)
		if not parts.is_empty():
			for part in parts:
				if _player_object_exposed(sprite.get_global_transform_with_canvas() * (part.rect as Rect2), occluded_regions).is_empty(): continue
				counts[part.title] = int(counts.get(part.title, 0)) + 1
			if visible_site: represented_sites[site_id] = true
			continue
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
			var spatial = adapter.get("spatial")
			var props: Dictionary = spatial.get("objects", {}) if spatial is Dictionary else {}
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
	var sites_map = snapshot.get("sprite_sites", {})
	if sites_map is Dictionary:
		return str(sites_map.get(str(sprite.name), ""))
	return ""

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
	_pick_nearest()

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
				var site := _player_object_site(furniture)
				if sites.has(site) and site_visible(site): hovered = site
				hover_text = _player_object_title(furniture)
				if sites.has(site): hover_text = str(sites[site].title)
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

func _route_cost(from: Vector2, route: PackedVector2Array, to: Vector2) -> float:
	var cost := from.distance_to(route[0]) + route[route.size() - 1].distance_to(to)
	for i in range(1, route.size()): cost += route[i - 1].distance_to(route[i])
	return cost

# ================================================================ people ===

func _make_actor(key: String, sprite_key: String) -> CharacterBody2D:
	if execution_bridge != null:
		return execution_bridge.acquire_actor(key, sprite_key, self)
	return create_body(key, sprite_key)

## Called once for an actor by the retaining executor, never to replace a moved body.
func create_body(key: String, sprite_key: String) -> CharacterBody2D:
	var actor := CharacterBody2D.new()
	actor.set_script(ACTOR)
	var path := PIXEL_FRAMES % sprite_key
	if not ResourceLoader.exists(path): path = ANIME_FRAMES % sprite_key
	if not ResourceLoader.exists(path):
		actor.free()
		Bindings.fail(self, "Missing actor asset binding: " + key + " -> " + sprite_key)
		return null
	actor.standard = load(path)
	actor.name = key
	adapter.objects_root().add_child(actor)
	actor.collision_mask = 3
	actor.speed = 130.0 if key == _player_key else 110.0
	actors[key] = actor
	return actor

## 多地图章节：此处的人站到各自的位置。animate 时新来的从门口走进来，离开的走向门口。
func _spawn_people() -> void:
	var shown := {}
	for p in snapshot.get("people", []):
		if str(p.get("map", "")) == adapter.map_key() and bool(p.get("present", true)):
			shown[str(p.key)] = p
	var stands: Array = adapter.stands()
	for key in actors.keys():
		if key == _player_key or shown.has(key): continue
		var leaving: CharacterBody2D = actors[key]
		if execution_bridge != null:
			execution_bridge.park_actor(key, self)
		else:
			actors.erase(key)
			if is_instance_valid(leaving): leaving.queue_free()
	var index := 0
	for key in shown:
		var p: Dictionary = shown[key]
		var actor: CharacterBody2D = actors.get(key)
		if actor == null:
			actor = _make_actor(key, str(p.get("sprite", "")))
			if actor == null: continue
			if execution_bridge == null:
				actor.position = stands[(absi(key.hash()) + index) % stands.size()] if not stands.is_empty() else player.position + Vector2(40 * index, 30)
		if not actor.travelling: _frame_pose(actor, str(p.get("frame", "")))
		index += 1

func _frame_pose(actor: CharacterBody2D, frame: String) -> void:
	actor.pose({"操作": "interact", "递物": "interact", "拉拽": "interact", "交谈": "talk", "蹲查": "inspect"}.get(frame, "idle"), "front")

func load_map(key: String, spawn: String, at := Vector2.ZERO, use_at := false) -> void:
	if adapter != null and adapter.map_key() == key:
		# setup already adopted the retained body at its actual endpoint; prevent cyclical floor reloads
		return
	if player == null: return
	if adapter.has_method("valid_transition") and not adapter.valid_transition(key, spawn, at, use_at):
		adapter.blocked_entry()
		return
	if not _applying_scene and adapter.has_method("can_enter") and not adapter.can_enter(key):
		adapter.blocked_entry()
		field_event.emit({"kind": "blocked_move", "map": key})
		return
	var source_map := str(adapter.map_key())
	var source_point := player.position
	for actor_key in actors.keys():
		if actor_key == _player_key: continue
		if execution_bridge != null:
			execution_bridge.park_actor(actor_key, self)
			continue
		var actor: CharacterBody2D = actors[actor_key]
		if is_instance_valid(actor):
			_cancel_motion(actor)
			actor.queue_free()
		actors.erase(actor_key)
	for who in _daily.keys(): _daily_release(who)
	_cancel_motion(player)
	player.get_parent().remove_child(player)
	add_child(player)
	adapter.load_map(key, spawn)
	_cached_object_sprites.clear()
	remove_child(player)
	adapter.objects_root().add_child(player)
	player.position = at if use_at else adapter.spawn_point(spawn)
	execution_transitions.append({"from_map": source_map, "from_position": [source_point.x, source_point.y],
		"to_map": key, "to_position": [player.position.x, player.position.y], "spawn": spawn, "continuous": use_at})
	if execution_bridge != null: execution_bridge.note_map(self)
	_after_map_load()
	# 先按快照站好此处的人，再告诉章节到了哪里（回来的快照只让新来的人走进来）。
	if not execution_only and adapter.fixed_actors().is_empty(): _spawn_people()
	if execution_bridge != null: execution_bridge.sync_obstacles(self)
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

# ============================================================ stage events ===

## Only the prepared DV movement intent enters physical execution.
func execute_move(destination: Dictionary, who: String, serial: int, action: String) -> void:
	execution_transitions.clear()
	if adapter.has_method("execute_destination"):
		adapter.execute_destination(destination, who, serial, action)
		return
	var target := str(destination.get("departure_station", destination.get("room", "")))
	var room := str(destination.get("execution_room", destination.get("room", target)))
	move_to_station(target, room, who, serial, action)

func move_to_station(target: String, room: String, who: String, serial: int, action: String) -> void:
	_daily_go({"who": who, "target": target, "room": room, "serial": serial, "action": action})

func _actor(who: String) -> CharacterBody2D:
	return actors.get(who)

func _next_motion(actor: CharacterBody2D) -> int:
	var id := actor.get_instance_id()
	_motion[id] = int(_motion.get(id, 0)) + 1
	return _motion[id]

func _cancel_motion(actor: CharacterBody2D) -> void:
	_next_motion(actor)
	actor.stop_travel()

## 沿路走；受阻由角色停步并报告失败。
func _follow(actor: CharacterBody2D, route: PackedVector2Array) -> Dictionary:
	_cancel_motion(actor)
	var generation := _next_motion(actor)
	var id := actor.get_instance_id()
	actor.follow_path(route)
	while actor.travelling:
		await get_tree().physics_frame
		if _closing or not is_instance_valid(actor) or int(_motion.get(id, 0)) != generation:
			return {"ok": false, "reason": "cancelled"}
	return actor.travel_result

func _follow_route(actor: CharacterBody2D, destination: Vector2) -> Dictionary:
	var route := FieldNavigation.route_on_grid(actor.position, destination, adapter.nav_grid(actor))
	if route.is_empty():
		_cancel_motion(actor)
		return {"ok": false, "reason": "no_route"}
	route.append(destination)
	return await _follow(actor, route)

func occupied_positions(walker: CharacterBody2D) -> Array[Vector2]:
	if execution_bridge != null: return execution_bridge.occupied_positions(walker, execution_resource, adapter.map_key())
	var result: Array[Vector2] = []
	for other in actors.values():
		if not is_instance_valid(other) or other == walker or other.get_parent() != walker.get_parent(): continue
		result.append(other.position)
	return result

func position_occupied(walker: CharacterBody2D, point: Vector2) -> bool:
	for position: Vector2 in occupied_positions(walker):
		var separation: Vector2 = (position - point).abs()
		if separation.x < 19.0 and separation.y < 15.0: return true
	return false

# ---------------------------------------------------------------- camera ---

## Keep continuous approach samples and include every grid centre inside the same radius.
## Sparse directional sampling must not hide a narrow but physically legal approach.
func _station_approaches(station: Dictionary, grid: AStarGrid2D) -> Array[Vector2]:
	var points: Array[Vector2] = []
	var radius := float(station.radius)
	if radius <= 0.0:
		points.append(station.point)
		return points
	var seen := {}
	for fraction in [1.0, 0.75]:
		for heading in APPROACH:
			var point: Vector2 = station.point + heading.normalized() * radius * fraction
			if not seen.has(point):
				seen[point] = true
				points.append(point)
	var start := Vector2i(((station.point - Vector2.ONE * radius - grid.offset) / grid.cell_size).ceil())
	var stop := Vector2i(((station.point + Vector2.ONE * radius - grid.offset) / grid.cell_size).floor())
	for y in range(maxi(start.y, grid.region.position.y), mini(stop.y, grid.region.end.y - 1) + 1):
		for x in range(maxi(start.x, grid.region.position.x), mini(stop.x, grid.region.end.x - 1) + 1):
			var cell := Vector2i(x, y)
			if grid.is_point_solid(cell): continue
			var point := grid.get_point_position(cell)
			if point.distance_to(station.point) <= radius and not seen.has(point):
				seen[point] = true
				points.append(point)
	return points

func _daily_go(ev: Dictionary) -> void:
	var who := str(ev.who)
	var actor: CharacterBody2D = actors.get(who)
	if actor == null:
		_daily_report(who, int(ev.serial), "blocked", "", str(ev.get("action", "rest")), {"station": str(ev.target)})
		return
	var station: Dictionary = adapter.station(str(ev.target), who)
	if station.is_empty():
		_daily_report(who, int(ev.serial), "blocked", "", str(ev.get("action", "rest")), {"station": str(ev.target)})
		return
	var grid: AStarGrid2D = adapter.nav_grid(actor)
	var destination := Vector2.ZERO
	var best_route := PackedVector2Array()
	var best_cost := INF
	for group in station.get("approach_groups", [_station_approaches(station, grid)]):
		for point: Vector2 in group:
			if adapter.entered_room(point) != str(ev.room) or not adapter.walkable(point, actor): continue
			if position_occupied(actor, point): continue
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
			# Generic room candidates are ordered by proximity within each group.
			if bool(station.get("room_target", false)): break
		if not best_route.is_empty(): break
	if best_route.is_empty():
		_daily_report(who, int(ev.serial), "blocked", "", str(ev.get("action", "rest")), {"station": str(ev.target)})
		return
	_cancel_motion(actor)
	if actor.seated: actor.stand_up()
	if bool(station.get("room_target", false)): station.point = destination
	_daily[who] = {"serial": int(ev.serial), "room": str(ev.room), "action": str(ev.get("action", "rest")), "destination": destination,
		"station": str(ev.target), "station_point": station.point, "station_radius": float(station.radius),
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
			_daily_report(who, int(job.serial), "blocked", adapter.entered_room(actor.position), str(job.action))

func _daily_report(who: String, serial: int, status: String, room: String, action := "rest", details: Dictionary = {}) -> void:
	var proof := details.duplicate()
	if _daily.has(who) and int(_daily[who].serial) == serial and str(_daily[who].action) == action:
		var job: Dictionary = _daily[who]
		proof.merge({"station": job.station, "approach_point": [job.destination.x, job.destination.y],
			"station_point": [job.station_point.x, job.station_point.y], "station_radius": job.station_radius, "arrival_tolerance": 4.0}, true)
		var actor := _actor(who)
		if actor != null:
			proof.merge({"actual_position": [actor.position.x, actor.position.y], "arrival_error": actor.position.distance_to(job.destination),
				"station_distance": actor.position.distance_to(job.station_point)}, true)
	var body := _actor(who)
	if body != null:
		proof["actual_position"] = [body.position.x, body.position.y]
	proof["map"] = adapter.map_key()
	proof["transitions"] = execution_transitions.duplicate(true)
	_daily_release(who)
	var event := {"kind": "daily", "who": who, "serial": serial, "status": status, "room": room, "action": action}
	event.merge(proof, true)
	field_event.emit(event)

# ==============================================================================
# 内聚现场支持类 (Field Subsystems Inner Classes)
# ==============================================================================

class FieldVocabularyInner:
	static var _data := {
		"FIELD_WORDS": {
			"门关": "（关闭）", "门开": "（开启）", "宿舍门开": "宿舍门（开启）", "宿舍门关": "宿舍门（关闭）",
			"未命名": "未命名物品", "床头油灯": "床头柜上的油灯", "床头柜": "床头柜", "地毯": "地毯",
			"地毯旁白前缀": "地毯。", "壁灯": "壁灯"
		},
		"PRESENTATION": {
			"composites": {
				"res://ShareAsset/Internal/Props/FirstAwakeningInterior/nightstand.png": {
					"lamp": {"x": 6, "y": 4, "width": 8, "height": 13, "word": "床头油灯"},
					"cabinet": {"x": 1, "y": 16, "width": 18, "height": 20, "word": "床头柜"}
				}
			}
		},
		"OBJECT_NAMES": {
			"SheetedBed": "罩着防尘布的床", "EmptyBookcase": "书架", "EmptyWardrobe": "衣柜", "Nightstand": "床头柜",
			"Bookcase": "书架", "DeskChair": "椅子", "ReadingChair": "扶手椅", "ReadingTable": "小几",
			"StudyTable": "长桌", "RewardLocker": "领取柜", "CylinderPlayer": "手摇试听机", "StudyFern": "花架",
			"StudyConsole": "边桌", "PaperBasket": "纸篓", "WallClock": "挂钟", "LinenChest": "箱子",
			"Wardrobe": "衣柜", "Dresser": "梳妆台", "Washstand": "水槽", "Sideboard": "碗柜",
			"Worktable": "案板", "CupTable": "小圆桌", "CoatStand": "衣架", "StoreSack": "袋子",
			"PantrySack": "袋子", "CorridorConsole": "边桌", "CorridorPlant": "花架", "ShoeBench": "长凳",
			"ToolBench": "工作台", "DemonPrints": "地上的脚印", "Transmitter": "铜口", "Mouthpiece": "铜口",
			"ZoneRules": "公告框", "SealRack": "架子", "WorksShelf": "书架", "Shelf": "书架",
			"VaultDoor": "门", "Warning": "告示", "CopyDesk": "书桌", "PlanCabinet": "柜子",
			"Pedestal": "台座", "RelayColumn": "柱子", "Piano": "钢琴", "Globe": "地球仪",
			"Armchair": "扶手椅", "Range": "灶", "Basin": "洗手盆", "Bed": "床",
			"Chair": "椅子", "Desk": "书桌", "Coat": "衣架", "Crate": "箱子",
			"Barrel": "水桶", "Sack": "袋子", "Lamp": "灯", "Fern": "盆栽",
			"Bench": "长凳", "Notice": "公告框", "Pillar": "柱子", "Hydrangea": "绣球花",
			"Blackboard": "黑板", "ChessChair": "椅子", "Chess": "棋盘桌", "Fireplace": "壁炉",
			"FireSide": "边桌", "LaundryTub": "洗衣盆", "DishRack": "碗碟架", "Drying": "晾衣绳",
			"Heater": "热水炉", "Lectern": "讲台", "Ledger": "账簿台", "MedCabinet": "药柜",
			"MusicStand": "谱架", "Pots": "花盆", "Pump": "手动水泵", "RationCounter": "配给台",
			"ReagentCabinet": "试剂柜", "Spring": "汲水泉", "Tools": "园艺工具", "Tub": "浴缸",
			"Valve": "阀门", "Buckets": "水桶", "Bollard": "系缆桩", "Engine": "机械装置",
			"Hood": "排烟罩", "LaneGate": "门扇", "LongTable": "长桌", "PianoStool": "琴凳",
			"Plant": "花架", "Rockfall": "岩石", "Screen": "屏风", "Timetable": "课表板",
			"Trough": "水槽", "Table": "桌子", "Bin": "储物箱", "Frame": "框架",
			"Stand": "架子", "round": "树"
		},
		"TEXTURE_NAMES": {
			"planter_bed": "种植床", "ward_bed": "病床", "lecture_desk_row": "课桌", "music_stand": "谱架",
			"tuning_frame": "框架", "lamp_post": "路灯", "piano_stool": "琴凳", "granary_bin": "储物箱",
			"basin_row": "洗手盆", "bucket_pair": "水桶", "chess_table": "棋盘桌", "doctor_desk": "诊疗桌",
			"folding_screen": "屏风", "gate_leaf": "门扇", "plant_stand": "花架", "side_table_plain": "边桌",
			"stone_bench": "石凳", "long_table": "长桌", "timetable_board": "课表板"
		},
		"TEXTURE_PREFIX_NAMES": {
			"outdoor_hydrangea_": "绣球花", "outdoor_round_": "树", "outdoor_boulder_": "岩石"
		}
	}

	static func table(key: String) -> Dictionary:
		var value = _data.get(key, {})
		return value if value is Dictionary else {}

	static func word(key: String) -> String:
		return str(table("FIELD_WORDS").get(key, ""))

	static func sprite_parts(sprite: Sprite2D) -> Array[Dictionary]:
		var result: Array[Dictionary] = []
		var presentation := table("PRESENTATION")
		if not presentation.get("composites") is Dictionary:
			PresentationBindingsInner.fail(sprite, "Missing composite presentation declarations")
			return result
		var definitions: Dictionary = presentation.composites
		if not definitions.has(sprite.texture.resource_path): return result
		var parts: Dictionary = definitions[sprite.texture.resource_path]
		for key in parts:
			var spec: Dictionary = parts[key]
			var title := word(str(spec.word))
			if title == "":
				PresentationBindingsInner.fail(sprite, "Missing composite vocabulary binding: " + str(spec.word))
				return []
			var rect := Rect2(float(spec.x), float(spec.y), float(spec.width), float(spec.height))
			if not Rect2(Vector2.ZERO, sprite.texture.get_size()).encloses(rect):
				PresentationBindingsInner.fail(sprite, "Composite region exceeds texture: " + str(key))
				return []
			result.append({"rect": Rect2(sprite.get_rect().position + rect.position, rect.size), "title": title})
		return result

class PresentationBindingsInner:
	static func actor_error(root: Node, bindings: Dictionary, player_key: String) -> String:
		if bindings.is_empty() or not bindings.has(player_key): return "Missing player actor binding: " + player_key
		var nodes := {}
		for key in bindings:
			var path := str(bindings[key])
			if path == "" or nodes.has(path): return "Invalid or duplicate actor node binding: " + str(key)
			nodes[path] = true
			if root.get_node_or_null(path) is not CharacterBody2D: return "Missing actor node binding: " + str(key) + " -> " + path
		return ""

	static func fail(node: Node, message: String) -> void:
		push_error(message)
		if node.is_inside_tree(): node.get_tree().quit(1)

class SightMaskInner extends Node2D:
	var cell_size: int = 32
	var sight_radius: int = 15
	var unseen_shade: Color = Color(0.08, 0.08, 0.08, 0.85)
	var remembered_shade: Color = Color(0.18, 0.18, 0.18, 0.5)
	var host: Node
	var visible_cells: Dictionary = {}
	var seen_cells: Dictionary = {}
	var active_region := ""
	var _origin := Vector2i(-999, -999)
	var _entry := Vector2i(-999, -999)
	var _clock := 0.0

	func reset() -> void:
		visible_cells.clear()
		seen_cells = {}
		active_region = ""
		_origin = Vector2i(-999, -999)
		_entry = Vector2i(-999, -999)
		queue_redraw()

	func can_see(point: Vector2) -> bool:
		var cell := Vector2i((point / cell_size).floor())
		if _terrain_fog():
			return active_region != "" and seen_cells.has(cell) and host.sight_region(cell) == active_region
		return visible_cells.has(cell)

	func terrain_known(point: Vector2) -> bool:
		return seen_cells.has(Vector2i((point / cell_size).floor()))

	func _process(delta: float) -> void:
		if Engine.is_editor_hint() or host == null: return
		_clock += delta
		var entry := Vector2i((host.sight_entry_origin() / cell_size).floor())
		var origin := entry if _terrain_fog() else Vector2i((host.sight_origin() / cell_size).floor())
		if origin == _origin and entry == _entry and _clock < 0.5: return
		_clock = 0.0
		_origin = origin
		_entry = entry
		_cast(origin)
		queue_redraw()

	func _cast(origin: Vector2i) -> void:
		visible_cells.clear()
		var size: Vector2i = host.sight_size()
		if _terrain_fog():
			var entry := Vector2i((host.sight_entry_origin() / cell_size).floor())
			_reveal_room(entry, size)
			if active_region != "":
				for y in range(size.y):
					for x in range(size.x):
						var cell := Vector2i(x, y)
						if host.sight_region(cell) == active_region:
							visible_cells[cell] = true
			return
		for y in range(maxi(0, origin.y - sight_radius), mini(size.y, origin.y + sight_radius + 1)):
			for x in range(maxi(0, origin.x - sight_radius), mini(size.x, origin.x + sight_radius + 1)):
				var target := Vector2i(x, y)
				if (target - origin).length_squared() > sight_radius * sight_radius: continue
				if _clear(origin, target): visible_cells[target] = true

	func _reveal_room(origin: Vector2i, size: Vector2i) -> void:
		if not _terrain_fog(): return
		var region: String = host.sight_region(origin)
		if region == "":
			active_region = ""
			return
		active_region = region
		for y in range(size.y):
			for x in range(size.x):
				var cell := Vector2i(x, y)
				if host.sight_region(cell) == region:
					seen_cells[cell] = true

	func _terrain_fog() -> bool:
		return host != null and host.has_method("sight_terrain_fog") and host.sight_terrain_fog()

	func _clear(a: Vector2i, b: Vector2i) -> bool:
		var delta := b - a
		var steps := maxi(absi(delta.x), absi(delta.y))
		if steps <= 1: return true
		for i in range(1, steps):
			var check := Vector2i((Vector2(a) + Vector2(delta) * (float(i) / float(steps))).round())
			if host.sight_blocks(check): return false
		return true

	func _draw() -> void:
		if host == null: return
		var size: Vector2i = host.sight_size()
		var rect_size := Vector2(cell_size, cell_size)
		if _terrain_fog():
			for y in range(size.y):
				for x in range(size.x):
					var cell := Vector2i(x, y)
					if visible_cells.has(cell) or not host.sight_interior(cell): continue
					var shade := remembered_shade if seen_cells.has(cell) else unseen_shade
					draw_rect(Rect2(Vector2(cell * cell_size), rect_size), shade, true)
			return
		for y in range(size.y):
			for x in range(size.x):
				var cell := Vector2i(x, y)
				if visible_cells.has(cell):
					seen_cells[cell] = true
					continue
				var shade := remembered_shade if seen_cells.has(cell) else unseen_shade
				draw_rect(Rect2(Vector2(cell * cell_size), rect_size), shade, true)

class HintOverlayInner extends Node2D:
	var hover_color: Color = Color(0.9, 0.8, 0.4, 0.9)
	var near_color: Color = Color(0.8, 0.75, 0.6, 0.6)
	var shade_color: Color = Color(0.1, 0.1, 0.1, 0.4)
	var spark_edge_color: Color = Color(0.95, 0.9, 0.5, 0.8)
	var spark_light_color: Color = Color(1.0, 1.0, 0.8, 0.9)
	var spark_center_color: Color = Color(1.0, 1.0, 1.0, 1.0)
	var source: Node2D
	var clock := 0.0

	func _ready() -> void:
		if material == null:
			var unlit := CanvasItemMaterial.new()
			unlit.light_mode = CanvasItemMaterial.LIGHT_MODE_UNSHADED
			material = unlit

	func _process(delta: float) -> void:
		clock += delta
		queue_redraw()

	func _draw() -> void:
		if source == null: return
		var sites: Dictionary = source.get("sites") if source.get("sites") != null else {}
		var hovered: String = str(source.get("hovered")) if source.get("hovered") != null else ""
		var nearest: String = str(source.get("nearest")) if source.get("nearest") != null else ""
		for id: String in sites.keys():
			var data: Dictionary = sites[id]
			var box: Rect2 = data.get("box", Rect2())
			if box.size == Vector2.ZERO: continue
			if id == hovered:
				draw_rect(box, hover_color, false, 2.0)
			elif id == nearest and hovered == "":
				draw_rect(box, near_color, false, 1.0)
			elif bool(data.get("new", false)):
				var phase := fposmod(clock * 2.0, 1.0)
				var spark_radius := int(round(1.0 + phase * 3.0))
				_draw_spark(box.position + box.size * 0.5, spark_radius)

	func _draw_spark(center: Vector2, radius: int) -> void:
		draw_circle(center, float(radius + 1), spark_edge_color)
		draw_circle(center, float(radius), spark_light_color)
		draw_circle(center, float(maxi(1, radius - 1)), spark_center_color)

class RoomStageInner extends Node2D:
	const VEIL := preload("res://scenes/Shared/Field/room_veil.gdshader")
	var field: Node2D
	var _map: Node2D
	var _room := ""
	var _lights: Array[Node2D] = []
	var _veil: ColorRect
	var _layer: CanvasLayer
	var _bounds := Rect2()
	var _reduce_motion := false

	func _ready() -> void:
		var settings := get_node_or_null("/root/GameSettings")
		if settings != null:
			_reduce_motion = bool(settings.get_value("accessibility", "reduce_motion"))
			settings.setting_changed.connect(func(section: String, key: String, value):
				if section == "accessibility" and key == "reduce_motion":
					_reduce_motion = bool(value)
					_room = ""
			)
		_layer = CanvasLayer.new()
		_layer.layer = 1
		add_child(_layer)
		_veil = ColorRect.new()
		_veil.mouse_filter = Control.MOUSE_FILTER_IGNORE
		_veil.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		var mat := ShaderMaterial.new()
		mat.shader = VEIL
		_veil.material = mat
		_layer.add_child(_veil)

	func _process(delta: float) -> void:
		if not is_instance_valid(field.player) or field.adapter.map == null: return
		var adapter: Node2D = field.adapter
		var room: String = adapter.room_of(field.player.position)
		var indoor: bool = adapter.sight_terrain_fog()
		_veil.visible = indoor
		if _map != adapter.map or room != _room:
			_map = adapter.map
			_room = room
			_bounds = adapter.stage_bounds(field.player.position)
			_rebuild_lights(indoor)
		if not indoor: return
		var viewport: Vector2 = get_viewport_rect().size
		var zoom := clampf(minf(viewport.x * 0.9 / _bounds.size.x, viewport.y * 0.88 / _bounds.size.y), 2.0, 3.4)
		field.camera.zoom = Vector2.ONE * zoom if _reduce_motion else field.camera.zoom.lerp(Vector2.ONE * zoom, 1.0 - exp(-delta * 8.0))
		var transform: Transform2D = field.get_global_transform_with_canvas()
		var screen_bounds: Rect2 = transform * _bounds
		_veil.material.set_shader_parameter("room_rect", Vector4(screen_bounds.position.x / viewport.x, screen_bounds.position.y / viewport.y, screen_bounds.end.x / viewport.x, screen_bounds.end.y / viewport.y))
		_veil.material.set_shader_parameter("pixel_size", Vector2.ONE / viewport)

	func _rebuild_lights(indoor: bool) -> void:
		for node in _lights:
			remove_child(node)
			node.queue_free()
		_lights.clear()
		if not indoor:
			field.camera.zoom = Vector2(2, 2)
			return
		var gradient := Gradient.new()
		gradient.offsets = PackedFloat32Array([0, 0.16, 0.48, 1])
		gradient.colors = PackedColorArray([Color(1,1,1,1), Color(1,1,1,0.82), Color(1,1,1,0.26), Color(1,1,1,0)])
		var glow := GradientTexture2D.new()
		glow.gradient = gradient
		glow.width = 256
		glow.height = 256
		glow.fill = GradientTexture2D.FILL_RADIAL
		glow.fill_from = Vector2(0.5, 0.5)
		glow.fill_to = Vector2(1, 0.5)
		for declaration in _map.get_meta("stage_lights", []):
			var point := Vector2(float(declaration.point[0]), float(declaration.point[1]))
			if not _bounds.grow(12).has_point(point): continue
			var window: bool = str(declaration.kind) == "window"
			var light := PointLight2D.new()
			light.position = point
			light.texture = glow
			light.texture_scale = 1.1 if window else 1.45
			light.color = Color(0.63, 0.79, 0.94) if window else Color(1.0, 0.81, 0.71)
			light.energy = 0.6 if window else 0.9
			light.height = 48.0
			light.shadow_enabled = false
			light.range_z_min = -100
			light.range_z_max = 100
			add_child(light)
			_lights.append(light)
			if window: _window_beam(point)

	func _window_beam(point: Vector2) -> void:
		var beam := Polygon2D.new()
		beam.position = point
		beam.polygon = PackedVector2Array([Vector2(-12,0), Vector2(12,0), Vector2(92,160), Vector2(30,160)])
		beam.vertex_colors = PackedColorArray([Color(0.6,0.76,0.9,0.12),Color(0.6,0.76,0.9,0.12),Color(0.6,0.76,0.9,0),Color(0.6,0.76,0.9,0)])
		beam.z_index = 25
		add_child(beam)
		_lights.append(beam)
		if _reduce_motion: return
		var dust := GPUParticles2D.new()
		dust.position = point + Vector2(30, 70)
		dust.amount = 18
		dust.lifetime = 8.0
		dust.preprocess = 4.0
		dust.visibility_rect = Rect2(-64,-80,176,200)
		dust.z_index = 26
		var motion := ParticleProcessMaterial.new()
		motion.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_BOX
		motion.emission_box_extents = Vector3(28,60,0)
		motion.direction = Vector3(0.2,-1,0)
		motion.spread = 35
		motion.initial_velocity_min = 1.0
		motion.initial_velocity_max = 3.0
		motion.gravity = Vector3.ZERO
		motion.color = Color(0.8,0.86,0.91,0.18)
		motion.scale_min = 0.35
		motion.scale_max = 0.8
		dust.process_material = motion
		add_child(dust)
		_lights.append(dust)

class SpatialInputInner extends Node:
	const NAV := FieldNavigationInner
	var field: Node2D
	var options: Array = []
	var submitted := false
	var _route := PackedVector2Array()
	var _site := ""
	var _sequence := 0
	var _walking := false
	var _walk_id := ""
	var _progress := Vector2.ZERO
	var _stuck := 0.0
	var _pending_door: Dictionary = {}

	func set_options(value: Array) -> void:
		options = value
		submitted = false

	func enabled() -> bool:
		return is_instance_valid(field) and field.field_active() and bool(field.snapshot.get("player_control", false)) and not submitted and not field._daily.has(field.player.name)

	func _stamp(stage: String, id: String, data: Dictionary = {}) -> void:
		field.field_event.emit({"kind": "timing", "stage": stage, "interaction_id": id, "details": data})

	func _id() -> String:
		_sequence += 1
		return "spatial-%d-%d" % [Time.get_ticks_usec(), _sequence]

	func _stop_walk(reason: String) -> void:
		_route.clear()
		_site = ""
		_pending_door = {}
		if _walking:
			_stamp("local_motion_end", _walk_id, {"reason": reason, "game_time": field.snapshot.get("game_time", 0), "position": [field.player.position.x, field.player.position.y]})
		_walking = false
		if is_instance_valid(field.player) and not field.player.travelling: field.player.pose("idle")

	func _begin_walk() -> void:
		if _walking: return
		if field.player.seated: field.player.stand_up()
		_walking = true
		_progress = field.player.position
		_stuck = 0.0
		_walk_id = _id()
		_stamp("local_motion_begin", _walk_id, {"game_time": field.snapshot.get("game_time", 0), "position": [field.player.position.x, field.player.position.y]})

	func _physics_process(delta: float) -> void:
		if not enabled():
			if _walking: _stop_walk("input-blocked")
			return
		var direction := Input.get_vector("move_left", "move_right", "move_up", "move_down")
		if direction != Vector2.ZERO:
			_route.clear()
			_site = ""
			_pending_door = {}
			_begin_walk()
			_step(direction * field.player.speed * delta, delta)
		elif not _route.is_empty():
			_begin_walk()
			var difference: Vector2 = _route[0] - field.player.position
			if difference.length() <= 1.0:
				_route.remove_at(0)
				if _route.is_empty():
					var site := _site
					var door := _pending_door
					_stop_walk("arrived")
					if site != "": _interact(site)
					elif not door.is_empty(): _submit(door, "", "doorway-click")
			else:
				_step(difference.limit_length(field.player.speed * delta), delta)
				if field.player.position.distance_to(_progress) >= 0.5:
					_progress = field.player.position
					_stuck = 0.0
				else:
					_stuck += delta
					if _stuck >= 0.5: _stop_walk("blocked")
		elif _walking:
			_stop_walk("released")

	func _step(motion: Vector2, delta: float) -> void:
		var next: Vector2 = field.player.position + motion
		var room := str(field.snapshot.get("execution_room", field.snapshot.get("room", "")))
		var next_room: String = field.adapter.room_of(next)
		var exit_map: String = field.adapter.manual_exit_target(next) if field.adapter.has_method("manual_exit_target") else ""
		if next_room != room or exit_map != "":
			if not field.adapter.walkable(next, field.player): return
			_stop_walk("doorway")
			for option in options:
				var binding: Dictionary = option.get("interaction", {})
				var destination: Dictionary = binding.get("destination", {})
				if str(binding.get("kind", "")) == "doorway" and ((exit_map != "" and str(destination.get("map", "")) == exit_map) or (exit_map == "" and str(destination.get("map", "")) == field.adapter.map_key() and str(destination.get("room", "")) == next_room)):
					_submit(option, "", "doorway")
					return
			return
		field.player.velocity = motion / maxf(delta, 0.000001)
		field.player.face_vector(motion)
		field.player.pose("walk")
		field.player.move_and_slide()
		field.player.velocity = Vector2.ZERO

	func _unhandled_input(event: InputEvent) -> void:
		if not enabled() or event.is_echo(): return
		if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
			var site := str(field.hovered)
			if site != "": _approach(site)
			else: _navigate(field.get_global_mouse_position())
			get_viewport().set_input_as_handled()
		elif event.is_action_pressed("interact") or event.is_action_pressed("dialogue_continue"):
			var site := str(field.nearest)
			if site != "": _interact(site)
			else:
				var door := _near_door()
				if not door.is_empty(): _submit(door, "", "doorway-interact")
			get_viewport().set_input_as_handled()
		elif event.is_action_pressed("manuscript") or event.is_action_pressed("wait"):
			_show_self_choices()
			get_viewport().set_input_as_handled()

	func _navigate(point: Vector2) -> void:
		_stop_walk("new-target")
		var door := _door_option(point)
		var destination_room: String = field.adapter.room_of(point)
		var own_room := str(field.snapshot.get("execution_room", ""))
		if not field.in_sight(point) and door.is_empty():
			var doorway_visible := false
			for direction in field.APPROACH:
				if field.in_sight(point + direction.normalized() * 32.0): doorway_visible = true
			var admitted := false
			for option in options:
				var binding: Dictionary = option.get("interaction", {})
				if str(binding.get("kind", "")) == "doorway" and str(binding.get("destination", {}).get("room", "")) == destination_room: admitted = true
			if not doorway_visible or (not admitted and door.is_empty()): return
		if not field.adapter.walkable(point, field.player): return
		_route = NAV.route_on_grid(field.player.position, point, _local_grid(destination_room if destination_room != own_room else ""))
		if not _route.is_empty():
			_route.append(point)
			_pending_door = door

	func _door_option(point: Vector2) -> Dictionary:
		if not field.adapter.has_method("doorway_rooms"): return {}
		var rooms: Array[String] = field.adapter.doorway_rooms(point)
		var own_room := str(field.snapshot.get("execution_room", ""))
		if own_room not in rooms: return {}
		var found: Dictionary = {}
		for option in options:
			var binding: Dictionary = option.get("interaction", {})
			var target: Dictionary = binding.get("destination", {})
			var room := str(target.get("room", ""))
			if str(binding.get("kind", "")) == "doorway" and str(target.get("map", "")) == field.adapter.map_key() and room != own_room and room in rooms:
				if not found.is_empty(): return {}
				found = option
		return found

	func _near_door() -> Dictionary:
		var nearest: Dictionary = {}
		var best := INF
		for offset in [Vector2.ZERO, Vector2.UP * 24, Vector2.DOWN * 24, Vector2.LEFT * 24, Vector2.RIGHT * 24]:
			var point: Vector2 = field.player.position + offset
			if not field.adapter.walkable(point, field.player): continue
			var choice := _door_option(point)
			if not choice.is_empty() and offset.length_squared() < best:
				nearest = choice
				best = offset.length_squared()
		return nearest

	func _local_grid(destination_room := "") -> AStarGrid2D:
		var grid: AStarGrid2D = field.adapter.nav_grid(field.player)
		var room := str(field.snapshot.get("execution_room", ""))
		for y in range(grid.region.position.y, grid.region.end.y):
			for x in range(grid.region.position.x, grid.region.end.x):
				var cell := Vector2i(x, y)
				var candidate_room: String = field.adapter.room_of(grid.get_point_position(cell))
				if candidate_room != room and candidate_room != destination_room: grid.set_point_solid(cell)
		return grid

	func _approach(site: String) -> void:
		if not field.site_visible(site): return
		if _in_reach(site):
			_interact(site)
			return
		var center: Vector2 = field.site_point(site)
		var station := {"point": center, "radius": float(field.sites[site].reach) - 4.0}
		var grid: AStarGrid2D = _local_grid()
		var best := INF
		var chosen := PackedVector2Array()
		for point in field._station_approaches(station, grid):
			if field.adapter.entered_room(point) != str(field.snapshot.get("execution_room", "")) or not field.adapter.walkable(point, field.player): continue
			if field.position_occupied(field.player, point): continue
			var route := NAV.route_on_grid(field.player.position, point, grid)
			if route.is_empty(): continue
			var distance: float = field._route_cost(field.player.position, route, point)
			if distance < best:
				best = distance
				chosen = route
				chosen.append(point)
		_stop_walk("new-target")
		_route = chosen
		_site = site

	func _in_reach(site: String) -> bool:
		return field.sites.has(site) and field.site_visible(site) and field.player.position.distance_to(field.site_point(site)) <= float(field.sites[site].reach)

	func choices_for(site: String) -> Array:
		var result: Array = []
		for option in options:
			var binding: Dictionary = option.get("interaction", {})
			if (str(binding.get("kind", "")) == "site" and str(binding.get("site", "")) == site) or (str(binding.get("kind", "")) == "person" and site == "person:" + str(binding.get("person", ""))): result.append(option)
		return result

	func _interact(site: String) -> void:
		if not _in_reach(site): return
		_stop_walk("interaction")
		var choices := choices_for(site)
		if choices.size() == 1: _submit(choices[0], site, "hotspot")
		elif choices.size() > 1:
			var id := _id()
			_stamp("input_request", id, {"site": site, "request": "local-choices"})
			field.field_event.emit({"kind": "spatial_choices", "interaction_id": id, "site": site, "title": field.sites[site].title, "options": choices, "screen_position": field.get_global_transform_with_canvas() * field.site_point(site)})

	func _show_self_choices() -> void:
		var choices: Array = []
		for option in options:
			if str(option.get("interaction", {}).get("kind", "")) == "self": choices.append(option)
		if choices.is_empty(): return
		var id := _id()
		_stamp("input_request", id, {"site": "self", "request": "local-choices"})
		field.field_event.emit({"kind": "spatial_choices", "interaction_id": id, "site": "self", "title": "随身手稿", "options": choices, "screen_position": field.get_global_transform_with_canvas() * field.player.position})

	func select(id: String, site: String) -> void:
		if submitted or (site != "self" and not _in_reach(site)): return
		for option in options:
			if str(option.id) == id and ((site == "self" and str(option.get("interaction", {}).get("kind", "")) == "self") or option in choices_for(site)):
				_submit(option, site, "local-choice")
				return

	func _submit(option: Dictionary, site: String, source: String) -> void:
		var id := _id()
		_stamp("input_request", id, {"choice_id": option.id, "site": site, "source": source, "game_time": field.snapshot.get("game_time", 0)})
		submitted = true
		field.field_event.emit({"kind": "select", "choice_id": option.id, "site": site, "interaction_id": id, "context": field.context()})

class FieldNavigationInner extends RefCounted:
	const CLICK_TARGET_WIDTH := 24.0
	const ROUTE_CELL := 8

	static func walkable(center: Vector2, floors: Dictionary, obstacles: Array[Rect2]) -> bool:
		for corner in [Vector2(-10,-14),Vector2(9,-14),Vector2(-10,1),Vector2(9,1)]:
			if not floors.has(Vector2i((center + corner) / 32.0)): return false
		for obstacle in obstacles:
			if obstacle.grow_individual(10,1,10,14).has_point(center): return false
		return true

	static func route(start: Vector2, finish: Vector2, floors: Dictionary, obstacles: Array[Rect2], map_size: Vector2i) -> PackedVector2Array:
		return route_on_grid(start, finish, build_route_grid(floors, obstacles, map_size))

	static func build_route_grid(floors: Dictionary, obstacles: Array[Rect2], map_size: Vector2i) -> AStarGrid2D:
		var grid := AStarGrid2D.new()
		grid.region = Rect2i(Vector2i.ZERO, map_size * (32 / ROUTE_CELL))
		grid.cell_size = Vector2(ROUTE_CELL, ROUTE_CELL)
		grid.offset = grid.cell_size / 2.0
		grid.diagonal_mode = AStarGrid2D.DIAGONAL_MODE_NEVER
		grid.update()
		for y in grid.region.size.y:
			for x in grid.region.size.x:
				var node := Vector2i(x, y)
				var center := grid.get_point_position(node)
				grid.set_point_solid(node, not walkable(center, floors, obstacles))
		return grid

	static func route_on_grid(start: Vector2, finish: Vector2, grid: AStarGrid2D) -> PackedVector2Array:
		var from := _free_endpoint(grid, start)
		var to := _free_endpoint(grid, finish)
		if from.x < 0 or to.x < 0: return PackedVector2Array()
		return grid.get_point_path(from, to)

	static func _free_endpoint(grid: AStarGrid2D, point: Vector2) -> Vector2i:
		var cell := Vector2i(((point - grid.offset) / grid.cell_size).round())
		var best := Vector2i(-1, -1)
		var distance := INF
		for y in range(-1, 2):
			for x in range(-1, 2):
				var candidate := cell + Vector2i(x, y)
				if not grid.region.has_point(candidate) or grid.is_point_solid(candidate): continue
				var offset := grid.get_point_position(candidate).distance_squared_to(point)
				if offset < distance:
					distance = offset
					best = candidate
		return best

	static func site_point(id: String, sites: Dictionary, actors: Dictionary) -> Vector2:
		var site: Dictionary = sites[id]
		return actors[site.title].position if actors.has(site.title) else site.point

	static func clicked_site(position: Vector2, sites: Dictionary, actors: Dictionary) -> String:
		var chosen := ""
		var best_distance := INF
		for id in sites:
			var point := site_point(id, sites, actors)
			var actor_site := actors.has(sites[id].title)
			var top := 52.0 if actor_site else 80.0 if id.begins_with("bed_") else 56.0
			if not Rect2(point + Vector2(-CLICK_TARGET_WIDTH, -top), Vector2(CLICK_TARGET_WIDTH * 2.0, top + 10.0)).has_point(position):
				continue
			var marker := point + Vector2(0, -46 if actor_site else -12)
			var distance := position.distance_squared_to(marker)
			if distance < best_distance:
				best_distance = distance
				chosen = id
		return chosen

