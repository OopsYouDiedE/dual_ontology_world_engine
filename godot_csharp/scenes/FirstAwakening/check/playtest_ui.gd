extends SceneTree

const OUT := "res://../artifacts/first-awakening/"
var scene: Node2D
var record := {}

func _initialize() -> void:
	call_deferred("run")

func frames(count := 3) -> void:
	for i in count: await process_frame

func key(code: Key, unicode := 0) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.unicode = unicode
	event.pressed = true
	Input.parse_input_event(event)
	await process_frame
	event.pressed = false
	Input.parse_input_event(event.duplicate())
	await process_frame

func click(control: Control) -> void:
	var point := control.get_global_rect().get_center()
	var motion := InputEventMouseMotion.new()
	motion.position = point
	Input.parse_input_event(motion)
	var event := InputEventMouseButton.new()
	event.button_index = MOUSE_BUTTON_LEFT
	event.position = point
	event.pressed = true
	Input.parse_input_event(event)
	await process_frame
	event.pressed = false
	Input.parse_input_event(event.duplicate())
	await frames()

func button(caption: String) -> Button:
	for node in scene.modal.find_children("*", "Button", true, false):
		if node.text == caption or node.tooltip_text == caption: return node
	return null

func shot(name: String) -> void:
	await frames(3)
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(OUT + name + ".png")

func type_text(content: String) -> void:
	for ch in content:
		var code := ch.unicode_at(0)
		await key(KEY_NONE, code)

func select_all() -> void:
	var event := InputEventKey.new()
	event.keycode = KEY_A
	event.physical_keycode = KEY_A
	event.ctrl_pressed = true
	event.pressed = true
	Input.parse_input_event(event)
	await process_frame
	event.pressed = false
	Input.parse_input_event(event)
	await process_frame

