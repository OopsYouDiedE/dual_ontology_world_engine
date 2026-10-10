extends SceneTree
## 界面视觉验收截图（有窗口，开发检查，不是盲玩）：res://scenes/UI/ 下每个场景单独成图。
## 组件与面板逐个实例化，填入引擎记录里的真实事件数据；现场类（抬头、对白、提示描边、视野遮罩）
## 取正式宿主 Main.tscn 里的实例在第一章现场上拍。不推进剧情、不派发 Core 行动。
## 记录缺少的数据（学园图、结局卡等）用本文件末尾的中性样例。编辑器预览页（Workbench）只在编辑器里可见，不在此截。
##   dotnet run --project godot_csharp/tools/PlayerTranscript   # 先生成引擎记录
##   godot --path godot_csharp --script res://tools/capture_ui_visual_round.gd -- --round=after-01 [--part=live|standalone|book] [--locale=en|ja]
## book 只拍标题页与设置册三页；locale 切换界面语言（并按语言换字）。
## 输出：artifacts/ui-graphic-design/<round>/<场景路径>.png 与 manifest.json。

const Fixture := preload("res://scenes/Awakening/check/chapter1_fixture.gd")
const Paper := preload("res://scenes/UI/Components/Paper.gd")
const SOURCE := "res://../artifacts/basic-ui-system/player-traces/engine-full-output.jsonl"
const OUT_ROOT := "res://../artifacts/ui-graphic-design/"
const UI := "res://scenes/UI/"
const VIEW := Vector2i(1280, 992)

var fx: Fixture
var host: Node
var out_dir := ""
var manifest: Array = []
var packets: Array = []
var failures := 0
var stage_root: Control

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	if DisplayServer.get_name() == "headless":
		push_error("需要正式渲染器，不能用 --headless。")
		quit(2)
		return
	var round_name := ""
	var part := "all"
	var locale := ""
	for arg in OS.get_cmdline_user_args():
		if str(arg).begins_with("--round="): round_name = str(arg).trim_prefix("--round=")
		if str(arg).begins_with("--part="): part = str(arg).trim_prefix("--part=")
		if str(arg).begins_with("--locale="): locale = str(arg).trim_prefix("--locale=")
	if not RegEx.create_from_string("^[A-Za-z0-9][A-Za-z0-9_-]*$").search(round_name):
		push_error("传入 --round=<名称>。")
		quit(2)
		return
	out_dir = ProjectSettings.globalize_path(OUT_ROOT + round_name)
	DirAccess.make_dir_recursive_absolute(out_dir)
	if not _read_packets():
		quit(2)
		return
	root.size = VIEW
	if locale != "":
		TranslationServer.set_locale(locale)
	# 自动加载在 --script 下缺席：用一份不入树的设置实例把外观（含按语言换字）套到主题上。
	preload("res://scenes/UI/Components/UIAppearance.gd").apply(load("res://scenes/Shared/Settings/GameSettings.gd").new())
	if part in ["all", "live"]: await _live_field()
	if part in ["all", "standalone"]: await _standalone()
	if part == "book": await _book_pages()
	var file := FileAccess.open(out_dir.path_join("manifest.json"), FileAccess.WRITE)
	file.store_string(JSON.stringify(manifest, "  "))
	file.close()
	print("UI_VISUAL_ROUND=%s shots=%d failures=%d" % [out_dir, manifest.size(), failures])
	await preload("res://scenes/Shared/Audio/Sound.gd").quit_cleanly(1 if failures > 0 else 0)

func _read_packets() -> bool:
	var file := FileAccess.open(SOURCE, FileAccess.READ)
	if file == null:
		push_error("缺少引擎记录：" + ProjectSettings.globalize_path(SOURCE))
		return false
	while not file.eof_reached():
		var line := file.get_line()
		if line.strip_edges() == "": continue
		var parsed = JSON.parse_string(line)
		if parsed is Dictionary: packets.append(parsed)
	return not packets.is_empty()

# ============================================================ 现场类场景 ===

