extends SceneTree
## Independent automatic UI checks; injected progress is explicitly not blind play.
var game: Node
var holder: Node
var rows := []
var failed := 0
var out := "C:/Users/iii/Desktop/dual_ontology_world_engine/artifacts/remake-chapter2/shared-runtime"
func _initialize(): call_deferred("run")
func check(ok: bool, label: String):
	rows.append({"ok":ok,"label":label})
	if not ok: failed += 1
	print(("PASS " if ok else "FAIL ") + label)
	if not ok and is_instance_valid(game) and game.get("bridge") != null:
		print("FAIL_STATE ", JSON.stringify({"busy":game.busy,"actions":game.action_playing,"bridge_waiting":game.bridge.IsWaiting(),"dialogue":game.dialogue.active,"modal":game.modal_kind,"mode":game.state.get("mode",""),"day":game.state.get("day",0),"slot":game.state.get("slot",0),"output":game.visible_output}))
func pause(t := 0.15): await create_timer(t).timeout
func key(k):
	for pressed in [true,false]:
		var e := InputEventKey.new()
		e.keycode = k
		e.pressed = pressed
		root.push_input(e)
func click(b: Button):
	var p := b.get_global_rect().get_center()
	for pressed in [true,false]:
		var e := InputEventMouseButton.new()
		e.position = p
		e.button_index = MOUSE_BUTTON_LEFT
		e.pressed = pressed
		root.push_input(e)
func nodes(n: Node, kind: String) -> Array:
	var found := []
	if n.is_class(kind): found.append(n)
	for child in n.get_children(): found.append_array(nodes(child, kind))
	return found
func texts(n: Node) -> String:
	if not is_instance_valid(n): return ""
	var result := ""
	if n is Label or n is RichTextLabel or n is Button: result += str(n.text) + "\n"
	for child in n.get_children(): result += texts(child)
	return result
func shot(name: String):
	await pause()
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(out + "/unified-" + name + ".png")
func idle(c2 := false) -> bool:
	return not game.busy and not game.action_playing and game.bridge.IsWaiting() and not game.dialogue.active and game.modal_kind == "" if c2 else not game.dialogue.active and not game.story_lock and game.modal_kind == ""
func drain(c2 := false, seconds := 60.0) -> bool:
	var deadline := Time.get_ticks_msec() + int(seconds * 1000)
	var stable := 0
	while Time.get_ticks_msec() < deadline:
		await pause(0.06)
		if idle(c2):
			stable += 1
			if stable >= 5: return true
		else: stable = 0
		if game.modal_kind == "name":
			game._name_field.text = "独立UI验收"
			game._confirm_name()
		elif game.modal_kind == "story_image": key(KEY_SPACE)
		elif c2 and game.modal_kind == "menu":
			var options := nodes(game.modal_root,"Button")
			if not options.is_empty(): click(options[0])
		elif game.dialogue.active and not game.dialogue.busy:
			var d = game.dialogue
			if d.body.visible_characters >= 0: d.reveal()
			elif d.line != null:
				var choices: Array = d.line.responses.filter(func(r): return r.is_allowed)
				if choices.is_empty(): d.advance()
				else: d.choose(str(choices[0].next_id))
	return false
func until_modal(kind: String):
	var deadline := Time.get_ticks_msec() + 90000
	while game.modal_kind != kind and Time.get_ticks_msec() < deadline:
		await pause(0.06)
		if game.dialogue.active and not game.dialogue.busy:
			if game.dialogue.body.visible_characters >= 0: game.dialogue.reveal()
			else: game.dialogue.advance()
func fresh(path: String, chapter1 := false, skip := false):
	if is_instance_valid(holder):
		holder.queue_free()
		await pause(0.4)
	holder = load(path).instantiate()
	game = holder.get_node("Awakening") if chapter1 else holder
	if skip: game.set_meta("skip_intro",true)
	root.add_child(holder)
	current_scene = holder
	await pause(0.6)
func identity():
	check(game.dialogue.scene_file_path == "res://scenes/Shared/UI/ChapterDialogue.tscn", "same shared dialogue scene")
	check(game.ui.scene_file_path == "res://scenes/Shared/UI/FieldUI.tscn", "same shared FieldUI scene")
	check(game.chapter_ui.get_script().resource_path == "res://scenes/Shared/UI/ChapterUI.gd", "same shared ChapterUI controller")
	check(game.modal_kind == "", "default idle has no modal")
