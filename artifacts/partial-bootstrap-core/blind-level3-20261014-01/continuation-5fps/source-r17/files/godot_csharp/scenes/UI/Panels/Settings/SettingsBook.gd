@tool
extends Control
## 册子里的设置页（设计规范第六部分 §4.7）：三张索引签——
##   凡例：左页画面、声音，右页语言、无障碍；印好的选项，所选者以笔迹圈出，音量是刻度尺。
##   操作：按分组列出可改键的动作；点键帽，它被笔迹圈出，再按想要的键（Esc 放弃）；与别的动作同键时行尾盖“重键”印戳。
##   书签：存档位（贴片）与所选书签的详情；读起、夹入、撕去发 bookmark_requested，由宿主接入存档后处理。
## 读写都经 GameSettings 自动加载；无头检查里自动加载缺席时用一份不入树的私有实例（不写盘），编辑器预览只用出厂默认。
## 界面不写操作说明（设计规范第六部分 §4.2）。

signal closed
## kind：read 从此处读起 / save 夹在此页 / delete 撕去书签；slot 为书签位序号。
signal bookmark_requested(kind: String, slot: int)

const Paper := preload("res://scenes/UI/Components/Paper.gd")
const CHOICE := preload("res://scenes/UI/Components/PaperChoice.gd")
const SCALE := preload("res://scenes/UI/Components/PaperScale.gd")
const OPTION := preload("res://scenes/UI/Components/PaperOption.tscn")
const SETTINGS_SCRIPT := preload("res://scenes/Shared/Settings/GameSettings.gd")
const SLOTS := 3
const GROUPS := [
	["move", "移动", "Movement", "移動"],
	["world", "现场", "Field", "現場"],
	["dialogue", "对话", "Dialogue", "会話"],
	["replay", "回放", "Replay", "再生"],
	["system", "系统", "System", "システム"],
]
const ZH_NUMERALS := ["零", "一", "二", "三", "四", "五", "六", "七", "八", "九", "十"]
## 同一时刻能同时生效的分组：现场（移动、现场）与对话、回放互不相干，同键不算冲突；系统键与谁都比。
const CONTEXTS := {"move": "field", "world": "field", "dialogue": "dialogue", "replay": "replay", "system": "any"}
## 有意共用同一键的动作对：对话里回车既继续也确认回应。
const SHARED_KEYS := [["dialogue_continue", "dialogue_choose"]]

@export_enum("rules", "controls", "bookmarks") var page := "rules":
	set(value):
		page = value
		_render()

## 书签：[{chapter, place, time, read_to, played, picture}]；位置即序号，空字典为空位。宿主接入存档前为空。
var bookmarks: Array = []:
	set(value):
		bookmarks = value
		_render()

## 游戏中才可“夹在此页”；标题页打开时为假。
var can_save := false:
	set(value):
		can_save = value
		_render()

static var _fallback: Node
var _capture: Dictionary = {}
var _slot := 0

@onready var book: Control = $Book

func _ready() -> void:
	book.tab_selected.connect(func(id: String) -> void: page = id)
	var gs := _gs()
	if gs != null and gs.is_inside_tree():
		gs.setting_changed.connect(_on_setting_changed)
	_render()
	if not Engine.is_editor_hint():
		_focus_tab.call_deferred()

func _focus_tab() -> void:
	for b in book.tab_buttons():
		if (b as Button).button_pressed:
			(b as Button).grab_focus()

func close() -> void:
	closed.emit()

# ============================================================ settings io ===

func _gs() -> Node:
	if Engine.is_editor_hint():
		return null
	var node := get_node_or_null("/root/GameSettings") if is_inside_tree() else null
	if node != null:
		return node
	if _fallback == null:
		_fallback = SETTINGS_SCRIPT.new()
	return _fallback

func _value(section: String, key: String) -> Variant:
	var gs := _gs()
	if gs != null:
		return gs.get_value(section, key)
	if section == "language":
		return "zh_CN"
	if section == "controls":
		return true
	return SETTINGS_SCRIPT.SPEC.get(section, {}).get(key, {}).get("default")

func _store(section: String, key: String, value: Variant) -> void:
	var gs := _gs()
	if gs != null:
		gs.set_value(section, key, value)

