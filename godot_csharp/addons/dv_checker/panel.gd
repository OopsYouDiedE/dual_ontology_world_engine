@tool
extends VBoxContainer

const PYTHON_SETTING = "dv_checker/python_executable"
const CHECKER_SETTING = "dv_checker/checker_path"
var settings: EditorSettings
var source_path := ""
var saved_text := ""
var editor: CodeEdit
var status: Label
var diagnostics: ItemList
var raw: TextEdit
var python_path: LineEdit
var checker_path: LineEdit
var file_dialog: FileDialog
var discard_dialog: ConfirmationDialog
var pending_location: Dictionary = {}
var worker: Thread
var check_button: Button
var checked_source := ""
var checked_text := ""
var check_on_save: CheckBox

func configure(editor_settings: EditorSettings) -> void:
	settings = editor_settings
	custom_minimum_size = Vector2(600, 320)
	var tools := HBoxContainer.new()
	add_child(tools)
	_button(tools, "Open .dv", choose_file)
	_button(tools, "Save", _save_requested)
	check_button = _button(tools, "Save & check", check_source)
	check_on_save = CheckBox.new()
	check_on_save.text = "Check on save"
	check_on_save.button_pressed = true
	tools.add_child(check_on_save)
	status = Label.new()
	status.text = "Open a DV source file."
	status.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	status.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	status.clip_text = true
	tools.add_child(status)
	python_path = _path_row("Python", PYTHON_SETTING, "python")
	checker_path = _path_row("Checker", CHECKER_SETTING, ProjectSettings.globalize_path("res://../language_core/tools/check_dv.py"))
	var split := HSplitContainer.new()
	split.size_flags_vertical = Control.SIZE_EXPAND_FILL
	add_child(split)
	editor = CodeEdit.new()
	editor.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	editor.gutters_draw_line_numbers = true
	editor.indent_use_spaces = true
	editor.indent_size = 4
	editor.text_changed.connect(_text_changed)
	split.add_child(editor)
	var results := VBoxContainer.new()
	results.custom_minimum_size.x = 320
	results.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	split.add_child(results)
	diagnostics = ItemList.new()
	diagnostics.size_flags_vertical = Control.SIZE_EXPAND_FILL
	diagnostics.item_activated.connect(_diagnostic_activated)
	results.add_child(diagnostics)
	raw = TextEdit.new()
	raw.editable = false
	raw.custom_minimum_size.y = 90
	raw.placeholder_text = "Raw checker output"
	results.add_child(raw)
	file_dialog = FileDialog.new()
	file_dialog.access = FileDialog.ACCESS_FILESYSTEM
	file_dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILE
	file_dialog.filters = PackedStringArray(["*.dv ; DuelView source"])
	file_dialog.current_dir = ProjectSettings.globalize_path("res://../language_core")
	file_dialog.file_selected.connect(func(path: String): open_location(path, 1, 1))
	add_child(file_dialog)
	discard_dialog = ConfirmationDialog.new()
	discard_dialog.dialog_text = "Discard unsaved changes and open the selected file?"
	discard_dialog.confirmed.connect(_open_pending)
	add_child(discard_dialog)

func _button(parent: Node, title: String, action: Callable) -> Button:
	var button := Button.new()
	button.text = title
	button.pressed.connect(action)
	parent.add_child(button)
	return button

func _path_row(title: String, key: String, default_value: String) -> LineEdit:
	var row := HBoxContainer.new()
	add_child(row)
	var label := Label.new()
	label.text = title
	label.custom_minimum_size.x = 70
	row.add_child(label)
	var field := LineEdit.new()
	field.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	field.text = str(settings.get_setting(key)) if settings.has_setting(key) else default_value
	field.text_changed.connect(func(value: String): settings.set_setting(key, value))
	row.add_child(field)
	return field

func choose_file() -> void:
	file_dialog.popup_centered_ratio(0.7)

func _text_changed() -> void:
	status.text = source_path.get_file() + (" • modified; diagnostics are stale" if editor.text != saved_text else "")
	status.tooltip_text = source_path

