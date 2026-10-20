extends SceneTree

const REPORT_PATH := "res://../artifacts/tilemap-workflow/ui-engine-report.json"
const MAX_TEXT_ITEMS := 200
const DEFAULT_FONTS := ["Microsoft YaHei UI", "Noto Sans CJK SC"]

var report: Dictionary = {"errors": [], "warnings": []}


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() != 1:
		_fail("arguments", "Expected exactly one request JSON path in user args.")
		_finish()
		return
	var request_path := String(args[0])
	if not FileAccess.file_exists(request_path):
		_fail("request", "Request file does not exist: %s" % request_path)
		_finish()
		return
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(request_path))
	if not parsed is Dictionary:
		_fail("request", "Request JSON must be an object.")
		_finish()
		return
	var request: Dictionary = parsed
	match String(request.get("mode", "")):
		"scene":
			await _inspect_scene(request)
		"text":
			_inspect_text(request)
		_:
			_fail("mode", "mode must be 'scene' or 'text'.")
	_finish()


func _inspect_scene(request: Dictionary) -> void:
	var scene_path := String(request.get("scene", ""))
	if scene_path.is_empty() or not scene_path.begins_with("res://"):
		_fail("scene", "scene must be a res:// resource path.")
		return
	var viewport_size: Variant = request.get("viewport", [1280, 992])
	if not viewport_size is Array or viewport_size.size() != 2:
		_fail("viewport", "viewport must be [width, height].")
		return
	var dimensions := Vector2i(int(viewport_size[0]), int(viewport_size[1]))
	if dimensions.x < 960 or dimensions.x > 2560 or dimensions.y < 640 or dimensions.y > 1440:
		_fail("viewport", "viewport dimensions must be within 960..2560 by 640..1440.")
		return
	root.content_scale_size = dimensions
	root.size = dimensions
	var packed := load(scene_path) as PackedScene
	if packed == null:
		_fail("scene", "Could not load PackedScene: %s" % scene_path)
		return
	var instance := packed.instantiate()
	root.add_child(instance)
	await process_frame
	await process_frame
	var controls: Array[Dictionary] = []
	_walk_controls(instance, controls)
	report["mode"] = "scene"
	report["scene"] = scene_path
	report["viewport"] = [root.size.x, root.size.y]
	report["logical_viewport"] = [root.content_scale_size.x, root.content_scale_size.y]
	report["controls"] = controls
	if bool(request.get("capture", false)):
		if OS.has_feature("headless") or DisplayServer.get_name() == "headless":
			report["capture"] = "skipped_headless"
		else:
			var capture_path := ProjectSettings.globalize_path("res://../artifacts/tilemap-workflow/ui-preview.png")
			await RenderingServer.frame_post_draw
			var image := root.get_texture().get_image()
			var capture_error := image.save_png(capture_path)
			if capture_error != OK:
				_fail("capture", "Could not save UI capture (%s): %s" % [capture_error, capture_path])
			else:
				report["capture"] = capture_path
	instance.queue_free()
	await process_frame


func _walk_controls(node: Node, output: Array[Dictionary]) -> void:
	if node is Control:
		var control := node as Control
		var entry := {
			"path": str(control.get_path()),
			"type": control.get_class(),
			"size": _vec(control.size),
			"combined_minimum_size": _vec(control.get_combined_minimum_size()),
			"rect": _rect(control.get_global_rect()),
			"theme_type": control.theme_type_variation,
			"focus_mode": control.focus_mode,
			"theme": _control_theme_details(control),
		}
		output.append(entry)
		_check_containment(control)
	for child in node.get_children():
		_walk_controls(child, output)


