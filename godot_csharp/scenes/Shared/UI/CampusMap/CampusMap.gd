@tool
extends Control
## 地图显示与回放：地点、连线、当前位置与可见地点均由宿主数据提供。
## 编辑器示例仅用于调布局；空格暂停，F 倍速，Esc 跳过回放。

const Paper := preload("res://scenes/Shared/UI/Common/Paper.gd")

@export var map_size: Vector2 = Vector2(1000, 640):
	set(value):
		map_size = value
		custom_minimum_size = map_size
		queue_redraw()

@export var bg_color: Color = Color("ebe2c8"):
	set(value):
		bg_color = value
		queue_redraw()

@export var border_color: Color = Color(0.17, 0.13, 0.09, 0.35):
	set(value):
		border_color = value
		queue_redraw()

@export var link_color: Color = Color(0.17, 0.13, 0.09, 0.28):
	set(value):
		link_color = value
		queue_redraw()

@export var node_fill_color: Color = Color("f3ecd8"):
	set(value):
		node_fill_color = value
		queue_redraw()

@export var node_hover_color: Color = Color("f6eedb"):
	set(value):
		node_hover_color = value
		queue_redraw()

@export var node_current_color: Color = Color("e9d9b0"):
	set(value):
		node_current_color = value
		queue_redraw()

@export_group("Editor Preview")
@export var preview_all_nodes: bool = true:
	set(value):
		preview_all_nodes = value
		queue_redraw()

@export var preview_here: String = "":
	set(value):
		preview_here = value
		queue_redraw()
@export var preview_figures: Array[Dictionary] = []:
	set(value):
		preview_figures = value
		queue_redraw()

@export_group("")
## 仅编辑器预览的地点模板；运行时不据此解释地点或导航。
const PREVIEW_NODES := {
	"ArchiveFront": {"pos": Vector2(500, 70), "label": "名录库"},
	"Hall": {"pos": Vector2(500, 200), "label": "中央厅"},
	"Lecture": {"pos": Vector2(300, 200), "label": "讲堂"},
	"Resonance": {"pos": Vector2(120, 300), "label": "星仪院一角"},
	"Commons": {"pos": Vector2(500, 380), "label": "起居厅"},
	"DormW": {"pos": Vector2(370, 330), "label": "甲区"},
	"DormL": {"pos": Vector2(630, 330), "label": "丁区"},
	"DormS": {"pos": Vector2(370, 440), "label": "丙区"},
	"DormLife": {"pos": Vector2(630, 440), "label": "乙区"},
	"Logistics": {"pos": Vector2(820, 430), "label": "后勤院"},
	"Granary": {"pos": Vector2(900, 330), "label": "粮仓"},
	"Clinic": {"pos": Vector2(820, 220), "label": "诊疗所"},
	"Greenhouse": {"pos": Vector2(880, 90), "label": "温室"},
	"Oath": {"pos": Vector2(500, 560), "label": "誓约庭"},
}
const PREVIEW_LINKS := [["ArchiveFront", "Hall"], ["Hall", "Lecture"], ["Lecture", "Resonance"], ["Hall", "Commons"], ["Commons", "DormW"], ["Commons", "DormL"],
	["Commons", "DormS"], ["Commons", "DormLife"], ["Commons", "Oath"], ["Commons", "Logistics"], ["Logistics", "Granary"], ["Logistics", "Clinic"],
	["Clinic", "Greenhouse"], ["Logistics", "Oath"]]

@export var preview_links: Array = PREVIEW_LINKS.duplicate(true):
	set(value):
		preview_links = value
		queue_redraw()
@export var playback_hint: String = "空格 暂停　F 倍速（%sx）　Esc 跳过":
	set(value):
		playback_hint = value
		queue_redraw()
@export var playback_hint_size: int = 14:
	set(value):
		playback_hint_size = value
		queue_redraw()
@export var ink_color: Color = Color(0.12, 0.1, 0.09, 0.55):
	set(value):
		ink_color = value
		queue_redraw()

@export var link_width: float = 2.0
@export var border_inset: float = 6.0
@export var border_width: float = 1.0
@export var wing_outline: Rect2 = Rect2(300, 295, 400, 180)
@export var wing_outline_color: Color = Color(0.168627, 0.12549, 0.086275, 0.18)
@export var bubble_color: Color = Color(1, 0.98, 0.92, 0.92)
@export var bubble_border_color: Color = Color(0.168627, 0.12549, 0.086275, 0.5)
@export var bubble_font_size: int = 13
@export var figure_font_size: int = 12

signal picked(node: String)
signal playback_done

