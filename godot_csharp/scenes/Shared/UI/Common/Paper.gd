@tool
extends RefCounted
## 手稿界面：羊皮纸底、墨色、印红。所有面板与按钮共用这一套样式。

const PAPER := Color("e6dcbf")
const PAPER_DARK := Color("d6c9a6")
const INK := Color("2b2016")
const SEAL := Color("8a3324")
const MUTED := Color("6b5d48")
const RULE := Color(0.17, 0.13, 0.09, 0.28)
const MATCH := Color("3f5a3a")
const Sound := preload("res://scenes/Shared/Audio/Sound.gd")
const FONT: Font = preload("res://ShareAsset/External/ZhaohuaTypeWriter/ZhaohuaTypeWriter.ttf")

## 打字机字体缺字时（▸ ■ ◎ ⊥ ✕ ✓ 等符号）由系统中文字体补字。
static var _stack: Font

static func font() -> Font:
	if _stack == null:
		var system := SystemFont.new()
		system.font_names = PackedStringArray(["Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans CJK SC", "Segoe UI Symbol"])
		var variation := FontVariation.new()
		variation.base_font = FONT
		variation.fallbacks = [system]
		_stack = variation
	return _stack

static func theme() -> Theme:
	return preload("res://scenes/Shared/UI/Common/PaperTheme.tres")

## Story sources name semantic colors; the presentation theme owns their values.
static func story_text(text: String) -> String:
	for key in {"seal": SEAL, "ink": INK, "muted": MUTED, "match": MATCH}:
		var color: Color = {"seal": SEAL, "ink": INK, "muted": MUTED, "match": MATCH}[key]
		text = text.replace("[color=%s]" % key, "[color=#%s]" % color.to_html(false))
	return text

static func sheet(margin := 34) -> StyleBoxFlat:
	var box := StyleBoxFlat.new()
	box.bg_color = PAPER
	box.border_color = Color("7d6a4c")
	box.set_border_width_all(2)
	box.set_corner_radius_all(3)
	box.shadow_color = Color(0, 0, 0, 0.45)
	box.shadow_size = 14
	box.set_content_margin_all(margin)
	return box

static func label(text: String, size := 0, color := Color(-1, -1, -1, -1)) -> RichTextLabel:
	var l: RichTextLabel = preload("res://scenes/Shared/UI/Common/PaperLabel.tscn").instantiate()
	if size > 0:
		l.add_theme_font_size_override("normal_font_size", size)
		l.add_theme_font_size_override("bold_font_size", size)
	if color.a >= 0:
		l.add_theme_color_override("default_color", color)
	l.text = story_text(text)
	return l

static func button(text: String, action: Callable, size := 0) -> Button:
	var b: Button = preload("res://scenes/Shared/UI/Common/PaperButton.tscn").instantiate()
	b.text = text
	if size > 0:
		b.add_theme_font_size_override("font_size", size)
	if not Engine.is_editor_hint():
		b.pressed.connect(func(): Sound.sfx("ui_click", -4.0))
		if action.is_valid():
			b.pressed.connect(action)
	return b

static func rule() -> ColorRect:
	return preload("res://scenes/Shared/UI/Common/PaperRule.tscn").instantiate()

const MODAL_SCENE: PackedScene = preload("res://scenes/Shared/UI/Common/PaperModal.tscn")

## 两章原样共用的纸页弹窗；流程状态与导航仍由各宿主负责。
static func modal(layer: CanvasLayer, title: String, size: Vector2) -> Control:
	var instance: Control = MODAL_SCENE.instantiate()
	instance.set("modal_title", title)
	instance.set("modal_size", size)
	layer.add_child(instance)
	return instance

## 立即移除并释放全部子节点（避免 queue_free 延迟导致计数残留）。
static func clear_children(node: Node) -> void:
	for child in node.get_children():
		node.remove_child(child)
		child.queue_free()
