extends Node2D
## Large ornaments are independent source crops, never repeated floor cells.
const SURFACE = preload("res://scenes/Shared/Field/AuthoredSurface.gd")
const ROOT := "res://ShareAsset/Internal/Props/AuthoredRooms/shura_gallery_"
const SOURCE_SCALE := 7.0 / 24.0
const TURN_SIZE := 60.0 * SOURCE_SCALE

func _ready() -> void:
	texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	# Inset the border from the north wall and the foot-sorted south railing.
	# Source crop offsets align the straight double line with each turn.
	_strip("border_south", Rect2(48 + TURN_SIZE, 204.625, 416 - TURN_SIZE * 2, 20 * SOURCE_SCALE))
	_strip("border_south", Rect2(48 + TURN_SIZE, 265.875, 416 - TURN_SIZE * 2, 20 * SOURCE_SCALE))
	_strip("border_west", Rect2(48.58333333333333, 202 + TURN_SIZE, 20 * SOURCE_SCALE, 70 - TURN_SIZE * 2))
	_strip("border_west", Rect2(458.75, 202 + TURN_SIZE, 20 * SOURCE_SCALE, 70 - TURN_SIZE * 2))
	for placement in [[Vector2(48, 202), 0.0], [Vector2(464, 202), PI / 2.0], [Vector2(464, 272), PI], [Vector2(48, 272), -PI / 2.0]]:
		var corner := Sprite2D.new()
		corner.name = "ReferenceCorner"
		corner.texture = load(ROOT + "border_turn.png")
		corner.centered = false
		corner.scale = Vector2.ONE * SOURCE_SCALE
		corner.position = placement[0]
		corner.rotation = float(placement[1])
		add_child(corner)
	var center := Sprite2D.new()
	center.name = "SingleLargeDiamond"
	center.texture = load(ROOT + "center_diamond.png")
	center.centered = false
	center.scale = Vector2.ONE * SOURCE_SCALE
	# Source point (767,623) is placed at (256,237), with the same phase as
	# the surrounding floor sample whose source origin is (370,512).
	center.position = Vector2(256, 237) + Vector2(706 - 767, 566 - 623) * SOURCE_SCALE
	add_child(center)

func _strip(piece: String, rect: Rect2) -> void:
	var strip := SURFACE.new()
	strip.name = "Reference_" + piece
	add_child(strip)
	var coverage: Array[Rect2] = [rect]
	strip.configure(load(ROOT + piece + ".png"), SOURCE_SCALE, rect.position, coverage)
