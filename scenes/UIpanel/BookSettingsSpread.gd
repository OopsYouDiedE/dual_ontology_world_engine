extends Control
## 透明双页内容；纸张、书脊和翻页由外壳提供。

signal ui_event(event: Dictionary)
signal setting_changed(key: String, value: Variant)
signal close_requested

const MaterialControls := preload("res://scenes/components/shared/MaterialControls.gd")
const SettingsSchema := preload("res://scenes/components/shared/GameSettings.gd")
const INK := Color("29372e")
const MOSS := Color("53684c")
const MUTED := Color("777d6d")
const ERROR := Color("934d3d")
const CONSUMER_SETTINGS := [
	"graphics.ui_scale", "audio.page_turn", "gameplay.skip_read_only",
	"gameplay.autosave_policy", "gameplay.deduction_hints",
	"controls.wheel_mode", "controls.hold_threshold",
	"controls.camera_distance", "controls.mouse_sensitivity",
]
const ACTION_NAMES := {
	"move_up": "向前", "move_down": "向后", "move_left": "向左", "move_right": "向右",
	"interact": "交互", "notebook": "开书", "manuscript": "推演", "campus_map": "地图",
	"wait": "等待", "cancel": "返回", "dialogue_continue": "继续", "dialogue_history": "回看",
	"fast_forward": "快进", "settings": "设置", "book_prev": "上页", "book_next": "下页",
}
const WORDS := {
	"常用": ["General", "基本"], "操作": ["Controls", "操作"], "玩法": ["Preferences", "プレイ"],
	"音画": ["Sound & display", "音声・画面"], "辅助": ["Reading", "補助"], "语言": ["Language", "言語"],
	"字速": ["Text speed", "文字速度"], "缓慢": ["Slow", "遅い"], "正常": ["Normal", "標準"],
	"快速": ["Fast", "速い"], "即时": ["Instant", "即時"], "已读跳过": ["Read text only", "既読のみ"],
	"自动存档": ["Autosave", "自動保存"], "关闭": ["Off", "オフ"], "章节": ["Chapter", "章"],
	"场景": ["Scene", "場面"], "事件": ["Event", "出来事"], "推演提示": ["Inference hints", "推理ヒント"],
	"轻微": ["Subtle", "控えめ"], "完整": ["Full", "標準"], "镜头": ["Camera", "カメラ"],
	"距离": ["Distance", "距離"], "近": ["Near", "近い"], "标准": ["Standard", "標準"], "远": ["Far", "遠い"],
	"灵敏度": ["Sensitivity", "感度"], "窗口": ["Window", "ウィンドウ"], "窗口模式": ["Window mode", "表示モード"],
	"全屏": ["Fullscreen", "全画面"], "无边框": ["Borderless", "枠なし"], "分辨率": ["Resolution", "解像度"],
	"默认": ["Default", "既定"], "帧率": ["Frame limit", "フレーム上限"], "不限": ["Unlimited", "無制限"],
	"垂直同步": ["V-sync", "垂直同期"], "界面缩放": ["Interface scale", "UI 倍率"], "声音": ["Sound", "音声"],
	"总音量": ["Master", "全体"], "音乐": ["Music", "音楽"], "音效": ["Effects", "効果音"],
	"语音": ["Voice", "ボイス"], "翻页": ["Page turns", "ページ音"], "失焦静音": ["Mute unfocused", "非選択時ミュート"],
	"键鼠": ["Keyboard", "キーボード"], "手柄": ["Controller", "コントローラー"],
	"滚轮": ["Mouse wheel", "ホイール"], "缩放": ["Zoom", "ズーム"], "翻书": ["Turn pages", "ページ送り"],
	"长按": ["Hold duration", "長押し"], "恢复键位": ["Reset bindings", "割当を戻す"],
	"字体": ["Typeface", "書体"], "书页": ["Book", "書籍"], "清晰": ["Plain", "明瞭"],
	"字号": ["Text size", "文字サイズ"], "高对比": ["High contrast", "高コントラスト"],
	"减少动态": ["Reduce motion", "動きを抑える"], "屏幕闪光": ["Screen flashes", "画面の閃光"],
	"速查": ["Quick reference", "早見表"], "点选": ["Select", "選ぶ"], "开书": ["Book", "手帳"],
	"返回": ["Back", "戻る"], "左键": ["Left click", "左クリック"], "右键": ["Right click", "右クリック"],
	"向前": ["Forward", "前へ"], "向后": ["Back", "後ろへ"], "向左": ["Left", "左へ"], "向右": ["Right", "右へ"],
	"交互": ["Interact", "調べる"], "推演": ["Infer", "推理"], "地图": ["Map", "地図"], "等待": ["Wait", "待つ"],
	"继续": ["Continue", "続ける"], "回看": ["History", "履歴"], "快进": ["Skip", "早送り"],
	"设置": ["Settings", "設定"], "上页": ["Previous", "前頁"], "下页": ["Next", "次頁"],
	"取消": ["Cancel", "取消"], "等待按键…": ["Press an input…", "入力を待機中…"],
	"已恢复": ["Restored", "復元済み"], "已更改": ["Updated", "変更済み"],
	"暂不可调": ["Unavailable", "変更できません"], "设置不可用": ["Settings unavailable", "設定を開けません"],
	"全屏跟随屏幕": ["Display resolution", "画面サイズ"],
}

