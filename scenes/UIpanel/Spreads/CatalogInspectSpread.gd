extends Control

## 通用左索引+右检视模板（CatalogInspectSpread.gd）
## 吸收合并原 EvidenceArchiveUI (B01), RulebookUI (B02), ConventionUI (B04)
## 及子组件 EvidenceInspectUI, InventoryUI, InvestigationUI, TextComparisonUI。
## 通过传入 mode = "evidence" | "rulebook" | "convention" 动态绑定数据源。

signal entry_selected(entry_id: String, category: String)
signal pin_toggled(pinned_entry_id: String, is_pinned: bool)
signal doubt_toggled(rule_id: String, is_doubtful: bool)
signal convention_signed(viewer_id: String, clause_id: String)

@export var mode := "evidence" # "evidence", "rulebook", "convention"

# Left Page UI Elements
@onready var left_title: Label = $HBoxContainer/LeftPage/Margin/VBox/Header/LeftTitle
@onready var tabs_container: HBoxContainer = $HBoxContainer/LeftPage/Margin/VBox/Header/TabsContainer
@onready var items_container: VBoxContainer = $HBoxContainer/LeftPage/Margin/VBox/Scroll/ItemsContainer

# Right Page UI Elements
@onready var right_title: Label = $HBoxContainer/RightPage/Margin/VBox/Header/RightTitle
@onready var category_badge: Label = $HBoxContainer/RightPage/Margin/VBox/Header/CategoryBadge
@onready var detail_text: RichTextLabel = $HBoxContainer/RightPage/Margin/VBox/DetailText
@onready var action_slot: HBoxContainer = $HBoxContainer/RightPage/Margin/VBox/ActionSlot
@onready var btn_pin: Button = $HBoxContainer/RightPage/Margin/VBox/ActionSlot/PinButton
@onready var btn_doubt: Button = $HBoxContainer/RightPage/Margin/VBox/ActionSlot/DoubtButton
@onready var btn_sign: Button = $HBoxContainer/RightPage/Margin/VBox/ActionSlot/SignButton
@onready var comparison_box: PanelContainer = $HBoxContainer/RightPage/Margin/VBox/ComparisonBox
@onready var comparison_text: Label = $HBoxContainer/RightPage/Margin/VBox/ComparisonBox/Margin/ComparisonText

var _current_category := "all"
var _selected_id := ""
var _selected_record: Dictionary = {}
var _entries: Array[Dictionary] = []
var _pinned: Dictionary = {}
var _doubts: Dictionary = {}

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	btn_pin.pressed.connect(_on_pin_pressed)
	btn_doubt.pressed.connect(_on_doubt_pressed)
	btn_sign.pressed.connect(_on_sign_pressed)
	set_mode(mode)

func set_mode(new_mode: String) -> void:
	mode = new_mode
	match mode:
		"evidence":
			left_title.text = "物证与调查案卷 (Evidence Archive)"
			_setup_tabs(["全部", "自检", "物证", "文献", "人物", "随身"])
			btn_pin.show()
			btn_doubt.hide()
			btn_sign.hide()
			comparison_box.hide()
		"rulebook":
			left_title.text = "世界规则与法度 (Rulebook)"
			_setup_tabs(["全部", "规则", "概念", "词汇", "语法"])
			btn_pin.hide()
			btn_doubt.show()
			btn_sign.hide()
			comparison_box.show()
		"convention":
			left_title.text = "契约与社会盟约 (Conventions)"
			_setup_tabs(["全部", "已签署", "待签署"])
			btn_pin.hide()
			btn_doubt.hide()
			btn_sign.show()
			comparison_box.hide()

	_load_mock_or_real_data()

func configure(data: Dictionary, _book_data: RefCounted = null) -> void:
	if data.has("mode"):
		set_mode(data["mode"])
	elif mode.is_empty():
		set_mode("evidence")
	_load_mock_or_real_data()

func _setup_tabs(tab_names: Array) -> void:
	for child in tabs_container.get_children():
		child.queue_free()

	for t_name in tab_names:
		var btn := Button.new()
		btn.text = t_name
		btn.pressed.connect(func():
			_current_category = t_name
			_filter_and_render_list()
		)
		tabs_container.add_child(btn)

