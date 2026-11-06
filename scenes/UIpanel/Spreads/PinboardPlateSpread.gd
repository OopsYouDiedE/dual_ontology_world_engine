extends Control

## 通用自由图版与笔记模板（PinboardPlateSpread.gd）
## 吸收合并原 HypothesisNoteUI (B05) 与 GalleryPlateUI (B06)。
## 支持双页跨页画布、线索卡片钉板连线、手写批注与高清图版检视。

signal hypothesis_selected(hypothesis_id: String)
signal status_changed(hypothesis_id: String, status: String)
signal deduction_requested(hypothesis_id: String)
signal note_saved(hypothesis_id: String, note_text: String)
signal plate_selected(plate_id: String)

@export var mode := "hypothesis" # "hypothesis", "gallery"

# Left Page UI Elements
@onready var left_title: Label = $HBoxContainer/LeftPage/Margin/VBox/Header/LeftTitle
@onready var mode_tabs: HBoxContainer = $HBoxContainer/LeftPage/Margin/VBox/Header/ModeTabs
@onready var note_edit_box: VBoxContainer = $HBoxContainer/LeftPage/Margin/VBox/NoteEditBox
@onready var hypo_title_edit: LineEdit = $HBoxContainer/LeftPage/Margin/VBox/NoteEditBox/HypoTitleEdit
@onready var status_option: OptionButton = $HBoxContainer/LeftPage/Margin/VBox/NoteEditBox/StatusOption
@onready var note_text_edit: TextEdit = $HBoxContainer/LeftPage/Margin/VBox/NoteEditBox/NoteTextEdit
@onready var btn_save_note: Button = $HBoxContainer/LeftPage/Margin/VBox/NoteEditBox/ActionRow/SaveNoteButton
@onready var btn_run_deduction: Button = $HBoxContainer/LeftPage/Margin/VBox/NoteEditBox/ActionRow/DeductionButton

@onready var gallery_box: VBoxContainer = $HBoxContainer/LeftPage/Margin/VBox/GalleryBox
@onready var gallery_grid: GridContainer = $HBoxContainer/LeftPage/Margin/VBox/GalleryBox/Scroll/GalleryGrid

# Right Page UI Elements
@onready var right_title: Label = $HBoxContainer/RightPage/Margin/VBox/Header/RightTitle
@onready var pinboard_canvas: Control = $HBoxContainer/RightPage/Margin/VBox/CanvasBox/PinboardCanvas

var _current_hypo_id := "hypo_01"
var _clue_cards := [
	{"id": "card_01", "name": "门锁白磷粉末", "pos": Vector2(80, 100), "type": "physical"},
	{"id": "card_02", "name": "露西安的不在场证明", "pos": Vector2(380, 140), "type": "mind"},
	{"id": "card_03", "name": "抹去的泥泞鞋印", "pos": Vector2(160, 320), "type": "negative"},
	{"id": "card_04", "name": "第14号保密条例", "pos": Vector2(400, 360), "type": "convention"}
]

var _wires := [
	{"from": 0, "to": 2, "color": Color("8a3324")}, # Negative conflict red
	{"from": 1, "to": 0, "color": Color("3a6b8c")}, # Fact link
	{"from": 2, "to": 3, "color": Color("c9983e")}  # Mind link
]

var _plates := [
	{"id": "plate_map", "name": "海岛学院1844全景鸟瞰图"},
	{"id": "plate_lock", "name": "暗簧门锁机械剖面图"},
	{"id": "plate_alembic", "name": "白磷蒸馏回路机理"},
	{"id": "plate_orchid", "name": "夜兰变种花粉结构"}
]

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	status_option.clear()
	status_option.add_item("状态: 存疑 (Doubt)", 0)
	status_option.add_item("状态: 确立 (Verified)", 1)
	status_option.add_item("状态: 排除 (Refuted)", 2)
	status_option.item_selected.connect(_on_status_selected)

	btn_save_note.pressed.connect(_on_save_note_pressed)
	btn_run_deduction.pressed.connect(_on_run_deduction_pressed)

	pinboard_canvas.draw.connect(_on_canvas_draw)
	pinboard_canvas.gui_input.connect(_on_canvas_gui_input)

	_setup_mode_tabs()
	set_mode(mode)

