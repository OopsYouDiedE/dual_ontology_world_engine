extends Control

## 3D 胡桃木书案标题与章节结算界面（TitleStageUI.gd）
## 真实还原早期经典 3D 胡桃木书桌、月下窗棂、烛火与《安眠者有福》无框字迹排版
## 保持 2:1 参考画幅比例与三维摄像机推拉入场

signal start_game_requested
signal continue_game_requested
signal open_casebook_requested(page_id: String)
signal open_settings_requested
signal settlement_completed
signal quit_requested

const STAGE_3D_SCENE := preload("res://scenes/components/world/LowpolyBookStage3D.tscn")
const Materials := preload("res://scenes/components/shared/MaterialControls.gd")
const MENU_ROWS := [0.38, 0.47, 0.56, 0.65, 0.74]

@onready var stage_viewport: SubViewport = $StageViewport
@onready var stage_display: TextureRect = $StageDisplay
@onready var title_shade: ColorRect = $TitleShade
@onready var title_menu: Control = $TitleMenu
@onready var title_label: Label = $TitleMenu/TitleLabel
@onready var title_rule: ColorRect = $TitleMenu/TitleRule
@onready var btn_continue: Button = $TitleMenu/ContinueButton
@onready var btn_start: Button = $TitleMenu/StartButton
@onready var btn_load: Button = $TitleMenu/LoadButton
@onready var btn_settings: Button = $TitleMenu/SettingsButton
@onready var btn_quit: Button = $TitleMenu/QuitButton
@onready var notice_label: Label = $TitleMenu/ActionResult

@onready var settlement_view: Control = $SettlementView
@onready var settlement_title: Label = $SettlementView/CenterContainer/PanelContainer/MarginContainer/VBoxContainer/SettlementTitle
@onready var settlement_stats: Label = $SettlementView/CenterContainer/PanelContainer/MarginContainer/VBoxContainer/SettlementStats
@onready var btn_settle_proceed: Button = $SettlementView/CenterContainer/PanelContainer/MarginContainer/VBoxContainer/ProceedButton

var _stage_3d: Node3D
var _pending := false
var _buttons: Array[Button] = []
var _transition: Tween

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	_buttons = [btn_continue, btn_start, btn_load, btn_settings, btn_quit]

	Materials.apply(self)
	_setup_3d_stage()
	_setup_shade_material()
	_setup_button_styles()

	resized.connect(_layout_reference)

	btn_start.pressed.connect(_on_start_pressed)
	btn_continue.pressed.connect(_on_continue_pressed)
	btn_load.pressed.connect(func(): open_casebook_requested.emit("B07"))
	btn_settings.pressed.connect(func(): open_settings_requested.emit())
	btn_quit.pressed.connect(func(): quit_requested.emit())
	btn_settle_proceed.pressed.connect(func():
		hide_stage()
		settlement_completed.emit()
	)

	_layout_reference.call_deferred()

func _setup_3d_stage() -> void:
	if STAGE_3D_SCENE != null:
		_stage_3d = STAGE_3D_SCENE.instantiate()
		stage_viewport.add_child(_stage_3d)
		if _stage_3d.has_method("set_context"):
			_stage_3d.set_context(true) # desktop title mode
		if _stage_3d.has_method("set_zoom"):
			_stage_3d.set_zoom(0.0)

func _setup_shade_material() -> void:
	var shader := Shader.new()
	shader.code = """shader_type canvas_item;
void fragment() {
	float shade = smoothstep(0.48, 0.73, UV.x);
	COLOR = vec4(0.064, 0.075, 0.091, shade * 0.98);
}"""
	var mat := ShaderMaterial.new()
	mat.shader = shader
	title_shade.material = mat

func _setup_button_styles() -> void:
	for button in _buttons:
		for state in ["normal", "hover", "pressed", "disabled", "focus"]:
			button.add_theme_stylebox_override(state, StyleBoxEmpty.new())
		button.add_theme_color_override("font_color", Color("e4d8c1"))
		button.add_theme_color_override("font_hover_color", Color("fff2d8"))
		button.add_theme_color_override("font_focus_color", Color("fff2d8"))
		button.add_theme_color_override("font_pressed_color", Color("ffffff"))
		button.add_theme_color_override("font_disabled_color", Color("938b7e"))

