@tool
extends Node
## 通用界面：抬头、对白、菜单、证物卡、表单、笔记本、重演手稿、地图与结束卡。
## 只读宿主事件与快照，玩家操作以 ui_event 回传；状态与规则由宿主处理。
## 各组件在 HUD/、Dialogue/、Menu/、Card/、Form/、Notebook/、
## Manuscript/、CampusMap/、Ending/、Settings/ 中，都可在编辑器里改值预览。
## 设置册（凡例 / 操作 / 书签）随时按设置键打开，现场空闲时按返回键也打开。

signal ui_event(event: Dictionary)

const Paper := preload("res://scenes/UI/Components/Paper.gd")
const Sound := preload("res://scenes/Shared/Audio/Sound.gd")
const CHOICE_MENU := preload("res://scenes/UI/Panels/Menu/ChoiceMenu.tscn")
const EVIDENCE_CARD := preload("res://scenes/UI/Panels/Card/EvidenceCard.tscn")
const FORM_PANEL := preload("res://scenes/UI/Panels/Form/FormPanel.tscn")
const FORM_LAYOUT := preload("res://scenes/UI/Panels/Form/FormPanel.gd")
const NOTEBOOK := preload("res://scenes/UI/Panels/Notebook/Notebook.tscn")
const MANUSCRIPT := preload("res://scenes/UI/Panels/Manuscript/ReplayManuscript.tscn")
const CAMPUS_MAP := preload("res://scenes/UI/Panels/CampusMap/CampusMap.tscn")
const ENDING := preload("res://scenes/UI/Panels/Ending/EndingCard.tscn")
const SETTINGS_BOOK := preload("res://scenes/UI/Panels/Settings/SettingsBook.tscn")
const UIAppearance := preload("res://scenes/UI/Components/UIAppearance.gd")

@export_group("Editor Preview")
## 在编辑器里切换整套界面的预览页；各组件的细节在其自身场景里改值预览。
@export_enum("现场", "对白", "菜单", "证物卡", "表单", "笔记本", "重演手稿", "结束") var preview_page := 0:
	set(value):
		preview_page = value
		_preview()

@onready var hud: CanvasLayer = $HUD
@onready var dialogue: CanvasLayer = $Dialogue
@onready var modal_layer: CanvasLayer = $ModalLayer

var snapshot: Dictionary = {}
var modal_root: Control
var modal_kind := ""
var _modal_event: Dictionary = {}
var _close_option: Dictionary = {}
var _form: Control
var _form_actions: Array = []
var _manuscript: Control
var _notebook: Control
var _campus: Control
var _closing := false
var _speakers: Dictionary = {}
var _trace_display: Dictionary = {}
var _modal_generation := 0
var _dialogue_generation := 0

class LinesPresenter extends Node:
	## 把一次 lines 事件逐句交给共用对白框；最后一句带上选项。选择结果留给界面回执。
	var lines: Array = []
	var choices: Array = []
	var position := 0
	var chosen := ""
	func StartDialogue(_title: String) -> Dictionary:
		position = 0
		return _packet()
	func AdvanceDialogue() -> Dictionary:
		position += 1
		return _packet()
	func Choose(id: String) -> Dictionary:
		chosen = id
		return {"finished": true, "lines": [], "choices": [], "commands": []}
	func _packet() -> Dictionary:
		if position >= lines.size(): return {"finished": true, "lines": [], "choices": [], "commands": []}
		var packet := {"finished": false, "lines": [lines[position]], "choices": [], "commands": []}
		if position == lines.size() - 1:
			for c in choices:
				packet.choices.append({"id": str(c.id), "label": str(c.label), "is_available": bool(c.get("available", true)), "lock_hint": str(c.get("lock_hint", ""))})
		return packet

var _presenter: LinesPresenter

func _ready() -> void:
	if Engine.is_editor_hint():
		_preview()
		return
	UIAppearance.bind(self)
	_presenter = LinesPresenter.new()
	add_child(_presenter)
	dialogue.layer = 20
	dialogue.use_compact_layout()
	dialogue.single_press_keyboard_continue = true
	dialogue.dim_visited_responses = true
	dialogue.apply_locale_font(Paper.font(), null)
	dialogue.panel.hide()

