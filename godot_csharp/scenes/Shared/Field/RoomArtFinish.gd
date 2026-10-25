extends Node2D
## Presentation only: source pixels and world geometry remain unchanged.
const AMBIENT := Color(0.79, 0.83, 0.91)
var _lights: Array[PointLight2D] = []
var _rows: Array = []
var _contacts: Array[Rect2] = []
var _panel_cells: Dictionary = {}

static func attach(layout: Node2D) -> void:
	if layout.has_meta("outdoor_core") or layout.has_node("RoomArtFinish"): return
	var finish := Node2D.new()
	finish.set_script(load("res://scenes/Shared/Field/RoomArtFinish.gd"))
	finish.name = "RoomArtFinish"
	layout.add_child(finish)

func _ready() -> void:
	var layout := get_parent() as Node2D
	layout.modulate = AMBIENT
	z_index = -8
	_rows = layout.get_meta("terrain_rows", [])
	_add_windows(layout)
	var objects := layout.get_node_or_null("Objects")
	if objects != null:
		for object in objects.get_children():
			if object is Sprite2D and object.texture != null:
				var bounds: Rect2 = object.get_rect()
				# Enlarge around the existing foot, never move a site or body.
				var factor := 1.25
				for edge in [bounds.position.x, bounds.end.x]:
					var foot: Vector2 = object.position + Vector2(float(edge) * factor, -3)
					if not _floor_at(int(floorf(foot.x / 32.0)), int(floorf(foot.y / 32.0))): factor = 1.0
				object.scale *= factor
				var width: float = clampf(bounds.size.x * object.scale.x * 0.78, 8.0, 140.0)
				_contacts.append(Rect2(object.position + Vector2(-width * 0.5, -4), Vector2(width, 8)))
	var gradient := Gradient.new()
	gradient.offsets = PackedFloat32Array([0.0, 0.12, 0.38, 0.7, 1.0])
	gradient.colors = PackedColorArray([Color.WHITE, Color(1,1,1,0.8), Color(1,1,1,0.3), Color(1,1,1,0.06), Color(1,1,1,0)])
	var glow := GradientTexture2D.new()
	glow.width = 256
	glow.height = 256
	glow.gradient = gradient
	glow.fill = GradientTexture2D.FILL_RADIAL
	glow.fill_from = Vector2(0.5, 0.5)
	glow.fill_to = Vector2(1, 0.5)
	for declaration in layout.get_meta("stage_lights", []):
		var window: bool = str(declaration.kind) == "window"
		var light := PointLight2D.new()
		light.position = Vector2(float(declaration.point[0]), float(declaration.point[1]))
		light.texture = glow
		light.texture_scale = 0.85 if window else 0.95
		light.color = Color(0.72, 0.84, 1.0) if window else Color(1.0, 0.77, 0.48)
		light.energy = 0.32 if window else 0.58
		light.height = 40
		light.range_z_min = -100
		light.range_z_max = 100
		add_child(light)
		_lights.append(light)
	queue_redraw()

func _add_windows(layout: Node2D) -> void:
	var path := "res://Contracts/room_art_finish.json"
	if not FileAccess.file_exists(path): return
	var spec = JSON.parse_string(FileAccess.get_file_as_string(path))
	if not spec is Dictionary: return
	for part in spec.get(str(layout.name), []):
		var origin := Vector2(float(part.origin[0]), float(part.origin[1]))
		for panel in part.get("panels", []):
			_panel_cells[Vector2i(int((origin.x + float(panel[0])) / 32), int((origin.y + float(panel[1])) / 32))] = true
		for window in part.windows:
			var atlas := AtlasTexture.new()
			atlas.atlas = load(str(part.texture))
			# Explicit recipe crop includes the original curtains and sill.
			atlas.region = Rect2(float(window[0]) - float(window[2]) * 0.5 - 13, float(window[1]), float(window[2]) + 26, 49)
			var sprite := Sprite2D.new()
			sprite.texture = atlas
			sprite.position = origin + Vector2(float(window[0]), float(window[1]) + 24.5)
			sprite.scale = Vector2(1.25, 1.15)
			sprite.z_index = 1
			add_child(sprite)

func set_view_bounds(bounds: Rect2) -> void:
	for light in _lights:
		light.enabled = bounds.grow(12).has_point(light.position)

func _floor_at(x: int, y: int) -> bool:
	return y >= 0 and y < _rows.size() and x >= 0 and x < str(_rows[y]).length() and str(_rows[y])[x] in ".DS"

func _draw() -> void:
	# Narrow wall contact: only actual floor cells, never a screen vignette.
	for y in _rows.size():
		for x in str(_rows[y]).length():
			if _panel_cells.has(Vector2i(x, y)) and str(_rows[y])[x] == "w":
				var panel := Rect2(x * 32, y * 32 + 12, 32, 19)
				draw_rect(panel, Color("51402f"))
				draw_rect(panel.grow(-3), Color("41352c"))
				draw_rect(panel.grow(-4), Color("69523a"), false, 1)
				draw_line(panel.position, panel.position + Vector2(32, 0), Color("b59a68"), 1)
				draw_line(panel.position + Vector2(0, 18), panel.end, Color("2a2729"), 2)
			if not _floor_at(x, y): continue
			for step in range(8):
				var alpha := 0.16 * pow(1.0 - float(step) / 8.0, 2.0)
				if not _floor_at(x, y - 1):
					draw_rect(Rect2(x * 32, y * 32 + step, 32, 1), Color(0.08, 0.07, 0.10, alpha))
				if not _floor_at(x - 1, y):
					draw_rect(Rect2(x * 32 + step, y * 32, 1, 32), Color(0.08, 0.07, 0.10, alpha * 0.7))
	for contact in _contacts:
		for step in range(5):
			var inset := float(step)
			draw_style_box(_contact_style(0.035), contact.grow(-inset * 0.6))

func _contact_style(alpha: float) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.08, 0.06, 0.10, alpha)
	style.set_corner_radius_all(4)
	return style
