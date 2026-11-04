@tool
extends VBoxContainer
## Shared, presentation-only settings page for the title and the in-game book.
## Add to a themed page container; _ready builds it once. Locale refreshes keep
## the same Controls, so focus and scroll position survive a language change.

const MaterialControls := preload("res://scenes/components/shared/MaterialControls.gd")
const SettingsSchema := preload("res://scenes/components/shared/GameSettings.gd")
const ReplayPreferences := preload("res://scenes/UIpanel/BookSettingsSpread/ReplayPreferences.gd")
const LocaleSettings := preload("res://scenes/components/shared/LocaleSettings.gd")
const ManuscriptControls := preload("res://scenes/UIpanel/CasebookUI/ManuscriptControls.gd")

var manuscript_theme: Theme
var settings_endpoint: Node
var locale_endpoint: Node
var section_indices := PackedInt32Array()
var _settings: Node
var _locale: Node
var _texts: Array[Dictionary] = []
var _bindings: Array[Dictionary] = []
var _language_buttons: Dictionary = {}
var _help_lines: Array[Dictionary] = []
var _refresh_serial := 0
var _section_picker: OptionButton
var _sections: Array[VBoxContainer] = []
var _section_sources: Array[String] = []
var _section: VBoxContainer


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	theme_type_variation = &"BookColumn"
	size_flags_horizontal = Control.SIZE_EXPAND_FILL
	if Engine.is_editor_hint():
		var preferences := ReplayPreferences.new()
		var values := {}
		for section: String in SettingsSchema.SPEC:
			values[section] = {}
			for key: String in SettingsSchema.SPEC[section]:
				values[section][key] = SettingsSchema.SPEC[section][key].default
		preferences.restore_data({"values": values})
		add_child(preferences)
		_settings = preferences
		_locale = preferences
		manuscript_theme = preload("res://scenes/UIpanel/TitleUI/TitleTheme.tres")
	else:
		_settings = settings_endpoint if settings_endpoint != null else get_node_or_null("/root/GameSettings")
		_locale = locale_endpoint if locale_endpoint != null else get_node_or_null("/root/GameLocale")
	_apply_text_scale()
	if _locale != null: _locale.language_changed.connect(func(_code: String): _refresh_content())
	_section_picker = OptionButton.new()
	_section_picker.name = "SettingsSection"
	_section_picker.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_section_picker.item_selected.connect(func(index: int):
		for i in _sections.size(): _sections[i].visible = i == index
	)
	add_child(_section_picker)
	_build_language_choices()
	if _settings != null:
		_build_settings()
		_settings.setting_changed.connect(_on_setting_changed)
	else:
		_add_text("设置暂时不可用。")
	_refresh_content()
	if not section_indices.is_empty():
		_section_picker.hide()
		for i in _sections.size(): _sections[i].visible = i in section_indices


func _notification(what: int) -> void:
	if what == NOTIFICATION_TRANSLATION_CHANGED and is_node_ready():
		refresh_translations()


func _build_language_choices() -> void:
	_add_heading("界面语言")
	var choices := HBoxContainer.new()
	choices.name = "LanguageChoices"
	_section.add_child(choices)
	var group := ButtonGroup.new()
	for code: String in LocaleSettings.LANGUAGES:
		var button := Button.new()
		button.name = "Language_" + code
		MaterialControls.prepare_button(button)
		button.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
		button.text = LocaleSettings.NATIVE_NAMES[code]
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		button.clip_text = true
		button.toggle_mode = true
		button.button_group = group
		button.disabled = _locale == null
		button.pressed.connect(_choose_language.bind(code))
		choices.add_child(button)
		_language_buttons[code] = button


func _build_settings() -> void:
	_add_heading("声音")
	_add_volume("master", "总音量")
	_add_volume("music", "音乐")
	_add_volume("sfx", "音效")
	_add_volume("voice", "语音")
	_add_toggle("audio", "mute_unfocused", "窗口失焦时静音")
	_add_heading("显示")
	_add_choice("graphics", "window_mode", "窗口模式",
		["windowed", "fullscreen", "borderless"], ["窗口", "全屏", "无边框"])
	_add_choice("graphics", "window_scale", "窗口大小",
		[1.0, 1.5, 2.0], ["100%", "150%", "200%"])
	_add_toggle("graphics", "vsync", "垂直同步")
	_add_choice("graphics", "max_fps", "帧率上限",
		[0, 30, 60, 120, 144], ["不限", "30", "60", "120", "144"])
	_add_choice("graphics", "wall_transparency", "墙体遮挡", ["dither", "alpha"], ["点阵透明", "半透明"])
	_add_heading("阅读与辅助")
	_add_toggle("accessibility", "subtitles", "显示字幕")
	_add_choice("accessibility", "text_scale", "设置页文字大小", [1.0, 1.25, 1.5], ["100%", "125%", "150%"])
	_add_toggle("accessibility", "reduce_motion", "减少动态效果")
	_add_toggle("accessibility", "screen_flash", "屏幕闪光")
	_add_choice("accessibility", "text_speed", "文字速度",
		[0.5, 1.0, 2.0, 0.0], ["缓慢", "正常", "快速", "立即显示"])
	_add_heading("操作与镜头")
	_add_choice("controls", "camera_distance", "镜头距离", [0.8, 1.0, 1.3], ["近", "标准", "远"])
	_add_choice("controls", "mouse_sensitivity", "鼠标灵敏度", [0.5, 1.0, 1.5, 2.0], ["低", "标准", "高", "超高"])


