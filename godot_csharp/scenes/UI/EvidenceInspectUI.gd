extends Control

## Material 3 Evidence Inspection Interface (Task 7.07)
## Clean 3D presentation viewport with rotation, zooming, and verified observation text.
## Shows only permitted details; returns to original target on close.

signal closed(original_target_id: String)

const MaterialControls := preload("res://scenes/UI/Components/MaterialControls.gd")

@onready var modal_backdrop: ColorRect = %Backdrop
@onready var main_card: PanelContainer = %MainCard
@onready var title_label: Label = %TitleLabel
@onready var source_time_label: Label = %SourceTimeLabel
@onready var close_btn: Button = %CloseButton
@onready var observations_list: VBoxContainer = %ObservationsList
@onready var viewport_container: SubViewportContainer = %ViewportContainer
@onready var sub_viewport: SubViewport = %SubViewport
@onready var camera: Camera3D = %Camera3D
@onready var mesh_pivot: Node3D = %MeshPivot
@onready var evidence_mesh: MeshInstance3D = %EvidenceMesh

var _is_open: bool = false
var _original_target_id: String = ""
var _is_dragging: bool = false
var _drag_start_pos: Vector2 = Vector2.ZERO
var _pivot_rotation: Vector3 = Vector3.ZERO
var _camera_dist: float = 2.0
const MIN_DIST := 1.0
const MAX_DIST := 4.5

func _ready() -> void:
	visible = false
	MaterialControls.apply(self)
	MaterialControls.prepare_button(close_btn)
	close_btn.pressed.connect(close_inspect)

func is_inspect_open() -> bool:
	return _is_open

## Opens 3D evidence inspect interface
## data format:
## {
##   "id": "evidence_key_01",
##   "target_id": "door_key",
##   "title": "黄铜齿纹钥匙",
##   "source": "走廊地板拾取",
##   "time": "22:18",
##   "mesh_path": "optional_mesh.tres",
##   "details": [
##     {"label": "材质", "text": "陈旧黄铜，表面有氧化铜绿", "revealed": true},
##     {"label": "齿纹", "text": "单排四齿，第三齿有磨锉痕迹", "revealed": true},
##     {"label": "暗记", "text": "尾部刻有极细微的'R05'", "revealed": false}
##   ]
## }
func open_inspect(data: Dictionary) -> void:
	_original_target_id = str(data.get("target_id", data.get("id", "")))
	title_label.text = str(data.get("title", data.get("name", "证物检视")))
	
	var src := str(data.get("source", "现场发现"))
	var tm := str(data.get("time", ""))
	source_time_label.text = "发现来源: %s  时间: %s" % [src, tm] if not tm.is_empty() else "发现来源: %s" % src
	
	_populate_observations(data.get("details", data.get("observations", [])))
	_setup_3d_display(data)
	
	visible = true
	_is_open = true
	
	await get_tree().process_frame
	close_btn.grab_focus()

func _populate_observations(details: Array) -> void:
	for child in observations_list.get_children():
		observations_list.remove_child(child)
		child.queue_free()
		
	if details.is_empty():
		var empty_lbl := Label.new()
		empty_lbl.text = "暂无更细致的检视记录。"
		empty_lbl.add_theme_color_override("font_color", MaterialControls.COLOR_OUTLINE)
		observations_list.add_child(empty_lbl)
		return
		
	for item in details:
		if not item is Dictionary:
			continue
		# Strictly show only permitted/revealed details (Task 7.07)
		var is_revealed: bool = bool(item.get("revealed", true))
		var card := PanelContainer.new()
		var box := StyleBoxFlat.new()
		box.bg_color = MaterialControls.COLOR_SURFACE_VARIANT
		box.corner_radius_top_left = 8
		box.corner_radius_top_right = 8
		box.corner_radius_bottom_right = 8
		box.corner_radius_bottom_left = 8
		box.content_margin_left = 12.0
		box.content_margin_top = 8.0
		box.content_margin_right = 12.0
		box.content_margin_bottom = 8.0
		card.add_theme_stylebox_override("panel", box)
		
		var vbox := VBoxContainer.new()
		vbox.add_theme_constant_override("separation", 2)
		
		var tag_lbl := Label.new()
		tag_lbl.text = str(item.get("label", "特征"))
		tag_lbl.add_theme_font_size_override("font_size", 14)
		vbox.add_child(tag_lbl)
		
		var text_lbl := Label.new()
		text_lbl.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		text_lbl.add_theme_font_size_override("font_size", 13)
		if is_revealed:
			text_lbl.text = str(item.get("text", item.get("desc", "")))
		else:
			text_lbl.text = "[尚未取得深入观察证据]"
			text_lbl.add_theme_color_override("font_color", MaterialControls.COLOR_OUTLINE)
		vbox.add_child(text_lbl)
		
		card.add_child(vbox)
		observations_list.add_child(card)

func _setup_3d_display(data: Dictionary) -> void:
	_camera_dist = 2.0
	camera.position = Vector3(0, 0, _camera_dist)
	_pivot_rotation = Vector3.ZERO
	mesh_pivot.rotation_degrees = _pivot_rotation
	
	# Load mesh if supplied, or standard clean placeholder box
	var mesh_obj: Mesh = null
	var mesh_path: String = str(data.get("mesh_path", ""))
	if not mesh_path.is_empty() and ResourceLoader.exists(mesh_path):
		mesh_obj = load(mesh_path) as Mesh
	if mesh_obj == null:
		var box := BoxMesh.new()
		box.size = Vector3(0.6, 0.6, 0.6)
		mesh_obj = box
	evidence_mesh.mesh = mesh_obj

func close_inspect() -> void:
	if not _is_open:
		return
	_is_open = false
	visible = false
	var target := _original_target_id
	closed.emit(target)

func _gui_input(event: InputEvent) -> void:
	if not _is_open:
		return
	if event.is_action_pressed("ui_cancel") or event.is_action_pressed("cancel"):
		close_inspect()
		get_viewport().set_input_as_handled()

func _input(event: InputEvent) -> void:
	if not _is_open:
		return
	# 3D rotate and zoom control on viewport
	if event is InputEventMouseButton:
		var mb := event as InputEventMouseButton
		if mb.button_index == MOUSE_BUTTON_LEFT:
			_is_dragging = mb.pressed
			_drag_start_pos = mb.position
		elif mb.button_index == MOUSE_BUTTON_WHEEL_UP and mb.pressed:
			_camera_dist = clampf(_camera_dist - 0.2, MIN_DIST, MAX_DIST)
			camera.position.z = _camera_dist
		elif mb.button_index == MOUSE_BUTTON_WHEEL_DOWN and mb.pressed:
			_camera_dist = clampf(_camera_dist + 0.2, MIN_DIST, MAX_DIST)
			camera.position.z = _camera_dist
	elif event is InputEventMouseMotion and _is_dragging:
		var mm := event as InputEventMouseMotion
		_pivot_rotation.y += mm.relative.x * 0.4
		_pivot_rotation.x += mm.relative.y * 0.4
		_pivot_rotation.x = clampf(_pivot_rotation.x, -70.0, 70.0)
		mesh_pivot.rotation_degrees = _pivot_rotation
