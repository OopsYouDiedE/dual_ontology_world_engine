extends VBoxContainer
## An abstract list, not a spatial reconstruction. Geometry is not projected.
var data: Dictionary = {}

func configure(value: Dictionary) -> void:
	data = value.duplicate(true)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	for child in get_children():
		remove_child(child)
		child.queue_free()
	var rooms: Dictionary = data.get("rooms", {})
	var current := str(data.get("current_room", ""))
	for room_id in rooms:
		var record: Dictionary = rooms[room_id]
		if not bool(record.get("visited", false)): continue
		var item := Label.new()
		item.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		item.text = ("● " if str(room_id) == current else "• ") + str(record.get("label", ""))
		add_child(item)
