extends Node2D

const Actor = preload("res://scenes/Story/StoryActor.gd")
const Balloon = preload("res://scenes/Story/StoryDialogue.gd")
const Item = preload("res://scenes/Story/RoomItem.cs")
const Binding = preload("res://scenes/Story/RoomBinding.cs")
const TILESET_PATH := "res://ShareAsset/External/Victorian/tilesets/Victorian.tres"

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
var conversation := false
var montage_playing := false
var breakfast_performance: Node2D

func _ready() -> void:
	get_window().title = "安眠者有福 · " + ("后勤院早餐" if room_kind == "breakfast" else "公共娱乐室围谈")
	if room_kind == "breakfast":
		preload("res://scenes/Story/StoryPresentation.gd").install(self, 3, Vector2(224, 195))
	else:
		preload("res://scenes/Story/StoryPresentation.gd").install(self, 3, Vector2(224, 208))
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
	player = make_actor("侦探", Vector2(144, 197) if room_kind == "breakfast" else Vector2(224, 280), "侦探", "res://ShareAsset/Internal/Characters/animation_v2/detective.tres")
	player.pose("idle", "front" if room_kind == "breakfast" else "back")
	if room_kind == "breakfast":
		cast = [
			make_actor("主厨", Vector2(202, 171), "主厨", "res://ShareAsset/Internal/Characters/animation_v2/chef.tres"),
			make_actor("博物学家", Vector2(248, 197), "自然学家", "res://ShareAsset/Internal/Characters/animation_v2/naturalist.tres"),
			make_actor("机械工程师", Vector2(288, 197), "工程师", "res://ShareAsset/Internal/Characters/animation_v2/engineer.tres")]
		story = load("res://scenes/Story/breakfast.dialogue")
		player.sit_at("front", 12.0)
		cast[0].set_meta("rest_facing", "front")
		cast[1].set_meta("seated", true)
		cast[1].set_meta("seated_facing", "front")
		cast[1].set_meta("seat_height", 12.0)
		cast[1].set_meta("reach", 96.0)
		cast[2].set_meta("seated", true)
		cast[2].set_meta("seated_facing", "front")
		cast[2].set_meta("seat_height", 12.0)
		rest_pose(cast[1])
		rest_pose(cast[2])
		var serving_bowl := Sprite2D.new()
		serving_bowl.name = "ServingBowl"
		serving_bowl.texture = load("res://ShareAsset/Internal/Props/story_soup_bowl.res")
		serving_bowl.position = Vector2(-13, -18)
		cast[0].add_child(serving_bowl)
		breakfast_performance = load("res://scenes/Story/BreakfastPerformance.gd").new()
		add_child(breakfast_performance)
	else:
		cast = [
			make_actor("诗人", Vector2(204, 198), "诗人", "res://ShareAsset/Internal/Characters/animation_v2/poet.tres"),
			make_actor("盗贼", Vector2(144, 244), "盗贼", "res://ShareAsset/Internal/Characters/animation_v2/thief.tres"),
			make_actor("律师", Vector2(304, 244), "律师", "res://ShareAsset/Internal/Characters/animation_v2/lawyer.tres"),
			make_actor("画家", Vector2(300, 198), "画家", "res://ShareAsset/Internal/Characters/animation_v2/painter.tres")]
		story = load("res://scenes/Story/parlor.dialogue")
		cast[3].set_meta("rest_facing", "left")
		rest_pose(cast[3])
		for index in [0, 1, 2]:
			cast[index].set_meta("seated", true)
			cast[index].set_meta("seated_facing", "front")
			cast[index].set_meta("seat_height", 20.0 if index == 0 else 0.0)
			rest_pose(cast[index])
	dialogue = Balloon.new()
	add_child(dialogue)
	dialogue.heading.text = "序幕  /  " + ("后勤院 · 早餐" if room_kind == "breakfast" else "公共娱乐室 · 围谈")
	dialogue.finished.connect(on_finished)
	dialogue.line_shown.connect(on_line)
	caption = Label.new()
	caption.add_theme_font_size_override("font_size", 8)
	caption.add_theme_color_override("font_color", Color("f1dfb1"))
	caption.add_theme_color_override("font_shadow_color", Color("141519"))
	caption.add_theme_constant_override("shadow_offset_x", 1)
	caption.add_theme_constant_override("shadow_offset_y", 2)
	caption.mouse_filter = Control.MOUSE_FILTER_IGNORE
	preload("res://scenes/Story/StoryPresentation.gd").add_prompt(self,caption)
	add_child(load("res://scenes/Story/DemoNavigation.gd").new())
	dialogue.idle("早餐前" if room_kind == "breakfast" else "公共娱乐室", "M 观看日常片段     R 播放剧情对白     E 交谈或查看家具。")

