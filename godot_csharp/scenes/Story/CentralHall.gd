extends Node2D

const Actor = preload("res://scenes/Story/StoryActor.gd")
const Balloon = preload("res://scenes/Story/StoryDialogue.gd")
const Item = preload("res://scenes/Story/RoomItem.cs")
const Binding = preload("res://scenes/Story/RoomBinding.cs")
const ART := "res://ShareAsset/Images/shot01/"
var decoration := Node2D.new()
var items := Node2D.new()
var actors := Node2D.new()
var player: CharacterBody2D
var musician: CharacterBody2D
var dialogue: CanvasLayer
var story: Resource
var clock_item: Sprite2D
var nearest: Node2D
var caption: Label
var room_binding: Node
var completed := false
var response := ""
var bell := AudioStreamPlayer.new()
var debug_geometry := false
var collision_rects: Array[Rect2] = []

func _ready() -> void:
	get_window().title = "安眠者有福 · 中央厅清晨"
	get_window().content_scale_size = Vector2i(1024, 800)
	get_window().size = Vector2i(1024, 800)
	texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	decoration.name = "Decoration"
	items.name = "Items"
	actors.name = "Actors"
	actors.y_sort_enabled = true
	add_child(decoration)
	add_child(items)
	items.y_sort_enabled = true
	# Actors and props share the same feet-based sorting branch.
	items.add_child(actors)
	build_room()
	room_binding = Binding.new()
	room_binding.name = "RoomBinding"
	room_binding.set("RoomName", "中央厅")
	var paths: Array[NodePath] = []
	for node in decoration.get_children():
		if node is TileMapLayer:
			paths.append(NodePath("../Decoration/" + node.name))
	room_binding.set("DecorationLayerPaths", paths)
	add_child(room_binding)
	player = make_actor("Detective", Vector2(512, 510), "res://ShareAsset/Images/characters/animation_v2/detective.tres")
	player.pose("idle", "back")
	musician = make_actor("Musician", Vector2(368, 306), "res://ShareAsset/Images/characters/animation_v2/musician.tres")
	musician.special = load("res://ShareAsset/Images/characters/story_musician/special.tres")
	dialogue = Balloon.new()
	add_child(dialogue)
	dialogue.finished.connect(on_finished)
	dialogue.line_shown.connect(on_line)
	story = load("res://scenes/Story/central_hall.dialogue")
	caption = Label.new()
	caption.add_theme_font_size_override("font_size", 14)
	caption.add_theme_color_override("font_color", Color("f1dfb1"))
	caption.add_theme_color_override("font_shadow_color", Color("141519"))
	caption.add_theme_constant_override("shadow_offset_x", 1)
	caption.add_theme_constant_override("shadow_offset_y", 2)
	caption.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(caption)
	add_child(bell)
	bell.stream = make_bell()
	bell.volume_db = -14
	add_child(load("res://scenes/Story/DemoNavigation.gd").new())

func layer(label: String) -> TileMapLayer:
	var result := TileMapLayer.new()
	result.name = label
	result.tile_set = load("res://ShareAsset/Victorian/tilesets/Victorian.tres").duplicate()
	decoration.add_child(result)
	return result

func paint(map: TileMapLayer, rect: Rect2i, set_index: int, terrain_name: String) -> void:
	var cells: Array[Vector2i] = []
	for y in range(rect.position.y, rect.end.y):
		for x in range(rect.position.x, rect.end.x):
			cells.append(Vector2i(x, y))
	for terrain in map.tile_set.get_terrains_count(set_index):
		if map.tile_set.get_terrain_name(set_index, terrain) == terrain_name:
			map.set_cells_terrain_connect(cells, set_index, terrain, false)
			return
	assert(false, "Missing terrain " + terrain_name)

