extends Node2D
## The legacy map protocol is a transient projection of the selected DV floor.
## Visual geometry is built by WorldStage3D; this supplies navigation/room cells.
const DV_MAP = preload("res://geometry/LegacyDvMap.gd")
@export var map_id := "main_2f"

func _ready() -> void:
	initialize_dv_map()

func initialize_dv_map() -> void:
	if has_meta("terrain_rows"): return
	var spec := DV_MAP.map_data(map_id)
	var floor := DV_MAP.spatial_floor(map_id)
	if spec.is_empty() or floor.is_empty():
		push_error("DV legacy map has no validated spatial floor: " + map_id)
		return
	set_meta("dv_spatial_definition", spec.source_definition)
	set_meta("dv_spatial_sha256", spec.source_sha256)
	set_meta("map_size", Vector2(float(spec.size[0]),float(spec.size[1])))
	set_meta("title", "学院 " + str(spec.floor_id))
	# A full pixel-grid image never becomes the authored physical geometry.
	var columns := int(ceilf(float(spec.size[0]) / 32.0))
	var rows := int(ceilf(float(spec.size[1]) / 32.0))
	var tiles: Array = []
	for y in rows:
		tiles.append("#".repeat(columns))
	for surface in floor.pieces:
		if str(surface.get("kind", "")) != "floor" or not surface.has("rect"): continue
		var r: Array = surface.rect
		var first := DV_MAP.design_to_pixel(map_id,Vector3(float(r[0]),float(r[1]),0.0))
		var last := DV_MAP.design_to_pixel(map_id,Vector3(float(r[2]),float(r[3]),0.0))
		var x0 := maxi(0,int(ceilf(first.x/32.0-0.5)))
		var x1 := mini(columns-1,int(floorf(last.x/32.0-0.5)))
		var y0 := maxi(0,int(ceilf(first.y/32.0-0.5)))
		var y1 := mini(rows-1,int(floorf(last.y/32.0-0.5)))
		for y in range(y0,y1+1):
			var row: String = tiles[y]
			for x in range(x0,x1+1):
				row = row.substr(0, x) + "." + row.substr(x + 1)
			tiles[y] = row
	set_meta("terrain_rows", tiles)
	# Geometry for path blocking comes from the same authored wall pieces.
	var solids := get_node("Solids") as StaticBody2D
	for piece in floor.pieces:
		if str(piece.get("kind", "")) != "wall_lower" or not piece.has("rect"): continue
		var r: Array = piece.rect
		var begin := DV_MAP.design_to_pixel(map_id,Vector3(float(r[0]),float(r[1]),0.0))
		var end := DV_MAP.design_to_pixel(map_id,Vector3(float(r[2]),float(r[3]),0.0))
		var dimensions := end-begin
		if dimensions.x <= 0.0 or dimensions.y <= 0.0: continue
		var collision := CollisionShape2D.new()
		collision.name = str(piece.id).replace("-","_")
		var shape := RectangleShape2D.new()
		shape.size = dimensions
		collision.position = (begin+end)*0.5
		collision.shape = shape
		solids.add_child(collision)
	for site in spec.sites:
		var record: Dictionary = spec.sites[site]
		var marker := Marker2D.new()
		marker.name = str(site)
		marker.position = Vector2(float(record.point[0]),float(record.point[1]))
		marker.set_meta("dv_id", "site:" + str(site))
		marker.set_meta("status", str(record.get("status","unconfigured")))
		get_node("Sites").add_child(marker)