func run() -> void:
	scene = load("res://scenes/FirstAwakening/FirstAwakening.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	await frames(8)
	record.opening_character = scene.dialogue.line.character
	await shot("playtest-opening")
	for i in 4: await key(KEY_SPACE)
	record.opening_closed = not scene.dialogue.active
	await key(KEY_F)
	record.first_mind_voice = scene.dialogue.line.character == "侦探（心声）"
	await shot("playtest-mind-introduction")
	for i in 12:
		if not scene.dialogue.active: break
		await key(KEY_SPACE)
	record.flow_open = scene.modal_open and scene.mode == "flow"
	await shot("playtest-flow-empty")
	record.music_started = scene.reasoning_music != null and scene.reasoning_music.playing
	await click(scene.modal.find_child("MindNotebook",true,false).get_node("Views/Next"))
	await click(scene.modal.find_child("MindNotebook",true,false).get_node("Views/Next"))
	record.dormitory_view = scene.replay_stage.manual_focus == Vector2(160,220)
	await shot("playtest-view-dormitory")
	await click(scene.modal.find_child("MindNotebook",true,false).get_node("Views/Previous"))
	var stage: Control = scene.replay_stage
	var drag_origin := stage.get_global_rect().position + Vector2(320,180)
	var down := InputEventMouseButton.new()
	down.button_index = MOUSE_BUTTON_LEFT
	down.position = drag_origin
	down.pressed = true
	Input.parse_input_event(down)
	await process_frame
	var drag := InputEventMouseMotion.new()
	drag.position = drag_origin + Vector2(80,0)
	drag.relative = Vector2(80,0)
	drag.button_mask = MOUSE_BUTTON_MASK_LEFT
	Input.parse_input_event(drag)
	await process_frame
	var up := InputEventMouseButton.new()
	up.button_index = MOUSE_BUTTON_LEFT
	up.position = drag.position
	up.pressed = false
	Input.parse_input_event(up)
	await process_frame
	record.drag_view = stage.manual_focus.x < 640
	await click(scene.modal.find_child("MindNotebook",true,false).get_node("Views/Previous"))
	record.follow_view = scene.replay_stage.manual_focus == Vector2.ZERO
	await click(button("音乐：开"))
	await create_timer(0.4).timeout
	record.music_muted = not scene.reasoning_music.playing
	await click(button("音乐：关"))
	record.music_resumed = scene.reasoning_music.playing
	var mind_script: Script = load("res://scenes/FirstAwakening/MindPresentation.gd")
	scene.mind_text = "音乐家走向隔离门。\n音乐家用木楔撑门。"
	var aliases: Dictionary = mind_script.compile_draft(scene)
	record.aliases_before_evidence = aliases.get("codes",[]) == ["ACT|音乐家|GoTo|door","ACT|音乐家|BraceDoorWithWedge|door"]
	scene.mind_text = "音乐家把木楔垫在门底。\n侦探把木楔裹进纸里。"
	record.free_phrasing = mind_script.compile_draft(scene).get("codes",[]) == ["ACT|音乐家|BraceDoorWithWedge|door","ACT|侦探|WrapWedge|wedge"]
	scene.mind_text = "音乐家不要用木楔撑门。"
	record.negation_rejected = mind_script.compile_draft(scene).has("error")
	scene.mind_text = ""
	var editor := scene.modal.find_child("DraftEditor", true, false) as TextEdit
	var page: Control = scene.modal.find_child("MindNotebook",true,false)
	var person: LineEdit = page.get_node("Heading/Person")
	await click(person)
	await select_all()
	await type_text("工程师")
	record.title_typed = scene.mind_person == "工程师"
	await click(page.get_node("Heading/Choose"))
	var popup: PopupMenu = page.get_node("PersonPopup")
	record.dropdown_visible = popup.visible and popup.item_count == 5
	await shot("playtest-person-dropdown")
	await key(KEY_DOWN)
	var selected: String = popup.get_item_text(popup.get_focused_item()) if popup.get_focused_item() >= 0 else ""
	await key(KEY_ENTER)
	await frames()
	record.dropdown_selected = not popup.visible and scene.mind_person == selected
	await click(editor)
	await key(KEY_TAB)
	record.default_actor_applied = mind_script.compile_draft(scene).get("codes",[]) == ["ACT|"+selected+"|GoTo|door"]
	record.example_inserted = editor.text == "走到隔离门。" and scene.mind_text == editor.text
	record.example_runnable = not button("从头重演").disabled
	await click(editor)
	await select_all()
	await key(KEY_BACKSPACE)
	await type_text("音乐家走到门边观察。")
	record.typed_unicode = editor.text
	record.editor_focused = editor.has_focus()
	await shot("playtest-typed")
	await click(button("词汇"))
	record.vocabulary_visible = scene.modal.find_child("Vocabulary", true, false).visible
	record.drawer_half = absf(page.get_node("Writing/Vocabulary").size.y / page.get_node("Writing").size.y - 0.5) < 0.03
	await shot("playtest-vocabulary")
	await click(button("收起"))
	await key(KEY_ENTER)
	await type_text("未知人物开门。")
	record.invalid_text = editor.text
	record.invalid_error = (scene.modal.find_child("DraftError",true,false) as Label).text
	record.run_disabled = button("从头重演").disabled
	await shot("playtest-error")
	await click(button("返回现场"))
	record.returned = not scene.modal_open
	await create_timer(0.4).timeout
	record.music_stopped_on_return = not scene.reasoning_music.playing
	await key(KEY_F)
	editor = scene.modal.find_child("DraftEditor", true, false) as TextEdit
	record.draft_retained = editor.text
	await shot("playtest-reopened")
	await click(editor)
	await select_all()
	await key(KEY_BACKSPACE)
	await click(button("词汇"))
	await click(button("人物"))
	await click(button("音乐家"))
	await click(button("动作"))
	await click(button("走到"))
	await click(button("对象"))
	await click(scene.modal.find_child("Search",true,false))
	await type_text("隔离门")
	await click(button("隔离门"))
	record.vocabulary_inserted = editor.text
	record.vocabulary_composed_correctly = editor.text == "音乐家走到隔离门"
	record.vocabulary_focus = editor.has_focus()
	record.vocabulary_run_enabled = not button("从头重演").disabled
	await shot("playtest-vocabulary-inserted")
	await click(button("从头重演"))
	record.replay_has_result = not scene.sim_result.is_empty()
	record.replay_event_count = scene.replay_events.size()
	await shot("playtest-replayed")
	editor = scene.modal.find_child("DraftEditor", true, false) as TextEdit
	await click(editor)
	await key(KEY_ENTER)
	await type_text("未知人物开门。")
	record.stale_status = scene.replay_status.text
	record.stale_dirty = scene.mind_dirty
	record.stale_result_preserved = not scene.sim_result.is_empty()
	await shot("playtest-stale")
	await select_all()
	await key(KEY_BACKSPACE)
	for i in 8:
		await type_text("音乐家走到门边观察。")
		await key(KEY_ENTER)
	record.long_draft_lines = editor.get_line_count()
	await shot("playtest-long-draft-before")
	await click(button("词汇"))
	await shot("playtest-long-draft-covered")
	await click(button("人物"))
	await click(button("音乐家"))
	await click(button("动作"))
	await click(button("走到"))
	await click(button("对象"))
	await click(scene.modal.find_child("Search",true,false))
	await type_text("隔离门")
	await click(button("隔离门"))
	record.long_draft_last_line = editor.get_line(editor.get_line_count() - 1)
	record.long_draft_focus = editor.has_focus()
	await shot("playtest-long-draft-inserted")
	await click(button("返回现场"))
	scene._begin_dialogue("transmitter")
	await frames(8)
	scene.dialogue.reveal()
	record.transmitter_choices = scene.dialogue.choices.get_child_count()
	await shot("playtest-transmitter-choices")
	var player_start: Vector2 = scene.player.position
	var moving := InputEventKey.new()
	moving.keycode = KEY_D
	moving.physical_keycode = KEY_D
	moving.pressed = true
	Input.parse_input_event(moving)
	for i in 8: await physics_frame
	moving.pressed = false
	Input.parse_input_event(moving)
	record.movement_during_dialogue = scene.player.position.distance_to(player_start)
	await key(KEY_DOWN)
	await key(KEY_DOWN)
	var focused_choice := root.gui_get_focus_owner()
	record.choice_focus = focused_choice.text if focused_choice is Button else "none"
	await key(KEY_ENTER)
	await frames(12)
	record.choice_response_character = scene.dialogue.line.character
	record.choice_response_text = scene.dialogue.line.text
	record.choice_history_count = scene.dialogue.history.size()
	record.choice_history = scene.dialogue.history.duplicate()
	await shot("playtest-transmitter-response")
	var file := FileAccess.open(OUT + "playtest-ui-observations.json", FileAccess.WRITE)
	file.store_string(JSON.stringify(record, "\t"))
	print("PLAYTEST_UI ", JSON.stringify(record))
	var failed: Array[String] = []
	for name in record:
		if record[name] is bool and not record[name]: failed.append(name)
	print("PLAYTEST_FAILURES ",failed)
	quit(0 if failed.is_empty() else 1)
