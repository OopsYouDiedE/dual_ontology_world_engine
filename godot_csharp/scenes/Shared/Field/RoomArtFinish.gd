extends Node2D
## Presentation only: source pixels and world geometry remain unchanged.
const AMBIENT := Color(0.79, 0.83, 0.91)
var _lights: Array[PointLight2D] = []
var _rows: Array = []
var _contacts: Array[Rect2] = []
var _panel_cells: Dictionary = {}
var _rugs: Array[Rect2] = []

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
		if str(declaration.kind) == "table_lamp" and declaration.has("texture"):
			var table := layout.get_node("Objects/" + str(declaration.owner)) as Sprite2D
			var lamp := preload("res://scenes/Shared/Field/OilLamp.gd").new()
			lamp.name = "OilLamp" + str(_lights.size())
			table.add_child(lamp)
			# Inherit table ordering, cancel its source-image scale, keep base fixed.
			lamp.position = (Vector2(float(declaration.base[0]), float(declaration.base[1])) - table.position) / table.scale
			lamp.scale = Vector2.ONE / table.scale
			lamp.configure(declaration, light)
	queue_redraw()

func _add_windows(layout: Node2D) -> void:
	var path := "res://Contracts/room_art_finish.json"
	if not FileAccess.file_exists(path): return
	var spec = JSON.parse_string(FileAccess.get_file_as_string(path))
	if not spec is Dictionary: return
	for part in spec.get(str(layout.name), []):
		var origin := Vector2(float(part.origin[0]), float(part.origin[1]))
		for entry in part.get("materials", []):
			for object in layout.get_node("Objects").get_children():
				if object is Sprite2D and object.texture.resource_path == str(entry.diffuse):
					object.texture = load(str(entry.canvas_texture))
		for panel in part.get("panels", []):
			_panel_cells[Vector2i(int((origin.x + float(panel[0])) / 32), int((origin.y + float(panel[1])) / 32))] = true
		for sample in part.get("floor_samples", []):
			var surface := preload("res://scenes/Shared/Field/AuthoredSurface.gd").new()
			surface.z_index = -1
			add_child(surface)
			var areas: Array[Rect2] = []
			for area in sample.areas:
				areas.append(Rect2(float(area[0]),float(area[1]),float(area[2]),float(area[3])))
			surface.configure(load(str(sample.texture)),float(sample.scale),origin,areas)
			surface.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
		for rug in part.get("ornaments", []):
			_rugs.append(Rect2(origin + Vector2(float(rug[0]), float(rug[1])), Vector2(float(rug[2]), float(rug[3]))))
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
	for rug in _rugs: _draw_rug(rug)
	# Narrow wall contact: only actual floor cells, never a screen vignette.
	for y in _rows.size():
		for x in str(_rows[y]).length():
			if _panel_cells.has(Vector2i(x, y)) and str(_rows[y])[x] == "w":
				var paired := _panel_cells.has(Vector2i(x + 1, y)) and x % 2 == 0
				if x % 2 == 1 and _panel_cells.has(Vector2i(x - 1, y)): continue
				var panel := Rect2(x * 32, y * 32 + 9, 64 if paired else 32, 22)
				draw_rect(panel, Color("51402f"))
				draw_rect(panel.grow(-3), Color("41352c"))
				draw_rect(panel.grow(-4), Color("69523a"), false, 1)
				draw_line(panel.position, panel.position + Vector2(panel.size.x, 0), Color("b59a68"), 1)
				draw_line(panel.position + Vector2(0, 21), panel.end, Color("2a2729"), 2)
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

func _draw_rug(rect: Rect2) -> void:
	draw_rect(rect, Color("4c302d"))
	draw_rect(rect.grow(-2), Color("a38550"))
	draw_rect(rect.grow(-3), Color("1e403d"))
	draw_rect(rect.grow(-7), Color("9a8150"), false, 0.75)
	draw_rect(rect.grow(-9), Color("304d45"), false, 0.5)
	var gold := Color("b39a62")
	# Low-contrast woven field, fine linked border and corner returns.
	for y in range(int(rect.position.y+11),int(rect.end.y-10),3):
		draw_line(Vector2(rect.position.x+11,y),Vector2(rect.end.x-11,y),Color(0.65,0.72,0.57,0.035),0.5)
	for x in range(int(rect.position.x+18),int(rect.end.x-14),14):
		for y in [rect.position.y+5,rect.end.y-5]:
			draw_polyline(PackedVector2Array([Vector2(x-2,y),Vector2(x,y-1.5),Vector2(x+2,y),Vector2(x,y+1.5),Vector2(x-2,y)]),gold,0.5,true)
	for y in range(int(rect.position.y+20),int(rect.end.y-15),16):
		for x in [rect.position.x+5,rect.end.x-5]:
			draw_polyline(PackedVector2Array([Vector2(x,y-2),Vector2(x+1.5,y),Vector2(x,y+2),Vector2(x-1.5,y),Vector2(x,y-2)]),gold,0.5,true)
	for corner in [rect.position, Vector2(rect.end.x,rect.position.y), rect.end, Vector2(rect.position.x,rect.end.y)]:
		var sign_x := 1.0 if corner.x == rect.position.x else -1.0
		var sign_y := 1.0 if corner.y == rect.position.y else -1.0
		var inside: Vector2 = corner + Vector2(sign_x*12,sign_y*12)
		draw_polyline(PackedVector2Array([inside+Vector2(0,sign_y*12),inside,inside+Vector2(sign_x*12,0)]),gold,0.75,true)
	var count := maxi(1, int(rect.size.y / 112))
	for n in count:
		var center := Vector2(rect.get_center().x,rect.position.y+rect.size.y*(n+0.5)/count)
		var rx := minf(rect.size.x*0.24,30)
		var ry := minf(rect.size.y/(count*3.2),26)
		draw_polyline(PackedVector2Array([center+Vector2(0,-ry),center+Vector2(rx,0),center+Vector2(0,ry),center+Vector2(-rx,0),center+Vector2(0,-ry)]),gold,1,true)
		var inner := PackedVector2Array([center+Vector2(0,-ry*0.72),center+Vector2(rx*0.72,0),center+Vector2(0,ry*0.72),center+Vector2(-rx*0.72,0),center+Vector2(0,-ry*0.72)])
		draw_polyline(inner,Color("657153"),0.75,true)
		for petal in range(4):
			var curve := PackedVector2Array()
			for sample in range(25):
				var angle := float(sample)/24.0*TAU
				var radius := 4.0*(1.0-cos(angle))
				var local := Vector2(radius*cos(angle),radius*sin(angle)*0.55).rotated(petal*PI*0.5)
				curve.append(center+local)
			draw_polyline(curve,gold,0.6,true)
		draw_circle(center,1.5,Color("925d3e"))
