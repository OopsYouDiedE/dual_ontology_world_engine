@tool
extends CanvasLayer
## Quiet field overlay. All time, place and perception text comes from the host.
signal settings_requested
signal action_cancelled(action_id: String)
signal book_open_requested
signal perception_inspect_requested(record: Dictionary)
const Chrome := preload("res://scenes/UIpanel/FieldHUD/FieldChrome.gd")

var top_bar: PanelContainer
var room_label: Label
var target_label: Label
var action_label: Label
var status_label: Label
var time_label: Label
var danger_badge: PanelContainer
var danger_label: Label
var hud_settings_button: Button
var hint_label: Label
var hover_tip: Label
var abnormal_stamps_box: HBoxContainer
var action_points_box: HBoxContainer
var action_points_label: Label
var capsule_queue_box: HBoxContainer
var edge_wave_box: PanelContainer
var edge_wave_label: Label
var witness_layer: Control
var _clock_row: HBoxContainer
var _clock_backdrop: Control
var _cost_cells: HBoxContainer
var _overlay_blocked := false
var _shutting_down := false
var _last_snapshot: Dictionary = {}
var _abnormal_signature := ""
var _hint_serial := 0
var _wave_serial := 0
var _wave_tween: Tween
var _entrance: Tween
var _witnesses: Dictionary = {}
var _seen_perceptions: Dictionary = {}
var _screen_scale := 1.0

func _ready() -> void:
	layer = 10
	process_mode = Node.PROCESS_MODE_ALWAYS
	_build()
	get_viewport().size_changed.connect(_layout_hud)
	var settings := get_node_or_null("/root/GameSettings")
	if settings != null: settings.setting_changed.connect(_on_setting_changed)
	_layout_hud()
	if not Engine.is_editor_hint():
		top_bar.modulate.a = 0
		_entrance = create_tween()
		_entrance.tween_property(top_bar, "modulate:a", 1.0, 0.2)

func _label(parent: Node, id: String, text := "") -> Label:
	var label := Label.new()
	label.name = id
	label.text = text
	label.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(label)
	return label

