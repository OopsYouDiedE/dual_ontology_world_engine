class_name WinterScene
extends Node2D

## 对应 game_resource/dist/winter.js
## 原样搬运冬日荒野场景逻辑

const WORLD_WIDTH := 1600.0
const WORLD_HEIGHT := 900.0

var keys := {}
var totals := {"wood": 0, "ice": 0, "stone": 0}
var last_time := 0.0
var time := 0.0
var paused := false
var ready_flag := false
var work_held := false
var log_until := 0.0
var event_log_text := "威尔逊已抵达营地。使用 WASD 或方向键移动。"
var random_seed := 721

func random() -> float:
	random_seed = int((int(random_seed) * 1664525 + 1013904223) & 0xFFFFFFFF)
	return float(random_seed & 0xFFFFFFFF) / 4294967296.0

var player := {
	"name": "威尔逊",
	"sheet": "wilson",
	"x": 1080.0,
	"y": 580.0,
	"dir": 0,
	"mode": "idle",
	"phase": 0.0,
	"progress": 0.0,
	"speed": 175.0,
	"target": null
}

var companion := {
	"name": "温蒂",
	"sheet": "wendy",
	"x": 830.0,
	"y": 425.0,
	"dir": 0,
	"mode": "idle",
	"phase": 0.0,
	"progress": 0.0,
	"speed": 104.0,
	"target": null,
	"wait": 1.2,
	"stuck": 0.0
}

var nodes := [
	{"x": 1336.0, "y": 538.0, "kind": "wood", "label": "枯枝", "stock": 3, "max": 3, "cooldown": 0.0},
	{"x": 1408.0, "y": 739.0, "kind": "stone", "label": "碎石", "stock": 3, "max": 3, "cooldown": 0.0},
	{"x": 603.0, "y": 655.0, "kind": "ice", "label": "冰晶", "stock": 3, "max": 3, "cooldown": 0.0},
	{"x": 666.0, "y": 369.0, "kind": "wood", "label": "枯枝", "stock": 3, "max": 3, "cooldown": 0.0},
	{"x": 385.0, "y": 364.0, "kind": "ice", "label": "冰晶", "stock": 3, "max": 3, "cooldown": 0.0},
	{"x": 950.0, "y": 837.0, "kind": "wood", "label": "枯枝", "stock": 3, "max": 3, "cooldown": 0.0}
]

var kind_names := {"wood": "木料", "ice": "冰晶", "stone": "石料"}
var snow: Array[Dictionary] = []
var sprites := {}
var background_texture: Texture2D

func _ready() -> void:
	# 初始化雪花
	snow.clear()
	for i in range(70):
		snow.append({
			"x": random() * 1600.0,
			"y": random() * 900.0,
			"r": random() * 1.6 + 0.5,
			"speed": random() * 20.0 + 12.0,
			"phase": random() * 6.28
		})
	
	load_assets()

func load_assets() -> void:
	var bg_img = Image.load_from_file("res://show_sample/assets/winter-ground.png")
	if bg_img == null:
		var global_path = ProjectSettings.globalize_path("res://show_sample/assets/winter-ground.png")
		bg_img = Image.load_from_file(global_path)
	if bg_img:
		background_texture = ImageTexture.create_from_image(bg_img)
	
	var wilson_img = Image.load_from_file("res://show_sample/assets/wilson-sprite.png")
	if wilson_img == null:
		wilson_img = Image.load_from_file(ProjectSettings.globalize_path("res://show_sample/assets/wilson-sprite.png"))
	
	var wendy_img = Image.load_from_file("res://show_sample/assets/wendy-sprite.png")
	if wendy_img == null:
		wendy_img = Image.load_from_file(ProjectSettings.globalize_path("res://show_sample/assets/wendy-sprite.png"))
	
	if wilson_img and wendy_img:
		sprites["wilson"] = parse_sheet(wilson_img)
		sprites["wendy"] = parse_sheet(wendy_img)
		ready_flag = true
		log_message("威尔逊已抵达营地。使用 WASD 或方向键移动。")

