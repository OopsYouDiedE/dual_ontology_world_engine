extends Node2D
## 第二章场景浏览：载入 Maps/ 下的地图，侦探用鼠标点击或 WASD/方向键行走，进门口自动换图。
## 室外分六个相互重叠的区域：走出本区核心范围即换到所在区域，保持世界坐标；重叠宽 24 m，镜头连续。
## 走到建筑或树冠背后时，遮挡物半透明。只承载场景与移动；剧情、认知与规则由第二章系统另行接入。

const FieldNavigation := preload("res://scenes/Chapter2/Field/FieldNavigation.gd")
const CLICK_STOP_RADIUS := 8.0
const CLICK_REPATH_DELAY := 0.15
const CLICK_MAX_BLOCKED_REPATHS := 4
const APPROACH_DIRECTIONS := [Vector2.UP, Vector2.DOWN, Vector2.LEFT, Vector2.RIGHT, Vector2(-1, -1), Vector2(1, -1), Vector2(-1, 1), Vector2(1, 1)]

const MAPS := "res://scenes/Chapter2/Maps/%s.tscn"
const ACTOR := preload("res://scenes/Story/StoryActor.gd")
const FRAMES := preload("res://ShareAsset/Internal/Characters/animation_v2/detective.tres")

@export var start_map := "living_hall"
@export var start_spawn := "north_door"

var map: Node2D
var player: CharacterBody2D
var camera: Camera2D
var place_label: Label
var armed := true          # 换图后须先离开出入口区域，才会再次触发
var switching := false
var occluders: Array[Sprite2D] = []
var click_path := PackedVector2Array()
var click_waypoint := 0
var click_interaction_id := ""
var click_last_position := Vector2(INF, INF)
var click_stall_seconds := 0.0
var click_blocked_repaths := 0
var click_progress_anchor := Vector2(INF, INF)
var floors: Dictionary = {}
var obstacle_rects: Array[Rect2] = []
var route_grid: AStarGrid2D
var navigation_size := Vector2i.ZERO
const SEAM := 36.0         # 区域切换的回差（像素）

func _ready() -> void:
	player = CharacterBody2D.new()
	player.set_script(ACTOR)
	player.standard = FRAMES
	player.speed = 120.0
	player.name = "Detective"
	add_child(player)
	camera = Camera2D.new()
	camera.zoom = Vector2(2, 2)
	camera.position_smoothing_enabled = true
	player.add_child(camera)
	var hud := CanvasLayer.new()
	add_child(hud)
	place_label = Label.new()
	place_label.position = Vector2(12, 8)
	place_label.add_theme_color_override("font_color", Color(0.95, 0.92, 0.84))
	place_label.add_theme_color_override("font_outline_color", Color(0.1, 0.08, 0.1))
	place_label.add_theme_constant_override("outline_size", 6)
	hud.add_child(place_label)
	var args := OS.get_cmdline_user_args()
	for a in args:
		if a.begins_with("--map="): start_map = a.substr(6)
		elif a.begins_with("--spawn="): start_spawn = a.substr(8)
	load_map(start_map, start_spawn)

func load_map(key: String, spawn: String, at := Vector2.ZERO, use_at := false) -> void:
	_cancel_click_navigation()
	if map:
		# 先停掉旧图出口的检测并断开信号，再把侦探移出、释放旧图，避免物理回调里改动碰撞体
		for exit in map.get_node("Exits").get_children():
			var area := exit as Area2D
			area.monitoring = false
			for c in area.body_entered.get_connections():
				area.body_entered.disconnect(c.callable)
			for c in area.body_exited.get_connections():
				area.body_exited.disconnect(c.callable)
		player.get_parent().remove_child(player)
		add_child(player)
		var old := map
		remove_child(old)
		old.queue_free()
	map = (load(MAPS % key) as PackedScene).instantiate()
	add_child(map)
	move_child(map, 0)
	remove_child(player)
	map.get_node("Objects").add_child(player)
	var marker := map.get_node_or_null("Spawns/" + spawn) as Marker2D
	var fallback := Vector2(64, 64)
	if map.has_meta("outdoor_core"):
		fallback = (map.get_meta("outdoor_core") as Rect2).get_center() - (map.get_meta("outdoor_origin") as Vector2)
	player.position = at if use_at else (marker.position if marker else fallback)
	var size: Vector2 = map.get_meta("map_size", Vector2(1024, 1024))
	camera.limit_left = 0
	camera.limit_top = 0
	camera.limit_right = int(size.x)
	camera.limit_bottom = int(size.y)
	camera.reset_smoothing()
	_rebuild_navigation(size)
	occluders.clear()
	for node in map.get_node("Objects").get_children():
		if node is Sprite2D and node.has_meta("occluder"):
			occluders.append(node)
	switching = false
	place_label.text = String(map.get_meta("title", key))
	armed = false
	for exit in map.get_node("Exits").get_children():
		(exit as Area2D).body_entered.connect(_on_exit_entered.bind(exit))
		(exit as Area2D).body_exited.connect(_on_exit_left)

func _on_exit_entered(body: Node, exit: Area2D) -> void:
	if body != player or not armed or not field_active():
		return
	armed = false
	call_deferred("load_map", String(exit.get_meta("target")), String(exit.get_meta("spawn")))

func _on_exit_left(body: Node) -> void:
	if body == player:
		armed = true

