extends SceneTree

const PilotScript := preload("res://scenes/Host/AgentPilot.gd")
const MainScript := preload("res://scenes/Host/Main.gd")

class NeutralPlayer extends Node2D:
	var travelling := false

class NeutralAdapter extends RefCounted:
	var key := "neutral-map"
	func map_key() -> String: return key
	func sight_size() -> Vector2i: return Vector2i(1, 1)
	func seen_cells() -> Dictionary: return {}
	func walkable(_point: Vector2, _player: Node) -> bool: return false

class NeutralField extends Node2D:
	signal field_event(input: Dictionary)
	var player := NeutralPlayer.new()
	var adapter := NeutralAdapter.new()
	var click_path := PackedVector2Array()
	var sites: Dictionary = {}
	var approached: Array[String] = []
	func _init() -> void: add_child(player)
	func field_active() -> bool: return true
	func player_controlled() -> bool: return true
	func in_sight(_point: Vector2) -> bool: return false
	func context() -> Dictionary: return {"map": adapter.map_key()}
	func _approach(site: String) -> void:
		approached.append(site)
		click_path = PackedVector2Array([Vector2(48, 16)])
	func end_path() -> void: click_path = PackedVector2Array()

class NeutralDialogue extends RefCounted:
	var active := false

class NeutralUi extends Node:
	var dialogue := NeutralDialogue.new()
	var modal_kind := ""
	var snapshot: Dictionary = {}
	func is_busy() -> bool: return false

class NeutralEngine extends Node:
	var pending := ""
	var inputs: Dictionary = {}
	var feedback: Array = []
	var submitted: Array = []
	var field_inputs: Array = []
	var offers: Array = []
	var order: Array[String] = []
	var observation := {"Turn": 1, "Complete": false, "Candidates": [{"Token": "neutral-current"}]}
	var closed := false
	func offer(token: String, site := "neutral-site") -> void:
		pending = token
		inputs = {token: {"kind": "field_action", "action": "interact", "site": site}}
	func AgentCandidateInputJson(token: String) -> String:
		return JSON.stringify(inputs.get(token, {"boundary": "unknown_consumed_or_expired_token"}))
	func AgentInteractionFeedback(token: String, reached: bool) -> bool:
		var accepted := pending != "" and token == pending
		feedback.append({"token": token, "reached": reached, "accepted": accepted})
		order.append("feedback:%s:%s" % [token, reached])
		if accepted: pending = ""
		return accepted
	func AgentObservationJson() -> String: return JSON.stringify(observation)
	func AgentTimeJson() -> String: return JSON.stringify({"Day": 1, "Minutes": 0, "Phase": "neutral"})
	func AgentSelections() -> int: return submitted.size()
	func AgentLocate(_space: String, _x: float, _y: float) -> String: return ""
	func AgentObserveField(current: Array) -> String:
		offers = current
		order.append("observe-field")
		return ""
	func AgentEnabled() -> bool: return true
	func CloseAgent() -> void: closed = true
	func Dispatch(message: Dictionary) -> Array:
		submitted.append(message.duplicate(true))
		return [{"kind": "player_field", "input": inputs[str(message.token)]}]
	func DispatchField(message: Dictionary) -> Array:
		field_inputs.append(message.duplicate(true))
		order.append("dispatch-field")
		return []

var outcomes: Array = []
var traces: Array = []
var evidence: String

func _initialize() -> void:
	evidence = OS.get_environment("INTERACTION_RECEIPT_EVIDENCE")
	_run.call_deferred()

func record(name: String, holds: bool, observed: Variant) -> void:
	var value := {"behavior": name, "holds": holds, "observed": observed}
	outcomes.append(value)
	print("BEHAVIOR " + JSON.stringify(value))

func fixture() -> Dictionary:
	# Main remains outside the tree, so its production _ready never opens a
	# title, loads chapter content or creates a game session. Only the formal
	# token -> player_field and field_event -> DispatchField chain is exercised.
	var main = MainScript.new()
	var engine := NeutralEngine.new()
	var ui := NeutralUi.new()
	var field := NeutralField.new()
	var pilot = PilotScript.new()
	root.add_child(engine)
	root.add_child(ui)
	root.add_child(field)
	root.add_child(pilot)
	main.engine = engine
	main.ui = ui
	main.field = field
	main.pilot = pilot
	pilot.host = main
	pilot.engine = engine
	pilot.recorded.connect(func(kind: String, value: Variant) -> void:
		traces.append({"kind": kind, "value": value}))
	field.field_event.connect(main._field_input)
	return {"main": main, "engine": engine, "ui": ui, "field": field, "pilot": pilot}

func dispose(f: Dictionary) -> void:
	f.pilot.free()
	f.field.free()
	f.ui.free()
	f.engine.free()
	f.main.free()

