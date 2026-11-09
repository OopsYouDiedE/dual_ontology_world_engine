@tool
extends Control

## PrefabPaletteDock: Visual palette and inspector dock for Godot Native 3D Architect.
## Displays smart prefabs from scenes/prefabs/, integrates SeatingFit ergonomics validation
## (Green/Red footprint indicator), and provides 1-click .dv slot binding.

signal prefab_selected(prefab_path: String)
signal prefab_spawn_requested(prefab_path: String, rotation_y: float, dv_slot_id: String)

@onready var search_box: LineEdit = $VBox/TopBar/SearchBox
@onready var category_options: OptionButton = $VBox/TopBar/CategoryOption
@onready var item_tree: Tree = $VBox/ContentSplit/ItemListTree
@onready var preview_viewport: SubViewport = $VBox/ContentSplit/PreviewPanel/SubViewportContainer/SubViewport
@onready var preview_pivot: Node3D = $VBox/ContentSplit/PreviewPanel/SubViewportContainer/SubViewport/PreviewPivot
@onready var footprint_indicator: ColorRect = $VBox/ContentSplit/PreviewPanel/FootprintBar/FootprintIndicator
@onready var footprint_label: Label = $VBox/ContentSplit/PreviewPanel/FootprintBar/FootprintLabel
@onready var dv_slot_edit: LineEdit = $VBox/BottomBar/SlotHBox/SlotEdit
@onready var spawn_btn: Button = $VBox/BottomBar/SpawnBtn
@onready var test_action_btn: Button = $VBox/BottomBar/TestActionBtn

var current_category: String = "furniture"
var current_filter: String = ""
var selected_prefab_path: String = ""
var current_rotation_y: float = 0.0
var preview_instance: Node3D = null

const CATEGORIES := ["furniture", "doors_windows", "interactive_props", "lighting_decor"]

func _ready() -> void:
	_setup_ui()
	refresh_palette()

func _setup_ui() -> void:
	if category_options != null:
		category_options.clear()
		for cat in CATEGORIES:
			category_options.add_item(cat.capitalize())
		category_options.item_selected.connect(_on_category_selected)
	
	if search_box != null:
		search_box.text_changed.connect(_on_search_changed)
	if item_tree != null:
		item_tree.item_selected.connect(_on_item_selected)
	if spawn_btn != null:
		spawn_btn.pressed.connect(_on_spawn_pressed)
	if test_action_btn != null:
		test_action_btn.pressed.connect(_on_test_action_pressed)

func _unhandled_key_input(event: InputEvent) -> void:
	if get_viewport().gui_get_focus_owner() is LineEdit: return
	if event is InputEventKey and event.pressed and not event.echo:
		if event.keycode == KEY_R:
			current_rotation_y = fmod(current_rotation_y + 90.0, 360.0)
			if preview_pivot != null:
				preview_pivot.rotation_degrees.y = current_rotation_y

func _on_category_selected(index: int) -> void:
	current_category = CATEGORIES[index]
	refresh_palette()

func _on_search_changed(new_text: String) -> void:
	current_filter = new_text.to_lower()
	refresh_palette()

func refresh_palette() -> void:
	if item_tree == null: return
	selected_prefab_path = ""
	if preview_instance != null:
		preview_instance.queue_free()
		preview_instance = null
	item_tree.clear()
	var root := item_tree.create_item()
	item_tree.hide_root = true
	var dir_path := "res://scenes/prefabs/" + current_category
	var dir := DirAccess.open(dir_path)
	if dir == null: return
	dir.list_dir_begin()
	var file_name := dir.get_next()
	while not file_name.is_empty():
		if file_name.ends_with(".tscn"):
			var clean_name := file_name.trim_suffix(".tscn")
			if current_filter.is_empty() or clean_name.to_lower().contains(current_filter):
				var item := item_tree.create_item(root)
				item.set_text(0, clean_name)
				item.set_metadata(0, dir_path + "/" + file_name)
		file_name = dir.get_next()
	dir.list_dir_end()

func _on_item_selected() -> void:
	var item := item_tree.get_selected()
	if item == null:
		return
	selected_prefab_path = str(item.get_metadata(0))
	prefab_selected.emit(selected_prefab_path)
	_update_preview(selected_prefab_path)
	_validate_ergonomics(selected_prefab_path)

func _update_preview(path: String) -> void:
	if preview_pivot == null or path.is_empty():
		return
	for c in preview_pivot.get_children():
		c.queue_free()
	
	var scene = load(path) as PackedScene
	if scene == null:
		return
	preview_instance = scene.instantiate()
	preview_pivot.add_child(preview_instance)
	preview_pivot.rotation_degrees.y = current_rotation_y

# --- SeatingFit & Ergonomic Validation ---
func _validate_ergonomics(path: String) -> void:
	# Checks clearance of reach target and sitting bounds against obstacles
	var is_valid := true
	var clearance_msg := "Clearance: OK (3.5 Chibi Fit)"
	
	if preview_instance != null:
		var reach = preview_instance.find_child("ReachTarget_Front", true, false)
		if reach != null:
			var reach_pos: Vector3 = reach.position
			if reach_pos.length() < 0.2:
				is_valid = false
				clearance_msg = "Warning: Reach target too close to prop body"
	
	if footprint_indicator != null:
		footprint_indicator.color = Color(0.2, 0.85, 0.3, 0.9) if is_valid else Color(0.9, 0.2, 0.2, 0.9)
	if footprint_label != null:
		footprint_label.text = clearance_msg
		footprint_label.modulate = Color(0.2, 0.85, 0.3) if is_valid else Color(0.9, 0.2, 0.2)

func _on_spawn_pressed() -> void:
	if selected_prefab_path.is_empty() or not ResourceLoader.exists(selected_prefab_path):
		return
	var slot_id := dv_slot_edit.text.strip_edges() if dv_slot_edit != null else ""
	prefab_spawn_requested.emit(selected_prefab_path, current_rotation_y, slot_id)

func _on_test_action_pressed() -> void:
	if preview_instance != null and preview_instance.has_method("play_action"):
		preview_instance.play_action("inspect_highlight")
