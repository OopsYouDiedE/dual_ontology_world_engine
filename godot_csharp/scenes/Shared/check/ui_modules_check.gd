extends SceneTree
## 共享 UI 模块自动检查（不是盲玩）：逐个实例化 scenes/UI 下的组件，喂样例数据，检查节点显隐与内容。
## 运行：godot --headless --path godot_csharp --script res://scenes/Shared/check/ui_modules_check.gd
const UI := "res://scenes/UI/"
const SAMPLE_PICTURE := "res://ShareAsset/Internal/Characters/animation_v2/chef.png"

var failures: Array[String] = []
var checks := 0

func ok(condition: bool, message: String) -> void:
	checks += 1
	if not condition:
		failures.append(message)
		print("  FAIL: ", message)

func _initialize() -> void:
	call_deferred("run")

func spawn(path: String) -> Node:
	var scene := load(path) as PackedScene
	ok(scene != null, "load " + path)
	var node := scene.instantiate()
	root.add_child(node)
	return node

func scene_files(base: String) -> Array:
	var files: Array = []
	for file in DirAccess.get_files_at(base):
		if file.ends_with(".tscn"):
			files.append(base + "/" + file)
	for sub in DirAccess.get_directories_at(base):
		files.append_array(scene_files(base + "/" + sub))
	return files

func run() -> void:
	for path in scene_files("res://scenes/UI"):
		if path.contains("/Workbench/"):
			continue
		var node := spawn(path)
		ok(node != null, "instantiate " + path)
		await process_frame
		node.queue_free()
		await process_frame
	await check_hud()
	await check_choice_menu()
	await check_evidence_card()
	await check_form()
	await check_notebook()
	await check_manuscript()
	await check_campus_map()
	await check_ending()
	await check_modal()
	await check_workbench()
	print("UI_MODULES_CHECK: %d checks, %d failures" % [checks, failures.size()])
	for message in failures:
		print("  - ", message)
	quit(0 if failures.is_empty() else 1)

func check_hud() -> void:
	var hud := spawn(UI + "Panels/HUD/FieldHUD.tscn")
	await process_frame
	ok(not hud.get_node("Keys").visible and not hud.get_node("Hint").visible and not hud.get_node("Time").visible, "HUD starts with empty pieces hidden")
	hud.set_title("生活翼 · 活动厅")
	ok(hud.get_node("Title").text == "生活翼 · 活动厅", "HUD title")
	hud.set_time("07:20")
	ok(hud.get_node("Time").visible and hud.get_node("Time").text == "07:20", "HUD time shown")
	hud.set_time("")
	ok(not hud.get_node("Time").visible, "HUD time hidden when empty")
	hud.set_keys(["E 检视", "Tab 笔记本"])
	ok(hud.get_node("Keys").visible and hud.get_node("Keys").text == "E 检视　　Tab 笔记本", "HUD keys joined")
	hud.set_hint("靠近一处，按 E")
	ok(hud.get_node("Hint").visible, "HUD hint shown")
	hud.set_hint("")
	ok(not hud.get_node("Hint").visible, "HUD hint hidden when empty")
	hud.show_hover("一张桌子", Vector2(5000, 5000))
	var tip: Label = hud.get_node("HoverTip")
	ok(tip.visible and tip.position.x <= 1272 - tip.size.x + 0.01 and tip.position.y <= 960, "HUD hover clamped inside viewport")
	hud.show_hover("一张桌子", Vector2(-50, -50))
	ok(tip.position.x >= 8 and tip.position.y >= 64, "HUD hover clamped at top-left")
	hud.hide_hover()
	ok(not tip.visible, "HUD hover hidden")
	hud.show_aside("好像有人来过。", 3.0)
	ok(hud.get_node("Aside").visible, "HUD aside shown")
	hud._process(1.0)
	ok(hud.get_node("Aside").visible, "HUD aside persists before timeout")
	hud._process(2.5)
	ok(not hud.get_node("Aside").visible, "HUD aside auto-hides")
	hud.flash_title("第三日 · 清晨")
	ok(hud.get_node("Flash").visible and hud.get_node("Flash").text.contains("第三日"), "HUD flash title")
	hud.fade_to(0.5, 0.0)
	ok(is_equal_approx(hud.get_node("Fade").color.a, 0.5), "HUD fade_to instant")
	await hud.fade_to(1.0, 0.1)
	ok(is_equal_approx(hud.get_node("Fade").color.a, 1.0), "HUD fade_to awaitable")
	hud.set_bar_visible(false)
	ok(not hud.get_node("Title").visible and not hud.get_node("Vignette").visible, "HUD bar hidden for cinematics")
	hud.set_bar_visible(true)
	ok(hud.get_node("Title").visible, "HUD bar restored")
	hud.queue_free()
	await process_frame

