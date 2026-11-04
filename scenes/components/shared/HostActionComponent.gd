extends RefCounted
## HostActionComponent owns host-side presentation work; session access goes through Main.
var host: Node
var _queued_player_input: Dictionary = {}
var _execution_started_usec := 0
var _world_clock_cursor_usec := 0
var _world_clock_generation := -1

func _init(main: Node) -> void:
	host = main

func _case_reasoning_ui(input: Dictionary) -> void:
	var request: Dictionary = input.get("request", {})
	var serial := int(input.get("serial", -1))
	var generation: int = host._session_generation
	var target = host.ui
	var response: Dictionary = await host.request_case_reasoning(request)
	if generation != host._session_generation or not is_instance_valid(target) or target != host.ui: return
	target.case_reasoning_response(serial, request, response)

func _field_input_busy() -> bool:
	# Local movement only owns geometry. Background NPC work must not freeze
	# an otherwise free player's feet; formal actions have a separate gate.
	return _player_movement_active() or (is_instance_valid(host.dev_console) and host.dev_console.is_open()) or host.flow.blocks_input() or not is_instance_valid(host.ui) or host.ui.is_busy()

func _player_movement_active() -> bool:
	if not is_instance_valid(host.field): return false
	var viewer := str(host.field.snapshot.get("viewer", ""))
	for record: Dictionary in host._detached_movements.values():
		if str(record.intent.get("actor", "")) == viewer: return true
	for record: Dictionary in host._detached_activities.values():
		if str(record.intent.get("actor", "")) == viewer: return true
	return false

func _formal_selection_ready() -> bool:
	return not host._field_input_busy() and _queued_player_input.is_empty() and not host.flow.book_reading 		and is_instance_valid(host.field) and not host.field.snapshot.get("player_options", []).is_empty()

func flush_player_input() -> bool:
	if _queued_player_input.is_empty(): return false
	if host.flow.closing: return true
	if host._field_input_busy() or host.flow.draining or host.flow.completion_pending or host.flow.background_execution 		or not host._queue.is_empty() or not host._dv_intent.is_empty() or not host.session_is_idle(): return true
	if str(host.observe_session().get("pending", {}).get("status", "")) == "prepared": return true
	var input := _queued_player_input
	_queued_player_input = {}
	host._interaction_id = str(input.interaction_id)
	host._interaction_site = str(input.site)
	host.dispatch(input)
	return true

func _field_input(input: Dictionary) -> void:
	if host.flow.closing: return
	match str(input.get("kind", "")):
		"timing": host.record_timing(input)
		"prepare_ahead":
			if host._formal_selection_ready(): host._submit_session(input)
		"cancel_ahead":
			host._preview_tokens.clear()
			host._submit_session(input)
		"locomotion":
			if str(input.get("actor", "")) != str(host.field._player_key): return
			var body: CharacterBody3D = host.field.player.physical_body
			var point: Array = input.get("to_position", [])
			if not is_instance_valid(body) or point.size() != 3: return
			if body.global_position.distance_squared_to(Vector3(point[0],point[1],point[2])) > 0.000001: return
			host._submit_session(input)
		"select":
			if not host._formal_selection_ready():
				host.field.spatial.release_submission()
				return
			_queued_player_input = input.duplicate(true)
			flush_player_input()
		"interaction_hint":
			if is_instance_valid(host.ui): host.ui.show_aside(str(input.get("text", "")))
		"spatial_choices":
			host._interaction_id = str(input.interaction_id)
			host._timing("local_choices_ready")
			host.ui.spatial_choices(input)

