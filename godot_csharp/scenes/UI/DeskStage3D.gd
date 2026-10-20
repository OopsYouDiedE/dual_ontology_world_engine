@tool
extends Node3D
## Independent title-only stage. World simulation and input remain in TitleUI.

const DESK_PATH := "res://ShareAsset/Internal/Props/UIDesk/Painterly3D/desk.glb"
const PHYSICAL_LAYOUT_PATH := "res://ShareAsset/Internal/Props/UIDesk/Painterly3D/desk-physical-layout.json"
const PHYSICAL_LAYOUT: JSON = preload(PHYSICAL_LAYOUT_PATH)
const SURFACE_SHADER := preload("res://scenes/UI/painterly_surface.gdshader")
const GLASS_SHADER := preload("res://scenes/UI/desk_glass.gdshader")
const FLAME_SHADER := preload("res://scenes/UI/desk_flame.gdshader")
const LAMP_BASE_ENERGY := 9.0
const Atmosphere = preload("res://scenes/UI/DeskAtmosphere3D.gd")
const CAMERA_ELEVATION := deg_to_rad(60.0)
const CAMERA_DISTANCE := 12.0
const CAMERA_VIEW_HEIGHT := 7.2
const BOOK_READING_OFFSET := 4.3

var camera: Camera3D
var lamp_light: OmniLight3D
var flame: MeshInstance3D
var flame_halo: MeshInstance3D
var wind := 0.0
var light_intensity := 1.0
var pointer := Vector2.ZERO
var open_amount := 0.0
var turn_lift := 0.0
var reduced_motion := false
var _pointer_target := Vector2.ZERO
var _elapsed := 0.0
var _materials: Array[ShaderMaterial] = []
var _flame_material: ShaderMaterial
var _atlas: Texture2D
var _normal_atlas: Texture2D
var _roughness_atlas: Texture2D
var _desk: Node3D
var atmosphere: Node3D
var _wick_tip := Vector3.ZERO
var _flame_height := 0.0
var _flame_radius := 0.0

func _load_physical_layout() -> bool:
	# The Blender exporter measures the actual wick after the physical-size
	# calibration. Lighting consumes that result, never a separate guessed pivot.
	var value = PHYSICAL_LAYOUT.data
	if not value is Dictionary:
		push_error("Title desk physical layout is missing or invalid: " + PHYSICAL_LAYOUT_PATH)
		return false
	var millimeters := float(value.get("mm_per_unit", 0.0))
	var tip: Array = value.get("lamp_wick_tip_world", [])
	if millimeters <= 0.0 or tip.size() != 3:
		push_error("Title desk physical layout has no valid unit scale or wick anchor")
		return false
	_wick_tip = Vector3(float(tip[0]), float(tip[1]), float(tip[2]))
	_flame_height = float(value.get("flame_height_mm", 0.0)) / millimeters
	_flame_radius = float(value.get("flame_radius_mm", 0.0)) / millimeters
	if _flame_height <= 0.0 or _flame_radius <= 0.0:
		push_error("Title desk physical layout has no valid flame dimensions")
		return false
	return true

func _ready() -> void:
	_build_stage()
	_update_camera()

