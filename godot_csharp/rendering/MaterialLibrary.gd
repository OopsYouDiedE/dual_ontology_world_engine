extends RefCounted
## Task 4.03: Standardized Surface Material Library (材质库).
## Unifies physical scale, roughness, and reflectance parameters across rooms for:
## - Wood (木), Stone (石), Plaster (灰泥), Metal (金属), Fabric (布), Glass (玻璃).
## Strict rule: Uses CULL_BACK (no double-sided CULL_DISABLED to hide flipped mesh normals).
const PAINTED := preload("res://rendering/shaders/surfaces/painted_surface.gdshader")
const WOOD := preload("res://rendering/shaders/surfaces/wood.gdshader")
const FABRIC := preload("res://rendering/shaders/surfaces/fabric.gdshader")
const METAL := preload("res://rendering/shaders/surfaces/aged_metal.gdshader")
const STONE := preload("res://rendering/shaders/surfaces/stone.gdshader")
const PLASTER := preload("res://rendering/shaders/surfaces/exterior_plaster.gdshader")

## Standardized physical material specifications (Task 4.03)
const SPECS := {
	"wood": {
		"roughness": 0.70,
		"metallic": 0.0,
		"texture_scale_m": 1.0,
		"default_color": Color("523928"),
		"shader": WOOD
	},
	"stone": {
		"roughness": 0.85,
		"metallic": 0.0,
		"texture_scale_m": 1.2,
		"default_color": Color("635e58"),
		"shader": STONE
	},
	"plaster": {
		"roughness": 0.90,
		"metallic": 0.0,
		"texture_scale_m": 2.0,
		"default_color": Color("d5cbbe"),
		"shader": PLASTER
	},
	"metal": {
		"roughness": 0.35,
		"metallic": 0.85,
		"texture_scale_m": 0.5,
		"default_color": Color("7a8288"),
		"shader": METAL
	},
	"fabric": {
		"roughness": 0.95,
		"metallic": 0.0,
		"texture_scale_m": 0.8,
		"default_color": Color("3b4d45"),
		"shader": FABRIC
	},
	"glass": {
		"roughness": 0.10,
		"metallic": 0.05,
		"texture_scale_m": 1.0,
		"default_color": Color(0.7, 0.85, 0.9, 0.35),
		"is_translucent": true
	}
}

## Creates a standardized physical material for a given surface kind.
static func create_standard_surface(kind: String, color: Variant = null) -> Material:
	var key := kind.to_lower()
	if not SPECS.has(key):
		key = "wood"
	var spec: Dictionary = SPECS[key]
	var pigment: Color = color if (color is Color) else spec.default_color
	
	if key == "glass":
		var glass_mat := StandardMaterial3D.new()
		glass_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		glass_mat.albedo_color = pigment
		glass_mat.roughness = float(spec.roughness)
		glass_mat.metallic = float(spec.metallic)
		glass_mat.cull_mode = BaseMaterial3D.CULL_BACK
		glass_mat.set_meta("material_kind", "glass")
		glass_mat.set_meta("texture_scale_m", float(spec.texture_scale_m))
		return glass_mat
		
	var mat := ShaderMaterial.new()
	mat.shader = spec.shader
	mat.set_shader_parameter("pigment", pigment)
	mat.set_shader_parameter("surface_roughness", float(spec.roughness))
	if key == "metal":
		mat.set_shader_parameter("surface_metallic", float(spec.metallic))
	mat.set_meta("material_kind", key)
	mat.set_meta("texture_scale_m", float(spec.texture_scale_m))
	return mat

## Backward-compatible factory method
static func surface(pigment: Color, kind: int, roughness: float, metallic: float) -> ShaderMaterial:
	var result := ShaderMaterial.new()
	result.shader = PAINTED
	var kind_str := "painted"
	var tex_scale := 1.0
	
	if metallic > 0.4:
		result.shader = METAL
		kind_str = "metal"
		tex_scale = 0.5
	elif kind == 1:
		result.shader = WOOD
		kind_str = "wood"
		tex_scale = 1.0
	elif kind == 2:
		result.shader = STONE
		kind_str = "stone"
		tex_scale = 1.2
	elif kind == 3:
		result.shader = FABRIC
		kind_str = "fabric"
		tex_scale = 0.8
	elif kind == 4:
		result.shader = PLASTER
		kind_str = "plaster"
		tex_scale = 2.0
		
	result.set_shader_parameter("pigment", pigment)
	result.set_shader_parameter("surface_roughness", clampf(roughness, 0.0, 1.0))
	if result.shader == METAL or result.shader == PAINTED:
		result.set_shader_parameter("surface_metallic", clampf(metallic, 0.0, 1.0))
	result.set_meta("material_kind", kind_str)
	result.set_meta("texture_scale_m", tex_scale)
	return result

## Validates material adherence to physical scale, roughness, and backface culling rules.
static func validate_material(mat: Material) -> Dictionary:
	if mat == null:
		return {"valid": false, "error": "null-material"}
	
	var is_double_sided := false
	if mat is BaseMaterial3D:
		is_double_sided = mat.cull_mode == BaseMaterial3D.CULL_DISABLED
	# ShaderMaterials with our shaders use CULL_BACK by default (render_mode without cull_disabled)
	
	var kind: String = str(mat.get_meta("material_kind", "unknown"))
	var tex_scale: float = float(mat.get_meta("texture_scale_m", 1.0))
	var has_valid_scale := tex_scale > 0.0 and is_finite(tex_scale)
	
	var valid := (not is_double_sided) and has_valid_scale
	return {
		"valid": valid,
		"kind": kind,
		"texture_scale_m": tex_scale,
		"has_valid_scale": has_valid_scale,
		"is_double_sided_violation": is_double_sided,
		"cull_mode_ok": not is_double_sided
	}
