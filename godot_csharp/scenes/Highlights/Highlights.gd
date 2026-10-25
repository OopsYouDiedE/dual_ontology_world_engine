extends Control
## Six local excerpts assembled from reusable terrain and prop resources.
## This scene has no GameEngine, DvSession, island AI or authoritative effects.
const ACTOR = preload("res://scenes/Highlights/HighlightActor.gd")
const EFFECT = preload("res://scenes/Highlights/HighlightEffects.gd")
const STRUCTURE = preload("res://scenes/Highlights/StageStructure.gd")
const AUTHORED_SURFACE = preload("res://scenes/Shared/Field/AuthoredSurface.gd")
const GALLERY_ORNAMENT = preload("res://scenes/Highlights/GalleryFloorOrnament.gd")
const PAINTERLY = preload("res://scenes/Highlights/PainterlyStage.gd")
const TILESET = preload("res://ShareAsset/Internal/Props/Highlights/Terrain/HighlightsTerrain.tres")
const ART := "res://ShareAsset/Internal/Props/Highlights/"
const TITLES := ["疯骑士", "人在史在", "十九路棋", "最后馈赠", "热饭", "最后抢运"]

var stages: Array = []
var stage_index := 0
var line_index := -1
var viewport: SubViewport
var world: Node2D
var sorted_world: Node2D
var floor_layer: TileMapLayer
var wall_layer: TileMapLayer
var water_layer: TileMapLayer
var actors: Dictionary = {}
var props: Dictionary = {}
var effects: Dictionary = {}
var title_label: Label
var condition_label: Label
var speaker_label: Label
var prose: RichTextLabel
var progress_label: Label
var state_label: Label
var next_button: Button
var stage_buttons: Array[Button] = []
var cargo: Dictionary = {}
var received: Array[String] = []
var tide_level := 0
var tide_barrier: StaticBody2D
var stage_camera: Camera2D
var focus_button: Button
var cue_busy := false
var cue_failed := false
var stage_epoch := 0
var painterly: Node2D
var material_scheme := 1
var material_button: Button
var dining_button: Button
var action_phase := "idle"

func _ready() -> void:
	get_tree().set_meta("highlight_launch_consumed", true)
	theme = preload("res://scenes/UI/Styles/HighlightsTheme.tres")
	var payload = JSON.parse_string(FileAccess.get_file_as_string("res://Contracts/highlight_stages.json"))
	if not payload is Dictionary or not payload.has("stages"):
		push_error("Highlight content is missing or malformed")
		return
	stages = payload["stages"]
	_build_ui()
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--highlight="):
			var requested := argument.trim_prefix("--highlight=")
			for index in range(stages.size()):
				if str(stages[index]["id"]) == requested:
					stage_index = index
	_select_stage(stage_index)