func boundaries(c2 := false):
	key(KEY_H)
	await pause()
	check(game.dialogue.history_panel.visible,"H opens original history")
	key(KEY_H)
	await pause()
	check(not game.dialogue.history_panel.visible,"H closes history")
	key(KEY_Q)
	await pause()
	check(game.modal_kind == "menu","Q opens shared action menu")
	await shot("c2-menu" if c2 else "c1-menu")
	key(KEY_ESCAPE)
	await pause()
	check(game.modal_kind == "","ESC closes menu")
func run():
	DirAccess.make_dir_recursive_absolute(out)
	if "--chapter2-only" in OS.get_cmdline_user_args():
		await chapter2()
		return
	await fresh("res://scenes/Main2D.tscn",true,false)
	check(await drain(),"chapter1 default formal scene drains opening")
	if failed: finish(); return
	identity()
	await shot("c1-default-hud")
	await boundaries()
	await fresh("res://scenes/Main2D.tscn",true,true)
	game.jump_to("investigate")
	check(await drain(), "INJECT investigation settles room entry")
	check(game.stage == "investigate", "INJECT chapter1 skip_intro/jump_to investigation")
	key(KEY_R)
	await pause()
	check(game.modal_kind == "", "R gated before replay unlock")
	key(KEY_Q)
	await pause()
	var menu := texts(game.modal_root)
	check(menu.contains("笔记本（Tab）") and not menu.contains("重演手稿（R）"),"Q only available notebook/history/near action")
	key(KEY_ESCAPE)
	await pause()
	game.player.position = game.site_marker("E12") if game.has_method("site_marker") else game.sites.E1.point
	check(await drain(), "INJECT near evidence room entry settles")
	game.nearest = "E12"
	game.dialogue_closed_ms = -100000
	check(game.site_visible("E12") and game.field_active(),"INJECT legal nearby site visible and field active")
	key(KEY_E)
	await until_modal("card")
	check(game.modal_kind == "card", "E opens original nearby evidence card")
	check(texts(game.modal_root).contains(game.engine.CardFacts("E12")), "chapter1 card retains original C# facts")
	await shot("c1-card")
	key(KEY_ESCAPE)
	await pause()
	check(game.modal_kind == "", "card closes via ESC")
	key(KEY_TAB)
	await pause()
	check(game.modal_kind == "notebook" and texts(game.modal_root).contains(game.Content.EVIDENCE.E12.name),"Tab shared notebook preserves collected evidence")
	await shot("c1-notebook")
	key(KEY_TAB)
	await pause()
	check(game.modal_kind == "", "Tab closes notebook")
	game.engine.SetProgress("state","replay_open",true)
	game.engine.SetProgress("recall_marks",2,"huo")
	key(KEY_R)
	await pause()
	check(game.manuscript.get_script().resource_path == "res://scenes/Shared/UI/ReplayManuscript.gd", "chapter1 shared manuscript source")
	check(game.manuscript.option_buttons.size() == 5,"chapter1 sixth variable originally hidden")
	check(texts(game.manuscript).contains("回忆所证"),"chapter1 original recall mark retained")
	check(game.manuscript.run_buttons[0].disabled,"chapter1 ready gate retained")
	await shot("c1-replay-unready")
	for i in game.manuscript.option_buttons.size():
		click(game.manuscript.option_buttons[i][0])
		await pause()
	check(not game.manuscript.run_buttons[1].disabled,"chapter1 real option clicks satisfy gate")
	click(game.manuscript.run_buttons[1])
	await pause()
	await until_modal("replay")
	await pause()
	check(not game.replay_result.is_empty(),"chapter1 real run button calls original C# replay")
	if game.modal_kind != "replay": game.open_manuscript()
	await pause()
	await shot("c1-replay-result")
	click(game.manuscript.option_buttons[0][1])
	await pause()
	check(game.manuscript.stale_label.text.contains("已过期"),"chapter1 stale results retained")
	key(KEY_R)
	await pause()
	check(game.modal_kind == "", "R closes replay")
	check(await drain(),"chapter1 idle before switch")
	await chapter2()
