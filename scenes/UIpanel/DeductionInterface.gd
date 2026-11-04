@tool
extends Control

## Material 3 Deduction Interface (Tasks 7.D1~7.D8)
## Strict cognitive-only mental deduction workspace.
## 7.D5: Hypothesis-clue link lines & highlight in 2D UI & 3D viewport.
## 7.D6: Contradiction conflict red lines & warning badges.
## 7.D7: Historical deduction plan restoration and side-by-side comparison.
## 7.D8: Exit deduction with 100% clean recovery of original field without residue or physical side-effects.

signal deduction_submitted(hypothesis_id: String, payload: Dictionary)
signal deduction_closed

const MaterialControls := preload("res://scenes/components/shared/MaterialControls.gd")
const Chrome := preload("res://scenes/UIpanel/FieldHUD/FieldChrome.gd")
const UIInput := preload("res://scenes/UIpanel/UIInput.gd")
const DeductionVisual3D_Script := preload("res://scenes/UIpanel/DeductionInterface/DeductionVisual3D.gd")

const COLOR_LINK := Color("96b7a6")
const COLOR_CONFLICT := Color("d39282")

@onready var modal_backdrop: ColorRect = %Backdrop
@onready var main_card: PanelContainer = %MainCard
@onready var title_label: Label = %TitleLabel
@onready var close_btn: Button = %CloseButton
@onready var btn_current_mode: Button = %BtnCurrentMode
@onready var btn_history_mode: Button = %BtnHistoryMode

# Modes
@onready var three_columns: HBoxContainer = %ThreeColumns
@onready var history_view: VBoxContainer = %HistoryView

# Column 1: Evidence panel (7.D2)
@onready var evidence_list: VBoxContainer = %EvidenceList
@onready var evidence_scroll: ScrollContainer = %EvidenceScroll

# Column 2: Hypothesis panel (7.D3)
@onready var hypothesis_list: VBoxContainer = %HypothesisList
@onready var hypothesis_scroll: ScrollContainer = %HypothesisScroll

# Column 3: Submission & Cost panel (7.D4)
@onready var selected_hypothesis_title: Label = %SelectedHypothesisTitle
@onready var selected_hypothesis_details: Label = %SelectedHypothesisDetails
@onready var conflict_notice_card: PanelContainer = %ConflictNoticeCard
@onready var conflict_notice_label: Label = %ConflictNoticeLabel
@onready var cost_time_label: Label = %CostTimeLabel
@onready var cost_comp_label: Label = %CostCompLabel
@onready var cost_stam_label: Label = %CostStamLabel
@onready var submit_btn: Button = %SubmitButton
@onready var cost_warning_label: Label = %CostWarningLabel
@onready var confirm_dialog: ConfirmationDialog = %ConfirmDialog
@onready var snackbar: PanelContainer = %Snackbar

# 7.D5 Canvas
@onready var connection_canvas: Control = %ConnectionCanvas

# 7.D7 History
@onready var history_list: VBoxContainer = %HistoryList
@onready var comparison_title: Label = %ComparisonTitle
@onready var comparison_details: Label = %ComparisonDetails
@onready var restore_historical_btn: Button = %RestoreHistoricalBtn

var _navigation = UIInput.new()
var _is_open: bool = false
var _deduction_data: Dictionary = {}
var _selected_hypothesis_id: String = ""
var _selected_hypothesis_data: Dictionary = {}
var _cost_data: Dictionary = {}
var _cost_valid: bool = false
var _action_id: String = ""

# Link & Evidence Registries (7.D5, 7.D6)
var _evidence_cards: Dictionary = {} # ev_id -> PanelContainer
var _evidence_data: Dictionary = {} # ev_id -> Dictionary
var _hypothesis_buttons: Dictionary = {} # hypo_id -> Button
var _active_lines: Array = [] # [{"from": Vector2, "to": Vector2, "is_conflict": bool}]
var _visual_3d: Node3D = null

# History (7.D7)
var _history_records: Array = []
var _selected_history_data: Dictionary = {}

