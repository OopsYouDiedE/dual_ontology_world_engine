@tool
extends Node
## Colours and native controls sampled from the supplied low-poly concept.
const INK := Color("eee4d3")
const MUTED := Color("a79f91")
const GOLD := Color("c3b18b")
const RED := Color("b88875")
const GREEN := Color("a4aa8b")
const SURFACE := Color(0.23, 0.215, 0.185, 0.74)

static func contrast(root: Node) -> bool:
	var settings := root.get_node_or_null("/root/GameSettings")
	return settings != null and bool(settings.get_value("accessibility", "high_contrast"))

static func opacity(root: Node, standard: float) -> float:
	return 1.0 if contrast(root) else standard

static func ui_scale(root: Node) -> float:
	var settings := root.get_node_or_null("/root/GameSettings")
	var value: Variant = settings.get_value("graphics", "ui_scale") if settings != null else null
	return clampf(float(value), 0.85, 1.25) if value is float or value is int else 1.0

static func font_scale(root: Node) -> float:
	var settings := root.get_node_or_null("/root/GameSettings")
	var value: Variant = settings.get_value("accessibility", "text_scale") if settings != null else null
	return ui_scale(root) * (clampf(float(value), 1.0, 1.5) if value is float or value is int else 1.0)

static func scaled_font_size(root: Node, base_size: int) -> int:
	return maxi(12, roundi(base_size * font_scale(root)))

static func box(alpha := 0.78, margins := Vector4(14, 10, 14, 10), accent := false) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = Color(SURFACE, alpha)
	style.content_margin_left = margins.x
	style.content_margin_top = margins.y
	style.content_margin_right = margins.z
	style.content_margin_bottom = margins.w
	style.border_color = Color(GOLD, 0.64 if accent else 0.23)
	style.border_width_bottom = 1
	return style

static func apply(root: Control, font_size := 20) -> void:
	var theme := Theme.new()
	var font := SystemFont.new()
	font.font_names = PackedStringArray(["SimSun", "Songti SC", "Noto Serif CJK SC", "serif"])
	var settings := root.get_node_or_null("/root/GameSettings")
	if settings != null and bool(settings.get_value("accessibility", "plain_fonts")):
		font.font_names = PackedStringArray(["Microsoft YaHei UI", "Noto Sans CJK SC", "sans-serif"])
	theme.default_font = font
	theme.default_font_size = scaled_font_size(root, font_size)
	for kind in ["Label", "Button", "OptionButton", "CheckButton", "CheckBox", "LineEdit", "TextEdit", "SpinBox", "PopupMenu", "TooltipLabel"]:
		theme.set_font("font", kind, font)
		theme.set_color("font_color", kind, INK)
	for kind in ["normal_font", "bold_font", "italics_font", "bold_italics_font"]:
		theme.set_font(kind, "RichTextLabel", font)
	for kind in ["Button", "OptionButton", "CheckButton", "CheckBox"]:
		theme.set_stylebox("normal", kind, box(opacity(root, 0.75)))
		theme.set_stylebox("hover", kind, box(opacity(root, 0.9), Vector4(14, 10, 14, 10), true))
		theme.set_stylebox("pressed", kind, box(0.95, Vector4(14, 10, 14, 10), true))
		theme.set_stylebox("hover_pressed", kind, box(0.95, Vector4(14, 10, 14, 10), true))
		theme.set_stylebox("disabled", kind, box(opacity(root, 0.48)))
		var focus := StyleBoxFlat.new()
		focus.bg_color = Color.TRANSPARENT
		focus.border_width_bottom = 1
		focus.border_color = GOLD
		theme.set_stylebox("focus", kind, focus)
		for state in ["font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color"]:
			theme.set_color(state, kind, INK)
		theme.set_color("font_disabled_color", kind, Color(MUTED, 0.64))
	theme.set_stylebox("panel", "PanelContainer", box(opacity(root, 0.75)))
	theme.set_stylebox("panel", "Panel", box(opacity(root, 0.75)))
	theme.set_stylebox("normal", "LineEdit", box(0.8))
	theme.set_stylebox("normal", "TextEdit", box(0.8))
	theme.set_stylebox("panel", "PopupMenu", box(0.98))
	theme.set_stylebox("panel", "TooltipPanel", box(0.96))
	theme.set_color("default_color", "RichTextLabel", INK)
	theme.set_font_size("normal_font_size", "RichTextLabel", theme.default_font_size)
	theme.set_font_size("font_size", "TooltipLabel", maxi(14, theme.default_font_size - 2))
	var separator := StyleBoxLine.new()
	separator.color = Color(GOLD, 0.4)
	separator.thickness = 1
	theme.set_stylebox("separator", "HSeparator", separator)
	theme.set_stylebox("separator", "VSeparator", separator)
	var track := box(0.24, Vector4.ZERO)
	var fill := box(0.9, Vector4.ZERO)
	fill.bg_color = GOLD
	theme.set_stylebox("background", "ProgressBar", track)
	theme.set_stylebox("fill", "ProgressBar", fill)
	root.theme = theme

