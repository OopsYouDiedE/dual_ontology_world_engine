extends RefCounted
## 手稿界面：羊皮纸底、墨色、印红。所有面板与按钮共用这一套样式。

const PAPER := Color("e6dcbf")
const PAPER_DARK := Color("d6c9a6")
const INK := Color("2b2016")
const SEAL := Color("8a3324")
const MUTED := Color("6b5d48")
const RULE := Color(0.17, 0.13, 0.09, 0.28)
const MATCH := Color("3f5a3a")
const FONT: Font = preload("res://ShareAsset/External/ZhaohuaTypeWriter/ZhaohuaTypeWriter.ttf")

static func theme() -> Theme:
	var t := Theme.new()
	t.default_font = FONT
	t.default_font_size = 19
	for state in ["normal", "hover", "pressed", "focus", "disabled"]:
		var box := StyleBoxFlat.new()
		box.bg_color = {"normal": Color("efe6cc"), "hover": Color("f6eedb"), "pressed": Color("d8c8a0"), "focus": Color("efe6cc"), "disabled": Color("e2d7ba")}[state]
		box.border_color = SEAL if state in ["hover", "focus"] else Color("8c7a5c")
		box.set_border_width_all(1)
		box.border_width_bottom = 2
		box.set_corner_radius_all(2)
		box.content_margin_left = 14
		box.content_margin_right = 14
		box.content_margin_top = 6
		box.content_margin_bottom = 6
		if state == "focus":
			box.draw_center = false
		t.set_stylebox(state, "Button", box)
	t.set_color("font_color", "Button", INK)
	t.set_color("font_hover_color", "Button", INK)
	t.set_color("font_pressed_color", "Button", INK)
	t.set_color("font_focus_color", "Button", INK)
	t.set_color("font_disabled_color", "Button", Color(INK, 0.4))
	t.set_color("default_color", "RichTextLabel", INK)
	t.set_color("font_color", "Label", INK)
	var line := StyleBoxFlat.new()
	line.bg_color = Color("f3ecd8")
	line.border_color = Color("8c7a5c")
	line.set_border_width_all(1)
	line.set_content_margin_all(8)
	t.set_stylebox("normal", "LineEdit", line)
	t.set_stylebox("focus", "LineEdit", line)
	t.set_color("font_color", "LineEdit", INK)
	t.set_color("caret_color", "LineEdit", INK)
	t.set_color("font_placeholder_color", "LineEdit", MUTED)
	var bar := StyleBoxFlat.new()
	bar.bg_color = Color(INK, 0.35)
	bar.set_corner_radius_all(3)
	t.set_stylebox("grabber", "VScrollBar", bar)
	t.set_stylebox("grabber_highlight", "VScrollBar", bar)
	t.set_stylebox("grabber_pressed", "VScrollBar", bar)
	var track := StyleBoxFlat.new()
	track.bg_color = Color(INK, 0.08)
	t.set_stylebox("scroll", "VScrollBar", track)
	return t

static func sheet(margin := 34) -> StyleBoxFlat:
	var box := StyleBoxFlat.new()
	box.bg_color = PAPER
	box.border_color = Color("7d6a4c")
	box.set_border_width_all(2)
	box.set_corner_radius_all(3)
	box.shadow_color = Color(0, 0, 0, 0.45)
	box.shadow_size = 14
	box.set_content_margin_all(margin)
	return box

static func label(text: String, size := 19, color := INK) -> RichTextLabel:
	var l := RichTextLabel.new()
	l.bbcode_enabled = true
	l.fit_content = true
	l.scroll_active = false
	l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	l.add_theme_font_size_override("normal_font_size", size)
	l.add_theme_font_size_override("bold_font_size", size)
	l.add_theme_color_override("default_color", color)
	l.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	l.text = text
	return l

static func button(text: String, action: Callable, size := 19) -> Button:
	var b := Button.new()
	b.text = text
	b.add_theme_font_size_override("font_size", size)
	b.focus_mode = Control.FOCUS_NONE
	b.pressed.connect(action)
	return b

static func rule() -> ColorRect:
	var r := ColorRect.new()
	r.color = RULE
	r.custom_minimum_size = Vector2(0, 1)
	r.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return r
