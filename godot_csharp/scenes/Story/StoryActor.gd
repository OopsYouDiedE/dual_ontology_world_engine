extends CharacterBody2D
## The feet are the world origin. Scripted blocking uses the same collision as walking.
signal arrived

const ARRIVAL_RADIUS := 0.1
const STUCK_DISTANCE := 0.5
const STUCK_SECONDS := 0.5
const MAX_ROUTE_STEPS_PER_TICK := 32

var sprite: AnimatedSprite2D
var facing := "front"
var target := Vector2.ZERO
var travelling := false
var travel_result := {"ok": false, "reason": "idle"}
var _progress_point := Vector2.ZERO
var _stuck_time := 0.0
var speed := 90.0
@export var standard: SpriteFrames
@export var special: SpriteFrames
@export var visual_scale := 1.0
@export var foot_offset := -23.0
@export var linear_filter := false
var path := PackedVector2Array()
var waypoint := 0
var seated := false
var seat_direction := "front"
var seat_height := 0.0

func _ready() -> void:
	if standard != null:
		visual_scale = float(standard.get_meta("visual_scale", visual_scale))
		foot_offset = float(standard.get_meta("foot_offset", foot_offset))
		linear_filter = bool(standard.get_meta("linear_filter", linear_filter))
	collision_layer = 2
	collision_mask = 3
	if has_node("Sprite"):
		sprite = $Sprite
		apply_visual(sprite)
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
	var shadow := Sprite2D.new()
	shadow.name = "ContactShadow"
	var fade := Gradient.new()
	fade.colors = PackedColorArray([Color(0.05, 0.045, 0.04, 0.4), Color(0.05, 0.045, 0.04, 0)])
	var texture := GradientTexture2D.new()
	texture.gradient = fade
	texture.width = 48
	texture.height = 48
	texture.fill = GradientTexture2D.FILL_RADIAL
	texture.fill_from = Vector2(0.5, 0.5)
	texture.fill_to = Vector2(1, 0.5)
	shadow.texture = texture
	shadow.scale = Vector2(0.5, 0.18)
	shadow.position.y = -1
	shadow.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	add_child(shadow)
	apply_visual(sprite)
	add_child(sprite)
	sprite.sprite_frames = standard
	pose("idle")

## Only the drawing scales; body geometry, movement and Y sorting remain in world units.
func apply_visual(target_sprite: AnimatedSprite2D) -> void:
	target_sprite.scale = Vector2.ONE * visual_scale
	target_sprite.position.y = foot_offset
	target_sprite.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR if linear_filter else CanvasItem.TEXTURE_FILTER_NEAREST

func pose(action: String, direction: String = "") -> void:
	if seated:
		hold_seat()
		return
	sprite.position.y = foot_offset
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
	sprite.position.y = foot_offset
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
		sprite.position.y = foot_offset - cushion_height * float(index) / maxi(1, count - 1)
		await get_tree().create_timer(duration / count).timeout
	sit_at(direction, cushion_height)

func rise_from_seat(duration := 0.4) -> void:
	var animation := "sit_" + ("left" if seat_direction == "right" else seat_direction)
	var count := standard.get_frame_count(animation)
	for index in range(count - 1, -1, -1):
		sprite.frame = index
		sprite.position.y = foot_offset - seat_height * float(index) / maxi(1, count - 1)
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
	sprite.position.y = foot_offset - seat_height

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

func face_vector(direction: Vector2) -> void:
	if absf(direction.x) > absf(direction.y):
		facing = "right" if direction.x > 0 else "left"
	else:
		facing = "front" if direction.y > 0 else "back"

func follow_path(points: PackedVector2Array) -> Dictionary:
	stop_travel()
	if points.is_empty():
		travel_result = {"ok": false, "reason": "no_route"}
		return travel_result
	path = points.duplicate()
	waypoint = 0
	target = path[0]
	travelling = true
	travel_result = {"ok": false, "reason": "pending"}
	_progress_point = position
	_stuck_time = 0.0
	await arrived
	return travel_result

## 暂停不结束路线；恢复时重新计受阻时间，原来的 arrived 等待仍然有效。
func pause_travel() -> void:
	travelling = false
	velocity = Vector2.ZERO
	pose("idle")

func resume_travel() -> void:
	if path.is_empty(): return
	travelling = true
	_progress_point = position
	_stuck_time = 0.0

## 成功、受阻和取消都释放 arrived 等待者；调用方通过回值或 travel_result 区分结果。
func stop_travel(reason := "cancelled") -> void:
	if travelling or not path.is_empty():
		_finish_travel(false, reason)
	else:
		velocity = Vector2.ZERO
		pose("idle")

func _finish_travel(ok: bool, reason: String) -> void:
	travelling = false
	velocity = Vector2.ZERO
	path.clear()
	_stuck_time = 0.0
	travel_result = {"ok": ok, "reason": reason}
	pose("idle")
	arrived.emit()

func _physics_process(delta: float) -> void:
	if not travelling or delta <= 0.0:
		return
	# A waypoint does not consume a whole physics tick. Spend the remaining
	# distance on the next segment, keeping every corner and every collision.
	var remaining := speed * delta
	for _step in MAX_ROUTE_STEPS_PER_TICK:
		var difference := target - position
		if difference.length() > ARRIVAL_RADIUS:
			if remaining <= 0.0: break
			var motion := difference.limit_length(remaining)
			# move_and_slide integrates over the full physics delta on each call.
			# Dividing this segment's motion by that same delta avoids extra speed.
			velocity = motion / delta
			face_vector(difference)
			pose("walk")
			move_and_slide()
			remaining = maxf(0.0, remaining - motion.length())
			if position.distance_to(target) > ARRIVAL_RADIUS: break
		waypoint += 1
		if waypoint >= path.size():
			_finish_travel(true, "arrived")
			return
		target = path[waypoint]
	if position.distance_to(_progress_point) >= STUCK_DISTANCE:
		_progress_point = position
		_stuck_time = 0.0
	else:
		_stuck_time += delta
		if _stuck_time >= STUCK_SECONDS:
			_finish_travel(false, "stuck")
