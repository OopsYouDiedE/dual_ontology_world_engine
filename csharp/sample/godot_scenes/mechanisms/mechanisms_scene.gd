class_name MechanismsScene
extends Node2D

## 对应 game_resource/dist/mechanisms/app.js
## 原样搬运庄园五幕剧机制演示逻辑

const CHAPTERS := [
	["言灵发动", "ACT I", "举起有效凭证，让言灵短暂拨开认知的遮蔽。事物始终在那里，变化的是你看见的现实。"],
	["脑内推演", "ACT II", "用灰色人偶复现进入房间的动作。一个词让自我开始模糊；及时停止联想，换一种说法。"],
	["日常互动", "ACT III", "恶魔伯爵提出一场普通的邀约。选择回复策略，与麦斯威尔坐下来下一盘棋。"],
	["恶作剧", "ACT IV", "一张不合时宜的规则，让经过的人都绕起了圈。伯爵也未能幸免。"],
	["保护威尔逊", "ACT V", "伏案的侦探逐渐失去对周围的注意。阴影靠近，同伴必须打断尚未完成的认识。"]
]

const CYCLE := [0, 1, 2, 2]
const DIRS := ["南", "西南", "西", "西北", "北", "东北", "东", "东南"]

var ready_flag := false
var scene := 0
var phase := "idle"
var elapsed := 0.0
var time := 0.0
var paused := false
var credential := false
var danger := 0.0
var reveal := 0.0
var rule := false
var actors: Array = []
var player: Dictionary = {}
var companion: Dictionary = {}
var earl: Dictionary = {}
var target = null # Vector2 or null
var keys := {}

var speech_text := ""
var record_text := ""
var hud_label := "现实辨识"
var hud_value := 0.0
var hud_note := ""

var art_bg: Texture2D
var art_wilson: Dictionary = {}
var art_maxwell: Dictionary = {}
var art_gray: Dictionary = {}
var art_actions: Dictionary = {}

func _ready() -> void:
	load_assets()
	select_scene(0)

func create_person(kind: String, x: float, y: float) -> Dictionary:
	return {
		"kind": kind,
		"x": x,
		"y": y,
		"dir": 0,
		"moving": false,
		"pose": null
	}

func load_assets() -> void:
	var base_path := "res://show_sample/mechanisms/assets/"
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
	
	# key_white: 漫水填充外圈白色背景为透明
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

func set_phase(next_phase: String) -> void:
	phase = next_phase
	elapsed = 0.0
	target = null

func select_scene(index: int) -> void:
	scene = index
	phase = "idle"
	elapsed = 0.0
	danger = 0.0
	reveal = 0.0
	credential = false
	rule = false
	target = null
	keys.clear()
	actors.clear()
	
	player = create_person("wilson", 590, 580)
	actors.append(player)
	companion = {}
	earl = {}
	
	if scene == 0:
		companion = create_person("gray", 825, 460)
		actors.append(companion)
		speech_text = ""
		record_text = "书房一切如常。至少，你所见的如此。"
	elif scene == 1:
		companion = create_person("gray", 320, 640)
		actors.append(companion)
		speech_text = ""
		record_text = "复现场景已展开。灰色人偶等待你的规则。"
	elif scene == 2:
		earl = create_person("maxwell", 745, 500)
		earl["pose"] = "invite"
		actors.append(earl)
		phase = "invite"
		speech_text = "麦斯威尔：侦探，有兴趣陪我下一盘棋吗？"
		record_text = "收到邀约：与麦斯威尔下国际象棋。"
	elif scene == 3:
		player["x"] = 410.0
		player["y"] = 620.0
		earl = create_person("maxwell", 985, 450)
		actors.append(earl)
		for i in range(4):
			actors.append(create_person("gray", 545 + i * 75, 520 + (i % 2) * 100))
		speech_text = ""
		record_text = "公告板空着。这里暂时没有人转圈。"
	elif scene == 4:
		player["x"] = 760.0
		player["y"] = 565.0
		companion = create_person("gray", 410, 610)
		actors.append(companion)
		speech_text = ""
		record_text = "同伴在房间另一侧。威尔逊准备开始推理。"
	
	update_hud()

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
	if player.get("pose") != null:
		return false
	return not (phase in ["seating", "chess", "chessMove", "thinking", "interrupting", "recovering"])

func _process(delta: float) -> void:
	if not ready_flag:
		return
	var dt := minf(delta, 0.05)
	if not paused:
		update_logic(dt)
	queue_redraw()

