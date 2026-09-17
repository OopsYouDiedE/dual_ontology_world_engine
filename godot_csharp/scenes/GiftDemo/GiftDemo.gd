extends "res://ShareAsset/scripts/SceneBase.gd"

# 演示调度属于游戏层；玩家等待时间不计入九秒录制节奏。
var phase := "ready"
var clock := 0.0
var auto_demo := false
var capture_dir := ""
var captured := false
var font := SystemFont.new()
var action: Button
var caption: Label
var detail: Label
var gift_owner := "wilson"
var coins := 0

func _ready() -> void:
	font.font_names = PackedStringArray(["Microsoft YaHei", "Noto Sans CJK SC"])
	art_bg = load("res://ShareAsset/assets/environment/research_hall/reference/research_hall_approved.png")
	art_wilson = parse_atlas((load("res://scenes/GiftDemo/assets/wilson-walk.png") as Texture2D).get_image(), 8, 3, false)
	var maxwell_image := (load("res://scenes/GiftDemo/assets/maxwell-walk.png") as Texture2D).get_image()
	maxwell_image.convert(Image.FORMAT_RGBA8)
	art_maxwell = parse_atlas(maxwell_image, 8, 3, true)
	ready_flag = true
	for arg in OS.get_cmdline_user_args():
		if arg == "--gift-demo": auto_demo = true
		if arg.begins_with("--gift-capture="): capture_dir = arg.trim_prefix("--gift-capture=")
	var title := make_label(Vector2(68, 38), Vector2(1344, 48), 28)
	title.text = "精心挑选的礼物"
	var subtitle := make_label(Vector2(68, 84), Vector2(1344, 36), 17)
	subtitle.text = "公共研究厅 · 日常交互"
	caption = make_label(Vector2(250, 164), Vector2(980, 100), 34)
	caption.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	detail = make_label(Vector2(250, 694), Vector2(980, 42), 20)
	detail.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	action = Button.new()
	action.position = Vector2(450, 754)
	action.size = Vector2(580, 64)
	action.add_theme_font_override("font", font)
	action.add_theme_font_size_override("font_size", 24)
	var style := StyleBoxFlat.new()
	style.bg_color = Color("30281f")
	style.border_color = Color("bb9660")
	style.set_border_width_all(2)
	style.set_corner_radius_all(5)
	action.add_theme_stylebox_override("normal", style)
	var hover := style.duplicate()
	hover.bg_color = Color("59442b")
	action.add_theme_stylebox_override("hover", hover)
	action.add_theme_stylebox_override("focus", hover)
	action.pressed.connect(advance)
	add_child(action)
	var help := make_label(Vector2(250, 840), Vector2(980, 30), 16)
	help.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	help.text = "空格 / 点击：互动     R：重来     D：九秒演示"
	reset()
	if auto_demo: advance()

func make_label(pos: Vector2, extent: Vector2, size_px: int) -> Label:
	var label := Label.new()
	label.position = pos
	label.size = extent
	label.add_theme_font_override("font", font)
	label.add_theme_font_size_override("font_size", size_px)
	label.add_theme_color_override("font_color", Color("f4e3bd"))
	label.add_theme_color_override("font_shadow_color", Color.BLACK)
	label.add_theme_constant_override("shadow_offset_x", 2)
	label.add_theme_constant_override("shadow_offset_y", 3)
	add_child(label)
	return label

func reset() -> void:
	phase = "ready"
	clock = 0
	coins = 0
	gift_owner = "wilson"
	captured = false
	caption.text = "送他一个，长得像他的棋子。"
	detail.text = "礼物：麦斯威尔模样的兵 × 1"
	action.text = "送给麦斯威尔"
	action.show()

func advance() -> void:
	if phase == "ready":
		phase = "give"
		clock = 0
		caption.text = "麦斯威尔：雕工勉强过得去。"
		detail.text = "他接过棋子，翻看底座。"
		action.hide()
	elif phase == "choice":
		phase = "reply"
		clock = 0
		caption.text = "威尔逊：整盒里面这个最便宜。"
		detail.text = ""
		action.hide()
	elif phase == "end": reset()

func _input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo:
		if event.keycode == KEY_SPACE:
			advance()
			get_viewport().set_input_as_handled()
		elif event.keycode == KEY_R:
			auto_demo = false
			reset()
		elif event.keycode == KEY_D:
			auto_demo = true
			reset()
			advance()

