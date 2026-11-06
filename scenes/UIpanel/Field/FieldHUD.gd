extends CanvasLayer

## 常驻探案 HUD（FieldHUD.gd）
## 内聚原 FieldHUD, FieldChrome, CurrentActionUI, ActionQueuePanel, TooltipGuideUI, Snackbar。

signal book_open_requested
signal action_cancelled(action_id: String)
signal settings_requested
signal perception_inspect_requested(record: Dictionary)

@onready var top_bar: PanelContainer = $TopBar
@onready var room_label: Label = $TopBar/MarginContainer/HBoxContainer/RoomLabel
@onready var time_label: Label = $TopBar/MarginContainer/HBoxContainer/TimeLabel
@onready var danger_label: Label = $TopBar/MarginContainer/HBoxContainer/DangerLabel
@onready var btn_book: Button = $TopBar/MarginContainer/HBoxContainer/BookButton

@onready var action_progress_box: PanelContainer = $BottomCenter/VBoxContainer/ActionProgressBox
@onready var action_name_label: Label = $BottomCenter/VBoxContainer/ActionProgressBox/VBoxContainer/HBoxContainer/ActionNameLabel
@onready var action_bar: ProgressBar = $BottomCenter/VBoxContainer/ActionProgressBox/VBoxContainer/ProgressBar
@onready var queue_container: HBoxContainer = $BottomCenter/VBoxContainer/QueueContainer

@onready var snackbar: PanelContainer = $TopCenter/Snackbar
@onready var snackbar_label: Label = $TopCenter/Snackbar/MarginContainer/SnackbarLabel
@onready var key_guides: HBoxContainer = $BottomRight/KeyGuides

var _snackbar_timer := 0.0

func _ready() -> void:
	layer = 10
	process_mode = Node.PROCESS_MODE_ALWAYS
	btn_book.pressed.connect(func(): book_open_requested.emit())
	snackbar.hide()
	action_progress_box.hide()

func _process(delta: float) -> void:
	if _snackbar_timer > 0.0:
		_snackbar_timer -= delta
		if _snackbar_timer <= 0.0:
			var tw := create_tween()
			tw.tween_property(snackbar, "modulate:a", 0.0, 0.3)
			tw.tween_callback(snackbar.hide)

func update_hud(time_str: String, location_str: String, erosion_level: float) -> void:
	if not time_str.is_empty(): time_label.text = time_str
	if not location_str.is_empty(): room_label.text = location_str
	var pct := int(erosion_level * 100.0)
	danger_label.text = "侵蚀度 %d%%" % pct
	danger_label.visible = pct > 0

func update_action_progress(action_name: String, progress: float, queue_items: Array = []) -> void:
	if action_name.is_empty() or progress >= 1.0:
		action_progress_box.hide()
	else:
		action_progress_box.show()
		action_name_label.text = action_name
		action_bar.value = clampf(progress * 100.0, 0.0, 100.0)

	# Update queue
	for child in queue_container.get_children():
		child.queue_free()

	for item in queue_items:
		var lbl := Label.new()
		lbl.text = "[" + str(item) + "]"
		lbl.add_theme_font_size_override("font_size", 12)
		queue_container.add_child(lbl)

func show_snackbar(message: String, kind: String = "info") -> void:
	snackbar_label.text = message
	match kind:
		"warning":
			snackbar_label.modulate = Color("e89a3c")
		"error":
			snackbar_label.modulate = Color("e04b4b")
		_:
			snackbar_label.modulate = Color("ffffff")

	snackbar.modulate.a = 1.0
	snackbar.show()
	_snackbar_timer = 2.5
