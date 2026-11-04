@tool
extends Node3D
## Title-only, spatial dust. No screen overlay, gameplay, collisions or CPU loop.
## Godot 4.6: GPUParticles3D / ParticleProcessMaterial / BaseMaterial3D.

@export var volume_center := Vector3(-1.8, 1.6, -1.1)
@export var volume_size := Vector3(7.8, 2.1, 5.0)
@export_range(8, 64, 1) var particle_capacity := 32
@export_range(0.0, 1.0, 0.01) var density := 0.62
@export_range(0.0, 0.5, 0.01) var opacity := 0.18
@export var dust_color := Color(0.85, 0.90, 0.94, 1.0)
@export var diameter_range := Vector2(0.014, 0.028)
@export var drift_speed_range := Vector2(0.014, 0.042)
@export_range(4.0, 24.0, 0.5) var particle_lifetime := 18.0
@export_range(0.0, 24.0, 0.1) var prewarm_seconds := 2.5

var emitter: GPUParticles3D
var particle_material: ParticleProcessMaterial
var dust_material: StandardMaterial3D
var reduced_motion := false
var active := true
var _wind := 0.0

func _ready() -> void:
	_build()
	apply_configuration()

func _build() -> void:
	if emitter != null: return
	emitter = GPUParticles3D.new()
	emitter.name = "DeskMotes"
	emitter.amount = clampi(particle_capacity, 8, 64)
	emitter.lifetime = clampf(particle_lifetime, 4.0, 24.0)
	emitter.one_shot = false
	emitter.explosiveness = 0.0
	emitter.randomness = 0.25
	emitter.local_coords = true
	emitter.fixed_fps = 12
	emitter.fract_delta = true
	emitter.interpolate = true
	emitter.preprocess = clampf(prewarm_seconds, 0.0, 24.0)
	emitter.use_fixed_seed = true
	emitter.seed = 73417
	emitter.draw_order = GPUParticles3D.DRAW_ORDER_VIEW_DEPTH
	emitter.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF

	particle_material = ParticleProcessMaterial.new()
	particle_material.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_BOX
	particle_material.direction = Vector3(0.65, 0.08, 0.30).normalized()
	particle_material.spread = 95.0
	particle_material.gravity = Vector3(0.0, -0.0007, 0.0)
	particle_material.tangential_accel_min = -0.003
	particle_material.tangential_accel_max = 0.003
	particle_material.lifetime_randomness = 0.18
	particle_material.turbulence_enabled = false
	particle_material.attractor_interaction_enabled = false
	particle_material.collision_mode = ParticleProcessMaterial.COLLISION_DISABLED
	# Soft arrival and departure avoid popping; no sparkling alpha oscillation.
	var life_gradient := Gradient.new()
	life_gradient.offsets = PackedFloat32Array([0.0, 0.10, 0.73, 1.0])
	life_gradient.colors = PackedColorArray([
		Color(1,1,1,0), Color(1,1,1,1), Color(1,1,1,0.80), Color(1,1,1,0)
	])
	var life_texture := GradientTexture1D.new()
	life_texture.gradient = life_gradient
	particle_material.color_ramp = life_texture
	emitter.process_material = particle_material

	dust_material = StandardMaterial3D.new()
	dust_material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	dust_material.blend_mode = BaseMaterial3D.BLEND_MODE_ADD
	dust_material.shading_mode = BaseMaterial3D.SHADING_MODE_PER_PIXEL
	dust_material.specular_mode = BaseMaterial3D.SPECULAR_DISABLED
	dust_material.roughness = 1.0
	dust_material.metallic = 0.0
	# A small scattered-light floor keeps camera-facing motes visible beside
	# the lamp; their ordinary diffuse shading still follows its live energy.
	dust_material.emission_enabled = true
	dust_material.emission = Color(0.70, 0.50, 0.25)
	dust_material.emission_energy_multiplier = 2.4
	dust_material.billboard_mode = BaseMaterial3D.BILLBOARD_PARTICLES
	dust_material.billboard_keep_scale = true
	dust_material.vertex_color_use_as_albedo = true
	dust_material.cull_mode = BaseMaterial3D.CULL_DISABLED
	dust_material.depth_draw_mode = BaseMaterial3D.DEPTH_DRAW_DISABLED
	# Depth testing stays enabled: a lifted book/opaque prop can hide a mote.
	# The same scene lights shade these particles; no second flicker clock.
	dust_material.albedo_texture = _soft_disc()
	var quad := QuadMesh.new()
	quad.size = Vector2.ONE
	quad.material = dust_material
	emitter.draw_pass_1 = quad
	add_child(emitter)

