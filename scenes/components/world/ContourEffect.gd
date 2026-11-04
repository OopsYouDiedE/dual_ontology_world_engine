extends MeshInstance3D
## Depth discontinuities only: lighting and authored texture patterns stay intact.
const EFFECT := preload("res://assets/materials/post/contour.gdshader")

func _init() -> void:
	name = "ContourEffect"
	var quad := QuadMesh.new()
	quad.size = Vector2(2, 2)
	mesh = quad
	var ink_material := ShaderMaterial.new()
	ink_material.shader = EFFECT
	ink_material.render_priority = 127
	material_override = ink_material
	cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	ignore_occlusion_culling = true
	extra_cull_margin = 16384.0
	position.z = -0.5

func configure(enabled: bool, width_pixels: float = 1.0, strength: float = 0.38) -> void:
	visible = enabled
	material_override.set_shader_parameter("width_pixels", clampf(width_pixels, 0.5, 2.0))
	material_override.set_shader_parameter("strength", clampf(strength, 0.0, 1.0))