func _ready() -> void:
	visible = false
	Chrome.apply(self, 17)
	main_card.add_theme_stylebox_override("panel", Chrome.box(0.96, Vector4(28, 24, 28, 24), true))
	get_viewport().size_changed.connect(_fit_workspace)
	_fit_workspace()
	Chrome.button(close_btn)
	Chrome.button(submit_btn)
	Chrome.button(btn_current_mode)
	Chrome.button(btn_history_mode)
	Chrome.button(restore_historical_btn)
	if Engine.is_editor_hint():
		open_deduction({"title": "走廊异响", "evidences": [{"id": "sample", "title": "磨损的钥匙"}], "hypotheses": [{"id": "sample", "title": "侧门曾开启"}]})
		return
	
	close_btn.pressed.connect(exit_deduction)
	submit_btn.pressed.connect(_on_submit_pressed)
	confirm_dialog.confirmed.connect(_on_confirmed_submission)
	
	btn_current_mode.pressed.connect(func(): _switch_mode("current"))
	btn_history_mode.pressed.connect(func(): _switch_mode("history"))
	restore_historical_btn.pressed.connect(_on_restore_historical_pressed)
	
	connection_canvas.draw.connect(_on_connection_canvas_draw)

func is_deduction_open() -> bool:
	return _is_open

## Entry point for mental deduction (Tasks 7.D1 ~ 7.D7)
func open_deduction(data: Dictionary) -> void:
	_deduction_data = data.duplicate(true)
	_action_id = str(data.get("action_id", data.get("id", "")))
	_cost_data = data.get("cost", {}).duplicate(true)
	_history_records = data.get("history_deductions", []).duplicate(true)
	_selected_hypothesis_id = ""
	_selected_hypothesis_data.clear()
	_selected_history_data.clear()
	_active_lines.clear()
	
	title_label.text = "脑内推演 · %s" % str(data.get("summary", data.get("title", "案情推演")))
	
	_populate_evidence(data.get("evidences", []))
	_populate_hypotheses(data.get("hypotheses", []))
	_populate_history()
	_update_cost_display()
	_update_submission_state()
	_switch_mode("current")
	
	if not Engine.is_editor_hint() and not data.get("spatial_clues", []).is_empty(): _setup_3d_visual_layer(data.get("spatial_clues", []))
	
	visible = true
	_is_open = true
	
	if Engine.is_editor_hint(): return
	await get_tree().process_frame
	close_btn.grab_focus()

func _switch_mode(mode: String) -> void:
	if mode == "current":
		three_columns.visible = true
		history_view.visible = false
		btn_current_mode.disabled = true
		btn_history_mode.disabled = false
		connection_canvas.visible = true
		_update_links()
	else:
		three_columns.visible = false
		history_view.visible = true
		btn_current_mode.disabled = false
		btn_history_mode.disabled = true
		connection_canvas.visible = false

## 7.D5: Setup temporary 3D visual layer in viewport
func _setup_3d_visual_layer(spatial_clues: Array) -> void:
	if is_instance_valid(_visual_3d):
		_visual_3d.cleanup()
		_visual_3d = null
		
	_visual_3d = DeductionVisual3D_Script.new()
	add_child(_visual_3d)
	
	for clue in spatial_clues:
		if clue is Dictionary:
			var cid := str(clue.get("id", ""))
			var pos: Vector3 = clue.get("position", Vector3.ZERO)
			_visual_3d.register_spatial_clue(cid, pos)

