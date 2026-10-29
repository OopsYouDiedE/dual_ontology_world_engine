extends RefCounted
## Presentation helpers for Material 3 design system; never inspect or mutate session state.
const BASE_THEME := preload("res://scenes/UI/Components/MaterialTheme.tres")

## Color tokens for Material 3
const COLOR_PRIMARY := Color(0.18, 0.28, 0.25, 1.0)
const COLOR_ON_PRIMARY := Color(1.0, 1.0, 1.0, 1.0)
const COLOR_SURFACE := Color(0.96, 0.93, 0.86, 1.0)
const COLOR_SURFACE_VARIANT := Color(0.90, 0.88, 0.82, 1.0)
const COLOR_ON_SURFACE := Color(0.12, 0.13, 0.12, 1.0)
const COLOR_OUTLINE := Color(0.55, 0.55, 0.50, 1.0)
const COLOR_ERROR := Color(0.72, 0.18, 0.15, 1.0)
const COLOR_WARNING := Color(0.78, 0.52, 0.10, 1.0)
const COLOR_SUCCESS := Color(0.18, 0.55, 0.28, 1.0)

## Apply to a shared parent so all native controls inherit the same typography.
static func apply(parent: Control, scale := 1.0) -> void:
	var theme_copy := BASE_THEME.duplicate() as Theme
	theme_copy.default_font_size = roundi(18.0 * clampf(scale, 0.8, 2.0))
	parent.theme = theme_copy

## Use for interactive native buttons so the focusable target is at least 48 px.
static func prepare_button(button: BaseButton) -> void:
	button.custom_minimum_size.x = maxf(button.custom_minimum_size.x, 48.0)
	button.custom_minimum_size.y = maxf(button.custom_minimum_size.y, 48.0)
	button.focus_mode = Control.FOCUS_ALL

## Create an M3 styled card container
static func create_card() -> PanelContainer:
	var card := PanelContainer.new()
	card.theme = BASE_THEME
	var box := StyleBoxFlat.new()
	box.bg_color = COLOR_SURFACE
	box.border_width_left = 1
	box.border_width_top = 1
	box.border_width_right = 1
	box.border_width_bottom = 1
	box.border_color = COLOR_OUTLINE
	box.corner_radius_top_left = 12
	box.corner_radius_top_right = 12
	box.corner_radius_bottom_right = 12
	box.corner_radius_bottom_left = 12
	box.content_margin_left = 16.0
	box.content_margin_top = 12.0
	box.content_margin_right = 16.0
	box.content_margin_bottom = 12.0
	card.add_theme_stylebox_override("panel", box)
	return card

## Create an M3 styled badge/tag for status or danger
static func create_badge(text: String, severity: String = "info") -> PanelContainer:
	var badge := PanelContainer.new()
	badge.theme = BASE_THEME
	var box := StyleBoxFlat.new()
	var bg_color := COLOR_SURFACE_VARIANT
	var text_color := COLOR_ON_SURFACE
	match severity:
		"safe", "success":
			bg_color = Color(0.85, 0.94, 0.87, 1.0)
			text_color = COLOR_SUCCESS
		"warning":
			bg_color = Color(0.98, 0.92, 0.80, 1.0)
			text_color = COLOR_WARNING
		"critical", "danger", "error":
			bg_color = Color(0.96, 0.84, 0.84, 1.0)
			text_color = COLOR_ERROR
		"primary":
			bg_color = COLOR_PRIMARY
			text_color = COLOR_ON_PRIMARY
	box.bg_color = bg_color
	box.corner_radius_top_left = 8
	box.corner_radius_top_right = 8
	box.corner_radius_bottom_right = 8
	box.corner_radius_bottom_left = 8
	box.content_margin_left = 10.0
	box.content_margin_top = 4.0
	box.content_margin_right = 10.0
	box.content_margin_bottom = 4.0
	badge.add_theme_stylebox_override("panel", box)
	var label := Label.new()
	label.text = text
	label.add_theme_color_override("font_color", text_color)
	label.add_theme_font_size_override("font_size", 14)
	badge.add_child(label)
	return badge

