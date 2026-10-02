extends Control
## Visible material details transcribed from red_token.dialogue OBS_E05–E16.
## This display owns no evidence, trial state, actions, or historical conclusions.

const DETAILS: Array[String] = ["E05", "E06", "E07", "E08", "E09", "E09.deep", "E09.completion", "E13", "E14", "E15", "E16"]
const INK := Color("382f25")
const FAINT := Color("766d5e")
const PAPER := Color("eee4ce")
const EDGE := Color("988269")
const WOOD := Color("b28b58")
const METAL := Color("9b9d91")
const ASH := Color("615f58")
const BLUE := Color("397b91")
const RED := Color("a34b3c")
const WATER := Color("98b8bd")

var evidence_id := ""
var _labels: Array[Label] = []
var _label_rects: Array[Rect2] = []
var _view_height := 380.0

static func has_detail(id: String) -> bool:
	return id in DETAILS

func setup(id: String) -> void:
	evidence_id = id
	for label in _labels:
		remove_child(label)
		label.queue_free()
	_labels.clear()
	_label_rects.clear()
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_view_height = 420.0 if id in ["E09", "E09.deep", "E13", "E14", "E15"] else 370.0
	if id == "E09.deep": _view_height = 500.0
	custom_minimum_size = Vector2(600, _view_height)
	_build_labels()
	_layout_labels()
	queue_redraw()

func _ready() -> void:
	resized.connect(_layout_labels)
	if evidence_id.is_empty(): setup("")

func _notification(what: int) -> void:
	if what == NOTIFICATION_THEME_CHANGED and is_inside_tree():
		for label in _labels:
			label.add_theme_font_size_override("font_size", maxi(18, get_theme_font_size("font_size", "Label")))

func _label(text: String, rect: Rect2, color := INK) -> void:
	var label := Label.new()
	# Set width before text shaping so autowrap never measures a zero-width label.
	label.size = rect.size
	label.text = text
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	label.add_theme_color_override("font_color", color)
	label.add_theme_font_size_override("font_size", maxi(18, get_theme_font_size("font_size", "Label")))
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	add_child(label)
	_labels.append(label)
	_label_rects.append(rect)

func _layout_labels() -> void:
	var ratio := maxf(size.x, 600.0) / 600.0
	for i in _labels.size():
		var rect := _label_rects[i]
		_labels[i].position = Vector2(rect.position.x * ratio, rect.position.y)
		_labels[i].autowrap_mode = TextServer.AUTOWRAP_OFF
		_labels[i].size = Vector2(rect.size.x * ratio, rect.size.y)
		_labels[i].autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		_labels[i].set_deferred("size", Vector2(rect.size.x * ratio, rect.size.y))
	queue_redraw()

