@tool
extends Control

## Material 3 Hazard Alert Interface (Task 7.12)
## Displays hazard type, known source, and actionable evacuate/resolve options.
## UISystem pauses world progression while its actionable alert is open.

signal evacuate_requested(hazard_id: String)
signal resolve_requested(hazard_id: String)
signal alert_dismissed(hazard_id: String)

const MaterialControls := preload("res://scenes/components/shared/MaterialControls.gd")
const Chrome := preload("res://scenes/UIpanel/FieldHUD/FieldChrome.gd")
const UIInput := preload("res://scenes/UIpanel/UIInput.gd")

@onready var alert_card: PanelContainer = %AlertCard
@onready var hazard_type_label: Label = %HazardTypeLabel
@onready var source_label: Label = %SourceLabel
@onready var evacuate_btn: Button = %EvacuateButton
@onready var resolve_btn: Button = %ResolveButton
@onready var dismiss_btn: Button = %DismissButton
@onready var severity_badge_container: MarginContainer = %BadgeContainer

var _current_hazard_id: String = ""
var _is_active: bool = false

func _ready() -> void:
	visible = false
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	Chrome.apply(self, 16)
	
	Chrome.button(evacuate_btn)
	Chrome.button(resolve_btn)
	Chrome.button(dismiss_btn)
	if Engine.is_editor_hint():
		show_alert({"hazard_type": "烟雾扩散", "source": "二楼走廊", "can_resolve": true})
		return
	
	evacuate_btn.pressed.connect(_on_evacuate_pressed)
	resolve_btn.pressed.connect(_on_resolve_pressed)
	dismiss_btn.pressed.connect(_on_dismiss_pressed)

func is_alert_active() -> bool:
	return _is_active

## Shows an active danger alert.
## alert_data:
## {
##   "hazard_id": "collapse_hazard_01",
##   "hazard_type": "结构坍塌危险",
##   "source": "西侧配电间支柱开裂",
##   "severity": "critical", # "critical" | "warning" | "caution"
##   "can_resolve": true,
##   "resolve_action": "启动支撑柱加固",
##   "evacuate_action": "向主廊道撤退"
## }
func show_alert(alert_data: Dictionary) -> void:
	_current_hazard_id = str(alert_data.get("hazard_id", ""))
	var hazard_type: String = str(alert_data.get("hazard_type", "不明危险"))
	var source: String = str(alert_data.get("source", "来源未知"))
	var severity: String = str(alert_data.get("severity", "critical"))
	var can_resolve: bool = bool(alert_data.get("can_resolve", false))
	var resolve_label: String = str(alert_data.get("resolve_action", "现场处置"))
	var evacuate_label: String = str(alert_data.get("evacuate_action", "立即撤退"))
	
	hazard_type_label.text = hazard_type
	source_label.text = source
	source_label.visible = not source.is_empty() and bool(alert_data.get("source_known", true))
	
	# Update badge
	for child in severity_badge_container.get_children():
		severity_badge_container.remove_child(child)
		child.queue_free()
	var badge := Chrome.glyph("abnormal", Vector2(26, 26), Chrome.RED if severity == "critical" else Chrome.GOLD)
	severity_badge_container.add_child(badge)
	
	# Update buttons
	evacuate_btn.text = evacuate_label
	resolve_btn.visible = can_resolve
	if can_resolve:
		resolve_btn.text = resolve_label
		
	var box := Chrome.box(0.94, Vector4(16, 10, 12, 10))
	box.border_width_left = 2
	box.border_color = Chrome.RED if severity == "critical" else Chrome.GOLD
	alert_card.add_theme_stylebox_override("panel", box)
	visible = true
	_is_active = true
	if not Engine.is_editor_hint(): dismiss_btn.grab_focus()

func dismiss_alert() -> void:
	if not _is_active:
		return
	_is_active = false
	visible = false
	alert_dismissed.emit(_current_hazard_id)

func _on_evacuate_pressed() -> void:
	if not _is_active: return
	evacuate_requested.emit(_current_hazard_id)
	dismiss_alert()

func _on_resolve_pressed() -> void:
	if not _is_active: return
	resolve_requested.emit(_current_hazard_id)
	dismiss_alert()

func _on_dismiss_pressed() -> void:
	dismiss_alert()

func _input(event: InputEvent) -> void:
	if _is_active and UIInput.is_back(event):
		dismiss_alert()
		get_viewport().set_input_as_handled()
