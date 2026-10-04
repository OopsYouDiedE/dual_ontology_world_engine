extends "res://scenes/Chapter2/Field/Chapter2Field.gd"
## 第二章游戏宿主：地图行走 + 原型系统（Chapter2Bridge 在后台线程跑原型的文字循环）。
## 每个时段做一件事：走到人跟前按 E 与他打交道，走到物件或公告前按 E 细看；Q 打开行动菜单；
## Tab 看笔记；R 重演；H 回看。时段过去后，众人按推演换到各自的去处。

const Paper := preload("res://scenes/Shared/UI/Paper.gd")
const Sound := preload("res://scenes/Shared/Audio/Sound.gd")
const ReplayManuscript := preload("res://scenes/Shared/UI/ReplayManuscript.gd")
const ReplayStage := preload("res://scenes/Chapter2/Game/ReplayStage.gd")
const HintOverlay := preload("res://scenes/Shared/UI/HintOverlay.gd")
const ChapterUI := preload("res://scenes/Shared/UI/ChapterUI.gd")
const BridgeScript := preload("res://scenes/Chapter2/Game/Chapter2Bridge.cs")
const FRAMES_DIR := "res://ShareAsset/Internal/Characters/animation_v2/%s.tres"

@export_file("*.dll") var world_script_path := ""

var PLACE_MAP := {}
var MAP_PLACE := {}
var SPRITES := {}
const PORTRAITS := {
	"侦探": "res://ShareAsset/Internal/Characters/FirstAwakeningPortraits/detective.png",
	"工程师": "res://ShareAsset/Internal/Characters/FirstAwakeningPortraits/engineer.png",
	"厨师": "res://ShareAsset/Internal/Characters/FirstAwakeningPortraits/chef.png",
	"音乐家": "res://ShareAsset/Internal/Characters/FirstAwakeningPortraits/musician.png",
}

const SLOT_TINT := [Color(1.0, 0.96, 0.9), Color(1, 1, 1), Color(1, 1, 0.97), Color(1.0, 0.95, 0.86), Color(0.96, 0.8, 0.68), Color(0.42, 0.47, 0.66)]

var bridge: Node
var state := {}
var busy := true
var ui_lock := false
var info_capture := false
var info_lines: PackedStringArray = []
var info_title := "笔记本"
var card_texture: Texture2D
var actors := {}
var last_slot_key := ""
var ended := false
var intro_done := false
var pending_proposal := ""
var pending_rule_menu: Array = []
var action_playing := false
signal action_group_finished
var action_jobs := 0
var frames_revision := -1
var frames_consumed := 0
var story_presentations_consumed := 0
var story_media: Array = []

var chapter_ui: Node
var closed_ms := -600
var dialogue: CanvasLayer
var visible_output: Array[String] = []
var output_pending: PackedStringArray = []
var awaiting_replay := false
var skip_replay := false
var replay_vars: Array = [0, 0, 0]
var last_run_vars: Array = []
var replay_result := {}
var replay_variables: Array = []
var replay_context: Array = []
var replay_evidence := {}
var manuscript: HBoxContainer
var modal_layer: CanvasLayer
var modal_root: Control
var modal_kind := ""
var hud_title: Label
var hud_hint: Label
var hud_keys: Label
var hover_tip: Label
var aside: Label
var stage_node: Node2D
var sites := {}
var examined := {}
var nearest := ""
var hovered := ""
var ui: CanvasLayer
var tint: CanvasModulate
var internal_log: Array[String] = []
var record_textures := {}
var pending_inspection := {}
var pending_board := false
var modal_box: VBoxContainer
var busy_label: Label

func _ready() -> void:
	TranslationServer.set_locale("zh_CN")
	texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	get_window().content_scale_size = Vector2i(1280, 992)
	get_window().content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	get_window().content_scale_stretch = Window.CONTENT_SCALE_STRETCH_INTEGER
	get_window().size = Vector2i(1280, 992)
	get_window().title = "安眠者有福 · 第二章"
	start_map = "central_hall"
	start_spawn = "south"
	super()
	player.collision_mask = 1
	place_label.visible = false
	tint = CanvasModulate.new()
	add_child(tint)
	_build_ui()
	bridge = BridgeScript.new()
	bridge.name = "Bridge"
	add_child(bridge)
	bridge.WorldScriptPath = world_script_path
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--world-script="):
			bridge.WorldScriptPath = argument.substr(15)
	dialogue = $Dialogue
	chapter_ui.configure_dialogue(dialogue, PORTRAITS)
	dialogue.finished.connect(_dialogue_finished)
	bridge.Start()
	Sound.music("day", 2.5)

# ------------------------------------------------------------------ 宿主循环 --

func _process(_delta: float) -> void:
	_update_targets()
	_refresh_hud()
	_refresh_hud()
	if bridge == null:
		return
	var waiting: bool = bridge.IsWaiting()
	var out: Array = bridge.TakeOutput()
	for line in out:
		_on_output(String(line))
	if action_playing:
		return
	if busy and waiting:
		busy = false
		busy_label.visible = false
		_on_ready()
	elif bridge.IsDone() and not ended:
		ended = true
		busy = false
		busy_label.visible = false
		state = bridge.Snapshot()
		await _play_actions()
		_present_output()

func _on_output(line: String) -> void:
	if line.begins_with("\u001estory-media:"):
		if info_capture:
			info_lines.append(line)
		else:
			output_pending.append(line)
		return
	visible_output.append(line)
	_log(line)
	if info_capture:
		info_lines.append(line)
	elif not awaiting_replay and not line.strip_edges().is_empty() and not (line.begins_with("【第") and line.contains("】你在")):
		output_pending.append(line)

