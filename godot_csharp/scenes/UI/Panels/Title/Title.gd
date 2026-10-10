@tool
extends Control
## 标题页（设计规范第六部分 §4.7）：油灯下桌上合着的一本册子，竖开 1 : √2，斜放约 4°，与地图同为 3/4 俯视。
## 封面在不旋转的子视口里排版，显示时旋转、最近邻取样；SubViewportContainer 用同一变换的逆变换把点击映回封面坐标。
## 近侧书页厚度向下露出，硬边投影偏右下；册子按屏幕坐标乘灯光图；灯焰光晕轻微闪动（“减少动效”时静止）。
## 菜单：开始阅读 / 续读（尚无书签时不可点）/ 凡例（打开设置册）/ 合卷。只回传输入，由宿主处理会话与退出；不放剧情文字。

signal ui_event(event: Dictionary)

const Sound := preload("res://scenes/Shared/Audio/Sound.gd")
const UIAppearance := preload("res://scenes/UI/Components/UIAppearance.gd")
const SETTINGS_BOOK := preload("res://scenes/UI/Panels/Settings/SettingsBook.tscn")
const FLICKER := [0.78, 0.88, 1.0]
## 书名与菜单（界面文字，非剧情）：书名取项目译名表里的定名。
const TEXTS := {
	"zh_CN": ["安眠者有福", "开始阅读", "续读", "凡例", "合卷"],
	"en": ["Blessed Are Those Who Sleep", "Begin reading", "Continue", "Settings", "Close the book"],
	"ja": ["眠る者に福あれ", "読みはじめる", "続きから", "設定", "本を閉じる"],
}
## 书名字号：中文五字用最大的报头；译名更长，用略小的报头，两行以内排下。
const TITLE_TYPES := {"zh_CN": &"PixelDisplay", "en": &"PixelMast", "ja": &"PixelMast"}

@onready var start_button: Button = %StartButton
@onready var continue_button: Button = %ContinueButton
@onready var settings_button: Button = %SettingsButton
@onready var quit_button: Button = %QuitButton
@onready var cover: SubViewportContainer = $Lit/Cover
@onready var viewport: SubViewport = $Lit/Cover/Viewport

var _settings: Control
var _flicker_wait := 0.0
var _labels := {}

func _ready() -> void:
	$Shadow.draw.connect(_draw_shadow)
	$Lit/Pages.draw.connect(_draw_pages)
	cover.item_rect_changed.connect(_redraw_book)
	$Lit/Cover/Viewport/CoverRoot/Spine.color = get_theme_color("spine", "Title")
	$Lit/Cover/Viewport/CoverRoot/SlipShadow.color = get_theme_color("slip_shadow", "Title")
	$Lit/Cover/Viewport/CoverRoot/Slip.resized.connect(_place_slip_shadow)
	_place_slip_shadow()
	_localize()
	for b: Button in [start_button, continue_button, settings_button, quit_button]:
		b.focus_entered.connect(_mark.bind(b, true))
		b.focus_exited.connect(_mark.bind(b, false))
		b.mouse_entered.connect(_hover.bind(b))
	if Engine.is_editor_hint():
		return
	UIAppearance.bind(self)
	Sound.music("main_theme", 2.5)
	start_button.pressed.connect(_request.bind("start"))
	quit_button.pressed.connect(_request.bind("quit"))
	settings_button.pressed.connect(open_settings)
	# 存档尚未接入：没有书签可续读。
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
	var title_label := $Lit/Cover/Viewport/CoverRoot/Slip/Column/TitleLabel
	title_label.pixel_type = TITLE_TYPES[_language()]
	title_label.text = texts[0]
	var buttons: Array[Button] = [start_button, continue_button, settings_button, quit_button]
	for i in buttons.size():
		_labels[buttons[i]] = texts[i + 1]
		_mark(buttons[i], buttons[i].has_focus())

