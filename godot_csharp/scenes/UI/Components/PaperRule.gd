@tool
extends Control
## 规线：thin 为 1 px 淡规线；double 为报纸式粗细双线（上 3 px、下 1 px）；dash 为叠题中间的居中短线。
## 颜色取主题 Paper/colors/rule 与 rule_strong。

@export_enum("thin", "double", "dash") var style := "thin":
	set(value):
		style = value
		_size()
		queue_redraw()

func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_size()

func _size() -> void:
	custom_minimum_size.y = 6 if style == "double" else 1

func _draw() -> void:
	match style:
		"double":
			var strong := get_theme_color("rule_strong", "Paper")
			draw_rect(Rect2(0, 0, size.x, 3), strong)
			draw_rect(Rect2(0, 5, size.x, 1), strong)
		"dash":
			var w := minf(64.0, size.x)
			draw_rect(Rect2(roundf((size.x - w) / 2.0), 0, w, 1), get_theme_color("rule_strong", "Paper"))
		_:
			draw_rect(Rect2(0, 0, size.x, 1), get_theme_color("rule", "Paper"))