## Populates evidence panel (Task 7.D2)
func _populate_evidence(evidences: Array) -> void:
	_evidence_cards.clear()
	_evidence_data.clear()
	for child in evidence_list.get_children():
		evidence_list.remove_child(child)
		child.queue_free()
		
	if evidences.is_empty():
		var empty_lbl := Label.new()
		empty_lbl.text = "尚无相关证据"
		empty_lbl.add_theme_color_override("font_color", Chrome.MUTED)
		empty_lbl.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		evidence_list.add_child(empty_lbl)
		return
		
	for ev in evidences:
		if not ev is Dictionary:
			continue
		var ev_id: String = str(ev.get("id", ev.get("title", "ev_%d" % _evidence_cards.size())))
		_evidence_data[ev_id] = ev
		
		var card := PanelContainer.new()
		card.name = "EvCard_" + ev_id
		var box := _create_card_box(false, false)
		card.add_theme_stylebox_override("panel", box)
		
		var vbox := VBoxContainer.new()
		vbox.add_theme_constant_override("separation", 4)
		
		var title_row := HBoxContainer.new()
		var title_lbl := Label.new()
		title_lbl.text = str(ev.get("title", ev.get("label", "未命名证据")))
		title_lbl.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		title_lbl.add_theme_font_size_override("font_size", 17)
		title_row.add_child(title_lbl)
		
		var badge_holder := MarginContainer.new()
		badge_holder.name = "BadgeHolder"
		title_row.add_child(badge_holder)
		vbox.add_child(title_row)
		
		var body_lbl := Label.new()
		body_lbl.text = str(ev.get("body", ev.get("text", "")))
		body_lbl.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		body_lbl.add_theme_font_size_override("font_size", 15)
		vbox.add_child(body_lbl)
		
		var meta_lbl := Label.new()
		var source := str(ev.get("source", "未知来源"))
		var time_str := str(ev.get("time", ""))
		meta_lbl.text = "%s · %s" % [source, time_str] if not time_str.is_empty() else "%s" % source
		meta_lbl.add_theme_font_size_override("font_size", 13)
		meta_lbl.add_theme_color_override("font_color", Chrome.MUTED)
		vbox.add_child(meta_lbl)
		
		card.add_child(vbox)
		evidence_list.add_child(card)
		_evidence_cards[ev_id] = card

func _create_card_box(is_supported: bool, is_conflict: bool) -> StyleBoxFlat:
	var box := StyleBoxFlat.new()
	box.corner_radius_top_left = 2
	box.corner_radius_top_right = 2
	box.corner_radius_bottom_right = 2
	box.corner_radius_bottom_left = 2
	box.content_margin_left = 12.0
	box.content_margin_top = 8.0
	box.content_margin_right = 12.0
	box.content_margin_bottom = 8.0
	
	if is_conflict:
		box.bg_color = Color(0.24, 0.10, 0.10, 0.9)
		box.border_color = COLOR_CONFLICT
		box.set_border_width_all(1)
	elif is_supported:
		box.bg_color = Color(0.12, 0.22, 0.20, 0.9)
		box.border_color = COLOR_LINK
		box.set_border_width_all(1)
	else:
		box.bg_color = Color(Chrome.SURFACE, 0.68)
		box.border_width_left = 0
		box.border_width_top = 0
		box.border_width_right = 0
		box.border_width_bottom = 0
	return box

## Populates hypothesis panel (Task 7.D3)
func _populate_hypotheses(hypotheses: Array) -> void:
	_hypothesis_buttons.clear()
	for child in hypothesis_list.get_children():
		hypothesis_list.remove_child(child)
		child.queue_free()
		
	if hypotheses.is_empty():
		var empty_lbl := Label.new()
		empty_lbl.text = "尚无假设"
		empty_lbl.add_theme_color_override("font_color", Chrome.MUTED)
		empty_lbl.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		hypothesis_list.add_child(empty_lbl)
		return
		
	for hypo in hypotheses:
		if not hypo is Dictionary:
			continue
		var hypo_id := str(hypo.get("id", ""))
		var hypo_label := str(hypo.get("label", hypo.get("title", hypo_id)))
		var prereq := str(hypo.get("prerequisites", hypo.get("condition", "")))
		var desc := str(hypo.get("description", hypo.get("desc", "")))
		
		var btn := Button.new()
		Chrome.button(btn)
		btn.alignment = HORIZONTAL_ALIGNMENT_LEFT
		btn.text = hypo_label
		
		var tooltip_parts: Array[String] = []
		if not desc.is_empty():
			tooltip_parts.append(desc)
		if not prereq.is_empty():
			tooltip_parts.append("前提：" + prereq)
		btn.tooltip_text = "\n".join(tooltip_parts)
		
		btn.pressed.connect(func(): _on_hypothesis_selected(hypo_id, hypo))
		hypothesis_list.add_child(btn)
		_hypothesis_buttons[hypo_id] = btn

