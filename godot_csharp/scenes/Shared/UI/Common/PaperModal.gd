@tool
extends Control
## 纸页模态弹窗预制件（所有章节共用）
## 标题、尺寸、蒙版底色在检视栏配置；页脚按钮用 add_footer_button / clear_footer。
## 编辑器里 Editor Preview 的 preview_text / preview_footer 只用于预览，运行时不显示。

const Paper := preload("res://scenes/Shared/UI/Common/Paper.gd")

@export var modal_title: String = "":
	set(value):
		modal_title = value
		_update_title()

@export var modal_size: Vector2 = Vector2(860, 680):
	set(value):
		modal_size = value
		_update_size()

@export var dim_color: Color = Color(0.03, 0.03, 0.05, 0.55):
	set(value):
		dim_color = value
		if is_instance_valid(dim_rect):
			dim_rect.color = dim_color

@export var sheet_margin: int = 34:
	set(value):
		sheet_margin = value
		_update_sheet_style()

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
@onready var title_label: RichTextLabel = $PanelContainer/Column/TitleLabel
@onready var title_rule: ColorRect = $PanelContainer/Column/TitleRule
@onready var scroll: ScrollContainer = $PanelContainer/Column/ScrollContainer
@onready var body: VBoxContainer = $PanelContainer/Column/ScrollContainer/Body

var _footer_rule: ColorRect
var _footer: HFlowContainer

func _ready() -> void:
	if sheet_margin != 34:
		_update_sheet_style()
	_update_size()
	_update_title()
	if is_instance_valid(dim_rect):
		dim_rect.color = dim_color
	$PanelContainer/Column/ScrollContainer/Body/Preview.text = preview_text
	$PanelContainer/Column/ScrollContainer/Body/Preview.visible = Engine.is_editor_hint()
	set_meta("column", column)
	set_meta("body", body)
	_update_preview_footer()
	resized.connect(_update_size)

func _update_sheet_style() -> void:
	if is_instance_valid(panel):
		var style := panel.get_theme_stylebox("panel").duplicate() as StyleBoxFlat
		style.set_content_margin_all(sheet_margin)
		panel.add_theme_stylebox_override("panel", style)

func _update_size() -> void:
	if not is_instance_valid(panel):
		return
	var available := get_viewport_rect().size - Vector2(32, 32)
	var fitted := Vector2(minf(modal_size.x, maxf(1, available.x)), minf(modal_size.y, maxf(1, available.y)))
	panel.custom_minimum_size = fitted
	panel.set_anchors_preset(Control.PRESET_CENTER)
	panel.offset_left = -fitted.x / 2.0
	panel.offset_right = fitted.x / 2.0
	panel.offset_top = -fitted.y / 2.0
	panel.offset_bottom = fitted.y / 2.0

func _update_title() -> void:
	if not is_instance_valid(title_label) or not is_instance_valid(title_rule):
		return
	if modal_title.is_empty():
		title_label.visible = false
		title_rule.visible = false
	else:
		title_label.visible = true
		title_rule.visible = true
		title_label.text = "[b]" + modal_title + "[/b]"

func _update_preview_footer() -> void:
	if not Engine.is_editor_hint() or not is_node_ready():
		return
	clear_footer()
	for label in preview_footer:
		add_footer_button(label, Callable())

## 在列底追加页脚按钮（首次调用时建立分隔线与靠右的 HFlowContainer）。
func add_footer_button(label: String, action: Callable, disabled := false, tooltip := "") -> Button:
	if not is_instance_valid(_footer):
		_footer_rule = Paper.rule()
		column.add_child(_footer_rule)
		_footer = HFlowContainer.new()
		_footer.name = "Footer"
		_footer.alignment = FlowContainer.ALIGNMENT_END
		_footer.add_theme_constant_override("h_separation", 12)
		_footer.add_theme_constant_override("v_separation", 8)
		column.add_child(_footer)
	var b := Paper.button(label, action)
	b.disabled = disabled
	b.tooltip_text = tooltip
	_footer.add_child(b)
	return b

func clear_footer() -> void:
	for node in [_footer, _footer_rule]:
		if is_instance_valid(node):
			node.get_parent().remove_child(node)
			node.queue_free()
	_footer = null
	_footer_rule = null

func get_footer() -> HFlowContainer:
	return _footer

func get_column() -> VBoxContainer:
	return column

func get_body() -> VBoxContainer:
	return body

func get_scroll() -> ScrollContainer:
	return scroll

func get_panel() -> PanelContainer:
	return panel
