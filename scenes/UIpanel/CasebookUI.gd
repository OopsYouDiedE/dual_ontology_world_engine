@tool
extends CanvasLayer
signal opened
signal closing
signal closed
signal reading_changed(active: bool)
signal bookmark_quick_save_requested
signal return_title_requested
signal page_switched(page_id: String)
signal ui_event(event: Dictionary)
signal reopen_requested
const BookScene := preload("res://scenes/UIpanel/CasebookUI/PhysicalBookView.tscn")
var book: Control
var archive: RefCounted
var pause_world := false
var replay_mode := false
var restore_previous_focus := true
var snapshot: Dictionary = {}
var _open := false
var _closing := false
var _blocked := false
var _previous_focus: WeakRef

func _ready() -> void:
	layer = 30
	process_mode = Node.PROCESS_MODE_ALWAYS
	book = BookScene.instantiate()
	book.desktop = false
	book.archive = archive
	add_child(book)
	book.configure(snapshot, archive)
	book.ui_event.connect(func(event: Dictionary): ui_event.emit(event))
	book.page_switched.connect(func(id: String): page_switched.emit(id))
	book.opened.connect(func(): opened.emit())
	book.closed.connect(_on_closed)
	visible = false

func configure(data: Dictionary, book_data: RefCounted = null) -> void:
	snapshot = data
	if book_data != null: archive = book_data
	if is_instance_valid(book): book.configure(snapshot, archive)

func open(initial_page := "B01") -> void:
	if _closing or _blocked: return
	if not _open:
		var focus: Control = get_viewport().gui_get_focus_owner()
		_previous_focus = weakref(focus) if focus != null else null
		_open = true
		visible = true
		reading_changed.emit(true)
	book.archive = archive
	book.configure(snapshot, archive)
	book.open_section(initial_page)

func open_section(page: String) -> void:
	open(page)

func switch_page(page: String) -> void:
	open(page)

func close() -> void:
	if not _open: return
	closing.emit()
	book.close_book()

func _on_closed() -> void:
	_open = false
	visible = false
	reading_changed.emit(false)
	closed.emit()
	if restore_previous_focus and _previous_focus != null:
		var previous: Control = _previous_focus.get_ref()
		if is_instance_valid(previous) and previous.is_visible_in_tree(): previous.grab_focus()
	_previous_focus = null

func is_active() -> bool:
	return _open

func is_open() -> bool:
	return _open

func transition_state() -> String:
	return book.transition_state() if is_instance_valid(book) else "closed"

func set_external_blocked(value: bool) -> void:
	_blocked = value
	if is_instance_valid(book): book.set_external_blocked(value)

func release_world_pause() -> void:
	pass

func begin_shutdown() -> void:
	_closing = true
	_open = false
	visible = false
	if is_instance_valid(book): book.set_external_blocked(true)

func show_bookmark_result(message: String, _success := false) -> void:
	if is_instance_valid(book): book.show_bookmark_message(message)

func set_bookmark_loading(blocked: bool) -> void:
	set_external_blocked(blocked)