func _on_hypothesis_selected(hypo_id: String, hypo_data: Dictionary) -> void:
	_selected_hypothesis_id = hypo_id
	_selected_hypothesis_data = hypo_data
	
	selected_hypothesis_title.text = str(hypo_data.get("label", hypo_data.get("title", hypo_id)))
	var desc := str(hypo_data.get("description", ""))
	var prereq := str(hypo_data.get("prerequisites", ""))
	
	var details: Array[String] = []
	if not desc.is_empty():
		details.append("%s" % desc)
	if not prereq.is_empty():
		details.append("前提：%s" % prereq)
	selected_hypothesis_details.text = "\n".join(details)
	selected_hypothesis_details.visible = not details.is_empty()
	
	_update_links()
	_update_submission_state()

## 7.D5 & 7.D6: Update link lines and card highlights
func _update_links() -> void:
	_active_lines.clear()
	
	# Reset evidence cards appearance
	for ev_id in _evidence_cards:
		var card: PanelContainer = _evidence_cards[ev_id]
		card.add_theme_stylebox_override("panel", _create_card_box(false, false))
		var badge_holder: Node = card.find_child("BadgeHolder", true, false)
		if badge_holder != null:
			for c in badge_holder.get_children():
				badge_holder.remove_child(c)
				c.queue_free()
				
	conflict_notice_card.visible = false
	
	if _selected_hypothesis_id.is_empty():
		connection_canvas.queue_redraw()
		if is_instance_valid(_visual_3d):
			_visual_3d.set_lines([])
		return
		
	var hypo_btn: Button = _hypothesis_buttons.get(_selected_hypothesis_id, null)
	if hypo_btn == null or not is_instance_valid(hypo_btn):
		connection_canvas.queue_redraw()
		return
		
	var hypo_rect: Rect2 = hypo_btn.get_global_rect()
	var canvas_transform: Transform2D = connection_canvas.get_global_transform().affine_inverse()
	var hypo_left_pt: Vector2 = canvas_transform * Vector2(hypo_rect.position.x, hypo_rect.position.y + hypo_rect.size.y * 0.5)
	
	var supported_ids: Array = _selected_hypothesis_data.get("supported_by", [])
	var conflict_ids: Array = _selected_hypothesis_data.get("conflicts_with", [])
	var conflict_reason: String = str(_selected_hypothesis_data.get("conflict_reason", "与已知物证互斥"))
	
	var lines_3d: Array = []
	
	# 1. Supported Links (7.D5)
	for sid in supported_ids:
		var ev_id := str(sid)
		var card: PanelContainer = _evidence_cards.get(ev_id, null)
		if card != null and is_instance_valid(card):
			card.add_theme_stylebox_override("panel", _create_card_box(true, false))
			var badge_holder: Node = card.find_child("BadgeHolder", true, false)
			if badge_holder != null:
				var badge := _relation_badge("相容", Chrome.GREEN)
				badge_holder.add_child(badge)
				
			var card_rect: Rect2 = card.get_global_rect()
			var card_right_pt: Vector2 = canvas_transform * Vector2(card_rect.position.x + card_rect.size.x, card_rect.position.y + card_rect.size.y * 0.5)
			
			_active_lines.append({
				"from": card_right_pt,
				"to": hypo_left_pt,
				"is_conflict": false
			})
			lines_3d.append({"from_id": ev_id, "to_id": _selected_hypothesis_id, "is_conflict": false})
			
	# 2. Contradiction Links (7.D6)
	if not conflict_ids.is_empty():
		conflict_notice_card.visible = true
		conflict_notice_label.text = "%s" % conflict_reason
		
		for cid in conflict_ids:
			var ev_id := str(cid)
			var card: PanelContainer = _evidence_cards.get(ev_id, null)
			if card != null and is_instance_valid(card):
				card.add_theme_stylebox_override("panel", _create_card_box(false, true))
				var badge_holder: Node = card.find_child("BadgeHolder", true, false)
				if badge_holder != null:
					var badge := _relation_badge("矛盾", Chrome.RED)
					badge_holder.add_child(badge)
					
				var card_rect: Rect2 = card.get_global_rect()
				var card_right_pt: Vector2 = canvas_transform * Vector2(card_rect.position.x + card_rect.size.x, card_rect.position.y + card_rect.size.y * 0.5)
				
				_active_lines.append({
					"from": card_right_pt,
					"to": hypo_left_pt,
					"is_conflict": true
				})
				lines_3d.append({"from_id": ev_id, "to_id": _selected_hypothesis_id, "is_conflict": true})
				
	connection_canvas.queue_redraw()
	if is_instance_valid(_visual_3d):
		_visual_3d.set_lines(lines_3d)

