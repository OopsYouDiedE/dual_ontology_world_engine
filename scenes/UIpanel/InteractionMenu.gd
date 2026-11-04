@tool
extends Control
## Target-side action slips. Every option and cost is supplied by the live host.
signal action_selected(action_id: String, target_id: String)
signal menu_closed
signal blocked_feedback(reason: String)
signal word_magic_applied(target_id: String, formula: String)
signal pocket_item_used(target_id: String, item_id: String)
signal parameter_action_requested(action_id: String, target_id: String, parameters: Dictionary)
signal cost_preview_requested(current: int, cost: int)
signal cost_preview_cleared

const Chrome := preload("res://scenes/UIpanel/FieldHUD/FieldChrome.gd")
const UIInput := preload("res://scenes/UIpanel/UIInput.gd")
const MAX_FIRST_LEVEL_ITEMS := 5
const MENU_WIDTH := 400.0
var backdrop: Control
var menu_panel: Control
var target_title: Label
var target_location: Label
var close_button: Button
var submenu_back_btn: Button
var submenu_title: Label
var drawer_container: Control
var drawer_title: Label
var drawer_slots_box: HBoxContainer
var drawer_close_btn: Button
var drawer_selection: Label
var drawer_submit: Button
var _drawer_scroll: ScrollContainer
var _current_target_id := ""
var _target_name := ""
var _location_desc := ""
var _actions_data: Array = []
var _source_data: Dictionary = {}
var _is_open := false
var _active_parent_action: Dictionary = {}
var _submenu_stack: Array[Dictionary] = []
var _list_page := 0
var _in_submenu := false
var _drawer_open := false
var _drawer_mode := ""
var _drawer_action: Dictionary = {}
var _selected_drawer_item: Dictionary = {}
var _spell_words: Array[String] = []
var _parameter_values: Dictionary = {}
var _target_screen := Vector2.ZERO
var _left_side := false
var _navigation = UIInput.new()
var _entrance: Tween
var _return_focus: WeakRef
var _rows: Array[Button] = []
var _row_items: Array[Dictionary] = []
var _nodes := PackedVector2Array()
var _focused_index := -1
var _drawer_index := -1
var _scale := 1.0
var _pivot := Vector2.ZERO
var _empty: Label
var _more: Button

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	Chrome.apply(self, 30)
	backdrop = Control.new()
	backdrop.name = "Backdrop"
	add_child(backdrop)
	backdrop.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	backdrop.gui_input.connect(_on_backdrop_gui_input)
	menu_panel = Control.new()
	menu_panel.name = "MenuPanel"
	menu_panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(menu_panel)
	menu_panel.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	target_title = _label(menu_panel, "TargetTitle")
	target_title.hide()
	target_location = _label(menu_panel, "TargetLocation")
	target_location.hide()
	close_button = _quiet_button(menu_panel, "CloseButton", "×", close_menu)
	close_button.hide()
	submenu_back_btn = _quiet_button(menu_panel, "SubmenuBackBtn", "‹", _back_to_primary_list)
	submenu_title = _label(menu_panel, "SubmenuTitle")
	submenu_back_btn.hide()
	submenu_title.hide()
	_empty = _label(menu_panel, "EmptyActions")
	_empty.hide()
	_more = _quiet_button(menu_panel, "MoreActions", "更多  ›", func():
		_list_page += 1
		_close_drawer(false)
		_render_primary_list()
		_focus_first.call_deferred()
	)
	_more.hide()
	_build_drawer()
	resized.connect(_place_menu)
	var settings := get_node_or_null("/root/GameSettings")
	if settings != null: settings.setting_changed.connect(_on_setting_changed)
	visible = false

func _label(parent: Node, id: String) -> Label:
	var label := Label.new()
	label.name = id
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	label.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	parent.add_child(label)
	return label

func _quiet_button(parent: Node, id: String, caption: String, action: Callable) -> Button:
	var button := Button.new()
	button.name = id
	button.text = caption
	button.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	Chrome.button(button, true)
	parent.add_child(button)
	button.pressed.connect(action)
	return button

