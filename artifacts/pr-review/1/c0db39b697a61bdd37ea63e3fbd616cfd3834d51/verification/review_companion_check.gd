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
	host.red.call("IdentifyInvestigator", "自动回归")
	host.red.call("IntroduceCompanions")
	host.red.call("ReceiveCopperIntroduction")
	host.red_companions_met = true
	host.conversation_staged = true
	host.investigation_phase = "investigation"
	host.story_backlog.clear()
	host.name_entry_active = false
	host.story_lock = false
	host.player.stand_up()
	host.player.position = Vector2(248, 424)
	host.actors["厨师"].position = Vector2(248, 512)
	await frames(2)
	host._move_to_click(Vector2(248, 568))
	await frames(30)
	await route_to(Vector2(248, 568), "companion blocks north opening; repeated click uses alternate route")
	print("companion_check failures=", failures)
	quit(1 if failures else 0)
