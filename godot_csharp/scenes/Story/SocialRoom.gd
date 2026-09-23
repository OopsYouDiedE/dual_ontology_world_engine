extends Node2D

const Actor = preload("res://scenes/Story/StoryActor.gd")
const Balloon = preload("res://scenes/Story/StoryDialogue.gd")
const Item = preload("res://scenes/Story/RoomItem.cs")
const Binding = preload("res://scenes/Story/RoomBinding.cs")
const TILESET_PATH := "res://ShareAsset/Victorian/tilesets/Victorian.tres"

@export_enum("breakfast", "parlor") var room_kind := "breakfast"
var decoration := Node2D.new()
var items := Node2D.new()
var actors := Node2D.new()
var collision_rects: Array[Rect2] = []
var player: CharacterBody2D
var cast: Array[CharacterBody2D] = []
var dialogue: CanvasLayer
var story: Resource
var nearest: Node2D
var caption: Label
var debug_geometry := false

func _ready() -> void:
	get_window().title = "安眠者有福 · " + ("后勤院早餐" if room_kind == "breakfast" else "公共娱乐室围谈")
	get_window().content_scale_size = Vector2i(1024, 800)
	get_window().size = Vector2i(1024, 800)
	texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	decoration.name = "Decoration"
	items.name = "Items"
	items.y_sort_enabled = true
	actors.name = "Actors"
	actors.y_sort_enabled = true
	add_child(decoration)
	add_child(items)
	items.add_child(actors)
	build_room()
	var binding := Binding.new()
	binding.name = "RoomBinding"
	binding.set("RoomName", "后勤院" if room_kind == "breakfast" else "公共娱乐室")
	var layer_paths: Array[NodePath] = []
	for node in decoration.get_children():
		if node is TileMapLayer:
			layer_paths.append(NodePath("../Decoration/" + node.name))
	binding.set("DecorationLayerPaths", layer_paths)
	add_child(binding)
	player = make_actor("侦探", Vector2(512, 520), "侦探", "res://ShareAsset/Images/characters/animation_v2/detective.tres")
	if room_kind == "breakfast":
		cast = [
			make_actor("主厨", Vector2(404, 338), "主厨", "res://ShareAsset/Images/characters/animation_v2/chef.tres"),
			make_actor("博物学家", Vector2(620, 338), "自然学家", "res://ShareAsset/Images/characters/animation_v2/naturalist.tres"),
			make_actor("机械工程师", Vector2(784, 421), "工程师", "res://ShareAsset/Images/characters/animation_v2/engineer.tres")]
		story = load("res://scenes/Story/breakfast.dialogue")
	else:
		cast = [
			make_actor("诗人", Vector2(512, 340), "诗人", "res://ShareAsset/Images/characters/animation_v2/poet.tres"),
			make_actor("盗贼", Vector2(374, 449), "盗贼", "res://ShareAsset/Images/characters/animation_v2/thief.tres"),
			make_actor("律师", Vector2(650, 449), "律师", "res://ShareAsset/Images/characters/animation_v2/lawyer.tres"),
			make_actor("画家", Vector2(758, 350), "画家", "res://ShareAsset/Images/characters/animation_v2/painter.tres")]
		story = load("res://scenes/Story/parlor.dialogue")
		for index in [1, 2]:
			cast[index].set_meta("seated", true)
			rest_pose(cast[index])
	dialogue = Balloon.new()
	add_child(dialogue)
	dialogue.heading.text = "序幕  /  " + ("后勤院 · 早餐" if room_kind == "breakfast" else "公共娱乐室 · 围谈")
	dialogue.finished.connect(on_finished)
	dialogue.line_shown.connect(on_line)
	caption = Label.new()
	caption.add_theme_font_size_override("font_size", 14)
	caption.add_theme_color_override("font_color", Color("f1dfb1"))
	caption.add_theme_color_override("font_shadow_color", Color("141519"))
	caption.add_theme_constant_override("shadow_offset_x", 1)
	caption.add_theme_constant_override("shadow_offset_y", 2)
	caption.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(caption)
	add_child(load("res://scenes/Story/DemoNavigation.gd").new())
	dialogue.idle("早餐前" if room_kind == "breakfast" else "公共娱乐室", "R 开始或重演剧情     E 与人物交谈或查看家具。")

