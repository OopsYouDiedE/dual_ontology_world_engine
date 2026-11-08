extends Control
const Sound := preload("res://scenes/components/shared/Sound.gd")

## 书本外壳容器（BookShellUI.gd）
## 吸收合并原 CasebookUI, PhysicalBookView, NativeBookInterface, BookPageLayout 等。
## 支持 3D 弯曲纸面反投影与 F2 键 2D 平面直通双模式切换！
## 托管 4 套数据驱动双页模板，映射 B01 ~ B08 八大案卷章节。

signal opened
signal closed
signal page_switched(page_id: String)
signal ui_event(event: Dictionary)
signal return_title_requested
signal quick_save_requested

const CATALOG_INSPECT_SCENE := preload("res://scenes/UIpanel/Spreads/CatalogInspectSpread.tscn")
const CHRONICLE_MAP_SCENE := preload("res://scenes/UIpanel/Spreads/ChronicleMapSpread.tscn")
const PINBOARD_PLATE_SCENE := preload("res://scenes/UIpanel/Spreads/PinboardPlateSpread.tscn")
const LEDGER_SYSTEM_SCENE := preload("res://scenes/UIpanel/Spreads/LedgerSystemSpread.tscn")
const STAGE_3D_SCENE := preload("res://scenes/components/world/LowpolyBookStage3D.tscn")

const PAGE_NAMES := ["档案", "规则", "日程", "契约", "笔记", "图版", "存档", "设置"]
const TAB_INKS := ["eadcc5", "eee0c5", "30291e", "e8dfcf", "30291e", "30252c", "eee5cf", "edddce"]
const TEXTURE_SIZE := Vector2i(1600, 1040)

const SECTIONS := {
	"B01": {"name": "档案", "spread": "catalog", "mode": "evidence"},
	"B02": {"name": "规则", "spread": "catalog", "mode": "rulebook"},
	"B03": {"name": "日程", "spread": "chronicle", "mode": "schedule"},
	"B04": {"name": "契约", "spread": "catalog", "mode": "convention"},
	"B05": {"name": "笔记", "spread": "pinboard", "mode": "hypothesis"},
	"B06": {"name": "图版", "spread": "pinboard", "mode": "gallery"},
	"B07": {"name": "存档", "spread": "ledger", "mode": "archive"},
	"B08": {"name": "设置", "spread": "ledger", "mode": "settings"}
}

@export var use_3d_book_stage := true
@export var desktop := false

# Containers
@onready var matte: ColorRect = $CompositionMatte
@onready var paper_viewport: SubViewport = $PaperViewport
@onready var spread_container: Control = $PaperViewport/SpreadContainer
@onready var stage_viewport: SubViewport = $StageViewport
@onready var stage_display: TextureRect = $StageDisplay
@onready var flat_mode_layer: Control = $FlatModeLayer
@onready var flat_page_anchor: Control = $FlatModeLayer/CenterContainer/PageAnchor
@onready var flat_tabs_container: HBoxContainer = $FlatModeLayer/FlatChrome/FlatTabsContainer
@onready var flat_close_button: Button = $FlatModeLayer/FlatChrome/FlatCloseButton

var _spreads: Dictionary = {} # "catalog", "chronicle", "pinboard", "ledger"
var _current_section := "B01"
var _stage_3d: Node3D
var _is_open := false
var _snapshot: Dictionary = {}
var _frame := Rect2()
var _tabs: Array[Button] = []
var _stamps: Array[Button] = []
var _tween: Tween

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	paper_viewport.transparent_bg = true

	_build_3d_controls()
	_build_flat_tabs()
	_instantiate_spreads()
	_setup_3d_stage()

	resized.connect(_resize)
	flat_close_button.pressed.connect(close)

	set_flat_mode(not use_3d_book_stage)
	hide()

func _build_3d_controls() -> void:
	for index in range(PAGE_NAMES.size()):
		var btn := _ink_button(PAGE_NAMES[index], Color(TAB_INKS[index]))
		btn.name = "Bookmark" + str(index + 1)
		btn.tooltip_text = ["证据与档案", "知识与规则", "事件与日程", "契约与公约", "猜想与笔记", "图版", "存取读档", "设置"][index]
		btn.pressed.connect(open_section.bind("B%02d" % (index + 1)))
		btn.hide()
		add_child(btn)
		_tabs.append(btn)

	for index in range(2):
		var btn := _ink_button("保存" if index == 0 else "合上", Color("eddec9"))
		btn.name = "SaveStamp" if index == 0 else "CloseStamp"
		btn.tooltip_text = "保存当前进度" if index == 0 else "合上案卷"
		if index == 0:
			btn.pressed.connect(func(): quick_save_requested.emit())
		else:
			btn.pressed.connect(close)
		btn.hide()
		add_child(btn)
		_stamps.append(btn)

