extends Control
## The dialogue mutation waits for confirmation before revealing the next line.
signal confirmed(value: String)

const WRITING_FONT: Font = preload("res://ShareAsset/External/Yozai/Yozai-Regular.ttf")
const JAPANESE_WRITING_FONT: Font = preload("res://ShareAsset/External/ZenKurenaido/ZenKurenaido-Regular.ttf")
const ENGLISH_WRITING_FONT: Font = preload("res://ShareAsset/External/playwriteustrad/PlaywriteUSTrad[wght].ttf")

@onready var title: Label = $Center/Panel/Margin/Content/Title
@onready var input: LineEdit = $Center/Panel/Margin/Content/Name
@onready var error_label: Label = $Center/Panel/Margin/Content/Error
@onready var confirm: Button = $Center/Panel/Margin/Content/Confirm
@onready var suggested: Button = $Center/Panel/Margin/Content/Suggested

func _ready() -> void:
	input.text_changed.connect(_on_text_changed)
	input.text_submitted.connect(func(value: String): _submit(value))
	confirm.pressed.connect(func(): _submit(input.text))
	suggested.pressed.connect(func(): _submit("林"))
	var locale := get_node_or_null("/root/GameLocale")
	if locale and locale.has_signal("language_changed"):
		locale.language_changed.connect(func(_code: String): apply_locale_theme())
	apply_locale_theme()

func validation_error(value: String) -> String:
	var name := value.strip_edges()
	if name.is_empty():
		return tr("FA_UI_351")
	if name.length() > 16:
		return tr("FA_UI_352")
	if name in ["霍铎", "沈鼎", "苏泠", "侦探", "铜口"]:
		return tr("FA_UI_353")
	return ""

func apply_locale_theme() -> void:
	var locale := get_node_or_null("/root/GameLocale")
	var code := str(locale.current_language()) if locale else TranslationServer.get_locale()
	var font: Font = WRITING_FONT
	if code == "ja":
		font = JAPANESE_WRITING_FONT
	elif code == "en":
		font = ENGLISH_WRITING_FONT
	for control in [title, input, error_label, confirm, suggested]:
		if is_instance_valid(control):
			control.add_theme_font_override("font", font)
	# Match the notebook's paper editor edge and focus treatment.
	if is_instance_valid(input) and theme:
		input.add_theme_stylebox_override("normal", theme.get_stylebox("normal", "TextEdit"))
		input.add_theme_stylebox_override("hover", theme.get_stylebox("normal", "TextEdit"))
		input.add_theme_stylebox_override("pressed", theme.get_stylebox("normal", "TextEdit"))
		input.add_theme_stylebox_override("focus", theme.get_stylebox("focus", "TextEdit"))

func request_name() -> String:
	input.clear()
	error_label.hide()
	confirm.disabled = true
	show()
	input.grab_focus()
	var value: String = await confirmed
	hide()
	return value

func _on_text_changed(value: String) -> void:
	var issue := validation_error(value)
	confirm.disabled = not issue.is_empty()
	error_label.text = issue
	error_label.visible = not issue.is_empty()

func _submit(value: String) -> void:
	var trimmed := value.strip_edges()
	var issue := validation_error(trimmed)
	if not issue.is_empty():
		error_label.text = issue
		error_label.show()
		return
	confirmed.emit(trimmed)

func _gui_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_ESCAPE:
		accept_event()
