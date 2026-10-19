extends Node2D
## 通用世界现场：执行正式 DV 移动意图，承接人物、视线、悬停、碰撞与镜头。
## 地图适配器提供几何、房间、锚点和道具显示；完成回执经 field_event 交回宿主。
## 接口与分层见设计第四部分 §2。

const ACTOR := preload("res://scenes/Story/StoryActor.gd")
const FieldNavigation := preload("res://scenes/Shared/FieldNavigation.gd")
const FieldVocabulary := preload("res://scenes/Shared/Field/FieldVocabulary.gd")
const Bindings := preload("res://scenes/Shared/Field/PresentationBindings.gd")
const SightMaskScene := preload("res://scenes/UI/Panels/Field/SightMask.tscn")
const HintOverlayScene := preload("res://scenes/UI/Panels/Field/HintOverlay.tscn")
const ROOM_STAGE := preload("res://scenes/Shared/Field/RoomStage.gd")
const SPATIAL_INPUT := preload("res://scenes/Shared/Field/SpatialInput.gd")
const PIXEL_FRAMES := "res://ShareAsset/Internal/Characters/PixelCast/%s.tres"
const ANIME_FRAMES := "res://ShareAsset/Internal/Characters/AnimeCast/%s.tres"
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
	sight = SightMaskScene.instantiate()
	sight.name = "Sight"
	sight.z_index = 40
	sight.host = adapter
	sight.set_process(false)
	add_child(sight)
	hints = HintOverlayScene.instantiate()
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
	if execution_bridge != null and adapter.map_key() == key:
		# setup already adopted the retained body at its actual endpoint.
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

func position_occupied(walker: CharacterBody2D, point: Vector2) -> bool:
	for other in actors.values():
		if not is_instance_valid(other) or other == walker or other.get_parent() != walker.get_parent(): continue
		var separation: Vector2 = (other.position - point).abs()
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
	for point in _station_approaches(station, grid):
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
	if best_route.is_empty():
		_daily_report(who, int(ev.serial), "blocked", "", str(ev.get("action", "rest")), {"station": str(ev.target)})
		return
	_cancel_motion(actor)
	if actor.seated: actor.stand_up()
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