func parse_sheet(img: Image) -> Dictionary:
	var sw: int = img.get_width() / 4
	var sh: int = img.get_height() / 4
	var frames: Array = []
	var heights: Array[float] = []
	
	for row in range(4):
		var row_frames: Array = []
		for col in range(4):
			var min_x: int = sw
			var min_y: int = sh
			var max_x: int = 0
			var max_y: int = 0
			for y in range(4, sh - 4):
				for x in range(4, sw - 4):
					var gx: int = col * sw + x
					var gy: int = row * sh + y
					var pixel: Color = img.get_pixel(gx, gy)
					if pixel.a > (40.0 / 255.0):
						min_x = mini(min_x, x)
						min_y = mini(min_y, y)
						max_x = maxi(max_x, x)
						max_y = maxi(max_y, y)
			var h: float = float(max_y - min_y)
			heights.append(h)
			row_frames.append({
				"rect": Rect2(col * sw, row * sh, sw, sh),
				"anchorX": float(min_x + max_x) / 2.0,
				"anchorY": float(max_y),
				"height": h
			})
		frames.append(row_frames)
	
	heights.sort()
	var median_h: float = heights[8] if heights.size() > 8 else 122.0
	var scale_val: float = 122.0 / median_h if median_h > 0 else 1.0
	return {
		"texture": ImageTexture.create_from_image(img),
		"frames": frames,
		"scale": scale_val
	}

func log_message(message: String) -> void:
	event_log_text = message
	log_until = time + 5.0

func walkable(x: float, y: float) -> bool:
	return x > 120.0 and x < 1490.0 and y > 190.0 and y < 845.0 and not (x > 1060.0 and y < 355.0)

func facing(a: Dictionary, dx: float, dy: float) -> void:
	if abs(dx) > abs(dy):
		a["dir"] = 1 if dx < 0 else 2
	elif dy != 0:
		a["dir"] = 3 if dy < 0 else 0

func move_actor(a: Dictionary, dx: float, dy: float, dt: float) -> bool:
	facing(a, dx, dy)
	var nx: float = float(a["x"]) + dx * float(a["speed"]) * dt
	var ny: float = float(a["y"]) + dy * float(a["speed"]) * dt
	var moved: bool = false
	if walkable(nx, float(a["y"])):
		if nx != float(a["x"]):
			moved = true
		a["x"] = nx
	if walkable(float(a["x"]), ny):
		if ny != float(a["y"]):
			moved = true
		a["y"] = ny
	a["mode"] = "walk" if moved else "idle"
	a["progress"] = 0.0
	a["phase"] = float(a["phase"]) + dt * (9.0 if moved else 2.0)
	return moved

func nearest(a: Dictionary) -> Dictionary:
	var available: Array = []
	for n in nodes:
		if n["stock"] > 0:
			available.append(n)
	if available.is_empty():
		return {}
	available.sort_custom(func(b, c):
		var d1 = Vector2(a["x"] - b["x"], a["y"] - b["y"]).length_squared()
		var d2 = Vector2(a["x"] - c["x"], a["y"] - c["y"]).length_squared()
		return d1 < d2
	)
	return available[0]

func in_reach(a: Dictionary, n: Dictionary) -> bool:
	if n.is_empty():
		return false
	return Vector2(a["x"] - n["x"], a["y"] - n["y"]).length() < 88.0

func harvest(a: Dictionary, n: Dictionary, dt: float) -> void:
	a["mode"] = "work"
	a["phase"] = float(a["phase"]) + dt * 7.0
	facing(a, n["x"] - a["x"], n["y"] - a["y"])
	a["progress"] = float(a["progress"]) + dt / 1.45
	if float(a["progress"]) >= 1.0:
		a["progress"] = 0.0
		n["stock"] = int(n["stock"]) - 1
		var kind: String = n["kind"]
		totals[kind] = int(totals.get(kind, 0)) + 1
		var suffix: String = "，这处物资需要恢复。" if n["stock"] == 0 else "。"
		log_message(str(a["name"]) + "采集了" + kind_names.get(kind, "") + " +1" + suffix)
		if n["stock"] == 0:
			n["cooldown"] = 28.0
			a["mode"] = "idle"
			a["target"] = null
			if a == companion:
				a["wait"] = 1.3

