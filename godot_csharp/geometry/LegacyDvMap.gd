extends RefCounted
## Read-only logical-coordinate projection for the existing gameplay input API.
## Room bounds and walkability are derived from the selected 95 DV floor; no
## authored legacy 2D geometry is loaded or treated as spatial truth.
## This never edits Core state or synthesizes interaction readiness.
const COORDINATES = preload("res://geometry/DesignCoordinates.gd")
const CONTENT = preload("res://scenes/Host/DvPresentationContent.cs")
const SCALE_PX_PER_M := 32.0
const BORDER_PX := 64.0
const SOURCE_FILE := "95-configurable-building.data.dv"
const SOURCE_ID := "space.configurable_building"
static var _cache: Dictionary = {}

static func supports(map_id: String) -> bool:
	return map_id == "main_1f" or map_id == "main_2f"

static func _floor_id(map_id: String) -> String:
	return "1F" if map_id == "main_1f" else "2F"

static func _load(map_id: String) -> Dictionary:
	if not supports(map_id):
		return {}
	if _cache.has(map_id):
		return _cache[map_id]
	var reader = CONTENT.new()
	var record: Dictionary = reader.ReadPublishedData(SOURCE_FILE, SOURCE_ID)
	var source: Dictionary = record.get("value", {})
	if source.get("version", "") != "configurable-building-v1" or source.get("coordinate_space", "") != "building-local":
		push_error("Legacy DV map requires the selected, building-local spatial publication: " + map_id)
		return {}
	var target: Dictionary = {}
	for floor in source.get("floors", []):
		if str(floor.get("id", "")) == _floor_id(map_id):
			target = floor
			break
	if target.is_empty():
		push_error("Legacy DV map missing declared floor: " + map_id)
		return {}
	var min_x := INF
	var min_y := INF
	var max_x := -INF
	var max_y := -INF
	for rect in target.get("footprint", []):
		min_x = minf(min_x, float(rect[0]))
		min_y = minf(min_y, float(rect[1]))
		max_x = maxf(max_x, float(rect[2]))
		max_y = maxf(max_y, float(rect[3]))
	if not is_finite(min_x) or max_x <= min_x or max_y <= min_y:
		push_error("Legacy DV map invalid declared footprint: " + map_id)
		return {}
	var origin := Vector2(min_x, min_y)
	var size := Vector2(ceilf((max_x-min_x)*SCALE_PX_PER_M),ceilf((max_y-min_y)*SCALE_PX_PER_M)) + Vector2.ONE * BORDER_PX * 2
	var rooms: Array = []
	for room in target.rooms:
		var b: Array = room.bbox
		var rect := _box(origin, b)
		var label := str(room.id).replace("-", "")
		rooms.append({"id":map_id + ":" + label, "region":label,
			"rect":[rect.position.x,rect.position.y,rect.size.x,rect.size.y],
			"rects":[[rect.position.x,rect.position.y,rect.size.x,rect.size.y]],
			"dv_room_id":str(room.id)})
	var fallback := "J" if map_id == "main_1f" else "foyer"
	rooms.append({"id":map_id + ":" + fallback,"region":fallback,
		"rect":[0,0,size.x,size.y],"rects":[[0,0,size.x,size.y]],
		"fallback_corridor":true})
	var markers := {}
	for anchor in source.get("spatial_anchors", []):
		if str(anchor.get("floor", "")) != _floor_id(map_id): continue
		var anchor_id := str(anchor.id)
		if not anchor_id.begins_with("site:"): continue
		var site_id := anchor_id.trim_prefix("site:")
		var at := _point(origin,anchor.position)
		markers[site_id] = {"point":[at.x,at.y],"path":"Sites/"+site_id,
			"label":site_id,"room":str(anchor.host_room),
			"status":str(anchor.get("status", "unconfigured"))}
	var terrain_rows := _terrain_rows(target, origin, size)
	var map_record := {"scene":"res://scenes/World/Maps/" + map_id + ".tscn",
		"size":[size.x,size.y], "rooms":rooms, "sites":markers,
		"stations":{}, "stands":{}, "spawns":{}, "exits":[],
		"terrain_rows":terrain_rows,
		"terrain_source":"95-configurable-building.data.dv/floors/" + _floor_id(map_id) + "/pieces[kind=floor]",
		"floor_id":_floor_id(map_id), "floor_elevation_m":float(target.z),
		"origin_design":[origin.x,origin.y], "source_definition":SOURCE_ID,
		"source_sha256":reader.PublishedDataSha256(SOURCE_FILE)}
	var result := {"catalog":map_record, "floor":target,"source":source,"origin":origin}
	_cache[map_id] = result
	return result

