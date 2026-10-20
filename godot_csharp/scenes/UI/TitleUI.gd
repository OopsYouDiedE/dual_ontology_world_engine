@tool
extends Control

## 标题页：油灯下桌上合着的一本册子，竖开 1 : √2，斜放约 4°；近俯视（约 60°）。
## 封面在子视口里排版；显示时在桌面上转 4°，随桌面平面（Plane）纵向缩短到 0.87；
## SubViewportContainer 把点击映回封面坐标。
## 菜单：开始阅读 / 续读 / 凡例（设置）/ 合卷。向宿主回传纯数据 ui_event 字典。

signal ui_event(event: Dictionary)

const Sound := preload("res://scenes/Shared/Audio/Sound.gd")
const SETTINGS_SCENE := preload("res://scenes/UI/SettingsUI.tscn")

const TEXTS := {
	"zh_CN": ["安眠者有福", "开始阅读", "续读", "凡例", "合卷"],
	"en": ["Blessed Are Those Who Sleep", "Begin reading", "Continue", "Settings", "Close the book"],
	"ja": ["眠る者に福あれ", "読みはじめる", "続きから", "設定", "本を閉じる"],
}
const TITLE_TYPES := {"zh_CN": &"PixelDisplay", "en": &"PixelMast", "ja": &"PixelMast"}

var mode := "title"

@onready var start_button: Button = %StartButton
@onready var continue_button: Button = %ContinueButton
@onready var settings_button: Button = %SettingsButton
@onready var quit_button: Button = %QuitButton
@onready var title_label: Label = %TitleLabel
@onready var rule: Control = %Rule
@onready var cover: SubViewportContainer = $Lit/Plane/Cover
@onready var viewport: SubViewport = $Lit/Plane/Cover/Viewport
@onready var pages: Control = $Lit/Pages
@onready var glow: TextureRect = $Glow

var _settings_instance: Node
var _light_time := 0.0
var _reduce_motion := false
var _labels := {}

func _ready() -> void:
	_place_book()
	pages.draw.connect(_draw_pages)
	rule.draw.connect(_draw_rule)
	cover.item_rect_changed.connect(_redraw_book)
	
	$Lit/Plane/Cover/Viewport/CoverRoot/Spine.color = get_theme_color("spine", "Title")
	$Lit/Plane/Cover/Viewport/CoverRoot/SlipShadow.color = get_theme_color("slip_shadow", "Title")
	$Lit/Plane/Cover/Viewport/CoverRoot/Slip.resized.connect(_place_slip_shadow)
	_place_slip_shadow()
	$Lit/Plane/Cover/Viewport/CoverRoot/Spine.hide()
	$Lit/Plane/Cover/Viewport/CoverRoot/SlipShadow.hide()
	_localize()

	for b: Button in [start_button, continue_button, settings_button, quit_button]:
		b.focus_entered.connect(_mark.bind(b, true))
		b.focus_exited.connect(_mark.bind(b, false))
		b.mouse_entered.connect(_hover.bind(b))

	if Engine.is_editor_hint():
		return

	Sound.music("main_theme", 2.5)
	var settings := get_node_or_null("/root/GameSettings")
	if settings != null:
		_reduce_motion = bool(settings.get_value("accessibility", "reduce_motion"))
		settings.setting_changed.connect(func(section: String, key: String, value):
			if section == "accessibility" and key == "reduce_motion": _reduce_motion = bool(value)
		)
	start_button.pressed.connect(_request.bind("start"))
	quit_button.pressed.connect(_request.bind("quit"))
	settings_button.pressed.connect(open_settings)
	continue_button.disabled = true
	start_button.grab_focus()

func _notification(what: int) -> void:
	if what == NOTIFICATION_TRANSLATION_CHANGED and is_node_ready():
		_localize()

func _language() -> String:
	var locale := TranslationServer.get_locale()
	return "ja" if locale.begins_with("ja") else "en" if locale.begins_with("en") else "zh_CN"

func _localize() -> void:
	var texts: Array = TEXTS[_language()]
	title_label.theme_type_variation = TITLE_TYPES[_language()]
	title_label.text = texts[0]
	var buttons: Array[Button] = [start_button, continue_button, settings_button, quit_button]
	for i in buttons.size():
		_labels[buttons[i]] = texts[i + 1]
		_mark(buttons[i], buttons[i].has_focus())

func _process(delta: float) -> void:
	if Engine.is_editor_hint():
		return
	_light_time += delta
	var wave := 0.0 if _reduce_motion else sin(_light_time * 4.5) * 0.025 + sin(_light_time * 11.3) * 0.012
	var intensity := 0.96 + wave
	if is_instance_valid(glow): glow.modulate.a = intensity * 0.15
	var material := $Lit.material as ShaderMaterial
	if material != null: material.set_shader_parameter("flicker_intensity", intensity)

