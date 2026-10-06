@tool
extends Control
## 纸页模态弹窗预制件（两章共用）
## 支持在检视栏（Inspector）中自由配置标题、尺寸、蒙版底色等。

const Paper := preload("res://scenes/Shared/UI/Paper.gd")

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

@export_multiline var preview_text: String = "纸页内容预览。运行时由宿主填入。":
	set(value):
		preview_text = value
		if is_node_ready():
			$PanelContainer/Column/ScrollContainer/Body/Preview.text = value

@onready var dim_rect: ColorRect = $DimRect
@onready var panel: PanelContainer = $PanelContainer
@onready var column: VBoxContainer = $PanelContainer/Column
@onready var title_label: RichTextLabel = $PanelContainer/Column/TitleLabel
@onready var title_rule: ColorRect = $PanelContainer/Column/TitleRule
@onready var scroll: ScrollContainer = $PanelContainer/Column/ScrollContainer
@onready var body: VBoxContainer = $PanelContainer/Column/ScrollContainer/Body

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

func _update_sheet_style() -> void:
	if is_instance_valid(panel):
		var style := panel.get_theme_stylebox("panel").duplicate() as StyleBoxFlat
		style.set_content_margin_all(sheet_margin)
		panel.add_theme_stylebox_override("panel", style)

func _update_size() -> void:
	if not is_instance_valid(panel):
		return
	panel.custom_minimum_size = modal_size
	panel.set_anchors_preset(Control.PRESET_CENTER)
	panel.offset_left = -modal_size.x / 2.0
	panel.offset_right = modal_size.x / 2.0
	panel.offset_top = -modal_size.y / 2.0
	panel.offset_bottom = modal_size.y / 2.0

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

func get_column() -> VBoxContainer:
	return column

func get_body() -> VBoxContainer:
	return body

func get_scroll() -> ScrollContainer:
	return scroll

func get_panel() -> PanelContainer:
	return panel
