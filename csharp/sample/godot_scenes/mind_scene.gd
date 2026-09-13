class_name MindScene
extends Node2D

## 对应 game_resource/dist/mind.js
## 原样搬运未发生之事：脑内推演场景逻辑

const MindLogic = preload("res://show_sample/mind_logic.gd")

var state: Dictionary = {}
var rule_pieces: Array[bool] = [false, false, false]
var words: Array = ["", "", ""]
const RULE_LABELS := ["薇洛处于潮湿", "接触火焰言灵", "火转蒸汽 · 不点燃"]
const MEANINGS := {
	"sina": "你",
	"o": "让",
	"seli": "燃起火焰",
	"telo": "变得潮湿",
	"lete": "覆上寒霜"
}
const STATUS_NAMES := {
	"calm": "平静",
	"wet": "潮湿",
	"burning": "燃烧",
	"cold": "寒冷"
}

var book_open := false
var ready_flag := false
var clock := 0.0
var pending = null # Dictionary { "word": String, "elapsed": float } or null
var effect = null  # Dictionary { "type": String, "elapsed": float } or null
var rule_flash := 0.0
var motes: Array[Dictionary] = []

var ground_texture: Texture2D
var wilson_sprite_sheet: Dictionary = {}
var willow_sprite_sheet: Dictionary = {}

# UI 展示文字变量
var subject_thought := "“现实尚未发生，念头可以重来。”"
var trial_number_str := "尚未施放"
var result_title := "现实尚未发生，念头可以重来。"
var result_text := "先试一次火焰，再组装规则，比较水与火相遇后的结果。"
var rule_status_str := "尚未写入"
var trace_info := {"input": "", "rule": "", "output": "", "visible": false}

func _ready() -> void:
	state = MindLogic.create_mind_state()
	motes.clear()
	for i in range(38):
		motes.append({
			"x": fmod(float(i) * 137.31, 1.0),
			"y": fmod(float(i) * 73.71, 1.0),
			"phase": float(i) * 2.17
		})
	load_assets()

func load_assets() -> void:
	var g_img = Image.load_from_file("res://show_sample/assets/mind-ground.png")
	if g_img == null:
		g_img = Image.load_from_file(ProjectSettings.globalize_path("res://show_sample/assets/mind-ground.png"))
	if g_img:
		ground_texture = ImageTexture.create_from_image(g_img)
	
	var wilson_img = Image.load_from_file("res://show_sample/assets/wilson-sprite.png")
	if wilson_img == null:
		wilson_img = Image.load_from_file(ProjectSettings.globalize_path("res://show_sample/assets/wilson-sprite.png"))
	if wilson_img:
		wilson_sprite_sheet = parse_frames(wilson_img, 4, 4)
	
	var willow_img = Image.load_from_file("res://show_sample/assets/willow-mind-sprite.png")
	if willow_img == null:
		willow_img = Image.load_from_file(ProjectSettings.globalize_path("res://show_sample/assets/willow-mind-sprite.png"))
	if willow_img:
		willow_sprite_sheet = parse_frames(willow_img, 4, 1)
	
	ready_flag = (ground_texture != null and not wilson_sprite_sheet.is_empty() and not willow_sprite_sheet.is_empty())

func parse_frames(im: Image, cols: int, rows: int) -> Dictionary:
	var sw := im.get_width() / cols
	var sh := im.get_height() / rows
	var result: Array = []
	
	for row in range(rows):
		var row_arr: Array = []
		for col in range(cols):
			var min_x := sw
			var min_y := sh
			var max_x := 0
			var max_y := 0
			for y in range(3, sh - 3):
				for x in range(3, sw - 3):
					var px = im.get_pixel(col * sw + x, row * sh + y)
					if px.a > (60.0 / 255.0):
						min_x = mini(min_x, x)
						min_y = mini(min_y, y)
						max_x = maxi(max_x, x)
						max_y = maxi(max_y, y)
			var h := float(max_y - min_y)
			row_arr.append({
				"rect": Rect2(col * sw, row * sh, sw, sh),
				"anchorX": float(min_x + max_x) / 2.0,
				"anchorY": float(max_y),
				"height": h
			})
		result.append(row_arr)
	
	return {
		"texture": ImageTexture.create_from_image(im),
		"frames": result
	}

func toggle_rule_piece(idx: int) -> void:
	if pending != null or state.get("ruleActive", false):
		return
	rule_pieces[idx] = not rule_pieces[idx]

