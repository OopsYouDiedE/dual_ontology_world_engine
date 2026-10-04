extends Node2D
## 昨夜回放：在真实地图上用半透明墨色人影重演，每人头顶一个心声气泡（此刻他听到/相信的东西）。
## 同一时刻，四个气泡并排显示四种不同的认知。可暂停、倍速、跳过；跳过与看完的结算相同。

const Paper := preload("res://scenes/Shared/UI/Paper.gd")

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
const ORDER := ["shadow"]
const INK := Color(0.16, 0.13, 0.2, 0.74)

var restore_map := ""
var restore_position := Vector2.ZERO
var restore_camera := Vector2.ZERO
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
		var hold := maxf(1.6, str(frame.text).length() / 12.0)
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
	restore_map = String(host.map.name)
	restore_position = host.player.position
	restore_camera = host.camera.position
	host.player.modulate.a = 0.0
	for actor in host.actors.values():
		actor.modulate.a = 0.0
	host.ui.visible = false
	host.camera.zoom = Vector2.ONE
	host.camera.global_position = host.map.get_meta("map_size") / 2.0
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
	controls.add_theme_font_override("font", Paper.font())
	controls.add_theme_font_size_override("font_size", 16)
	controls.add_theme_color_override("font_color", Color("cfc3a2"))
	controls.position = Vector2(960, 950)
	controls.size = Vector2(300, 30)
	controls.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	layer.add_child(controls)
	door_label = Label.new()
	door_label.add_theme_font_override("font", Paper.font())
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
	host.load_map(restore_map, "", restore_position, true, false)
	host.player.modulate.a = 1.0
	host.camera.position = restore_camera
	for actor in host.actors.values():
		actor.modulate.a = 1.0
	host.ui.visible = true
	host.camera.zoom = Vector2(2, 2)
	for node in figures.values():
		node.queue_free()
	figures.clear()
	bubbles.clear()
	if is_instance_valid(layer):
		layer.queue_free()

func position_of(_frame: Dictionary, _key: String) -> Vector2:
	var stands: Node = host.map.get_node_or_null("Stands")
	return stands.get_child(0).position if stands and stands.get_child_count() > 0 else host.player.position

func _apply(frame: Dictionary, _previous: Dictionary) -> void:
	var place := String(frame.place)
	var located: bool = host.PLACE_MAP.has(place)
	if located:
		var target: Array = host.PLACE_MAP[place]
		if String(host.map.name) != String(target[0]):
			host.load_map(target[0], target[1], Vector2.ZERO, false, false)
		for actor in host.actors.values():
			actor.modulate.a = 0.0
		host.camera.global_position = host.map.get_meta("map_size") / 2.0
		host.camera.reset_smoothing()
	var text: String = frame.text
	var parts := text.split("　心声：", true, 1)
	caption.text = "[b]昨夜[/b]　" + parts[0]
	var figure: AnimatedSprite2D = figures.shadow
	figure.position = position_of(frame, "shadow")
	figure.visible = located
	# No identity is revealed by the silhouette, even when a hypothesis names a person.
	figure.modulate = Color(0, 0, 0, 0.74)
	var bubble: PanelContainer = bubbles.shadow
	var thought: RichTextLabel = bubble.get_meta("text")
	thought.text = "[b]%s[/b]%s" % [frame.figure, "　" + parts[1] if parts.size() > 1 else ""]
	bubble.visible = parts.size() > 1
	door_label.text = host._place_name(place) if located else "地点尚未确定"
	door_label.position = Vector2(24, 64)
	call_deferred("_place_bubbles", frame)

func _face(figure: AnimatedSprite2D, facing: String, flip: bool) -> void:
	figure.flip_h = flip
	figure.play("walk_" + facing)

func _place_bubbles(frame: Dictionary) -> void:
	if figures.is_empty():
		return
	var placed: Array[Rect2] = []
	# 相机在同一帧里刚换了缩放与位置，视口的画布变换要到下一帧才更新：按相机参数直接换算屏幕坐标。
	var zoom: Vector2 = host.camera.zoom
	var center: Vector2 = host.camera.global_position
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
