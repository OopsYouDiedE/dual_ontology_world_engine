extends PanelContainer
## A non-modal status message. The caller supplies already permitted text.
signal action_requested
signal dismissed

var _timer: SceneTreeTimer
var _generation := 0
var _message: Label
var _action: Button

func _ready() -> void:
	theme = preload("res://scenes/UI/Components/MaterialTheme.tres")
	theme_type_variation = &"SnackbarPanel"
	var row := HBoxContainer.new()
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	row.add_theme_constant_override("separation", 16)
	add_child(row)
	_message = Label.new()
	_message.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_message.theme_type_variation = &"SnackbarText"
	_message.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_message.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(_message)
	_action = Button.new()
	_action.custom_minimum_size.y = 48
	_action.pressed.connect(_on_action)
	row.add_child(_action)
	visible = false

## duration <= 0 keeps the message until dismissed or replaced.
func show_message(message: String, action_text := "", duration := 5.0) -> void:
	_generation += 1
	_message.text = message
	_action.text = action_text
	_action.visible = not action_text.is_empty()
	mouse_filter = Control.MOUSE_FILTER_STOP if _action.visible else Control.MOUSE_FILTER_IGNORE
	visible = true
	if duration > 0.0:
		var current := _generation
		_timer = get_tree().create_timer(duration)
		_timer.timeout.connect(func() -> void:
			if current == _generation: dismiss())

func dismiss() -> void:
	if not visible: return
	_generation += 1
	visible = false
	dismissed.emit()

func _on_action() -> void:
	dismiss()
	action_requested.emit()