var mode := "travel"            # travel / replay
var here := ""
var known: Dictionary = {}      # travel：侦探知道的地点；replay：全部画出（那是假设，不是所见）
var figures: Array = []         # {pos, label, alpha, bubble}
var hovered := ""
var paused := false
var speed := 1.0
var skipping := false
var nodes: Dictionary = {}
var links: Array = []

func show_data(data: Dictionary) -> void:
	nodes = data.get("nodes", {}).duplicate(true)
	links = data.get("links", []).duplicate(true)
	known = data.get("known", {}).duplicate(true)
	here = str(data.get("here", ""))
	if here == "":
		for key in nodes:
			if str(nodes[key].get("map", "")) == str(data.get("map", "")) and str(data.get("map", "")) != "":
				here = key
				break
	if is_node_ready(): _prepare_locations()
	queue_redraw()

func _display_nodes() -> Dictionary:
	return PREVIEW_NODES if Engine.is_editor_hint() else nodes

func _prepare_locations() -> void:
	var display := _display_nodes()
	var locations: Control = $Locations
	for child in locations.get_children():
		child.visible = display.has(str(child.name))
	for key in display:
		var location := locations.get_node_or_null(str(key)) as Panel
		if location == null:
			location = locations.get_child(0).duplicate() as Panel
			location.name = str(key)
			locations.add_child(location)
		var position_data = display[key].get("pos", Vector2.ZERO)
		var center := Vector2.ZERO
		if position_data is Vector2: center = position_data
		elif position_data is Array and position_data.size() >= 2: center = Vector2(float(position_data[0]), float(position_data[1]))
		elif position_data is Dictionary: center = Vector2(float(position_data.get("x", 0)), float(position_data.get("y", 0)))
		location.position = center - location.size / 2.0
		location.get_node("Name").text = str(display[key].get("label", key))

func _ready() -> void:
	custom_minimum_size = map_size
	mouse_filter = Control.MOUSE_FILTER_STOP
	_prepare_locations()
	queue_redraw()

func _process(_delta: float) -> void:
	if Engine.is_editor_hint():
		queue_redraw()

func _gui_input(event: InputEvent) -> void:
	if Engine.is_editor_hint():
		return
	if mode != "travel":
		return
	if event is InputEventMouseMotion:
		var h := _node_at(event.position)
		if h != hovered:
			hovered = h
			queue_redraw()
	elif event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		var n := _node_at(event.position)
		if n != "":
			picked.emit(n)

func _shown(key: String) -> bool:
	if Engine.is_editor_hint():
		return preview_all_nodes or ["Hall", "Commons", "Lecture", "Oath"].has(key)
	return nodes.has(key) and (mode == "replay" or known.has(key))

func _node_at(p: Vector2) -> String:
	for key in _display_nodes():
		if not _shown(key):
			continue
		var location := get_node_or_null("Locations/" + key) as Control
		if location != null and Rect2(location.position - Vector2(2, 2), location.size + Vector2(4, 4)).has_point(p):
			return key
	return ""

func _draw() -> void:
	var draw_size: Vector2 = map_size
	draw_rect(Rect2(Vector2.ZERO, draw_size), bg_color)
	draw_rect(Rect2(Vector2.ONE * border_inset, draw_size - Vector2.ONE * border_inset * 2.0), border_color, false, border_width)
	var font := Paper.font()
	for link in (preview_links if Engine.is_editor_hint() else links):
		if link.size() < 2 or not _display_nodes().has(link[0]) or not _display_nodes().has(link[1]):
			continue
		if _shown(link[0]) and _shown(link[1]):
			draw_line(_position(link[0]), _position(link[1]), link_color, link_width)
	if Engine.is_editor_hint(): draw_rect(wing_outline, wing_outline_color, false, border_width)
	_sync_locations()
	for f in (preview_figures if Engine.is_editor_hint() else figures):
		_draw_ink(f.get("pos", Vector2.ZERO), float(f.get("alpha", 1.0)), str(f.get("label", "")))
		if str(f.get("bubble", "")) != "":
			var text: String = f.bubble
			var w := minf(260.0, 14.0 * text.length() + 16.0)
			var r := Rect2(f.pos + Vector2(-w / 2.0, -78), Vector2(w, 26))
			draw_rect(r, bubble_color)
			draw_rect(r, bubble_border_color, false, border_width)
			draw_string(font, r.position + Vector2(6, 18), text, HORIZONTAL_ALIGNMENT_LEFT, w - 12, bubble_font_size, Paper.INK)
	if mode == "replay":
		draw_string(font, Vector2(20, draw_size.y - 18), playback_hint % str(speed), HORIZONTAL_ALIGNMENT_LEFT, -1, playback_hint_size, Paper.MUTED)