func begin_shutdown() -> void:
	if _closing: return
	_closing = true
	_dialogue_generation += 1
	if is_instance_valid(dialogue): dialogue.cancel_presentation()
	close_modal()

func _exit_tree() -> void:
	begin_shutdown()

func is_busy() -> bool:
	return modal_kind != "" or (is_instance_valid(dialogue) and dialogue.active)

# ================================================================ snapshot ===

func apply_snapshot(value: Dictionary) -> void:
	var cast_changed: bool = value.get("cast", {}) != snapshot.get("cast", {})
	snapshot = value
	var head: Dictionary = value.get("hud", {})
	hud.set_title(str(head.get("title", "")))
	hud.set_time(str(head.get("time", "")))
	var presentation: Dictionary = value.get("presentation", {})
	if presentation.has("window_title"): get_window().title = str(presentation.window_title)
	if cast_changed: dialogue.set_dialogue_cast(value.get("cast", {}))
	if modal_kind == "notebook" and is_instance_valid(_notebook): _notebook.show_data(value.get("notebook", {}))
	if modal_kind == "replay" and is_instance_valid(_manuscript): _manuscript.show_data(_replay_data())

func set_hint(text: String) -> void: hud.set_hint(text)

func show_hover(text: String, screen_position: Vector2) -> void:
	if text == "": hud.hide_hover()
	else: hud.show_hover(text, screen_position)

# ================================================================== events ===

## 呈现一个宿主事件。台词等读完后返回回执，其余立即返回空字典。
func present(ev: Dictionary) -> Dictionary:
	if _closing: return {}
	if str(ev.get("kind", "")) in ["lines", "candidates", "card", "form", "ending", "aside", "title"]: _trace_display = ev
	match str(ev.get("kind", "")):
		"lines": return await _lines(ev)
		"candidates": _candidates(ev)
		"card": _card(ev)
		"form": _form_open(ev)
		"ending": _ending(ev)
		"aside": hud.show_aside(str(ev.text), float(ev.get("seconds", 3.2)))
		"title": hud.flash_title(str(ev.text))
		"music": Sound.music(str(ev.id), float(ev.get("fade", 2.5)))
		"sound": Sound.sfx(str(ev.id), float(ev.get("volume", -4.0)))
		"duck": Sound.duck(bool(ev.get("on", false)))
		"close", "interaction_closed": close_modal()
		"manuscript": open_manuscript()
		"replay_play": return await _campus_replay(ev)
		"error": push_error(str(ev.message))
		_: push_error("Unsupported display output: " + str(ev.get("kind", "")))
	return {}

func _character(line: Dictionary) -> String:
	# 标准说话人键供立绘与声音查找；显示名由事件提供。
	var speaker := str(line.get("speaker", ""))
	var name := str(line.get("speaker_name", speaker))
	return name if RegEx.create_from_string("^[A-Z][A-Za-z]+$").search(speaker) != null else speaker

func _lines(ev: Dictionary) -> Dictionary:
	_dialogue_generation += 1
	var generation := _dialogue_generation
	if dialogue.active: dialogue.cancel_presentation()
	var lines: Array = []
	var line_index := 0
	for line in ev.get("lines", []):
		var entry: Dictionary = line.duplicate()
		entry.trace_id = str(ev.get("trace_id", ""))
		entry.line_index = line_index
		line_index += 1
		entry.speaker = _character(line)
		_speakers[entry.speaker] = str(line.get("speaker_name", entry.speaker))
		lines.append(entry)
	_presenter.lines = lines
	_presenter.choices = ev.get("choices", [])
	_presenter.chosen = ""
	if lines.is_empty(): return _ack(ev)
	dialogue.panel.show()
	dialogue.start_packet(_presenter, "lines", self)
	await dialogue.finished
	if _closing or generation != _dialogue_generation: return {}
	dialogue.panel.hide()
	if _presenter.chosen != "":
		return {"kind": "select", "presentation_id": str(ev.get("presentation_id", "")), "choice_id": _presenter.chosen, "token": _token(ev, _presenter.chosen)}
	return _ack(ev)

