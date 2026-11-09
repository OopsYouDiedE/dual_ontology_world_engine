extends Control

## 现场引导面板（右上角）：目标、任务进度与阻碍、推荐下一步（可直接点击执行）、
## 本人身体需要（Sims 式需要条）、时间流速（暂停 / 1 / 2 / 3 档）与“侦探自主”托管开关。
## 只读 FieldGUI 转交的 interface 字典；所有操作只发出意图信号，不认识现场宿主与会话桥。
## 快捷键：空格暂停、1/2/3 流速、Enter 执行第一项推荐、F6 切换托管。

signal action_requested(action_id: String)
signal speed_requested(speed: float)
signal pause_requested(paused: bool)
signal autonomy_requested(enabled: bool)

const SPEEDS := [60.0, 240.0, 960.0]
const SPEED_NAMES := ["▶", "▶▶", "▶▶▶"]
const FONT := preload("res://assets/external/NotoSerifSC/NotoSerifSC.ttf")
const PAPER := Color(0.07, 0.062, 0.055, 0.84)
const INK := Color("efe4cc")
const MUTED := Color("a99c82")
const GOLD := Color("d9b56c")
const NEED_ORDER := ["energy", "hunger", "thirst", "hygiene", "social", "comfort"]
const NEED_LABELS := {"energy": "精力", "hunger": "饱食", "thirst": "水分", "hygiene": "清洁", "social": "社交", "comfort": "舒适"}

var _objective: Label
var _task: Label
var _task_bar: ProgressBar
var _status: Label
var _next_title: Label
var _next_box: VBoxContainer
var _needs_grid: GridContainer
var _need_bars: Dictionary = {}
var _speed_buttons: Array = []
var _pause_button: Button
var _auto_button: CheckButton
var _actions_signature := "-"
var _first_action := ""
var _paused := false
var _speed := 60.0
var _auto := false
var _syncing := false

func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	var panel := PanelContainer.new()
	panel.name = "GuidancePanel"
	panel.add_theme_stylebox_override("panel", _paper_style())
	panel.anchor_left = 1.0
	panel.anchor_right = 1.0
	panel.offset_left = -378.0
	panel.offset_right = -18.0
	panel.offset_top = 18.0
	panel.offset_bottom = 18.0
	panel.grow_horizontal = Control.GROW_DIRECTION_BEGIN
	panel.grow_vertical = Control.GROW_DIRECTION_END
	panel.mouse_filter = Control.MOUSE_FILTER_STOP
	add_child(panel)
	var column := VBoxContainer.new()
	column.add_theme_constant_override("separation", 7)
	panel.add_child(column)
	_objective = _make_label(column, 18, GOLD)
	_task = _make_label(column, 15, INK)
	_task_bar = ProgressBar.new()
	_task_bar.custom_minimum_size = Vector2(0, 6)
	_task_bar.show_percentage = false
	_task_bar.add_theme_stylebox_override("background", _bar_style(Color(1, 1, 1, 0.08)))
	_task_bar.add_theme_stylebox_override("fill", _bar_style(GOLD))
	column.add_child(_task_bar)
	_status = _make_label(column, 13, MUTED)
	_next_title = _make_label(column, 14, GOLD)
	_next_box = VBoxContainer.new()
	_next_box.add_theme_constant_override("separation", 4)
	column.add_child(_next_box)
	column.add_child(HSeparator.new())
	var needs_title := _make_label(column, 14, GOLD)
	needs_title.text = "身体需要"
	_needs_grid = GridContainer.new()
	_needs_grid.columns = 2
	_needs_grid.add_theme_constant_override("h_separation", 10)
	_needs_grid.add_theme_constant_override("v_separation", 4)
	column.add_child(_needs_grid)
	column.add_child(HSeparator.new())
	var speed_row := HBoxContainer.new()
	speed_row.add_theme_constant_override("separation", 6)
	column.add_child(speed_row)
	_pause_button = _make_button(speed_row, "Ⅱ", "暂停 / 继续（空格）")
	_pause_button.pressed.connect(_on_pause_pressed)
	for index in SPEEDS.size():
		var button := _make_button(speed_row, SPEED_NAMES[index], "流速 %d 档（数字键 %d）" % [index + 1, index + 1])
		var value: float = SPEEDS[index]
		button.pressed.connect(func(): speed_requested.emit(value))
		_speed_buttons.append(button)
	_auto_button = CheckButton.new()
	_auto_button.text = "侦探自主"
	_auto_button.focus_mode = Control.FOCUS_NONE
	_auto_button.tooltip_text = "交给侦探按任务与身体需要自行行动（F6）"
	_auto_button.add_theme_font_override("font", FONT)
	_auto_button.add_theme_font_size_override("font_size", 14)
	_auto_button.add_theme_color_override("font_color", INK)
	_auto_button.toggled.connect(_on_auto_toggled)
	speed_row.add_child(_auto_button)