func _build_ui() -> void:
	var backdrop := ColorRect.new()
	backdrop.color = Color("253335")
	backdrop.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(backdrop)
	var stack := VBoxContainer.new()
	stack.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(stack)
	var header := PanelContainer.new()
	stack.add_child(header)
	var head := VBoxContainer.new()
	header.add_child(head)
	var disclaimer := Label.new()
	disclaimer.text = "高光舞台 · 条件节选 / 在场人物演出 · 不提交正式会话"
	disclaimer.theme_type_variation = &"StageMeta"
	head.add_child(disclaimer)
	var navigation := HBoxContainer.new()
	head.add_child(navigation)
	for index in range(stages.size()):
		var button := Button.new()
		button.text = "%d %s" % [index + 1, TITLES[index]]
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		button.pressed.connect(_select_stage.bind(index))
		navigation.add_child(button)
		stage_buttons.append(button)
	title_label = Label.new()
	title_label.theme_type_variation = &"StageTitle"
	head.add_child(title_label)
	condition_label = Label.new()
	condition_label.theme_type_variation = &"StageMeta"
	condition_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	head.add_child(condition_label)
	viewport = SubViewport.new()
	viewport.size = Vector2i(512, 384)
	viewport.disable_3d = true
	viewport.transparent_bg = false
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	viewport.canvas_item_default_texture_filter = Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_FILTER_NEAREST
	add_child(viewport)
	var picture := TextureRect.new()
	picture.texture = viewport.get_texture()
	picture.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	picture.size_flags_vertical = Control.SIZE_EXPAND_FILL
	picture.custom_minimum_size = Vector2(0, 360)
	picture.mouse_filter = Control.MOUSE_FILTER_IGNORE
	stack.add_child(picture)
	var reading := PanelContainer.new()
	reading.custom_minimum_size.y = 226
	stack.add_child(reading)
	var text_stack := VBoxContainer.new()
	reading.add_child(text_stack)
	speaker_label = Label.new()
	text_stack.add_child(speaker_label)
	prose = RichTextLabel.new()
	prose.bbcode_enabled = false
	prose.fit_content = false
	prose.custom_minimum_size.y = 90
	prose.size_flags_vertical = Control.SIZE_EXPAND_FILL
	text_stack.add_child(prose)
	var controls := HBoxContainer.new()
	text_stack.add_child(controls)
	next_button = Button.new()
	next_button.text = "演出下一句  [空格]"
	next_button.pressed.connect(_advance)
	controls.add_child(next_button)
	var restart := Button.new()
	restart.text = "重置本段"
	restart.pressed.connect(func(): _select_stage(stage_index))
	controls.add_child(restart)
	focus_button = Button.new()
	focus_button.text = "棋局近看"
	focus_button.toggle_mode = true
	focus_button.toggled.connect(_focus_board)
	controls.add_child(focus_button)
	material_button = Button.new()
	material_button.pressed.connect(_cycle_material)
	controls.add_child(material_button)
	dining_button = Button.new()
	dining_button.text = "绕桌落座 [R]"
	dining_button.pressed.connect(_dining_walk)
	controls.add_child(dining_button)
	progress_label = Label.new()
	progress_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	controls.add_child(progress_label)
	var main_button := Button.new()
	main_button.text = "返回标题"
	main_button.pressed.connect(func(): get_tree().change_scene_to_file("res://scenes/Host/Main.tscn"))
	controls.add_child(main_button)
	state_label = Label.new()
	state_label.theme_type_variation = &"StageMeta"
	state_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	text_stack.add_child(state_label)

func _select_stage(index: int) -> void:
	stage_epoch += 1
	for performer in actors.values():
		if not performer.route.is_empty():
			performer.route.clear()
			performer.route_finished.emit(performer.actor_id, false)
	painterly = null
	action_phase = "idle"
	cue_busy = false
	cue_failed = false
	stage_index = index
	line_index = -1
	if is_instance_valid(world):
		viewport.remove_child(world)
		world.queue_free()
	actors.clear()
	props.clear()
	effects.clear()
	cargo.clear()
	received.clear()
	tide_level = 0
	tide_barrier = null
	world = Node2D.new()
	viewport.add_child(world)
	stage_camera = Camera2D.new()
	stage_camera.position = Vector2(256, 192)
	world.add_child(stage_camera)
	var base := Polygon2D.new()
	base.polygon = PackedVector2Array([Vector2.ZERO, Vector2(512, 0), Vector2(512, 384), Vector2(0, 384)])
	base.color = Color("333e3d")
	world.add_child(base)
	floor_layer = _tile_layer("Floor")
	wall_layer = _tile_layer("Walls")
	water_layer = _tile_layer("Water")
	sorted_world = Node2D.new()
	sorted_world.name = "FootSortedActorsAndProps"
	sorted_world.y_sort_enabled = true
	world.add_child(sorted_world)
	var id := str(stages[index]["id"])
	viewport.size = Vector2i(2048, 1024) if id == "shura_poet" else Vector2i(512, 384)
	if id == "shura_poet": stage_camera.zoom = Vector2(4, 4)
	stage_camera.position = Vector2(256, 208) if id == "shura_poet" else Vector2(256, 192)
	focus_button.visible = id == "chess_god"
	focus_button.set_pressed_no_signal(false)
	var material := 4 if id in ["scholar_relic", "demon_miracle"] else 3
	if id == "kitchen_dumplings":
		material = 2
	floor_layer.set_cells_terrain_connect(_cells(Rect2i(1, 2, 14, 10)), 0, material)
	var walls := _cells(Rect2i(0, 0, 16, 3))
	for door_cell in _cells(Rect2i(1, 0, 2, 3)):
		walls.erase(door_cell)
	walls.append_array(_cells(Rect2i(0, 3, 1, 9)))
	walls.append_array(_cells(Rect2i(15, 3, 1, 9)))
	wall_layer.set_cells_terrain_connect(walls, 1, 0 if material == 4 else 1)
	var structure := STRUCTURE.new()
	structure.transfer = id == "last_transfer"
	structure.gallery = id == "shura_poet"
	world.add_child(structure)
	world.move_child(structure, water_layer.get_index())
	structure.install_foreground(sorted_world)
	_barrier(Vector2(16, 192), Vector2(32, 384))
	_barrier(Vector2(496, 192), Vector2(32, 384))
	_barrier(Vector2(16, 72), Vector2(32, 48))
	_barrier(Vector2(296, 72), Vector2(400, 48))
	_barrier(Vector2(64, -8), Vector2(64, 16))
	_barrier(Vector2(256, 376), Vector2(512, 16))
	_spawn_prop("door", Vector2(64, 96), "entry", false)
	_spawn_prop("window", Vector2(432, 88), "window", false)
	match id:
		"shura_poet": _poet_stage()
		"scholar_relic": _scholar_stage()
		"chess_god": _chess_stage()
		"demon_miracle": _miracle_stage()
		"kitchen_dumplings": _kitchen_stage()
		"last_transfer": _transfer_stage()
	var modern_stage := id in ["scholar_relic", "kitchen_dumplings"]
	material_button.visible = modern_stage
	dining_button.visible = id == "kitchen_dumplings"
	dining_button.disabled = false
	if modern_stage:
		_install_modern_stage(id, structure)
	title_label.text = str(stages[index]["title"]) + "  /  " + str(stages[index]["location"])
	condition_label.text = str(stages[index]["condition"])
	for i in range(stage_buttons.size()):
		stage_buttons[i].disabled = i == index
	next_button.disabled = false
	state_label.text = "WASD / 方向键：侦探局部走位 · 空格：下一句 · 1–6：切换舞台 · 32 px 格 / 32×48 人物"
	_advance()