func _build_flat_tabs() -> void:
	for child in flat_tabs_container.get_children():
		child.queue_free()

	for sec_id in ["B01", "B02", "B03", "B04", "B05", "B06", "B07", "B08"]:
		var info: Dictionary = SECTIONS[sec_id]
		var btn := Button.new()
		btn.text = "%s [%s]" % [info["name"], sec_id]
		btn.pressed.connect(func(): open_section(sec_id))
		flat_tabs_container.add_child(btn)

func _ink_button(caption: String, ink: Color) -> Button:
	var button := Button.new()
	button.text = caption
	button.flat = true
	button.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	button.focus_mode = Control.FOCUS_ALL
	for state in ["normal", "hover", "pressed", "disabled"]:
		button.add_theme_stylebox_override(state, StyleBoxEmpty.new())
	var focus := StyleBoxFlat.new()
	focus.bg_color = Color.TRANSPARENT
	focus.border_color = Color("e5d1ac")
	focus.border_width_bottom = 1
	button.add_theme_stylebox_override("focus", focus)
	button.add_theme_color_override("font_color", ink)
	button.add_theme_color_override("font_hover_color", ink.lightened(0.2))
	button.add_theme_color_override("font_focus_color", ink.lightened(0.2))
	button.add_theme_color_override("font_pressed_color", Color("fff5de"))
	button.add_theme_color_override("font_disabled_color", Color(ink, 0.45))
	return button

func _instantiate_spreads() -> void:
	_spreads["catalog"] = CATALOG_INSPECT_SCENE.instantiate()
	_spreads["chronicle"] = CHRONICLE_MAP_SCENE.instantiate()
	_spreads["pinboard"] = PINBOARD_PLATE_SCENE.instantiate()
	_spreads["ledger"] = LEDGER_SYSTEM_SCENE.instantiate()

	for key in _spreads:
		var s: Control = _spreads[key]
		spread_container.add_child(s)
		s.hide()

	# Connect ledger spread signals
	var ledger: Control = _spreads["ledger"]
	if ledger.has_signal("return_title_requested"):
		ledger.return_title_requested.connect(_on_ledger_return_title)

func _on_ledger_return_title() -> void:
	close()
	return_title_requested.emit()

func _setup_3d_stage() -> void:
	if STAGE_3D_SCENE != null:
		_stage_3d = STAGE_3D_SCENE.instantiate()
		stage_viewport.add_child(_stage_3d)
		_stage_3d.set_page_texture(paper_viewport.get_texture())
		_stage_3d.set_context(desktop)

func get_safe_frame() -> Rect2:
	var available := size
	if available.x < 2.0 or available.y < 2.0:
		available = get_viewport().get_visible_rect().size
	var width := minf(available.x, available.y * 2.0)
	var dimensions := Vector2(width, width * 0.5)
	return Rect2((available - dimensions) * 0.5, dimensions)

func _resize() -> void:
	if not is_instance_valid(_stage_3d): return
	_frame = get_safe_frame()
	stage_display.position = _frame.position
	stage_display.size = _frame.size
	stage_viewport.size = Vector2i(_frame.size).max(Vector2i(2, 1))
	_stage_3d.resize_view(Vector2(stage_viewport.size))
	_position_controls()

func _position_controls() -> void:
	if not is_instance_valid(_stage_3d) or not _is_open or not use_3d_book_stage:
		for b in _tabs + _stamps:
			b.hide()
		return

	var unit := _frame.size.x / 1280.0
	for index in range(_tabs.size()):
		var button := _tabs[index]
		button.size = Vector2(96.0, 44.0) * unit
		button.position = _frame.position + _stage_3d.anchor_screen(_stage_3d.tab_anchor(index)) - button.size * 0.5
		button.add_theme_font_size_override("font_size", maxi(13, roundi(25.0 * unit)))
		button.visible = _is_open
		button.disabled = desktop and index < 6
		button.modulate.a = 1.0 if _current_section == "B%02d" % (index + 1) else 0.94

	for index in range(_stamps.size()):
		var button := _stamps[index]
		var anchor: Vector3 = _stage_3d.stamp_anchor(index)
		button.size = Vector2(98.0, 44.0) * unit
		button.position = _frame.position + _stage_3d.anchor_screen(anchor) - button.size * 0.5
		button.add_theme_font_size_override("font_size", maxi(13, roundi(26.0 * unit)))
		button.visible = _is_open
		button.disabled = index == 0 and desktop

