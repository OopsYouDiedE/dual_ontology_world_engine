@tool
extends Control
## Reference title composition, authored on the same 2:1 frame as the room.
signal ui_event(event: Dictionary)
signal transition_finished(shown: bool)
signal close_requested
signal field_reveal_finished(cancelled: bool)

const BookScene := preload("res://scenes/UIpanel/BookUI.tscn")
const Materials := preload("res://scenes/components/shared/MaterialControls.gd")
const Sound := preload("res://scenes/components/shared/Sound.gd")
const MENU_ROWS := [0.38, 0.47, 0.56, 0.65, 0.74]

var mode := "title"
var replay_mode := false
var native_character := false
var archive: RefCounted
var initial_page := "B01"
var book: Control
var start_button: Button
var continue_button: Button
var settings_button: Button
var quit_button: Button
var title_label: Label
var _load_button: Button
var _title_shade: ColorRect
var _menu: Control
var _rule: ColorRect
var _notice: Label
var _buttons: Array[Button] = []
var _pending := false
var _blocked := false
var _transition: Tween
var _latest_path := ""
var _has_native_save := false

func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	process_mode = Node.PROCESS_MODE_ALWAYS
	Materials.apply(self)
	book = BookScene.instantiate()
	book.desktop = true
	book.archive = archive
	add_child(book)
	book.ui_event.connect(_on_book_event)
	book.closed.connect(_on_book_closed)
	_build_presentation()
	resized.connect(_layout_reference)
	var settings := get_node_or_null("/root/GameSettings")
	if settings != null: settings.setting_changed.connect(_on_setting_changed)
	var locale := get_node_or_null("/root/GameLocale")
	if locale != null and locale.has_signal("language_changed"):
		locale.language_changed.connect(func(_language: String): _localize())
	_localize()
	_refresh_continue()
	_layout_reference.call_deferred()
	if not Engine.is_editor_hint():
		Sound.music("main_theme", 2.5)
		transition_in.call_deferred()

func _build_presentation() -> void:
	_title_shade = ColorRect.new()
	_title_shade.name = "ReferenceTitleShade"
	_title_shade.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var shader := Shader.new()
	shader.code = "shader_type canvas_item; void fragment(){ float shade=smoothstep(0.48,0.73,UV.x); COLOR=vec4(0.064,0.075,0.091,shade*0.98); }"
	var shade_material := ShaderMaterial.new()
	shade_material.shader = shader
	_title_shade.material = shade_material
	add_child(_title_shade)
	_menu = Control.new()
	_menu.name = "TitleMenu"
	_menu.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_menu)
	title_label = Label.new()
	title_label.name = "TitleLabel"
	title_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	title_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	title_label.add_theme_color_override("font_color", Color("efe4cf"))
	_menu.add_child(title_label)
	_rule = ColorRect.new()
	_rule.name = "TitleRule"
	_rule.color = Color(0.78, 0.73, 0.64, 0.43)
	_rule.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_menu.add_child(_rule)
	continue_button = _make_choice("继续", "ContinueButton", _continue_game)
	start_button = _make_choice("新游戏", "StartButton", func(): _request("start"))
	_load_button = _make_choice("读取", "LoadButton", open_load_book)
	settings_button = _make_choice("设置", "SettingsButton", open_settings)
	quit_button = _make_choice("退出", "QuitButton", func(): _request("quit"))
	_notice = Label.new()
	_notice.name = "ActionResult"
	_notice.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_notice.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_notice.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_notice.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_notice.add_theme_color_override("font_color", Color("d4c4ad"))
	_notice.hide()
	_menu.add_child(_notice)

func _make_choice(caption: String, node_name: String, action: Callable) -> Button:
	var button := Button.new()
	button.name = node_name
	button.text = caption
	button.flat = true
	button.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	button.focus_mode = Control.FOCUS_ALL
	for state in ["normal", "hover", "pressed", "disabled", "focus"]:
		button.add_theme_stylebox_override(state, StyleBoxEmpty.new())
	button.add_theme_color_override("font_color", Color("e4d8c1"))
	button.add_theme_color_override("font_hover_color", Color("fff2d8"))
	button.add_theme_color_override("font_focus_color", Color("fff2d8"))
	button.add_theme_color_override("font_pressed_color", Color("ffffff"))
	button.add_theme_color_override("font_disabled_color", Color("938b7e"))
	button.pressed.connect(action)
	_menu.add_child(button)
	_buttons.append(button)
	return button