func build_room() -> void:
	var breakfast := room_kind == "breakfast"
	var floor := layer("StoneFloor")
	paint(floor, Rect2i(1,3,12,6 if breakfast else 7), 0, "石板地面")
	floor.modulate = Color("c5bba9") if breakfast else Color("b5afa5")
	var walls := layer("Masonry")
	paint(walls, Rect2i(1,0,12,3), 1, "石砌墙面")
	walls.modulate = Color("b5ad9d")
	var wainscot := layer("Wainscot")
	paint(wainscot, Rect2i(1,2,12,1), 1, "木护墙")
	var rug := layer("Carpet")
	paint(rug, Rect2i(3,5,8,3 if breakfast else 4), 0, "蓝金地毯")
	var caps := layer("RoomBoundary")
	var boundaries := [Rect2i(0,0,1,9),Rect2i(13,0,1,9),Rect2i(1,8,5,1),Rect2i(8,8,5,1)] if breakfast else [Rect2i(0,0,1,10),Rect2i(13,0,1,10),Rect2i(1,9,5,1),Rect2i(8,9,5,1)]
	for boundary in boundaries:
		paint(caps,boundary,0,"木质墙顶")
	for x in ([56] if breakfast else [48,400]):
		add_wall_sprite("Window"+str(x),"高窗","table",load("res://ShareAsset/Internal/Props/story_window.res"),Vector2(x,96),Rect2(x-12,86,24,10))
	if breakfast:
		collider(Rect2(0,0,448,96))
		collider(Rect2(0,96,32,192))
		collider(Rect2(416,96,32,192))
		collider(Rect2(32,256,160,32))
		collider(Rect2(256,256,160,32))
	else:
		collider(Rect2(0,0,448,96))
		collider(Rect2(0,96,32,224))
		collider(Rect2(416,96,32,224))
		collider(Rect2(32,288,160,32))
		collider(Rect2(256,288,160,32))
	var entrance := make_item("Entrance","返回中央厅","exit",null,Vector2(224,272 if breakfast else 304))
	entrance.set_meta("reach",30.0)
	if breakfast:
		add_wall_sprite("Hearth","壁炉与灶台","hearth",load("res://ShareAsset/Internal/Props/story_hearth.res"),Vector2(205,112),Rect2(104,96,202,16))
		add_wall_sprite("KitchenDoor","厨房木门","pantry",load("res://ShareAsset/Internal/Props/story_closed_door.res"),Vector2(380,128),Rect2(352,112,56,16))
		var prep := make_item("PrepIsland","备餐台","table",load("res://ShareAsset/Internal/Props/story_prep_island.res"),Vector2(224,153))
		prep.modulate = Color("9b9f9d")
		collider(Rect2(136,139,176,14),prep)
		var table := make_item("BreakfastTable","长餐桌","table",load("res://ShareAsset/Internal/Props/breakfast_table_clear.res"),Vector2(224,238))
		collider(Rect2(120,205,208,17),table)
		for x in [-80,24,80]:
			var bowl := Sprite2D.new()
			bowl.name = "SoupBowl" + str(x)
			bowl.texture = load("res://ShareAsset/Internal/Props/breakfast_exchange_bowl.res")
			bowl.position = Vector2(64 if x == 80 else x,-53)
			table.add_child(bowl)
		for x in [144,248,288]:
			var chair := make_item("Chair"+str(x),"餐椅","chair",load("res://ShareAsset/Internal/Props/chair.res"),Vector2(x,211))
			chair.set_meta("reach", 0.0)
			# These seats face the camera: the backrest is behind the seated body,
			# while the dining table covers the chair legs in the foreground.
			chair.reparent(decoration)
		for x in [64,384]:
			var side := make_item("SideTable"+str(x),"备餐小桌","table",load("res://ShareAsset/Internal/Props/story_side_table.res"),Vector2(x,225))
			collider(Rect2(x-28,207,56,18),side)
	else:
		add_wall_sprite("Fireplace","壁炉","fireplace",load("res://ShareAsset/Internal/Props/story_fireplace.res"),Vector2(224,128),Rect2(184,112,80,16))
		for x in [96,352]:
			add_wall_sprite("Bookcase"+str(x),"收藏柜","bookcase",load("res://ShareAsset/Internal/Props/story_parlor_bookcase.res"),Vector2(x,128),Rect2(x-20,116,40,12))
		var sofa := make_item("Sofa","会客沙发","chair",load("res://ShareAsset/Internal/Props/story_sofa.res"),Vector2(224,184))
		collider(Rect2(176,170,96,14),sofa)
		for x in [144,304]:
			var chair := make_item("LeftChair" if x == 144 else "RightChair","扶手椅","chair",load("res://ShareAsset/Internal/Props/story_armchair.res"),Vector2(x,248))
			# Backrests and cushions behind the body; no foreground arm hides the hands.
			chair.reparent(decoration)
			collider(Rect2(x-20,240,40,8),chair)
		var table := make_item("TeaTable","会客桌","table",load("res://ShareAsset/Internal/Props/story_tea_table.res"),Vector2(224,248))
		collider(Rect2(196,232,56,16),table)
		var piano := make_item("Piano","钢琴","piano",load("res://ShareAsset/Internal/Props/story_parlor_piano.res"),Vector2(80,272))
		collider(Rect2(44,258,72,14),piano)
		var desk := make_item("SideTable","阅览桌","table",load("res://ShareAsset/Internal/Props/story_side_table.res"),Vector2(368,272))
		collider(Rect2(340,254,56,18),desk)
		var books := Sprite2D.new()
		books.texture = load("res://ShareAsset/Internal/Props/books.res")
		books.position = Vector2(0,-28)
		desk.add_child(books)
	for x in [48,400]:
		var plant_y := 177 if breakfast else 176
		var plant := make_item("Plant"+str(x),"盆栽","table",load("res://ShareAsset/Internal/Props/story_tree.res"),Vector2(x,plant_y))
		collider(Rect2(x-10,plant_y-10,20,10),plant)
	preload("res://scenes/Story/StoryPresentation.gd").daylight(decoration,true,true)
	var quiet_rug := Polygon2D.new()
	var rug_bottom := 253 if breakfast else 285
	quiet_rug.polygon = PackedVector2Array([Vector2(99,163),Vector2(349,163),Vector2(349,rug_bottom),Vector2(99,rug_bottom)])
	quiet_rug.color = Color(0.08, 0.12, 0.18, 0.56)
	decoration.add_child(quiet_rug)
	decoration.move_child(quiet_rug, rug.get_index() + 1)
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
	result.tile_set = load("res://ShareAsset/Internal/Props/ReasoningStudy/Study.tres").duplicate()
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
	if dialogue == null or dialogue.active or dialogue.history_panel.visible or montage_playing:
		return
	var direction := Vector2(
		float(Input.is_physical_key_pressed(KEY_D) or Input.is_physical_key_pressed(KEY_RIGHT)) - float(Input.is_physical_key_pressed(KEY_A) or Input.is_physical_key_pressed(KEY_LEFT)),
		float(Input.is_physical_key_pressed(KEY_S) or Input.is_physical_key_pressed(KEY_DOWN)) - float(Input.is_physical_key_pressed(KEY_W) or Input.is_physical_key_pressed(KEY_UP)))
	if direction.length_squared() > 0 and breakfast_performance != null:
		breakfast_performance.release()
	if player.seated and direction.length_squared() > 0:
		player.stand_up()
	player.move_input(direction.normalized())
	if player.position.y > (279 if room_kind == "breakfast" else 311):
		get_tree().change_scene_to_file("res://scenes/Story/CentralHall.tscn")
		return
	nearest = null
	var best := 96.0 if room_kind == "breakfast" else 72.0
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
	queue_redraw()

