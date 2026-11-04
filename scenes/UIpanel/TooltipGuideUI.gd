@tool
extends Control
## Short contextual text follows the pointer or the focused control.
signal guide_dismissed(guide_id: String)

const Chrome := preload("res://scenes/UIpanel/FieldHUD/FieldChrome.gd")
@onready var tooltip_panel: PanelContainer = %TooltipPanel
@onready var tooltip_title: Label = %TooltipTitle
@onready var tooltip_body: Label = %TooltipBody
@onready var key_prompt_label: Label = %KeyPromptLabel
@onready var guide_banner: PanelContainer = %GuideBanner
@onready var guide_title: Label = %GuideTitle
@onready var guide_text: Label = %GuideText
@onready var guide_close_btn: Button = %GuideCloseBtn

var _active_guide_id := ""
var _generation := 0
var _target: WeakRef
var _seen: Dictionary = {}

func _ready() -> void:
	Chrome.apply(self, 15)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	tooltip_panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	tooltip_panel.hide()
	guide_banner.hide()
	Chrome.button(guide_close_btn, true)
	guide_close_btn.pressed.connect(dismiss_guide)
	var config := ConfigFile.new()
	if config.load("user://ui_guides.cfg") == OK:
		for key in config.get_section_keys("seen"): _seen[key] = true

func bind_tooltip(control: Control, title: String, content: String, key_prompt := "") -> void:
	control.mouse_entered.connect(func():
		_target = weakref(control)
		show_tooltip(control.get_global_rect().end, title, content, key_prompt)
	)
	control.focus_entered.connect(func():
		_target = weakref(control)
		show_tooltip(control.get_global_rect().end, title, content, key_prompt)
	)
	control.mouse_exited.connect(hide_tooltip)
	control.focus_exited.connect(hide_tooltip)

func show_tooltip(target_pos: Vector2, title: String, content: String, key_prompt := "") -> void:
	tooltip_title.text = title
	tooltip_title.visible = not title.is_empty()
	tooltip_body.text = content
	tooltip_body.visible = not content.is_empty()
	key_prompt_label.text = key_prompt
	key_prompt_label.visible = not key_prompt.is_empty()
	tooltip_panel.show()
	tooltip_panel.reset_size()
	tooltip_panel.position = Chrome.bounded_position(tooltip_panel, target_pos + Vector2(14, 14))
	_refit.bind(target_pos).call_deferred()

func _refit(target_pos: Vector2) -> void:
	if tooltip_panel.visible: tooltip_panel.position = Chrome.bounded_position(tooltip_panel, target_pos + Vector2(14, 14))

func hide_tooltip() -> void:
	tooltip_panel.hide()
	_target = null

func show_feature_guide(guide_id: String, title: String, guide_content: String) -> void:
	if _seen.has(guide_id): return
	_generation += 1
	_active_guide_id = guide_id
	guide_title.text = title
	guide_title.visible = not title.is_empty()
	guide_text.text = guide_content
	guide_banner.show()
	guide_banner.reset_size()
	guide_banner.position = Chrome.bounded_position(guide_banner, Vector2(get_viewport_rect().size.x - guide_banner.size.x - 24, 96))

func dismiss_guide() -> void:
	_generation += 1
	guide_banner.hide()
	if _active_guide_id.is_empty(): return
	var id := _active_guide_id
	_active_guide_id = ""
	if id != "snackbar_msg":
		_seen[id] = true
		if not Engine.is_editor_hint():
			var config := ConfigFile.new()
			for key in _seen: config.set_value("seen", key, true)
			config.save("user://ui_guides.cfg")
	guide_dismissed.emit(id)

func show_message(message: String, duration := 3.0) -> void:
	show_feature_guide("snackbar_msg", "", message)
	var generation := _generation
	var timer := get_tree().create_timer(duration)
	timer.timeout.connect(func():
		if generation == _generation: dismiss_guide()
	)

func _process(_delta: float) -> void:
	if _target != null:
		var target: Variant = _target.get_ref()
		if target == null or not target.is_visible_in_tree(): hide_tooltip()