func set_desktop_mode(is_desktop: bool) -> void:
	desktop = is_desktop
	if is_instance_valid(_stage_3d):
		_stage_3d.set_context(desktop)
		_position_controls()

func configure(data: Variant, _book_data: RefCounted = null) -> void:
	if data is Dictionary:
		_snapshot = data
	elif data != null and data.has_method("observe"):
		_snapshot = data.observe()
	for s_key in _spreads:
		if _spreads[s_key].has_method("configure"):
			_spreads[s_key].configure(_snapshot)

func book_open() -> bool:
	return _is_open

func close_book() -> void:
	close()

func toggle_book(_topic := "") -> void:
	if _is_open:
		close()
	else:
		open("B01")

func refresh(_force := false) -> void:
	if _is_open:
		open_section(_current_section)

func book_state() -> Dictionary:
	return {"open": _is_open, "section": _current_section}

func open(initial_page := "B01") -> void:
	_is_open = true
	matte.visible = desktop or not use_3d_book_stage

	if is_instance_valid(_stage_3d):
		_stage_3d.set_context(desktop)
		_stage_3d.set_open(1.0)
		_resize()

	open_section(initial_page)
	show()
	_position_controls()
	opened.emit()

func close() -> void:
	_is_open = false
	if is_instance_valid(_stage_3d):
		_stage_3d.set_open(0.0)
	_position_controls()
	hide()
	closed.emit()

func open_section(page_id: String) -> void:
	Sound.sfx("page", -4.0, randf_range(0.96, 1.04))
	if not SECTIONS.has(page_id):
		page_id = "B01"
	if desktop and page_id not in ["B07", "B08"]:
		page_id = "B07"

	_current_section = page_id
	var info: Dictionary = SECTIONS[page_id]
	var spread_type: String = info["spread"]
	var spread_mode: String = info["mode"]

	for key in _spreads:
		var s: Control = _spreads[key]
		if key == spread_type:
			if s.has_method("set_mode"):
				s.set_mode(spread_mode)
			s.show()
		else:
			s.hide()

	_position_controls()
	page_switched.emit(page_id)

func set_flat_mode(is_flat: bool) -> void:
	use_3d_book_stage = not is_flat
	if is_flat:
		if spread_container.get_parent() != flat_page_anchor:
			spread_container.get_parent().remove_child(spread_container)
			flat_page_anchor.add_child(spread_container)
		flat_mode_layer.show()
		stage_display.hide()
		matte.hide()
	else:
		if spread_container.get_parent() != paper_viewport:
			spread_container.get_parent().remove_child(spread_container)
			paper_viewport.add_child(spread_container)
		flat_mode_layer.hide()
		stage_display.show()
		matte.visible = desktop
	_position_controls()

func toggle_flat_mode() -> void:
	set_flat_mode(use_3d_book_stage)

func _local_point(point: Vector2) -> Vector2:
	return get_global_transform_with_canvas().affine_inverse() * point

func _input(event: InputEvent) -> void:
	if not is_visible_in_tree() or not _is_open or not use_3d_book_stage: return
	if not is_instance_valid(_stage_3d): return

	var copy := event.duplicate() as InputEvent
	if copy is InputEventMouse:
		var local := _local_point(event.position)
		for button in _tabs + _stamps:
			if button.visible and button.get_rect().has_point(local):
				return
		var uv: Vector2 = _stage_3d.page_point(local - _frame.position, Vector2(TEXTURE_SIZE))
		copy.position = uv
		copy.global_position = uv
		if not Rect2(Vector2.ZERO, Vector2(TEXTURE_SIZE)).has_point(uv):
			return
	paper_viewport.push_input(copy, true)
	if paper_viewport.is_input_handled() or event is InputEventMouse:
		get_viewport().set_input_as_handled()