func _layout_reference() -> void:
	if not is_instance_valid(book) or not is_instance_valid(_menu): return
	var frame: Rect2 = book.get_safe_frame()
	var dimensions := frame.size
	var unit := dimensions.x / 1280.0
	_title_shade.position = frame.position
	_title_shade.size = dimensions
	_menu.position = frame.position
	_menu.size = dimensions
	title_label.position = dimensions * Vector2(0.56, 0.105)
	title_label.size = dimensions * Vector2(0.42, 0.15)
	title_label.add_theme_font_size_override("font_size", roundi(64.0 * unit))
	if theme != null and theme.default_font != null:
		var title_font := FontVariation.new()
		title_font.base_font = theme.default_font
		title_font.spacing_glyph = roundi(12.0 * unit)
		title_label.add_theme_font_override("font", title_font)
	_rule.position = dimensions * Vector2(0.656, 0.290)
	_rule.size = Vector2(dimensions.x * 0.228, maxf(1.0, unit))
	for index in range(_buttons.size()):
		var button := _buttons[index]
		button.position = Vector2(dimensions.x * 0.60, dimensions.y * (MENU_ROWS[index] - 0.041))
		button.size = dimensions * Vector2(0.34, 0.082)
		button.add_theme_font_size_override("font_size", roundi(32.0 * unit))
	_notice.position = dimensions * Vector2(0.58, 0.82)
	_notice.size = dimensions * Vector2(0.38, 0.12)
	_notice.add_theme_font_size_override("font_size", roundi(21.0 * unit))

func _on_setting_changed(section: String, key: String, _value: Variant) -> void:
	if (section == "accessibility" and key in ["plain_fonts", "high_contrast"]) or (section == "graphics" and key == "ui_scale"):
		Materials.apply(self)
		_layout_reference()

func _localize() -> void:
	var captions := ["安眠者有福", "继续", "新游戏", "读取", "设置", "退出"]
	var language := TranslationServer.get_locale()
	if language.begins_with("en"):
		captions = ["Blessed Are Those\nWho Sleep", "Continue", "New game", "Load", "Settings", "Quit"]
	elif language.begins_with("ja"):
		captions = ["眠る者に福あれ", "続きから", "はじめる", "記録", "設定", "終了"]
	title_label.text = captions[0]
	for index in range(_buttons.size()): _buttons[index].text = captions[index + 1]
	_layout_reference()

func _refresh_continue() -> void:
	_latest_path = ""
	_has_native_save = false
	if Engine.is_editor_hint():
		continue_button.disabled = true
		return
	if native_character:
		var save_file := _native_path()
		_has_native_save = FileAccess.file_exists(save_file)
	else:
		var latest_time := 0
		for directory in ["user://saves", "user://dv-sessions"]:
			var folder := DirAccess.open(directory)
			if folder == null: continue
			for filename in folder.get_files():
				if not filename.ends_with(".dvsave"): continue
				var path: String = directory.path_join(filename)
				var modified := int(FileAccess.get_modified_time(path))
				if modified > latest_time:
					latest_time = modified
					_latest_path = path
	continue_button.disabled = not _has_native_save if native_character else _latest_path.is_empty()
	if _blocked or _pending: continue_button.disabled = true
	var save_path := _native_path()
	var save := {"available": _has_native_save, "path": save_path, "title": "最近的记录", "native": true, "modified": FileAccess.get_modified_time(save_path)}
	book.configure({"native_save": save} if native_character else {}, archive)

func _native_path() -> String:
	var path := "user://character-session.dvsave"
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--dv-save-slot="):
			path = "user://sessions/" + argument.trim_prefix("--dv-save-slot=") + "/character-session.dvsave"
	return path

func _continue_game() -> void:
	if native_character: _request("load")
	elif not _latest_path.is_empty(): _request("load", _latest_path)

