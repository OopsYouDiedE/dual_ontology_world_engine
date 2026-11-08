extends Control

## 时间与所在房间：左上角两行无底文字。主行写房间，次行写日期与时刻，其间一道细墨线。
## 平时以低透明度停留；换房间或鼠标靠近时加深，数秒后退回。

signal room_pressed(room_id: String)
signal emphasis_changed(emphasized: bool)

const PLAIN_NUMERALS := ["〇", "一", "二", "三", "四", "五", "六", "七", "八", "九", "十"]
const FORMAL_NUMERALS := ["零", "壹", "贰", "叁", "肆", "伍", "陆", "柒", "捌", "玖", "拾"]

@export_group("排版")
@export var screen_margin := Vector2(28, 22)
@export var line_gap := 6
@export var rule_length := 132.0
@export var rule_width := 1.0
@export var ornament := "❦"

@export_group("字体")
@export var primary_font: Font = preload("res://assets/external/NotoSerifSC/NotoSerifSC.ttf")
@export var secondary_font: Font = preload("res://assets/external/NotoSerifSC/NotoSerifSC.ttf")
@export var primary_size := 19
@export var secondary_size := 13

@export_group("色彩")
@export var primary_color := Color("efe4cc")
@export var secondary_color := Color("c9b994")
@export var rule_color := Color(0.79, 0.69, 0.5, 0.55)
@export var outline_color := Color(0.09, 0.06, 0.04, 0.8)
@export var outline_size := 4

@export_group("时刻")
## 日数用大写数字（第壹日）。
@export var formal_day_numerals := true
## 时刻写作“午后三时二十分”；关闭后为 15:20。
@export var period_clock := true
@export var paused_mark := "　停摆"

@export_group("干扰")
@export_range(0.0, 1.0) var rest_alpha := 0.5
@export_range(0.0, 1.0) var emphasis_alpha := 0.95
@export var emphasis_seconds := 3.0
@export var fade_speed := 2.5
@export var hover_reveals := true
## 为真时点击房间名发出 room_pressed；为假时整块不拦截鼠标。
@export var clickable := false

@onready var column: VBoxContainer = $Column
@onready var room_label: Label = $Column/Room
@onready var clock_label: Label = $Column/Clock

var _room_id := ""
var _emphasis_left := 0.0
var _emphasized := false

func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	room_label.gui_input.connect(_on_room_input)
	column.resized.connect(queue_redraw)
	apply_style()
	modulate.a = rest_alpha

func apply_style() -> void:
	column.position = screen_margin
	column.add_theme_constant_override("separation", line_gap)
	room_label.label_settings = _label_settings(primary_font, primary_size, primary_color)
	clock_label.label_settings = _label_settings(secondary_font, secondary_size, secondary_color)
	room_label.mouse_filter = Control.MOUSE_FILTER_STOP if clickable else Control.MOUSE_FILTER_IGNORE
	queue_redraw()

# ================================================================ public ===

func set_place(room_id: String, room_name: String) -> void:
	if room_id == _room_id: return
	_room_id = room_id
	var text := room_name if not room_name.is_empty() else room_id
	room_label.text = text if ornament.is_empty() else ornament + " " + text
	emphasize()
	queue_redraw()

func set_time(game_seconds: float, paused := false) -> void:
	var text := format_time(game_seconds) + (paused_mark if paused else "")
	if clock_label.text != text: clock_label.text = text

## 短暂加深显示；seconds 为负时使用 emphasis_seconds。
func emphasize(seconds := -1.0) -> void:
	_emphasis_left = emphasis_seconds if seconds < 0.0 else seconds

func format_time(game_seconds: float) -> String:
	var day := int(game_seconds / 86400.0) + 1
	var hour := int(fmod(game_seconds, 86400.0) / 3600.0)
	var minute := int(fmod(game_seconds, 3600.0) / 60.0)
	var day_text := "第%s日" % (_numeral(day, FORMAL_NUMERALS) if formal_day_numerals else str(day))
	if not period_clock: return "%s　%02d:%02d" % [day_text, hour, minute]
	var half := hour % 12
	if half == 0: half = 12
	var minute_text := "整" if minute == 0 else ("零" if minute < 10 else "") + _numeral(minute, PLAIN_NUMERALS) + "分"
	return "%s　%s%s时%s" % [day_text, "午前" if hour < 12 else "午后", _numeral(half, PLAIN_NUMERALS), minute_text]

# ============================================================== internal ===

func _process(delta: float) -> void:
	_emphasis_left = maxf(0.0, _emphasis_left - delta)
	var hovered := hover_reveals and column.get_global_rect().grow(10.0).has_point(get_global_mouse_position())
	var emphasized := _emphasis_left > 0.0 or hovered
	if emphasized != _emphasized:
		_emphasized = emphasized
		emphasis_changed.emit(emphasized)
	modulate.a = move_toward(modulate.a, emphasis_alpha if emphasized else rest_alpha, fade_speed * delta)

func _draw() -> void:
	if rule_length <= 0.0 or room_label.text.is_empty(): return
	var y := column.position.y + room_label.position.y + room_label.size.y + line_gap * 0.5
	var x := column.position.x + 2.0
	var shadow := Color(outline_color, outline_color.a * 0.6)
	draw_line(Vector2(x, y + 1.0), Vector2(x + rule_length, y + 1.0), shadow, rule_width + 1.0, true)
	draw_line(Vector2(x, y), Vector2(x + rule_length, y), rule_color, rule_width, true)
	draw_circle(Vector2(x + rule_length + 3.0, y), 1.3, rule_color)

func _on_room_input(event: InputEvent) -> void:
	if clickable and event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		room_label.accept_event()
		room_pressed.emit(_room_id)

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

static func _numeral(value: int, table: Array) -> String:
	if value <= 10: return table[value]
	if value < 20: return table[10] + table[value - 10]
	var ones := value % 10
	return table[int(value / 10.0)] + table[10] + (table[ones] if ones != 0 else "")
