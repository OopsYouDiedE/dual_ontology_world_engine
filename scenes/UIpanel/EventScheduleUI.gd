@tool
extends "res://scenes/UIpanel/CasebookUI/BookPageLayout.gd"

signal room_filtered(room_id: String)
signal slot_selected(person_id: String, time_slot: String)

var _mode: String = "timeline"
var _events: Array = []
var _selected: Dictionary = {}
var _compare: Dictionary = {}
var _compare_armed: bool = false
var _time_page: int = 0
var _room_filter: String = ""
var _replay: bool = false
var _map_points: Dictionary = {}
var _map_edges: Array = []

func configure(data: Dictionary, book_data: RefCounted = null) -> void:
	var requested_view: String = str(data.get("schedule_view", ""))
	if requested_view in ["timeline", "map", "todo"]: _mode = requested_view
	super.configure(data, book_data)

func _build_content() -> void:
	_collect_events()
	_begin("事件与日程", "记录对读" if not _compare.is_empty() else _title(_selected, "时序记录"))
	var tabs: HBoxContainer = _row(left_body, 4)
	for value in [["timeline", "时间轴"], ["map", "区域"], ["todo", "待办"]]:
		var key: String = str(value[0])
		var tab: Button = _button(tabs, str(value[1]), func(): _mode = key; _build_content(), _mode == key)
		tab.alignment = HORIZONTAL_ALIGNMENT_CENTER
		tab.add_theme_font_size_override("font_size", _scaled(18))
	if _mode == "map": _render_map()
	elif _mode == "todo": _render_todos()
	else: _render_timeline()
	_render_detail()

func _collect_events() -> void:
	_events.clear()
	var seen: Dictionary = {}
	for value: Dictionary in _entries("records") + _entries("testimony") + _entries("evidence"):
		var id: String = str(value.get("id", ""))
		if id.is_empty() or seen.has(id): continue
		_events.append(value)
		seen[id] = true
	var book: Dictionary = _dict(snapshot.get("casebook", {}))
	for value in _array(book.get("timeline", [])):
		if not value is Dictionary or not bool(value.get("acquired", value.get("known", false))): continue
		var id: String = str(value.get("id", ""))
		if not id.is_empty() and not seen.has(id):
			_events.append(value)
			seen[id] = true
	_events.sort_custom(func(a: Dictionary, b: Dictionary): return float(a.get("game_time", 0)) < float(b.get("game_time", 0)) if str(a.get("game_time", "")).is_valid_float() and str(b.get("game_time", "")).is_valid_float() else str(a.get("game_time", "")) < str(b.get("game_time", "")))
	if not _selected.is_empty():
		for value: Dictionary in _events:
			if str(value.get("id", "")) == str(_selected.get("id", "")):
				_selected = value
				break
	if _selected.is_empty() and not _events.is_empty(): _selected = _events.back()

func _person(value: Dictionary) -> String:
	var person: String = str(value.get("subject_name", value.get("speaker_name", value.get("actor_name", ""))))
	return person if not person.is_empty() else "本人"

func _moment(value: Dictionary) -> String:
	var time_value: Variant = value.get("game_time", "")
	var result: String = _time(time_value)
	if str(time_value).is_valid_float() and float(time_value) >= 1440:
		result = "第%d日\n%s" % [floori(float(time_value) / 1440.0) + 1, result]
	return result

func _is_heard(value: Dictionary) -> bool:
	return str(value.get("category", "")) == "testimony" or str(value.get("certainty", "")) == "subjective_statement" or str(value.get("type", "")) in ["rumor", "heard"]

