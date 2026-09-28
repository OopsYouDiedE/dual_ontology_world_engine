extends Node2D
## Auto-opening double door: the real leaves block until someone approaches.
## Frame, lintel and number plate are painted into the architecture.
@onready var room: Node2D = get_parent().get_parent()
var openness := 0.0
@onready var leaf_collision: CollisionShape2D = $Body/LeafCollision
var left: Texture2D = preload("res://ShareAsset/Internal/Props/FirstAwakeningInterior/room_door_left.png")
var right: Texture2D = preload("res://ShareAsset/Internal/Props/FirstAwakeningInterior/room_door_right.png")

func _physics_process(delta: float) -> void:
	if not is_instance_valid(room) or not ("actors" in room) or room.actors == null: return
	var nearby := false
	for actor in room.actors.values():
		if actor.position.distance_to(position - Vector2(0, 12)) < 86:
			nearby = true
	openness = move_toward(openness, 1.0 if nearby else 0.0, delta * 5.0)
	leaf_collision.set_deferred("disabled", nearby or openness > 0.1)
	queue_redraw()

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