func _process(delta: float) -> void:
	clock += delta
	time += delta
	if phase == "give" and clock >= 1.4:
		phase = "choice"
		clock = 0
		caption.text = "麦斯威尔：为什么我是兵？"
		detail.text = "底座上，清清楚楚刻着一个「兵」。"
		action.text = "“整盒里面这个最便宜。”"
		action.show()
	elif phase == "choice" and auto_demo and clock >= 2.0: advance()
	elif phase == "reply" and clock >= 1.6:
		phase = "retort"
		clock = 0
		caption.text = "麦斯威尔：很好。\n你的谢礼也按这个预算。"
		detail.text = "他收起棋子，郑重推来一枚硬币。"
	elif phase == "retort" and clock >= 2.5:
		phase = "end"
		clock = 0
		gift_owner = "maxwell"
		coins = 1
		detail.text = "礼物已收下   ·   获得硬币 × 1   ·   他记住了你的“慷慨”"
		action.text = "再送一次"
		action.show()
	if phase == "end" and clock >= 1.5 and capture_dir != "" and not captured:
		captured = true
		capture()
	queue_redraw()

func capture() -> void:
	await RenderingServer.frame_post_draw
	DirAccess.make_dir_recursive_absolute(capture_dir)
	var error := get_viewport().get_texture().get_image().save_png(capture_dir.path_join("gift-demo.png"))
	print("GiftDemo: owner=", gift_owner, ", coins=", coins, ", capture=", error)
	get_tree().quit(int(error))

func _draw() -> void:
	if not ready_flag: return
	draw_texture_rect(art_bg, Rect2(0, -75, 1480, 986), false)
	draw_rect(Rect2(0, 0, 1480, 900), Color(0.04, 0.025, 0.02, 0.22))
	draw_rect(Rect2(0, 0, 1480, 125), Color(0.06, 0.045, 0.035, 0.85))
	draw_rect(Rect2(0, 677, 1480, 223), Color(0.06, 0.045, 0.035, 0.96))
	draw_ellipse_shape(Vector2(575, 620), 75, 17, Color(0, 0, 0, 0.35))
	draw_ellipse_shape(Vector2(935, 620), 75, 17, Color(0, 0, 0, 0.35))
	draw_sprite_frame(art_wilson, 1, 6, 575, 620, 290)
	draw_sprite_frame(art_maxwell, 1, 2, 935, 620, 330)
	var gift_pos := Vector2(647, 490)
	if phase == "give": gift_pos = gift_pos.lerp(Vector2(855, 475), smoothstep(0, 1, minf(clock / 0.65, 1)))
	elif phase != "ready": gift_pos = Vector2(855, 475)
	if phase == "retort": gift_pos = gift_pos.lerp(Vector2(953, 520), minf(clock * 2, 1))
	if phase != "end" and not (phase == "retort" and clock > 0.5):
		draw_set_transform(gift_pos, sin(clock * 5) * 0.16 if phase == "give" else 0)
		draw_circle(Vector2(0, -20), 33, Color("d9bb80"))
		draw_sprite_frame(art_maxwell, 1, 0, 0, 8, 75)
		draw_style_box(pawn_base(), Rect2(-26, 4, 52, 16))
		draw_set_transform(Vector2.ZERO)
	if phase == "choice":
		draw_circle(Vector2(740, 407), 51, Color("e0c492"))
		draw_arc(Vector2(740, 407), 46, 0, TAU, 48, Color("493328"), 3, true)
		draw_string(font, Vector2(719, 423), "兵", HORIZONTAL_ALIGNMENT_LEFT, -1, 43, Color("35271f"))
	if phase in ["retort", "end"]:
		var progress := minf(clock / 0.75, 1) if phase == "retort" else 1.0
		var coin_pos := Vector2(867, 557).lerp(Vector2(647, 557), smoothstep(0, 1, progress))
		draw_circle(coin_pos, 16, Color("d8ad4e"))
		draw_arc(coin_pos, 12, 0, TAU, 32, Color("67461e"), 2, true)
		draw_string(font, coin_pos + Vector2(-5, 7), "1", HORIZONTAL_ALIGNMENT_LEFT, -1, 20, Color("67461e"))

func pawn_base() -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = Color("ab8758")
	style.border_color = Color("33231e")
	style.set_border_width_all(2)
	return style
