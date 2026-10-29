extends Control

## Material 3 Hazard Alert Interface (Task 7.12)
## Displays hazard type, known source, and actionable evacuate/resolve options.
## Never pauses real-time danger resolution.
## Floats at top without blocking escape operations or scene navigation.

signal evacuate_requested(hazard_id: String)
signal resolve_requested(hazard_id: String)
signal alert_dismissed(hazard_id: String)

const MaterialControls := preload("res://scenes/UI/Components/MaterialControls.gd")

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
	MaterialControls.apply(self)
	
	MaterialControls.prepare_button(evacuate_btn)
	MaterialControls.prepare_button(resolve_btn)
	MaterialControls.prepare_button(dismiss_btn)
	
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
	
	hazard_type_label.text = "【紧急警报】%s" % hazard_type
	source_label.text = "危险源: %s" % source
	
	# Update badge
	for child in severity_badge_container.get_children():
		severity_badge_container.remove_child(child)
		child.queue_free()
	var badge := MaterialControls.create_badge("真实危险结算中", "critical")
	severity_badge_container.add_child(badge)
	
	# Update buttons
	evacuate_btn.text = evacuate_label
	resolve_btn.visible = can_resolve
	if can_resolve:
		resolve_btn.text = resolve_label
		
	# High contrast alert card style
	var box := StyleBoxFlat.new()
	box.bg_color = Color(0.18, 0.04, 0.04, 0.95)
	box.border_color = MaterialControls.COLOR_ERROR
	box.set_border_width_all(2)
	box.corner_radius_top_left = 12
	box.corner_radius_top_right = 12
	box.corner_radius_bottom_right = 12
	box.corner_radius_bottom_left = 12
	box.content_margin_left = 20.0
	box.content_margin_top = 12.0
	box.content_margin_right = 20.0
	box.content_margin_bottom = 12.0
	alert_card.add_theme_stylebox_override("panel", box)
	
	visible = true
	_is_active = true

func dismiss_alert() -> void:
	if not _is_active:
		return
	_is_active = false
	visible = false
	alert_dismissed.emit(_current_hazard_id)

func _on_evacuate_pressed() -> void:
	evacuate_requested.emit(_current_hazard_id)
	dismiss_alert()

func _on_resolve_pressed() -> void:
	resolve_requested.emit(_current_hazard_id)
	dismiss_alert()

func _on_dismiss_pressed() -> void:
	dismiss_alert()
