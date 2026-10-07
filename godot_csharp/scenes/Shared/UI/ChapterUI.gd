@tool
extends Node
## 所有章节完全一致的界面：抬头、对白、菜单、证物卡、表单、笔记本、重演手稿、学园图与章末卡。
## 只读章节事件与快照（设计第四部分 §2），玩家的操作以 ui_event 发出，由宿主转给章节；
## 这里不含任何章节的文字、表格或规则。各组件在 HUD/、Dialogue/、Menu/、Card/、Form/、Notebook/、
## Manuscript/、CampusMap/、Ending/ 中，都可在编辑器里改值预览。

signal ui_event(event: Dictionary)

const Paper := preload("res://scenes/Shared/UI/Common/Paper.gd")
const Sound := preload("res://scenes/Shared/Audio/Sound.gd")
const CHOICE_MENU := preload("res://scenes/Shared/UI/Menu/ChoiceMenu.tscn")
const EVIDENCE_CARD := preload("res://scenes/Shared/UI/Card/EvidenceCard.tscn")
const FORM_PANEL := preload("res://scenes/Shared/UI/Form/FormPanel.tscn")
const NOTEBOOK := preload("res://scenes/Shared/UI/Notebook/Notebook.tscn")
const MANUSCRIPT := preload("res://scenes/Shared/UI/Manuscript/ReplayManuscript.tscn")
const CAMPUS_MAP := preload("res://scenes/Shared/UI/CampusMap/CampusMap.tscn")
const CampusMap := preload("res://scenes/Shared/UI/CampusMap/CampusMap.gd")
const ENDING := preload("res://scenes/Shared/UI/Ending/EndingCard.tscn")

@export_group("Editor Preview")
## 在编辑器里切换整套界面的预览页；各组件的细节在其自身场景里改值预览。
@export_enum("现场", "对白", "菜单", "证物卡", "表单", "笔记本", "重演手稿", "章末") var preview_page := 0:
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
	if is_instance_valid(dialogue): dialogue.cancel_presentation()
	if is_instance_valid(_campus): _campus.cancel_playback()
	close_modal()

func is_busy() -> bool:
	return modal_kind != "" or (is_instance_valid(dialogue) and dialogue.active)

# ================================================================ snapshot ===

func apply_snapshot(value: Dictionary) -> void:
	var cast_changed: bool = value.get("cast", {}) != snapshot.get("cast", {})
	snapshot = value
	var head: Dictionary = value.get("hud", {})
	hud.set_title(str(head.get("title", "")))
	hud.set_time(str(head.get("time", "")))
	hud.set_keys(head.get("keys", []))
	var chapter: Dictionary = value.get("chapter", {})
	if chapter.has("window_title"): get_window().title = str(chapter.window_title)
	if cast_changed: dialogue.set_dialogue_cast(value.get("cast", {}))
	if modal_kind == "notebook" and is_instance_valid(_notebook): _notebook.show_data(value.get("notebook", {}))
	if modal_kind == "replay" and is_instance_valid(_manuscript): _manuscript.show_data(_replay_data())

func set_hint(text: String) -> void: hud.set_hint(text)

func show_hover(text: String, screen_position: Vector2) -> void:
	if text == "": hud.hide_hover()
	else: hud.show_hover(text, screen_position)

# ================================================================== events ===

## 呈现一个章节事件。台词会等到读完，返回回执（presented / select）；其余立即返回空字典。
func present(ev: Dictionary) -> Dictionary:
	if _closing: return {}
	match str(ev.kind):
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
		_: push_error("Unsupported chapter display output: " + str(ev.kind))
	return {}

func _character(line: Dictionary) -> String:
	# 第二章人物台词以人物编号说话；立绘与声音认人名。侦探的称呼另由显示名给出。
	var speaker := str(line.get("speaker", ""))
	var name := str(line.get("speaker_name", speaker))
	return name if RegEx.create_from_string("^[A-Z][A-Za-z]+$").search(speaker) != null else speaker

func _lines(ev: Dictionary) -> Dictionary:
	var lines: Array = []
	for line in ev.get("lines", []):
		var entry: Dictionary = line.duplicate()
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
	if _closing: return {}
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
	return modal_kind == "form"

func _select(ev: Dictionary, id: String) -> void:
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
	if is_instance_valid(modal_root): modal_root.queue_free()
	modal_root = null
	modal_kind = ""
	_modal_event = {}
	_close_option = {}
	_form = null
	_manuscript = null
	_notebook = null

func _footer(label: String, action: Callable, disabled := false, tooltip := "") -> Button:
	return modal_root.add_footer_button(label, action, disabled, tooltip)