func _build_stage() -> void:
	if camera != null: return
	if not _load_physical_layout(): return
	var packed := load(DESK_PATH) as PackedScene
	if packed != null:
		_desk = packed.instantiate() as Node3D
		_desk.name = "DeskGeometry"
		add_child(_desk)
		_style_geometry(_desk)
	var environment := Environment.new()
	environment.background_mode = Environment.BG_COLOR
	environment.background_color = Color("1b120d")
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color("c0a884")
	environment.ambient_light_energy = 0.16
	environment.tonemap_mode = Environment.TONE_MAPPER_ACES
	# Diffuse ambience stays low. This dim room hemisphere supplies reflection
	# radiance for glass/brass without a painted highlight or fake window plane.
	var sky_material := ProceduralSkyMaterial.new()
	sky_material.sky_top_color = Color("a28c69")
	sky_material.sky_horizon_color = Color("423127")
	sky_material.ground_bottom_color = Color("160e09")
	sky_material.ground_horizon_color = Color("423127")
	sky_material.sky_energy_multiplier = 0.65
	sky_material.ground_energy_multiplier = 0.45
	sky_material.use_debanding = false
	var sky := Sky.new()
	sky.sky_material = sky_material
	environment.sky = sky
	environment.reflected_light_source = Environment.REFLECTION_SOURCE_SKY
	environment.ssao_enabled = true
	environment.ssao_radius = 0.32
	environment.ssao_intensity = 1.15
	environment.ssao_power = 1.3
	environment.ssao_detail = 0.55
	environment.ssao_light_affect = 0.20
	environment.glow_enabled = true
	environment.glow_intensity = 0.28
	environment.glow_hdr_threshold = 1.5
	environment.glow_bloom = 0.0
	var world := WorldEnvironment.new()
	world.environment = environment
	add_child(world)
	camera = Camera3D.new()
	camera.name = "DeskCamera"
	camera.projection = Camera3D.PROJECTION_PERSPECTIVE
	# Fixed close-view optics: larger props crop naturally at the frame edge.
	# Window width changes the side margins, never the camera's height/distance.
	camera.keep_aspect = Camera3D.KEEP_HEIGHT
	camera.fov = rad_to_deg(2.0 * atan(CAMERA_VIEW_HEIGHT / (2.0 * CAMERA_DISTANCE)))
	camera.near = 0.05
	camera.far = 50.0
	add_child(camera)
	camera.make_current()
	lamp_light = OmniLight3D.new()
	lamp_light.name = "OilLampLight"
	lamp_light.position = _wick_tip + Vector3(0, _flame_height * 0.58, 0)
	lamp_light.light_color = Color(1.0, 0.88, 0.70)
	lamp_light.light_energy = LAMP_BASE_ENERGY
	lamp_light.omni_range = 18.0
	lamp_light.omni_attenuation = 1.35
	lamp_light.shadow_enabled = true
	lamp_light.shadow_bias = 0.015
	lamp_light.shadow_normal_bias = 0.035
	lamp_light.light_size = _flame_radius
	add_child(lamp_light)
	var fill := DirectionalLight3D.new()
	fill.name = "RoomBounce"
	fill.light_color = Color("d6dbe3")
	fill.light_energy = 0.20
	fill.rotation_degrees = Vector3(-58,30,-15)
	fill.shadow_enabled = false
	fill.sky_mode = DirectionalLight3D.SKY_MODE_LIGHT_ONLY
	add_child(fill)
	_flame_material = ShaderMaterial.new()
	_flame_material.shader = FLAME_SHADER
	_flame_material.set_shader_parameter("flame_height", _flame_height)
	_flame_material.set_shader_parameter("flame_radius", _flame_radius)
	flame = MeshInstance3D.new()
	flame.name = "LampFlame"
	var shape := SphereMesh.new()
	shape.radius = _flame_radius
	shape.height = _flame_height
	shape.radial_segments = 24
	shape.rings = 24
	flame.mesh = shape
	flame.material_override = _flame_material
	flame.position = _wick_tip + Vector3(0, _flame_height * 0.5 - 0.006, 0)
	flame.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(flame)
	atmosphere = Atmosphere.new()
	atmosphere.name = "DeskAtmosphere"
	atmosphere.volume_center = Vector3(-1.8, 1.8, 1.2)
	atmosphere.volume_size = Vector3(7.4, 2.0, 6.2)
	atmosphere.density = 0.75
	atmosphere.opacity = 0.24
	atmosphere.dust_color = Color(0.95, 0.78, 0.52, 1.0)
	atmosphere.diameter_range = Vector2(0.018, 0.036)
	atmosphere.particle_lifetime = 12.0
	atmosphere.prewarm_seconds = 4.0
	add_child(atmosphere)
	if _atlas != null: apply_material_atlas(_atlas)
	if _normal_atlas != null or _roughness_atlas != null:
		apply_material_maps(_normal_atlas, _roughness_atlas)
	# Capture the authored static props once. Animated pages, flame and dust
	# remain on layer 1 and cannot become frozen images in the reflection map.
	var reflection := ReflectionProbe.new()
	reflection.name = "DeskStaticReflections"
	reflection.position = Vector3(-1.0, 6.0, 1.5)
	reflection.size = Vector3(20.0, 14.0, 18.0)
	reflection.max_distance = 26.0
	reflection.cull_mask = 2
	reflection.box_projection = true
	reflection.intensity = 0.65
	reflection.interior = false
	reflection.ambient_mode = ReflectionProbe.AMBIENT_DISABLED
	reflection.enable_shadows = true
	reflection.update_mode = ReflectionProbe.UPDATE_ONCE
	add_child(reflection)

