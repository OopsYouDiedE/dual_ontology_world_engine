@tool
extends Control
## 摊开的册子（设计规范第六部分 §4.7）：整套界面是所控角色的案卷册子。封板上两页横格纸，
## 中缝分级阴影，左右露出书页侧边，书口外一列索引签。左右页各有一列内容（超高时滚动），右页底部是页脚。
## 尺寸、颜色取主题 PaperBook 条目；页面样式取 PaperBookPageLeft / PaperBookPageRight。
## set_tabs([{id, label}], 当前 id) 建索引签，点签发 tab_selected(id)；left_content() / right_content() 取内容列；
## add_footer_button(文字, 回调, 是否主操作) 往右页页脚加按钮（主操作最后加）；clear_pages() 清空两页与页脚。

signal tab_selected(id: String)

const Paper := preload("res://scenes/UI/Components/Paper.gd")

@export_group("Editor Preview")
@export var preview_tabs: PackedStringArray = PackedStringArray(["凡例", "操作", "书签"]):
	set(value):
		preview_tabs = value
		_preview()

@onready var _book: Control = $Book
@onready var _board: Control = $Book/Board
@onready var _overlay: Control = $Book/Overlay
@onready var _left: PanelContainer = $Book/LeftPage
@onready var _right: PanelContainer = $Book/RightPage
@onready var _tabs: VBoxContainer = $Book/Tabs
@onready var _footer: HBoxContainer = $Book/RightPage/Column/Footer

var _group := ButtonGroup.new()

func _ready() -> void:
	$DimRect.color = get_theme_color("dim", "PaperBook")
	_board.draw.connect(_draw_board)
	_overlay.draw.connect(_draw_overlay)
	resized.connect(_layout)
	_layout()
	_preview()

func _notification(what: int) -> void:
	if what == NOTIFICATION_THEME_CHANGED and is_node_ready():
		$DimRect.color = get_theme_color("dim", "PaperBook")
		_layout()

func _preview() -> void:
	if not Engine.is_editor_hint() or not is_node_ready():
		return
	var items: Array = []
	for label in preview_tabs:
		items.append({"id": label, "label": label})
	set_tabs(items, preview_tabs[0] if not preview_tabs.is_empty() else "")

func _k(key: String) -> float:
	return float(get_theme_constant(key, "PaperBook"))

## 册子居中；书口外的索引签也要装进视口，所以整体宽度含签宽与左侧封板边。
func _layout() -> void:
	if not is_node_ready():
		return
	var view := size if size.x > 0.0 else get_viewport_rect().size
	var margin := _k("viewport_margin")
	var edge := _k("edge")
	var room := view - Vector2(margin * 2.0 + edge + _k("tab_width") + 8.0, margin * 2.0 + edge * 2.0)
	var w := floorf(minf(_k("width"), room.x) / 2.0) * 2.0
	var h := floorf(minf(_k("height"), room.y))
	_book.position = Vector2(roundf((view.x - (w + edge + _k("tab_width") + 8.0)) / 2.0 + edge), roundf((view.y - h) / 2.0))
	_book.size = Vector2(w, h)
	var half := w / 2.0
	_left.position = Vector2.ZERO
	_left.size = Vector2(half, h)
	_right.position = Vector2(half, 0)
	_right.size = Vector2(half, h)
	_board.size = _book.size
	_overlay.size = _book.size
	_tabs.position = Vector2(w, _k("tab_top"))
	_board.queue_redraw()
	_overlay.queue_redraw()

## 封板与书页侧边（画在两页之下）。
func _draw_board() -> void:
	var w := _book.size.x
	var h := _book.size.y
	var edge := _k("edge")
	var stack := int(_k("stack"))
	_board.draw_rect(Rect2(-edge, -edge, w + edge * 2.0, h + edge * 2.0), get_theme_color("board", "PaperBook"))
	var light := get_theme_color("stack_light", "PaperBook")
	var dark := get_theme_color("stack_dark", "PaperBook")
	for i in range(0, stack, 2):
		_board.draw_rect(Rect2(-stack + i, 4, 2, h - 2), light if (i / 2) % 2 == 0 else dark)
		_board.draw_rect(Rect2(w + i, 4, 2, h - 2), dark if (i / 2) % 2 == 0 else light)