func update_logic(dt: float) -> void:
	time += dt
	elapsed += dt
	for p in actors:
		p["moving"] = false
	
	# 玩家受控移动
	if is_player_free():
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
	
	# 各幕状态推进
	if scene == 0:
		if phase == "casting" and elapsed > 1.2:
			set_phase("revealed")
			player["pose"] = null
			reveal = 7.0
			record_text = "凭证生效。七秒内可看清灰色来客身后的异常投影。"
		if reveal > 0.0:
			reveal = maxf(0.0, reveal - dt)
			if reveal == 0.0:
				set_phase("idle")
				speech_text = "视线重新蒙上薄雾。"
				record_text = "客观事物没有改变，短暂的现实辨识结束。"
	
	elif scene == 1:
		if phase == "danger":
			danger = minf(0.95, danger + dt * 0.12)
		elif phase == "simulate":
			var points = [Vector2(410, 595), Vector2(410, 430), Vector2(650, 430), Vector2(850, 560)]
			var idx: int = mini(3, int(elapsed / 2.3))
			move_person(companion, points[idx].x, points[idx].y, dt, 105.0)
			if elapsed > 10.0:
				set_phase("done")
				record_text = "推演完成。人偶沿替代规则行动，未再触及危险词。"
	
	elif scene == 2:
		if phase == "seating":
			earl["pose"] = null
			var a = move_person(player, 445, 555, dt)
			var b = move_person(earl, 655, 555, dt)
			if a and b:
				set_phase("chess")
				speech_text = "麦斯威尔：请。你执白。"
				record_text = "邀约成立，双方已入座。点击落子观看三帧动作。"
		elif phase == "chessMove" and elapsed > 2.4:
			set_phase("chess")
			speech_text = "麦斯威尔：轮到你了。"
			record_text = "双方各走了一步。棋局继续。"
	
	elif scene == 3:
		if rule:
			var grays: Array = []
			for p in actors:
				if p["kind"] == "gray":
					grays.append(p)
			for i in range(grays.size()):
				var a_angle = elapsed * 1.3 + float(i) * PI / 2.0
				move_person(grays[i], 660.0 + cos(a_angle) * 125.0, 545.0 + sin(a_angle) * 68.0, dt, 160.0)
		if phase == "circling":
			if elapsed > 3.0:
				var a_angle = (elapsed - 3.0) * 1.3
				move_person(earl, 660.0 + cos(a_angle) * 125.0, 545.0 + sin(a_angle) * 68.0, dt, 175.0)
			if elapsed > 4.0 and elapsed < 4.2:
				speech_text = "麦斯威尔：……我为什么也在转？"
			if elapsed > 11.0:
				set_phase("investigating")
				speech_text = "麦斯威尔：等等。是那张纸。"
				record_text = "伯爵察觉来源，走向规则。"
		elif phase == "investigating":
			if move_person(earl, 700, 415, dt, 135.0):
				set_phase("ripping")
				earl["pose"] = "rip"
		elif phase == "ripping":
			if elapsed > 0.8:
				rule = false
			if elapsed > 1.8:
				set_phase("done")
				speech_text = "麦斯威尔：这是什么垃圾规则！给我适可而止！"
				record_text = "规则被撕毁并废除。所有人停止绕圈。"
	
	elif scene == 4:
		if phase == "thinking":
			danger = minf(0.94, danger + dt * 0.095)
		elif phase == "interrupting":
			danger = minf(0.94, danger + dt * 0.02)
			if move_person(companion, 865, 565, dt, 190.0):
				set_phase("recovering")
				companion["pose"] = "interrupt"
				record_text = "同伴伸手打断，尚未成立的连接停止延伸。"
		elif phase == "recovering":
			danger = maxf(0.0, danger - dt * 0.65)
			if elapsed > 1.7:
				set_phase("done")
				player["pose"] = null
				companion["pose"] = null
				danger = 0.0
				speech_text = "威尔逊：……我听见了。谢谢。"
				record_text = "威尔逊恢复对周围的注意。危险认识没有成立。"
	
	update_hud()