func _accept_execution(input: Dictionary) -> void:
	if host._dv_intent.is_empty(): return
	var action: Dictionary = host._dv_intent.action
	var destination: Dictionary = host._dv_intent.get("destination", {})
	var arrived = str(input.get("kind", "")) == "daily" and str(input.get("status", "")) == "arrived"
	arrived = arrived and int(input.get("serial", -1)) == host._dv_serial
	arrived = arrived and str(input.get("who", "")) == str(host._dv_intent.actor) and str(input.get("action", "")) == str(action.id)
	arrived = arrived and str(input.get("room", "")) == str(destination.get("execution_room", destination.get("room", "")))
	var travel: Dictionary = action.get("travel", {})
	if travel.has("station"):
		arrived = arrived and str(input.get("station", "")) == str(travel.station)
		if str(input.get("execution_world", "")) != "room-graph":
			arrived = arrived and input.has("arrival_error") and float(input.get("arrival_error", 5.0)) <= 4.0
	var execution = input
	host._dv_complete("applied" if arrived else "rejected", execution)

func _background_npc_intent(ev: Dictionary) -> bool:
	if host.flow.background_execution or not is_instance_valid(host.field): return false
	var intent: Dictionary = ev.get("intent", {})
	return str(intent.get("status", "")) == "prepared" and str(intent.get("actor", "")) != str(host.field.snapshot.get("viewer", ""))

func _execute_reading_npc(ev: Dictionary) -> void:
	host.flow.transition("background_execution", true)
	host._timing("reading_npc_execution_begin", {"actor": ev.intent.actor, "action_event": ev.intent.action_event})
	await host._dv_execute(ev.intent, ev.get("execution_snapshot", {}))
	host._timing("reading_npc_execution_end", {"actor": ev.intent.actor, "action_event": ev.intent.action_event})
	host.flow.transition("background_execution", false)