func _get_safe_frame() -> Rect2:
	var available := size
	if available.x < 2.0 or available.y < 2.0:
		available = get_viewport().get_visible_rect().size
	var width := minf(available.x, available.y * 2.0)
	var dimensions := Vector2(width, width * 0.5)
	return Rect2((available - dimensions) * 0.5, dimensions)

func _layout_reference() -> void:
	var frame := _get_safe_frame()
	var dimensions := frame.size
	var unit := dimensions.x / 1280.0

	title_shade.position = frame.position
	title_shade.size = dimensions
	title_menu.position = frame.position
	title_menu.size = dimensions

	title_label.position = dimensions * Vector2(0.56, 0.105)
	title_label.size = dimensions * Vector2(0.42, 0.15)
	title_label.add_theme_font_size_override("font_size", roundi(64.0 * unit))

	if theme != null and theme.default_font != null:
		var title_font := FontVariation.new()
		title_font.base_font = theme.default_font
		title_font.spacing_glyph = roundi(12.0 * unit)
		title_label.add_theme_font_override("font", title_font)

	title_rule.position = dimensions * Vector2(0.656, 0.290)
	title_rule.size = Vector2(dimensions.x * 0.228, maxf(1.0, unit))

	for index in range(_buttons.size()):
		var button := _buttons[index]
		button.position = Vector2(dimensions.x * 0.60, dimensions.y * (MENU_ROWS[index] - 0.041))
		button.size = dimensions * Vector2(0.34, 0.082)
		button.add_theme_font_size_override("font_size", roundi(32.0 * unit))

	notice_label.position = dimensions * Vector2(0.58, 0.82)
	notice_label.size = dimensions * Vector2(0.38, 0.12)
	notice_label.add_theme_font_size_override("font_size", roundi(21.0 * unit))

func show_title() -> void:
	modulate.a = 1.0
	_pending = false
	_set_buttons_enabled(true)
	notice_label.hide()
	notice_label.text = ""

	if is_instance_valid(_stage_3d):
		if _stage_3d.has_method("set_context"):
			_stage_3d.set_context(true)
		if _stage_3d.has_method("set_zoom"):
			_stage_3d.set_zoom(0.0)

	settlement_view.hide()
	title_menu.show()
	title_shade.show()
	_layout_reference()
	show()

func _on_start_pressed() -> void:
	if _pending: return
	_pending = true
	_set_buttons_enabled(false)
	notice_label.text = "稍候…"
	notice_label.show()

	if is_instance_valid(_stage_3d) and _stage_3d.has_method("set_zoom"):
		if is_instance_valid(_transition): _transition.kill()
		_transition = create_tween().set_parallel(true)
		_transition.tween_method(_stage_3d.set_zoom, 0.0, 1.0, 0.55).set_trans(Tween.TRANS_CUBIC)
		_transition.tween_property(self, "modulate:a", 0.0, 0.45).set_delay(0.10)
		await _transition.finished

	start_game_requested.emit()

func _on_continue_pressed() -> void:
	if _pending: return
	_pending = true
	_set_buttons_enabled(false)
	notice_label.text = "稍候…"
	notice_label.show()

	if is_instance_valid(_stage_3d) and _stage_3d.has_method("set_zoom"):
		if is_instance_valid(_transition): _transition.kill()
		_transition = create_tween().set_parallel(true)
		_transition.tween_method(_stage_3d.set_zoom, 0.0, 1.0, 0.55).set_trans(Tween.TRANS_CUBIC)
		_transition.tween_property(self, "modulate:a", 0.0, 0.45).set_delay(0.10)
		await _transition.finished

	continue_game_requested.emit()

func _set_buttons_enabled(enabled: bool) -> void:
	for b in _buttons:
		if is_instance_valid(b):
			b.disabled = not enabled

func show_settlement(report: Dictionary) -> void:
	var day_num: int = int(report.get("day", 1))
	settlement_title.text = "【阶段结算报告】 第 %d 日终了" % day_num

	var clues: int = int(report.get("clues_found", 0))
	var deductions: int = int(report.get("deductions_resolved", 0))
	var erosion: float = float(report.get("erosion", 0.0))

	settlement_stats.text = """
调查推进统计：
- 搜集证据与口供: %d 条
- 闭合霍恩逻辑推演: %d 项
- 侦探心智源质侵蚀: %d%%
- 翌日因果锁链: 已联结 (.sleep_night.done -> .wake)
""" % [clues, deductions, int(erosion * 100)]

	title_menu.hide()
	title_shade.hide()
	settlement_view.show()
	show()

func hide_stage() -> void:
	hide()
