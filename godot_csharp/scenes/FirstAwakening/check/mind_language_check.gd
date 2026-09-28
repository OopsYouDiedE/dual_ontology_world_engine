extends SceneTree

const Language = preload("res://scenes/FirstAwakening/MindLanguage.gd")
const DraftEditor = preload("res://scenes/FirstAwakening/MindDraftEditor.gd")
const SITES := {
	"door":{"title":"隔离门与内销"}, "paper":{"title":"废纸篓里的包纸"},
	"stop":{"title":"墙边的铁止挡座"}, "water":{"title":"水阀"},
	"window_0":{"title":"铁格雾窗"}, "window_1":{"title":"铁格雾窗"},
	"bed_0":{"title":"整理过的寝具"}, "bed_1":{"title":"整理过的寝具"},
	"room_0":{"title":"R-05 门牌"}, "engineer":{"title":"工程师"}
}
var failures: Array[String] = []
var checks := 0

func require(ok: bool, message: String) -> void:
	checks += 1
	if not ok: failures.append(message)

func accepts(text: String, expected: Array) -> void:
	var result := Language.compile_text(text, SITES)
	require(result.get("codes", []) == expected and not result.has("error"), "parse: " + text + " => " + str(result))

func rejects(text: String, line := 0) -> void:
	var result := Language.compile_text(text, SITES)
	require(result.has("error") and result.get("line", -1) == line and not result.has("codes") and not result.has("sentences"), "reject whole draft: " + text)

func _initialize() -> void:
	var catalog := Language.get_catalog(SITES)
	require(catalog.targets.size() == SITES.size() + Language.PARTS.size(), "all sites plus explicit parts")
	# Every actor can attempt every verb on every available target. Wrong tools or
	# targets must reach world execution, never be removed by occupational grammar.
	for person in catalog.people:
		for verb in catalog.verbs:
			for target in catalog.targets:
				var expected := ["ACT|%s|%s|%s" % [person, verb.code, target.key]]
				accepts(person + verb.word + target.word, expected)
			for alias in verb.aliases:
				accepts(person + alias + "水阀", ["ACT|%s|%s|water" % [person, verb.code]])
		for target in catalog.targets:
			for alias in target.aliases:
				accepts(person + "观察" + alias, ["ACT|%s|Inspect|%s" % [person, target.key]])
		for phrase in catalog.phrases:
			accepts(person + phrase.word, ["ACT|%s|%s|%s" % [person, phrase.code, phrase.target]])
	for prefix in ["不", "不要", "没有", "还未", "别"]:
		rejects("侦探" + prefix + "移开水阀")
	for text in ["也许侦探移开水阀", "侦探移开水阀再等等", "侦探移开水阀并且侦探观察水阀", "侦探观察铁格雾窗", "侦探观察不存在的物件", "侦探消灭水阀", "陌生人观察水阀", "ACT|侦探|Inspect|water"]:
		rejects(text)
	for condition in catalog.conditions:
		var observer: String = ""
		for person in catalog.people:
			if condition.begins_with(person): observer = person
		var matching := {}
		for form in catalog.condition_forms:
			for suffix in [form.word] + form.aliases:
				if condition == observer + suffix: matching = form
		var expected := ["WHEN|%s|%s|%s" % [observer, matching.code, matching.target], "ACT|厨师|RemoveWedge|wedge"]
		accepts(condition + "厨师撤下木楔", expected)
		for order in catalog.order_words:
			accepts(order + condition + "厨师撤下木楔", expected)
			accepts(condition + order + "厨师撤下木楔", expected)
			rejects(order + condition + order + "厨师撤下木楔")
	accepts(" 侦探 把 水阀 移开。工程师将生活翼内销插入；音乐家观察R-05的铁格雾窗然后厨师观察R-06铁格雾窗", ["ACT|侦探|MoveStop|water", "ACT|工程师|SlideInnerBolt|inner_bolt", "ACT|音乐家|Inspect|window_0", "ACT|厨师|Inspect|window_1"])
	accepts("# 私人笔记\n＃ 自由批注\n\n音乐家走到门边观察.\r\n随后,音乐家看见热烟后,侦探撤下门底的木楔;然后，厨师用纸包起木楔", ["ACT|音乐家|GoTo|door", "WHEN|音乐家|Smoke|door", "ACT|侦探|RemoveWedge|wedge", "ACT|厨师|WrapWedge|wedge"])
	accepts("# 仅笔记\n\n", [])
	# Shared predicates preserve the real target. The bridge routes these natural
	# combinations to the matching physical rule, not to an occupational recipe.
	accepts("厨师拔出内销；工程师擦拭R-05的窗户；音乐家关闭水阀", ["ACT|厨师|RemoveWedge|inner_bolt", "ACT|工程师|WipeFloor|window_0", "ACT|音乐家|CloseDoor|water"])
	rejects("音乐家走到门边观察。\n# 笔记\n\n侦探召唤幽灵。", 3)
	rejects("侦探观察水阀；工程师做未知动作")
	rejects("侦探观察水阀 # 尾注")
	require(Language.words("ACT|厨师|Inspect|water", SITES) == "厨师观察水阀", "human description")
	var titled := Language.compile_text("观察水阀；音乐家走到隔离门。\n厨师看见内销拔出后，插入内销", SITES, "工程师")
	require(titled.get("codes", []) == ["ACT|工程师|Inspect|water", "ACT|音乐家|GoTo|door", "WHEN|厨师|InnerBoltWithdrawn|inner_bolt", "ACT|工程师|SlideInnerBolt|inner_bolt"], "default person does not replace explicit actor or observer")
	require(titled.get("sentences", []).size() == 3 and titled.sentences[2].line == 1, "default person preserves lines")
	for draft in ["观察水阀", "音乐家观察水阀", ""]:
		var invalid := Language.compile_text(draft, SITES, "陌生人")
		require(invalid.has("error") and not invalid.has("codes"), "invalid heading refuses entire draft")
	var invalid_line := Language.compile_text("观察水阀\n做未知动作", SITES, "侦探")
	require(invalid_line.get("line", -1) == 1 and not invalid_line.has("codes"), "titled draft atomic error location")
	rejects("观察水阀")
	var explicit_unknown := Language.compile_text("陌生人观察水阀", SITES, "侦探")
	require(explicit_unknown.has("error"), "default actor does not swallow unknown explicit people")
	call_deferred("_check_editor")