func _on_connection_canvas_draw() -> void:
	for line in _active_lines:
		var p1: Vector2 = line.from
		var p2: Vector2 = line.to
		var is_conflict: bool = line.is_conflict
		var col: Color = COLOR_CONFLICT if is_conflict else COLOR_LINK
		var width: float = 3.0 if is_conflict else 2.5
		
		# Draw smooth horizontal S-curve
		var ctrl_offset: float = absf(p2.x - p1.x) * 0.5
		var ctrl1 := Vector2(p1.x + ctrl_offset, p1.y)
		var ctrl2 := Vector2(p2.x - ctrl_offset, p2.y)
		
		# Sample cubic Bezier curve points
		var points: PackedVector2Array = []
		var steps := 24
		for i in range(steps + 1):
			var t := float(i) / float(steps)
			var pt := p1.bezier_interpolate(ctrl1, ctrl2, p2, t)
			points.append(pt)
		connection_canvas.draw_polyline(points, col, width, true)
		
		# If conflict, draw alert icon dot at midpoint
		if is_conflict:
			var mid_pt := p1.bezier_interpolate(ctrl1, ctrl2, p2, 0.5)
			connection_canvas.draw_circle(mid_pt, 5.0, COLOR_CONFLICT)

## 7.D7: Populates historical deductions panel
func _populate_history() -> void:
	for child in history_list.get_children():
		history_list.remove_child(child)
		child.queue_free()
		
	if _history_records.is_empty():
		var empty_lbl := Label.new()
		empty_lbl.text = "尚无记录"
		empty_lbl.add_theme_color_override("font_color", Chrome.MUTED)
		history_list.add_child(empty_lbl)
		comparison_title.text = ""
		comparison_details.text = ""
		restore_historical_btn.disabled = true
		return
		
	for record in _history_records:
		if not record is Dictionary:
			continue
		var hid := str(record.get("id", ""))
		var title := str(record.get("title", "历史方案"))
		var time_str := str(record.get("timestamp", ""))
		var outcome := str(record.get("outcome", ""))
		
		var btn := Button.new()
		Chrome.button(btn)
		btn.alignment = HORIZONTAL_ALIGNMENT_LEFT
		btn.text = "%s  (%s)" % [title, time_str] if not time_str.is_empty() else title
		btn.tooltip_text = "推演结局: " + outcome
		btn.pressed.connect(func(): _on_history_selected(record))
		history_list.add_child(btn)

func _on_history_selected(record: Dictionary) -> void:
	_selected_history_data = record
	var title := str(record.get("title", "历史方案"))
	var hypo_id := str(record.get("hypothesis_id", ""))
	var outcome := str(record.get("outcome", "未注记"))
	var supported: Array = record.get("supported_evidences", [])
	
	comparison_title.text = "%s" % title
	
	var diff_lines: Array[String] = []
	diff_lines.append("当时：%s" % _hypothesis_label(hypo_id))
	diff_lines.append("当前：%s" % (_hypothesis_label(_selected_hypothesis_id) if not _selected_hypothesis_id.is_empty() else "未选定"))
	diff_lines.append("结论：%s" % outcome)
	diff_lines.append("证据：%s" % _evidence_labels(supported))
	
	comparison_details.text = "\n".join(diff_lines)
	restore_historical_btn.disabled = false