func send(line: String, info := false) -> void:
	if busy:
		return
	busy = true
	busy_label.text = "推演中…"
	busy_label.visible = true
	info_capture = info
	info_lines = []
	if not info:
		_log("[color=#8a3324]▸ " + line + "[/color]")
	bridge.Send(line)

func send_many(lines: Array) -> void:
	if busy:
		return
	busy = true
	busy_label.text = "推演中…"
	busy_label.visible = true
	info_capture = false
	for l in lines:
		_log("[color=#8a3324]▸ " + String(l) + "[/color]")
		bridge.Send(String(l))

func _on_ready() -> void:
	state = bridge.Snapshot()
	if state.is_empty():
		return
	PLACE_MAP = state.host_places
	MAP_PLACE = state.map_places
	SPRITES = state.sprites
	story_media.append_array(state.story_presentations.slice(story_presentations_consumed))
	story_presentations_consumed = state.story_presentations.size()
	var slot_key := "%d-%d" % [state.day, state.slot]
	var changed := slot_key != last_slot_key
	last_slot_key = slot_key
	_refresh_hud()
	tint.color = SLOT_TINT[int(state.slot)]
	var mode := String(state.mode)
	# 配乐随局面：第 4 日清晨的对质与第 7 日名录库之议用对质曲，夜里是夜曲，其余是日间曲。
	var confronting := mode in ["confront", "council"] or (int(state.day) == 4 and int(state.slot) == 0)
	Sound.music("confrontation" if confronting else ("night" if int(state.slot) == 5 else "day"))
	if changed:
		Sound.sfx("bell_slot", -10.0)
		var place := String(state.det_place)
		if MAP_PLACE.get(String(map.name), "") != place and PLACE_MAP.has(place):
			var target: Array = PLACE_MAP[place]
			load_map(target[0], target[1])
		else:
			_spawn_actors()
	else:
		_spawn_actors()
	if awaiting_replay:
		awaiting_replay = false
		if state.has("replay"):
			accept_replay(state.replay)
		else:
			_show_info(info_lines)
		info_capture = false
		return
	if info_capture:
		await _play_actions()
		info_capture = false
		if not pending_rule_menu.is_empty():
			var request := pending_rule_menu
			pending_rule_menu = []
			_show_rule_choices(request[0], request[1])
			return
		if pending_board:
			pending_board = false
			open_record_card("board:" + String(state.room.place))
			return
		var marker := -1
		for index in info_lines.size():
			if info_lines[index].begins_with("\u001estory-media:"):
				marker = index
				break
		if marker >= 0:
			output_pending.append_array(info_lines.slice(marker))
			_show_info(info_lines.slice(0, marker))
		else:
			_show_info(info_lines)
		return
	await _play_actions()
	_present_output()

func load_map(key: String, spawn: String, at := Vector2.ZERO, use_at := false, synchronize := true) -> void:
	actors.clear()
	super(key, spawn, at, use_at)
	if synchronize and bridge != null and bridge.SetMap(key):
		if bridge.IsWaiting():
			state = bridge.Snapshot()
		else:
			busy = true
			busy_label.text = "推演中…"
			busy_label.visible = true
	_spawn_actors()

func _spawn_actors() -> void:
	if map == null:
		return
	for a in actors.values():
		if is_instance_valid(a):
			a.queue_free()
	actors.clear()
	sites.clear()
	if state.is_empty():
		return
	var here := String(MAP_PLACE.get(String(map.name), ""))
	if here == "" or here != String(state.room.place):
		_build_sites()
		return
	var stands: Array = []
	var stand_node := map.get_node_or_null("Stands")
	if stand_node:
		for m in stand_node.get_children():
			stands.append((m as Marker2D).position)
	if String(map.name) == "granary":
		_build_sites()
		return
	var i := 0
	for p in state.room.people:
		var pos: Vector2 = stands[i % max(1, stands.size())] if not stands.is_empty() else Vector2(96 + 40 * i, 160)
		if stands.size() > 0 and i >= stands.size():
			pos += Vector2(18, 10)
		i += 1
		var actor := CharacterBody2D.new()
		actor.set_script(ACTOR)
		var path: String = String(p.frames_path) if p.has("frames_path") else FRAMES_DIR % SPRITES[String(p.id)]
		actor.standard = load(path)
		actor.name = String(p.name)
		actor.set_meta("person", String(p.name))
		actor.position = pos
		map.get_node("Objects").add_child(actor)
		var tag := Label.new()
		tag.text = String(p.name)
		tag.add_theme_font_override("font", Paper.font())
		tag.add_theme_font_size_override("font_size", 10)
		tag.add_theme_color_override("font_color", Color(0.97, 0.94, 0.86))
		tag.add_theme_color_override("font_outline_color", Color(0.1, 0.08, 0.1))
		tag.add_theme_constant_override("outline_size", 4)
		tag.position = Vector2(-tag.text.length() * 5, -62)
		actor.add_child(tag)
		actor.collision_mask = 1
		actor.face_point(player.position)
		actors[String(p.name)] = actor
	_build_sites()

func _physics_process(delta: float) -> void:
	super(delta)

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_H and dialogue.history_panel.visible:
		dialogue.toggle_history()
		get_viewport().set_input_as_handled()
		return
	if (not field_active() and modal_kind == "") or not (event is InputEventKey) or not event.pressed or event.echo:
		return
	var k := (event as InputEventKey).keycode
	if modal_kind != "":
		if ChapterUI.closes_modal(k, modal_kind):
			var story_image := modal_kind == "story_image"
			_close_modal()
			if story_image:
				_dialogue_finished()
		return
	if busy:
		return
	match k:
		KEY_E, KEY_SPACE, KEY_ENTER:
			if Time.get_ticks_msec() - closed_ms < 600:
				return
			_interact()
		KEY_Q:
			_open_actions()
		KEY_TAB:
			open_notebook()
		KEY_R:
			open_manuscript()
		KEY_H:
			dialogue.toggle_history()
		_:
			return
	get_viewport().set_input_as_handled()

