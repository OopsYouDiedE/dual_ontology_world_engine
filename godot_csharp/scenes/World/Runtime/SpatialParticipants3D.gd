extends Node3D
## Native metre-scale participant registry and bounded direct approach.
## A scene must explicitly register its published actors and objects.
signal approach_finished(token: String, actor_id: String, result: String)
signal facing_finished(token: String, actor_id: String, result: String)

var _actors: Dictionary = {}
var _objects: Dictionary = {}
var _motions: Dictionary = {}
var _facings: Dictionary = {}

func get_spatial_participants() -> Node:
	return self

func register_spatial_actor(actor_id: String, body: CharacterBody3D, visual: Node, contact_slots: Dictionary, speed_mps: float, arrival_tolerance_m: float, foot_offset_m: float, stall_timeout_s: float, minimum_progress_m: float, gravity_mps2: float, turn_speed_degrees_s: float, turn_tolerance_degrees: float) -> bool:
	if actor_id.is_empty() or _actors.has(actor_id) or not is_instance_valid(body) or not is_instance_valid(visual) or not body.is_inside_tree() or not _rigid(body) or not body.is_ancestor_of(visual) or not is_finite(speed_mps) or not is_finite(arrival_tolerance_m) or not is_finite(foot_offset_m) or not is_finite(stall_timeout_s) or not is_finite(minimum_progress_m) or not is_finite(gravity_mps2) or not is_finite(turn_speed_degrees_s) or not is_finite(turn_tolerance_degrees) or speed_mps <= 0.0 or arrival_tolerance_m <= 0.0 or foot_offset_m < 0.0 or stall_timeout_s <= 0.0 or minimum_progress_m <= 0.0 or gravity_mps2 <= 0.0 or turn_speed_degrees_s <= 0.0 or turn_tolerance_degrees <= 0.0 or turn_tolerance_degrees >= 180.0:
		return false
	var slots := {}
	for key in contact_slots:
		var marker = contact_slots[key]
		if not key is String or key.is_empty() or not marker is Marker3D or not is_instance_valid(marker) or not body.is_ancestor_of(marker) or not _valid_contact_transform(marker): return false
		slots[key] = weakref(marker)
	_actors[actor_id] = {"body":weakref(body),"visual":weakref(visual),"contacts":slots,"speed":speed_mps,"tolerance":arrival_tolerance_m,"foot_offset":foot_offset_m,"stall_timeout":stall_timeout_s,"minimum_progress":minimum_progress_m,"gravity":gravity_mps2,"turn_speed":turn_speed_degrees_s,"turn_tolerance":turn_tolerance_degrees}
	return true

func register_spatial_object(object_id: String, object: Node3D) -> bool:
	if object_id.is_empty() or _objects.has(object_id) or not is_instance_valid(object) or not object.is_inside_tree() or not _rigid(object) or str(object.get_meta("world_object_id", "")) != object_id:
		return false
	_objects[object_id] = weakref(object)
	return true

func resolve_spatial_actor(actor_id: String) -> CharacterBody3D:
	if not _actors.has(actor_id): return null
	return _actors[actor_id].body.get_ref() as CharacterBody3D

func resolve_spatial_object(object_id: String) -> Node3D:
	if not _objects.has(object_id): return null
	return _objects[object_id].get_ref() as Node3D

func resolve_actor_contact_slot(actor_id: String, slot_id: String) -> Marker3D:
	var body := resolve_spatial_actor(actor_id)
	if body == null or not _actors[actor_id].contacts.has(slot_id): return null
	var marker := _actors[actor_id].contacts[slot_id].get_ref() as Marker3D
	if marker == null or not body.is_ancestor_of(marker) or not _valid_contact_transform(marker): return null
	return marker

func resolve_spatial_visual(actor_id: String) -> Node:
	var body := resolve_spatial_actor(actor_id)
	if body == null: return null
	var visual := _actors[actor_id].visual.get_ref() as Node
	if visual == null or not body.is_ancestor_of(visual): return null
	return visual

