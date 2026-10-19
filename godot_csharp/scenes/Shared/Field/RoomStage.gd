extends Node2D
## Presentation only: authored light anchors, room framing and bounded dust.
const VEIL := preload("res://scenes/Shared/Field/room_veil.gdshader")
var field: Node2D
var _map: Node2D
var _room := ""
var _lights: Array[Node2D] = []
var _veil: ColorRect
var _layer: CanvasLayer
var _bounds := Rect2()
var _reduce_motion := false

func _ready() -> void:
	var settings := get_node_or_null("/root/GameSettings")
	if settings != null:
		_reduce_motion = bool(settings.get_value("accessibility", "reduce_motion"))
		settings.setting_changed.connect(func(section: String, key: String, value):
			if section == "accessibility" and key == "reduce_motion":
				_reduce_motion = bool(value)
				_room = ""
		)
	_layer = CanvasLayer.new()
	_layer.layer = 1
	add_child(_layer)
	_veil = ColorRect.new()
	_veil.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_veil.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var material := ShaderMaterial.new()
	material.shader = VEIL
	_veil.material = material
	_layer.add_child(_veil)

func _process(delta: float) -> void:
	if not is_instance_valid(field.player) or field.adapter.map == null: return
	var adapter: Node2D = field.adapter
	var room: String = adapter.room_of(field.player.position)
	var indoor: bool = adapter.sight_terrain_fog()
	_veil.visible = indoor
	if _map != adapter.map or room != _room:
		_map = adapter.map
		_room = room
		_bounds = adapter.stage_bounds(field.player.position)
		_rebuild_lights(indoor)
	if not indoor: return
	var viewport: Vector2 = get_viewport_rect().size
	var zoom := clampf(minf(viewport.x * 0.9 / _bounds.size.x, viewport.y * 0.88 / _bounds.size.y), 2.0, 3.4)
	field.camera.zoom = Vector2.ONE * zoom if _reduce_motion else field.camera.zoom.lerp(Vector2.ONE * zoom, 1.0 - exp(-delta * 8.0))
	var transform: Transform2D = field.get_global_transform_with_canvas()
	var screen_bounds: Rect2 = transform * _bounds
	_veil.material.set_shader_parameter("room_rect", Vector4(screen_bounds.position.x / viewport.x, screen_bounds.position.y / viewport.y, screen_bounds.end.x / viewport.x, screen_bounds.end.y / viewport.y))
	_veil.material.set_shader_parameter("pixel_size", Vector2.ONE / viewport)

func _rebuild_lights(indoor: bool) -> void:
	for node in _lights:
		remove_child(node)
		node.queue_free()
	_lights.clear()
	if not indoor:
		field.camera.zoom = Vector2(2, 2)
		return
	var gradient := Gradient.new()
	gradient.offsets = PackedFloat32Array([0, 0.16, 0.48, 1])
	gradient.colors = PackedColorArray([Color(1,1,1,1), Color(1,1,1,0.82), Color(1,1,1,0.26), Color(1,1,1,0)])
	var glow := GradientTexture2D.new()
	glow.gradient = gradient
	glow.width = 256
	glow.height = 256
	glow.fill = GradientTexture2D.FILL_RADIAL
	glow.fill_from = Vector2(0.5, 0.5)
	glow.fill_to = Vector2(1, 0.5)
	for declaration in _map.get_meta("stage_lights", []):
		var point := Vector2(float(declaration.point[0]), float(declaration.point[1]))
		if not _bounds.grow(12).has_point(point): continue
		var window: bool = str(declaration.kind) == "window"
		var light := PointLight2D.new()
		light.position = point
		light.texture = glow
		light.texture_scale = 1.1 if window else 1.45
		# Warm porcelain/rose, deliberately outside the reserved amber palette.
		light.color = Color(0.63, 0.79, 0.94) if window else Color(1.0, 0.81, 0.71)
		light.energy = 0.6 if window else 0.9
		light.range_z_min = -100
		light.range_z_max = 100
		add_child(light)
		_lights.append(light)
		if window:
			_window_beam(point)

func _window_beam(point: Vector2) -> void:
	var beam := Polygon2D.new()
	beam.position = point
	beam.polygon = PackedVector2Array([Vector2(-12,0), Vector2(12,0), Vector2(92,160), Vector2(30,160)])
	beam.vertex_colors = PackedColorArray([Color(0.6,0.76,0.9,0.12),Color(0.6,0.76,0.9,0.12),Color(0.6,0.76,0.9,0),Color(0.6,0.76,0.9,0)])
	beam.z_index = 25
	add_child(beam)
	_lights.append(beam)
	if _reduce_motion: return
	var dust := GPUParticles2D.new()
	dust.position = point + Vector2(30, 70)
	dust.amount = 18
	dust.lifetime = 8.0
	dust.preprocess = 4.0
	dust.visibility_rect = Rect2(-64,-80,176,200)
	dust.z_index = 26
	var motion := ParticleProcessMaterial.new()
	motion.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_BOX
	motion.emission_box_extents = Vector3(28,60,0)
	motion.direction = Vector3(0.2,-1,0)
	motion.spread = 35
	motion.initial_velocity_min = 1.0
	motion.initial_velocity_max = 3.0
	motion.gravity = Vector3.ZERO
	motion.color = Color(0.8,0.86,0.91,0.18)
	motion.scale_min = 0.35
	motion.scale_max = 0.8
	dust.process_material = motion
	add_child(dust)
	_lights.append(dust)
