extends CharacterBody2D
## Simple movable stand-in used to inspect TileMap collision and scale.

@export var speed := 120.0
@export var map_pixel_size := Vector2(640, 480)

func _ready() -> void:
	var camera := Camera2D.new()
	camera.name = "PreviewCamera"
	var view_size := get_viewport_rect().size
	var fit_scale := minf(view_size.x / maxf(map_pixel_size.x, 1.0), view_size.y / maxf(map_pixel_size.y, 1.0))
	var zoom_scale := maxi(1, floori(fit_scale))
	camera.zoom = Vector2(zoom_scale, zoom_scale)
	camera.position_smoothing_enabled = false
	camera.top_level = true
	add_child(camera)
	camera.global_position = map_pixel_size * 0.5
	camera.make_current()
	var sprite := Sprite2D.new()
	sprite.name = "StaticCharacter"
	sprite.texture = load("res://ShareAsset/Internal/Characters/atlas/06_detective.tres")
	sprite.offset = Vector2(0, -23)
	add_child(sprite)

func _physics_process(_delta: float) -> void:
	var direction := Input.get_vector("ui_left", "ui_right", "ui_up", "ui_down")
	if Input.is_key_pressed(KEY_A): direction.x -= 1.0
	if Input.is_key_pressed(KEY_D): direction.x += 1.0
	if Input.is_key_pressed(KEY_W): direction.y -= 1.0
	if Input.is_key_pressed(KEY_S): direction.y += 1.0
	velocity = direction.normalized() * speed
	move_and_slide()