var _data: Dictionary = {}
var _settings: Node
var _bindings: Array[Dictionary] = []
var _texts: Array[Dictionary] = []
var _pages: Array[Control] = []
var _action_buttons: Dictionary = {}
var _device_buttons: Dictionary = {}
var _fit_buttons: Array[Button] = []
var _current_pair := 0
var _device_kind := "keyboard"
var _capturing := ""
var _capture_bar: HBoxContainer
var _capture_inset: Control
var _capture_label: Label
var _cancel_capture: Button
var _reset_keys: Button
var _diagram: Control
var _built := false
var corner_tab_a: Button
var corner_tab_b: Button

var left_root: Control:
	get: return _pages[_current_pair * 2] if _pages.size() == 4 else null
var right_root: Control:
	get: return _pages[_current_pair * 2 + 1] if _pages.size() == 4 else null


func configure(data: Dictionary, _book_data: RefCounted = null) -> void:
	_data = data.duplicate()
	if _built:
		_connect_settings()
		_refresh()


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	mouse_filter = Control.MOUSE_FILTER_PASS
	size = Vector2(1600, 1040)
	_connect_settings()
	_build()
	_built = true
	_apply_presentation()
	_switch_pair(_current_pair)
	visibility_changed.connect(_on_visibility_changed)
	_refresh()


func _connect_settings() -> void:
	var next: Node = _data.get("settings_endpoint") as Node
	if next == null and not Engine.is_editor_hint(): next = get_node_or_null("/root/GameSettings")
	if is_instance_valid(_settings) and _settings.has_signal("setting_changed") and _settings.setting_changed.is_connected(_on_setting_changed):
		_settings.setting_changed.disconnect(_on_setting_changed)
	_settings = next
	if _settings != null and _settings.has_signal("setting_changed"):
		_settings.setting_changed.connect(_on_setting_changed)