func _ack(ev: Dictionary) -> Dictionary:
	return {"kind": "presented", "presentation_id": str(ev.get("presentation_id", ""))}

func _token(ev: Dictionary, id: String) -> String:
	for option in ev.get("choices", ev.get("options", [])):
		if str(option.id) == id: return str(option.get("token", ""))
	return ""

## 对白框回调：说话人的显示名（侦探用玩家起的名字）。
func _localized_speaker(canonical: String) -> String:
	return str(_speakers.get(canonical, canonical))

func dialogue_has_modal_input() -> bool:
	return modal_kind != ""

func _select(ev: Dictionary, id: String) -> void:
	if _closing or modal_kind == "" or ev != _modal_event: return
	close_modal()
	ui_event.emit({"kind": "select", "presentation_id": str(ev.get("presentation_id", "")), "choice_id": id, "token": _token(ev, id)})

func _remember_close(ev: Dictionary) -> void:
	_close_option = {}
	for option in ev.get("options", []):
		if str(option.id) in ["close", "cancel"] or str(option.id).ends_with(":close"):
			_close_option = option

# =================================================================== modal ===

func open_modal(title: String, size: Vector2, kind: String) -> VBoxContainer:
	close_modal()
	modal_kind = kind
	if kind != "ending": Sound.sfx("page", -6.0)
	modal_root = Paper.modal(modal_layer, title, size)
	return modal_root.get_meta("body")

func close_modal() -> void:
	_modal_generation += 1
	if is_instance_valid(_campus): _campus.cancel_playback()
	_campus = null
	if is_instance_valid(modal_root):
		modal_root.hide()
		if modal_root.get_parent() != null: modal_root.get_parent().remove_child(modal_root)
		modal_root.queue_free()
	modal_root = null
	modal_kind = ""
	_modal_event = {}
	_close_option = {}
	_form = null
	_form_actions.clear()
	_manuscript = null
	_notebook = null

## role：auto（两项以上时最后一项为主操作）、primary、secondary（设计规范第六部分 §4.4）。
func _footer(label: String, action: Callable, disabled := false, tooltip := "", role := "auto") -> Button:
	var button: Button = modal_root.add_footer_button(label, action, disabled, tooltip, role)
	button.custom_minimum_size.x = maxf(128, button.custom_minimum_size.x)
	return button

func _candidates(ev: Dictionary) -> void:
	var options: Array = ev.get("options", [])
	var body := open_modal(str(ev.get("title", "")), Vector2(832, 0), "menu")
	_modal_event = ev
	_remember_close(ev)
	var menu: Control = CHOICE_MENU.instantiate()
	body.add_child(menu)
	menu.show_data({"prompt": str(ev.get("prompt", "")), "options": options})
	menu.chosen.connect(func(id: String) -> void: _select(ev, id))

func _card(ev: Dictionary) -> void:
	# 证物名作卡内叠题（设计规范第六部分 §4.7），弹窗本身不另起标题。
	var wide: bool = str(ev.get("picture", "")) != "" or ev.has("table")
	var body := open_modal("", Vector2(1152 if wide else 832, 0), "card")
	_modal_event = ev
	_remember_close(ev)
	var view: Control = EVIDENCE_CARD.instantiate()
	body.add_child(view)
	var inline: Array = []
	for option in ev.get("options", []):
		if str(option.get("group", "")) == "inline": inline.append(option)
	var data := ev.duplicate()
	data.inline = inline
	if data.get("picture", "") is String and not ResourceLoader.exists(str(data.get("picture", ""))): data.picture = ""
	view.show_data(data)
	view.chosen.connect(func(id: String) -> void: _select(ev, id))
	for option in ev.get("options", []):
		if str(option.get("group", "")) == "inline": continue
		_footer(str(option.label), _select.bind(ev, str(option.id)), not bool(option.get("available", true)), str(option.get("lock_hint", "")))

