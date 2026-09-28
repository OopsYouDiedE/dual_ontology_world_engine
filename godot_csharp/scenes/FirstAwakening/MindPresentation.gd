extends RefCounted
## Paper layout lives in MindNotebook.tscn; the finite language has one source.
const Stage = preload("res://scenes/FirstAwakening/ReplayStage.tscn")
const Notebook = preload("res://scenes/FirstAwakening/MindNotebook.tscn")
const Language = preload("res://scenes/FirstAwakening/MindLanguage.gd")
const INK := Color("303930")
const LIGHT := INK
const MUTED := Color("62665a")
const UI_THEME := preload("res://ShareAsset/Internal/Props/FirstAwakeningInterior/mind_theme.tres")
const WRITING_FONT := preload("res://ShareAsset/External/Yozai/Yozai-Regular.ttf")
const JAPANESE_WRITING_FONT := preload("res://ShareAsset/External/ZenKurenaido/ZenKurenaido-Regular.ttf")
const ENGLISH_WRITING_FONT := preload("res://ShareAsset/External/playwriteustrad/PlaywriteUSTrad[wght].ttf")
const PRINT_FONT := preload("res://ShareAsset/External/ZhaohuaTypeWriter/ZhaohuaTypeWriter.ttf")
const JAPANESE_PRINT_FONT := preload("res://ShareAsset/External/newtegomin/NewTegomin-Regular.ttf")
const INVESTIGATION_CONTENT: Resource = preload("res://scenes/FirstAwakening/investigation_content.tres")
const PAPER_BUTTON_THEME: Theme = preload("res://scenes/FirstAwakening/paper_button_theme.tres")

static func tx(source: String) -> String:
	return TranslationServer.translate(source)

static func words(code: String) -> String:
	return Language.words(code)

static func compile_draft(scene: Node2D) -> Dictionary:
	if scene.mind_drafts.is_empty() and not scene.mind_text.is_empty():
		return Language.compile_text(scene.mind_text, scene.sites, scene.mind_person)
	var all_codes: Array[String] = []
	var actor_codes := {}
	for actor in Language.PEOPLE:
		var draft: String = scene.mind_drafts.get(actor, "")
		if draft.strip_edges().is_empty(): continue
		var compiled := Language.compile_text(draft, scene.sites, actor)
		if compiled.has("error"):
			compiled.error = Language.display_person(actor) + " · " + str(compiled.error)
			compiled.person = actor
			return compiled
		for sentence in compiled.sentences:
			if sentence.actor != actor or (not sentence.observer.is_empty() and sentence.observer != actor):
				return {"error": tx("FA_MIND_087") % Language.display_person(actor), "person": actor, "line": sentence.line}
		actor_codes[actor] = compiled.codes
		all_codes.append_array(compiled.codes)
	return {"codes": all_codes, "actor_codes": actor_codes}

static func run_draft(scene: Node2D) -> void:
	var compiled := compile_draft(scene)
	if compiled.has("error") or compiled.codes.is_empty(): return
	scene.sim_actor_actions = compiled.get("actor_codes", {})
	if scene.sim_actor_actions.is_empty(): scene.sim_actions.assign(compiled.codes)
	else: scene.sim_actions.clear()
	scene._run_replay()
	if scene.mind_note.is_empty() and not scene.mind_dirty:
		scene._start_replay(false)
		refresh(scene)