func _build() -> void:
	corner_tab_a = _button(self, "常用", "CornerTabA")
	corner_tab_b = _button(self, "操作", "CornerTabB")
	for index in 2:
		var tab: Button = corner_tab_a if index == 0 else corner_tab_b
		tab.position = Vector2(1204 + index * 154, 28)
		tab.size = Vector2(138, 46)
		tab.toggle_mode = true
		tab.pressed.connect(_switch_pair.bind(index))
	var play := _page("Gameplay", "玩法", false)
	_add_choice(play, "language", "locale", "语言", ["zh_CN", "en", "ja"], ["简体中文", "English", "日本語"])
	_add_choice(play, "accessibility", "text_speed", "字速", [0.5, 1.0, 2.0, 0.0], ["缓慢", "正常", "快速", "即时"])
	_add_toggle(play, "gameplay", "skip_read_only", "已读跳过")
	_add_choice(play, "gameplay", "autosave_policy", "自动存档", ["off", "chapter", "scene", "event"], ["关闭", "章节", "场景", "事件"])
	_add_choice(play, "gameplay", "deduction_hints", "推演提示", ["off", "subtle", "standard"], ["关闭", "轻微", "完整"])
	_space(play, 44)
	_section(play, "镜头")
	_add_choice(play, "controls", "camera_distance", "距离", [0.8, 1.0, 1.3], ["近", "标准", "远"])
	_add_slider(play, "controls", "mouse_sensitivity", "灵敏度", 0.2, 2.5, 0.1, "scale")
	var audiovisual := _page("Audiovisual", "音画", true)
	_add_choice(audiovisual, "graphics", "window_mode", "窗口模式", ["windowed", "fullscreen", "borderless"], ["窗口", "全屏", "无边框"])
	_add_choice(audiovisual, "graphics", "resolution", "分辨率", ["default", "1280x720", "1600x900", "1920x1080", "2560x1440"], ["默认", "1280 × 720", "1600 × 900", "1920 × 1080", "2560 × 1440"])
	_add_choice(audiovisual, "graphics", "max_fps", "帧率", [0, 30, 60, 120, 144], ["不限", "30", "60", "120", "144"])
	_add_toggle(audiovisual, "graphics", "vsync", "垂直同步")
	_add_choice(audiovisual, "graphics", "ui_scale", "界面缩放", [0.85, 1.0, 1.15, 1.25], ["85%", "100%", "115%", "125%"])
	_section(audiovisual, "声音")
	for item: Array in [["master", "总音量"], ["music", "音乐"], ["sfx", "音效"], ["voice", "语音"], ["page_turn", "翻页"]]:
		_add_slider(audiovisual, str(item[0]), str(item[1]))
	_add_toggle(audiovisual, "audio", "mute_unfocused", "失焦静音")
	var operations := _page("Operations", "操作", false)
	_build_bindings(operations)
	var reading := _page("Reading", "辅助", true)
	_add_choice(reading, "accessibility", "plain_fonts", "字体", [false, true], ["书页", "清晰"])
	_add_choice(reading, "accessibility", "text_scale", "字号", [1.0, 1.25, 1.5], ["100%", "125%", "150%"])
	_add_toggle(reading, "accessibility", "high_contrast", "高对比")
	_add_toggle(reading, "accessibility", "reduce_motion", "减少动态")
	_add_toggle(reading, "accessibility", "screen_flash", "屏幕闪光")
	_space(reading, 44)
	_section(reading, "速查")
	_diagram = Control.new()
	_diagram.name = "InteractionSketch"
	_diagram.custom_minimum_size = Vector2(638, 248)
	_diagram.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_diagram.resized.connect(queue_redraw)
	reading.add_child(_diagram)


func _page(node_name: String, heading: String, right: bool) -> VBoxContainer:
	var page := VBoxContainer.new()
	page.name = node_name
	page.position = Vector2(868 if right else 84, 106)
	page.size = Vector2(648, 840)
	page.add_theme_constant_override("separation", 8)
	page.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(page)
	_pages.append(page)
	var title := _label(page, heading)
	title.custom_minimum_size.y = 62
	title.set_meta("heading", true)
	var rule := HSeparator.new()
	rule.modulate = Color(1, 1, 1, 0.5)
	page.add_child(rule)
	_space(page, 8)
	return page


func _label(parent: Node, source: String) -> Label:
	var label := Label.new()
	label.text = _t(source)
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	parent.add_child(label)
	_texts.append({"node": label, "source": source})
	return label


func _button(parent: Node, source: String, node_name := "") -> Button:
	var button := Button.new()
	button.name = node_name if not node_name.is_empty() else source
	button.text = _t(source)
	button.flat = true
	button.clip_text = true
	MaterialControls.prepare_button(button)
	button.custom_minimum_size.y = 44
	button.resized.connect(_fit_button_text.bind(button))
	_fit_buttons.append(button)
	parent.add_child(button)
	if not source.is_empty(): _texts.append({"node": button, "source": source})
	return button


func _row(parent: Node, source: String) -> HBoxContainer:
	var row := HBoxContainer.new()
	row.custom_minimum_size.y = 48
	row.add_theme_constant_override("separation", 18)
	parent.add_child(row)
	var caption := _label(row, source)
	caption.custom_minimum_size.x = 228
	caption.clip_text = true
	return row