func _cycle_material() -> void:
	material_scheme = (material_scheme + 1) % 3
	if is_instance_valid(painterly): painterly.set_scheme(material_scheme)
	_update_material_label()

func _update_material_label() -> void:
	material_button.text = ["A · 32px重复", "B · 原稿直接平铺", "C · 原稿镜像接缝"][material_scheme]

func _install_modern_stage(id: String, structure: Node2D) -> void:
	for child in world.get_children():
		if child is Label: child.hide()
	wall_layer.hide()
	structure.hide()
	props["entry"].hide()
	props["window"].hide()
	viewport.size = Vector2i(1024, 768)
	stage_camera.zoom = Vector2(2, 2)
	painterly = PAINTERLY.new()
	world.add_child(painterly)
	world.move_child(painterly, sorted_world.get_index())
	painterly.configure(id == "kitchen_dumplings", floor_layer)
	painterly.set_scheme(material_scheme)
	_update_material_label()
	for person in actors.values(): person.use_modern_art()
	if id == "kitchen_dumplings":
		_replace_prop("worktable", "table", Vector2(126, 73.18), Rect2(-52, -43, 104, 37))
		props["worktable"].position = Vector2(256, 252)
		props["flour_stock"].position = Vector2(116, 150)
		_modern_prop("chair", Vector2(256, 302), Vector2(18, 35.33), "DiningChair", Rect2(-9, -22, 18, 8))
	else:
		props["watercourse"].hide()
		actors["detective"].position = Vector2(208, 304)
		actors["scholar"].set_pose("idle", "front")
	state_label.text = "Astra原图裁切 · 32px物理格 · 128px材质模块 · 无正式会话"

func _modern_prop(kind: String, at: Vector2, size: Vector2, key: String, collision := Rect2()) -> Node2D:
	var node := Node2D.new()
	node.name = key
	node.position = at
	sorted_world.add_child(node)
	var sprite := Sprite2D.new()
	sprite.name = "Art"
	sprite.texture = load("res://ShareAsset/Internal/Props/ModernStage/%s.png" % kind)
	sprite.scale = size / sprite.texture.get_size()
	sprite.position.y = -size.y / 2.0
	sprite.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	node.add_child(sprite)
	if collision.has_area(): _barrier(collision.get_center(), collision.size, node)
	props[key] = node
	return node

