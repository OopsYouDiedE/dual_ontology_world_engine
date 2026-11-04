@tool
extends VBoxContainer
## A native Control page mounted by the same physical book as title settings.
signal page_requested(page: String)
signal ui_event(event: Dictionary)
const LABELS := {"evidence": "证物", "testimony": "证言", "hypotheses": "推论", "items": "物品", "people": "人物", "records": "记录", "gallery": "图版"}
const PAGE_LENGTH := 5
var archive: RefCounted
var section := "index"
var page_index := 0
var _editor: TextEdit
var _note_title: LineEdit
var _related: Array = []
var _draft_id := ""
var _draft_initialized := false
var _draft_text := ""
var _draft_title := ""
var _editor_caret := Vector2i.ZERO
var _editor_scroll := 0.0
var _view_serial := 0

func _ready() -> void:
	if Engine.is_editor_hint():
		theme = preload("res://scenes/UIpanel/TitleUI/TitleTheme.tres")
		if archive == null: archive = preload("res://scenes/UIpanel/CasebookUI/CasebookData.gd").new()
	else:
		name = "ArchivePage"
	theme_type_variation = &"BookColumn"
	size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_build()

func _notification(what: int) -> void:
	if what == NOTIFICATION_TRANSLATION_CHANGED and is_node_ready():
		_rebuild_translated.call_deferred()

func capture_view_state() -> Dictionary:
	if is_instance_valid(_editor):
		_draft_text = _editor.text
		_editor_caret = Vector2i(_editor.get_caret_column(), _editor.get_caret_line())
		_editor_scroll = _editor.scroll_vertical
	if is_instance_valid(_note_title): _draft_title = _note_title.text
	return {"draft_initialized": _draft_initialized, "draft_id": _draft_id,
		"draft_title": _draft_title, "draft_text": _draft_text, "related": _related.duplicate(),
		"caret": _editor_caret, "editor_scroll": _editor_scroll}

func restore_view_state(state: Dictionary) -> void:
	if state.is_empty(): return
	_draft_initialized = bool(state.get("draft_initialized", false))
	_draft_id = str(state.get("draft_id", ""))
	_draft_title = str(state.get("draft_title", ""))
	_draft_text = str(state.get("draft_text", ""))
	_related = state.get("related", []).duplicate()
	_editor_caret = state.get("caret", Vector2i.ZERO)
	_editor_scroll = float(state.get("editor_scroll", 0.0))

func request_adjacent_page(direction: int) -> bool:
	if archive == null or not LABELS.has(section): return false
	var total: int = archive.list_section(section).size()
	var target := page_index + direction
	if target < 0 or target * PAGE_LENGTH >= total: return true
	page_requested.emit(section + "@" + str(target))
	return true

func _rebuild_translated() -> void:
	if not is_inside_tree(): return
	_view_serial += 1
	var serial := _view_serial
	capture_view_state()
	var controls := _focusable()
	var focused := get_viewport().gui_get_focus_owner()
	var focus_index := controls.find(focused)
	var scroll := _parent_scroll()
	var position := scroll.scroll_vertical if scroll != null else 0
	_build()
	await get_tree().process_frame
	if not is_inside_tree() or serial != _view_serial: return
	controls = _focusable()
	if focus_index >= 0 and not controls.is_empty():
		controls[mini(focus_index, controls.size() - 1)].grab_focus()
	if scroll != null: scroll.set_deferred("scroll_vertical", position)

func _parent_scroll() -> ScrollContainer:
	var ancestor := get_parent()
	while ancestor != null:
		if ancestor is ScrollContainer: return ancestor
		ancestor = ancestor.get_parent()
	return null

func _focusable() -> Array[Control]:
	return preload("res://scenes/UIpanel/CasebookUI/UITransition.gd").focusable_controls(self)

