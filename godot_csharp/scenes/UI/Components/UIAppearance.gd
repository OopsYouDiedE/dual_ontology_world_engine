extends RefCounted
## 外观设置的落地（设计规范第六部分 §4.8）：纸面纹理、文字大小、高对比、简明字体，以及按语言换字（§4.5）。
## 只改三份界面主题里已有的条目——字号、颜色、样式盒的颜色与纹理、字体变体的字形来源——
## 不给节点加覆盖、不新建样式盒；每项原值在第一次改动前记下，关掉选项即还原。
## 像素标题由 PaperPixelText 自己读取；减少动效由各动效处用 reduce_motion() 判断。
## UISystem 与标题页在 _ready 里调用 bind()；设置变化时自动重新套用。

const THEME_PATHS := [
	"res://scenes/UI/Styles/UITheme.tres",
	"res://scenes/UI/Styles/DialogueTheme.tres",
	"res://scenes/UI/Styles/TitleTheme.tres",
]
const PLAIN_TEXTURE := "res://ShareAsset/Internal/Props/UIPaper/paper_plain.png"
const NOTO_SERIF := "res://ShareAsset/External/NotoSerifSC/NotoSerifSC[wght].ttf"
const NOTO_SANS := "res://ShareAsset/External/NotoSansSC/NotoSansSC[wght].ttf"
## 简明字体：装饰性字形（仿旧打字机、手写、行书、美术字）换成思源宋体 / 黑体，字重如下。
const PLAIN_MAP := {
	"ZhaohuaTypeWriter": [NOTO_SERIF, 400],
	"Yozai": [NOTO_SANS, 400],
	"ZhiMangXing": [NOTO_SANS, 900],
	"ZCOOLQingKe": [NOTO_SANS, 700],
}
## 按语言换字（第三项为字距，缺省沿用原值）：英文笔迹用 Playwrite、报头用思源宋体特粗并收紧字距；日文报头用祐字、笔迹用紅道、正文用 New Tegomin（日文字形）。
## 简明字体优先于此。字体都在库内，许可证随素材保存。
const LOCALE_MAP := {
	"en": {
		"Yozai": ["res://ShareAsset/External/playwriteustrad/PlaywriteUSTrad[wght].ttf", 400],
		"ZhiMangXing": [NOTO_SERIF, 900, 1],
	},
	"ja": {
		"ZhiMangXing": ["res://ShareAsset/External/yujisyuku/YujiSyuku-Regular.ttf", 400],
		"Yozai": ["res://ShareAsset/External/ZenKurenaido/ZenKurenaido-Regular.ttf", 400],
		"ZhaohuaTypeWriter": ["res://ShareAsset/External/newtegomin/NewTegomin-Regular.ttf", 400],
	},
}

static var _bound := false
static var _sizes := {}
static var _colors := {}
static var _boxes := {}
static var _fonts := {}

static func settings(node: Node) -> Node:
	if node == null or not node.is_inside_tree():
		return null
	return node.get_node_or_null("/root/GameSettings")

static func reduce_motion(node: Node) -> bool:
	var gs := settings(node)
	return gs != null and bool(gs.get_value("accessibility", "reduce_motion"))

static func pixel_titles(node: Node) -> bool:
	var gs := settings(node)
	return gs == null or bool(gs.get_value("graphics", "pixel_titles"))

static func bind(node: Node) -> void:
	if Engine.is_editor_hint():
		return
	var gs := settings(node)
	if gs == null:
		return
	if not _bound:
		_bound = true
		gs.setting_changed.connect(func(section: String, _key: String, _value: Variant) -> void:
			if section in ["graphics", "accessibility", "language"]:
				apply(gs))
	apply(gs)

static func apply(gs: Object) -> void:
	var language := str(gs.current_language())
	var paper := bool(gs.get_value("graphics", "paper_texture"))
	var factor := float(gs.get_value("accessibility", "text_scale"))
	var contrast := bool(gs.get_value("accessibility", "high_contrast"))
	var plain := bool(gs.get_value("accessibility", "plain_fonts"))
	for index in THEME_PATHS.size():
		var theme := load(THEME_PATHS[index]) as Theme
		if theme == null:
			continue
		theme.set_block_signals(true)
		_apply_sizes(index, theme, factor)
		_apply_colors(index, theme, contrast)
		_apply_boxes(theme, paper, contrast)
		_apply_fonts(theme, plain, language)
		theme.set_block_signals(false)
		theme.emit_changed()

# ------------------------------------------------------------------ sizes ---

static func _apply_sizes(index: int, theme: Theme, factor: float) -> void:
	if not _sizes.has(index):
		var record := {"": theme.default_font_size}
		for type in theme.get_font_size_type_list():
			for name in theme.get_font_size_list(type):
				record[type + "/" + name] = theme.get_font_size(name, type)
		_sizes[index] = record
	var saved: Dictionary = _sizes[index]
	for key in saved:
		var original := int(saved[key])
		if original <= 0:
			continue
		var scaled := maxi(1, roundi(original * factor))
		if key == "":
			theme.default_font_size = scaled
		else:
			var parts: PackedStringArray = str(key).split("/")
			theme.set_font_size(parts[1], parts[0], scaled)