func _on_setting_changed(section: String, _key: String, _v: Variant) -> void:
	if section == "language":
		_render.call_deferred()

func _lang() -> String:
	var gs := _gs()
	return str(gs.current_language()) if gs != null else "zh_CN"

func _t(zh: String, en: String, ja: String) -> String:
	match _lang():
		"en": return en
		"ja": return ja
	return zh

# ================================================================= render ===

func _render() -> void:
	if not is_node_ready():
		return
	_capture = {}
	book.set_tabs([
		{"id": "rules", "label": _t("凡例", "Settings", "設定")},
		{"id": "controls", "label": _t("操作", "Controls", "操作")},
		{"id": "bookmarks", "label": _t("书签", "Saves", "栞")},
	], page)
	book.clear_pages()
	match page:
		"controls": _render_controls()
		"bookmarks": _render_bookmarks()
		_: _render_rules()
	if page != "bookmarks":
		book.add_footer_button(_t("恢复此页默认", "Restore defaults", "初期値に戻す"), _reset_page)
	book.add_footer_button(_t("合上", "Close", "閉じる"), close, true)

func _section(parent: Control, text: String) -> void:
	if parent.get_child_count() > 0:
		var gap := Control.new()
		gap.custom_minimum_size.y = get_theme_constant("section_gap", "PaperBook")
		gap.mouse_filter = Control.MOUSE_FILTER_IGNORE
		parent.add_child(gap)
	var label := Label.new()
	label.theme_type_variation = &"BookSection"
	label.text = text
	parent.add_child(label)

func _row(parent: Control, text: String, control: Control) -> HBoxContainer:
	var row := PanelContainer.new()
	row.theme_type_variation = &"BookRow"
	var line := HBoxContainer.new()
	line.theme_type_variation = &"BookRowLine"
	var label := Label.new()
	label.theme_type_variation = &"BookRowLabel"
	label.text = text
	label.custom_minimum_size.x = get_theme_constant("label_width", "PaperBook")
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	# 外文行名较长时折成两行，不把行撑出页面
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	line.add_child(label)
	control.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	line.add_child(control)
	row.add_child(line)
	parent.add_child(row)
	return line

func _choice_row(parent: Control, text: String, section: String, key: String, options: Array) -> Control:
	var choice: HBoxContainer = CHOICE.new()
	choice.setup(options, _value(section, key))
	choice.changed.connect(func(v: Variant) -> void: _store(section, key, v))
	_row(parent, text, choice)
	return choice

func _scale_row(parent: Control, text: String, section: String, key: String) -> Control:
	var ruler: Range = SCALE.new()
	ruler.value = float(_value(section, key))
	ruler.value_text = _tenths
	ruler.value_changed.connect(func(v: float) -> void: _store(section, key, v))
	_row(parent, text, ruler)
	return ruler

func _tenths(v: float) -> String:
	var n := clampi(roundi(v * 10.0), 0, 10)
	if _lang() == "zh_CN":
		return "无声" if n == 0 else ZH_NUMERALS[n] + "成"
	return "%d%%" % (n * 10)

# ------------------------------------------------------------------ 凡例 ---

