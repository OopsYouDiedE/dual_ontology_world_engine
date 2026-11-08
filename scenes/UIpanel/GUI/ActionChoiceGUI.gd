extends Control

## 物件可选动作：选中人物或物件时，在其旁浮出的无底文字列表。
## 主行为对象名，其下一道细线；次级为编号动作，不可行的动作以淡墨显示。
## 平时半透明，鼠标靠近时加深；数字键 1–9 直接选择。锚点由主控按目标的屏幕位置传入。

signal action_chosen(action_id: String)
signal action_hovered(action_id: String)
signal dismissed

const ROMAN := ["Ⅰ", "Ⅱ", "Ⅲ", "Ⅳ", "Ⅴ", "Ⅵ", "Ⅶ", "Ⅷ", "Ⅸ"]

@export_group("位置")
## 为真时跟随 set_anchor 传入的屏幕点；无锚点时停在 fixed_anchor（视口比例）。
@export var follow_anchor := true
@export var anchor_offset := Vector2(28, -18)
@export var fixed_anchor := Vector2(0.7, 0.52)
@export var edge_margin := 18.0
@export var follow_smoothing := 14.0

@export_group("内容")
@export_enum("罗马数字", "阿拉伯数字", "无") var numbering := 0
@export_range(1, 9) var max_options := 6
@export var show_unavailable := true
@export var show_heading := true
@export var heading_ornament := "❧"
@export var pending_mark := "▸ "
@export var empty_text := "此处暂无可做之事"
@export var hotkeys_enabled := true
@export var notice_seconds := 3.0

@export_group("字体")
@export var heading_font: Font = preload("res://assets/external/NotoSerifSC/NotoSerifSC.ttf")
@export var option_font: Font = preload("res://assets/external/NotoSerifSC/NotoSerifSC.ttf")
@export var notice_font: Font = preload("res://assets/external/NotoSerifSC/NotoSerifSC.ttf")
@export var heading_size := 16
@export var option_size := 14
@export var notice_size := 12

@export_group("色彩")
@export var heading_color := Color("efe4cc")
@export var option_color := Color("e6dbc2")
@export var hover_color := Color("e2bd72")
@export var disabled_color := Color(0.75, 0.7, 0.62, 0.45)
@export var pending_color := Color("d8b46a")
@export var notice_color := Color("d9a58a")
@export var rule_color := Color(0.79, 0.69, 0.5, 0.55)
@export var outline_color := Color(0.09, 0.06, 0.04, 0.8)
@export var outline_size := 4

@export_group("干扰")
@export_range(0.0, 1.0) var rest_alpha := 0.6
@export_range(0.0, 1.0) var hover_alpha := 1.0
@export var fade_speed := 4.0
@export var rule_length := 96.0

@onready var column: VBoxContainer = $Column
@onready var heading_label: Label = $Column/Heading
@onready var rule_space: Control = $Column/Rule
@onready var options_box: VBoxContainer = $Column/Options
@onready var notice_label: Label = $Column/Notice

var _ids: Array[String] = []
var _enabled: Array[bool] = []
var _buttons: Array[Button] = []
var _presented := false
var _anchor := Vector2.ZERO
var _has_anchor := false
var _snap := true
var _pending := ""
var _notice_left := 0.0

func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	apply_style()
	modulate.a = 0.0
	column.visible = false

func apply_style() -> void:
	heading_label.label_settings = _label_settings(heading_font, heading_size, heading_color)
	notice_label.label_settings = _label_settings(notice_font, notice_size, notice_color)
	rule_space.custom_minimum_size = Vector2(rule_length, 7.0)
	for button in _buttons: _style_button(button)
	_refresh_captions()

# ================================================================ public ===

## options 每项：{id, label, enabled = true, hint = ""}。
func present(heading: String, options: Array) -> void:
	_clear_options()
	heading_label.text = heading if heading_ornament.is_empty() else heading_ornament + " " + heading
	heading_label.visible = show_heading
	rule_space.visible = show_heading
	for option in options:
		if not option is Dictionary: continue
		var id := str(option.get("id", ""))
		var enabled := bool(option.get("enabled", true))
		if id.is_empty() or (not enabled and not show_unavailable): continue
		if _buttons.size() >= max_options: break
		var button := Button.new()
		_style_button(button)
		button.tooltip_text = str(option.get("hint", ""))
		button.set_meta("caption", str(option.get("label", id)))
		button.pressed.connect(_choose.bind(_buttons.size()))
		button.mouse_entered.connect(func(): action_hovered.emit(id))
		options_box.add_child(button)
		_ids.append(id)
		_enabled.append(enabled)
		_buttons.append(button)
	if _buttons.is_empty() and not empty_text.is_empty():
		var empty := Label.new()
		empty.mouse_filter = Control.MOUSE_FILTER_IGNORE
		empty.text = empty_text
		empty.label_settings = _label_settings(option_font, option_size, disabled_color)
		options_box.add_child(empty)
	if not _presented: _snap = true
	_presented = true
	column.visible = true
	_refresh_captions()

func dismiss() -> void:
	if not _presented: return
	_presented = false
	_pending = ""
	for button in _buttons: button.mouse_filter = Control.MOUSE_FILTER_IGNORE
	dismissed.emit()

func is_presented() -> bool:
	return _presented

