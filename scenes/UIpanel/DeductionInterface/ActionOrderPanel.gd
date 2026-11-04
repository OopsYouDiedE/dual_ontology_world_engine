extends PanelContainer
## Rearranging the canvas is local. Only submitted authorizes the physical plan.
signal submitted(order: Array[String])
signal validation_requested(order: Array[String])
signal closed

const Chrome := preload("res://scenes/UIpanel/FieldHUD/FieldChrome.gd")
const UIInput := preload("res://scenes/UIpanel/UIInput.gd")
var cards: Array = []
var rows: VBoxContainer
var message: Label
var _canvas: Control
var _tiles: Array[Control] = []
var _validate: Button
var _execute: Button
var _close: Button
var _caption: Label
var _validated_order: Array[String] = []
var _navigation = UIInput.new()
var _submitted := false
var _configured := false
var _checking := false
var _spec: Dictionary = {}

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	Chrome.apply(self, 17)
	add_theme_stylebox_override("panel", Chrome.box(0.97, Vector4(30, 26, 30, 24), true))

func configure(spec: Dictionary) -> void:
	_spec = spec.duplicate(true)
	set_anchors_and_offsets_preset(Control.PRESET_TOP_LEFT)
	custom_minimum_size = Vector2(880, 570)
	for child in get_children():
		remove_child(child)
		child.queue_free()
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 20)
	add_child(box)
	var header := HBoxContainer.new()
	header.add_theme_constant_override("separation", 16)
	box.add_child(header)
	var heading := VBoxContainer.new()
	heading.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(heading)
	_caption = Label.new()
	_caption.text = "脑内推演"
	_caption.add_theme_font_size_override("font_size", 12)
	_caption.add_theme_color_override("font_color", Chrome.GOLD)
	heading.add_child(_caption)
	var title := Label.new()
	title.text = str(spec.get("title", "行动顺序"))
	title.add_theme_font_size_override("font_size", 28)
	title.tooltip_text = str(spec.get("hint", ""))
	heading.add_child(title)
	_close = Button.new()
	_close.text = "×"
	_close.tooltip_text = "返回现场"
	Chrome.button(_close, true)
	_close.custom_minimum_size.x = 44
	_close.pressed.connect(_leave)
	header.add_child(_close)
	rows = VBoxContainer.new()
	rows.size_flags_vertical = Control.SIZE_EXPAND_FILL
	box.add_child(rows)
	_canvas = Control.new()
	_canvas.name = "PlanCanvas"
	_canvas.custom_minimum_size = Vector2(0, 340)
	_canvas.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_canvas.mouse_filter = Control.MOUSE_FILTER_IGNORE
	rows.add_child(_canvas)
	_canvas.draw.connect(_draw_links)
	_canvas.resized.connect(_layout_cards)
	var items: Variant = spec.get("cards", [])
	cards = items.duplicate(true) if items is Array else []
	message = Label.new()
	message.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	message.add_theme_font_size_override("font_size", 15)
	message.add_theme_color_override("font_color", Chrome.MUTED)
	message.custom_minimum_size.y = 22
	box.add_child(message)
	var controls := HBoxContainer.new()
	controls.alignment = BoxContainer.ALIGNMENT_END
	controls.add_theme_constant_override("separation", 16)
	box.add_child(controls)
	_validate = Button.new()
	_validate.text = "推演"
	Chrome.button(_validate)
	_validate.custom_minimum_size.x = 100
	_validate.pressed.connect(_request_validation)
	controls.add_child(_validate)
	_execute = Button.new()
	_execute.text = "执行计划"
	_execute.tooltip_text = "按此顺序行动"
	Chrome.button(_execute, true)
	_execute.pressed.connect(_submit_plan)
	controls.add_child(_execute)
	_configured = true
	refresh()
	if not get_viewport().size_changed.is_connected(center_in_viewport): get_viewport().size_changed.connect(center_in_viewport)
	if not visibility_changed.is_connected(_on_visibility): visibility_changed.connect(_on_visibility)
	center_in_viewport.call_deferred()
	hide()