func _build_labels() -> void:
	var title := ""
	match evidence_id:
		"E05": title = "案脚与封存楔 · 接触面局部"
		"E06": title = "门槛 · 窄缝与灰带"
		"E07": title = "五号房纸包 · 内外两面"
		"E08": title = "粗布与布夹 · 折边和自由面"
		"E09", "E09.deep": title = "受流盆与进流盒 · 两处独立进水"
		"E09.completion": title = "盆侧当班存根 · 两联签记"
		"E13": title = "归还匣 · 原物记录与独立试件"
		"E14": title = "收班夹 · 白片与本人清点签条"
		"E15": title = "前抽屉与值守椅 · 晨间局部"
		"E16": title = "院务移物单 · 现物核对字段"
	_label(title, Rect2(18, 8, 564, 32))
	_label("现场检视 · 局部记录", Rect2(18, 42, 564, 26), FAINT)
	match evidence_id:
		"E05":
			_label("案脚断口", Rect2(30, 76, 245, 28))
			_label("封存木楔", Rect2(320, 76, 250, 28))
			_label("碎石片替垫", Rect2(35, 272, 235, 28))
			_label("新鲜木纤维 / 斜接触面", Rect2(305, 272, 275, 28))
			_label("断口纤维与接触面可比；楔斜面方向可与门底浅色区比对。", Rect2(18, 316, 564, 42))
		"E06":
			_label("生活翼侧 · 门扇下缘", Rect2(30, 77, 540, 28))
			_label("两侧沉灰", Rect2(30, 260, 165, 28))
			_label("浅色楔形遮蔽区", Rect2(200, 260, 260, 28))
			_label("窄缝", Rect2(485, 260, 95, 28))
			_label("配重牵门合回；门底仅有窄缝。内销在本侧，外闩由检视孔可见。", Rect2(18, 307, 564, 52))
		"E07":
			_label("包纸展开 · 内侧", Rect2(28, 76, 250, 28))
			_label("包纸外侧", Rect2(335, 76, 235, 28))
			_label("灰层转印 / 中央折线", Rect2(25, 268, 280, 28))
			_label("外侧干净 / 撕口", Rect2(327, 268, 250, 28))
			_label("内印贴合封存楔的斜面；撕口与五号房抽屉空白纸相合。", Rect2(18, 313, 564, 44))
		"E08":
			_label("原粗布 · 布夹仍在", Rect2(30, 77, 340, 28))
			_label("内折 / 夹持压痕", Rect2(355, 118, 215, 52))
			_label("浸润界的细灰", Rect2(355, 183, 215, 45))
			_label("自由面地板粗纤维", Rect2(330, 246, 250, 44))
			_label("夹边与自由面的痕迹分别保留。安全洗布对照另用试件，不改原布。", Rect2(18, 311, 564, 48))
		"E09", "E09.deep":
			_label("排灰支路", Rect2(24, 77, 245, 28))
			_label("厨房清水 · 另一入口", Rect2(334, 77, 245, 28))
			_label("上游当班捕集片", Rect2(178, 116, 190, 50))
			_label("自由跌水空隙", Rect2(186, 193, 200, 30))
			_label("盆壁灰黑水位圈", Rect2(24, 335, 272, 28))
			_label("清水直接入盆", Rect2(332, 335, 250, 28))
			if evidence_id == "E09.deep":
				_label("检验口取出 · 捕集片局部", Rect2(228, 368, 350, 31))
				_label("网面附新灰\n更换号与盆侧存根一致", Rect2(228, 404, 350, 56))
				_label("排灰水经过捕集片后越过落水空隙；厨房清水由另一入口入盆。", Rect2(18, 465, 564, 30))
			else:
				_label("两入口与盆面分开；捕集片细节须由安全检验口核验。", Rect2(18, 376, 564, 35))
		"E09.completion":
			_label("当班存根 · 第一联", Rect2(36, 85, 248, 28))
			_label("出水已转清", Rect2(38, 128, 245, 36))
			_label("签：沈鼎", Rect2(38, 208, 245, 32))
			_label("当班存根 · 第二联", Rect2(328, 85, 250, 28))
			_label("收到主厨确认\n已停流", Rect2(330, 126, 240, 66))
			_label("签：霍铎", Rect2(330, 208, 240, 32))
			_label("两联当班号一致\n更换号与上游捕集片一致", Rect2(18, 281, 564, 65))
		"E13":
			_label("原匣 · 单向双槽", Rect2(25, 76, 275, 28))
			_label("筹槽", Rect2(39, 111, 92, 28))
			_label("柄槽", Rect2(166, 111, 92, 28))
			_label("当班纸带 · 缺角接收印", Rect2(24, 269, 295, 46))
			_label("另色试片 / 独立试槽", Rect2(322, 76, 258, 40), BLUE)
			_label("各槽有对应接收件", Rect2(322, 233, 258, 28), BLUE)
			_label("白片出口", Rect2(373, 293, 202, 28), BLUE)
			_label("双槽接收件均到位才吐白片；筹槽接收印独立保留。", Rect2(18, 373, 564, 37))
		"E14":
			_label("白片与当班纸带\n接收号一致", Rect2(29, 94, 245, 56))
			_label("四张本人清点签条", Rect2(305, 76, 278, 28))
			_label("侦探 · 五号房", Rect2(312, 119, 266, 30))
			_label("沈鼎 · 房号已登记", Rect2(312, 181, 266, 30))
			_label("霍铎 · 房号已登记", Rect2(312, 243, 266, 30))
			_label("苏泠 · 房号已登记", Rect2(312, 305, 266, 30))
			_label("签条列姓名房号；清点后返房。未载睡眠或封存时刻。", Rect2(18, 371, 564, 41))
		"E15":
			_label("原前抽屉 · 封断 / 灰面", Rect2(23, 76, 302, 30))
			_label("已断的一次性铅封", Rect2(24, 115, 283, 28))
			_label("灰中半手印", Rect2(27, 271, 255, 28))
			_label("拉手新擦痕", Rect2(29, 310, 255, 28))
			_label("值守椅 · 缺角红筹", Rect2(327, 76, 253, 30))
			_label("筹面覆细灰", Rect2(341, 316, 240, 28))
			_label("半手印比四人都窄，残缺形状不能直接标出身份。", Rect2(18, 373, 564, 37))
		"E16":
			_label("院务移物单", Rect2(43, 85, 514, 32))
			_label("登记：四人返房后启前屉，取红筹，置值守椅。", Rect2(43, 134, 514, 53))
			_label("当班号与纸带一致\n匣号与现匣一致\n封条号与断封一致", Rect2(43, 204, 514, 82))
			_label("签认：接待者", Rect2(43, 307, 514, 35))

