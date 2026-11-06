extends Control

## 书本外壳容器（BookShellUI.gd）
## 吸收合并原 CasebookUI, PhysicalBookView, NativeBookInterface, BookPageLayout 等。
## 支持 3D 弯曲纸面反投影与 F2 键 2D 平面直通双模式切换！
## 托管 4 套数据驱动双页模板，映射 B01 ~ B08 八大案卷章节。

signal opened
signal closed
signal page_switched(page_id: String)
signal ui_event(event: Dictionary)

const CATALOG_INSPECT_SCENE := preload("res://scenes/UIpanel/Spreads/CatalogInspectSpread.tscn")
const CHRONICLE_MAP_SCENE := preload("res://scenes/UIpanel/Spreads/ChronicleMapSpread.tscn")
const PINBOARD_PLATE_SCENE := preload("res://scenes/UIpanel/Spreads/PinboardPlateSpread.tscn")
const LEDGER_SYSTEM_SCENE := preload("res://scenes/UIpanel/Spreads/LedgerSystemSpread.tscn")
const STAGE_3D_SCENE := preload("res://scenes/components/world/LowpolyBookStage3D.tscn")

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

# Dual-pipeline containers
@onready var paper_viewport: SubViewport = $PaperViewport
@onready var spread_container: Control = $PaperViewport/SpreadContainer
@onready var stage_viewport: SubViewport = $StageViewport
@onready var stage_display: TextureRect = $StageDisplay
@onready var flat_mode_layer: Control = $FlatModeLayer
@onready var flat_page_anchor: Control = $FlatModeLayer/CenterContainer/PageAnchor
@onready var tabs_bar: HBoxContainer = $ChromeLayer/TopChrome/Margin/HBox/TabsContainer
@onready var btn_close: Button = $ChromeLayer/TopChrome/Margin/HBox/CloseButton
@onready var mode_indicator: Label = $ChromeLayer/TopChrome/Margin/HBox/ModeIndicator

var _spreads: Dictionary = {} # "catalog", "chronicle", "pinboard", "ledger"
var _current_section := "B01"
var _stage_3d: Node3D
var _is_open := false
var _snapshot: Dictionary = {}

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	btn_close.pressed.connect(close)
	_build_tabs()
	_instantiate_spreads()
	_setup_3d_stage()
	set_flat_mode(not use_3d_book_stage)
	hide()

func _build_tabs() -> void:
	for child in tabs_bar.get_children():
		child.queue_free()

	for sec_id in ["B01", "B02", "B03", "B04", "B05", "B06", "B07", "B08"]:
		var info: Dictionary = SECTIONS[sec_id]
		var btn := Button.new()
		btn.text = "%s [%s]" % [info["name"], sec_id]
		btn.pressed.connect(func(): open_section(sec_id))
		tabs_bar.add_child(btn)

func _instantiate_spreads() -> void:
	# Instantiate the 4 unified spread templates
	_spreads["catalog"] = CATALOG_INSPECT_SCENE.instantiate()
	_spreads["chronicle"] = CHRONICLE_MAP_SCENE.instantiate()
	_spreads["pinboard"] = PINBOARD_PLATE_SCENE.instantiate()
	_spreads["ledger"] = LEDGER_SYSTEM_SCENE.instantiate()

	for key in _spreads:
		var s: Control = _spreads[key]
		spread_container.add_child(s)
		s.hide()

func _setup_3d_stage() -> void:
	if STAGE_3D_SCENE != null:
		_stage_3d = STAGE_3D_SCENE.instantiate()
		stage_viewport.add_child(_stage_3d)

func configure(data: Dictionary, _book_data: RefCounted = null) -> void:
	_snapshot = data
	for s_key in _spreads:
		if _spreads[s_key].has_method("configure"):
			_spreads[s_key].configure(data)

func open(initial_page := "B01") -> void:
	_is_open = true
	open_section(initial_page)
	show()
	opened.emit()

func close() -> void:
	_is_open = false
	hide()
	closed.emit()

func open_section(page_id: String) -> void:
	if not SECTIONS.has(page_id):
		page_id = "B01"
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

	page_switched.emit(page_id)

func set_flat_mode(is_flat: bool) -> void:
	use_3d_book_stage = not is_flat
	if is_flat:
		# Reparent spread_container to flat mode anchor
		if spread_container.get_parent() != flat_page_anchor:
			spread_container.get_parent().remove_child(spread_container)
			flat_page_anchor.add_child(spread_container)
		flat_mode_layer.show()
		stage_display.hide()
		mode_indicator.text = "【F2 平面直通模式】"
	else:
		# Reparent spread_container back to PaperViewport
		if spread_container.get_parent() != paper_viewport:
			spread_container.get_parent().remove_child(spread_container)
			paper_viewport.add_child(spread_container)
		flat_mode_layer.hide()
		stage_display.show()
		mode_indicator.text = "【3D 书案视口模式】"

func toggle_flat_mode() -> void:
	set_flat_mode(use_3d_book_stage)
