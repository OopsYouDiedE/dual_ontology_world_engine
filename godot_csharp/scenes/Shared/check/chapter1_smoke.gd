extends SceneTree

const Samples := preload("res://scenes/Shared/check/fixture_data.gd")
## 统一宿主上的第一章冒烟：醒来 → 相遇 → 调查（证物卡与试验）→ 重演回放 → 始业之答 → 门开与章末卡。
## 对白与表单由脚本代按；这是自动检查，不是游玩验证。
##   godot --headless --path godot_csharp --script res://scenes/Shared/check/chapter1_smoke.gd

const Fixture := preload("res://scenes/Awakening/check/chapter1_fixture.gd")
var fx: Fixture
const Autoplay := preload("res://scenes/Shared/check/chapter_autoplay.gd")
var failures := 0
var host: Node
var auto

func check(ok: bool, what: String) -> void:
	print(("  ok  " if ok else "  FAIL ") + what)
	if not ok: failures += 1

func _initialize() -> void:
	_run.call_deferred()

func settle(condition: Callable, limit := 3000) -> bool:
	for i in limit:
		if host.ui.modal_kind == "form" and str(host.ui._modal_event.fields[0].get("id", "")) == "name": fx.name_form_answer(Samples.text("chapter1-smoke", "text-001"))
		else: auto.step()
		if condition.call(): return true
		await process_frame
	return condition.call()

func stage() -> String: return str(host.ui.snapshot.get("stage", ""))

func _run() -> void:
	fx = Fixture.new(self)
	host = await fx.fresh(false)
	auto = Autoplay.new(host)
	var field: Node2D = host.field
	check(await settle(func(): return stage() == "roam" and not host.ui.is_busy()), Samples.text("chapter1-smoke", "text-002") % auto.lines.size())
	check(field.player != null and field.player.seated, Samples.text("chapter1-smoke", "text-003"))
	# 走进活动厅：相遇、报名、公告框、核验报偿，直到进入调查。
	field.player.stand_up()
	field.player.position = field.adapter.anchor("meeting:Detective") + Vector2(0, 40)
	var seen := {"board": false}
	var reached := await settle(func():
		if host.ui.modal_kind == "card":
			seen.board = seen.board or str(host.ui._modal_event.get("id", "")) == "board"
			auto.select("close")
		return stage() == "investigate" and not host.ui.is_busy(), 6000)
	var board_closed: bool = seen.board
	check(reached, Samples.text("chapter1-smoke", "text-004") % [stage(), auto.lines.size()])
	check(board_closed, Samples.text("chapter1-smoke", "text-005"))
	# 门厅的门：检视 → 证物卡 → 拔闩推门的试验。
	field.player.position = field.adapter.anchor("site:E1") + Vector2(24, 20)
	for i in 10: await physics_frame
	print("    E1: site=%s visible=%s active=%s dist=%.1f enabled=%s room=%s" % [field.sites.has("E1"), field.site_visible("E1"), field.field_active(),
		field.player.position.distance_to(field.adapter.anchor("site:E1")), host.ui.snapshot.sites.get("E1", {}), field.adapter.entered_room(field.player.position)])
	field.interact("E1")
	check(await settle(func(): return host.ui.modal_kind == "card" and not host.ui.dialogue.active), Samples.text("chapter1-smoke", "text-006"))
	var options: Array = host.ui._modal_event.get("options", [])
	check(options.any(func(o): return str(o.id).ends_with(":experiment")), Samples.text("chapter1-smoke", "text-007"))
	auto.select("experiment")
	check(await settle(func(): return host.ui.modal_kind == "card" and not host.ui.dialogue.active), Samples.text("chapter1-smoke", "text-008"))
	check(str(host.ui._modal_event.get("notes", [])).contains(Samples.text("chapter1-smoke", "text-009")), Samples.text("chapter1-smoke", "text-010"))
	auto.select("close")
	# 跳到全部吻合，回放一次（加速跳过），回到手稿。
	host.dispatch({"kind": "debug_jump", "state": "replay_match"})
	check(await settle(func(): return not host.ui.is_busy(), 3000), Samples.text("chapter1-smoke", "text-011"))
	check(bool(host.ui.snapshot.replay.result.get("full_match", false)), Samples.text("chapter1-smoke", "text-012"))
	host.ui.open_manuscript()
	check(host.ui.modal_kind == "replay", Samples.text("chapter1-smoke", "text-013"))
	host.dispatch({"kind": "replay", "vars": [0, 0, 0, 0, 0, 1], "skip": false})
	var stage_node: Node = field.adapter.stage_node
	check(await settle(func():
		if stage_node.playing: stage_node.skipping = true
		return host.ui.modal_kind == "replay" and not host.ui.dialogue.active, 6000), Samples.text("chapter1-smoke", "text-014"))
	host.ui.close_modal()
	# 始业之答：成对作答直到门开。
	var picks := Samples.array("chapter1-smoke", "case-001")
	check(await fx.compare_hands(), Samples.text("chapter1-smoke", "text-015"))
	fx.probe("apply_recall", Samples.array("chapter1-smoke", "case-002"))
	auto.choose = func(actions: Array) -> String:
		if host.ui.modal_kind != "form" or str(host.ui._modal_event.get("layout", "")) != "tabs" or picks.is_empty(): return ""
		var pick: Dictionary = picks.pop_front()
		host.ui._form.select("say", pick.say)
		host.ui._form.select("show", pick.show)
		return "submit"
	field.player.position = field.adapter.anchor("site:E8") + Vector2(0, 30)
	for i in 10: await physics_frame
	field.interact("E8")
	check(await settle(func(): return host.ui.modal_kind == "menu"), Samples.text("chapter1-smoke", "text-016"))
	auto.select("answer")
	var ended := await settle(func(): return host.ui.modal_kind == "ending", 20000)
	check(ended, Samples.text("chapter1-smoke", "text-017"))
	var text := "\n".join(auto.lines)
	check(text.contains(fx.line_text("s10_hand.006")) or text.contains(Samples.text("chapter1-smoke", "text-018")), Samples.text("chapter1-smoke", "text-019"))
	check(field.adapter.get_node("ObjectsAndPeople/MechanicalIsolationDoor/DoorGap").visible, Samples.text("chapter1-smoke", "text-020"))
	check(FileAccess.file_exists("user://chapter1_end.json"), Samples.text("chapter1-smoke", "text-021"))
	var actions: Array = host.ui._modal_event.get("actions", [])
	check(actions.any(func(a): return str(a.id) == "continue"), Samples.text("chapter1-smoke", "text-022"))
	print(Samples.text("chapter1-smoke", "text-023") % [auto.lines.size(), failures])
	host.begin_shutdown()
	host.queue_free()
	await process_frame
	await process_frame
	await preload("res://scenes/Shared/Audio/Sound.gd").quit_cleanly(1 if failures > 0 else 0)