func _live_field() -> void:
	fx = Fixture.new(self)
	host = await fx.fresh(true)
	if host == null:
		_fail("正式宿主未能进入第一章现场")
		return
	var ui: Node = host.ui
	await _frames(40)
	var field_state := _last_state(func(s): return bool(s.get("field_enabled", false)) and not (s.get("hud", {}).get("keys", []) as Array).is_empty())
	if not field_state.is_empty(): ui.apply_snapshot(field_state)

	# Panels/HUD/FieldHUD.tscn：标题、钟点、键位、近身提示、悬停名、心声、标题闪现。
	ui.set_hint("衣架上的风衣　E")
	ui.show_hover("书架", Vector2(560, 420))
	await _shot("Panels/HUD/FieldHUD", "现场抬头：标题、钟点、键位、近身提示、悬停名", {})
	ui.show_hover("", Vector2.ZERO)
	ui.set_hint("")
	var aside := _pick(func(ev, _s): return ev.kind == "aside")
	ui.hud.show_aside(str(aside.event.get("text", "样例心声。")) if not aside.is_empty() else "样例心声。", 30.0)
	await _frames(20)
	await _shot("Panels/HUD/FieldHUD-aside", "现场抬头：心声", {})
	ui.hud.flash_title("第一章")
	await _frames(40)
	await _shot("Panels/HUD/FieldHUD-flash", "现场抬头：标题闪现（中性样例）", {})
	await _frames(200)

	# Panels/Field/HintOverlay.tscn 与 SightMask.tscn：正式现场里的描边、新物件闪烁与视野遮罩。
	await _shot("Panels/Field/HintOverlay+SightMask", "现场：最近物件描边、未检视闪烁、视野外遮罩", {})

	# Panels/Dialogue/ChapterDialogue.tscn：有立绘、旁白、带选项三种。
	var speech := _pick(func(ev, _s): return ev.kind == "lines" and _has_speaker(ev) and (ev.get("choices", []) as Array).is_empty(),
		func(ev): return str(_longest_line(ev).get("text", "")).length())
	await _dialogue(ui, "Panels/Dialogue/ChapterDialogue-portrait", "对白：有立绘", speech, false)
	var narration := _pick(func(ev, _s): return ev.kind == "lines" and not _has_speaker(ev))
	await _dialogue(ui, "Panels/Dialogue/ChapterDialogue-narration", "对白：旁白", narration, false)
	var choosing := _pick(func(ev, _s): return ev.kind == "lines" and not (ev.get("choices", []) as Array).is_empty(),
		func(ev): return (ev.choices as Array).size())
	await _dialogue(ui, "Panels/Dialogue/ChapterDialogue-choices", "对白：选项", choosing, true)

	# UISystem.tscn：总装里的一个弹窗叠在现场上（证物卡）。
	var card := _pick(func(ev, _s): return ev.kind == "card" and str(ev.get("picture", "")) != "")
	if not card.is_empty():
		ui.present(card.event)
		await _frames(10)
		await _shot("UISystem", "总装：证物卡弹窗叠在现场上", {"title": str(card.event.get("title", ""))})
		ui.close_modal()

	# Panels/Field/ReplayOverlay.tscn：回放抬头叠在现场上。
	var overlay: CanvasLayer = load(UI + "Panels/Field/ReplayOverlay.tscn").instantiate()
	root.add_child(overlay)
	overlay.get_node("Bar/Caption").text = Paper.story_text("昨夜重演 · 样例说明文字")
	overlay.get_node("Door").text = "门：闩上"
	await _frames(6)
	await _shot("Panels/Field/ReplayOverlay", "回放抬头（中性样例文字）", {})
	overlay.queue_free()
	await fx.shutdown()
	host = null
	await _frames(4)

func _dialogue(ui: Node, slug: String, label: String, item: Dictionary, keep_choices: bool) -> void:
	if item.is_empty():
		_fail("缺少样本：" + slug)
		return
	ui.apply_snapshot(item.state)
	var ev: Dictionary = item.event.duplicate(true)
	ev.lines = [(ev.lines as Array)[-1] if keep_choices else _longest_line(ev)]
	if not keep_choices: ev.erase("choices")
	ui.present(ev)
	var d = ui.dialogue
	for i in 240:
		await process_frame
		if d.active and d.line != null and not d.busy: break
	if d.body.visible_characters >= 0: d.reveal()
	await _frames(20)
	await _shot(slug, label, {"line_id": str((ev.lines as Array)[0].get("line_id", ""))})
	d.cancel_presentation()
	d.panel.hide()
	await _frames(4)