func _form_open(ev: Dictionary) -> void:
	var wide: bool = FORM_LAYOUT.uses_columns(ev)
	var body := open_modal(str(ev.get("title", "")), Vector2(1152 if wide else 832, 0), "form")
	_modal_event = ev
	_form = FORM_PANEL.instantiate()
	body.add_child(_form)
	_form.show_data(ev)
	var header: Control = _form.pinned_header()
	if header != null: modal_root.pin_header(header)
	_form_actions = []
	for action in ev.get("actions", []):
		# 并排成栏时各组同时可见，切换分组的动作不再显示。
		if action.has("tab") and wide: continue
		var button := _footer(str(action.label), _form_action.bind(ev, action), false, "", "secondary" if action.has("tab") else "auto")
		_form_actions.append([button, action])
	_form.changed.connect(_refresh_form_actions)
	_form.submit_requested.connect(_submit_form.bind(ev))
	_refresh_form_actions()
	if _form.has_method("focus_text"): _form.focus_text.call_deferred()

func _submit_form(ev: Dictionary) -> void:
	if _closing or modal_kind != "form" or ev != _modal_event: return
	for pair in _form_actions:
		if bool(pair[1].get("default", false)) and not pair[0].disabled:
			_form_action(ev, pair[1])
			return

func _refresh_form_actions() -> void:
	if not is_instance_valid(_form): return
	for pair in _form_actions:
		var requires: Array = pair[1].get("requires", [])
		pair[0].disabled = not bool(pair[1].get("available", true)) or (not requires.is_empty() and not _form.is_complete(requires))

func _form_action(ev: Dictionary, action: Dictionary) -> void:
	if _closing or modal_kind != "form" or ev != _modal_event or not is_instance_valid(_form): return
	if not bool(action.get("available", true)): return
	if action.has("tab"):
		_form.show_field(str(action.tab))
		return
	var requires: Array = action.get("requires", [])
	if not requires.is_empty() and not _form.is_complete(requires): return
	var values: Dictionary = _form.values()
	close_modal()
	ui_event.emit({"kind": "form", "presentation_id": str(ev.get("presentation_id", "")), "action": str(action.id), "values": values})

func _ending(ev: Dictionary) -> void:
	# 章末卡的标题由卡片自己排在左下（设计规范第六部分 §4.6）。
	var body := open_modal("", Vector2(832, 0), "ending")
	_modal_event = ev
	var view: Control = ENDING.instantiate()
	body.add_child(view)
	view.show_data(ev)
	for action in ev.get("actions", []):
		_footer(str(action.label), _navigate.bind(str(action.id)), not bool(action.get("available", true)))

func _navigate(destination: String) -> void:
	if _closing or modal_kind != "ending": return
	close_modal()
	ui_event.emit({"kind": "navigate", "to": destination})

# ========================================================== local panels ===

func open_notebook() -> void:
	var body := open_modal("笔记本", Vector2(1152, 0), "notebook")
	_notebook = NOTEBOOK.instantiate()
	body.add_child(_notebook)
	_notebook.show_data(snapshot.get("notebook", {}))
	_notebook.open_card.connect(func(id: String) -> void:
		close_modal()
		ui_event.emit({"kind": "card", "card_id": id}))
	if bool(snapshot.get("controls", {}).get("manuscript", false)): _footer("重演手稿（R）", open_manuscript, false, "", "secondary")
	_footer("合上（Tab）", close_modal, false, "", "secondary")

func _replay_data() -> Dictionary:
	return snapshot.get("replay", {})

func open_manuscript() -> void:
	if not bool(_replay_data().get("open", false)): return
	var body := open_modal("", Vector2(1152, 0), "replay")
	_manuscript = MANUSCRIPT.instantiate()
	_manuscript.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_manuscript.custom_minimum_size = Vector2(0, 704)
	body.add_child(_manuscript)
	_manuscript.show_data(_replay_data())
	_manuscript.vars_changed.connect(func(vars: Array) -> void: ui_event.emit({"kind": "replay_vars", "vars": vars}))
	_manuscript.run_requested.connect(func(vars: Array, skip: bool) -> void: ui_event.emit({"kind": "replay", "vars": vars, "skip": skip}))
	_manuscript.open_card.connect(func(id: String) -> void:
		close_modal()
		ui_event.emit({"kind": "card", "card_id": id}))
	_manuscript.close_requested.connect(close_modal)
	ui_event.emit({"kind": "opened", "panel": "manuscript"})

