extends SceneTree
## Finite native-Control input probe. No Main, DV session, gameplay or artwork.
## Use a fresh XDG_DATA_HOME / isolated user-data directory and --out=<new dir>.
const UIInput := preload("res://scenes/Shared/Input/UIInput.gd")
const BookProjection := preload("res://scenes/UI/BookProjection3D.gd")
const Modal := preload("res://scenes/UI/ModalPanel.gd")
const Settings := preload("res://scenes/Shared/Settings/GameSettings.gd")

class InputDesk extends Node3D:
	func set_pointer(_point: Vector2) -> void: pass

class Forwarder extends Node:
	var projection: RefCounted
	var enabled := true
	func _input(event: InputEvent) -> void:
		if enabled:
			projection.forward(event)
			get_viewport().set_input_as_handled()
	func _process(delta: float) -> void:
		if enabled: projection.update(Vector2.ZERO, delta, true)

var output := ""
var rows: Array[Dictionary] = []
var failures: Array[String] = []
var origin := 0
var forwarder: Node
var page: Control
var page_viewport: SubViewport
var archive: Node
var selected: Array[String] = []

func _initialize() -> void:
	origin = Time.get_ticks_usec()
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--out="): output = argument.trim_prefix("--out=")
	if output.is_empty() or not output.is_absolute_path() or DirAccess.dir_exists_absolute(output):
		push_error("Input probe requires --out=<new absolute directory>")
		quit(2)
		return
	if FileAccess.file_exists("user://settings.cfg"):
		push_error("Input probe requires isolated, empty user-data; it will not overwrite existing preferences")
		quit(2)
		return
	DirAccess.make_dir_recursive_absolute(output)
	_run.call_deferred()

func _run() -> void:
	var archive_script := load("res://scenes/Host/FrameArchive.cs")
	if archive_script == null or not archive_script.can_instantiate():
		push_error("Build the .NET project before the input probe")
		quit(2)
		return
	archive = archive_script.new()
	root.add_child(archive)
	_setup_page()
	await process_frame
	await process_frame
	var first := page.get_node("Scroll/Column/First") as Button
	var second := page.get_node("Scroll/Column/Second") as Button
	var slider := page.get_node("Scroll/Column/Volume") as HSlider
	var accessibility_label := page.get_node("Scroll/Column/AccessibilityOnly") as Label
	var accessibility_enabled := is_accessibility_enabled()
	var initial_target: Control = accessibility_label if accessibility_enabled else first
	page_viewport.gui_release_focus()
	_button(JOY_BUTTON_DPAD_DOWN, true)
	_button(JOY_BUTTON_DPAD_DOWN, false)
	_observe("initial controller focus respects native accessibility mode",
		page_viewport.gui_get_focus_owner() == initial_target,
		{"accessibility_enabled": accessibility_enabled, "expected_control": initial_target.name})
	first.grab_focus()
	_axis(JOY_AXIS_LEFT_Y, 0.18)
	_observe("stick drift leaves focus alone", page_viewport.gui_get_focus_owner() == first)
	_button(JOY_BUTTON_DPAD_DOWN, true)
	_button(JOY_BUTTON_DPAD_DOWN, false)
	_observe("D-pad skips disabled controls", page_viewport.gui_get_focus_owner() == second)
	_button(JOY_BUTTON_A, true)
	_button(JOY_BUTTON_A, false)
	_observe("native A press/release activates once", selected == ["Second"])
	_axis(JOY_AXIS_LEFT_Y, 0.85)
	_axis(JOY_AXIS_LEFT_Y, 0.0)
	_observe("left stick reaches volume slider", page_viewport.gui_get_focus_owner() == slider)
	_axis(JOY_AXIS_LEFT_X, 0.85)
	_axis(JOY_AXIS_LEFT_X, 0.0)
	_observe("horizontal stick edits native slider", is_equal_approx(slider.value, 0.6), {"value": slider.value})
	first.grab_focus()
	forwarder.projection.reset_navigation()
	_axis(JOY_AXIS_LEFT_Y, 0.85)
	await create_timer(0.44, true, false, true).timeout
	_observe("held stick repeats after initial delay", page_viewport.gui_get_focus_owner() == slider)
	_axis(JOY_AXIS_LEFT_Y, 0.0)
	await create_timer(0.24, true, false, true).timeout
	_observe("neutral stick stops repeat", page_viewport.gui_get_focus_owner() == slider)
	_axis(JOY_AXIS_RIGHT_Y, 0.8)
	await create_timer(0.16, true, false, true).timeout
	_axis(JOY_AXIS_RIGHT_Y, 0.0)
	var scroll := page.get_node("Scroll") as ScrollContainer
	_observe("right stick scrolls the active page", scroll.scroll_vertical > 0, {"scroll": scroll.scroll_vertical})
	forwarder.enabled = false
	forwarder.projection.reset_navigation()
	await _probe_modal_return()
	_probe_bindings()
	archive.WriteRecord(output.path_join("input-observations.dvlog"), JSON.stringify({
		"fixture": "native-ui-input-v1", "scope": "native Control and production input helpers only",
		"events": rows, "failures": failures, "no_dv_session": true,
		"physical_controller_tested": false, "elapsed_us": Time.get_ticks_usec() - origin}))
	print("Native input observation complete. Evidence: ", output, ". Failures: ", "; ".join(failures))
	quit(0 if failures.is_empty() else 1)