func _replace_prop(key: String, kind: String, size: Vector2, collision: Rect2) -> void:
	var node: Node2D = props[key]
	for child in node.get_children():
		if child is StaticBody2D:
			node.remove_child(child)
			child.queue_free()
	var art: Sprite2D = node.get_node("Art")
	art.texture = load("res://ShareAsset/Internal/Props/ModernStage/%s.png" % kind)
	art.centered = true
	art.scale = size / art.texture.get_size()
	art.position = Vector2(0, -size.y / 2.0)
	art.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	_barrier(collision.get_center(), collision.size, node)

func _dining_walk() -> void:
	if cue_busy or not dining_button.visible: return
	var epoch := stage_epoch
	cue_busy = true
	dining_button.disabled = true
	next_button.disabled = true
	action_phase = "walking"
	var path: Array[Vector2] = [Vector2(160, 312), Vector2(160, 156), Vector2(352, 156), Vector2(352, 312), Vector2(256, 312), Vector2(256, 300)]
	if await _walk_and_wait("detective", path) and epoch == stage_epoch:
		action_phase = "seated"
		actors["detective"].sit()
		state_label.text = "完整绕桌后实际到达座位 · 已切换背向坐姿 · 重置本段可重新演出"
	if epoch == stage_epoch:
		cue_busy = false
		next_button.disabled = cue_failed

func _focus_board(focused: bool) -> void:
	if is_instance_valid(stage_camera):
		stage_camera.zoom = Vector2.ONE * (3.0 if focused else 1.0)
		stage_camera.position = Vector2(256, 208) if focused else Vector2(256, 192)

func _tile_layer(layer_name: String) -> TileMapLayer:
	var layer := TileMapLayer.new()
	layer.name = layer_name
	layer.tile_set = TILESET
	layer.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	world.add_child(layer)
	return layer

func _cells(rectangle: Rect2i) -> Array[Vector2i]:
	var result: Array[Vector2i] = []
	for y in range(rectangle.position.y, rectangle.end.y):
		for x in range(rectangle.position.x, rectangle.end.x):
			result.append(Vector2i(x, y))
	return result

func _barrier(at: Vector2, size: Vector2, parent: Node = null) -> StaticBody2D:
	var body := StaticBody2D.new()
	body.position = at
	body.collision_layer = 1
	body.collision_mask = 2
	var shape := CollisionShape2D.new()
	var rectangle := RectangleShape2D.new()
	rectangle.size = size
	shape.shape = rectangle
	body.add_child(shape)
	(parent if parent != null else world).add_child(body)
	return body

func _spawn_prop(kind: String, at: Vector2, id: String = "", blocking: bool = true) -> Node2D:
	var families := {"door": "Architecture", "window": "Architecture", "arch": "Architecture", "stele": "Relics",
		"dumpling_table": "Kitchen", "stove": "Kitchen", "grain_crates": "Logistics", "seedlings": "Logistics",
		"go_table": "Furniture", "screen": "Furniture", "pump": "Mechanisms", "seal_anchor": "Mechanisms", "sword": "Handheld"}
	var node := Node2D.new()
	node.position = at
	node.name = id if not id.is_empty() else kind
	var sprite := Sprite2D.new()
	sprite.name = "Art"
	if kind == "sack":
		sprite.texture = load("res://ShareAsset/Internal/Props/FirstAwakeningInterior/sack.png")
	else:
		sprite.texture = load(ART + str(families[kind]) + "/" + kind + ".tres")
	sprite.centered = false
	sprite.position = Vector2(-sprite.texture.get_width() / 2.0, -sprite.texture.get_height())
	sprite.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	var shadow := Polygon2D.new()
	shadow.name = "ContactShadow"
	var outline := PackedVector2Array()
	for step in range(16):
		var angle := TAU * step / 16.0
		outline.append(Vector2(cos(angle) * sprite.texture.get_width() * 0.35, sin(angle) * 3.0 - 2.0))
	shadow.polygon = outline
	shadow.color = Color(0.12, 0.15, 0.14, 0.35)
	node.add_child(shadow)
	node.add_child(sprite)
	sorted_world.add_child(node)
	if blocking:
		_barrier(Vector2(0, -10), Vector2(sprite.texture.get_width() * 0.75, 18), node)
	props[node.name] = node
	return node