func _press_tab(backwards := false) -> void:
	var event := InputEventKey.new()
	event.keycode = KEY_TAB
	event.physical_keycode = KEY_TAB
	event.pressed = true
	event.shift_pressed = backwards
	Input.parse_input_event(event)
	await process_frame
	event = event.duplicate()
	event.pressed = false
	Input.parse_input_event(event)
	await process_frame

func _check_editor() -> void:
	var canvas := Control.new()
	root.add_child(canvas)
	var previous := Button.new()
	previous.name = "Previous"
	previous.text = "之前"
	canvas.add_child(previous)
	var editor := DraftEditor.new()
	editor.name = "Draft"
	editor.position = Vector2(0, 40)
	editor.size = Vector2(640, 360)
	canvas.add_child(editor)
	var next := Button.new()
	next.name = "Next"
	next.text = "之后"
	next.position = Vector2(0, 410)
	canvas.add_child(next)
	editor.focus_next = NodePath("../Next")
	editor.focus_previous = NodePath("../Previous")
	editor.configure(Language.get_catalog(SITES), "侦探")
	await process_frame
	editor.grab_focus()
	require(editor.text.is_empty() and editor.placeholder_text == "走到隔离门。", "ghost is not saved draft data")
	await _press_tab()
	require(editor.text == "走到隔离门。" and editor.has_focus(), "Tab inserts real empty-page example")
	editor.text = "走到隔"
	editor.set_caret_column(editor.text.length())
	require(editor.get_completion_suffix() == "离门", "only shared lexical prefix, no invented full rule")
	await _press_tab()
	require(editor.text == "走到隔离门", "Tab accepts lexical suffix")
	editor.text = "侦探观"
	editor.set_caret_column(editor.text.length())
	require(editor.get_completion_suffix() == "察", "explicit actor plus lexical token")
	editor.text = "侦探观察R-05的窗"
	editor.set_caret_column(editor.text.length())
	require(editor.get_completion_suffix() == "户", "site alias completion")
	editor.text = "走"
	editor.set_caret_column(1)
	require(editor.get_completion_suffix().is_empty(), "ambiguous verbs never select a plan")
	await _press_tab()
	require(next.has_focus() and editor.text == "走", "no candidate uses normal Tab focus")
	editor.grab_focus()
	await _press_tab(true)
	require(previous.has_focus() and editor.text == "走", "ShiftTab always moves focus back")
	editor.grab_focus()
	editor.text = "侦探观"
	editor.set_caret_column(2)
	require(editor.get_completion_suffix().is_empty(), "no ghost in middle of text")
	editor.set_caret_column(3)
	editor.select_all()
	require(editor.get_completion_suffix().is_empty(), "no ghost replaces selection")
	editor.deselect()
	editor.text = "# 侦探观"
	editor.set_caret_column(editor.text.length())
	require(editor.get_completion_suffix().is_empty(), "no ghost in notes")
	editor.text = ""
	editor.configure(Language.get_catalog(SITES), "陌生人")
	require(editor.get_completion_suffix().is_empty(), "invalid heading has no example insertion")
	canvas.queue_free()
	await process_frame
	for failure in failures: push_error(failure)
	print("MIND LANGUAGE: %d checks, %d failures" % [checks, failures.size()])
	quit(0 if failures.is_empty() else 1)
