@tool
extends Node2D
## 昨夜回放：在真实地图上用半透明墨色人影重演，每人头顶一个心声气泡（此刻他听到/相信的东西）。
## 同一时刻，四个气泡并排显示四种不同的认知。可暂停、倍速、跳过；跳过与看完的结算相同。

const Paper := preload("res://scenes/UI/Components/Paper.gd")
@export var overlay_scene: PackedScene = preload("res://scenes/UI/Panels/Field/ReplayOverlay.tscn")
@export var bubble_scene: PackedScene = preload("res://scenes/UI/Components/ReplayBubble.tscn")

const ROOM_ANCHORS := {
	"R05": Vector2(930, 200), "R06": Vector2(930, 600), "R07": Vector2(706, 600), "R08": Vector2(706, 200),
	"corridor": Vector2(800, 388), "hall": Vector2(440, 340), "pantry": Vector2(470, 652), "foyer": Vector2(224, 388),
	"lounge": Vector2(64, 388),
}
const SPOTS := {
	"door": ["foyer", Vector2(178, 384)], "tube": ["hall", Vector2(500, 132)], "slate": ["hall", Vector2(532, 290)],
	"desk": ["R08", Vector2(690, 214)], "sink": ["pantry", Vector2(352, 560)],
}
const OFFSETS := {"detective": Vector2(-22, 4), "huo": Vector2(22, 4), "shen": Vector2(-6, 18), "su": Vector2(10, -12)}
const ORDER := ["detective", "huo", "shen", "su"]
const INK := Color(0.16, 0.13, 0.2, 0.74)

signal playback_cancelled
var _cancelled := false
var host: Node
var figures: Dictionary = {}
var bubbles: Dictionary = {}
var layer: CanvasLayer
var caption: RichTextLabel
var controls: Label
var door_label: Label
var playing := false
var paused := false
var speed := 1.0
var skipping := false

## 回放所需的文字（标题、人名、门的状态）随事件给出。
var labels: Dictionary = {}

func play(ev: Dictionary) -> void:
	var frames: Array = ev.get("frames", [])
	labels = ev
	if _cancelled or frames.is_empty():
		return
	playing = true
	paused = false
	skipping = false
	speed = 1.0
	_setup()
	var previous: Dictionary = {}
	for index in frames.size():
		if skipping:
			break
		var frame: Dictionary = frames[index]
		_apply(frame, previous)
		previous = frame
		var hold := 1.6 if str(frame.actor) == "" else (0.6 if str(frame.label).contains("来到") else 1.3)
		var waited := 0.0
		while waited < hold and not skipping:
			await get_tree().process_frame
			if _cancelled: return
			if not paused:
				waited += get_process_delta_time() * speed
	if not skipping:
		var waited := 0.0
		while waited < 1.2 and not skipping:
			await get_tree().process_frame
			if _cancelled: return
			waited += get_process_delta_time()
	_teardown()
	playing = false

func cancel_playback() -> void:
	if _cancelled: return
	_cancelled = true
	if playing:
		_teardown()
		playing = false
	playback_cancelled.emit()

func _exit_tree() -> void:
	cancel_playback()

func _setup() -> void:
	for actor in host.actors.values():
		actor.modulate.a = 0.0
	host.decals.modulate.a = 0.35
	host.set_bar_visible(false)
	host.field.follow_camera = false
	host.field.camera.zoom = Vector2.ONE
	host.field.camera.position = Vector2(640, 352)
	layer = overlay_scene.instantiate()
	add_child(layer)
	caption = layer.get_node("Bar/Caption")
	controls = layer.get_node("Controls")
	door_label = layer.get_node("Door")
	for key in ORDER:
		var actor: CharacterBody2D = host.actor_of(key)
		var sprite := AnimatedSprite2D.new()
		sprite.sprite_frames = actor.standard
		actor.apply_visual(sprite)
		# Replay positions replace position.y; offset is measured in texture coordinates.
		sprite.offset.y = actor.foot_offset / actor.visual_scale
		sprite.position.y = 0.0
		sprite.modulate = INK
		sprite.play("idle_front")
		sprite.z_index = 60
		add_child(sprite)
		figures[key] = sprite
		var bubble: PanelContainer = bubble_scene.instantiate()
		layer.add_child(bubble)
		bubbles[key] = bubble

func _teardown() -> void:
	if not is_instance_valid(host): return
	for actor in host.actors.values():
		if is_instance_valid(actor): actor.modulate.a = 1.0
	if is_instance_valid(host.decals): host.decals.modulate.a = 1.0
	host.set_bar_visible(true)
	if is_instance_valid(host.field.camera): host.field.camera.zoom = Vector2(2, 2)
	host.field.follow_camera = true
	for node in figures.values():
		if is_instance_valid(node): node.queue_free()
	figures.clear()
	bubbles.clear()
	if is_instance_valid(layer):
		layer.queue_free()