func _on_visibility() -> void:
	if not _configured or not visible: return
	_submitted = false
	_checking = false
	_refresh_submission()
	center_in_viewport.call_deferred()
	if not _tiles.is_empty():
		var first: Button = _tiles[0].find_child("MoveNext", true, false)
		if first != null: first.grab_focus()

func center_in_viewport() -> void:
	if not _configured: return
	var screen := get_viewport_rect().size
	custom_minimum_size = Vector2(minf(920, screen.x - 48), minf(590, screen.y - 64))
	size = custom_minimum_size
	position = ((screen - size) * 0.5).max(Vector2(20, 20))
	_layout_cards.call_deferred()

func refresh() -> void:
	if not _configured: return
	for child in _canvas.get_children():
		_canvas.remove_child(child)
		child.queue_free()
	_tiles.clear()
	for index in range(cards.size()):
		var card: Dictionary = cards[index]
		var tile := PanelContainer.new()
		tile.name = "Step%d" % (index + 1)
		tile.add_theme_stylebox_override("panel", Chrome.box(0.8, Vector4(16, 12, 12, 12)))
		tile.set_drag_forwarding(_drag_card.bind(index), _can_drop_card.bind(index), _drop_card.bind(index))
		_canvas.add_child(tile)
		_tiles.append(tile)
		var content := VBoxContainer.new()
		content.add_theme_constant_override("separation", 12)
		tile.add_child(content)
		var headline := HBoxContainer.new()
		headline.add_theme_constant_override("separation", 12)
		content.add_child(headline)
		var index_label := Label.new()
		index_label.text = "%02d" % (index + 1)
		index_label.add_theme_font_size_override("font_size", 14)
		index_label.add_theme_color_override("font_color", Chrome.GOLD)
		headline.add_child(index_label)
		var label := Label.new()
		label.text = str(card.get("label", "动作"))
		label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		headline.add_child(label)
		var evidence := str(card.get("evidence_label", card.get("source_label", "")))
		if not evidence.is_empty():
			var note := Label.new()
			note.text = evidence
			note.add_theme_font_size_override("font_size", 13)
			note.add_theme_color_override("font_color", Chrome.MUTED)
			content.add_child(note)
		var controls := HBoxContainer.new()
		controls.alignment = BoxContainer.ALIGNMENT_END
		controls.add_theme_constant_override("separation", 6)
		content.add_child(controls)
		for direction in [-1, 1]:
			var button := Button.new()
			button.name = "MovePrevious" if direction < 0 else "MoveNext"
			button.text = "‹" if direction < 0 else "›"
			button.tooltip_text = "向前" if direction < 0 else "向后"
			button.custom_minimum_size = Vector2(40, 40)
			Chrome.button(button, true)
			button.disabled = index + direction < 0 or index + direction >= cards.size()
			button.pressed.connect(move_card.bind(index, direction))
			controls.add_child(button)
	_layout_cards.call_deferred()
	_refresh_submission()

func _layout_cards() -> void:
	if not is_instance_valid(_canvas): return
	var count := _tiles.size()
	if count == 0: return
	var columns := 2 if count > 1 and _canvas.size.x >= 570 else 1
	var row_count := ceili(float(count) / float(columns))
	var gap := Vector2(62, 34)
	var width := (_canvas.size.x - gap.x * float(columns - 1)) / float(columns)
	var height := maxf(128, (_canvas.size.y - gap.y * float(row_count - 1)) / float(row_count))
	_canvas.custom_minimum_size.y = maxf(320, height * float(row_count) + gap.y * float(row_count - 1))
	for index in range(count):
		var row := index / columns
		var column := index % columns
		if row % 2 == 1: column = columns - 1 - column
		_tiles[index].position = Vector2(float(column) * (width + gap.x), float(row) * (height + gap.y))
		_tiles[index].size = Vector2(width, height)
	_canvas.queue_redraw()

