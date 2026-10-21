extends Control

const GEOMETRY := preload("res://scenes/Host/GeometryCatalog.gd")
var data: Dictionary = {}

func configure(value: Dictionary) -> void:
	data = value
	custom_minimum_size = Vector2(620, 340)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	queue_redraw()

func _draw() -> void:
	if data.is_empty(): return
	var map_id := str(data.id)
	var known: Dictionary = data.rooms
	var rooms: Array[Dictionary] = []
	var bounds := Rect2()
	for room: Dictionary in GEOMETRY.map_data(map_id).get("rooms", []):
		if not known.has(str(room.id)): continue
		rooms.append(room)
		var raw: Array = room.rect
		var rect := Rect2(float(raw[0]), float(raw[1]), float(raw[2]), float(raw[3]))
		bounds = rect if bounds.size == Vector2.ZERO else bounds.merge(rect)
	if rooms.is_empty(): return
	var scale_value := minf((size.x - 44.0) / bounds.size.x, (size.y - 44.0) / bounds.size.y)
	var offset := (size - bounds.size * scale_value) / 2.0 - bounds.position * scale_value
	var ink := get_theme_color("font_color", "Label")
	var font := get_theme_default_font()
	for room: Dictionary in rooms:
		var id := str(room.id)
		var current := id == str(data.current_room)
		var record: Dictionary = known[id]
		for raw: Array in room.rects:
			var rect := Rect2(Vector2(float(raw[0]), float(raw[1])) * scale_value + offset, Vector2(float(raw[2]), float(raw[3])) * scale_value)
			draw_rect(rect, Color(ink, 0.16 if current else 0.055))
			draw_rect(rect, Color(ink, 0.8 if bool(record.visited) else 0.35), false, 2.0 if current else 1.0)
		var raw: Array = room.rect
		var center := Vector2(float(raw[0]) + float(raw[2]) / 2.0, float(raw[1]) + float(raw[3]) / 2.0) * scale_value + offset
		var label := str(record.label).left(18)
		var width := minf(float(raw[2]) * scale_value - 8.0, 240.0)
		draw_string(font, center + Vector2(-width / 2.0, 5.0), label, HORIZONTAL_ALIGNMENT_CENTER, width, 15, ink)
		if current:
			draw_circle(center + Vector2(0, 23), 4.0, ink)
	for door: Dictionary in GEOMETRY.map_data(map_id).get("doors", []):
		var from := str(door.from)
		var to := str(door.to)
		var admitted := false
		for connection: Dictionary in data.connections:
			if (str(connection.from) == from and str(connection.to) == to) or (str(connection.from) == to and str(connection.to) == from):
				admitted = true
				break
		if not admitted: continue
		var raw: Array = door.point
		var point := Vector2(float(raw[0]), float(raw[1])) * scale_value + offset
		draw_circle(point, 5.0, Color(0.73, 0.53, 0.27))
		draw_arc(point, 5.0, 0.0, TAU, 20, ink, 1.0)
