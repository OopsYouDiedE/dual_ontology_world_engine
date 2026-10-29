extends CanvasLayer

signal settings_requested

const Transition := preload("res://scenes/UI/UITransition.gd")

@onready var top_bar: PanelContainer = %TopBar
@onready var room_label: Label = %RoomLabel
@onready var target_label: Label = %TargetLabel
@onready var action_label: Label = %ActionLabel
@onready var status_label: Label = %StatusLabel
@onready var time_label: Label = %TimeLabel
@onready var danger_badge: PanelContainer = %DangerBadge
@onready var danger_label: Label = %DangerLabel
@onready var hud_settings_button: Button = %HudSettingsButton
@onready var hint_label: Label = %HintLabel
@onready var hover_tip: Label = %HoverTip

var _bar_motion = Transition.new()
var _hint_motion = Transition.new()
var _hover_motion = Transition.new()
var _overlay_blocked := false
var _shutting_down := false
var _hint_text := ""
var _hover_serial := 0
var _last_snapshot: Dictionary = {}
var _last_danger_level: Variant = null

func _ready() -> void:
	layer = 10
	for control: Control in [room_label, target_label, action_label, status_label, time_label, danger_label, hint_label, hover_tip, hud_settings_button]:
		control.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	_refresh_chrome()
	# The field's A/Enter action belongs to the nearby object. The guide remains
	# mouse-accessible and has dedicated keyboard/controller shortcuts.
	hud_settings_button.focus_mode = Control.FOCUS_NONE
	hud_settings_button.pressed.connect(func():
		if not _overlay_blocked and not _shutting_down: settings_requested.emit()
	)
	_bar_motion.configure(self, top_bar, top_bar, null, false, 1.0)
	_hint_motion.configure(self, hint_label, hint_label, null, false, 1.0)
	_hover_motion.configure(self, hover_tip, hover_tip, null, false, 1.0)
	_hint_motion.completed.connect(func(_id: int, shown: bool, cancelled: bool):
		if not shown and not cancelled and _hint_text.is_empty(): hint_label.text = ""
	)
	_bar_motion.request(true)
	var settings := get_node_or_null("/root/GameSettings")
	if settings != null: settings.setting_changed.connect(_on_setting_changed)

func _notification(what: int) -> void:
	if what == NOTIFICATION_TRANSLATION_CHANGED and is_node_ready(): _refresh_chrome()

func _refresh_chrome() -> void:
	var locale := TranslationServer.get_locale()
	hud_settings_button.text = "凡例" if locale.begins_with("zh") or locale.begins_with("ja") else "Guide"
	if not _last_snapshot.is_empty(): apply_snapshot(_last_snapshot)

func apply_snapshot(snapshot: Dictionary) -> void:
	_last_snapshot = snapshot
	var hud: Dictionary = snapshot.get("hud", {})
	room_label.text = str(hud.get("room", hud.get("title", "")))
	room_label.tooltip_text = room_label.text
	
	# Current target (Task 7.03) - only display known/obtained target, no spoiler
	var raw_target: Variant = hud.get("target", snapshot.get("target", snapshot.get("selected_target", "")))
	var target_str := str(raw_target)
	if not target_str.is_empty() and target_str != "null" and target_str != "<null>":
		target_label.text = "目标: " + target_str
		target_label.tooltip_text = target_str
		target_label.visible = true
	else:
		target_label.visible = false
		
	# Currently executing action (Task 7.03)
	var raw_action: Variant = hud.get("action", hud.get("current_action", snapshot.get("current_action", "")))
	var action_str := str(raw_action)
	if not action_str.is_empty() and action_str != "null" and action_str != "<null>":
		action_label.text = "正在执行: " + action_str
		action_label.tooltip_text = action_str
		action_label.visible = true
	else:
		action_label.visible = false
		
	status_label.text = str(hud.get("chapter", ""))
	if status_label.text.is_empty() and snapshot.has("chapter"):
		var locale := TranslationServer.get_locale()
		var chapter := str(snapshot.chapter)
		status_label.text = "Chapter " + chapter if locale.begins_with("en") else "第 " + chapter + " 章"
	status_label.visible = not status_label.text.is_empty()
	status_label.tooltip_text = status_label.text
	time_label.text = str(hud.get("time", ""))
	
	# Danger indication with recognizable change feedback (Task 7.03)
	var raw_danger: Variant = hud.get("danger", snapshot.get("danger", snapshot.get("danger_level", snapshot.get("threat", 0))))
	_apply_danger_indicator(raw_danger)