# ============================================================ 单独实例化 ===

func _make_stage() -> void:
	if stage_root != null:
		return
	var canvas := CanvasLayer.new()
	root.add_child(canvas)
	stage_root = Control.new()
	stage_root.theme = Paper.theme()
	stage_root.size = Vector2(VIEW)
	canvas.add_child(stage_root)

func _standalone() -> void:
	_make_stage()

	# ---- 组件 ----
	await _sheet("Components/PaperButton", "按钮：各类型变体 × 常态 / 按下 / 禁用 / 焦点", Vector2(1040, 0), func(body: VBoxContainer):
		var variations: Array = ["Button"]
		for v in Paper.theme().get_type_variation_list("Button"): variations.append(v)
		for v in variations:
			var row := HBoxContainer.new()
			body.add_child(row)
			var name := Label.new()
			name.text = str(v)
			name.custom_minimum_size.x = 260
			row.add_child(name)
			for state in ["常态", "按下", "禁用", "焦点"]:
				var b := Paper.button("选项文字", Callable(), &"" if v == "Button" else StringName(v))
				if state == "按下":
					b.toggle_mode = true
					b.button_pressed = true
				elif state == "禁用": b.disabled = true
				row.add_child(b)
				if state == "焦点": b.grab_focus.call_deferred())
	await _sheet("Components/PaperLabel", "文字：各类型变体与语义色", Vector2(1040, 0), func(body: VBoxContainer):
		var variations: Array = ["RichTextLabel"]
		for v in Paper.theme().get_type_variation_list("RichTextLabel"):
			if str(v).begins_with("PaperLabel"): variations.append(v)
		for v in variations:
			var l := Paper.label("%s　印刷正文，墨色。[color=muted]淡墨[/color] [color=seal]印红[/color] [color=match]相容[/color] [color=hand]笔迹[/color]" % v,
				&"" if v == "RichTextLabel" else StringName(v))
			l.custom_minimum_size.x = 960
			body.add_child(l)
		for pt in ["PixelMast", "PixelHeadline", "PixelTitle", "PixelSub", "PixelColumn"]:
			var px := Paper.pixel("%s　像素标题" % pt, StringName(pt))
			px.align = "left"
			body.add_child(px))
	await _sheet("Components/PaperRule", "规线：细线 / 双线 / 叠题短线", Vector2(832, 0), func(body: VBoxContainer):
		body.add_child(Paper.label("上一段文字。"))
		body.add_child(Paper.rule())
		body.add_child(Paper.label("报纸双线："))
		var d := Paper.rule()
		d.style = "double"
		body.add_child(d)
		body.add_child(Paper.label("[center]叠题主标题[/center]", &"PaperLabelTitle"))
		var dash := Paper.rule()
		dash.style = "dash"
		body.add_child(dash)
		body.add_child(Paper.label("[center]副题[/center]", &"PaperLabelDeck")))
	await _sheet("Components/PaperCheck", "选项行：常态 / 选中 / 禁用", Vector2(832, 0), func(body: VBoxContainer):
		var states := [["是我闩的门。", false, false], ["苏泠当时在她房里。", true, false], ["门外确实有人在喊。", false, true]]
		for i in states.size():
			var b := Paper.check(states[i][0], Callable())
			b.serial = Paper.roman(i + 1)
			b.button_pressed = states[i][1]
			b.disabled = states[i][2]
			body.add_child(b))
	await _sheet("Components/PaperEntry", "条目行（候选菜单、笔记本）", Vector2(832, 0), func(body: VBoxContainer):
		for i in 3:
			var e := Paper.entry(["问问他知道些什么", "请他看看这件东西", "没什么了"][i], Callable(), Paper.roman(i + 1))
			e.disabled = i == 1
			body.add_child(e))
	await _sheet("Components/PaperStamp", "印戳：相容 / 冲突 / 未查", Vector2(576, 0), func(body: VBoxContainer):
		var row := HBoxContainer.new()
		body.add_child(row)
		for pair in [["吻合", "match"], ["冲突", "seal"], ["未查", "muted"]]:
			row.add_child(Paper.stamp(pair[0], pair[1])))
	await _sheet("Components/PaperTally", "计数", Vector2(576, 0), func(body: VBoxContainer):
		body.add_child(Paper.tally(6, 2)))
	await _sheet("Components/PaperPixelText", "像素字：报头 / 大标题 / 标题 / 副标题 / 栏题", Vector2(1152, 0), func(body: VBoxContainer):
		for pair in [["证物录", "PixelMast"], ["侦探的外衣口袋", "PixelHeadline"], ["昨夜重演", "PixelTitle"], ["生活翼 · 五号房", "PixelSub"], ["可做的事", "PixelColumn"]]:
			body.add_child(Paper.pixel(pair[0], StringName(pair[1]))))
	await _modal_shot("Components/PaperModal", "纸页弹窗：标题、正文、页脚（次 + 主）", "弹窗标题", func(modal: Control):
		modal.get_body().add_child(Paper.label("正文一行。弹窗高度应随内容收缩。"))
		modal.add_footer_button("取消", Callable())
		modal.add_footer_button("确定", Callable()))
	await _sheet("Components/DialogueResponse", "对白选项按钮", Vector2(832, 0), func(body: VBoxContainer):
		for text in ["问问他知道些什么", "没什么了"]:
			var b: Button = load(UI + "Components/DialogueResponse.tscn").instantiate()
			b.text = text
			b.theme = load(UI + "Styles/DialogueTheme.tres")
			body.add_child(b))
	await _sheet("Components/ReplayBubble", "回放气泡", Vector2(576, 0), func(body: VBoxContainer):
		var bubble: Control = load(UI + "Components/ReplayBubble.tscn").instantiate()
		body.add_child(bubble)
		bubble.get_node("Text").text = Paper.story_text("样例：气泡里的一句话。"))

	# ---- 面板（放进与 UISystem 相同的纸页弹窗，宽度按面板用途） ----
	var picture := _pick(func(ev, _s): return ev.kind == "card" and str(ev.get("picture", "")) != "",
		func(ev): return str(ev.get("facts", "")).length())
	await _panel_event("Panels/Card/EvidenceCard", "证物卡：局部图 + 事实 + 来源", "Card/EvidenceCard.tscn", picture)
	var rich := _pick(func(ev, _s): return ev.kind == "card" and (ev.has("table") or not (ev.get("notes", []) as Array).is_empty()),
		func(ev): return (ev.get("notes", []) as Array).size() + (10 if ev.has("table") else 0))
	await _panel_event("Panels/Card/EvidenceCard-notes", "证物卡：笔记或表格", "Card/EvidenceCard.tscn", rich)
	var short_card := _pick(func(ev, _s): return ev.kind == "card" and str(ev.get("picture", "")) == "" and not ev.has("table"),
		func(ev): return -str(ev.get("facts", "")).length())
	await _panel_event("Panels/Card/EvidenceCard-short", "证物卡：短文字", "Card/EvidenceCard.tscn", short_card)
	var name_form := _pick(func(ev, _s): return ev.kind == "form" and _has_text_field(ev))
	await _panel_event("Panels/Form/FormPanel-text", "表单：填空", "Form/FormPanel.tscn", name_form)
	var stack_form := _pick(func(ev, _s): return ev.kind == "form" and str(ev.get("layout", "stack")) != "tabs" and not _has_text_field(ev),
		func(ev): return (ev.get("fields", []) as Array).size())
	await _panel_event("Panels/Form/FormPanel-choices", "表单：单选（第一题已选）", "Form/FormPanel.tscn", stack_form, func(view): _select_first(view))
	var tabs_form := _pick(func(ev, _s): return ev.kind == "form" and str(ev.get("layout", "")) == "tabs")
	await _panel_event("Panels/Form/FormPanel-tabs", "对质：陈述与出示（各选一项）", "Form/FormPanel.tscn", tabs_form, func(view): _select_first(view, 2))
	var menu := _pick(func(ev, _s): return ev.kind == "candidates", func(ev): return (ev.get("options", []) as Array).size())
	if not menu.is_empty():
		var data := {"prompt": str(menu.event.get("prompt", "")), "options": menu.event.get("options", [])}
		await _panel_data("Panels/Menu/ChoiceMenu", "候选菜单", "Menu/ChoiceMenu.tscn", str(menu.event.get("title", "")), data)
	else: _fail("缺少样本：候选菜单")
	var notebook_state := _last_state(func(s): return not (s.get("notebook", {}).get("sections", []) as Array).is_empty())
	await _panel_data("Panels/Notebook/Notebook", "笔记本", "Notebook/Notebook.tscn", "笔记本", notebook_state.get("notebook", {}), Vector2(1152, 0))
	var unrun := _last_state(func(s): return bool(s.get("replay", {}).get("open", false)) and (s.get("replay", {}).get("result", {}) as Dictionary).is_empty())
	if not unrun.is_empty():
		var replay: Dictionary = unrun.replay.duplicate(true)
		var vars: Array = []
		for i in (replay.questions as Array).size(): vars.append(0 if i < 2 else -1)
		replay.vars = vars
		await _panel_data("Panels/Manuscript/ReplayManuscript", "重演手稿：未重演，选了两题", "Manuscript/ReplayManuscript.tscn", "", replay, Vector2(1152, 0))
	else: _fail("缺少样本：未重演的手稿")
	var run := _last_state(func(s): return not (s.get("replay", {}).get("result", {}) as Dictionary).is_empty())
	if not run.is_empty():
		await _panel_data("Panels/Manuscript/ReplayManuscript-result", "重演手稿：对照结果", "Manuscript/ReplayManuscript.tscn", "", run.replay, Vector2(1152, 0))
	else: _fail("缺少样本：重演结果")
	await _panel_data("Panels/CampusMap/CampusMap", "学园图（中性样例）", "CampusMap/CampusMap.tscn", "学园图", _campus_sample(), Vector2(1152, 0))
	await _panel_data("Panels/Ending/EndingCard", "章末卡（中性样例）", "Ending/EndingCard.tscn", "", _ending_sample(), Vector2(832, 0), Callable(), _ending_sample().actions)

	await _book_pages()