static func button(control: BaseButton, quiet := false) -> void:
	control.custom_minimum_size.y = maxf(36, control.custom_minimum_size.y)
	control.focus_mode = Control.FOCUS_ALL
	control.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	if quiet:
		control.add_theme_stylebox_override("normal", StyleBoxEmpty.new())
		control.add_theme_stylebox_override("hover", box(0.3))

static func bounded_position(control: Control, requested: Vector2, margin := 18.0) -> Vector2:
	var screen := control.get_viewport_rect().size
	var extent := control.size.max(control.get_combined_minimum_size())
	return Vector2(clampf(requested.x, margin, maxf(margin, screen.x - extent.x - margin)), clampf(requested.y, margin, maxf(margin, screen.y - extent.y - margin)))

static func glyph(kind: String, dimensions := Vector2(24, 24), color := INK) -> Control:
	var icon := Control.new()
	icon.custom_minimum_size = dimensions
	icon.mouse_filter = Control.MOUSE_FILTER_IGNORE
	icon.draw.connect(func():
		var s := icon.size
		var center := s * 0.5
		match kind:
			"book":
				var points := PackedVector2Array([s * Vector2(0.16, 0.2), s * Vector2(0.5, 0.28), s * Vector2(0.84, 0.2), s * Vector2(0.84, 0.76), s * Vector2(0.5, 0.84), s * Vector2(0.16, 0.76), s * Vector2(0.16, 0.2)])
				icon.draw_polyline(points, color, 1.4, true)
				icon.draw_line(s * Vector2(0.5, 0.28), s * Vector2(0.5, 0.84), color, 1.3, true)
			"sound", "ear":
				for radius in [0.18, 0.31, 0.43]:
					icon.draw_arc(center - Vector2(s.x * 0.22, 0), s.x * radius, -PI * 0.32, PI * 0.32, 14, Color(color, 0.9 - radius), 1.3, true)
			"clock":
				icon.draw_arc(center, s.x * 0.32, 0, TAU, 24, color, 1.3, true)
				icon.draw_line(center, center - Vector2(0, s.y * 0.22), color, 1.3, true)
				icon.draw_line(center, center + Vector2(s.x * 0.18, 0), color, 1.3, true)
			_:
				var points := PackedVector2Array()
				for i in range(8): points.append(center + Vector2.from_angle(float(i) * TAU / 8.0) * s.x * 0.36)
				icon.draw_colored_polygon(points, Color(color, 0.1))
				points.append(points[0])
				icon.draw_polyline(points, color, 1.3, true)
				icon.draw_line(center - Vector2(0, s.y * 0.15), center + Vector2(0, s.y * 0.07), color, 2, true)
				icon.draw_circle(center + Vector2(0, s.y * 0.2), 1.1, color)
	)
	return icon