func _request(kind: String, path := "") -> void:
	if _pending or _blocked: return
	if kind != "quit":
		_pending = true
		_set_buttons_enabled(false)
	var event := {"kind": kind}
	if not path.is_empty(): event["path"] = path
	ui_event.emit(event)

func open_settings() -> void:
	open_section("B08")

func open_load_book() -> void:
	_refresh_continue()
	open_section("B07")

func open_section(page: String) -> void:
	if _pending or _blocked: return
	_menu.hide()
	_title_shade.hide()
	_notice.text = ""
	_notice.hide()
	book.open_section(page)

func _on_book_closed() -> void:
	_menu.show()
	_title_shade.show()
	_refresh_continue()

func _on_book_event(event: Dictionary) -> void:
	match str(event.get("kind", "")):
		"bookmark_load", "native_load": _request("load", "" if native_character else str(event.get("path", "")))
		"return_title", "close_book": book.close_book()
		"bookmark_save": book.show_bookmark_message("进入游戏后保存")
		"bookmark_delete":
			var path := str(event.get("path", ""))
			var permitted := path == _native_path() if native_character else (path == _latest_path and not path.is_empty())
			if not permitted:
				book.show_bookmark_message("书签路径不匹配")
				return
			var error := DirAccess.remove_absolute(ProjectSettings.globalize_path(path))
			_refresh_continue()
			book.show_bookmark_message("已删除" if error == OK else "未能删除")
		_: ui_event.emit(event)

func _set_buttons_enabled(enabled: bool) -> void:
	for button in _buttons: button.disabled = not enabled
	if enabled: _refresh_continue()

func set_external_blocked(value: bool) -> void:
	_blocked = value
	book.set_external_blocked(value)
	_set_buttons_enabled(not value and not _pending)

func begin_opening(_kind := "start") -> void:
	_pending = true
	_set_buttons_enabled(false)
	_notice.text = "稍候…"
	_notice.show()
	if is_instance_valid(_transition): _transition.kill()
	_transition = create_tween()
	_transition.tween_method(book.stage.set_zoom, 0.0, 0.45, 0.55).set_trans(Tween.TRANS_SINE)

func set_loading_stage(_stage: String) -> void:
	_notice.text = "稍候…"
	_notice.show()

func cancel_opening() -> void:
	if is_instance_valid(_transition): _transition.kill()
	_pending = false
	_notice.text = ""
	_notice.hide()
	book.stage.set_zoom(0.0)
	_set_buttons_enabled(true)

func show_start_error(message: String, _retryable := false) -> void:
	cancel_opening()
	_notice.text = message
	_notice.show()

func show_load_error(message: String) -> void:
	show_start_error(message, true)

func transition_in() -> bool:
	modulate.a = 0.0
	var tween := create_tween()
	tween.tween_property(self, "modulate:a", 1.0, 0.25)
	await tween.finished
	transition_finished.emit(true)
	return true

func transition_out() -> bool:
	var tween := create_tween()
	tween.tween_property(self, "modulate:a", 0.0, 0.25)
	await tween.finished
	transition_finished.emit(false)
	return true

func transition_to_field() -> bool:
	if is_instance_valid(_transition): _transition.kill()
	_menu.hide()
	_title_shade.hide()
	_notice.hide()
	var tween := create_tween().set_parallel(true)
	tween.tween_method(book.stage.set_zoom, 0.45, 1.0, 0.55).set_trans(Tween.TRANS_CUBIC)
	tween.tween_property(self, "modulate:a", 0.0, 0.45).set_delay(0.10)
	await tween.finished
	field_reveal_finished.emit(false)
	return true

func page_trace_context() -> Dictionary:
	return book.page_trace_context() if book.is_active() else {"panel": "title", "page": "cover"}

func capture_replay_state() -> Dictionary:
	return book.capture_replay_state()

func restore_replay_state(state: Dictionary) -> void:
	book.restore_replay_state(state)

func _unhandled_input(event: InputEvent) -> void:
	if Engine.is_editor_hint(): return
	var Navigation := preload("res://scenes/UIpanel/UIInput.gd")
	if Navigation.is_back(event):
		if is_instance_valid(book) and book.is_active():
			book.close_book()
			get_viewport().set_input_as_handled()
		elif _pending:
			cancel_opening()
			get_viewport().set_input_as_handled()
