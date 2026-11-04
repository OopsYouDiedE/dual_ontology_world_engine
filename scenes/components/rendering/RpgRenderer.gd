extends Node3D
## One renderer owns each World3D. Map geometry and actors are siblings, not children.
const Profile = preload("res://scenes/components/rendering/RpgRenderProfile.gd")
enum Quality { BALANCED, HIGH }
@export var profile: Resource
@export var quality: Quality = Quality.BALANCED
@export var enabled := true:
	set(value):
		enabled = value
		if is_node_ready():
			if enabled: _activate()
			else: _deactivate()
@onready var world: WorldEnvironment = $WorldEnvironment
@onready var sun: DirectionalLight3D = $Sun
var environment: Environment
var supported := false
var _owned_world: World3D

func _ready() -> void:
	sun.visible = false
	world.environment = null
	supported = RenderingServer.get_current_rendering_method() == "forward_plus"
	if enabled:
		_activate()

func _activate() -> void:
	if is_instance_valid(_owned_world): return
	if not profile is Profile:
		push_error("RpgRenderer expects a valid RpgRenderProfile before activation.")
		return
	if not supported:
		push_error("RpgRenderer requires Forward+. Change renderer and restart the host.")
		return
	var target_world := get_viewport().find_world_3d()
	if target_world == null:
		push_error("RpgRenderer requires a World3D.")
		return
	if target_world.has_meta("rpg_renderer") or target_world.environment != null or _has_world_environment(get_tree().root, target_world):
		push_error("RpgRenderer cannot replace an existing World3D environment or renderer.")
		return
	_owned_world = target_world
	_owned_world.set_meta("rpg_renderer", get_instance_id())
	environment = Environment.new()
	var attributes := CameraAttributesPractical.new()
	attributes.auto_exposure_enabled = false
	world.camera_attributes = attributes
	sun.shadow_enabled = true
	sun.directional_shadow_mode = DirectionalLight3D.SHADOW_PARALLEL_4_SPLITS
	sun.directional_shadow_blend_splits = true
	sun.shadow_bias = 0.04
	sun.shadow_normal_bias = 1.0
	sun.light_angular_distance = 0.6
	sun.light_bake_mode = Light3D.BAKE_DYNAMIC
	apply_profile(profile)
	set_quality(quality)
	world.environment = environment
	sun.visible = true

func _has_world_environment(node: Node, target_world: World3D) -> bool:
	if node is WorldEnvironment and node != world and node.environment != null:
		if node.get_viewport().find_world_3d() == target_world:
			return true
	for child in node.get_children():
		if _has_world_environment(child, target_world): return true
	return false

func _deactivate() -> void:
	if is_instance_valid(sun): sun.visible = false
	if is_instance_valid(world):
		world.environment = null
		world.camera_attributes = null
	if is_instance_valid(_owned_world) and _owned_world.get_meta("rpg_renderer", 0) == get_instance_id():
		_owned_world.remove_meta("rpg_renderer")
	_owned_world = null
	environment = null

func apply_profile(value: Resource) -> void:
	if not value is Profile:
		push_error("RpgRenderer expects RpgRenderProfile.")
		return
	profile = value
	if environment == null: return
	var sky_material := ProceduralSkyMaterial.new()
	sky_material.sky_top_color = profile.sky_top
	sky_material.sky_horizon_color = profile.sky_horizon
	sky_material.ground_bottom_color = Color(0.08, 0.09, 0.11)
	sky_material.ground_horizon_color = profile.sky_horizon
	sky_material.sky_energy_multiplier = profile.sky_energy
	sky_material.ground_energy_multiplier = profile.sky_energy
	var sky := Sky.new()
	sky.sky_material = sky_material
	environment.sky = sky
	environment.background_mode = Environment.BG_SKY
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	environment.reflected_light_source = Environment.REFLECTION_SOURCE_SKY
	environment.tonemap_mode = Environment.TONE_MAPPER_AGX
	environment.tonemap_exposure = profile.exposure
	environment.glow_enabled = profile.bloom > 0.0
	environment.glow_intensity = profile.bloom
	environment.glow_hdr_threshold = 1.5
	environment.ssao_radius = profile.ssao_radius
	environment.ssao_intensity = profile.ssao_intensity
	environment.ssil_intensity = 0.45
	environment.sdfgi_cascades = 2
	environment.sdfgi_min_cell_size = profile.gi_cell_size
	environment.sdfgi_use_occlusion = true
	environment.sdfgi_bounce_feedback = 0.5
	environment.volumetric_fog_density = profile.fog_density
	environment.volumetric_fog_albedo = profile.fog_color
	environment.volumetric_fog_length = 80.0
	environment.volumetric_fog_detail_spread = 2.0
	sun.rotation_degrees = profile.sun_rotation
	sun.light_color = profile.sun_color
	sun.light_energy = profile.sun_energy
	sun.shadow_opacity = profile.shadow_opacity
	sun.directional_shadow_max_distance = profile.shadow_distance
	set_quality(quality)

func set_quality(value: int) -> void:
	quality = clampi(value, Quality.BALANCED, Quality.HIGH) as Quality
	if environment == null: return
	# GI is a scene decision. Screen-space and volume effects also need HIGH.
	environment.sdfgi_enabled = profile.sdfgi_enabled
	environment.ssao_enabled = profile.ssao_enabled
	environment.ssil_enabled = profile.ssil_enabled and quality == Quality.HIGH
	environment.ssr_enabled = profile.ssr_enabled and quality == Quality.HIGH
	environment.volumetric_fog_enabled = profile.volumetric_fog_enabled and quality == Quality.HIGH
func _exit_tree() -> void:
	_deactivate()
