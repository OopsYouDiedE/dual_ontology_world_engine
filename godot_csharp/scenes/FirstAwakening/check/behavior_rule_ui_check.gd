extends SceneTree

var scene: Node2D
var failures := 0
const OUT := "res://../artifacts/first-awakening/"

func _initialize() -> void:
	call_deferred("run")

func check(ok: bool, message: String) -> void:
	if not ok:
		failures += 1
		push_error(message)

func settle() -> void:
	for index in 3: await process_frame

func run() -> void:
	TranslationServer.set_locale("zh_CN")
	scene = load("res://scenes/FirstAwakening/FirstAwakening.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	await settle()
	scene.dialogue.active = false
	scene.dialogue.panel.hide()
	scene.meeting_done = true
	scene._open_flow()
	await settle()
	var presentation: Script = load("res://scenes/FirstAwakening/MindPresentation.gd")
	var page: Control = scene.modal.find_child("MindNotebook", true, false)
	check(page != null, "复用原纸面")
	var editor: TextEdit = page.get_node("Writing/DraftEditor")
	editor.text = """音乐家用木楔撑开隔离门。
每当音乐家看见热烟后，音乐家移开铁止挡座。
侦探走到隔离门。
每当侦探看见热烟后，优先侦探取下木楔。
每当侦探看见木楔撤下后，侦探用纸包起木楔。
工程师走到隔离门。
每当工程师看见隔离门关闭后，工程师插入生活翼内销。
厨师走到隔离门。
每当厨师看见止挡座移开后，厨师用湿布擦拭地面。
每当门外的人看见隔离门关闭后，门外的人落下主楼侧横闩。"""
	editor.text_changed.emit()
	await settle()
	var compiled: Dictionary = presentation.compile_draft(scene)
	check(not compiled.has("error"), "同页自由人物组合可编译：" + str(compiled.get("error", "")))
	if compiled.has("error"):
		quit(1)
		return
	check(compiled.actor_codes.size() == 5, "按实际行为者执行而非纸页权限")
	page.get_node("Footer/Run").pressed.emit()
	await settle()
	check(not scene.sim_result.is_empty(), "真实原运行按钮产生结果")
	for id in ["door-mask", "paper-inner-transfer", "inner-bolt", "outer-bar", "wipe-after-stop"]:
		check(scene.sim_result.ids.has(id), "持续规则产生现场痕迹 " + id)
	check(scene.sim_result.failures.is_empty(), "有效规则无执行失败")
	while scene.replay_index < scene.replay_events.size():
		scene._replay_step(1)
	if DisplayServer.get_name() != "headless":
		await RenderingServer.frame_post_draw
		root.get_texture().get_image().save_png(OUT + "behavior-rules-paper.png")
	check(scene._replay_submission_validation().ready, "完整相容重演可供提交")
	var original_person: String = scene.mind_person
	scene.mind_person = "厨师"
	check(scene._replay_submission_validation().ready, "切换整理页不改变已运行规则")
	scene.mind_person = original_person
	page = scene.modal.find_child("MindNotebook", true, false)
	editor = page.get_node("Writing/DraftEditor")
	editor.text += "\n厨师关闭水阀。"
	editor.text_changed.emit()
	page.get_node("Footer/Run").pressed.emit()
	await settle()
	while scene.replay_index < scene.replay_events.size():
		scene._replay_step(1)
	check(not scene.sim_result.failures.is_empty(), "已关闭水阀的关闭尝试实际失败")
	check(scene._replay_submission_validation().ready, "失败尝试不否决其余相容经过")
	page = scene.modal.find_child("MindNotebook", true, false)
	editor = page.get_node("Writing/DraftEditor")
	editor.text += "\n厨师拧开水阀。"
	editor.text_changed.emit()
	await settle()
	check(scene.mind_dirty, "修改规则使旧运行失效")
	var saved := editor.text
	scene._close_modal()
	scene._open_flow()
	await settle()
	check(scene.modal.find_child("DraftEditor", true, false).text == saved, "返回现场后保留规则草稿")
	print("BEHAVIOR_RULE_UI failures=", failures)
	quit(1 if failures else 0)
