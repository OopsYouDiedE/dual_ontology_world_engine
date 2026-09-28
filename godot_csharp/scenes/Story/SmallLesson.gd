extends Node2D
## A wordless four-second greeting. Only the teacher performs; students listen.

const Actor = preload("res://scenes/Story/StoryActor.gd")
const PROPS := "res://ShareAsset/Internal/Props/"
const CHARACTERS := "res://ShareAsset/Internal/Characters/"
const MAXWELL_STAND := preload("res://ShareAsset/Internal/Characters/atlas/05_demon_caretaker.tres")
const MAXWELL_HAND := preload("res://ShareAsset/Internal/Characters/maxwell_hand_chest.res")
const MAXWELL_BOW := preload("res://ShareAsset/Internal/Characters/maxwell_bow.res")

var decoration := Node2D.new()
var occupants := Node2D.new()
var maxwell: Sprite2D
var montage_playing := false

func _ready() -> void:
	get_window().title = "安眠者有福 · 初次授课"
	preload("res://scenes/Story/StoryPresentation.gd").install(self, 3, Vector2(224, 176))
	texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	add_child(decoration)
	add_child(occupants)
	occupants.y_sort_enabled = true
	build_room()
	build_people()

func layer(label: String) -> TileMapLayer:
	var map := TileMapLayer.new()
	map.name = label
	map.tile_set = load(PROPS + "ReasoningStudy/Study.tres").duplicate()
	decoration.add_child(map)
	return map

func paint(map: TileMapLayer, region: Rect2i, set_index: int, terrain_name: String) -> void:
	var cells: Array[Vector2i] = []
	for y in range(region.position.y, region.end.y):
		for x in range(region.position.x, region.end.x):
			cells.append(Vector2i(x, y))
	for terrain in map.tile_set.get_terrains_count(set_index):
		if map.tile_set.get_terrain_name(set_index, terrain) == terrain_name:
			map.set_cells_terrain_connect(cells, set_index, terrain, false)
			return
	push_error("Missing terrain " + terrain_name)

func prop(label: String, path: String, feet: Vector2, parent: Node2D = occupants) -> Sprite2D:
	var sprite := Sprite2D.new()
	sprite.name = label
	sprite.texture = load(PROPS + path)
	sprite.position = feet
	sprite.offset.y = -sprite.texture.get_height() / 2.0
	parent.add_child(sprite)
	return sprite

func build_room() -> void:
	var floor := layer("StoneFloor")
	paint(floor, Rect2i(1, 3, 12, 6), 0, "石板地面")
	floor.modulate = Color("b5afa5")
	var wall := layer("Masonry")
	paint(wall, Rect2i(1, 0, 12, 3), 1, "石砌墙面")
	wall.modulate = Color("b5ad9d")
	paint(layer("Wainscot"), Rect2i(1, 2, 12, 1), 1, "木护墙")
	paint(layer("Carpet"), Rect2i(4, 4, 7, 4), 0, "蓝金地毯")
	var quiet_rug := Polygon2D.new()
	quiet_rug.polygon = PackedVector2Array([Vector2(131,131),Vector2(349,131),Vector2(349,253),Vector2(131,253)])
	quiet_rug.color = Color(0.08, 0.12, 0.18, 0.56)
	decoration.add_child(quiet_rug)
	for region in [Rect2i(0, 0, 1, 9), Rect2i(13, 0, 1, 9), Rect2i(1, 8, 5, 1), Rect2i(8,8,5,1)]:
		paint(layer("Boundary" + str(region.position)), region, 0, "木质墙顶")
	prop("TeachingBoard", "classroom_board.res", Vector2(224, 120), decoration)
	prop("Window", "story_window.res", Vector2(96, 112), decoration)
	prop("Banner", "story_banner_1.res", Vector2(328, 108), decoration)
	prop("ReferenceShelf", "story_parlor_bookcase.res", Vector2(64, 160), decoration)
	prop("SideDoor", "story_closed_door.res", Vector2(384, 128), decoration)
	prop("WallLamp", "wall_lantern.res", Vector2(352, 104), decoration)
	prop("Plant", "story_tree.res", Vector2(384, 196), decoration)
	prop("Lectern", "classroom_lectern.res", Vector2(190, 168))
	for x in [176, 288]:
		prop("Chair" + str(x), "classroom_chair.res", Vector2(x, 266), decoration)
		prop("Desk" + str(x), "classroom_desk.res", Vector2(x, 236))
	preload("res://scenes/Story/StoryPresentation.gd").daylight(decoration, true, true)

func build_people() -> void:
	maxwell = Sprite2D.new()
	maxwell.name = "Maxwell"
	maxwell.texture = MAXWELL_STAND
	maxwell.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	maxwell.position = Vector2(266, 170)
	maxwell.offset.y = -23
	occupants.add_child(maxwell)
	for student in [["Detective", "detective.tres", 176], ["Poet", "poet.tres", 288]]:
		var actor := Actor.new()
		actor.name = student[0]
		actor.standard = load(CHARACTERS + "animation_v2/" + student[1])
		actor.position = Vector2(student[2], 258)
		occupants.add_child(actor)
		actor.sit_at("back", 0.0)

func play_montage() -> void:
	if montage_playing:
		return
	montage_playing = true
	maxwell.texture = MAXWELL_STAND
	await get_tree().create_timer(0.55).timeout
	maxwell.texture = MAXWELL_HAND
	await get_tree().create_timer(0.55).timeout
	maxwell.texture = MAXWELL_BOW
	await get_tree().create_timer(0.9).timeout
	maxwell.texture = MAXWELL_HAND
	await get_tree().create_timer(0.4).timeout
	maxwell.texture = MAXWELL_STAND
	await get_tree().create_timer(1.6).timeout
	montage_playing = false

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo and not montage_playing:
		if event.keycode == KEY_M:
			play_montage()
		elif event.keycode == KEY_ESCAPE:
			get_tree().change_scene_to_file("res://scenes/Story/CentralHall.tscn")