func _style_geometry(node: Node) -> void:
	if node is MeshInstance3D:
		var object := node as MeshInstance3D
		object.layers = 3
		for surface_index in object.mesh.get_surface_count():
			var original := object.mesh.surface_get_material(surface_index) as StandardMaterial3D
			if original == null: continue
			var label := original.resource_name
			if label == "LampGlass" or label == "LensGlass" or label == "InkGlass" or label == "GlassStroke":
				object.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
				var transparent := ShaderMaterial.new()
				transparent.shader = GLASS_SHADER
				var ink_glass := label == "InkGlass"
				transparent.set_shader_parameter("glass_tint", Color(0.08, 0.22, 0.42, 1.0) if ink_glass else Color(0.95, 0.90, 0.77, 1.0))
				transparent.set_shader_parameter("base_opacity", 0.13 if ink_glass else (0.055 if label == "GlassStroke" else (0.012 if label == "LampGlass" else 0.025)))
				transparent.set_shader_parameter("edge_opacity", 0.44 if ink_glass else (0.18 if label == "LampGlass" else 0.32))
				transparent.set_shader_parameter("glass_roughness", 0.13 if ink_glass else (0.065 if label == "LensGlass" else 0.10))
				transparent.set_shader_parameter("waviness", 0.0015 if label == "LensGlass" else 0.003)
				object.set_surface_override_material(surface_index, transparent)
				continue
			var surface := ShaderMaterial.new()
			surface.shader = SURFACE_SHADER
			var pigment := original.albedo_color
			var roughness := maxf(0.58, original.roughness)
			var metallic := original.metallic
			var gain := 1.0
			var coat := 0.05
			var kind := 1.0
			var atlas_origin := Vector2(0.5, 0.0)
			var use_atlas := false
			if label.begins_with("Pewter"):
				kind = 3.0
				atlas_origin = Vector2(0.5, 0.5)
				pigment = Color(0.68, 0.40, 0.11, 1.0)
				metallic = 0.88
				roughness = 0.31
				if label == "PewterEdge":
					roughness = 0.23
					gain = 1.12
				elif label == "PewterDark":
					roughness = 0.46
					gain = 0.58
				use_atlas = true
			elif label.begins_with("Wood_"):
				kind = 0.0
				atlas_origin = Vector2.ZERO
				pigment = Color(0.32, 0.13, 0.055, 1.0)
				roughness = 0.58
				metallic = 0.0
				coat = 0.0
				use_atlas = true
			elif label == "PolishedWood":
				kind = 4.0
				atlas_origin = Vector2.ZERO
				pigment = Color(0.20, 0.075, 0.025, 1.0)
				roughness = 0.34
				gain = 0.72
				metallic = 0.0
				coat = 0.30
				use_atlas = true
			elif label == "WoodGrain":
				pigment = Color(0.12, 0.045, 0.018, 1.0)
			elif label == "WoodWear":
				pigment = Color(0.30, 0.13, 0.050, 1.0)
			elif label == "InkBlue":
				pigment = Color(0.06, 0.16, 0.45, 1.0)
				roughness = 0.23
				metallic = 0.02
				coat = 0.38
			elif label == "InkEdge":
				roughness = 0.24
				coat = 0.40
			elif label.contains("Paper"):
				kind = 2.0
				atlas_origin = Vector2(0.0, 0.5)
				roughness = 0.92
				metallic = 0.0
				coat = 0.0
				use_atlas = true
			surface.set_shader_parameter("base_color", pigment)
			surface.set_shader_parameter("surface_roughness", roughness)
			surface.set_shader_parameter("surface_metallic", metallic)
			surface.set_shader_parameter("atlas_gain", gain)
			surface.set_shader_parameter("coat_amount", coat)
			surface.set_shader_parameter("material_kind",kind)
			surface.set_shader_parameter("atlas_origin",atlas_origin)
			surface.set_meta("use_atlas", use_atlas)
			object.set_surface_override_material(surface_index,surface)
			_materials.append(surface)
	for child in node.get_children(): _style_geometry(child)