func _build() -> void:
	top_bar = PanelContainer.new()
	top_bar.name = "TopBar"
	top_bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	top_bar.add_theme_stylebox_override("panel", StyleBoxEmpty.new())
	add_child(top_bar)
	Chrome.apply(top_bar, 32)
	_clock_backdrop = Control.new()
	_clock_backdrop.name = "ClockShade"
	_clock_backdrop.mouse_filter = Control.MOUSE_FILTER_IGNORE
	top_bar.add_child(_clock_backdrop)
	_clock_backdrop.draw.connect(func():
		var s := _clock_backdrop.size
		var polygon := PackedVector2Array([Vector2(-22 * _screen_scale, -10 * _screen_scale), Vector2(s.x, -10 * _screen_scale), Vector2(s.x, s.y + 10 * _screen_scale), Vector2(-22 * _screen_scale, s.y + 10 * _screen_scale)])
		_clock_backdrop.draw_polygon(polygon, PackedColorArray([Color(0.08, 0.07, 0.06, 0.32), Color.TRANSPARENT, Color.TRANSPARENT, Color(0.08, 0.07, 0.06, 0.32)]))
	)
	_clock_row = HBoxContainer.new()
	_clock_row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	top_bar.add_child(_clock_row)
	time_label = _label(_clock_row, "TimeLabel")
	var divider := VSeparator.new()
	divider.name = "ClockDivider"
	divider.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	divider.custom_minimum_size = Vector2(1, 28)
	_clock_row.add_child(divider)
	room_label = _label(_clock_row, "RoomLabel")
	room_label.clip_text = true
	room_label.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	room_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	hud_settings_button = Button.new()
	hud_settings_button.name = "HudSettingsButton"
	hud_settings_button.text = ""
	hud_settings_button.tooltip_text = "案卷"
	hud_settings_button.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	for state in ["normal", "hover", "pressed", "disabled"]:
		hud_settings_button.add_theme_stylebox_override(state, StyleBoxEmpty.new())
	var focus := StyleBoxFlat.new()
	focus.bg_color = Color.TRANSPARENT
	focus.border_width_bottom = 1
	focus.border_color = Chrome.GOLD
	hud_settings_button.add_theme_stylebox_override("focus", focus)
	hud_settings_button.pressed.connect(func():
		if not _overlay_blocked and not _shutting_down: book_open_requested.emit()
	)
	top_bar.add_child(hud_settings_button)
	target_label = _label(top_bar, "TargetLabel")
	action_label = _label(top_bar, "ActionLabel")
	status_label = _label(top_bar, "StatusLabel")
	for item: Label in [target_label, action_label, status_label]: item.hide()
	danger_badge = PanelContainer.new()
	danger_badge.name = "DangerBadge"
	danger_badge.add_theme_stylebox_override("panel", StyleBoxEmpty.new())
	_clock_row.add_child(danger_badge)
	danger_label = _label(danger_badge, "DangerLabel")
	danger_label.add_theme_color_override("font_color", Chrome.RED)
	danger_badge.hide()
	abnormal_stamps_box = HBoxContainer.new()
	abnormal_stamps_box.name = "AbnormalStampsBox"
	add_child(abnormal_stamps_box)
	Chrome.apply(abnormal_stamps_box, 18)
	action_points_box = HBoxContainer.new()
	action_points_box.name = "ActionPointsBox"
	action_points_box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	action_points_box.add_theme_constant_override("separation", 12)
	add_child(action_points_box)
	Chrome.apply(action_points_box, 22)
	_cost_cells = HBoxContainer.new()
	_cost_cells.add_theme_constant_override("separation", 3)
	action_points_box.add_child(_cost_cells)
	action_points_label = _label(action_points_box, "ActionPointsLabel")
	action_points_box.hide()
	witness_layer = Control.new()
	witness_layer.name = "WitnessLayer"
	witness_layer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(witness_layer)
	witness_layer.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	Chrome.apply(witness_layer, 24)
	capsule_queue_box = HBoxContainer.new()
	capsule_queue_box.name = "CapsuleQueueBox"
	capsule_queue_box.alignment = BoxContainer.ALIGNMENT_CENTER
	capsule_queue_box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(capsule_queue_box)
	Chrome.apply(capsule_queue_box, 24)
	capsule_queue_box.hide()
	hint_label = _label(self, "HintLabel")
	hint_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	hint_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	hint_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	Chrome.apply(hint_label, 24)
	hint_label.add_theme_stylebox_override("normal", Chrome.box(0.7, Vector4(18, 10, 18, 10)))
	hint_label.hide()
	hover_tip = _label(self, "HoverTip")
	Chrome.apply(hover_tip, 22)
	hover_tip.add_theme_stylebox_override("normal", Chrome.box(0.88, Vector4(12, 7, 12, 7)))
	hover_tip.hide()
	edge_wave_box = PanelContainer.new()
	edge_wave_box.name = "EdgeWaveBox"
	edge_wave_box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(edge_wave_box)
	Chrome.apply(edge_wave_box, 24)
	edge_wave_box.add_theme_stylebox_override("panel", Chrome.box(0.5, Vector4(16, 10, 16, 10)))
	var row := HBoxContainer.new()
	row.name = "WaveRow"
	row.add_theme_constant_override("separation", 14)
	edge_wave_box.add_child(row)
	row.add_child(Chrome.glyph("sound", Vector2(30, 30), Chrome.MUTED))
	edge_wave_label = _label(row, "EdgeWaveLabel")
	edge_wave_label.clip_text = true
	edge_wave_label.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	edge_wave_box.hide()

func _on_setting_changed(section: String, key: String, _value: Variant) -> void:
	if section != "accessibility" and not (section == "graphics" and key == "ui_scale"): return
	for control: Control in [top_bar, hint_label, hover_tip, edge_wave_box, capsule_queue_box, witness_layer]:
		Chrome.apply(control, 24)
	_layout_hud()

