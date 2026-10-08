@tool
extends Node2D
const Paper := preload("res://scenes/UI/Components/Paper.gd")
## 交互线索与描边提示组件。未检视物件闪烁，悬停或距离最近的物件呈现像素描边。
## 支持在检视器中配置描边与闪烁色调。

## 颜色定义在 UITheme.tres 的 HintOverlay/colors/hover。
var hover_color: Color:
	get: return Paper.theme().get_color("hover", "HintOverlay")

## 颜色定义在 UITheme.tres 的 HintOverlay/colors/near。
var near_color: Color:
	get: return Paper.theme().get_color("near", "HintOverlay")

## 颜色定义在 UITheme.tres 的 HintOverlay/colors/shade。
var shade_color: Color:
	get: return Paper.theme().get_color("shade", "HintOverlay")

## 颜色定义在 UITheme.tres 的 HintOverlay/colors/spark_edge。
var spark_edge_color: Color:
	get: return Paper.theme().get_color("spark_edge", "HintOverlay")

## 颜色定义在 UITheme.tres 的 HintOverlay/colors/spark_light。
var spark_light_color: Color:
	get: return Paper.theme().get_color("spark_light", "HintOverlay")

## 颜色定义在 UITheme.tres 的 HintOverlay/colors/spark_center。
var spark_center_color: Color:
	get: return Paper.theme().get_color("spark_center", "HintOverlay")

@export_group("Editor Preview")
@export var preview_enabled := true:
	set(value):
		preview_enabled = value
		queue_redraw()
@export var preview_box := Rect2(20, 20, 48, 48):
	set(value):
		preview_box = value
		queue_redraw()
@export var preview_spark_position := Vector2(44, 44):
	set(value):
		preview_spark_position = value
		queue_redraw()
@export_range(1, 8, 1) var preview_spark_radius := 3:
	set(value):
		preview_spark_radius = value
		queue_redraw()

var source: Node2D
var clock := 0.0

func _ready() -> void:
	# Legacy hosts may attach the script directly. Explicit scene/Inspector materials take priority.
	if not Engine.is_editor_hint() and material == null:
		var unlit := CanvasItemMaterial.new()
		unlit.light_mode = CanvasItemMaterial.LIGHT_MODE_UNSHADED
		material = unlit

func _process(delta: float) -> void:
	if Engine.is_editor_hint(): return
	clock = fmod(clock + delta, 22.0)
	queue_redraw()

func _draw() -> void:
	if Engine.is_editor_hint():
		if preview_enabled:
			_outline(preview_box, hover_color)
			_spark(preview_spark_position, preview_spark_radius)
		return
	if source == null or not source.field_active(): return
	for id in source.sites:
		if not source.site_visible(id):
			continue
		var point: Vector2 = source.site_marker(id)
		if source.site_is_new(id):
			var phase := fmod(clock + float(posmod(id.hash(), 220)) / 100.0, 2.2)
			var frame := int(phase * 8.0)
			if frame < 8:
				var radius: int = [0, 1, 2, 4, 3, 2, 1, 0][frame]
				if radius > 0:
					_spark(point.round(), radius)
	# 只描一件：鼠标所指的那件；没有指着东西时，才描最近的那件。
	for id in [source.hovered if source.hovered != "" else source.nearest]:
		if id == "" or not source.sites.has(id) or not source.site_visible(id):
			continue
		var box: Rect2 = source.site_box(id)
		_outline(box, hover_color if id == source.hovered else near_color)

func _outline(box: Rect2, color: Color) -> void:
	var r := Rect2(box.position.round(), box.size.round())
	draw_rect(Rect2(r.position, Vector2(r.size.x, 1)), color)
	draw_rect(Rect2(r.position + Vector2(0, r.size.y - 1), Vector2(r.size.x, 1)), color)
	draw_rect(Rect2(r.position, Vector2(1, r.size.y)), color)
	draw_rect(Rect2(r.position + Vector2(r.size.x - 1, 0), Vector2(1, r.size.y)), color)
	draw_rect(Rect2(r.position - Vector2(1, 1), Vector2(r.size.x + 2, 1)), shade_color)
	draw_rect(Rect2(r.position + Vector2(-1, r.size.y), Vector2(r.size.x + 2, 1)), shade_color)

func _spark(point: Vector2, radius: int) -> void:
	draw_rect(Rect2(point + Vector2(-radius, -1), Vector2(radius * 2 + 1, 3)), spark_edge_color)
	draw_rect(Rect2(point + Vector2(-1, -radius), Vector2(3, radius * 2 + 1)), spark_edge_color)
	draw_rect(Rect2(point + Vector2(-radius, 0), Vector2(radius * 2 + 1, 1)), spark_light_color)
	draw_rect(Rect2(point + Vector2(0, -radius), Vector2(1, radius * 2 + 1)), spark_light_color)
	draw_rect(Rect2(point, Vector2.ONE), spark_center_color)
