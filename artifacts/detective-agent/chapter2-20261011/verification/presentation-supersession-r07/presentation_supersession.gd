extends SceneTree
## Independent neutral contract fixture. It loads the production seat script only;
## fake public inputs contain no chapter, story, route, answer, or private state.

const PilotScript := preload("res://scenes/Host/AgentPilot.gd")
const EXPIRED := "unknown_consumed_or_expired_token"

class FakeEngine extends Node:
	var turn := 20
	var candidates: Array = []
	var inputs: Dictionary = {}
	var boundaries: Dictionary = {}
	var reads: Array = []
	var closed := 0
	var decisions := 0
	var observation_reads := 0
	func AgentCandidateInputJson(token: String) -> String:
		reads.append(token)
		if boundaries.has(token): return JSON.stringify({"boundary": boundaries[token]})
		if inputs.has(token): return JSON.stringify(inputs[token])
		return JSON.stringify({"boundary": "unknown_consumed_or_expired_token"})
	func AgentObservationJson() -> String:
		observation_reads += 1
		return JSON.stringify({"Turn": turn, "Candidates": candidates, "Complete": false})
	func AgentDecideJson() -> String:
		decisions += 1
		return JSON.stringify({"Token": candidates[0].Token, "Reason": "neutral_public_choice"})
	func AgentTimeJson() -> String: return "{}"
	func AgentSelections() -> int: return decisions
	func AgentEnabled() -> bool: return closed == 0
	func CloseAgent() -> void: closed += 1
	func publish(new_turn: int, offered: Array, available: Dictionary) -> void:
		turn = new_turn
		candidates = offered.duplicate(true)
		inputs = available.duplicate(true)

class FakeDialogue extends Node:
	var active := false
	var advances := 0
	var on_advance := Callable()
	func advance() -> void:
		advances += 1
		active = false
		if on_advance.is_valid(): on_advance.call()

class FakeUi extends Node:
	var modal_kind := ""
	var dialogue: Node
	var closed_modals := 0
	var released_dialogues := 0
	var form_choices: Array = []
	func _init() -> void:
		dialogue = FakeDialogue.new()
		add_child(dialogue)
	func is_busy() -> bool: return true
	func dialogue_choices_shown() -> bool: return true
	func release_dialogue() -> void:
		released_dialogues += 1
		dialogue.active = false
	func close_modal() -> void: closed_modals += 1
	func show_form_choice(value: Dictionary) -> void: form_choices.append(value.duplicate(true))

class FakeHost extends Node:
	var field: Node
	var ui: Node
	var engine: Node
	var ready_to_decide := true
	var submitted: Array = []
	func settled() -> bool: return ready_to_decide
	func submit_token(token: String) -> void:
		submitted.append(token)
		ready_to_decide = false

var outcomes: Array = []
var failure_labels: Array = []
var traces: Array = []
var current_case := ""

func _initialize() -> void:
	_run.call_deferred()

func _offer(token: String, kind: String = "select") -> Dictionary:
	return {"Token": token, "Kind": kind, "Label": "Neutral public input", "New": true}

func _rig(selected: String = "alpha", selected_turn: int = 20) -> Dictionary:
	var container := Node.new()
	root.add_child(container)
	var engine := FakeEngine.new()
	container.add_child(engine)
	var ui := FakeUi.new()
	container.add_child(ui)
	var host := FakeHost.new()
	container.add_child(host)
	host.engine = engine
	host.ui = ui
	var pilot: Node = PilotScript.new()
	container.add_child(pilot)
	pilot.host = host
	pilot.engine = engine
	pilot.line_seconds = 0.08
	pilot._last_decision = {"Token": selected}
	pilot._decision_turn = selected_turn
	var events: Array = []
	var stops: Array = []
	pilot.recorded.connect(func(kind: String, value: Variant) -> void:
		var entry := {"kind": kind, "value": value.duplicate(true) if value is Dictionary else value}
		events.append(entry)
		traces.append({"case": current_case, "event": entry, "elapsed_ms": Time.get_ticks_msec()})
	)
	pilot.stopped.connect(func(reason: String, summary: Dictionary) -> void:
		stops.append({"reason": reason, "summary": summary.duplicate(true)})
	)
	return {"container": container, "engine": engine, "host": host, "ui": ui,
		"pilot": pilot, "events": events, "stops": stops}