func check_choice_menu() -> void:
	var menu := spawn(UI + "Panels/Menu/ChoiceMenu.tscn")
	await process_frame
	var picked: Array = []
	menu.chosen.connect(func(id: String): picked.append(id))
	menu.show_data({"prompt": "你要做什么？", "options": [
		{"id": "look", "label": "看一看", "available": true, "lock_hint": ""},
		{"id": "ask", "label": "问一问", "available": true, "lock_hint": ""},
		{"id": "locked", "label": "还不能选", "available": false, "lock_hint": "还缺一条线索"},
	]})
	ok(menu.get_node("Prompt").visible, "ChoiceMenu prompt visible")
	ok(menu.get_node("Choices").get_child_count() == 3, "ChoiceMenu has 3 buttons")
	var locked: Button = menu.get_node("Choices").get_child(2)
	ok(locked.disabled and locked.tooltip_text == "还缺一条线索", "ChoiceMenu locked option disabled with tooltip")
	menu.get_node("Choices").get_child(1).pressed.emit()
	ok(picked == ["ask"], "ChoiceMenu emits chosen(id)")
	menu.show_data({"options": [{"id": "x", "label": "唯一"}]})
	ok(not menu.get_node("Prompt").visible and menu.get_node("Choices").get_child_count() == 1, "ChoiceMenu without prompt")
	menu.queue_free()
	await process_frame

func check_evidence_card() -> void:
	var card := spawn(UI + "Panels/Card/EvidenceCard.tscn")
	await process_frame
	var picked: Array = []
	card.chosen.connect(func(id: String): picked.append(id))
	card.show_data({"picture": SAMPLE_PICTURE, "intro": "一份简介", "facts": "亲见的事实。", "source": "现场",
		"notes": ["[color=seal]回忆：这个声音似曾相识。[/color]", "旁人没有提起过它。"], "items": ["第一条", "第二条"],
		"table": [["位置", "谁", "听到"], ["门口", "甲", "脚步"], ["窗边", "乙", "风声"]],
		"inline": [{"id": "a", "label": "选项甲", "available": true}, {"id": "b", "label": "选项乙", "available": false, "lock_hint": "锁"}]})
	ok(card.get_node("Row/Picture").visible and card.get_node("Row/Picture").texture != null, "EvidenceCard picture shown")
	ok(card.get_node("Row/Texts/Intro").visible and card.get_node("Row/Texts/Facts").visible, "EvidenceCard intro and facts")
	ok(card.get_node("Row/Texts/Source").visible and card.get_node("Row/Texts/Source").text.begins_with("来源："), "EvidenceCard source prefixed")
	var notes: Control = card.get_node("Notes")
	ok(notes.visible and notes.get_child_count() == 2, "EvidenceCard notes")
	ok(notes.get_child(0).text.contains("#" + card.Paper.color("seal").to_html(false)), "EvidenceCard recall note carries the seal color from story markup")
	ok(notes.get_child(1).get_theme_color("default_color") == card.Paper.color("ink"), "EvidenceCard plain note is ink")
	ok(card.get_node("Items").get_child_count() == 2, "EvidenceCard items")
	var table: GridContainer = card.get_node("Table")
	ok(table.visible and table.columns == 3 and table.get_child_count() == 9, "EvidenceCard table columns follow first row")
	ok(card.get_node("Inline").visible and card.get_node("Inline/Choices").get_child_count() == 2, "EvidenceCard inline options")
	ok(card.get_node("Inline/Choices").get_child(1).disabled, "EvidenceCard inline lock")
	card.get_node("Inline/Choices").get_child(0).pressed.emit()
	ok(picked == ["a"], "EvidenceCard inline emits chosen")
	card.show_data({"facts": "只有事实"})
	ok(not card.get_node("Row/Picture").visible and not card.get_node("Row/Texts/Source").visible and not card.get_node("Row/Texts/Intro").visible, "EvidenceCard hides empty parts")
	ok(not card.get_node("Notes").visible and not card.get_node("Table").visible and not card.get_node("Inline").visible, "EvidenceCard hides empty lists")
	card.queue_free()
	await process_frame