func _render_timeline() -> void:
	var filtered: Array = []
	var persons: Array[String] = []
	var moments: Array[String] = []
	for value: Dictionary in _events:
		if not _room_filter.is_empty() and str(value.get("place", "")) != _room_filter: continue
		filtered.append(value)
		var who: String = _person(value)
		var when: String = _moment(value)
		if not persons.has(who): persons.append(who)
		if not moments.has(when): moments.append(when)
	if not _room_filter.is_empty():
		_button(left_body, _room_filter + "  ×", func(): _room_filter = ""; _time_page = 0; _build_content())
	if filtered.is_empty():
		_empty(left_body, "尚无行动记录")
		return
	var total: int = maxi(1, ceili(moments.size() / 3.0))
	_time_page = clampi(_time_page, 0, total - 1)
	var times: Array = moments.slice(_time_page * 3, mini(moments.size(), (_time_page + 1) * 3))
	var content: VBoxContainer = _scroll(left_body)
	var grid: GridContainer = GridContainer.new()
	grid.columns = times.size() + 1
	grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	grid.add_theme_constant_override("h_separation", 10)
	grid.add_theme_constant_override("v_separation", 12)
	content.add_child(grid)
	var first: Label = _text(grid, "", 17, true)
	first.custom_minimum_size.x = 92
	for moment in times:
		var caption: Label = _text(grid, str(moment), 17, true)
		caption.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		caption.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	for person in persons:
		var name_label: Label = _text(grid, person, 19)
		name_label.custom_minimum_size.x = 92
		name_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		for moment in times:
			var matches: Array = []
			for event: Dictionary in filtered:
				if _person(event) == person and _moment(event) == str(moment): matches.append(event)
			if matches.is_empty():
				var blank: Label = _text(grid, "·", 18, true)
				blank.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
				blank.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
				blank.custom_minimum_size = Vector2(100, 70)
				blank.size_flags_horizontal = Control.SIZE_EXPAND_FILL
				continue
			var selected_index: int = -1
			for index in matches.size():
				if str(matches[index].get("id", "")) == str(_selected.get("id", "")): selected_index = index
			var shown: Dictionary = matches[maxi(0, selected_index)]
			var value: Dictionary = matches[(selected_index + 1) % matches.size()] if selected_index >= 0 else matches[0]
			var caption: String = ("?  " if _is_heard(shown) else "") + (str(shown.get("place", "")) if not str(shown.get("place", "")).is_empty() else "记录")
			if matches.size() > 1: caption += " ·%d" % matches.size()
			var cell: Button = _button(grid, caption, func(): _select_event(value), selected_index >= 0)
			cell.custom_minimum_size = Vector2(100, 70)
			cell.alignment = HORIZONTAL_ALIGNMENT_CENTER
			cell.add_theme_font_size_override("font_size", _scaled(17))
			cell.tooltip_text = _title(shown) + "\n" + _source(shown)
			cell.draw.connect(func(): _draw_cell(cell, _is_heard(shown)))
	_pager(left_body, _time_page, total, func(index: int): _time_page = index; _build_content())
	var legend: HBoxContainer = _row(left_body, 18)
	_text(legend, "□ 亲见", 15, true)
	_text(legend, "┄? 听闻", 15, true)

func _draw_cell(cell: Control, heard: bool) -> void:
	var rect: Rect2 = Rect2(Vector2(1, 1), cell.size - Vector2(2, 2))
	var color: Color = Color(_muted, 0.50)
	if not heard:
		cell.draw_rect(rect, color, false, 1.0)
		return
	var corners: Array = [rect.position, Vector2(rect.end.x, rect.position.y), rect.end, Vector2(rect.position.x, rect.end.y)]
	for i in range(4): cell.draw_dashed_line(corners[i], corners[(i + 1) % 4], color, 1.0, 5.0)

func _select_event(value: Dictionary) -> void:
	if _compare_armed and not _selected.is_empty() and str(value.get("id", "")) != str(_selected.get("id", "")):
		_compare = _selected.duplicate(true)
		_compare_armed = false
	_selected = value
	_replay = false
	_build_content()
	slot_selected.emit(_person(value), _moment(value))

func _render_detail() -> void:
	if _selected.is_empty():
		_empty(right_body, "等待新的记录")
		return
	var content: VBoxContainer = _scroll(right_body)
	if not _compare.is_empty():
		_text(content, _title(_compare), 23)
		_text(content, _source(_compare), 16, true)
		_prose(content, _body(_compare), 20)
		_rule(content)
		_text(content, _title(_selected), 23)
	_text(content, _source(_selected), 16, true)
	var lines: Array = _array(_selected.get("lines", []))
	if _replay and not lines.is_empty():
		for line in lines:
			if line is Dictionary:
				var speaker: String = str(line.get("speaker_name", line.get("label", "")))
				if not speaker.is_empty(): _text(content, speaker, 17, true)
				_prose(content, str(line.get("text", "")), 21)
	else:
		_prose(content, _body(_selected), 22)
	var collaboration: Dictionary = _dict(_selected.get("collaboration", _selected.get("collab", {})))
	if not collaboration.is_empty():
		_rule(content)
		_text(content, str(collaboration.get("partner", "")), 20)
		_text(content, str(collaboration.get("status_label", collaboration.get("status", ""))), 18, true)
	var contradiction: String = str(_selected.get("contradiction", ""))
	if not contradiction.is_empty(): _text(content, contradiction, 19).add_theme_color_override("font_color", _red)
	var actions: HBoxContainer = _row(right_body)
	if not lines.is_empty(): _button(actions, "原记录" if _replay else "回看对白", func(): _replay = not _replay; _build_content())
	if not _compare.is_empty():
		_button(actions, "记为疑点", _note_comparison)
		_button(actions, "结束对读", func(): _compare = {}; _build_content())
	else:
		_button(actions, "取消选取" if _compare_armed else "对读", func(): _compare_armed = not _compare_armed; _build_content(), _compare_armed)

