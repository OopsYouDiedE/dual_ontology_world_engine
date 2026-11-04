extends Node3D
## A deterministic visual shrub. It creates no collision or game state.
@export_range(1, 500, 1) var leaf_count := 100
@export var random_seed := 41
@export_range(0.1, 3.0, 0.05) var radius := 0.65
@export_range(0.0, 1.0, 0.01) var wind_strength := 0.12
@export var leaf_color := Color(0.18, 0.38, 0.12)

const WIND_SHADER = preload("res://scenes/components/rendering/FoliageWind.tres")
var _leaves: MeshInstance3D
var _material: ShaderMaterial

func _ready() -> void:
	rebuild()

## Rebuild after changing geometry parameters. Each cluster has one shared leaf material.
func rebuild() -> void:
	if is_instance_valid(_leaves):
		_leaves.queue_free()
	var random := RandomNumberGenerator.new()
	random.seed = random_seed
	var surface := SurfaceTool.new()
	surface.begin(Mesh.PRIMITIVE_TRIANGLES)
	for i in range(maxi(leaf_count, 1)):
		var angle := random.randf_range(0.0, TAU)
		var radial := random.randf_range(0.1, 0.9) * radius
		var base := Vector3(cos(angle) * radial, random.randf_range(0.1, 0.8) * radius, sin(angle) * radial)
		var outward := Vector3(cos(angle), random.randf_range(0.5, 1.4), sin(angle)).normalized()
		var side := Vector3(-sin(angle), 0, cos(angle))
		var length := radius * random.randf_range(0.28, 0.55)
		var width := length * random.randf_range(0.23, 0.4)
		var tip := base + outward * length
		var middle := base.lerp(tip, 0.55) + Vector3.UP * length * 0.08
		var normal := side.cross(outward).normalized()
		_add_vertex(surface, base, Vector2(0.5, 0.0), normal)
		_add_vertex(surface, middle - side * width, Vector2(0.0, 0.55), normal)
		_add_vertex(surface, tip, Vector2(0.5, 1.0), normal)
		_add_vertex(surface, base, Vector2(0.5, 0.0), normal)
		_add_vertex(surface, tip, Vector2(0.5, 1.0), normal)
		_add_vertex(surface, middle + side * width, Vector2(1.0, 0.55), normal)
	_material = ShaderMaterial.new()
	_material.shader = WIND_SHADER
	_material.set_shader_parameter("wind_strength", wind_strength)
	_material.set_shader_parameter("leaf_color", leaf_color)
	_leaves = MeshInstance3D.new()
	_leaves.mesh = surface.commit()
	_leaves.material_override = _material
	# GPU wind displacement must remain inside the CPU visibility bounds.
	_leaves.extra_cull_margin = maxf(wind_strength * 2.0, 0.1)
	add_child(_leaves)

func _add_vertex(surface: SurfaceTool, point: Vector3, uv: Vector2, normal: Vector3) -> void:
	surface.set_uv(uv)
	surface.set_normal(normal)
	surface.add_vertex(point)