func _layout_hud() -> void:
	if not is_node_ready(): return
	var screen := get_viewport().get_visible_rect().size
	_screen_scale = clampf(minf(screen.x / 1600.0, screen.y / 800.0), 0.55, 1.6)
	var scale := _screen_scale * Chrome.ui_scale(self)
	top_bar.position = Vector2(screen.x * 0.03, 26 * scale)
	top_bar.custom_minimum_size = Vector2(370, 52) * scale
	top_bar.size = Vector2(minf(510 * scale, screen.x - top_bar.position.x - 32), 52 * scale)
	_clock_row.add_theme_constant_override("separation", roundi(24 * scale))
	for label: Label in [time_label, room_label]:
		label.add_theme_font_size_override("font_size", maxi(17, roundi(38 * _screen_scale * Chrome.font_scale(self))))
		label.add_theme_color_override("font_color", Chrome.INK)
		label.add_theme_color_override("font_shadow_color", Color(0.04, 0.035, 0.03, 0.6))
		label.add_theme_constant_override("shadow_offset_y", 1)
	_clock_backdrop.queue_redraw()
	abnormal_stamps_box.position = top_bar.position + Vector2(0, 68 * scale)
	action_points_box.position = top_bar.position + Vector2(0, 112 * scale)
	capsule_queue_box.position = Vector2(screen.x * 0.5 - 270 * scale, screen.y - 78 * scale)
	capsule_queue_box.size = Vector2(540, 54) * scale
	hint_label.position = Vector2(screen.x * 0.5 - 350 * scale, screen.y - 150 * scale)
	hint_label.size = Vector2(700, 68) * scale
	for label: Label in [hint_label, hover_tip, edge_wave_label, action_points_label]:
		label.add_theme_font_size_override("font_size", maxi(14, roundi(25 * _screen_scale * Chrome.font_scale(self))))

func apply_snapshot(snapshot: Dictionary) -> void:
	_last_snapshot = snapshot
	var hud: Dictionary = snapshot.get("hud", {}) if snapshot.get("hud", {}) is Dictionary else {}
	room_label.text = str(hud.get("room", hud.get("title", snapshot.get("logical_location", snapshot.get("room", "")))))
	room_label.tooltip_text = room_label.text
	time_label.text = _time_text(hud.get("time", snapshot.get("time", snapshot.get("game_time", ""))))
	var danger: Variant = hud.get("danger", snapshot.get("danger"))
	danger_badge.visible = (danger is int or danger is float) and float(danger) > 0
	if danger_badge.visible:
		danger_label.text = "!" if float(danger) > 1 else "·"
		danger_badge.tooltip_text = "危险" if float(danger) > 1 else "警戒"
	var abnormalities: Variant = hud.get("abnormals", snapshot.get("abnormals", []))
	_update_abnormal_stamps(abnormalities if abnormalities is Array else [])
	var perceptions: Variant = snapshot.get("perceptions", hud.get("perceptions", []))
	if perceptions is Array:
		for record in perceptions:
			if record is Dictionary: present_perception(record)

func _time_text(value: Variant) -> String:
	if value is Dictionary:
		if value.has("text"): return str(value.text)
		if value.has("hour") and value.has("minute"): return "%02d:%02d" % [int(value.hour), int(value.minute)]
		return ""
	if value is int or value is float:
		var minutes := int(value)
		return "%02d:%02d" % [posmod(minutes / 60, 24), posmod(minutes, 60)]
	return str(value) if value != null else ""