func build_room() -> void:
	var floor := layer("StoneFloor")
	paint(floor, Rect2i(2, 6, 28, 12), 0, "a2_floor_r0_c0" if room_kind == "breakfast" else "a2_floor_r3_c0")
	floor.modulate = Color("b5aa99")
	var walls := layer("Masonry")
	paint(walls, Rect2i(2, 0, 28, 6), 1, "a4_wall_side_p0_c5")
	walls.modulate = Color("b4a38b")
	var wainscot := layer("Wainscot")
	paint(wainscot, Rect2i(2, 5, 28, 2), 1, "a4_wall_side_p0_c6")
	wainscot.modulate = Color("897568")
	var rug := layer("Carpet")
	paint(rug, Rect2i(9, 10, 14, 6), 0, "a2_floor_r2_c2")
	rug.modulate = Color("ad8589")
	var caps := layer("RoomBoundary")
	for boundary in [Rect2i(1, 0, 1, 18), Rect2i(30, 0, 1, 18), Rect2i(2, 17, 13, 1), Rect2i(17, 17, 13, 1)]:
		paint(caps, boundary, 0, "a4_wall_top_p0_c5")
	for x in [160, 864]:
		decor("Window" + str(x), "B", Rect2i(3, 0, 2, 2), Vector2(x, 144))
	for x in [256, 768]:
		decor("Sconce" + str(x), "C", Rect2i(7, 5, 2, 1), Vector2(x, 156))
	# Room bounds leave a 64px opening at the south entrance; all collision uses world pixels.
	collider(Rect2(0, 0, 1024, 192))
	collider(Rect2(0, 192, 64, 384))
	collider(Rect2(960, 192, 64, 384))
	collider(Rect2(64, 544, 416, 32))
	collider(Rect2(544, 544, 416, 32))
	var entrance := make_item("Entrance", "返回中央厅", "exit", null, Vector2(512, 552))
	entrance.set_meta("reach", 38.0)
	if room_kind == "breakfast":
		add_wall_sprite("Hearth", "壁炉与灶台", "hearth", load("res://ShareAsset/Images/shot02/hearth.res"), Vector2(512, 272), Rect2(352, 252, 320, 20))
		add_wall_sprite("Pantry", "厨房储物柜", "pantry", load("res://ShareAsset/Images/shot02/pantry.res"), Vector2(864, 304), Rect2(840, 274, 48, 48))
		var table := make_item("BreakfastTable", "长餐桌", "table", load("res://ShareAsset/Images/shot02/breakfast_table.res"), Vector2(512, 442))
		collider(Rect2(288, 366, 448, 60), table)
		for x in [336, 688]:
			add_atlas_item("Chair" + str(x), "餐椅", "chair", "B", Rect2i(11, 5, 1, 1), Vector2(x, 488), Rect2(x - 12, 472, 24, 16))
	else:
		add_atlas_item("Fireplace", "壁炉", "fireplace", "B", Rect2i(8, 0, 3, 2), Vector2(512, 278), Rect2(464, 244, 96, 28))
		add_atlas_item("LeftBookcase", "收藏柜", "bookcase", "C", Rect2i(8, 10, 2, 2), Vector2(194, 254), Rect2(162, 230, 64, 24))
		add_atlas_item("RightBookcase", "收藏柜", "bookcase", "C", Rect2i(8, 10, 2, 2), Vector2(830, 254), Rect2(798, 230, 64, 24))
		var tea_table := add_atlas_item("TeaTable", "会客桌", "table", "B", Rect2i(4, 7, 2, 1), Vector2(512, 436), Rect2(480, 420, 64, 16))
		var service := Sprite2D.new()
		service.name = "TeaService"
		service.texture = atlas_texture("C", Rect2i(3, 3, 2, 1))
		service.position.y = -27
		tea_table.add_child(service)
		add_atlas_item("Sofa", "会客沙发", "chair", "B", Rect2i(9, 4, 3, 1), Vector2(512, 326), Rect2(464, 312, 96, 14))
		add_atlas_item("LeftChair", "扶手椅", "chair", "B", Rect2i(11, 5, 1, 1), Vector2(374, 448), Rect2(362, 432, 24, 16))
		add_atlas_item("RightChair", "扶手椅", "chair", "B", Rect2i(11, 5, 1, 1), Vector2(650, 448), Rect2(638, 432, 24, 16))
		add_atlas_item("Piano", "钢琴", "piano", "B", Rect2i(8, 7, 3, 2), Vector2(208, 484), Rect2(160, 456, 96, 28))
		decor("MantelPicture", "C", Rect2i(12, 8, 2, 1), Vector2(512, 182))

