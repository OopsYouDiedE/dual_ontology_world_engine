@tool
extends Control
## 标题预制件仅显示菜单并回传输入，由宿主处理会话与退出。

signal ui_event(event: Dictionary)

const Sound := preload("res://scenes/Shared/Audio/Sound.gd")
@onready var start_button: Button = $MenuBox/StartButton
@onready var quit_button: Button = $MenuBox/QuitButton

func _ready() -> void:
	if Engine.is_editor_hint():
		return
	
	Sound.music("main_theme", 2.5)
	start_button.pressed.connect(_request.bind("start"))
	quit_button.pressed.connect(_request.bind("quit"))
	start_button.grab_focus()

func _request(kind: String) -> void:
	if start_button.disabled: return
	start_button.disabled = true
	quit_button.disabled = true
	Sound.sfx("ui_click", -4.0)
	ui_event.emit({"kind": kind})