func _update_abnormal_stamps(records: Array) -> void:
	var signature := JSON.stringify(records)
	if signature == _abnormal_signature: return
	_abnormal_signature = signature
	for child in abnormal_stamps_box.get_children():
		abnormal_stamps_box.remove_child(child)
		child.queue_free()
	for value in records:
		var record: Dictionary = value if value is Dictionary else {"label": str(value)}
		var label := str(record.get("label", record.get("name", record.get("id", ""))))
		if label.is_empty(): continue
		var button := Button.new()
		button.tooltip_text = label
		Chrome.button(button, true)
		button.custom_minimum_size = Vector2(36, 36)
		var icon := Chrome.glyph(str(record.get("icon", "injured" if label.contains("伤") else "abnormal")), Vector2(28, 28), Chrome.RED)
		button.add_child(icon)
		icon.position = Vector2(4, 4)
		icon.size = Vector2(28, 28)
		button.pressed.connect(func(): book_open_requested.emit())
		abnormal_stamps_box.add_child(button)
	abnormal_stamps_box.visible = abnormal_stamps_box.get_child_count() > 0 and not _overlay_blocked

func show_cost_preview(current_ap: int, cost_ap: int) -> void:
	if _overlay_blocked or cost_ap <= 0:
		hide_cost_preview()
		return
	for child in _cost_cells.get_children():
		_cost_cells.remove_child(child)
		child.queue_free()
	action_points_box.show()
	if current_ap >= 0:
		var count := mini(current_ap, 10)
		for i in range(count):
			var cell := ColorRect.new()
			cell.mouse_filter = Control.MOUSE_FILTER_IGNORE
			cell.custom_minimum_size = Vector2(5, 16)
			cell.size_flags_vertical = Control.SIZE_SHRINK_CENTER
			cell.color = Chrome.RED if i >= count - mini(cost_ap, count) else Chrome.GREEN
			_cost_cells.add_child(cell)
		action_points_label.text = "%d → %d" % [current_ap, maxi(0, current_ap - cost_ap)]
	else: action_points_label.text = "−%d" % cost_ap
	action_points_label.tooltip_text = "精力"

func hide_cost_preview() -> void:
	if is_instance_valid(action_points_box): action_points_box.hide()

func update_action_capsule(action_name: String, progress_ratio: float, action_id: String) -> void:
	for child in capsule_queue_box.get_children():
		capsule_queue_box.remove_child(child)
		child.queue_free()
	capsule_queue_box.visible = not action_name.is_empty() and not _overlay_blocked
	if action_name.is_empty(): return
	var button := Button.new()
	button.text = action_name + "  ×"
	button.tooltip_text = "取消"
	Chrome.button(button)
	button.pressed.connect(func(): action_cancelled.emit(action_id))
	capsule_queue_box.add_child(button)
	if progress_ratio >= 0:
		var progress := ProgressBar.new()
		progress.mouse_filter = Control.MOUSE_FILTER_IGNORE
		progress.max_value = 1
		progress.value = clampf(progress_ratio, 0, 1)
		progress.show_percentage = false
		button.add_child(progress)
		progress.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
		progress.offset_top = -2

func show_offscreen_wave(text: String, duration := 3.5, screen_direction := Vector2.LEFT) -> void:
	if text.is_empty() or _shutting_down: return
	_wave_serial += 1
	var serial := _wave_serial
	if is_instance_valid(_wave_tween): _wave_tween.kill()
	edge_wave_label.text = text
	edge_wave_box.show()
	edge_wave_box.modulate.a = 0
	var screen := get_viewport().get_visible_rect().size
	edge_wave_box.size = Vector2(minf(520 * _screen_scale, screen.x - 40), 56 * _screen_scale)
	var position := (screen - edge_wave_box.size) * 0.5
	if absf(screen_direction.x) >= absf(screen_direction.y):
		position.x = 24 if screen_direction.x < 0 else screen.x - edge_wave_box.size.x - 24
	else:
		position.y = 110 if screen_direction.y < 0 else screen.y - edge_wave_box.size.y - 90
	edge_wave_box.position = Chrome.bounded_position(edge_wave_box, position)
	_wave_tween = create_tween()
	_wave_tween.tween_property(edge_wave_box, "modulate:a", 1.0, 0.18)
	_wave_tween.tween_interval(maxf(0.4, duration))
	_wave_tween.tween_property(edge_wave_box, "modulate:a", 0.0, 0.5)
	_wave_tween.tween_callback(func():
		if serial == _wave_serial: edge_wave_box.hide()
	)