func _add_choice(parent: Node, section: String, key: String, source: String, values: Array, captions: Array) -> void:
	var row := _row(parent, source)
	row.name = section + "_" + key
	var button := _button(row, "", "Value")
	button.alignment = HORIZONTAL_ALIGNMENT_RIGHT
	button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	button.pressed.connect(_cycle.bind(section, key, values, 1))
	button.gui_input.connect(func(event: InputEvent):
		if event.is_action_pressed("ui_left"):
			_cycle(section, key, values, -1)
			button.accept_event()
		elif event.is_action_pressed("ui_right"):
			_cycle(section, key, values, 1)
			button.accept_event()
	)
	_bindings.append({"section": section, "key": key, "kind": "choice", "control": button, "values": values, "captions": captions})


func _add_toggle(parent: Node, section: String, key: String, source: String) -> void:
	var row := _row(parent, source)
	row.name = section + "_" + key
	var gap := Control.new()
	gap.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	gap.mouse_filter = Control.MOUSE_FILTER_IGNORE
	row.add_child(gap)
	var check := CheckButton.new()
	check.name = "Value"
	MaterialControls.prepare_button(check)
	check.custom_minimum_size = Vector2(86, 44)
	check.toggled.connect(func(value: bool): _write(section, key, value))
	row.add_child(check)
	_bindings.append({"section": section, "key": key, "kind": "toggle", "control": check})


## 三参形式用于音量；完整形式用于镜头灵敏度和长按时间。
func _add_slider(parent: Node, section_or_key: String, key_or_source: String, source := "", low := 0.0, high := 1.0, step_size := 0.01, format_kind := "percent") -> void:
	var section := "audio" if source.is_empty() else section_or_key
	var key := section_or_key if source.is_empty() else key_or_source
	var caption := key_or_source if source.is_empty() else source
	var row := _row(parent, caption)
	row.name = section + "_" + key
	var slider := HSlider.new()
	slider.name = "Value"
	slider.min_value = low
	slider.max_value = high
	slider.step = step_size
	slider.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	slider.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	slider.custom_minimum_size.y = 38
	slider.focus_mode = Control.FOCUS_ALL
	slider.value_changed.connect(func(value: float): _write(section, key, value))
	row.add_child(slider)
	var amount := Label.new()
	amount.custom_minimum_size.x = 86
	amount.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	amount.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	row.add_child(amount)
	_bindings.append({"section": section, "key": key, "kind": "slider", "control": slider, "amount": amount, "format": format_kind})


func _section(parent: Node, source: String) -> void:
	var label := _label(parent, source)
	label.custom_minimum_size.y = 32
	label.set_meta("section", true)


func _space(parent: Node, height: float) -> void:
	var spacer := Control.new()
	spacer.custom_minimum_size.y = height
	if height <= 8.0: spacer.set_meta("compact_space", height)
	spacer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(spacer)


func _build_bindings(parent: Node) -> void:
	var selector := HBoxContainer.new()
	selector.add_theme_constant_override("separation", 18)
	parent.add_child(selector)
	for item: Array in [["keyboard", "键鼠"], ["gamepad", "手柄"]]:
		var button := _button(selector, str(item[1]), str(item[0]))
		button.custom_minimum_size.x = 180
		button.toggle_mode = true
		button.pressed.connect(_select_device.bind(str(item[0])))
		_device_buttons[str(item[0])] = button
	var grid := GridContainer.new()
	grid.name = "Bindings"
	grid.columns = 2
	grid.add_theme_constant_override("h_separation", 28)
	grid.add_theme_constant_override("v_separation", 5)
	parent.add_child(grid)
	for action: String in SettingsSchema.CONTROL_ACTIONS:
		var row := HBoxContainer.new()
		row.name = action
		row.custom_minimum_size = Vector2(310, 46)
		grid.add_child(row)
		var name_label := _label(row, str(ACTION_NAMES.get(action, action)))
		name_label.custom_minimum_size.x = 110
		name_label.clip_text = true
		var binding := _button(row, "", "Binding")
		binding.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		binding.alignment = HORIZONTAL_ALIGNMENT_RIGHT
		binding.pressed.connect(_start_capture.bind(action))
		_action_buttons[action] = binding
	_space(parent, 8)
	_add_choice(parent, "controls", "wheel_mode", "滚轮", ["zoom", "pages"], ["缩放", "翻书"])
	_add_slider(parent, "controls", "hold_threshold", "长按", 0.2, 1.0, 0.05, "seconds")
	_capture_bar = HBoxContainer.new()
	_capture_bar.add_theme_constant_override("separation", 12)
	parent.add_child(_capture_bar)
	_capture_inset = Control.new()
	_capture_inset.name = "StampClearance"
	_capture_inset.custom_minimum_size.x = 36.0
	_capture_inset.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_capture_inset.hide()
	_capture_bar.add_child(_capture_inset)
	_reset_keys = _button(_capture_bar, "恢复键位", "ResetBindings")
	_reset_keys.custom_minimum_size.x = 170
	_reset_keys.pressed.connect(_on_reset_keys_pressed)
	_capture_label = Label.new()
	_capture_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_capture_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_capture_label.clip_text = true
	_capture_bar.add_child(_capture_label)
	_cancel_capture = _button(_capture_bar, "取消", "CancelCapture")
	_cancel_capture.custom_minimum_size.x = 96
	_cancel_capture.pressed.connect(_stop_capture)
	_cancel_capture.hide()