func _dv_execute(intent: Dictionary, execution_snapshot: Dictionary) -> void:
	if str(intent.get("status", "")) == "in_flight":
		# This is a scheduling boundary, not a player's selection menu.
		host.flow.transition("player_options_open", true)
		if is_instance_valid(host.ui): host.ui.set_player_options([])
		return
	if str(intent.get("status", "")) == "awaiting_input":
		host.field.spatial.set_options(intent.options)
		host.flow.transition("player_options_open", true)
		if is_instance_valid(host.ui): host.ui.set_player_options(intent.options)
		host._timing("player_options_ready", {"options": intent.options.size()})
		return
	if str(intent.get("status", "")) != "prepared": return
	host._dv_intent = intent
	_execution_started_usec = Time.get_ticks_usec()
	if _world_clock_generation != host._session_generation:
		_world_clock_generation = host._session_generation
		_world_clock_cursor_usec = _execution_started_usec
	host.flow.transition("player_options_open", false)
	host._dv_serial += 1
	var action: Dictionary = intent.action
	host._timing("execution_begin", {"action_event": intent.action_event, "actor": intent.actor})
	if bool(intent.get("background_activity", action.get("background_activity", false))):
		var epoch: int = host._session_generation
		var event_id := str(intent.action_event)
		host._submit_session({"kind": "detach_activity", "action_event": event_id})
		while not host._movement_detach_answers.has(event_id) and epoch == host._session_generation and not host.flow.closing:
			await host.get_tree().process_frame
		if epoch != host._session_generation or host.flow.closing: return
		var detached: bool = host._movement_detach_answers.get(event_id, false)
		host._movement_detach_answers.erase(event_id)
		if detached:
			var body: Node = null
			var record: Dictionary = host.field_executor._bodies.get(str(intent.actor), {})
			if not record.is_empty() and is_instance_valid(record.get("body")): body = record.body
			if is_instance_valid(body): body.set_meta("background_activity_owner", event_id)
			host._detached_activities[event_id] = {"intent": intent.duplicate(true), "epoch": epoch, "body": weakref(body) if is_instance_valid(body) else null}
			host._dv_intent = {}
			host._movement_schedule_pending = true
			# The host owns the activity lifetime. GameEngine holds this original
			# receipt until the actual Core computation completes or fails.
			host._submit_session({"kind": "dv_complete", "receipt": {"action_event": event_id, "outcome": "applied", "actual": intent.execution_cost,
				"executor": "godot", "field": {"executor": "dv-world", "background_activity": true,
				"host_elapsed_seconds": float(Time.get_ticks_usec() - _execution_started_usec) / 1000000.0,
				"host_elapsed_basis": "godot-monotonic-prepared-to-receipt"}}})
			return
	if (str(action.get("kind", "")) == "wait" or str(action.get("kind", "")).begins_with("wait.")) and str(intent.get("actor", "")) == str(host.field.snapshot.get("viewer", "")):
		# Core commits the declared waiting cost. Rendering latency does not own time.
		var epoch = host._session_generation
		var serial = host._dv_serial
		var duration: float = host.presentation_duration(intent)
		if duration < 0.0:
			host._dv_complete("rejected", {"executor": "godot-wait", "reason": "presentation-timing-unavailable"})
			return
		var until_seconds: float = host.presentation_seconds() + duration
		while host.presentation_seconds() < until_seconds:
			await host.get_tree().process_frame
			if epoch != host._session_generation or serial != host._dv_serial: return
			if bool(action.get("interruptible", false)) and Input.is_action_just_pressed("ui_cancel"):
				host._dv_complete("cancelled", {"executor": "godot-wait", "reason": "player-cancelled-wait"})
				return
		host._dv_complete("applied", {"executor": "godot-wait"})
	elif action.has("spatial_execution_ref"):
		var epoch = host._session_generation
		var serial = host._dv_serial
		var action_event = str(intent.action_event)
		var participants: Node = null
		if is_instance_valid(host.field) and host.field.has_method("get_spatial_participants"):
			participants = host.field.get_spatial_participants()
		await host.character_controller3d.execute_prepared(intent, intent.spatial_execution, participants, epoch)
	elif str(action.kind) == "move" or action.get("travel", {}).has("station"):
		var serial: int = host._dv_serial
		var epoch: int = host._session_generation
		var event_id := str(intent.action_event)
		var can_detach: bool = str(action.kind) == "move" and not action.get("travel", {}).has("station")
		can_detach = can_detach and host.field_executor.can_detach_movement(intent, execution_snapshot, host.field)
		if can_detach:
			host._submit_session({"kind": "detach_movement", "action_event": event_id})
			while not host._movement_detach_answers.has(event_id) and epoch == host._session_generation and not host.flow.closing:
				await host.get_tree().process_frame
			if epoch != host._session_generation or host.flow.closing: return
			var detached: bool = host._movement_detach_answers.get(event_id, false)
			host._movement_detach_answers.erase(event_id)
			if detached:
				host._detached_movements[event_id] = {"intent": intent.duplicate(true), "serial": serial, "started_usec": _execution_started_usec, "epoch": epoch}
				host._dv_intent = {}
				host._movement_schedule_pending = true
				host.execute_character_movement.call_deferred(intent, execution_snapshot, serial)
				return
		await host.execute_character_movement(intent, execution_snapshot, serial)
	elif intent.get("conversation_execution", false) is Dictionary:
		var epoch: int = host._session_generation
		var serial: int = host._dv_serial
		var controller = preload("res://scenes/components/characters/ConversationController3D.gd").new()
		var current := func() -> bool: return is_instance_valid(host) and epoch == host._session_generation and serial == host._dv_serial
		var context: Node2D = await host.field_executor.conversation_context(intent, execution_snapshot, host.field)
		if not current.call(): return
		if not is_instance_valid(context):
			host._dv_complete("rejected", {"executor": "godot-native-conversation", "reason": "conversation-context-unavailable"})
			return
		var measured: Dictionary = await controller.execute(intent, context.get_spatial_participants(), host, current)
		measured.execution_world = "isolated-godot" if context.execution_only else "visible-godot"
		if current.call(): host._dv_complete(str(measured.outcome), measured)
	else:
		if bool(intent.get("presentation_hold", action.get("presentation_hold", false))):
			var duration: float = host.presentation_duration(intent)
			if duration < 0.0:
				host._dv_complete("rejected", {"executor": "godot", "reason": "presentation-timing-unavailable"})
				return
			var epoch: int = host._session_generation
			var serial: int = host._dv_serial
			var until_seconds: float = host.presentation_seconds() + duration
			while host.presentation_seconds() < until_seconds:
				await host.get_tree().process_frame
				if epoch != host._session_generation or serial != host._dv_serial: return
		var site = host._interaction_site if str(intent.actor) == str(host.field.snapshot.get("viewer", "")) else ""
		var contact = true
		if site != "" and site != "self":
			contact = host.field.spatial._in_reach(site)
		host._dv_complete("applied" if contact else "rejected", {"executor": "dv-world", "site": site, "contact": contact, "context": host.field.context()})

