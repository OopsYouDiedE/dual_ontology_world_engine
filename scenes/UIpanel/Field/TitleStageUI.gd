extends Control

## 胡桃木书案标题与章节结算界面（TitleStageUI.gd）
## 吸收合并原 TitleUI, TitleSpread 与 StageSettlementUI。

signal start_game_requested
signal continue_game_requested
signal open_casebook_requested(page_id: String)
signal open_settings_requested
signal settlement_completed
signal quit_requested

@onready var title_view: Control = $TitleView
@onready var btn_start: Button = $TitleView/CenterContainer/VBoxContainer/StartButton
@onready var btn_continue: Button = $TitleView/CenterContainer/VBoxContainer/ContinueButton
@onready var btn_casebook: Button = $TitleView/CenterContainer/VBoxContainer/CasebookButton
@onready var btn_settings: Button = $TitleView/CenterContainer/VBoxContainer/SettingsButton
@onready var btn_quit: Button = $TitleView/CenterContainer/VBoxContainer/QuitButton

@onready var settlement_view: Control = $SettlementView
@onready var settlement_title: Label = $SettlementView/CenterContainer/PanelContainer/MarginContainer/VBoxContainer/SettlementTitle
@onready var settlement_stats: Label = $SettlementView/CenterContainer/PanelContainer/MarginContainer/VBoxContainer/SettlementStats
@onready var btn_settle_proceed: Button = $SettlementView/CenterContainer/PanelContainer/MarginContainer/VBoxContainer/ProceedButton

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	btn_start.pressed.connect(func(): start_game_requested.emit())
	btn_continue.pressed.connect(func(): continue_game_requested.emit())
	btn_casebook.pressed.connect(func(): open_casebook_requested.emit("B01"))
	btn_settings.pressed.connect(func(): open_settings_requested.emit())
	btn_quit.pressed.connect(func(): quit_requested.emit())
	btn_settle_proceed.pressed.connect(func():
		hide_stage()
		settlement_completed.emit()
	)

func show_title() -> void:
	settlement_view.hide()
	title_view.show()
	show()

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

	title_view.hide()
	settlement_view.show()
	show()

func hide_stage() -> void:
	hide()