## This driver supports a direct unobstructed approach on one elevation only.
## It never teleports or completes an interaction; collision/stall rejects it.
func request_spatial_approach(actor_id: String, stand_world: Vector3, token: String) -> bool:
	var body := resolve_spatial_actor(actor_id)
	if token.is_empty() or not stand_world.is_finite() or body == null or not body.is_inside_tree() or not _rigid(body) or _motions.has(actor_id) or _facings.has(actor_id): return false
	for state in _motions.values() + _facings.values():
		if str(state.token) == token: return false
	var tolerance := float(_actors[actor_id].tolerance)
	if absf(body.global_position.y - float(_actors[actor_id].foot_offset) - stand_world.y) > tolerance: return false
	_motions[actor_id] = {"token":token,"target":stand_world,"last_distance":INF,"stalled_seconds":0.0}
	return true

func request_spatial_facing(actor_id: String, face_world: Vector3, token: String, actor_facing_slot: String) -> bool:
	var body := resolve_spatial_actor(actor_id)
	var marker := resolve_actor_contact_slot(actor_id,actor_facing_slot)
	if token.is_empty() or not face_world.is_finite() or body == null or marker == null or not _upright(body) or not body.is_on_floor() or _motions.has(actor_id) or _facings.has(actor_id): return false
	var delta := face_world - marker.global_position
	var forward := marker.global_transform.basis.z
	if Vector2(delta.x,delta.z).length_squared() < 0.00000001 or Vector2(forward.x,forward.z).length_squared() < 0.00000001: return false
	for state in _motions.values() + _facings.values():
		if str(state.token) == token: return false
	_facings[actor_id] = {"token":token,"target":face_world,"slot":actor_facing_slot}
	return true

func cancel_spatial_facing(actor_id: String, token: String) -> bool:
	if not _facings.has(actor_id) or str(_facings[actor_id].token) != token: return false
	_finish_facing(actor_id,"cancelled")
	return true

func cancel_spatial_approach(actor_id: String, token: String) -> bool:
	if not _motions.has(actor_id) or str(_motions[actor_id].token) != token: return false
	_finish(actor_id,"cancelled")
	return true

func _physics_process(delta: float) -> void:
	if not is_finite(delta) or delta <= 0.0:
		for actor_id in _motions.keys(): _finish(actor_id,"invalid-physics-step")
		for actor_id in _facings.keys(): _finish_facing(actor_id,"invalid-physics-step")
		return
	for actor_id in _facings.keys():
		var body := resolve_spatial_actor(actor_id)
		var state: Dictionary = _facings[actor_id]
		var marker := resolve_actor_contact_slot(actor_id,str(state.slot))
		if body == null or marker == null or not _upright(body) or not body.is_on_floor():
			_finish_facing(actor_id,"actor-or-marker-unavailable")
			continue
		var target: Vector3 = state.target
		var forward := marker.global_transform.basis.z
		var direction := target - marker.global_position
		forward.y = 0.0
		direction.y = 0.0
		if forward.length_squared() < 0.00000001 or direction.length_squared() < 0.00000001:
			_finish_facing(actor_id,"invalid-facing-direction")
			continue
		forward = forward.normalized()
		direction = direction.normalized()
		var angle := atan2(forward.cross(direction).y,forward.dot(direction))
		if absf(angle) <= deg_to_rad(float(_actors[actor_id].turn_tolerance)):
			_finish_facing(actor_id,"faced")
			continue
		body.rotate_y(clampf(angle,-deg_to_rad(float(_actors[actor_id].turn_speed))*delta,deg_to_rad(float(_actors[actor_id].turn_speed))*delta))
	for actor_id in _motions.keys():
		var body := resolve_spatial_actor(actor_id)
		if body == null or not body.is_inside_tree() or not _rigid(body):
			_finish(actor_id,"actor-unavailable")
			continue
		var state: Dictionary = _motions[actor_id]
		var target: Vector3 = state.target
		var offset := Vector2(target.x-body.global_position.x,target.z-body.global_position.z)
		var distance := offset.length()
		var elevation_error := absf(body.global_position.y - float(_actors[actor_id].foot_offset) - target.y)
		if elevation_error > float(_actors[actor_id].tolerance):
			_finish(actor_id,"elevation-changed")
			continue
		if distance <= float(_actors[actor_id].tolerance):
			if body.is_on_floor():
				_finish(actor_id,"arrived")
			else:
				body.velocity.x = 0.0
				body.velocity.z = 0.0
				body.velocity.y -= float(_actors[actor_id].gravity) * delta
				body.move_and_slide()
				if body.velocity.y <= 0.0: body.apply_floor_snap()
			continue
		if distance >= float(state.last_distance) - float(_actors[actor_id].minimum_progress): state.stalled_seconds += delta
		else:
			state.stalled_seconds = 0.0
			state.last_distance = distance
		if float(state.stalled_seconds) >= float(_actors[actor_id].stall_timeout):
			_finish(actor_id,"blocked")
			continue
		var direction := offset / distance
		var speed := minf(float(_actors[actor_id].speed),distance / maxf(delta,0.0001))
		body.velocity.x = direction.x * speed
		body.velocity.z = direction.y * speed
		body.velocity.y -= float(_actors[actor_id].gravity) * delta
		body.move_and_slide()
		if body.velocity.y <= 0.0: body.apply_floor_snap()

