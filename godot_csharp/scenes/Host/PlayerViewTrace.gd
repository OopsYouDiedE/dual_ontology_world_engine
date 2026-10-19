extends Node
## 被动观察已绘制的界面。只记录可见内容与输入，不推进剧情，也不把缓存中的文字当作已显示。

var host: Node
var record_visible: Callable
var record_input: Callable
var _last_frame := ""
var _wired: Dictionary = {}
var _control_context: Dictionary = {}
var _closing := false
var _timed_presentations: Dictionary = {}

func configure(owner_node: Node, visible_sink: Callable, input_sink: Callable) -> void:
	host = owner_node
	record_visible = visible_sink
	record_input = input_sink
	RenderingServer.frame_post_draw.connect(_after_draw)

func begin_shutdown() -> void:
	if _closing: return
	_closing = true
	if RenderingServer.frame_post_draw.is_connected(_after_draw):
		RenderingServer.frame_post_draw.disconnect(_after_draw)

func _exit_tree() -> void:
	begin_shutdown()

func _input(event: InputEvent) -> void:
	if _closing or not record_input.is_valid(): return
	var data: Dictionary = {}
	if event is InputEventKey and not event.echo:
		data = {"device": "keyboard", "key": OS.get_keycode_string(event.keycode), "pressed": event.pressed,
			"unicode": event.unicode, "shift": event.shift_pressed, "ctrl": event.ctrl_pressed}
	elif event is InputEventMouseButton:
		data = {"device": "mouse", "button": event.button_index, "pressed": event.pressed,
			"position": [event.position.x, event.position.y], "double_click": event.double_click}
	if not data.is_empty():
		data.merge(_context())
		record_input.call(data)

func _context() -> Dictionary:
	if is_instance_valid(host.ui): return host.ui.view_trace_context()
	return {"panel": "title"}

func _activation(control: Control, path: String) -> void:
	if _closing or not is_instance_valid(control): return
	# Other signal listeners may have replaced the page before this listener runs.
	var data: Dictionary = _control_context.get(control.get_instance_id(), {}).duplicate()
	data.merge({"device": "control_signal", "path": path,
		"text": _plain_text(control), "pressed": true})
	# pressed can come from mouse, keyboard, accessibility or a screenshot driver.
	record_input.call(data)

func _wire(control: Control) -> void:
	var id := control.get_instance_id()
	_control_context[id] = _context()
	if _wired.has(id): return
	_wired[id] = true
	var path := str(host.get_path_to(control))
	if control is BaseButton: control.pressed.connect(_activation.bind(control, path))
	elif control is LineEdit:
		control.text_changed.connect(func(value: String) -> void:
			if _closing: return
			var data := _context()
			data.merge({"device": "text_control", "path": path, "value": value})
			record_input.call(data))

func _after_draw() -> void:
	if _closing or not is_instance_valid(host): return
	var items: Array = []
	var viewport_rect: Rect2 = host.get_viewport().get_visible_rect()
	if is_instance_valid(host.title_layer):
		_scan(host.title_layer, viewport_rect, items)
	elif is_instance_valid(host.ui):
		var ui: Node = host.ui
		# HUD (10), paper (15), dialogue (20), then its later history sibling.
		# The dim overlay is translucent; only the actual panels cover lower text.
		var history_cover: Array[Rect2] = []
		if ui.dialogue.visible: _add_cover(ui.dialogue.history_panel, history_cover)
		var dialogue_cover: Array[Rect2] = history_cover.duplicate()
		if ui.dialogue.visible:
			_add_cover(ui.dialogue.panel, dialogue_cover)
			if is_instance_valid(ui.dialogue.portrait_frame):
				_add_cover(ui.dialogue.portrait_frame, dialogue_cover)
		var lower_cover: Array[Rect2] = dialogue_cover.duplicate()
		if ui.modal_layer.visible and is_instance_valid(ui.modal_root):
			_add_cover(ui.modal_root.get_node_or_null("PanelContainer"), lower_cover)
		if is_instance_valid(host.field): _scan(host.field, viewport_rect, items, lower_cover)
		_scan(ui.hud, viewport_rect, items, lower_cover)
		if ui.modal_layer.visible and is_instance_valid(ui.modal_root):
			_scan(ui.modal_root, viewport_rect, items, dialogue_cover)
		if ui.dialogue.visible:
			_scan(ui.dialogue.panel, viewport_rect, items, history_cover)
			_scan(ui.dialogue.history_panel, viewport_rect, items)
	var data := _context()
	data["event"] = "visible_frame"
	data["view_schema"] = 2
	data["texts"] = items
	data["objects"] = _scene_objects()
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
	if not is_instance_valid(host.field): return []
	var covered: Array[Rect2] = []
	if is_instance_valid(host.ui):
		var ui: Node = host.ui
		if ui.dialogue.history_panel.visible:
			covered.append(ui.dialogue.history_panel.get_global_rect())
		if is_instance_valid(ui.modal_root):
			var paper: Control = ui.modal_root.get_node_or_null("PanelContainer")
			if paper != null and paper.is_visible_in_tree(): covered.append(paper.get_global_rect())
		if ui.dialogue.active and ui.dialogue.panel.is_visible_in_tree():
			covered.append(ui.dialogue.panel.get_global_rect())
			_add_cover(ui.dialogue.portrait_frame, covered)
	return host.field.player_visible_objects(covered)

