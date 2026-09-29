extends SceneTree

var failures := 0
var bridge: Node
var sites := {"notebook":{"title":"纸面"}, "study":{"title":"研习桌"}, "water":{"title":"水阀"}, "door":{"title":"隔离门"}, "paper":{"title":"包纸"}, "slit":{"title":"检视孔"}, "stop":{"title":"止挡座"}, "floor_wipe":{"title":"地面"}}

func _initialize() -> void:
	call_deferred("run")

func check(value: bool, label: String) -> void:
	if not value:
		failures += 1
		push_error(label)

func execute(actor: String, entries: Array[String]) -> Dictionary:
	return bridge.CompileActorDrafts({actor: entries}, sites)

func run() -> void:
	bridge = preload("res://scenes/FirstAwakening/HallReplayBridge.cs").new()
	root.add_child(bridge)
	for actor in ["侦探", "工程师", "厨师", "音乐家", "门外的人"]:
		var verb := "PullDoor" if actor == "门外的人" else "PushDoor"
		var result := execute(actor, ["RULE|%s|DoorOpen|door" % actor, "ACT|%s|CloseDoor|door" % actor, "ACT|%s|%s|door" % [actor,verb]])
		check(not result.has("error"), actor + "可编写同一种规则")
		check(result.states.back().closed, actor + "规则在开门后实际关门")
		check(result.states.back().failures.is_empty(), actor + "没有职业权限限制")
	var reactive := execute("音乐家", ["RULE|音乐家|DoorOpen|door", "ACT|音乐家|CloseDoor|door", "ACT|音乐家|PushDoor|door", "ACT|音乐家|PushDoor|door"])
	var closed_count := 0
	for state in reactive.states:
		if state.action == "CloseDoor": closed_count += 1
	check(closed_count == 2, "条件解除后重新出现会再次触发，不是一次性指令")
	var shared_condition := execute("音乐家", ["RULE|音乐家|DoorOpen|door", "ACT|音乐家|Inspect|door", "RULE|音乐家|DoorOpen|door", "ACT|音乐家|CloseDoor|door", "ACT|音乐家|PushDoor|door"])
	check(shared_condition.states.back().closed, "同一观察条件下的不同规则分别触发，不互相吞掉")
	var priority := execute("音乐家", ["RULE|音乐家|DoorOpen|door", "ACT|音乐家|Inspect|door", "PREFER|音乐家|DoorOpen|door", "ACT|音乐家|CloseDoor|door", "ACT|音乐家|PushDoor|door"])
	check(priority.states.back().closed, "优先规则决定实际动作")
	for state in priority.states:
		check(state.action != "Inspect", "低优先规则在门关后不继续按旧条件执行")
	var forbidden := execute("音乐家", ["RULE|音乐家|DoorOpen|door", "ACT|音乐家|CloseDoor|door", "FORBID|音乐家|DoorOpen|door", "ACT|音乐家|CloseDoor|door", "ACT|音乐家|PushDoor|door", "WHEN|音乐家|DoorOpen|door", "ACT|音乐家|CloseDoor|door"])
	check(not forbidden.states.back().closed, "禁止约束适用于普通条件句和持续候选，不能绕过")
	var alias_ban := execute("厨师", ["FORBID|厨师|WaterRunning|water", "ACT|厨师|CloseDoor|water", "ACT|厨师|OpenValve|water", "ACT|厨师|CloseValve|water"])
	check(alias_ban.states.back().valve_open, "对象分派后的同义动作不能绕过禁令")
	var unseen: Dictionary = bridge.CompileActorDrafts({"工程师":["RULE|工程师|DoorOpen|door", "ACT|工程师|CloseDoor|door"],"音乐家":["ACT|音乐家|PushDoor|door"]}, sites)
	check(not unseen.states.back().closed, "研习桌旁的人不从客观开门状态获得远程观察")
	var smoke := execute("厨师", ["PREFER|厨师|Smoke|door", "ACT|厨师|RemoveWedge|wedge", "ACT|厨师|BraceDoorWithWedge|door"])
	check(smoke.states.back().closed and smoke.states.back().wedge_removed, "规则无需排在撑门指令后，亲见烟后改变实际行动")
	check(smoke.states.back().ids.has("door-mask"), "物理痕迹来自实际撑门与烟气规则")
	var loop := execute("音乐家", ["RULE|音乐家|DoorOpen|door", "ACT|音乐家|CloseDoor|door", "RULE|音乐家|DoorClosed|door", "ACT|音乐家|PushDoor|door"])
	check(not loop.states.back().failures.is_empty(), "循环行为明确未完成而非假通关")
	var narrow: Dictionary = bridge.CompileDraft(["ACT|侦探|BraceDoorWithWedge|door", "ACT|侦探|GoTo|outside"], sites)
	check(narrow.states.back().sides["侦探"] == "inside" and not narrow.states.back().succeeded, "十厘米撑楔开口不能直接穿行")
	var wider: Dictionary = bridge.CompileDraft(["ACT|侦探|BraceDoorWithWedge|door", "ACT|侦探|PushDoor|door", "ACT|侦探|GoTo|outside"], sites)
	check(wider.states.back().sides["侦探"] == "outside" and wider.states.back().failures.is_empty(), "先探路再开大门允许同一人物撤离")
	check(not wider.states.back().passage_open and not wider.states.back().closed, "跨侧松手后配重退回撑楔窄缝")
	var one_person: Dictionary = bridge.CompileDraft(["ACT|侦探|PushDoor|door", "ACT|侦探|GoTo|outside", "ACT|侦探|OuterBar|outer_bar"], sites)
	check(one_person.states.back().outer_bar and one_person.states.back().failures.is_empty(), "外闩要求外侧操作但不要求第五人身份")
	var inside_bar: Dictionary = bridge.CompileDraft(["ACT|侦探|OuterBar|outer_bar"], sites)
	check(not inside_bar.states.back().outer_bar and not inside_bar.states.back().succeeded, "门合拢后内侧人物不能操作外闩")
	for hypothesis in ["escape", "betrayal", "fifth_person"]:
		check(not bridge.ProbeHypothesis(hypothesis).contains("无法识别"), "具体物理反例可检验：" + hypothesis)
	print("BEHAVIOR_RULE_RUNTIME failures=", failures)
	quit(1 if failures else 0)