func check_form() -> void:
	var form := spawn(UI + "Panels/Form/FormPanel.tscn")
	await process_frame
	var changes := [0]
	var submits := [0]
	form.changed.connect(func(): changes[0] += 1)
	form.submit_requested.connect(func(): submits[0] += 1)
	form.show_data({"prompt": "说点什么", "layout": "stack", "fields": [
		{"id": "name", "kind": "text", "heading": "名字", "placeholder": "最多八个字", "max_length": 8},
		{"id": "sentence", "kind": "choice", "heading": "选句", "options": [{"id": "s0", "label": "“你好”"}, {"id": "s1", "label": "“谁在那里”"}], "value": "s0"},
		{"id": "spot", "kind": "choice", "heading": "选位", "flow": true, "options": [{"id": "door", "label": "门口"}, {"id": "window", "label": "窗边"}]},
	]})
	ok(form.get_node("Prompt").visible, "FormPanel prompt")
	ok(form.get_node("Fields").get_child_count() == 3, "FormPanel stacks 3 fields")
	for group in form.get_node("Fields").get_children():
		ok(group.visible, "FormPanel stack shows every field")
	ok(form.values() == {"name": "", "sentence": "s0", "spot": ""}, "FormPanel initial values")
	ok(not form.is_complete(["name", "spot"]), "FormPanel incomplete")
	form.button_for("spot", "window").pressed.emit()
	ok(form.values().spot == "window", "FormPanel choice toggled")
	ok(form.button_for("spot", "window").button_pressed and not form.button_for("spot", "door").button_pressed, "FormPanel single selection")
	form.button_for("spot", "door").pressed.emit()
	ok(form.values().spot == "door" and not form.button_for("spot", "window").button_pressed, "FormPanel selection moves")
	form.line_edit_for("name").text = "  "
	ok(not form.is_complete(["name"]), "FormPanel blank text is incomplete")
	form.line_edit_for("name").text = "阿青"
	form.line_edit_for("name").text_changed.emit("阿青")
	ok(form.values().name == "阿青" and form.is_complete(["name", "sentence", "spot"]), "FormPanel complete")
	form.line_edit_for("name").text_submitted.emit("阿青")
	ok(submits[0] == 1, "FormPanel Enter emits submit_requested")
	ok(changes[0] >= 3, "FormPanel emits changed")
	# tabs
	# STORY-HARDCODE[CSH-015][text][fixture]: UI测试自带的案件问答样例，不视为正式剧情规则。
	form.show_data({"header": {"question": "提问者问：[b]谁关了灯？[/b]", "instructions": "陈述一句，再出示一件。"}, "layout": "tabs", "unset_label": "（未选）",
		"summary": [{"field": "say", "label": "陈述"}, {"field": "show", "label": "出示"}],
		"fields": [
			{"id": "say", "kind": "choice", "options": [{"id": "a", "label": "结论甲"}, {"id": "b", "label": "结论乙"}]},
			{"id": "show", "kind": "choice", "options": [], "empty_text": "还没有可出示的东西。"},
		]})
	ok(form.get_node("Header").visible and form.get_node("Header/Question").visible, "FormPanel header pinned")
	var summary: RichTextLabel = form.get_node("Header/Summary")
	ok(summary.text == "陈述：（未选）\n出示：（未选）", "FormPanel summary shows unset labels")
	ok(form.get_node("Fields").get_child(0).visible and not form.get_node("Fields").get_child(1).visible, "FormPanel tabs show first group only")
	form.show_field("show")
	ok(not form.get_node("Fields").get_child(0).visible and form.get_node("Fields").get_child(1).visible, "FormPanel show_field swaps group")
	form.show_field("say")
	form.button_for("say", "b").pressed.emit()
	ok(summary.text == "陈述：结论乙\n出示：（未选）", "FormPanel summary tracks selection")
	ok(form.is_complete(["say"]) and not form.is_complete(["say", "show"]), "FormPanel is_complete with required list")
	form.queue_free()
	await process_frame