func update_player(dt: float) -> void:
	var direction := ""
	if keys.get("w", false) or keys.get("up", false):
		direction = "up"
	elif keys.get("s", false) or keys.get("down", false):
		direction = "down"
	elif keys.get("a", false) or keys.get("left", false):
		direction = "left"
	elif keys.get("d", false) or keys.get("right", false):
		direction = "right"
	
	var near_node = nearest(player)
	player["target"] = near_node if in_reach(player, near_node) else null
	
	if direction != "":
		var vec_map := {
			"up": Vector2(0, -1),
			"down": Vector2(0, 1),
			"left": Vector2(-1, 0),
			"right": Vector2(1, 0)
		}
		var vec: Vector2 = vec_map.get(direction, Vector2.ZERO)
		move_actor(player, vec.x, vec.y, dt)
	elif work_held and player["target"] != null:
		harvest(player, player["target"], dt)
	else:
		player["mode"] = "idle"
		player["phase"] = float(player["phase"]) + dt
		player["progress"] = 0.0

func update_ai(dt: float) -> void:
	var a := companion
	if float(a["wait"]) > 0.0:
		a["wait"] = float(a["wait"]) - dt
		a["mode"] = "idle"
		a["phase"] = float(a["phase"]) + dt
		return
	
	if a["target"] == null or a["target"].get("stock", 0) == 0:
		var choices: Array = []
		for n in nodes:
			if n["stock"] > 0 and n != player["target"]:
				choices.append(n)
		if choices.size() > 0:
			choices.sort_custom(func(b, c):
				var d1 = Vector2(a["x"] - b["x"], a["y"] - b["y"]).length_squared()
				var d2 = Vector2(a["x"] - c["x"], a["y"] - c["y"]).length_squared()
				return d1 < d2
			)
			var idx = mini(1, choices.size() - 1)
			a["target"] = choices[idx]
		else:
			var near = nearest(a)
			a["target"] = near if not near.is_empty() else null
		a["progress"] = 0.0
		a["stuck"] = 0.0
	
	if a["target"] == null or a["target"].is_empty():
		a["mode"] = "idle"
		return
	
	if in_reach(a, a["target"]):
		harvest(a, a["target"], dt)
		return
	
	var dx: float = float(a["target"]["x"]) - float(a["x"])
	var dy: float = float(a["target"]["y"]) - float(a["y"])
	var mx: float = 0.0
	var my: float = 0.0
	if abs(dx) > abs(dy):
		mx = sign(dx)
	else:
		my = sign(dy)
	
	if not move_actor(a, mx, my, dt):
		a["stuck"] = float(a["stuck"]) + dt
		var alt_x = 0.0 if mx != 0 else (sign(dx) if sign(dx) != 0 else 1.0)
		var alt_y = 0.0 if my != 0 else (sign(dy) if sign(dy) != 0 else 1.0)
		move_actor(a, alt_x, alt_y, dt)
		if float(a["stuck"]) > 2.0:
			a["target"] = null
			a["wait"] = 0.5
	else:
		a["stuck"] = 0.0

func _process(delta: float) -> void:
	if not ready_flag:
		return
	var dt: float = minf(delta, 0.04)
	if not paused:
		time += dt
		for n in nodes:
			if n["stock"] == 0:
				n["cooldown"] = float(n["cooldown"]) - dt
				if float(n["cooldown"]) <= 0.0:
					n["stock"] = n["max"]
		update_player(dt)
		update_ai(dt)
		for s in snow:
			s["x"] = float(s["x"]) + dt * (7.0 + sin(time * 0.2 + float(s["phase"])) * 5.0)
			s["y"] = float(s["y"]) + dt * float(s["speed"])
			if float(s["y"]) > 900.0:
				s["y"] = -4.0
				s["x"] = random() * 1600.0
			if float(s["x"]) > 1600.0:
				s["x"] = 0.0
		if time > log_until:
			event_log_text = "温蒂会自主寻找并采集物资，收获计入营地。"
			log_until = 999999999.0
	queue_redraw()