func _build_drawer() -> void:
	drawer_container = Control.new()
	drawer_container.name = "DrawerContainer"
	drawer_container.mouse_filter = Control.MOUSE_FILTER_IGNORE
	menu_panel.add_child(drawer_container)
	drawer_title = _label(drawer_container, "DrawerTitle")
	drawer_title.hide()
	_drawer_scroll = ScrollContainer.new()
	_drawer_scroll.name = "SlotsScroll"
	_drawer_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_AUTO
	_drawer_scroll.vertical_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	drawer_container.add_child(_drawer_scroll)
	drawer_slots_box = HBoxContainer.new()
	drawer_slots_box.name = "DrawerSlotsBox"
	drawer_slots_box.add_theme_constant_override("separation", 8)
	_drawer_scroll.add_child(drawer_slots_box)
	drawer_selection = _label(drawer_container, "DrawerSelection")
	drawer_selection.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	drawer_selection.clip_text = true
	drawer_submit = _quiet_button(drawer_container, "DrawerSubmit", "使用", _submit_drawer)
	drawer_close_btn = _quiet_button(drawer_container, "DrawerCloseBtn", "‹", _close_drawer)
	drawer_close_btn.tooltip_text = "收回"
	drawer_container.hide()

func _on_setting_changed(section: String, key: String, _value: Variant) -> void:
	if section != "accessibility" and not (section == "graphics" and key == "ui_scale"): return
	Chrome.apply(self, 30)
	if _is_open:
		_render_primary_list()
		_place_menu()

func is_menu_open() -> bool:
	return _is_open

func get_current_target_id() -> String:
	return _current_target_id

func open_for_target(data: Dictionary, target_screen_pos := Vector2.ZERO) -> void:
	if not is_node_ready(): return
	_source_data = data.duplicate(true)
	_current_target_id = str(data.get("site", data.get("target_id", data.get("id", ""))))
	_target_name = str(data.get("target_name", data.get("name", data.get("title", ""))))
	_location_desc = str(data.get("status", data.get("location", data.get("room", ""))))
	var choices: Variant = data.get("options", data.get("actions", []))
	_actions_data = choices.duplicate(true) if choices is Array else []
	target_title.text = _target_name
	target_location.text = _location_desc
	_submenu_stack.clear()
	_active_parent_action.clear()
	_list_page = 0
	_in_submenu = false
	_target_screen = target_screen_pos
	var position_value: Variant = data.get("screen_position", data.get("screen_pos"))
	if position_value is Vector2: _target_screen = position_value
	elif position_value is Array and position_value.size() >= 2:
		_target_screen = Vector2(float(position_value[0]), float(position_value[1]))
	elif position_value is Dictionary:
		_target_screen = Vector2(float(position_value.get("x", 0)), float(position_value.get("y", 0)))
	if _target_screen == Vector2.ZERO:
		_target_screen = get_local_mouse_position()
		if _target_screen.x < 18 or _target_screen.y < 18: _target_screen = size * Vector2(0.56, 0.52)
	var focus := get_viewport().gui_get_focus_owner()
	_return_focus = weakref(focus) if focus != null else null
	_close_drawer(false)
	_navigation.reset()
	_is_open = true
	visible = true
	_render_primary_list()
	if is_instance_valid(_entrance): _entrance.kill()
	menu_panel.modulate.a = 0.0
	_entrance = create_tween()
	_entrance.tween_property(menu_panel, "modulate:a", 1.0, 0.14)
	_place_menu.call_deferred()
	_focus_first.call_deferred()

func close_menu() -> void:
	if not _is_open: return
	_is_open = false
	visible = false
	_navigation.reset()
	_close_drawer(false)
	if is_instance_valid(_entrance): _entrance.kill()
	cost_preview_cleared.emit()
	menu_closed.emit()
	if _return_focus != null:
		var previous: Variant = _return_focus.get_ref()
		if previous is Control and previous.is_visible_in_tree() and previous.focus_mode != Control.FOCUS_NONE:
			previous.grab_focus()
	_return_focus = null

func _on_backdrop_gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		close_menu()
		accept_event()

func _input(event: InputEvent) -> void:
	if not _is_open or Engine.is_editor_hint(): return
	if UIInput.is_back(event, false):
		if UIInput.is_back(event):
			if _drawer_open: _close_drawer()
			elif _in_submenu or _list_page > 0: _back_to_primary_list()
			else: close_menu()
		get_viewport().set_input_as_handled()
	elif _navigation.handle(menu_panel, event):
		get_viewport().set_input_as_handled()

