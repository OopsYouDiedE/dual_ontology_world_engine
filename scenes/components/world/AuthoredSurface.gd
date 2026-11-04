extends Node2D
## A source-image repeat, sampled at one declared scale and world phase.
## Cell boundaries clip coverage only; they never shrink or reshape the motif.
var texture: Texture2D
var source_scale := 1.0
var phase := Vector2.ZERO
var areas: Array[Rect2] = []

func configure(image: Texture2D, scale_ratio: float, origin: Vector2, coverage: Array[Rect2]) -> void:
	texture = image
	source_scale = scale_ratio
	phase = origin
	areas = coverage.duplicate()
	texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	queue_redraw()

func _draw() -> void:
	if texture == null or source_scale <= 0.0:
		return
	var period := texture.get_size() * source_scale
	for area in areas:
		var first := Vector2(floorf((area.position.x - phase.x) / period.x), floorf((area.position.y - phase.y) / period.y))
		var last := Vector2(ceilf((area.end.x - phase.x) / period.x), ceilf((area.end.y - phase.y) / period.y))
		for row in range(int(first.y), int(last.y)):
			for column in range(int(first.x), int(last.x)):
				var origin := phase + Vector2(column, row) * period
				var clipped := area.intersection(Rect2(origin, period))
				if clipped.has_area():
					var sample := Rect2((clipped.position - origin) / source_scale, clipped.size / source_scale)
					draw_texture_rect_region(texture, clipped, sample)