func _input(event: InputEvent) -> void:
	if event is InputEventKey:
		var key_code = event.keycode
		var pressed = event.pressed
		var is_echo = event.echo
		if key_code == KEY_W:
			keys["w"] = pressed
		elif key_code == KEY_S:
			keys["s"] = pressed
		elif key_code == KEY_A:
			keys["a"] = pressed
		elif key_code == KEY_D:
			keys["d"] = pressed
		elif key_code == KEY_UP:
			keys["up"] = pressed
		elif key_code == KEY_DOWN:
			keys["down"] = pressed
		elif key_code == KEY_LEFT:
			keys["left"] = pressed
		elif key_code == KEY_RIGHT:
			keys["right"] = pressed
		elif key_code == KEY_SPACE:
			work_held = pressed
		elif key_code == KEY_P and pressed and not is_echo:
			set_pause(not paused)

func set_pause(val: bool) -> void:
	paused = val
	keys.clear()
	work_held = false

func reset() -> void:
	player["x"] = 1080.0
	player["y"] = 580.0
	player["dir"] = 0
	player["mode"] = "idle"
	player["phase"] = 0.0
	player["progress"] = 0.0
	player["target"] = null
	
	companion["x"] = 830.0
	companion["y"] = 425.0
	companion["dir"] = 0
	companion["mode"] = "idle"
	companion["phase"] = 0.0
	companion["progress"] = 0.0
	companion["target"] = null
	companion["wait"] = 1.2
	companion["stuck"] = 0.0
	
	totals["wood"] = 0
	totals["ice"] = 0
	totals["stone"] = 0
	
	for n in nodes:
		n["stock"] = n["max"]
		n["cooldown"] = 0.0
	
	log_message("营地已重置。靠近物资标记，按住空格开始采集。")
	set_pause(false)

func _draw() -> void:
	if not ready_flag:
		return
	
	# 背景
	if background_texture:
		draw_texture_rect(background_texture, Rect2(0, 0, WORLD_WIDTH, WORLD_HEIGHT), false)
	
	# 节点
	draw_nodes()
	
	# 角色排序
	var actors = [player, companion]
	actors.sort_custom(func(a, b): return a["y"] < b["y"])
	for a in actors:
		draw_actor(a)
	
	# 雪花
	for s in snow:
		var sx = float(s["x"]) + sin(time + float(s["phase"])) * 3.0
		var sy = float(s["y"])
		draw_circle(Vector2(sx, sy), float(s["r"]), Color(0.96, 0.99, 0.98, 0.73))
	
	# HUD 顶部物资与提示
	draw_hud()

func draw_nodes() -> void:
	for n in nodes:
		var selected = (n == player["target"])
		var pos = Vector2(n["x"], n["y"])
		var stock = int(n["stock"])
		
		var circle_color = Color(0.38, 0.48, 0.47, 0.47) if stock == 0 else (Color(0.94, 0.89, 0.67) if selected else Color(0.84, 0.89, 0.85, 0.63))
		var radius = 45.0 if selected else 32.0
		draw_arc(pos, radius, 0.0, TAU, 32, circle_color, 3.0 if selected else 1.5)
		
		if stock > 0:
			var diamond_color = Color(0.97, 0.87, 0.55) if selected else Color(0.86, 0.90, 0.84)
			var d_center = pos + Vector2(0, -9)
			var points: PackedVector2Array = [
				d_center + Vector2(0, -4),
				d_center + Vector2(4, 0),
				d_center + Vector2(0, 4),
				d_center + Vector2(-4, 0)
			]
			draw_colored_polygon(points, diamond_color)
			draw_string(ThemeDB.fallback_font, pos + Vector2(-20, 30), str(n["label"]) + " (" + str(stock) + ")", HORIZONTAL_ALIGNMENT_CENTER, -1, 12, Color.WHITE)
		else:
			var cd = ceil(float(n["cooldown"]))
			draw_string(ThemeDB.fallback_font, pos + Vector2(-15, 24), str(int(cd)) + "s", HORIZONTAL_ALIGNMENT_CENTER, -1, 11, Color(0.24, 0.35, 0.32))