func write_rule() -> void:
	if pending != null:
		return
	if not (rule_pieces[0] and rule_pieces[1] and rule_pieces[2]):
		return
	var active: bool = not state.get("ruleActive", false)
	state["ruleActive"] = active
	rule_flash = 1.5 if active else 0.0
	rule_status_str = "已写入 · 等待触发" if active else "尚未写入"
	subject_thought = "“假设已成立。现在，让它接受检验。”" if active else "“暂时拿掉假设，结果会不同吗？”"

func pick_word(word: String) -> void:
	if pending != null:
		return
	if word == "sina":
		words[0] = "sina"
	elif word == "o":
		words[1] = "o"
	else:
		words[2] = word

func clear_words() -> void:
	if pending != null:
		return
	words = ["", "", ""]

func cast() -> void:
	if pending != null or not ready_flag:
		return
	if words[0] == "" or words[1] == "" or words[2] == "":
		return
	pending = {
		"word": words[2],
		"elapsed": 0.0
	}
	subject_thought = "“" + words[0] + " " + words[1] + " " + words[2] + "。”"

func show_result(res: Dictionary) -> void:
	effect = {"type": res.get("effect", ""), "elapsed": 0.0}
	trial_number_str = "推演 %02d" % int(state.get("trials", 0))
	result_title = res.get("title", "")
	result_text = res.get("explanation", "")
	trace_info["input"] = res.get("input", "")
	trace_info["rule"] = res.get("rule", "")
	trace_info["output"] = res.get("output", "")
	trace_info["visible"] = true
	
	var trig = res.get("triggered", false)
	if trig:
		subject_thought = "“改变结果的，是规则。”"
		rule_flash = 2.5
		rule_status_str = "刚刚触发 · 火焰已改写"
	elif res.get("effect") == "water":
		subject_thought = "“前提准备好了，再试一次火焰。”"
	elif res.get("effect") == "fire":
		subject_thought = "“言灵生效了。规则会改变它吗？”"
	else:
		subject_thought = "“这一道言灵，没有碰到那条规则。”"

func restore() -> void:
	state["status"] = "calm"
	effect = null
	rule_flash = 0.0
	subject_thought = "“回到原点，再验证一次。”"
	result_title = "心象已还原"
	result_text = "规则与言灵组合保留，可在相同条件下重新比较。"
	trace_info["visible"] = false

func reset_mind() -> void:
	pending = null
	state = MindLogic.create_mind_state()
	rule_pieces = [false, false, false]
	words = ["", "", ""]
	restore()
	trial_number_str = "尚未施放"
	result_title = "现实尚未发生，念头可以重来。"
	result_text = "先试一次火焰，再组装规则，比较水与火相遇后的结果。"

func _process(delta: float) -> void:
	var dt := minf(delta, 0.05)
	clock += dt
	if rule_flash > 0.0:
		rule_flash -= dt
	if effect != null:
		effect["elapsed"] = float(effect["elapsed"]) + dt
	if pending != null:
		pending["elapsed"] = float(pending["elapsed"]) + dt
		if float(pending["elapsed"]) >= 1.1:
			var w = pending["word"]
			pending = null
			show_result(MindLogic.resolve_utterance(state, w))
	queue_redraw()

func _input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo:
		if event.keycode == KEY_ENTER:
			cast()