func _render_rules() -> void:
	var left: Control = book.left_content()
	var right: Control = book.right_content()
	var on_off := [{"value": true, "label": _t("开", "On", "オン")}, {"value": false, "label": _t("关", "Off", "オフ")}]
	_section(left, _t("画面", "Display", "画面"))
	_choice_row(left, _t("显示方式", "Window", "表示"), "graphics", "window_mode", [
		{"value": "windowed", "label": _t("窗口", "Window", "ウィンドウ")},
		{"value": "fullscreen", "label": _t("全屏", "Full", "全画面")},
		{"value": "borderless", "label": _t("无边框", "Borderless", "枠なし")},
	])
	_choice_row(left, _t("窗口倍率", "Scale", "倍率"), "graphics", "window_scale", [
		{"value": 1.0, "label": "1×"}, {"value": 1.5, "label": "1.5×"}, {"value": 2.0, "label": "2×"},
	])
	_choice_row(left, _t("垂直同步", "V-Sync", "垂直同期"), "graphics", "vsync", on_off)
	_choice_row(left, _t("帧率上限", "Frame cap", "上限"), "graphics", "max_fps", [
		{"value": 0, "label": _t("不限", "None", "なし")}, {"value": 30, "label": "30"}, {"value": 60, "label": "60"}, {"value": 120, "label": "120"},
	])
	_choice_row(left, _t("像素标题", "Pixel titles", "ドット見出し"), "graphics", "pixel_titles", on_off)
	_choice_row(left, _t("纸面纹理", "Paper grain", "紙の質感"), "graphics", "paper_texture", on_off)
	_section(left, _t("声音", "Sound", "音"))
	for pair in [["master", _t("总音量", "Master", "全体")], ["music", _t("配乐", "Music", "音楽")],
			["sfx", _t("音效", "Effects", "効果音")], ["voice", _t("人声", "Voices", "声")]]:
		_scale_row(left, pair[1], "audio", pair[0])
	_choice_row(left, _t("失焦静音", "Mute unfocused", "背面で消音"), "audio", "mute_unfocused", on_off)

	_section(right, _t("语言", "Language", "言語"))
	var untranslated := _t("译文尚未完成", "Translation not finished", "翻訳は未完成")
	_choice_row(right, _t("文字", "Text", "文字"), "language", "locale", [
		{"value": "zh_CN", "label": "中文"},
		{"value": "en", "label": "English", "disabled": true, "tooltip": untranslated},
		{"value": "ja", "label": "日本語", "disabled": true, "tooltip": untranslated},
	])
	_section(right, _t("无障碍", "Accessibility", "アクセシビリティ"))
	_choice_row(right, _t("文字大小", "Text size", "文字サイズ"), "accessibility", "text_scale", [
		{"value": 1.0, "label": _t("标准", "Normal", "標準")}, {"value": 1.25, "label": _t("大", "Large", "大")},
		{"value": 1.5, "label": _t("特大", "Larger", "特大")},
	])
	_choice_row(right, _t("出字速度", "Text speed", "文字送り"), "accessibility", "text_speed", [
		{"value": 0.5, "label": _t("慢", "Slow", "遅い")}, {"value": 1.0, "label": _t("中", "Normal", "普通")},
		{"value": 2.0, "label": _t("快", "Fast", "速い")}, {"value": 0.0, "label": _t("即现", "Instant", "瞬時")},
	])
	_choice_row(right, _t("自动续读", "Auto-advance", "オート"), "accessibility", "auto_advance", on_off)
	_choice_row(right, _t("续读间隔", "Auto delay", "オート間隔"), "accessibility", "auto_advance_delay", [
		{"value": 1.0, "label": _t("短", "Short", "短い")}, {"value": 2.0, "label": _t("中", "Medium", "普通")},
		{"value": 4.0, "label": _t("长", "Long", "長い")},
	])
	_choice_row(right, _t("简明字体", "Plain fonts", "読みやすい字体"), "accessibility", "plain_fonts", on_off)
	_choice_row(right, _t("高对比", "High contrast", "高コントラスト"), "accessibility", "high_contrast", on_off)
	_choice_row(right, _t("减少动效", "Reduce motion", "動きを減らす"), "accessibility", "reduce_motion", on_off)

# ------------------------------------------------------------------ 操作 ---

func _render_controls() -> void:
	var left: Control = book.left_content()
	var right: Control = book.right_content()
	for group in GROUPS:
		var parent: Control = left if group[0] in ["move", "world"] else right
		_section(parent, _t(group[1], group[2], group[3]))
		for action in SETTINGS_SCRIPT.CONTROL_ACTIONS:
			if str(SETTINGS_SCRIPT.ACTION_GROUPS.get(action, "")) == group[0]:
				_key_row(parent, action)
		if group[0] == "dialogue":
			_choice_row(parent, _t("快进方式", "Fast-forward", "早送り"), "controls", "hold_fast_forward", [
				{"value": true, "label": _t("按住", "Hold", "長押し")}, {"value": false, "label": _t("切换", "Toggle", "切替")},
			])

func _action_name(action: String) -> String:
	var gs := _gs()
	if gs != null:
		return str(gs.action_label(action))
	return str(SETTINGS_SCRIPT.ACTION_NAMES.get(action, [action])[0])