func update_hud() -> void:
	if scene == 0:
		hud_label = "现实辨识"
		hud_value = reveal / 7.0
		hud_note = "凭证有效 · 可以发动言灵" if credential else "需要有效凭证才能拨开认知遮蔽"
	elif scene == 1:
		hud_label = "自我失稳"
		hud_value = danger
		hud_note = "立即停止联想。危险来自词汇本身。" if phase == "danger" else ("替代词：来访者 · 推演不触及异常名称" if rule else "复现场景 · 只操纵灰色人偶")
	elif scene == 2:
		hud_label = "邀约状态"
		hud_value = 1.0 if (phase in ["chess", "chessMove"]) else 0.0
		hud_note = "双方已同意 · 正在对弈" if (phase in ["chess", "chessMove"]) else "等待玩家选择回复策略"
	elif scene == 3:
		hud_label = "规则效力"
		hud_value = 1.0 if rule else 0.0
		hud_note = "规则正在影响房间内的来客" if rule else "规则尚未生效或已被废除"
	elif scene == 4:
		hud_label = "危险认识"
		hud_value = danger
		hud_note = "连接已中断 · 注意恢复" if phase == "done" else "只呈现伏案、阴影和打断，不展示线索连接"

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
	elif event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		var mx = clampf(event.position.x, 270.0, 1000.0)
		var my = clampf(event.position.y, 370.0, 705.0)
		target = Vector2(mx, my)

func _draw() -> void:
	if not ready_flag:
		return
	
	# 背景舞台
	if art_bg:
		draw_texture_rect(art_bg, Rect2(0, 0, 1200, 800), false)
	
	if scene == 1:
		draw_rect(Rect2(0, 0, 1200, 800), Color(0.08, 0.18, 0.2, 0.66))
		var path_pts: PackedVector2Array = [
			Vector2(320, 640), Vector2(410, 595), Vector2(410, 430), Vector2(650, 430), Vector2(850, 560)
		]
		draw_polyline(path_pts, Color(0.76, 0.83, 0.75, 0.3), 2.0)
	
	if reveal > 0.0:
		draw_rect(Rect2(0, 0, 1200, 800), Color(0.47, 0.55, 0.52, 0.13))
		if not companion.is_empty():
			draw_shadow(companion["x"], companion["y"], 0.9)
		draw_ellipse_ring(Vector2(player["x"], player["y"]), 95, 38, Color(0.82, 0.76, 0.6, 0.5), 2.0)
	
	if scene == 3 and rule:
		draw_rect(Rect2(673, 347, 34, 46), Color(0.82, 0.76, 0.61))
	
	if scene == 4 and danger > 0.0:
		draw_shadow(player["x"] - 82, player["y"], danger * 0.9)
		draw_shadow(player["x"] + 65, player["y"] + 15, danger)
	
	# 下棋动作或常规人物渲染
	if scene == 2 and (phase in ["chess", "chessMove"]):
		var f_idx = 0 if phase == "chess" else CYCLE[mini(3, int(elapsed / 0.6))]
		draw_sprite_frame(art_actions, 2, f_idx, 550, 575, 180)
	else:
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
			frame_num = 2 if phase == "recovering" else (int(time * 2.0) % 2)
		elif pose_name == "rip":
			row = 1
			frame_num = 2 if phase == "done" else CYCLE[mini(3, int(elapsed * 2.5))]
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
	# 顶部章节指示
	var c = CHAPTERS[scene]
	draw_rect(Rect2(20, 20, 480, 75), Color(0.08, 0.12, 0.14, 0.88))
	draw_string(ThemeDB.fallback_font, Vector2(35, 48), c[1] + " · " + c[0], HORIZONTAL_ALIGNMENT_LEFT, -1, 16, Color(0.95, 0.9, 0.75))
	draw_string(ThemeDB.fallback_font, Vector2(35, 75), c[2], HORIZONTAL_ALIGNMENT_LEFT, 450, 12, Color(0.85, 0.88, 0.82))
	
	# 仪表盘
	draw_rect(Rect2(20, 105, 480, 50), Color(0.08, 0.12, 0.14, 0.88))
	var meter_txt = "%s: %d%% (%s)" % [hud_label, int(hud_value * 100), hud_note]
	draw_string(ThemeDB.fallback_font, Vector2(35, 136), meter_txt, HORIZONTAL_ALIGNMENT_LEFT, -1, 13, Color(0.9, 0.82, 0.65))
	
	# 对话台词与动作记录
	if speech_text != "":
		draw_rect(Rect2(350, 700, 500, 40), Color(0.1, 0.15, 0.18, 0.9))
		draw_string(ThemeDB.fallback_font, Vector2(365, 725), speech_text, HORIZONTAL_ALIGNMENT_LEFT, -1, 14, Color.WHITE)
	if record_text != "":
		draw_rect(Rect2(20, 750, 900, 35), Color(0.06, 0.1, 0.12, 0.9))
		draw_string(ThemeDB.fallback_font, Vector2(35, 773), "记录: " + record_text, HORIZONTAL_ALIGNMENT_LEFT, -1, 13, Color(0.8, 0.85, 0.8))