func position_of(frame: Dictionary, key: String) -> Vector2:
	var room: String = frame.rooms[key]
	var spot: String = frame.spots.get(key, "")
	if SPOTS.has(spot) and SPOTS[spot][0] == room:
		return SPOTS[spot][1] + OFFSETS[key] * 0.6
	if room == "corridor":
		return Vector2(650 + ORDER.find(key) * 90, 388)
	return ROOM_ANCHORS.get(room, Vector2(640, 500)) + OFFSETS[key]

func _apply(frame: Dictionary, previous: Dictionary) -> void:
	var who := str(frame.actor)
	caption.text = "[b]%s[/b]　%s" % [str(labels.get("caption", "")), str(frame.label)]
	for key in ORDER:
		var figure: AnimatedSprite2D = figures[key]
		var target := position_of(frame, key)
		if previous.is_empty():
			figure.position = target
		elif figure.position.distance_to(target) > 1.0:
			# 沿地图上的可走路线移动，不穿过桌椅。
			var route: PackedVector2Array = host.ink_route(figure.position, target)
			var total := 0.0
			var last := figure.position
			for point in route:
				total += last.distance_to(point)
				last = point
			var tween := create_tween().bind_node(figure)
			last = figure.position
			var facing := "front"
			for point in route:
				var step := point - last
				if step.length() < 0.5:
					continue
				facing = "left" if absf(step.x) > absf(step.y) else ("front" if step.y > 0 else "back")
				tween.tween_callback(_face.bind(figure, facing, step.x > 0 and facing == "left"))
				tween.tween_property(figure, "position", point, maxf(0.02, 0.8 / speed * step.length() / maxf(total, 1.0)))
				last = point
			tween.tween_callback(figure.play.bind("idle_" + facing))
		figure.modulate = INK if who != str(labels.names[key]) else Color(0.1, 0.08, 0.14, 0.9)
		var bubble: PanelContainer = bubbles[key]
		var text: RichTextLabel = bubble.get_meta("text")
		text.text = "[b]%s[/b]　%s" % [str(labels.names[key]), str(frame.thoughts[key])]
	var texts: Dictionary = labels.get("door_texts", {})
	var door := str(texts.get("open", "")) if bool(frame.door_open) else str(texts.get("bolted", "") if bool(frame.door_bolted) else texts.get("closed", ""))
	if bool(frame.smoke):
		door += "　·　" + str(texts.get("ash", "")) if bool(frame.ash) else ""
	door_label.text = door
	door_label.position = Vector2(120, 600)
	call_deferred("_place_bubbles", frame)

func _face(figure: AnimatedSprite2D, facing: String, flip: bool) -> void:
	figure.flip_h = flip
	figure.play("walk_" + facing)

func _place_bubbles(frame: Dictionary) -> void:
	if figures.is_empty():
		return
	var placed: Array[Rect2] = []
	# 相机在同一帧里刚换了缩放与位置，视口的画布变换要到下一帧才更新：按相机参数直接换算屏幕坐标。
	var zoom: Vector2 = host.field.camera.zoom
	var center: Vector2 = host.field.camera.position
	var half := Vector2(640, 496)
	var keys := ORDER.duplicate()
	keys.sort_custom(func(a, b): return position_of(frame, a).y > position_of(frame, b).y)
	for key in keys:
		var bubble: PanelContainer = bubbles[key]
		bubble.reset_size()
		var size := bubble.get_combined_minimum_size()
		var anchor: Vector2 = (position_of(frame, key) - center) * zoom + half
		var rect := Rect2(anchor + Vector2(-size.x / 2.0, -62 - size.y), size)
		rect.position.x = clampf(rect.position.x, 6, 1274 - size.x)
		rect.position.y = clampf(rect.position.y, 60, 940 - size.y)
		for _guard in 8:
			var hit := false
			for other in placed:
				if other.grow(4).intersects(rect):
					rect.position.y = other.position.y - size.y - 6
					hit = true
			if not hit:
				break
		rect.position.y = maxf(rect.position.y, 58)
		bubble.position = rect.position
		placed.append(rect)

func _unhandled_input(event: InputEvent) -> void:
	if not playing or event is not InputEventKey or not event.pressed or event.echo:
		return
	match event.keycode:
		KEY_SPACE:
			paused = not paused
			controls.text = ("已暂停　" if paused else "") + "空格 暂停　F 倍速　Esc 跳过"
		KEY_F, KEY_2:
			speed = 2.5 if speed == 1.0 else 1.0
			controls.text = ("倍速　" if speed > 1.0 else "") + "空格 暂停　F 倍速　Esc 跳过"
		KEY_ESCAPE:
			skipping = true
	get_viewport().set_input_as_handled()