func _process(delta: float) -> void:
	if _is_open: _navigation.tick(menu_panel, delta)

func _children(item: Dictionary) -> Array:
	var value: Variant = item.get("options", item.get("children", []))
	return value if value is Array else []

func _render_primary_list() -> void:
	for row in _rows:
		menu_panel.remove_child(row)
		row.queue_free()
	_rows.clear()
	_row_items.clear()
	_nodes.clear()
	_focused_index = -1
	_in_submenu = not _submenu_stack.is_empty()
	_active_parent_action = _submenu_stack.back() if _in_submenu else {}
	var items: Array = _children(_active_parent_action) if _in_submenu else _actions_data
	var page_size: int = 4 if items.size() > MAX_FIRST_LEVEL_ITEMS else MAX_FIRST_LEVEL_ITEMS
	var start: int = _list_page * page_size
	if start >= items.size():
		_list_page = 0
		start = 0
	var count: int = mini(page_size, items.size() - start)
	for i in range(count):
		if items[start + i] is Dictionary:
			_row_items.append(items[start + i])
			_rows.append(_action_button(items[start + i], _rows.size()))
	_more.visible = start + count < items.size()
	_empty.visible = items.is_empty()
	_empty.text = "暂无可用动作" if items.is_empty() else ""
	submenu_back_btn.visible = _in_submenu or _list_page > 0
	submenu_title.visible = submenu_back_btn.visible
	submenu_title.text = str(_active_parent_action.get("label", _active_parent_action.get("text", ""))) if _in_submenu else "%d / %d" % [_list_page + 1, maxi(1, ceili(float(items.size()) / page_size))]
	_place_menu()

func _action_button(item: Dictionary, index: int) -> Button:
	var button := Button.new()
	button.name = "Action_" + str(index)
	var available := bool(item.get("available", not bool(item.get("disabled", false))))
	var label := str(item.get("label", item.get("text", item.get("name", item.get("id", "")))))
	var reason := str(item.get("reason", item.get("lock_hint", "")))
	button.text = label
	button.alignment = HORIZONTAL_ALIGNMENT_LEFT
	button.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	button.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	button.clip_text = true
	button.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	button.focus_mode = Control.FOCUS_ALL
	button.set_meta("available", available)
	button.tooltip_text = label + ("\n" + reason if not reason.is_empty() else "")
	var background := Control.new()
	background.name = "CutCornerSlip"
	background.mouse_filter = Control.MOUSE_FILTER_IGNORE
	background.show_behind_parent = true
	button.add_child(background)
	background.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	background.draw.connect(func(): _draw_slip(background, button, available))
	for state in ["normal", "hover", "pressed", "focus", "disabled", "hover_pressed"]:
		var blank := StyleBoxEmpty.new()
		blank.content_margin_left = 44
		blank.content_margin_right = 142
		button.add_theme_stylebox_override(state, blank)
	for state in ["font_color", "font_hover_color", "font_pressed_color", "font_focus_color"]:
		button.add_theme_color_override(state, Chrome.INK if available else Color(Chrome.MUTED, 0.58))
	var cost := _label(button, "ActionCost")
	cost.text = _cost_text(item.get("cost", item.get("execution_cost", ""))) if available else reason
	cost.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	cost.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	cost.clip_text = true
	cost.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	cost.add_theme_color_override("font_color", Color(Chrome.INK, 0.9) if available else Color(Chrome.MUTED, 0.7))
	cost.set_anchors_and_offsets_preset(Control.PRESET_RIGHT_WIDE)
	cost.offset_left = -146
	cost.offset_right = -24
	menu_panel.add_child(button)
	button.pressed.connect(func(): _choose_action(item, index))
	button.mouse_entered.connect(func():
		_focused_index = index
		_preview_cost(item)
		background.queue_redraw()
		queue_redraw()
	)
	button.mouse_exited.connect(func():
		cost_preview_cleared.emit()
		background.queue_redraw()
	)
	button.focus_entered.connect(func():
		_focused_index = index
		_preview_cost(item)
		background.queue_redraw()
		queue_redraw()
	)
	button.focus_exited.connect(func():
		cost_preview_cleared.emit()
		background.queue_redraw()
	)
	button.button_down.connect(background.queue_redraw)
	button.button_up.connect(background.queue_redraw)
	button.resized.connect(background.queue_redraw)
	return button