## 正在走近或执行中的动作以 pending_mark 标出，其余动作暂不可选。
func set_pending(action_id: String) -> void:
	if action_id == _pending: return
	_pending = action_id
	_refresh_captions()

func set_screen_anchor(screen_position: Vector2) -> void:
	_anchor = screen_position
	_has_anchor = true

func clear_anchor() -> void:
	_has_anchor = false

func show_notice(text: String, seconds := -1.0) -> void:
	notice_label.text = text
	_notice_left = 0.0 if text.is_empty() else (notice_seconds if seconds < 0.0 else seconds)

# ============================================================== internal ===

func _process(delta: float) -> void:
	_notice_left = maxf(0.0, _notice_left - delta)
	notice_label.visible = _presented and _notice_left > 0.0
	var hovered := _presented and column.get_global_rect().grow(12.0).has_point(get_global_mouse_position())
	var target := 0.0
	if _presented: target = hover_alpha if hovered or _notice_left > 0.0 else rest_alpha
	modulate.a = move_toward(modulate.a, target, fade_speed * delta)
	if not _presented and modulate.a <= 0.01:
		column.visible = false
		if options_box.get_child_count() > 0: _clear_options()
		return
	_place(delta)
	queue_redraw()

func _unhandled_input(event: InputEvent) -> void:
	if not hotkeys_enabled or not _presented or not is_visible_in_tree(): return
	if event is InputEventKey and event.pressed and not event.echo:
		var index := int(event.keycode) - KEY_1
		if index >= 0 and index < mini(9, _ids.size()):
			get_viewport().set_input_as_handled()
			_choose(index)

func _choose(index: int) -> void:
	if index >= _ids.size() or not _enabled[index] or not _pending.is_empty(): return
	action_chosen.emit(_ids[index])

## 锚点右侧放不下时翻到左侧，再夹在视口边距内。
func _place(delta: float) -> void:
	column.reset_size()
	var view := size
	var anchored := follow_anchor and _has_anchor
	var base := _anchor if anchored else view * fixed_anchor
	var target := base + anchor_offset
	if anchored and target.x + column.size.x > view.x - edge_margin:
		target.x = base.x - anchor_offset.x - column.size.x
	target.x = clampf(target.x, edge_margin, maxf(edge_margin, view.x - column.size.x - edge_margin))
	target.y = clampf(target.y, edge_margin, maxf(edge_margin, view.y - column.size.y - edge_margin))
	if _snap:
		column.position = target
		_snap = false
	else:
		column.position = column.position.lerp(target, 1.0 - exp(-follow_smoothing * delta))

func _draw() -> void:
	if not column.visible or not rule_space.visible: return
	var origin := rule_space.global_position - global_position
	var y := origin.y + rule_space.size.y * 0.5
	var x := origin.x + 2.0
	var shadow := Color(outline_color, outline_color.a * 0.6)
	draw_line(Vector2(x, y + 1.0), Vector2(x + rule_length, y + 1.0), shadow, 2.0, true)
	draw_line(Vector2(x, y), Vector2(x + rule_length, y), rule_color, 1.0, true)
	draw_circle(Vector2(x + rule_length + 3.0, y), 1.3, rule_color)

func _refresh_captions() -> void:
	for index in _buttons.size():
		var button := _buttons[index]
		var pending := _ids[index] == _pending
		var caption := str(button.get_meta("caption", _ids[index]))
		button.text = _number(index) + (pending_mark + caption if pending else caption)
		button.disabled = not _enabled[index] or not _pending.is_empty()
		button.add_theme_color_override("font_disabled_color", pending_color if pending else disabled_color)

func _number(index: int) -> String:
	match numbering:
		0: return ROMAN[index] + "　"
		1: return "%d. " % (index + 1)
	return ""

func _clear_options() -> void:
	for child in options_box.get_children():
		options_box.remove_child(child)
		child.queue_free()
	_ids.clear()
	_enabled.clear()
	_buttons.clear()

func _style_button(button: Button) -> void:
	button.flat = true
	button.focus_mode = Control.FOCUS_NONE
	button.mouse_filter = Control.MOUSE_FILTER_STOP
	button.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	button.alignment = HORIZONTAL_ALIGNMENT_LEFT
	var empty := StyleBoxEmpty.new()
	empty.content_margin_top = 1.0
	empty.content_margin_bottom = 1.0
	for style in ["normal", "hover", "pressed", "hover_pressed", "disabled", "focus"]:
		button.add_theme_stylebox_override(style, empty)
	button.add_theme_font_override("font", option_font)
	button.add_theme_font_size_override("font_size", option_size)
	button.add_theme_color_override("font_color", option_color)
	button.add_theme_color_override("font_hover_color", hover_color)
	button.add_theme_color_override("font_pressed_color", hover_color)
	button.add_theme_color_override("font_hover_pressed_color", hover_color)
	button.add_theme_color_override("font_disabled_color", disabled_color)
	button.add_theme_color_override("font_outline_color", outline_color)
	button.add_theme_constant_override("outline_size", outline_size)

func _label_settings(font: Font, font_size: int, color: Color) -> LabelSettings:
	var settings := LabelSettings.new()
	settings.font = font
	settings.font_size = font_size
	settings.font_color = color
	settings.outline_size = outline_size
	settings.outline_color = outline_color
	settings.shadow_size = 2
	settings.shadow_color = Color(outline_color, outline_color.a * 0.5)
	settings.shadow_offset = Vector2(0, 1)
	return settings
