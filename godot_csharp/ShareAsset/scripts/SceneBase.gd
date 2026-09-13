class_name SceneBase
extends Node2D

## 游戏场景公用基类
## 承载手绘精灵漫水透明化切片、八向动画走位、统一 HUD 呈现与输入路由

const CYCLE := [0, 1, 2, 2]
const DIRS := ["南", "西南", "西", "西北", "北", "东北", "东", "东南"]

var ready_flag := false
var time := 0.0
var elapsed := 0.0
var paused := false

var actors: Array = []
var player: Dictionary = {}
var target = null # Vector2 or null
var keys := {}

var hud_title := ""
var hud_act := ""
var hud_desc := ""
var hud_label := "状态指标"
var hud_value := 0.0
var hud_note := ""
var speech_text := ""
var record_text := ""

var art_bg: Texture2D
var art_wilson: Dictionary = {}
var art_maxwell: Dictionary = {}
var art_gray: Dictionary = {}
var art_actions: Dictionary = {}

func _ready() -> void:
	load_common_assets()
	_init_scene()
	queue_redraw()

func load_common_assets() -> void:
	var base_path := "res://ShareAsset/assets/"
	art_bg = load_texture(base_path + "manor-stage.png")
	var w_img = load_image(base_path + "wilson-walk.png")
	var m_img = load_image(base_path + "maxwell-walk.png")
	var g_img = load_image(base_path + "gray-walk.png")
	var a_img = load_image(base_path + "story-actions.png")

	if w_img and m_img and g_img and a_img:
		art_wilson = parse_atlas(w_img, 8, 3, false)
		art_maxwell = parse_atlas(m_img, 8, 3, true)
		art_gray = parse_atlas(g_img, 8, 3, true)
		art_actions = parse_atlas(a_img, 6, 3, false)
		ready_flag = true

func load_image(path: String) -> Image:
	var img = Image.load_from_file(path)
	if img == null:
		img = Image.load_from_file(ProjectSettings.globalize_path(path))
	return img

func load_texture(path: String) -> Texture2D:
	var img = load_image(path)
	return ImageTexture.create_from_image(img) if img else null

func parse_atlas(im: Image, cols: int, rows: int, key_white: bool) -> Dictionary:
	var w := im.get_width()
	var h := im.get_height()

	if key_white:
		var visited := {}
		var queue: Array[Vector2i] = []
		var is_white = func(px: Color) -> bool:
			return px.r > (226.0 / 255.0) and px.g > (226.0 / 255.0) and px.b > (226.0 / 255.0)

		for x in range(w):
			if is_white.call(im.get_pixel(x, 0)):
				queue.append(Vector2i(x, 0))
				visited[Vector2i(x, 0)] = true
			if is_white.call(im.get_pixel(x, h - 1)):
				queue.append(Vector2i(x, h - 1))
				visited[Vector2i(x, h - 1)] = true
		for y in range(h):
			if is_white.call(im.get_pixel(0, y)):
				queue.append(Vector2i(0, y))
				visited[Vector2i(0, y)] = true
			if is_white.call(im.get_pixel(w - 1, y)):
				queue.append(Vector2i(w - 1, y))
				visited[Vector2i(w - 1, y)] = true

		var head := 0
		while head < queue.size():
			var p: Vector2i = queue[head]
			head += 1
			var px = im.get_pixel(p.x, p.y)
			px.a = 0.0
			im.set_pixel(p.x, p.y, px)

			var neighbors: Array[Vector2i] = [
				Vector2i(p.x + 1, p.y),
				Vector2i(p.x - 1, p.y),
				Vector2i(p.x, p.y + 1),
				Vector2i(p.x, p.y - 1)
			]
			for n in neighbors:
				if n.x >= 0 and n.x < w and n.y >= 0 and n.y < h and not visited.has(n):
					visited[n] = true
					if is_white.call(im.get_pixel(n.x, n.y)):
						queue.append(n)

	var frames: Array = []
	for r in range(rows):
		var row_frames: Array = []
		for c in range(cols):
			var left := int(float(c * w) / float(cols))
			var right := int(float((c + 1) * w) / float(cols))
			var top := int(float(r * h) / float(rows))
			var bottom := int(float((r + 1) * h) / float(rows))

			var x0 := right
			var y0 := bottom
			var x1 := left
			var y1 := top

			for y in range(top, bottom):
				for x in range(left, right):
					if im.get_pixel(x, y).a > (100.0 / 255.0):
						x0 = mini(x0, x)
						x1 = maxi(x1, x)
						y0 = mini(y0, y)
						y1 = maxi(y1, y)

			var fw := maxi(1, x1 - x0 + 1)
			var fh := maxi(1, y1 - y0 + 1)
			row_frames.append({
				"rect": Rect2(x0, y0, fw, fh),
				"w": float(fw),
				"h": float(fh)
			})
		frames.append(row_frames)

	return {
		"texture": ImageTexture.create_from_image(im),
		"frames": frames
	}

