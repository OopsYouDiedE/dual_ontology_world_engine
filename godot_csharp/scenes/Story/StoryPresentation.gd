extends RefCounted
## World pixels remain 32px tiles / 32x48 people. Only the camera magnifies them.
const WINDOW_SIZE := Vector2i(1280, 992)

static func install(room: Node2D, zoom_level := 2, focus := Vector2(320, 248)) -> void:
	var window := room.get_window()
	window.content_scale_size = WINDOW_SIZE
	window.content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	window.content_scale_aspect = Window.CONTENT_SCALE_ASPECT_KEEP
	window.content_scale_stretch = Window.CONTENT_SCALE_STRETCH_INTEGER
	window.size = WINDOW_SIZE
	window.min_size = WINDOW_SIZE
	var camera := Camera2D.new()
	camera.name = "StoryCamera"
	camera.zoom = Vector2.ONE * zoom_level
	# The bottom 224 screen pixels are the separate Dialogue Manager presentation.
	camera.position = focus
	room.add_child(camera)
	camera.make_current()

static func daylight(parent: Node2D, warm := true, compact := false) -> void:
	var light := Node2D.new()
	light.name = "WindowLight"
	parent.add_child(light)
	for x in ([36, 52, 68] if compact else [44, 62, 88]):
		var ray := Polygon2D.new()
		var top := 96 if compact else 128
		var bottom := 272 if compact else 360
		var reach := 130 if compact else 208
		ray.polygon = PackedVector2Array([Vector2(x,top), Vector2(x+12,top), Vector2(x+reach,bottom), Vector2(x+reach-24,bottom)])
		var tint := Color(1.0,0.83,0.53,0.19) if warm else Color(0.86,0.86,0.72,0.08)
		ray.vertex_colors = PackedColorArray([tint,tint,Color(tint,0.01),Color(tint,0.01)])
		light.add_child(ray)
	var shade := Polygon2D.new()
	shade.polygon = PackedVector2Array([Vector2(32,96),Vector2(416,96),Vector2(416,105),Vector2(32,105)]) if compact else PackedVector2Array([Vector2(32,128),Vector2(608,128),Vector2(608,137),Vector2(32,137)])
	shade.color = Color(0.08,0.06,0.04,0.22)
	light.add_child(shade)

static func add_prompt(room: Node2D, caption: Label) -> void:
	var overlay := CanvasLayer.new()
	overlay.layer = 3
	room.add_child(overlay)
	overlay.add_child(caption)
	caption.position = Vector2(32,738)
	caption.add_theme_font_size_override("font_size",14)