func _on_restore_historical_pressed() -> void:
	if _selected_history_data.is_empty():
		return
	var hypo_id := str(_selected_history_data.get("hypothesis_id", ""))
	_switch_mode("current")
	
	# Select hypothesis if available
	for child in hypothesis_list.get_children():
		if child is Button and _hypothesis_buttons.get(hypo_id, null) == child:
			child.emit_signal("pressed")
			break

## Updates cost display (Task 7.D4)
func _update_cost_display() -> void:
	_cost_valid = true
	var time_val: Variant = _cost_data.get("time", null)
	var comp_val: Variant = _cost_data.get("computation", null)
	var stam_val: Variant = _cost_data.get("stamina", null)
	
	if time_val is int or time_val is float:
		cost_time_label.text = "%d 秒" % int(time_val)
	else:
		cost_time_label.text = "未知"
		_cost_valid = false
		
	if comp_val is int or comp_val is float:
		cost_comp_label.text = "%d 算力" % int(comp_val)
	else:
		cost_comp_label.text = "未知"
		_cost_valid = false
		
	if stam_val is int or stam_val is float:
		cost_stam_label.text = "%d 点" % int(stam_val)
	else:
		cost_stam_label.text = "未知"
		_cost_valid = false
		
	if not _cost_valid:
		cost_warning_label.text = "代价未明"
		cost_warning_label.visible = true
	else:
		cost_warning_label.visible = false

func _update_submission_state() -> void:
	var can_submit: bool = _cost_valid and not _selected_hypothesis_id.is_empty()
	submit_btn.disabled = not can_submit
	if _selected_hypothesis_id.is_empty():
		selected_hypothesis_title.text = "尚未选择假设"
		selected_hypothesis_details.visible = false

func _on_submit_pressed() -> void:
	if not _cost_valid or _selected_hypothesis_id.is_empty(): return
	_on_confirmed_submission()

func _on_confirmed_submission() -> void:
	if not _is_open:
		return
	var hypo_id := _selected_hypothesis_id
	var payload := {
		"action_id": _action_id,
		"hypothesis_id": hypo_id,
		"cost": _cost_data
	}
	exit_deduction()
	deduction_submitted.emit(hypo_id, payload)

## Strict recovery without physical side-effects (Task 7.D8)
## Cleans up all 2D links, 3D temporary layers, and restores original scene.
func exit_deduction() -> void:
	if not _is_open:
		return
	_is_open = false
	visible = false
	_navigation.reset()
	confirm_dialog.hide()
	# Clean up 2D connection state
	_active_lines.clear()
	if is_instance_valid(connection_canvas):
		connection_canvas.queue_redraw()
		
	# Clean up 3D visual layer (Task 7.D8: no orphan nodes, zero physical residue)
	if is_instance_valid(_visual_3d):
		_visual_3d.cleanup()
		_visual_3d = null
		
	deduction_closed.emit()

func _input(event: InputEvent) -> void:
	if Engine.is_editor_hint() or not _is_open: return
	if UIInput.is_back(event):
		exit_deduction()
		get_viewport().set_input_as_handled()
	elif _navigation.handle(main_card, event):
		get_viewport().set_input_as_handled()

func _process(delta: float) -> void:
	if _is_open: _navigation.tick(main_card, delta)

func _fit_workspace() -> void:
	if not is_instance_valid(main_card): return
	var screen := get_viewport_rect().size
	main_card.custom_minimum_size = Vector2(minf(1080, screen.x - 48), minf(680, screen.y - 64))

func _relation_badge(text: String, color: Color) -> Label:
	var label := Label.new()
	label.text = text
	label.add_theme_font_size_override("font_size", 13)
	label.add_theme_color_override("font_color", color)
	return label

func _hypothesis_label(id: String) -> String:
	var button: Button = _hypothesis_buttons.get(id)
	return button.text if button != null else "假设"

func _evidence_labels(ids: Array) -> String:
	var labels: Array[String] = []
	for id in ids:
		var data: Dictionary = _evidence_data.get(str(id), {})
		var label := str(data.get("title", data.get("label", "")))
		if not label.is_empty(): labels.append(label)
	return "、".join(labels)
