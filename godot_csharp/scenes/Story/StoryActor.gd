extends CharacterBody2D
## The feet are the world origin. Scripted blocking uses the same collision as walking.
signal arrived

var sprite := AnimatedSprite2D.new()
var facing := "front"
var target := Vector2.ZERO
var travelling := false
var speed := 90.0
var standard: SpriteFrames
var special: SpriteFrames
var path := PackedVector2Array()
var waypoint := 0

func _ready() -> void:
	collision_layer = 2
	collision_mask = 3
	var shape := CollisionShape2D.new()
	var box := RectangleShape2D.new()
	box.size = Vector2(16, 12)
	shape.shape = box
	shape.position.y = -6
	add_child(shape)
	sprite.position.y = -23
	sprite.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	add_child(sprite)
	sprite.sprite_frames = standard
	pose("idle")

func pose(action: String, direction: String = "") -> void:
	if not direction.is_empty():
		facing = direction
	sprite.sprite_frames = standard
	sprite.flip_h = facing == "right"
	var suffix := "left" if facing == "right" else facing
	var animation := action + "_" + suffix
	if not standard.has_animation(animation):
		animation = "idle_" + suffix
	sprite.play(animation)

func perform(action: String) -> void:
	sprite.sprite_frames = special
	sprite.flip_h = false
	sprite.play(action)
	await sprite.animation_finished
	pose("idle")

func move_input(direction: Vector2) -> void:
	velocity = direction * speed
	if direction.length_squared() > 0:
		face_vector(direction)
		pose("walk")
	else:
		pose("idle")
	move_and_slide()

func face_vector(direction: Vector2) -> void:
	if absf(direction.x) > absf(direction.y):
		facing = "right" if direction.x > 0 else "left"
	else:
		facing = "front" if direction.y > 0 else "back"

func walk_to(point: Vector2) -> void:
	await follow_path(PackedVector2Array([point]))

func follow_path(points: PackedVector2Array) -> void:
	if points.is_empty():
		return
	path = points
	waypoint = 0
	target = path[0]
	travelling = true
	await arrived

func _physics_process(delta: float) -> void:
	if not travelling:
		return
	var difference := target - position
	if difference.length() <= speed * delta:
		position = target
		waypoint += 1
		if waypoint < path.size():
			target = path[waypoint]
			return
		travelling = false
		velocity = Vector2.ZERO
		pose("idle")
		arrived.emit()
	else:
		move_input(difference.normalized())
