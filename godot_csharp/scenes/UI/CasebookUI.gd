extends CanvasLayer
## One physical volume owns in-game archive, settings and bookmarks.
signal opened
signal closing
signal closed
signal reading_changed(active: bool)
signal ui_event(event: Dictionary)
signal reopen_requested
const BOOK_SCENE := preload("res://scenes/UI/TitleUI.tscn")
var archive: RefCounted
var restore_previous_focus := false
var pause_world := true
var replay_mode := false
var book: Control
var _active := false
var _closing := false
var _shutting_down := false
var _external_blocked := false
var _operation_blocked := false
var _was_paused := false
var _owns_world_pause := false

func _ready() -> void:
	layer = 30
	process_mode = Node.PROCESS_MODE_ALWAYS
	visible = false

func open(page := "settings") -> void:
	if _shutting_down or _closing or _operation_blocked: return
	if _active:
		book.open_section(page)
		return
	_was_paused = get_tree().paused
	_owns_world_pause = pause_world
	if _owns_world_pause: get_tree().paused = true
	book = BOOK_SCENE.instantiate()
	book.mode = "casebook"
	book.replay_mode = replay_mode
	book.initial_page = page
	book.archive = archive
	book.process_mode = Node.PROCESS_MODE_ALWAYS
	book.ui_event.connect(func(event: Dictionary): ui_event.emit(event))
	book.close_requested.connect(close)
	add_child(book)
	book.set_external_blocked(_external_blocked or _operation_blocked)
	# Reading observers synchronously capture this volume's page and controls.
	# Publish the active lifecycle only after the book has entered the tree.
	_active = true
	visible = true
	reading_changed.emit(true)
	opened.emit()

func close() -> void:
	if not _active or _closing or _shutting_down or _operation_blocked: return
	_closing = true
	closing.emit()
	if is_instance_valid(book): await book.transition_out()
	if _shutting_down: return
	if is_instance_valid(book):
		remove_child(book)
		book.queue_free()
	book = null
	_active = false
	_closing = false
	visible = false
	release_world_pause()
	reading_changed.emit(false)
	closed.emit()

func is_active() -> bool:
	return _active and not _shutting_down

func is_open() -> bool:
	return is_active() and not _closing

func can_interact() -> bool:
	return is_open() and not _external_blocked and not _operation_blocked and is_instance_valid(book) and not bool(book.get("_turning"))

func transition_state() -> String:
	if not is_active(): return "closed"
	if _closing: return "closing"
	return "turning" if is_instance_valid(book) and bool(book.get("_turning")) else "open"

func set_external_blocked(blocked: bool) -> void:
	_external_blocked = blocked
	if is_instance_valid(book): book.set_external_blocked(blocked or _operation_blocked)

func set_bookmark_loading(blocked: bool) -> void:
	_operation_blocked = blocked
	if is_instance_valid(book): book.set_external_blocked(blocked or _external_blocked)

func show_bookmark_result(message: String, success := false) -> void:
	if not is_instance_valid(book): return
	if success: book.open_section("load")
	book.show_bookmark_message(message)

func release_world_pause() -> void:
	if not _owns_world_pause: return
	_owns_world_pause = false
	get_tree().paused = _was_paused

func begin_shutdown() -> void:
	if _shutting_down: return
	_shutting_down = true
	var was_active := _active
	_active = false
	visible = false
	release_world_pause()
	if was_active: reading_changed.emit(false)

func _exit_tree() -> void:
	begin_shutdown()