func _draw() -> void:
	if not has_detail(evidence_id): return
	draw_set_transform(Vector2.ZERO, 0, Vector2(maxf(size.x, 600.0) / 600.0, 1))
	draw_rect(Rect2(0, 0, 600, _view_height), Color("e5ddcc"))
	draw_rect(Rect2(1, 1, 598, _view_height - 2), EDGE, false, 1)
	draw_line(Vector2(18, 71), Vector2(582, 71), EDGE, 1)
	match evidence_id:
		"E05": _draw_foot()
		"E06": _draw_threshold()
		"E07": _draw_paper()
		"E08": _draw_cloth()
		"E09", "E09.deep": _draw_inlets(evidence_id == "E09.deep")
		"E09.completion":
			_sheet(Rect2(25, 78, 258, 186))
			_sheet(Rect2(317, 78, 258, 186))
			for x in [38, 330]:
				draw_line(Vector2(x, 197), Vector2(x + 227, 197), EDGE, 1)
				draw_line(Vector2(x, 243), Vector2(x + 227, 243), EDGE, 1)
		"E13": _draw_receiver()
		"E14": _draw_signatures()
		"E15": _draw_drawer()
		"E16": _sheet(Rect2(28, 78, 544, 270))
	draw_set_transform(Vector2.ZERO)

func _sheet(rect: Rect2) -> void:
	draw_rect(rect, PAPER)
	draw_rect(rect, EDGE, false, 1)

func _poly(points: Array[Vector2], color: Color, outlined := true) -> void:
	var path := PackedVector2Array(points)
	draw_colored_polygon(path, color)
	if outlined:
		path.append(path[0])
		draw_polyline(path, INK, 1.4, true)

func _leader(a: Vector2, b: Vector2, color := FAINT) -> void:
	draw_circle(a, 2.5, color)
	draw_line(a, b, color, 1.2, true)

func _arrow(a: Vector2, b: Vector2, color := BLUE) -> void:
	draw_line(a, b, color, 2, true)
	var direction := (b - a).normalized()
	var side := direction.orthogonal()
	draw_line(b, b - direction * 9 + side * 5, color, 2, true)
	draw_line(b, b - direction * 9 - side * 5, color, 2, true)

func _wedge(origin: Vector2, scale_factor := 1.0, gray := false) -> void:
	_poly([origin, origin + Vector2(153, -62) * scale_factor, origin + Vector2(153, 31) * scale_factor, origin + Vector2(0, 31) * scale_factor], WOOD)
	for n in 6:
		var offset := Vector2(17 + n * 19, 9 + (n % 2) * 4) * scale_factor
		draw_line(origin + offset, origin + offset + Vector2(25, -6) * scale_factor, Color("826442"), 1.3)
	if gray:
		for n in 40:
			var x := float(13 + (n * 29) % 131)
			var y := -x * 0.39 + float((n * 11) % 17)
			draw_circle(origin + Vector2(x, y) * scale_factor, (1.0 + (n % 3) * 0.65) * scale_factor, ASH)

