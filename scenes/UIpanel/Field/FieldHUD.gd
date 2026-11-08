extends CanvasLayer
const Sound := preload("res://scenes/components/shared/Sound.gd")

## Persistent investigation overlay aligned with five-day Core HUD presentation.
## Reads live facts, objectives, targets, and action clock from CampaignRoomHost.

signal book_open_requested
signal action_cancelled(action_id: String)
signal settings_requested
signal perception_inspect_requested(record: Dictionary)

@onready var top_bar: PanelContainer = $TopBar
@onready var room_label: Label = $TopBar/MarginContainer/HBoxContainer/TitleBox/RoomLabel
@onready var time_label: Label = $TopBar/MarginContainer/HBoxContainer/TitleBox/TimeLabel
@onready var mode_label: Label = $TopBar/MarginContainer/HBoxContainer/ModeLabel
@onready var pause_button: Button = $TopBar/MarginContainer/HBoxContainer/ControlsBox/PauseButton
@onready var speed1_button: Button = $TopBar/MarginContainer/HBoxContainer/ControlsBox/Speed1Button
@onready var speed16_button: Button = $TopBar/MarginContainer/HBoxContainer/ControlsBox/Speed16Button
@onready var speed128_button: Button = $TopBar/MarginContainer/HBoxContainer/ControlsBox/Speed128Button
@onready var speed1024_button: Button = $TopBar/MarginContainer/HBoxContainer/ControlsBox/Speed1024Button
@onready var ai_button: Button = $TopBar/MarginContainer/HBoxContainer/ControlsBox/AIButton
@onready var book_button: Button = $TopBar/MarginContainer/HBoxContainer/ControlsBox/BookButton
@onready var save_button: Button = $TopBar/MarginContainer/HBoxContainer/ControlsBox/SaveButton

@onready var snackbar: PanelContainer = $TopCenter/Snackbar
@onready var snackbar_label: Label = $TopCenter/Snackbar/MarginContainer/SnackbarLabel

@onready var side_panel: PanelContainer = $SidePanel
@onready var task_title: Label = $SidePanel/Margin/Sections/TaskTitle
@onready var task_status: Label = $SidePanel/Margin/Sections/TaskStatus
@onready var physiology_summary: Label = $SidePanel/Margin/Sections/PhysiologySummary
@onready var intent_button: Button = $SidePanel/Margin/Sections/IntentButton
@onready var action_title: Label = $SidePanel/Margin/Sections/ActionTitle
@onready var action_hint: Label = $SidePanel/Margin/Sections/ActionHint
@onready var target_container: VBoxContainer = $SidePanel/Margin/Sections/TargetScroll/TargetContainer
@onready var actions_container: VBoxContainer = $SidePanel/Margin/Sections/ActionsScroll/ActionsContainer
@onready var footer_hint: Label = $SidePanel/Margin/Sections/FooterHint

@onready var footer_panel: PanelContainer = $FooterPanel
@onready var feedback_label: Label = $FooterPanel/Margin/FooterContent/FeedbackLabel
@onready var action_progress_box: VBoxContainer = $FooterPanel/Margin/FooterContent/ActionProgressBox
@onready var action_name_label: Label = $FooterPanel/Margin/FooterContent/ActionProgressBox/ActionNameLabel
@onready var progress_bar: ProgressBar = $FooterPanel/Margin/FooterContent/ActionProgressBox/ProgressBar

const Casebook := preload("res://scenes/components/shared/MaterialControls.gd")
const GOLD := Casebook.COLOR_PRIMARY        # rubric (oxblood)
const TEXT_LIGHT := Casebook.COLOR_ON_SURFACE  # ink
const TEXT_MUTED := Casebook.INK_SOFT
const TEXT_GOLD := Casebook.COLOR_PRIMARY

var _snackbar_timer := 0.0
var _presentation_timer := 0.0
var _selected_target_id := ""

func _ready() -> void:
	layer = 10
	process_mode = Node.PROCESS_MODE_ALWAYS
	_apply_styles()
	_connect_controls()
	snackbar.hide()
	action_progress_box.hide()
	_refresh_presentation()

func _process(delta: float) -> void:
	_presentation_timer -= delta
	if _presentation_timer <= 0.0:
		_presentation_timer = 0.25
		_refresh_presentation()
	if _snackbar_timer > 0.0:
		_snackbar_timer -= delta
		if _snackbar_timer <= 0.0:
			var tw := create_tween()
			tw.tween_property(snackbar, "modulate:a", 0.0, 0.3)
			tw.tween_callback(snackbar.hide)

func _get_active_field() -> Node:
	var main := get_parent()
	if main == null: return null
	var field: Node = main.get("field")
	return field if is_instance_valid(field) else null

