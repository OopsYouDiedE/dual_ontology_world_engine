extends "res://scenes/Chapter2/Field/Chapter2Field.gd"
## 第二章游戏宿主：地图行走 + 原型系统（Chapter2Bridge 在后台线程跑原型的文字循环）。
## 每个时段做一件事：走到人跟前按 E 与他打交道，走到物件或公告前按 E 细看；Q 打开行动菜单；
## J 展开记录；Tab 看笔记；Enter 直接输入原型命令（与文字原型相同）。时段过去后，众人按推演换到各自的去处。

const Paper := preload("res://scenes/Awakening/Paper.gd")
const BridgeScript := preload("res://scenes/Chapter2/Game/Chapter2Bridge.cs")
const FRAMES_DIR := "res://ShareAsset/Internal/Characters/animation_v2/%s.tres"

const PLACE_MAP := {
	"Hall": ["central_hall", "south"], "Lecture": ["lecture", "from_central"], "Logistics": ["logistics", "west"],
	"Clinic": ["clinic", "south"], "Greenhouse": ["greenhouse", "south"], "ArchiveFront": ["archive", "south"],
	"Oath": ["campus_s", "gate_inside"], "DormLife": ["living_hall", "north_door"], "DormW": ["zone_jia", "from_hall"],
	"DormS": ["zone_bing", "from_hall"], "DormL": ["zone_ding", "from_hall"],
}
const MAP_PLACE := {
	"central_hall": "Hall", "lecture": "Lecture", "logistics": "Logistics", "granary": "Logistics", "clinic": "Clinic",
	"greenhouse": "Greenhouse", "archive": "ArchiveFront", "campus_s": "Oath", "living_hall": "DormLife",
	"zone_jia": "DormW", "zone_bing": "DormS", "zone_ding": "DormL",
}
const SPRITES := {
	"Det": "detective", "Huo": "engineer", "Shen": "chef", "Su": "musician", "Lucian": "poet", "Gu": "archaeologist",
	"Quer": "thief", "Ward": "lawyer", "Selma": "naturalist", "Ruan": "chessplayer", "Elena": "dancer", "Moran": "painter",
	"Veronica": "chemist", "Bertrand": "marksman", "Margaret": "architect", "Mora": "doctor",
}
const JOBS := ["开粮", "煮粥", "照看病人", "修门闩", "修温室", "上课", "领粮", "补记台账", "挑水", "来中央厅", "清点", "夜巡", "守名录库"]
const SLOT_TINT := [Color(1.0, 0.96, 0.9), Color(1, 1, 1), Color(1, 1, 0.97), Color(1.0, 0.95, 0.86), Color(0.96, 0.8, 0.68), Color(0.42, 0.47, 0.66)]
const INFO := {"状态": "状态", "笔记": "笔记", "规矩": "规矩", "传播": "传播", "清晨痕迹": "清晨", "重演": "重演", "消息": "消息",
	"公告": "公告", "同行": "同行", "证物": "证物", "物件": "物件", "帮助": "帮助"}

var bridge: Node
var state := {}
var busy := true
var ui_lock := false
var info_capture := false
var info_lines: PackedStringArray = []
var actors := {}
var last_slot_key := ""
var ended := false
var intro_done := false
var pending_proposal := ""

var ui: CanvasLayer
var tint: CanvasModulate
var top_label: RichTextLabel
var log_panel: PanelContainer
var log_text: RichTextLabel
var log_expanded := false
var modal: PanelContainer
var modal_box: VBoxContainer
var input_line: LineEdit
var busy_label: Label
var fade: ColorRect

func _ready() -> void:
	start_map = "central_hall"
	start_spawn = "south"
	super()
	place_label.visible = false
	tint = CanvasModulate.new()
	add_child(tint)
	_build_ui()
	bridge = BridgeScript.new()
	bridge.name = "Bridge"
	add_child(bridge)
	bridge.Start()
	_show_intro()

# ------------------------------------------------------------------ 宿主循环 --

func _process(_delta: float) -> void:
	if bridge == null:
		return
	var waiting: bool = bridge.IsWaiting()
	var out: Array = bridge.TakeOutput()
	for line in out:
		_on_output(String(line))
	if busy and waiting:
		busy = false
		busy_label.visible = false
		_on_ready()
	elif bridge.IsDone() and not ended:
		ended = true
		busy = false
		busy_label.visible = false
		_show_ending()