func _draw() -> void:
	if not ready_flag:
		return
	
	# 背景铺满
	draw_texture_rect(ground_texture, Rect2(0, 0, 1480, 900), false)
	
	# 梦境漂浮光点
	for m in motes:
		var mx = float(m["x"]) * 1480.0 + sin(clock * 0.17 + float(m["phase"])) * 12.0
		var my = float(m["y"]) * 900.0 + sin(clock * 0.2 + float(m["phase"])) * 9.0
		var alpha = 0.15 + sin(clock * 0.5 + float(m["phase"])) * 0.12
		draw_rect(Rect2(mx, my, 2, 2), Color(0.84, 0.87, 0.71, alpha))
	
	var wilson_pos = Vector2(580, 520)
	var willow_pos = Vector2(860, 500)
	var char_height = 165.0
	
	# 若规则激活，在威尔逊和薇洛之间绘制虚线连线与旋转菱形
	if state.get("ruleActive", false):
		var line_color = Color(0.87, 0.81, 0.55, 0.8) if rule_flash > 0 else Color(0.75, 0.81, 0.63, 0.3)
		draw_line(wilson_pos + Vector2(0, -char_height - 9), willow_pos + Vector2(0, -char_height - 9), line_color, 1.5)
		var center_pt = (wilson_pos + willow_pos) / 2.0 + Vector2(0, -char_height - 30)
		var diamond_pts: PackedVector2Array = [
			center_pt + Vector2(0, -6),
			center_pt + Vector2(6, 0),
			center_pt + Vector2(0, 6),
			center_pt + Vector2(-6, 0)
		]
		draw_colored_polygon(diamond_pts, line_color)
	
	# 薇洛立绘
	var willow_frame_idx: int = 0
	if effect != null and effect["type"] == "steam" and float(effect["elapsed"]) < 3.0:
		willow_frame_idx = 1
	else:
		var st = state.get("status", "calm")
		var st_map := {"calm": 0, "burning": 1, "wet": 2, "cold": 3}
		willow_frame_idx = st_map.get(st, 0)
	
	draw_actor_frame(willow_sprite_sheet, 0, willow_frame_idx, willow_pos, char_height, "薇洛 · 心象")
	draw_actor_frame(wilson_sprite_sheet, 1, 0, wilson_pos, char_height, "威尔逊 · 推演者")
	
	# 状态特效与命中特效
	draw_status_effects(willow_pos, char_height)
	draw_event_effect(willow_pos, char_height)
	
	# 言灵弹道
	if pending != null:
		var t = minf(float(pending["elapsed"]) / 1.1, 1.0)
		var bx = wilson_pos.x + (willow_pos.x - wilson_pos.x) * t
		var by = (wilson_pos.y - char_height * 0.6) + (willow_pos.y - wilson_pos.y) * t - sin(t * PI) * 45.0
		draw_circle(Vector2(bx, by), 12.0, Color(0.96, 0.9, 0.68, 0.7))
		draw_string(ThemeDB.fallback_font, Vector2(bx - 15, by - 15), str(pending["word"]), HORIZONTAL_ALIGNMENT_CENTER, -1, 13, Color(0.96, 0.9, 0.68))
	
	# 绘制 HUD 交互面板
	draw_mind_ui()

func draw_actor_frame(sheet: Dictionary, row: int, col: int, pos: Vector2, height: float, tag_name: String) -> void:
	if sheet.is_empty():
		return
	var f = sheet["frames"][row][col]
	var scale_val = height / float(sheet["frames"][0][0]["height"])
	
	# 阴影
	draw_custom_ellipse(pos, 29, 10, Color(0.01, 0.05, 0.03, 0.42))
	
	var src_rect: Rect2 = f["rect"]
	var dest_pos = pos + Vector2(-float(f["anchorX"]) * scale_val, -float(f["anchorY"]) * scale_val)
	var dest_size = Vector2(src_rect.size.x * scale_val, src_rect.size.y * scale_val)
	draw_texture_rect_region(sheet["texture"], Rect2(dest_pos, dest_size), src_rect)
	
	# 标签
	draw_string(ThemeDB.fallback_font, pos + Vector2(-40, 22), tag_name, HORIZONTAL_ALIGNMENT_CENTER, -1, 12, Color(0.85, 0.87, 0.74))

func draw_status_effects(p: Vector2, height: float) -> void:
	var kind: String = state.get("status", "calm")
	if kind == "burning":
		for i in range(16):
			var age = fmod(clock * 1.1 + float(i) * 0.137, 1.0)
			var fx = sin(float(i) * 2.5 + clock * 0.9) * (24.0 - age * 10.0)
			var fy = -age * height * 0.85
			var c = Color(0.95, 0.59, 0.21, (1.0 - age) * 0.7)
			draw_circle(p + Vector2(fx, fy), 7.0 * (1.0 - age) + 2.0, c)
	elif kind == "wet":
		for i in range(12):
			var wy = fmod(clock * 0.7 + float(i) * 0.103, 1.0) * height - height
			var wx = sin(float(i) * 3.8) * 32.0
			draw_line(p + Vector2(wx, wy), p + Vector2(wx - 2, wy + 7), Color(0.58, 0.83, 0.82, 0.6), 1.5)
	elif kind == "cold":
		for i in range(8):
			var cx = sin(float(i) * 3.1) * 37.0
			var cy = -15.0 - float(i) * height / 10.0
			draw_circle(p + Vector2(cx, cy), 3.0, Color(0.76, 0.9, 0.89, 0.7))

