extends "res://scenes/FirstAwakening/check/review_dynamic_check.gd"

func _run() -> void:
	host = load("res://scenes/FirstAwakening/FirstAwakening.tscn").instantiate()
	root.add_child(host)
	current_scene = host
	await frames(20)
	while host.dialogue.active:
		await host.dialogue.advance()
		await host.dialogue.advance()
		await frames(2)
	host._close_modal()
	host.red.call("IntroduceCompanions")
	host.red.call("ReceiveCopperIntroduction")
	host.story_backlog.clear()
	# Real authored records obtained through the existing reader.
	for id in ["E05", "E06"]:
		var result: Dictionary = host.red.call("Inspect", id)
		host._accept_red_inspection(id, result)
		var record: Dictionary = await host.ObservationReader.read(host.dialogue, host.RED_DIALOGUE, host.Content.observation_title(id), host)
		host.red_notes[id] = record.paragraphs
	host.story_backlog.clear()
	host._open_recorded_evidence("E05", false)
	await frames(6)
	var book: Node = host.modal.get_node("EvidenceNotebook")
	book._contents.grab_focus()
	print("evidence focus before=", root.gui_get_focus_owner(), " record=", host.last_recorded_evidence)
	var key := InputEventKey.new()
	key.keycode = KEY_RIGHT
	key.physical_keycode = KEY_RIGHT
	key.pressed = true
	Input.parse_input_event(key)
	await frames(4)
	key = key.duplicate()
	key.pressed = false
	Input.parse_input_event(key)
	print("evidence focus after=", root.gui_get_focus_owner(), " record=", host.last_recorded_evidence)
	await screenshot("evidence-focused-right-key")
	host._close_modal()
	host.story_backlog.clear()
	host._begin_dialogue("S10")
	await frames(6)
	var max_height := 0
	var steps := 0
	while host.dialogue.active and steps < 50:
		host.dialogue.reveal()
		await frames(4)
		var height: int = host.dialogue.body.get_content_height()
		var available: float = host.dialogue.body.size.y
		if height > max_height:
			max_height = height
			print("S10 line content_height=", height, " available=", available, " text=", host.dialogue.body.text)
			await screenshot("s10-longest-dialogue")
		await host.dialogue.advance()
		await frames(6)
		steps += 1
	print("S10 maximum rendered content height=", max_height)
	quit(0)