func _on_output(line: String) -> void:
	if line.strip_edges() == "" or line.begins_with("【第") and line.contains("】你在"):
		return
	if info_capture:
		info_lines.append(line)
		return
	_log(line)

func send(line: String, info := false) -> void:
	if busy:
		return
	busy = true
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
	busy_label.visible = true
	info_capture = false
	for l in lines:
		_log("[color=#8a3324]▸ " + String(l) + "[/color]")
		bridge.Send(String(l))

func _on_ready() -> void:
	if info_capture:
		info_capture = false
		_show_info(info_lines)
	state = bridge.Snapshot()
	if state.is_empty():
		return
	var slot_key := "%d-%d" % [state.day, state.slot]
	var changed := slot_key != last_slot_key
	last_slot_key = slot_key
	_refresh_hud()
	tint.color = SLOT_TINT[int(state.slot)]
	if changed:
		var place := String(state.det_place)
		if MAP_PLACE.get(String(map.name), "") != place and PLACE_MAP.has(place):
			var target: Array = PLACE_MAP[place]
			load_map(target[0], target[1])
		else:
			_spawn_actors()
		_flash_title("第%d日 · %s" % [state.day, state.slot_name])
	var mode := String(state.mode)
	if mode in ["card", "yesno", "confront"]:
		_log("（原型在等你作答：在下方输入框里输入）")
		input_line.grab_focus()
	elif mode == "council":
		_open_council()
	elif int(state.day) == 4 and int(state.slot) == 0 and mode == "main" and intro_done:
		_open_confront(false)
	elif int(state.slot) == 5 and mode == "main" and intro_done:
		_open_night()

# ------------------------------------------------------------------ 地图与人物 --

func load_map(key: String, spawn: String, at := Vector2.ZERO, use_at := false) -> void:
	actors.clear()
	super(key, spawn, at, use_at)
	_spawn_actors()

func _spawn_actors() -> void:
	if map == null:
		return
	for a in actors.values():
		if is_instance_valid(a):
			a.queue_free()
	actors.clear()
	if state.is_empty():
		return
	var here := String(MAP_PLACE.get(String(map.name), ""))
	if here == "":
		return
	var stands: Array = []
	var stand_node := map.get_node_or_null("Stands")
	if stand_node:
		for m in stand_node.get_children():
			stands.append((m as Marker2D).position)
	if String(map.name) == "granary":
		return
	var i := 0
	for p in state.people:
		if String(p.id) == "Det" or String(p.place) != here:
			continue
		if int(state.slot) == 5 and bool(p.asleep):
			continue
		var pos: Vector2 = stands[i % max(1, stands.size())] if not stands.is_empty() else Vector2(96 + 40 * i, 160)
		if stands.size() > 0 and i >= stands.size():
			pos += Vector2(18, 10)
		i += 1
		var actor := CharacterBody2D.new()
		actor.set_script(ACTOR)
		var path := FRAMES_DIR % SPRITES.get(String(p.id), "detective")
		actor.standard = load(path) if ResourceLoader.exists(path) else FRAMES
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
		actor.face_point(player.position)
		actors[String(p.name)] = actor

func _physics_process(delta: float) -> void:
	if ui_lock or busy and modal.visible:
		player.velocity = Vector2.ZERO
		player.pose("idle")
		return
	super(delta)

func _unhandled_input(event: InputEvent) -> void:
	if ended or not (event is InputEventKey) or not event.pressed or event.echo:
		return
	var k := (event as InputEventKey).keycode
	if modal.visible:
		if k == KEY_ESCAPE:
			_close_modal()
		return
	if input_line.has_focus():
		return
	if busy:
		return
	match k:
		KEY_E, KEY_SPACE:
			_interact()
		KEY_Q:
			_open_actions()
		KEY_TAB:
			send("笔记", true)
		KEY_J:
			log_expanded = not log_expanded
			_layout_log()
		KEY_ENTER, KEY_SLASH:
			input_line.grab_focus()
		_:
			return
	get_viewport().set_input_as_handled()