func _finish(actor_id: String, result: String) -> void:
	if not _motions.has(actor_id): return
	var token := str(_motions[actor_id].token)
	_motions.erase(actor_id)
	var body := resolve_spatial_actor(actor_id)
	if body != null:
		body.velocity.x = 0.0
		body.velocity.z = 0.0
	approach_finished.emit(token,actor_id,result)

func _exit_tree() -> void:
	for actor_id in _motions.keys(): _finish(actor_id,"scene-exited")
	for actor_id in _facings.keys(): _finish_facing(actor_id,"scene-exited")

func _finish_facing(actor_id: String, result: String) -> void:
	if not _facings.has(actor_id): return
	var token := str(_facings[actor_id].token)
	_facings.erase(actor_id)
	facing_finished.emit(token,actor_id,result)

func _upright(node: Node3D) -> bool:
	return _rigid(node) and node.global_transform.basis.y.dot(Vector3.UP) > 0.99999

func _rigid(node: Node3D) -> bool:
	if not node.is_inside_tree() or not node.global_position.is_finite(): return false
	var b := node.global_transform.basis
	return b.x.is_finite() and b.y.is_finite() and b.z.is_finite() and b.determinant() > 0.0 and absf(b.x.length_squared()-1.0) < 0.00001 and absf(b.y.length_squared()-1.0) < 0.00001 and absf(b.z.length_squared()-1.0) < 0.00001 and absf(b.x.dot(b.y)) < 0.00001 and absf(b.x.dot(b.z)) < 0.00001 and absf(b.y.dot(b.z)) < 0.00001

## A marker may inherit model scale. It must still have finite orthogonal axes.
func _valid_contact_transform(node: Node3D) -> bool:
	if not node.is_inside_tree() or not node.global_position.is_finite(): return false
	var b := node.global_transform.basis
	if not b.x.is_finite() or not b.y.is_finite() or not b.z.is_finite() or b.determinant() <= 0.0 or b.x.length_squared() < 0.00000001 or b.y.length_squared() < 0.00000001 or b.z.length_squared() < 0.00000001: return false
	var x := b.x.normalized()
	var y := b.y.normalized()
	var z := b.z.normalized()
	return absf(x.dot(y)) < 0.00001 and absf(x.dot(z)) < 0.00001 and absf(y.dot(z)) < 0.00001
