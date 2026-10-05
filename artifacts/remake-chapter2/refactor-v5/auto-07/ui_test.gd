extends SceneTree
var game: Node
var commands: Array[String] = []
var checks := {}
const OUT = "C:/Users/iii/Desktop/dual_ontology_world_engine/artifacts/remake-chapter2/refactor-v5/auto-07/"
func _initialize():
	call_deferred("run")
func wait_ready():
	for i in 1800:
		await process_frame
		if not game.busy and game.bridge.IsWaiting():
			return true
	assert(false, "command timed out")
	return false
func command(text: String, info := false):
	commands.append(text)
	game.close_modal()
	game.send(text, info)
	await wait_ready()
	game.dialogue.hide()
func labels(node: Node, results: Array):
	for child in node.get_children():
		if child is RichTextLabel:
			results.append({"text": child.get_parsed_text(), "size": [child.size.x, child.size.y], "visible": child.is_visible_in_tree()})
		labels(child, results)
func capture(name: String):
	await process_frame
	await process_frame
	await RenderingServer.frame_post_draw
	get_root().get_texture().get_image().save_png(OUT + name + ".png")
	var result: Array = []
	labels(game.modal_root, result)
	checks[name] = result
func run():
	game = load("res://scenes/Chapter2/Game/Chapter2Game.tscn").instantiate()
	root.add_child(game)
	await wait_ready()
	game.dialogue.hide()
	checks["dialogue_canvas_hidden"] = not game.dialogue.visible
	game.info_title = "笔记"
	await command("笔记", true)
	await capture("notes")
	game.info_title = "帮助"
	await command("帮助", true)
	await capture("help")
	checks["help_texture_absent"] = game.card_texture == null
	game.close_modal()
	var found := false
	for id in game.sites:
		if game.sites[id].has("thing"):
			commands.append("查 " + str(game.sites[id].thing))
			game.complete_click_interaction(id)
			await wait_ready()
			game.dialogue.hide()
			checks["object_title"] = game.info_title
			checks["object_has_texture"] = game.card_texture != null
			await capture("object_card")
			found = true
			break
	assert(found)
	while int(game.state.day) == 2:
		await command("守夜 后勤院" if int(game.state.slot) == 5 else "歇")
	await command("查 粮仓")
	await command("清晨", true)
	checks["day"] = game.state.day
	checks["replay_options"] = game.state.replay_options
	assert(not game.state.replay_options.is_empty(), "no real replay options after stakeout and granary")
	game.open_manuscript()
	await process_frame
	game.manuscript._choose(0, 1)
	game.run_replay(game.manuscript.selection, true)
	commands.append("重演 第%d夜 1" % int(game.state.replay_night))
	await wait_ready()
	game.dialogue.hide()
	await capture("replay")
	checks["raw_replay"] = game.state.replay
	var card_id := ""
	for finding in game.replay_result.findings:
		if finding.status != "unvisited":
			card_id = finding.id
			break
	assert(card_id != "")
	game.open_card(card_id)
	await capture("replay_card")
	checks["replay_card_original"] = game.replay_evidence[card_id].text
	game.open_manuscript()
	await process_frame
	game.manuscript._choose(0, 0)
	await capture("replay_stale")
	checks["stale"] = game.manuscript.stale_label.get_parsed_text()
	game.bridge._ExitTree()
	for i in 600:
		await process_frame
		if game.bridge.IsDone():
			break
	await process_frame
	FileAccess.open(OUT + "inputs.txt", FileAccess.WRITE).store_string("\n".join(commands) + "\n")
	FileAccess.open(OUT + "visible_output.txt", FileAccess.WRITE).store_string("\n".join(game.visible_output) + "\n")
	FileAccess.open(OUT + "checks.json", FileAccess.WRITE).store_string(JSON.stringify(checks, "\t"))
	print("AUTO07_DONE ", checks.day, " options=", checks.replay_options.size())
	quit()
