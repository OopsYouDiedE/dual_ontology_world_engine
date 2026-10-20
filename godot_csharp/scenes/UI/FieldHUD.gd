extends CanvasLayer

signal settings_requested

@onready var top_bar: PanelContainer = %TopBar
@onready var room_label: Label = %RoomLabel
@onready var status_label: Label = %StatusLabel
@onready var time_label: Label = %TimeLabel
@onready var hud_settings_button: Button = %HudSettingsButton
@onready var hint_label: Label = %HintLabel
@onready var hover_tip: Label = %HoverTip

func _ready() -> void:
	layer = 10
	hud_settings_button.pressed.connect(func(): settings_requested.emit())
	hover_tip.visible = false
	hint_label.visible = false

func apply_snapshot(snapshot: Dictionary) -> void:
	var hud: Dictionary = snapshot.get("hud", {})
	if hud.has("room"):
		room_label.text = str(hud.get("room", ""))
	if hud.has("chapter"):
		status_label.text = str(hud.get("chapter", ""))
	if hud.has("time"):
		time_label.text = str(hud.get("time", ""))

func show_hover(text: String, pos: Vector2) -> void:
	if text == "":
		hide_hover()
		return
	hover_tip.text = text
	hover_tip.position = pos + Vector2(16, 16)
	hover_tip.visible = true

func hide_hover() -> void:
	hover_tip.visible = false

func set_hint(text: String) -> void:
	if text == "":
		hint_label.visible = false
		hint_label.text = ""
	else:
		hint_label.text = text
		hint_label.visible = true
