@tool
extends PanelContainer
## 印戳（设计规范第六部分 §4.6）：只用于状态，小号单线框，按文字哈希固定一个 ±2° 以内的倾角。
## tone：seal（印红，冲突）、match（相容）、muted（未查）。文字来自快照或面板状态标签，组件不造词。

@export var stamp_text := "印戳":
	set(value):
		stamp_text = value
		if is_node_ready():
			_apply()

@export_enum("seal", "match", "muted") var tone := "seal":
	set(value):
		tone = value
		if is_node_ready():
			_apply()

@export var animate := false

const UIAppearance := preload("res://scenes/UI/Components/UIAppearance.gd")

func _ready() -> void:
	resized.connect(func() -> void: pivot_offset = size / 2.0)
	_apply()

func _apply() -> void:
	var suffix := "" if tone == "seal" else tone.capitalize()
	theme_type_variation = StringName("PaperStampOuter" + suffix)
	$Text.theme_type_variation = StringName("PaperStampText" + suffix)
	$Text.text = stamp_text
	rotation_degrees = float(posmod(stamp_text.hash(), 9) - 4) * 0.5
	pivot_offset = size / 2.0
	if animate and not Engine.is_editor_hint() and is_inside_tree() and not UIAppearance.reduce_motion(self):
		scale = Vector2(1.15, 1.15)
		create_tween().tween_property(self, "scale", Vector2.ONE, 0.1)