func _unhandled_input(event: InputEvent) -> void:
	if not event is InputEventKey or not event.pressed or event.echo:
		return
	if event.keycode == KEY_F3:
		debug_geometry = not debug_geometry
		queue_redraw()
	if dialogue.active or dialogue.history_panel.visible or montage_playing:
		return
	if breakfast_performance != null and event.keycode in [KEY_R, KEY_E, KEY_ENTER, KEY_M]:
		breakfast_performance.release()
	if event.keycode == KEY_M:
		play_montage()
	elif event.keycode == KEY_R:
		reset_scene()
		begin("opening")
	elif event.keycode in [KEY_E, KEY_ENTER] and nearest:
		player.face_point(nearest.position)
		if nearest is CharacterBody2D:
			nearest.face_point(player.position)
			begin("person_" + str(nearest.get_meta("display_name")))
		else:
			if str(nearest.get("DialogueTitle")) == "exit":
				get_tree().change_scene_to_file("res://scenes/Story/CentralHall.tscn")
			else:
				begin(str(nearest.get("DialogueTitle")))

func reset_scene() -> void:
	if breakfast_performance != null:
		breakfast_performance.release()
	player.position = player.get_meta("start_position")
	for index in cast.size():
		cast[index].position = cast[index].get_meta("start_position")
		cast[index].stand_up()
		rest_pose(cast[index])
	if room_kind == "breakfast":
		player.sit_at("front", 12.0)
		var table := items.get_node("BreakfastTable")
		table.get_node("SoupBowl24").position.x = 24
		table.get_node("SoupBowl80").position = Vector2(64, -53)
		table.get_node("SoupBowl24").show()
		cast[0].get_node("ServingBowl").show()
	else:
		player.pose("idle", "back")