## 标题页与设置册（整屏）。
func _book_pages() -> void:
	_make_stage()
	_clear_stage()
	var title: Control = load(UI + "Panels/Title/Title.tscn").instantiate()
	stage_root.add_child(title)
	title.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	await _frames(10)
	await _shot("Panels/Title/Title", "标题页：油灯下桌上的册子", {})
	title.queue_free()

	# ---- 设置册（整屏）：凡例、操作、书签三页 ----
	for page in [["rules", "凡例：画面、声音、语言、无障碍"], ["controls", "操作：改键"], ["bookmarks", "书签：存档位（存档尚未接入）"]]:
		_clear_stage()
		var backdrop := TextureRect.new()
		backdrop.texture = load("res://ShareAsset/Internal/Props/UIDesk/desk.png")
		backdrop.size = Vector2(VIEW)
		stage_root.add_child(backdrop)
		var book: Control = load(UI + "Panels/Settings/SettingsBook.tscn").instantiate()
		book.page = page[0]
		stage_root.add_child(book)
		book.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		await _frames(10)
		await _shot("Panels/Settings/SettingsBook-" + page[0], "设置册 · " + page[1], {})
		book.queue_free()

## 纸页上的一组组件；dark 为真时用深卡纸底。
func _sheet(slug: String, label: String, size: Vector2, build: Callable, dark := false) -> void:
	_clear_stage()
	var backdrop := ColorRect.new()
	backdrop.color = Color("1b1816")
	backdrop.size = Vector2(VIEW)
	stage_root.add_child(backdrop)
	var sheet := PanelContainer.new()
	sheet.theme_type_variation = &"PaperSheet"
	if dark:
		sheet.theme = load(UI + "Styles/DialogueTheme.tres")
		sheet.theme_type_variation = &""
	stage_root.add_child(sheet)
	var body := VBoxContainer.new()
	sheet.add_child(body)
	build.call(body)
	await _frames(4)
	sheet.size = Vector2(size.x, 0)
	sheet.reset_size()
	sheet.size.x = size.x
	sheet.position = (Vector2(VIEW) - sheet.size) / 2.0
	await _frames(4)
	await _shot(slug, label, {})