func _draw_slip(canvas: Control, button: Button, available: bool) -> void:
	var s: Vector2 = canvas.size
	if s.x < 1 or s.y < 1: return
	var cut: float = minf(s.y * 0.45, 26.0 * _scale)
	var points := PackedVector2Array([Vector2(cut, 0), Vector2(s.x, 0), Vector2(s.x, s.y), Vector2(cut, s.y), Vector2(0, s.y * 0.5)])
	if _left_side:
		points = PackedVector2Array([Vector2(0, 0), Vector2(s.x - cut, 0), Vector2(s.x, s.y * 0.5), Vector2(s.x - cut, s.y), Vector2(0, s.y)])
	var highlighted := button.has_focus() or button.is_hovered() or button.button_pressed
	var base := Color("494336") if highlighted else Color("3d3a31")
	base.a = Chrome.opacity(self, 0.82 if highlighted else 0.64) if available else 0.42
	var colors := PackedColorArray()
	for point in points:
		var far_edge: float = point.x / maxf(s.x, 1.0) if not _left_side else 1.0 - point.x / maxf(s.x, 1.0)
		colors.append(Color(base, base.a * lerpf(1.0, 0.55, far_edge)))
	canvas.draw_polygon(points, colors)
	var outline: PackedVector2Array = points.duplicate()
	outline.append(points[0])
	canvas.draw_polyline(outline, Color(Chrome.GOLD, 0.7 if highlighted else 0.33), maxf(1, _scale), true)

func _choose_action(item: Dictionary, index: int) -> void:
	if not bool(item.get("available", not bool(item.get("disabled", false)))):
		blocked_feedback.emit(str(item.get("reason", item.get("lock_hint", "暂不可用"))))
		return
	var mode := str(item.get("drawer", ""))
	if _drawer_open and index == _drawer_index and not drawer_submit.disabled:
		_submit_drawer()
	elif not mode.is_empty():
		_drawer_index = index
		_open_drawer(mode, item)
	elif item.get("parameters") is Dictionary and not item.parameters.is_empty():
		_drawer_index = index
		_open_drawer("parameters", item)
	elif not _children(item).is_empty():
		_submenu_stack.append(item)
		_list_page = 0
		_close_drawer(false)
		_render_primary_list()
		_focus_first.call_deferred()
	else:
		var action_id := str(item.get("id", ""))
		if not action_id.is_empty():
			action_selected.emit(action_id, _current_target_id)
			close_menu()

func _cost_text(value: Variant) -> String:
	if value is String: return value.replace("耗时", "").replace("精力", "").strip_edges()
	if value is int or value is float: return "%s分" % str(float(value)).trim_suffix(".0") if float(value) > 0 else ""
	if value is Dictionary:
		var parts: Array[String] = []
		var minutes: Variant = value.get("time", value.get("minutes"))
		var stamina: Variant = value.get("stamina", value.get("energy", value.get("ap")))
		if (minutes is int or minutes is float) and float(minutes) > 0: parts.append("%s分" % str(float(minutes)).trim_suffix(".0"))
		if (stamina is int or stamina is float) and float(stamina) > 0: parts.append("−%s" % str(float(stamina)).trim_suffix(".0"))
		return " · ".join(parts)
	return ""

func _preview_cost(item: Dictionary) -> void:
	var cost: Variant = item.get("cost", item.get("execution_cost", {}))
	if cost is Dictionary:
		var amount: Variant = cost.get("stamina", cost.get("energy", cost.get("ap", 0)))
		if (amount is int or amount is float) and float(amount) > 0:
			var remaining: Variant = item.get("remaining_ap", _source_data.get("remaining_ap", _source_data.get("stamina", -1)))
			cost_preview_requested.emit(int(remaining) if remaining is int or remaining is float else -1, ceili(float(amount)))
			return
	cost_preview_cleared.emit()

func _back_to_primary_list() -> void:
	_close_drawer(false)
	if _list_page > 0: _list_page = 0
	elif not _submenu_stack.is_empty(): _submenu_stack.pop_back()
	_render_primary_list()
	_focus_first.call_deferred()

