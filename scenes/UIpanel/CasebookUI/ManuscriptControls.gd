@tool
extends RefCounted
## Procedural ink-and-rubric styling for native controls on book pages.
## Presentation only: it reads no session state and changes no settings.
## Textures are generated once per pixel size and shared by every book page.

const INK := Color("33261d")
const RUBRIC := Color("8a3324")
const GOLD := Color("bc9450")

static var _icon_cache: Dictionary = {}


## Applies box, tick, slider and focus styles to a page theme copy.
static func style_theme(theme: Theme, font_size: int) -> void:
	var box_size := clampi(roundi(font_size * 0.92), 16, 48)
	var off := _icon("box_off", box_size)
	var on := _icon("box_on", box_size)
	for prefix in ["", "_mirrored"]:
		theme.set_icon("unchecked" + prefix, "CheckButton", off)
		theme.set_icon("checked" + prefix, "CheckButton", on)
		theme.set_icon("unchecked_disabled" + prefix, "CheckButton", _icon("box_off_dim", box_size))
		theme.set_icon("checked_disabled" + prefix, "CheckButton", _icon("box_on_dim", box_size))
	theme.set_constant("h_separation", "CheckButton", roundi(font_size * 0.6))

	var focus := StyleBoxFlat.new()
	focus.draw_center = false
	focus.border_width_bottom = 2
	focus.border_color = Color(RUBRIC, 0.75)
	focus.content_margin_bottom = 2.0
	for kind in ["Button", "CheckButton", "OptionButton"]:
		theme.set_stylebox("focus", kind, focus)
		theme.set_color("font_focus_color", kind, RUBRIC)
		theme.set_color("font_hover_pressed_color", kind, RUBRIC)
		theme.set_color("font_disabled_color", kind, Color(INK, 0.38))

	# A ruled ink line with a rubric fill reads as a measure drawn on vellum.
	var track := StyleBoxFlat.new()
	track.bg_color = Color(INK, 0.28)
	track.content_margin_top = 1.5
	track.content_margin_bottom = 1.5
	var fill := StyleBoxFlat.new()
	fill.bg_color = Color(RUBRIC, 0.85)
	fill.content_margin_top = 1.5
	fill.content_margin_bottom = 1.5
	var grabber_size := clampi(roundi(font_size * 0.75), 14, 36)
	theme.set_stylebox("slider", "HSlider", track)
	theme.set_stylebox("grabber_area", "HSlider", fill)
	theme.set_stylebox("grabber_area_highlight", "HSlider", fill)
	theme.set_icon("grabber", "HSlider", _icon("diamond", grabber_size))
	theme.set_icon("grabber_highlight", "HSlider", _icon("diamond_hot", grabber_size))
	theme.set_icon("grabber_disabled", "HSlider", _icon("diamond_dim", grabber_size))

	var rule := StyleBoxLine.new()
	rule.color = Color(INK, 0.22)
	rule.thickness = 1
	theme.set_stylebox("separator", "HSeparator", rule)


static func _icon(kind: String, size: int) -> ImageTexture:
	var key := "%s@%d" % [kind, size]
	if _icon_cache.has(key): return _icon_cache[key]
	var image := Image.create_empty(size, size, false, Image.FORMAT_RGBA8)
	var s := float(size)
	for y in size:
		for x in size:
			var p := Vector2(x + 0.5, y + 0.5) / s
			image.set_pixel(x, y, _shade(kind, p, s))
	var texture := ImageTexture.create_from_image(image)
	_icon_cache[key] = texture
	return texture


## Returns the antialiased colour of one icon pixel in unit coordinates.
static func _shade(kind: String, p: Vector2, s: float) -> Color:
	var aa := 1.0 / s
	var dim := kind.ends_with("_dim")
	var colour := Color(0, 0, 0, 0)
	if kind.begins_with("box"):
		# Square frame drawn as a slightly heavy pen line.
		var half := 0.36
		var d := maxf(absf(p.x - 0.5), absf(p.y - 0.5)) - half
		var frame := 1.0 - smoothstep(0.045 - aa, 0.045 + aa, absf(d))
		colour = _over(colour, Color(INK, 0.82 * frame))
		if kind.begins_with("box_on"):
			# Rubric tick that overruns the frame, as a scribe's mark would.
			var tick := minf(_segment(p, Vector2(0.24, 0.52), Vector2(0.44, 0.74)),
				_segment(p, Vector2(0.44, 0.74), Vector2(0.84, 0.16)))
			var ink := 1.0 - smoothstep(0.07 - aa, 0.07 + aa, tick)
			colour = _over(colour, Color(RUBRIC, ink))
	elif kind.begins_with("diamond"):
		var d := absf(p.x - 0.5) + absf(p.y - 0.5)
		var body := 1.0 - smoothstep(0.40 - aa, 0.40 + aa, d)
		var edge := 1.0 - smoothstep(0.035 - aa, 0.035 + aa, absf(d - 0.40))
		var fill := GOLD if kind == "diamond_hot" else RUBRIC
		colour = _over(colour, Color(fill, body))
		colour = _over(colour, Color(INK, 0.7 * edge))
	if dim: colour.a *= 0.4
	return colour


static func _segment(p: Vector2, a: Vector2, b: Vector2) -> float:
	var ab := b - a
	var t := clampf((p - a).dot(ab) / ab.length_squared(), 0.0, 1.0)
	return p.distance_to(a + ab * t)


static func _over(base: Color, top: Color) -> Color:
	var alpha := top.a + base.a * (1.0 - top.a)
	if alpha <= 0.0: return Color(0, 0, 0, 0)
	var rgb := (Color(top.r, top.g, top.b) * top.a + Color(base.r, base.g, base.b) * base.a * (1.0 - top.a)) / alpha
	return Color(rgb.r, rgb.g, rgb.b, alpha)
