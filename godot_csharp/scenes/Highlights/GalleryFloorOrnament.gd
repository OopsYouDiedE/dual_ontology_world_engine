extends Node2D
## Large ornaments are independent source crops, never repeated floor cells.
const ROOT := "res://ShareAsset/Internal/Props/AuthoredRooms/shura_gallery_"
const SOURCE_SCALE := 7.0 / 24.0
const BORDER = preload("res://scenes/World/Runtime/AuthoredTerrainBorder.gd")

func _ready() -> void:
	texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	# Inset the border from the north wall and the foot-sorted south railing.
	# Source crop offsets align the straight double line with each turn.
	var border := BORDER.new()
	border.name = "AutomaticSourceBorder"
	# Cell (0,0) begins at this point; keep coverage at 48..464, 204..268.
	border.position = Vector2(48, 204)
	add_child(border)
	var cells: Array[Vector2i] = []
	for y in 2:
		for x in 13: cells.append(Vector2i(x, y))
	border.configure(cells)
	var center := Sprite2D.new()
	center.name = "SingleLargeDiamond"
	center.texture = load(ROOT + "center_diamond.png")
	center.centered = false
	center.scale = Vector2.ONE * SOURCE_SCALE
	# Source point (767,623) is placed at (256,237), with the same phase as
	# the surrounding floor sample whose source origin is (370,512).
	center.position = Vector2(256, 237) + Vector2(706 - 767, 566 - 623) * SOURCE_SCALE
	add_child(center)