func _open_drawer(mode: String, action: Dictionary = {}) -> void:
	_drawer_open = true
	_drawer_mode = mode
	_drawer_action = action
	_selected_drawer_item.clear()
	_spell_words.clear()
	_parameter_values.clear()
	for child in drawer_slots_box.get_children():
		drawer_slots_box.remove_child(child)
		child.queue_free()
	drawer_selection.text = ""
	drawer_submit.disabled = true
	drawer_submit.text = "施加" if mode == "word_magic" else "使用" if mode == "items" else "确定"
	drawer_title.text = str(action.get("label", ""))
	drawer_container.show()
	if mode == "parameters":
		_build_parameters(action.get("parameters", {}))
	else:
		var entries := _children(action)
		if entries.is_empty():
			var raw: Variant = action.get("items", action.get("words", _source_data.get("pocket", _source_data.get("items", [])) if mode == "items" else _source_data.get("word_options", _source_data.get("words", []))))
			if raw is Array: entries = raw
		for value in entries:
			var entry: Dictionary = value if value is Dictionary else {"id": str(value), "label": str(value)}
			var slot := Button.new()
			slot.name = "PocketSlot_" + str(drawer_slots_box.get_child_count())
			slot.toggle_mode = true
			slot.text = "" if mode == "items" else str(entry.get("label", entry.get("name", entry.get("word", entry.get("id", "")))))
			slot.tooltip_text = str(entry.get("label", entry.get("name", entry.get("title", entry.get("id", ""))))) + ("\n" + str(entry.get("reason", "")) if entry.has("reason") else "")
			slot.disabled = not bool(entry.get("available", not bool(entry.get("disabled", false))))
			Chrome.button(slot)
			for state in ["normal", "hover", "pressed", "hover_pressed", "focus", "disabled"]:
				var style := Chrome.box(0.68, Vector4(5, 5, 5, 5), state in ["pressed", "hover_pressed", "focus"])
				style.border_width_left = 1
				style.border_width_right = 1
				style.border_width_top = 1
				style.border_width_bottom = 1
				style.border_color = Color(Chrome.GOLD, 0.92 if state in ["pressed", "hover_pressed", "focus"] else 0.54)
				slot.add_theme_stylebox_override(state, style)
			drawer_slots_box.add_child(slot)
			if mode == "items":
				var icon := Chrome.item_icon(entry, Vector2(74, 74) * _scale)
				slot.add_child(icon)
				icon.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
				icon.offset_left = 6 * _scale
				icon.offset_right = -6 * _scale
				icon.offset_top = 6 * _scale
				icon.offset_bottom = -6 * _scale
			slot.pressed.connect(func(): _select_drawer_entry(entry, slot))
		if entries.is_empty():
			var empty := _label(drawer_slots_box, "EmptyPocket")
			empty.text = "口袋是空的" if mode == "items" else "尚未掌握言灵"
			empty.add_theme_color_override("font_color", Chrome.MUTED)
	_place_menu()
	_focus_drawer.call_deferred()

func _select_drawer_entry(entry: Dictionary, slot: Button) -> void:
	if _drawer_mode == "word_magic" and bool(entry.get("token", _drawer_action.get("compose", false))):
		var word := str(entry.get("word", entry.get("id", entry.get("label", ""))))
		if slot.button_pressed: _spell_words.append(word)
		else: _spell_words.erase(word)
		drawer_selection.text = " ".join(_spell_words)
		drawer_submit.disabled = _spell_words.is_empty()
	else:
		for child in drawer_slots_box.get_children():
			if child is Button: child.set_pressed_no_signal(child == slot)
		_selected_drawer_item = entry
		drawer_selection.text = str(entry.get("label", entry.get("name", entry.get("title", entry.get("formula", entry.get("id", ""))))))
		drawer_submit.disabled = false
	_place_menu()

func _build_parameters(specification: Dictionary) -> void:
	for key in specification:
		var spec: Variant = specification[key]
		if spec is not Dictionary: continue
		var column := VBoxContainer.new()
		drawer_slots_box.add_child(column)
		var label := _label(column, "ParameterLabel")
		label.text = str(spec.get("label", key))
		var value := SpinBox.new()
		value.min_value = float(spec.get("min", 0))
		value.max_value = float(spec.get("max", 60))
		value.step = float(spec.get("step", 1))
		value.value = float(spec.get("value", value.min_value))
		value.suffix = str(spec.get("unit", ""))
		value.custom_minimum_size = Vector2(130, 48) * _scale
		column.add_child(value)
		_parameter_values[str(key)] = value.value
		value.value_changed.connect(func(number: float): _parameter_values[str(key)] = number)
	drawer_submit.disabled = _parameter_values.is_empty()

