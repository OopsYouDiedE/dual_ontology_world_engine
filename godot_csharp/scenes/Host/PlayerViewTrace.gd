extends Node

var host: Node
var record_visible: Callable
var record_input: Callable
var _last_frame := ""
var _last_data: Dictionary = {}
var _wired: Dictionary = {}
var _control_context: Dictionary = {}
var _closing := false
var _enabled := false
var _connections: Array[Dictionary] = []
var _timed_presentations: Dictionary = {}

func configure(owner_node: Node, visible_sink: Callable, input_sink: Callable, enabled := false) -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	host = owner_node
	record_visible = visible_sink
	record_input = input_sink
	set_enabled(enabled)

func set_enabled(enabled: bool) -> void:
	_enabled = enabled
	set_process_input(enabled)
	if not enabled:
		for connection: Dictionary in _connections:
			var control: Node = connection.node.get_ref()
			if is_instance_valid(control): control.disconnect(str(connection.signal), connection.callback)
		_connections.clear()
		_wired.clear()
		_control_context.clear()
		_timed_presentations.clear()
	if enabled and not RenderingServer.frame_post_draw.is_connected(_after_draw):
		RenderingServer.frame_post_draw.connect(_after_draw)
	elif not enabled and RenderingServer.frame_post_draw.is_connected(_after_draw):
		RenderingServer.frame_post_draw.disconnect(_after_draw)

func begin_shutdown() -> void:
	if _closing: return
	_closing = true
	set_enabled(false)

func _exit_tree() -> void:
	begin_shutdown()

func _input(event: InputEvent) -> void:
	if _closing or not _enabled or not record_input.is_valid(): return
	var data: Dictionary = {}
	if event is InputEventKey and not event.echo:
		data = {"device": "keyboard", "key": OS.get_keycode_string(event.keycode), "pressed": event.pressed,
			"unicode": event.unicode, "shift": event.shift_pressed, "ctrl": event.ctrl_pressed}
	elif event is InputEventMouseButton:
		data = {"device": "mouse", "button": event.button_index, "pressed": event.pressed,
			"position": [event.position.x, event.position.y], "double_click": event.double_click}
	elif event is InputEventJoypadButton:
		data = {"device": "joypad_button", "device_id": event.device,
			"button": event.button_index, "pressed": event.pressed, "pressure": event.pressure}
	elif event is InputEventJoypadMotion:
		data = {"device": "joypad_axis", "device_id": event.device,
			"axis": event.axis, "value": event.axis_value}
	if not data.is_empty():
		data.merge(_context())
		record_input.call(data)

func _context() -> Dictionary:
	if is_instance_valid(host.title_layer):
		var book: Control = host.title_layer.get_child(0)
		return book.page_trace_context()
	if is_instance_valid(host.ui): return host.ui.view_trace_context()
	return {"panel": "title"}

func _activation(control: Control, path: String) -> void:
	if _closing or not _enabled or not is_instance_valid(control): return
	var data: Dictionary = _control_context.get(control.get_instance_id(), {}).duplicate()
	data.merge({"device": "control_signal", "path": path,
		"text": _plain_text(control), "pressed": true})
	record_input.call(data)

func _wire(control: Control, context: Dictionary) -> void:
	var id := control.get_instance_id()
	_control_context[id] = context
	if _wired.has(id): return
	_wired[id] = true
	var path := str(host.get_path_to(control))
	if control is BaseButton:
		var callback := _activation.bind(control, path)
		control.pressed.connect(callback)
		_connections.append({"node": weakref(control), "signal": "pressed", "callback": callback})
	elif control is LineEdit:
		var callback := func(value: String) -> void:
			if _closing or not _enabled: return
			var data := _context()
			data.merge({"device": "text_control", "path": path, "value": value})
			record_input.call(data)
		control.text_changed.connect(callback)
		_connections.append({"node": weakref(control), "signal": "text_changed", "callback": callback})

func _after_draw() -> void:
	if _closing or not is_instance_valid(host): return
	host._timing("visible_trace_begin")
	_collect_after_draw()
	host._timing("visible_trace_end")

