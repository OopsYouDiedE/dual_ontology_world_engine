@tool
extends Control
## 纸页模态弹窗预制件（所有章节共用；设计规范第六部分 §4.5）
## 宽度取主题里最近的宽度档（576 / 832 / 1152）；高度按内容计算，上限为视口减边距，超出才滚动。
## modal_size.y 大于 0 时另作高度上限。标题居中，下接双线；页脚次操作靠左、主操作靠右。
## 页脚按钮用 add_footer_button / clear_footer。编辑器里 Editor Preview 的 preview_text / preview_footer 只用于预览。

const Paper := preload("res://scenes/UI/Components/Paper.gd")

@export var modal_title: String = "":
	set(value):
		modal_title = value
		_update_title()

@export var modal_size: Vector2 = Vector2(832, 0):
	set(value):
		modal_size = value
		_queue_fit()

@export_group("Editor Preview")
@export_multiline var preview_text: String = "纸页内容预览。运行时由宿主填入。":
	set(value):
		preview_text = value
		if is_node_ready():
			$PanelContainer/Column/ScrollContainer/Body/Preview.text = value

## 页脚按钮文案（每项一个按钮）；仅编辑器预览。
@export var preview_footer: PackedStringArray = PackedStringArray():
	set(value):
		preview_footer = value
		_update_preview_footer()

@onready var dim_rect: ColorRect = $DimRect
@onready var panel: PanelContainer = $PanelContainer
@onready var column: VBoxContainer = $PanelContainer/Column
@onready var title_label: Control = $PanelContainer/Column/TitleLabel
@onready var title_rule: Control = $PanelContainer/Column/TitleRule
@onready var scroll: ScrollContainer = $PanelContainer/Column/ScrollContainer
@onready var body: VBoxContainer = $PanelContainer/Column/ScrollContainer/Body

var _footer_rule: Control
var _footer: HBoxContainer
var _footer_buttons: Array = []
var _footer_roles: Array = []
var _fit_queued := false

func _ready() -> void:
	_update_title()
	if is_instance_valid(dim_rect):
		dim_rect.color = get_theme_color("dim", "PaperModal")
	$PanelContainer/Column/ScrollContainer/Body/Preview.text = preview_text
	$PanelContainer/Column/ScrollContainer/Body/Preview.visible = Engine.is_editor_hint()
	set_meta("column", column)
	set_meta("body", body)
	_update_preview_footer()
	resized.connect(_queue_fit)
	body.minimum_size_changed.connect(_queue_fit)
	column.minimum_size_changed.connect(_queue_fit)
	body.child_entered_tree.connect(func(_n: Node) -> void: _queue_fit())
	_fit()

func _queue_fit() -> void:
	if _fit_queued or not is_node_ready(): return
	_fit_queued = true
	_fit.call_deferred()

## 宽度档：取主题 PaperModal 常量里离请求宽度最近的一档。
func _snap_width(requested: float) -> float:
	var best := float(get_theme_constant("width_medium", "PaperModal"))
	for key in ["width_small", "width_medium", "width_large"]:
		var w := float(get_theme_constant(key, "PaperModal"))
		if absf(w - requested) < absf(best - requested): best = w
	return best

func _fit() -> void:
	_fit_queued = false
	if not is_instance_valid(panel) or not is_inside_tree(): return
	var margin := float(get_theme_constant("viewport_margin", "PaperModal"))
	var available := get_viewport_rect().size - Vector2(margin, margin) * 2.0
	var width := minf(_snap_width(modal_size.x), maxf(1.0, available.x))
	var cap := available.y if modal_size.y <= 0.0 else minf(modal_size.y, available.y)
	var box := panel.get_theme_stylebox("panel")
	var chrome := box.get_margin(SIDE_TOP) + box.get_margin(SIDE_BOTTOM)
	var others := column.get_combined_minimum_size().y - scroll.custom_minimum_size.y
	var wanted := body.get_combined_minimum_size().y
	var scroll_height := clampf(wanted, 0.0, maxf(0.0, cap - chrome - others))
	scroll.custom_minimum_size.y = scroll_height
	var height := chrome + others + scroll_height
	panel.custom_minimum_size = Vector2(width, height)
	panel.set_anchors_preset(Control.PRESET_CENTER)
	panel.offset_left = -round(width / 2.0)
	panel.offset_right = round(width / 2.0)
	panel.offset_top = -round(height / 2.0)
	panel.offset_bottom = round(height / 2.0)
	panel.size = Vector2(width, height)

func _update_title() -> void:
	if not is_instance_valid(title_label) or not is_instance_valid(title_rule):
		return
	var shown := not modal_title.is_empty()
	title_label.visible = shown
	title_rule.visible = shown
	title_label.text = modal_title if shown else ""
	_queue_fit()

func _update_preview_footer() -> void:
	if not Engine.is_editor_hint() or not is_node_ready():
		return
	clear_footer()
	for label in preview_footer:
		add_footer_button(label, Callable())

## 在列底追加页脚按钮。role：auto（两项以上时最后一项为主操作）、primary、secondary。
func add_footer_button(label: String, action: Callable, disabled := false, tooltip := "", role := "auto") -> Button:
	if not is_instance_valid(_footer):
		_footer_rule = Paper.rule()
		column.add_child(_footer_rule)
		_footer = HBoxContainer.new()
		_footer.name = "Footer"
		_footer.theme_type_variation = &"PaperModalFooter"
		column.add_child(_footer)
	var b := Paper.button(label, action)
	b.disabled = disabled
	b.tooltip_text = tooltip
	_footer_buttons.append(b)
	_footer_roles.append(role)
	_arrange_footer()
	return b

func _arrange_footer() -> void:
	for child in _footer.get_children():
		_footer.remove_child(child)
		if child not in _footer_buttons: child.queue_free()
	var count := _footer_buttons.size()
	var primary := -1
	for i in count:
		if _footer_roles[i] == "primary": primary = i
	if primary < 0 and count >= 2 and _footer_roles[count - 1] == "auto": primary = count - 1
	for i in count:
		var b: Button = _footer_buttons[i]
		b.theme_type_variation = &"PaperAction" if i == primary else &""
	# 次操作靠左，主操作（或唯一的按钮）靠右。
	var right := primary if primary >= 0 else (0 if count == 1 else -1)
	for i in count:
		if i == right:
			var spacer := Control.new()
			spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
			spacer.mouse_filter = Control.MOUSE_FILTER_IGNORE
			_footer.add_child(spacer)
		_footer.add_child(_footer_buttons[i])
	_queue_fit()

func clear_footer() -> void:
	for node in [_footer, _footer_rule]:
		if is_instance_valid(node):
			node.get_parent().remove_child(node)
			node.queue_free()
	_footer = null
	_footer_rule = null
	_footer_buttons.clear()
	_footer_roles.clear()

## 把现有内容放在滚动区上方；节点仍由其原有面板负责更新。
func pin_header(header: Control) -> void:
	if not is_instance_valid(header):
		return
	header.reparent(column, false)
	column.move_child(header, scroll.get_index())
	_queue_fit()

func get_footer() -> HBoxContainer:
	return _footer

func get_column() -> VBoxContainer:
	return column

func get_body() -> VBoxContainer:
	return body

func get_scroll() -> ScrollContainer:
	return scroll

func get_panel() -> PanelContainer:
	return panel