func _connect_controls() -> void:
	book_button.pressed.connect(func(): Sound.sfx("ui_click"); book_open_requested.emit())
	intent_button.pressed.connect(func(): Sound.sfx("ui_click"); book_open_requested.emit())

	pause_button.pressed.connect(func():
		var field := _get_active_field()
		if field != null and "player_paused" in field:
			field.set_paused(not field.player_paused)
			_refresh_presentation()
	)

	speed1_button.pressed.connect(func():
		var field := _get_active_field()
		if field != null and field.has_method("set_speed"):
			field.set_speed(1.0)
			_refresh_presentation()
	)

	speed16_button.pressed.connect(func():
		var field := _get_active_field()
		if field != null and field.has_method("set_speed"):
			field.set_speed(16.0)
			_refresh_presentation()
	)

	speed128_button.pressed.connect(func():
		var field := _get_active_field()
		if field != null and field.has_method("set_speed"):
			field.set_speed(128.0)
			_refresh_presentation()
	)

	speed1024_button.pressed.connect(func():
		var field := _get_active_field()
		if field != null and field.has_method("set_speed"):
			field.set_speed(1024.0)
			_refresh_presentation()
	)

	ai_button.pressed.connect(func():
		var field := _get_active_field()
		if field != null and "controller_ai" in field:
			field.set_control_ai(not field.controller_ai)
			_refresh_presentation()
	)

	save_button.pressed.connect(func():
		var field := _get_active_field()
		if field != null and field.has_method("save_game"):
			field.save_game()
			_refresh_presentation()
	)

func _apply_styles() -> void:
	var hud_font: Font
	var font_path := "res://assets/external/NotoSerifSC/NotoSerifSC.ttf"
	if ResourceLoader.exists(font_path): hud_font = load(font_path)

	# Printed casebook cards: square, ink-ruled, resting on the scene.
	top_bar.add_theme_stylebox_override("panel", Casebook.ledger_panel(0.97))
	side_panel.add_theme_stylebox_override("panel", Casebook.ledger_panel(0.95))
	footer_panel.add_theme_stylebox_override("panel", Casebook.ledger_panel(0.95))
	snackbar.add_theme_stylebox_override("panel", Casebook.ledger_panel(0.98))
	snackbar_label.add_theme_color_override("font_color", TEXT_LIGHT)
	var track := Casebook._box(Color(Casebook.INK_SOFT, 0.18), 0, 2)
	var fill := Casebook._box(Casebook.COLOR_PRIMARY, 0, 2)
	track.anti_aliasing = false
	fill.anti_aliasing = false
	progress_bar.add_theme_stylebox_override("background", track)
	progress_bar.add_theme_stylebox_override("fill", fill)
	progress_bar.show_percentage = false

	# Colors & Fonts
	room_label.add_theme_color_override("font_color", TEXT_LIGHT)
	time_label.add_theme_color_override("font_color", GOLD)
	mode_label.add_theme_color_override("font_color", TEXT_MUTED)

	task_title.add_theme_color_override("font_color", TEXT_GOLD)
	task_status.add_theme_color_override("font_color", TEXT_MUTED)
	physiology_summary.add_theme_color_override("font_color", TEXT_MUTED)

	action_title.add_theme_color_override("font_color", TEXT_LIGHT)
	action_hint.add_theme_color_override("font_color", TEXT_MUTED)
	footer_hint.add_theme_color_override("font_color", TEXT_MUTED)
	feedback_label.add_theme_color_override("font_color", TEXT_MUTED)
	action_name_label.add_theme_color_override("font_color", TEXT_LIGHT)

	for btn in [pause_button, speed1_button, speed16_button, speed128_button, speed1024_button, ai_button, book_button, save_button, intent_button]:
		_style_button(btn, hud_font)

	if hud_font != null:
		for lbl in [room_label, time_label, mode_label, task_title, task_status, physiology_summary, action_title, action_hint, footer_hint, feedback_label, action_name_label]:
			lbl.add_theme_font_override("font", hud_font)

func _style_button(btn: Button, font: Font, selected := false) -> void:
	Casebook.style_ledger_button(btn, font, 15, selected)

