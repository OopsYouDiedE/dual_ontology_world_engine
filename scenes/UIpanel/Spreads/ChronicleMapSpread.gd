extends Control

## 通用日程矩阵+地图联动模板（ChronicleMapSpread.gd）
## 吸收合并原 EventScheduleUI (B03), CampusMapPanel, CollaborationUI。
## 左页展示角色作息与时间轴矩阵，右页展示学院矢量地图与同伴协作指派。

signal slot_selected(person_id: String, time_slot: String)
signal room_selected(room_id: String)
signal collaboration_assigned(companion_id: String, room_id: String)

@onready var left_title: Label = $HBoxContainer/LeftPage/Margin/VBox/Header/LeftTitle
@onready var day_selector: OptionButton = $HBoxContainer/LeftPage/Margin/VBox/Header/DaySelector
@onready var timeline_grid: VBoxContainer = $HBoxContainer/LeftPage/Margin/VBox/Scroll/TimelineGrid

@onready var right_title: Label = $HBoxContainer/RightPage/Margin/VBox/Header/RightTitle
@onready var map_canvas: Control = $HBoxContainer/RightPage/Margin/VBox/MapBox/MapCanvas
@onready var selected_room_label: Label = $HBoxContainer/RightPage/Margin/VBox/RoomInfoBox/RoomLabel
@onready var companion_selector: OptionButton = $HBoxContainer/RightPage/Margin/VBox/CollaborationBox/CompanionSelector
@onready var btn_assign: Button = $HBoxContainer/RightPage/Margin/VBox/CollaborationBox/AssignButton

var _current_day := 1
var _selected_room := "archive"
var _rooms := {
	"archive": {"name": "学院主案室", "pos": Vector2(180, 140), "size": Vector2(100, 70)},
	"library": {"name": "古文献藏书阁", "pos": Vector2(320, 120), "size": Vector2(120, 80)},
	"courtyard": {"name": "中庭回廊", "pos": Vector2(240, 240), "size": Vector2(140, 90)},
	"laboratory": {"name": "源质炼金工坊", "pos": Vector2(100, 280), "size": Vector2(110, 80)},
	"dormitory": {"name": "教职员寓所", "pos": Vector2(420, 260), "size": Vector2(110, 80)},
	"lighthouse": {"name": "海崖灯塔", "pos": Vector2(260, 390), "size": Vector2(90, 70)}
}

var _characters := [
	{"id": "detective_huo", "name": "霍恩侦探", "schedule": ["主案室搜查", "中庭问询", "藏书阁检索", "寓所就寝"]},
	{"id": "lucian_gu", "name": "露西安·顾", "schedule": ["温室培植", "藏书阁自习", "中庭漫步", "教职员寓所"]},
	{"id": "elena_moran", "name": "艾莲娜·莫兰", "schedule": ["主案室归档", "炼金工坊取样", "炼金工坊", "教职员寓所"]},
	{"id": "shen_su", "name": "沈宿", "schedule": ["巡视灯塔", "中庭守备", "海崖巡视", "哨戒所就寝"]}
]

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	day_selector.clear()
	for i in range(1, 6):
		day_selector.add_item("第 %d 日因果时序" % i, i)
	day_selector.item_selected.connect(func(idx): select_day(idx + 1))

	companion_selector.clear()
	companion_selector.add_item("同伴: 艾莲娜·莫兰", 0)
	companion_selector.add_item("同伴: 露西安·顾", 1)

	btn_assign.pressed.connect(_on_assign_pressed)
	map_canvas.draw.connect(_on_map_draw)
	map_canvas.gui_input.connect(_on_map_gui_input)

	_render_timeline()
	select_room("archive")

func configure(_data: Dictionary, _book_data: RefCounted = null) -> void:
	_render_timeline()

func select_day(day_idx: int) -> void:
	_current_day = day_idx
	_render_timeline()

