@tool
extends PanelContainer
## Edge feedback has no world-state authority and never blocks the scene.
signal action_requested
signal dismissed

const Chrome := preload("res://scenes/UIpanel/FieldHUD/FieldChrome.gd")
var _generation := 0
var _message: Label
var _action: Button
var _motion: Tween

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	Chrome.apply(self, 15)
	add_theme_stylebox_override("panel", Chrome.box(0.9, Vector4(16, 8, 8, 8)))
	var row := HBoxContainer.new()
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	row.add_theme_constant_override("separation", 14)
	add_child(row)
	_message = Label.new()
	_message.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_message.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_message.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(_message)
	_action = Button.new()
	Chrome.button(_action, true)
	row.add_child(_action)
	_action.pressed.connect(_on_action)
	get_viewport().size_changed.connect(_place)
	visible = false

func show_message(message: String, action_text := "", duration := 3.5) -> void:
	if message.is_empty(): return
	Chrome.apply(self, 15)
	_generation += 1
	var current := _generation
	_message.text = message
	_action.text = action_text
	_action.visible = not action_text.is_empty()
	mouse_filter = Control.MOUSE_FILTER_STOP if _action.visible else Control.MOUSE_FILTER_IGNORE
	visible = true
	_place()
	if _motion != null: _motion.kill()
	modulate.a = 0
	_motion = create_tween()
	_motion.tween_property(self, "modulate:a", 1.0, 0.14)
	if duration > 0:
		_motion.tween_interval(duration)
		_motion.tween_property(self, "modulate:a", 0.0, 0.24)
		_motion.tween_callback(func():
			if current == _generation:
				visible = false
				dismissed.emit()
		)

func _place() -> void:
	if not is_node_ready(): return
	var viewport_size := get_viewport_rect().size
	var width := minf(540 * Chrome.ui_scale(self), maxf(180, viewport_size.x - 48))
	set_anchors_and_offsets_preset(Control.PRESET_TOP_LEFT)
	custom_minimum_size.x = width
	size.x = width
	reset_size()
	position = Vector2(viewport_size.x - size.x - 24, viewport_size.y - size.y - 26)

func dismiss() -> void:
	if not visible: return
	_generation += 1
	if _motion != null: _motion.kill()
	visible = false
	dismissed.emit()

func _on_action() -> void:
	dismiss()
	action_requested.emit()
