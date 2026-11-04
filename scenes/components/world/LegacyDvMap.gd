extends RefCounted
## Read-only logical-coordinate projection for the existing gameplay input API.
## Room bounds and walkability are derived from the selected 95 DV floor; no
## authored legacy 2D geometry is loaded or treated as spatial truth.
## This never edits Core state or synthesizes interaction readiness.
const COORDINATES = preload("res://scenes/components/world/DesignCoordinates.gd")
const CONTENT = preload("res://scenes/main/DvPresentationContent.cs")
const SCALE_PX_PER_M := 32.0
const BORDER_PX := 64.0
const SOURCE_FILE := "95-configurable-building.data.dv"
const SOURCE_ID := "space.configurable_building"
static var _cache: Dictionary = {}

# The binding belongs to the publication. The two defaults retain compatibility
# with older publications that predate map_floor_bindings.
static var _published_source: Dictionary = {}

static func invalidate_published() -> void:
	_cache.clear()
	_published_source.clear()

static func _source() -> Dictionary:
	if _published_source.is_empty():
		var reader = CONTENT.new()
		var record: Dictionary = reader.ReadPublishedData(SOURCE_FILE, SOURCE_ID)
		_published_source = record.get("value", {})
	return _published_source

static func _binding(map_id: String) -> Dictionary:
	var bindings: Dictionary = _source().get("map_floor_bindings", {})
	if not bindings.is_empty():
		var value = bindings.get(map_id, {})
		return value if value is Dictionary else {}
	# Compatibility for the previously published two-floor content.
	if map_id == "main_1f": return {"floor":"1F", "fallback_region":"J"}
	if map_id == "main_2f": return {"floor":"2F", "fallback_region":"foyer"}
	return {}

static func supports(map_id: String) -> bool:
	return not _binding(map_id).is_empty()

static func published_maps() -> Array:
	return _source().get("map_floor_bindings", {}).keys()

## Published flight/landing references supply the complete physical traversal.
## The flight's from/to fields describe its owning floor, not a map transfer.
static func floor_connections(map_id: String) -> Array:
	var result: Array = []
	for declaration: Dictionary in _source().get("host_floor_connections", []):
		var reverse := str(declaration.to_map) == map_id
		if not reverse and str(declaration.from_map) != map_id: continue
		var waypoints: Array = [declaration.start_design]
		var flights: Array = []
		for reference in declaration.flight_refs:
			for flight: Dictionary in _source().effective_stairs:
				if str(flight.id) == str(reference): flights.append(flight.segments[0])
		if flights.size() != declaration.flight_refs.size() or flights.is_empty():
			push_error("Floor connection has unresolved flight references: " + str(declaration.id))
			continue
		var landings: Array = []
		for reference in declaration.landing_refs:
			for floor: Dictionary in _source().floors:
				for piece: Dictionary in floor.pieces:
					if str(piece.id) == str(reference): landings.append(piece)
		if landings.size() != flights.size():
			push_error("Floor connection needs one real landing per flight: " + str(declaration.id))
			continue
		for index in range(flights.size()):
			var flight: Dictionary = flights[index]
			waypoints.append(flight.points[0])
			waypoints.append(flight.points[-1])
			if index == flights.size()-1: continue
			var landing: Dictionary = landings[index]
			var center := Vector2((float(landing.rect[0])+float(landing.rect[2]))*0.5,(float(landing.rect[1])+float(landing.rect[3]))*0.5)
			var end := Vector2(float(flight.points[-1][0]),float(flight.points[-1][1]))
			var start := Vector2(float(flight.points[0][0]),float(flight.points[0][1]))
			var next: Dictionary = flights[index+1]
			var next_start := Vector2(float(next.points[0][0]),float(next.points[0][1]))
			var next_end := Vector2(float(next.points[-1][0]),float(next.points[-1][1]))
			var direction := (end-start).normalized()
			var next_direction := (next_start-next_end).normalized()
			var a := end + direction*(center-end).dot(direction)
			var b := next_start + next_direction*(center-next_start).dot(next_direction)
			var elevation := float(landing.z)+float(landing.height)
			if absf(elevation-float(flight.points[-1][2]))>0.001 or absf(elevation-float(next.points[0][2]))>0.001:
				push_error("Floor connection landing elevation mismatch: " + str(declaration.id))
				waypoints.clear()
				break
			waypoints.append([a.x,a.y,elevation])
			waypoints.append([b.x,b.y,elevation])
		if waypoints.is_empty(): continue
		waypoints.append(declaration.end_design)
		if reverse: waypoints.reverse()
		var target := str(declaration.from_map if reverse else declaration.to_map)
		var entry := declaration.duplicate(true)
		entry["from_map"] = map_id
		entry["to_map"] = target
		entry["from_room"] = str(declaration.to_room if reverse else declaration.from_room)
		entry["to_room"] = str(declaration.from_room if reverse else declaration.to_room)
		entry["waypoints_design"] = waypoints
		entry["start_design"] = waypoints[0]
		entry["end_design"] = waypoints[-1]
		result.append(entry)
	return result

