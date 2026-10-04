extends SceneTree

var host: Node
var report: Array[String] = []
var folder := "res://../artifacts/remake-chapter2/shared-runtime/"

func _initialize() -> void:
	call_deferred("run")

func note(text: String) -> void:
	report.append(text)
	print("SEMANTIC " + text)
	var file := FileAccess.open(ProjectSettings.globalize_path(folder + "semantic-host-validation.md"), FileAccess.WRITE)
	file.store_string("# Independent local Godot semantic validation\n\n" + "\n\n".join(report) + "\n")
	file.close()

func check(value: bool, text: String) -> void:
	note(("PASS " if value else "FAIL ") + text)

func tick() -> void:
	await process_frame
	if is_instance_valid(host) and is_instance_valid(host.dialogue) and host.dialogue.active:
		host.dialogue.reveal()
		host.dialogue.advance()

func settled() -> void:
	var until := Time.get_ticks_msec() + 120000
	while Time.get_ticks_msec() < until:
		await tick()
		if not host.busy and not host.action_playing and not host.dialogue.active:
			await create_timer(0.12).timeout
			return
	note("FAIL settle timeout mode=" + String(host.state.get("mode", "?")))

func command(text: String) -> void:
	note("INPUT " + text)
	host.close_modal()
	host.send(text)
	await settled()
	note("STATE day=%s slot=%s mode=%s questions=%s" % [host.state.day, host.state.slot, host.state.mode, host.state.get("replay_questions", []).size()])

func shot(name: String) -> void:
	await process_frame
	await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(ProjectSettings.globalize_path(folder + "semantic-c2-" + name + ".png"))

func texts(node: Node) -> String:
	var out := ""
	if node is Label or node is RichTextLabel or node is Button:
		out += String(node.text) + "\n"
	for child in node.get_children():
		out += texts(child)
	return out

func buttons(node: Node) -> Array:
	var out := []
	if node is Button:
		out.append(node)
	for child in node.get_children():
		out.append_array(buttons(child))
	return out

func reality() -> String:
	var d := {}
	for key in ["day", "slot", "det_place", "room", "news", "notebook_cards", "frames_revision", "frames"]:
		d[key] = host.state[key]
	return JSON.stringify(d)

func run() -> void:
	note("Automatic local host self-check, not blind play. Default C2 scene; original bridge commands and entity callbacks; no injected candidates, truth or world state.")
	host = load("res://scenes/Chapter2/Game/Chapter2Game.tscn").instantiate()
	root.add_child(host)
	await settled()
	if OS.get_cmdline_user_args().has("--diff"):
		await replay_diff()
		return
	if OS.get_cmdline_user_args().has("--resume"):
		note("Previous process ended during user interruption, with input through D2-night stakeout retained in tmp/semantic-host-interrupted.out. Repeating only original actions needed to recreate the candidate state; earlier completed HUD/menu/empty replay checks and screenshots are retained, not repeated. User interruption is not a game failure.")
	else:
		await initial_checks()
	await continue_checks()

func raw(text: String) -> void:
	host.bridge.Send(text)
	while not host.bridge.IsWaiting():
		await process_frame
	host.bridge.TakeOutput()
	host.state = host.bridge.Snapshot()

func replay_diff() -> void:
	note("Diagnostic only: repeat original bridge commands without replaying already verified host action animations. This is not play validation.")
	host.set_process(false)
	for i in 5:
		await raw("歇")
	for text in ["守夜 后勤院", "go 后勤院", "查 台账", "go 诊疗所", "查 碗"]:
		await raw(text)
	host.last_slot_key = "%d-%d" % [host.state.day, host.state.slot]
	host.open_manuscript()
	var before: Dictionary = host.state.duplicate(true)
	var selection := []
	for group in host.state.replay_questions:
		selection.append(0)
	selection[0] = 1
	host.replay_ui_run(selection, true)
	while not host.bridge.IsWaiting():
		await process_frame
	host._process(0)
	await process_frame
	for key in ["day", "slot", "det_place", "room", "news", "notebook_cards", "frames_revision", "frames"]:
		var equal: bool = JSON.stringify(before[key]) == JSON.stringify(host.state[key])
		note("DIFF " + key + "=" + str(equal))
		if not equal:
			note("BEFORE " + key + "=" + JSON.stringify(before[key]))
			note("AFTER " + key + "=" + JSON.stringify(host.state[key]))
	quit()