func _control_theme_details(control: Control) -> Dictionary:
	var result: Dictionary = {}
	if control is PanelContainer:
		var panel_style := control.get_theme_stylebox("panel")
		result["panel"] = _style_details(panel_style)
		_validate_stylebox(panel_style, "%s/panel" % control.get_path())
	if control is Button:
		var button := control as Button
		var states := {}
		for state in ["normal", "hover", "pressed", "focus", "disabled", "hover_pressed"]:
			var style := button.get_theme_stylebox(state)
			states[state] = _style_details(style)
			_validate_stylebox(style, "%s/%s" % [button.get_path(), state])
		result["button_states"] = states
		var normal := button.get_theme_stylebox("normal")
		var pressed := button.get_theme_stylebox("pressed")
		var focus_style := button.get_theme_stylebox("focus")
		var visible_focus := button.focus_mode != Control.FOCUS_NONE and _style_has_visible_paint(focus_style)
		result["focus_paint_configured"] = visible_focus
		if not visible_focus:
			_warn("focus", "%s has no focus-capable mode or resolved focus style." % button.get_path())
		if normal != null and pressed != null:
			var normal_top := normal.get_content_margin(SIDE_TOP)
			var normal_bottom := normal.get_content_margin(SIDE_BOTTOM)
			var pressed_top := pressed.get_content_margin(SIDE_TOP)
			var pressed_bottom := pressed.get_content_margin(SIDE_BOTTOM)
			var adjustment_ok := is_equal_approx(pressed_top - normal_top, 1.0) and is_equal_approx(pressed_bottom - normal_bottom, -1.0) and is_equal_approx(pressed_top + pressed_bottom, normal_top + normal_bottom)
			result["pressed_content_adjustment"] = {"normal_top": normal_top, "normal_bottom": normal_bottom, "pressed_top": pressed_top, "pressed_bottom": pressed_bottom, "top_plus_1_bottom_minus_1_total_preserved": adjustment_ok}
	if control is TextEdit or control is Label or control is Button:
		result["font"] = _resolved_font_details(control)
	if control is TextEdit:
		var editor_states := {}
		for state in ["normal", "read_only", "focus"]:
			var style := control.get_theme_stylebox(state)
			editor_states[state] = _style_details(style)
			_validate_stylebox(style, "%s/%s" % [control.get_path(), state])
		result["editor_states"] = editor_states
	return result


func _resolved_font_details(control: Control) -> Dictionary:
	var font: Font
	var font_size := 0
	if control is Button:
		font = control.get_theme_font("font")
		font_size = control.get_theme_font_size("font_size")
	elif control is Label:
		font = control.get_theme_font("font")
		font_size = control.get_theme_font_size("font_size")
	elif control is TextEdit:
		font = control.get_theme_font("font")
		font_size = control.get_theme_font_size("font_size")
	return {"resource": font.resource_path if font != null else "", "class": font.get_class() if font != null else "", "font_size": font_size}


func _style_details(style: StyleBox) -> Dictionary:
	if style == null:
		return {"resolved": false}
	var result := {
		"resolved": true,
		"class": style.get_class(),
		"content_margins": {"left": style.get_content_margin(SIDE_LEFT), "top": style.get_content_margin(SIDE_TOP), "right": style.get_content_margin(SIDE_RIGHT), "bottom": style.get_content_margin(SIDE_BOTTOM)},
	}
	if style is StyleBoxTexture:
		var sliced := style as StyleBoxTexture
		result["texture"] = sliced.texture.resource_path if sliced.texture != null else ""
		result["texture_size"] = _vec(sliced.texture.get_size()) if sliced.texture != null else [0, 0]
		result["axis_stretch_horizontal"] = sliced.axis_stretch_horizontal
		result["axis_stretch_vertical"] = sliced.axis_stretch_vertical
		result["texture_margins"] = {"left": sliced.texture_margin_left, "top": sliced.texture_margin_top, "right": sliced.texture_margin_right, "bottom": sliced.texture_margin_bottom}
		result["slice_requirements"] = {"horizontal_tile": sliced.axis_stretch_horizontal == StyleBoxTexture.AXIS_STRETCH_MODE_TILE, "vertical_tile": sliced.axis_stretch_vertical == StyleBoxTexture.AXIS_STRETCH_MODE_TILE, "all_margins_explicit_positive": sliced.texture_margin_left > 0.0 and sliced.texture_margin_top > 0.0 and sliced.texture_margin_right > 0.0 and sliced.texture_margin_bottom > 0.0}
	if style is StyleBoxFlat:
		var flat := style as StyleBoxFlat
		result["corner_radius"] = {"top_left": flat.corner_radius_top_left, "top_right": flat.corner_radius_top_right, "bottom_right": flat.corner_radius_bottom_right, "bottom_left": flat.corner_radius_bottom_left}
		result["border_widths"] = {"left": flat.border_width_left, "top": flat.border_width_top, "right": flat.border_width_right, "bottom": flat.border_width_bottom}
		result["anti_aliasing"] = flat.anti_aliasing
		result["anti_aliasing_size"] = flat.anti_aliasing_size
	return result


func _validate_stylebox(style: StyleBox, context: String) -> void:
	if style == null:
		_fail("missing_style", context)
		return
	if not style is StyleBoxTexture:
		return
	var sliced := style as StyleBoxTexture
	if sliced.texture == null:
		_fail("missing_texture", context)
		return
	for side in ["left","top","right","bottom"]:
		if float(style.get("content_margin_" + side)) < 0.0:
			_warn("content_margin", "%s needs explicit content_margin_%s." % [context,side])
	if sliced.texture_margin_left + sliced.texture_margin_right >= sliced.texture.get_width() or sliced.texture_margin_top + sliced.texture_margin_bottom >= sliced.texture.get_height():
		_fail("slice_center", "%s has no positive center region." % context)


