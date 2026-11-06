extends Node

## 统一输入路由器（UIInput.gd）
## 统一管理快捷键与调试热键分发：F2（平面直通UI）、F3（认知透视）、F4（3D建造台）。

signal flat_ui_toggled(active: bool)
signal perspective_toggled(active: bool)
signal architect_builder_toggled(active: bool)
signal notebook_toggled
signal cancel_requested

var is_flat_ui := false
var is_perspective := false
var is_architect := false
var input_enabled := true

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS

func _unhandled_input(event: InputEvent) -> void:
	if not input_enabled: return
	if not (event is InputEventKey and event.is_pressed() and not event.is_echo()):
		return

	var key_event := event as InputEventKey
	match key_event.keycode:
		KEY_F2:
			is_flat_ui = not is_flat_ui
			flat_ui_toggled.emit(is_flat_ui)
			get_viewport().set_input_as_handled()
		KEY_F3:
			is_perspective = not is_perspective
			perspective_toggled.emit(is_perspective)
			get_viewport().set_input_as_handled()
		KEY_F4:
			is_architect = not is_architect
			architect_builder_toggled.emit(is_architect)
			get_viewport().set_input_as_handled()
		KEY_TAB, KEY_B:
			notebook_toggled.emit()
			get_viewport().set_input_as_handled()
		KEY_ESCAPE:
			cancel_requested.emit()
			get_viewport().set_input_as_handled()

static func is_back(event: InputEvent, pressed_only := true) -> bool:
	var matches: bool = event.is_action("cancel") or event.is_action("ui_cancel") \
		or (event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_RIGHT)
	return matches and (not pressed_only or (event.is_pressed() and not event.is_echo()))
