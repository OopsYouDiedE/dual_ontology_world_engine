extends SceneTree

const OUT := "res://../artifacts/first-awakening/"
var scene: Node2D
var failures: Array[String] = []
var checks := 0

func _initialize() -> void:
	call_deferred("run")

func require(ok: bool, message: String) -> void:
	checks += 1
	if not ok:
		failures.append(message)
		push_error(message)

func settle() -> void:
	for i in 5: await process_frame

func press(caption: String) -> void:
	for node in scene.modal.find_children("*","Button",true,false):
		if (TranslationServer.translate(node.text) == caption or TranslationServer.translate(node.tooltip_text) == caption) and node.is_visible_in_tree():
			require(not node.disabled,"button enabled: "+caption)
			node.pressed.emit()
			await settle()
			return
	require(false,"button exists: "+caption)

func shot(name: String) -> void:
	if DisplayServer.get_name() == "headless": return
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(OUT+name+".png")

func editor() -> TextEdit:
	return scene.modal.find_child("DraftEditor",true,false)

func set_drafts(pages: Dictionary) -> void:
	scene.mind_drafts = pages.duplicate(true)
	editor().text = scene.mind_drafts.get(scene.mind_person, "")
	editor().text_changed.emit()

func run() -> void:
	TranslationServer.set_locale("zh_CN")
	scene = load("res://scenes/FirstAwakening/FirstAwakening.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	await settle()
	scene.dialogue.active = false
	scene.dialogue.panel.hide()
	scene._open_flow()
	await settle()
	require(scene.modal.find_child("MindNotebook",true,false) != null,"authored notebook scene is the runtime UI")
	require(editor().text.is_empty(),"no answer is prefilled")
	await shot("paper-empty")
	editor().grab_focus()
	var tab_event := InputEventKey.new()
	tab_event.keycode = KEY_TAB
	tab_event.pressed = true
	editor()._gui_input(tab_event)
	await settle()
	require(editor().text == "走到隔离门。","Tab completes example hypothesis when empty")
	editor().text = ""
	editor().text_changed.emit()
	scene.mind_dirty = false
	await settle()
	var heading: HBoxContainer = scene.modal.find_child("Heading", true, false)
	heading.get_node("NextPerson").pressed.emit()
	await settle()
	require(scene.mind_person == "工程师", "NextPerson black triangle switches character")
	heading.get_node("PrevPerson").pressed.emit()
	await settle()
	require(scene.mind_person == "侦探", "PrevPerson black triangle returns character")
	scene.mind_dirty = false
	await press("词汇")
	await shot("paper-words")
	await press("动作")
	require(editor().text.is_empty() and not scene.mind_dirty,"browsing people never edits the draft")
	await press("人物")
	await press("侦探")
	await press("动作")
	await press("用纸包起")
	await press("对象")
	await press("木楔")
	require(editor().text == "侦探用纸包起木楔" and editor().has_focus(),"vocabulary inserts at caret and restores focus")
	await press("句式")
	await shot("paper-conditions")
	await press("收起")
	set_drafts({
		"侦探":"走到隔离门。\n侦探看见热烟后，撤下木楔。\n用纸包起木楔。",
		"工程师":"走到隔离门。\n工程师看见木楔撤下后，插入生活翼内销。",
		"厨师":"走到隔离门。\n厨师看见止挡座移开后，用湿布擦拭地面。",
		"音乐家":"用木楔撑开隔离门。\n音乐家看见热烟后，移开铁止挡座。",
		"门外的人":"门外的人看见隔离门关闭后，落下主楼侧横闩。"
	})
	await settle()
	if "--layout-only" in OS.get_cmdline_user_args():
		await shot("paper-written")
		finish()
		return
	await press("从头重演")
	require(not scene.sim_result.is_empty(),"written hypothesis runs Core")
	require(scene.replay_states.size() == scene.replay_events.size()+1,"each replay frame has an authoritative state")
	for i in scene.replay_events.size(): scene._replay_step(1)
	await settle()
	require(scene.sim_result.ids.has("paper-inner-transfer") and scene.sim_result.conflicts.is_empty(),"valid causal chain produces compatible traces")
	require(not scene.replay_stage.wedge.visible,"removed wedge is absent in playback")
	await shot("paper-result")
	var old_result: Dictionary = scene.sim_result.duplicate(true)
	editor().text += "\n音乐家没有走到门边观察。"
	editor().text_changed.emit()
	await settle()
	require(scene.mind_dirty and not scene.replay_playing and scene.sim_result == old_result,"invalid edit keeps previous replay explicitly stale")
	require(scene.replay_status.text.contains("旧重演"),"old run is identified on paper")
	await shot("paper-error")
	var saved: String = editor().text
	await press("返回现场")
	scene._open_flow()
	await settle()
	require(editor().text == saved,"unsubmitted draft survives returning to field")
	# A legal but physically blocked action must be visible as failure, never a success animation.
	set_drafts({"音乐家":"用木楔撑开隔离门。", "门外的人":"落下主楼侧横闩。"})
	await settle()
	await press("从头重演")
	for i in scene.replay_events.size(): scene._replay_step(1)
	await settle()
	require(not scene.sim_result.outer_bar,"outer bar cannot lock an open door")
	require(not scene.replay_states.back().succeeded,"blocked physical action is recorded")
	require(not scene.replay_stage.outside_bar.visible,"blocked outer bar is not drawn as locked")
	await shot("paper-blocked")
	# Returning to before a trace appeared cannot verify a future state.
	set_drafts({"音乐家":"用木楔撑开隔离门。", "侦探":"走到隔离门。\n侦探看见热烟后，撤下木楔。"})
	await settle()
	await press("从头重演")
	for i in scene.replay_events.size(): scene._replay_step(1)
	scene.evidence["trace"] = true
	scene._replay_step(-scene.replay_events.size())
	scene._request_trace_check("门底遮蔽","door-mask","trace")
	require(scene.checks_this_run == 0,"rewinding prevents verification of a future trace")
	set_drafts({"厨师":"用木楔撑开隔离门。", "工程师":"走到隔离门。", "侦探":"走到隔离门。\n侦探看见隔离门打开后，撤下木楔。"})
	await press("从头重演")
	for i in scene.replay_events.size(): scene._replay_step(1)
	await settle()
	require(scene.sim_result.closed and not scene.sim_result.braced and scene.sim_result.failures.is_empty(),"composed people, verbs, objects and a visible condition execute")
	await shot("paper-composed")
	set_drafts({"工程师":"拧开水阀。"})
	await press("从头重演")
	scene._replay_step(1)
	await settle()
	require(scene.replay_stage.water_stream.visible,"water flow is drawn from the current replay state")
	scene._replay_step(-1)
	await settle()
	require(not scene.replay_stage.water_stream.visible,"rewind removes the later water flow")
	set_drafts({"侦探":"插入内销。拔出内销。", "门外的人":"落下外闩。"})
	await press("从头重演")
	for i in scene.replay_events.size(): scene._replay_step(1)
	require(not scene._trace_visible("inner-bolt"),"withdrawing a bolt prevents checking a historical double-lock as current")
	finish()

func finish() -> void:
	var report := {"checks":checks,"failures":failures,"rendered":DisplayServer.get_name() != "headless"}
	FileAccess.open(OUT+"paper-notebook-check.json",FileAccess.WRITE).store_string(JSON.stringify(report,"\t"))
	print("PAPER_NOTEBOOK ",JSON.stringify(report))
	quit(0 if failures.is_empty() else 1)