func _setup_page() -> void:
	page_viewport = SubViewport.new()
	page_viewport.size = Vector2i(580, 820)
	page_viewport.handle_input_locally = true
	root.add_child(page_viewport)
	page = Control.new()
	page.name = "Page"
	page.size = Vector2(580, 820)
	page_viewport.add_child(page)
	var scroll := ScrollContainer.new()
	scroll.name = "Scroll"
	scroll.size = Vector2(540, 380)
	scroll.follow_focus = true
	page.add_child(scroll)
	var column := VBoxContainer.new()
	column.name = "Column"
	column.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.add_child(column)
	var accessibility_label := Label.new()
	accessibility_label.name = "AccessibilityOnly"
	accessibility_label.text = "Screen-reader description"
	accessibility_label.focus_mode = Control.FOCUS_ACCESSIBILITY
	accessibility_label.custom_minimum_size.y = 48
	column.add_child(accessibility_label)
	for label in ["First", "Disabled", "Second"]:
		var button := Button.new()
		button.name = label
		button.text = label
		button.custom_minimum_size.y = 48
		button.disabled = label == "Disabled"
		button.pressed.connect(func(): selected.append(label))
		column.add_child(button)
	var slider := HSlider.new()
	slider.name = "Volume"
	slider.min_value = 0.0
	slider.max_value = 1.0
	slider.step = 0.1
	slider.value = 0.5
	slider.custom_minimum_size.y = 48
	column.add_child(slider)
	for index in 16:
		var button := Button.new()
		button.name = "Entry%d" % index
		button.text = button.name
		button.custom_minimum_size.y = 48
		column.add_child(button)
	var desk := InputDesk.new()
	root.add_child(desk)
	var surface := TextureRect.new()
	surface.size = Vector2(580, 820)
	root.add_child(surface)
	var binding := Node3D.new()
	desk.add_child(binding)
	forwarder = Forwarder.new()
	forwarder.projection = BookProjection.new()
	forwarder.projection.configure(desk, surface, page_viewport, binding)
	root.add_child(forwarder)

func _probe_modal_return() -> void:
	var lower := _modal("Lower")
	lower.show_panel()
	await create_timer(0.35, true, false, true).timeout
	var upper := _modal("Upper")
	lower.set_external_blocked(true)
	upper.closed.connect(func(): lower.set_external_blocked(false))
	upper.show_panel()
	await create_timer(0.35, true, false, true).timeout
	_mouse_back(true)
	await create_timer(0.35, true, false, true).timeout
	_mouse_back(false)
	_observe("right-click returns one modal level", not upper.is_active() and lower.is_open())
	_observe("back release does not close restored modal", lower.can_interact())
	_button(JOY_BUTTON_B, true)
	await create_timer(0.35, true, false, true).timeout
	_button(JOY_BUTTON_B, false)
	_observe("controller B closes the remaining modal", not lower.is_active())
	upper.queue_free()
	lower.queue_free()

