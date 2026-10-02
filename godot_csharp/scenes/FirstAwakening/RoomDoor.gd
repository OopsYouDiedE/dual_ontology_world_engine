extends Node2D
## Dormitory access is granted by the owner, before proximity can open the leaves.
## Frame, lintel and number plate are painted into the architecture.
@export var dorm_owner := "detective"
@export var interior_bounds := Rect2(32, 32, 256, 256)
@onready var room: Node2D = get_parent().get_parent()
var openness := 0.0
var privacy_covers: Array[Polygon2D] = []
@onready var leaf_collision: CollisionShape2D = $Body/LeafCollision
var left: Texture2D = preload("res://ShareAsset/Internal/Props/FirstAwakeningInterior/room_door_left.png")
var right: Texture2D = preload("res://ShareAsset/Internal/Props/FirstAwakeningInterior/room_door_right.png")

func _ready() -> void:
	# An unentered room reads as roofed wall mass: a cap-coloured slab with a lit rim,
	# so the closed door belongs to architecture instead of opening onto a void.
	for bounds in [interior_bounds, Rect2(position + Vector2(-32, -96), Vector2(64, 32))]:
		var top_left: Vector2 = bounds.position - position
		for layer in [[Color("342832"), 0.0], [Color("1d161f"), 2.0]]:
			var cover := Polygon2D.new()
			var inset: float = layer[1]
			var start := top_left + Vector2(inset, inset)
			var extent: Vector2 = bounds.size - Vector2(inset, inset) * 2.0
			cover.polygon = PackedVector2Array([start, start + Vector2(extent.x, 0), start + extent, start + Vector2(0, extent.y)])
			cover.color = layer[0]
			cover.z_index = 40
			add_child(cover)
			privacy_covers.append(cover)
	refresh_access()

func owner_actor() -> CharacterBody2D:
	var actor_name: String = {"detective": "侦探", "engineer": "工程师", "cook": "厨师", "musician": "音乐家"}[dorm_owner]
	return room.actors.get(actor_name)

func access_allowed() -> bool:
	return dorm_owner == "detective" or (room.has_method("can_access_dorm") and bool(room.call("can_access_dorm", dorm_owner)))

func threshold_collision_rect() -> Rect2:
	var size: Vector2 = leaf_collision.shape.size
	return Rect2(position + leaf_collision.position - size * 0.5, size)

func contains_private_point(point: Vector2) -> bool:
	return interior_bounds.has_point(point) or Rect2(position + Vector2(-32, -96), Vector2(64, 96)).has_point(point)

func door_click_rect() -> Rect2:
	return Rect2(position + Vector2(-32, -48), Vector2(64, 56))

func refresh_access() -> void:
	var allowed := access_allowed()
	for cover in privacy_covers:
		cover.visible = not allowed
	if not allowed:
		leaf_collision.set_deferred("disabled", false)
		queue_redraw()

func _physics_process(delta: float) -> void:
	if not is_instance_valid(room) or not ("actors" in room) or room.actors == null: return
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
