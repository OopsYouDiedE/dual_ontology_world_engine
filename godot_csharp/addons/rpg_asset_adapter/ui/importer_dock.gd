@tool
class_name ImporterDock
extends Control

## UI Dock for the RPG Asset Adapter Godot 4 editor plugin.
## Provides drag-and-drop / directory browsing, asset scanning, inspection,
## and batch conversion into Godot 4 TileSets, Terrains, and Scenes.

const AdapterFacadeScript = preload("res://addons/rpg_asset_adapter/adapter_facade.gd")
const AssetDetectorScript = preload("res://addons/rpg_asset_adapter/detector/asset_detector.gd")

var scanned_assets: Array = []

@onready var source_dir_edit: LineEdit = %SourceDirEdit
@onready var browse_source_btn: Button = %BrowseSourceBtn
@onready var output_dir_edit: LineEdit = %OutputDirEdit
@onready var browse_output_btn: Button = %BrowseOutputBtn
@onready var theme_name_edit: LineEdit = %ThemeNameEdit
@onready var detect_mode_opt: OptionButton = %DetectModeOption
@onready var tile_size_opt: OptionButton = %TileSizeOption
@onready var apply_metadata_chk: CheckBox = %ApplyMetadataCheck
@onready var scan_btn: Button = %ScanButton
@onready var convert_btn: Button = %ConvertButton
@onready var asset_tree: Tree = %AssetTree
@onready var status_label: Label = %StatusLabel
@onready var progress_bar: ProgressBar = %ProgressBar

var _source_dialog: FileDialog
var _output_dialog: FileDialog


func _ready() -> void:
	_init_detect_mode_options()
	_init_tile_size_options()
	_init_tree_columns()
	_setup_dialogs()

	if scan_btn:
		scan_btn.pressed.connect(_on_scan_pressed)
	if convert_btn:
		convert_btn.pressed.connect(_on_convert_pressed)
		convert_btn.disabled = true
	if browse_source_btn:
		browse_source_btn.pressed.connect(_on_browse_source_pressed)
	if browse_output_btn:
		browse_output_btn.pressed.connect(_on_browse_output_pressed)


func _init_detect_mode_options() -> void:
	if detect_mode_opt == null:
		return
	detect_mode_opt.clear()
	detect_mode_opt.add_item("AUTO (Automatic)", AssetDetectorScript.DetectMode.AUTO)
	detect_mode_opt.add_item("RPG Maker Standard", AssetDetectorScript.DetectMode.RPG_MAKER)
	detect_mode_opt.add_item("KR Non-RM Square", AssetDetectorScript.DetectMode.KR_NON_RM)
	detect_mode_opt.add_item("RPG Character", AssetDetectorScript.DetectMode.RPG_CHARACTER)
	detect_mode_opt.add_item("KR Extension", AssetDetectorScript.DetectMode.KR_EXTENSION)
	detect_mode_opt.add_item("Generic Animation", AssetDetectorScript.DetectMode.GENERIC_ANIMATION)
	detect_mode_opt.add_item("Manual Override", AssetDetectorScript.DetectMode.MANUAL)


func _init_tile_size_options() -> void:
	if tile_size_opt == null:
		return
	tile_size_opt.clear()
	tile_size_opt.add_item("Auto Detect", 0)
	tile_size_opt.add_item("16 px", 16)
	tile_size_opt.add_item("32 px", 32)
	tile_size_opt.add_item("48 px (Default MZ/MV)", 48)
	tile_size_opt.add_item("64 px", 64)
	tile_size_opt.select(0)


func _init_tree_columns() -> void:
	if asset_tree == null:
		return
	asset_tree.columns = 4
	asset_tree.set_column_titles_visible(true)
	asset_tree.set_column_title(0, "Asset File")
	asset_tree.set_column_title(1, "Detected Kind")
	asset_tree.set_column_title(2, "Tile Size")
	asset_tree.set_column_title(3, "Status / Warnings")
	asset_tree.set_column_expand(0, true)
	asset_tree.set_column_expand(1, false)
	asset_tree.set_column_custom_minimum_width(1, 160)
	asset_tree.set_column_expand(2, false)
	asset_tree.set_column_custom_minimum_width(2, 80)
	asset_tree.set_column_expand(3, true)


func _setup_dialogs() -> void:
	_source_dialog = FileDialog.new()
	_source_dialog.file_mode = FileDialog.FILE_MODE_OPEN_DIR
	_source_dialog.access = FileDialog.ACCESS_FILESYSTEM
	_source_dialog.dir_selected.connect(_on_source_dir_selected)
	add_child(_source_dialog)

	_output_dialog = FileDialog.new()
	_output_dialog.file_mode = FileDialog.FILE_MODE_OPEN_DIR
	_output_dialog.access = FileDialog.ACCESS_RESOURCES
	_output_dialog.dir_selected.connect(_on_output_dir_selected)
	add_child(_output_dialog)


func _on_browse_source_pressed() -> void:
	if _source_dialog:
		_source_dialog.popup_centered(Vector2i(800, 600))


func _on_browse_output_pressed() -> void:
	if _output_dialog:
		_output_dialog.popup_centered(Vector2i(800, 600))


func _on_source_dir_selected(dir: String) -> void:
	if source_dir_edit:
		source_dir_edit.text = dir
		if theme_name_edit and theme_name_edit.text.strip_edges().is_empty():
			var default_theme := dir.get_file()
			if default_theme == "":
				default_theme = dir.get_base_dir().get_file()
			theme_name_edit.text = default_theme.capitalize()


func _on_output_dir_selected(dir: String) -> void:
	if output_dir_edit:
		output_dir_edit.text = dir