func _soft_disc() -> GradientTexture2D:
	var gradient := Gradient.new()
	gradient.offsets = PackedFloat32Array([0.0, 0.22, 0.50, 0.76, 1.0])
	gradient.colors = PackedColorArray([
		Color(1,1,1,0.85), Color(1,1,1,0.64), Color(1,1,1,0.24),
		Color(1,1,1,0.035), Color(1,1,1,0.0)
	])
	var texture := GradientTexture2D.new()
	texture.width = 32
	texture.height = 32
	texture.gradient = gradient
	texture.fill = GradientTexture2D.FILL_RADIAL
	texture.fill_from = Vector2(0.5, 0.5)
	texture.fill_to = Vector2(1.0, 0.5)
	return texture

func apply_configuration() -> void:
	if emitter == null: return
	particle_capacity = clampi(particle_capacity, 8, 64)
	particle_lifetime = clampf(particle_lifetime, 4.0, 24.0)
	# Changing capacity/lifetime restarts the emitter; tune these at creation.
	# Use set_density at runtime to retain the existing GPU particle state.
	if emitter.amount != particle_capacity: emitter.amount = particle_capacity
	if not is_equal_approx(emitter.lifetime, particle_lifetime):
		emitter.lifetime = particle_lifetime
	emitter.preprocess = clampf(prewarm_seconds, 0.0, 24.0)
	particle_material.initial_velocity_min = maxf(0.0, drift_speed_range.x)
	particle_material.initial_velocity_max = maxf(particle_material.initial_velocity_min, drift_speed_range.y)
	particle_material.scale_min = clampf(diameter_range.x, 0.004, 0.12)
	particle_material.scale_max = clampf(diameter_range.y, particle_material.scale_min, 0.12)
	set_volume(volume_center, volume_size)
	set_density(density)
	set_opacity(opacity)
	set_wind(_wind)
	_sync_pause()

func set_volume(center: Vector3, size: Vector3) -> void:
	volume_center = center
	volume_size = size.abs().max(Vector3(0.1, 0.1, 0.1))
	if emitter == null: return
	emitter.position = volume_center
	particle_material.emission_box_extents = volume_size * 0.5
	# Bounds include worst-case drift; these do not require capture_aabb/GPU readback.
	var drift := maxf(drift_speed_range.x, drift_speed_range.y) * particle_lifetime
	var padding := Vector3.ONE * (drift + 0.45)
	emitter.visibility_aabb = AABB(-volume_size * 0.5 - padding, volume_size + padding * 2.0)

func set_density(value: float) -> void:
	density = clampf(value, 0.0, 1.0)
	if emitter != null:
		emitter.amount_ratio = density
		_sync_pause()

func set_opacity(value: float) -> void:
	opacity = clampf(value, 0.0, 0.5)
	if dust_material != null:
		dust_material.albedo_color = Color(dust_color.r, dust_color.g, dust_color.b, opacity)
	_sync_pause()

func set_wind(value: float) -> void:
	_wind = clampf(value, -1.0, 1.0)
	if particle_material != null:
		particle_material.gravity = Vector3(_wind * 0.006, -0.0007, _wind * 0.002)

func set_reduce_motion(value: bool) -> void:
	reduced_motion = value
	_sync_pause()

func set_active(value: bool) -> void:
	active = value
	_sync_pause()

func _sync_pause() -> void:
	if emitter == null: return
	var enabled := active and not reduced_motion and density > 0.0 and opacity > 0.0
	# Hiding is immediate; speed_scale=0 freezes existing positions and lifetimes.
	# Leave emitting true so resuming does not restart or erase the warmed state.
	emitter.visible = enabled
	emitter.speed_scale = 1.0 if enabled else 0.0

func get_render_summary() -> Dictionary:
	return {
		"capacity": particle_capacity, "density": density, "opacity": opacity,
		"active": active, "reduced_motion": reduced_motion,
		"volume_center": volume_center, "volume_size": volume_size,
		"prewarm_seconds": prewarm_seconds, "fixed_fps": 12
	}
