extends RefCounted
## Display-only material construction; gameplay definitions remain in DV.
const PAINTED := preload("res://rendering/shaders/surfaces/painted_surface.gdshader")
const WOOD := preload("res://rendering/shaders/surfaces/wood.gdshader")
const FABRIC := preload("res://rendering/shaders/surfaces/fabric.gdshader")
const METAL := preload("res://rendering/shaders/surfaces/aged_metal.gdshader")
const STONE := preload("res://rendering/shaders/surfaces/stone.gdshader")

static func surface(pigment: Color, kind: int, roughness: float, metallic: float) -> ShaderMaterial:
	var result := ShaderMaterial.new()
	result.shader = PAINTED
	if metallic > 0.4:
		result.shader = METAL
	elif kind == 1:
		result.shader = WOOD
	elif kind == 2:
		result.shader = STONE
	elif kind == 3:
		result.shader = FABRIC
	result.set_shader_parameter("pigment", pigment)
	result.set_shader_parameter("surface_roughness", clampf(roughness, 0.0, 1.0))
	if result.shader == METAL or result.shader == PAINTED:
		result.set_shader_parameter("surface_metallic", clampf(metallic, 0.0, 1.0))
	return result
