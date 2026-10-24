extends CharacterBody2D
## A local stage performer. Never owns a DvSession or grants world effects.
signal route_finished(actor_id: String, reached: bool)

var actor_id := ""
var display_name := ""
var player_controlled := false
var translucent := false
var speed := 68.0
var route: Array[Vector2] = []
var sprite: AnimatedSprite2D
var facing := "front"
var gesture_time := 0.0
var motion_time := 0.0
var blocked_time := 0.0
var last_point := Vector2.ZERO

func _ready() -> void:
	collision_layer = 2
	collision_mask = 3
	var shape := CollisionShape2D.new()
	var capsule := CircleShape2D.new()
	capsule.radius = 7.0
	shape.shape = capsule
	shape.position = Vector2(0, -5)
	add_child(shape)
	sprite = AnimatedSprite2D.new()
	sprite.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	sprite.position = Vector2(0, -23)
	var local_path := "res://ShareAsset/Internal/Characters/Highlights/%s.png" % actor_id
	if ResourceLoader.exists(local_path):
		var sheet := load(local_path) as Texture2D
		var frames := SpriteFrames.new()
		frames.remove_animation("default")
		for index in range(3):
			var direction: String = ["front", "back", "left"][index]
			var atlas := AtlasTexture.new()
			atlas.atlas = sheet
			atlas.region = Rect2(index * 32, 0, 32, 48)
			for action in ["idle", "walk", "talk"]:
				frames.add_animation(action + "_" + direction)
				frames.add_frame(action + "_" + direction, atlas)
		sprite.sprite_frames = frames
	else:
		sprite.sprite_frames = load("res://ShareAsset/Internal/Characters/PixelCast/%s.tres" % actor_id)
	add_child(sprite)
	if translucent:
		modulate.a = 0.38
	last_point = position
	set_pose("idle")
	queue_redraw()

func set_pose(action: String, direction: String = "") -> void:
	if not direction.is_empty():
		facing = direction
	sprite.flip_h = facing == "right"
	var suffix := "left" if facing == "right" else facing
	var animation := action + "_" + suffix
	if sprite.sprite_frames.has_animation(animation):
		sprite.play(animation)

func walk(points: Array[Vector2]) -> void:
	route = points.duplicate()
	blocked_time = 0.0
	last_point = position

func gesture() -> void:
	gesture_time = 1.5
	set_pose("talk")

func _physics_process(delta: float) -> void:
	motion_time += delta
	gesture_time = maxf(0.0, gesture_time - delta)
	var direction := Vector2.ZERO
	if player_controlled:
		direction = Input.get_vector("move_left", "move_right", "move_up", "move_down")
		if not direction.is_zero_approx():
			route.clear()
	if direction.is_zero_approx() and not route.is_empty():
		var remaining := route[0] - position
		if remaining.length() < 2.0:
			route.pop_front()
			if route.is_empty():
				route_finished.emit(actor_id, true)
		else:
			direction = remaining.normalized()
	velocity = direction * speed
	move_and_slide()
	if not direction.is_zero_approx():
		if absf(direction.x) > absf(direction.y):
			facing = "right" if direction.x > 0 else "left"
		else:
			facing = "front" if direction.y > 0 else "back"
		set_pose("walk")
		sprite.position.y = -23 + sin(motion_time * 13.0) * 0.8
		if not route.is_empty():
			if position.distance_to(last_point) < 0.1:
				blocked_time += delta
			else:
				blocked_time = 0.0
			if blocked_time > 1.0:
				route.clear()
				route_finished.emit(actor_id, false)
	else:
		set_pose("talk" if gesture_time > 0.0 else "idle")
		sprite.position.y = -23
	sprite.rotation = sin(motion_time * 7) * 0.035 if gesture_time > 0.0 else 0.0
	last_point = position

func _draw() -> void:
	draw_ellipse_shadow()

func draw_ellipse_shadow() -> void:
	draw_set_transform(Vector2(0, -1), 0, Vector2(1, 0.33))
	draw_circle(Vector2.ZERO, 9.0, Color(0.12, 0.16, 0.16, 0.32))
	draw_set_transform(Vector2.ZERO)