static func open(scene: Node2D) -> void:
	scene._open_modal(tx("FA_MIND_001"), "flow")
	for child in scene.modal_box.get_children():
		scene.modal_box.remove_child(child)
		child.queue_free()
	var page: Control = Notebook.instantiate()
	scene.modal_box.add_child(page)
	var language: OptionButton = page.get_node("Language")
	for entry in [["中文", "zh_CN"], ["English", "en"], ["日本語", "ja"]]:
		language.add_item(entry[0])
		language.set_item_metadata(language.item_count - 1, entry[1])
	for i in language.item_count:
		if language.get_item_metadata(i) == GameLocale.current_language():
			language.select(i)
	language.item_selected.connect(func(index: int):
		GameLocale.set_language(str(language.get_item_metadata(index)))
		open(scene))
	if TranslationServer.get_locale().begins_with("ja") or TranslationServer.get_locale().begins_with("en"):
		var handwriting: Font = JAPANESE_WRITING_FONT if TranslationServer.get_locale().begins_with("ja") else ENGLISH_WRITING_FONT
		var locale_theme: Theme = page.theme.duplicate()
		locale_theme.default_font = JAPANESE_PRINT_FONT
		page.theme = locale_theme
		for control in page.find_children("*", "Control", true, false):
			if control.has_theme_font_override("font") and control.get_theme_font("font") == WRITING_FONT:
				control.add_theme_font_override("font", handwriting)
	var editor: TextEdit = page.get_node("Writing/DraftEditor")
	editor.text = scene.mind_drafts.get(scene.mind_person, scene.mind_text if scene.mind_drafts.is_empty() else "")
	editor.configure(Language.get_catalog(scene.sites), Language.display_person(scene.mind_person))
	var person: LineEdit = page.get_node("Heading/Person")
	person.text = Language.display_person(scene.mind_person)
	var popup: PopupMenu = page.get_node("PersonPopup")
	page.get_node("Heading/Choose").pressed.connect(_show_people.bind(page))
	var prev_person: Button = page.get_node_or_null("Heading/PrevPerson")
	if prev_person:
		prev_person.pressed.connect(func():
			var idx := Language.PEOPLE.find(Language.canonical_person(person.text.strip_edges()))
			if idx < 0: idx = 0
			var next_idx := posmod(idx - 1, Language.PEOPLE.size())
			person.text = Language.display_person(Language.PEOPLE[next_idx])
			person.text_changed.emit(person.text)
			editor.grab_focus())
	var next_person: Button = page.get_node_or_null("Heading/NextPerson")
	if next_person:
		next_person.pressed.connect(func():
			var idx := Language.PEOPLE.find(Language.canonical_person(person.text.strip_edges()))
			if idx < 0: idx = 0
			var next_idx := posmod(idx + 1, Language.PEOPLE.size())
			person.text = Language.display_person(Language.PEOPLE[next_idx])
			person.text_changed.emit(person.text)
			editor.grab_focus())
	person.text_submitted.connect(func(_val): editor.grab_focus())
	person.gui_input.connect(func(event):
		if event is InputEventKey and event.pressed and event.keycode == KEY_DOWN:
			_show_people(page)
			person.accept_event())
	popup.id_pressed.connect(func(index):
		person.text = Language.display_person(Language.PEOPLE[index])
		person.text_changed.emit(person.text)
		editor.grab_focus())
	var error: Label = page.get_node("Writing/DraftError")
	var run: Button = page.get_node("Footer/Run")
	var music: Button = page.get_node("MusicToggle")
	for path in ["MusicToggle", "Return", "Footer/Words", "Footer/Run", "Writing/Vocabulary/Toolbar/Close", "Writing/Vocabulary/Tabs/Actions", "Writing/Vocabulary/Tabs/Verbs", "Writing/Vocabulary/Tabs/Objects", "Writing/Vocabulary/Tabs/Conditions", "Writing/Vocabulary/Tabs/Notes"]:
		(page.get_node(path) as Button).add_theme_font_override("font", serif())

	music.text = tx("FA_MIND_002") if scene.reasoning_music_enabled else tx("FA_MIND_003")
	music.pressed.connect(func():
		scene.reasoning_music_enabled = not scene.reasoning_music_enabled
		start_music(scene) if scene.reasoning_music_enabled else stop_music(scene)
		music.text = tx("FA_MIND_002") if scene.reasoning_music_enabled else tx("FA_MIND_003"))
	page.get_node("Return").pressed.connect(scene._close_modal)
	start_music(scene)
	var stage: Control = Stage.instantiate()
	stage.source = scene
	if not scene.sim_result.is_empty(): stage.events.assign(scene.replay_events)
	stage.frame = scene.replay_index if not scene.sim_result.is_empty() else 0
	stage.highlight = "|".join(scene.replay_pause_ids.get(scene.replay_index, []))
	page.get_node("StageMount").add_child(stage)
	scene.replay_stage = stage
	page.get_node("Transport").setup(scene)
	page.set_meta("view_index",0)
	for pair in [["Previous",-1],["Next",1]]:
		page.get_node("Views/"+pair[0]).pressed.connect(func():
			var names := ["跟随","门厅","宿舍","研习室","备餐间"]
			var label_ids := ["FA_MIND_016", "FA_MIND_017", "FA_MIND_018", "FA_MIND_019", "FA_MIND_020"]
			var index: int = posmod(int(page.get_meta("view_index"))+int(pair[1]),names.size())
			page.set_meta("view_index",index)
			page.get_node("Views/Current").text = tx("FA_MIND_015") % tx(label_ids[index])
			stage.set_view(names[index]))
	scene.replay_status = page.get_node("ReplayStatus")
	scene.replay_feedback = page.get_node("FeedbackScroll/Feedback")
	page.set_meta("vocabulary_tab","actions")
	page.set_meta("person_filter","")
	page.get_node("Footer/Words").pressed.connect(func():
		if page.get_node("Writing/Vocabulary").visible: _hide_vocabulary(page)
		else: _show_vocabulary(scene,page,"verbs"))
	page.get_node("Writing/Vocabulary/Toolbar/Close").pressed.connect(func():
		_hide_vocabulary(page))
	page.get_node("Writing/Vocabulary/Toolbar/Search").text_changed.connect(func(_query): _populate_vocabulary(scene,page))
	for tab in [["Actions","actions"],["Verbs","verbs"],["Objects","objects"],["Conditions","conditions"],["Notes","notes"]]:
		page.get_node("Writing/Vocabulary/Tabs/"+tab[0]).pressed.connect(_show_vocabulary.bind(scene,page,tab[1]))
	run.pressed.connect(run_draft.bind(scene))
	var validate := func():
		var compiled := compile_draft(scene)
		error.text = compiled.get("error", "")
		error.visible = not error.text.is_empty()
		for line_index in editor.get_line_count(): editor.set_line_background_color(line_index,Color(0,0,0,0))
		if compiled.has("line") and compiled.get("person", scene.mind_person) == scene.mind_person: editor.set_line_background_color(compiled.line,Color(0.6,0.2,0.12,0.10))
		run.disabled = compiled.has("error") or compiled.get("codes",[]).is_empty()
	var changed := func():
		if page.get_meta("loading_draft", false): return
		scene.mind_drafts[scene.mind_person] = editor.text
		scene.mind_text = editor.text
		scene.mind_dirty = true
		scene.mind_note = ""
		scene.replay_playing = false
		scene.replay_stage.progress = 0.0
		scene.pending_inspections.clear()
		validate.call()
		refresh(scene)
	editor.text_changed.connect(changed)
	person.text_changed.connect(func(value):
		var selected: String = Language.canonical_person(value.strip_edges())
		if not Language.PEOPLE.has(selected): return
		scene.mind_person = selected
		page.set_meta("loading_draft", true)
		editor.text = scene.mind_drafts.get(selected, "")
		scene.mind_text = editor.text
		editor.configure(Language.get_catalog(scene.sites),value.strip_edges())
		page.set_meta("loading_draft", false)
		validate.call())
	validate.call()
	refresh(scene)

