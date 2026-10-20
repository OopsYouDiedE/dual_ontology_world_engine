extends CanvasLayer

signal settings_requested

const Transition := preload("res://scenes/UI/UITransition.gd")

@onready var top_bar: PanelContainer = %TopBar
@onready var room_label: Label = %RoomLabel
@onready var status_label: Label = %StatusLabel
@onready var time_label: Label = %TimeLabel
@onready var hud_settings_button: Button = %HudSettingsButton
@onready var hint_label: Label = %HintLabel
@onready var hover_tip: Label = %HoverTip

var _bar_motion = Transition.new()
var _hint_motion = Transition.new()
var _hover_motion = Transition.new()
var _overlay_blocked := false
var _shutting_down := false
var _hint_text := ""

func _ready() -> void:
	layer = 10
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

func apply_snapshot(snapshot: Dictionary) -> void:
	var hud: Dictionary = snapshot.get("hud", {})
	if hud.has("room"): room_label.text = str(hud.get("room", ""))
	if hud.has("chapter"): status_label.text = str(hud.get("chapter", ""))
	if hud.has("time"): time_label.text = str(hud.get("time", ""))

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
	hover_tip.text = text
	hover_tip.position = pos + Vector2(16, 16)
	_hover_motion.request(true, true, 0.10)

func hide_hover() -> void:
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