func stamp(map: TileMapLayer, path: String, at: Vector2i) -> void:
	var texture: Texture2D = load(path)
	var source := TileSetAtlasSource.new()
	source.texture = texture
	source.texture_region_size = Vector2i(32, 32)
	var source_id := map.tile_set.add_source(source)
	for y in int(texture.get_height() / 32):
		for x in int(texture.get_width() / 32):
			var cell := Vector2i(x, y)
			source.create_tile(cell)
			map.set_cell(at + cell, source_id, cell)

func build_room() -> void:
	var floor_map := layer("StoneFloor")
	floor_map.tile_set = TileSet.new()
	floor_map.tile_set.tile_size = Vector2i(32, 32)
	var stone := TileSetAtlasSource.new()
	stone.texture = load("res://ShareAsset/Images/story_hall/stone_slabs.res")
	stone.texture_region_size = Vector2i(32, 32)
	floor_map.tile_set.add_source(stone, 0)
	for y in 2:
		for x in 2:
			stone.create_tile(Vector2i(x, y))
	for y in range(6, 18):
		for x in 32:
			floor_map.set_cell(Vector2i(x, y), 0, Vector2i(x % 2, y % 2))
	floor_map.modulate = Color("a69d8e")
	var masonry := layer("Masonry")
	paint(masonry, Rect2i(0, 0, 32, 7), 1, "a4_wall_side_p0_c5")
	masonry.modulate = Color("a59b8b")
	var wood := layer("Wainscot")
	paint(wood, Rect2i(0, 5, 32, 2), 1, "a4_wall_side_p0_c6")
	wood.modulate = Color("b9a995")
	var finish := ShaderMaterial.new()
	finish.shader = load("res://scenes/Shot01/material_finish.gdshader")
	wood.material = finish
	var rug := layer("EntryRug")
	paint(rug, Rect2i(13, 14, 6, 4), 0, "a2_floor_r2_c0")
	rug.modulate = Color("737b7d")
	var details := TileMapLayer.new()
	details.name = "ArchitecturalFinish"
	details.tile_set = TileSet.new()
	details.tile_set.tile_size = Vector2i(32, 32)
	decoration.add_child(details)
	stamp(details, ART + "compass.res", Vector2i(13, 8))
	for x in [3, 8, 20, 25]:
		stamp(details, ART + "portal.res", Vector2i(x, 1))
		# The sample ends at the threshold; the exits disclose their limited scope.
		var threshold := make_item("Door" + str(x), "门廊", "door", null, Vector2(x * 32 + 64, 232))
		threshold.set_meta("reach", 45.0)
		if x in [3, 8]:
			threshold.set("DisplayName", "后勤院早餐厅" if x == 3 else "公共娱乐室")
			threshold.set_meta("destination", "res://scenes/Story/Breakfast.tscn" if x == 3 else "res://scenes/Story/ParlorGathering.tscn")
	for x in [0, 30]:
		stamp(details, ART + "window.res", Vector2i(x, 0))
	stamp(details, "res://ShareAsset/Images/story_hall/banner_1.res", Vector2i(7, 2))
	stamp(details, "res://ShareAsset/Images/story_hall/banner_2.res", Vector2i(24, 2))
	clock_item = make_item("Clock", "机械钟", "clock", load(ART + "clock.res"), Vector2(512, 224))
	clock_item.z_index = 0
	clock_item.set_meta("reach", 62.0)
	var academy := Sprite2D.new()
	academy.texture = load("res://ShareAsset/Images/story_hall/banner_3.res")
	academy.position = Vector2(512, 213)
	items.add_child(academy)
	for x in [160, 864]:
		var bench_item := make_item("Bench" + str(x), "窗边长椅", "bench", load(ART + "bench.res"), Vector2(x, 402))
		collider(Rect2(x - 59, 380, 118, 22), bench_item)
	for x in [80, 944]:
		var statue := Sprite2D.new()
		statue.texture = load(ART + "statue.res")
		statue.position = Vector2(x, 520)
		statue.offset.y = -statue.texture.get_height() / 2.0
		items.add_child(statue)
		collider(Rect2(x - 25, 495, 50, 25))
	for x in [42, 982]:
		var tree := Sprite2D.new()
		tree.texture = load(ART + "revision2/tree.res")
		tree.position = Vector2(x, 304)
		tree.offset.y = -tree.texture.get_height() / 2.0
		items.add_child(tree)
		collider(Rect2(x - 19, 288, 38, 16))
	var light := Sprite2D.new()
	light.texture = load(ART + "morning_light.res")
	light.position = Vector2(290, 386)
	light.modulate.a = 0.40
	light.z_index = 3
	add_child(light)
	collider(Rect2(0, 0, 1024, 224))
	collider(Rect2(-32, 224, 64, 352))
	collider(Rect2(992, 224, 64, 352))
	collider(Rect2(0, 560, 1024, 32))