func draw_actor(a: Dictionary) -> void:
	var sheet_name = a["sheet"]
	var s = sprites.get(sheet_name, {})
	if s.is_empty():
		return
	
	var mode = a["mode"]
	var phase_val = float(a["phase"])
	var col = (2 + int(phase_val) % 2) if mode == "work" else ((int(phase_val) % 2) if mode == "walk" else 0)
	var dir_row = [0, 2, 1, 3][int(a["dir"])] if sheet_name == "wilson" else int(a["dir"])
	var f = s["frames"][dir_row][col]
	var scale_val: float = float(s["scale"])
	var pos = Vector2(a["x"], a["y"])
	
	# 脚下阴影
	draw_ellipse_shape(pos, 25.0, 9.0, Color(0.07, 0.14, 0.13, 0.23))
	if a == player:
		draw_ellipse_outline(pos + Vector2(0, 1), 31.0, 11.0, Color(0.93, 0.85, 0.62, 0.7), 2.0)
	
	var bob = sin(phase_val * PI) * 1.5 if mode == "walk" else 0.0
	var flip_h = (sheet_name == "wendy" and int(a["dir"]) == 1 and col >= 2)
	
	var src_rect: Rect2 = f["rect"]
	var dest_pos = pos + Vector2(-float(f["anchorX"]) * scale_val, -float(f["anchorY"]) * scale_val + bob)
	var dest_size = Vector2(src_rect.size.x * scale_val, src_rect.size.y * scale_val)
	
	if flip_h:
		dest_pos.x += dest_size.x
		dest_size.x = -dest_size.y
	
	draw_texture_rect_region(s["texture"], Rect2(dest_pos, dest_size), src_rect)
	
	# 头顶名字标签
	var is_player = (a == player)
	var tag_title = str(a["name"]) + (" · 你" if is_player else " · AI")
	draw_string(ThemeDB.fallback_font, pos + Vector2(-30, -130), tag_title, HORIZONTAL_ALIGNMENT_CENTER, -1, 12, Color(0.94, 0.87, 0.69) if is_player else Color(0.89, 0.91, 0.87))
	
	# 采集进度条
	if mode == "work":
		var bar_rect = Rect2(pos.x - 26, pos.y - 117, 52, 4)
		draw_rect(bar_rect, Color(0.15, 0.23, 0.21, 0.87))
		draw_rect(Rect2(pos.x - 26, pos.y - 117, 52.0 * float(a["progress"]), 4), Color(0.91, 0.78, 0.49))

func draw_ellipse_shape(center: Vector2, rx: float, ry: float, color: Color) -> void:
	var points: PackedVector2Array = []
	var count = 24
	for i in range(count):
		var theta = float(i) / float(count) * TAU
		points.append(center + Vector2(cos(theta) * rx, sin(theta) * ry))
	draw_colored_polygon(points, color)

func draw_ellipse_outline(center: Vector2, rx: float, ry: float, color: Color, width: float) -> void:
	var points: PackedVector2Array = []
	var count = 24
	for i in range(count + 1):
		var theta = float(i % count) / float(count) * TAU
		points.append(center + Vector2(cos(theta) * rx, sin(theta) * ry))
	draw_polyline(points, color, width)

func draw_hud() -> void:
	# 物资统计条
	var hud_bg = Rect2(20, 20, 480, 50)
	draw_rect(hud_bg, Color(0.1, 0.15, 0.13, 0.85))
	var txt = "木料: %d   冰晶: %d   石料: %d" % [totals["wood"], totals["ice"], totals["stone"]]
	draw_string(ThemeDB.fallback_font, Vector2(35, 52), txt, HORIZONTAL_ALIGNMENT_LEFT, -1, 16, Color(0.95, 0.92, 0.84))
	
	# 日志条
	var log_bg = Rect2(20, 840, 1000, 40)
	draw_rect(log_bg, Color(0.1, 0.15, 0.13, 0.85))
	draw_string(ThemeDB.fallback_font, Vector2(35, 866), event_log_text, HORIZONTAL_ALIGNMENT_LEFT, -1, 14, Color(0.9, 0.93, 0.88))
	
	# 状态提示
	var p_mode_str = "行走中" if player["mode"] == "walk" else ("正在采集" if player["mode"] == "work" else "待命")
	var ai_mode_str = "前往采集点" if companion["mode"] == "walk" else ("正在采集" if companion["mode"] == "work" else "待命")
	var state_txt = "威尔逊: " + p_mode_str + " | 温蒂: " + ai_mode_str
	draw_string(ThemeDB.fallback_font, Vector2(520, 52), state_txt, HORIZONTAL_ALIGNMENT_LEFT, -1, 14, Color(0.8, 0.85, 0.8))