func _interact() -> void:
	var best: Node2D = null
	var best_d := 64.0
	for a in actors.values():
		var d: float = (a as Node2D).position.distance_to(player.position)
		if d < best_d:
			best_d = d
			best = a
	if best:
		_open_person(String(best.get_meta("person")))
	else:
		_open_place()

# ------------------------------------------------------------------ 菜单 --

func _menu(title: String, items: Array, note := "") -> void:
	## items: [[文字, Callable], ...]
	_open_modal(title)
	if note != "":
		modal_box.add_child(Paper.label(note, 16, Paper.MUTED))
	var scroll := ScrollContainer.new()
	scroll.custom_minimum_size = Vector2(560, min(420, 46 * items.size() + 8))
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	var col := VBoxContainer.new()
	col.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.add_child(col)
	modal_box.add_child(scroll)
	for it in items:
		var cb: Callable = it[1]
		var b := Paper.button(String(it[0]), func(): cb.call(), 17)
		b.alignment = HORIZONTAL_ALIGNMENT_LEFT
		col.add_child(b)
	modal_box.add_child(Paper.button("返回（Esc）", _close_modal, 16))

func _act(line: String) -> Callable:
	return func():
		_close_modal()
		send(line)

func _info(line: String) -> Callable:
	return func():
		_close_modal()
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

func _open_person(who: String) -> void:
	var items := [
		["问%s知道什么" % who, _act("问 " + who)],
		["请%s做一件事……" % who, func(): _pick("请%s做什么" % who, JOBS, func(j): send("请 %s %s" % [who, j]))],
		["请%s同去看……" % who, func(): _pick_thing("请%s一起去看什么" % who, func(t): send("同去 %s %s" % [who, t]))],
		["对%s讲一条规矩……" % who, func(): _pick_rule("对%s讲哪条规矩" % who, "讲 " + who)],
		["把一条消息转告%s……" % who, func(): _pick_news(who)],
		["帮%s干活，或兑现答应他的事" % who, _act("帮 " + who)],
		["把今天的口粮分给%s" % who, _act("分粮 " + who)],
		["请%s交出钥匙给……" % who, func(): _pick("交给谁", _people_names(), func(t): send("交钥匙 %s %s" % [who, t]))],
		["对%s只说一半实话，关于……" % who, func(): _pick("关于谁", _people_names(), func(t): send("片面告知 %s %s" % [who, t]))],
		["看你对%s的笔记" % who, _info("笔记 " + who)],
	]
	_menu(who, items, "每件事占一个时段（看笔记不占）。")

func _open_place() -> void:
	var here := String(MAP_PLACE.get(String(map.name), ""))
	var items := []
	if here != "" and state.has("things"):
		for t in state.things:
			if String(t.place) == here:
				items.append(["细看：" + String(t.key), _act("查 " + String(t.key))])
		if here in state.boards:
			items.append(["读这里的公告（不占时段）", _info("公告")])
			items.append(["在这里贴一条规矩……", func(): _pick_rule("贴哪条规矩", "贴 " + _place_name(here))])
		match here:
			"Lecture":
				items.append(["上课", _act("上课")])
				items.append(["上课，并在答疑时提问……", func(): _ask_demon()])
			"Logistics":
				items.append(["领今天的口粮", _act("领粮")])
			"Greenhouse":
				items.append(["请两个人一起修温室……", func(): _pick_crew("修温室：选两个人", 2, "修温室")])
			"ArchiveFront":
				items.append(["请人来试开名录库之门……", func(): _pick_crew("开库：选人（配重、锁芯、传声中继各一站）", 3, "开库")])
			"Hall":
				if int(state.day) == 2:
					items.append(["调停配给的争执", _act("调停")])
	if items.is_empty():
		items.append(["这里没有能细看的东西。按 Q 打开行动菜单。", _close_modal])
	_menu("此处：" + (_place_name(here) if here != "" else String(map.get_meta("title", map.name))), items)