func _load_mock_or_real_data() -> void:
	_entries.clear()
	match mode:
		"evidence":
			_entries = [
				{"id": "ev_huo_diary", "category": "文献", "name": "霍恩的随身日记", "text": "记载了抵达海岛学院第一日的异常气候与灯塔钟声。", "pinned": false},
				{"id": "ev_keycard", "category": "物证", "name": "带划痕的权限门卡", "text": "从实验室门洞旁提取，表面有轻度白磷氧化痕迹。", "pinned": false},
				{"id": "ev_lucian", "category": "人物", "name": "露西安·顾 (Lucian Gu)", "text": "神学院助教，案发时段声称正在温室修剪夜兰。", "pinned": false},
				{"id": "ev_self", "category": "自检", "name": "霍恩心智状态", "text": "双本体感知清晰，源质侵蚀度处于安全阈值（8%）。", "pinned": false}
			]
		"rulebook":
			_entries = [
				{"id": "rule_lock_closed", "category": "规则", "name": "门洞锁闭物理律", "text": "任何门洞在没有有效磁极授权时，视锥与碰撞完全阻断。", "doubt": false},
				{"id": "rule_erosion_cycle", "category": "规则", "name": "源质侵蚀记忆周期", "text": "若心智侵蚀突破临界，跨日睡眠将截断部分主观认知记录。", "doubt": false},
				{"id": "term_dual_ontology", "category": "概念", "name": "双本体隔离 (Dual Ontology)", "text": "物理域事实与角色主观所知互为投影，严禁直接跨域读写。", "doubt": false}
			]
		"convention":
			_entries = [
				{"id": "conv_investigation", "category": "已签署", "name": "特别调查特权协议", "text": "授权霍恩侦探在全岛36个区域执行搜查，免于常规宵禁制约。", "signed": true},
				{"id": "conv_secret_order", "category": "待签署", "name": "第七机关协同隐秘条约", "text": "要求对于涉及源质侵蚀的超自然事实实施局部信息过滤。", "signed": false}
			]

	_filter_and_render_list()
	if not _entries.is_empty():
		select_entry(_entries[0]["id"])

func _filter_and_render_list() -> void:
	for child in items_container.get_children():
		child.queue_free()

	for item in _entries:
		var cat: String = str(item.get("category", ""))
		if _current_category != "all" and _current_category != "全部" and cat != _current_category:
			continue

		var item_id: String = str(item.get("id", ""))
		var btn := Button.new()
		var prefix := "★ " if _pinned.get(item_id, false) else "• "
		btn.text = prefix + str(item.get("name", item_id))
		btn.alignment = HORIZONTAL_ALIGNMENT_LEFT
		btn.pressed.connect(func(): select_entry(item_id))
		items_container.add_child(btn)

func select_entry(id: String) -> void:
	_selected_id = id
	_selected_record = {}
	for item in _entries:
		if str(item.get("id", "")) == id:
			_selected_record = item
			break

	if _selected_record.is_empty(): return

	right_title.text = str(_selected_record.get("name", ""))
	category_badge.text = "分类: " + str(_selected_record.get("category", "普通"))
	detail_text.text = str(_selected_record.get("text", ""))

	var is_pin: bool = _pinned.get(id, false)
	btn_pin.text = "取消钉选" if is_pin else "钉选到图版 (Pin)"

	var is_doubt: bool = _doubts.get(id, false)
	btn_doubt.text = "取消存疑" if is_doubt else "标记为存疑 (Doubt)"
	if mode == "rulebook":
		comparison_text.text = "对照文书: 《1844 帝国刑事法度全编》第 14 卷对比已就绪。"

	if mode == "convention":
		var signed: bool = bool(_selected_record.get("signed", false))
		btn_sign.text = "已完成签署" if signed else "签署此盟约 (Sign)"
		btn_sign.disabled = signed

	entry_selected.emit(id, str(_selected_record.get("category", "")))

func _on_pin_pressed() -> void:
	if _selected_id.is_empty(): return
	var current: bool = _pinned.get(_selected_id, false)
	_pinned[_selected_id] = not current
	pin_toggled.emit(_selected_id, not current)
	select_entry(_selected_id)
	_filter_and_render_list()

func _on_doubt_pressed() -> void:
	if _selected_id.is_empty(): return
	var current: bool = _doubts.get(_selected_id, false)
	_doubts[_selected_id] = not current
	doubt_toggled.emit(_selected_id, not current)
	select_entry(_selected_id)

func _on_sign_pressed() -> void:
	if _selected_id.is_empty(): return
	_selected_record["signed"] = true
	convention_signed.emit("detective_huo", _selected_id)
	select_entry(_selected_id)