func _add_cover(control: Control, covered: Array[Rect2]) -> void:
	if is_instance_valid(control) and control.is_visible_in_tree() and control.modulate.a > 0.001:
		covered.append(control.get_global_rect())

func _uncovered_rects(bounds: Rect2, covered: Array[Rect2]) -> Array[Rect2]:
	var regions: Array[Rect2] = [bounds]
	for cover in covered:
		var remaining: Array[Rect2] = []
		for region in regions:
			var overlap := region.intersection(cover)
			if not overlap.has_area():
				remaining.append(region)
				continue
			# Disjoint strips retain all uncovered portions without double-counting rows.
			for strip in [Rect2(region.position, Vector2(region.size.x, overlap.position.y - region.position.y)),
				Rect2(Vector2(region.position.x, overlap.end.y), Vector2(region.size.x, region.end.y - overlap.end.y)),
				Rect2(Vector2(region.position.x, overlap.position.y), Vector2(overlap.position.x - region.position.x, overlap.size.y)),
				Rect2(Vector2(overlap.end.x, overlap.position.y), Vector2(region.end.x - overlap.end.x, overlap.size.y))]:
				if strip.has_area(): remaining.append(strip)
		regions = remaining
	return regions

func _scan(node: Node, clip: Rect2, items: Array, covered: Array[Rect2] = []) -> void:
	if node is CanvasLayer and not node.visible: return
	if node is CanvasItem and (not node.is_visible_in_tree() or node.modulate.a <= 0.001): return
	var child_clip := clip
	if node is Control:
		var control: Control = node
		var bounds := control.get_global_rect()
		var visible_bounds := bounds.intersection(clip)
		if not visible_bounds.has_area(): return
		_wire(control)
		var regions := _uncovered_rects(visible_bounds, covered)
		var text := _visible_text(control, regions)
		if text != "":
			var fully_exposed := regions.size() == 1 and regions[0].encloses(bounds)
			var item := {"text": text, "content_clipped": not fully_exposed}
			if control.has_meta("player_source"): item.merge(control.get_meta("player_source"))
			if control is RichTextLabel:
				item["content_clipped"] = item["content_clipped"] or control.get_content_height() > bounds.size.y + 1
				item["visible_characters"] = control.visible_characters
				item["complete"] = control.visible_characters < 0 or control.visible_characters >= control.get_total_character_count()
			items.append(item)
		if control.clip_contents or control is ScrollContainer: child_clip = visible_bounds
	for child in node.get_children(): _scan(child, child_clip, items, covered)

func _plain_text(control: Control) -> String:
	if control is RichTextLabel: return control.get_parsed_text()
	if control is Label or control is Button: return control.atr(control.text)
	if control is LineEdit or control is TextEdit:
		return control.text if control.text != "" else control.atr(control.placeholder_text)
	return ""

func _visible_text(control: Control, regions: Array[Rect2]) -> String:
	if regions.is_empty(): return ""
	var text := _plain_text(control)
	var bounds := control.get_global_rect()
	var fully_exposed := regions.size() == 1 and regions[0].encloses(bounds)
	if control is RichTextLabel:
		var rich: RichTextLabel = control
		if rich.visible_characters >= 0: text = text.substr(0, rich.visible_characters)
		if fully_exposed and rich.get_content_height() <= bounds.size.y + 1: return text
		# Keep only fully visible wrapped rows; partly clipped glyphs are not claimed as read.
		var rows: Array[String] = []
		var scroll: float = rich.get_v_scroll_bar().value if rich.scroll_active else 0.0
		for index in rich.get_line_count():
			var start_y: float = bounds.position.y + rich.get_line_offset(index) - scroll
			var end_y: float = start_y + rich.get_line_height(index)
			var row := Rect2(Vector2(bounds.position.x, start_y), Vector2(bounds.size.x, end_y - start_y))
			var row_visible := false
			for region in regions:
				if region.encloses(row):
					row_visible = true
					break
			if not row_visible: continue
			var span: Vector2i = rich.get_line_range(index)
			if span.x < text.length(): rows.append(text.substr(span.x, maxi(0, mini(span.y, text.length()) - span.x)))
		return "\n".join(rows)
	# Plain controls have no wrapped-row ranges; partial exposure cannot prove their full text.
	if not fully_exposed: return ""
	if control is Label and control.visible_characters >= 0: text = text.substr(0, control.visible_characters)
	return text