func set_mode(new_mode: String) -> void:
	mode = new_mode
	if mode == "hypothesis":
		left_title.text = "探案假说与笔记"
		right_title.text = "自由图版钉板连线 (Pinboard)"
		note_edit_box.show()
		gallery_box.hide()
	else:
		left_title.text = "全景图版画廊 (Plates)"
		right_title.text = "图版原稿与高精图纸检视"
		note_edit_box.hide()
		gallery_box.show()
		_render_gallery()
	pinboard_canvas.queue_redraw()

func configure(data: Dictionary, _book_data: RefCounted = null) -> void:
	if data.has("mode"):
		set_mode(data["mode"])

func _setup_mode_tabs() -> void:
	for child in mode_tabs.get_children():
		child.queue_free()

	var btn_h := Button.new()
	btn_h.text = "探案笔记"
	btn_h.pressed.connect(func(): set_mode("hypothesis"))
	mode_tabs.add_child(btn_h)

	var btn_g := Button.new()
	btn_g.text = "图版画廊"
	btn_g.pressed.connect(func(): set_mode("gallery"))
	mode_tabs.add_child(btn_g)

func _render_gallery() -> void:
	for child in gallery_grid.get_children():
		child.queue_free()

	for plate in _plates:
		var btn := Button.new()
		btn.custom_minimum_size = Vector2(160, 90)
		btn.text = plate["name"]
		btn.pressed.connect(func():
			plate_selected.emit(plate["id"])
		)
		gallery_grid.add_child(btn)

func _on_canvas_draw() -> void:
	if mode == "hypothesis":
		# Draw corkboard pins and lines
		for w in _wires:
			var c1: Vector2 = _clue_cards[w["from"]]["pos"] + Vector2(60, 25)
			var c2: Vector2 = _clue_cards[w["to"]]["pos"] + Vector2(60, 25)
			pinboard_canvas.draw_line(c1, c2, w["color"], 2.5, true)

		for card in _clue_cards:
			var rect := Rect2(card["pos"], Vector2(140, 50))
			pinboard_canvas.draw_rect(rect, Color("f4edd9"))
			pinboard_canvas.draw_rect(rect, Color("5c4b37"), false, 1.5)
			# Pin head
			pinboard_canvas.draw_circle(card["pos"] + Vector2(10, 10), 4.0, Color("a83232"))
			pinboard_canvas.draw_string(ThemeDB.fallback_font, card["pos"] + Vector2(20, 30), card["name"], HORIZONTAL_ALIGNMENT_LEFT, 110, 12, Color("1e1812"))
	else:
		# Draw schematic blueprint for gallery
		var center := pinboard_canvas.size * 0.5
		pinboard_canvas.draw_rect(Rect2(center - Vector2(250, 180), Vector2(500, 360)), Color("e8e0cc"))
		pinboard_canvas.draw_rect(Rect2(center - Vector2(250, 180), Vector2(500, 360)), Color("30291e"), false, 2.0)
		pinboard_canvas.draw_string(ThemeDB.fallback_font, center + Vector2(-150, 0), "【1844 帝国海岛学院总体拓扑图版】", HORIZONTAL_ALIGNMENT_CENTER, -1, 16, Color("1e1812"))

func _on_canvas_gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		var pos := (event as InputEventMouseButton).position
		for card in _clue_cards:
			var rect := Rect2(card["pos"], Vector2(140, 50))
			if rect.has_point(pos):
				hypo_title_edit.text = "关于 [" + card["name"] + "] 的推演假说"
				note_text_edit.text = "从现场收集到的特征看，此条目与密室案件核心因果链紧密相关。"
				hypothesis_selected.emit(card["id"])
				break

func _on_status_selected(idx: int) -> void:
	var statuses := ["doubt", "verified", "refuted"]
	status_changed.emit(_current_hypo_id, statuses[idx])

func _on_save_note_pressed() -> void:
	note_saved.emit(_current_hypo_id, note_text_edit.text)

func _on_run_deduction_pressed() -> void:
	deduction_requested.emit(_current_hypo_id)
