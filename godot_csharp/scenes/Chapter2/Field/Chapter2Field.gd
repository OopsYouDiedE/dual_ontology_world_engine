extends Node2D
## 第二章场景浏览：载入 Maps/ 下的地图，侦探用 WASD/方向键行走，进门口自动换图。
## 室外分六个相互重叠的区域：走出本区核心范围即换到所在区域，保持世界坐标；重叠宽 24 m，镜头连续。
## 走到建筑或树冠背后时，遮挡物半透明。只承载场景与移动；剧情、认知与规则由第二章系统另行接入。

const MAPS := "res://scenes/Chapter2/Maps/%s.tscn"
const ACTOR := preload("res://scenes/Story/StoryActor.gd")
const FRAMES := preload("res://ShareAsset/Internal/Characters/PixelCast/detective.tres")

@export var start_map := "living_hall"
@export var start_spawn := "north_door"
@export var hud_scene: PackedScene = preload("res://scenes/Chapter2/Field/FieldHUD.tscn")

var map: Node2D
var player: CharacterBody2D
var camera: Camera2D
var place_label: Label
var armed := true          # 换图后须先离开出入口区域，才会再次触发
var switching := false
var occluders: Array[Sprite2D] = []
const SEAM := 36.0         # 区域切换的回差（像素）

func _ready() -> void:
	player = CharacterBody2D.new()
	player.set_script(ACTOR)
	player.standard = FRAMES
	player.name = "Detective"
	add_child(player)
	camera = Camera2D.new()
	camera.zoom = Vector2(2, 2)
	camera.position_smoothing_enabled = true
	player.add_child(camera)
	var hud: CanvasLayer = hud_scene.instantiate()
	add_child(hud)
	place_label = hud.get_node("Place")
	var args := OS.get_cmdline_user_args()
	for a in args:
		if a.begins_with("--map="): start_map = a.substr(6)
		elif a.begins_with("--spawn="): start_spawn = a.substr(8)
	load_map(start_map, start_spawn)

func load_map(key: String, spawn: String, at := Vector2.ZERO, use_at := false) -> void:
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
	if body != player or not armed:
		return
	armed = false
	call_deferred("load_map", String(exit.get_meta("target")), String(exit.get_meta("spawn")))

func _on_exit_left(body: Node) -> void:
	if body == player:
		armed = true

func _physics_process(_delta: float) -> void:
	if not armed and not _inside_exit():
		armed = true
	_check_region()
	_fade_occluders()
	var dir := Input.get_vector("ui_left", "ui_right", "ui_up", "ui_down")
	if Input.is_physical_key_pressed(KEY_A): dir.x -= 1
	if Input.is_physical_key_pressed(KEY_D): dir.x += 1
	if Input.is_physical_key_pressed(KEY_W): dir.y -= 1
	if Input.is_physical_key_pressed(KEY_S): dir.y += 1
	dir = dir.limit_length(1.0)
	player.velocity = dir * 120.0
	player.move_and_slide()
	if dir.length() > 0.1:
		var facing := "front"
		if abs(dir.x) > abs(dir.y): facing = "right" if dir.x > 0 else "left"
		elif dir.y < 0: facing = "back"
		player.pose("walk", facing)
	else:
		player.pose("idle")

func _inside_exit() -> bool:
	for exit in map.get_node("Exits").get_children():
		if (exit as Area2D).overlaps_body(player):
			return true
	return false

func _check_region() -> void:
	if switching or not map.has_meta("outdoor_core"):
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
