@tool
@static_unload
extends RefCounted
## 手稿界面的组件工厂。外观（语义色、字号、字体角色、间距）全部定义在
## res://scenes/UI/Styles/UITheme.tres（由 tools/build_ui_theme.py 生成）；这里只按类型变体创建组件、读取主题条目。

const Sound := preload("res://scenes/Shared/Audio/Sound.gd")
const THEME: Theme = preload("res://scenes/UI/Styles/UITheme.tres")
const SEMANTIC_COLORS := ["seal", "ink", "muted", "match", "hand"]
## 功能图标（Material Symbols Outlined 码位）；只用于功能，不作装饰（设计规范第六部分 §4.7）。
const ICONS := {
	"check_box_outline_blank": "", "check_box": "", "chevron_left": "", "chevron_right": "",
	"close": "", "radio_button_unchecked": "", "radio_button_checked": "", "person": "",
	"play_arrow": "", "pause": "", "fast_forward": "", "replay": "", "skip_next": "",
	"menu_book": "", "map": "", "schedule": "", "edit_note": "", "location_on": "",
}

static func theme() -> Theme:
	return THEME

## 主题里的语义色：paper、paper_dark、ink、muted、seal、match、hand、card_dark、rule。
static func color(name: String) -> Color:
	return THEME.get_color(name, "Paper")

static func font() -> Font:
	return THEME.default_font

## 字体角色：print（印刷）、register（登记）、hand（笔迹）、icon、icon_filled。
static func role_font(role: String) -> Font:
	return THEME.get_font(role, "Paper")

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

static func _wire(b: Button, on_press: Callable) -> Button:
	if not Engine.is_editor_hint():
		b.pressed.connect(func(): Sound.sfx("ui_click", -4.0))
		if on_press.is_valid():
			b.pressed.connect(on_press)
	return b

## 次操作（默认按钮）；variation 可换成主题里的其他按钮变体。
static func button(text: String, on_press: Callable, variation: StringName = &"") -> Button:
	var b: Button = preload("res://scenes/UI/Components/PaperButton.tscn").instantiate()
	b.text = text
	if variation != &"":
		b.theme_type_variation = variation
	return _wire(b, on_press)

## 主操作：墨色实心；每个弹窗页脚至多一个。
static func action(text: String, on_press: Callable) -> Button:
	return button(text, on_press, &"PaperAction")

## 选项行：表单与重演手稿的单选（序号由调用方设 serial）。
static func check(text: String, on_press: Callable, variation: StringName = &"") -> Button:
	var b: Button = preload("res://scenes/UI/Components/PaperCheck.tscn").instantiate()
	b.text = text
	if variation != &"":
		b.theme_type_variation = variation
	return _wire(b, on_press)

## 条目行：候选菜单与笔记本；serial 为左侧登记体序号。
static func entry(text: String, on_press: Callable, serial := "", variation: StringName = &"") -> Button:
	var b: Button = preload("res://scenes/UI/Components/PaperEntry.tscn").instantiate()
	b.text = text
	b.serial = serial
	if variation != &"":
		b.theme_type_variation = variation
	return _wire(b, on_press)

## 印戳：文字来自快照或面板状态标签；tone 为 seal / match / muted。
static func stamp(text: String, tone := "seal", animate := false) -> Control:
	var s: Control = preload("res://scenes/UI/Components/PaperStamp.tscn").instantiate()
	s.animate = animate
	s.tone = tone
	s.stamp_text = text
	return s

## 计数：total 个小格，filled 个填墨。
static func tally(total: int, filled: int) -> Control:
	var t: Control = preload("res://scenes/UI/Components/PaperTally.tscn").instantiate()
	t.total = total
	t.filled = filled
	return t

## 功能图标（Label）；filled 为真时用实心字形。
static func icon(name: String, filled := false, large := false) -> Label:
	var l := Label.new()
	l.theme = THEME
	l.theme_type_variation = &"PaperIconLarge" if large else (&"PaperIconFilled" if filled else &"PaperIcon")
	l.text = ICONS.get(name, "")
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return l

## 罗马数字序号（选项行用）。
static func roman(n: int) -> String:
	var values := [[10, "X"], [9, "IX"], [5, "V"], [4, "IV"], [1, "I"]]
	var out := ""
	for pair in values:
		while n >= pair[0]:
			out += pair[1]
			n -= pair[0]
	return out

static func icon_text(name: String) -> String:
	return ICONS.get(name, "")

## 像素字（标题类大字，半分辨率无抗锯齿渲染）；pixel_type 为主题里的 Pixel* 类型。
static func pixel(text: String, pixel_type: StringName = &"PixelTitle") -> Control:
	var p: Control = preload("res://scenes/UI/Components/PaperPixelText.tscn").instantiate()
	p.pixel_type = pixel_type
	p.text = text
	return p

static func rule() -> Control:
	return preload("res://scenes/UI/Components/PaperRule.tscn").instantiate()

## 通用纸页容器；动态加载避免 Paper 与 PaperModal 互相预加载。size.x 取最近的宽度档，size.y 为内容高度上限。
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
