extends Node2D

const Actor = preload("res://scenes/Story/StoryActor.gd")
const Balloon = preload("res://scenes/Story/StoryDialogue.gd")
const Item = preload("res://scenes/Story/RoomItem.cs")
const Binding = preload("res://scenes/Story/RoomBinding.cs")
const ART := "res://ShareAsset/Internal/Props/"
var decoration := Node2D.new()
var items := Node2D.new()
var actors := Node2D.new()
var player: CharacterBody2D
var musician: CharacterBody2D
var naturalist: CharacterBody2D
var dialogue: CanvasLayer
var story: Resource
var clock_item: Sprite2D
var nearest: Node2D
var caption: Label
var room_binding: Node
var completed := false
var response := ""
var conversation := false
var montage_playing := false
var bell := AudioStreamPlayer.new()
var debug_geometry := false
var collision_rects: Array[Rect2] = []

func _ready() -> void:
	get_window().title = "安眠者有福 · 中央厅清晨"
	preload("res://scenes/Story/StoryPresentation.gd").install(self, 3, Vector2(320, 248))
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
	player = make_actor("Detective", Vector2(320, 342), "res://ShareAsset/Internal/Characters/animation_v2/detective.tres")
	player.pose("idle", "back")
	musician = make_actor("Musician", Vector2(224, 190), "res://ShareAsset/Internal/Characters/animation_v2/musician.tres")
	musician.special = load("res://ShareAsset/Internal/Characters/story_musician/special.tres")
	naturalist = make_actor("Naturalist", Vector2(480, 204), "res://ShareAsset/Internal/Characters/animation_v2/naturalist.tres")
	naturalist.pose("inspect", "right")
	dialogue = Balloon.new()
	add_child(dialogue)
	dialogue.finished.connect(on_finished)
	dialogue.line_shown.connect(on_line)
	story = load("res://scenes/Story/central_hall.dialogue")
	caption = Label.new()
	caption.add_theme_font_size_override("font_size", 8)
	caption.add_theme_color_override("font_color", Color("f1dfb1"))
	caption.add_theme_color_override("font_shadow_color", Color("141519"))
	caption.add_theme_constant_override("shadow_offset_x", 1)
	caption.add_theme_constant_override("shadow_offset_y", 2)
	caption.mouse_filter = Control.MOUSE_FILTER_IGNORE
	preload("res://scenes/Story/StoryPresentation.gd").add_prompt(self,caption)
	add_child(bell)
	bell.stream = make_bell()
	bell.volume_db = -14
	add_child(load("res://scenes/Story/DemoNavigation.gd").new())
	dialogue.idle("中央厅", "M 观看七秒日常片段    R 播放剧情对白    E 查看或交谈。")

func layer(label: String) -> TileMapLayer:
	var result := TileMapLayer.new()
	result.name = label
	result.tile_set = load("res://ShareAsset/Internal/Props/ReasoningStudy/Study.tres").duplicate()
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
	paint(floor_map, Rect2i(1, 4, 18, 8), 0, "石板地面")
	var masonry := layer("Masonry")
	paint(masonry, Rect2i(1, 0, 18, 4), 1, "石砌墙面")
	var wood := layer("Wainscot")
	paint(wood, Rect2i(1, 3, 18, 1), 1, "木护墙")
	var rug := layer("EntryRug")
	paint(rug, Rect2i(8, 9, 4, 3), 0, "蓝金地毯")
	var caps := layer("RoomBoundary")
	for rect in [Rect2i(0,0,1,12), Rect2i(19,0,1,12), Rect2i(1,11,8,1), Rect2i(11,11,8,1)]:
		paint(caps, rect, 0, "木质墙顶")
	var details := TileMapLayer.new()
	details.name = "ArchitecturalFinish"
	details.tile_set = TileSet.new()
	details.tile_set.tile_size = Vector2i(32,32)
	decoration.add_child(details)
	stamp(details, ART + "story_compass.res", Vector2i(8,5))
	for x in [2, 5, 13, 16]:
		stamp(details, ART + "story_portal.res", Vector2i(x,0))
		var threshold := make_item("Door" + str(x), "门廊", "door", null, Vector2(x*32+32,140))
		threshold.set_meta("reach", 30.0)
		if x in [2,5]:
			threshold.set("DisplayName", "后勤院早餐厅" if x == 2 else "公共娱乐室")
			threshold.set_meta("destination", "res://scenes/Story/Breakfast.tscn" if x == 2 else "res://scenes/Story/ParlorGathering.tscn")
	for x in [1,18]:
		stamp(details, ART + "story_window.res", Vector2i(x,0))
	clock_item = make_item("Clock", "机械钟", "clock", load(ART + "story_clock.res"), Vector2(320,128))
	clock_item.set_meta("reach", 44.0)
	for spec in [[1,256],[3,320],[2,384]]:
		var banner := Sprite2D.new()
		banner.texture = load(ART + "story_banner_%d.res" % spec[0])
		banner.position = Vector2(spec[1],56)
		decoration.add_child(banner)
	for x in [88,552]:
		var bench := make_item("Bench"+str(x),"窗边长椅","bench",load(ART+"story_bench.res"),Vector2(x,252))
		collider(Rect2(x-35,236,70,16),bench)
	for x in [64,576]:
		var statue := make_item("Statue"+str(x),"学院塑像","bench",load(ART+"story_statue.res"),Vector2(x,344))
		collider(Rect2(x-14,328,28,16),statue)
	for x in [48,496]:
		var tree := make_item("Tree"+str(x),"窗边盆栽","bench",load(ART+"story_tree.res"),Vector2(x,184))
		collider(Rect2(x-10,174,20,10),tree)
	preload("res://scenes/Story/StoryPresentation.gd").daylight(decoration)
	collider(Rect2(0,0,640,128))
	collider(Rect2(0,128,32,256))
	collider(Rect2(608,128,32,256))
	collider(Rect2(0,368,640,32))
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
	if not montage_playing:
		$StoryCamera.position.x = clampf(player.position.x, 213.0, 427.0)
		$StoryCamera.position.y = clampf(player.position.y, 166.0, 256.0)
	if not is_instance_valid(dialogue) or dialogue.active or dialogue.history_panel.visible or montage_playing:
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
	queue_redraw()

