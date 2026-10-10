@tool
extends Control
## 学园线路图（贝克式）：站点吸附 32 像素网格，连线只走横、竖、45 度；地点、连线、当前位置与可见地点均由宿主数据提供。
## 编辑器示例取自 Workbench/samples/campus_preview.json，仅用于调布局；空格暂停，F 倍速，Esc 跳过回放。

const Paper := preload("res://scenes/UI/Components/Paper.gd")
const PREVIEW_SAMPLE := "res://scenes/UI/Workbench/samples/campus_preview.json"
const STATION_OUTLINE := 2.0
const PERSON := ""
const HALF := Vector2(0.5, 0.5)

@export var map_size: Vector2 = Vector2(1000, 640):
	set(value):
		map_size = value
		custom_minimum_size = map_size
		if is_node_ready(): _prepare_locations()
		queue_redraw()

@export_group("Editor Preview")
@export var preview_all_nodes: bool = true:
	set(value):
		preview_all_nodes = value
		queue_redraw()

## 为空时取示例文件的当前位置。
@export var preview_here: String = "":
	set(value):
		preview_here = value
		queue_redraw()
@export var preview_figures: Array[Dictionary] = []:
	set(value):
		preview_figures = value
		queue_redraw()
## 为空时取示例文件的连线。
@export var preview_links: Array = []:
	set(value):
		preview_links = value
		queue_redraw()
@export var playback_hint: String = "空格 暂停　F 倍速（%sx）　Esc 跳过":
	set(value):
		playback_hint = value
		queue_redraw()
@export var wing_outline: Rect2 = Rect2(300, 295, 400, 180)

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

var _preview: Dictionary = {}
var _preview_loaded := false
var _centers: Dictionary = {}       # 地点 -> 站点圆心（取整后的面板坐标）
var _exits: Dictionary = {}         # 地点 -> 连线离开该站的方向（各分量为 -1/0/1）
var _name_rects: Dictionary = {}    # 地点 -> 站名矩形
var _name_sides: Dictionary = {}    # 地点 -> 站名所在一侧
var _here_rect := Rect2()
var _origin := Vector2.ZERO         # 让整张图居中的平移量

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

# ---------------------------------------------------------------- data ---

func _load_preview() -> void:
	if _preview_loaded or not Engine.is_editor_hint():
		return
	_preview_loaded = true
	if not FileAccess.file_exists(PREVIEW_SAMPLE):
		return
	var parsed = JSON.parse_string(FileAccess.get_file_as_string(PREVIEW_SAMPLE))
	if parsed is Dictionary:
		_preview = parsed

func _display_nodes() -> Dictionary:
	if not Engine.is_editor_hint():
		return nodes
	_load_preview()
	return _preview.get("nodes", {})

func _display_links() -> Array:
	if not Engine.is_editor_hint():
		return links
	_load_preview()
	if not preview_links.is_empty():
		return preview_links
	return _preview.get("links", [])

func _display_here() -> String:
	if not Engine.is_editor_hint():
		return here
	_load_preview()
	if preview_here != "":
		return preview_here
	return str(_preview.get("here", ""))

func _display_figures() -> Array:
	if not Engine.is_editor_hint():
		return figures
	_load_preview()
	var source: Array = _preview.get("figures", [])
	if not preview_figures.is_empty():
		source = preview_figures
	var result: Array = []
	for f in source:
		var copy: Dictionary = f.duplicate()
		copy["pos"] = _vec(copy.get("pos", Vector2.ZERO))
		result.append(copy)
	return result

static func _vec(position_data) -> Vector2:
	if position_data is Vector2: return position_data
	if position_data is Array and position_data.size() >= 2: return Vector2(float(position_data[0]), float(position_data[1]))
	if position_data is Dictionary: return Vector2(float(position_data.get("x", 0)), float(position_data.get("y", 0)))
	return Vector2.ZERO

# -------------------------------------------------------------- layout ---