static func _floor_id(map_id: String) -> String:
	return str(_binding(map_id).get("floor", ""))

static func _load(map_id: String) -> Dictionary:
	if not supports(map_id):
		return {}
	if _cache.has(map_id):
		return _cache[map_id]
	var reader = CONTENT.new()
	var source: Dictionary = _source()
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
	var fallback := str(_binding(map_id).get("fallback_region", "unassigned"))
	# Semantic open-space regions are DV declarations over the published floor.
	# They describe room identity only; physical walls and walkability stay derived
	# from that same floor. No map transition or movement receipt is synthesized.
	for region in source.get("semantic_regions", []):
		if str(region.get("floor", "")) != _floor_id(map_id): continue
		var rectangles: Array = []
		for bounds in region.get("rects", []):
			var area := _box(origin, bounds)
			rectangles.append([area.position.x,area.position.y,area.size.x,area.size.y])
		if rectangles.is_empty(): continue
		var label := str(region.id)
		rooms.append({"id":map_id+":"+label,"region":label,
			"rect":rectangles[0],"rects":rectangles,"semantic_open_space":true})
		if label == fallback: fallback = "unassigned"
	rooms.append({"id":map_id + ":" + fallback,"region":fallback,
		"rect":[0,0,size.x,size.y],"rects":[[0,0,size.x,size.y]],
		"fallback_corridor":true})
	# Clear room bounds stop at the wall face. Assign each real door throat
	# to its adjoining circulation region, so crossing a thick wall does not
	# introduce a spurious fallback-room transition. Physics still owns clearance.
	var throat_regions: Array = []
	for opening in target.get("openings", []):
		if absf(float(opening.get("bottom", INF))-float(target.z)) > 0.05: continue
		var bounds: Array = opening.get("rect", [])
		if bounds.size() != 4: continue
		var throat := _box(origin, bounds)
		var center := throat.get_center()
		var choices: Array = []
		var best_distance := INF
		for candidate in rooms:
			if bool(candidate.get("fallback_corridor", false)): continue
			if not bool(candidate.get("semantic_open_space", false)): continue
			for raw in candidate.rects:
				var area := Rect2(float(raw[0]),float(raw[1]),float(raw[2]),float(raw[3]))
				if area.grow(1.0).intersects(throat):
					var distance := center.distance_squared_to(center.clamp(area.position,area.end))
					if distance < best_distance - 0.001:
						choices = [candidate]
						best_distance = distance
					elif absf(distance-best_distance) <= 0.001 and not choices.has(candidate):
						choices.append(candidate)
		if choices.size() == 1:
			throat_regions.append({"room":choices[0].id,"rect":[throat.position.x,throat.position.y,throat.size.x,throat.size.y]})
	var markers := {}
	var spawns := {}
	var stations := {}
	for anchor in source.get("spatial_anchors", []):
		if str(anchor.get("floor", "")) != _floor_id(map_id): continue
		var anchor_id := str(anchor.id)
		if str(anchor.get("host_map", map_id)) != map_id: continue
		var at := _point(origin,anchor.position)
		var entry := {"point":[at.x,at.y],"room":str(anchor.get("host_room", "")),
			"status":str(anchor.get("status", "unconfigured"))}
		if anchor_id.begins_with("site:"):
			var site_id := anchor_id.trim_prefix("site:")
			entry["path"] = "Sites/" + site_id
			entry["label"] = site_id
			markers[site_id] = entry
		elif anchor_id.begins_with("spawn:"):
			spawns[anchor_id.trim_prefix("spawn:")] = entry
		elif anchor_id.begins_with("station:"):
			stations[anchor_id.trim_prefix("station:")] = entry
	for spawn in source.get("host_spawns", []):
		if str(spawn.get("host_map", "")) != map_id: continue
		if str(spawn.get("floor", "")) != _floor_id(map_id): continue
		var at := _point(origin, spawn.position)
		spawns[str(spawn.id)] = {"point":[at.x,at.y], "room":str(spawn.get("host_room", ""))}
	var terrain_rows := _terrain_rows(target, origin, size)
	var map_record := {"scene":"res://scenes/maps/" + map_id + ".tscn",
		"size":[size.x,size.y], "rooms":rooms, "sites":markers,
		"stations":stations, "stands":{}, "spawns":spawns, "exits":[], "portal_regions":throat_regions,
		"doors":_doorways(target, origin, rooms),
		"terrain_rows":terrain_rows,
		"terrain_source":"95-configurable-building.data.dv/floors/" + _floor_id(map_id) + "/pieces[kind=floor]",
		"floor_id":_floor_id(map_id), "floor_elevation_m":float(target.z),
		"origin_design":[origin.x,origin.y], "source_definition":SOURCE_ID,
		"source_sha256":reader.PublishedDataSha256(SOURCE_FILE)}
	var result := {"catalog":map_record, "floor":target,"source":source,"origin":origin}
	_cache[map_id] = result
	return result

