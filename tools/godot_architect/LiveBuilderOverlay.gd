extends CanvasLayer
## F4 candidate builder. Nothing is published to the campaign until explicitly saved.

const WALL = preload("res://scenes/components/architecture/SmartWall3D.gd")
const STAIR = preload("res://scenes/components/architecture/SmartStair3D.gd")
const RAILING = preload("res://scenes/components/architecture/SmartRailing3D.gd")
const CATEGORIES := ["furniture", "doors_windows", "interactive_props", "lighting_decor"]

@onready var panel: PanelContainer = $RootPanel
@onready var prefab_list: ItemList = $RootPanel/VBox/PrefabDrawer/ItemList
@onready var category_options: OptionButton = $RootPanel/VBox/ToolBar/CategoryOption
@onready var status_label: Label = $RootPanel/VBox/StatusBar/StatusLabel

var is_active := false
var current_mode := "select"
var selected_prefab_path := ""
var ghost_preview: Node3D
var _first_point := Vector3.ZERO
var _anchored := false

func _ready() -> void:
	panel.hide()
	$RootPanel/VBox/ToolBar/CloseBtn.pressed.connect(toggle_builder)
	$RootPanel/VBox/ToolBar/WallBtn.pressed.connect(_set_mode.bind("wall"))
	$RootPanel/VBox/ToolBar/RailingBtn.pressed.connect(_set_mode.bind("railing"))
	$RootPanel/VBox/ToolBar/StairBtn.pressed.connect(_set_mode.bind("stair"))
	for category in CATEGORIES: category_options.add_item(category.capitalize())
	category_options.item_selected.connect(func(index: int) -> void: _load_category(CATEGORIES[index]))
	prefab_list.item_selected.connect(_on_prefab_selected)
	_load_category(CATEGORIES[0])

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo:
		if event.keycode == KEY_F4:
			toggle_builder()
			get_viewport().set_input_as_handled()
		elif is_active and event.keycode == KEY_R and is_instance_valid(ghost_preview):
			ghost_preview.rotation_degrees.y = fposmod(ghost_preview.rotation_degrees.y + 90.0, 360.0)
			get_viewport().set_input_as_handled()
	elif is_active and event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		if not panel.get_global_rect().has_point(event.position):
			var point := _world_point(event.position)
			if point.is_finite():
				_place_at(point)
				get_viewport().set_input_as_handled()

func toggle_builder() -> void:
	var main := get_tree().current_scene
	if not is_active and (main == null or str(main.get("current_mode")) != "field"):
		return
	is_active = not is_active
	panel.visible = is_active
	_anchored = false
	if not is_active:
		_cleanup_ghost()
	elif current_mode == "prefab" and not selected_prefab_path.is_empty():
		_create_ghost(selected_prefab_path)
	status_label.text = "Select a tool; placed objects are unpublished candidates." if is_active else "Builder closed"

func _set_mode(mode: String) -> void:
	current_mode = mode
	_anchored = false
	_cleanup_ghost()
	status_label.text = "Click two floor points for " + mode + " (stairs rise 1.2m)."

func _load_category(category: String) -> void:
	prefab_list.clear()
	selected_prefab_path = ""
	_cleanup_ghost()
	var directory := DirAccess.open("res://scenes/prefabs/" + category)
	if directory == null: return
	directory.list_dir_begin()
	var name := directory.get_next()
	while not name.is_empty():
		if name.ends_with(".tscn"):
			var index := prefab_list.add_item(name.trim_suffix(".tscn"))
			prefab_list.set_item_metadata(index, "res://scenes/prefabs/" + category + "/" + name)
		name = directory.get_next()
	directory.list_dir_end()

func _on_prefab_selected(index: int) -> void:
	selected_prefab_path = str(prefab_list.get_item_metadata(index))
	current_mode = "prefab"
	_anchored = false
	_create_ghost(selected_prefab_path)
	status_label.text = "Click the room floor to place a candidate; R rotates." 