func _process(delta: float) -> void:
	if Engine.is_editor_hint():
		return
	if UIAppearance.reduce_motion(self):
		$Glow.modulate.a = 1.0
		return
	_flicker_wait -= delta
	if _flicker_wait <= 0.0:
		_flicker_wait = randf_range(0.08, 0.3)
		$Glow.modulate.a = FLICKER[randi() % FLICKER.size()]

## 指示手只标出当前焦点（设计规范第六部分 §4.6）。
func _mark(b: Button, on: bool) -> void:
	b.text = ("☞ " + str(_labels[b])) if on else str(_labels[b])

func _hover(b: Button) -> void:
	if not b.disabled:
		b.grab_focus()

func _place_slip_shadow() -> void:
	var slip: Control = $Lit/Cover/Viewport/CoverRoot/Slip
	var shadow: Control = $Lit/Cover/Viewport/CoverRoot/SlipShadow
	var k := float(get_theme_constant("slip_shadow", "Title"))
	shadow.position = slip.position + Vector2(k, k)
	shadow.size = slip.size

func _redraw_book() -> void:
	$Shadow.queue_redraw()
	$Lit/Pages.queue_redraw()

## 封面四角（左上、右上、右下、左下）在屏幕上的位置：封面自身的变换（含旋转与轴心）。
func cover_corners() -> PackedVector2Array:
	var xf := cover.get_transform()
	var s := cover.size
	return PackedVector2Array([xf * Vector2.ZERO, xf * Vector2(s.x, 0), xf * s, xf * Vector2(0, s.y)])

## 3/4 俯视：册子平放，近侧（下沿）书页厚度向下挤出；两层封皮边夹着横纹书页。
func _draw_pages() -> void:
	var pages: Control = $Lit/Pages
	var c := cover_corners()
	var thick := get_theme_constant("thickness", "Title")
	var light := get_theme_color("page_light", "Title")
	var dark := get_theme_color("page_dark", "Title")
	var edge := get_theme_color("cover_edge", "Title")
	for k in range(1, thick + 1):
		var color := edge if (k <= 2 or k >= thick - 1) else (light if k % 2 == 1 else dark)
		var d0 := Vector2(0, k - 1)
		var d1 := Vector2(0, k)
		pages.draw_colored_polygon(PackedVector2Array([c[3] + d0, c[2] + d0, c[2] + d1, c[3] + d1]), color)

## 硬边投影：册子连同厚度整体偏向右下（灯在左上）。
func _draw_shadow() -> void:
	var c := cover_corners()
	var down := Vector2(0, get_theme_constant("thickness", "Title"))
	var offset := Vector2(get_theme_constant("shadow_x", "Title"), get_theme_constant("shadow_y", "Title"))
	$Shadow.draw_colored_polygon(PackedVector2Array([c[0] + offset, c[1] + offset, c[2] + down + offset, c[3] + down + offset]),
		get_theme_color("shadow", "Title"))

# ================================================================ settings ===

func open_settings() -> void:
	if is_instance_valid(_settings):
		return
	Sound.sfx("page", -6.0)
	_settings = SETTINGS_BOOK.instantiate()
	_settings.can_save = false
	add_child(_settings)
	_settings.closed.connect(close_settings)
	viewport.gui_disable_input = true

func close_settings() -> void:
	if not is_instance_valid(_settings):
		return
	remove_child(_settings)
	_settings.queue_free()
	_settings = null
	viewport.gui_disable_input = false
	settings_button.grab_focus()

func _unhandled_input(event: InputEvent) -> void:
	if Engine.is_editor_hint():
		return
	if is_instance_valid(_settings):
		if event.is_action_pressed("cancel") or event.is_action_pressed("settings"):
			close_settings()
			get_viewport().set_input_as_handled()
	elif event.is_action_pressed("settings"):
		open_settings()
		get_viewport().set_input_as_handled()

func _request(kind: String) -> void:
	if start_button.disabled:
		return
	for b: Button in [start_button, continue_button, settings_button, quit_button]:
		b.disabled = true
	Sound.sfx("ui_click", -4.0)
	ui_event.emit({"kind": kind})