static func _region_at(rooms: Array, point: Vector2) -> String:
	for room in rooms:
		if bool(room.get("fallback_corridor", false)): continue
		for bounds in room.get("rects", []):
			if Rect2(float(bounds[0]),float(bounds[1]),float(bounds[2]),float(bounds[3])).has_point(point):
				return str(room.id)
	return ""

static func _doorways(floor: Dictionary, origin: Vector2, rooms: Array) -> Array:
	var result: Array = []
	var seen := {}
	# Openings above the floor are windows, not walking passages. Candidate
	# edges are derived from the actual aperture rectangles; physics still
	# decides whether a door leaf, lock or obstacle prevents movement.
	for opening in floor.get("openings", []):
		if absf(float(opening.get("bottom", INF))-float(floor.z)) > 0.05: continue
		var bounds: Array = opening.get("rect", [])
		if bounds.size() != 4: continue
		var center := Vector2((float(bounds[0])+float(bounds[2]))*0.5,(float(bounds[1])+float(bounds[3]))*0.5)
		var portal := _point(origin,[center.x,center.y])
		var pairs: Array = [
			[Vector2(float(bounds[0])-0.02,center.y),Vector2(float(bounds[2])+0.02,center.y)],
			[Vector2(center.x,float(bounds[1])-0.02),Vector2(center.x,float(bounds[3])+0.02)]]
		for pair in pairs:
			var a := _region_at(rooms,_point(origin,[pair[0].x,pair[0].y]))
			var b := _region_at(rooms,_point(origin,[pair[1].x,pair[1].y]))
			if a.is_empty() or b.is_empty() or a == b: continue
			var key := str(opening.id)+":"+a+":"+b
			if seen.has(key): continue
			seen[key] = true
			result.append({"from":a,"to":b,"point":[portal.x,portal.y],"opening_ref":str(opening.id)})
	# Adjacent named open-floor regions need no invented door. Only shared
	# boundaries become candidates, and the native capsule checks the crossing.
	for i in range(rooms.size()):
		if bool(rooms[i].get("fallback_corridor",false)): continue
		for j in range(i+1,rooms.size()):
			if bool(rooms[j].get("fallback_corridor",false)): continue
			if not bool(rooms[i].get("semantic_open_space",false)) or not bool(rooms[j].get("semantic_open_space",false)): continue
			for aa in rooms[i].rects:
				for bb in rooms[j].rects:
					var a := Rect2(float(aa[0]),float(aa[1]),float(aa[2]),float(aa[3]))
					var b := Rect2(float(bb[0]),float(bb[1]),float(bb[2]),float(bb[3]))
					var point := Vector2.INF
					var low := maxf(a.position.y,b.position.y)
					var high := minf(a.end.y,b.end.y)
					if high > low and (absf(a.end.x-b.position.x)<0.01 or absf(b.end.x-a.position.x)<0.01):
						point = Vector2(a.end.x if absf(a.end.x-b.position.x)<0.01 else b.end.x,(low+high)*0.5)
					low = maxf(a.position.x,b.position.x)
					high = minf(a.end.x,b.end.x)
					if high > low and (absf(a.end.y-b.position.y)<0.01 or absf(b.end.y-a.position.y)<0.01):
						point = Vector2((low+high)*0.5,a.end.y if absf(a.end.y-b.position.y)<0.01 else b.end.y)
					if point.is_finite(): result.append({"from":str(rooms[i].id),"to":str(rooms[j].id),"point":[point.x,point.y],"source":"DV semantic-region shared boundary"})
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
