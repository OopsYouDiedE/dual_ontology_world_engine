@tool
extends Node2D
## 交互线索与描边提示组件。未检视物件闪烁，悬停或距离最近的物件呈现像素描边。
## 支持在检视器中配置描边与闪烁色调。

@export var hover_color: Color = Color("f4df9d"):
	set(value):
		hover_color = value
		queue_redraw()

@export var near_color: Color = Color(0.95, 0.88, 0.62, 0.7):
	set(value):
		near_color = value
		queue_redraw()

@export var shade_color: Color = Color(0.12, 0.09, 0.07, 0.6):
	set(value):
		shade_color = value
		queue_redraw()

@export var spark_edge_color: Color = Color("756044"):
	set(value):
		spark_edge_color = value
		queue_redraw()

@export var spark_light_color: Color = Color("f4df9d"):
	set(value):
		spark_light_color = value
		queue_redraw()

@export var spark_center_color: Color = Color("fff2c9"):
	set(value):
		spark_center_color = value
		queue_redraw()

var source: Node2D
var clock := 0.0

func _ready() -> void:
	var unlit := CanvasItemMaterial.new()
	unlit.light_mode = CanvasItemMaterial.LIGHT_MODE_UNSHADED
	material = unlit

func _process(delta: float) -> void:
	clock = fmod(clock + delta, 22.0)
	queue_redraw()

func _draw() -> void:
	if source == null or not source.field_active():
		if Engine.is_editor_hint():
			# 编辑器预览样本：绘制演示描边与闪烁点
			_outline(Rect2(Vector2(20, 20), Vector2(48, 48)), hover_color)
			_spark(Vector2(44, 44), 3)
		return
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
