extends Node
## Executes only a C#-checked prepared interaction. All positions come from
## registered native metre-scale bodies and slots. This node never edits Core.
const Layers = preload("res://geometry/PhysicsLayers.gd")
var _transactions: Dictionary = {}
var _finished: Dictionary = {}
var _reservations: Dictionary = {}
var _next_visual_token := 1

func execute(intent: Dictionary, binding: Dictionary, participants: Node, epoch: int) -> Dictionary:
	var event_id := str(intent.get("action_event", ""))
	var key := str(epoch) + ":" + event_id
	if _transactions.has(key) or _finished.has(key):
		return {"duplicate":true,"action_event":event_id,"epoch":epoch}
	if event_id.is_empty() or str(intent.get("status", "")) != "prepared":
		return _rejected(event_id, epoch, "spatial-intent-not-prepared")
	if str(binding.get("action_event", "")) != event_id or str(binding.get("actor", "")) != str(intent.get("actor", "")) or str(binding.get("action_id", "")) != str(intent.get("action", {}).get("id", "")):
		return _remember(key, _rejected(event_id, epoch, "spatial-binding-mismatch"))
	if str(binding.get("status", "")) != "ready":
		return _remember(key, _rejected(event_id, epoch, str(binding.get("reason", "spatial-action-not-ready"))))
	if not is_instance_valid(participants) or not participants.is_inside_tree():
		return _remember(key, _rejected(event_id, epoch, "native-spatial-port-unavailable"))
	for method in ["resolve_spatial_actor", "resolve_spatial_object", "resolve_actor_contact_slot", "resolve_spatial_visual", "request_spatial_approach", "cancel_spatial_approach", "request_spatial_facing", "cancel_spatial_facing"]:
		if not participants.has_method(method):
			return _remember(key, _rejected(event_id, epoch, "native-spatial-port-missing:" + method))
	var actor_id := str(binding.actor)
	var target_id := str(binding.get("target_id", ""))
	var body = participants.resolve_spatial_actor(actor_id)
	var target = participants.resolve_spatial_object(target_id)
	var visual = participants.resolve_spatial_visual(actor_id)
	if not body is CharacterBody3D or not target is Node3D or not visual is Node3D or not target.has_method("resolve_interaction_slot"):
		return _remember(key, _rejected(event_id, epoch, "native-spatial-participant-unavailable"))
	if str(target.get_meta("world_object_id", "")) != target_id or body.get_world_3d() != target.get_world_3d():
		return _remember(key, _rejected(event_id, epoch, "native-spatial-world-mismatch"))
	var stand = target.resolve_interaction_slot(str(binding.stand_slot))
	var face = target.resolve_interaction_slot(str(binding.face_slot))
	var contact = target.resolve_interaction_slot(str(binding.contact_slot))
	var actor_contact = participants.resolve_actor_contact_slot(actor_id, str(binding.actor_contact_slot))
	var facing = participants.resolve_actor_contact_slot(actor_id, str(binding.actor_facing_slot))
	for marker in [stand,face,contact,actor_contact,facing]:
		if not marker is Marker3D or not _finite_node(marker):
			return _remember(key, _rejected(event_id, epoch, "native-spatial-slot-unavailable"))
	if not visual.has_method("play_visual") or not visual.has_method("cancel_visual") or not visual.has_method("active_generation") or not visual.has_method("active_token") or not visual.has_signal("visual_completed") or not visual.has_signal("visual_cancelled"):
		return _remember(key, _rejected(event_id, epoch, "native-spatial-visual-unavailable"))
	var reserve: Array[String] = ["actor:" + actor_id]
	for resource in binding.get("resource_ids", []): reserve.append("object:" + target_id + ":resource:" + str(resource))
	for resource in reserve:
		if _reservations.has(resource):
			return _remember(key, _rejected(event_id, epoch, "native-spatial-resource-busy:" + resource))
	for resource in reserve: _reservations[resource] = key
	_next_visual_token += 1
	var tx := {"key":key,"event":event_id,"epoch":epoch,"actor":actor_id,"target_id":target_id,
		"port":participants,"body":body,"target":target,"visual":visual,"stand":stand,"face":face,
		"contact":contact,"actor_contact":actor_contact,"facing":facing,"binding":binding,
		"resources":reserve,"abort":"","phase":"approach","phase_result":"",
		"visual_token":_next_visual_token,"generation":-1,"visual_started":false,"visual_finished":false,
		"contact_seen":false,"contact_samples":0,"started_usec":Time.get_ticks_usec(),"callbacks":[]}
	_transactions[key] = tx
	var approach_callback := func(token: String, who: String, result: String) -> void:
		if _transactions.get(key) == tx and token == key and who == actor_id and tx.phase == "approach": tx.phase_result = result
	var facing_callback := func(token: String, who: String, result: String) -> void:
		if _transactions.get(key) == tx and token == key and who == actor_id and tx.phase == "facing": tx.phase_result = result
	if not participants.has_signal("approach_finished") or not participants.has_signal("facing_finished"):
		return _finish(tx, "rejected", "native-spatial-completion-signals-missing")
	_connect(tx,participants,"approach_finished",approach_callback)
	_connect(tx,participants,"facing_finished",facing_callback)
	if not participants.request_spatial_approach(actor_id,stand.global_position,key):
		return _finish(tx,"rejected","native-spatial-approach-rejected")
	var reason := await _wait_motion(tx,float(binding.approach_timeout_s))
	if not reason.is_empty(): return _finish(tx,"rejected",reason)
	if not _at_stand(tx): return _finish(tx,"rejected","native-spatial-stand-not-reached")
	tx.phase = "facing"
	tx.phase_result = ""
	if not participants.request_spatial_facing(actor_id,face.global_position,key,str(binding.actor_facing_slot)):
		return _finish(tx,"rejected","native-spatial-facing-rejected")
	reason = await _wait_motion(tx,float(binding.turn_timeout_s))
	if not reason.is_empty(): return _finish(tx,"rejected",reason)
	if not _at_stand(tx) or not _facing_target(tx): return _finish(tx,"rejected","native-spatial-alignment-not-reached")
	if str(binding.contact_phase) == "before-and-finish" and not _contact_valid(tx):
		return _finish(tx,"rejected","native-spatial-contact-not-reached")
	tx.phase = "visual"
	var done_callback := func(token: int, generation: int) -> void:
		if _transactions.get(key) == tx and token == tx.visual_token and generation == tx.generation and tx.abort.is_empty(): tx.visual_finished = true
	var cancelled_callback := func(token: int, generation: int) -> void:
		if _transactions.get(key) == tx and token == tx.visual_token and generation == tx.generation and tx.abort.is_empty(): tx.abort = "native-spatial-visual-cancelled"
	_connect(tx,visual,"visual_completed",done_callback)
	_connect(tx,visual,"visual_cancelled",cancelled_callback)
	var clip: Dictionary = binding.visual
	if not visual.play_visual(str(clip.library),str(clip.clip),int(tx.visual_token),false,bool(clip.anchor_root),float(clip.speed)):
		return _finish(tx,"rejected","native-spatial-visual-start-failed")
	tx.visual_started = true
	tx.generation = int(visual.active_generation())
	if tx.generation < 0 or int(visual.active_token()) != int(tx.visual_token):
		return _finish(tx,"rejected","native-spatial-visual-token-mismatch")
	var until := Time.get_ticks_usec() + int(float(binding.visual_timeout_s)*1000000.0)
	while not tx.visual_finished:
		reason = _invalidated(tx)
		if not reason.is_empty(): return _finish(tx,"rejected",reason)
		if not _at_stand(tx): return _finish(tx,"rejected","native-spatial-actor-left-stand")
		if _contact_valid(tx): tx.contact_seen = true
		if Time.get_ticks_usec() >= until: return _finish(tx,"rejected","native-spatial-visual-timeout")
		await get_tree().physics_frame
	reason = _invalidated(tx)
	if not reason.is_empty(): return _finish(tx,"rejected",reason)
	if not _at_stand(tx) or not _facing_target(tx): return _finish(tx,"rejected","native-spatial-final-alignment-lost")
	if not _contact_valid(tx): return _finish(tx,"rejected","native-spatial-final-contact-lost")
	tx.contact_seen = true
	return _finish(tx,"applied","")