func _refresh_presentation() -> void:
	if not is_inside_tree(): return
	var field := _get_active_field()
	if field == null: return

	# 1. Update Room & Time
	var room_id := str(field.get("room_id"))
	if field.has_method("room_label") and not room_id.is_empty():
		var r_name := str(field.room_label(room_id))
		room_label.text = r_name if not r_name.is_empty() else room_id
	else:
		room_label.text = room_id

	var state: Dictionary = field.get("state") if field.get("state") is Dictionary else {}
	var seconds := float(state.get("game_seconds", -1.0))
	var speed := float(field.get("time_scale")) if "time_scale" in field else 1.0

	if seconds >= 0.0:
		var day := maxi(1, int(seconds / 86400.0) + 1)
		var minute_of_day := posmod(int(seconds / 60.0), 1440)
		var speed_text := str(int(speed)) if speed >= 1.0 else "1"
		time_label.text = "第 %d 日   %02d:%02d   ·   %s×" % [day, int(minute_of_day / 60), posmod(minute_of_day, 60), speed_text]
	else:
		time_label.text = "第 1 日   07:00   ·   1×"

	# 2. Update Mode & Buttons
	var is_ai: bool = bool(field.get("controller_ai")) if "controller_ai" in field else false
	var is_paused: bool = bool(field.get("player_paused")) if "player_paused" in field else false
	mode_label.text = "AI 控制中" if is_ai else "侦探接管"
	pause_button.text = "继续" if is_paused else "暂停"
	ai_button.text = "接管" if is_ai else "交给 AI"

	# 3. Observation & Intent
	var info: Dictionary = field.interface_observation() if field.has_method("interface_observation") else {}
	var player_paths: Dictionary = info.get("player_paths", {})
	var task: Dictionary = player_paths.get("task", {})
	var task_lbl := str(task.get("label", "尚无已解锁任务"))
	task_title.text = task_lbl if not task_lbl.is_empty() else "尚无已解锁任务"

	var task_reason := str(player_paths.get("reason", "当前没有已解锁任务，继续日常判断。"))
	task_status.text = "尚无已知任务\n当前建议：" + task_reason

	var physiology: Dictionary = player_paths.get("physiology", {})
	var phys_status := str(physiology.get("status", "satisfied"))
	physiology_summary.text = "身体状态平稳" if phys_status == "satisfied" else "身体感到疲乏或不适"

	# 4. Action / Progress
	var progress_dict: Dictionary = field.visible_progress(field._viewer()) if field.has_method("visible_progress") and field.has_method("_viewer") else {}
	var has_progress := not progress_dict.is_empty() and float(progress_dict.get("progress", 0.0)) < 1.0

	if has_progress:
		var act_label := str(field.visible_activity_label(field._viewer())) if field.has_method("visible_activity_label") and field.has_method("_viewer") else "行动中"
		action_name_label.text = act_label + "…"
		progress_bar.value = float(progress_dict.get("progress", 0.0))
		action_progress_box.show()
		feedback_label.hide()
	else:
		action_progress_box.hide()
		feedback_label.show()
		var fb := str(field.get("feedback")) if "feedback" in field else ""
		feedback_label.text = fb if not fb.is_empty() else "WASD 行走 · 点击物件或按 E 查看行动 · 滚轮缩放 · Tab 案卷 · 右键返回"

	# 5. Populate Targets & Actions in SidePanel
	var targets: Array = info.get("targets", []) if info.get("targets", []) is Array else []
	var selected_focus: String = str(field.get("selected_focus")) if "selected_focus" in field else ""
	if _selected_target_id.is_empty() and not targets.is_empty():
		_selected_target_id = str(targets[0].get("id", ""))
	if not selected_focus.is_empty():
		_selected_target_id = selected_focus.split(".")[0]

	_render_targets_and_actions(targets, field)

func _render_targets_and_actions(targets: Array, field: Node) -> void:
	for child in target_container.get_children():
		child.queue_free()
	for child in actions_container.get_children():
		child.queue_free()

	var hud_font: Font
	var font_path := "res://assets/external/NotoSerifSC/NotoSerifSC.ttf"
	if ResourceLoader.exists(font_path): hud_font = load(font_path)

	var active_target_dict: Dictionary = {}

	for target in targets:
		if not target is Dictionary: continue
		var t_id: String = str(target.get("id", ""))
		var t_label: String = str(target.get("label", t_id))
		var t_actions: Array = target.get("actions", [])
		var btn_text := "%s  ·  %d 项" % [t_label, t_actions.size()]

		var t_btn := Button.new()
		t_btn.text = btn_text
		_style_button(t_btn, hud_font, t_id == _selected_target_id)
		if t_id == _selected_target_id:
			active_target_dict = target

		t_btn.pressed.connect(func():
			Sound.sfx("ui_click", -8.0)
			_selected_target_id = t_id
			if field.has_method("select_focus"): field.select_focus(t_id)
			_refresh_presentation()
		)
		target_container.add_child(t_btn)

	# Populate actions for active target
	if not active_target_dict.is_empty():
		var acts: Array = active_target_dict.get("actions", [])
		for act in acts:
			if not act is Dictionary: continue
			var act_id: String = str(act.get("id", ""))
			var act_label: String = str(act.get("label", act_id))

			var act_btn := Button.new()
			act_btn.text = act_label
			_style_button(act_btn, hud_font)
			act_btn.text = "›  " + act_label

			act_btn.pressed.connect(func():
				Sound.sfx("ui_click")
				if field.has_method("request_action"):
					field.request_action(act_id)
					_refresh_presentation()
			)
			actions_container.add_child(act_btn)

func update_hud(time_str: String, location_str: String, _erosion_level: float) -> void:
	if not time_str.is_empty(): time_label.text = time_str
	if not location_str.is_empty(): room_label.text = location_str

func show_snackbar(message: String, kind: String = "info") -> void:
	snackbar_label.text = message
	match kind:
		"warning": snackbar_label.modulate = Color("e89a3c")
		"error": snackbar_label.modulate = Color("e04b4b")
		_: snackbar_label.modulate = Color("ffffff")
	snackbar.modulate.a = 1.0
	snackbar.show()
	_snackbar_timer = 2.5