func check_notebook() -> void:
	var notebook := spawn(UI + "Panels/Notebook/Notebook.tscn")
	await process_frame
	var opened: Array = []
	notebook.open_card.connect(func(id: String): opened.append(id))
	notebook.show_data({"empty_text": "还没有记下任何东西。", "sections": []})
	ok(notebook.get_node("Empty").visible and notebook.get_node("Sections").get_child_count() == 0, "Notebook empty text")
	# STORY-HARDCODE[CSH-016][text][fixture]: UI测试自带的具体调查样例文本，未证明与正式案件相同。
	notebook.show_data({"empty_text": "空", "sections": [
		{"heading": "[b]看过的[/b]", "style": "grid", "entries": [{"label": "水渍", "card_id": "E1"}, {"label": "钥匙", "card_id": "E2"}, {"label": "门锁", "card_id": "E3"}]},
		{"heading": "[b]规矩[/b]", "style": "list", "entries": [{"label": "夜里不得离开房间。"}, {"label": "（待查）", "muted": true}]},
	]})
	ok(not notebook.get_node("Empty").visible, "Notebook empty text hidden when sections exist")
	var sections: Control = notebook.get_node("Sections")
	var grid: GridContainer = null
	var list: VBoxContainer = null
	for child in sections.get_children():
		if child is GridContainer:
			grid = child
		elif child is VBoxContainer:
			list = child
	ok(grid != null and grid.columns == 2 and grid.get_child_count() == 3 and grid.get_child(0) is Button, "Notebook grid of buttons")
	ok(list != null and list.get_child_count() == 2 and list.get_child(0) is RichTextLabel, "Notebook list of labels")
	ok(list.get_child(1).get_theme_color("default_color") == notebook.Paper.color("muted"), "Notebook muted entry")
	grid.get_child(1).pressed.emit()
	ok(opened == ["E2"], "Notebook emits open_card")
	notebook.queue_free()
	await process_frame

func check_manuscript() -> void:
	var m := spawn(UI + "Panels/Manuscript/ReplayManuscript.tscn")
	await process_frame
	var changed: Array = []
	var runs: Array = []
	var cards: Array = []
	var closed := [0]
	m.vars_changed.connect(func(v: Array): changed.append(v))
	m.run_requested.connect(func(v: Array, skip: bool): runs.append([v, skip]))
	m.open_card.connect(func(id: String): cards.append(id))
	m.close_requested.connect(func(): closed[0] += 1)
	var data := {"title": "重演推演", "hint": "先设定。", "questions": [
		{"question": "谁先到？", "options": ["甲", "乙"], "mark": "", "fresh": false},
		{"question": "几点？", "options": ["七点", "八点", "九点"], "mark": "　[color=seal]✓[/color]", "fresh": true},
	], "vars": [-1, -1], "last_run_vars": [], "result": {}}
	m.show_data(data)
	ok(m.option_buttons.size() == 2 and m.option_buttons[1].size() == 3, "Manuscript builds questions")
	ok(m.run_buttons[0].disabled and m.run_buttons[1].disabled, "Manuscript run disabled until all vars set")
	ok(m.stale_label.text == m.unset_hint, "Manuscript unset hint")
	m.option_buttons[0][1].pressed.emit()
	ok(changed.size() == 1 and changed[0] == [1, -1], "Manuscript emits vars_changed")
	m.option_buttons[1][2].pressed.emit()
	ok(changed.size() == 2 and changed[1] == [1, 2] and not m.run_buttons[0].disabled, "Manuscript run enabled when complete")
	m.run_buttons[0].pressed.emit()
	m.run_buttons[1].pressed.emit()
	ok(runs.size() == 2 and runs[0] == [[1, 2], false] and runs[1] == [[1, 2], true], "Manuscript emits run_requested")
	data["vars"] = [1, 2]
	data["last_run_vars"] = [1, 2]
	data["result"] = {"full_match": true, "findings": [
		{"title": "门闩", "status": "match", "text": "", "card_id": "E1"},
		{"title": "脚印", "status": "conflict", "text": "方向对不上。", "card_id": "E2"},
		{"title": "窗台", "status": "unvisited", "text": "还没查。", "card_id": "E3"},
	]}
	m.show_data(data)
	ok(m.stale_label.text == "", "Manuscript fresh result is not stale")
	ok(m.results_box.get_child_count() == 4, "Manuscript full-match banner plus 3 findings")
	var open_buttons: Array = []
	for row in m.results_box.get_children():
		var b := row.get_node_or_null("OpenCard") as Button
		if b != null:
			open_buttons.append(b)
	ok(open_buttons.size() == 3 and open_buttons[0].visible and open_buttons[1].visible and not open_buttons[2].visible, "Manuscript open-card button only for visited findings")
	open_buttons[1].pressed.emit()
	ok(cards == ["E2"], "Manuscript emits open_card")
	m.option_buttons[0][0].pressed.emit()
	ok(m.stale_label.text == m.stale_hint, "Manuscript stale after changing a var")
	ok(m.results_box.get_child_count() == 3, "Manuscript stale hides full-match banner")
	m.get_node("LeftPage/Scroll/Column/Actions/Close").pressed.emit()
	ok(closed[0] == 1, "Manuscript emits close_requested")
	m.queue_free()
	await process_frame

