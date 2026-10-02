extends RefCounted
## The first case exposes world events; the four remembered instincts stay fixed.
const Notebook = preload("res://scenes/FirstAwakening/MindNotebook.tscn")
const Stage = preload("res://scenes/FirstAwakening/ReplayStage.tscn")
const Paper = preload("res://scenes/FirstAwakening/MindPresentation.gd")
const INK := Color("303930")
const MUTED := Color("62665a")
const EVENTS := [
	{"id":"sound", "label":"FA_MIND_093", "word":"门外传来低频异响", "clues":["study", "table_wedge", "chair_marks"]},
	{"id":"smoke", "label":"FA_MIND_094", "word":"热烟到达门缝", "clues":["trace"]},
	{"id":"outer_bar", "label":"FA_MIND_095", "word":"主楼侧外闩落下", "clues":["slit"]},
	{"id":"bell_stops", "label":"警铃止息，依规程拔出内销", "word":"警铃止息，依规程拔出内销", "clues":["alarm_regulations"]},
	{"id":"inspection", "label":"FA_MIND_123", "word":"院方从主楼侧进出四间居室", "clues":["heel_tracks"]},
	{"id":"morning_bolt", "label":"主厨今晨顺手插上内销", "word":"主厨今晨顺手插上内销", "clues":["chef_morning"]},
	{"id":"engineer_test", "label":"工程师今晨拔销试门后复插", "word":"工程师今晨拔销试门后复插", "clues":["engineer_testimony"]},
	{"id":"escape_gap", "label":"FA_MIND_096", "word":"有人试图从窄缝直接离开", "clues":["trace"]},
	{"id":"intruder", "label":"FA_MIND_097", "word":"一直在门外的第五人清理室内", "clues":["wash", "paper"]},
	{"id":"no_reopening", "label":"灰落之后重门从未再次开启", "word":"灰落之后重门从未再次开启", "clues":["outer_sweep"]},
	{"id":"detective_removal", "label":"侦探夜里搬走四人的行李", "word":"侦探夜里搬走四人的行李", "clues":["heel_tracks"]},
	{"id":"engineer_first_bolt", "label":"工程师今晨第一个插上内销", "word":"工程师今晨第一个插上内销", "clues":["heel_tracks", "outer_sweep"]}
]

static func _known(scene: Node2D, entry: Dictionary) -> bool:
	for clue in entry.clues:
		if scene.evidence.has(clue): return true
	return false

static func tx(key: String) -> String:
	return TranslationServer.translate(key)

static func _label(parent: Node, value: String, size := 20, color := INK) -> RichTextLabel:
	var result := RichTextLabel.new()
	result.text = value
	result.bbcode_enabled = true
	result.fit_content = true
	result.scroll_active = false
	result.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	result.add_theme_font_size_override("normal_font_size", size)
	result.add_theme_color_override("default_color", color)
	result.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	parent.add_child(result)
	return result

static func _button(parent: Node, value: String, action: Callable, disabled := false) -> Button:
	var result := Button.new()
	result.text = value
	result.disabled = disabled
	result.custom_minimum_size.y = 38
	result.add_theme_font_size_override("font_size", 18)
	result.pressed.connect(action)
	parent.add_child(result)
	return result

static func _entry(id: String) -> Dictionary:
	for entry in EVENTS:
		if entry.id == id: return entry
	return {}

static func _parse(scene: Node2D, value: String) -> Dictionary:
	var ids: Array[String] = []
	var lines := value.split("\n")
	for i in lines.size():
		var word: String = lines[i].strip_edges()
		if word.is_empty(): continue
		var matched: Dictionary = {}
		for entry in EVENTS:
			if word == tx(entry.label) or word == entry.word:
				matched = entry
				break
		if matched.is_empty(): return {"ids": [], "error": tx("FA_MIND_108") % [i + 1, word]}
		if not _known(scene, matched): return {"ids": [], "error": tx("FA_MIND_109") % [i + 1, word]}
		if ids.has(matched.id): return {"ids": [], "error": tx("FA_MIND_110") % [i + 1, word]}
		ids.append(matched.id)
	return {"ids": ids, "error": "" if not ids.is_empty() else tx("FA_MIND_111")}

static func _sync(scene: Node2D, editor: TextEdit, error: Label, run: Button) -> void:
	scene.state.event_text = editor.text
	scene.state.event_text_locale = TranslationServer.get_locale()
	var parsed := _parse(scene, editor.text)
	scene.state.event_draft.assign(parsed.ids)
	error.text = parsed.error
	error.visible = not error.text.is_empty()
	run.disabled = not error.text.is_empty()
	if run.has_meta("changes_button"):
		var changes: Button = run.get_meta("changes_button")
		changes.disabled = run.disabled or scene.replay_states.is_empty()
	scene.mind_dirty = true
	scene.mind_note = ""
	scene.replay_playing = false
	scene.pending_inspections.clear()
	Paper.refresh(scene)

static func _insert(scene: Node2D, editor: TextEdit, error: Label, run: Button, word: String) -> void:
	var line: String = editor.get_line(editor.get_caret_line())
	var at := editor.get_caret_column()
	var insertion := word
	if not line.strip_edges().is_empty():
		if at == 0: insertion += "\n"
		elif at >= line.length(): insertion = "\n" + insertion
		else: insertion = "\n" + insertion + "\n"
	editor.insert_text_at_caret(insertion)
	_sync(scene, editor, error, run)
	editor.grab_focus()