func _add_heading(source: String) -> void:
	if source == "声音" and _sections.size() == 1:
		_section_sources[0] = "语言与声音"
		_section.add_child(HSeparator.new())
		return
	_section = VBoxContainer.new()
	_section.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_section.add_theme_constant_override("separation", 12)
	_section.visible = _sections.is_empty()
	_sections.append(_section)
	_section_sources.append(source)
	_section_picker.add_item(tr(source))
	add_child(_section)


func _add_text(source: String, parent: Node = null) -> Label:
	var label := Label.new()
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	(parent if parent != null else _section if _section != null else self).add_child(label)
	_texts.append({"control": label, "source": source})
	return label


func _add_volume(key: String, source: String) -> void:
	var row := VBoxContainer.new()
	row.name = "audio_" + key
	_section.add_child(row)
	var heading := HBoxContainer.new()
	row.add_child(heading)
	_add_text(source, heading)
	var amount := Label.new()
	amount.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	heading.add_child(amount)
	var slider := HSlider.new()
	slider.name = "Value"
	slider.min_value = 0.0
	slider.max_value = 1.0
	slider.step = 0.01
	slider.custom_minimum_size.y = 28.0
	slider.value_changed.connect(_set_value.bind("audio", key))
	row.add_child(slider)
	_bindings.append({"section": "audio", "key": key, "source": source,
		"kind": "volume", "control": slider, "amount": amount})


func _add_toggle(section: String, key: String, source: String) -> void:
	var check := CheckButton.new()
	check.name = section + "_" + key
	MaterialControls.prepare_button(check)
	check.toggled.connect(_set_value.bind(section, key))
	_section.add_child(check)
	_bindings.append({"section": section, "key": key, "source": source,
		"kind": "toggle", "control": check})


func _add_choice(section: String, key: String, source: String,
		allowed: Array, captions: Array) -> void:
	# Caption left and value right, matching the toggle rows on the same page.
	var row := HBoxContainer.new()
	row.name = section + "_" + key
	_section.add_child(row)
	var label := _add_text(source, row)
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	var option := Button.new()
	option.name = "Value"
	MaterialControls.prepare_button(option)
	option.alignment = HORIZONTAL_ALIGNMENT_RIGHT
	option.size_flags_horizontal = Control.SIZE_SHRINK_END
	option.pressed.connect(_cycle_value.bind(section, key, allowed))
	row.add_child(option)
	_bindings.append({"section": section, "key": key, "source": source,
		"kind": "choice", "control": option, "allowed": allowed, "captions": captions})


func _build_help() -> void:
	_add_heading("操作说明")
	for specification: Array in [
		["移动：%s；鼠标左键点击地面。", ["move_up", "move_down", "move_left", "move_right"]],
		["现场交互：%s；鼠标悬停或点击热点。", ["interact"]],
		["继续对白：%s。", ["dialogue_continue"]],
		["返回或关闭：%s。", ["cancel"]],
		["设置：%s。", ["settings"]],
		["案卷：%s。", ["notebook"]],
		["推演：%s；地图：%s。", ["manuscript", "campus_map"]],
		["书页前后翻阅：%s。", ["book_prev", "book_next"]],
	]:
		var label := Label.new()
		label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		add_child(label)
		_help_lines.append({"control": label, "source": specification[0], "actions": specification[1]})
	_add_text("手柄方向键选择条目，右摇杆滚动纸页。")


func _choose_language(code: String) -> void:
	if _locale != null: _locale.set_language(code)


func _set_value(value: Variant, section: String, key: String) -> void:
	if _settings != null: _settings.set_value(section, key, value)


func _cycle_value(section: String, key: String, allowed: Array) -> void:
	if _settings == null: return
	var current: Variant = _settings.get_value(section, key)
	var next := (maxi(0, allowed.find(current)) + 1) % allowed.size()
	_settings.set_value(section, key, allowed[next])


func _apply_text_scale() -> void:
	var focused := get_viewport().gui_get_focus_owner() if is_inside_tree() else null
	var scale := float(_settings.get_value("accessibility", "text_scale")) if _settings != null else 1.0
	if manuscript_theme != null:
		theme = manuscript_theme.duplicate() as Theme
		theme.default_font_size = roundi(24.0 * scale)
		for kind in ["Label", "Button", "CheckButton", "OptionButton"]:
			theme.set_font_size("font_size", kind, roundi(24.0 * scale))
		for kind in ["Button", "CheckButton", "OptionButton"]:
			for state in ["normal", "hover", "pressed", "disabled", "focus"]:
				theme.set_stylebox(state, kind, StyleBoxEmpty.new())
			theme.set_color("font_color", kind, Color("33261d"))
			theme.set_color("font_pressed_color", kind, Color("8a3324"))
			theme.set_color("font_hover_color", kind, Color("8a3324"))
		ManuscriptControls.style_theme(theme, roundi(24.0 * scale))
	else:
		MaterialControls.apply(self, scale)
	if focused != null and is_ancestor_of(focused): _reveal_focus_after_layout.call_deferred(weakref(focused))