func _mark(b: Button, on: bool) -> void:
	b.text = ("☞ " + str(_labels[b])) if on else str(_labels[b])

func _hover(b: Button) -> void:
	if not b.disabled:
		b.grab_focus()

func _place_slip_shadow() -> void:
	var slip: Control = $Lit/Plane/Cover/Viewport/CoverRoot/Slip
	var shadow: Control = $Lit/Plane/Cover/Viewport/CoverRoot/SlipShadow
	var k := float(get_theme_constant("slip_shadow", "Title"))
	shadow.position = slip.position + Vector2(k, k)
	shadow.size = slip.size

func _redraw_book() -> void:
	pages.queue_redraw()

func _place_book() -> void:
	var center := Vector2(get_theme_constant("book_x", "Title"), get_theme_constant("book_y", "Title"))
	var book_size := Vector2(get_theme_constant("book_w", "Title"), get_theme_constant("book_h", "Title"))
	var plane: Control = $Lit/Plane
	plane.pivot_offset = center
	plane.scale = Vector2(1.0, float(get_theme_constant("elev_q", "Title")) / 100.0)
	cover.position = center - book_size / 2.0
	cover.size = book_size
	cover.pivot_offset = book_size / 2.0
	cover.rotation_degrees = float(get_theme_constant("book_angle", "Title")) / 10.0

func cover_corners() -> PackedVector2Array:
	var xf := get_global_transform().affine_inverse() * cover.get_global_transform()
	var s := cover.size
	return PackedVector2Array([xf * Vector2.ZERO, xf * Vector2(s.x, 0), xf * s, xf * Vector2(0, s.y)])

func _draw_pages() -> void:
	var c := cover_corners()
	var thick := get_theme_constant("thickness", "Title")
	var light := get_theme_color("page_light", "Title")
	var dark := get_theme_color("page_dark", "Title")
	var edge := get_theme_color("cover_edge", "Title")
	for spread in range(18, 0, -1):
		var offset := Vector2(4, thick + 4)
		var center := (c[0] + c[2]) / 2.0
		var polygon := PackedVector2Array()
		for point in c: polygon.append(point + (point - center).normalized() * spread + offset)
		pages.draw_colored_polygon(polygon, Color(0.04, 0.03, 0.025, 0.016))
	for k in range(1, thick + 1):
		var color := edge if (k <= 2 or k >= thick - 1) else (light if k % 2 == 1 else dark)
		color = color.darkened(0.4 * float(k) / maxf(1.0, thick))
		var d0 := Vector2(0, k - 1)
		var d1 := Vector2(0, k)
		pages.draw_colored_polygon(PackedVector2Array([c[3] + d0, c[2] + d0, c[2] + d1, c[3] + d1]), color)

func _draw_rule() -> void:
	var strong := get_theme_color("rule_strong", "Paper")
	rule.draw_rect(Rect2(0, 0, rule.size.x, 3), strong)
	rule.draw_rect(Rect2(0, 5, rule.size.x, 1), strong)

# ================================================================ settings ===

func open_settings() -> void:
	if is_instance_valid(_settings_instance) and _settings_instance.visible:
		return
	Sound.sfx("ui_click", -4.0)
	if not is_instance_valid(_settings_instance):
		_settings_instance = SETTINGS_SCENE.instantiate()
		add_child(_settings_instance)
		if _settings_instance.has_signal("closed"):
			_settings_instance.closed.connect(close_settings)
	_settings_instance.open()
	viewport.gui_disable_input = true

func close_settings() -> void:
	if is_instance_valid(_settings_instance):
		_settings_instance.visible = false
	viewport.gui_disable_input = false
	settings_button.grab_focus()

func _unhandled_input(event: InputEvent) -> void:
	if Engine.is_editor_hint():
		return
	if is_instance_valid(_settings_instance) and _settings_instance.visible:
		if event.is_action_pressed("cancel") or event.is_action_pressed("ui_cancel") or event.is_action_pressed("settings"):
			close_settings()
			get_viewport().set_input_as_handled()
		return
	if event.is_action_pressed("ui_accept") or event.is_action_pressed("dialogue_continue"):
		_request("start")
		get_viewport().set_input_as_handled()
	elif event.is_action_pressed("settings"):
		open_settings()
		get_viewport().set_input_as_handled()
	elif event.is_action_pressed("cancel") or event.is_action_pressed("ui_cancel"):
		_request("quit")
		get_viewport().set_input_as_handled()

func _request(kind: String) -> void:
	if start_button.disabled:
		return
	for b: Button in [start_button, continue_button, settings_button, quit_button]:
		b.disabled = true
	Sound.sfx("ui_click", -4.0)
	ui_event.emit({"kind": kind})
