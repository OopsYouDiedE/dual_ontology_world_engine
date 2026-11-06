extends RefCounted
## Plain-paper native controls. The shared book owns geometry and lighting.
const COLOR_PRIMARY := Color("465142")
const COLOR_ON_PRIMARY := Color("f1e4ce")
const COLOR_SURFACE := Color("e3cfba")
const COLOR_SURFACE_VARIANT := Color("cfbba3")
const COLOR_ON_SURFACE := Color("2e2923")
const COLOR_OUTLINE := Color("9d8870")
const COLOR_ERROR := Color("8a4b40")
const COLOR_WARNING := Color("917344")
const COLOR_SUCCESS := Color("48624d")

static func apply(parent: Control, scale := 1.0) -> void:
	var theme_copy := Theme.new()
	var size_scale: float = clampf(scale, 0.8, 1.8)
	var plain := false
	var contrast := false
	if parent.is_inside_tree():
		var settings: Node = parent.get_node_or_null("/root/GameSettings")
		if settings != null:
			var text_scale: Variant = settings.get_value("accessibility", "text_scale")
			if text_scale != null: size_scale *= float(text_scale)
			var ui_scale: Variant = settings.get_value("graphics", "ui_scale")
			if ui_scale != null: size_scale *= float(ui_scale)
			plain = bool(settings.get_value("accessibility", "plain_fonts"))
			contrast = bool(settings.get_value("accessibility", "high_contrast"))
	var font_path := "res://assets/external/NotoSerifSC/NotoSerifSC[wght].ttf"
	if plain and ResourceLoader.exists("res://assets/external/NotoSansSC/NotoSansSC[wght].ttf"):
		font_path = "res://assets/external/NotoSansSC/NotoSansSC[wght].ttf"
	if ResourceLoader.exists(font_path):
		var font := FontVariation.new()
		font.base_font = load(font_path)
		font.variation_opentype = {TextServerManager.get_primary_interface().name_to_tag("wght"): 420.0}
		theme_copy.default_font = font
		for kind in ["Label", "Button", "OptionButton", "CheckButton", "CheckBox", "LineEdit", "TextEdit", "SpinBox", "PopupMenu", "TooltipLabel"]:
			theme_copy.set_font("font", kind, font)
		for kind in ["normal_font", "bold_font", "italics_font", "bold_italics_font"]:
			theme_copy.set_font(kind, "RichTextLabel", font)
	theme_copy.default_font_size = roundi(24.0 * size_scale)
	var ink := Color("121b16") if contrast else COLOR_ON_SURFACE
	var empty := StyleBoxEmpty.new()
	for type_name in ["PanelContainer", "Panel"]: theme_copy.set_stylebox("panel", type_name, empty)
	for type_name in ["Label", "RichTextLabel", "Button", "OptionButton", "CheckButton", "CheckBox", "LineEdit", "TextEdit", "SpinBox"]:
		theme_copy.set_color("font_color", type_name, ink)
		theme_copy.set_color("default_color", type_name, ink)
		theme_copy.set_color("font_hover_color", type_name, COLOR_PRIMARY)
		theme_copy.set_color("font_focus_color", type_name, ink)
		theme_copy.set_color("font_pressed_color", type_name, COLOR_ON_PRIMARY)
		theme_copy.set_color("font_disabled_color", type_name, Color("75644f"))
		theme_copy.set_font_size("font_size", type_name, theme_copy.default_font_size)
	for type_name in ["Button", "OptionButton", "CheckButton", "CheckBox"]:
		theme_copy.set_stylebox("normal", type_name, _box(Color(0,0,0,0), 12, 9))
		theme_copy.set_stylebox("hover", type_name, _box(Color(0.29,0.38,0.31,0.09), 12, 9))
		theme_copy.set_stylebox("pressed", type_name, _box(COLOR_PRIMARY, 12, 9))
		theme_copy.set_stylebox("disabled", type_name, _box(Color(0,0,0,0), 12, 9))
		var focus := _box(Color(0,0,0,0), 12, 9)
		focus.border_color = COLOR_PRIMARY
		focus.border_width_bottom = 2
		theme_copy.set_stylebox("focus", type_name, focus)
	for type_name in ["LineEdit", "TextEdit"]:
		var edit := _box(Color(0.95,0.93,0.87,0.35), 14, 12)
		edit.border_width_bottom = 1
		edit.border_color = Color("a6ab97")
		theme_copy.set_stylebox("normal", type_name, edit)
		theme_copy.set_stylebox("read_only", type_name, empty)
		theme_copy.set_color("caret_color", type_name, ink)
		theme_copy.set_color("selection_color", type_name, Color(0.35,0.45,0.37,0.22))
	var line := StyleBoxLine.new()
	line.color = Color(0.35,0.40,0.32,0.25)
	line.thickness = 1
	theme_copy.set_stylebox("separator", "HSeparator", line)
	for type_name in ["HSlider", "HScrollBar", "VScrollBar"]:
		theme_copy.set_stylebox("slider", type_name, _box(Color("c9caba"), 0, 2))
		theme_copy.set_stylebox("grabber_area", type_name, _box(COLOR_PRIMARY, 0, 2))
		theme_copy.set_stylebox("grabber_area_highlight", type_name, _box(COLOR_PRIMARY.lightened(0.12), 0, 3))
	var popup := _box(COLOR_SURFACE.lightened(0.035), 12, 10)
	popup.border_color = COLOR_OUTLINE
	popup.set_border_width_all(1)
	theme_copy.set_stylebox("panel", "PopupMenu", popup)
	theme_copy.set_color("font_color", "PopupMenu", ink)
	theme_copy.set_font_size("font_size", "PopupMenu", theme_copy.default_font_size)
	parent.theme = theme_copy

static func _box(color: Color, horizontal := 12.0, vertical := 8.0) -> StyleBoxFlat:
	var box := StyleBoxFlat.new()
	box.bg_color = color
	box.content_margin_left = horizontal
	box.content_margin_right = horizontal
	box.content_margin_top = vertical
	box.content_margin_bottom = vertical
	return box

static func prepare_button(button: BaseButton) -> void:
	button.custom_minimum_size = button.custom_minimum_size.max(Vector2(44, 44))
	button.focus_mode = Control.FOCUS_ALL
	button.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND

static func create_card() -> PanelContainer:
	var card := PanelContainer.new()
	card.add_theme_stylebox_override("panel", _box(Color(0,0,0,0), 12, 12))
	return card

static func create_badge(text: String, severity := "info") -> PanelContainer:
	var badge := PanelContainer.new()
	var tint := COLOR_PRIMARY
	if severity in ["critical", "danger", "error"]: tint = COLOR_ERROR
	elif severity == "warning": tint = COLOR_WARNING
	var box := _box(Color(tint, 0.08), 10, 5)
	box.border_width_left = 2
	box.border_color = tint
	badge.add_theme_stylebox_override("panel", box)
	var label := Label.new()
	label.text = text
	label.add_theme_color_override("font_color", tint)
	label.add_theme_font_size_override("font_size", 20)
	badge.add_child(label)
	return badge

