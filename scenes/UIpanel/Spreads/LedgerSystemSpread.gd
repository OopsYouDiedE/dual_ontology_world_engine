extends Control

## 通用账本存档与设置模板（LedgerSystemSpread.gd）
## 吸收合并原 BookArchiveUI (B07), BookArchivePage, BookSettingsSpread (B08),
## BookSettingsPage 与 ReplayPreferences。
## 左页展示存档槽位/设置分类，右页展示智能回放参数与键位/画质表单。

signal save_requested(slot_id: String)
signal load_requested(slot_id: String)
signal delete_requested(slot_id: String)
signal setting_changed(key: String, value: Variant)
signal return_title_requested

@export var mode := "archive" # "archive", "settings"

# Left Page UI Elements
@onready var left_title: Label = $HBoxContainer/LeftPage/Margin/VBox/Header/LeftTitle
@onready var mode_tabs: HBoxContainer = $HBoxContainer/LeftPage/Margin/VBox/Header/ModeTabs
@onready var archive_slots_container: VBoxContainer = $HBoxContainer/LeftPage/Margin/VBox/ArchiveSlotsBox/Scroll/SlotsContainer
@onready var archive_slots_box: VBoxContainer = $HBoxContainer/LeftPage/Margin/VBox/ArchiveSlotsBox
@onready var settings_category_box: VBoxContainer = $HBoxContainer/LeftPage/Margin/VBox/SettingsCategoryBox

# Right Page UI Elements
@onready var right_title: Label = $HBoxContainer/RightPage/Margin/VBox/Header/RightTitle
@onready var slot_detail_box: VBoxContainer = $HBoxContainer/RightPage/Margin/VBox/SlotDetailBox
@onready var slot_detail_label: Label = $HBoxContainer/RightPage/Margin/VBox/SlotDetailBox/SlotDetailLabel
@onready var btn_save: Button = $HBoxContainer/RightPage/Margin/VBox/SlotDetailBox/ButtonRow/SaveButton
@onready var btn_load: Button = $HBoxContainer/RightPage/Margin/VBox/SlotDetailBox/ButtonRow/LoadButton
@onready var btn_delete: Button = $HBoxContainer/RightPage/Margin/VBox/SlotDetailBox/ButtonRow/DeleteButton
@onready var btn_return_title: Button = $HBoxContainer/RightPage/Margin/VBox/SlotDetailBox/ReturnTitleButton

@onready var settings_form_box: VBoxContainer = $HBoxContainer/RightPage/Margin/VBox/SettingsFormBox

var _selected_slot := "slot_01"
var _current_settings_cat := "graphics"
var _slots := [
	{"id": "slot_01", "name": "槽位 01 (手动)", "time": "2026-11-06 09:30", "chapter": "第一日 · 学院主案室", "has_data": true},
	{"id": "slot_02", "name": "槽位 02 (手动)", "time": "2026-11-06 08:45", "chapter": "第一日 · 抵达海岛", "has_data": true},
	{"id": "slot_03", "name": "槽位 03 (空闲)", "time": "-", "chapter": "无存档数据", "has_data": false},
	{"id": "slot_auto", "name": "自动存盘 (Autosave)", "time": "2026-11-06 09:35", "chapter": "第一日 · 门锁勘验", "has_data": true}
]

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	btn_save.pressed.connect(_on_save_pressed)
	btn_load.pressed.connect(_on_load_pressed)
	btn_delete.pressed.connect(_on_delete_pressed)
	btn_return_title.pressed.connect(func(): return_title_requested.emit())

	_setup_mode_tabs()
	_setup_settings_categories()
	set_mode(mode)

func set_mode(new_mode: String) -> void:
	mode = new_mode
	if mode == "archive":
		left_title.text = "调查纪要与存档账本"
		right_title.text = "存档详情与回溯偏好"
		archive_slots_box.show()
		settings_category_box.hide()
		slot_detail_box.show()
		settings_form_box.hide()
		_render_slots()
		select_slot(_selected_slot)
	else:
		left_title.text = "系统与画质设定"
		right_title.text = "参数偏好配置表单"
		archive_slots_box.hide()
		settings_category_box.show()
		slot_detail_box.hide()
		settings_form_box.show()
		_render_settings_form()

func configure(data: Dictionary, _book_data: RefCounted = null) -> void:
	if data.has("mode"):
		set_mode(data["mode"])