func _modal_shot(slug: String, label: String, title: String, build: Callable, size := Vector2(832, 680)) -> Control:
	_clear_stage()
	var layer := CanvasLayer.new()
	layer.layer = 5
	stage_root.add_child(layer)
	var modal: Control = Paper.modal(layer, title, size)
	modal.theme = Paper.theme()
	build.call(modal)
	await _frames(6)
	await _shot(slug, label, {})
	return modal

func _panel_event(slug: String, label: String, scene: String, item: Dictionary, after: Callable = Callable()) -> void:
	if item.is_empty():
		_fail("缺少样本：" + slug)
		return
	var ev: Dictionary = item.event.duplicate(true)
	if ev.get("picture", "") is String and str(ev.get("picture", "")) != "" and not ResourceLoader.exists(str(ev.picture)): ev.picture = ""
	var inline: Array = []
	for option in ev.get("options", []):
		if str(option.get("group", "")) == "inline": inline.append(option)
	ev.inline = inline
	var size := Vector2(832, 0)
	if str(ev.get("kind", "")) == "card" and (str(ev.get("picture", "")) != "" or ev.has("table")): size.x = 1152
	if str(ev.get("kind", "")) == "form" and preload("res://scenes/UI/Panels/Form/FormPanel.gd").uses_columns(ev): size.x = 1152
	var modal_title := "" if str(ev.get("kind", "")) == "card" else str(ev.get("title", ""))
	await _panel_data(slug, label, scene, modal_title, ev, size, after,
		ev.get("options", ev.get("actions", [])))

