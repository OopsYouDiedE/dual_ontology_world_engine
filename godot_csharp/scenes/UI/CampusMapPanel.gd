extends VBoxContainer

## Material 3 Campus Map & Route Cognition Panel (Task 7.10)
## Strictly shows visited rooms, travelled routes, unvisited placeholder, and current target.
## Strictly forbids leaking unexplored floor geometry or real-time door lock states.

const MaterialControls := preload("res://scenes/UI/Components/MaterialControls.gd")

var data: Dictionary = {}

func _ready() -> void:
	size_flags_horizontal = Control.SIZE_EXPAND_FILL

func configure(value: Dictionary) -> void:
	data = value.duplicate(true)
	for child in get_children():
		remove_child(child)
		child.queue_free()
		
	var current_room_id := str(data.get("current_room", ""))
	var current_label := str(data.get("current_label", current_room_id))
	var target_label := str(data.get("target_label", data.get("target", "")))
	var rooms: Dictionary = data.get("rooms", {})
	var connections: Array = data.get("connections", [])
	
	add_theme_constant_override("separation", 14)
	
	# 1. Location & Target Status Card
	var status_card := PanelContainer.new()
	var box := StyleBoxFlat.new()
	box.bg_color = MaterialControls.COLOR_SURFACE_VARIANT
	box.corner_radius_top_left = 8
	box.corner_radius_top_right = 8
	box.corner_radius_bottom_right = 8
	box.corner_radius_bottom_left = 8
	box.content_margin_left = 14.0
	box.content_margin_top = 10.0
	box.content_margin_right = 14.0
	box.content_margin_bottom = 10.0
	status_card.add_theme_stylebox_override("panel", box)
	
	var status_vbox := VBoxContainer.new()
	status_vbox.add_theme_constant_override("separation", 4)
	
	var loc_lbl := Label.new()
	loc_lbl.text = "● 当前位置：%s" % current_label
	loc_lbl.add_theme_font_size_override("font_size", 16)
	loc_lbl.add_theme_color_override("font_color", MaterialControls.COLOR_PRIMARY)
	status_vbox.add_child(loc_lbl)
	
	var target_lbl := Label.new()
	if not target_label.is_empty() and target_label != "null":
		target_lbl.text = "◆ 当前指引目标：%s" % target_label
		target_lbl.add_theme_color_override("font_color", MaterialControls.COLOR_WARNING)
	else:
		target_lbl.text = "◆ 当前目标：自由探索中"
		target_lbl.add_theme_color_override("font_color", MaterialControls.COLOR_OUTLINE)
	target_lbl.add_theme_font_size_override("font_size", 14)
	status_vbox.add_child(target_lbl)
	status_card.add_child(status_vbox)
	add_child(status_card)
	
	# 2. Known Visited Rooms (Task 7.10)
	var rooms_header := Label.new()
	rooms_header.text = "已踏足地点 (实地探明)"
	rooms_header.add_theme_font_size_override("font_size", 15)
	add_child(rooms_header)
	
	var rooms_grid := GridContainer.new()
	rooms_grid.columns = 2
	rooms_grid.add_theme_constant_override("h_separation", 12)
	rooms_grid.add_theme_constant_override("v_separation", 6)
	add_child(rooms_grid)
	
	for room_id in rooms:
		var record: Dictionary = rooms[room_id]
		if not bool(record.get("visited", false)):
			continue
		var room_card := PanelContainer.new()
		var r_box := StyleBoxFlat.new()
		r_box.bg_color = MaterialControls.COLOR_SURFACE
		r_box.border_width_left = 1
		r_box.border_width_top = 1
		r_box.border_width_right = 1
		r_box.border_width_bottom = 1
		r_box.border_color = MaterialControls.COLOR_OUTLINE
		r_box.corner_radius_top_left = 6
		r_box.corner_radius_top_right = 6
		r_box.corner_radius_bottom_right = 6
		r_box.corner_radius_bottom_left = 6
		r_box.content_margin_left = 10.0
		r_box.content_margin_top = 6.0
		r_box.content_margin_right = 10.0
		r_box.content_margin_bottom = 6.0
		room_card.add_theme_stylebox_override("panel", r_box)
		
		var r_lbl := Label.new()
		var is_cur := str(room_id) == current_room_id
		r_lbl.text = ("▶ " if is_cur else "  ") + str(record.get("label", room_id))
		if is_cur:
			r_lbl.add_theme_color_override("font_color", MaterialControls.COLOR_PRIMARY)
		room_card.add_child(r_lbl)
		rooms_grid.add_child(room_card)
		
	# Unknown status marker: strictly prevents leaking undisclosed rooms
	var unknown_lbl := Label.new()
	unknown_lbl.text = "◌ 其余区域：[未知 · 尚未涉足，不显示预知轮廓与门锁]"
	unknown_lbl.add_theme_color_override("font_color", MaterialControls.COLOR_OUTLINE)
	unknown_lbl.add_theme_font_size_override("font_size", 13)
	add_child(unknown_lbl)
	
	add_child(HSeparator.new())
	
	# 3. Travelled Connections (Task 7.10)
	var conn_header := Label.new()
	conn_header.text = "已实际走过通行连接"
	conn_header.add_theme_font_size_override("font_size", 15)
	add_child(conn_header)
	
	if connections.is_empty():
		var no_conn := Label.new()
		no_conn.text = "暂无已确认通行的双向路线记录。"
		no_conn.add_theme_color_override("font_color", MaterialControls.COLOR_OUTLINE)
		add_child(no_conn)
	else:
		var conn_vbox := VBoxContainer.new()
		conn_vbox.add_theme_constant_override("separation", 6)
		for conn in connections:
			if not conn is Dictionary:
				continue
			var conn_lbl := Label.new()
			conn_lbl.text = "  ↔ " + str(conn.get("label", ""))
			conn_lbl.add_theme_font_size_override("font_size", 14)
			conn_vbox.add_child(conn_lbl)
		add_child(conn_vbox)