func check_campus_map() -> void:
	var map := spawn(UI + "Panels/CampusMap/CampusMap.tscn")
	await process_frame
	map.here = "Hall"
	map.known = {"Hall": true, "Commons": true}
	var picked: Array = []
	map.picked.connect(func(n: String): picked.append(n))
	ok(map.has_method("play") and map.has_method("cancel_playback") and "cancelled" in map, "CampusMap replay API")
	ok(map.has_signal("picked"), "CampusMap picked signal")
	map.cancel_playback()
	ok(map.cancelled, "CampusMap cancel_playback sets cancelled")
	map.queue_free()
	await process_frame

func check_ending() -> void:
	var ending := spawn(UI + "Panels/Ending/EndingCard.tscn")
	await process_frame
	ending.show_data({"outcome": "天亮了。", "rows": ["[color=seal]留下的钩：[/color]钥匙", "你没有交出的：名字", "按空格回到标题。"]})
	ok(ending.get_node("Outcome").visible and ending.get_node("Rows").get_child_count() == 3, "EndingCard outcome and rows")
	ok(ending.get_node("Rows").get_child(0).text.contains("#8a3324"), "EndingCard semantic colors resolved")
	ending.show_data({"outcome": "", "rows": []})
	ok(not ending.get_node("Outcome").visible and ending.get_node("Rows").get_child_count() == 0, "EndingCard empty")
	ending.queue_free()
	await process_frame

func check_modal() -> void:
	var modal := spawn(UI + "Components/PaperModal.tscn")
	await process_frame
	var fired := [0]
	var first: Button = modal.add_footer_button("开始", func(): fired[0] += 1)
	var second: Button = modal.add_footer_button("算了", Callable(), true, "还不行")
	ok(modal.get_footer() != null and modal.get_footer().get_child_count() == 2, "PaperModal footer holds buttons")
	ok(second.disabled and second.tooltip_text == "还不行", "PaperModal footer disabled + tooltip")
	first.pressed.emit()
	ok(fired[0] == 1, "PaperModal footer action fires")
	var rules := 0
	for child in modal.get_column().get_children():
		if child is ColorRect:
			rules += 1
	ok(rules == 2, "PaperModal adds one rule for the footer (title rule + footer rule)")
	modal.clear_footer()
	ok(modal.get_footer() == null, "PaperModal clear_footer")
	await process_frame
	var count := 0
	for child in modal.get_column().get_children():
		if child is HFlowContainer:
			count += 1
	ok(count == 0, "PaperModal footer node removed")
	modal.queue_free()
	await process_frame

func check_workbench() -> void:
	var bench := spawn(UI + "Workbench/UIWorkbench.tscn")
	await process_frame
	var pages: Node = bench.get_node("Pages")
	ok(pages.get_child_count() >= 18, "Workbench has module pages")
	for page in pages.get_children():
		ok(page.get_node("Viewport").get_child_count() > 0, "Workbench page populated: " + page.name)
		ok(not page.name.begins_with("一章") and not page.name.begins_with("二章"), "Workbench has no per-chapter page: " + page.name)
	var raw := FileAccess.get_file_as_string("res://scenes/UI/Workbench/UIWorkbench.tscn")
	ok(not raw.contains("scenes/Awakening/") and not raw.contains("scenes/Chapter2/"), "Workbench references no chapter folders")
	bench.queue_free()
	await process_frame
