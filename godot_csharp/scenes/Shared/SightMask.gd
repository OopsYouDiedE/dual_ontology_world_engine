extends Node2D
## 视野（设计总纲 §四.6，两章共用）：玩家只看得见侦探看得见的。
## 以 32px 格为单位，从侦探所在格向四周投射视线，被墙挡住的格子暗下去：
## 去过、看过的地方留下陈设的记忆（半暗，不显示其中的人）；从没看见过的地方全黑。
## 宿主提供：sight_origin() -> Vector2、sight_blocks(cell: Vector2i) -> bool、sight_size() -> Vector2i（格数）。

const CELL := 32
const RADIUS := 15
const SEEN_SHADE := Color(0.03, 0.03, 0.05, 0.62)
const UNSEEN_SHADE := Color(0.02, 0.02, 0.03, 0.96)

var host: Node
var visible_cells: Dictionary = {}
var seen_cells: Dictionary = {}
var _origin := Vector2i(-999, -999)
var _clock := 0.0

func reset() -> void:
	visible_cells.clear()
	seen_cells.clear()
	_origin = Vector2i(-999, -999)
	queue_redraw()

func can_see(point: Vector2) -> bool:
	return visible_cells.has(Vector2i(floor(point.x / CELL), floor(point.y / CELL)))

func _process(delta: float) -> void:
	if host == null:
		return
	_clock += delta
	var origin := Vector2i((host.sight_origin() / CELL).floor())
	if origin == _origin and _clock < 0.5:
		return
	_clock = 0.0
	_origin = origin
	_cast(origin)
	queue_redraw()

func _cast(origin: Vector2i) -> void:
	visible_cells.clear()
	var size: Vector2i = host.sight_size()
	for y in range(maxi(0, origin.y - RADIUS), mini(size.y, origin.y + RADIUS + 1)):
		for x in range(maxi(0, origin.x - RADIUS), mini(size.x, origin.x + RADIUS + 1)):
			var target := Vector2i(x, y)
			if (target - origin).length_squared() > RADIUS * RADIUS:
				continue
			if _clear(origin, target):
				visible_cells[target] = true
				seen_cells[target] = true

## 视线从格心到格心；途中（不含两端）碰到挡视线的格子即被挡住。墙本身看得见。
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

func _draw() -> void:
	if host == null:
		return
	var size: Vector2i = host.sight_size()
	var view := _view_cells()
	for y in range(maxi(0, view.position.y), mini(size.y, view.end.y)):
		for x in range(maxi(0, view.position.x), mini(size.x, view.end.x)):
			var c := Vector2i(x, y)
			if visible_cells.has(c):
				continue
			draw_rect(Rect2(Vector2(c * CELL), Vector2(CELL, CELL)), SEEN_SHADE if seen_cells.has(c) else UNSEEN_SHADE)

func _view_cells() -> Rect2i:
	var canvas := get_viewport().get_canvas_transform()
	var top_left := canvas.affine_inverse() * Vector2.ZERO
	var bottom_right := canvas.affine_inverse() * get_viewport().get_visible_rect().size
	return Rect2i(Vector2i((top_left / CELL).floor()) - Vector2i.ONE, Vector2i(((bottom_right - top_left) / CELL).ceil()) + Vector2i(3, 3))
