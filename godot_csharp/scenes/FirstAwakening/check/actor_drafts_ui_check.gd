extends SceneTree

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	TranslationServer.set_locale("zh_CN")
	var scene: Node2D = load("res://scenes/FirstAwakening/FirstAwakening.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	await process_frame
	var presentation: Script = load("res://scenes/FirstAwakening/MindPresentation.gd")
	scene.dialogue.active = false
	scene.dialogue.panel.hide()
	scene.meeting_done = true
	scene._open_flow()
	await process_frame
	var page: Control = scene.modal.find_child("MindNotebook", true, false)
	var editor: TextEdit = page.get_node("Writing/DraftEditor")
	editor.text = "走到隔离门。"
	editor.text_changed.emit()
	await process_frame
	page.get_node("Heading/NextPerson").pressed.emit()
	await process_frame
	assert(scene.mind_person == "工程师" and editor.text.is_empty(), "switch opens a separate blank draft")
	editor.text = "走到隔离门。"
	editor.text_changed.emit()
	await process_frame
	page.get_node("Heading/PrevPerson").pressed.emit()
	await process_frame
	assert(scene.mind_person == "侦探" and editor.text == "走到隔离门。", "switch restores the first person's draft")
	assert(scene.mind_drafts["工程师"] == "走到隔离门。", "other person's text remains unchanged")
	editor.text = "音乐家用木楔撑开门。"
	editor.text_changed.emit()
	await process_frame
	var cross_page: Dictionary = presentation.compile_draft(scene)
	assert(not cross_page.has("error"), "a page can draft another person's behavior rule")
	assert(cross_page.get("actor_codes", {}).get("音乐家", []) == ["ACT|音乐家|BraceDoorWithWedge|door"], "the action is regrouped under its actual actor")
	scene.mind_drafts = {
		"侦探":"走到隔离门。\n侦探看见热烟后，撤下木楔。\n用纸包起木楔。",
		"工程师":"走到隔离门。\n工程师看见木楔撤下后，插入生活翼内销。",
		"厨师":"走到隔离门。\n厨师看见止挡座移开后，用湿布擦拭地面。",
		"音乐家":"用木楔撑开隔离门。\n音乐家看见热烟后，移开铁止挡座。",
		"门外的人":"门外的人看见隔离门关闭后，落下主楼侧横闩。"
	}
	var compiled: Dictionary = presentation.compile_draft(scene)
	assert(not compiled.has("error"), "all character pages compile together: " + str(compiled.get("error", "")))
	presentation.run_draft(scene)
	await process_frame
	assert(not scene.sim_result.is_empty(), "combined replay ran")
	for id in ["door-mask", "paper-inner-transfer", "inner-bolt", "outer-bar", "wipe-after-stop"]:
		assert(scene.sim_result.ids.has(id), "combined replay produces " + id)
	print("ACTOR_DRAFTS_UI_CHECK_OK")
	quit()
