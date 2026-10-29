extends RefCounted
## Presentation helpers only; they never inspect or mutate session state.
const BASE_THEME := preload("res://scenes/UI/Components/MaterialTheme.tres")

## Apply to a shared parent so all native controls inherit the same typography.
## Scale changes the default size; existing controls with explicit font overrides
## must be updated by their owner.
static func apply(parent: Control, scale := 1.0) -> void:
	var theme_copy := BASE_THEME.duplicate() as Theme
	theme_copy.default_font_size = roundi(18.0 * clampf(scale, 0.8, 2.0))
	parent.theme = theme_copy

## Use for interactive native buttons so the focusable target is at least 48 px.
static func prepare_button(button: BaseButton) -> void:
	button.custom_minimum_size.x = maxf(button.custom_minimum_size.x, 48.0)
	button.custom_minimum_size.y = maxf(button.custom_minimum_size.y, 48.0)
	button.focus_mode = Control.FOCUS_ALL