func _read(section: String, key: String) -> Variant:
	if _settings != null:
		var value: Variant = _settings.get_value(section, key)
		if value != null: return value
	if section == "language": return "zh_CN"
	return SettingsSchema.SPEC.get(section, {}).get(key, {}).get("default")


func _supported(section: String, key: String) -> bool:
	if _settings == null: return false
	var id := section + "." + key
	if not CONSUMER_SETTINGS.has(id): return true
	var capabilities: Variant = _data.get("settings_capabilities", [])
	return bool(capabilities.get(id, false)) if capabilities is Dictionary else id in capabilities


func set_setting_capabilities(capabilities: Array) -> void:
	_data["settings_capabilities"] = capabilities.duplicate()
	if _built: _refresh()


func _write(section: String, key: String, value: Variant) -> void:
	if not _supported(section, key): return
	if _settings.set_value(section, key, value):
		var actual: Variant = _settings.get_value(section, key)
		setting_changed.emit(key, actual)
		ui_event.emit({"type": "setting_changed", "section": section, "key": key, "value": actual})
	_refresh()


func _cycle(section: String, key: String, values: Array, direction: int) -> void:
	if section == "graphics" and key == "resolution" and _read("graphics", "window_mode") != "windowed": return
	var index := maxi(0, values.find(_read(section, key)))
	_write(section, key, values[posmod(index + direction, values.size())])


func _refresh() -> void:
	if not _built: return
	for item: Dictionary in _texts:
		item.node.text = _t(str(item.source))
	for binding: Dictionary in _bindings:
		var section := str(binding.section)
		var key := str(binding.key)
		var value: Variant = _read(section, key)
		var available := _supported(section, key)
		var control: Control = binding.control
		control.tooltip_text = "" if available else _t("暂不可调" if _settings != null else "设置不可用")
		match str(binding.kind):
			"choice":
				var option := control as Button
				var allowed: Array = binding["values"]
				var chosen := maxi(0, allowed.find(value))
				option.text = _t(str(binding.captions[chosen])) + "  ›"
				option.disabled = not available
				if section == "graphics" and key == "resolution" and _read("graphics", "window_mode") != "windowed":
					option.disabled = true
					option.tooltip_text = _t("全屏跟随屏幕")
					option.text = _t("全屏跟随屏幕")
			"toggle":
				var check := control as CheckButton
				check.set_pressed_no_signal(bool(value))
				check.disabled = not available
			"slider":
				var slider := control as HSlider
				slider.set_value_no_signal(float(value))
				slider.editable = available
				slider.modulate.a = 1.0 if available else 0.4
				var amount: Label = binding.amount
				match str(binding.format):
					"seconds": amount.text = "%.2f s" % float(value)
					"scale": amount.text = "%.1f×" % float(value)
					_: amount.text = "%d%%" % roundi(float(value) * 100.0)
	for action: String in _action_buttons:
		var button: Button = _action_buttons[action]
		button.text = "…" if action == _capturing else _binding_text(action, true)
		button.tooltip_text = _binding_text(action, false)
		button.disabled = _settings == null or not _settings.has_method("rebind_action")
	for kind: String in _device_buttons:
		_device_buttons[kind].set_pressed_no_signal(kind == _device_kind)
	_reset_keys.disabled = _settings == null or not _settings.has_method("reset_bindings")
	corner_tab_a.set_pressed_no_signal(_current_pair == 0)
	corner_tab_b.set_pressed_no_signal(_current_pair == 1)
	for button: Button in _fit_buttons: _fit_button_text(button)
	queue_redraw()