func make_item(label: String, title: String, entry: String, texture: Texture2D, feet: Vector2) -> Sprite2D:
	var item := Item.new()
	item.name = label
	item.set("DisplayName", title)
	item.set("DialogueTitle", entry)
	item.texture = texture
	item.position = feet
	if texture:
		item.offset.y = -texture.get_height() / 2.0
	item.set_meta("reach", 64.0)
	items.add_child(item)
	return item

func collider(rect: Rect2, parent: Node2D = self) -> void:
	collision_rects.append(rect)
	var body := StaticBody2D.new()
	body.position = rect.get_center() - parent.position if parent != self else rect.get_center()
	var shape := CollisionShape2D.new()
	var box := RectangleShape2D.new()
	box.size = rect.size
	shape.shape = box
	body.add_child(shape)
	parent.add_child(body)

func make_actor(label: String, at: Vector2, path: String) -> CharacterBody2D:
	var actor := Actor.new()
	actor.name = label
	actor.position = at
	actor.standard = load(path)
	actors.add_child(actor)
	return actor

func _physics_process(_delta: float) -> void:
	if not is_instance_valid(dialogue) or dialogue.active or dialogue.history_panel.visible:
		return
	var direction := Vector2(
		float(Input.is_physical_key_pressed(KEY_D) or Input.is_physical_key_pressed(KEY_RIGHT)) - float(Input.is_physical_key_pressed(KEY_A) or Input.is_physical_key_pressed(KEY_LEFT)),
		float(Input.is_physical_key_pressed(KEY_S) or Input.is_physical_key_pressed(KEY_DOWN)) - float(Input.is_physical_key_pressed(KEY_W) or Input.is_physical_key_pressed(KEY_UP)))
	player.move_input(direction.normalized())
	nearest = null
	var distance := 78.0
	for candidate in [musician] + items.get_children():
		if candidate == actors or (candidate != musician and not candidate.has_meta("reach")):
			continue
		var current: float = player.position.distance_to(candidate.position)
		var reach: float = candidate.get_meta("reach", 72.0)
		if current < distance and current < reach:
			nearest = candidate
			distance = current
	caption.visible = nearest != null
	if nearest:
		caption.text = "E  ·  " + ("音乐家" if nearest == musician else str(nearest.get("DisplayName")))
		caption.position = nearest.position + Vector2(-40, 10)
	queue_redraw()

func _unhandled_input(event: InputEvent) -> void:
	if event is not InputEventKey or not event.pressed or event.echo:
		return
	if event.keycode == KEY_F3:
		debug_geometry = not debug_geometry
		queue_redraw()
	if dialogue.active or dialogue.history_panel.visible:
		return
	if event.keycode == KEY_R:
		reset_blocking()
		begin("opening")
	elif event.keycode in [KEY_E, KEY_ENTER] and nearest:
		if nearest.has_meta("destination"):
			get_tree().change_scene_to_file(str(nearest.get_meta("destination")))
		else:
			begin(("musician" if completed else "opening") if nearest == musician else str(nearest.get("DialogueTitle")))

func begin(title: String) -> void:
	caption.hide()
	player.pose("idle")
	dialogue.start(story, title, self)

func reset_blocking() -> void:
	player.position = Vector2(512, 510)
	musician.position = Vector2(368, 306)
	player.pose("idle", "back")
	musician.pose("idle", "front")
	completed = false
	response = ""

