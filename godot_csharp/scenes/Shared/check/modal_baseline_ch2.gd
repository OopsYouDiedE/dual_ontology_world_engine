extends Node
const Paper = preload("res://scenes/Shared/UI/Paper.gd")
const Sound = preload("res://scenes/Shared/Audio/Sound.gd")
var modal_layer: CanvasLayer
var modal_root: Control
var modal_kind := ""
var manuscript: Control
var click_path := PackedVector2Array()
var click_waypoint := 0
var click_interaction_id := ""
var click_target := ""
var click_last_position := Vector2(INF, INF)
var click_stall_seconds := 0.0
func open_modal(title: String, size: Vector2, kind: String) -> VBoxContainer:
	close_modal()
	modal_kind = kind
	_cancel_click()
	if kind != "ending":
		Sound.sfx("page", -6.0)
	var root := Control.new()
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.theme = Paper.theme()
	modal_layer.add_child(root)
	modal_root = root
	var dim := ColorRect.new()
	dim.color = Color(0.03, 0.03, 0.05, 0.55)
	dim.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.add_child(dim)
	var panel := PanelContainer.new()
	panel.add_theme_stylebox_override("panel", Paper.sheet())
	panel.position = (Vector2(1280, 992) - size) / 2.0
	panel.size = size
	panel.custom_minimum_size = size
	root.add_child(panel)
	var column := VBoxContainer.new()
	column.add_theme_constant_override("separation", 12)
	panel.add_child(column)
	if not title.is_empty():
		column.add_child(Paper.label("[b]" + title + "[/b]", 25))
		column.add_child(Paper.rule())
	var scroll := ScrollContainer.new()
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	column.add_child(scroll)
	var body := VBoxContainer.new()
	body.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	body.add_theme_constant_override("separation", 10)
	scroll.add_child(body)
	root.set_meta("column", column)
	return body

func close_modal() -> void:
	if is_instance_valid(modal_root):
		modal_root.queue_free()
	modal_root = null
	modal_kind = ""

func _cancel_click() -> void:
	click_path = PackedVector2Array()
	click_waypoint = 0
	click_target = ""