func decor(label: String, sheet: String, region: Rect2i, feet: Vector2) -> void:
	var sprite := Sprite2D.new()
	sprite.name = label
	sprite.texture = atlas_texture(sheet, region)
	sprite.position = feet
	sprite.offset.y = -region.size.y * 16.0
	decoration.add_child(sprite)

func layer(label: String) -> TileMapLayer:
	var result := TileMapLayer.new()
	result.name = label
	result.tile_set = load(TILESET_PATH).duplicate()
	decoration.add_child(result)
	return result

func paint(target: TileMapLayer, rect: Rect2i, set_index: int, terrain_name: String) -> void:
	var cells: Array[Vector2i] = []
	for y in range(rect.position.y, rect.end.y):
		for x in range(rect.position.x, rect.end.x):
			cells.append(Vector2i(x, y))
	for terrain in target.tile_set.get_terrains_count(set_index):
		if target.tile_set.get_terrain_name(set_index, terrain) == terrain_name:
			target.set_cells_terrain_connect(cells, set_index, terrain, false)
			return
	push_error("Missing Victorian terrain: " + terrain_name)

func atlas_texture(sheet: String, region: Rect2i) -> Texture2D:
	var tileset: TileSet = load(TILESET_PATH)
	for index in tileset.get_source_count():
		var source := tileset.get_source(tileset.get_source_id(index)) as TileSetAtlasSource
		if source == null:
			continue
		var filename: String = source.get_meta("source_filename", "")
		if filename == "" and source.texture:
			filename = source.texture.resource_path.get_file().get_basename()
		if filename == "Victorian_Inside_" + sheet:
			var atlas := AtlasTexture.new()
			atlas.atlas = source.texture
			atlas.region = Rect2(region.position * 32, region.size * 32)
			return atlas
	push_error("Missing Victorian furniture atlas: " + sheet)
	return null

func add_atlas_item(label: String, title: String, entry: String, sheet: String, region: Rect2i, feet: Vector2, block: Rect2) -> Sprite2D:
	var item := make_item(label, title, entry, atlas_texture(sheet, region), feet)
	collider(block, item)
	return item

func add_wall_sprite(label: String, title: String, entry: String, texture: Texture2D, feet: Vector2, block: Rect2) -> Sprite2D:
	var item := make_item(label, title, entry, texture, feet)
	collider(block, item)
	return item

func make_item(label: String, title: String, entry: String, texture: Texture2D, feet: Vector2) -> Sprite2D:
	var item := Item.new()
	item.name = label
	item.set("DisplayName", title)
	item.set("DialogueTitle", entry)
	item.texture = texture
	item.position = feet
	if texture:
		item.offset.y = -texture.get_height() / 2.0
	item.set_meta("reach", 68.0)
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

func make_actor(label: String, at: Vector2, display: String, path: String) -> CharacterBody2D:
	var actor := Actor.new()
	actor.name = label
	actor.position = at
	actor.standard = load(path)
	actor.set_meta("display_name", display)
	actor.set_meta("start_position", at)
	actors.add_child(actor)
	return actor