static func item_icon(entry: Dictionary, dimensions := Vector2(86, 86)) -> Control:
	var view := TextureRect.new()
	view.name = "PocketObject"
	view.custom_minimum_size = dimensions
	view.size = dimensions
	view.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	view.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	view.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var resource_path := str(entry.get("image", entry.get("icon", "")))
	if resource_path.begins_with("res://") and ResourceLoader.exists(resource_path):
		var resource: Resource = load(resource_path)
		if resource is Texture2D:
			view.texture = resource
			return view
	var words := (str(entry.get("icon_kind", "")) + " " + str(entry.get("kind", "")) + " " + str(entry.get("type", "")) + " " + str(entry.get("label", entry.get("name", entry.get("title", entry.get("id", "")))))).to_lower()
	var kind := "object"
	if words.contains("key") or words.contains("钥匙"): kind = "key"
	elif words.contains("watch") or words.contains("clock") or words.contains("怀表"): kind = "watch"
	elif words.contains("book") or words.contains("notebook") or words.contains("笔记") or words.contains("书"): kind = "book"
	var viewport := SubViewport.new()
	viewport.name = "ObjectViewport"
	viewport.size = Vector2i(192, 192)
	viewport.own_world_3d = true
	viewport.transparent_bg = true
	viewport.render_target_update_mode = SubViewport.UPDATE_ONCE
	viewport.msaa_3d = Viewport.MSAA_4X
	view.add_child(viewport)
	view.texture = viewport.get_texture()
	var world := Node3D.new()
	viewport.add_child(world)
	var environment := WorldEnvironment.new()
	environment.environment = Environment.new()
	environment.environment.background_mode = Environment.BG_COLOR
	environment.environment.background_color = Color.TRANSPARENT
	environment.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.environment.ambient_light_color = Color("e0d3bc")
	environment.environment.ambient_light_energy = 0.85
	world.add_child(environment)
	var camera := Camera3D.new()
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.size = 1.65
	camera.position = Vector3(1.3, 2.5, 3.8) if kind == "book" else Vector3(0.35, 0.25, 3)
	world.add_child(camera)
	camera.basis = Basis.looking_at(-camera.position, Vector3.UP)
	camera.current = true
	var light := DirectionalLight3D.new()
	light.rotation_degrees = Vector3(-45, -30, 0)
	light.light_energy = 0.7
	light.light_color = Color("ffe7c6")
	world.add_child(light)
	var prop := Node3D.new()
	world.add_child(prop)
	match kind:
		"book":
			prop.rotation_degrees.y = -15
			_prop_box(prop, Vector3(0.78, 0.06, 1.12), Vector3(0, -0.07, 0), "2f3c31")
			_prop_box(prop, Vector3(0.72, 0.12, 1.04), Vector3(0.02, 0.01, 0), "d3bfa4")
			_prop_box(prop, Vector3(0.78, 0.07, 1.12), Vector3(0, 0.10, 0), "485040")
			_prop_box(prop, Vector3(0.07, 0.20, 1.12), Vector3(-0.36, 0.02, 0), "29342b")
		"key":
			prop.rotation_degrees.z = -34
			_prop_ring(prop, 0.13, 0.23, Vector3(0, 0.42, 0), "8d8977")
			_prop_box(prop, Vector3(0.095, 0.81, 0.09), Vector3(0, -0.13, 0), "918b79")
			_prop_box(prop, Vector3(0.23, 0.08, 0.09), Vector3(-0.07, -0.44, 0), "8b8473")
			_prop_box(prop, Vector3(0.20, 0.08, 0.09), Vector3(-0.06, -0.25, 0), "8b8473")
		"watch":
			var rim := _prop_cylinder(prop, 0.43, 0.14, Vector3(0, -0.03, 0), "8f8b7a", 12)
			rim.rotation_degrees.x = 90
			var face := _prop_cylinder(prop, 0.36, 0.025, Vector3(0, -0.03, 0.084), "dccbb0", 12)
			face.rotation_degrees.x = 90
			_prop_ring(prop, 0.08, 0.13, Vector3(0, 0.56, 0), "9c9683")
			_prop_box(prop, Vector3(0.12, 0.11, 0.1), Vector3(0, 0.41, 0), "938b77")
			for i in range(12):
				var a := float(i) * TAU / 12.0
				var tick := _prop_box(prop, Vector3(0.025, 0.058, 0.01), Vector3(sin(a) * 0.29, cos(a) * 0.29 - 0.03, 0.105), "706756")
				tick.rotation.z = -a
			var minute := _prop_box(prop, Vector3(0.024, 0.23, 0.014), Vector3(-0.055, 0.06, 0.116), "5f584a")
			minute.rotation_degrees.z = 30
			var hour := _prop_box(prop, Vector3(0.17, 0.028, 0.016), Vector3(0.07, -0.04, 0.12), "5f584a")
			hour.rotation_degrees.z = -13
		_:
			_prop_box(prop, Vector3(0.72, 0.9, 0.12), Vector3.ZERO, "9d8f78")
			_prop_box(prop, Vector3(0.025, 0.9, 0.02), Vector3(0, 0, 0.071), "d9c8a9")
	return view

static func _prop_material(color: String) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = Color(color)
	material.roughness = 0.92
	return material

static func _prop_box(parent: Node3D, dimensions: Vector3, at: Vector3, color: String) -> MeshInstance3D:
	var mesh := MeshInstance3D.new()
	var box_mesh := BoxMesh.new()
	box_mesh.size = dimensions
	mesh.mesh = box_mesh
	mesh.material_override = _prop_material(color)
	mesh.position = at
	parent.add_child(mesh)
	return mesh

static func _prop_cylinder(parent: Node3D, radius: float, height: float, at: Vector3, color: String, segments := 10) -> MeshInstance3D:
	var mesh := MeshInstance3D.new()
	var shape := CylinderMesh.new()
	shape.top_radius = radius
	shape.bottom_radius = radius
	shape.height = height
	shape.radial_segments = segments
	shape.rings = 1
	mesh.mesh = shape
	mesh.material_override = _prop_material(color)
	mesh.position = at
	parent.add_child(mesh)
	return mesh

static func _prop_ring(parent: Node3D, inner: float, outer: float, at: Vector3, color: String) -> MeshInstance3D:
	var mesh := MeshInstance3D.new()
	var ring := TorusMesh.new()
	ring.inner_radius = inner
	ring.outer_radius = outer
	ring.rings = 12
	ring.ring_segments = 6
	mesh.mesh = ring
	mesh.material_override = _prop_material(color)
	mesh.position = at
	mesh.rotation_degrees.x = 90
	parent.add_child(mesh)
	return mesh