func present_perception(record: Dictionary) -> void:
	var id := str(record.get("id", record.get("event_id", "")))
	if id.is_empty() or _seen_perceptions.has(id): return
	var kind := str(record.get("kind", record.get("type", "")))
	var label := str(record.get("label", record.get("text", "")))
	if label.is_empty(): return
	if kind in ["heard", "sound", "offscreen"]:
		show_offscreen_wave(label, float(record.get("duration", 3.5)), _vector(record.get("screen_direction", Vector2.LEFT)))
		_seen_perceptions[id] = true
	elif kind in ["witness", "seen", "witnessed"] and record.has("screen_position"):
		show_witness(record, _vector(record.screen_position))
		_seen_perceptions[id] = true
	if _seen_perceptions.size() > 256: _seen_perceptions.erase(_seen_perceptions.keys()[0])

func show_witness(record: Dictionary, screen_position: Vector2) -> void:
	var id := str(record.get("id", record.get("actor_id", "")))
	if id.is_empty(): return
	if _witnesses.has(id) and is_instance_valid(_witnesses[id]):
		_witnesses[id].position = Chrome.bounded_position(_witnesses[id], screen_position + Vector2(14, -48))
		return
	var tag := Button.new()
	tag.text = str(record.get("label", record.get("text", "")))
	tag.tooltip_text = str(record.get("description", ""))
	Chrome.button(tag, true)
	tag.pressed.connect(func(): perception_inspect_requested.emit(record))
	witness_layer.add_child(tag)
	tag.reset_size()
	tag.position = Chrome.bounded_position(tag, screen_position + Vector2(14, -48))
	_witnesses[id] = tag
	get_tree().create_timer(float(record.get("duration", 3.5))).timeout.connect(func():
		if is_instance_valid(tag): tag.queue_free()
		_witnesses.erase(id)
	)

func _vector(value: Variant) -> Vector2:
	if value is Vector2: return value
	if value is Array and value.size() >= 2: return Vector2(float(value[0]), float(value[1]))
	if value is Dictionary: return Vector2(float(value.get("x", 0)), float(value.get("y", 0)))
	return Vector2.ZERO

func show_hover(text: String, pos: Vector2) -> void:
	if _shutting_down or _overlay_blocked or text.is_empty(): return
	hover_tip.text = text
	var font := hover_tip.get_theme_font("font")
	var font_size := hover_tip.get_theme_font_size("font_size")
	hover_tip.size = Vector2(minf(font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, font_size).x + 24, get_viewport().get_visible_rect().size.x - 36), font.get_height(font_size) + 16)
	hover_tip.position = Chrome.bounded_position(hover_tip, pos + Vector2(18, 18))
	hover_tip.show()

func hide_hover() -> void:
	if is_instance_valid(hover_tip): hover_tip.hide()

func set_hint(text: String) -> void:
	_hint_serial += 1
	var serial := _hint_serial
	hint_label.text = text
	hint_label.visible = not text.is_empty() and not _overlay_blocked
	if text.is_empty(): return
	get_tree().create_timer(3.0).timeout.connect(func():
		if serial == _hint_serial: hint_label.hide()
	)

func finish_entrance() -> void:
	if is_instance_valid(_entrance): _entrance.kill()
	top_bar.modulate.a = 1

func set_overlay_blocked(blocked: bool) -> void:
	_overlay_blocked = blocked
	top_bar.visible = not blocked
	hud_settings_button.disabled = blocked or _shutting_down
	witness_layer.visible = not blocked
	abnormal_stamps_box.visible = not blocked and abnormal_stamps_box.get_child_count() > 0
	if blocked:
		hide_hover()
		hide_cost_preview()
		hint_label.hide()
		capsule_queue_box.hide()

func begin_shutdown() -> void:
	_shutting_down = true
	if is_instance_valid(_wave_tween): _wave_tween.kill()
	if is_instance_valid(_entrance): _entrance.kill()
	hud_settings_button.disabled = true
	witness_layer.hide()
	hide_hover()
	hint_label.hide()
	edge_wave_box.hide()