func _key_row(parent: Control, action: String) -> void:
	var gs := _gs()
	var keys := HBoxContainer.new()
	keys.theme_type_variation = &"PaperKeyRow"
	var events: Array = gs.action_events(action) if gs != null else []
	var clash := false
	for i in events.size():
		var cap: Button = OPTION.instantiate()
		cap.theme_type_variation = &"PaperKeyCap"
		cap.text = str(gs.event_label(events[i]))
		cap.toggled.connect(func(on: bool) -> void: _capture_toggled(cap, action, i, on))
		keys.add_child(cap)
		if not _clashes(gs, action, events[i]).is_empty():
			clash = true
	if clash:
		keys.add_child(Paper.stamp(_t("重键", "Clash", "重複"), "seal"))
	_row(parent, _action_name(action), keys)

## 真正的冲突：同一时刻都可能生效的两个动作用了同一个键。
func _clashes(gs: Node, action: String, event: InputEvent) -> Array:
	var mine := str(CONTEXTS.get(str(SETTINGS_SCRIPT.ACTION_GROUPS.get(action, "")), "any"))
	var result: Array = []
	for other in gs.find_conflicts(action, event):
		var theirs := str(CONTEXTS.get(str(SETTINGS_SCRIPT.ACTION_GROUPS.get(other, "")), "any"))
		if mine != "any" and theirs != "any" and mine != theirs:
			continue
		if [action, other] in SHARED_KEYS or [other, action] in SHARED_KEYS:
			continue
		result.append(other)
	return result

func _capture_toggled(cap: Button, action: String, index: int, on: bool) -> void:
	if on:
		if not _capture.is_empty() and is_instance_valid(_capture.button) and _capture.button != cap:
			(_capture.button as Button).set_pressed_no_signal(false)
			(_capture.button as Button).queue_redraw()
		_capture = {"button": cap, "action": action, "index": index}
	elif not _capture.is_empty() and _capture.button == cap:
		_capture = {}

## 改键中：下一个按键（或除左键、滚轮外的鼠标键）就是新键；Esc 放弃。
func _input(event: InputEvent) -> void:
	if _capture.is_empty() or Engine.is_editor_hint():
		return
	if event is InputEventKey and event.pressed and not event.echo:
		get_viewport().set_input_as_handled()
		if event.keycode == KEY_ESCAPE:
			_end_capture()
		else:
			_finish_capture(event)
	elif event is InputEventMouseButton and event.pressed and event.button_index not in [
			MOUSE_BUTTON_LEFT, MOUSE_BUTTON_WHEEL_UP, MOUSE_BUTTON_WHEEL_DOWN, MOUSE_BUTTON_WHEEL_LEFT, MOUSE_BUTTON_WHEEL_RIGHT]:
		get_viewport().set_input_as_handled()
		_finish_capture(event)

func _end_capture() -> void:
	if not _capture.is_empty() and is_instance_valid(_capture.button):
		(_capture.button as Button).set_pressed_no_signal(false)
		(_capture.button as Button).queue_redraw()
	_capture = {}

func _finish_capture(event: InputEvent) -> void:
	var action := str(_capture.action)
	var index := int(_capture.index)
	_capture = {}
	var gs := _gs()
	if gs != null:
		gs.rebind(action, index, event)
	_render.call_deferred()

func is_capturing() -> bool:
	return not _capture.is_empty()

# ------------------------------------------------------------------ 书签 ---

func _slot_data(index: int) -> Dictionary:
	if index < bookmarks.size() and bookmarks[index] is Dictionary:
		return bookmarks[index]
	return {}

func _slot_name(index: int) -> String:
	match _lang():
		"en": return "Bookmark %d" % (index + 1)
		"ja": return "栞 その%d" % (index + 1)
	return "第%s枚书签" % ZH_NUMERALS[index + 1]