func _check(condition: bool, label: String, actual: Variant = null) -> void:
	outcomes.append({"case": current_case, "requirement": label, "passed": condition, "actual": actual})
	if not condition: failure_labels.append(current_case + ": " + label)
	print(JSON.stringify(outcomes.back()))

func _events(rig: Dictionary, kind: String) -> Array:
	return rig.events.filter(func(value: Dictionary) -> bool: return value.kind == kind)

func _assert_superseded(rig: Dictionary, old_turn: int, new_turn: int) -> void:
	var changed := _events(rig, "superseded_action")
	_check(changed.size() == 1, "old input produces one explicit supersession receipt", changed)
	if changed.size() == 1:
		var value: Dictionary = changed[0].value
		_check(value.get("token") == "alpha" and int(value.get("decision_turn", -1)) == old_turn
			and int(value.get("current_turn", -1)) == new_turn and value.get("reason") == "visible_candidates_changed",
			"receipt associates the selected token with the earlier and replacement public turns", value)
	_check(rig.host.submitted.is_empty(), "old token is never submitted", rig.host.submitted)
	_check(not rig.pilot.stopping and rig.engine.closed == 0 and rig.stops.is_empty(),
		"supersession leaves the seat available", {"stopping": rig.pilot.stopping, "closed": rig.engine.closed, "stops": rig.stops})

func _test_valid() -> void:
	current_case = "valid_current_token"
	var rig := _rig()
	var input := {"kind": "select", "value": "neutral-A"}
	rig.engine.publish(20, [_offer("alpha")], {"alpha": input})
	var returned: Dictionary = rig.pilot._current_input("alpha")
	_check(returned == input, "current input is returned intact", returned)
	rig.pilot._submit_current("alpha")
	_check(rig.host.submitted == ["alpha"], "valid selected token reaches ordinary host submission", rig.host.submitted)
	_check(rig.engine.reads == ["alpha", "alpha"], "submission rechecks the token before dispatch", rig.engine.reads)
	_check(rig.events.is_empty() and rig.stops.is_empty() and rig.engine.closed == 0,
		"valid input neither supersedes nor stops", {"events": rig.events, "stops": rig.stops, "closed": rig.engine.closed})
	rig.container.free()

func _test_direct_supersession() -> void:
	current_case = "direct_public_replacement"
	var rig := _rig()
	rig.engine.publish(21, [_offer("beta")], {"beta": {"kind": "select"}})
	rig.pilot._submit_current("alpha")
	_assert_superseded(rig, 20, 21)
	_check(rig.engine.observation_reads == 1, "expiry checks the current public observation", rig.engine.observation_reads)
	rig.container.free()

func _test_delayed_selection() -> void:
	current_case = "selection_replaced_while_waiting"
	var rig := _rig()
	rig.engine.publish(20, [_offer("alpha")], {"alpha": {"kind": "select"}})
	create_timer(0.02).timeout.connect(func() -> void:
		rig.engine.publish(21, [_offer("beta")], {"beta": {"kind": "select"}})
	)
	await rig.pilot._act("alpha")
	_assert_superseded(rig, 20, 21)
	_check(rig.engine.reads == ["alpha", "alpha"], "initial read and delayed submission use separate validity checks", rig.engine.reads)
	_check(_events(rig, "act").size() == 1 and rig.ui.closed_modals == 1,
		"ordinary presentation wait executes before submission recheck", {"act": _events(rig, "act"), "closed_modals": rig.ui.closed_modals})
	rig.container.free()

func _test_delayed_presented() -> void:
	current_case = "public_reply_replaced_during_dialogue_read"
	var rig := _rig()
	rig.engine.publish(20, [_offer("alpha", "presented")], {"alpha": {"kind": "presented"}})
	rig.ui.dialogue.active = true
	rig.ui.dialogue.on_advance = func() -> void:
		rig.pilot.take_ui_reply({"kind": "presented"})
		rig.engine.publish(21, [_offer("beta")], {"beta": {"kind": "select"}})
	await rig.pilot._act("alpha")
	_assert_superseded(rig, 20, 21)
	_check(rig.ui.dialogue.advances == 1 and rig.pilot._reply_token == "",
		"normal dialogue reply finishes before stale token is checked", {"advances": rig.ui.dialogue.advances, "reply_token": rig.pilot._reply_token})
	rig.container.free()