func _build() -> void:
	for child in get_children():
		remove_child(child)
		child.queue_free()
	_editor = null
	_note_title = null
	if archive == null:
		_text(tr("尚未收录。"))
		return
	if section == "index":
		_index()
	elif section.begins_with("entry:"):
		_detail(section.trim_prefix("entry:"))
	elif section.begins_with("write:"):
		_note_editor(section.trim_prefix("write:"))
	else:
		_list()

func _index() -> void:
	_label(tr("已知资料"), &"BookSectionHeading")
	var grid := GridContainer.new()
	grid.columns = 2
	grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	add_child(grid)
	for key in LABELS:
		var count: int = archive.list_section(key).size()
		var button := _button("%s\n%02d" % [tr(LABELS[key]), count], func(): page_requested.emit(key), grid)
		button.theme_type_variation = &"BookIndexButton"
		button.custom_minimum_size = Vector2(0, 74)
	_button(tr("续读 · 书签"), func(): page_requested.emit("load"))
	_button(tr("凡例与设置"), func(): page_requested.emit("settings"))

func _list() -> void:
	if not LABELS.has(section): return
	if section == "hypotheses":
		_button(tr("新增猜想"), func(): page_requested.emit("write:"))
	var list: Array = archive.list_section(section)
	if list.is_empty():
		_text(tr("尚未收录。"))
		return
	page_index = clampi(page_index, 0, maxi(0, (list.size() - 1) / PAGE_LENGTH))
	for i in range(page_index * PAGE_LENGTH, mini(list.size(), (page_index + 1) * PAGE_LENGTH)):
		var entry: Dictionary = list[i]
		var row := VBoxContainer.new()
		row.theme_type_variation = &"BookRow"
		add_child(row)
		var id := str(entry.id)
		_button("%02d　%s" % [i + 1, str(entry.get("title", ""))], func(): page_requested.emit("entry:" + id), row)
		var excerpt := str(entry.get("text", "")).replace("\n", " ").left(96)
		if excerpt != "": _text(excerpt + ("…" if str(entry.get("text", "")).length() > 96 else ""), &"BookCaption", row)
		row.add_child(HSeparator.new())
	var navigation := HBoxContainer.new()
	add_child(navigation)
	var previous := _button(tr("上一页"), func(): page_requested.emit(section + "@" + str(page_index - 1)), navigation)
	previous.disabled = page_index == 0
	var next := _button(tr("下一页"), func(): page_requested.emit(section + "@" + str(page_index + 1)), navigation)
	next.disabled = (page_index + 1) * PAGE_LENGTH >= list.size()
	_label(tr("第 %d 页") % (page_index + 1), &"BookCaptionLabel")

func _detail(id: String) -> void:
	var entry: Dictionary = archive.find_entry(id)
	if entry.is_empty():
		_text(tr("尚未收录。"))
		return
	_label(str(entry.get("title", "")), &"BookSectionHeading")
	var image_path := str(entry.get("image", ""))
	if image_path.begins_with("res://") and ResourceLoader.exists(image_path):
		var texture := load(image_path) as Texture2D
		if texture != null:
			var mount := PanelContainer.new()
			mount.theme_type_variation = &"BookImageMount"
			add_child(mount)
			var picture := TextureRect.new()
			picture.texture = texture
			picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
			picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
			picture.custom_minimum_size = Vector2(0, 240 if str(entry.category) == "people" else 200)
			picture.mouse_filter = Control.MOUSE_FILTER_IGNORE
			mount.add_child(picture)
	var original := str(entry.get("text", ""))
	if original != "":
		_label(tr("个人批注") if bool(entry.get("personal", false)) else tr("原文"), &"BookCaptionLabel")
		_text(original, &"BookHandText" if bool(entry.get("personal", false)) else &"BookBody")
	var by := str(entry.get("speaker_name", ""))
	if by == "": by = tr("当时记录")
	var cat := str(entry.get("category", ""))
	var certainty_desc := ""
	match cat:
		"testimony": certainty_desc = "【主观证言】陈述人所言，未被证实为客观事实"
		"hypotheses": certainty_desc = "【脑内推论】认知推演假说，尚未证实"
		"evidence": certainty_desc = "【客观物证】现场发现的实体留存"
		_: certainty_desc = "【档案记录】"
	_text("%s\n%s · %s\n%s · %s\n%s · %s" % [tr(certainty_desc), tr("来源"), by, tr("取得时间"), archive.time_text(entry.get("game_time", "")), tr("地点"), str(entry.get("place", "—"))], &"BookCaption")
	var related: Array = entry.get("related", [])
	if not related.is_empty():
		_label(tr("关联证据"), &"BookCaptionLabel")
		for related_id in related:
			var linked: Dictionary = archive.find_entry(str(related_id))
			if not linked.is_empty():
				_button(str(linked.get("title", tr("查看原文"))), func(): page_requested.emit("entry:" + str(related_id)))
	if bool(entry.get("personal", false)):
		_button(tr("编辑猜想"), func(): page_requested.emit("write:" + id))
	else:
		_button(tr("据此写下猜想"), func(): page_requested.emit("write:" + id))