func _fit_button_text(button: Button) -> void:
	if not _built or theme == null or button.text.is_empty(): return
	var font: Font = button.get_theme_font("font")
	if font == null: return
	var available_width := maxf(72.0, button.size.x - 8.0)
	var fitted_size := theme.get_font_size("font_size", "Button")
	# 按键名保持完整；只有宽度不足的键帽收小，正文仍按设置缩放。
	while fitted_size > 18 and font.get_string_size(button.text, HORIZONTAL_ALIGNMENT_LEFT, -1.0, fitted_size).x > available_width:
		fitted_size -= 1
	if button.get_theme_font_size("font_size") != fitted_size:
		button.add_theme_font_size_override("font_size", fitted_size)


func _binding_text(action: String, compact: bool) -> String:
	if _settings != null and _settings.has_method("action_device_text"):
		return _settings.action_device_text(action, _device_kind, compact)
	return "—"


func _apply_presentation() -> void:
	MaterialControls.apply(self)
	var factor := float(_read("accessibility", "text_scale")) * float(_read("graphics", "ui_scale"))
	var text_size := roundi(24.0 * factor)
	_capture_inset.visible = factor >= 1.75
	var contrast := bool(_read("accessibility", "high_contrast"))
	var ink := Color("101c12") if contrast else INK
	var primary := Color("173d22") if contrast else MOSS
	for kind in ["Label", "Button", "CheckButton"]:
		theme.set_font_size("font_size", kind, text_size)
		theme.set_color("font_color", kind, ink)
	for kind in ["Button", "CheckButton"]:
		# 纸上的短选项不用厚按钮边距；大字号仍留在同一对开内。
		for state in ["normal", "disabled"]:
			theme.set_stylebox(state, kind, StyleBoxEmpty.new())
		for state in ["hover", "pressed", "focus"]:
			var box := StyleBoxFlat.new()
			box.bg_color = Color(primary, 0.10 if state != "focus" else 0.0)
			box.content_margin_left = 4.0
			box.content_margin_right = 4.0
			box.content_margin_top = 1.0
			box.content_margin_bottom = 1.0
			box.border_color = primary
			box.border_width_bottom = 2 if state == "pressed" else 1 if state == "focus" else 0
			theme.set_stylebox(state, kind, box)
		theme.set_color("font_hover_color", kind, primary)
		theme.set_color("font_pressed_color", kind, primary)
		theme.set_color("font_hover_pressed_color", kind, primary)
		theme.set_color("font_focus_color", kind, primary)
		theme.set_color("font_disabled_color", kind, Color(ink, 0.4))
	if bool(_read("accessibility", "plain_fonts")):
		var plain := SystemFont.new()
		plain.font_names = PackedStringArray(["Noto Sans CJK SC", "Microsoft YaHei", "PingFang SC", "sans-serif"])
		theme.default_font = plain
		for kind in ["Label", "Button", "CheckButton"]: theme.set_font("font", kind, plain)
	for item: Dictionary in _texts:
		var node: Control = item.node
		if node.has_meta("heading"): node.add_theme_font_size_override("font_size", 42)
		elif node.has_meta("section"):
			node.add_theme_font_size_override("font_size", 24)
			node.add_theme_color_override("font_color", primary)
	for page: Control in _pages:
		var compact_bindings := page.name == "Operations" and factor >= 1.75
		page.add_theme_constant_override("separation", 1 if compact_bindings else 4 if factor >= 1.5 else 8)
		var bindings_grid: GridContainer = page.get_node_or_null("Bindings") as GridContainer
		if bindings_grid != null: bindings_grid.add_theme_constant_override("v_separation", 0 if compact_bindings else 5)
		for child: Node in page.get_children():
			if child is Control and child.has_meta("compact_space"):
				child.custom_minimum_size.y = 0.0 if compact_bindings else float(child.get_meta("compact_space"))
	queue_redraw()


