@tool
extends Node2D
## 房间记忆与当前房间由脚下室内地面触发；当前房间整体可见。
## 宿主提供 sight_entry_origin（脚下）、sight_size、sight_region（完整房间）、
## sight_interior（不含门道与墙的内部地面）。
## sight_terrain_fog 关闭时用于室外：sight_origin 与 sight_blocks 提供径向视线。

@export var cell_size: int = 32:
	set(value):
		cell_size = value
		queue_redraw()

@export var sight_radius: int = 15:
	set(value):
		sight_radius = value
		queue_redraw()

@export var unseen_shade: Color = Color(0.02, 0.02, 0.03, 1.0):
	set(value):
		unseen_shade = value
		queue_redraw()

@export var remembered_shade: Color = Color(0.02, 0.02, 0.03, 0.62):
	set(value):
		remembered_shade = value
		queue_redraw()

var host: Node
@export_group("编辑器预览")
@export var preview_unseen_rooms: Array[Rect2] = [Rect2(0, 0, 192, 160)]:
	set(value):
		preview_unseen_rooms = value
		queue_redraw()
@export var preview_remembered_rooms: Array[Rect2] = [Rect2(224, 0, 192, 160)]:
	set(value):
		preview_remembered_rooms = value
		queue_redraw()
var visible_cells: Dictionary = {}
var seen_cells: Dictionary = {}
var active_region := ""
var _origin := Vector2i(-999, -999)
var _entry := Vector2i(-999, -999)
var _clock := 0.0

func reset() -> void:
	visible_cells.clear()
	seen_cells = {}
	active_region = ""
	_origin = Vector2i(-999, -999)
	_entry = Vector2i(-999, -999)
	queue_redraw()

func can_see(point: Vector2) -> bool:
	var cell := Vector2i((point / cell_size).floor())
	if _terrain_fog():
		return active_region != "" and seen_cells.has(cell) and host.sight_region(cell) == active_region
	return visible_cells.has(cell)

func terrain_known(point: Vector2) -> bool:
	return seen_cells.has(Vector2i((point / cell_size).floor()))

func _process(delta: float) -> void:
	if Engine.is_editor_hint():
		return
	if host == null:
		return
	_clock += delta
	var entry := Vector2i((host.sight_entry_origin() / cell_size).floor())
	var origin := entry if _terrain_fog() else Vector2i((host.sight_origin() / cell_size).floor())
	if origin == _origin and entry == _entry and _clock < 0.5:
		return
	_clock = 0.0
	_origin = origin
	_entry = entry
	_cast(origin)
	queue_redraw()

func _cast(origin: Vector2i) -> void:
	visible_cells.clear()
	var size: Vector2i = host.sight_size()
	if _terrain_fog():
		var entry := Vector2i((host.sight_entry_origin() / cell_size).floor())
		_reveal_room(entry, size)
		if active_region != "":
			for y in range(size.y):
				for x in range(size.x):
					var cell := Vector2i(x, y)
					if host.sight_region(cell) == active_region:
						visible_cells[cell] = true
		return
	for y in range(maxi(0, origin.y - sight_radius), mini(size.y, origin.y + sight_radius + 1)):
		for x in range(maxi(0, origin.x - sight_radius), mini(size.x, origin.x + sight_radius + 1)):
			var target := Vector2i(x, y)
			if (target - origin).length_squared() > sight_radius * sight_radius:
				continue
			if _clear(origin, target):
				visible_cells[target] = true

func _reveal_room(origin: Vector2i, size: Vector2i) -> void:
	if not _terrain_fog():
		return
	if not host.sight_interior(origin):
		return
	var region: String = host.sight_region(origin)
	if region == "":
		return
	active_region = region
	if seen_cells.has(origin):
		return
	for y in range(size.y):
		for x in range(size.x):
			var cell := Vector2i(x, y)
			if host.sight_region(cell) == region:
				seen_cells[cell] = true

func _clear(a: Vector2i, b: Vector2i) -> bool:
	var steps := maxi(absi(b.x - a.x), absi(b.y - a.y)) * 2
	if steps <= 2:
		return true
	var from := Vector2(a) + Vector2(0.5, 0.5)
	var to := Vector2(b) + Vector2(0.5, 0.5)
	for i in range(1, steps):
		var c := Vector2i(from.lerp(to, float(i) / steps).floor())
		if c == a or c == b:
			continue
		if host.sight_blocks(c):
			return false
	return true

func _terrain_fog() -> bool:
	return host != null and (not host.has_method("sight_terrain_fog") or host.sight_terrain_fog())

func terrain_shade(cell: Vector2i) -> Color:
	if not _terrain_fog():
		return Color.TRANSPARENT
	if not host.sight_interior(cell):
		return Color.TRANSPARENT
	if not seen_cells.has(cell):
		return unseen_shade
	if host.sight_region(cell) == active_region:
		return Color.TRANSPARENT
	return remembered_shade

func _draw() -> void:
	if Engine.is_editor_hint():
		for room in preview_unseen_rooms:
			draw_rect(room, unseen_shade)
		for room in preview_remembered_rooms:
			draw_rect(room, remembered_shade)
		return
	if host == null:
		return
	if not _terrain_fog():
		return
	var size: Vector2i = host.sight_size()
	var view := _view_cells()
	for y in range(maxi(0, view.position.y), mini(size.y, view.end.y)):
		for x in range(maxi(0, view.position.x), mini(size.x, view.end.x)):
			var c := Vector2i(x, y)
			var shade := terrain_shade(c)
			if shade.a == 0.0:
				continue
			draw_rect(Rect2(Vector2(c * cell_size), Vector2(cell_size, cell_size)), shade)

func _view_cells() -> Rect2i:
	var canvas := get_viewport().get_canvas_transform()
	var top_left := canvas.affine_inverse() * Vector2.ZERO
	var bottom_right := canvas.affine_inverse() * get_viewport().get_visible_rect().size
	return Rect2i(Vector2i((top_left / cell_size).floor()) - Vector2i.ONE, Vector2i(((bottom_right - top_left) / cell_size).ceil()) + Vector2i(3, 3))