func _candidates(ev: Dictionary) -> void:
	var options: Array = ev.get("options", [])
	var body := open_modal(str(ev.get("title", "")), Vector2(860, mini(800, 220 + 56 * options.size())), "menu")
	_modal_event = ev
	_remember_close(ev)
	var menu: Control = CHOICE_MENU.instantiate()
	body.add_child(menu)
	menu.show_data({"prompt": str(ev.get("prompt", "")), "options": options})
	menu.chosen.connect(func(id: String) -> void: _select(ev, id))

func _card(ev: Dictionary) -> void:
	var text := str(ev.get("facts", ""))
	var tall: bool = ev.has("picture") or ev.has("table") or not ev.get("notes", []).is_empty()
	var body := open_modal(str(ev.get("title", "")), Vector2(900, 660 if tall else clampi(340 + 30 * text.length() / 34, 340, 640)), "card")
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
	var body := open_modal(str(ev.get("title", "")), Vector2(900, 820 if str(ev.get("layout", "")) == "tabs" else 600), "form")
	_modal_event = ev
	_form = FORM_PANEL.instantiate()
	body.add_child(_form)
	_form.show_data(ev)
	_form_actions = []
	for action in ev.get("actions", []):
		var button := _footer(str(action.label), _form_action.bind(ev, action))
		_form_actions.append([button, action])
	_form.changed.connect(_refresh_form_actions)
	_form.submit_requested.connect(func() -> void:
		for pair in _form_actions:
			if bool(pair[1].get("default", false)) and not pair[0].disabled: _form_action(ev, pair[1]))
	_refresh_form_actions()
	if _form.has_method("focus_text"): _form.focus_text.call_deferred()

func _refresh_form_actions() -> void:
	if not is_instance_valid(_form): return
	for pair in _form_actions:
		var requires: Array = pair[1].get("requires", [])
		pair[0].disabled = not requires.is_empty() and not _form.is_complete(requires)

func _form_action(ev: Dictionary, action: Dictionary) -> void:
	if action.has("tab"):
		_form.show_field(str(action.tab))
		return
	var requires: Array = action.get("requires", [])
	if not requires.is_empty() and not _form.is_complete(requires): return
	var values: Dictionary = _form.values()
	close_modal()
	ui_event.emit({"kind": "form", "presentation_id": str(ev.get("presentation_id", "")), "action": str(action.id), "values": values})

func _ending(ev: Dictionary) -> void:
	var body := open_modal(str(ev.get("title", "")), Vector2(820, 680), "ending")
	_modal_event = ev
	var view: Control = ENDING.instantiate()
	body.add_child(view)
	view.show_data(ev)
	for action in ev.get("actions", []):
		_footer(str(action.label), func() -> void: ui_event.emit({"kind": "navigate", "to": str(action.id)}))

# ========================================================== local panels ===

func open_notebook() -> void:
	var body := open_modal("笔记本", Vector2(1040, 820), "notebook")
	_notebook = NOTEBOOK.instantiate()
	body.add_child(_notebook)
	_notebook.show_data(snapshot.get("notebook", {}))
	_notebook.open_card.connect(func(id: String) -> void:
		close_modal()
		ui_event.emit({"kind": "card", "card_id": id}))
	if bool(snapshot.get("controls", {}).get("manuscript", false)): _footer("重演手稿（R）", open_manuscript)
	_footer("合上（Tab）", close_modal)

func _replay_data() -> Dictionary:
	return snapshot.get("replay", {})

func open_manuscript() -> void:
	if not bool(_replay_data().get("open", false)): return
	open_modal("", Vector2(1200, 900), "replay")
	var column: VBoxContainer = modal_root.get_meta("column")
	for child in column.get_children(): child.queue_free()
	_manuscript = MANUSCRIPT.instantiate()
	_manuscript.size_flags_vertical = Control.SIZE_EXPAND_FILL
	column.add_child(_manuscript)
	_manuscript.show_data(_replay_data())
	_manuscript.vars_changed.connect(func(vars: Array) -> void: ui_event.emit({"kind": "replay_vars", "vars": vars}))
	_manuscript.run_requested.connect(func(vars: Array, skip: bool) -> void: ui_event.emit({"kind": "replay", "vars": vars, "skip": skip}))
	_manuscript.open_card.connect(func(id: String) -> void:
		close_modal()
		ui_event.emit({"kind": "card", "card_id": id}))
	_manuscript.close_requested.connect(close_modal)
	ui_event.emit({"kind": "opened", "panel": "manuscript"})

func open_campus_map() -> void:
	var campus: Dictionary = snapshot.get("campus", {})
	open_modal("学园图（点一处，直接去那里；走路不过时辰）", Vector2(1080, 820), "map")
	var column: VBoxContainer = modal_root.get_meta("column")
	var view: Control = CAMPUS_MAP.instantiate()
	for key in CampusMap.NODES:
		if str(CampusMap.NODES[key].map) == str(campus.get("map", "")): view.here = key
	view.known = campus.get("known", {})
	column.add_child(view)
	view.picked.connect(func(node: String) -> void:
		close_modal()
		ui_event.emit({"kind": "travel", "node": CampusMap.NODES[node]}))
	_footer("合上（M）", close_modal)