static func _show_people(page: Control) -> void:
	var person: LineEdit = page.get_node("Heading/Person")
	var popup: PopupMenu = page.get_node("PersonPopup")
	popup.clear()
	for i in Language.PEOPLE.size():
		var visible_name: String = Language.display_person(Language.PEOPLE[i])
		if person.text.is_empty() or Language.PEOPLE.has(Language.canonical_person(person.text)) or visible_name.contains(person.text):
			popup.add_item(visible_name,i)
	if popup.item_count == 0: return
	var rect := person.get_global_rect()
	popup.position = Vector2i(rect.position + Vector2(0,rect.size.y + 4))
	popup.size = Vector2i(int(rect.size.x),0)
	popup.popup()

static func _hide_vocabulary(page: Control) -> void:
	page.get_node("Writing/Vocabulary").hide()
	page.get_node("Footer/Words/Arrow").text = String.chr(0xe5ce)
	page.get_node("Writing/DraftEditor").grab_focus()

static func _show_vocabulary(scene: Node2D, page: Control, tab: String) -> void:
	page.set_meta("vocabulary_tab",tab)
	page.set_meta("person_filter","")
	var search: LineEdit = page.get_node("Writing/Vocabulary/Toolbar/Search")
	search.text = ""
	search.placeholder_text = tx("FA_MIND_027") if tab == "notes" else tx("FA_MIND_026")
	page.get_node("Writing/Vocabulary").show()
	page.get_node("Footer/Words/Arrow").text = String.chr(0xe5cf)
	_populate_vocabulary(scene,page)

static func _insert_word(page: Control, text: String) -> void:
	var editor: TextEdit = page.get_node("Writing/DraftEditor")
	if TranslationServer.get_locale().begins_with("en") and editor.get_caret_column() > 0:
		var before := editor.get_line(editor.get_caret_line()).substr(editor.get_caret_column() - 1, 1)
		if before != " " and not ".,;:".contains(before) and not text.begins_with(","):
			text = " " + text
	editor.insert_text_at_caret(text)
	editor.grab_focus()