func _style_has_visible_paint(style: StyleBox) -> bool:
	if style == null or style is StyleBoxEmpty:
		return false
	if style is StyleBoxFlat:
		var flat := style as StyleBoxFlat
		if flat.bg_color.a > 0.0 or flat.shadow_color.a > 0.0:
			return true
		return (flat.border_width_left > 0 and flat.border_color.a > 0.0) or (flat.border_width_top > 0 and flat.border_color.a > 0.0) or (flat.border_width_right > 0 and flat.border_color.a > 0.0) or (flat.border_width_bottom > 0 and flat.border_color.a > 0.0)
	if style is StyleBoxTexture:
		return (style as StyleBoxTexture).texture != null
	return true


func _check_containment(control: Control) -> void:
	var parent := control.get_parent() as Control
	if parent == null:
		return
	var ancestor: Node = parent
	while ancestor != null:
		if ancestor is ScrollContainer:
			return
		ancestor = ancestor.get_parent()
	var child_rect := control.get_global_rect()
	var parent_rect := parent.get_global_rect()
	if not parent_rect.encloses(child_rect):
		_warn("bounds", "%s bounds %s exceed Control parent %s bounds %s." % [control.get_path(), str(child_rect), parent.get_path(), str(parent_rect)])


func _inspect_text(request: Dictionary) -> void:
	var items: Variant = request.get("items", request.get("texts", []))
	if not items is Array or items.is_empty() or items.size() > MAX_TEXT_ITEMS:
		_fail("items", "items/texts must contain 1..%d entries." % MAX_TEXT_ITEMS)
		return
	var results: Array[Dictionary] = []
	for index in items.size():
		var item: Variant = items[index]
		if not item is Dictionary:
			_fail("items", "items[%d] must be an object." % index)
			continue
		var width := float(item.get("width", 0))
		var height := float(item.get("height", 0))
		var font_size := int(item.get("font_size", 0))
		var text := String(item.get("text", ""))
		if width <= 0 or width > 10000 or height <= 0 or height > 10000 or font_size < 1 or font_size > 256:
			_fail("range", "items[%d] width/height must be 1..10000 and font_size 1..256." % index)
			continue
		var font: Font = _load_requested_font(String(item.get("font_path", "")))
		if font == null:
			_fail("font", "items[%d] font_path must load a Font resource or a SystemFont is unavailable." % index)
			continue
		var paragraph := TextParagraph.new()
		paragraph.width = width
		paragraph.break_flags = TextServer.BREAK_MANDATORY | TextServer.BREAK_WORD_BOUND | TextServer.BREAK_GRAPHEME_BOUND
		paragraph.add_string(text, font, font_size)
		var line_sizes: Array[Array] = []
		for line_index in paragraph.get_line_count():
			line_sizes.append(_vec(paragraph.get_line_size(line_index)))
		var measured := paragraph.get_size()
		results.append({"index": index, "text_length": text.length(), "font": {"resource": font.resource_path, "class": font.get_class(), "requested_system_families": DEFAULT_FONTS if String(item.get("font_path", "")).is_empty() else []}, "width": width, "height_limit": height, "font_size": font_size, "line_count": paragraph.get_line_count(), "line_sizes": line_sizes, "measured_size": _vec(measured), "fits_width": measured.x <= width, "fits_height": measured.y <= height})
	report["mode"] = "text"
	report["texts"] = results


func _load_requested_font(font_path: String) -> Font:
	if not font_path.is_empty():
		if not font_path.begins_with("res://"):
			return null
		return load(font_path) as Font
	var system_font := SystemFont.new()
	system_font.font_names = PackedStringArray(DEFAULT_FONTS)
	return system_font


func _vec(value: Vector2) -> Array:
	return [value.x, value.y]


func _rect(value: Rect2) -> Dictionary:
	return {"position": _vec(value.position), "size": _vec(value.size)}


func _fail(kind: String, message: String) -> void:
	var errors: Array = report["errors"]
	errors.append({"kind": kind, "message": message})


func _warn(kind: String, message: String) -> void:
	var warnings: Array = report["warnings"]
	warnings.append({"kind": kind, "message": message})


func _finish() -> void:
	report["semantic_review"] = "not_performed"
	var output_path := ProjectSettings.globalize_path(REPORT_PATH)
	var output := FileAccess.open(output_path, FileAccess.WRITE)
	if output == null:
		push_error("Could not open report output: %s" % output_path)
		quit(2)
		return
	output.store_string(JSON.stringify(report, "\t"))
	output.close()
	print("ui_inspect mode=%s errors=%d warnings=%d" % [String(report.get("mode", "invalid")), report["errors"].size(), report["warnings"].size()])
	quit(0 if report["errors"].is_empty() else 1)
