@tool
extends Button
## Material Symbols Outlined glyphs with full keyboard and touch targets.
const SYMBOLS: Font = preload("res://ShareAsset/External/MaterialSymbols/MaterialSymbolsOutlined[FILL,GRAD,opsz,wght].ttf")
const CODEPOINTS := {
	"reset": 0xf053, "previous": 0xe045, "next": 0xe044,
	"play": 0xe037, "pause": 0xe034, "fast": 0xe01f,
	"left": 0xe5cb, "right": 0xe5cc, "up": 0xe5ce, "down": 0xe5cf,
}
@export var glyph: String = "play":
	set(value):
		if glyph == value: return
		glyph = value
		queue_redraw()

func _ready() -> void:
	flat = true
	text = ""
	focus_mode = Control.FOCUS_ALL
	mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	custom_minimum_size = Vector2(36, 34)
	if tooltip_text.is_empty():
		var names := {"reset":"FA_MIND_062", "previous":"FA_MIND_063", "next":"FA_MIND_064", "play":"FA_MIND_065", "pause":"FA_MIND_066", "fast":"FA_MIND_068", "left":"FA_MIND_073", "right":"FA_MIND_074", "up":"FA_MIND_075", "down":"FA_MIND_076"}
		tooltip_text = tr(str(names.get(glyph,"FA_MIND_077"))) + tr("FA_MIND_085")
	for state in ["normal", "hover", "pressed", "disabled", "focus", "hover_pressed"]:
		add_theme_stylebox_override(state, StyleBoxEmpty.new())
	mouse_entered.connect(queue_redraw)
	mouse_exited.connect(queue_redraw)
	focus_entered.connect(queue_redraw)
	focus_exited.connect(queue_redraw)
	button_down.connect(queue_redraw)
	button_up.connect(queue_redraw)
	toggled.connect(func(_pressed: bool): queue_redraw())

func _draw() -> void:
	if is_hovered() and not disabled:
		draw_rect(Rect2(Vector2(2, 2), size - Vector2(4, 4)), Color(0.2, 0.25, 0.2, 0.08))
	var ink := Color("141613")
	if disabled:
		ink = Color(0.14, 0.16, 0.14, 0.32)
	if has_focus() and not disabled:
		draw_line(Vector2(5, size.y - 2), Vector2(size.x - 5, size.y - 2), ink, 1.2, false)
	if toggle_mode and button_pressed and not disabled:
		draw_rect(Rect2(floorf(size.x / 2.0) - 3, size.y - 4, 6, 2), ink)
	var symbol := String.chr(int(CODEPOINTS.get(glyph, CODEPOINTS["play"])))
	var font_size := 24
	var text_size := SYMBOLS.get_string_size(symbol, HORIZONTAL_ALIGNMENT_LEFT, -1, font_size)
	var baseline := Vector2((size.x - text_size.x) / 2.0, (size.y - text_size.y) / 2.0 + SYMBOLS.get_ascent(font_size))
	SYMBOLS.draw_string(get_canvas_item(), baseline, symbol, HORIZONTAL_ALIGNMENT_LEFT, -1, font_size, ink)