func _render_timeline() -> void:
	for child in timeline_grid.get_children():
		child.queue_free()

	for char_data in _characters:
		var row := PanelContainer.new()
		var margin := MarginContainer.new()
		margin.add_theme_constant_override("margin_left", 8)
		margin.add_theme_constant_override("margin_top", 6)
		margin.add_theme_constant_override("margin_right", 8)
		margin.add_theme_constant_override("margin_bottom", 6)
		row.add_child(margin)

		var hbox := HBoxContainer.new()
		hbox.add_theme_constant_override("separation", 10)
		margin.add_child(hbox)

		var name_lbl := Label.new()
		name_lbl.custom_minimum_size = Vector2(90, 0)
		name_lbl.text = char_data["name"]
		name_lbl.add_theme_font_size_override("font_size", 13)
		hbox.add_child(name_lbl)

		var times: Array = ["清晨", "午后", "黄昏", "深夜"]
		var sched: Array = char_data["schedule"]
		for i in range(4):
			var act_btn := Button.new()
			act_btn.size_flags_horizontal = Control.SIZE_EXPAND_FILL
			var act_title: String = sched[i] if i < sched.size() else "空闲"
			act_btn.text = "%s: %s" % [times[i], act_title]
			act_btn.add_theme_font_size_override("font_size", 11)
			act_btn.pressed.connect(func():
				slot_selected.emit(char_data["id"], times[i])
				_auto_highlight_room_for_activity(act_title)
			)
			hbox.add_child(act_btn)

		timeline_grid.add_child(row)

func _auto_highlight_room_for_activity(activity: String) -> void:
	if "藏书" in activity: select_room("library")
	elif "中庭" in activity: select_room("courtyard")
	elif "工坊" in activity or "炼金" in activity: select_room("laboratory")
	elif "寓所" in activity: select_room("dormitory")
	elif "灯塔" in activity or "海崖" in activity: select_room("lighthouse")
	else: select_room("archive")

func select_room(room_id: String) -> void:
	_selected_room = room_id
	if _rooms.has(room_id):
		var r_data: Dictionary = _rooms[room_id]
		selected_room_label.text = "当前指派选中区域: " + r_data["name"] + " (" + room_id + ")"
	room_selected.emit(room_id)
	map_canvas.queue_redraw()

func _on_map_draw() -> void:
	# Draw corridors / topological edges
	var edges := [
		["archive", "courtyard"],
		["library", "courtyard"],
		["laboratory", "courtyard"],
		["dormitory", "courtyard"],
		["courtyard", "lighthouse"]
	]
	for e in edges:
		if _rooms.has(e[0]) and _rooms.has(e[1]):
			var c1: Vector2 = _rooms[e[0]]["pos"] + _rooms[e[0]]["size"] * 0.5
			var c2: Vector2 = _rooms[e[1]]["pos"] + _rooms[e[1]]["size"] * 0.5
			map_canvas.draw_line(c1, c2, Color(0.6, 0.55, 0.45, 0.6), 3.0)

	# Draw room rects
	for r_id in _rooms:
		var r_info: Dictionary = _rooms[r_id]
		var rect := Rect2(r_info["pos"], r_info["size"])
		var is_sel: bool = (r_id == _selected_room)
		var fill_col := Color("967b5c") if is_sel else Color("d8cdb8")
		var border_col := Color("4a3c2c") if is_sel else Color("7a6a56")

		map_canvas.draw_rect(rect, fill_col)
		map_canvas.draw_rect(rect, border_col, false, 2.0)
		map_canvas.draw_string(ThemeDB.fallback_font, r_info["pos"] + Vector2(10, 35), r_info["name"], HORIZONTAL_ALIGNMENT_LEFT, -1, 13, Color("201b15"))

func _on_map_gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		var m_pos := (event as InputEventMouseButton).position
		for r_id in _rooms:
			var rect := Rect2(_rooms[r_id]["pos"], _rooms[r_id]["size"])
			if rect.has_point(m_pos):
				select_room(r_id)
				break

func _on_assign_pressed() -> void:
	var companion_id := "elena_moran" if companion_selector.selected == 0 else "lucian_gu"
	collaboration_assigned.emit(companion_id, _selected_room)
	selected_room_label.text += " [已指派 %s 前往协同调查]" % ("艾莲娜" if companion_id == "elena_moran" else "露西安")
