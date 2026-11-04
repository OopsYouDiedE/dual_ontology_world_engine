extends RefCounted
## Executes a prepared conversation in the native physical world.
## Core remains responsible for willingness, claims, and need effects.

func execute(intent: Dictionary, port: Node, owner: Node, current: Callable) -> Dictionary:
	var spec: Dictionary = intent.conversation_execution
	var duration: float = owner.presentation_duration(intent)
	if duration < 0.0: return _receipt("rejected", "presentation-timing-unavailable", Time.get_ticks_usec())
	spec = spec.duplicate(true)
	spec.duration_seconds = duration
	var ids: Array = [str(intent.actor)]
	for id in spec.participants:
		if str(id) not in ids: ids.append(str(id))
	var bodies: Array = []
	var token := "conversation:" + str(intent.action_event)
	var started := Time.get_ticks_usec()
	if not is_instance_valid(port): return _receipt("rejected", "conversation-port-unavailable", started)
	for id in ids:
		var body = port.resolve_spatial_actor(id)
		if not is_instance_valid(body) or not body.has_method("start_conversation"):
			return _receipt("rejected", "conversation-participant-unavailable:" + id, started)
		if body.get_meta("conversation_owner", "") != "":
			return _receipt("rejected", "conversation-participant-busy:" + id, started)
		bodies.append(body)
	for body in bodies: body.set_meta("conversation_owner", token)
	var reason := await _perform(spec, ids, bodies, port, owner, current, token)
	for id in ids:
		if is_instance_valid(port):
			port.cancel_spatial_approach(id, token + ":approach:" + id)
			port.cancel_spatial_facing(id, token + ":face:" + id)
	for body in bodies:
		if is_instance_valid(body) and body.get_meta("conversation_owner", "") == token:
			body.end_conversation()
			body.remove_meta("conversation_owner")
	var result := _receipt("applied" if reason.is_empty() else "rejected", reason, started)
	result.participants = ids
	result.duration_seconds = spec.duration_seconds
	return result

func _perform(spec: Dictionary, ids: Array, bodies: Array, port: Node, owner: Node, current: Callable, token: String) -> String:
	var speaker = bodies[0]
	var partner = bodies[1]
	if speaker.get_world_3d() != partner.get_world_3d(): return "conversation-world-mismatch"
	var offset: Vector3 = speaker.global_position - partner.global_position
	offset.y = 0.0
	if offset.length() > float(spec.distance_m):
		if bool(speaker.is_seated) or bool(speaker.is_lying): return "conversation-posture-needs-transition"
		var stand: Vector3 = port.resolve_actor_foot_position(ids[1]) + offset.normalized() * float(spec.distance_m)
		var reason := await _motion(port, owner, current, ids[0], token + ":approach:" + ids[0], stand, "", float(spec.approach_timeout_s))
		if not reason.is_empty(): return reason
	for index in bodies.size():
		if not current.call(): return "conversation-session-changed"
		var body = bodies[index]
		var target = partner if index == 0 else speaker
		if not is_instance_valid(body) or not is_instance_valid(target): return "conversation-participant-left"
		if body.get_world_3d() != speaker.get_world_3d(): return "conversation-world-mismatch"
		if body.global_position.distance_to(target.global_position) > float(spec.maximum_distance_m): return "conversation-out-of-range"
		# Seated partners keep their seat; their normal conversation tracking turns the upper body.
		if not bool(body.is_seated) and not bool(body.is_lying):
			var reason := await _motion(port, owner, current, ids[index], token + ":face:" + ids[index], target.global_position, str(spec.facing_slot), float(spec.turn_timeout_s))
			if not reason.is_empty(): return reason
		body.start_conversation(target)
		if body.has_method("set_conversation_speaking"):
			body.set_conversation_speaking(index == 0)
	var until: float = owner.presentation_seconds() + float(spec.duration_seconds)
	while owner.presentation_seconds() < until:
		if not current.call(): return "conversation-session-changed"
		for body in bodies:
			if not is_instance_valid(body) or not body.is_inside_tree() or not body.is_conversing: return "conversation-interrupted"
			if body.global_position.distance_to(speaker.global_position) > float(spec.maximum_distance_m): return "conversation-participant-left"
		await owner.get_tree().physics_frame
	return ""

func _motion(port: Node, owner: Node, current: Callable, actor: String, token: String, point: Vector3, facing: String, timeout: float) -> String:
	var result := {"status":""}
	var callback := func(received: String, who: String, status: String) -> void:
		if received == token and who == actor: result.status = status
	var signal_name := "approach_finished" if facing.is_empty() else "facing_finished"
	port.connect(signal_name, callback)
	var accepted: bool = port.request_spatial_approach(actor, point, token) if facing.is_empty() else port.request_spatial_facing(actor, point, token, facing)
	var until: float = owner.presentation_seconds() + timeout
	while accepted and result.status == "" and current.call() and owner.presentation_seconds() < until:
		await owner.get_tree().physics_frame
	if is_instance_valid(port) and port.is_connected(signal_name, callback): port.disconnect(signal_name, callback)
	if not current.call(): return "conversation-session-changed"
	if not accepted: return "conversation-motion-rejected"
	if result.status != ("arrived" if facing.is_empty() else "faced"): return "conversation-motion-" + (str(result.status) if result.status != "" else "timeout")
	return ""

func _receipt(outcome: String, reason: String, started: int) -> Dictionary:
	return {"outcome":outcome,"executor":"godot-native-conversation","reason":reason,
		"elapsed_host_seconds":float(Time.get_ticks_usec()-started)/1000000.0}
