extends Node2D
## 昨夜回放：在真实地图上用半透明墨色人影重演，每人头顶一个心声气泡（此刻他听到/相信的东西）。
## 同一时刻，四个气泡并排显示四种不同的认知。可暂停、倍速、跳过；跳过与看完的结算相同。

const Paper := preload("res://scenes/Awakening/Paper.gd")
const Content := preload("res://scenes/Awakening/AwakeningContent.gd")

const ROOM_ANCHORS := {
	"R05": Vector2(150, 214), "R06": Vector2(470, 214), "R07": Vector2(790, 214), "R08": Vector2(1110, 214),
	"corridor": Vector2(640, 428), "hall": Vector2(300, 748), "pantry": Vector2(996, 768), "foyer": Vector2(648, 930),
	"main": Vector2(648, 1010),
}
const SPOTS := {
	"door": ["foyer", Vector2(648, 940)], "tube": ["hall", Vector2(498, 572)], "slate": ["hall", Vector2(380, 690)],
	"desk": ["R08", Vector2(1030, 212)], "sink": ["pantry", Vector2(862, 596)],
}
const OFFSETS := {"detective": Vector2(-22, 4), "huo": Vector2(22, 4), "shen": Vector2(-6, 18), "su": Vector2(10, -12)}
const ORDER := ["detective", "huo", "shen", "su"]
const INK := Color(0.16, 0.13, 0.2, 0.74)

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

func play(frames: Array) -> void:
	if frames.is_empty():
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
			if not paused:
				waited += get_process_delta_time() * speed
	if not skipping:
		var waited := 0.0
		while waited < 1.2 and not skipping:
			await get_tree().process_frame
			waited += get_process_delta_time()
	_teardown()
	playing = false

func _setup() -> void:
	for actor in host.actors.values():
		actor.modulate.a = 0.0
	host.decals.modulate.a = 0.35
	host.ui.visible = false
	host.follow_camera = false
	host.camera.zoom = Vector2.ONE
	host.camera.position = Vector2(640, 508)
	layer = CanvasLayer.new()
	layer.layer = 14
	add_child(layer)
	var theme := Paper.theme()
	var bar := PanelContainer.new()
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.09, 0.08, 0.1, 0.86)
	style.set_content_margin_all(10)
	bar.add_theme_stylebox_override("panel", style)
	bar.position = Vector2(0, 0)
	bar.size = Vector2(1280, 54)
	bar.custom_minimum_size = Vector2(1280, 54)
	bar.theme = theme
	layer.add_child(bar)
	caption = Paper.label("", 19, Color("e8d6a7"))
	bar.add_child(caption)
	controls = Label.new()
	controls.text = "空格 暂停　F 倍速　Esc 跳过"
	controls.add_theme_font_override("font", Paper.FONT)
	controls.add_theme_font_size_override("font_size", 16)
	controls.add_theme_color_override("font_color", Color("cfc3a2"))
	controls.position = Vector2(960, 950)
	controls.size = Vector2(300, 30)
	controls.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	layer.add_child(controls)
	door_label = Label.new()
	door_label.add_theme_font_override("font", Paper.FONT)
	door_label.add_theme_font_size_override("font_size", 16)
	door_label.add_theme_color_override("font_color", Color("f1e4bd"))
	door_label.add_theme_color_override("font_outline_color", Color(0.05, 0.04, 0.06))
	door_label.add_theme_constant_override("outline_size", 5)
	layer.add_child(door_label)
	for key in ORDER:
		var actor: CharacterBody2D = host.actor_of(key)
		var sprite := AnimatedSprite2D.new()
		sprite.sprite_frames = actor.standard
		sprite.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		sprite.offset = Vector2(0, -23)
		sprite.modulate = INK
		sprite.play("idle_front")
		sprite.z_index = 60
		add_child(sprite)
		figures[key] = sprite
		var bubble := PanelContainer.new()
		var b := StyleBoxFlat.new()
		b.bg_color = Color(0.93, 0.89, 0.78, 0.94)
		b.border_color = Color(0.17, 0.13, 0.09, 0.8)
		b.set_border_width_all(1)
		b.set_corner_radius_all(6)
		b.content_margin_left = 8
		b.content_margin_right = 8
		b.content_margin_top = 4
		b.content_margin_bottom = 4
		bubble.add_theme_stylebox_override("panel", b)
		bubble.theme = theme
		var text := Paper.label("", 15)
		text.custom_minimum_size = Vector2(196, 0)
		bubble.add_child(text)
		bubble.set_meta("text", text)
		layer.add_child(bubble)
		bubbles[key] = bubble

func _teardown() -> void:
	for actor in host.actors.values():
		actor.modulate.a = 1.0
	host.decals.modulate.a = 1.0
	host.ui.visible = true
	host.camera.zoom = Vector2(2, 2)
	host.follow_camera = true
	for node in figures.values():
		node.queue_free()
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
		return Vector2(380 + ORDER.find(key) * 170, 428)
	return ROOM_ANCHORS.get(room, Vector2(640, 500)) + OFFSETS[key]

func _apply(frame: Dictionary, previous: Dictionary) -> void:
	var who := str(frame.actor)
	caption.text = "[b]昨夜[/b]　" + str(frame.label)
	for key in ORDER:
		var figure: AnimatedSprite2D = figures[key]
		var target := position_of(frame, key)
		if previous.is_empty():
			figure.position = target
		elif figure.position.distance_to(target) > 1.0:
			var direction := target - figure.position
			var facing := "left" if absf(direction.x) > absf(direction.y) else ("front" if direction.y > 0 else "back")
			figure.flip_h = direction.x > 0 and facing == "left"
			figure.play("walk_" + facing)
			var tween := create_tween()
			tween.tween_property(figure, "position", target, 0.7 / speed)
			tween.tween_callback(figure.play.bind("idle_" + facing))
		figure.modulate = INK if who != Content.PERSON_NAMES[key] else Color(0.1, 0.08, 0.14, 0.9)
		var bubble: PanelContainer = bubbles[key]
		var text: RichTextLabel = bubble.get_meta("text")
		text.text = "[b]%s[/b]　%s" % [Content.PERSON_NAMES[key], str(frame.thoughts[key])]
	var door := "门：开着，木楔撑住" if bool(frame.door_open) else ("门：闩上了" if bool(frame.door_bolted) else "门：关着")
	if bool(frame.smoke):
		door += "　·　烟灰进了门厅" if bool(frame.ash) else ""
	door_label.text = door
	door_label.position = Vector2(560, 880)
	call_deferred("_place_bubbles", frame)

func _place_bubbles(frame: Dictionary) -> void:
	if figures.is_empty():
		return
	var placed: Array[Rect2] = []
	var canvas := get_viewport().get_canvas_transform()
	var keys := ORDER.duplicate()
	keys.sort_custom(func(a, b): return position_of(frame, a).y > position_of(frame, b).y)
	for key in keys:
		var bubble: PanelContainer = bubbles[key]
		bubble.reset_size()
		var size := bubble.get_combined_minimum_size()
		var anchor: Vector2 = canvas * position_of(frame, key)
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
