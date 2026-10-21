extends RefCounted
## The same ray/plane intersection is used by the shader and pointer input.
const SHADER := preload("res://scenes/UI/desk_perspective.gdshader")
var viewport: SubViewport
var display: TextureRect
var tilt := Vector2.ZERO
var pointer := Vector2.ZERO

func configure(target: SubViewport, surface: TextureRect) -> void:
	viewport = target
	display = surface
	display.texture = target.get_texture()
	var material := ShaderMaterial.new()
	material.shader = SHADER
	display.material = material

func update(mouse: Vector2, delta: float, reduced: bool) -> void:
	var normalized := (mouse / display.size - Vector2(0.5, 0.5)).clamp(Vector2(-0.5, -0.5), Vector2(0.5, 0.5)) * 2.0
	pointer = Vector2.ZERO if reduced else pointer.lerp(normalized, 1.0 - exp(-delta * 7.0))
	tilt = pointer * Vector2(0.035, -0.026)
	display.material.set_shader_parameter("tilt", tilt)
	display.material.set_shader_parameter("aspect", display.size.x / display.size.y)

func unproject(screen: Vector2) -> Vector2:
	var uv := screen / display.size
	var aspect := display.size.x / display.size.y
	var u := Vector3(cos(tilt.x), 0, -sin(tilt.x))
	var v := Vector3(sin(tilt.x) * sin(tilt.y), cos(tilt.y), cos(tilt.x) * sin(tilt.y))
	var normal := u.cross(v)
	var origin := Vector3(0, 0, 2.4)
	var ray := Vector3((uv.x - 0.5) * aspect, 0.5 - uv.y, -2.4)
	var hit := origin - ray * normal.dot(origin) / normal.dot(ray)
	var source := Vector2(hit.dot(u) / aspect + 0.5, 0.5 - hit.dot(v))
	return (Vector2(0.5, 0.5) + (source - Vector2(0.5, 0.5)) * 0.92) * Vector2(viewport.size)

func project(source: Vector2) -> Vector2:
	var uv := source / Vector2(viewport.size)
	uv = Vector2(0.5, 0.5) + (uv - Vector2(0.5, 0.5)) / 0.92
	var aspect := display.size.x / display.size.y
	var u := Vector3(cos(tilt.x), 0, -sin(tilt.x))
	var v := Vector3(sin(tilt.x) * sin(tilt.y), cos(tilt.y), cos(tilt.x) * sin(tilt.y))
	var point := u * ((uv.x - 0.5) * aspect) + v * (0.5 - uv.y)
	var perspective := 2.4 / (2.4 - point.z)
	return Vector2(point.x * perspective / aspect + 0.5, 0.5 - point.y * perspective) * display.size

func forward(event: InputEvent) -> void:
	var local := event.duplicate() as InputEvent
	if local is InputEventMouse:
		local.position = unproject(event.position)
		local.global_position = local.position
		if local is InputEventMouseMotion:
			local.relative = local.position - unproject(event.position - event.relative)
	viewport.push_input(local, true)
