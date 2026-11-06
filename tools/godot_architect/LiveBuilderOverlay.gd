extends CanvasLayer

## LiveBuilderOverlay: In-game F4 real-time building console.
## Allows designers and players to draw walls, railings, stairs, and place
## smart prefabs live in the room while controlling the chibi character!

@onready var panel: PanelContainer = $RootPanel
@onready var tool_bar: HBoxContainer = $RootPanel/VBox/ToolBar
@onready var prefab_list: ItemList = $RootPanel/VBox/PrefabDrawer/ItemList
@onready var category_options: OptionButton = $RootPanel/VBox/ToolBar/CategoryOption
@onready var status_label: Label = $RootPanel/VBox/StatusBar/StatusLabel
@onready var close_btn: Button = $RootPanel/VBox/ToolBar/CloseBtn

var is_active: bool = false
var current_mode: String = "select" # "select", "wall", "railing", "stair", "prefab"
var selected_prefab_path: String = ""
var ghost_preview: Node3D = null

const CATEGORIES := ["furniture", "doors_windows", "interactive_props", "lighting_decor"]

func _ready() -> void:
	panel.visible = false
	if close_btn != null:
		close_btn.pressed.connect(toggle_builder)
	if category_options != null:
		category_options.clear()
		for cat in CATEGORIES:
			category_options.add_item(cat.capitalize())
		category_options.item_selected.connect(_on_category_selected)
	if prefab_list != null:
		prefab_list.item_selected.connect(_on_prefab_selected)
	_load_category("furniture")

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo:
		if event.keycode == KEY_F4:
			toggle_builder()
			get_viewport().set_input_as_handled()
		elif is_active and event.keycode == KEY_R and ghost_preview != null:
			ghost_preview.rotation_degrees.y += 90.0
			get_viewport().set_input_as_handled()

func toggle_builder() -> void:
	is_active = not is_active
	panel.visible = is_active
	if not is_active:
		_cleanup_ghost()
		status_label.text = "Builder Closed."
	else:
		status_label.text = "F4 Live Builder Active. Select tool or prefab."

func _on_category_selected(idx: int) -> void:
	_load_category(CATEGORIES[idx])

func _load_category(cat: String) -> void:
	if prefab_list == null:
		return
	prefab_list.clear()
	var dir_path := "res://scenes/prefabs/" + cat
	var dir := DirAccess.open(dir_path)
	if dir == null:
		return
	dir.list_dir_begin()
	var file_name := dir.get_next()
	while not file_name.is_empty():
		if file_name.ends_with(".tscn"):
			var clean := file_name.trim_suffix(".tscn")
			var idx := prefab_list.add_item(clean)
			prefab_list.set_item_metadata(idx, dir_path + "/" + file_name)
		file_name = dir.get_next()

func _on_prefab_selected(idx: int) -> void:
	selected_prefab_path = str(prefab_list.get_item_metadata(idx))
	current_mode = "prefab"
	status_label.text = "Placing: " + prefab_list.get_item_text(idx) + " (Click in world to place, 'R' to rotate)"
	_create_ghost(selected_prefab_path)

func _create_ghost(path: String) -> void:
	_cleanup_ghost()
	var scn = load(path) as PackedScene
	if scn != null:
		ghost_preview = scn.instantiate()
		var tree := get_tree()
		if tree != null and tree.current_scene != null:
			tree.current_scene.add_child(ghost_preview)

func _cleanup_ghost() -> void:
	if ghost_preview != null and is_instance_valid(ghost_preview):
		ghost_preview.queue_free()
		ghost_preview = null

func _process(_delta: float) -> void:
	if not is_active or ghost_preview == null:
		return
	var cam := get_viewport().get_camera_3d()
	if cam == null:
		return
	var mouse_pos := get_viewport().get_mouse_position()
	var ray_o := cam.project_ray_origin(mouse_pos)
	var ray_d := cam.project_ray_normal(mouse_pos)
	var plane := Plane(Vector3.UP, 0.0)
	var hit = plane.intersects_ray(ray_o, ray_d)
	if hit != null:
		# Snap to 0.25m
		var snapped_hit := Vector3(
			snappedf(hit.x, 0.25),
			snappedf(hit.y, 0.25),
			snappedf(hit.z, 0.25)
		)
		ghost_preview.position = snapped_hit

func place_smart_wall() -> void:
	current_mode = "wall"
	status_label.text = "Mode: Click & Drag SmartWall3D"

func place_smart_railing() -> void:
	current_mode = "railing"
	status_label.text = "Mode: Click points for SmartRailing3D"

func place_smart_stair() -> void:
	current_mode = "stair"
	status_label.text = "Mode: Click & Drag SmartStair3D"