func _finish_background_activity(event_id: String) -> void:
	var record: Dictionary = host._detached_activities.get(event_id, {})
	if record.is_empty(): return
	var reference: Variant = record.get("body")
	var body: Variant = reference.get_ref() if reference is WeakRef else null
	if is_instance_valid(body) and str(body.get_meta("background_activity_owner", "")) == event_id:
		body.remove_meta("background_activity_owner")
	host._detached_activities.erase(event_id)

func _complete_detached_movement(receipt: Dictionary) -> void:
	var event_id := str(receipt.action_event)
	var record: Dictionary = host._detached_movements.get(event_id, {})
	if record.is_empty() or int(record.epoch) != host._session_generation: return
	var intent: Dictionary = record.intent
	var action: Dictionary = intent.action
	var destination: Dictionary = intent.get("destination", {})
	var execution: Dictionary = receipt.get("movement", {}).duplicate(true)
	var arrived: bool = str(receipt.get("receipt_kind", "")) == "movement" and str(execution.get("kind", "")) == "daily" and str(execution.get("status", "")) == "arrived"
	arrived = arrived and int(execution.get("serial", -1)) == int(record.serial) and str(execution.get("who", "")) == str(intent.actor) and str(execution.get("action", "")) == str(action.id)
	arrived = arrived and str(execution.get("room", "")) == str(destination.get("execution_room", destination.get("room", "")))
	var interrupted: bool = host.field_executor.verified_interruption(event_id, execution)
	execution.host_elapsed_seconds = float(Time.get_ticks_usec() - int(record.started_usec)) / 1000000.0
	execution.host_elapsed_basis = "godot-monotonic-prepared-to-receipt"
	var result := {"action_event": event_id, "outcome": "cancelled" if interrupted else ("applied" if arrived else "rejected"), "actual": intent.execution_cost,
		"executor": "room-graph" if str(execution.get("execution_world", "")) == "room-graph" else "godot", "field": execution}
	host._detached_movements.erase(event_id)
	# GameEngine serializes a physical arrival behind any current foreground receipt.
	# It must not replace that foreground intent or its completion gate.
	host._submit_session({"kind": "dv_complete", "receipt": result})

func _dv_complete(outcome: String, execution: Dictionary) -> void:
	if host._dv_intent.is_empty(): return
	execution = execution.duplicate(true)
	var completed_usec := Time.get_ticks_usec()
	execution["host_elapsed_seconds"] = float(completed_usec - _execution_started_usec) / 1000000.0
	# This is diagnostic evidence only; the Core commits execution_cost.
	_world_clock_cursor_usec = completed_usec
	execution["host_elapsed_basis"] = "godot-monotonic-prepared-to-receipt"
	host._timing("execution_end", {"outcome": outcome, "action_event": host._dv_intent.action_event})
	var receipt = {"action_event": host._dv_intent.action_event, "outcome": outcome, "actual": host._dv_intent.execution_cost, "executor": "room-graph" if str(execution.get("execution_world", "")) == "room-graph" else "godot", "field": execution}
	host.flow.transition("player_options_open", false)
	# NPCs may finish before a queued player action. Keep that player's
	# contact until their own receipt, so delayed inspection is still checked.
	if str(host._dv_intent.get("actor", "")) == str(host.field.snapshot.get("viewer", "")):
		host._interaction_site = ""
	host._dv_intent = {}
	host.flow.transition("completion_pending", true)
	host.dispatch.call_deferred({"kind": "dv_complete", "receipt": receipt})


func exclude_paused_time(microseconds: int) -> void:
	if _world_clock_cursor_usec != 0: _world_clock_cursor_usec += microseconds