## 设置册：整页册子盖在现场之上；合上后回到原处。
func open_settings(page := "rules") -> void:
	close_modal()
	modal_kind = "settings"
	Sound.sfx("page", -6.0)
	var book: Control = SETTINGS_BOOK.instantiate()
	book.page = page
	book.can_save = false
	modal_layer.add_child(book)
	modal_root = book
	book.closed.connect(close_modal)

func open_campus_map() -> void:
	var campus: Dictionary = snapshot.get("campus", {})
	var body := open_modal(str(campus.get("title", "学园图")), Vector2(1152, 0), "map")
	var view: Control = CAMPUS_MAP.instantiate()
	view.show_data(campus)
	view.known = campus.get("known", {})
	body.add_child(view)
	view.picked.connect(func(node: String) -> void:
		if not view.nodes.has(node): return
		var selected: Dictionary = view.nodes[node].duplicate(true)
		close_modal()
		ui_event.emit({"kind": "travel", "node_id": node, "node": selected}))
	_footer("合上（M）", close_modal, false, "", "secondary")

## 学园手稿图上的墨影回放（多地点的一夜）；看完回执。
func _campus_replay(ev: Dictionary) -> Dictionary:
	if str(ev.get("mode", "")) != "campus": return _ack(ev)
	var body := open_modal(str(ev.get("title", "墨影回放")), Vector2(1152, 0), "playback")
	var generation := _modal_generation
	_campus = CAMPUS_MAP.instantiate()
	_campus.mode = "replay"
	_campus.show_data(ev.get("campus", snapshot.get("campus", {})))
	body.add_child(_campus)
	var playback: Control = _campus
	Sound.sfx("replay", -4.0)
	Sound.duck(true)
	await playback.play(ev.get("steps", []), bool(ev.get("full_match", false)))
	Sound.duck(false)
	if _closing or generation != _modal_generation: return {}
	close_modal()
	return _ack(ev)

# ================================================================= keyboard ===

func _unhandled_input(event: InputEvent) -> void:
	if Engine.is_editor_hint() or _closing: return
	if event is not InputEventKey and event is not InputEventMouseButton: return
	if not event.is_pressed() or event.is_echo(): return
	var controls: Dictionary = snapshot.get("controls", {})
	if modal_kind == "ending":
		# 结局卡的按钮键（R、C 等）由宿主随事件给出，不是可改键动作；“Enter”按对话继续键处理。
		if event is not InputEventKey: return
		for action in _modal_event.get("actions", []):
			var key := str(action.get("key", ""))
			if (key == "Enter" and event.is_action_pressed("dialogue_continue")) or (key.length() == 1 and OS.get_keycode_string(event.keycode) == key):
				if not bool(action.get("available", true)): continue
				# 先标记已处理：续章会把本界面移出场景树，之后就拿不到 viewport 了。
				get_viewport().set_input_as_handled()
				_navigate(str(action.id))
				return
		return
	if modal_kind == "settings":
		if event.is_action_pressed("cancel") or event.is_action_pressed("settings"):
			close_modal()
			get_viewport().set_input_as_handled()
		return
	if modal_kind == "" and event.is_action_pressed("settings"):
		open_settings()
		get_viewport().set_input_as_handled()
		return
	if modal_kind == "card" and (event.is_action_pressed("cancel") or event.is_action_pressed("dialogue_continue")):
		_dismiss()
		get_viewport().set_input_as_handled()
		return
	if modal_kind != "":
		if event.is_action_pressed("cancel") or (event.is_action_pressed("notebook") and modal_kind == "notebook") \
				or (event.is_action_pressed("manuscript") and modal_kind == "replay") or (event.is_action_pressed("campus_map") and modal_kind == "map"):
			_dismiss()
			get_viewport().set_input_as_handled()
		return
	if dialogue.active or not bool(snapshot.get("field_enabled", false)): return
	if event.is_action_pressed("notebook"):
		if bool(controls.get("notebook", false)): open_notebook()
	elif event.is_action_pressed("manuscript"):
		if bool(controls.get("manuscript", false)): open_manuscript()
	elif event.is_action_pressed("campus_map"):
		if bool(controls.get("campus", false)): open_campus_map()
	elif event.is_action_pressed("wait"):
		if bool(controls.get("wait", false)): ui_event.emit({"kind": "action", "act": {"cmd": "stay"}})
	elif event.is_action_pressed("cancel"):
		open_settings()
	else:
		return
	get_viewport().set_input_as_handled()

