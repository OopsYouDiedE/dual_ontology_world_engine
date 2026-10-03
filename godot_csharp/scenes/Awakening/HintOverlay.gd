extends Node2D
## Interaction cues: unexamined objects glint, the hovered or nearest object gets a pixel outline.
## Decorations never glint or highlight; only sites registered by the host do.

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
	for id in [source.hovered, source.nearest]:
		if id == "" or not source.sites.has(id) or not source.site_visible(id):
			continue
		var box: Rect2 = source.site_box(id)
		_outline(box, Color("f4df9d") if id == source.hovered else Color(0.95, 0.88, 0.62, 0.7))

func _outline(box: Rect2, color: Color) -> void:
	var r := Rect2(box.position.round(), box.size.round())
	draw_rect(Rect2(r.position, Vector2(r.size.x, 1)), color)
	draw_rect(Rect2(r.position + Vector2(0, r.size.y - 1), Vector2(r.size.x, 1)), color)
	draw_rect(Rect2(r.position, Vector2(1, r.size.y)), color)
	draw_rect(Rect2(r.position + Vector2(r.size.x - 1, 0), Vector2(1, r.size.y)), color)
	var shade := Color(0.12, 0.09, 0.07, 0.6)
	draw_rect(Rect2(r.position - Vector2(1, 1), Vector2(r.size.x + 2, 1)), shade)
	draw_rect(Rect2(r.position + Vector2(-1, r.size.y), Vector2(r.size.x + 2, 1)), shade)

func _spark(point: Vector2, radius: int) -> void:
	var edge := Color("756044")
	var light := Color("f4df9d")
	draw_rect(Rect2(point + Vector2(-radius, -1), Vector2(radius * 2 + 1, 3)), edge)
	draw_rect(Rect2(point + Vector2(-1, -radius), Vector2(3, radius * 2 + 1)), edge)
	draw_rect(Rect2(point + Vector2(-radius, 0), Vector2(radius * 2 + 1, 1)), light)
	draw_rect(Rect2(point + Vector2(0, -radius), Vector2(1, radius * 2 + 1)), light)
	draw_rect(Rect2(point, Vector2.ONE), Color("fff2c9"))