func initial_checks() -> void:
	check(not host.state.is_empty(), "default scene initializes in real rendered window")
	check(not host.has_node("Console") and not host.has_node("CommandLine"), "no console node")
	await shot("hud")
	host.busy = true
	host._refresh_hud()
	check(not host.hud_keys.visible, "busy HUD hides keyboard hints")
	host.busy = false
	host._refresh_hud()
	var before := reality()
	for code in [KEY_SLASH, KEY_J]:
		var event := InputEventKey.new()
		event.pressed = true
		event.keycode = code
		host._unhandled_input(event)
	check(host.modal_kind == "" and reality() == before, "slash and J have no console/menu entry")
	host._open_actions()
	var menu := texts(host.modal_root)
	check(not menu.contains("输入") and not menu.contains("传送") and not menu.contains("歇") and not menu.contains("物件列表"), "Q menu contains scene actions only")
	note("Q labels=" + menu.replace("\n", " | "))
	await shot("menu")
	host.close_modal()
	host.open_manuscript()
	check(host.manuscript == null and texts(host.modal_root).contains("先走到现场细看"), "empty replay offers investigation guidance without three empty slots")
	await shot("replay-empty")
	host.close_modal()

func continue_checks() -> void:
	for i in 5:
		await command("歇")
	await command("守夜 后勤院")
	await command("go 后勤院")
	host._build_sites()
	check(host.sites.has("ledger") and host.sites.ledger.get("thing", "") == "台账", "original ledger entity binds original inspection")
	host.complete_click_interaction("ledger")
	await settled()
	check(host.modal_kind == "card", "inspection completes original dialogue then opens stored inspection card")
	note("Inspection visible=" + texts(host.modal_root).replace("\n", " | "))
	await shot("card")
	host.close_modal()
	await command("go 诊疗所")
	host._inspect("碗")
	await settled()
	host.close_modal()
	host.open_notebook()
	check(host.modal_kind == "notebook" and buttons(host.modal_root).size() > 1, "Tab notebook has grouped clickable known records")
	await shot("notebook")
	var card_before := reality()
	var cards: Array = host.state.notebook_cards
	var inspection := ""
	for card in cards:
		if String(card.id).begins_with("inspection:"):
			inspection = String(card.id)
			break
	host.open_record_card(inspection)
	check(texts(host.modal_root).contains("来源：") and reality() == card_before, "fact card includes source and opening it does not refresh world truth")
	host.close_modal()
	host.open_manuscript()
	var groups: Array = host.state.replay_questions
	check(groups.size() > 6 and host.manuscript.option_buttons.size() == groups.size(), "all original eligible groups above six are rendered")
	note("groups=" + str(groups.size()) + " original options=" + str(host.state.replay_options.size()))
	await shot("replay-questions")
	var selection := []
	for group in groups:
		selection.append(0)
	check(not host.replay_ui_ready(selection), "zero hypotheses rejected")
	selection[0] = 1
	check(host.replay_ui_ready(selection), "one original hypothesis accepted")
	for i in mini(3, groups.size()):
		selection[i] = 1
	check(host.replay_ui_ready(selection), "three hypotheses accepted")
	selection[3] = 1
	check(not host.replay_ui_ready(selection), "four hypotheses rejected")
	selection.fill(0)
	selection[0] = 1
	var replay_before := reality()
	host.replay_ui_run(selection, true)
	check(host.busy and host.awaiting_replay, "original UI callback submits asynchronous bridge replay")
	await settled()
	check(host.state.has("replay") and not host.replay_result.is_empty(), "real original replay returned through host async callback")
	check(reality() == replay_before, "replay leaves day slot physical room records news and world frames unchanged")
	note("REPLAY=" + JSON.stringify(host.state.get("replay", {})))
	await shot("result")
	host.close_modal()
	await command("go 生活翼")
	host.load_map("zone_yi", "from_hall")
	await settled()
	host._build_sites()
	check(host.state.room.place == "DormLife", "zone_yi uses C# DormLife mapping")
	var rests := 0
	for id in host.sites:
		if String(id).begins_with("rest:"):
			rests += 1
	check(rests > 0, "daytime physical chairs expose rest action")
	await command("贴 生活翼")
	for cell in 5:
		check(host.state.mode == "card" and int(host.state.card_cell) == cell, "ReadCard original single-cell prompt " + str(cell))
		var value: String = host.state.cells[cell][0]
		await command(value)
	check(host.state.mode == "yesno" and texts(host.modal_root).contains("就这么说吗"), "original ReadCard five selections reach yes/no confirmation")
	await command("否")
	var file := FileAccess.open(ProjectSettings.globalize_path(folder + "semantic-host-validation.md"), FileAccess.WRITE)
	file.store_string("# Independent local Godot semantic validation\n\n" + "\n\n".join(report) + "\n")
	file.close()
	quit()