func _on_scan_pressed() -> void:
	var src_dir := source_dir_edit.text.strip_edges() if source_dir_edit else ""
	if src_dir.is_empty():
		_set_status("Please select an asset source directory.", Color.ORANGE)
		return

	var mode_id: int = detect_mode_opt.get_selected_id() if detect_mode_opt else 0
	var size_id: int = tile_size_opt.get_selected_id() if tile_size_opt else 0

	_set_status("Scanning directory: %s..." % src_dir, Color.WHITE)
	scanned_assets = AdapterFacadeScript.scan_directory(src_dir, mode_id, size_id)

	_populate_asset_tree(scanned_assets)
	if scanned_assets.is_empty():
		_set_status("No compatible assets found in directory.", Color.ORANGE)
		if convert_btn:
			convert_btn.disabled = true
	else:
		_set_status("Scan complete: %d assets detected." % scanned_assets.size(), Color.GREEN)
		if convert_btn:
			convert_btn.disabled = false


func _populate_asset_tree(assets: Array) -> void:
	if asset_tree == null:
		return
	asset_tree.clear()
	var root := asset_tree.create_item()

	for item in assets:
		var node := asset_tree.create_item(root)
		var file_name: String = item.path.get_file()
		var kind_str := _asset_kind_to_string(item.kind)
		var size_str := "%d px" % item.tile_size
		var warning_str: String = item.warning

		node.set_text(0, file_name)
		node.set_text(1, kind_str)
		node.set_text(2, size_str)
		node.set_text(3, warning_str if warning_str != "" else "OK")

		if warning_str != "":
			node.set_custom_color(3, Color.YELLOW)
		else:
			node.set_custom_color(3, Color.LIGHT_GREEN)


func _on_convert_pressed() -> void:
	if scanned_assets.is_empty():
		_set_status("No scanned assets to convert.", Color.ORANGE)
		return

	var out_dir := output_dir_edit.text.strip_edges() if output_dir_edit else "res://imported_assets/Theme"
	var theme_name := theme_name_edit.text.strip_edges() if theme_name_edit else "Theme"
	if theme_name.is_empty():
		theme_name = "Theme"

	var size_id: int = tile_size_opt.get_selected_id() if tile_size_opt else 0
	var apply_meta: bool = apply_metadata_chk.button_pressed if apply_metadata_chk else true

	_set_status("Converting %d assets..." % scanned_assets.size(), Color.YELLOW)
	if progress_bar:
		progress_bar.value = 20

	var options := {
		"output_dir": out_dir,
		"theme_name": theme_name,
		"tile_size": size_id,
		"fps": 6.0,
		"apply_metadata": apply_meta
	}

	var res := AdapterFacadeScript.convert_assets(scanned_assets, options)

	if progress_bar:
		progress_bar.value = 100

	if res.get("success", false) and res.get("errors", []).is_empty():
		var ts_path: String = res.get("tileset_path", "")
		var num_scenes: int = res.get("generated_scenes", []).size()
		var num_frames: int = res.get("generated_sprite_frames", []).size()
		_set_status("Conversion finished! TileSet: %s, Scenes: %d, SpriteFrames: %d" % [ts_path.get_file(), num_scenes, num_frames], Color.GREEN)
	else:
		var errs: Array = res.get("errors", [])
		_set_status("Conversion encountered errors: %s" % str(errs), Color.RED)


func _set_status(msg: String, col: Color = Color.WHITE) -> void:
	if status_label:
		status_label.text = msg
		status_label.add_theme_color_override("font_color", col)


func _asset_kind_to_string(kind: int) -> String:
	match kind:
		AssetDetectorScript.AssetKind.RPG_A1: return "A1 (Water/Waterfall)"
		AssetDetectorScript.AssetKind.RPG_A2: return "A2 (Ground/Floor)"
		AssetDetectorScript.AssetKind.RPG_A3: return "A3 (Wall/Roof)"
		AssetDetectorScript.AssetKind.RPG_A4: return "A4 (Wall Top/Side)"
		AssetDetectorScript.AssetKind.RPG_A5: return "A5 (Normal Ground)"
		AssetDetectorScript.AssetKind.RPG_B: return "B (Upper Objects)"
		AssetDetectorScript.AssetKind.RPG_C: return "C (Upper Objects)"
		AssetDetectorScript.AssetKind.RPG_D: return "D (Upper Objects)"
		AssetDetectorScript.AssetKind.RPG_E: return "E (Upper Objects)"
		AssetDetectorScript.AssetKind.KR_NON_RM_A1: return "KR Non-RM A1"
		AssetDetectorScript.AssetKind.KR_NON_RM_A2: return "KR Non-RM A2"
		AssetDetectorScript.AssetKind.KR_NON_RM_A3: return "KR Non-RM A3"
		AssetDetectorScript.AssetKind.KR_NON_RM_A4: return "KR Non-RM A4"
		AssetDetectorScript.AssetKind.RPG_CHARACTER_SINGLE: return "Character ($ Single)"
		AssetDetectorScript.AssetKind.RPG_CHARACTER_SHEET: return "Character Sheet (8 Chars)"
		AssetDetectorScript.AssetKind.RPG_OBJECT_SINGLE: return "Object (!$ Single)"
		AssetDetectorScript.AssetKind.RPG_OBJECT_SHEET: return "Object Sheet (! Multi)"
		AssetDetectorScript.AssetKind.DOOR: return "Door (Animated)"
		AssetDetectorScript.AssetKind.TREASURE_CHEST: return "Treasure Chest"
		AssetDetectorScript.AssetKind.KR_F_EXTENSION: return "KR F Extension"
		AssetDetectorScript.AssetKind.GENERIC_ANIMATION: return "Generic Animation"
		AssetDetectorScript.AssetKind.RPG_TILESET_METADATA: return "Tilesets.json (Metadata)"
		_: return "Unknown"