func _actor(id: String, at: Vector2, person: String, player: bool = false, ghost: bool = false) -> CharacterBody2D:
	var actor := ACTOR.new()
	actor.actor_id = id
	actor.display_name = person
	actor.player_controlled = player
	actor.translucent = ghost
	actor.position = at
	sorted_world.add_child(actor)
	actors[id] = actor
	return actor

func _effect(kind: String, at: Vector2, parent: Node = null) -> Node2D:
	var effect := EFFECT.new()
	effect.kind = kind
	effect.position = at
	(parent if parent != null else sorted_world).add_child(effect)
	effects[kind] = effect
	return effect

func _poet_stage() -> void:
	floor_layer.clear()
	wall_layer.clear()
	# The gallery owns its original diamond floor and double-line border.
	# Coverage does not impose the old blob47 corner cut on these pixels.
	_authored_gallery_surface("floor", [Rect2(32, 192, 448, 96), Rect2(32, 96, 64, 96)], Vector2(140.20833333333334, 204.625))
	var ornament := GALLERY_ORNAMENT.new()
	ornament.name = "AuthoredGalleryBorderAndMedallion"
	world.add_child(ornament)
	world.move_child(ornament, sorted_world.get_index())
	props["entry"].position = Vector2(64, 192)
	props["window"].queue_free()
	props.erase("window")
	var facade := Sprite2D.new()
	facade.texture = load(ART + "Architecture/gallery_facade.png")
	facade.centered = false
	facade.position = Vector2(32, 96)
	world.add_child(facade)
	world.move_child(facade, sorted_world.get_index())
	_barrier(Vector2(16, 184), Vector2(32, 16))
	_barrier(Vector2(304, 184), Vector2(416, 16))
	_barrier(Vector2(256, 296), Vector2(512, 32))
	_actor("detective", Vector2(192, 240), "侦探", true)
	_actor("poet", Vector2(352, 240), "卢西安")
	_actor("thief", Vector2(416, 224), "雀儿")
	_label_at("桥口", Vector2(296, 254))

func _authored_gallery_surface(piece: String, coverage: Array[Rect2], origin: Vector2) -> void:
	var surface := AUTHORED_SURFACE.new()
	surface.name = "AuthoredGallery_" + piece
	world.add_child(surface)
	world.move_child(surface, sorted_world.get_index())
	surface.configure(load("res://ShareAsset/Internal/Props/AuthoredRooms/shura_gallery_" + piece + ".png"), 7.0 / 24.0, origin, coverage)

func _scholar_stage() -> void:
	_spawn_prop("stele", Vector2(272, 192), "mother_stele")
	_spawn_prop("pump", Vector2(432, 176), "watercourse")
	_actor("detective", Vector2(208, 320), "侦探", true)
	_actor("scholar", Vector2(368, 240), "顾平章")
	_label_at("海民母碑 · 引线未点燃", Vector2(176, 112))

func _chess_stage() -> void:
	_spawn_prop("screen", Vector2(160, 160), "tea_screen")
	_spawn_prop("go_table", Vector2(256, 240), "chess_table")
	_effect("go", Vector2.ZERO, props["chess_table"])
	_actor("chess_player", Vector2(184, 224), "阮清月")
	actors["chess_player"].set_pose("idle", "right")
	_actor("demon", Vector2(328, 224), "代管者")
	actors["demon"].set_pose("idle", "left")
	_actor("detective", Vector2(256, 328), "侦探", true)
	_label_at("十九路 · 黑白交替", Vector2(194, 278))

func _miracle_stage() -> void:
	for point in [Vector2(144, 168), Vector2(368, 168), Vector2(144, 304), Vector2(368, 304)]:
		_spawn_prop("seal_anchor", point, "anchor_%s_%s" % [point.x, point.y])
	_actor("demon", Vector2(256, 192), "代管者")
	_actor("detective", Vector2(256, 336), "侦探", true)
	_effect("miracle", Vector2.ZERO, world)
	_label_at("四锚闭合", Vector2(218, 112))