func _collect_after_draw() -> void:
	var items: Array = []
	# Scene callbacks cannot interleave this synchronous scan. Share its small
	# read-only input context, then copy before adding visible-frame payload.
	var context := _context()
	var viewport_rect: Rect2 = host.get_viewport().get_visible_rect()
	if is_instance_valid(host.title_layer):
		_scan_book(host.title_layer.get_child(0), items, context)
	elif is_instance_valid(host.ui):
		if str(context.get("panel", "")) == "casebook":
			_scan_book(host.ui.settings_ui.book, items, context)
		else: _scan(host.ui, viewport_rect, items, context)
	var data := context.duplicate()
	data["event"] = "visible_frame"
	data["view_schema"] = 3
	data["text_scope"] = "page-texture-content" if bool(context.get("projected_3d", false)) else "screen-control-content"
	data["texts"] = items
	data["objects"] = _scene_objects()
	if bool(context.get("projected_3d", false)):
		data["visibility_basis"] = "page-texture-membership; rendered-image-required-for-occlusion"
		data["screen_text_visibility"] = "not-inferred"
	if data == _last_data: return
	_last_data = data.duplicate(true)
	var signature := JSON.stringify(data)
	if signature == _last_frame: return
	_last_frame = signature
	data["frame"] = Engine.get_frames_drawn()
	record_visible.call(data)
	var presentation := str(data.get("presentation_id", ""))
	if presentation != "" and str(data.get("panel", "")) in ["dialogue", "spatial"] and not _timed_presentations.has(presentation):
		_timed_presentations[presentation] = true
		host._timing("local_choices_frame" if str(data.get("panel", "")) == "spatial" else "first_frame_drawn", {"presentation_id": presentation, "frame": data.frame})

func _scene_objects() -> Array:
	# Preparing a field under the title is not a player sighting. Partial
	# coverage during the reveal is described by its actual captured frames.
	if is_instance_valid(host.title_layer): return []
	if not is_instance_valid(host.field): return []
	var covered: Array[Rect2] = []
	if is_instance_valid(host.ui):
		for child in host.ui.get_children():
			if child is CanvasLayer and child.visible:
				for sub in child.get_children():
					if sub is Control and sub.is_visible_in_tree() and sub.modulate.a > 0.001:
						covered.append(sub.get_global_rect())
	return host.field.player_visible_objects(covered)

func _scan_book(book: Control, items: Array, context: Dictionary) -> void:
	# A page turn draws a frozen texture; current Controls belong to the next page.
	# Record the texture identity during motion, and settled page content afterwards.
	if bool(context.get("turning", false)): return
	var page: SubViewport = book.viewport
	_scan(page, Rect2(Vector2.ZERO, Vector2(page.size)), items, context)

func _scan(node: Node, clip: Rect2, items: Array, context: Dictionary) -> void:
	if node is CanvasLayer and not node.visible: return
	if node is CanvasItem and (not node.is_visible_in_tree() or node.modulate.a <= 0.001): return
	var child_clip := clip
	if node is Control:
		var control: Control = node
		var bounds := control.get_global_rect()
		var visible_bounds := bounds.intersection(clip)
		if not visible_bounds.has_area(): return
		_wire(control, context)
		var text := _plain_text(control)
		if text != "":
			var item := {"text": text, "content_clipped": false}
			if control.has_meta("player_source"): item.merge(control.get_meta("player_source"))
			if control is RichTextLabel:
				item["visible_characters"] = control.visible_characters
				item["complete"] = control.visible_characters < 0 or control.visible_characters >= control.get_total_character_count()
			items.append(item)
		if control.clip_contents or control is ScrollContainer: child_clip = visible_bounds
	for child in node.get_children(): _scan(child, child_clip, items, context)

func _plain_text(control: Control) -> String:
	if control is RichTextLabel: return control.get_parsed_text()
	if control is Label or control is Button: return control.atr(control.text)
	if control is LineEdit or control is TextEdit:
		return control.text if control.text != "" else control.atr(control.placeholder_text)
	return ""