func _panel_data(slug: String, label: String, scene: String, title: String, data: Dictionary, size := Vector2(900, 660),
		after: Callable = Callable(), footer: Array = []) -> void:
	if data.is_empty():
		_fail("缺少样本：" + slug)
		return
	var view: Control
	await _modal_shot_with(slug, label, title, size, func(modal: Control):
		view = load(UI + "Panels/" + scene).instantiate()
		if scene.begins_with("Manuscript"):
			view.custom_minimum_size = Vector2(0, 704)
			view.size_flags_vertical = Control.SIZE_EXPAND_FILL
		modal.get_body().add_child(view)
		view.show_data(data)
		if view.has_method("pinned_header"):
			var header: Control = view.pinned_header()
			if header != null: modal.pin_header(header)
		var columns: bool = preload("res://scenes/UI/Panels/Form/FormPanel.gd").uses_columns(data)
		for option in footer:
			if str(option.get("group", "")) == "inline": continue
			if option.has("tab") and columns: continue
			var b: Button = modal.add_footer_button(str(option.get("label", "")), Callable(), not bool(option.get("available", true)))
			b.custom_minimum_size.x = maxf(140, b.custom_minimum_size.x)
		if after.is_valid(): after.call(view))

func _modal_shot_with(slug: String, label: String, title: String, size: Vector2, build: Callable) -> void:
	_clear_stage()
	var backdrop := ColorRect.new()
	backdrop.color = Color("1b1816")
	backdrop.size = Vector2(VIEW)
	stage_root.add_child(backdrop)
	var layer := CanvasLayer.new()
	layer.layer = 5
	stage_root.add_child(layer)
	var modal: Control = Paper.modal(layer, title, size)
	build.call(modal)
	await _frames(30)
	await _shot(slug, label, {})

func _clear_stage() -> void:
	for child in stage_root.get_children():
		stage_root.remove_child(child)
		child.queue_free()

func _select_first(view: Control, count := 1) -> void:
	var picked := 0
	for field in view._fields:
		if picked >= count: break
		for option in field.get("options", []):
			if bool(option.get("available", true)):
				view.select(str(field.get("id", "")), str(option.get("id", "")))
				picked += 1
				break