func cancel_all(reason := "native-spatial-scene-exited") -> void:
	for tx in _transactions.values(): _cancel(tx,reason)

func cancel_action(action_event: String, epoch: int, reason := "native-spatial-action-cancelled") -> bool:
	var tx: Dictionary = _transactions.get(str(epoch)+":"+action_event,{})
	if tx.is_empty(): return false
	_cancel(tx,reason)
	return true

func _cancel(tx: Dictionary, reason: String) -> void:
	if not str(tx.abort).is_empty(): return
	tx.abort = reason
	if is_instance_valid(tx.port):
		tx.port.cancel_spatial_approach(str(tx.actor),str(tx.key))
		tx.port.cancel_spatial_facing(str(tx.actor),str(tx.key))
	if tx.visual_started and is_instance_valid(tx.visual) and int(tx.visual.active_token()) == int(tx.visual_token) and int(tx.visual.active_generation()) == int(tx.generation):
		tx.visual.cancel_visual()

func _wait_motion(tx: Dictionary, seconds: float) -> String:
	var until := Time.get_ticks_usec()+int(seconds*1000000.0)
	while str(tx.phase_result).is_empty():
		var reason := _invalidated(tx)
		if not reason.is_empty(): return reason
		if Time.get_ticks_usec() >= until: return "native-spatial-"+str(tx.phase)+"-timeout"
		await get_tree().physics_frame
	var expected := "arrived" if str(tx.phase) == "approach" else "faced"
	if str(tx.phase_result) != expected:
		return "native-spatial-"+str(tx.phase)+":"+str(tx.phase_result)
	return _invalidated(tx)