static func _populate_vocabulary(scene: Node2D, page: Control) -> void:
	var entries: VBoxContainer = page.get_node("Writing/Vocabulary/Scroll/Entries")
	for child in entries.get_children():
		entries.remove_child(child)
		child.queue_free()
	var query: String = page.get_node("Writing/Vocabulary/Toolbar/Search").text.strip_edges()
	var tab: String = str(page.get_meta("vocabulary_tab", "actions"))
	var scroll: ScrollContainer = page.get_node("Writing/Vocabulary/Scroll")
	var vbar: VScrollBar = scroll.get_v_scroll_bar()
	vbar.custom_minimum_size.x = 4
	vbar.theme = PAPER_BUTTON_THEME
	for pair in [["Actions","actions"],["Verbs","verbs"],["Objects","objects"],["Conditions","conditions"],["Notes","notes"]]:
		var tab_button: Button = page.get_node("Writing/Vocabulary/Tabs/"+pair[0])
		var is_active: bool = (tab == str(pair[1]))
		tab_button.add_theme_color_override("font_color", Color("111311") if is_active else Color("8a8e86"))
		tab_button.add_theme_color_override("font_hover_color", Color("111311"))
	if tab == "notes":
		var found := false
		for i in range(scene.dialogue.history.size()-1,-1,-1):
			var note: String = scene.dialogue.history[i]
			if not query.is_empty() and not note.contains(query): continue
			label(entries,note,19,INK)
			found = true
		if not found: label(entries,tx("FA_MIND_043") if not query.is_empty() else tx("FA_MIND_044"),19,MUTED)
		return
	var catalog: Dictionary = Language.get_catalog(scene.sites)
	if tab == "conditions" and query.is_empty():
		label(entries,catalog.help,18,MUTED)
		for template in catalog.templates: label(entries,template,18,INK)
		label(entries,tx("FA_MIND_037"),18,MUTED)
		var observers := HFlowContainer.new()
		entries.add_child(observers)
		for observer in catalog.people:
			flow_button(observers,observer,func():
				page.set_meta("observer",observer)
				_populate_vocabulary(scene,page),INK if page.get_meta("observer",Language.display_person("侦探")) == observer else MUTED)
	var found := false
	if tab == "actions" or not query.is_empty():
		if not query.is_empty(): label(entries,tx("FA_MIND_029"),20,INK,true)
		var people := HFlowContainer.new()
		entries.add_child(people)
		for person in catalog.people:
			if not query.is_empty() and not person.contains(query): continue
			flow_button(people,person,_insert_word.bind(page,person),INK)
			found = true
	if tab == "verbs" or not query.is_empty():
		if not query.is_empty(): label(entries,tx("FA_MIND_030"),20,INK,true)
		for verb in catalog.verbs:
			if _word_entry(entries,page,verb.word,verb.aliases,query): found = true
		var phrases := VBoxContainer.new()
		entries.add_child(phrases)
		for phrase in catalog.get("phrases",[]):
			if not query.is_empty() and not phrase.word.contains(query): continue
			flow_button(phrases,phrase.word,_insert_word.bind(page,phrase.word),MUTED)
			found = true
		phrases.visible = not query.is_empty()
		if phrases.get_child_count() > 0:
			var toggle := flow_button(entries,tx("FA_MIND_040"),func(): phrases.visible = not phrases.visible,MUTED)
			entries.move_child(toggle,phrases.get_index())
	if tab == "objects" or not query.is_empty():
		if not query.is_empty(): label(entries,tx("FA_MIND_031"),20,INK,true)
		for target in catalog.targets:
			if _word_entry(entries,page,target.word,target.aliases,query): found = true
	if tab == "conditions" or not query.is_empty():
		var conditions: Array = catalog.conditions
		if query.is_empty():
			conditions = conditions.filter(func(text): return text.begins_with(str(page.get_meta("observer",Language.display_person("侦探")))))
		for group in [["FA_MIND_038",conditions],["FA_MIND_039",catalog.order_words]]:
			label(entries,tx(group[0]),20,INK,true)
			for token in group[1]:
				if query.is_empty() or token.contains(query):
					flow_button(entries,token,_insert_word.bind(page,token),INK)
					found = true
	if not found and tab != "conditions": label(entries,tx("FA_MIND_042"),18,MUTED)

