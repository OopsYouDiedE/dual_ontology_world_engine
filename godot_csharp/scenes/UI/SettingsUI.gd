extends "res://scenes/UI/ModalPanel.gd"

signal reopen_requested

const BookSettingsPage := preload("res://scenes/UI/BookSettingsPage.gd")

var _title_source := "凡例与设置"

@onready var modal_title: Label = %ModalTitle
@onready var modal_close_button: Button = %ModalCloseButton
@onready var modal_body: VBoxContainer = %ModalBody

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	super._ready()
	modal_close_button.pressed.connect(close)
	_setup_default_content()
	_localize()

func _is_close_event(event: InputEvent) -> bool:
	return super._is_close_event(event) or event.is_action_pressed("settings")

func _input(event: InputEvent) -> void:
	if is_active() and not is_open() and not _external_blocked \
		and event.is_pressed() and not event.is_echo() and event.is_action_pressed("settings"):
		# Let the owner reverse a closing panel without bypassing its exit lock.
		get_viewport().set_input_as_handled()
		reopen_requested.emit()
		return
	super._input(event)

func open(title := "凡例与设置") -> void:
	_title_source = title
	_localize()
	show_panel()

func _notification(what: int) -> void:
	if what == NOTIFICATION_TRANSLATION_CHANGED and is_node_ready(): _localize()

func _localize() -> void:
	modal_title.text = tr(_title_source)
	modal_close_button.text = "× " + tr("关闭")

func _setup_default_content() -> void:
	for child in modal_body.get_children():
		modal_body.remove_child(child)
		child.queue_free()
	populate_page(modal_body)

## Shared settings content: the title embeds this in the physical book leaf.
func populate_page(container: VBoxContainer) -> void:
	container.add_child(BookSettingsPage.new())