## 中缝阴影（分级，不用平滑渐变）与两页的边线（画在两页之上）。
func _draw_overlay() -> void:
	var w := _book.size.x
	var h := _book.size.y
	var half := w / 2.0
	var gutter := get_theme_color("gutter", "PaperBook")
	var step := _k("gutter_step")
	var steps := int(_k("gutter_steps"))
	for i in steps:
		var fade := 1.0 - float(i) / float(steps)
		var left := gutter
		left.a = gutter.a * fade
		_overlay.draw_rect(Rect2(half - step * (i + 1), 0, step, h), left)
		var right := gutter
		right.a = gutter.a * fade * 0.8
		_overlay.draw_rect(Rect2(half + step * i, 0, step, h), right)
	var line := get_theme_color("margin_line", "PaperBook")
	var mx := _k("margin_x")
	_overlay.draw_rect(Rect2(mx, 0, 1, h), line)
	_overlay.draw_rect(Rect2(w - mx - 1.0, 0, 1, h), line)

## 索引签：汉字与假名竖排（一字一行）；拉丁字母整行转 90°，自上而下读，像书脊。
func set_tabs(items: Array, current: String = "") -> void:
	Paper.clear_children(_tabs)
	_group = ButtonGroup.new()
	for item in items:
		var id := str(item.get("id", ""))
		var b := Button.new()
		b.theme_type_variation = &"PaperBookTab"
		b.toggle_mode = true
		b.button_group = _group
		b.set_meta("id", id)
		b.pressed.connect(func() -> void: tab_selected.emit(id))
		_tabs.add_child(b)
		var label := str(item.get("label", id))
		if is_latin(label):
			_turned_label(b, label)
		else:
			b.text = vertical(label)
	select_tab(current if current != "" or items.is_empty() else str(items[0].get("id", "")))

func select_tab(id: String) -> void:
	for b in _tabs.get_children():
		var on := str(b.get_meta("id", "")) == id
		(b as Button).set_pressed_no_signal(on)
		b.custom_minimum_size = Vector2(_k("tab_width") + (8.0 if on else 0.0), maxf(_k("tab_height"), float(b.get_meta("tab_length", 0.0))))

## 拉丁字母的签：按钮自身不写字，里面放一行转 90° 的标签。
func _turned_label(b: Button, label: String) -> void:
	b.text = ""
	var font := b.get_theme_font("font")
	var font_size := b.get_theme_font_size("font_size")
	var length := font.get_string_size(label, HORIZONTAL_ALIGNMENT_LEFT, -1, font_size).x
	var height := font.get_height(font_size)
	var text := Label.new()
	text.text = label
	text.mouse_filter = Control.MOUSE_FILTER_IGNORE
	text.size = Vector2(ceilf(length), ceilf(height))
	text.rotation = PI / 2.0
	text.position = Vector2(roundf((_k("tab_width") + height) / 2.0), 12.0)
	b.add_child(text)
	b.set_meta("tab_length", ceilf(length) + 24.0)

static func is_latin(text: String) -> bool:
	return RegEx.create_from_string("[A-Za-z]").search(text) != null

func tab_buttons() -> Array[Node]:
	return _tabs.get_children()

static func vertical(text: String) -> String:
	if is_latin(text):
		return text
	var chars := PackedStringArray()
	for ch in text:
		chars.append(ch)
	return "\n".join(chars)

func left_content() -> VBoxContainer:
	return $Book/LeftPage/Column/Scroll/Content

func right_content() -> VBoxContainer:
	return $Book/RightPage/Column/Scroll/Content

func get_footer() -> HBoxContainer:
	return _footer

func clear_pages() -> void:
	Paper.clear_children(left_content())
	Paper.clear_children(right_content())
	Paper.clear_children(_footer)

## 页脚靠右：次操作在前，主操作最后加（设计规范第六部分 §4.6）。
func add_footer_button(label: String, action: Callable, primary := false, disabled := false, tooltip := "") -> Button:
	var b := Paper.action(label, action) if primary else Paper.button(label, action)
	b.disabled = disabled
	b.tooltip_text = tooltip
	_footer.add_child(b)
	return b
