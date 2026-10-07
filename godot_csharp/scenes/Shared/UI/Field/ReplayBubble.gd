@tool
extends PanelContainer
## 气泡正文和外观由 Text 节点与 panel 样式资源配置。

func _ready() -> void:
	set_meta("text", $Text)