func _draw_foot() -> void:
	draw_rect(Rect2(75, 112, 58, 121), WOOD)
	draw_rect(Rect2(62, 111, 98, 17), Color("896a46"))
	_poly([Vector2(75, 228), Vector2(89, 239), Vector2(101, 229), Vector2(110, 242), Vector2(133, 235), Vector2(133, 216), Vector2(75, 215)], WOOD)
	for n in 7:
		draw_line(Vector2(78 + n * 8, 225), Vector2(81 + n * 8, 238 + n % 4), Color("ddcba4"), 2)
	_poly([Vector2(68, 247), Vector2(91, 239), Vector2(135, 242), Vector2(151, 254), Vector2(61, 254)], Color("8b8b80"))
	_wedge(Vector2(350, 199), 1.15)
	_leader(Vector2(122, 239), Vector2(231, 161))
	_leader(Vector2(350, 209), Vector2(324, 253))
	_leader(Vector2(422, 169), Vector2(552, 218))

func _draw_threshold() -> void:
	draw_rect(Rect2(30, 111, 540, 107), Color("777a71"))
	for x in range(41, 564, 23): draw_line(Vector2(x, 115), Vector2(x + 2, 205), Color("656960"), 1)
	draw_rect(Rect2(30, 214, 540, 9), INK)
	draw_rect(Rect2(30, 233, 540, 20), Color("b9b3a4"))
	# A contact silhouette with clean center, bounded by separate ash accumulations.
	_poly([Vector2(222, 241), Vector2(358, 224), Vector2(381, 238), Vector2(240, 252)], Color("ded4b9"))
	for n in 125:
		var x := float(38 + (n * 43) % 525)
		var y := float(235 + (n * 7) % 17)
		if x > 213 and x < 390: continue
		draw_circle(Vector2(x, y), 1.0 + (n % 4) * 0.5, ASH)
	_leader(Vector2(160, 242), Vector2(111, 261))
	_leader(Vector2(297, 242), Vector2(310, 261))
	_leader(Vector2(548, 225), Vector2(533, 262))

func _draw_paper() -> void:
	_poly([Vector2(27, 114), Vector2(271, 114), Vector2(267, 124), Vector2(274, 132), Vector2(269, 145), Vector2(276, 160), Vector2(270, 246), Vector2(27, 246)], PAPER)
	draw_line(Vector2(147, 115), Vector2(147, 245), EDGE, 1.2)
	_poly([Vector2(55, 221), Vector2(223, 145), Vector2(238, 220), Vector2(76, 232)], Color("afa897"), false)
	# Transfer has a broad contact footprint; individual grains are not duplicates of the wedge.
	for n in 78:
		var x := float(60 + (n * 31) % 167)
		var low := 228.0
		var high := 221.0 - (x - 55) * 0.44
		var y := lerpf(high, low, float((n * 7) % 23) / 23.0)
		draw_circle(Vector2(x, y), 1.0 + n % 3, Color("77746a"))
	_poly([Vector2(333, 114), Vector2(572, 114), Vector2(565, 130), Vector2(574, 146), Vector2(568, 160), Vector2(573, 175), Vector2(568, 246), Vector2(333, 246)], PAPER)
	draw_line(Vector2(450, 115), Vector2(450, 245), EDGE, 1.2)
	_leader(Vector2(144, 195), Vector2(137, 270))
	_leader(Vector2(568, 156), Vector2(558, 269))

func _draw_cloth() -> void:
	_poly([Vector2(35, 117), Vector2(282, 128), Vector2(303, 284), Vector2(49, 278)], Color("c4b793"))
	for n in 18:
		draw_line(Vector2(42 + n * 13, 128), Vector2(56 + n * 13, 274), Color("9f947b"), 1)
	for n in 12:
		draw_line(Vector2(41, 130 + n * 12), Vector2(286, 139 + n * 12), Color("ded3b5"), 1)
	_poly([Vector2(35, 117), Vector2(282, 128), Vector2(283, 164), Vector2(38, 155)], Color("aa9e81"))
	draw_line(Vector2(40, 163), Vector2(285, 172), ASH, 2)
	for n in 48:
		draw_circle(Vector2(48 + (n * 19) % 226, 144 + (n * 7) % 18), 1.0 + (n % 3) * 0.45, ASH)
	draw_rect(Rect2(160, 109, 55, 55), Color("987e4d"))
	draw_rect(Rect2(169, 118, 37, 31), Color("b5a47a"), false, 3)
	draw_circle(Vector2(187, 135), 5, Color("706446"))
	for n in 12:
		var p := Vector2(64 + (n * 37) % 219, 200 + (n * 17) % 62)
		draw_polyline(PackedVector2Array([p, p + Vector2(13, -4), p + Vector2(21, 1)]), Color("776b55"), 2, true)
	_leader(Vector2(208, 135), Vector2(350, 141))
	_leader(Vector2(282, 171), Vector2(350, 205))
	_leader(Vector2(232, 241), Vector2(327, 263))

