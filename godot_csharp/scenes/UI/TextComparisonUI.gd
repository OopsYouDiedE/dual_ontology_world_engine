extends Control

## Material 3 Text Comparison Interface (Task 7.13)
## Side-by-side dual-pane document comparison with paragraph indexing.
## Highlights discrepancies with text/symbol tags ([≠差异], [＋新增], [－缺失]).
## Missing materials are NEVER auto-completed or hallucinated.

signal comparison_closed

const MaterialControls := preload("res://scenes/UI/Components/MaterialControls.gd")

@onready var modal_backdrop: ColorRect = %Backdrop
@onready var main_card: PanelContainer = %MainCard
@onready var title_label: Label = %TitleLabel
@onready var close_btn: Button = %CloseButton
@onready var source_a_label: Label = %SourceALabel
@onready var source_b_label: Label = %SourceBLabel
@onready var comparison_list: VBoxContainer = %ComparisonList

var _is_open: bool = false

func _ready() -> void:
	visible = false
	MaterialControls.apply(self)
	MaterialControls.prepare_button(close_btn)
	close_btn.pressed.connect(close_comparison)

func is_comparison_open() -> bool:
	return _is_open

## data format:
## {
##   "title": "两份配餐记录对读",
##   "source_a": "原件：配膳房总账",
##   "source_b": "抄本：护士站领用单",
##   "paragraphs": [
##     {
##       "index": "01",
##       "text_a": "三号病房配给营养液四份。",
##       "text_b": "三号病房配给营养液四份。",
##       "status": "match" # "match" | "diff" | "missing_a" | "missing_b"
##     },
##     {
##       "index": "02",
##       "text_a": "特护病房追加镇静药剂两支。",
##       "text_b": "特护病房追加止痛药剂两支。",
##       "status": "diff"
##     },
##     {
##       "index": "03",
##       "text_a": "经手人：值班护士长。",
##       "text_b": "",
##       "status": "missing_b" # missing in source B
##     }
##   ]
## }
func open_comparison(data: Dictionary) -> void:
	title_label.text = str(data.get("title", "文本材料对读"))
	source_a_label.text = "材料 A 来源: " + str(data.get("source_a", "未知来源"))
	source_b_label.text = "材料 B 来源: " + str(data.get("source_b", "未知来源"))
	
	_rebuild_rows(data.get("paragraphs", []))
	
	visible = true
	_is_open = true
	await get_tree().process_frame
	close_btn.grab_focus()

func _rebuild_rows(paragraphs: Array) -> void:
	for child in comparison_list.get_children():
		comparison_list.remove_child(child)
		child.queue_free()
		
	for item in paragraphs:
		if not item is Dictionary:
			continue
		var index_str: String = str(item.get("index", "00"))
		var text_a: String = str(item.get("text_a", ""))
		var text_b: String = str(item.get("text_b", ""))
		var status: String = str(item.get("status", "match"))
		
		# Auto-derive status if not specified
		if not item.has("status"):
			if text_a.is_empty() and not text_b.is_empty():
				status = "missing_a"
			elif not text_a.is_empty() and text_b.is_empty():
				status = "missing_b"
			elif text_a != text_b:
				status = "diff"
			else:
				status = "match"
				
		var row := PanelContainer.new()
		var box := StyleBoxFlat.new()
		box.corner_radius_top_left = 6
		box.corner_radius_top_right = 6
		box.corner_radius_bottom_right = 6
		box.corner_radius_bottom_left = 6
		box.content_margin_left = 12.0
		box.content_margin_top = 8.0
		box.content_margin_right = 12.0
		box.content_margin_bottom = 8.0
		
		match status:
			"diff":
				box.bg_color = Color(0.28, 0.16, 0.12, 0.8)
				box.border_color = MaterialControls.COLOR_WARNING
				box.set_border_width_all(1)
			"missing_a", "missing_b":
				box.bg_color = Color(0.22, 0.12, 0.12, 0.8)
				box.border_color = MaterialControls.COLOR_ERROR
				box.set_border_width_all(1)
			_:
				box.bg_color = MaterialControls.COLOR_SURFACE_VARIANT
		row.add_theme_stylebox_override("panel", box)
		
		var grid := HBoxContainer.new()
		grid.add_theme_constant_override("separation", 16)
		
		# Paragraph Index
		var idx_lbl := Label.new()
		idx_lbl.text = "[§%s]" % index_str
		idx_lbl.custom_minimum_size = Vector2(48, 0)
		idx_lbl.add_theme_font_size_override("font_size", 14)
		idx_lbl.add_theme_color_override("font_color", MaterialControls.COLOR_OUTLINE)
		grid.add_child(idx_lbl)
		
		# Pane A Text
		var pane_a := VBoxContainer.new()
		pane_a.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		var lbl_a := Label.new()
		lbl_a.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		lbl_a.add_theme_font_size_override("font_size", 15)
		
		if status == "missing_a":
			lbl_a.text = "[－缺失] [未取得对应文本片段]"
			lbl_a.add_theme_color_override("font_color", MaterialControls.COLOR_ERROR)
		elif status == "diff":
			lbl_a.text = "[≠差异] " + text_a
			lbl_a.add_theme_color_override("font_color", MaterialControls.COLOR_WARNING)
		else:
			lbl_a.text = "[＝一致] " + text_a
		pane_a.add_child(lbl_a)
		grid.add_child(pane_a)
		
		# Pane B Text
		var pane_b := VBoxContainer.new()
		pane_b.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		var lbl_b := Label.new()
		lbl_b.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		lbl_b.add_theme_font_size_override("font_size", 15)
		
		if status == "missing_b":
			lbl_b.text = "[－缺失] [未取得对应文本片段]"
			lbl_b.add_theme_color_override("font_color", MaterialControls.COLOR_ERROR)
		elif status == "diff":
			lbl_b.text = "[≠差异] " + text_b
			lbl_b.add_theme_color_override("font_color", MaterialControls.COLOR_WARNING)
		elif status == "missing_a":
			lbl_b.text = "[＋新增] " + text_b
			lbl_b.add_theme_color_override("font_color", MaterialControls.COLOR_SUCCESS)
		else:
			lbl_b.text = "[＝一致] " + text_b
		pane_b.add_child(lbl_b)
		grid.add_child(pane_b)
		
		row.add_child(grid)
		comparison_list.add_child(row)

func close_comparison() -> void:
	if not _is_open:
		return
	_is_open = false
	visible = false
	comparison_closed.emit()

func _gui_input(event: InputEvent) -> void:
	if not _is_open:
		return
	if event.is_action_pressed("ui_cancel") or event.is_action_pressed("cancel"):
		close_comparison()
		get_viewport().set_input_as_handled()
