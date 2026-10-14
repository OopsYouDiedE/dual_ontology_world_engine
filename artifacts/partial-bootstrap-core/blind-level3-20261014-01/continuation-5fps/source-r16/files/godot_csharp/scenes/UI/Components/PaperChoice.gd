@tool
extends HBoxContainer
## 一行圈选项（设计规范第六部分 §4.7）：setup([{value, label, disabled?, tooltip?}], 当前值)。
## 当前值被笔迹圈出；点另一项发 changed(value)。值为数字时按近似相等比较。

signal changed(value: Variant)

const Sound := preload("res://scenes/Shared/Audio/Sound.gd")
const OPTION := preload("res://scenes/UI/Components/PaperOption.tscn")

@export_group("Editor Preview")
@export var preview_labels: PackedStringArray = PackedStringArray(["窗口", "全屏", "无边框"]):
	set(value):
		preview_labels = value
		_preview()

var _group := ButtonGroup.new()
var _buttons: Array[Button] = []
var _value: Variant = null

func _init() -> void:
	if theme_type_variation == &"":
		theme_type_variation = &"PaperOptionRow"

func _ready() -> void:
	_preview()

func _preview() -> void:
	if not Engine.is_editor_hint() or not is_node_ready():
		return
	var options: Array = []
	for i in preview_labels.size():
		options.append({"value": i, "label": preview_labels[i]})
	setup(options, 0)

func setup(options: Array, value: Variant) -> void:
	for b in _buttons:
		remove_child(b)
		b.queue_free()
	_buttons.clear()
	for option in options:
		var b: Button = OPTION.instantiate()
		b.text = str(option.get("label", ""))
		b.button_group = _group
		b.disabled = bool(option.get("disabled", false))
		b.tooltip_text = str(option.get("tooltip", ""))
		b.set_meta("value", option.get("value"))
		b.pressed.connect(_on_pressed.bind(b))
		add_child(b)
		_buttons.append(b)
	set_value(value)

func set_value(value: Variant) -> void:
	_value = value
	for b in _buttons:
		b.set_pressed_no_signal(same(b.get_meta("value"), value))
		b.queue_redraw()

func get_value() -> Variant:
	return _value

func get_buttons() -> Array[Button]:
	return _buttons

func _on_pressed(b: Button) -> void:
	var v: Variant = b.get_meta("value")
	if same(v, _value):
		return
	_value = v
	if not Engine.is_editor_hint():
		Sound.sfx("ui_click", -4.0)
	changed.emit(v)

static func same(a: Variant, b: Variant) -> bool:
	var numbers := [TYPE_INT, TYPE_FLOAT]
	if typeof(a) in numbers and typeof(b) in numbers:
		return is_equal_approx(float(a), float(b))
	return typeof(a) == typeof(b) and a == b