func _render_bookmarks() -> void:
	var left: Control = book.left_content()
	var right: Control = book.right_content()
	_section(left, _t("书签", "Bookmarks", "栞"))
	var slots := VBoxContainer.new()
	slots.theme_type_variation = &"BookmarkSlots"
	left.add_child(slots)
	var group := ButtonGroup.new()
	for i in SLOTS:
		slots.add_child(_slot_button(i, _slot_data(i), group))
	var data := _slot_data(_slot)
	_section(right, _slot_name(_slot))
	if data.is_empty():
		var empty := Label.new()
		empty.theme_type_variation = &"BookmarkMeta"
		empty.text = _t("空白书签", "Empty", "空き")
		right.add_child(empty)
	else:
		for pair in [["chapter", _t("章节", "Chapter", "章")], ["place", _t("地点", "Place", "場所")], ["time", _t("时刻", "Time", "時刻")],
				["read_to", _t("读至", "Read to", "読了")], ["played", _t("已读", "Played", "経過")]]:
			if str(data.get(pair[0], "")) == "":
				continue
			var value := Label.new()
			value.theme_type_variation = &"BookRowValue"
			value.text = str(data.get(pair[0]))
			_row(right, pair[1], value)
	var offline := _t("存档尚未接入", "Saving is not available yet", "セーブは未対応")
	book.add_footer_button(_t("撕去书签", "Tear out", "破り取る"), _bookmark.bind("delete"), false, data.is_empty(), "")
	book.add_footer_button(_t("夹在此页", "Mark here", "挟む"), _bookmark.bind("save"), false, not can_save, "" if can_save else offline)
	book.add_footer_button(_t("从此处读起", "Read from here", "ここから読む"), _bookmark.bind("read"), false, data.is_empty(), "")

func _slot_button(index: int, data: Dictionary, group: ButtonGroup) -> Button:
	var b := Button.new()
	b.theme_type_variation = &"BookmarkButton"
	b.toggle_mode = true
	b.button_group = group
	b.custom_minimum_size.y = 120
	b.set_pressed_no_signal(index == _slot)
	b.pressed.connect(func() -> void:
		_slot = index
		_render())
	var panel := PanelContainer.new()
	panel.theme_type_variation = &"BookmarkSlotCurrent" if index == _slot else (&"BookmarkSlotEmpty" if data.is_empty() else &"BookmarkSlot")
	panel.set_anchors_preset(Control.PRESET_FULL_RECT)
	panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	b.add_child(panel)
	var line := HBoxContainer.new()
	line.theme_type_variation = &"BookRowLine"
	line.mouse_filter = Control.MOUSE_FILTER_IGNORE
	panel.add_child(line)
	if data.is_empty():
		var empty := Label.new()
		empty.theme_type_variation = &"BookmarkMeta"
		empty.text = _t("空白书签", "Empty", "空き")
		empty.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		empty.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		empty.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		line.add_child(empty)
		return b
	var picture := str(data.get("picture", ""))
	if picture != "" and ResourceLoader.exists(picture):
		var thumb := TextureRect.new()
		thumb.texture = load(picture)
		thumb.custom_minimum_size = Vector2(140, 96)
		thumb.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		thumb.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
		thumb.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		thumb.mouse_filter = Control.MOUSE_FILTER_IGNORE
		line.add_child(thumb)
	var lines := VBoxContainer.new()
	lines.theme_type_variation = &"BookmarkLines"
	lines.mouse_filter = Control.MOUSE_FILTER_IGNORE
	lines.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	line.add_child(lines)
	var title := Label.new()
	title.theme_type_variation = &"BookmarkTitle"
	title.text = str(data.get("chapter", ""))
	lines.add_child(title)
	for parts in [[data.get("place", ""), data.get("time", "")], [data.get("read_to", ""), data.get("played", "")]]:
		var shown := PackedStringArray()
		for part in parts:
			if str(part) != "": shown.append(str(part))
		if shown.is_empty():
			continue
		var meta := Label.new()
		meta.theme_type_variation = &"BookmarkMeta"
		meta.text = " · ".join(shown)
		lines.add_child(meta)
	return b

func _bookmark(kind: String) -> void:
	bookmark_requested.emit(kind, _slot)

# ================================================================== reset ===

func _reset_page() -> void:
	var gs := _gs()
	if gs == null:
		return
	if page == "controls":
		gs.reset_controls()
	else:
		for section in ["graphics", "audio", "accessibility"]:
			gs.reset_section(section)
	_render()