func _open_actions() -> void:
	var items := []
	if int(state.get("slot", 0)) == 5:
		items.append(["睡下", _act("睡")])
		items.append(["守夜……", func(): _open_night()])
	else:
		items.append(["歇着（什么也不做，过一个时段）", _act("歇")])
	if bool(state.get("can_reopen", false)):
		items.append(["重开对质（援引共同规则）", func(): _open_confront(true)])
	if bool(state.get("can_apologize", false)):
		items.append(["当众认错", _act("认错")])
	items.append(["去往……（不占时段）", func(): _pick_place()])
	for k in INFO.keys():
		items.append(["查看：" + k, _info(INFO[k])])
	items.append(["输入命令（Enter）", func(): _close_modal(); input_line.grab_focus()])
	_menu("第%d日 · %s" % [int(state.get("day", 2)), String(state.get("slot_name", ""))], items, "E 与人或物打交道；Q 本菜单；Tab 笔记；J 记录；Enter 输入命令。")

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

func _pick_place() -> void:
	var items := []
	for place in PLACE_MAP.keys():
		var target: Array = PLACE_MAP[place]
		items.append([_place_name(place), func(): _close_modal(); load_map(target[0], target[1])])
	_menu("去往（走过去不占时段）", items)

func _pick_news(who: String) -> void:
	var opts: Array = state.get("news", [])
	if opts.is_empty():
		_menu("转告", [["你还没有听到什么消息。", _close_modal]])
		return
	_pick("把哪条消息转告%s" % who, opts, func(n): send("转告 %s %s" % [who, String(n).split(".")[0]]))

func _pick_rule(title: String, prefix: String) -> void:
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
	for nm in _people_names():
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
	var items := [["睡下", _act("睡")]]
	for place in PLACE_MAP.keys():
		items.append(["守夜：" + _place_name(place), _act("守夜 " + _place_name(place))])
	_menu("夜里", items, "守夜只看得见身影、听得见动静；第二天清晨可以读痕迹、做重演。")

func _open_confront(reopen: bool) -> void:
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
	var items := []
	var plans: Array = state.get("plans", [])
	for i in plans.size():
		items.append(["支持：" + String(plans[i]), _act("支持 %d" % (i + 1))])
	items.append(["不表态", _act("不表态")])
	_menu("第7日 · 誓约庭：名录库之议", items, "十六人决定名录库归谁、怎么开。")

# ------------------------------------------------------------------ 界面 --

func _build_ui() -> void:
	ui = CanvasLayer.new()
	ui.layer = 10
	add_child(ui)
	var root := Control.new()
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.theme = Paper.theme()
	ui.add_child(root)
	var top := PanelContainer.new()
	var tb := StyleBoxFlat.new()
	tb.bg_color = Color(0.1, 0.08, 0.07, 0.72)
	tb.set_content_margin_all(6)
	top.add_theme_stylebox_override("panel", tb)
	top.set_anchors_preset(Control.PRESET_TOP_WIDE)
	top.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(top)
	top_label = Paper.label("", 15, Color(0.95, 0.91, 0.82))
	top_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	top.add_child(top_label)
	log_panel = PanelContainer.new()
	log_panel.add_theme_stylebox_override("panel", Paper.sheet(10))
	root.add_child(log_panel)
	log_text = RichTextLabel.new()
	log_text.bbcode_enabled = true
	log_text.scroll_following = true
	log_text.add_theme_font_override("normal_font", Paper.font())
	log_text.add_theme_font_size_override("normal_font_size", 14)
	log_text.add_theme_color_override("default_color", Paper.INK)
	var lv := VBoxContainer.new()
	log_panel.add_child(lv)
	log_text.size_flags_vertical = Control.SIZE_EXPAND_FILL
	lv.add_child(log_text)
	input_line = LineEdit.new()
	input_line.placeholder_text = "Enter 输入命令（如：问 茉拉；讲 沃德 甲；帮助）"
	input_line.add_theme_font_override("font", Paper.font())
	input_line.add_theme_font_size_override("font_size", 14)
	input_line.text_submitted.connect(func(t):
		input_line.release_focus()
		input_line.text = ""
		if t.strip_edges() != "":
			var head: String = t.strip_edges().split(" ")[0]
			send(t.strip_edges(), head in INFO.values() or head in ["笔记", "清晨", "痕迹", "重演"]))
	lv.add_child(input_line)
	_layout_log()
	busy_label = Label.new()
	busy_label.text = "……"
	busy_label.add_theme_font_override("font", Paper.font())
	busy_label.add_theme_color_override("font_color", Color(0.95, 0.9, 0.8))
	busy_label.set_anchors_preset(Control.PRESET_TOP_RIGHT)
	busy_label.position = Vector2(-60, 30)
	root.add_child(busy_label)
	modal = PanelContainer.new()
	modal.add_theme_stylebox_override("panel", Paper.sheet(22))
	modal.visible = false
	root.add_child(modal)
	modal_box = VBoxContainer.new()
	modal_box.add_theme_constant_override("separation", 6)
	modal.add_child(modal_box)
	fade = ColorRect.new()
	fade.color = Color(0, 0, 0, 0)
	fade.set_anchors_preset(Control.PRESET_FULL_RECT)
	fade.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(fade)