func _interact() -> void:
	if nearest != "":
		complete_click_interaction(nearest)
	else:
		_open_place()

func _menu(title: String, items: Array, note := "") -> void:
	close_modal()
	ui_lock = true
	modal_kind = "menu"
	_cancel_click_navigation()
	modal_box = chapter_ui.menu(title, items, note, _close_modal)
	modal_root = chapter_ui.modal_root

func _act(line: String) -> Callable:
	return func():
		_close_modal()
		send(line)

func _info(line: String) -> Callable:
	return func():
		_close_modal()
		info_title = "笔记本" if line == "笔记" else line
		card_texture = null
		send(line, true)

func _people_names() -> Array:
	var a := []
	if state.has("people"):
		for p in state.people:
			if String(p.id) != "Det":
				a.append(String(p.name))
	return a

func _place_name(place: String) -> String:
	return String(state.place_names.get(place, place)) if state.has("place_names") else place

func _room_people_names() -> Array:
	var names := []
	for p in state.room.people:
		names.append(String(p.name))
	return names

func _open_person(who: String) -> void:
	for p in state.room.people:
		if String(p.name) == who:
			_menu(who, _room_action_items(p.actions), "每件事占一个时段（看笔记与打赌不占）。")
			return

func _room_action_items(actions: Array) -> Array:
	var items := []
	for action in actions:
		var selected: Dictionary = action
		items.append([String(selected.label), func(): _choose_room_action(selected)])
	return items

func _choose_room_action(action: Dictionary) -> void:
	var command := String(action.command)
	match String(action.kind):
		"inspect":
			_inspect(command.trim_prefix("查 "))
		"notes":
			var who := command.trim_prefix("笔记 ")
			for person in state.room.people:
				if String(person.name) == who:
					open_record_card("person:" + String(person.id))
		"info":
			if command == "公告":
				_close_modal()
				pending_board = true
				send(command, true)
			else:
				_info(command).call()
		"confront":
			_open_confront(false)
		"reopen":
			_open_confront(true)
		"night":
			_open_night()
		"post", "tellrule":
			_pick_rule(String(action.label), command)
		"class_question":
			_ask_demon()
		"greenhouse":
			_pick_crew(String(action.label), 2, command)
		"archive":
			_pick_crew(String(action.label), 3, command)
		"ask":
			_pick(String(action.label), state.jobs, func(j): send(command + " " + j))
		"escort":
			_pick_thing(String(action.label), func(t): send(command + " " + t))
		"news":
			_pick_news(command.substr(command.find(" ") + 1))
		"givekey":
			_pick(String(action.label), _room_people_names(), func(t): send(command + " " + t))
		"slant":
			_pick(String(action.label), _people_names(), func(t): send(command + " " + t))
		"wager":
			_pick("猜谁今晚会去哪里", _people_names(), func(t): _pick_wager_place(command + " " + t))
		_:
			_act(command).call()

func _pick_wager_place(prefix: String) -> void:
	var items := []
	for place in PLACE_MAP:
		var command := prefix + " " + _place_name(place)
		items.append([_place_name(place), func(): _pick_wager_rule(command)])
	_menu("猜今晚会去哪里", items)

func _pick_wager_rule(prefix: String) -> void:
	var items := [["只赌去处", _act(prefix)]]
	for rule in state.rules:
		items.append(["说中就认「%s」" % String(rule.text), _act(prefix + " " + String(rule.label))])
	_menu("赌约", items)

func _open_place() -> void:
	var here := String(state.room.place)
	var items := []
	for action in state.room.actions:
		if String(action.kind) != "inspect":
			items.append_array(_room_action_items([action]))
	_menu("此处：" + (_place_name(here) if here != "" else String(map.get_meta("title", map.name))), items)

func _open_actions() -> void:
	var items := []
	if nearest != "":
		var id := nearest
		items.append(["检视：" + String(sites[id].title), func(): _close_modal(); complete_click_interaction(id)])
	for action in state.room.actions:
		if String(action.kind) != "inspect":
			items.append_array(_room_action_items([action]))
	if int(state.slot) == 5 and String(state.room.place) != "":
		items.append(["在这里守夜", _act("守夜 " + _place_name(String(state.room.place)))])
	items.append(["笔记本（Tab）", open_notebook])
	if not state.get("replay_questions", []).is_empty():
		items.append(["昨夜重演（R）", open_manuscript])
	items.append(["回看对白（H）", func(): _close_modal(); dialogue.toggle_history()])
	_menu(_place_name(String(state.room.place)), items)

func _pick(title: String, options: Array, done: Callable) -> void:
	var items := []
	for o in options:
		var v = o
		items.append([String(o), func(): _close_modal(); done.call(v)])
	_menu(title, items)

func _pick_thing(title: String, done: Callable) -> void:
	var opts := []
	for t in state.things:
		opts.append(String(t.key))
	_pick(title, opts, done)


func _pick_news(who: String) -> void:
	var opts: Array = state.get("news", [])
	if opts.is_empty():
		_menu("转告", [["你还没有听到什么消息。", _close_modal]])
		return
	_pick("把哪条消息转告%s" % who, opts, func(n): send("转告 %s %s" % [who, String(n).split(".")[0]]))

func _pick_rule(title: String, prefix: String) -> void:
	pending_rule_menu = [title, prefix]
	_close_modal()
	send("规矩", true)