func chapter2():
	await fresh("res://scenes/Chapter2/Game/Chapter2Game.tscn")
	check(await drain(true),"chapter2 default formal scene drains opening")
	if not idle(true): finish(); return
	identity()
	await shot("c2-default-hud")
	await boundaries(true)
	key(KEY_Q)
	await pause()
	var scroll: ScrollContainer = nodes(game.modal_root,"ScrollContainer")[0]
	var qtext := texts(game.modal_root)
	for item in game.state.global_actions: check(qtext.contains(item.label),"Q original global action " + item.label)
	for item in game.INFO: check(qtext.contains("查看：" + item),"Q original info " + item)
	scroll.scroll_vertical = int(scroll.get_v_scroll_bar().max_value)
	await shot("c2-menu-bottom")
	var options := nodes(scroll,"Button")
	var command: Button = options[-1]
	check(command.text == "输入命令（/）" and scroll.get_global_rect().has_point(command.get_global_rect().get_center()),"Q command reachable at scroll bottom")
	click(command)
	await pause()
	check(game.input_line.has_focus() and game.modal_kind == "","Q actual command click focuses input")
	game.input_line.release_focus()
	key(KEY_TAB)
	await pause(0.6)
	check(game.modal_kind == "notebook" and not game.info_lines.is_empty(),"chapter2 Tab shows original command output notebook")
	await shot("c2-notebook")
	key(KEY_TAB)
	await pause()
	var before: String = game.state.det_place
	game._open_place()
	await pause()
	var original: Array = game.state.room.actions.filter(func(a): return a.kind == "inspect")
	if not original.is_empty():
		var target = nodes(game.modal_root,"Button").filter(func(b): return b.text == original[0].label)[0]
		click(target)
		await until_modal("notebook")
		check(game.modal_kind == "notebook","chapter2 original legal room inspect preserves original info notebook")
		check(game.state.det_place == before,"chapter2 inspect preserves original room")
		await shot("c2-card")
		key(KEY_ESCAPE)
		await pause()
	else: check(false,"chapter2 room has original inspect action")
	game._close_modal()
	await pause()
	check(await drain(true,90),"chapter2 original inspect closes then bridge idle")
	var thing_ids: Array = game.sites.keys().filter(func(id): return game.sites[id].has("thing"))
	if not thing_ids.is_empty():
		game.complete_click_interaction(thing_ids[0])
		await until_modal("card")
		check(game.modal_kind == "card" and game.card_texture != null,"chapter2 actual thing path preserves textured shared card")
		await shot("c2-thing-card")
		game._close_modal()
		check(await drain(true,90),"chapter2 thing card closed and idle")
	else: check(false,"chapter2 original thing site available")
	for i in 6:
		if int(game.state.day) >= 3: break
		game.send("睡" if int(game.state.slot) == 5 else "歇")
		check(await drain(true,90),"chapter2 formal advance slot " + str(i))
		if not idle(true): break
	check(int(game.state.day) >= 3,"chapter2 reaches first replay night with formal commands")
	if int(game.state.day) >= 3 and idle(true):
		key(KEY_R)
		await pause()
		check(game.manuscript.get_script().resource_path == "res://scenes/Shared/UI/ReplayManuscript.gd", "chapter2 shared manuscript source")
		check(game.manuscript.run_buttons[0].disabled,"chapter2 all-zero original readiness gate")
		await shot("c2-replay-unready")
		if game.state.replay_options.is_empty():
			print("UNCOVERED chapter2 async replay: original legal replay_options empty; no injected hypothesis")
			key(KEY_R)
			await pause()
			finish()
			return
		click(game.manuscript.option_buttons[0][1])
		await pause()
		check(not game.manuscript.run_buttons[1].disabled,"chapter2 real hypothesis click enables run")
		before = game.state.det_place
		click(game.manuscript.run_buttons[1])
		var deadline := Time.get_ticks_msec() + 90000
		while game.modal_kind != "replay" and Time.get_ticks_msec() < deadline: await pause()
		check(game.modal_kind == "replay" and not game.replay_result.is_empty(),"chapter2 asynchronous bridge result returns shared manuscript")
		check(game.state.det_place == before,"chapter2 replay preserves Core reality location")
		await shot("c2-replay-result")
		click(game.manuscript.option_buttons[0][0])
		await pause()
		check(game.manuscript.stale_label.text.contains("已过期"),"chapter2 stale result retained")
		key(KEY_R)
		await pause()
	finish()
func finish():
	var f := FileAccess.open(out + "/unified-ui-checks.json",FileAccess.WRITE)
	f.store_string(JSON.stringify(rows,"\t"))
	print("UNIFIED_UI_FINISH ",failed," failures / ",rows.size()," assertions")
	quit(1 if failed else 0)
