extends Control

## 任务进度：屏幕下方正中的无底文字与一道细刻度线。
## 主行写正在做的事，线上以黄铜色标出进度，次行写剩余时长；无任务时淡出。
## ratio 为负表示进度未知（如正在走近），线上以一小段往返滑动示意。

signal task_started(label: String)
signal task_cleared
signal cancel_requested

@export_group("排版")
@export var bottom_margin := 54.0
@export var track_width := 220.0
@export var track_gap := 6.0

@export_group("字体")
@export var primary_font: Font = preload("res://assets/external/NotoSerifSC/NotoSerifSC.ttf")
@export var secondary_font: Font = preload("res://assets/external/NotoSerifSC/NotoSerifSC.ttf")
@export var primary_size := 15
@export var secondary_size := 12

@export_group("色彩")
@export var primary_color := Color("efe4cc")
@export var secondary_color := Color("c9b994")
@export var track_color := Color(0.85, 0.78, 0.62, 0.35)
@export var fill_color := Color("d8b46a")
@export var outline_color := Color(0.09, 0.06, 0.04, 0.8)
@export var outline_size := 4

@export_group("刻度")
@export var track_thickness := 1.0
@export var fill_thickness := 2.0
@export var head_size := 3.5
@export var end_tick_height := 5.0
@export var indeterminate_period := 1.8
@export_range(0.05, 0.9) var indeterminate_span := 0.22

@export_group("内容")
@export var show_remaining := true
@export var remaining_prefix := "尚需约 "
## 为真时右键发出 cancel_requested，并在次行显示 cancel_hint。
@export var cancel_on_right_click := false
@export var cancel_hint := "右键罢手"

@export_group("干扰")
@export_range(0.0, 1.0) var active_alpha := 0.78
@export var fade_speed := 3.0

@onready var column: VBoxContainer = $Column
@onready var task_label: Label = $Column/Task
@onready var track: Control = $Column/Track
@onready var detail_label: Label = $Column/Detail

var _active := false
var _label := ""
var _ratio := -1.0
var _shown_ratio := 0.0
var _clock := 0.0

func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	apply_style()
	modulate.a = 0.0
	column.visible = false

func apply_style() -> void:
	task_label.label_settings = _label_settings(primary_font, primary_size, primary_color)
	detail_label.label_settings = _label_settings(secondary_font, secondary_size, secondary_color)
	track.custom_minimum_size = Vector2(track_width, track_gap * 2.0 + maxf(fill_thickness, head_size * 2.0))
	queue_redraw()

# ================================================================ public ===

func show_task(label: String, ratio := -1.0, remaining_seconds := -1.0) -> void:
	if not _active or label != _label:
		_active = true
		_label = label
		_shown_ratio = maxf(0.0, ratio)
		task_label.text = label
		column.visible = true
		task_started.emit(label)
	_ratio = clampf(ratio, 0.0, 1.0) if ratio >= 0.0 else -1.0
	var detail := _detail_text(remaining_seconds)
	if detail_label.text != detail: detail_label.text = detail
	detail_label.visible = not detail.is_empty()

func clear_task() -> void:
	if not _active: return
	_active = false
	_label = ""
	task_cleared.emit()

func is_active() -> bool:
	return _active

# ============================================================== internal ===

func _process(delta: float) -> void:
	modulate.a = move_toward(modulate.a, active_alpha if _active else 0.0, fade_speed * delta)
	column.visible = _active or modulate.a > 0.01
	if not column.visible: return
	_clock += delta
	if _ratio >= 0.0: _shown_ratio = lerpf(_shown_ratio, _ratio, 1.0 - exp(-8.0 * delta))
	column.reset_size()
	column.position = Vector2((size.x - column.size.x) * 0.5, size.y - bottom_margin - column.size.y)
	queue_redraw()

func _unhandled_input(event: InputEvent) -> void:
	if cancel_on_right_click and _active and event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_RIGHT:
		cancel_requested.emit()

func _draw() -> void:
	if not column.visible: return
	var origin := track.global_position - global_position
	var y := origin.y + track.size.y * 0.5
	var x0 := origin.x
	var x1 := origin.x + track.size.x
	var shadow := Color(outline_color, outline_color.a * 0.6)
	draw_line(Vector2(x0, y + 1.0), Vector2(x1, y + 1.0), shadow, track_thickness + 1.0, true)
	draw_line(Vector2(x0, y), Vector2(x1, y), track_color, track_thickness, true)
	for x: float in [x0, x1]:
		draw_line(Vector2(x, y - end_tick_height * 0.5), Vector2(x, y + end_tick_height * 0.5), track_color, track_thickness, true)
	var from_ratio := 0.0
	var to_ratio := _shown_ratio
	if _ratio < 0.0:
		from_ratio = pingpong(_clock / maxf(0.1, indeterminate_period), 1.0) * (1.0 - indeterminate_span)
		to_ratio = from_ratio + indeterminate_span
	var head := Vector2(lerpf(x0, x1, to_ratio), y)
	draw_line(Vector2(lerpf(x0, x1, from_ratio), y + 1.0), head + Vector2(0, 1.0), shadow, fill_thickness + 1.0, true)
	draw_line(Vector2(lerpf(x0, x1, from_ratio), y), head, fill_color, fill_thickness, true)
	_draw_diamond(head + Vector2(0, 1.0), head_size + 1.0, shadow)
	_draw_diamond(head, head_size, fill_color)

func _draw_diamond(center: Vector2, radius: float, color: Color) -> void:
	draw_colored_polygon(PackedVector2Array([center + Vector2(0, -radius), center + Vector2(radius, 0),
		center + Vector2(0, radius), center + Vector2(-radius, 0)]), color)

func _detail_text(remaining_seconds: float) -> String:
	var parts: Array[String] = []
	if show_remaining and remaining_seconds >= 0.0: parts.append(remaining_prefix + _duration_text(remaining_seconds))
	if cancel_on_right_click and not cancel_hint.is_empty(): parts.append(cancel_hint)
	return "　·　".join(parts)

## 剩余游戏时长，按分钟取整。
static func _duration_text(seconds: float) -> String:
	if seconds < 60.0: return "片刻"
	var minutes := int(ceilf(seconds / 60.0))
	if minutes < 60: return "%d 分钟" % minutes
	return "%d 时 %d 分" % [int(minutes / 60.0), minutes % 60]

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