static func _word_entry(entries: VBoxContainer, page: Control, word: String, aliases: Array, query: String) -> bool:
	if not query.is_empty() and not (word + " ".join(aliases)).contains(query): return false
	var row := HFlowContainer.new()
	entries.add_child(row)
	flow_button(row,word,_insert_word.bind(page,word),INK)
	if not aliases.is_empty():
		var alternatives := HFlowContainer.new()
		entries.add_child(alternatives)
		for alias in aliases: flow_button(alternatives,alias,_insert_word.bind(page,alias),MUTED)
		alternatives.visible = not query.is_empty()
		flow_button(row,tx("FA_MIND_041"),func(): alternatives.visible = not alternatives.visible,MUTED)
	return true

static func refresh(scene: Node2D) -> void:
	if not is_instance_valid(scene.replay_feedback): return
	for child in scene.replay_feedback.get_children():
		scene.replay_feedback.remove_child(child)
		child.queue_free()
	var parent: VBoxContainer = scene.replay_feedback
	var obs: Node = scene.replay_feedback.get_parent().get_parent().get_node_or_null("Observation")
	var has_content: bool = not scene.sim_result.is_empty() or not scene.mind_note.is_empty()
	if obs: obs.visible = has_content
	if is_instance_valid(scene.replay_status):
		scene.replay_status.visible = has_content
		scene.replay_status.text = tx("FA_MIND_045") if scene.mind_dirty and not scene.sim_result.is_empty() else tx("FA_MIND_046") if scene.replay_playing else tx("FA_MIND_047") if not scene.sim_result.is_empty() and scene.replay_index >= scene.replay_events.size() else tx("FA_MIND_048") if not scene.sim_result.is_empty() else ""
	if not scene.mind_note.is_empty():
		label(parent,scene.mind_note,20,Color("8f3d32"))
		return
	var finished: bool = not scene.sim_result.is_empty() and scene.replay_index >= scene.replay_events.size()
	var result: Dictionary = scene.replay_stage._state(scene.replay_index)
	var caption: String = str(result.get("caption",""))
	if scene.replay_index > 0 and (not result.get("succeeded",true) or caption.length() > 32):
		label(parent,tx(caption),19,INK if result.get("succeeded",true) else Color("8f3d32"))
	var available: Array[int] = []
	for i in INVESTIGATION_CONTENT.replay_clues.size():
		var clue: Dictionary = INVESTIGATION_CONTENT.replay_clues[i]
		if scene._trace_visible(clue.required) or (finished and scene.evidence.has(clue.site)): available.append(i)
	if scene.sim_result.is_empty() or available.is_empty():
		return
	if not available.has(scene.mind_trace): scene.mind_trace = available.back()
	var trace: Dictionary = INVESTIGATION_CONTENT.replay_clues[scene.mind_trace]
	var produced: bool = result.ids.has(trace.required)
	if trace.id == "locks": produced = produced and result.inner_bolt and result.outer_bar
	var heading := HBoxContainer.new()
	heading.add_theme_constant_override("separation",12)
	parent.add_child(heading)
	var previous := paper_arrow(heading,"left",func():
		scene.mind_trace = available[posmod(available.find(scene.mind_trace)-1,available.size())]
		refresh(scene),"FA_MIND_049")
	previous.disabled = available.size() < 2
	label(heading,"%s  %d/%d" % [tx(trace.mind_title_id),available.find(scene.mind_trace)+1,available.size()],22,INK).name = "TraceTitle"
	var next := paper_arrow(heading,"right",func():
		scene.mind_trace = available[posmod(available.find(scene.mind_trace)+1,available.size())]
		refresh(scene),"FA_MIND_050")
	next.disabled = available.size() < 2
	var space := Control.new()
	space.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	heading.add_child(space)
	label(heading,tx("FA_MIND_051") % maxi(0,3-scene.checks_this_run),18,MUTED)
	var note := VBoxContainer.new()
	parent.add_child(note)
	label(note,tx(("FA_MIND_052" if scene.sim_checks.has(trace.canonical) else "FA_MIND_053") if produced else "FA_MIND_054"),19,INK if produced else Color("8f3d32"))
	var explanation := "FA_MIND_055"
	if produced:
		var trace_index: int = result.ids.find(trace.required)
		if trace_index >= 0: explanation = result.descriptions[trace_index]
	if not produced and finished:
		for conflict in scene.sim_result.conflicts:
			for term in trace.conflict_terms:
				if str(conflict).contains(term): explanation = conflict
	label(note,tx(explanation),19,LIGHT)
	if produced:
		flow_button(note,tx("FA_MIND_056") if scene.evidence.has(trace.site) else tx("FA_MIND_057"),func(): scene._request_trace_check(trace.canonical,trace.required,trace.site),Color("8a5d36"),scene.mind_dirty or scene.sim_checks.has(trace.canonical) or scene.checks_this_run >= 3)