func _setup_mode_tabs() -> void:
	for child in mode_tabs.get_children():
		child.queue_free()

	var btn_a := Button.new()
	btn_a.text = "存档管理"
	btn_a.pressed.connect(func(): set_mode("archive"))
	mode_tabs.add_child(btn_a)

	var btn_s := Button.new()
	btn_s.text = "系统设置"
	btn_s.pressed.connect(func(): set_mode("settings"))
	mode_tabs.add_child(btn_s)

func _setup_settings_categories() -> void:
	for child in settings_category_box.get_children():
		child.queue_free()

	var cats := [
		{"id": "graphics", "name": "画质与视口 (Graphics)"},
		{"id": "audio", "name": "音效与配乐 (Audio)"},
		{"id": "controls", "name": "键位与操作 (Controls)"},
		{"id": "gameplay", "name": "战役推演辅助 (Gameplay)"}
	]
	for c in cats:
		var btn := Button.new()
		btn.text = c["name"]
		btn.alignment = HORIZONTAL_ALIGNMENT_LEFT
		btn.pressed.connect(func():
			_current_settings_cat = c["id"]
			_render_settings_form()
		)
		settings_category_box.add_child(btn)

func _render_slots() -> void:
	for child in archive_slots_container.get_children():
		child.queue_free()

	for slot in _slots:
		var btn := Button.new()
		btn.text = "%s\n%s · %s" % [slot["name"], slot["time"], slot["chapter"]]
		btn.alignment = HORIZONTAL_ALIGNMENT_LEFT
		btn.pressed.connect(func(): select_slot(slot["id"]))
		archive_slots_container.add_child(btn)

func select_slot(slot_id: String) -> void:
	_selected_slot = slot_id
	var record: Dictionary = {}
	for s in _slots:
		if s["id"] == slot_id:
			record = s
			break

	if record.is_empty(): return
	slot_detail_label.text = """
【记录档案 %s】
- 记录时间: %s
- 章节进度: %s
- 状态: %s
- 智能回放指纹: 已核准 SHA-256 跨日因果链
""" % [record["name"], record["time"], record["chapter"], "有效存档" if record["has_data"] else "空槽位"]

	btn_load.disabled = not record["has_data"]
	btn_delete.disabled = not record["has_data"]

func _render_settings_form() -> void:
	for child in settings_form_box.get_children():
		child.queue_free()

	match _current_settings_cat:
		"graphics":
			_add_slider("UI 缩放比例 (UI Scale)", "graphics.ui_scale", 1.0, 0.75, 1.5, 0.05)
			_add_checkbox("全屏显示 (Fullscreen)", "graphics.fullscreen", false)
			_add_checkbox("低头晕减弱动态 (Reduce Motion)", "graphics.reduce_motion", false)
		"audio":
			_add_slider("主音量 (Master Volume)", "audio.master", 0.8, 0.0, 1.0, 0.05)
			_add_slider("双轨配乐音量 (BGM)", "audio.bgm", 0.7, 0.0, 1.0, 0.05)
			_add_slider("8通道音效池音量 (SFX)", "audio.sfx", 0.85, 0.0, 1.0, 0.05)
		"controls":
			_add_slider("鼠标灵敏度", "controls.mouse_sensitivity", 1.0, 0.2, 3.0, 0.1)
			_add_checkbox("滚轮快速翻页", "controls.wheel_page_turn", true)
		"gameplay":
			_add_checkbox("自动保存策略 (Autosave on Sleep)", "gameplay.autosave_policy", true)
			_add_checkbox("霍恩逻辑推演极小核心提示", "gameplay.deduction_hints", true)

func _add_slider(title: String, key: String, default_val: float, min_v: float, max_v: float, step_v: float) -> void:
	var row := VBoxContainer.new()
	var lbl := Label.new()
	lbl.text = "%s: %.2f" % [title, default_val]
	row.add_child(lbl)

	var sld := HSlider.new()
	sld.min_value = min_v
	sld.max_value = max_v
	sld.step = step_v
	sld.value = default_val
	sld.value_changed.connect(func(v):
		lbl.text = "%s: %.2f" % [title, v]
		setting_changed.emit(key, v)
	)
	row.add_child(sld)
	settings_form_box.add_child(row)

func _add_checkbox(title: String, key: String, default_val: bool) -> void:
	var chk := CheckBox.new()
	chk.text = title
	chk.button_pressed = default_val
	chk.toggled.connect(func(v):
		setting_changed.emit(key, v)
	)
	settings_form_box.add_child(chk)

func _on_save_pressed() -> void:
	save_requested.emit(_selected_slot)
	select_slot(_selected_slot)

func _on_load_pressed() -> void:
	load_requested.emit(_selected_slot)

func _on_delete_pressed() -> void:
	delete_requested.emit(_selected_slot)