func refresh(iface: Dictionary) -> void:
	var campaign: Dictionary = iface.get("campaign_progress", {}) if iface.get("campaign_progress") is Dictionary else {}
	var phase_info: Dictionary = campaign.get("phase", {}) if campaign.get("phase") is Dictionary else {}
	var objective := str(iface.get("objective", ""))
	_objective.text = objective if not objective.is_empty() else "调查进行中"
	var paths: Dictionary = iface.get("player_paths", {}) if iface.get("player_paths") is Dictionary else {}
	var task: Dictionary = paths.get("task", {}) if paths.get("task") is Dictionary else {}
	var open_day := bool(phase_info.get("active", false)) and not bool(phase_info.get("task_guidance", true))
	if not open_day and not task.is_empty():
		var done := int(task.get("completed_steps", 0))
		var total := maxi(1, int(task.get("total_steps", 1)))
		_task.text = "任务 · %s  %d/%d" % [str(task.get("label", task.get("id", ""))), done, total]
		_task_bar.max_value = total
		_task_bar.value = done
		_task_bar.visible = true
		var blockers: Array = task.get("blockers", []) if task.get("blockers") is Array else []
		_status.text = str(blockers[0]) if not blockers.is_empty() else str(task.get("reason", paths.get("reason", "")))
	else:
		_task.text = str(phase_info.get("label", "自主安排")) if open_day else "暂无进行中的任务"
		_task_bar.visible = false
		_status.text = str(phase_info.get("description", paths.get("reason", "")))
	var completed: Dictionary = paths.get("completed_tasks", {}) if paths.get("completed_tasks") is Dictionary else {}
	if int(completed.get("count", 0)) > 0: _status.text += "\n已完成任务 %d 项" % int(completed.get("count", 0))
	_refresh_next(iface, paths, task)
	_refresh_needs(iface, paths)
	_paused = bool(iface.get("paused", false))
	_speed = float(iface.get("speed", 60.0))
	_auto = bool(iface.get("controller_ai", false))
	_pause_button.modulate = GOLD if _paused else INK
	for index in _speed_buttons.size():
		var active := not _paused and absf(_speed - float(SPEEDS[index])) < 0.5
		(_speed_buttons[index] as Button).modulate = GOLD if active else Color(INK, 0.7)
	_syncing = true
	_auto_button.button_pressed = _auto
	_syncing = false

func _unhandled_key_input(event: InputEvent) -> void:
	if not is_visible_in_tree() or not event is InputEventKey or not event.pressed or event.echo: return
	match (event as InputEventKey).keycode:
		KEY_SPACE: pause_requested.emit(not _paused)
		KEY_1: speed_requested.emit(SPEEDS[0])
		KEY_2: speed_requested.emit(SPEEDS[1])
		KEY_3: speed_requested.emit(SPEEDS[2])
		KEY_F6: autonomy_requested.emit(not _auto)
		KEY_ENTER, KEY_KP_ENTER:
			if _first_action.is_empty(): return
			action_requested.emit(_first_action)
		_: return
	get_viewport().set_input_as_handled()

func _on_pause_pressed() -> void:
	pause_requested.emit(not _paused)

func _on_auto_toggled(enabled: bool) -> void:
	if not _syncing: autonomy_requested.emit(enabled)

func _refresh_next(iface: Dictionary, paths: Dictionary, task: Dictionary) -> void:
	var choices: Array = iface.get("choices", []) if iface.get("choices") is Array else []
	var by_id := {}
	for choice in choices:
		if choice is Dictionary: by_id[str(choice.get("id", choice.get("action_id", "")))] = choice
	var wanted: Array = []
	var physiology: Dictionary = paths.get("physiology", {}) if paths.get("physiology") is Dictionary else {}
	for source in [physiology.get("recommended_actions", []), task.get("recommended_actions", [])]:
		if not source is Array: continue
		for entry in source:
			var id := str(entry.get("id", entry.get("action", ""))) if entry is Dictionary else str(entry)
			if by_id.has(id) and not wanted.has(id): wanted.append(id)
	var recommended := not wanted.is_empty()
	if not recommended:
		for choice in choices:
			if wanted.size() >= 5: break
			if choice is Dictionary and bool(choice.get("available", true)):
				wanted.append(str(choice.get("id", choice.get("action_id", ""))))
	_next_title.text = "推荐下一步（Enter 执行第一项）" if recommended else "当前可做的事（点击即前往）"
	_first_action = str(wanted[0]) if not wanted.is_empty() else ""
	var signature := ",".join(PackedStringArray(wanted))
	if signature == _actions_signature: return
	_actions_signature = signature
	for child in _next_box.get_children(): child.queue_free()
	if wanted.is_empty():
		var idle := _make_label(_next_box, 13, MUTED)
		idle.text = "暂时没有可做的事；可加快时间，等待现场变化。"
		return
	for id in wanted:
		var choice: Dictionary = by_id.get(id, {})
		var button := _make_button(_next_box, "› " + str(choice.get("label", id)), str(choice.get("hint", "")))
		button.alignment = HORIZONTAL_ALIGNMENT_LEFT
		button.clip_text = true
		button.custom_minimum_size = Vector2(0, 28)
		var action_id := str(id)
		button.pressed.connect(func(): action_requested.emit(action_id))

