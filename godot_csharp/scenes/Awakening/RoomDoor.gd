extends Node2D
const Sound := preload("res://scenes/Shared/Audio/Sound.gd")
## Dormitory doors: access is granted by the host (can_access_dorm); leaves open when someone is near.
## Frame, lintel and number plate are painted into the architecture.
@export var dorm_owner := ""
@export var interior_bounds := Rect2(32, 32, 256, 256)
@onready var room: Node2D = get_parent().get_parent()
var openness := 0.0
## 门扇是否已转开（用于合页与撞框的声音，只在玩家附近发声）。
var swung := false
@onready var leaf_collision: CollisionShape2D = $Body/LeafCollision
var left: Texture2D = preload("res://ShareAsset/Internal/Props/FirstAwakeningInterior/room_door_left.png")
var right: Texture2D = preload("res://ShareAsset/Internal/Props/FirstAwakeningInterior/room_door_right.png")

func _ready() -> void:
	refresh_access()

func owner_actor() -> CharacterBody2D:
	return null if dorm_owner == "" else room.actor_of(dorm_owner)

func access_allowed() -> bool:
	if room.actors.is_empty(): return false
	return dorm_owner == "" or dorm_owner == room.player_key() or (room.has_method("can_access_dorm") and bool(room.call("can_access_dorm", dorm_owner)))

func threshold_collision_rect() -> Rect2:
	var size: Vector2 = leaf_collision.shape.size
	return Rect2(position + leaf_collision.position - size * 0.5, size)

func door_click_rect() -> Rect2:
	return Rect2(position + Vector2(-32, -48), Vector2(64, 56))

func refresh_access() -> void:
	var allowed := access_allowed()
	if not allowed:
		leaf_collision.set_deferred("disabled", false)
		queue_redraw()

func _physics_process(delta: float) -> void:
	if not is_instance_valid(room) or not ("actors" in room) or room.actors.is_empty(): return
	refresh_access()
	var owner := owner_actor()
	if owner != null and owner != room.player:
		owner.add_collision_exception_with($Body)
	if not access_allowed():
		# The owner opens the leaves; the player still lacks access.
		var owner_near := owner != null and owner.position.distance_to(position - Vector2(0, 12)) < 86
		openness = move_toward(openness, 1.0 if owner_near else 0.0, delta * 5.0)
		queue_redraw()
		return
	var nearby := false
	for actor in room.actors.values():
		if actor.position.distance_to(position - Vector2(0, 12)) < 86:
			nearby = true
	openness = move_toward(openness, 1.0 if nearby else 0.0, delta * 5.0)
	leaf_collision.set_deferred("disabled", nearby or openness > 0.1)
	_door_sound()
	queue_redraw()

func _door_sound() -> void:
	if not swung and openness > 0.05:
		swung = true
		if _player_near():
			Sound.sfx("door_open", -8.0, randf_range(0.94, 1.06))
	elif swung and openness <= 0.0:
		swung = false
		if _player_near():
			Sound.sfx("door_close", -8.0, randf_range(0.94, 1.06))

func _player_near() -> bool:
	return "player" in room and is_instance_valid(room.player) and room.player.position.distance_to(position) < 260

func _draw() -> void:
	# Each leaf swings into the room, foreshortening toward its hinge.
	var width := roundf(32 * (1.0 - openness) + 5 * openness)
	var height := left.get_height()
	draw_texture_rect(left, Rect2(-32, -height, width, height), false)
	draw_texture_rect(right, Rect2(32 - width, -height, width, height), false)
	if openness > 0.05:
		var edge := Color(0.1, 0.06, 0.08, 0.9)
		draw_rect(Rect2(-32 + width, -height, 1, height), edge)
		draw_rect(Rect2(31 - width, -height, 1, height), edge)