# ================================================================== 公共 ===

func _events() -> Array:
	var result: Array = []
	var state: Dictionary = {}
	for packet in packets:
		for ev in packet.get("output", []):
			if not ev is Dictionary: continue
			if str(ev.get("kind", "")) == "state":
				state = ev.get("snapshot", {})
				continue
			result.append({"event": ev, "state": state})
	return result

func _pick(filter: Callable, score: Callable = Callable()) -> Dictionary:
	var best: Dictionary = {}
	var best_score := -INF
	for item in _events():
		if not filter.call(item.event, item.state): continue
		var value: float = score.call(item.event) if score.is_valid() else 0.0
		if best.is_empty() or value > best_score:
			best = item
			best_score = value
	return best

func _last_state(filter: Callable) -> Dictionary:
	var found: Dictionary = {}
	for packet in packets:
		for ev in packet.get("output", []):
			if ev is Dictionary and str(ev.get("kind", "")) == "state" and filter.call(ev.get("snapshot", {})):
				found = ev.snapshot
	return found

func _shot(slug: String, label: String, extra: Dictionary) -> void:
	await _frames(8)
	await RenderingServer.frame_post_draw
	var path := out_dir.path_join(slug.replace("/", "__") + ".png")
	var error := root.get_texture().get_image().save_png(path)
	if error != OK: _fail("保存失败：" + path)
	var entry := {"scene": "res://scenes/UI/" + slug.get_slice("-", 0) + ".tscn", "file": path.get_file(), "label": label}
	entry.merge(extra)
	manifest.append(entry)
	print("shot " + path)

func _frames(count: int) -> void:
	for i in count: await process_frame

func _fail(message: String) -> void:
	failures += 1
	push_error("ui visual round: " + message)

func _has_speaker(ev: Dictionary) -> bool:
	for line in ev.get("lines", []):
		if str(line.get("speaker", "")) not in ["", "narrator", "旁白"]: return true
	return false

func _has_text_field(ev: Dictionary) -> bool:
	for field in ev.get("fields", []):
		if str(field.get("kind", "choice" if field.has("options") else "text")) == "text": return true
	return false

func _longest_line(ev: Dictionary) -> Dictionary:
	var best: Dictionary = {}
	for line in ev.get("lines", []):
		if best.is_empty() or str(line.get("text", "")).length() > str(best.get("text", "")).length(): best = line
	return best

# ------------------------------------------------- 记录缺少面板的中性样例 ---

func _campus_sample() -> Dictionary:
	var nodes := {}
	var layout := [["N1", 500, 70], ["N2", 500, 200], ["N3", 300, 200], ["N4", 120, 300], ["N5", 500, 380],
		["N6", 370, 330], ["N7", 630, 330], ["N8", 370, 440], ["N9", 630, 440], ["N10", 820, 430], ["N11", 900, 330], ["N12", 820, 220]]
	for i in layout.size():
		var row: Array = layout[i]
		nodes[row[0]] = {"pos": [row[1], row[2]], "label": "地点%d" % (i + 1), "map": "sample_%d" % i, "spawn": ""}
	var links := [["N1", "N2"], ["N2", "N3"], ["N3", "N4"], ["N2", "N5"], ["N5", "N6"], ["N5", "N7"], ["N6", "N8"], ["N7", "N9"],
		["N5", "N10"], ["N10", "N11"], ["N2", "N12"]]
	var known := {}
	for key in ["N1", "N2", "N3", "N5", "N6", "N7", "N9", "N10"]: known[key] = true
	return {"title": "学园图", "map": "sample_4", "here": "N5", "nodes": nodes, "links": links, "known": known}

func _ending_sample() -> Dictionary:
	return {"kind": "ending", "title": "第一章　终", "outcome": "样例结局文字，用于检查版式。",
		"rows": ["样例记录一", "样例记录二", "样例记录三"],
		"actions": [{"id": "sample_continue", "label": "继续 (C)", "key": "C"}, {"id": "sample_restart", "label": "重新开始 (R)", "key": "R"}]}
