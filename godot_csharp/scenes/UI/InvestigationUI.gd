extends CanvasLayer

signal closed
signal action_chosen(id: String)

@onready var modal_box: PanelContainer = %ModalBox
@onready var modal_title: Label = %ModalTitle
@onready var modal_close_button: Button = %ModalCloseButton
@onready var modal_body: VBoxContainer = %ModalBody
@onready var modal_footer: HBoxContainer = %ModalFooter

var _current_panel := ""

func _ready() -> void:
	layer = 25
	visible = false
	modal_close_button.pressed.connect(close)

func _input(event: InputEvent) -> void:
	if not visible: return
	if event is InputEventKey and event.pressed and event.keycode == KEY_ESCAPE:
		close()
		get_viewport().set_input_as_handled()

func open_modal(title: String, min_size := Vector2(620, 420), panel_id := "") -> VBoxContainer:
	_current_panel = panel_id
	modal_title.text = title
	modal_box.custom_minimum_size = min_size
	for child in modal_body.get_children():
		child.queue_free()
	for child in modal_footer.get_children():
		child.queue_free()
	visible = true
	return modal_body

func close() -> void:
	visible = false
	_current_panel = ""
	closed.emit()

func open_notebook(data: Dictionary) -> void:
	var body := open_modal("随身手账", Vector2(680, 460), "notebook")
	var label := RichTextLabel.new()
	label.bbcode_enabled = true
	label.size_flags_vertical = Control.SIZE_EXPAND_FILL
	var text := "[b]人物与线索记录[/b]\n\n"
	var people: Array = data.get("people", [])
	if people.is_empty():
		text += "暂无额外记录。"
	else:
		for p in people:
			text += "• [b]%s[/b]：%s\n" % [p.get("name", ""), p.get("desc", "")]
	label.text = text
	body.add_child(label)

func open_manuscript(data: Dictionary) -> void:
	var body := open_modal("案情手稿", Vector2(720, 500), "replay")
	var label := RichTextLabel.new()
	label.bbcode_enabled = true
	label.size_flags_vertical = Control.SIZE_EXPAND_FILL
	var text := "[b]推演与假设[/b]\n\n"
	var hypotheses: Array = data.get("hypotheses", [])
	if hypotheses.is_empty():
		text += "当前尚未建立能够解释现有观察的推演手稿。"
	else:
		for h in hypotheses:
			text += "• %s\n" % str(h)
	label.text = text
	body.add_child(label)

func open_card(data: Dictionary) -> void:
	var body := open_modal(str(data.get("title", "证物卡片")), Vector2(560, 380), "card")
	var label := RichTextLabel.new()
	label.bbcode_enabled = true
	label.size_flags_vertical = Control.SIZE_EXPAND_FILL
	label.text = str(data.get("text", data.get("body", "")))
	body.add_child(label)
	var actions: Array = data.get("actions", [])
	for act in actions:
		var btn := Button.new()
		btn.text = str(act.get("label", act.get("text", "确定")))
		var aid := str(act.get("id", ""))
		btn.pressed.connect(func():
			action_chosen.emit(aid)
			close()
		)
		modal_footer.add_child(btn)

func open_campus_map(data: Dictionary) -> void:
	var body := open_modal("校区平面图", Vector2(720, 500), "campus_map")
	var label := RichTextLabel.new()
	label.bbcode_enabled = true
	label.size_flags_vertical = Control.SIZE_EXPAND_FILL
	var text: String = str(data.get("desc", ""))
	if text == "":
		text = "[b]校区地图[/b]\n\n可通过现场进行路线导航与区域查看。"
	label.text = text
	body.add_child(label)

func open_ending(data: Dictionary) -> void:
	var body := open_modal(str(data.get("title", "案情收卷")), Vector2(640, 420), "ending")
	var label := RichTextLabel.new()
	label.bbcode_enabled = true
	label.size_flags_vertical = Control.SIZE_EXPAND_FILL
	label.text = str(data.get("text", "已完成阶段推演。"))
	body.add_child(label)

func show_spatial_choices(data: Dictionary) -> void:
	var title := str(data.get("title", "可行动作"))
	var body := open_modal(title, Vector2(500, 320), "spatial")
	var options: Array = data.get("options", [])
	if options.is_empty():
		var label := Label.new()
		label.text = "没有可执行的动作。"
		body.add_child(label)
	else:
		for opt in options:
			var btn := Button.new()
			btn.text = str(opt.get("label", opt.get("id", "")))
			var cid := str(opt.get("id", ""))
			btn.custom_minimum_size = Vector2(0, 36)
			btn.pressed.connect(func():
				action_chosen.emit(cid)
				close()
			)
			body.add_child(btn)