# ----------------------------------------------------------------- colors ---

static func _apply_colors(index: int, theme: Theme, contrast: bool) -> void:
	if not _colors.has(index):
		var record := {}
		for type in theme.get_color_type_list():
			for name in theme.get_color_list(type):
				record[type + "/" + name] = theme.get_color(name, type)
		_colors[index] = record
	var saved: Dictionary = _colors[index]
	for key in saved:
		var parts: PackedStringArray = str(key).split("/")
		theme.set_color(parts[1], parts[0], _contrast(saved[key]) if contrast else saved[key])

## 高对比：淡墨与规线一律换成墨色并提高不透明度；纸色、笔迹色、印红不变。
static func _contrast(color: Color) -> Color:
	var palette: Dictionary = _colors.get(0, {})
	var ink: Color = palette.get("Paper/ink", Color(0.133, 0.11, 0.082))
	for key in ["Paper/muted", "Paper/rule"]:
		if palette.has(key) and _same_rgb(color, palette[key]):
			return Color(ink.r, ink.g, ink.b, maxf(color.a, 0.85))
	return color

static func _same_rgb(a: Color, b: Color) -> bool:
	return absf(a.r - b.r) < 0.01 and absf(a.g - b.g) < 0.01 and absf(a.b - b.b) < 0.01

# -------------------------------------------------------------- styleboxes ---

static func _apply_boxes(theme: Theme, paper: bool, contrast: bool) -> void:
	var plain_texture := load(PLAIN_TEXTURE) as Texture2D
	for type in theme.get_stylebox_type_list():
		for name in theme.get_stylebox_list(type):
			var box := theme.get_stylebox(name, type)
			if box == null:
				continue
			if not _boxes.has(box):
				_boxes[box] = _snapshot(box)
			var saved: Dictionary = _boxes[box]
			if box is StyleBoxTexture:
				(box as StyleBoxTexture).texture = saved.texture if paper or plain_texture == null else plain_texture
			elif box is StyleBoxFlat:
				var flat := box as StyleBoxFlat
				flat.border_color = _contrast(saved.border_color) if contrast else saved.border_color
				flat.bg_color = _contrast(saved.bg_color) if contrast else saved.bg_color
			elif box is StyleBoxLine:
				(box as StyleBoxLine).color = _contrast(saved.color) if contrast else saved.color

static func _snapshot(box: StyleBox) -> Dictionary:
	if box is StyleBoxTexture:
		return {"texture": (box as StyleBoxTexture).texture}
	if box is StyleBoxFlat:
		return {"border_color": (box as StyleBoxFlat).border_color, "bg_color": (box as StyleBoxFlat).bg_color}
	if box is StyleBoxLine:
		return {"color": (box as StyleBoxLine).color}
	return {}

# ------------------------------------------------------------------ fonts ---

static func _apply_fonts(theme: Theme, plain: bool, language: String) -> void:
	var pending: Array = []
	if theme.default_font != null:
		pending.append(theme.default_font)
	for type in theme.get_font_type_list():
		for name in theme.get_font_list(type):
			pending.append(theme.get_font(name, type))
	var seen := {}
	while not pending.is_empty():
		var font: Font = pending.pop_back()
		if font == null or seen.has(font):
			continue
		seen[font] = true
		for fallback in font.fallbacks:
			pending.append(fallback)
		if font is FontVariation:
			_swap(font as FontVariation, plain, language)

static func _swap(variation: FontVariation, plain: bool, language: String) -> void:
	if not _fonts.has(variation):
		var base := variation.base_font
		var path := base.resource_path if base != null else ""
		var family := ""
		for key in PLAIN_MAP:
			if path.contains(key):
				family = key
		_fonts[variation] = {} if family == "" else {
			"base_font": base, "opentype": variation.variation_opentype.duplicate(),
			"embolden": variation.variation_embolden, "family": family,
			"spacing": variation.spacing_glyph,
		}
	var saved: Dictionary = _fonts[variation]
	if saved.is_empty():
		return
	var family := str(saved.family)
	var target: Array = []
	if plain:
		target = PLAIN_MAP[family]
	elif LOCALE_MAP.get(language, {}).has(family):
		target = LOCALE_MAP[language][family]
	var replacement: Font = load(str(target[0])) as Font if not target.is_empty() else null
	if replacement != null:
		variation.base_font = replacement
		variation.variation_opentype = {TextServerManager.get_primary_interface().name_to_tag("wght"): int(target[1])}
		variation.variation_embolden = 0.0
		variation.spacing_glyph = int(target[2]) if target.size() > 2 else int(saved.spacing)
	else:
		variation.base_font = saved.base_font
		variation.variation_opentype = saved.opentype
		variation.variation_embolden = saved.embolden
		variation.spacing_glyph = saved.spacing