func _invalidated(tx: Dictionary) -> String:
	if not str(tx.abort).is_empty(): return str(tx.abort)
	for node in [tx.port,tx.body,tx.target,tx.visual,tx.stand,tx.face,tx.contact,tx.actor_contact,tx.facing]:
		if not is_instance_valid(node) or not node.is_inside_tree(): return "native-spatial-participant-left"
	if not _rigid_root(tx.body) or not _rigid_root(tx.target): return "native-spatial-invalid-transform"
	if tx.body.get_world_3d() != tx.target.get_world_3d(): return "native-spatial-world-changed"
	if tx.port.resolve_spatial_actor(str(tx.actor)) != tx.body or tx.port.resolve_spatial_object(str(tx.target_id)) != tx.target:
		return "native-spatial-participant-replaced"
	return ""

func _at_stand(tx: Dictionary) -> bool:
	if not _invalidated(tx).is_empty() or not tx.body.is_on_floor(): return false
	var foot = _foot_position(tx.body)
	return foot is Vector3 and foot.is_finite() and foot.distance_to(tx.stand.global_position) <= float(tx.binding.arrival_tolerance_m)

func _foot_position(body: CharacterBody3D) -> Variant:
	var capsules: Array[CollisionShape3D] = []
	for child in body.get_children():
		if child is CollisionShape3D and not child.disabled and child.shape is CapsuleShape3D: capsules.append(child)
	if capsules.size() != 1: return null
	var capsule := capsules[0]
	if not _rigid_root(capsule) or capsule.global_transform.basis.y.distance_to(Vector3.UP) > 0.0001: return null
	if not is_finite(float(capsule.shape.height)) or float(capsule.shape.height) <= 0.0: return null
	return capsule.global_transform * Vector3(0.0,-float(capsule.shape.height)/2.0,0.0)

func _facing_target(tx: Dictionary) -> bool:
	var forward: Vector3 = tx.facing.global_transform.basis.z
	var toward: Vector3 = tx.face.global_position-tx.facing.global_position
	forward.y = 0.0
	toward.y = 0.0
	if forward.length_squared() < 0.000001 or toward.length_squared() < 0.000001: return false
	return forward.normalized().angle_to(toward.normalized()) <= float(tx.binding.facing_tolerance_rad)

func _contact_valid(tx: Dictionary) -> bool:
	tx.contact_samples += 1
	var source: Vector3 = tx.actor_contact.global_position
	var destination: Vector3 = tx.contact.global_position
	var tolerance := float(tx.binding.contact_tolerance_m)
	if not source.is_finite() or not destination.is_finite() or source.distance_to(destination) > tolerance: return false
	var world: World3D = tx.body.get_world_3d()
	var query := PhysicsShapeQueryParameters3D.new()
	var sphere := SphereShape3D.new()
	sphere.radius = tolerance
	query.shape = sphere
	query.transform = Transform3D(Basis.IDENTITY,source)
	query.collision_mask = Layers.SOLID
	query.exclude = [tx.body.get_rid()]
	var target_touched := false
	for hit in world.direct_space_state.intersect_shape(query,64):
		var collider = hit.get("collider")
		if collider is Node and (collider == tx.target or tx.target.is_ancestor_of(collider)): target_touched = true
	if not target_touched: return false
	if source.distance_squared_to(destination) > 0.00000001:
		var ray := PhysicsRayQueryParameters3D.create(source,destination,Layers.SOLID,[tx.body.get_rid()])
		ray.hit_from_inside = true
		var hit := world.direct_space_state.intersect_ray(ray)
		if not hit.is_empty():
			var collider = hit.get("collider")
			if not collider is Node or (collider != tx.target and not tx.target.is_ancestor_of(collider)): return false
	return true