func _on_setting_changed(section: String, key: String, _value: Variant) -> void:
	if (section == "accessibility" and key in ["text_scale", "plain_fonts", "high_contrast"]) or (section == "graphics" and key == "ui_scale"):
		_apply_presentation()
	_refresh()


func _select_device(kind: String) -> void:
	_stop_capture()
	_device_kind = kind
	_capture_label.text = ""
	_refresh()


func _start_capture(action: String) -> void:
	if _settings == null or not _settings.has_method("rebind_action"): return
	_capturing = action
	_capture_label.text = _t("等待按键…")
	_capture_label.add_theme_color_override("font_color", MOSS)
	_cancel_capture.show()
	ui_event.emit({"type": "binding_capture", "active": true, "action": action})
	_refresh()


func _stop_capture() -> void:
	var was_active := not _capturing.is_empty()
	_capturing = ""
	if _cancel_capture != null: _cancel_capture.hide()
	if _capture_label != null: _capture_label.text = ""
	if was_active: ui_event.emit({"type": "binding_capture", "active": false})
	_refresh()


func is_capturing_input() -> bool:
	return not _capturing.is_empty()


func _input(event: InputEvent) -> void:
	if _capturing.is_empty() or not is_visible_in_tree(): return
	get_viewport().set_input_as_handled()
	if event is InputEventKey and event.pressed and event.keycode == KEY_ESCAPE:
		_stop_capture()
		return
	if event is InputEventMouseButton and event.pressed:
		if event.button_index == MOUSE_BUTTON_RIGHT or (event.button_index == MOUSE_BUTTON_LEFT and _cancel_capture.get_global_rect().has_point(event.position)):
			_stop_capture()
			return
	if event is InputEventJoypadButton and event.pressed and event.button_index == JOY_BUTTON_B:
		_stop_capture()
		return
	if event is InputEventKey and (not event.pressed or event.echo): return
	if event is InputEventMouseButton and not event.pressed: return
	if event is InputEventJoypadButton and not event.pressed: return
	if event is InputEventJoypadMotion and absf(event.axis_value) < 0.7: return
	if not (event is InputEventKey or event is InputEventMouseButton or event is InputEventJoypadButton or event is InputEventJoypadMotion): return
	var result: Dictionary = _settings.rebind_action(_capturing, event, _device_kind)
	if bool(result.get("ok", false)):
		var action := _capturing
		_stop_capture()
		_capture_label.text = _t("已更改") + " · " + str(result.get("label", ""))
		ui_event.emit({"type": "binding_changed", "action": action, "device": _device_kind})
	elif str(result.get("reason", "")) == "conflict":
		var conflicts: Array = result.get("conflicts", [])
		var other := _t(str(ACTION_NAMES.get(str(conflicts[0]), str(conflicts[0])))) if not conflicts.is_empty() else ""
		_capture_label.text = "↔ " + other
		_capture_label.add_theme_color_override("font_color", ERROR)
		ui_event.emit({"type": "binding_conflict", "action": _capturing, "conflicts": conflicts})
	_refresh()


func _on_reset_keys_pressed() -> void:
	_stop_capture()
	if _settings == null or not _settings.has_method("reset_bindings"): return
	_settings.reset_bindings(_device_kind)
	_capture_label.text = _t("已恢复")
	_capture_label.add_theme_color_override("font_color", MOSS)
	ui_event.emit({"type": "bindings_reset", "device": _device_kind})
	_refresh()


func _switch_pair(pair: int) -> void:
	_stop_capture()
	_current_pair = clampi(pair, 0, 1)
	for index in _pages.size(): _pages[index].visible = (index < 2) == (_current_pair == 0)
	_refresh()
	ui_event.emit({"type": "settings_pair_changed", "pair": _current_pair})


func show_pair(pair: int) -> void:
	_switch_pair(pair)
	show()


func restore_focus() -> void:
	if not _built: return
	(corner_tab_a if _current_pair == 0 else corner_tab_b).grab_focus()