func create_person(kind: String, x: float, y: float) -> Dictionary:
	return {
		"kind": kind,
		"x": x,
		"y": y,
		"dir": 0,
		"moving": false,
		"pose": null
	}

func direction_from(dx: float, dy: float) -> int:
	return int((round(atan2(-dx, dy) / (PI / 4.0)) + 8.0)) % 8

func move_person(p: Dictionary, x: float, y: float, dt: float, speed: float = 120.0) -> bool:
	var dx: float = x - float(p["x"])
	var dy: float = y - float(p["y"])
	var dist: float = Vector2(dx, dy).length()
	if dist < 4.0:
		p["moving"] = false
		return true
	p["dir"] = direction_from(dx, dy)
	var angle: float = float(p["dir"]) * PI / 4.0
	var step: float = minf(speed * dt, dist)
	p["x"] = float(p["x"]) - sin(angle) * step
	p["y"] = float(p["y"]) + cos(angle) * step
	p["moving"] = true
	return false

func is_player_free() -> bool:
	if player.is_empty() or player.get("pose") != null:
		return false
	return true

func process_player_movement(dt: float) -> void:
	if not is_player_free():
		return
	var dx: float = (1.0 if (keys.get("d", false) or keys.get("right", false)) else 0.0) - (1.0 if (keys.get("a", false) or keys.get("left", false)) else 0.0)
	var dy: float = (1.0 if (keys.get("s", false) or keys.get("down", false)) else 0.0) - (1.0 if (keys.get("w", false) or keys.get("up", false)) else 0.0)
	if dx != 0.0 or dy != 0.0:
		var vec = Vector2(dx, dy).normalized()
		player["x"] = float(player["x"]) + vec.x * 130.0 * dt
		player["y"] = float(player["y"]) + vec.y * 130.0 * dt
		player["dir"] = direction_from(dx, dy)
		player["moving"] = true
	elif target != null:
		if move_person(player, target.x, target.y, dt, 130.0):
			target = null
	player["x"] = clampf(float(player["x"]), 270.0, 1000.0)
	player["y"] = clampf(float(player["y"]), 370.0, 705.0)

func _process(delta: float) -> void:
	if not ready_flag:
		return
	var dt := minf(delta, 0.05)
	if not paused:
		time += dt
		elapsed += dt
		for p in actors:
			p["moving"] = false
		process_player_movement(dt)
		_update_scene_logic(dt)
	queue_redraw()

func _input(event: InputEvent) -> void:
	if event is InputEventKey:
		var code = event.keycode
		var pressed = event.pressed
		if code == KEY_W: keys["w"] = pressed
		elif code == KEY_S: keys["s"] = pressed
		elif code == KEY_A: keys["a"] = pressed
		elif code == KEY_D: keys["d"] = pressed
		elif code == KEY_UP: keys["up"] = pressed
		elif code == KEY_DOWN: keys["down"] = pressed
		elif code == KEY_LEFT: keys["left"] = pressed
		elif code == KEY_RIGHT: keys["right"] = pressed

		if pressed:
			if code == KEY_1: change_to_scene(1)
			elif code == KEY_2: change_to_scene(2)
			elif code == KEY_3: change_to_scene(3)
			elif code == KEY_4: change_to_scene(4)
			elif code == KEY_5: change_to_scene(5)
			elif code == KEY_6: change_to_scene(6)
			else:
				_on_scene_key_down(code)

	elif event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		var mx = clampf(event.position.x, 270.0, 1000.0)
		var my = clampf(event.position.y, 370.0, 705.0)
		target = Vector2(mx, my)

func change_to_scene(scene_num: int) -> void:
	var path := "res://scenes/Scene%d/Scene%d.tscn" % [scene_num, scene_num]
	if ResourceLoader.exists(path):
		get_tree().change_scene_to_file(path)

func _draw() -> void:
	if not ready_flag:
		return
	if art_bg:
		draw_texture_rect(art_bg, Rect2(0, 0, 1200, 800), false)

	_draw_scene_custom()

	var sorted_actors = actors.duplicate()
	sorted_actors.sort_custom(func(a, b): return a["y"] < b["y"])
	for p in sorted_actors:
		render_person(p)

	if target != null and is_player_free():
		draw_ellipse_ring(target, 12, 5, Color(0.82, 0.76, 0.6, 0.5), 1.5)

	draw_hud_panel()

