@tool
extends CanvasLayer
## 场景抬头与状态 HUD 组件。可在检视栏中配置文本与视觉效果。

@export var title_text: String = "FA_UI_050":
	set(value):
		title_text = value
		_update_ui()

@export var time_text: String = "07:20":
	set(value):
		time_text = value
		_update_ui()

@export var hint_text: String = "":
	set(value):
		hint_text = value
		_update_ui()

@export var show_vignette: bool = true:
	set(value):
		show_vignette = value
		_update_ui()

@onready var title_node: Label = $Title
@onready var time_node: Label = $Time
@onready var hint_node: Label = $Hint
@onready var vignette_node: TextureRect = $Vignette

func _ready() -> void:
	_update_ui()

func _update_ui() -> void:
	if not is_inside_tree():
		return
	var t := get_node_or_null("Title") as Label
	if t:
		t.text = title_text
	var tm := get_node_or_null("Time") as Label
	if tm:
		tm.text = time_text
	var h := get_node_or_null("Hint") as Label
	if h:
		h.text = hint_text
	var v := get_node_or_null("Vignette") as TextureRect
	if v:
		v.visible = show_vignette

func set_title(text: String) -> void:
	title_text = text

func set_time(text: String) -> void:
	time_text = text

func set_hint(text: String) -> void:
	hint_text = text