func _note_editor(source_id: String) -> void:
	var source: Dictionary = archive.find_entry(source_id)
	if not _draft_initialized:
		_draft_initialized = true
		if bool(source.get("personal", false)):
			_draft_id = source_id
			_draft_text = str(source.get("text", ""))
			_draft_title = str(source.get("title", ""))
			_related = source.get("related", []).duplicate()
		elif not source.is_empty():
			_related = [source_id]
	_label(tr("个人猜想与批注，不作为已证实事实。"), &"BookCaptionLabel")
	_note_title = LineEdit.new()
	_note_title.name = "NoteTitle"
	_note_title.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	_note_title.placeholder_text = tr("题名")
	_note_title.text = _draft_title
	_note_title.theme_type_variation = &"BookLineEdit"
	add_child(_note_title)
	_editor = TextEdit.new()
	_editor.name = "NoteText"
	_editor.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	_editor.placeholder_text = tr("写下你的猜想……")
	_editor.text = _draft_text
	_editor.wrap_mode = TextEdit.LINE_WRAPPING_BOUNDARY
	_editor.custom_minimum_size = Vector2(0, 260)
	_editor.theme_type_variation = &"BookNoteEdit"
	add_child(_editor)
	_editor.set_caret_line(_editor_caret.y)
	_editor.set_caret_column(_editor_caret.x)
	_editor.set_deferred("scroll_vertical", _editor_scroll)
	if not _related.is_empty():
		_label(tr("关联证据"), &"BookCaptionLabel")
		for id in _related:
			var entry: Dictionary = archive.find_entry(str(id))
			_label(str(entry.get("title", "")), &"BookCaptionLabel")
	var save := _button(tr("保存批注"), func():
		var id: String = archive.add_note(_editor.text, _related, _note_title.text.strip_edges(), _draft_id)
		if id != "": page_requested.emit("entry:" + id)
	)
	save.name = "SaveNote"
	save.disabled = _editor.text.strip_edges() == ""
	_editor.text_changed.connect(func(): save.disabled = _editor.text.strip_edges() == "")
	_button(tr("打开推演界面"), func(): ui_event.emit({"kind": "open_reasoning"}))

func _label(value: String, variation: StringName) -> Label:
	var label := Label.new()
	label.text = value
	label.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	label.theme_type_variation = variation
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	add_child(label)
	return label

func _text(value: String, variation: StringName = &"BookBody", parent: Node = null) -> RichTextLabel:
	var label := RichTextLabel.new()
	label.theme_type_variation = variation
	label.fit_content = true
	label.scroll_active = false
	label.selection_enabled = true
	label.bbcode_enabled = false
	label.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	label.text = value
	(parent if parent != null else self).add_child(label)
	return label

func _button(value: String, action: Callable, parent: Node = null) -> Button:
	var button := Button.new()
	button.text = value
	button.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	button.theme_type_variation = &"BookOption"
	button.alignment = HORIZONTAL_ALIGNMENT_LEFT
	button.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	button.pressed.connect(action)
	(parent if parent != null else self).add_child(button)
	return button
