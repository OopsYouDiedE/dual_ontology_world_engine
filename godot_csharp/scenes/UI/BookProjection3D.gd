extends RefCounted
## One camera/plane contract drives both rendering and native Control input.
var desk: Node3D
var display: TextureRect
var viewport: SubViewport
var book: Node3D
var page_size := Vector2(4.8, 6.8)
var paper_y := 0.325
var pointer := Vector2.ZERO

func configure(stage: Node3D, surface: TextureRect, page: SubViewport, binding: Node3D) -> void:
	desk = stage
	display = surface
	viewport = page
	book = binding

func update(mouse: Vector2, _delta: float, reduced: bool) -> void:
	var dimensions := display.size.max(Vector2.ONE)
	pointer = Vector2.ZERO if reduced else ((mouse / dimensions - Vector2.ONE * 0.5) * 2.0).clamp(-Vector2.ONE, Vector2.ONE)
	desk.set_pointer(pointer)

func unproject(screen: Vector2) -> Vector2:
	var dimensions := display.size.max(Vector2.ONE)
	var pixel := screen * Vector2(desk.get_viewport().size) / dimensions
	var camera: Camera3D = desk.camera
	var origin := book.to_local(camera.project_ray_origin(pixel))
	var direction := book.global_basis.inverse() * camera.project_ray_normal(pixel)
	if absf(direction.y) < 0.00001:
		return Vector2(-10000, -10000)
	var distance := (paper_y - origin.y) / direction.y
	if distance < 0.0:
		return Vector2(-10000, -10000)
	var hit := origin + direction * distance
	return Vector2(hit.x / page_size.x, hit.z / page_size.y + 0.5) * Vector2(viewport.size)

func project(source: Vector2) -> Vector2:
	var uv := source / Vector2(viewport.size)
	var point := book.to_global(Vector3(uv.x * page_size.x, paper_y, (uv.y - 0.5) * page_size.y))
	return desk.camera.unproject_position(point) * display.size / Vector2(desk.get_viewport().size)

func forward(event: InputEvent) -> void:
	var local := event.duplicate() as InputEvent
	if local is InputEventMouse:
		var inverse := display.get_global_transform_with_canvas().affine_inverse()
		var screen: Vector2 = inverse * event.position
		local.position = unproject(screen)
		local.global_position = local.position
		if local is InputEventMouseMotion:
			local.relative = local.position - unproject(inverse * (event.position - event.relative))
	viewport.push_input(local, true)