func _show_rule_choices(title: String, prefix: String) -> void:
	var items := [["拼一条新规矩……", func(): _card_builder(func(cells): send(prefix + " " + cells))]]
	for r in state.get("rules", []):
		var label := String(r.label)
		items.append(["%s「%s」" % [label, String(r.text)], _act(prefix + " " + label)])
	_menu(title, items)

func _ask_demon() -> void:
	var items := []
	for t in state.topics:
		var topic := String(t)
		items.append(["问“%s”——含糊地" % topic, _act("上课 %s 含糊" % topic)])
		items.append(["问“%s”——精确地" % topic, _act("上课 %s 精确" % topic)])
	_menu("答疑：问什么（课堂上的回答是公开的）", items)

func _pick_crew(title: String, n: int, cmd: String) -> void:
	_open_modal(title)
	var picks := {}
	var grid := GridContainer.new()
	grid.columns = 3
	modal_box.add_child(grid)
	for nm in _room_people_names():
		var c := CheckBox.new()
		c.text = nm
		c.add_theme_font_override("font", Paper.font())
		c.add_theme_color_override("font_color", Paper.INK)
		c.toggled.connect(func(on): picks[nm] = on)
		grid.add_child(c)
	var go := func():
		var who := []
		for k in picks.keys():
			if picks[k]:
				who.append(k)
		if who.size() < 1:
			return
		_close_modal()
		send(cmd + " " + " ".join(who))
	modal_box.add_child(Paper.button("就请这几位", go, 17))
	modal_box.add_child(Paper.button("返回（Esc）", _close_modal, 16))

func _card_builder(done: Callable) -> void:
	_open_modal("拼一条规矩：主语、条件、行为、例外、记账")
	var cells: Array = state.cells
	var names := ["谁来做", "什么时候", "做什么", "什么情况不算", "怎么留记录"]
	var boxes := []
	for i in 5:
		var row := HBoxContainer.new()
		var l := Label.new()
		l.text = names[i]
		l.custom_minimum_size = Vector2(130, 0)
		l.add_theme_font_override("font", Paper.font())
		l.add_theme_color_override("font_color", Paper.INK)
		row.add_child(l)
		var ob := OptionButton.new()
		ob.add_theme_font_override("font", Paper.font())
		ob.custom_minimum_size = Vector2(380, 0)
		for o in cells[i]:
			ob.add_item(String(o))
		if i == 0:
			for n in _people_names():
				ob.add_item(String(n))
		row.add_child(ob)
		modal_box.add_child(row)
		boxes.append(ob)
	var go := func():
		var parts := []
		for i in 5:
			var ob: OptionButton = boxes[i]
			var idx := ob.selected
			if i == 0 and idx >= (cells[0] as Array).size():
				parts.append(ob.get_item_text(idx))
			else:
				parts.append(str(idx + 1))
		_close_modal()
		done.call(" ".join(parts))
	modal_box.add_child(Paper.button("就这么说", go, 17))
	modal_box.add_child(Paper.button("返回（Esc）", _close_modal, 16))

func _open_night() -> void:
	var here := String(state.room.place)
	if here == "":
		return
	_menu("夜里", [["在这里守夜", _act("守夜 " + _place_name(here))]], "守夜只看得见身影、听得见动静；第二天清晨可以读痕迹、做重演。")

func _open_confront(reopen: bool) -> void:
	Sound.music("confrontation")
	_open_modal("中央厅：公开对质" if not reopen else "重开对质")
	modal_box.add_child(Paper.label("每句陈述配一件证物（不配就不说）。证物要你手里有：去细看、去问人。", 15, Paper.MUTED))
	var ev_names := ["（不说这句）"]
	var ev_index := [0]
	for i in state.evidence.size():
		var e: Dictionary = state.evidence[i]
		if bool(e.have):
			ev_names.append(String(e.name))
			ev_index.append(i + 1)
	var picks := []
	var scroll := ScrollContainer.new()
	scroll.custom_minimum_size = Vector2(760, 300)
	var col := VBoxContainer.new()
	col.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.add_child(col)
	modal_box.add_child(scroll)
	for c in state.claims.size():
		var row := VBoxContainer.new()
		row.add_child(Paper.label("%d. %s" % [c + 1, String(state.claims[c])], 15))
		var ob := OptionButton.new()
		ob.add_theme_font_override("font", Paper.font())
		for n in ev_names:
			ob.add_item(n)
		row.add_child(ob)
		col.add_child(row)
		picks.append(ob)
	var common := CheckBox.new()
	common.text = "提议共同规则：公开指控须附可检查的材料"
	common.add_theme_font_override("font", Paper.font())
	common.add_theme_color_override("font_color", Paper.INK)
	modal_box.add_child(common)
	var proposal := {"cells": pending_proposal}
	var prop_label := Paper.label("（不当众提出新规矩）" if pending_proposal == "" else "将当众提出这条规矩（五格：%s）" % pending_proposal, 15, Paper.MUTED if pending_proposal == "" else Paper.SEAL)
	modal_box.add_child(prop_label)
	var finish := func():
		var lines := ["重开对质" if reopen else "对质"]
		for c in picks.size():
			var ob: OptionButton = picks[c]
			if ob.selected > 0:
				lines.append("%d %d" % [c + 1, ev_index[ob.selected]])
		if common.button_pressed:
			lines.append("共同")
		if proposal.cells != "":
			lines.append("提规 " + proposal.cells)
		lines.append("完")
		pending_proposal = ""
		_close_modal()
		send_many(lines)
	var row2 := HBoxContainer.new()
	var propose := func():
		_card_builder(func(cells): _confront_proposal(reopen, cells))
	row2.add_child(Paper.button("当众提出一条新规矩……", propose, 16))
	row2.add_child(Paper.button("开始对质", finish, 17))
	if reopen:
		row2.add_child(Paper.button("返回（Esc）", _close_modal, 16))
	modal_box.add_child(row2)

