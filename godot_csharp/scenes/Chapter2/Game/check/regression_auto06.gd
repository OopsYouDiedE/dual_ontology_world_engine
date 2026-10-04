extends SceneTree

const OUT := "C:/Users/iii/Desktop/dual_ontology_world_engine/artifacts/remake-chapter2/refactor-v5/auto-06"
var game: Node
var commands: Array[String] = []
var checks: Array = []

func _initialize() -> void:
	call_deferred("run")

func persist() -> void:
	DirAccess.make_dir_recursive_absolute(OUT)
	var f := FileAccess.open(OUT + "/commands.txt", FileAccess.WRITE)
	f.store_string("\n".join(commands) + "\n")
	if game:
		f = FileAccess.open(OUT + "/visible_output.txt", FileAccess.WRITE)
		f.store_string("\n".join(game.visible_output) + "\n")
	f = FileAccess.open(OUT + "/checks.json", FileAccess.WRITE)
	f.store_string(JSON.stringify(checks, "  "))

func require(ok: bool, reason: String) -> bool:
	if not ok:
		checks.append({"failure": reason})
		persist()
		push_error("FAIL: " + reason)
		quit(1)
	return ok

func settle() -> void:
	for i in 10:
		await process_frame
	await RenderingServer.frame_post_draw

func ready() -> bool:
	var deadline := Time.get_ticks_msec() + 12000
	while game.busy and Time.get_ticks_msec() < deadline:
		await process_frame
	await settle()
	return require(not game.busy, "bridge timeout")

func command(line: String, title := "笔记本") -> bool:
	game.close_modal()
	game.card_texture = null
	game.info_title = title
	commands.append(line)
	game.send(line, true)
	return await ready()

func labels(node: Node) -> Array:
	var found: Array = []
	if node is RichTextLabel:
		found.append(node)
	for child in node.get_children():
		found.append_array(labels(child))
	return found

func capture(name: String, expected: Array) -> bool:
	await settle()
	var rows: Array = []
	var text: Array[String] = []
	for label in labels(game.modal_root):
		var parsed: String = label.get_parsed_text()
		text.append(parsed)
		rows.append({"text": parsed, "size": [label.size.x, label.size.y], "position": [label.global_position.x, label.global_position.y]})
		if not parsed.strip_edges().is_empty():
			if not require(label.size.x > 0 and label.size.y > 0, name + " nonempty label has zero size"):
				return false
	for line in expected:
		if not String(line).strip_edges().is_empty():
			if not require(text.has(String(line)), name + " missing original line: " + String(line)):
				return false
	checks.append({"capture": name, "labels": rows})
	root.get_texture().get_image().save_png(OUT + "/" + name + ".png")
	persist()
	return true

func run() -> void:
	DirAccess.make_dir_recursive_absolute(OUT)
	game = load("res://scenes/Chapter2/Game/Chapter2Game.tscn").instantiate()
	root.add_child(game)
	if not await ready(): return
	if not await command("笔记"): return
	if not await capture("notebook", Array(game.info_lines)): return
	if not await command("查 中央厅公告", "中央厅公告"): return
	if not await capture("inspect-no-image", Array(game.info_lines)): return
	if not await command("帮助", "帮助"): return
	if not await capture("help", Array(game.info_lines)): return
	game.close_modal()
	var picked := ""
	for id in game.sites:
		if game.sites[id].get("thing", "") == "中央厅公告":
			picked = id
			break
	if not require(not picked.is_empty(), "central board site absent"): return
	commands.append("查 中央厅公告")
	game.complete_click_interaction(picked)
	if not await ready(): return
	if not require(game.card_texture != null, "real object card missing source texture"): return
	checks.append({"source_texture": game.card_texture.resource_path})
	if not await capture("object-image-card", Array(game.info_lines)): return
	while int(game.state.day) < 3:
		if not await command("歇"): return
	if not await command("查 粮仓", "粮仓"): return
	game.close_modal()
	game.open_manuscript()
	await settle()
	game.manuscript._choose(0, 1)
	commands.append("重演 第%d夜 1" % int(game.state.replay_night))
	game.manuscript._run(true)
	if not await ready(): return
	if not require(not game.replay_result.is_empty(), "real replay has no result"): return
	for finding in game.replay_result.findings:
		var row_name: String = game.replay_evidence[finding.id].name
		if not require(not row_name.begins_with("吻合") and not row_name.begins_with("未查") and not row_name.begins_with("冲突"), "duplicated replay status in evidence name"): return
	if not await capture("replay-results", []): return
	var card_id := ""
	for finding in game.replay_result.findings:
		if finding.status != "unvisited":
			card_id = str(finding.id)
			break
	if not require(not card_id.is_empty(), "no visited replay evidence"): return
	var original: String = game.replay_evidence[card_id].text
	game.open_card(card_id)
	if not await capture("replay-evidence-card", [original]): return
	game.open_manuscript()
	await settle()
	game.manuscript._choose(0, 0)
	if not await capture("replay-stale", ["变量已改动：下面是上一次重演的结果，已过期。"]) : return
	checks.append({"pass": true, "day": game.state.day, "commands": commands.size()})
	persist()
	print("PASS auto06 commands=", commands.size(), " output=", game.visible_output.size())
	quit(0)