func _refresh_needs(iface: Dictionary, paths: Dictionary) -> void:
	var physiology: Dictionary = paths.get("physiology", {}) if paths.get("physiology") is Dictionary else {}
	var entries: Array = []
	if physiology.get("needs") is Array:
		for need in physiology.get("needs"):
			if need is Dictionary: entries.append(need)
	var raw: Dictionary = iface.get("needs", {}) if iface.get("needs") is Dictionary else {}
	for id in NEED_ORDER:
		var listed := false
		for entry in entries:
			if str(entry.get("id", "")) == id: listed = true
		if not listed and raw.has(id):
			entries.append({"id": id, "label": NEED_LABELS.get(id, id), "value": raw[id], "critical_below": 20})
	for entry in entries.slice(0, 6):
		var id := str(entry.get("id", ""))
		var bar: ProgressBar = _need_bars.get(id)
		if bar == null:
			var name_label := _make_label(_needs_grid, 13, INK)
			name_label.text = str(entry.get("label", NEED_LABELS.get(id, id)))
			bar = ProgressBar.new()
			bar.custom_minimum_size = Vector2(170, 10)
			bar.size_flags_horizontal = Control.SIZE_EXPAND_FILL
			bar.size_flags_vertical = Control.SIZE_SHRINK_CENTER
			bar.show_percentage = false
			bar.max_value = 100
			bar.add_theme_stylebox_override("background", _bar_style(Color(1, 1, 1, 0.08)))
			_needs_grid.add_child(bar)
			_need_bars[id] = bar
		var value := clampf(float(entry.get("value", 0.0)), 0.0, 100.0)
		bar.value = value
		var critical := float(entry.get("critical_below", 20.0))
		var color := Color("7fc77a") if value >= 60.0 else (Color("d9c25a") if value >= critical + 10.0 else Color("d9694f"))
		if bar.get_meta("fill_color", Color.TRANSPARENT) != color:
			bar.set_meta("fill_color", color)
			bar.add_theme_stylebox_override("fill", _bar_style(color))
		bar.tooltip_text = "%s %.0f / 100（低于 %.0f 时会优先照护）" % [str(entry.get("label", id)), value, critical]

func _paper_style() -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = PAPER
	style.border_color = Color(GOLD, 0.45)
	style.set_border_width_all(1)
	style.set_corner_radius_all(10)
	style.content_margin_left = 16; style.content_margin_right = 16
	style.content_margin_top = 12; style.content_margin_bottom = 14
	style.shadow_color = Color(0, 0, 0, 0.35)
	style.shadow_size = 12
	return style

func _bar_style(color: Color) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = color
	style.set_corner_radius_all(4)
	return style

func _make_label(parent: Node, size: int, color: Color) -> Label:
	var label := Label.new()
	label.add_theme_font_override("font", FONT)
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", color)
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(label)
	return label

func _make_button(parent: Node, text: String, tip: String) -> Button:
	var button := Button.new()
	button.text = text
	button.tooltip_text = tip
	button.focus_mode = Control.FOCUS_NONE
	button.add_theme_font_override("font", FONT)
	button.add_theme_font_size_override("font_size", 14)
	button.add_theme_color_override("font_color", INK)
	button.add_theme_color_override("font_hover_color", GOLD)
	var normal := StyleBoxFlat.new()
	normal.bg_color = Color(1, 1, 1, 0.05)
	normal.set_corner_radius_all(6)
	normal.content_margin_left = 10; normal.content_margin_right = 10
	normal.content_margin_top = 4; normal.content_margin_bottom = 4
	var hover := normal.duplicate() as StyleBoxFlat
	hover.bg_color = Color(GOLD, 0.16)
	button.add_theme_stylebox_override("normal", normal)
	button.add_theme_stylebox_override("hover", hover)
	button.add_theme_stylebox_override("pressed", hover)
	parent.add_child(button)
	return button