func _confront_proposal(reopen: bool, cells: String) -> void:
	pending_proposal = cells
	_open_confront(reopen)

func _open_council() -> void:
	Sound.music("confrontation")
	var items := []
	var plans: Array = state.get("plans", [])
	for i in plans.size():
		items.append(["支持：" + String(plans[i]), _act("支持 %d" % (i + 1))])
	items.append(["不表态", _act("不表态")])
	_menu("第7日 · 誓约庭：名录库之议", items, "十六人决定名录库归谁、怎么开。")

# ------------------------------------------------------------------ 界面 --

func _build_ui() -> void:
	chapter_ui = ChapterUI.new()
	chapter_ui.name = "ChapterUI"
	add_child(chapter_ui)
	chapter_ui.build_hud($FieldUI)
	ui = chapter_ui.ui
	hud_title = chapter_ui.hud_title
	hud_hint = chapter_ui.hud_hint
	hud_keys = chapter_ui.hud_keys
	hover_tip = chapter_ui.hover_tip
	aside = chapter_ui.aside
	modal_layer = chapter_ui.modal_layer
	busy_label = chapter_ui.busy_label
	stage_node = ReplayStage.new()
	stage_node.name = "ReplayStage"
	stage_node.host = self
	stage_node.z_index = 60
	add_child(stage_node)
	var hints := HintOverlay.new()
	hints.source = self
	hints.z_index = 50
	add_child(hints)
	_refresh_hud()


func _log(t: String) -> void:
	internal_log.append(t)

func _refresh_hud() -> void:
	if state.is_empty():
		return
	var here := String(MAP_PLACE.get(String(map.name), ""))
	hud_title.text = "学园 · " + (_place_name(here) if here != "" else String(map.get_meta("title", map.name)))
	hud_hint.text = "第%d日 · %s" % [int(state.day), String(state.slot_name)]
	hud_keys.visible = field_active()
	if hud_keys.visible:
		hud_keys.text = chapter_ui.key_hint(nearest != "", true, not state.get("replay_questions", []).is_empty(), true)

func _open_modal(title: String) -> void:
	modal_box = open_modal(title, Vector2(960, 720), "menu")

func _close_modal() -> void:
	var continue_story := modal_kind in ["card", "notebook", "story_image"] and (not output_pending.is_empty() or String(state.get("mode", "main")) == "story")
	close_modal()
	if continue_story:
		_present_output()

func _show_info(lines: PackedStringArray) -> void:
	var size := Vector2(900, 660) if card_texture else Vector2(1000, 780)
	modal_box = open_modal(info_title, size, "card" if card_texture else "notebook")
	var text: VBoxContainer = chapter_ui.card_body(modal_box, card_texture) if card_texture else modal_box
	for line in lines:
		text.add_child(Paper.label(_escape_text(line), 20 if card_texture else 19))
	add_footer([["返回现场", _close_modal]])

func _flash_title(text: String) -> void:
	var l := Paper.label("[center]" + text + "[/center]", 30, Color(0.97, 0.93, 0.84))
	l.set_anchors_preset(Control.PRESET_CENTER)
	l.custom_minimum_size = Vector2(600, 0)
	l.position = get_viewport().get_visible_rect().size / 2 - Vector2(300, 60)
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	ui.get_child(0).add_child(l)
	var tw := create_tween()
	tw.tween_property(l, "modulate:a", 0.0, 1.8).set_delay(0.8)
	tw.tween_callback(l.queue_free)

func _show_intro() -> void:
	_present_output()

func _show_ending() -> void:
	Sound.music("main_theme", 4.0)
	_open_modal("第二章 · 终")
	modal_box.add_child(Paper.label(_escape_text("\n".join(visible_output).right(2400)), 19))
	add_footer([["回到标题", func(): get_tree().change_scene_to_file("res://scenes/Title/Title.tscn")]])


func open_modal(title: String, size: Vector2, kind: String) -> VBoxContainer:
	close_modal()
	ui_lock = true
	modal_kind = kind
	_cancel_click_navigation()
	var body: VBoxContainer = chapter_ui.open_modal(title, size, kind)
	modal_root = chapter_ui.modal_root
	return body

func add_footer(actions: Array) -> HBoxContainer:
	return chapter_ui.add_footer(actions)

func close_modal() -> void:
	chapter_ui.close_modal()
	modal_root = null
	manuscript = null
	modal_kind = ""
	ui_lock = false

func _escape_text(text: String) -> String:
	return text.replace("[", "[lb]")

func _present_output() -> void:
	# Every spoken word comes from the same ConsoleUI as command-line play.
	if not output_pending.is_empty() and output_pending[0].begins_with("\u001estory-media:"):
		var sequence := output_pending[0].trim_prefix("\u001estory-media:").to_int()
		output_pending.remove_at(0)
		_show_story_media(sequence)
		return
	if not output_pending.is_empty():
		var script_text := "~ shared_output\n"
		while not output_pending.is_empty() and not output_pending[0].begins_with("\u001estory-media:"):
			var paragraph := output_pending[0]
			output_pending.remove_at(0)
			for line in paragraph.split("\n"):
				if line.strip_edges().is_empty():
					continue
				# Paginate long lines without changing their words.
				var speaker := "侦探（心声）"
				for who in _people_names() + ["侦探", "铜口", "教师"]:
					if line.strip_edges().begins_with(String(who) + "："):
						speaker = who
				for start in range(0, line.length(), 68):
					script_text += speaker + ": " + _escape_text(line.substr(start, 68)) + "\n"
		script_text += "=> END"
		var resource: Resource = get_node("/root/DialogueManager").create_resource_from_text(script_text)
		dialogue.panel.show()
		dialogue.start(resource, "shared_output", self)
	else:
		_dialogue_finished()

