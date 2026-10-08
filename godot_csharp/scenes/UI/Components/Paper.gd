@tool
@static_unload
extends RefCounted
## 手稿界面的组件工厂。外观（羊皮纸底、墨色、印红、字号、间距）全部定义在
## res://scenes/UI/Styles/UITheme.tres；这里只按类型变体创建组件、读取主题颜色。

const Sound := preload("res://scenes/Shared/Audio/Sound.gd")
const THEME: Theme = preload("res://scenes/UI/Styles/UITheme.tres")
const SEMANTIC_COLORS := ["seal", "ink", "muted", "match"]

static func theme() -> Theme:
	return THEME

## 主题里的语义色：paper、paper_dark、ink、seal、muted、rule、match。
static func color(name: String) -> Color:
	return THEME.get_color(name, "Paper")

static func font() -> Font:
	return THEME.default_font

## 剧本只写语义色名，颜色值由主题决定。
static func story_text(text: String) -> String:
	for key in SEMANTIC_COLORS:
		text = text.replace("[color=%s]" % key, "[color=#%s]" % color(key).to_html(false))
	return text

static func label(text: String, variation: StringName = &"") -> RichTextLabel:
	var l: RichTextLabel = preload("res://scenes/UI/Components/PaperLabel.tscn").instantiate()
	if variation != &"":
		l.theme_type_variation = variation
	l.text = story_text(text)
	return l

static func button(text: String, action: Callable, variation: StringName = &"") -> Button:
	var b: Button = preload("res://scenes/UI/Components/PaperButton.tscn").instantiate()
	b.text = text
	if variation != &"":
		b.theme_type_variation = variation
	if not Engine.is_editor_hint():
		b.pressed.connect(func(): Sound.sfx("ui_click", -4.0))
		if action.is_valid():
			b.pressed.connect(action)
	return b

static func rule() -> ColorRect:
	return preload("res://scenes/UI/Components/PaperRule.tscn").instantiate()

## 通用纸页容器；动态加载避免 Paper 与 PaperModal 互相预加载。
static func modal(layer: CanvasLayer, title: String, size: Vector2) -> Control:
	var scene: PackedScene = load("res://scenes/UI/Components/PaperModal.tscn")
	var instance: Control = scene.instantiate()
	instance.set("modal_title", title)
	instance.set("modal_size", size)
	layer.add_child(instance)
	return instance

## 立即移除并释放全部子节点（避免 queue_free 延迟导致计数残留）。
static func clear_children(node: Node) -> void:
	for child in node.get_children():
		node.remove_child(child)
		child.queue_free()
