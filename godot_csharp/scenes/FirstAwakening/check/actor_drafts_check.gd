extends SceneTree

func _initialize() -> void:
	var bridge := preload("res://scenes/FirstAwakening/HallReplayBridge.cs").new()
	root.add_child(bridge)
	var sites := {
		"notebook":{"title":"私人笔记"}, "study":{"title":"研习桌"},
		"water":{"title":"水阀"}, "door":{"title":"隔离门"},
		"paper":{"title":"包纸"}, "stop":{"title":"止挡座"},
		"floor_wipe":{"title":"地面擦痕"}, "slit":{"title":"检视孔"}
	}
	var drafts := {
		"门外的人":["WHEN|门外的人|DoorClosed|door", "ACT|门外的人|OuterBar|outer_bar"],
		"厨师":["ACT|厨师|GoTo|door", "WHEN|厨师|StopMoved|stop", "ACT|厨师|WipeFloor|floor"],
		"工程师":["ACT|工程师|GoTo|door", "WHEN|工程师|WedgeRemoved|wedge", "ACT|工程师|SlideInnerBolt|inner_bolt"],
		"侦探":["ACT|侦探|GoTo|door", "WHEN|侦探|Smoke|door", "ACT|侦探|RemoveWedge|wedge", "ACT|侦探|WrapWedge|wedge"],
		"音乐家":["ACT|音乐家|BraceDoorWithWedge|door", "WHEN|音乐家|Smoke|door", "ACT|音乐家|MoveStop|stop"]
	}
	var replay: Dictionary = bridge.CompileActorDrafts(drafts, sites)
	assert(not replay.has("error"), "actor drafts compile")
	var last: Dictionary = replay.states.back()
	for id in ["door-mask", "paper-inner-transfer", "inner-bolt", "outer-bar", "wipe-after-stop"]:
		assert(last.ids.has(id), "combined reconstruction produces " + id)
	assert(last.failures.is_empty() and last.conflicts.is_empty(), "no failed or contradictory action")
	var illegal: Dictionary = bridge.CompileActorDrafts({"侦探":["ACT|音乐家|MoveStop|stop"]}, sites)
	assert(illegal.has("error"), "one character cannot write another character's action")
	var unobserved: Dictionary = bridge.CompileActorDrafts({"侦探":["ACT|侦探|GoTo|door", "WHEN|侦探|Smoke|door", "ACT|侦探|RemoveWedge|wedge"]}, sites)
	assert(not unobserved.states.back().smoke and not unobserved.states.back().wedge_removed, "waiting cannot create smoke through a closed door")
	assert(not unobserved.states.back().failures.is_empty(), "unresolved observation reports a failure")
	print("ACTOR_DRAFTS_CHECK_OK")
	quit()