func _kitchen_stage() -> void:
	_spawn_prop("dumpling_table", Vector2(256, 240), "worktable")
	_spawn_prop("stove", Vector2(400, 176), "cookstove")
	_spawn_prop("grain_crates", Vector2(144, 160), "flour_stock")
	_actor("chef", Vector2(256, 184), "沈鼎")
	_actor("musician", Vector2(176, 240), "苏泠")
	_actor("thief", Vector2(336, 240), "雀儿")
	_actor("demon", Vector2(432, 68), "窗外虚影", false, true)
	actors["demon"].collision_layer = 0
	actors["demon"].collision_mask = 0
	actors["demon"].z_index = 1
	_actor("detective", Vector2(256, 328), "侦探", true)
	_effect("steam", Vector2(400, 150))
	_effect("dumplings", Vector2(0, -10), props["worktable"])
	_label_at("白面 · 猪肉白菜馅", Vector2(194, 284))

func _transfer_stage() -> void:
	_spawn_prop("grain_crates", Vector2(160, 160), "stored_grain")
	_spawn_prop("sack", Vector2(128, 264), "grain_one", false)
	_spawn_prop("sack", Vector2(224, 264), "grain_two", false)
	_spawn_prop("seedlings", Vector2(320, 264), "seedling_last", false)
	_actor("chef", Vector2(128, 304), "沈鼎")
	_actor("detective", Vector2(224, 304), "侦探", true)
	_label_at("粮仓出库", Vector2(96, 216))
	_label_at("高处接收台", Vector2(300, 84))
	_barrier(Vector2(288, 144), Vector2(8, 96))
	_barrier(Vector2(352, 192), Vector2(128, 16))
	water_layer.set_cells_terrain_connect(_cells(Rect2i(1, 0, 14, 6)), 2, 2)
	water_layer.position.y = 376
	_effect("tide_edge", Vector2.ZERO, water_layer)
	tide_barrier = _barrier(Vector2(256, 388), Vector2(448, 16))

func _label_at(text: String, at: Vector2) -> void:
	var label := Label.new()
	label.text = text
	label.position = at
	label.theme = theme
	label.theme_type_variation = &"StageMeta"
	label.scale = Vector2.ONE * 0.75
	world.add_child(label)

func _advance() -> void:
	if cue_busy or cue_failed:
		return
	var lines: Array = stages[stage_index]["lines"]
	if line_index + 1 >= lines.size():
		return
	line_index += 1
	var line: Dictionary = lines[line_index]
	speaker_label.text = str(line.get("speaker", ""))
	prose.text = str(line.get("text", ""))
	var epoch := stage_epoch
	cue_busy = true
	next_button.disabled = true
	progress_label.text = "%d / %d · 现场执行中" % [line_index + 1, lines.size()]
	await _run_cue(str(line.get("cue", "")), str(line.get("actor", "")))
	if epoch != stage_epoch:
		return
	cue_busy = false
	progress_label.text = "%d / %d · %s" % [line_index + 1, lines.size(), "现场受阻" if cue_failed else ("段落结束" if line_index + 1 == lines.size() else "动作已落定")]
	next_button.disabled = cue_failed or line_index + 1 == lines.size()

func _run_cue(cue: String, actor_id: String) -> void:
	var actor = actors.get(actor_id)
	if is_instance_valid(actor):
		actor.gesture()
	var id := str(stages[stage_index]["id"])
	if cue in ["cane_draw", "cane_raise", "cane_strike", "cane_stop"]:
		var poses := {"cane_draw": "draw", "cane_raise": "raise", "cane_strike": "strike", "cane_stop": "block"}
		action_phase = str(poses[cue])
		actor.held_pose = action_phase
		actor.set_pose(action_phase, "front")
		if action_phase == "strike":
			var sound := AudioStreamPlayer.new()
			sound.stream = load("res://ShareAsset/Internal/Props/ModernStage/cane-impact.wav")
			sound.volume_db = -10
			world.add_child(sound)
			sound.finished.connect(sound.queue_free)
			sound.play()
		await get_tree().create_timer(0.65 if action_phase != "strike" else 0.35).timeout
	match cue:
		"approach":
			if id == "shura_poet":
				await _walk_and_wait("poet", [Vector2(288 - min(line_index, 5) * 3, 240)])
			elif id == "scholar_relic":
				await _walk_and_wait("scholar", [Vector2(272, 240)])
			elif id == "demon_miracle":
				await _walk_and_wait("demon", [Vector2(256, 224)])
		"black_move", "white_move":
			effects["go"].add_stone(cue == "black_move")
			state_label.text = "棋盘实际落子：%d 手 · 正常交替；本段没有终局或胜负判定" % effects["go"].stones.size()
		"miracle":
			effects["miracle"].active = true
			state_label.text = "舞台效果：残雾凝晶 · 四锚条件已在段首说明 · 单一代管者实体"
		"dumpling":
			effects["dumplings"].dumplings += 3
			actors["chef"].gesture()
			actors["musician"].gesture()
			actors["thief"].gesture()
		"steam":
			effects["steam"].active = true
			if actor_id == "demon":
				actors["demon"].set_pose("talk", "left")
		"transfer":
			await _transfer_one(actor_id)
		"tide":
			tide_level = mini(tide_level + 1, 3)
			if tide_level == 3:
				for person in ["chef", "detective"]:
					if actors[person].position.y > 184:
						if not await _walk_and_wait(person, [Vector2(440, 280), Vector2(440, 176)]):
							return
			var edge: int = [376, 344, 312, 232][tide_level]
			var rise := create_tween()
			rise.tween_property(water_layer, "position:y", float(edge), 1.8)
			# Diagrammatic edge advance, not an asserted metre level or full-island tide model.
			tide_barrier.position.y = edge + 12
			await rise.finished
			if tide_level == 3:
				actors["detective"].player_controlled = false
			state_label.text = "源质逼近1F低沿（示意，无绝对液面） · 已实际送达：%d 件 / 当前搬运：%d 件" % [received.size(), cargo.size()]

