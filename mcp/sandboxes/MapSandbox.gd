extends Node2D
## Pure GDScript map authoring workspace. Uses the production actor and collisions.

const GEOMETRY = preload("res://scenes/Host/GeometryCatalog.gd")
const VALIDATOR = preload("res://sandboxes/GeometryContractValidator.gd")
const ACTOR = preload("res://scenes/Story/StoryActor.gd")
const FRAMES = preload("res://ShareAsset/Internal/Characters/PixelCast/detective.tres")
var contract: Dictionary
var map_key := "living_floor"
var layout: Node2D
var actor: CharacterBody2D
var camera: Camera2D
var status: Label
var markers: OptionButton
var regions_visible := true
var item: Dictionary
var lighting_enabled := true
var shadow_enabled := true
var light_root: Node2D
var ambient: CanvasModulate
var glow: GradientTexture2D

func _ready() -> void:
	contract = GEOMETRY.contract()
	var layer := CanvasLayer.new()
	layer.layer = 128
	add_child(layer)
	var panel := PanelContainer.new()
	panel.position = Vector2(12, 12)
	panel.size = Vector2(620, 110)
	layer.add_child(panel)
	var column := VBoxContainer.new()
	panel.add_child(column)
	var row := HBoxContainer.new()
	column.add_child(row)
	var maps := OptionButton.new()
	for name in contract.maps:
		maps.add_item(str(name))
		if name == map_key: maps.select(maps.item_count - 1)
	maps.item_selected.connect(func(index: int): _load_map(maps.get_item_text(index)))
	row.add_child(maps)
	markers = OptionButton.new()
	markers.item_selected.connect(_place_at_marker)
	row.add_child(markers)
	var toggle := CheckButton.new()
	toggle.text = "房间 / 门 / 场点"
	toggle.button_pressed = true
	toggle.toggled.connect(func(value: bool): regions_visible = value; queue_redraw())
	row.add_child(toggle)
	var light_toggle := CheckButton.new()
	light_toggle.text = "灯光"
	light_toggle.button_pressed = lighting_enabled
	light_toggle.toggled.connect(func(value: bool):
		lighting_enabled = value
		light_root.visible = value
		ambient.color = Color(0.47, 0.49, 0.56) if value else Color.WHITE)
	row.add_child(light_toggle)
	var shadow_toggle := CheckButton.new()
	shadow_toggle.text = "遮罩"
	shadow_toggle.button_pressed = shadow_enabled
	shadow_toggle.toggled.connect(func(value: bool):
		shadow_enabled = value
		for light in light_root.get_children():
			if light is PointLight2D: light.shadow_enabled = value)
	row.add_child(shadow_toggle)
	status = Label.new()
	column.add_child(status)
	var help := Label.new()
	help.text = "WASD / 方向键移动 · 滚轮缩放 · 下拉选择站点 · 无 Core 会话"
	column.add_child(help)
	_load_map(map_key)

func _load_map(next: String) -> void:
	set_physics_process(false)
	if is_instance_valid(layout):
		remove_child(layout)
		layout.queue_free()
	map_key = next
	item = contract.maps[map_key]
	layout = (load(str(item.scene)) as PackedScene).instantiate()
	add_child(layout)
	_apply_additions()
	_build_lighting()
	actor = ACTOR.new()
	actor.standard = FRAMES
	layout.get_node("Objects").add_child(actor)
	camera = Camera2D.new()
	camera.zoom = Vector2(1.6, 1.6)
	camera.position_smoothing_enabled = true
	camera.position_smoothing_speed = 9.0
	actor.add_child(camera)
	markers.clear()
	var mismatches: Array[String] = VALIDATOR.validate_layout(map_key, layout)
	for group: String in ["spawns", "stands", "sites", "stations"]:
		for id in item[group]:
			var marker: Dictionary = item[group][id]
			markers.add_item(group + "/" + str(id))
			markers.set_item_metadata(markers.item_count - 1, marker.point)
	for site: Dictionary in contract.additions.get(map_key, {}).get("sites", []):
		markers.add_item("sites/" + str(site.id))
		markers.set_item_metadata(markers.item_count - 1, site.point)
	if markers.item_count > 0: _place_at_marker(0)
	status.text = "%s · %d 个锚点 · %s" % [map_key, markers.item_count,
		"完整契约相符" if mismatches.is_empty() else "契约差异：" + ", ".join(mismatches)]
	GEOMETRY.bind_layout(map_key, layout)
	set_physics_process(true)
	queue_redraw()