func _position(key: String) -> Vector2:
	var location := get_node_or_null("Locations/" + key) as Control
	return location.position + location.size / 2.0 if location != null else Vector2.ZERO

func _sync_locations() -> void:
	var current := preview_here if Engine.is_editor_hint() else here
	for key in _display_nodes():
		var location := get_node_or_null("Locations/" + key) as Panel
		if location == null:
			continue
		location.visible = _shown(key)
		var fill := node_hover_color if key == hovered else (node_current_color if key == current else node_fill_color)
		var box := location.get_theme_stylebox("panel").duplicate() as StyleBoxFlat
		box.bg_color = fill
		box.border_color = Color(Paper.SEAL, 0.9) if key == hovered or key == current else Color(Paper.INK, 0.5)
		location.add_theme_stylebox_override("panel", box)
		location.get_node("Here").visible = key == current

func _draw_ink(p: Vector2, alpha: float, label_text: String) -> void:
	var ink := Color(ink_color, ink_color.a * alpha)
	draw_circle(p + Vector2(0, -30), 6, ink)
	draw_colored_polygon(PackedVector2Array([p + Vector2(-7, -23), p + Vector2(7, -23), p + Vector2(10, 0), p + Vector2(-10, 0)]), ink)
	draw_string(Paper.font(), p + Vector2(-50, 14), label_text, HORIZONTAL_ALIGNMENT_CENTER, 100, figure_font_size, Color(Paper.INK, 0.8 * alpha))

func _input(event: InputEvent) -> void:
	if Engine.is_editor_hint():
		return
	if mode != "replay" or not is_visible_in_tree() or event is not InputEventKey or not event.pressed or event.echo:
		return
	match event.keycode:
		KEY_SPACE: paused = not paused
		KEY_F: speed = 3.0 if speed == 1.0 else 1.0
		KEY_ESCAPE: skipping = true
		_: return
	queue_redraw()
	get_viewport().set_input_as_handled()

## 回放一夜：steps = [{t, who, name, build, from, to, act}]；named 为 true 时（全部吻合）写出名字与心声。
signal playback_cancelled
var cancelled := false
const PresentationWait := preload("res://scenes/Shared/PresentationWait.gd")

func cancel_playback() -> void:
	if cancelled: return
	cancelled = true
	playback_cancelled.emit()
	figures.clear()
	queue_redraw()

func _exit_tree() -> void:
	cancel_playback()

func _wait_frame() -> void:
	if cancelled: return
	await PresentationWait.until(get_tree().process_frame, playback_cancelled)

func play(steps: Array, named: bool) -> void:
	if Engine.is_editor_hint() or cancelled:
		return
	mode = "replay"
	figures.clear()
	skipping = false
	paused = false
	speed = 1.0
	var by_who := {}
	for s in steps:
		if not nodes.has(str(s.get("from", ""))) or not nodes.has(str(s.get("to", ""))): continue
		if not by_who.has(s.who):
			var start := str(s.from)
			by_who[s.who] = {"pos": Vector2.ZERO, "slot": by_who.size(), "label": str(s.name) if named else str(s.build), "alpha": 1.0, "bubble": ""}
			by_who[s.who].pos = _position(start) + _offset(int(by_who[s.who].slot))
			figures.append(by_who[s.who])
	queue_redraw()
	for s in steps:
		if cancelled: return
		if not by_who.has(s.who) or not nodes.has(str(s.get("to", ""))): continue
		if skipping:
			break
		var f: Dictionary = by_who[s.who]
		var target: Vector2 = _position(str(s.to)) + _offset(int(f.slot))
		if str(s.act) == "":
			await _tween(f, target, 0.55)
		else:
			f.bubble = str(s.act) if named else "……"
			queue_redraw()
			await _wait(1.1 if named else 0.5)
			f.bubble = ""
	if cancelled: return
	figures.clear()
	queue_redraw()
	playback_done.emit()

func _tween(f: Dictionary, target: Vector2, duration: float) -> void:
	var from: Vector2 = f.pos
	var t := 0.0
	while t < duration and not skipping and not cancelled:
		if not paused:
			t += get_process_delta_time() * speed
			f.pos = from.lerp(target, clampf(t / duration, 0, 1))
			queue_redraw()
		await _wait_frame()
	if not cancelled: f.pos = target

func _wait(seconds: float) -> void:
	var t := 0.0
	while t < seconds and not skipping and not cancelled:
		if not paused:
			t += get_process_delta_time() * speed
		await _wait_frame()

## 每个身影在地点框下方有自己的位子，免得叠在一起。
static func _offset(slot: int) -> Vector2:
	return Vector2(float(slot % 6) * 24.0 - 60.0, 54.0 + float((slot / 6) % 3) * 30.0)