func _submit_drawer() -> void:
	if not _drawer_open or drawer_submit.disabled: return
	drawer_submit.disabled = true
	if _drawer_mode == "parameters":
		parameter_action_requested.emit(str(_drawer_action.get("id", "")), _current_target_id, _parameter_values.duplicate())
	elif not str(_selected_drawer_item.get("action_id", "")).is_empty():
		action_selected.emit(str(_selected_drawer_item.action_id), _current_target_id)
	elif _drawer_mode == "items":
		pocket_item_used.emit(_current_target_id, str(_selected_drawer_item.get("item_id", _selected_drawer_item.get("id", ""))))
	elif _drawer_mode == "word_magic":
		var formula := " ".join(_spell_words) if not _spell_words.is_empty() else str(_selected_drawer_item.get("formula", _selected_drawer_item.get("word", _selected_drawer_item.get("id", ""))))
		word_magic_applied.emit(_current_target_id, formula)
	close_menu()

func _close_drawer(restore_focus := true) -> void:
	_drawer_open = false
	_drawer_mode = ""
	_drawer_index = -1
	_selected_drawer_item.clear()
	_spell_words.clear()
	_parameter_values.clear()
	if is_instance_valid(drawer_container): drawer_container.hide()
	if restore_focus and _is_open:
		_place_menu()
		_focus_first.call_deferred()

func _row_offset(index: int) -> float:
	if _rows.size() == 4: return float([-220.0, -112.0, 0.0, 136.0][index]) * _scale
	if _rows.size() == 1: return 0.0
	return (float(index) - float(_rows.size() - 1) * 0.5) * 112.0 * _scale

func _arc_x(y: float) -> float:
	return 0.00145 * pow(y / maxf(_scale, 0.01), 2) * _scale