func stage_arrival() -> void:
	# Build a temporary route from the actual feet position, including the other actor.
	# This avoids trapping the opening if approached from above the musician.
	dialogue.idle("中央厅", "晨光穿过高窗，钟摆的影子缓缓挪动。")
	var grid := AStarGrid2D.new()
	grid.region = Rect2i(0, 0, 128, 72)
	grid.cell_size = Vector2(8, 8)
	grid.diagonal_mode = AStarGrid2D.DIAGONAL_MODE_NEVER
	grid.update()
	var obstacles := collision_rects.duplicate()
	obstacles.append(Rect2(musician.position + Vector2(-8, -12), Vector2(16, 12)))
	for y in 72:
		for x in 128:
			var cell := Vector2i(x, y)
			var point := Vector2(cell * 8)
			for rect in obstacles:
				var expanded := Rect2(rect.position + Vector2(-9, -1), rect.size + Vector2(18, 14))
				if expanded.has_point(point):
					grid.set_point_solid(cell)
					break
	var start := Vector2i((player.position / 8).round())
	grid.set_point_solid(start, false)
	var route := grid.get_point_path(start, Vector2i(54, 40))
	await player.follow_path(route)
	player.pose("idle", "left")
	musician.pose("idle", "right")

func listen_to_clock() -> void:
	musician.pose("idle", "left")
	bell.play()
	await musician.perform("listen_left")
	musician.pose("idle", "right")

func ring_clock() -> void:
	bell.play()
	await get_tree().create_timer(1.5).timeout

func set_response(value: String) -> void:
	response = value

func finish_opening() -> void:
	completed = true
	musician.pose("idle", "front")
	player.pose("idle", "left")

func inspect_clock() -> void:
	player.pose("inspect", "back")
	await player.sprite.animation_finished
	player.pose("idle", "back")

func on_line(character: String) -> void:
	player.pose("talk" if character == "侦探" else "idle")
	musician.pose("talk" if character == "音乐家" else "idle")

func on_finished() -> void:
	player.pose("idle")
	musician.pose("idle")
	dialogue.idle("晨光未散" if completed else "中央厅", "钟声落下，厅里又安静了。还可以看看机械钟，或与音乐家再聊几句。" if completed else "走近音乐家，或先看看机械钟。")

func _draw() -> void:
	if not debug_geometry or not is_instance_valid(player):
		return
	for x in range(0, 1025, 32):
		draw_line(Vector2(x, 224), Vector2(x, 560), Color(0.5, 0.8, 0.8, 0.2))
	for y in range(224, 561, 32):
		draw_line(Vector2(0, y), Vector2(1024, y), Color(0.5, 0.8, 0.8, 0.2))
	for rect in collision_rects:
		draw_rect(rect, Color(1, 0.5, 0.3, 0.7), false)
	for actor in [player, musician]:
		draw_rect(Rect2(actor.position + Vector2(-32, -55), Vector2(64, 64)), Color(0.7, 0.7, 1), false)
		draw_rect(Rect2(actor.position + Vector2(-8, -12), Vector2(16, 12)), Color(0.5, 1, 0.5), false)
		draw_circle(actor.position, 2, Color.YELLOW)

func make_bell() -> AudioStreamWAV:
	# Quiet synthetic clock chime, no external recording or music dependency.
	var stream := AudioStreamWAV.new()
	stream.format = AudioStreamWAV.FORMAT_16_BITS
	stream.mix_rate = 22050
	var data := PackedByteArray()
	data.resize(22050 * 2 * 3)
	for i in 22050 * 3:
		var t := float(i) / 22050.0
		var envelope := exp(-t * 2.2) * minf(t * 70, 1.0)
		var sample := (sin(TAU * 523.25 * t) + 0.35 * sin(TAU * 1048 * t) + 0.14 * sin(TAU * 1567 * t)) * envelope
		data.encode_s16(i * 2, int(sample * 15000))
	stream.data = data
	return stream
