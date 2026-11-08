extends Control
## 标题界面：右侧书名与菜单，左侧留给书房 3D 场景。点击桌上的书由 Main 判定。
## 只发出意图信号，由 Main 决定开局、读档或打开书本检视器的哪一页。

signal new_game_requested
signal continue_requested
signal book_requested(page_id: String)
signal quit_requested

const TITLE_FONT := preload("res://assets/external/yujisyuku/YujiSyuku-Regular.ttf")
const BODY_FONT := preload("res://assets/external/NotoSerifSC/NotoSerifSC.ttf")
const LATIN_FONT := preload("res://assets/external/Almendra/Almendra-Regular.ttf")
const Sound := preload("res://scenes/components/shared/Sound.gd")
const CREAM := Color("eadcbd")
const GOLD := Color("c9a45c")
const EMBER := Color("e0794a")

@onready var shade: TextureRect = $Shade
@onready var menu: VBoxContainer = $Menu
@onready var rule: TextureRect = $Menu/Rule
@onready var new_button: Button = $Menu/NewButton
@onready var continue_button: Button = $Menu/ContinueButton
@onready var records_button: Button = $Menu/RecordsButton
@onready var settings_button: Button = $Menu/SettingsButton
@onready var quit_button: Button = $Menu/QuitButton
@onready var hint: Label = $Hint
@onready var fade: ColorRect = $Fade

## 标题显示中、可以接受点击时为 true。
var interactive := false
var _tween: Tween
var _time := 0.0

func _ready() -> void:
	theme = _build_theme()
	var gradient := Gradient.new()
	gradient.offsets = PackedFloat32Array([0.0, 0.5, 1.0])
	gradient.colors = PackedColorArray([Color(GOLD, 0.0), GOLD, Color(GOLD, 0.0)])
	var line := GradientTexture2D.new()
	line.gradient = gradient
	line.width = 256
	line.height = 2
	rule.texture = line
	new_button.pressed.connect(_emit.bind(new_game_requested))
	continue_button.pressed.connect(_emit.bind(continue_requested))
	records_button.pressed.connect(func(): _emit_book("save"))
	settings_button.pressed.connect(func(): _emit_book("settings"))
	quit_button.pressed.connect(_emit.bind(quit_requested))
	for button: Button in [new_button, continue_button, records_button, settings_button, quit_button]:
		button.mouse_entered.connect(func(): Sound.sfx("ui_click", -18.0, 1.4))
	modulate.a = 0.0
	hide()

## 显示标题。first=true 时从全黑淡入。
func show_title(can_continue: bool, first := false) -> void:
	continue_button.disabled = not can_continue
	show()
	interactive = true
	if _tween != null: _tween.kill()
	_tween = create_tween().set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_OUT)
	if first:
		fade.color.a = 1.0
		modulate.a = 1.0
		menu.modulate.a = 0.0
		hint.modulate.a = 0.0
		_tween.tween_property(fade, "color:a", 0.0, 1.8)
		_tween.parallel().tween_property(menu, "modulate:a", 1.0, 1.2).set_delay(0.6)
		_tween.tween_property(hint, "modulate:a", 1.0, 0.8)
	else:
		fade.color.a = 0.0
		menu.modulate.a = 1.0
		hint.modulate.a = 1.0
		_tween.tween_property(self, "modulate:a", 1.0, 0.45)
	(continue_button if can_continue else new_button).grab_focus.call_deferred()

func hide_title(duration := 0.4) -> void:
	interactive = false
	if _tween != null: _tween.kill()
	_tween = create_tween()
	_tween.tween_property(self, "modulate:a", 0.0, duration)
	_tween.tween_callback(hide)

func _process(delta: float) -> void:
	_time += delta
	# 书名像被火光照着一样缓慢明暗。
	var title := $Menu/TitleLabel as Label
	title.add_theme_color_override("font_shadow_color", Color(EMBER, 0.28 + 0.10 * sin(_time * 0.9)))
	hint.self_modulate.a = 0.55 + 0.25 * sin(_time * 1.6)

func _emit(signal_ref: Signal) -> void:
	if not interactive: return
	Sound.sfx("ui_click", -6.0)
	signal_ref.emit()

func _emit_book(page_id: String) -> void:
	if not interactive: return
	Sound.sfx("ui_click", -6.0)
	book_requested.emit(page_id)

func _build_theme() -> Theme:
	var theme := Theme.new()
	theme.default_font = BODY_FONT
	theme.default_font_size = 28
	theme.set_type_variation("TitleLabel", "Label")
	theme.set_font("font", "TitleLabel", TITLE_FONT)
	theme.set_font_size("font_size", "TitleLabel", 92)
	theme.set_color("font_color", "TitleLabel", CREAM)
	theme.set_constant("shadow_offset_x", "TitleLabel", 0)
	theme.set_constant("shadow_offset_y", "TitleLabel", 0)
	theme.set_constant("shadow_outline_size", "TitleLabel", 18)
	theme.set_type_variation("SubtitleLabel", "Label")
	theme.set_font("font", "SubtitleLabel", LATIN_FONT)
	theme.set_font_size("font_size", "SubtitleLabel", 22)
	theme.set_color("font_color", "SubtitleLabel", Color(GOLD, 0.85))
	theme.set_type_variation("HintLabel", "Label")
	theme.set_font_size("font_size", "HintLabel", 20)
	theme.set_color("font_color", "HintLabel", Color(CREAM, 0.8))
	theme.set_color("font_color", "Button", Color(CREAM, 0.82))
	theme.set_color("font_focus_color", "Button", CREAM)
	theme.set_color("font_hover_color", "Button", GOLD)
	theme.set_color("font_pressed_color", "Button", EMBER)
	theme.set_color("font_disabled_color", "Button", Color(CREAM, 0.25))
	theme.set_font_size("font_size", "Button", 30)
	for state in ["normal", "disabled", "pressed"]:
		theme.set_stylebox(state, "Button", _button_style(Color(0, 0, 0, 0)))
	theme.set_stylebox("hover", "Button", _button_style(Color(GOLD, 0.10)))
	theme.set_stylebox("focus", "Button", _button_style(Color(GOLD, 0.06), true))
	return theme

static func _button_style(color: Color, outlined := false) -> StyleBoxFlat:
	var box := StyleBoxFlat.new()
	box.bg_color = color
	box.content_margin_top = 8.0
	box.content_margin_bottom = 8.0
	if outlined:
		box.border_color = Color(GOLD, 0.45)
		box.border_width_bottom = 1
		box.border_width_top = 1
	return box