func _localized_speaker(canonical: String) -> String:
	return canonical

func _show_story_media(sequence: int) -> void:
	var matches := story_media.filter(func(item: Dictionary): return int(item.sequence) == sequence)
	var presentation: Dictionary = matches[0]
	story_media.erase(presentation)
	var content := String(presentation.content)
	match String(presentation.kind):
		"Image":
			var body := open_modal("", Vector2(1080, 800), "story_image")
			var picture := TextureRect.new()
			picture.texture = load(content)
			picture.custom_minimum_size = Vector2(1000, 640)
			picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
			picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
			body.add_child(picture)
			add_footer([["继续", func(): close_modal(); _present_output()]])
			return
		"Audio":
			var audio := AudioStreamPlayer.new()
			audio.stream = load(content)
			add_child(audio)
			audio.finished.connect(audio.queue_free)
			audio.play()
		"Animation":
			var actor: CharacterBody2D = player if String(presentation.actor) == "Det" else null
			for person in state.people:
				if String(person.id) == String(presentation.actor):
					actor = player if String(person.id) == "Det" else actors.get(String(person.name))
			if actor != null:
				if actor.standard.has_animation(content):
					actor.sprite.play(content)
				else:
					actor.pose(content)
	_present_output()

func _dialogue_finished() -> void:
	closed_ms = Time.get_ticks_msec()
	dialogue.panel.hide()
	intro_done = true
	if not output_pending.is_empty():
		_present_output()
		return
	if not pending_inspection.is_empty() and story_media.is_empty() and String(state.get("mode", "main")) == "main":
		var record := "inspection:" + String(pending_inspection.place)
		pending_inspection = {}
		open_record_card(record)
		return
	if ended:
		_show_ending()
		return
	var mode := String(state.get("mode", "main"))
	if mode == "story":
		var choice: Dictionary = state.story_choice
		var items := []
		for option in choice.options:
			items.append([String(option.text), _act(String(option.id))])
		_menu(String(choice.prompt), items)
	elif mode == "council":
		_open_council()
	elif mode == "card":
		var cell: int = state.card_cell
		var options: Array = state.cells[cell].duplicate()
		if cell == 0:
			options.append_array(_people_names())
		_pick(["谁来做", "什么时候", "做什么", "什么情况不算", "怎么留记录"][cell], options, func(value): send(String(value)))
	elif mode == "yesno":
		_menu("就这么说吗？", [["是", _act("是")], ["否", _act("否")]])
	elif mode == "confront":
		_open_confront(false)
	elif int(state.get("day", 2)) == 4 and int(state.get("slot", 0)) == 0:
		_open_confront(false)

func field_active() -> bool:
	return not busy and not ui_lock and not ended and not (is_instance_valid(dialogue) and (dialogue.active or dialogue.history_panel.visible)) and not stage_node.playing and not action_playing

func clicked_site(pos: Vector2) -> String:
	return FieldNavigation.clicked_site(pos, sites, actors)

func site_point(id: String) -> Vector2:
	return FieldNavigation.site_point(id, sites, actors)

func site_marker(id: String) -> Vector2:
	return map.to_global(site_point(id))

func site_box(id: String) -> Rect2:
	var point := site_marker(id)
	return Rect2(point + Vector2(-24, -52), Vector2(48, 62))

func site_visible(id: String) -> bool:
	return sites.has(id)

func site_is_new(id: String) -> bool:
	return not examined.has(String(map.name) + ":" + id)

func complete_click_interaction(id: String) -> void:
	var site: Dictionary = sites[id]
	examined[String(map.name) + ":" + id] = true
	if site.has("person"):
		_open_person(site.person)
	elif site.has("rest"):
		_act(String(site.rest)).call()
	elif site.has("thing"):
		_inspect(String(site.thing))
	else:
		_open_place()

func _inspect(thing: String) -> void:
	_close_modal()
	pending_inspection = {"place": String(state.room.place), "thing": thing}
	send("查 " + thing)

func _build_sites() -> void:
	sites.clear()
	for who in actors:
		sites["person:" + who] = {"title": who, "point": actors[who].position, "person": who}
	var aliases := {"传声中继": "中央基座", "中央公告板": "中央厅公告", "配给台公告板": "配给台公告", "门闩": "粮仓", "存粮": "粮仓", "试剂柜": "药柜", "库门锁芯": "名录库之门", "唤名警告": "名录库之门", "生活翼公告板": "生活翼公告", "课表": "讲堂公告", "誓约庭": "铺石"}
	var markers := map.get_node_or_null("Sites")
	if markers and String(MAP_PLACE.get(String(map.name), "")) == String(state.room.place):
		for marker in markers.get_children():
			var title: String = marker.get_meta("label", marker.name)
			var thing: String = aliases.get(title, title)
			if title == "公告框":
				thing = {"zone_jia": "文契公告", "zone_bing": "星仪公告", "zone_ding": "后勤公告", "zone_yi": "生活翼公告"}.get(String(map.name), "生活翼公告")
			var site := {"title": title, "point": marker.position}
			for t in state.room.things:
				if t.key == thing:
					site["thing"] = thing
			_cache_site_texture(String(marker.name), thing)
			sites[String(marker.name)] = site
	_register_rest_sites()
	_refresh_hud()