func _draw_links() -> void:
	var accent := Chrome.GREEN if _validated_order == _order() and not _validated_order.is_empty() else Chrome.GOLD
	for index in range(_tiles.size() - 1):
		var from_rect := Rect2(_tiles[index].position, _tiles[index].size)
		var to_rect := Rect2(_tiles[index + 1].position, _tiles[index + 1].size)
		var vertical := absf(from_rect.position.y - to_rect.position.y) > 20
		var start := from_rect.get_center()
		var end := to_rect.get_center()
		if vertical:
			start.y = from_rect.end.y + 4
			end.y = to_rect.position.y - 4
		else:
			start.x = from_rect.end.x + 4 if end.x > start.x else from_rect.position.x - 4
			end.x = to_rect.position.x - 4 if end.x > from_rect.get_center().x else to_rect.end.x + 4
		_canvas.draw_line(start, end, Color(accent, 0.65), 1.5, true)
		var direction := (end - start).normalized()
		var perpendicular := Vector2(-direction.y, direction.x)
		_canvas.draw_colored_polygon(PackedVector2Array([end, end - direction * 7 + perpendicular * 4, end - direction * 7 - perpendicular * 4]), Color(accent, 0.8))

func move_card(index: int, direction: int) -> void:
	var destination := index + direction
	if index < 0 or index >= cards.size() or destination < 0 or destination >= cards.size(): return
	var other: Variant = cards[destination]
	cards[destination] = cards[index]
	cards[index] = other
	_invalidate_plan()
	refresh()

func _drag_card(_position: Vector2, index: int) -> Variant:
	if index < 0 or index >= cards.size(): return null
	var preview := Label.new()
	preview.text = str(cards[index].get("label", "动作"))
	preview.add_theme_color_override("font_color", Chrome.INK)
	_tiles[index].set_drag_preview(preview)
	return {"type": "action_order", "index": index, "owner": get_instance_id()}

func _can_drop_card(_position: Vector2, data: Variant, _index: int) -> bool:
	return data is Dictionary and str(data.get("type", "")) == "action_order" and int(data.get("owner", -1)) == get_instance_id()

func _drop_card(_position: Vector2, data: Variant, destination: int) -> void:
	if not _can_drop_card(_position, data, destination): return
	var index := int(data.index)
	if index == destination or index < 0 or index >= cards.size(): return
	var card: Variant = cards[index]
	cards.remove_at(index)
	cards.insert(destination, card)
	_invalidate_plan()
	refresh()

func _invalidate_plan() -> void:
	_validated_order.clear()
	_submitted = false
	_checking = false
	message.text = ""
	_caption.text = "脑内推演"

func _order() -> Array[String]:
	var order: Array[String] = []
	for card: Dictionary in cards: order.append(str(card.get("action", "")))
	return order

func _request_validation() -> void:
	if _checking or cards.is_empty(): return
	_checking = true
	_validate.disabled = true
	_caption.text = "推演中"
	validation_requested.emit(_order())

func set_validation_result(error: String) -> void:
	_checking = false
	if error.is_empty():
		_validated_order = _order()
		_caption.text = "与已知记录相容"
		message.text = ""
	else:
		_validated_order.clear()
		_caption.text = "存在矛盾"
		message.text = _readable_message(error)
		message.add_theme_color_override("font_color", Chrome.RED)
	_refresh_submission()
	_canvas.queue_redraw()

func _readable_message(text: String) -> String:
	var result := text
	for card: Dictionary in cards:
		var id := str(card.get("action", ""))
		if not id.is_empty(): result = result.replace(id, str(card.get("label", "该动作")))
	return result

func _refresh_submission() -> void:
	if not _configured: return
	var can_validate := not validation_requested.get_connections().is_empty()
	_validate.visible = can_validate
	_validate.disabled = _checking or cards.is_empty()
	_execute.disabled = _submitted or cards.is_empty() or (can_validate and _validated_order != _order())

func _submit_plan() -> void:
	if _execute.disabled: return
	_submitted = true
	_execute.disabled = true
	submitted.emit(_order())

func _leave() -> void:
	hide()
	_navigation.reset()
	closed.emit()

func _input(event: InputEvent) -> void:
	if not visible or not _configured: return
	if UIInput.is_back(event):
		_leave()
		get_viewport().set_input_as_handled()
	elif _navigation.handle(self, event):
		get_viewport().set_input_as_handled()

func _process(delta: float) -> void:
	if visible and _configured:
		_navigation.tick(self, delta)
		if not message.text.is_empty(): message.text = _readable_message(message.text)