func _walk_and_wait(actor_id: String, points: Array[Vector2]) -> bool:
	var epoch := stage_epoch
	var actor = actors[actor_id]
	var manual: bool = actor.player_controlled
	actor.player_controlled = false
	actor.walk(points)
	var result: Array = await actor.route_finished
	if epoch != stage_epoch: return false
	actor.player_controlled = manual
	var reached := bool(result[1])
	if not reached:
		cue_failed = true
		state_label.text = "%s 的现场走位受阻；已停止后续旁白。可调整布局后重置本段。" % actor.display_name
	return reached

func _transfer_one(actor_id: String) -> void:
	var item := ""
	for candidate in ["grain_one", "grain_two", "seedling_last"]:
		if candidate not in received:
			item = candidate
			break
	if item.is_empty():
		return
	var actor = actors[actor_id]
	var item_node: Node2D = props[item]
	# Approach below the cargo; the upper receiving route has an actual stair gap.
	var pickup: Array[Vector2] = []
	if actor.position.y < 224:
		pickup.append(Vector2(440, 176))
		pickup.append(Vector2(440, 296))
	else:
		pickup.append(Vector2(actor.position.x, 296))
	pickup.append(Vector2(item_node.position.x, 296))
	pickup.append(Vector2(item_node.position.x, 280))
	if not await _walk_and_wait(actor_id, pickup):
		return
	cargo[actor_id] = item
	item_node.reparent(actor, false)
	item_node.position = Vector2(0, -18)
	item_node.get_node("ContactShadow").visible = false
	var drop: Vector2 = [Vector2(320, 128), Vector2(352, 128), Vector2(320, 160)][received.size()]
	if not await _walk_and_wait(actor_id, [Vector2(440, 280), Vector2(440, 176), drop + Vector2(0, 16)]):
		return
	# A handover follows physical arrival. The original item persists on the bay.
	item_node.reparent(sorted_world, false)
	item_node.position = drop
	item_node.get_node("ContactShadow").visible = true
	received.append(item)
	cargo.erase(actor_id)
	state_label.text = "高处已接收：%d 件 · 当前物件留在接收台 · 本段不判整场抢运结果" % received.size()
	# Park the first carrier clear of the next receiving line.
	var park := Vector2(392, 128) if actor_id == "chef" else Vector2(432, 160)
	await _walk_and_wait(actor_id, [Vector2(440, 176), park])

func _input(event: InputEvent) -> void:
	if not event is InputEventKey:
		return
	if event.keycode == KEY_R and dining_button.visible:
		get_viewport().set_input_as_handled()
		if event.pressed and not event.echo: _dining_walk()
		return
	if event.keycode == KEY_SPACE or (event.keycode >= KEY_1 and event.keycode <= KEY_6):
		# Consume both edges before GUI focus can activate a toggle button.
		get_viewport().set_input_as_handled()
		if event.pressed and not event.echo:
			if event.keycode == KEY_SPACE:
				_advance()
			else:
				_select_stage(event.keycode - KEY_1)