func localize() -> void:
	_refresh()


func _notification(what: int) -> void:
	if what == NOTIFICATION_TRANSLATION_CHANGED and _built: _refresh()


func _on_visibility_changed() -> void:
	if not is_visible_in_tree(): _stop_capture()


func _t(source: String) -> String:
	var locale := TranslationServer.get_locale()
	if WORDS.has(source):
		if locale.begins_with("en"): return str(WORDS[source][0])
		if locale.begins_with("ja"): return str(WORDS[source][1])
	return source


func _draw() -> void:
	if not _built: return
	var primary := Color("173d22") if bool(_read("accessibility", "high_contrast")) else MOSS
	for index in 2:
		var x := 1324.0 + index * 154.0
		draw_colored_polygon(PackedVector2Array([Vector2(x, 28), Vector2(x + 18, 28), Vector2(x + 18, 46)]), primary if index == _current_pair else Color(primary, 0.2))
	var font := get_theme_font("font", "Label")
	draw_string(font, Vector2(374, 1006), "%02d" % (_current_pair * 2 + 1), HORIZONTAL_ALIGNMENT_CENTER, 52, 20, MUTED)
	draw_string(font, Vector2(1174, 1006), "%02d" % (_current_pair * 2 + 2), HORIZONTAL_ALIGNMENT_CENTER, 52, 20, MUTED)
	if _current_pair != 1 or not is_instance_valid(_diagram): return
	var origin := get_global_transform().affine_inverse() * _diagram.global_position
	var tint := Color(primary, 0.15)
	for i in 3:
		var x := origin.x + 18 + i * 214
		var y := origin.y + 12
		if i == 0:
			draw_rect(Rect2(x + 18, y + 25, 44, 66), tint, true)
			draw_rect(Rect2(x + 18, y + 25, 44, 66), primary, false, 2)
			draw_circle(Vector2(x + 54, y + 63), 2.5, primary)
			draw_colored_polygon(PackedVector2Array([Vector2(x + 42, y + 80), Vector2(x + 42, y + 112), Vector2(x + 52, y + 103), Vector2(x + 62, y + 102)]), primary)
			for n in 3:
				draw_rect(Rect2(x + 94 + (4 if n == 1 else 0), y + 34 + n * 25, 70, 17), tint, true)
				draw_line(Vector2(x + 102, y + 42 + n * 25), Vector2(x + 151, y + 42 + n * 25), primary, 2)
		elif i == 1:
			var points := PackedVector2Array([Vector2(x + 28, y + 37), Vector2(x + 86, y + 43), Vector2(x + 145, y + 37), Vector2(x + 145, y + 112), Vector2(x + 86, y + 119), Vector2(x + 28, y + 112)])
			draw_colored_polygon(points, tint)
			draw_polyline(points, primary, 2, true)
			draw_line(points[5], points[0], primary, 2, true)
			draw_line(Vector2(x + 86, y + 43), Vector2(x + 86, y + 119), primary, 2)
		else:
			draw_arc(Vector2(x + 91, y + 76), 36, PI, TAU, 24, primary, 2, true)
			draw_line(Vector2(x + 55, y + 76), Vector2(x + 55, y + 120), primary, 2)
			draw_line(Vector2(x + 127, y + 76), Vector2(x + 127, y + 120), primary, 2)
			draw_arc(Vector2(x + 91, y + 120), 36, 0, PI, 24, primary, 2, true)
			draw_line(Vector2(x + 91, y + 40), Vector2(x + 91, y + 88), primary, 2)
			draw_rect(Rect2(x + 96, y + 56, 18, 22), primary, true)
		var captions := ["点选", "开书", "返回"]
		var keys: Array[String] = [_t("左键"), _binding_text("notebook", true), _t("右键")]
		if _device_kind == "gamepad": keys = [_binding_text("interact", true), _binding_text("notebook", true), "B"]
		draw_string(font, Vector2(x, y + 184), _t(captions[i]), HORIZONTAL_ALIGNMENT_CENTER, 178, 25, primary)
		draw_string(font, Vector2(x, y + 220), keys[i], HORIZONTAL_ALIGNMENT_CENTER, 178, 20, MUTED)