func _physics_process(delta: float) -> void:
	if not armed and not _inside_exit():
		armed = true
	_check_region()
	_fade_occluders()
	if not field_active():
		_cancel_click_navigation()
		player.velocity = Vector2.ZERO
		player.pose("idle")
		return
	var direction := Vector2(
		float(Input.is_physical_key_pressed(KEY_D) or Input.is_physical_key_pressed(KEY_RIGHT)) - float(Input.is_physical_key_pressed(KEY_A) or Input.is_physical_key_pressed(KEY_LEFT)),
		float(Input.is_physical_key_pressed(KEY_S) or Input.is_physical_key_pressed(KEY_DOWN)) - float(Input.is_physical_key_pressed(KEY_W) or Input.is_physical_key_pressed(KEY_UP)))
	var movement_scale := 1.0
	if direction.length_squared() > 0:
		_cancel_click_navigation()
	if direction == Vector2.ZERO and click_interaction_id != "":
		var target_id := click_interaction_id
		if player.position.distance_to(site_point(target_id)) <= site_reach(target_id) + 6.0:
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
				if player.position.distance_to(site_point(target_id)) <= site_reach(target_id) + 8.0:
					_complete_click_interaction(target_id)
					return
		else:
			var remaining := click_path[click_waypoint] - player.position
			direction = remaining.normalized()
			movement_scale = minf(1.0, remaining.length() / maxf(player.speed * delta, 0.00001))
	if not player.seated:
		player.move_input(direction.normalized() * movement_scale)

func _inside_exit() -> bool:
	for exit in map.get_node("Exits").get_children():
		if (exit as Area2D).overlaps_body(player):
			return true
	return false

func _check_region() -> void:
	if switching or not field_active() or not map.has_meta("outdoor_core"):
		return
	var g: Vector2 = player.position + (map.get_meta("outdoor_origin") as Vector2)
	if (map.get_meta("outdoor_core") as Rect2).grow(SEAM).has_point(g):
		return
	var cores: Dictionary = map.get_meta("outdoor_cores")
	var origins: Dictionary = map.get_meta("outdoor_origins")
	for key in cores:
		if (cores[key] as Rect2).has_point(g):
			switching = true
			call_deferred("load_map", String(key), "", g - (origins[key] as Vector2), true)
			return

func _fade_occluders() -> void:
	var head := player.global_position + Vector2(0, -24)
	for s in occluders:
		var behind := player.position.y < s.position.y and s.get_rect().has_point(s.to_local(head))
		s.modulate.a = move_toward(s.modulate.a, 0.42 if behind else 1.0, 0.12)

# Host hooks: Chapter2Game supplies visible targets and interaction callbacks.
func field_active() -> bool:
	return true

func clicked_site(_position: Vector2) -> String:
	return ""

func site_point(_id: String) -> Vector2:
	return Vector2.ZERO

func site_reach(_id: String) -> float:
	return 64.0

func complete_click_interaction(_id: String) -> void:
	pass

func _input(event: InputEvent) -> void:
	if not field_active():
		return
	if event is not InputEventMouseButton or not event.pressed or event.button_index != MOUSE_BUTTON_LEFT:
		return
	if get_viewport().gui_get_hovered_control() != null:
		return
	click_blocked_repaths = 0
	click_progress_anchor = player.position
	_cancel_click_navigation()
	var destination: Vector2 = map.get_global_transform_with_canvas().affine_inverse() * event.position
	var site_id := clicked_site(destination)
	if site_id == "":
		_move_to_click(destination)
	else:
		_approach_site(site_id)
	get_viewport().set_input_as_handled()

func _cancel_click_navigation() -> void:
	click_path.clear()
	click_waypoint = 0
	click_interaction_id = ""
	click_last_position = Vector2(INF, INF)
	click_stall_seconds = 0.0

func _complete_click_interaction(id: String) -> void:
	_cancel_click_navigation()
	player.move_input(Vector2.ZERO)
	complete_click_interaction(id)

func _move_to_click(destination: Vector2) -> void:
	_cancel_click_navigation()
	if not _walkable(destination):
		return
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
	var reach := site_reach(id)
	# 与行走中“到了就打开”的判定一致：站在够得着的地方，再点一次就直接打开，不绕到别处。
	if player.position.distance_to(point) <= reach + 6.0:
		_complete_click_interaction(id)
		return
	var best_path := PackedVector2Array()
	var best_destination := Vector2.ZERO
	var best_cost := INF
	var grid := _navigation_grid()
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
	return FieldNavigation.walkable(point, floors, obstacle_rects)

func _navigation_grid() -> AStarGrid2D:
	if route_grid == null:
		route_grid = FieldNavigation.build_route_grid(floors, obstacle_rects, navigation_size)
	return route_grid

func _route(from: Vector2, to: Vector2) -> PackedVector2Array:
	return FieldNavigation.route_on_grid(from, to, _navigation_grid())

func _rebuild_navigation(size: Vector2) -> void:
	# Existing map colliders remain the authority; Area2D exits are not obstacles.
	floors.clear()
	obstacle_rects.clear()
	route_grid = null
	navigation_size = Vector2i(ceil(size.x / 32.0), ceil(size.y / 32.0))
	for y in navigation_size.y:
		for x in navigation_size.x:
			floors[Vector2i(x, y)] = true
	for node in map.find_children("*", "CollisionShape2D", true, false):
		var collision := node as CollisionShape2D
		if collision.disabled or collision.shape == null or collision.get_parent() is not StaticBody2D:
			continue
		var local_rect := collision.shape.get_rect()
		var transform := map.global_transform.affine_inverse() * collision.global_transform
		var bounds := Rect2(transform * local_rect.position, Vector2.ZERO)
		for corner in [local_rect.position, Vector2(local_rect.end.x, local_rect.position.y), local_rect.end, Vector2(local_rect.position.x, local_rect.end.y)]:
			bounds = bounds.expand(transform * corner)
		obstacle_rects.append(bounds)