func _draw_inlets(deep: bool) -> void:
	# Diagram shows permanent pipe geometry, not flowing water or a trial result.
	draw_rect(Rect2(71, 108, 37, 100), METAL)
	draw_rect(Rect2(66, 108, 47, 12), EDGE)
	draw_rect(Rect2(55, 146, 69, 37), Color("c0b69c"))
	draw_rect(Rect2(65, 155, 48, 18), Color("514f48"))
	if deep:
		for n in 20: draw_circle(Vector2(69 + (n * 11) % 40, 158 + (n * 7) % 13), 1.4, Color("bbb2a0"))
	else:
		for x in range(70, 111, 9): draw_line(Vector2(x, 154), Vector2(x, 176), EDGE, 1)
	_leader(Vector2(123, 164), Vector2(175, 143))
	draw_rect(Rect2(474, 109, 33, 91), METAL)
	draw_rect(Rect2(469, 108, 43, 12), EDGE)
	# Dashed open air paths end above the basin, without a connected return pipe.
	for y in [215, 231]:
		draw_line(Vector2(89, y), Vector2(89, y + 7), WATER, 3)
		draw_line(Vector2(490, y), Vector2(490, y + 7), WATER, 3)
	_leader(Vector2(110, 216), Vector2(183, 209))
	_poly([Vector2(41, 247), Vector2(553, 247), Vector2(516, 321), Vector2(76, 321)], Color("a6aaa0"))
	_poly([Vector2(63, 257), Vector2(532, 257), Vector2(509, 305), Vector2(89, 305)], Color("c3c5b5"))
	draw_polyline(PackedVector2Array([Vector2(74, 280), Vector2(92, 300), Vector2(507, 300), Vector2(524, 280)]), ASH, 5, true)
	for n in 37: draw_circle(Vector2(95 + (n * 31) % 408, 295 + n % 5), 1.4, ASH)
	draw_circle(Vector2(298, 309), 9, Color("60655e"))
	_leader(Vector2(143, 300), Vector2(143, 334))
	if deep:
		# A separate extracted section visually opens the inspection housing.
		draw_line(Vector2(126, 166), Vector2(149, 184), EDGE, 1)
		draw_rect(Rect2(147, 172, 26, 14), METAL)
		for n in 8: draw_circle(Vector2(150 + (n * 7) % 20, 176 + n % 7), 1.5, ASH)
		# Enlarged observation of the removed plate, apart from the pipe section.
		draw_rect(Rect2(43, 368, 158, 78), METAL)
		draw_rect(Rect2(52, 377, 140, 60), Color("dadbc9"))
		for x in range(57, 190, 12): draw_line(Vector2(x, 378), Vector2(x, 436), EDGE, 1)
		for y in range(382, 435, 11): draw_line(Vector2(53, y), Vector2(191, y), EDGE, 1)
		for n in 61:
			var p := Vector2(61 + fposmod(sin(float(n) * 12.9898 + 2.1) * 43758.5453, 1.0) * 119, 384 + fposmod(sin(float(n) * 78.233 + 1.7) * 32521.654, 1.0) * 44)
			draw_circle(p, 1.4 + n % 4, ASH)

func _token(origin: Vector2, scale_factor: float, color: Color) -> void:
	_poly([origin, origin + Vector2(35, 0) * scale_factor, origin + Vector2(35, 10) * scale_factor, origin + Vector2(48, 10) * scale_factor, origin + Vector2(48, 42) * scale_factor, origin + Vector2(0, 42) * scale_factor], color)
	draw_line(origin + Vector2(8, 13) * scale_factor, origin + Vector2(27, 13) * scale_factor, INK, 2)

