@tool
extends "res://scenes/UIpanel/CasebookUI/BookPageLayout.gd"

signal save_requested(slot_id: String)
signal load_requested(slot_id: String)
signal delete_requested(slot_id: String)
signal return_title_requested

var _slots: Array = []
var _selected_path: String = ""
var _selected: Dictionary = {}
var _filter: String = "all"
var _busy: bool = false
var _message: String = ""
var _native_mode: bool = false

func _build_content() -> void:
	_collect_slots()
	_selected = {}
	for value: Dictionary in _slots:
		if str(value.get("path", "")) == _selected_path: _selected = value
	if not _selected_path.is_empty() and _selected.is_empty(): _selected_path = ""
	var can_save: bool = bool(snapshot.get("allow_save", not str(snapshot.get("viewer", "")).is_empty()))
	if (_native_mode or not can_save) and _selected.is_empty() and not _slots.is_empty():
		_selected = _slots[0]
		_selected_path = str(_selected.get("path", ""))
	_begin("存档与书签", _title(_selected, "空书签" if _native_mode else "新书签" if can_save else "书签详情"))
	var tabs: HBoxContainer = _row(left_body, 4)
	for value in [["all", "全部"], ["manual", "手动"], ["auto", "自动"]]:
		var filter: String = str(value[0])
		var tab: Button = _button(tabs, str(value[1]), func(): _filter = filter; _build_content(), _filter == filter)
		tab.add_theme_font_size_override("font_size", _scaled(18))
		tab.alignment = HORIZONTAL_ALIGNMENT_CENTER
	var catalog: VBoxContainer = _scroll(left_body)
	if can_save and not _native_mode: _button(catalog, "+ 新书签", func(): _selected_path = ""; _message = ""; _build_content(), _selected_path.is_empty())
	var shown: int = 0
	for value: Dictionary in _slots:
		var automatic: bool = bool(value.get("automatic", false))
		if _filter == "auto" and not automatic: continue
		if _filter == "manual" and automatic: continue
		shown += 1
		var slot: VBoxContainer = _column(catalog, 4)
		var path: String = str(value.get("path", ""))
		var button: Button = _button(slot, _title(value, "书签"), func(): _selected_path = path; _message = ""; _build_content(), path == _selected_path)
		button.disabled = _busy
		var stamp: String = str(value.get("modified_label", ""))
		if not stamp.is_empty(): _text(slot, stamp, 15, true)
		var room: String = str(value.get("room", ""))
		if not room.is_empty(): _text(slot, room, 17, true)
	if shown == 0:
		if _native_mode and _slots.is_empty():
			var empty_slot: Button = _button(catalog, "空书签", Callable())
			empty_slot.disabled = true
		else: _empty(catalog, "尚无书签" if _slots.is_empty() else "无匹配书签")
	var return_button: Button = _button(left_body, "返回标题", func(): _confirm("返回标题？未保存的进度将丢失。", {"kind": "return_title"}, "返回"))
	return_button.disabled = _busy
	_render_detail(can_save)

func _collect_slots() -> void:
	_slots.clear()
	_native_mode = snapshot.has("native_save") or snapshot.has("native_saves")
	var seen: Dictionary = {}
	if _native_mode:
		var native: Dictionary = _dict(snapshot.get("native_save", {}))
		var supplied: Array = _array(snapshot.get("native_saves", [])).duplicate()
		if not native.is_empty(): supplied.append(native)
		for value in supplied:
			if not value is Dictionary or not bool(value.get("available", true)): continue
			var path: String = str(value.get("path", ""))
			if path.is_empty() or seen.has(path) or not FileAccess.file_exists(path): continue
			var slot: Dictionary = _read_slot(path, value)
			slot["native"] = true
			_slots.append(slot)
			seen[path] = true
	else:
		for folder in ["user://saves", "user://bookmarks"]:
			var directory: DirAccess = DirAccess.open(folder)
			if directory == null: continue
			for file in directory.get_files():
				if not file.ends_with(".dvsave"): continue
				var path: String = folder + "/" + file
				if not seen.has(path):
					_slots.append(_read_slot(path))
					seen[path] = true
		var sessions: DirAccess = DirAccess.open("user://dv-sessions")
		if sessions != null:
			for folder in sessions.get_directories():
				var path: String = "user://dv-sessions/" + folder + "/save.dvsave"
				if FileAccess.file_exists(path) and not seen.has(path):
					_slots.append(_read_slot(path, {"automatic": true, "title": "会话书签"}))
					seen[path] = true
	_slots.sort_custom(func(a: Dictionary, b: Dictionary): return int(a.get("modified", 0)) > int(b.get("modified", 0)))