func draw_event_effect(p: Vector2, height: float) -> void:
	if effect == null:
		return
	var t: float = float(effect["elapsed"])
	var eff_type: String = effect["type"]
	if eff_type == "steam" and t < 4.0:
		for i in range(16):
			var age = fmod(t * 0.45 + float(i) * 0.071, 1.0)
			var sx = sin(float(i) * 2.8 + t * 0.5) * (20.0 + age * 40.0)
			var sy = -age * (height + 45.0)
			var alpha = maxf(0.0, 1.0 - t / 4.0) * (1.0 - age) * 0.6
			draw_circle(p + Vector2(sx, sy), 10.0 + age * 24.0, Color(0.85, 0.9, 0.85, alpha))
		if t < 2.8:
			draw_string(ThemeDB.fallback_font, p + Vector2(-45, -height - 30), "蒸汽 · 未点燃", HORIZONTAL_ALIGNMENT_CENTER, -1, 14, Color(0.94, 0.87, 0.69))
	elif t < 1.1:
		var col = Color(0.64, 0.88, 0.87) if eff_type == "water" else (Color(0.83, 0.92, 0.96) if eff_type == "frost" else Color(0.93, 0.76, 0.54))
		col.a = 1.0 - t / 1.1
		draw_arc(p + Vector2(0, -height * 0.4), 20.0 + t * 65.0, 0.0, TAU, 32, col, 2.0)

func draw_custom_ellipse(center: Vector2, rx: float, ry: float, color: Color) -> void:
	var pts: PackedVector2Array = []
	for i in range(24):
		var rad = float(i) / 24.0 * TAU
		pts.append(center + Vector2(cos(rad) * rx, sin(rad) * ry))
	draw_colored_polygon(pts, color)

func draw_mind_ui() -> void:
	# 顶部心象状态条
	draw_rect(Rect2(20, 20, 500, 75), Color(0.08, 0.14, 0.12, 0.9))
	var cur_status_str = STATUS_NAMES.get(state.get("status", "calm"), "平静")
	draw_string(ThemeDB.fallback_font, Vector2(35, 48), "薇洛心象: " + cur_status_str + " | " + trial_number_str, HORIZONTAL_ALIGNMENT_LEFT, -1, 16, Color(0.93, 0.88, 0.75))
	draw_string(ThemeDB.fallback_font, Vector2(35, 75), subject_thought, HORIZONTAL_ALIGNMENT_LEFT, -1, 13, Color(0.8, 0.85, 0.8))
	
	# 推演回响面板
	draw_rect(Rect2(20, 110, 500, 95), Color(0.08, 0.14, 0.12, 0.9))
	draw_string(ThemeDB.fallback_font, Vector2(35, 135), result_title, HORIZONTAL_ALIGNMENT_LEFT, -1, 15, Color(0.95, 0.92, 0.78))
	draw_string(ThemeDB.fallback_font, Vector2(35, 165), result_text, HORIZONTAL_ALIGNMENT_LEFT, 470, 12, Color(0.85, 0.88, 0.82))
	if trace_info["visible"]:
		var tr_str = "输入: %s  →  律则: %s  →  结果: %s" % [trace_info["input"], trace_info["rule"], trace_info["output"]]
		draw_string(ThemeDB.fallback_font, Vector2(35, 192), tr_str, HORIZONTAL_ALIGNMENT_LEFT, -1, 12, Color(0.9, 0.78, 0.5))
	
	# 底部言灵与规则提示
	draw_rect(Rect2(20, 800, 900, 70), Color(0.08, 0.14, 0.12, 0.9))
	var w0 = words[0] if words[0] != "" else "_"
	var w1 = words[1] if words[1] != "" else "_"
	var w2 = words[2] if words[2] != "" else "_"
	var speech_str = "言灵槽位: [ %s ] [ %s ] [ %s ]  (Enter 施放)" % [w0, w1, w2]
	draw_string(ThemeDB.fallback_font, Vector2(35, 830), speech_str, HORIZONTAL_ALIGNMENT_LEFT, -1, 15, Color(0.95, 0.9, 0.75))
	var rule_str = "固定规则: %s (潮湿遇火 → 蒸汽)" % rule_status_str
	draw_string(ThemeDB.fallback_font, Vector2(35, 855), rule_str, HORIZONTAL_ALIGNMENT_LEFT, -1, 13, Color(0.8, 0.85, 0.8))