func _place_menu() -> void:
	if not is_node_ready() or not is_instance_valid(menu_panel): return
	var screen: Vector2 = size
	_scale = clampf(minf(screen.x / 1600.0, screen.y / 800.0), 0.55, 1.6) * Chrome.ui_scale(self)
	var drawer_width: float = 300.0 * _scale if _drawer_open else 0.0
	var extent: float = maxf((MENU_WIDTH + 104.0) * _scale, 310.0 * _scale + drawer_width)
	_left_side = _target_screen.x + extent > screen.x - 24
	_pivot = _target_screen
	_pivot.x = clampf(_pivot.x, extent + 24 if _left_side else 24.0, screen.x - 24 if _left_side else maxf(24, screen.x - extent - 24))
	var minimum_y: float = -_row_offset(0) + 60.0 * _scale if not _rows.is_empty() else 60.0
	var maximum_y: float = screen.y - (_row_offset(_rows.size() - 1) + 70.0 * _scale) if not _rows.is_empty() else screen.y - 60.0
	_pivot.y = clampf(_pivot.y, minimum_y, maxf(minimum_y, maximum_y))
	_nodes.clear()
	for i in range(_rows.size()):
		var y: float = _row_offset(i)
		var node: Vector2 = _pivot + Vector2(-_arc_x(y) if _left_side else _arc_x(y), y)
		_nodes.append(node)
		var row: Button = _rows[i]
		var width: float = (260.0 if _drawer_open and i == _drawer_index else MENU_WIDTH) * _scale
		row.position = node + Vector2(-width - 28 * _scale if _left_side else 28 * _scale, -34 * _scale)
		row.size = Vector2(width, 68 * _scale)
		row.add_theme_font_size_override("font_size", maxi(16, roundi(36.0 * _scale * Chrome.font_scale(self) / Chrome.ui_scale(self))))
		for state in ["normal", "hover", "pressed", "focus", "disabled", "hover_pressed"]:
			var style: StyleBox = row.get_theme_stylebox(state).duplicate()
			style.content_margin_left = (22 if _left_side else 44) * _scale
			style.content_margin_right = (48 if _drawer_open and i == _drawer_index else 146) * _scale
			row.add_theme_stylebox_override(state, style)
		var cost := row.get_node("ActionCost") as Label
		cost.visible = not (_drawer_open and i == _drawer_index)
		cost.add_theme_font_size_override("font_size", maxi(14, roundi(28.0 * _scale * Chrome.font_scale(self) / Chrome.ui_scale(self))))
		cost.offset_left = -142 * _scale
		cost.offset_right = -24 * _scale
		row.get_node("CutCornerSlip").queue_redraw()
	var top_y: float = _pivot.y + (_row_offset(0) if not _rows.is_empty() else 0.0)
	submenu_back_btn.position = Vector2(_pivot.x + 12, top_y - 96 * _scale)
	submenu_back_btn.size = Vector2(44, 44) * _scale
	submenu_title.position = submenu_back_btn.position + Vector2(54 * _scale, 6 * _scale)
	submenu_title.size = Vector2(300, 40) * _scale
	submenu_title.add_theme_font_size_override("font_size", roundi(25 * _scale))
	_empty.position = _pivot + Vector2(32, -28) * _scale
	_empty.size = Vector2(320, 60) * _scale
	var bottom_y: float = _pivot.y + (_row_offset(_rows.size() - 1) if not _rows.is_empty() else 0.0)
	_more.position = Vector2(_pivot.x + (-200 if _left_side else 80) * _scale, bottom_y + 42 * _scale)
	_more.size = Vector2(240, 40) * _scale
	if _drawer_open and _drawer_index >= 0 and _drawer_index < _rows.size():
		var active_row: Button = _rows[_drawer_index]
		var width: float = 300.0 * _scale
		drawer_container.position = active_row.position + Vector2(-width - 8 * _scale if _left_side else active_row.size.x + 5 * _scale, -10 * _scale)
		drawer_container.size = Vector2(width, 148 * _scale)
		_drawer_scroll.position = Vector2.ZERO
		_drawer_scroll.size = Vector2(width, 92 * _scale)
		drawer_slots_box.add_theme_constant_override("separation", roundi(8 * _scale))
		for slot in drawer_slots_box.get_children():
			if slot is Button:
				slot.custom_minimum_size = Vector2(86, 86) * _scale if _drawer_mode == "items" else Vector2(86, 62) * _scale
				slot.add_theme_font_size_override("font_size", roundi(23 * _scale))
		drawer_selection.position = Vector2(6, 95) * _scale
		drawer_selection.size = Vector2(174, 40) * _scale
		drawer_selection.add_theme_font_size_override("font_size", roundi(23 * _scale))
		drawer_submit.position = Vector2(186, 95) * _scale
		drawer_submit.size = Vector2(80, 40) * _scale
		drawer_submit.visible = not drawer_submit.disabled
		drawer_close_btn.position = Vector2(width + 2, 22 * _scale)
		drawer_close_btn.size = Vector2(30, 42) * _scale
		drawer_close_btn.visible = _drawer_mode != "items"
	queue_redraw()

func _draw() -> void:
	if not _is_open or _nodes.is_empty(): return
	var points := PackedVector2Array()
	var first: float = _row_offset(0)
	var last: float = _row_offset(_rows.size() - 1)
	for i in range(41):
		var y: float = lerpf(first, last, float(i) / 40.0)
		points.append(_pivot + Vector2(-_arc_x(y) if _left_side else _arc_x(y), y))
	draw_polyline(points, Color(Chrome.INK, 0.73), maxf(1.0, 1.4 * _scale), true)
	for i in range(_nodes.size()):
		var selected := i == _drawer_index or (not _drawer_open and i == _focused_index)
		var available := bool(_row_items[i].get("available", not bool(_row_items[i].get("disabled", false))))
		if selected:
			draw_circle(_nodes[i], 13 * _scale, Color(Chrome.GOLD, 0.22))
			draw_circle(_nodes[i], 9 * _scale, Color(Chrome.GOLD, 0.8))
		draw_circle(_nodes[i], (6 if selected else 4.5) * _scale, Chrome.INK if available else Chrome.MUTED)

func _focus_first() -> void:
	if not _is_open: return
	for row in _rows:
		if bool(row.get_meta("available", true)):
			row.grab_focus()
			return
	if not _rows.is_empty(): _rows[0].grab_focus()

func _focus_drawer() -> void:
	if not _drawer_open: return
	for slot in drawer_slots_box.get_children():
		if slot is Button and not slot.disabled:
			slot.grab_focus()
			return
	drawer_close_btn.grab_focus()