func render_person(p: Dictionary) -> void:
	var pos = Vector2(p["x"], p["y"])
	draw_ellipse_shape(pos + Vector2(0, -3), 27, 8, Color(0, 0, 0, 0.33))
	var f: int = int(CYCLE[int(time * 6.0) % 4])

	if p.get("pose") != null:
		var pose_name: String = p["pose"]
		var row := 0
		var start := 0
		var height := 145.0
		var frame_num: int = f
		if pose_name == "cast":
			frame_num = CYCLE[mini(3, int(elapsed * 3.0))]
			height = 165.0
		elif pose_name == "think":
			start = 3
			height = 150.0
			frame_num = int(time * 2.0) % 2
		elif pose_name == "rip":
			row = 1
			frame_num = CYCLE[mini(3, int(elapsed * 2.5))]
			height = 165.0
		elif pose_name == "invite":
			row = 1
			start = 3
			height = 165.0
		elif pose_name == "interrupt":
			row = 2
			start = 3
			height = 145.0
			frame_num = CYCLE[mini(3, int(elapsed * 3.0))]
		draw_sprite_frame(art_actions, row, start + frame_num, pos.x, pos.y, height)
	else:
		var kind: String = p["kind"]
		var sheet = art_wilson if kind == "wilson" else (art_maxwell if kind == "maxwell" else art_gray)
		var row_idx = f if p.get("moving", false) else 1
		var col_idx = int(p.get("dir", 0))
		var h_val = 165.0 if kind == "maxwell" else 145.0
		draw_sprite_frame(sheet, row_idx, col_idx, pos.x, pos.y, h_val)

func draw_sprite_frame(sheet: Dictionary, row: int, col: int, x: float, y: float, height: float) -> void:
	if sheet.is_empty():
		return
	var f = sheet["frames"][row][col]
	var width = height * float(f["w"]) / float(f["h"])
	var src_rect: Rect2 = f["rect"]
	var dest_rect = Rect2(x - width / 2.0, y - height, width, height)
	draw_texture_rect_region(sheet["texture"], dest_rect, src_rect)

func draw_shadow(x: float, y: float, strength: float) -> void:
	var shadow_color = Color(0.03, 0.03, 0.04, 0.85 * strength)
	var pts: PackedVector2Array = [
		Vector2(x - 60, y + 10),
		Vector2(x - 90, y - 215),
		Vector2(x + 60, y + 10)
	]
	draw_colored_polygon(pts, shadow_color)

func draw_ellipse_shape(center: Vector2, rx: float, ry: float, color: Color) -> void:
	var pts: PackedVector2Array = []
	for i in range(20):
		var theta = float(i) / 20.0 * TAU
		pts.append(center + Vector2(cos(theta) * rx, sin(theta) * ry))
	draw_colored_polygon(pts, color)

func draw_ellipse_ring(center: Vector2, rx: float, ry: float, color: Color, width: float) -> void:
	var pts: PackedVector2Array = []
	for i in range(21):
		var theta = float(i % 20) / 20.0 * TAU
		pts.append(center + Vector2(cos(theta) * rx, sin(theta) * ry))
	draw_polyline(pts, color, width)

func draw_hud_panel() -> void:
	# 顶部指示
	draw_rect(Rect2(20, 20, 520, 75), Color(0.08, 0.12, 0.14, 0.88))
	draw_string(ThemeDB.fallback_font, Vector2(35, 48), hud_act + " · " + hud_title + "  [按键 1-6 换幕]", HORIZONTAL_ALIGNMENT_LEFT, -1, 15, Color(0.95, 0.9, 0.75))
	draw_string(ThemeDB.fallback_font, Vector2(35, 75), hud_desc, HORIZONTAL_ALIGNMENT_LEFT, 480, 12, Color(0.85, 0.88, 0.82))

	# 仪表盘
	draw_rect(Rect2(20, 105, 520, 50), Color(0.08, 0.12, 0.14, 0.88))
	var meter_txt = "%s: %d%% (%s)" % [hud_label, int(hud_value * 100), hud_note]
	draw_string(ThemeDB.fallback_font, Vector2(35, 136), meter_txt, HORIZONTAL_ALIGNMENT_LEFT, -1, 13, Color(0.9, 0.82, 0.65))

	# 台词与记录
	if speech_text != "":
		draw_rect(Rect2(350, 700, 520, 40), Color(0.1, 0.15, 0.18, 0.9))
		draw_string(ThemeDB.fallback_font, Vector2(365, 725), speech_text, HORIZONTAL_ALIGNMENT_LEFT, -1, 14, Color.WHITE)
	if record_text != "":
		draw_rect(Rect2(20, 750, 920, 35), Color(0.06, 0.1, 0.12, 0.9))
		draw_string(ThemeDB.fallback_font, Vector2(35, 773), "现场记录: " + record_text, HORIZONTAL_ALIGNMENT_LEFT, -1, 13, Color(0.8, 0.85, 0.8))

## 场景子类重写接口
func _init_scene() -> void:
	pass

func _update_scene_logic(_dt: float) -> void:
	pass

func _draw_scene_custom() -> void:
	pass

func _on_scene_key_down(_code: int) -> void:
	pass
