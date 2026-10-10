@tool
extends SubViewportContainer
## 像素字（设计规范第六部分 §4.3）：标题类大字在半分辨率下渲染、关闭抗锯齿，再按最近邻放大回原尺寸，
## 与地图的显示倍率一致。字体、字号、颜色与加粗取主题类型变体 pixel_type（Label 型）；
## 本组件只把字体换成同一字形数据的无抗锯齿副本、字号除以 pixel_scale。正文不用本组件。
## text 中的 BBCode 标记会被去掉；[center] 或 align 决定居中。
## 设置里关掉“像素标题”时按原尺寸、原字体平滑渲染；主题字号或字体变化（文字大小、简明字体）时重排。

const UIAppearance := preload("res://scenes/UI/Components/UIAppearance.gd")

static var _mono_cache := {}

@export_multiline var text := "":
	set(value):
		text = value
		_apply()

@export var pixel_type: StringName = &"PixelTitle":
	set(value):
		pixel_type = value
		_apply()

@export_enum("left", "center", "right") var align := "center":
	set(value):
		align = value
		_apply()

@export var autowrap := false:
	set(value):
		autowrap = value
		_apply()

var _viewport: SubViewport
var _label: Label

func _init() -> void:
	stretch = true
	texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	size_flags_horizontal = Control.SIZE_EXPAND_FILL

func _ready() -> void:
	_viewport = SubViewport.new()
	_viewport.transparent_bg = true
	_viewport.disable_3d = true
	_viewport.gui_disable_input = true
	_viewport.canvas_item_default_texture_filter = Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_FILTER_NEAREST
	add_child(_viewport)
	_label = Label.new()
	_label.set_anchors_preset(Control.PRESET_FULL_RECT)
	_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_viewport.add_child(_label)
	resized.connect(_fit_width)
	var gs := UIAppearance.settings(self)
	if gs != null:
		gs.setting_changed.connect(_on_setting_changed)
	_apply()

func _on_setting_changed(section: String, key: String, _v: Variant) -> void:
	if section == "graphics" and key == "pixel_titles":
		_apply()

func _notification(what: int) -> void:
	if what == NOTIFICATION_THEME_CHANGED:
		_apply()

func _pixelated() -> bool:
	return Engine.is_editor_hint() or UIAppearance.pixel_titles(self)

func _scale() -> int:
	return maxi(1, get_theme_constant("pixel_scale", pixel_type)) if _pixelated() else 1

func _apply() -> void:
	if not is_node_ready() or _label == null:
		return
	stretch_shrink = _scale()
	var plain := RegEx.create_from_string("\\[/?[a-z_]+(=[^\\]]*)?\\]").sub(text, "", true)
	var centered := text.contains("[center]") or align == "center"
	_label.text = plain
	_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER if centered else (HORIZONTAL_ALIGNMENT_RIGHT if align == "right" else HORIZONTAL_ALIGNMENT_LEFT)
	_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART if autowrap else TextServer.AUTOWRAP_OFF
	# 内部标签用一份运行时主题：字形取主题字体的无抗锯齿副本，字号除以倍率，颜色照抄主题。
	var inner := Theme.new()
	var font := get_theme_font("font", pixel_type)
	inner.set_font("font", "Label", _mono(font) if _pixelated() else font)
	inner.set_font_size("font_size", "Label", maxi(1, int(get_theme_font_size("font_size", pixel_type) / _scale())))
	inner.set_color("font_color", "Label", get_theme_color("font_color", pixel_type))
	# 行距归零：下面按字体多行尺寸算高度，Label 默认的 3 px 行距会让末行的下伸部被裁掉。
	inner.set_constant("line_spacing", "Label", 0)
	_label.theme = inner
	_fit_width()

## 无抗锯齿副本：同一字形数据，只改抗锯齿与微调；字重、加粗、字距沿用主题字体变体。
## 缓存按变体当前的字形来源区分（简明字体会换掉变体的 base_font）。
static func _mono(font: Font) -> Font:
	if font == null:
		return null
	var key: Variant = font
	if font is FontVariation:
		var v := font as FontVariation
		key = [font, v.base_font, v.variation_embolden, v.variation_opentype.hash()]
	if _mono_cache.has(key):
		return _mono_cache[key]
	var result: Font = font
	if font is FontVariation:
		var variation: FontVariation = (font as FontVariation).duplicate()
		variation.base_font = _mono(variation.base_font)
		result = variation
	elif font is FontFile:
		var file: FontFile = (font as FontFile).duplicate()
		file.antialiasing = TextServer.FONT_ANTIALIASING_NONE
		file.hinting = TextServer.HINTING_NORMAL
		file.subpixel_positioning = TextServer.SUBPIXEL_POSITIONING_DISABLED
		result = file
	_mono_cache[key] = result
	return result

func _fit_width() -> void:
	# SubViewportContainer 的最小尺寸由引擎按子视口算，脚本不能改写；这里直接给出自定最小尺寸。
	if _label == null:
		return
	var s := float(_scale())
	var font := _label.get_theme_font("font")
	var font_size := _label.get_theme_font_size("font_size")
	if font == null:
		return
	var wanted: Vector2
	if autowrap:
		var width := floorf(size.x / s) if size.x > 0.0 else 320.0
		var h := font.get_multiline_string_size(_label.text, HORIZONTAL_ALIGNMENT_LEFT, maxf(1.0, width), font_size).y
		wanted = Vector2(0, ceilf(h) * s)
	else:
		var one := font.get_string_size(_label.text, HORIZONTAL_ALIGNMENT_LEFT, -1, font_size)
		wanted = Vector2(ceilf(one.x), ceilf(font.get_height(font_size))) * s
	if custom_minimum_size != wanted:
		custom_minimum_size = wanted
