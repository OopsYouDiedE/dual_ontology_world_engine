@tool
extends Control

signal waypoint_added(actor: String, point: Vector2)
signal waypoint_removed(actor: String)

var beat: Dictionary = {}
var actor: String = ""
var zoom: float = 0.75
var offset: Vector2 = Vector2(24, 24)
var references: Array[Dictionary] = []

const ACTOR_COLORS := {
	"侦探": Color("e8b839"),    # Gold
	"工程师": Color("5888d4"),  # Blue
	"厨师": Color("e85848"),    # Coral Red
	"音乐家": Color("48c088")   # Emerald Green
}
const FALLBACK_COLORS: Array[Color] = [
	Color.CORNFLOWER_BLUE, Color.CORAL, Color.LIME_GREEN, Color.GOLD, Color.VIOLET, Color.TURQUOISE
]

func _ready() -> void:
	custom_minimum_size = Vector2(360, 260)
	mouse_filter = Control.MOUSE_FILTER_STOP
	clip_contents = true
	tooltip_text = "左键：添加 32px 网格途经点\n右键：撤销最后一个途经点\n中键拖动：平移画布\n滚轮：缩放"

func set_beat(value: Dictionary) -> void:
	beat = value.duplicate(true)
	queue_redraw()

func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseMotion and event.button_mask & MOUSE_BUTTON_MASK_MIDDLE:
		offset += event.relative
		queue_redraw()
		accept_event()
		return
	if event is InputEventMouseButton and event.pressed:
		if event.button_index == MOUSE_BUTTON_WHEEL_UP:
			zoom = clampf(zoom + 0.1, 0.25, 3.0)
			queue_redraw()
			accept_event()
			return
		elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			zoom = clampf(zoom - 0.1, 0.25, 3.0)
			queue_redraw()
			accept_event()
			return
		elif event.button_index == MOUSE_BUTTON_LEFT and not actor.is_empty():
			var world: Vector2 = (event.position - offset) / zoom
			waypoint_added.emit(actor, world.snapped(Vector2(32, 32)))
			accept_event()
			return
		elif event.button_index == MOUSE_BUTTON_RIGHT and not actor.is_empty():
			waypoint_removed.emit(actor)
			accept_event()
			return

func _draw() -> void:
	draw_rect(Rect2(Vector2.ZERO, size), Color("1a1e24"))
	var step: float = 32.0 * zoom
	var x: float = fposmod(offset.x, step)
	while x < size.x:
		draw_line(Vector2(x, 0), Vector2(x, size.y), Color("28303a"))
		x += step
	var y: float = fposmod(offset.y, step)
	while y < size.y:
		draw_line(Vector2(0, y), Vector2(size.x, y), Color("28303a"))
		y += step
	for reference: Dictionary in references:
		var texture: Texture2D = reference.texture
		var pos: Vector2 = reference.position
		var dimensions: Vector2 = texture.get_size() * reference.scale
		if reference.centered:
			pos -= dimensions / 2.0
		draw_texture_rect(texture, Rect2(pos * zoom + offset, dimensions * zoom), false, Color(1, 1, 1, 0.45))
	
	var tracks_value: Variant = beat.get("tracks", {})
	var tracks: Dictionary = tracks_value if tracks_value is Dictionary else {}
	var index: int = 0
	for name_variant: Variant in tracks:
		if not tracks[name_variant] is Dictionary:
			continue
		var name_str := str(name_variant)
		var track: Dictionary = tracks[name_variant]
		var color: Color = ACTOR_COLORS.get(name_str, FALLBACK_COLORS[index % FALLBACK_COLORS.size()])
		index += 1
		var points: PackedVector2Array = []
		var path_value: Variant = track.get("path", [])
		var path: Array = path_value if path_value is Array else []
		for point: Variant in path:
			if point is Array and point.size() == 2 and (point[0] is int or point[0] is float) and (point[1] is int or point[1] is float):
				points.append(Vector2(float(point[0]), float(point[1])) * zoom + offset)
		if points.size() > 1:
			var line_width := 3.5 if name_str == actor else 2.0
			draw_polyline(points, color, line_width)
		for p_idx in points.size():
			var point: Vector2 = points[p_idx]
			var radius := 5.0 if name_str == actor else 3.5
			draw_circle(point, radius, color)
			draw_string(ThemeDB.fallback_font, point + Vector2(6, -6), str(p_idx + 1), HORIZONTAL_ALIGNMENT_LEFT, -1, 10, Color(color, 0.75))
		if points.is_empty():
			continue
		var endpoint: Vector2 = points[-1]
		draw_circle(endpoint, 10.0, color, false, 2.0)
		var direction: Vector2 = Vector2.DOWN
		match str(track.get("face", "front")):
			"back": direction = Vector2.UP
			"left": direction = Vector2.LEFT
			"right": direction = Vector2.RIGHT
		draw_line(endpoint, endpoint + direction * 18.0, color, 3.0)
		var label_text := "%s (%s)" % [name_str, track.get("pose", "idle")]
		draw_string(ThemeDB.fallback_font, endpoint + Vector2(14, -14), label_text, HORIZONTAL_ALIGNMENT_LEFT, -1, 13, color)

func load_reference(path: String) -> String:
	references.clear()
	var resource: Resource = ResourceLoader.load(path, "PackedScene")
	if not resource is PackedScene:
		queue_redraw()
		return "无法读取场景参考。"
	var sc_state: SceneState = (resource as PackedScene).get_state()
	var positions: Dictionary = {NodePath("."): Vector2.ZERO}
	for i: int in range(sc_state.get_node_count()):
		var properties: Dictionary = {}
		for property_index: int in range(sc_state.get_node_property_count(i)):
			properties[str(sc_state.get_node_property_name(i, property_index))] = sc_state.get_node_property_value(i, property_index)
		var parent_path: NodePath = sc_state.get_node_path(i, true)
		var local_position: Vector2 = properties.get("position", Vector2.ZERO)
		var position: Vector2 = positions.get(parent_path, Vector2.ZERO) + local_position
		positions[sc_state.get_node_path(i)] = position
		var texture: Variant = properties.get("texture")
		if sc_state.get_node_type(i) == "Sprite2D" and texture is Texture2D:
			references.append({"texture": texture, "position": position, "scale": properties.get("scale", Vector2.ONE), "centered": properties.get("centered", true)})
	queue_redraw()
	return "只读参考：%s；%d 个 Sprite2D 纹理。" % [path, references.size()]