func _reveal_focus_after_layout(reference: WeakRef) -> void:
	await get_tree().process_frame
	if not is_inside_tree(): return
	var focused := reference.get_ref() as Control
	if not is_instance_valid(focused) or get_viewport().gui_get_focus_owner() != focused: return
	var ancestor := get_parent()
	while ancestor != null:
		if ancestor is ScrollContainer:
			ancestor.ensure_control_visible(focused)
			return
		ancestor = ancestor.get_parent()


func _on_setting_changed(section: String, _key: String, _value: Variant) -> void:
	if section == "accessibility" and _key == "text_scale": _apply_text_scale()
	# TranslationServer sends NOTIFICATION_TRANSLATION_CHANGED for the language.
	# Other settings can also change through another visible book instance.
	if section != "language": _refresh_content()


func refresh_translations() -> void:
	if not is_inside_tree(): return
	_apply_text_scale()
	_refresh_serial += 1
	var scrolls: Array[Dictionary] = []
	var ancestor := get_parent()
	while ancestor != null:
		if ancestor is ScrollContainer:
			scrolls.append({"node": weakref(ancestor), "vertical": ancestor.scroll_vertical,
				"horizontal": ancestor.scroll_horizontal})
		ancestor = ancestor.get_parent()
	var focused := get_viewport().gui_get_focus_owner()
	var focus_ref: WeakRef = weakref(focused) if focused != null and is_ancestor_of(focused) else null
	_refresh_content()
	_restore_view.call_deferred(scrolls, focus_ref, _refresh_serial)


func _refresh_content() -> void:
	for i in _section_sources.size(): _section_picker.set_item_text(i, tr(_section_sources[i]))
	for item: Dictionary in _texts:
		var label: Label = item.control
		label.text = tr(str(item.source))
	var current: String = str(_locale.current_language()) if _locale != null else "zh_CN"
	for code: String in _language_buttons:
		var button: Button = _language_buttons[code]
		button.set_pressed_no_signal(code == current)
	for binding: Dictionary in _bindings:
		_refresh_binding(binding)
	for item: Dictionary in _help_lines:
		var names: PackedStringArray = []
		for action: String in item.actions:
			var text: String = _settings.action_text(action) if _settings != null else ""
			if not text.is_empty(): names.append(text)
		var label: Label = item.control
		var source := str(item.source)
		if source.count("%s") > 1:
			var captions: Array[String] = []
			for action: String in item.actions:
				var caption: String = _settings.action_text(action) if _settings != null else ""
				captions.append(caption if not caption.is_empty() else tr("未绑定"))
			label.text = tr(source) % captions
		else:
			label.text = tr(source) % (" · ".join(names) if not names.is_empty() else tr("未绑定"))


func _refresh_binding(binding: Dictionary) -> void:
	var value: Variant = _settings.get_value(str(binding.section), str(binding.key))
	var caption := tr(str(binding.source))
	match str(binding.kind):
		"volume":
			var slider: HSlider = binding.control
			slider.set_value_no_signal(float(value))
			var amount: Label = binding.amount
			amount.text = "%d%%" % roundi(float(value) * 100.0)
			slider.tooltip_text = caption + " · " + amount.text
		"toggle":
			var check: CheckButton = binding.control
			check.text = caption
			check.set_pressed_no_signal(bool(value))
			check.tooltip_text = tr("已启用") if bool(value) else tr("已关闭")
		"choice":
			var option: Button = binding.control
			var allowed: Array = binding.allowed
			var selected := maxi(0, allowed.find(value))
			option.text = tr(str(binding.captions[selected])) + "　›"
			option.tooltip_text = caption + " · " + tr("选择下一项")
			if binding.key == "window_scale":
				option.disabled = _settings.get_value("graphics", "window_mode") != "windowed"
				if option.disabled: option.tooltip_text = tr("仅窗口模式可调")


func _restore_view(scrolls: Array[Dictionary], focus_ref: WeakRef, serial: int) -> void:
	if not is_inside_tree() or serial != _refresh_serial: return
	# Text minimum sizes queue container layout. Deferred property writes run
	# after that layout; ScrollContainer clamps only if the new content is shorter.
	for item: Dictionary in scrolls:
		var scroll := item.node.get_ref() as ScrollContainer
		if is_instance_valid(scroll) and scroll.is_inside_tree():
			scroll.set_deferred("scroll_vertical", int(item.vertical))
			scroll.set_deferred("scroll_horizontal", int(item.horizontal))
	if focus_ref != null and get_viewport().gui_get_focus_owner() == null:
		var focused := focus_ref.get_ref() as Control
		if is_instance_valid(focused) and focused.is_visible_in_tree() \
				and focused.focus_mode != Control.FOCUS_NONE:
			focused.grab_focus()