func _update_targets() -> void:
	nearest = ""
	hovered = ""
	if not field_active():
		hover_tip.hide()
		return
	var best := 70.0
	for id in sites:
		var distance := player.position.distance_to(site_point(id))
		if distance < best:
			best = distance
			nearest = id
	hovered = clicked_site(map.get_local_mouse_position())
	hover_tip.visible = hovered != ""
	if hovered != "":
		hover_tip.text = sites[hovered].title
		var at := get_viewport().get_mouse_position() + Vector2(16, -34)
		hover_tip.position = Vector2(clampf(at.x, 8, 1272 - hover_tip.size.x), clampf(at.y, 8, 962))

func open_notebook(kind := "") -> void:
	var body := open_modal("笔记本", Vector2(1000, 780), "notebook")
	var groups := {}
	for record in state.get("notebook_cards", []):
		if kind != "" and String(record.kind) != kind:
			continue
		var group := String(record.place) + " · " + String(record.kind)
		if not groups.has(group):
			groups[group] = []
		var id := String(record.id)
		groups[group].append([String(record.name), open_record_card.bind(id)])
	if groups.is_empty():
		body.add_child(Paper.label("还没有记下任何东西。", 19, Paper.MUTED))
	for group in groups:
		chapter_ui.notebook_section(body, String(group), groups[group])
	add_footer([["合上（Tab）", close_modal]])

func open_record_card(id: String) -> void:
	var cards: Array = state.get("notebook_cards", [])
	for record in cards:
		if String(record.id) != id:
			continue
		var texture: Texture2D = record_textures.get(String(record.image_key))
		if texture == null and SPRITES.has(String(record.image_key)):
			var frames: SpriteFrames = load(FRAMES_DIR % String(SPRITES[String(record.image_key)]))
			texture = frames.get_frame_texture("idle_front", 0)
			record_textures[String(record.image_key)] = texture
		var body := open_modal(String(record.name), Vector2(900, 660), "card")
		var text: VBoxContainer = chapter_ui.card_body(body, texture)
		for fact in record.facts:
			text.add_child(Paper.label(_escape_text(String(fact)), 20))
		for source in record.sources:
			text.add_child(Paper.label("来源：" + _escape_text(String(source)), 16, Paper.MUTED))
		add_footer([["笔记本", open_notebook], ["返回现场", close_modal]])
		return

func _cache_site_texture(site_id: String, thing: String) -> void:
	var object_names := {"relay": "RelayColumn", "mouthpiece": "Mouthpiece", "central_notice": "NoticeW", "reagent": "ReagentCabinet", "empty_bowl": "Bed1", "lock_core": "VaultDoor", "name_warning": "Warning", "ration_counter": "RationCounter", "ledger": "Ledger", "ration_notice": "RationNotice", "valve": "Valve", "blackboard": "Blackboard", "timetable": "Timetable", "hall_notice": "NoticeBoard", "fireplace": "Fireplace", "transmitter": "Transmitter", "zone_rules": "ZoneRules", "stele": "契约石碑"}
	if not object_names.has(site_id):
		return
	var object := map.get_node_or_null("Objects/" + String(object_names[site_id])) as Sprite2D
	if object != null and not thing.is_empty():
		record_textures[thing] = object.texture

func _register_rest_sites() -> void:
	if String(MAP_PLACE.get(String(map.name), "")) != String(state.room.place) or String(state.room.place) == "":
		return
	var night := int(state.slot) == 5
	var command := "睡" if night else "歇"
	var available := false
	for action in state.global_actions:
		if String(action.command) == command:
			available = true
	if not available:
		return
	for object in map.get_node("Objects").get_children():
		if not object is Sprite2D:
			continue
		var texture_path: String = object.texture.resource_path.get_file()
		var bed := texture_path.begins_with("bed_") or texture_path == "ward_bed.png"
		var chair := texture_path.begins_with("chair_") or texture_path.contains("bench") or texture_path.contains("armchair")
		if (night and bed) or (not night and chair):
			sites["rest:" + String(object.name)] = {"title": "睡下" if night else "坐下歇一会儿", "point": object.position, "rest": command}

func open_manuscript() -> void:
	var questions: Array = state.get("replay_questions", [])
	if questions.is_empty():
		var guidance := open_modal("昨夜重演", Vector2(1000, 780), "replay")
		guidance.add_child(Paper.label("先走到现场细看，再把留下的痕迹与听来的话对照。", 19, Paper.MUTED))
		var traces := []
		for record in state.get("notebook_cards", []):
			if String(record.kind) == "物证":
				var id := String(record.id)
				traces.append([String(record.name), open_record_card.bind(id)])
		chapter_ui.notebook_section(guidance, "已见痕迹", traces)
		var unseen: Array = state.get("replay_unseen", [])
		if not unseen.is_empty():
			guidance.add_child(Paper.label("待查：" + "、".join(unseen), 18, Paper.MUTED))
		add_footer([["返回现场", close_modal]])
		return
	var context: Array = [state.replay_night, questions]
	if context != replay_context:
		replay_context = context.duplicate(true)
		replay_vars = []
		for question in questions:
			replay_vars.append(0)
		last_run_vars = []
		replay_result.clear()
		replay_evidence.clear()
	replay_variables = questions
	var body := open_modal("", Vector2(1180, 860), "replay")
	manuscript = ReplayManuscript.new()
	manuscript.custom_minimum_size = Vector2(1108, 770)
	manuscript.host = self
	body.add_child(manuscript)

func replay_ui_data() -> Dictionary:
	return {"variables": replay_variables, "count": replay_variables.size(), "marks": {}, "fresh": [],
		"full_match_text": "已查痕迹吻合。", "ready_hint": "设定一至三项假设。", "show_empty_finding": false}

func replay_ui_evidence() -> Dictionary:
	return replay_evidence