func _apply_danger_indicator(danger_val: Variant) -> void:
	var danger_str := str(danger_val).to_lower()
	var level := 0
	if danger_val is int or danger_val is float:
		level = int(danger_val)
	elif danger_str in ["warning", "warn", "moderate"]:
		level = 1
	elif danger_str in ["danger", "critical", "high", "alert"]:
		level = 2
		
	var box := StyleBoxFlat.new()
	box.corner_radius_top_left = 6
	box.corner_radius_top_right = 6
	box.corner_radius_bottom_right = 6
	box.corner_radius_bottom_left = 6
	box.content_margin_left = 8.0
	box.content_margin_top = 2.0
	box.content_margin_right = 8.0
	box.content_margin_bottom = 2.0
	
	match level:
		0:
			box.bg_color = Color(0.18, 0.45, 0.25, 0.85)
			danger_label.text = "安全"
			danger_label.add_theme_color_override("font_color", Color(0.95, 0.98, 0.95, 1.0))
		1:
			box.bg_color = Color(0.75, 0.52, 0.12, 0.90)
			danger_label.text = "警戒"
			danger_label.add_theme_color_override("font_color", Color(1.0, 1.0, 1.0, 1.0))
		_:
			box.bg_color = Color(0.82, 0.18, 0.15, 0.95)
			danger_label.text = "危险！"
			danger_label.add_theme_color_override("font_color", Color(1.0, 1.0, 1.0, 1.0))
			
	danger_badge.add_theme_stylebox_override("panel", box)
	danger_badge.visible = true
	
	# Recognizable danger transition feedback
	if _last_danger_level != null and _last_danger_level != level:
		_pulse_danger_change()
	_last_danger_level = level

func _pulse_danger_change() -> void:
	var tween := create_tween()
	tween.tween_property(danger_badge, "modulate", Color(1.5, 1.5, 1.5, 1.0), 0.15)
	tween.tween_property(danger_badge, "modulate", Color(1.0, 1.0, 1.0, 1.0), 0.25)

func finish_entrance() -> void:
	# Initial layout is rendered behind the title; the reveal owns its motion.
	_bar_motion.finish_current()

func set_overlay_blocked(blocked: bool) -> void:
	_overlay_blocked = blocked
	hud_settings_button.disabled = blocked or _shutting_down
	Transition.set_interactive(top_bar, not blocked and not _shutting_down)
	if blocked: hide_hover()

func show_hover(text: String, pos: Vector2) -> void:
	if _shutting_down or _overlay_blocked: return
	if text.is_empty():
		hide_hover()
		return
	_hover_serial += 1
	hover_tip.text = text
	var viewport_size := get_viewport().get_visible_rect().size
	var font := hover_tip.get_theme_font("font")
	var font_size := hover_tip.get_theme_font_size("font_size")
	var measured := font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, font_size).x + 32.0
	hover_tip.size = Vector2(clampf(measured, 120.0, minf(420.0, viewport_size.x - 32.0)), 0.0)
	hover_tip.position = pos + Vector2(18, 18)
	_clamp_hover.call_deferred(pos, _hover_serial)
	_hover_motion.request(true, true, 0.10)

func _clamp_hover(pos: Vector2, serial: int) -> void:
	if serial != _hover_serial or _shutting_down: return
	var viewport_size := get_viewport().get_visible_rect().size
	var desired := pos + Vector2(18, 18)
	if desired.y + hover_tip.size.y > viewport_size.y - 16.0:
		desired.y = pos.y - hover_tip.size.y - 14.0
	hover_tip.position = Vector2(
		clampf(desired.x, 16.0, maxf(16.0, viewport_size.x - hover_tip.size.x - 16.0)),
		clampf(desired.y, 16.0, maxf(16.0, viewport_size.y - hover_tip.size.y - 16.0)))

func hide_hover() -> void:
	_hover_serial += 1
	if not _shutting_down: _hover_motion.request(false, true, 0.08)

func set_hint(text: String) -> void:
	if _shutting_down or text == _hint_text: return
	_hint_text = text
	if not text.is_empty(): hint_label.text = text
	_hint_motion.request(not text.is_empty(), true, 0.12)

func _on_setting_changed(section: String, key: String, value: Variant) -> void:
	if section == "accessibility" and key == "reduce_motion" and bool(value):
		_bar_motion.finish_current()
		_hint_motion.finish_current()
		_hover_motion.finish_current()

func begin_shutdown() -> void:
	if _shutting_down: return
	_shutting_down = true
	Transition.set_interactive(top_bar, false)
	_bar_motion.dispose()
	_hint_motion.dispose()
	_hover_motion.dispose()

func _exit_tree() -> void:
	begin_shutdown()