func _modal(label: String) -> CanvasLayer:
	var panel := Modal.new()
	panel.name = label
	var dimmer := ColorRect.new()
	dimmer.name = "Dimmer"
	dimmer.size = Vector2(580, 820)
	panel.add_child(dimmer)
	var center := Control.new()
	center.name = "CenterBox"
	center.size = Vector2(500, 400)
	panel.add_child(center)
	var close_button := Button.new()
	close_button.name = "ModalCloseButton"
	close_button.text = label
	close_button.size = Vector2(180, 48)
	center.add_child(close_button)
	close_button.owner = panel
	close_button.unique_name_in_owner = true
	close_button.pressed.connect(panel.close)
	root.add_child(panel)
	return panel

func _probe_bindings() -> void:
	var settings := Settings.new()
	var original_interact: Array[InputEvent] = settings.action_events("interact")
	for action in ["move_left", "interact", "cancel", "book_prev", "book_next"]:
		for event in settings.action_events(action):
			var data: Dictionary = settings._event_to_dict(event)
			var restored: InputEvent = settings._dict_to_event(data, action)
			_observe("binding round trip: " + action + " / " + settings.event_label(event), restored != null and settings._same_input(event, restored))
	settings.reset_action("move_left")
	var has_stick := false
	for event in settings.action_events("move_left"):
		if event is InputEventJoypadMotion: has_stick = true
	_observe("reset preserves controller movement binding", has_stick)
	var legacy := ConfigFile.new()
	legacy.set_value("controls", "interact", [{"type": "key", "keycode": KEY_F9}])
	legacy.save("user://settings.cfg")
	settings._load()
	var labels := settings.action_text("interact")
	_observe("legacy keyboard binding receives controller mapping", labels.contains("F9") and labels.contains("A"), {"bindings": labels})
	var events: Array[InputEvent] = settings.action_events("interact")
	for index in range(events.size() - 1, -1, -1):
		if events[index] is InputEventJoypadButton: settings.unbind("interact", index)
	settings.flush()
	# A new application first loads the shipped defaults, then its saved edits.
	InputMap.action_erase_events("interact")
	for event: InputEvent in original_interact: InputMap.action_add_event("interact", event)
	var reloaded := Settings.new()
	reloaded._load()
	_observe("explicit controller removal survives reload", reloaded.action_events("interact").size() == 1)
	settings.free()
	reloaded.free()

func _button(index: JoyButton, pressed: bool) -> void:
	var event := InputEventJoypadButton.new()
	event.device = 0
	event.button_index = index
	event.pressed = pressed
	_record({"kind": "joy_button", "button": index, "pressed": pressed})
	root.push_input(event, true)

func _axis(index: JoyAxis, value: float) -> void:
	var event := InputEventJoypadMotion.new()
	event.device = 0
	event.axis = index
	event.axis_value = value
	_record({"kind": "joy_axis", "axis": index, "value": value})
	root.push_input(event, true)

func _mouse_back(pressed: bool) -> void:
	var event := InputEventMouseButton.new()
	event.button_index = MOUSE_BUTTON_RIGHT
	event.pressed = pressed
	event.position = Vector2(24, 24)
	event.global_position = event.position
	_record({"kind": "mouse_button", "button": MOUSE_BUTTON_RIGHT, "pressed": pressed})
	root.push_input(event, true)

func _observe(label: String, valid: bool, detail: Dictionary = {}) -> void:
	_record({"kind": "observation", "description": label, "observed": valid, "detail": detail})
	if not valid: failures.append(label)

func _record(row: Dictionary) -> void:
	row.elapsed_us = Time.get_ticks_usec() - origin
	rows.append(row)