func replay_ui_ready(selection: Array) -> bool:
	var count := 0
	for index in selection.size():
		if int(replay_variables[index].choices[int(selection[index])]) != 0:
			count += 1
	return count > 0 and count <= 3

func replay_ui_opened() -> void:
	pass

func replay_ui_run(selection: Array, skip: bool) -> void:
	run_replay(selection, skip)

func run_replay(selection: Array, skip: bool) -> void:
	if not replay_ui_ready(selection):
		return
	var picks := []
	for index in selection.size():
		var choice: int = replay_variables[index].choices[int(selection[index])]
		if choice != 0:
			picks.append(str(choice))
	skip_replay = skip
	last_run_vars = selection.duplicate()
	close_modal()
	awaiting_replay = true
	send("重演 第%d夜 %s" % [int(state.replay_night), " ".join(picks)], true)

func accept_replay(result: Dictionary) -> void:
	replay_result = {"full_match": result.confirmed, "findings": []}
	replay_evidence.clear()
	for i in result.lines.size():
		var text: String = result.lines[i]
		var status := "match" if text.begins_with("吻合") else "conflict" if text.begins_with("冲突") else "unvisited"
		var id := str(i)
		var parts := text.substr(text.find("：") + 1).split("——", true, 1)
		replay_evidence[id] = {"name": parts[0], "text": text}
		replay_result.findings.append({"id": id, "status": status, "text": parts[1] if parts.size() > 1 else "", "place": parts[0]})
	if not skip_replay and not result.ink.is_empty():
		await stage_node.play(result.frames)
	open_manuscript()

func open_card(id: String) -> void:
	var body := open_modal("现场对照", Vector2(900, 660), "card")
	body.add_child(Paper.label(_escape_text(replay_evidence[id].text), 20))
	add_footer([["重演手稿", open_manuscript], ["返回现场", close_modal]])

func actor_of(_key: String) -> CharacterBody2D:
	return player

func _play_actions() -> void:
	if frames_revision != int(state.frames_revision):
		frames_revision = int(state.frames_revision)
		frames_consumed = 0
	var frames: Array = state.frames.slice(frames_consumed)
	frames_consumed = state.frames.size()
	if frames.is_empty():
		return
	action_playing = true
	busy = true
	busy_label.text = "人物正在行动…"
	busy_label.show()
	var restore := String(map.name)
	var position := player.position
	var camera_position := camera.position
	player.modulate.a = 0.0
	var groups: Array = []
	for frame in frames:
		if groups.is_empty() or String(groups[-1][0].place) != String(frame.place):
			groups.append([])
		groups[-1].append(frame)
	for group in groups:
		var frame: Dictionary = group[0]
		var place := String(frame.place)
		if not PLACE_MAP.has(place):
			continue
		var target: Array = PLACE_MAP[place]
		if String(map.name) != String(target[0]):
			load_map(target[0], target[1], Vector2.ZERO, false, false)
		for actor in actors.values():
			actor.hide()
		await _play_room_actions(group, target)
	aside.hide()
	load_map(restore, "", position, true, false)
	player.modulate.a = 1.0
	camera.position = camera_position
	busy = false
	action_playing = false
	busy_label.hide()

func _play_room_actions(frames: Array, target: Array) -> void:
	var by_actor := {}
	for frame in frames:
		var key := String(frame.text) if bool(frame.anonymous) else String(frame.actor)
		if not by_actor.has(key):
			by_actor[key] = []
		by_actor[key].append(frame)
	var spawn: Marker2D = map.get_node("Spawns/" + String(target[1]))
	var stands := map.get_node_or_null("Stands")
	var started := Time.get_ticks_msec()
	var first_time := float(frames[0].time)
	action_jobs = by_actor.size()
	var index := 0
	for actor_frames in by_actor.values():
		var frame: Dictionary = actor_frames[0]
		var figure := CharacterBody2D.new()
		figure.set_script(ACTOR)
		var frames_path := String(frame.frames_path)
		if frames_path.is_empty():
			var identity := "detective" if bool(frame.anonymous) else String(SPRITES[String(frame.actor)])
			frames_path = FRAMES_DIR % identity
		figure.standard = load(frames_path)
		map.get_node("Objects").add_child(figure)
		figure.collision_layer = 0
		figure.collision_mask = 0
		figure.speed = 180.0
		if bool(frame.anonymous):
			figure.modulate = Color(0, 0, 0, 0.74)
		var stand: Vector2 = stands.get_child(index % stands.get_child_count()).position if stands and stands.get_child_count() > 0 else spawn.position
		index += 1
		figure.position = stand
		_play_actor_actions(figure, actor_frames, spawn.position, stand, first_time, started)
	if action_jobs > 0:
		await action_group_finished

func _play_actor_actions(figure: CharacterBody2D, frames: Array, spawn: Vector2, stand: Vector2, first_time: float, started: int) -> void:
	for frame in frames:
		var delay := (float(frame.time) - first_time) * 0.35 - (Time.get_ticks_msec() - started) / 1000.0
		if delay > 0:
			await get_tree().create_timer(delay).timeout
		aside.text = String(frame.text)
		aside.show()
		if String(frame.frame) == "行走":
			var leaving := String(frame.text).contains("离开")
			figure.position = stand if leaving else spawn
			await figure.follow_path(_route(figure.position, spawn if leaving else stand))
		else:
			figure.pose(String(frame.animation))
			await get_tree().create_timer(0.35).timeout
	figure.queue_free()
	action_jobs -= 1
	if action_jobs == 0:
		action_group_finished.emit()

func ink_route(from: Vector2, to: Vector2) -> PackedVector2Array:
	return _route(from, to)