func _create_ghost(path: String) -> void:
	_cleanup_ghost()
	if not ResourceLoader.exists(path): return
	var packed := load(path) as PackedScene
	if packed == null: return
	ghost_preview = packed.instantiate() as Node3D
	if ghost_preview == null: return
	ghost_preview.name = "BuilderGhost"
	_disable_ghost_collision(ghost_preview)
	ghost_preview.process_mode = Node.PROCESS_MODE_DISABLED
	get_tree().current_scene.add_child(ghost_preview)

func _disable_ghost_collision(node: Node) -> void:
	if node is CollisionObject3D:
		node.collision_layer = 0
		node.collision_mask = 0
	for child in node.get_children(): _disable_ghost_collision(child)

func _cleanup_ghost() -> void:
	if is_instance_valid(ghost_preview): ghost_preview.queue_free()
	ghost_preview = null

func _process(_delta: float) -> void:
	if is_active and is_instance_valid(ghost_preview):
		var point := _world_point(get_viewport().get_mouse_position())
		if point.is_finite(): ghost_preview.global_position = point

func _world_point(mouse: Vector2) -> Vector3:
	var camera := get_viewport().get_camera_3d()
	if camera == null: return Vector3(INF, INF, INF)
	var origin := camera.project_ray_origin(mouse)
	var ray := camera.project_ray_normal(mouse)
	var query := PhysicsRayQueryParameters3D.create(origin, origin + ray * 500.0)
	var hit: Dictionary = get_viewport().get_world_3d().direct_space_state.intersect_ray(query)
	if not hit.is_empty():
		var normal: Vector3 = hit.get("normal", Vector3.ZERO)
		if normal.y > 0.65: return (hit.position as Vector3).snapped(Vector3(0.25, 0.01, 0.25))
	var plane := Plane(Vector3.UP, 0.0)
	var position: Variant = plane.intersects_ray(origin, ray)
	return (position as Vector3).snapped(Vector3(0.25, 0.01, 0.25)) if position != null else Vector3(INF, INF, INF)

func _candidate_root() -> Node3D:
	var main := get_tree().current_scene
	var root := main.get_node_or_null("BuilderCandidates") as Node3D
	if root == null:
		root = Node3D.new()
		root.name = "BuilderCandidates"
		main.add_child(root)
	return root

func _place_at(point: Vector3) -> void:
	if current_mode == "prefab":
		if not ResourceLoader.exists(selected_prefab_path): return
		var packed := load(selected_prefab_path) as PackedScene
		if packed == null: return
		var instance := packed.instantiate() as Node3D
		if instance == null: return
		instance.rotation_degrees.y = ghost_preview.rotation_degrees.y if is_instance_valid(ghost_preview) else 0.0
		_candidate_root().add_child(instance)
		instance.global_position = point
		instance.set_meta("building_candidate", true)
		status_label.text = "Candidate placed. Publish through the editor to keep it."
		return
	if current_mode not in ["wall", "railing", "stair"]: return
	if not _anchored:
		_first_point = point
		_anchored = true
		status_label.text = "Choose the second point."
		return
	_anchored = false
	if Vector2(point.x - _first_point.x, point.z - _first_point.z).length() < 0.25:
		status_label.text = "Points must be at least 0.25m apart."
		return
	var object: Node3D
	match current_mode:
		"wall":
			object = WALL.new()
			object.set("start_point", _first_point)
			object.set("end_point", point)
		"stair":
			object = STAIR.new()
			object.set("start_point", _first_point)
			object.set("end_point", point + Vector3.UP * 1.2)
		"railing":
			object = RAILING.new()
			var curve := Curve3D.new()
			curve.add_point(_first_point)
			curve.add_point(point)
			object.set("curve", curve)
	object.name = "Candidate_" + current_mode.capitalize()
	object.set_meta("building_candidate", true)
	_candidate_root().add_child(object)
	status_label.text = "Candidate " + current_mode + " placed. Click two more points to continue."

func _exit_tree() -> void:
	_cleanup_ghost()
