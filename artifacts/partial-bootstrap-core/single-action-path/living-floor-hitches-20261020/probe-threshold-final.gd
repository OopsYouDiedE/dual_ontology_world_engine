extends SceneTree
## Geometry-only diagnostic using the observed failed footpoint; no DV state.
func _initialize() -> void:
	var adapter: Node2D = load("res://scenes/Chapter2/Field/CampusMapView.gd").new()
	var layout: Node2D = load("res://scenes/Chapter2/Maps/living_floor.tscn").instantiate()
	adapter._new_map_record(layout)
	adapter.map = layout
	adapter.key = "living_floor"
	adapter._build_grid_for("living_floor", layout)
	for point: Vector2 in [Vector2(2416.0,973.833801269531),Vector2(2430.0,973.833801269531),Vector2(-1,-1)]:
		var grid: AStarGrid2D = adapter._grids.living_floor
		var cell := Vector2i((point/16.0).floor())
		var contains := false
		for rect: Rect2 in adapter._blocked_rects.living_floor:
			if rect.has_point(point): contains = true
		print("point=",point," terrain=",adapter._terrain_mark(Vector2i((point/32.0).floor()))," departure=",adapter.departure_room(point)," nav_center_solid=",grid.is_point_solid(cell) if grid.region.has_point(cell) else "outside"," in_original_inflated_obstacle=",contains)
	layout.free()
	adapter.free()
	quit()