func open_location(path: String, line: int, column: int) -> void:
	path = ProjectSettings.globalize_path(path).simplify_path()
	if path != source_path and editor.text != saved_text:
		pending_location = {"path": path, "line": line, "column": column}
		discard_dialog.popup_centered()
		return
	_load_location(path, line, column)

func _open_pending() -> void:
	_load_location(pending_location.path, pending_location.line, pending_location.column)
	pending_location.clear()

func _load_location(path: String, line: int, column: int) -> void:
	if path != source_path:
		var file := FileAccess.open(path, FileAccess.READ)
		if file == null:
			status.text = "Cannot open %s: %s" % [path, error_string(FileAccess.get_open_error())]
			return
		source_path = path
		saved_text = file.get_as_text()
		editor.text = saved_text
		saved_text = editor.text
	editor.set_caret_line(maxi(0, line - 1))
	editor.set_caret_column(maxi(0, column - 1))
	editor.center_viewport_to_caret()
	editor.grab_focus()
	_text_changed()

func save_source() -> bool:
	if source_path.is_empty():
		status.text = "Open a source file first."
		return false
	var file := FileAccess.open(source_path, FileAccess.WRITE)
	if file == null:
		status.text = "Cannot save: " + error_string(FileAccess.get_open_error())
		return false
	file.store_string(editor.text)
	file.flush()
	if file.get_error() != OK:
		status.text = "Save failed: " + error_string(file.get_error())
		return false
	saved_text = editor.text
	_text_changed()
	return true

func _save_requested() -> void:
	if check_on_save.button_pressed:
		check_source()
	else:
		save_source()

func _gui_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and event.keycode == KEY_S and event.is_command_or_control_pressed():
		_save_requested()
		accept_event()

func check_source() -> void:
	if worker != null or not save_source():
		return
	checked_source = source_path
	checked_text = editor.text
	diagnostics.clear()
	raw.text = ""
	status.text = "Checking " + checked_source
	check_button.disabled = true
	worker = Thread.new()
	var result := worker.start(_run_checker.bind(python_path.text, checker_path.text, checked_source))
	if result != OK:
		worker = null
		check_button.disabled = false
		status.text = "Cannot start checker: " + error_string(result)

func _run_checker(python: String, checker: String, path: String) -> Dictionary:
	var output: Array = []
	var exit_code := OS.execute(python, PackedStringArray([checker, "--format", "json", path]), output, true, false)
	return {"exit_code": exit_code, "output": "\n".join(output)}

func _process(_delta: float) -> void:
	if worker != null and not worker.is_alive():
		var result: Dictionary = worker.wait_to_finish()
		worker = null
		check_button.disabled = false
		show_result(result)

func show_result(result: Dictionary) -> void:
	raw.text = result.output
	var parser := JSON.new()
	if parser.parse(result.output) != OK or not parser.data is Dictionary:
		status.text = "Checker did not return JSON (exit %s). See raw output." % result.exit_code
		return
	var data: Dictionary = parser.data
	if not data.get("diagnostics") is Array or not data.get("success") is bool:
		status.text = "Checker response is missing diagnostics/success. See raw output."
		return
	for diagnostic in data.diagnostics:
		if not diagnostic is Dictionary:
			continue
		var entry: Dictionary = diagnostic
		var path := str(entry.get("path", checked_source))
		var line := int(entry.get("line", 1))
		var column := int(entry.get("column", 1))
		var message := "%s:%s:%s [%s] %s" % [path, line, column, entry.get("severity", "error"), entry.get("message", "")]
		var summary := "[%s] %s — %s:%s:%s" % [entry.get("severity", "error"), entry.get("message", ""), path.get_file(), line, column]
		var index := diagnostics.add_item(summary)
		diagnostics.set_item_tooltip(index, message)
		diagnostics.set_item_metadata(index, {"path": path, "line": line, "column": column})
	status.text = "Compiler check completed." if data.success and result.exit_code == 0 else "Compiler reported an error."
	if source_path != checked_source or editor.text != checked_text:
		status.text += " Results refer to the saved version of " + checked_source

func _diagnostic_activated(index: int) -> void:
	var location: Dictionary = diagnostics.get_item_metadata(index)
	open_location(location.path, location.line, location.column)

func shutdown() -> void:
	if worker != null:
		worker.wait_to_finish()
		worker = null
