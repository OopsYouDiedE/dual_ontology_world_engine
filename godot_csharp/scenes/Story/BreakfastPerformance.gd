extends Node2D
## One continuous, wordless exchange. The same performance runs for M and Movie Maker.
signal completed
const DURATION := 7.2
const ATLAS := preload("res://ShareAsset/Internal/Characters/animation_v2/breakfast_exchange.png")
var active := false
var elapsed := 0.0
var stage := "idle"
var bowl: Sprite2D
var giver: Sprite2D
var receiver: Sprite2D
var room: Node2D
var sound := AudioStreamPlayer.new()
var has_lifted := false
var has_landed := false
var presenting := false

func _ready() -> void:
	room = get_parent()
	bowl = room.items.get_node("BreakfastTable/SoupBowl80")
	giver = make_pose(room.cast[2], 0)
	receiver = make_pose(room.cast[1], 1)
	add_child(sound)
	giver.hide()
	receiver.hide()

func make_pose(actor: Node2D, row: int) -> Sprite2D:
	var sprite := Sprite2D.new()
	var texture := AtlasTexture.new()
	texture.atlas = ATLAS
	texture.region = Rect2(0, row * 64, 64, 64)
	sprite.texture = texture
	sprite.offset = Vector2(0, -23)
	sprite.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	actor.add_child(sprite)
	return sprite

func pose(sprite: Sprite2D, row: int, frame: int) -> void:
	(sprite.texture as AtlasTexture).region = Rect2(frame * 64, row * 64, 64, 64)

func play() -> void:
	presenting = true
	active = true
	elapsed = 0.0
	has_lifted = false
	has_landed = false
	room.cast[1].sprite.hide()
	room.cast[2].sprite.hide()
	giver.show()
	receiver.show()
	room.cast[0].get_node("ServingBowl").hide()
	room.items.get_node("BreakfastTable/SoupBowl24").hide()
	# Keep the detective and chef in the composition; only the pair performs.
	room.get_node("StoryCamera").zoom = Vector2(4, 4)
	room.get_node("StoryCamera").position = Vector2(224, 190)
	bowl.z_index = 3
	update_performance()

func release() -> void:
	if not presenting:
		return
	presenting = false
	giver.hide()
	receiver.hide()
	room.cast[1].sprite.show()
	room.cast[2].sprite.show()
	room.get_node("StoryCamera").zoom = Vector2(3, 3)
	room.get_node("StoryCamera").position = Vector2(224, 195)

func _process(delta: float) -> void:
	if not active:
		return
	elapsed = minf(elapsed + delta, DURATION)
	update_performance()
	if elapsed >= DURATION:
		active = false
		stage = "complete"
		completed.emit()

func move_bowl(from: Vector2, to: Vector2, start: float, duration: float, lift := 0.0) -> void:
	var progress := clampf((elapsed - start) / duration, 0.0, 1.0)
	var eased := smoothstep(0.0, 1.0, progress)
	bowl.global_position = (from.lerp(to, eased) + Vector2(0, -sin(progress * PI) * lift)).round()

func update_performance() -> void:
	# 0: an empty setting; 1: notice; 2: hesitate; 3: touch/lift; 4: offer;
	# 5: mutual contact; 6: receiver draws it in; 7: acknowledge and settle.
	if elapsed < 0.9:
		stage = "empty_place"
		pose(giver, 0, 0)
		pose(receiver, 1, 0)
		bowl.global_position = Vector2(288, 185)
	elif elapsed < 1.7:
		stage = "notice"
		pose(giver, 0, 1)
		pose(receiver, 1, 0)
	elif elapsed < 2.05:
		stage = "reach"
		pose(giver, 0, 2)
	elif elapsed < 2.45:
		stage = "lift"
		pose(giver, 0, 3)
		move_bowl(Vector2(288, 185), Vector2(288, 181), 2.05, 0.4)
		if not has_lifted:
			has_lifted = true
			ceramic(0.22)
	elif elapsed < 3.15:
		stage = "offer"
		pose(receiver, 1, 1)
		# Wrist anchors belong to each drawing: never tween the prop away
		# from a static hand while its owner is still carrying it.
		if elapsed < 2.65:
			pose(giver, 0, 6)
			bowl.global_position = Vector2(282, 180)
		elif elapsed < 2.85:
			pose(giver, 0, 7)
			bowl.global_position = Vector2(277, 180)
		else:
			pose(giver, 0, 4)
			bowl.global_position = Vector2(269, 180)
	elif elapsed < 3.75:
		stage = "receive"
		pose(receiver, 1, 2)
		bowl.global_position = Vector2(269, 180)
	elif elapsed < 4.35:
		stage = "take"
		pose(giver, 0, 5)
		if elapsed < 3.95:
			pose(receiver, 1, 6)
			bowl.global_position = Vector2(259, 180)
		elif elapsed < 4.15:
			pose(receiver, 1, 7)
			bowl.global_position = Vector2(251, 181)
		else:
			pose(receiver, 1, 3)
			bowl.global_position = Vector2(248, 181)
	elif elapsed < 5.05:
		stage = "acknowledge"
		pose(receiver, 1, 3)
	elif elapsed < 5.65:
		stage = "thanks"
		pose(receiver, 1, 5)
	elif elapsed < 6.1:
		stage = "set_down"
		pose(receiver, 1, 4)
		move_bowl(Vector2(248, 181), Vector2(248, 185), 5.65, 0.45)
	else:
		stage = "settle"
		pose(receiver, 1, 4)
		if not has_landed:
			has_landed = true
			ceramic(0.45)

func ceramic(volume: float) -> void:
	# A small in-engine ceramic/wood contact, not explanatory UI or a soundtrack.
	var data := PackedByteArray()
	data.resize(12000)
	var random := RandomNumberGenerator.new()
	random.seed = 17
	for index in 6000:
		var t := float(index) / 24000.0
		var wave := sin(TAU * 1480.0 * t) * exp(-t * 38.0) * 0.45
		wave += sin(TAU * 710.0 * t) * exp(-t * 65.0) * 0.25
		wave += random.randf_range(-1.0, 1.0) * exp(-t * 180.0) * 0.15
		data.encode_s16(index * 2, int(wave * volume * 22000.0))
	var stream := AudioStreamWAV.new()
	stream.format = AudioStreamWAV.FORMAT_16_BITS
	stream.mix_rate = 24000
	stream.data = data
	sound.stream = stream
	sound.play()