func _draw_receiver() -> void:
	draw_rect(Rect2(26, 142, 268, 109), METAL)
	for x in [43, 172]:
		draw_rect(Rect2(x, 144, 82, 18), INK)
		_arrow(Vector2(x + 40, 164), Vector2(x + 40, 188), FAINT)
		draw_rect(Rect2(x + 22, 192, 38, 20), Color("746b59"))
	draw_rect(Rect2(41, 226, 238, 14), EDGE)
	_sheet(Rect2(42, 317, 225, 36))
	_token(Vector2(61, 325), 0.46, ASH)
	for x in range(111, 260, 17): draw_circle(Vector2(x, 334), 2.5, Color("b6aa91"))
	# All blue parts belong to the separate, unoperated safe apparatus.
	draw_rect(Rect2(322, 125, 253, 224), Color("d6e2df"))
	draw_rect(Rect2(322, 125, 253, 224), BLUE, false, 2)
	for x in [344, 474]:
		draw_rect(Rect2(x, 159, 73, 16), BLUE)
		draw_rect(Rect2(x + 20, 197, 35, 19), Color("6c929b"))
	# Test coupon deliberately has a round bore and a complete corner, unlike the original.
	draw_rect(Rect2(354, 119, 37, 30), Color("5a8faa"))
	draw_rect(Rect2(354, 119, 37, 30), BLUE, false, 1.4)
	draw_circle(Vector2(373, 134), 5, Color("d6e2df"))
	draw_line(Vector2(493, 129), Vector2(517, 143), BLUE, 7)
	draw_circle(Vector2(490, 128), 8, BLUE)
	draw_rect(Rect2(408, 267, 62, 19), Color("596c68"))
	_sheet(Rect2(420, 272, 38, 16))

func _draw_signatures() -> void:
	_sheet(Rect2(46, 180, 125, 71))
	_sheet(Rect2(28, 274, 247, 43))
	for x in range(40, 266, 19): draw_circle(Vector2(x, 295), 3, Color("b3a990"))
	draw_rect(Rect2(29, 87, 246, 68), PAPER)
	for y in [111, 173, 235, 297]:
		_sheet(Rect2(300, y, 279, 49))
		draw_line(Vector2(314, y + 38), Vector2(563, y + 38), EDGE, 1)

func _draw_drawer() -> void:
	draw_rect(Rect2(30, 158, 267, 97), Color("8e8574"))
	draw_rect(Rect2(40, 168, 247, 77), Color("b3aa97"))
	for n in 98:
		var p := Vector2(43 + (n * 31) % 240, 171 + (n * 19) % 70)
		draw_circle(p, 1.0 + (n % 3) * 0.45, Color("8c8576"))
	# A partial palm and two incomplete digit traces, with no identity ridges.
	_poly([Vector2(134, 190), Vector2(145, 182), Vector2(158, 184), Vector2(170, 199), Vector2(175, 220), Vector2(160, 243), Vector2(135, 239), Vector2(127, 213)], Color("d8cdb7"), false)
	draw_line(Vector2(143, 193), Vector2(141, 171), Color("d8cdb7"), 9, true)
	draw_line(Vector2(157, 193), Vector2(160, 178), Color("d8cdb7"), 8, true)
	draw_rect(Rect2(41, 143, 43, 8), Color("a7a89b"))
	draw_rect(Rect2(93, 145, 35, 7), Color("a7a89b"))
	draw_line(Vector2(84, 144), Vector2(91, 149), INK, 1)
	draw_rect(Rect2(114, 295, 97, 8), Color("949b90"))
	draw_line(Vector2(146, 296), Vector2(170, 294), Color("ece7d7"), 3)
	_leader(Vector2(160, 211), Vector2(197, 267))
	draw_rect(Rect2(372, 114, 153, 93), Color("86623e"))
	for x in [382, 512]: draw_rect(Rect2(x, 200, 12, 93), Color("78563b"))
	_poly([Vector2(344, 207), Vector2(534, 207), Vector2(558, 256), Vector2(328, 256)], WOOD)
	_token(Vector2(415, 217), 1.15, RED)
	for n in 16: draw_circle(Vector2(421 + (n * 7) % 46, 224 + (n * 13) % 36), 1.0, Color("b9b1a1"))
	_leader(Vector2(450, 244), Vector2(454, 312))
