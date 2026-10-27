extends Node2D
## Source illustration separated by coverage; no painted replacement ornament.
const DISC = preload("res://ShareAsset/Internal/Props/AuthoredRooms/central_hall_medallion_ring.png")
const FIELD = preload("res://ShareAsset/Internal/Props/AuthoredRooms/central_hall_medallion_field.png")
const MASK = preload("res://scenes/World/Runtime/authored_medallion.gdshader")

func _ready() -> void:
	texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	var field := Polygon2D.new()
	field.name = "ReferenceFieldUnderOcclusion"
	var points := PackedVector2Array()
	for index in range(128):
		var angle := TAU * index / 128.0
		points.append(Vector2(cos(angle) * 146, sin(angle) * 111))
	field.polygon = points
	field.uv = points
	field.texture = FIELD
	field.texture_repeat = CanvasItem.TEXTURE_REPEAT_ENABLED
	add_child(field)
	var disc := Sprite2D.new()
	disc.name = "VisibleOriginalMedallion"
	disc.texture = DISC
	var material := ShaderMaterial.new()
	material.shader = MASK
	disc.material = material
	add_child(disc)
