@tool
extends RefCounted
## One camera/plane contract drives both rendering and native Control input.
const UIInput := preload("res://scenes/UIpanel/UIInput.gd")
var desk: Node3D
var display: TextureRect
var viewport: SubViewport
var book: Node3D
var page_size := Vector2(4.8, 6.8)
var paper_y := 0.325
var page_origin_x := 0.0
var resting_curve := 0.0
var pointer := Vector2.ZERO
var _navigation = UIInput.new()
var _control_root: Control

func configure(stage: Node3D, surface: TextureRect, page: SubViewport, binding: Node3D) -> void:
	desk = stage
	display = surface
	viewport = page
	# Popups must render into the same texture and receive the same UV input.
	# Otherwise OptionButton menus escape the paper into the root window.
	viewport.gui_embed_subwindows = true
	book = binding
	for child in viewport.get_children():
		if child is Control:
			_control_root = child
			break

func update(mouse: Vector2, delta: float, reduced: bool) -> void:
	var dimensions := display.size.max(Vector2.ONE)
	pointer = Vector2.ZERO if reduced else ((mouse / dimensions - Vector2.ONE * 0.5) * 2.0).clamp(-Vector2.ONE, Vector2.ONE)
	desk.set_pointer(pointer)
	if viewport.gui_disable_input: _navigation.reset()
	else: _navigation.tick(_control_root, delta)

func reset_navigation() -> void:
	_navigation.reset()

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
	# Intersect the same settled sinusoidal mesh used by the page shader.
	for iteration in range(5):
		var sample := origin + direction * distance
		var u := (sample.x - page_origin_x) / page_size.x
		var residual := sample.y - paper_y - resting_curve * sin(u * PI)
		var derivative := direction.y - resting_curve * PI / page_size.x * cos(u * PI) * direction.x
		distance -= residual / derivative
	var hit := origin + direction * distance
	return Vector2((hit.x - page_origin_x) / page_size.x, hit.z / page_size.y + 0.5) * Vector2(viewport.size)

func forward(event: InputEvent) -> bool:
	if viewport.gui_disable_input:
		_navigation.reset()
		return true
	if _navigation.handle(_control_root, event):
		viewport.set_input_as_handled()
		return true
	var local := event.duplicate() as InputEvent
	if local is InputEventMouse:
		var inverse := display.get_global_transform_with_canvas().affine_inverse()
		var screen: Vector2 = inverse * event.position
		local.position = unproject(screen)
		local.global_position = local.position
		if local is InputEventMouseMotion:
			local.relative = local.position - unproject(inverse * (event.position - event.relative))
	viewport.push_input(local, true)
	return viewport.is_input_handled()
