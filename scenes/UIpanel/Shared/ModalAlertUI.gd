extends CanvasLayer

## 全局模态确认框与源质侵蚀警报叠层（ModalAlertUI.gd）
## 合并原 ModalPanel 与 AlertUI。

signal confirmed
signal cancelled
signal alert_dismissed

@onready var dimmer: ColorRect = $Dimmer
@onready var modal_box: PanelContainer = $CenterContainer/ModalBox
@onready var title_label: Label = $CenterContainer/ModalBox/MarginContainer/VBoxContainer/TitleLabel
@onready var message_label: Label = $CenterContainer/ModalBox/MarginContainer/VBoxContainer/MessageLabel
@onready var btn_confirm: Button = $CenterContainer/ModalBox/MarginContainer/VBoxContainer/ButtonBox/ConfirmButton
@onready var btn_cancel: Button = $CenterContainer/ModalBox/MarginContainer/VBoxContainer/ButtonBox/CancelButton

@onready var erosion_banner: PanelContainer = $ErosionBanner
@onready var erosion_label: Label = $ErosionBanner/MarginContainer/HBoxContainer/ErosionLabel
@onready var erosion_bar: ProgressBar = $ErosionBanner/MarginContainer/HBoxContainer/ErosionBar
@onready var erosion_close_btn: Button = $ErosionBanner/MarginContainer/HBoxContainer/DismissButton

var _on_confirm_cb: Callable
var _on_cancel_cb: Callable

func _ready() -> void:
	layer = 100
	process_mode = Node.PROCESS_MODE_ALWAYS
	btn_confirm.pressed.connect(_on_confirm_pressed)
	btn_cancel.pressed.connect(_on_cancel_pressed)
	erosion_close_btn.pressed.connect(dismiss_erosion_alert)
	dimmer.gui_input.connect(_on_dimmer_input)
	close()

func show_confirm(title: String, message: String, on_confirm := Callable(), on_cancel := Callable()) -> void:
	title_label.text = title
	message_label.text = message
	_on_confirm_cb = on_confirm
	_on_cancel_cb = on_cancel
	dimmer.show()
	modal_box.show()
	erosion_banner.hide()
	show()
	btn_confirm.grab_focus()

func show_erosion_alert(level: float, message: String = "") -> void:
	var pct := int(clampf(level, 0.0, 1.0) * 100.0)
	var text := "【源质侵蚀警报】当前侵蚀度: %d%%" % pct
	if not message.is_empty():
		text += " - " + message
	erosion_label.text = text
	erosion_bar.value = pct
	dimmer.hide()
	modal_box.hide()
	erosion_banner.show()
	show()

func dismiss_erosion_alert() -> void:
	erosion_banner.hide()
	alert_dismissed.emit()
	if not modal_box.visible:
		hide()

func close() -> void:
	dimmer.hide()
	modal_box.hide()
	erosion_banner.hide()
	hide()

func _on_confirm_pressed() -> void:
	confirmed.emit()
	if _on_confirm_cb.is_valid():
		_on_confirm_cb.call()
	close()

func _on_cancel_pressed() -> void:
	cancelled.emit()
	if _on_cancel_cb.is_valid():
		_on_cancel_cb.call()
	close()

func _on_dimmer_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		_on_cancel_pressed()
