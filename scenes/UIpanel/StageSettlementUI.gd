@tool
extends Control
## Only completed, viewer-visible outcomes are rendered.
signal next_stage_requested
signal return_title_requested
signal close_requested

const Chrome := preload("res://scenes/UIpanel/FieldHUD/FieldChrome.gd")
const UIInput := preload("res://scenes/UIpanel/UIInput.gd")
@onready var settlement_panel: PanelContainer = %SettlementPanel
@onready var stage_title_label: Label = %StageTitleLabel
@onready var outcome_summary: RichTextLabel = %OutcomeSummary
@onready var survivors_list: VBoxContainer = %SurvivorsList
@onready var faction_changes_list: VBoxContainer = %FactionChangesList
@onready var ending_badge: Label = %EndingBadge
@onready var credits_scroll: ScrollContainer = %CreditsScroll
@onready var credits_label: RichTextLabel = %CreditsLabel
@onready var btn_continue: Button = %BtnContinue
@onready var btn_title: Button = %BtnTitle

var _settlement_data: Dictionary = {}
var _credits_visible := false
var _navigation = UIInput.new()

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	Chrome.apply(self, 19)
	settlement_panel.add_theme_stylebox_override("panel", StyleBoxEmpty.new())
	Chrome.button(btn_continue)
	Chrome.button(btn_title, true)
	btn_continue.pressed.connect(_on_continue_pressed)
	btn_title.pressed.connect(_on_title_pressed)
	_render_settlement()

func set_settlement_data(data: Dictionary) -> void:
	_settlement_data = data.duplicate(true)
	_credits_visible = false
	_render_settlement()
	if visible: btn_continue.grab_focus()

func is_settlement_open() -> bool:
	return visible and not _settlement_data.is_empty()

func _render_settlement() -> void:
	if not is_node_ready(): return
	visible = not _settlement_data.is_empty()
	if not visible: return
	stage_title_label.text = str(_settlement_data.get("stage_name", _settlement_data.get("title", "阶段结束")))
	outcome_summary.text = str(_settlement_data.get("summary", ""))
	var ending := bool(_settlement_data.get("is_ending", false))
	ending_badge.text = str(_settlement_data.get("ending_title", ""))
	ending_badge.visible = ending and not ending_badge.text.is_empty()
	credits_label.text = str(_settlement_data.get("credits", ""))
	credits_scroll.visible = _credits_visible and not credits_label.text.is_empty()
	%Columns.visible = not credits_scroll.visible
	btn_continue.visible = not ending or not credits_label.text.is_empty()
	btn_continue.text = "返回" if _credits_visible else "制作人员" if ending else "继续"
	for container in [survivors_list, faction_changes_list]:
		for child in container.get_children():
			container.remove_child(child)
			child.queue_free()
	var characters: Array = _settlement_data.get("characters", [])
	for value in characters:
		if value is not Dictionary: continue
		var row := HBoxContainer.new()
		row.add_theme_constant_override("separation", 18)
		var name_label := Label.new()
		name_label.text = str(value.get("name", ""))
		name_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		row.add_child(name_label)
		var status := Label.new()
		var parts: Array[String] = []
		for key in ["status", "disposition"]:
			var text := str(value.get(key, ""))
			if not text.is_empty(): parts.append(text)
		status.text = " · ".join(parts)
		status.add_theme_color_override("font_color", Chrome.MUTED)
		status.add_theme_font_size_override("font_size", 15)
		row.add_child(status)
		survivors_list.add_child(row)
	var factions: Array = _settlement_data.get("factions", [])
	for value in factions:
		if value is not Dictionary: continue
		var label := Label.new()
		label.text = "%s  %s" % [str(value.get("name", "")), str(value.get("change", ""))]
		label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		label.add_theme_color_override("font_color", Chrome.MUTED)
		faction_changes_list.add_child(label)
	%LeftCol.visible = not characters.is_empty()
	%RightCol.visible = not factions.is_empty()

func _on_continue_pressed() -> void:
	if bool(_settlement_data.get("is_ending", false)):
		_credits_visible = not _credits_visible
		_render_settlement()
	else:
		next_stage_requested.emit()

func _on_title_pressed() -> void:
	return_title_requested.emit()

func hide_settlement() -> void:
	visible = false
	_navigation.reset()
	close_requested.emit()

func _input(event: InputEvent) -> void:
	if not is_settlement_open(): return
	if UIInput.is_back(event):
		if _credits_visible:
			_credits_visible = false
			_render_settlement()
		get_viewport().set_input_as_handled()
	elif _navigation.handle(settlement_panel, event):
		get_viewport().set_input_as_handled()

func _process(delta: float) -> void:
	if is_settlement_open(): _navigation.tick(settlement_panel, delta)