func play_montage() -> void:
	if montage_playing:
		return
	montage_playing = true
	reset_scene()
	caption.hide()
	dialogue.panel.hide()
	if room_kind == "breakfast":
		await play_breakfast_montage()
	else:
		await play_parlor_montage()
	montage_playing = false
	dialogue.panel.show()
	dialogue.idle("早餐厅" if room_kind == "breakfast" else "公共娱乐室", "E 查看或交谈    R 重演对白    M 重看日常片段。")

func play_breakfast_montage() -> void:
	breakfast_performance.play()
	await breakfast_performance.completed

func play_parlor_montage() -> void:
	var whole_clip := get_tree().create_timer(4.0)
	$StoryCamera.position = Vector2(224, 208)
	await get_tree().create_timer(0.35).timeout
	await cast[0].rise_from_seat(0.36)
	cast[0].pose("interact", "right")
	await get_tree().create_timer(0.6).timeout
	cast[0].sprite.pause()
	# The invitation is complete before the guest moves. Everyone else stays still.
	await cast[3].walk_to(Vector2(244, 198))
	cast[3].pose("idle", "front")
	await get_tree().create_timer(0.15).timeout
	await cast[3].sit_down("front", 20.0, 0.4)
	if whole_clip.time_left > 0:
		await whole_clip.timeout

func begin(title: String) -> void:
	caption.hide()
	conversation = title == "opening" or title.begins_with("person_")
	player.pose("idle")
	dialogue.start(story, title, self)

func on_line(character: String) -> void:
	var speaker: CharacterBody2D = player if character == "侦探" else null
	for npc in cast:
		if character == str(npc.get_meta("display_name")):
			speaker = npc
	if conversation and speaker != null and speaker != player:
		player.face_point(speaker.position)
		speaker.face_point(player.position)
	player.pose("talk" if character == "侦探" else "idle")
	for npc in cast:
		if conversation and speaker != null and speaker != npc:
			npc.face_point(speaker.position)
		if npc.get_meta("seated", false):
			rest_pose(npc)
		else:
			npc.pose("talk" if character == str(npc.get_meta("display_name")) else "idle")

func rest_pose(npc: CharacterBody2D) -> void:
	if npc.get_meta("seated", false):
		var facing: String = npc.get_meta("seated_facing", "back")
		npc.sit_at(facing, float(npc.get_meta("seat_height", 0.0)))
	else:
		npc.pose("idle", npc.get_meta("rest_facing", "front"))

func on_finished() -> void:
	if room_kind == "breakfast" and player.position == player.get_meta("start_position"):
		player.sit_at("front", 12.0)
	else:
		player.pose("idle")
	for npc in cast:
		rest_pose(npc)
	dialogue.idle("早餐前" if room_kind == "breakfast" else "公共娱乐室", "仍可走动、查看房间物件；按 M 看日常片段，按 R 重演对白。")

func _draw() -> void:
	if not debug_geometry:
		return
	var width := 448
	var floor_top := 96
	var floor_bottom := 288 if room_kind == "breakfast" else 320
	for x in range(0, width + 1, 32):
		draw_line(Vector2(x, floor_top), Vector2(x, floor_bottom), Color(0.5, 0.8, 0.8, 0.2))
	for y in range(floor_top, floor_bottom + 1, 32):
		draw_line(Vector2(0, y), Vector2(width, y), Color(0.5, 0.8, 0.8, 0.2))
	for rect in collision_rects:
		draw_rect(rect, Color(1, 0.5, 0.3, 0.7), false)
	for actor in [player] + cast:
		draw_rect(Rect2(actor.position + Vector2(-32, -55), Vector2(64, 64)), Color(0.7, 0.7, 1), false)
		draw_rect(Rect2(actor.position + Vector2(-8, -12), Vector2(16, 12)), Color(0.5, 1, 0.5), false)
		draw_circle(actor.position, 2, Color.YELLOW)

