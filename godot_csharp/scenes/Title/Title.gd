@tool
extends Control
## 标题界面：选章预制件。允许在检视栏（Inspector）中配置标题文本与配色。

const Paper := preload("res://scenes/Shared/UI/Paper.gd")
const Sound := preload("res://scenes/Shared/Audio/Sound.gd")

@export var title_text: String = "安眠者有福":
	set(value):
		title_text = value
		_update_labels()

@export var subtitle_text: String = "Blessed Are the Sleepers":
	set(value):
		subtitle_text = value
		_update_labels()

@export var ch1_button_text: String = "第一章　门的这一侧":
	set(value):
		ch1_button_text = value
		_update_buttons()

@export var ch2_button_text: String = "第二章":
	set(value):
		ch2_button_text = value
		_update_buttons()

@export var quit_button_text: String = "退出":
	set(value):
		quit_button_text = value
		_update_buttons()

@export var background_color: Color = Color("1d1a1f"):
	set(value):
		background_color = value
		if is_instance_valid(bg):
			bg.color = background_color

@export var title_color: Color = Color("e6dcbf"):
	set(value):
		title_color = value
		_update_labels()

@export var subtitle_color: Color = Color("a89c84"):
	set(value):
		subtitle_color = value
		_update_labels()

@onready var bg: ColorRect = $Background
@onready var menu_box: VBoxContainer = $MenuBox
@onready var title_label: RichTextLabel = $MenuBox/TitleLabel
@onready var subtitle_label: RichTextLabel = $MenuBox/SubtitleLabel
@onready var ch1_button: Button = $MenuBox/Chapter1Button
@onready var ch2_button: Button = $MenuBox/Chapter2Button
@onready var quit_button: Button = $MenuBox/QuitButton

func _ready() -> void:
	theme = Paper.theme()
	_update_labels()
	_update_buttons()
	if is_instance_valid(bg):
		bg.color = background_color
	
	if Engine.is_editor_hint():
		return
	
	Sound.music("main_theme", 2.5)
	if is_instance_valid(ch1_button):
		ch1_button.pressed.connect(func():
			Sound.sfx("ui_click", -4.0)
			get_tree().change_scene_to_file("res://scenes/Main2D.tscn")
		)
	if is_instance_valid(ch2_button):
		ch2_button.pressed.connect(func():
			Sound.sfx("ui_click", -4.0)
			get_tree().change_scene_to_file("res://scenes/Chapter2/Game/Chapter2Game.tscn")
		)
	if is_instance_valid(quit_button):
		quit_button.pressed.connect(func():
			Sound.sfx("ui_click", -4.0)
			get_tree().quit()
		)

func _update_labels() -> void:
	if is_instance_valid(title_label):
		title_label.text = "[center]%s[/center]" % title_text
		title_label.add_theme_color_override("default_color", title_color)
	if is_instance_valid(subtitle_label):
		subtitle_label.text = "[center]%s[/center]" % subtitle_text
		subtitle_label.add_theme_color_override("default_color", subtitle_color)

func _update_buttons() -> void:
	if is_instance_valid(ch1_button):
		ch1_button.text = ch1_button_text
	if is_instance_valid(ch2_button):
		ch2_button.text = ch2_button_text
	if is_instance_valid(quit_button):
		quit_button.text = quit_button_text