static func paper_arrow(parent: Node, glyph: String, callback: Callable, tip: String) -> Button:
	var result: Button = preload("res://scenes/FirstAwakening/PaperIconButton.gd").new()
	result.set("glyph",glyph)
	result.custom_minimum_size = Vector2(32,36)
	result.tooltip_text = tx(tip)
	result.pressed.connect(callback)
	parent.add_child(result)
	return result

static func writing() -> Font:
	if TranslationServer.get_locale().begins_with("ja"): return JAPANESE_WRITING_FONT
	if TranslationServer.get_locale().begins_with("en"): return ENGLISH_WRITING_FONT
	return WRITING_FONT

static func serif() -> Font:
	return JAPANESE_PRINT_FONT if TranslationServer.get_locale().begins_with("ja") or TranslationServer.get_locale().begins_with("en") else PRINT_FONT

static func start_music(scene: Node2D) -> void:
	if not scene.reasoning_music_enabled: return
	if is_instance_valid(scene.reasoning_music_tween): scene.reasoning_music_tween.kill()
	if not scene.reasoning_music.playing:
		scene.reasoning_music.volume_db = -24.0
		scene.reasoning_music.play()
	scene.reasoning_music_tween = scene.create_tween()
	scene.reasoning_music_tween.tween_property(scene.reasoning_music, "volume_db", -13.0, 0.65)

static func stop_music(scene: Node2D) -> void:
	if not is_instance_valid(scene.reasoning_music): return
	if is_instance_valid(scene.reasoning_music_tween): scene.reasoning_music_tween.kill()
	scene.reasoning_music_tween = scene.create_tween()
	scene.reasoning_music_tween.tween_property(scene.reasoning_music, "volume_db", -80.0, 0.3)
	scene.reasoning_music_tween.tween_callback(scene.reasoning_music.stop)

static func return_to_field_music(scene: Node2D) -> void:
	if not is_instance_valid(scene.reasoning_music) or not scene.reasoning_music_enabled: return
	if is_instance_valid(scene.reasoning_music_tween): scene.reasoning_music_tween.kill()
	if not scene.reasoning_music.playing:
		scene.reasoning_music.volume_db = -24.0
		scene.reasoning_music.play()
	scene.reasoning_music_tween = scene.create_tween()
	scene.reasoning_music_tween.tween_property(scene.reasoning_music, "volume_db", -16.0, 0.5)

static func column(parent: Node, at: Vector2, dimensions: Vector2) -> VBoxContainer:
	var result := VBoxContainer.new()
	result.position = at
	result.size = dimensions
	result.add_theme_constant_override("separation",12)
	parent.add_child(result)
	return result

static func label(parent: Node, text: String, size: int, color: Color, handwritten := false) -> Label:
	var result := Label.new()
	result.text = text
	result.autowrap_mode = TextServer.AUTOWRAP_OFF if parent is HBoxContainer else TextServer.AUTOWRAP_WORD_SMART
	result.add_theme_font_size_override("font_size",size)
	result.add_theme_color_override("font_color",color)
	result.add_theme_font_override("font",writing() if handwritten else serif())
	parent.add_child(result)
	return result

static func button(parent: Node, text: String, callback: Callable, color: Color, disabled := false) -> Button:
	var result := Button.new()
	result.text = text
	result.disabled = disabled
	result.flat = true
	result.theme = PAPER_BUTTON_THEME
	result.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	result.custom_minimum_size.y = 34
	result.add_theme_font_override("font",serif())
	result.add_theme_font_size_override("font_size",18)
	for state in ["font_color","font_hover_color","font_pressed_color","font_focus_color"]:
		result.add_theme_color_override(state,color)
	result.pressed.connect(callback)
	parent.add_child(result)
	return result

static func flow_button(parent: Node, text: String, callback: Callable, color: Color, disabled := false, _kind := "dark") -> Button:
	var result := button(parent,text,callback,color,disabled)
	result.custom_minimum_size.y = 36
	return result