func _unhandled_input(event: InputEvent) -> void:
	if event is not InputEventKey or not event.pressed or event.echo:
		return
	if event.keycode == KEY_F3:
		debug_geometry = not debug_geometry
		queue_redraw()
	if dialogue.active or dialogue.history_panel.visible or montage_playing:
		return
	if event.keycode == KEY_M:
		play_montage()
	elif event.keycode == KEY_R:
		reset_blocking()
		begin("opening")
	elif event.keycode in [KEY_E, KEY_ENTER] and nearest:
		player.face_point(nearest.position)
		if nearest == musician:
			musician.face_point(player.position)
		if nearest.has_meta("destination"):
			get_tree().change_scene_to_file(str(nearest.get_meta("destination")))
		else:
			begin(("musician" if completed else "opening") if nearest == musician else str(nearest.get("DialogueTitle")))

func begin(title: String) -> void:
	caption.hide()
	conversation = title in ["opening", "musician"]
	player.pose("idle")
	dialogue.start(story, title, self)

func reset_blocking() -> void:
	player.position = Vector2(320, 342)
	musician.position = Vector2(224, 190)
	player.pose("idle", "back")
	musician.pose("idle", "front")
	completed = false
	response = ""

func play_montage() -> void:
	montage_playing = true
	reset_blocking()
	player.position = Vector2(160, 240)
	player.pose("walk", "right")
	musician.pose("idle", "back")
	naturalist.position = Vector2(480, 204)
	naturalist.pose("inspect", "right")
	$StoryCamera.position = Vector2(320, 166)
	caption.hide()
	dialogue.panel.hide()
	player.walk_to(Vector2(220, 240))
	await get_tree().create_timer(2.0).timeout
	bell.play()
	player.pose("idle", "back")
	naturalist.pose("idle", "left")
	musician.face_point(clock_item.position)
	musician.perform("listen_left", musician.facing == "right")
	await get_tree().create_timer(2.7).timeout
	musician.pose("idle", "right")
	naturalist.pose("idle", "left")
	await get_tree().create_timer(0.4).timeout
	naturalist.pose("inspect", "right")
	player.walk_to(Vector2(335, 240))
	await get_tree().create_timer(1.9).timeout
	player.pose("idle", "back")
	montage_playing = false
	dialogue.panel.show()
	dialogue.idle("中央厅", "E 查看或交谈    R 重演对白    M 重看日常片段。")

func stage_arrival() -> void:
	# Build a temporary route from the actual feet position, including the other actor.
	# This avoids trapping the opening if approached from above the musician.
	dialogue.idle("中央厅", "晨光穿过高窗，钟摆的影子缓缓挪动。")
	var grid := AStarGrid2D.new()
	grid.region = Rect2i(0, 0, 80, 48)
	grid.cell_size = Vector2(8, 8)
	grid.diagonal_mode = AStarGrid2D.DIAGONAL_MODE_NEVER
	grid.update()
	var obstacles := collision_rects.duplicate()
	obstacles.append(Rect2(musician.position + Vector2(-8, -12), Vector2(16, 12)))
	for y in 48:
		for x in 80:
			var cell := Vector2i(x, y)
			var point := Vector2(cell * 8)
			for rect in obstacles:
				var expanded := Rect2(rect.position + Vector2(-9, -1), rect.size + Vector2(18, 14))
				if expanded.has_point(point):
					grid.set_point_solid(cell)
					break
	var start := Vector2i((player.position / 8).round())
	grid.set_point_solid(start, false)
	var route := grid.get_point_path(start, Vector2i(36, 25))
	await player.follow_path(route)
	player.pose("idle", "left")
	musician.pose("idle", "right")

func listen_to_clock() -> void:
	musician.face_point(clock_item.position)
	musician.pose("idle")
	bell.play()
	await musician.perform("listen_left", musician.facing == "right")
	musician.face_point(player.position)
	musician.pose("idle")

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
	if conversation and character in ["侦探", "音乐家"]:
		player.face_point(musician.position)
		musician.face_point(player.position)
	player.pose("talk" if character == "侦探" else "idle")
	musician.pose("talk" if character == "音乐家" else "idle")

func on_finished() -> void:
	player.pose("idle")
	musician.pose("idle")
	dialogue.idle("晨光未散" if completed else "中央厅", "钟声落下。M 观看日常片段；还可以看看机械钟或与音乐家交谈。" if completed else "M 观看七秒日常片段；走近音乐家或查看机械钟。")

func _draw() -> void:
	if not debug_geometry or not is_instance_valid(player):
		return
	for x in range(0, 641, 32):
		draw_line(Vector2(x, 128), Vector2(x, 368), Color(0.5, 0.8, 0.8, 0.2))
	for y in range(128, 369, 32):
		draw_line(Vector2(0, y), Vector2(640, y), Color(0.5, 0.8, 0.8, 0.2))
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