static func _terrain_rows(floor: Dictionary, origin: Vector2, size: Vector2) -> Array:
	# The legacy host's logical navigation grid is a read-only projection of
	# published 3D floor rectangles. It contains no authored 2D room geometry.
	var columns := int(ceil(size.x / 32.0))
	var row_count := int(ceil(size.y / 32.0))
	var cells: Array = []
	for y in range(row_count):
		var row := PackedStringArray()
		row.resize(columns)
		for x in range(columns):
			row[x] = " "
		cells.append(row)
	for piece in floor.get("pieces", []):
		if piece.get("kind", "") != "floor": continue
		var rect: Array = piece.get("rect", [])
		if rect.size() != 4: continue
		var left := (float(rect[0]) - origin.x) * SCALE_PX_PER_M + BORDER_PX
		var top := (float(rect[1]) - origin.y) * SCALE_PX_PER_M + BORDER_PX
		var right := (float(rect[2]) - origin.x) * SCALE_PX_PER_M + BORDER_PX
		var bottom := (float(rect[3]) - origin.y) * SCALE_PX_PER_M + BORDER_PX
		var x0 := maxi(0, int(ceil((left - 16.0) / 32.0)))
		var y0 := maxi(0, int(ceil((top - 16.0) / 32.0)))
		var x1 := mini(columns - 1, int(floor((right - 16.0) / 32.0)))
		var y1 := mini(row_count - 1, int(floor((bottom - 16.0) / 32.0)))
		if x1 < x0 or y1 < y0: continue
		for y in range(y0, y1 + 1):
			var row: PackedStringArray = cells[y]
			for x in range(x0, x1 + 1):
				row[x] = "."
			cells[y] = row
	var result: Array = []
	for row: PackedStringArray in cells:
		result.append("".join(row))
	return result

static func _point(origin: Vector2, source: Array) -> Vector2:
	return (Vector2(float(source[0]),float(source[1]))-origin)*SCALE_PX_PER_M+Vector2.ONE*BORDER_PX

static func _box(origin: Vector2, b: Array) -> Rect2:
	var first := _point(origin,[b[0],b[1]])
	var last := _point(origin,[b[2],b[3]])
	return Rect2(first,last-first)

static func map_data(map_id: String) -> Dictionary:
	return _load(map_id).get("catalog", {})

static func spatial_floor(map_id: String) -> Dictionary:
	return _load(map_id).get("floor", {})

static func spatial_source(map_id: String) -> Dictionary:
	return _load(map_id).get("source", {})

static func design_to_pixel(map_id: String, point: Vector3) -> Vector2:
	var data := _load(map_id)
	if data.is_empty(): return Vector2.INF
	return _point(data.origin,[point.x,point.y])

static func pixel_to_design(map_id: String, point: Vector2) -> Vector3:
	var data := _load(map_id)
	if data.is_empty(): return Vector3.INF
	var xy: Vector2 = (point-Vector2.ONE*BORDER_PX)/SCALE_PX_PER_M+data.origin
	return Vector3(xy.x,xy.y,float(data.catalog.floor_elevation_m))

static func pixel_to_godot(map_id: String, point: Vector2) -> Vector3:
	return COORDINATES.to_godot(pixel_to_design(map_id,point))