func _note_comparison() -> void:
	if archive == null or _compare.is_empty(): return
	var ids: Array = [str(_compare.get("id", "")), str(_selected.get("id", ""))]
	var id: String = archive.add_note(_title(_compare) + "\n" + _title(_selected), ids, "记录对读")
	var note: Dictionary = _entry(id)
	if not note.is_empty(): note["judgement"] = "doubt"
	ui_event.emit({"kind": "book_page", "page": "B05", "entry_id": id})

func _render_map() -> void:
	var rooms: Dictionary = {}
	_map_edges.clear()
	var current_id: String = str(snapshot.get("execution_room", snapshot.get("current_room", "")))
	var current_label: String = str(_dict(snapshot.get("hud", {})).get("room", ""))
	if not current_id.is_empty(): rooms[current_id] = current_label if not current_label.is_empty() else "当前位置"
	for key in _dict(snapshot.get("known_rooms", {})):
		var value: Variant = snapshot.known_rooms[key]
		if value is Dictionary and bool(value.get("visited", false)): rooms[str(key)] = str(value.get("label", "已访地点"))
	for door in _array(_dict(snapshot.get("map_knowledge", {})).get("doors", [])):
		if not door is Dictionary or not bool(door.get("passage_observed", false)): continue
		var from: Dictionary = _dict(door.get("from", {}))
		var to: Dictionary = _dict(door.get("to", {}))
		var a: String = str(from.get("room", ""))
		var b: String = str(to.get("room", ""))
		if a.is_empty() or b.is_empty(): continue
		rooms[a] = str(from.get("label", "已访地点"))
		rooms[b] = str(to.get("label", "已访地点"))
		_map_edges.append([a, b])
	for edge in _array(snapshot.get("known_connections", [])):
		if not edge is Dictionary: continue
		var a: String = str(edge.get("from_room", edge.get("from", "")))
		var b: String = str(edge.get("to_room", edge.get("to", "")))
		if rooms.has(a) and rooms.has(b): _map_edges.append([a, b])
	if rooms.is_empty():
		_empty(left_body, "尚无区域记录")
		return
	var map_content: VBoxContainer = _scroll(left_body)
	var diagram: Control = Control.new()
	diagram.custom_minimum_size = Vector2(0, maxf(320, ceili(rooms.size() / 3.0) * 106 + 70))
	diagram.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	map_content.add_child(diagram)
	var keys: Array = rooms.keys()
	var layout: Callable = func():
		if not is_instance_valid(diagram) or not diagram.is_inside_tree(): return
		_map_points.clear()
		for index in keys.size():
			var column: int = index % 3
			var row: int = index / 3
			var p: Vector2 = Vector2(28 + column * maxf(110, (diagram.size.x - 190) / 2.0), 38 + row * 106)
			_map_points[str(keys[index])] = p + Vector2(72, 26)
			var button: Button = diagram.get_child(index) as Button
			button.position = p
			diagram.queue_redraw()
	for key in keys:
		var room_id: String = str(key)
		var room_name: String = str(rooms[key])
		var button: Button = _button(diagram, room_name, func(): _room_filter = room_name; _mode = "timeline"; _time_page = 0; room_filtered.emit(room_id); _build_content(), room_id == current_id)
		button.custom_minimum_size = Vector2(144, 52)
		button.size = Vector2(144, 52)
		button.add_theme_font_size_override("font_size", _scaled(17))
		button.tooltip_text = room_name
	diagram.resized.connect(layout)
	diagram.draw.connect(func():
		for edge: Array in _map_edges:
			if _map_points.has(edge[0]) and _map_points.has(edge[1]): diagram.draw_line(_map_points[edge[0]], _map_points[edge[1]], Color(_accent, 0.28), 1.5, true)
	)
	layout.call_deferred()
	_text(left_body, current_label, 18, true)

func _render_todos() -> void:
	var todos: Array = _array(_dict(snapshot.get("casebook", {})).get("todos", []))
	var collaborations: Array = _array(_dict(snapshot.get("collaboration", {})).get("arrangements", []))
	if todos.is_empty() and collaborations.is_empty():
		_empty(left_body, "暂无约定")
		return
	var content: VBoxContainer = _scroll(left_body)
	for value in todos + collaborations:
		if not value is Dictionary: continue
		var row: VBoxContainer = _column(content, 4)
		_button(row, _title(value, str(value.get("task", "约定"))), func(): _selected = value; _build_content())
		var info: String = str(value.get("time_label", value.get("time", ""))) + "  " + str(value.get("location", value.get("place", "")))
		_text(row, info.strip_edges(), 17, true)
		var status: String = str(value.get("status_label", ""))
		if not status.is_empty(): _text(row, status, 17, true)

func _reset_view() -> void:
	_selected = {}
	_compare = {}
	_compare_armed = false
	_room_filter = ""
	_time_page = 0