## 合上：宿主弹窗交还它的“返回”选项；本地面板直接合上。
func _dismiss() -> void:
	if modal_kind in ["card", "menu"]:
		if not _close_option.is_empty(): _select(_modal_event, str(_close_option.id))
		return
	if modal_kind == "form":
		for action in _modal_event.get("actions", []):
			if str(action.id) == "cancel":
				_form_action(_modal_event, action)
				return
		return
	if modal_kind == "playback": return
	close_modal()

# ================================================================== preview ===

func _preview() -> void:
	if not Engine.is_editor_hint() or not is_node_ready(): return
	close_modal()
	Paper.clear_children($ModalLayer)
	$Dialogue.visible = preview_page == 1
	var samples := [
		{}, {},
		{"kind": "candidates", "title": "跟他说什么", "options": [{"id": "ask", "label": "问问他知道些什么"}, {"id": "cancel", "label": "没什么了"}]},
		{"kind": "card", "title": "示例证物", "facts": "证物的可见事实。", "source": "现场 · 某处", "notes": ["[color=seal]示例：语义色的笔记。[/color]"], "options": [{"id": "close", "label": "返回现场"}]},
		{"kind": "form", "title": "报名", "prompt": "你叫什么？", "layout": "stack", "fields": [{"id": "name", "kind": "text", "placeholder": "名字"}], "actions": [{"id": "ok", "label": "就叫这个"}]},
		{}, {},
		{"kind": "ending", "title": "结束", "outcome": "这一段旅程结束了。", "rows": ["记录已保存。"], "actions": [{"id": "restart", "label": "重新开始 (R)"}]},
	]
	var sample: Dictionary = samples[preview_page] if preview_page >= 0 and preview_page < samples.size() else {}
	match str(sample.get("kind", "")):
		"candidates": _candidates(sample)
		"card": _card(sample)
		"form": _form_open(sample)
		"ending": _ending(sample)
	if preview_page == 5:
		var body := open_modal("笔记本", Vector2(1152, 0), "notebook")
		var view: Control = NOTEBOOK.instantiate()
		body.add_child(view)
		view.show_data({"empty_text": "还没有记下任何东西。", "sections": [{"heading": "[b]某处[/b]", "style": "grid", "entries": [{"label": "示例证物", "card_id": "E1"}]}]})
	elif preview_page == 6:
		var body := open_modal("", Vector2(1152, 0), "replay")
		var view: Control = MANUSCRIPT.instantiate()
		view.custom_minimum_size = Vector2(0, 704)
		body.add_child(view)

## 当前正在展示的来源；正文的可见程度由绘制后的控件读取决定。
func view_trace_context() -> Dictionary:
	var event: Dictionary = _modal_event if modal_kind != "" else _trace_display
	var result := {"trace_id": str(event.get("trace_id", "")), "presentation_id": str(event.get("presentation_id", "")),
		"panel": modal_kind if modal_kind != "" else "dialogue" if dialogue.active else "field"}
	if dialogue.active and dialogue.line != null:
		result.merge(dialogue.line.source)
		result["phase"] = "full" if dialogue.body.visible_characters < 0 else "partial"
	return result