func _apply_additions() -> void:
	var additions: Dictionary = contract.additions.get(map_key, {})
	for site: Dictionary in additions.get("sites", []):
		var marker := Marker2D.new()
		marker.name = str(site.id)
		marker.position = Vector2(site.point[0], site.point[1])
		marker.set_meta("label", str(site.get("label", site.id)))
		layout.get_node("Sites").add_child(marker)
	for prop: Dictionary in additions.get("props", []):
		var sprite := Sprite2D.new()
		sprite.name = str(prop.id)
		sprite.texture = load(str(prop.texture))
		sprite.position = Vector2(prop.position[0], prop.position[1])
		sprite.centered = false
		sprite.offset = Vector2(-sprite.texture.get_width() / 2.0, -sprite.texture.get_height())
		layout.get_node("Objects").add_child(sprite)
		if prop.has("footprint"):
			var rect: Array = prop.footprint
			var shape := RectangleShape2D.new()
			shape.size = Vector2(rect[2], rect[3])
			var solid := CollisionShape2D.new()
			solid.shape = shape
			solid.position = sprite.position + Vector2(rect[0], rect[1]) + shape.size / 2.0
			layout.get_node("Solids").add_child(solid)

func _build_lighting() -> void:
	ambient = CanvasModulate.new()
	ambient.color = Color(0.47, 0.49, 0.56) if lighting_enabled else Color.WHITE
	layout.add_child(ambient)
	light_root = Node2D.new()
	light_root.visible = lighting_enabled
	layout.add_child(light_root)
	if glow == null:
		var gradient := Gradient.new()
		gradient.offsets = PackedFloat32Array([0, 0.16, 0.48, 1])
		gradient.colors = PackedColorArray([Color.WHITE, Color(1,1,1,0.82), Color(1,1,1,0.26), Color(1,1,1,0)])
		glow = GradientTexture2D.new()
		glow.gradient = gradient
		glow.width = 256
		glow.height = 256
		glow.fill = GradientTexture2D.FILL_RADIAL
		glow.fill_from = Vector2(0.5, 0.5)
		glow.fill_to = Vector2(1, 0.5)
	for declaration: Dictionary in layout.get_meta("stage_lights", []):
		var window: bool = str(declaration.kind) == "window"
		var light := PointLight2D.new()
		light.texture = glow
		light.position = Vector2(declaration.point[0], declaration.point[1])
		light.texture_scale = 1.1 if window else 1.45
		light.color = Color(0.63, 0.79, 0.94) if window else Color(1.0, 0.81, 0.71)
		light.energy = 0.6 if window else 0.9
		light.height = 48.0
		light.shadow_enabled = shadow_enabled
		light_root.add_child(light)
	for solid in layout.get_node("Solids").get_children():
		var polygon := PackedVector2Array()
		if solid is CollisionShape2D and solid.shape is RectangleShape2D:
			var half: Vector2 = solid.shape.size * 0.5
			polygon = PackedVector2Array([Vector2(-half.x, -half.y), Vector2(half.x, -half.y), half, Vector2(-half.x, half.y)])
		elif solid is CollisionPolygon2D:
			polygon = solid.polygon
		if polygon.is_empty(): continue
		var occluder := LightOccluder2D.new()
		occluder.occluder = OccluderPolygon2D.new()
		occluder.occluder.polygon = polygon
		occluder.transform = solid.transform
		light_root.add_child(occluder)

func _place_at_marker(index: int) -> void:
	var point: Array = markers.get_item_metadata(index)
	actor.position = Vector2(float(point[0]), float(point[1]))
	camera.reset_smoothing()

func _physics_process(_delta: float) -> void:
	var direction := Input.get_vector("ui_left", "ui_right", "ui_up", "ui_down")
	if Input.is_physical_key_pressed(KEY_A): direction.x -= 1
	if Input.is_physical_key_pressed(KEY_D): direction.x += 1
	if Input.is_physical_key_pressed(KEY_W): direction.y -= 1
	if Input.is_physical_key_pressed(KEY_S): direction.y += 1
	direction = direction.limit_length()
	actor.velocity = direction * 120.0
	if direction.is_zero_approx():
		actor.pose("idle")
	else:
		actor.face_vector(direction)
		actor.pose("walk")
		actor.move_and_slide()

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed:
		if event.button_index == MOUSE_BUTTON_WHEEL_UP: camera.zoom *= 1.1
		if event.button_index == MOUSE_BUTTON_WHEEL_DOWN: camera.zoom /= 1.1
		camera.zoom = camera.zoom.clamp(Vector2(0.3, 0.3), Vector2(5, 5))

func _draw() -> void:
	if not regions_visible or item.is_empty(): return
	for room: Dictionary in item.rooms:
		for raw: Array in room.rects:
			draw_rect(Rect2(raw[0], raw[1], raw[2], raw[3]), Color(0.6, 0.8, 1.0, 0.45), false, 1.0)
	for door: Dictionary in item.doors:
		draw_circle(Vector2(door.point[0], door.point[1]), 5, Color(0.9, 0.7, 0.2))
	for site in item.sites.values():
		draw_circle(Vector2(site.point[0], site.point[1]), 3, Color(0.2, 0.9, 0.6))
	for site: Dictionary in contract.additions.get(map_key, {}).get("sites", []):
		draw_circle(Vector2(site.point[0], site.point[1]), 3, Color(0.2, 0.9, 0.6))