## 学园手稿图上的墨影回放（多地点的一夜）；看完回执。
func _campus_replay(ev: Dictionary) -> Dictionary:
	if str(ev.get("mode", "")) != "campus": return _ack(ev)
	open_modal("那一夜（墨影回放）", Vector2(1080, 800), "playback")
	var column: VBoxContainer = modal_root.get_meta("column")
	_campus = CAMPUS_MAP.instantiate()
	_campus.mode = "replay"
	column.add_child(_campus)
	await get_tree().process_frame
	Sound.sfx("replay", -4.0)
	Sound.duck(true)
	await _campus.play(ev.get("steps", []), bool(ev.get("full_match", false)))
	Sound.duck(false)
	if _closing: return {}
	close_modal()
	return _ack(ev)

# ================================================================= keyboard ===

func _unhandled_input(event: InputEvent) -> void:
	if Engine.is_editor_hint() or _closing: return
	if event is not InputEventKey or not event.pressed or event.echo: return
	var controls: Dictionary = snapshot.get("controls", {})
	if modal_kind == "ending":
		for action in _modal_event.get("actions", []):
			var key := str(action.get("key", ""))
			if (key == "Enter" and event.keycode in [KEY_ENTER, KEY_SPACE]) or (key.length() == 1 and OS.get_keycode_string(event.keycode) == key):
				ui_event.emit({"kind": "navigate", "to": str(action.id)})
				get_viewport().set_input_as_handled()
				return
		return
	if modal_kind == "card":
		_dismiss()
		get_viewport().set_input_as_handled()
		return
	if modal_kind != "":
		if event.keycode == KEY_ESCAPE or (event.keycode == KEY_TAB and modal_kind == "notebook") \
				or (event.keycode == KEY_R and modal_kind == "replay") or (event.keycode == KEY_M and modal_kind == "map"):
			_dismiss()
			get_viewport().set_input_as_handled()
		return
	if dialogue.active or not bool(snapshot.get("field_enabled", false)): return
	match event.keycode:
		KEY_TAB:
			if bool(controls.get("notebook", false)): open_notebook()
		KEY_R:
			if bool(controls.get("manuscript", false)): open_manuscript()
		KEY_M:
			if bool(controls.get("campus", false)): open_campus_map()
		KEY_T:
			if bool(controls.get("wait", false)): ui_event.emit({"kind": "action", "act": {"cmd": "stay"}})
		_:
			return
	get_viewport().set_input_as_handled()

## 合上：章节给的弹窗交还它的“返回”选项；界面自己的弹窗直接合上。
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
	for child in $ModalLayer.get_children(): child.queue_free()
	$Dialogue.visible = preview_page == 1
	var samples := [
		{}, {},
		{"kind": "candidates", "title": "跟霍铎说什么", "options": [{"id": "ask", "label": "问问他知道些什么"}, {"id": "cancel", "label": "没什么了"}]},
		{"kind": "card", "title": "闩上的隔离门", "facts": "门内侧的插闩落在闩槽里。", "source": "门厅 · 隔离门内侧", "notes": ["回忆：霍铎说闩。"], "options": [{"id": "close", "label": "返回现场"}]},
		{"kind": "form", "title": "报名", "prompt": "你叫什么？", "layout": "stack", "fields": [{"id": "name", "kind": "text", "placeholder": "名字"}], "actions": [{"id": "ok", "label": "就叫这个"}]},
		{}, {},
		{"kind": "ending", "title": "第一章 · 完", "outcome": "门开了。", "rows": ["留在恶魔那里的钩：无"], "actions": [{"id": "restart", "label": "重新开始 (R)"}]},
	]
	var sample: Dictionary = samples[preview_page] if preview_page < samples.size() else {}
	match str(sample.get("kind", "")):
		"candidates": _candidates(sample)
		"card": _card(sample)
		"form": _form_open(sample)
		"ending": _ending(sample)
	if preview_page == 5:
		var body := open_modal("笔记本", Vector2(1040, 820), "notebook")
		var view: Control = NOTEBOOK.instantiate()
		body.add_child(view)
		view.show_data({"empty_text": "还没有记下任何东西。", "sections": [{"heading": "[b]门厅[/b]", "style": "grid", "entries": [{"label": "闩上的隔离门", "card_id": "E1"}]}]})
	elif preview_page == 6:
		open_modal("", Vector2(1200, 900), "replay")
		var column: VBoxContainer = modal_root.get_meta("column")
		for child in column.get_children(): child.queue_free()
		column.add_child(MANUSCRIPT.instantiate())
