extends CharacterBody2D
## The feet are the world origin. Scripted blocking uses the same collision as walking.
signal arrived

var sprite: AnimatedSprite2D
var facing := "front"
var target := Vector2.ZERO
var travelling := false
var speed := 90.0
@export var standard: SpriteFrames
@export var special: SpriteFrames
var path := PackedVector2Array()
var waypoint := 0
var seated := false
var seat_direction := "front"
var seat_height := 0.0

func _ready() -> void:
	collision_layer = 2
	collision_mask = 3
	if has_node("Sprite"):
		sprite = $Sprite
		sprite.sprite_frames = standard
		pose("idle")
		return
	sprite = AnimatedSprite2D.new()
	var shape := CollisionShape2D.new()
	var box := RectangleShape2D.new()
	box.size = Vector2(16, 12)
	shape.shape = box
	shape.position.y = -6
	add_child(shape)
	# A small stepped footprint reads as contact at the native pixel scale.
	var shadow := Polygon2D.new()
	shadow.name = "ContactShadow"
	shadow.polygon = PackedVector2Array([Vector2(-9,-2),Vector2(-6,-4),Vector2(6,-4),Vector2(9,-2),Vector2(9,0),Vector2(6,2),Vector2(-6,2),Vector2(-9,0)])
	shadow.color = Color(0.06, 0.05, 0.04, 0.28)
	add_child(shadow)
	sprite.position.y = -23
	sprite.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	add_child(sprite)
	sprite.sprite_frames = standard
	pose("idle")

func pose(action: String, direction: String = "") -> void:
	if seated:
		hold_seat()
		return
	sprite.position.y = -23
	if not direction.is_empty():
		facing = direction
	sprite.sprite_frames = standard
	sprite.flip_h = facing == "right"
	var suffix := "left" if facing == "right" else facing
	var animation := action + "_" + suffix
	if not standard.has_animation(animation):
		animation = "idle_" + suffix
	sprite.play(animation)

func sit_at(direction: String, cushion_height: float) -> void:
	seated = true
	# The chair supplies the floor contact; don't leave a second shadow ahead of it.
	get_node("ContactShadow").hide()
	seat_direction = direction
	seat_height = cushion_height
	hold_seat()

func stand_up() -> void:
	seated = false
	get_node("ContactShadow").show()
	sprite.position.y = -23
	pose("idle")

func sit_down(direction: String, cushion_height: float, duration := 0.4) -> void:
	seated = false
	facing = direction
	sprite.sprite_frames = standard
	sprite.flip_h = direction == "right"
	var animation := "sit_" + ("left" if direction == "right" else direction)
	sprite.play(animation)
	sprite.pause()
	var count := standard.get_frame_count(animation)
	for index in count:
		sprite.frame = index
		sprite.position.y = -23.0 - cushion_height * float(index) / (count - 1)
		await get_tree().create_timer(duration / count).timeout
	sit_at(direction, cushion_height)

func rise_from_seat(duration := 0.4) -> void:
	var animation := "sit_" + ("left" if seat_direction == "right" else seat_direction)
	var count := standard.get_frame_count(animation)
	for index in range(count - 1, -1, -1):
		sprite.frame = index
		sprite.position.y = -23.0 - seat_height * float(index) / (count - 1)
		await get_tree().create_timer(duration / count).timeout
	stand_up()

func hold_seat() -> void:
	facing = seat_direction
	sprite.sprite_frames = standard
	sprite.flip_h = facing == "right"
	var animation := "sit_" + ("left" if facing == "right" else facing)
	sprite.play(animation)
	sprite.pause()
	sprite.frame = standard.get_frame_count(animation) - 1
	sprite.position.y = -23 - seat_height

func face_point(point: Vector2) -> void:
	if not seated and position.distance_squared_to(point) > 1.0:
		face_vector(point - position)
		pose("idle")

func perform(action: String, mirrored := false) -> void:
	sprite.sprite_frames = special
	sprite.flip_h = mirrored
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