func _connect(tx: Dictionary, node: Node, signal_name: String, callback: Callable) -> void:
	node.connect(signal_name,callback)
	tx.callbacks.append({"node":node,"signal":signal_name,"callback":callback})

func _finish(tx: Dictionary, outcome: String, reason: String) -> Dictionary:
	if _finished.has(tx.key): return {"duplicate":true,"action_event":tx.event,"epoch":tx.epoch}
	if outcome != "applied": _cancel(tx,reason)
	for connection in tx.callbacks:
		if is_instance_valid(connection.node) and connection.node.is_connected(connection.signal,connection.callback):
			connection.node.disconnect(connection.signal,connection.callback)
	for resource in tx.resources:
		if _reservations.get(resource) == tx.key: _reservations.erase(resource)
	_transactions.erase(tx.key)
	var receipt := {"action_event":tx.event,"epoch":tx.epoch,"outcome":outcome,
		"field":{"executor":"godot-native-spatial","protocol":"native-spatial-completion-v1",
		"reason":reason,"action_event":tx.event,"actor":tx.actor,"action_id":tx.binding.action_id,
		"spatial_execution_ref":tx.binding.spatial_execution_ref,"target_id":tx.target_id,
		"content_hash":str(tx.binding.get("content_hash", "")),
		"state_version":int(tx.binding.get("state_version", -1)),
		"visual_token":tx.visual_token,"visual_generation":tx.generation,"visual_finished":tx.visual_finished,
		"contact_seen":tx.contact_seen,"contact_samples":tx.contact_samples,
		"elapsed_host_seconds":float(Time.get_ticks_usec()-int(tx.started_usec))/1000000.0}}
	if outcome == "applied":
		var foot: Vector3 = _foot_position(tx.body)
		var forward: Vector3 = tx.facing.global_transform.basis.z
		var toward: Vector3 = tx.face.global_position-tx.facing.global_position
		forward.y = 0.0
		toward.y = 0.0
		receipt.field.final_on_floor = tx.body.is_on_floor()
		receipt.field.final_contact = true
		receipt.field.stand_error_m = foot.distance_to(tx.stand.global_position)
		receipt.field.contact_error_m = tx.actor_contact.global_position.distance_to(tx.contact.global_position)
		receipt.field.facing_error_rad = forward.normalized().angle_to(toward.normalized())
		receipt.field.foot_position = [foot.x,foot.y,foot.z]
		var target_point: Vector3 = tx.contact.global_position
		var source_point: Vector3 = tx.actor_contact.global_position
		receipt.field.contact_source = [source_point.x,source_point.y,source_point.z]
		receipt.field.contact_target = [target_point.x,target_point.y,target_point.z]
	return _remember(str(tx.key),receipt)

func _rejected(event_id: String, epoch: int, reason: String) -> Dictionary:
	return {"action_event":event_id,"epoch":epoch,"outcome":"rejected","field":{"executor":"godot-native-spatial","reason":reason,"visual_finished":false}}

func _remember(key: String, receipt: Dictionary) -> Dictionary:
	_finished[key] = true
	return receipt

func _finite_node(node: Node3D) -> bool:
	return node.global_transform.is_finite() and node.global_transform.basis.determinant() > 0.0

func _rigid_root(node: Node3D) -> bool:
	if not _finite_node(node): return false
	var basis := node.global_transform.basis
	return absf(basis.x.length()-1.0) <= 0.0001 and absf(basis.y.length()-1.0) <= 0.0001 and absf(basis.z.length()-1.0) <= 0.0001 and absf(basis.x.dot(basis.y)) <= 0.0001 and absf(basis.x.dot(basis.z)) <= 0.0001 and absf(basis.y.dot(basis.z)) <= 0.0001

func _exit_tree() -> void:
	cancel_all()