func apply_material_atlas(texture: Texture2D) -> void:
	_atlas = texture
	for surface in _materials:
		surface.set_shader_parameter("material_atlas",texture)
		surface.set_shader_parameter("has_atlas",texture != null and bool(surface.get_meta("use_atlas",false)))

func apply_material_maps(normal_atlas: Texture2D, roughness_atlas: Texture2D, normal_strength := 0.7) -> void:
	# Both maps share the base atlas' four-quadrant layout. Normal is OpenGL
	# tangent-space data; roughness is linear single-channel data, never sRGB.
	_normal_atlas = normal_atlas
	_roughness_atlas = roughness_atlas
	for surface in _materials:
		var use_maps := bool(surface.get_meta("use_atlas", false))
		surface.set_shader_parameter("normal_texture", normal_atlas)
		surface.set_shader_parameter("roughness_texture", roughness_atlas)
		surface.set_shader_parameter("has_normal_texture", use_maps and normal_atlas != null)
		surface.set_shader_parameter("has_roughness_texture", use_maps and roughness_atlas != null)
		surface.set_shader_parameter("normal_strength", normal_strength)

func set_open_amount(value: float) -> void:
	open_amount = clampf(value,0.0,1.0)
	if camera != null: _update_camera()

func set_pointer(normalized: Vector2) -> void:
	_pointer_target = normalized.clamp(Vector2(-1,-1),Vector2(1,1))

func set_turn_lift(value: float) -> void:
	turn_lift = clampf(value, 0.0, 1.0)

func set_reduce_motion(value: bool) -> void:
	reduced_motion = value
	if atmosphere != null: atmosphere.set_reduce_motion(value)
	if value:
		pointer = Vector2.ZERO
		wind = 0.0
		_update_camera()

func _process(delta: float) -> void:
	if camera == null: return
	_elapsed += minf(delta,0.1)
	pointer = Vector2.ZERO if reduced_motion else pointer.lerp(_pointer_target,1.0-exp(-delta*6.0))
	wind = 0.0 if reduced_motion else sin(_elapsed*1.4)*0.38+sin(_elapsed*2.31+1.7)*0.21
	light_intensity = 1.0 if reduced_motion else 1.0+sin(_elapsed*7.1)*0.022+sin(_elapsed*11.73+wind)*0.014-absf(wind)*0.025
	lamp_light.light_energy = LAMP_BASE_ENERGY*light_intensity
	lamp_light.position = _wick_tip + Vector3(wind * _flame_radius * 0.45, _flame_height * 0.58 * light_intensity, wind * _flame_radius * 0.20)
	# Growth and wind keep the flame's foot attached to the measured wick tip.
	flame.position = _wick_tip + Vector3(0, _flame_height * 0.5 * light_intensity - 0.006, 0)
	flame.scale = Vector3(1.0-absf(wind)*0.10,light_intensity,1.0)
	_flame_material.set_shader_parameter("wind", wind)
	_flame_material.set_shader_parameter("intensity", light_intensity)
	if atmosphere != null: atmosphere.set_wind(wind)
	_update_camera()

func _update_camera() -> void:
	if camera == null: return
	# Stay close at the authored 60-degree viewing angle. Opening tracks the page;
	# neither prop bounds nor the moving cover can pull the camera back/up.
	var slide := open_amount * (2.0 - open_amount)
	var center := Vector3(lerpf(0.7, BOOK_READING_OFFSET + 2.2, slide), 0.25, lerpf(1.5, 1.1, slide))
	var direction := Vector3(pointer.x * 0.012, sin(CAMERA_ELEVATION), cos(CAMERA_ELEVATION) + pointer.y * 0.010).normalized()
	camera.position = center + direction * CAMERA_DISTANCE
	camera.look_at(center,Vector3.UP)

func project_world(point: Vector3) -> Vector2:
	return camera.unproject_position(point)

func intersect_book(screen_point: Vector2, book_y := 0.12) -> Vector3:
	var origin := camera.project_ray_origin(screen_point)
	var direction := camera.project_ray_normal(screen_point)
	if absf(direction.y)<0.00001: return Vector3(INF,INF,INF)
	return origin+direction*((book_y-origin.y)/direction.y)