## 先走一段 45 度斜线（长度取横纵差的较小者），再走直线；最多两段。
static func _route(a: Vector2, b: Vector2) -> PackedVector2Array:
	var d := b - a
	var diagonal := minf(absf(d.x), absf(d.y))
	return PackedVector2Array([a, a + Vector2(signf(d.x), signf(d.y)) * diagonal, b])

## 站点吸附网格并整体居中；同时记下每站连线的出口方向，站名据此避开连线。
func _layout() -> void:
	_centers.clear()
	_exits.clear()
	var display := _display_nodes()
	var grid := float(get_theme_constant("grid", "CampusMap"))
	var on_grid := {}
	var low := Vector2(INF, INF)
	var high := Vector2(-INF, -INF)
	for key in display:
		var p := (_vec(display[key].get("pos", Vector2.ZERO)) / grid).round() * grid
		on_grid[key] = p
		low = low.min(p)
		high = high.max(p)
	if on_grid.is_empty():
		return
	_origin = ((map_size - (low + high)) / 2.0).round()
	for key in on_grid:
		_centers[key] = on_grid[key] + _origin
	for link in _display_links():
		if link.size() < 2 or not _centers.has(link[0]) or not _centers.has(link[1]):
			continue
		var route := _route(_centers[link[0]], _centers[link[1]])
		var first: Vector2 = route[1] - route[0] if route[1] != route[0] else route[2] - route[1]
		var last: Vector2 = route[2] - route[1] if route[2] != route[1] else route[1] - route[0]
		_add_exit(link[0], first)
		_add_exit(link[1], -last)

func _add_exit(key: String, direction: Vector2) -> void:
	if not _exits.has(key): _exits[key] = []
	_exits[key].append(Vector2(signf(direction.x), signf(direction.y)))

func _label_rect(center: Vector2, side: Vector2, size: Vector2) -> Rect2:
	var gap := float(get_theme_constant("station_radius", "CampusMap")) + 8.0
	var top_left: Vector2
	if side.x > 0.0: top_left = center + Vector2(gap, -size.y / 2.0)
	elif side.x < 0.0: top_left = center + Vector2(-gap - size.x, -size.y / 2.0)
	elif side.y < 0.0: top_left = center + Vector2(-size.x / 2.0, -gap - size.y + 2.0)
	else: top_left = center + Vector2(-size.x / 2.0, gap - 2.0)
	return Rect2(top_left.round(), size)

func _penalty(key: String, side: Vector2, rect: Rect2, taken: Array) -> float:
	var penalty := 0.0
	for direction in _exits.get(key, []):
		if direction.dot(side) > 0.0: penalty += 10.0
	var margin := float(get_theme_constant("border_inset", "CampusMap")) + 4.0
	if not Rect2(Vector2.ONE * margin, map_size - Vector2.ONE * margin * 2.0).encloses(rect): penalty += 20.0
	for other in taken:
		if other.intersects(rect): penalty += 10.0
	for other_key in _centers:
		if other_key != key and rect.grow(3.0).has_point(_centers[other_key]): penalty += 10.0
	return penalty

## 在 sides（按偏好排序）里挑扣分最少的一侧；返回 [侧, 矩形]。
func _place(key: String, size: Vector2, sides: Array, taken: Array) -> Array:
	var best_side: Vector2 = sides[0]
	var best_rect := _label_rect(_centers[key], best_side, size)
	var best := INF
	for side in sides:
		var rect := _label_rect(_centers[key], side, size)
		var penalty := _penalty(key, side, rect, taken)
		if penalty < best:
			best = penalty
			best_side = side
			best_rect = rect
	return [best_side, best_rect]

static func _text_size(label: Label) -> Vector2:
	var font := label.get_theme_font("font")
	var font_size := label.get_theme_font_size("font_size")
	return Vector2(ceilf(font.get_string_size(label.text, HORIZONTAL_ALIGNMENT_LEFT, -1, font_size).x) + 4.0, ceilf(font.get_height(font_size)))