func _test_boundary(name: String, boundary: String, selected: String, turn: int, offered: Array, requested: String = "alpha") -> void:
	current_case = name
	var rig := _rig(selected)
	rig.engine.publish(turn, offered, {})
	rig.engine.boundaries[requested] = boundary
	rig.pilot._submit_current(requested)
	_check(rig.host.submitted.is_empty(), "rejected token never reaches the host", rig.host.submitted)
	_check(rig.pilot.stopping and rig.engine.closed == 1 and rig.stops.size() == 1,
		"boundary stops and closes the seat", {"stopping": rig.pilot.stopping, "closed": rig.engine.closed, "stops": rig.stops})
	if rig.stops.size() == 1:
		_check(rig.stops[0].reason == boundary, "original boundary reason is retained", rig.stops[0].reason)
	_check(_events(rig, "superseded_action").is_empty(), "boundary is not mislabeled as supersession", _events(rig, "superseded_action"))
	rig.container.free()

func _test_next_normal_decision() -> void:
	current_case = "next_ordinary_observation_decides_new_token"
	var rig := _rig()
	rig.engine.publish(20, [_offer("alpha")], {"alpha": {"kind": "select"}})
	rig.pilot.before_action = func(_observation: Dictionary, decision: Dictionary) -> void:
		if decision.get("Token") == "alpha":
			create_timer(0.02).timeout.connect(func() -> void:
				rig.engine.publish(21, [_offer("beta")], {"beta": {"kind": "select"}})
			)
	rig.pilot.start()
	var deadline := Time.get_ticks_msec() + 2000
	while rig.host.submitted.is_empty() and not rig.pilot.stopping and Time.get_ticks_msec() < deadline:
		await process_frame
	_check(rig.host.submitted == ["beta"], "next normal decision submits only the replacement token", rig.host.submitted)
	var decisions := _events(rig, "decision")
	var tokens: Array = []
	for decision in decisions: tokens.append(decision.value.get("Token"))
	_check(tokens == ["alpha", "beta"], "production loop returns to observation and chooses the new public token", tokens)
	var observations := _events(rig, "observation")
	var turns: Array = []
	for observation in observations: turns.append(int(observation.value.get("Turn", -1)))
	_check(turns == [20, 21], "both decisions use the ordinary public observation path", turns)
	_check(_events(rig, "superseded_action").size() == 1 and rig.engine.closed == 0 and rig.stops.is_empty(),
		"replacement and later submission leave the seat open", {"supersession": _events(rig, "superseded_action"), "closed": rig.engine.closed, "stops": rig.stops})
	# Fixture cleanup only: terminate its polling coroutine without inventing a production stop.
	rig.pilot.stopping = true
	await create_timer(0.12).timeout
	rig.container.free()

func _run() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() != 1 or FileAccess.file_exists(args[0]):
		push_error("Requires one new result file path")
		quit(2)
		return
	_test_valid()
	_test_direct_supersession()
	await _test_delayed_selection()
	await _test_delayed_presented()
	_test_boundary("later_observation_with_empty_candidates", EXPIRED, "alpha", 21, [])
	_test_boundary("same_turn_nonempty_candidates", EXPIRED, "alpha", 20, [_offer("beta")])
	_test_boundary("older_turn_nonempty_candidates", EXPIRED, "alpha", 19, [_offer("beta")])
	_test_boundary("other_boundary_later_nonempty", "neutral_protocol_error", "alpha", 21, [_offer("beta")])
	_test_boundary("unselected_token_later_nonempty", EXPIRED, "alpha", 21, [_offer("beta")], "rogue")
	_test_boundary("no_selected_token_later_nonempty", EXPIRED, "", 21, [_offer("beta")])
	await _test_next_normal_decision()
	var result := {"passed": failure_labels.is_empty(), "failed_requirements": failure_labels,
		"outcomes": outcomes, "production_trace": traces,
		"scope": "neutral mechanism fixture; no story, gameplay, visual, or literary acceptance"}
	var output := FileAccess.open(args[0], FileAccess.WRITE)
	output.store_string(JSON.stringify(result, "\t"))
	output.close()
	print("PRESENTATION_SUPERSESSION_COMPLETE " + JSON.stringify({"passed": failure_labels.is_empty(), "failed_requirements": failure_labels}))
	quit(0 if failure_labels.is_empty() else 1)