func _physics_process(_delta: float) -> void:
	if dialogue == null or dialogue.active or dialogue.history_panel.visible:
		return
	var direction := Vector2(
		float(Input.is_physical_key_pressed(KEY_D) or Input.is_physical_key_pressed(KEY_RIGHT)) - float(Input.is_physical_key_pressed(KEY_A) or Input.is_physical_key_pressed(KEY_LEFT)),
		float(Input.is_physical_key_pressed(KEY_S) or Input.is_physical_key_pressed(KEY_DOWN)) - float(Input.is_physical_key_pressed(KEY_W) or Input.is_physical_key_pressed(KEY_UP)))
	player.move_input(direction.normalized())
	if player.position.y > 563:
		get_tree().change_scene_to_file("res://scenes/Story/CentralHall.tscn")
		return
	nearest = null
	var best := 72.0
	var candidates: Array[Node2D] = []
	for npc in cast:
		candidates.append(npc)
	candidates.append_array(items.get_children())
	for candidate in candidates:
		if candidate == actors:
			continue
		if not candidate.has_meta("reach") and not candidate.has_meta("display_name"):
			continue
		var dist: float = player.position.distance_to(candidate.position)
		if dist < best and dist < float(candidate.get_meta("reach", 72.0)):
			best = dist
			nearest = candidate
	caption.visible = nearest != null
	if nearest:
		var label := str(nearest.get_meta("display_name")) if nearest.has_meta("display_name") else str(nearest.get("DisplayName"))
		caption.text = "E  ·  " + label
		caption.position = nearest.position + Vector2(-40, 10)
	queue_redraw()

func _unhandled_input(event: InputEvent) -> void:
	if not event is InputEventKey or not event.pressed or event.echo:
		return
	if event.keycode == KEY_F3:
		debug_geometry = not debug_geometry
		queue_redraw()
	if dialogue.active or dialogue.history_panel.visible:
		return
	if event.keycode == KEY_R:
		reset_scene()
		begin("opening")
	elif event.keycode in [KEY_E, KEY_ENTER] and nearest:
		if nearest is CharacterBody2D:
			begin("person_" + str(nearest.get_meta("display_name")))
		else:
			if str(nearest.get("DialogueTitle")) == "exit":
				get_tree().change_scene_to_file("res://scenes/Story/CentralHall.tscn")
			else:
				begin(str(nearest.get("DialogueTitle")))

func reset_scene() -> void:
	player.position = Vector2(512, 520)
	for index in cast.size():
		cast[index].position = cast[index].get_meta("start_position")
		rest_pose(cast[index])
	player.pose("idle", "back")

func begin(title: String) -> void:
	caption.hide()
	player.pose("idle")
	dialogue.start(story, title, self)

func on_line(character: String) -> void:
	player.pose("talk" if character == "侦探" else "idle")
	for npc in cast:
		if npc.get_meta("seated", false):
			rest_pose(npc)
		else:
			npc.pose("talk" if character == str(npc.get_meta("display_name")) else "idle")

func rest_pose(npc: CharacterBody2D) -> void:
	if npc.get_meta("seated", false):
		npc.pose("sit", "back")
		npc.sprite.pause()
		npc.sprite.frame = npc.standard.get_frame_count("sit_back") - 1
	else:
		npc.pose("idle")

func on_finished() -> void:
	player.pose("idle")
	for npc in cast:
		rest_pose(npc)
	dialogue.idle("早餐前" if room_kind == "breakfast" else "公共娱乐室", "剧情告一段落。仍可走动、查看房间物件，按 R 重演。")

func _draw() -> void:
	if not debug_geometry:
		return
	for x in range(0, 1025, 32):
		draw_line(Vector2(x, 192), Vector2(x, 576), Color(0.5, 0.8, 0.8, 0.2))
	for y in range(192, 577, 32):
		draw_line(Vector2(0, y), Vector2(1024, y), Color(0.5, 0.8, 0.8, 0.2))
	for rect in collision_rects:
		draw_rect(rect, Color(1, 0.5, 0.3, 0.7), false)
	for actor in [player] + cast:
		draw_rect(Rect2(actor.position + Vector2(-32, -55), Vector2(64, 64)), Color(0.7, 0.7, 1), false)
		draw_rect(Rect2(actor.position + Vector2(-8, -12), Vector2(16, 12)), Color(0.5, 1, 0.5), false)
		draw_circle(actor.position, 2, Color.YELLOW)