func _run() -> void:
	var f := fixture()
	f.engine.offer("neutral-arrival")
	await f.pilot._act("neutral-arrival")
	record("selecting_site_only_begins_the_formal_field_approach",
		f.field.approached == ["neutral-site"] and f.engine.feedback.is_empty(),
		{"approached": f.field.approached, "feedback": f.engine.feedback, "submitted": f.engine.submitted})
	f.field.field_event.emit({"kind": "interact", "site": "different-site", "map": "neutral-map"})
	f.field.field_event.emit({"kind": "interact", "site": "neutral-site", "map": "different-map"})
	f.field.field_event.emit({"kind": "entered", "site": "neutral-site", "map": "neutral-map"})
	record("different_site_map_or_event_kind_cannot_confirm_contact", f.engine.feedback.is_empty(), f.engine.field_inputs)
	f.field.field_event.emit({"kind": "interact", "site": "neutral-site", "map": "neutral-map"})
	var matching: Array = f.engine.feedback.duplicate(true)
	f.field.field_event.emit({"kind": "interact", "site": "neutral-site", "map": "neutral-map"})
	record("matching_real_field_signal_confirms_contact_once_before_dispatch",
		matching == [{"token": "neutral-arrival", "reached": true, "accepted": true}]
		and f.engine.feedback == matching and f.engine.order[-3] == "feedback:neutral-arrival:true",
		{"feedback": f.engine.feedback, "order": f.engine.order})
	dispose(f)

	f = fixture()
	f.engine.offer("neutral-unreached")
	await f.pilot._act("neutral-unreached")
	record("unfinished_path_does_not_report_contact", not f.pilot._field_idle() and f.engine.feedback.is_empty(),
		{"click_path": Array(f.field.click_path), "feedback": f.engine.feedback})
	f.field.end_path()
	f.pilot._observe()
	var idleFeedback: Array = f.engine.feedback.duplicate(true)
	f.pilot._observe()
	record("field_becoming_idle_without_contact_reports_failure_once",
		idleFeedback == [{"token": "neutral-unreached", "reached": false, "accepted": true}]
		and f.engine.feedback == idleFeedback and f.engine.order[0] == "feedback:neutral-unreached:false",
		{"feedback": f.engine.feedback, "order": f.engine.order})
	dispose(f)

	f = fixture()
	f.engine.offer("neutral-superseded")
	await f.pilot._act("neutral-superseded")
	f.pilot._last_decision = {"Token": "neutral-superseded"}
	f.pilot._decision_turn = 1
	f.engine.inputs = {}
	f.engine.observation.Turn = 2
	await f.pilot._act("neutral-superseded")
	f.field.field_event.emit({"kind": "interact", "site": "neutral-site", "map": "neutral-map"})
	record("visible_replacement_cancels_pending_token_and_cannot_later_be_success",
		not f.pilot.stopping and f.engine.submitted.size() == 1
		and f.engine.feedback == [{"token": "neutral-superseded", "reached": false, "accepted": true}],
		{"feedback": f.engine.feedback, "submitted": f.engine.submitted, "stopping": f.pilot.stopping})
	dispose(f)

	f = fixture()
	f.engine.offer("neutral-stop-before-input")
	f.pilot._last_decision = {"Token": "neutral-stop-before-input"}
	f.pilot.stop("neutral-stop")
	record("stopping_after_selection_before_field_input_closes_pending_without_success",
		f.engine.closed and f.engine.submitted.is_empty()
		and f.engine.feedback == [{"token": "neutral-stop-before-input", "reached": false, "accepted": true}],
		{"feedback": f.engine.feedback, "closed": f.engine.closed})
	dispose(f)

	f = fixture()
	f.engine.offer("neutral-stop-during-path")
	f.pilot._last_decision = {"Token": "neutral-stop-during-path"}
	await f.pilot._act("neutral-stop-during-path")
	f.pilot.stop("neutral-stop")
	f.field.field_event.emit({"kind": "interact", "site": "neutral-site", "map": "neutral-map"})
	var accepted: Array = f.engine.feedback.filter(func(reply: Dictionary) -> bool: return reply.accepted)
	record("stopping_during_path_ends_contact_and_ignores_late_contact",
		accepted == [{"token": "neutral-stop-during-path", "reached": false, "accepted": true}]
		and not f.engine.feedback.any(func(reply: Dictionary) -> bool: return reply.reached), f.engine.feedback)
	dispose(f)

	var log := FileAccess.open(evidence, FileAccess.WRITE)
	for trace in traces: log.store_line(JSON.stringify({"trace": trace}))
	for outcome in outcomes: log.store_line(JSON.stringify(outcome))
	log.close()
	var valid := outcomes.all(func(outcome: Dictionary) -> bool: return outcome.holds)
	print("INTERACTION_RECEIPTS_COMPLETE " + str(valid))
	quit(0 if valid else 1)
