extends Node2D
## Coverage comes from the scene; image, scale and phase come from publication.
const SURFACE = preload("res://scenes/components/world/AuthoredSurface.gd")
const MEDALLION = preload("res://scenes/components/world/AuthoredMedallion.gd")
const ROOT := "res://assets/environment/AuthoredRooms/"
@export var authored_room := ""
@export var medallion_center := Vector2(-1, -1)

func _ready() -> void:
	var room := get_parent()
	var rows: Array = room.get_meta("terrain_rows", [])
	var publication = JSON.parse_string(FileAccess.get_file_as_string(ROOT + "publication.json"))
	if rows.is_empty() or not publication is Dictionary or not publication.get("rooms", {}).has(authored_room):
		push_error("Missing authored room surface or terrain_rows: " + str(room.name))
		return
	var spec: Dictionary = publication["rooms"][authored_room]
	var coverage: Array[Rect2] = []
	for y in range(rows.size()):
		var row := str(rows[y])
		for x in range(row.length()):
			if row[x] in ".DS":
				coverage.append(Rect2(x * 32, y * 32, 32, 32))
	var surface := SURFACE.new()
	surface.name = "AuthoredFloorBody"
	add_child(surface)
	var origin: Array = spec["world_phase_px"]
	surface.configure(load(ROOT + authored_room + "_floor.png"), float(spec["world_pixels_per_source_pixel"]), Vector2(float(origin[0]), float(origin[1])), coverage)
	if authored_room == "central_hall" and medallion_center.x >= 0:
		var ring := MEDALLION.new()
		ring.name = "AuthoredMedallionBorder"
		ring.position = medallion_center
		ring.scale = Vector2.ONE * float(spec["world_pixels_per_source_pixel"])
		add_child(ring)