func _prepare_locations() -> void:
	_layout()
	var display := _display_nodes()
	var locations: Control = $Locations
	for child in locations.get_children():
		child.visible = display.has(str(child.name))
	var reach := float(get_theme_constant("station_radius", "CampusMap")) + 6.0
	_name_rects.clear()
	_name_sides.clear()
	var taken: Array = []
	for key in display:
		if not _centers.has(key):
			continue
		var location := locations.get_node_or_null(str(key)) as Panel
		if location == null:
			location = locations.get_child(0).duplicate() as Panel
			location.name = str(key)
			locations.add_child(location)
		var name_label := location.get_node("Name") as Label
		name_label.text = str(display[key].get("label", key))
		name_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		name_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		var placed := _place(str(key), _text_size(name_label), [Vector2.RIGHT, Vector2.LEFT, Vector2.DOWN, Vector2.UP], taken)
		var rect: Rect2 = placed[1]
		taken.append(rect)
		_name_sides[key] = placed[0]
		_name_rects[key] = rect
		var center: Vector2 = _centers[key]
		var cover := Rect2(center - Vector2.ONE * reach, Vector2.ONE * reach * 2.0).merge(rect).grow(2.0)
		location.position = cover.position
		location.size = cover.size
		name_label.position = rect.position - cover.position
		name_label.size = rect.size

func _sync_locations() -> void:
	var current := _display_here()
	_here_rect = Rect2()
	for key in _display_nodes():
		var location := get_node_or_null("Locations/" + key) as Panel
		if location == null:
			continue
		location.visible = _shown(key) and _centers.has(key)
		location.theme_type_variation = &"CampusMapLocationHover" if key == hovered else (&"CampusMapLocationCurrent" if key == current else &"CampusMapLocation")
		var here_label := location.get_node("Here") as Label
		here_label.visible = key == current and location.visible
		if here_label.visible:
			here_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
			var sides: Array = [Vector2.UP, Vector2.DOWN, Vector2.LEFT, Vector2.RIGHT]
			sides.erase(_name_sides.get(key, Vector2.RIGHT))
			var taken: Array = _name_rects.values()
			taken.erase(_name_rects.get(key))
			var placed := _place(key, _text_size(here_label), sides, taken)
			_here_rect = placed[1]
			here_label.position = _here_rect.position - location.position
			here_label.size = _here_rect.size

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
		_load_preview()
		return preview_all_nodes or _preview.get("known", {}).has(key)
	return nodes.has(key) and (mode == "replay" or known.has(key))

func _node_at(p: Vector2) -> String:
	for key in _display_nodes():
		if not _shown(key):
			continue
		var location := get_node_or_null("Locations/" + key) as Control
		if location != null and Rect2(location.position - Vector2(2, 2), location.size + Vector2(4, 4)).has_point(p):
			return key
	return ""

# ---------------------------------------------------------------- draw ---