func _layout_log() -> void:
	var vs := get_viewport().get_visible_rect().size
	if log_expanded:
		log_panel.position = Vector2(vs.x * 0.08, 40)
		log_panel.size = Vector2(vs.x * 0.84, vs.y - 60)
	else:
		log_panel.position = Vector2(8, vs.y - 178)
		log_panel.size = Vector2(vs.x - 16, 170)

func _log(t: String) -> void:
	log_text.append_text(t + "\n")

func _refresh_hud() -> void:
	if state.is_empty():
		return
	var here := String(MAP_PLACE.get(String(map.name), ""))
	top_label.text = "[b]第%d日 · %s[/b]　你在：%s　　[color=#c8bca0]E 交互　Q 行动　Tab 笔记　J 记录　Enter 命令[/color]" % [
		int(state.day), String(state.slot_name), _place_name(here) if here != "" else String(map.get_meta("title", map.name))]

func _open_modal(title: String) -> void:
	for c in modal_box.get_children():
		c.queue_free()
	modal_box.add_child(Paper.label("[b]" + title + "[/b]", 19))
	modal_box.add_child(Paper.rule())
	modal.visible = true
	ui_lock = true
	await get_tree().process_frame
	var vs := get_viewport().get_visible_rect().size
	modal.size = Vector2.ZERO
	modal.position = (vs - modal.get_combined_minimum_size()) / 2

func _close_modal() -> void:
	modal.visible = false
	ui_lock = false

func _show_info(lines: PackedStringArray) -> void:
	_open_modal("记录")
	var t := RichTextLabel.new()
	t.custom_minimum_size = Vector2(820, 440)
	t.add_theme_font_override("normal_font", Paper.font())
	t.add_theme_font_size_override("normal_font_size", 15)
	t.add_theme_color_override("default_color", Paper.INK)
	t.text = "\n".join(lines)
	modal_box.add_child(t)
	modal_box.add_child(Paper.button("关闭（Esc）", _close_modal, 16))

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
	_open_modal("第二章")
	modal_box.add_child(Paper.label(
		"第 1 日傍晚，四个寝区的隔离门都开了。十六个人在起居厅里第一次见面——人人都以为彼此是初识。\n\n" +
		"另外三组各自答过了关于昨夜的题，答案字面上都对，裂痕却留了下来：文契组为名录库的柜锁，星仪组为沉静室的门，后勤组为白塔柜里少的那瓶试剂。\n\n" +
		"第 2 日清晨，铜口召集全体到中央厅。\n\n" +
		"[color=#6b5d48]每日五个时段，侦探每个时段只能做一件事；其余十五人各按自己的认知行动。夜里发生的事，只以痕迹、身影与转述到达你。第 7 日，十六人在誓约庭决定名录库归谁、怎么开。[/color]", 17))
	var go := func():
		intro_done = true
		_close_modal()
		if not busy:
			_on_ready()
	modal_box.add_child(Paper.button("开始", go, 18))

func _show_ending() -> void:
	_open_modal("第二章 · 终")
	var t := RichTextLabel.new()
	t.custom_minimum_size = Vector2(820, 420)
	t.add_theme_font_override("normal_font", Paper.font())
	t.add_theme_font_size_override("normal_font_size", 15)
	t.add_theme_color_override("default_color", Paper.INK)
	t.bbcode_enabled = true
	t.text = log_text.get_parsed_text().substr(max(0, log_text.get_parsed_text().length() - 2400))
	modal_box.add_child(t)
	modal_box.add_child(Paper.button("回到标题", func(): get_tree().change_scene_to_file("res://scenes/Title/Title.tscn"), 17))