func _read_slot(path: String, supplied: Dictionary = {}) -> Dictionary:
	var value: Dictionary = supplied.duplicate(true)
	for metadata_path in [path + ".meta.json", path.get_basename() + ".meta.json"]:
		if not FileAccess.file_exists(metadata_path): continue
		var metadata: Variant = JSON.parse_string(FileAccess.get_file_as_string(metadata_path))
		if metadata is Dictionary: value.merge(metadata, true)
		break
	value["path"] = path
	var modified: int = int(FileAccess.get_modified_time(path))
	value["modified"] = modified
	if modified > 0: value["modified_label"] = Time.get_datetime_string_from_unix_time(modified, true) + " UTC"
	if not value.has("title"):
		var name: String = path.get_file().get_basename()
		value["title"] = "载入前书签" if "before-load" in name else "书签"
	if not value.has("automatic"): value["automatic"] = "before-load" in path or "auto" in path.get_file()
	if not value.has("screenshot"):
		for picture in [path + ".png", path.get_basename() + ".png"]:
			if FileAccess.file_exists(picture):
				value["screenshot"] = picture
				break
	var readable: FileAccess = FileAccess.open(path, FileAccess.READ)
	value["readable"] = readable != null and readable.get_length() >= 16
	if readable != null: readable.close()
	return value

func _render_detail(can_save: bool) -> void:
	var texture: Texture2D = _texture(str(_selected.get("screenshot", snapshot.get("current_preview", ""))))
	if texture != null: _picture(right_body, texture, 340)
	else:
		_spacer(right_body, 20)
		_symbol(right_body, "folio", 150)
		_spacer(right_body, 14)
	if _selected.is_empty():
		var hud: Dictionary = _dict(snapshot.get("hud", {}))
		var room: String = str(hud.get("room", ""))
		if not room.is_empty(): _text(right_body, room, 27)
		var moment: String = _time(hud.get("time", ""))
		if moment != "—": _text(right_body, moment, 20, true)
		_spacer(right_body, 0, true)
		if can_save:
			var save: Button = _button(right_body, "保存书签", _save_new)
			save.disabled = _busy
		else: _text(right_body, "尚无存档", 20, true)
	else:
		var chapter: String = str(_selected.get("chapter", ""))
		if not chapter.is_empty(): _text(right_body, chapter, 24)
		var room: String = str(_selected.get("room", ""))
		if not room.is_empty(): _text(right_body, room, 21)
		if _selected.has("game_time"): _text(right_body, _time(_selected.game_time), 20, true)
		var stamp: String = str(_selected.get("modified_label", ""))
		if not stamp.is_empty(): _text(right_body, stamp, 17, true)
		_spacer(right_body, 0, true)
		var actions: HBoxContainer = _row(right_body)
		var load_button: Button = _button(actions, "读取", _request_load)
		load_button.disabled = _busy or not bool(_selected.get("readable", false))
		if can_save:
			var overwrite: Button = _button(actions, "覆盖", _request_overwrite)
			overwrite.disabled = _busy
		var delete: Button = _button(actions, "删除", _request_delete)
		delete.add_theme_color_override("font_color", _red)
		delete.disabled = _busy
		if not bool(_selected.get("readable", false)):
			_text(right_body, "存档暂不可读", 18).add_theme_color_override("font_color", _red)
	if not _message.is_empty(): _text(right_body, _message, 18, true)

func _save_new() -> void:
	if _busy: return
	if _native_mode:
		var path: String = str(_dict(snapshot.get("native_save", {})).get("path", ""))
		if not path.is_empty() and FileAccess.file_exists(path):
			_confirm("覆盖最近的书签？", {"kind": "bookmark_overwrite", "path": path, "native": true}, "覆盖")
			return
	save_requested.emit("")
	ui_event.emit({"kind": "bookmark_save", "native": _native_mode})

func _request_load() -> void:
	if _busy or _selected.is_empty(): return
	_confirm("读取这份书签？", {"kind": "bookmark_load", "path": _selected_path, "native": _native_mode}, "读取")

func _request_overwrite() -> void:
	if _busy or _selected.is_empty(): return
	_confirm("覆盖这份书签？", {"kind": "bookmark_overwrite", "path": _selected_path, "native": _native_mode}, "覆盖")

func _request_delete() -> void:
	if _busy or _selected.is_empty(): return
	_confirm("删除这份书签？", {"kind": "bookmark_delete", "path": _selected_path, "native": _native_mode}, "删除")

func show_bookmark_result(message: String, success := false) -> void:
	_message = message
	_busy = false
	if success: _selected_path = ""
	if is_node_ready(): _build_content()

func set_bookmark_loading(blocked: bool) -> void:
	_busy = blocked
	if is_node_ready(): _build_content()

func _reset_view() -> void:
	_selected_path = ""
	_selected = {}
	_busy = false
	_message = ""