static func _run(scene: Node2D, from_changes := false) -> void:
	if _parse(scene, scene.state.event_text).error != "": return
	scene._run_replay(from_changes)
	if scene.mind_note.is_empty() and not scene.mind_dirty:
		scene._start_replay(false)
		Paper.refresh(scene)

static func open(scene: Node2D) -> void:
	scene._open_modal("FA_MIND_001", "flow")
	for child in scene.modal_box.get_children():
		scene.modal_box.remove_child(child)
		child.queue_free()
	var page: Control = Notebook.instantiate()
	scene.modal_box.add_child(page)
	page.get_node("Heading").hide()
	page.get_node("Writing").hide()
	page.get_node("Footer").hide()
	page.get_node("Language").hide()
	page.get_node("Return").pressed.connect(scene._close_modal)
	page.get_node("MusicToggle").text = tx("FA_MIND_098") if scene.reasoning_music_enabled else tx("FA_MIND_099")
	page.get_node("MusicToggle").pressed.connect(func():
		scene.reasoning_music_enabled = not scene.reasoning_music_enabled
		if scene.reasoning_music_enabled: Paper.start_music(scene)
		else: Paper.stop_music(scene)
		page.get_node("MusicToggle").text = tx("FA_MIND_098") if scene.reasoning_music_enabled else tx("FA_MIND_099"))
	Paper.start_music(scene)
	var stage: Control = Stage.instantiate()
	stage.source = scene
	if not scene.sim_result.is_empty(): stage.events.assign(scene.replay_events)
	stage.frame = scene.replay_index if not scene.sim_result.is_empty() else 0
	stage.highlight = "|".join(scene.replay_pause_ids.get(scene.replay_index, []))
	page.get_node("StageMount").add_child(stage)
	scene.replay_stage = stage
	page.get_node("Transport").setup(scene)
	page.set_meta("view_index", 0)
	for pair in [["Previous", -1], ["Next", 1]]:
		page.get_node("Views/" + pair[0]).pressed.connect(func():
			var names := ["跟随", "门厅", "宿舍", "研习室", "备餐间"]
			var index: int = posmod(int(page.get_meta("view_index")) + int(pair[1]), names.size())
			page.set_meta("view_index", index)
			page.get_node("Views/Current").text = names[index]
			stage.set_view(names[index]))
	scene.replay_status = page.get_node("ReplayStatus")
	scene.replay_feedback = page.get_node("FeedbackScroll/Feedback")
	var panel := VBoxContainer.new()
	panel.name = "EventPanel"
	panel.position = Vector2(724, 100)
	panel.size = Vector2(512, 792)
	panel.add_theme_constant_override("separation", 8)
	page.add_child(panel)
	_label(panel, tx("FA_MIND_105"), 29)
	_label(panel, tx("FA_MIND_106"), 18, MUTED)
	var editor := TextEdit.new()
	editor.name = "EventDraftEditor"
	editor.custom_minimum_size.y = 258
	editor.add_theme_font_size_override("font_size", 22)
	editor.wrap_mode = TextEdit.LINE_WRAPPING_BOUNDARY
	editor.placeholder_text = tx("FA_MIND_107")
	if (scene.state.event_text.is_empty() or scene.state.event_text_locale != TranslationServer.get_locale()) and not scene.state.event_draft.is_empty():
		var words: Array[String] = []
		for id in scene.state.event_draft:
			var entry := _entry(id)
			if not entry.is_empty(): words.append(tx(entry.label))
		scene.state.event_text = "\n".join(words)
		scene.state.event_text_locale = TranslationServer.get_locale()
	editor.text = scene.state.event_text
	panel.add_child(editor)
	var error := _label(panel, "", 17, Color("8e3b2e"))
	error.custom_minimum_size.y = 25
	_label(panel, tx("FA_MIND_091"), 19, MUTED)
	var choice_scroll := ScrollContainer.new()
	choice_scroll.custom_minimum_size.y = 224
	choice_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	choice_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	panel.add_child(choice_scroll)
	var choices := VBoxContainer.new()
	choices.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	choices.add_theme_constant_override("separation", 1)
	choice_scroll.add_child(choices)
	var run := _button(panel, tx("FA_MIND_036"), _run.bind(scene))
	var changes := _button(panel, tx("FA_MIND_121"), _run.bind(scene, true), scene.replay_states.is_empty())
	changes.tooltip_text = tx("FA_MIND_122")
	run.set_meta("changes_button", changes)
	editor.text_changed.connect(_sync.bind(scene, editor, error, run))
	for entry in EVENTS:
		if _known(scene, entry):
			_button(choices, tx(entry.label), _insert.bind(scene, editor, error, run, tx(entry.label)))
	var spacer := Control.new()
	spacer.size_flags_vertical = Control.SIZE_EXPAND_FILL
	panel.add_child(spacer)
	_label(panel, tx("FA_MIND_092"), 17, MUTED)
	panel.move_child(run, panel.get_child_count() - 1)
	panel.move_child(changes, panel.get_child_count() - 1)
	var parsed := _parse(scene, editor.text)
	error.text = "" if editor.text.is_empty() else parsed.error
	error.visible = not error.text.is_empty()
	run.disabled = not parsed.error.is_empty()
	changes.disabled = run.disabled or scene.replay_states.is_empty()
	Paper.refresh(scene)