func _draw() -> void:
	if Engine.is_editor_hint() and is_node_ready():
		_prepare_locations()
	var draw_size: Vector2 = map_size
	var border_inset := float(get_theme_constant("border_inset", "CampusMap"))
	var border_width := float(get_theme_constant("border_width", "CampusMap"))
	var background := get_theme_color("background", "CampusMap")
	draw_rect(Rect2(Vector2.ZERO, draw_size), background)
	draw_rect(Rect2(Vector2.ONE * border_inset + HALF, draw_size - Vector2.ONE * border_inset * 2.0 - Vector2.ONE), get_theme_color("border", "CampusMap"), false, border_width)
	if Engine.is_editor_hint():
		draw_rect(Rect2(wing_outline.position + _origin, wing_outline.size), get_theme_color("wing_outline", "CampusMap"), false, border_width)
	_sync_locations()
	var font := Paper.font()
	var link_color := get_theme_color("link", "CampusMap")
	var link_width := float(get_theme_constant("link_width", "CampusMap"))
	for link in _display_links():
		if link.size() < 2 or not _centers.has(link[0]) or not _centers.has(link[1]):
			continue
		if not (_shown(link[0]) and _shown(link[1])):
			continue
		var route := _route(_centers[link[0]], _centers[link[1]])
		for i in 2:
			if route[i] != route[i + 1]:
				draw_line(route[i] + HALF, route[i + 1] + HALF, link_color, link_width)
		if route[1] != route[0] and route[1] != route[2]:
			draw_circle(route[1] + HALF, link_width / 2.0, link_color)
	var radius := float(get_theme_constant("station_radius", "CampusMap"))
	var current := _display_here()
	for key in _centers:
		if not _shown(key):
			continue
		if _name_rects.has(key):
			draw_rect(_name_rects[key].grow(2.0), background)
		var center: Vector2 = _centers[key] + HALF
		if key == hovered and mode == "travel":
			draw_circle(center, radius + 6.0, get_theme_color("hover", "CampusMap"))
		if key == current:
			draw_circle(center, radius, get_theme_color("here", "CampusMap"))
		else:
			draw_circle(center, radius, get_theme_color("station", "CampusMap"))
			draw_circle(center, radius - STATION_OUTLINE, get_theme_color("station_fill", "CampusMap"))
	if _here_rect.has_area():
		draw_rect(_here_rect.grow(2.0), background)
	var ink := get_theme_color("ink", "CampusMap")
	var bubble_size := get_theme_font_size("bubble", "CampusMap")
	for f in _display_figures():
		_draw_figure(f.get("pos", Vector2.ZERO), float(f.get("alpha", 1.0)), str(f.get("label", "")))
		if str(f.get("bubble", "")) != "":
			var text: String = f.bubble
			var w := minf(260.0, ceilf(font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, bubble_size).x) + 16.0)
			var r := Rect2((f.pos + Vector2(-w / 2.0, -78)).round(), Vector2(w, 26))
			draw_rect(r, get_theme_color("bubble", "CampusMap"))
			draw_rect(Rect2(r.position + HALF, r.size - Vector2.ONE), get_theme_color("bubble_border", "CampusMap"), false, border_width)
			draw_string(font, r.position + Vector2(8, 18), text, HORIZONTAL_ALIGNMENT_LEFT, w - 16, bubble_size, ink)
	if mode == "replay":
		draw_string(font, Vector2(20, draw_size.y - 18), playback_hint % str(speed), HORIZONTAL_ALIGNMENT_LEFT, -1, get_theme_font_size("hint", "CampusMap"), get_theme_color("muted", "CampusMap"))

func _position(key: String) -> Vector2:
	return _centers.get(key, Vector2.ZERO)

## 墨影：Material Symbols 的人形，下方写名字（或体型）的首字。p 为脚下的位置。
func _draw_figure(p: Vector2, alpha: float, label_text: String) -> void:
	var figure := get_theme_color("figure", "CampusMap")
	var tint := Color(figure, figure.a * alpha)
	var icon := get_theme_font("icon", "Paper")
	var top := p.round() + Vector2(0, -26)
	draw_string(icon, Vector2(top.x - 12.0, top.y + icon.get_ascent(24)), PERSON, HORIZONTAL_ALIGNMENT_CENTER, 24.0, 24, tint)
	draw_string(get_theme_font("register", "CampusMap"), Vector2(top.x - 12.0, top.y + 38.0), label_text.left(1), HORIZONTAL_ALIGNMENT_CENTER, 24.0, get_theme_font_size("figure", "CampusMap"), tint)

func _input(event: InputEvent) -> void:
	if Engine.is_editor_hint():
		return
	if mode != "replay" or not is_visible_in_tree() or (event is not InputEventKey and event is not InputEventMouseButton) \
			or not event.is_pressed() or event.is_echo():
		return
	if event.is_action_pressed("replay_pause"): paused = not paused
	elif event.is_action_pressed("replay_speed"): speed = 3.0 if speed == 1.0 else 1.0
	elif event.is_action_pressed("replay_skip"): skipping = true
	else: return
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

## 每个身影在站点下方有自己的位子，免得叠在一起。
static func _offset(slot: int) -> Vector2:
	return Vector2(float(slot % 6) * 26.0 - 65.0, 60.0 + float((slot / 6) % 3) * 34.0)
