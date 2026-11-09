extends CharacterBody3D
## Metre-scale collision body and KayKit animated character driver (Tasks 5.01 - 5.12).
const PhysicsLayers = preload("res://scenes/components/world/PhysicsLayers.gd")
const ActionQueueClass = preload("res://scenes/components/characters/ActorActionQueue.gd")

signal action_completed(action: String)
signal seat_state_changed(seated: bool)
signal bed_state_changed(lying: bool)
signal carry_state_changed(carrying: bool)
signal conversation_interrupted(reason: String)
signal yield_state_changed(is_yielding: bool, peer: Object)
signal action_rolled_back(action: String, reason: String)

var physical_body: CharacterBody3D:
	get: return self
var travel_result: Dictionary = {}

const NOMINAL_HEIGHT: float = 1.32
const COLLISION_RADIUS: float = 0.18
const EYE_HEIGHT: float = 1.15
const TURN_SPEED: float = 12.0 # rad/s
const INTERACTION_DISTANCE_MAX: float = 0.35 # Metres from stand slot
const CONTACT_DISTANCE_MAX: float = 1.25 # Metres to contact slot
const CONVERSATION_DISTANCE_MAX: float = 3.5 # Metres for conversation

class SpatialVisualBridge extends Node3D:
	signal visual_completed(token: int, generation: int)
	signal visual_cancelled(token: int, generation: int)
	var _token: int = 0
	var _generation: int = 0
	var _body_ref: WeakRef = null
	var _expected_animation := ""
	var _active := false
	var last_start_failure := ""
	
	func set_body(body: CharacterBody3D) -> void:
		_body_ref = weakref(body)
	
	func play_visual(library: String, clip: String, token: int, loop: bool, anchor_root: bool, speed: float) -> bool:
		var b = _body_ref.get_ref() if _body_ref != null else null
		last_start_failure = "invalid-visual-request"
		if b == null or not is_instance_valid(b) or _active or loop or not anchor_root or not is_finite(speed) or speed <= 0.0 or clip.is_empty(): return false
		var player: AnimationPlayer = b.animation_player
		var animation_name := library + "/" + clip if not library.is_empty() else clip
		last_start_failure = "animation-player-unavailable"
		if not is_instance_valid(player): return false
		last_start_failure = "declared-animation-unavailable:" + animation_name
		if not player.has_animation(animation_name): return false
		var animation := player.get_animation(animation_name)
		last_start_failure = "declared-animation-looping-or-unavailable:" + animation_name
		if animation == null or animation.loop_mode != Animation.LOOP_NONE: return false
		# Formal interaction playback uses the declared native clip exactly.
		# Decorative action aliases and their idle fallback cannot certify completion.
		_token = token
		_generation += 1
		_expected_animation = animation_name
		_active = true
		b.is_transitioning = true
		b.current_action = animation_name
		player.stop()
		player.speed_scale = speed
		player.play(animation_name)
		last_start_failure = ""
		return true
	
	func cancel_visual() -> void:
		if not _active: return
		_active = false
		_expected_animation = ""
		var b = _body_ref.get_ref() if _body_ref != null else null
		if b != null and is_instance_valid(b) and b.has_method("play_action"):
			if is_instance_valid(b.animation_player): b.animation_player.stop()
			b.is_transitioning = false
			b.play_action("idle", 1.0)
		visual_cancelled.emit(_token, _generation)
	
	func active_generation() -> int:
		return _generation
	
	func active_token() -> int:
		return _token
	
	func notify_visual_finished(clip_name: String) -> void:
		if not _active or clip_name != _expected_animation: return
		_active = false
		_expected_animation = ""
		visual_completed.emit(_token, _generation)

var visual: Node3D
var animation_player: AnimationPlayer
var collider: CollisionShape3D

# Standard spatial contact markers
var marker_hand_right: Marker3D = null
var marker_hand_left: Marker3D = null
var marker_chest: Marker3D = null
var marker_head: Marker3D = null
var marker_facing: Marker3D = null
var _head_skeleton: Skeleton3D
var _head_bone := -1
var _eye_bone_offset := Vector3.ZERO

# Turning and movement
var target_yaw: float = 0.0
var current_action: String = "idle"
var movement_speed: float = 0.0
var nominal_height: float = NOMINAL_HEIGHT
var collision_radius: float = COLLISION_RADIUS

# State transitions
var is_transitioning: bool = false

# 5.06 Seat
var is_seated: bool = false
var seat_node: Object = null
var seat_cushion_height: float = 0.0

# 5.07 Bed / Lie down
var is_lying: bool = false
var is_bedridden: bool = false
var bed_node: Object = null
var bed_stand_position: Vector3 = Vector3.ZERO
var bed_cushion_height: float = 0.55

# 5.08 Conversation
var is_conversing: bool = false
var conversation_partner: WeakRef = null
var conversation_speaking := false
var native_seat_driver: RefCounted

# 5.09 Hands and Items
var slot_right: BoneAttachment3D = null
var slot_left: BoneAttachment3D = null
var held_item_right: Node = null
var held_item_left: Node = null

# 5.11 Carrying
var is_carrying: bool = false
var carried_item_node: Node = null
var carry_speed_modifier: float = 0.65
var carry_clearance_width: float = 0.75

# 3.A6 Avoidance & Yielding
var is_yielding: bool = false
var yield_priority: int = 0
const AVOIDANCE_PROXIMITY_THRESHOLD: float = 0.85
const SAFE_PASSING_DISTANCE: float = 0.40

# 5.12 Action Queue
var action_queue: RefCounted = null

func _init() -> void:
	action_queue = ActionQueueClass.new(self)

func configure(model: Dictionary, height: float, radius: float) -> bool:
	if height <= 0.0: height = NOMINAL_HEIGHT
	if radius <= 0.0: radius = COLLISION_RADIUS
	nominal_height = height
	collision_radius = radius
	
	collision_layer = PhysicsLayers.ACTOR
	collision_mask = PhysicsLayers.ACTOR_MOVEMENT_MASK
	floor_snap_length = 0.35
	floor_constant_speed = true
	floor_max_angle = deg_to_rad(50.0)
	floor_stop_on_slope = true
	
	collider = CollisionShape3D.new()
	collider.name = "CollisionShape3D"
	var capsule := CapsuleShape3D.new()
	capsule.radius = radius
	capsule.height = height
	collider.shape = capsule
	collider.position = Vector3(0.0, height * 0.5, 0.0)
	add_child(collider)
	
	visual = SpatialVisualBridge.new()
	visual.name = "Visual"
	visual.set_body(self)
	add_child(visual)
	
	var char_id: String = str(model.get("character_id", ""))
	var ok: bool = preload("res://scenes/components/characters/ImportedCharacter.gd").attach(
		visual,
		str(model.get("path", "")),
		height,
		float(model.get("yaw_degrees", 0.0)),
		char_id
	)
	if not ok:
		return false
	
	animation_player = visual.get_meta("animation_player") if visual.has_meta("animation_player") else null
	if animation_player != null:
		animation_player.animation_finished.connect(_on_animation_finished)
		preload("res://scenes/components/characters/ConversationAnimationLibrary.gd").install(animation_player)
	
	slot_right = visual.get_meta("slot_right_hand") if visual.has_meta("slot_right_hand") else null
	slot_left = visual.get_meta("slot_left_hand") if visual.has_meta("slot_left_hand") else null

	# Attach standard contact markers (5.01 - 5.02, 5.09, 5.10)
	marker_chest = Marker3D.new()
	marker_chest.name = "SlotChest"
	marker_chest.position = Vector3(0.0, height * 0.6, 0.15)
	add_child(marker_chest)

	marker_head = Marker3D.new()
	marker_head.name = "SlotHead"
	_head_skeleton = visual.get_meta("skeleton",null) as Skeleton3D
	if _head_skeleton != null:
		for index in _head_skeleton.get_bone_count():
			if str(_head_skeleton.get_bone_name(index)).trim_prefix("mixamorig:").trim_prefix("mixamorig_") in ["Head","head"]:
				_head_bone = index
				break
	if _head_bone >= 0:
		var head_transform := _head_skeleton.global_transform * _head_skeleton.get_bone_global_pose(_head_bone)
		var eye := head_transform.origin
		eye.y = global_position.y + height * EYE_HEIGHT / NOMINAL_HEIGHT
		_eye_bone_offset = head_transform.affine_inverse() * eye
		var head_slot := BoneAttachment3D.new()
		head_slot.name = "HeadContact"
		head_slot.bone_name = _head_skeleton.get_bone_name(_head_bone)
		_head_skeleton.add_child(head_slot)
		head_slot.add_child(marker_head)
		marker_head.position = _eye_bone_offset
		set_meta("camera_anchor_source","calibrated-head-bone")
	else:
		marker_head.position = Vector3(0.0, height * EYE_HEIGHT / NOMINAL_HEIGHT, 0.0)
		add_child(marker_head)
		set_meta("camera_anchor_source","standing-height-fallback")

	marker_facing = Marker3D.new()
	marker_facing.name = "SlotFacing"
	marker_facing.position = Vector3(0.0, EYE_HEIGHT, 0.2)
	add_child(marker_facing)

	if slot_right != null:
		marker_hand_right = Marker3D.new()
		marker_hand_right.name = "SlotHandRight"
		slot_right.add_child(marker_hand_right)
	else:
		marker_hand_right = Marker3D.new()
		marker_hand_right.name = "SlotHandRight"
		marker_hand_right.position = Vector3(0.2, height * 0.5, 0.25)
		add_child(marker_hand_right)

	if slot_left != null:
		marker_hand_left = Marker3D.new()
		marker_hand_left.name = "SlotHandLeft"
		slot_left.add_child(marker_hand_left)
	else:
		marker_hand_left = Marker3D.new()
		marker_hand_left.name = "SlotHandLeft"
		marker_hand_left.position = Vector3(-0.2, height * 0.5, 0.25)
		add_child(marker_hand_left)
	
	set_meta("nominal_actor_height", height)
	set_meta("collision_radius", radius)
	set_meta("eye_height", EYE_HEIGHT)
	set_meta("feet_origin", Vector3.ZERO)
	return true

# ------------------------------------------------------------------
# Animation & Movement Driving (Tasks 5.03, 5.04)
# ------------------------------------------------------------------

func play_action(action: String, speed_scale: float = 1.0) -> void:
	if animation_player == null or not is_instance_valid(animation_player):
		return
	if is_transitioning and action in ["walk", "run", "idle", "carry_walk", "carry_idle"]:
		return
	
	var anim_name := action
	match action:
		"idle":
			anim_name = _conversation_animation() if is_conversing and not is_lying else "carry_idle" if is_carrying else "sit_idle" if is_seated else "idle"
		"talk", "listen":
			anim_name = _conversation_animation() if is_conversing else "sit_idle" if is_seated else "idle"
		"walk":
			anim_name = "carry_walk" if is_carrying else "walk"
		"run":
			anim_name = "carry_walk" if is_carrying else "run"
		"sit_down":
			anim_name = "sit_down"
			is_transitioning = true
		"sit_idle", "sit":
			anim_name = "sit_idle"
		"stand_up":
			anim_name = "stand_up"
			is_transitioning = true
		"lie_down":
			anim_name = "lie_down"
			is_transitioning = true
		"lie_idle", "lie":
			anim_name = "lie_idle"
		"lie_stand_up":
			anim_name = "lie_stand_up"
			is_transitioning = true
		"eat":
			anim_name = "eat"
			is_transitioning = true
		"interact", "read_gauge", "observe_gauge", "read_scroll", "inspect_document", "sign_covenant", "show_scroll":
			anim_name = "interact"
			is_transitioning = true
		"pickup", "take_item", "retrieve_key", "take_medicine":
			anim_name = "pickup"
			is_transitioning = true
		"operate", "open_door", "close_door", "turn_wheel", "operate_valve", "release_valve", "operate_latch", "support_cabinet":
			anim_name = "operate"
			is_transitioning = true
		"lockpick", "unlock_latch", "unlock_cabinet", "unlock_door":
			anim_name = "lockpick"
			is_transitioning = true
		"carry_idle", "deliver_meal", "carry_tray", "carry_medicine":
			anim_name = "carry_idle"
		"carry_walk", "serve_meal":
			anim_name = "carry_walk"
		_:
			anim_name = "idle"
	
	if not animation_player.has_animation(anim_name):
		# A missing one-shot must not block all future walking forever.
		is_transitioning = false
		anim_name = "idle"
	
	current_action = anim_name
	animation_player.speed_scale = speed_scale
	if animation_player.current_animation != anim_name or not animation_player.is_playing():
		animation_player.play(anim_name)

func set_movement_speed(speed: float) -> void:
	movement_speed = maxf(speed, 0.0)
	if native_seat_driver != null and bool(native_seat_driver.active): return
	if is_seated or is_lying or is_transitioning: return
	if movement_speed > 0.05:
		# The simulation may accelerate travel; that never turns a walk into a sprint.
		var stride := 0.85 if is_carrying else 1.05
		var clip := "carry_walk" if is_carrying else "walk"
		play_action(clip, clampf(movement_speed / stride, 0.55, 1.55))
	else:
		play_action("idle", 1.0)

func set_target_facing(yaw_rad: float) -> void:
	target_yaw = yaw_rad

func face_world_direction(direction: Vector3) -> void:
	var local := global_basis.inverse() * direction
	if Vector2(local.x, local.z).length_squared() > 0.000001:
		target_yaw = atan2(local.x, local.z)

# ------------------------------------------------------------------
# 5.06 Sit down & Stand up
# ------------------------------------------------------------------

func _get_occupant_safe(obj: Object) -> Object:
	if obj == null: return null
	if obj.has_method("get_occupant"):
		return obj.call("get_occupant")
	if obj.has_meta("occupant"):
		return obj.get_meta("occupant")
	if "occupant" in obj:
		return obj.occupant
	return null

func sit_down_at(target_pos: Vector3, facing_rad: float, cushion_h: float, seat_obj: Object = null) -> Dictionary:
	# The old decoration call cannot establish seat/foot contact or certify a pose.
	return {"ok":false,"reason":"prepared-native-seat-posture-required"}

func begin_native_posture(target: Node3D, spec: Dictionary) -> bool:
	if native_seat_driver == null:
		native_seat_driver = preload("res://scenes/components/characters/SeatPostureDriver3D.gd").new()
	if not native_seat_driver.begin(self,target,spec):
		set_meta("native_posture_start_failure",str(native_seat_driver.last_start_failure))
		return false
	set_meta("native_posture_start_failure","")
	is_transitioning = true
	return true

func advance_native_posture(delta: float) -> Dictionary:
	if native_seat_driver == null or not bool(native_seat_driver.active): return {}
	var result: Dictionary = native_seat_driver.step(delta)
	if result.is_empty(): return result
	is_transitioning = not (bool(result.ok) or bool(result.measurement.get("recovered_to_standing",false)))
	if bool(result.ok):
		if str(result.measurement.to) == "sitting":
			is_seated = true
			seat_node = native_seat_driver.target
			seat_state_changed.emit(true)
			play_action("sit_idle")
		else:
			release_seat()
			play_action("idle")
	elif bool(result.measurement.get("recovered_to_standing",false)):
		release_seat()
		play_action("idle")
	return result

func native_posture_floor_contact() -> bool:
	if native_seat_driver == null: return false
	var measurement: Dictionary = native_seat_driver.last_measurement
	if not bool(measurement.get("settled",false)): return false
	for key in ["left_sole_world","right_sole_world"]:
		var raw: Array = measurement.get(key,[])
		if raw.size() != 3: return false
		var point := Vector3(raw[0],raw[1],raw[2])
		var ray := PhysicsRayQueryParameters3D.create(point + Vector3.UP*0.04,point-Vector3.UP*0.06,PhysicsLayers.FLOOR)
		var hit := get_world_3d().direct_space_state.intersect_ray(ray)
		if hit.is_empty() or hit.normal.y < 0.7 or absf(hit.position.y-point.y) > 0.025: return false
	return true

func cancel_native_posture(reason: String) -> Dictionary:
	if native_seat_driver == null: return {}
	var result: Dictionary = native_seat_driver.cancel(reason)
	if not result.is_empty(): is_transitioning = true
	return result

func stand_up_from_seat() -> void:
	if not is_seated: return
	play_action("stand_up")

func release_seat() -> void:
	is_seated = false
	if seat_node != null:
		if seat_node.has_method("release_reservation"):
			seat_node.call("release_reservation", self)
		elif seat_node.has_meta("occupant"):
			if seat_node.get_meta("occupant") == self:
				seat_node.set_meta("occupant", null)
		elif "occupant" in seat_node and seat_node.occupant == self:
			seat_node.occupant = null
	seat_node = null
	seat_state_changed.emit(false)

# ------------------------------------------------------------------
# 5.07 Lie down & Stand up (Bed)
# ------------------------------------------------------------------

func get_world_pos() -> Vector3:
	return global_position if is_inside_tree() else position

func lie_down_at(bed_position: Vector3, bed_yaw: float, stand_exit_pos: Vector3, bed_obj: Object = null, bedridden: bool = false) -> Dictionary:
	if bed_obj != null:
		var occupant: Object = _get_occupant_safe(bed_obj)
		if occupant != null and is_instance_valid(occupant) and occupant != self:
			return {"ok": false, "reason": "occupied", "bed": bed_obj}
		
		if bed_obj.has_method("reserve"):
			bed_obj.call("reserve", self)
		else:
			bed_obj.set_meta("occupant", self)

	target_yaw = bed_yaw
	is_lying = true
	is_bedridden = bedridden
	bed_node = bed_obj
	bed_stand_position = stand_exit_pos
	if is_inside_tree():
		global_position = bed_position
	else:
		position = bed_position
	play_action("lie_down")
	bed_state_changed.emit(true)
	return {"ok": true, "reason": "lying"}

func stand_up_from_bed() -> bool:
	if not is_lying:
		return false
	if is_bedridden:
		return false
	play_action("lie_stand_up")
	return true

func release_bed() -> void:
	is_lying = false
	is_bedridden = false
	if bed_stand_position != Vector3.ZERO:
		if is_inside_tree():
			global_position = bed_stand_position
		else:
			position = bed_stand_position
	if bed_node != null:
		if bed_node.has_method("release_reservation"):
			bed_node.call("release_reservation", self)
		elif bed_node.has_meta("occupant"):
			if bed_node.get_meta("occupant") == self:
				bed_node.set_meta("occupant", null)
		elif "occupant" in bed_node and bed_node.occupant == self:
			bed_node.occupant = null
	bed_node = null
	bed_state_changed.emit(false)

# ------------------------------------------------------------------
# 5.08 Conversation & Mutual Facing
# ------------------------------------------------------------------

func start_conversation(partner: Object) -> void:
	if partner == null or not is_instance_valid(partner): return
	is_conversing = true
	conversation_speaking = false
	conversation_partner = weakref(partner)
	if partner is Node3D and is_instance_valid(partner) and not is_seated and not is_lying:
		var p_node: Node3D = partner as Node3D
		var p_pos: Vector3 = p_node.global_position if p_node.is_inside_tree() else p_node.position
		var dir: Vector3 = p_pos - get_world_pos()
		dir.y = 0.0
		if dir.length_squared() > 0.001:
			face_world_direction(dir)
	if not is_transitioning and movement_speed <= 0.05: play_action("idle")

func set_conversation_speaking(speaking: bool) -> void:
	conversation_speaking = speaking
	if is_conversing and not is_transitioning and movement_speed <= 0.05: play_action("idle")

func _conversation_animation() -> String:
	var fallback := "lie_idle" if is_lying else "sit_idle" if is_seated else "carry_idle" if is_carrying else "idle"
	if animation_player == null or is_lying or is_carrying: return fallback
	var posture := "seated" if is_seated else "standing"
	var occupied := is_carrying or is_instance_valid(held_item_right) or is_instance_valid(held_item_left)
	var clip := "conversation/" + ("talk" if conversation_speaking else "listen") + "_" + posture + ("_occupied" if occupied else "")
	return clip if animation_player.has_animation(clip) else fallback

func start_mutual_conversation(partner: Object) -> void:
	if partner == null or not is_instance_valid(partner): return
	start_conversation(partner)
	if partner.has_method("start_conversation"):
		partner.call("start_conversation", self)

# ------------------------------------------------------------------
# 3.A6 Avoidance & Yielding Negotiation
# ------------------------------------------------------------------

func evaluate_oncoming_actor(other_actor: CharacterBody3D, forward_dir: Vector3) -> Dictionary:
	if other_actor == null or not is_instance_valid(other_actor):
		is_yielding = false
		return {"action": "continue", "reason": "no_actor"}
	
	var other_pos: Vector3 = other_actor.global_position if other_actor.is_inside_tree() else other_actor.position
	var my_pos: Vector3 = get_world_pos()
	var dist: float = my_pos.distance_to(other_pos)
	
	if dist > AVOIDANCE_PROXIMITY_THRESHOLD:
		is_yielding = false
		return {"action": "continue", "distance": dist}
	
	var to_other := (other_pos - my_pos).normalized()
	to_other.y = 0.0
	var dot_facing := forward_dir.dot(to_other)
	
	if dot_facing < 0.1:
		is_yielding = false
		return {"action": "continue", "distance": dist}
	
	var other_priority: int = int(other_actor.get("yield_priority")) if other_actor.get("yield_priority") != null else 0
	var my_priority: int = yield_priority
	var should_i_yield: bool = false
	
	if my_priority < other_priority:
		should_i_yield = true
	elif my_priority > other_priority:
		should_i_yield = false
	else:
		should_i_yield = get_instance_id() < other_actor.get_instance_id()
	
	if should_i_yield:
		is_yielding = true
		yield_state_changed.emit(true, other_actor)
		play_action("idle")
		return {
			"action": "yield_pause",
			"reason": "yielding_to_peer",
			"distance": dist,
			"yielding_actor": name,
			"passing_actor": other_actor.name
		}
	else:
		is_yielding = false
		yield_state_changed.emit(false, other_actor)
		return {
			"action": "pass_through",
			"reason": "has_passage_priority",
			"distance": dist,
			"yielding_actor": other_actor.name,
			"passing_actor": name
		}

func check_capsule_overlap(other_actor: CharacterBody3D) -> Dictionary:
	if other_actor == null or not is_instance_valid(other_actor):
		return {"overlapping": false, "distance": INF}
	var other_pos: Vector3 = other_actor.global_position if other_actor.is_inside_tree() else other_actor.position
	var my_pos: Vector3 = get_world_pos()
	var dist_xz := Vector2(my_pos.x - other_pos.x, my_pos.z - other_pos.z).length()
	var other_radius: float = float(other_actor.get("collision_radius")) if other_actor.get("collision_radius") != null else COLLISION_RADIUS
	var min_clear_dist: float = collision_radius + other_radius
	return {
		"overlapping": dist_xz < min_clear_dist,
		"distance_xz": dist_xz,
		"min_clear_distance": min_clear_dist,
		"margin": dist_xz - min_clear_dist
	}

func end_conversation() -> void:
	is_conversing = false
	conversation_speaking = false
	conversation_partner = null
	if not is_transitioning and movement_speed <= 0.05:
		play_action("lie_idle" if is_lying else "sit_idle" if is_seated else "idle")

func update_conversation(delta: float) -> void:
	if not is_conversing: return
	if conversation_partner == null:
		end_conversation()
		return
	var partner = conversation_partner.get_ref()
	if not is_instance_valid(partner):
		end_conversation()
		conversation_interrupted.emit("partner_invalid")
		return
	if partner is Node3D:
		var p_node: Node3D = partner as Node3D
		var p_pos: Vector3 = p_node.global_position if p_node.is_inside_tree() else p_node.position
		var dist: float = get_world_pos().distance_to(p_pos)
		if dist > CONVERSATION_DISTANCE_MAX:
			end_conversation()
			conversation_interrupted.emit("target_left")
			return
		# Seated hips and planted feet retain their chair orientation.
		if not is_lying and not is_seated:
			var dir: Vector3 = p_pos - get_world_pos()
			dir.y = 0.0
			if dir.length_squared() > 0.01:
				face_world_direction(dir)

# ------------------------------------------------------------------
# 5.09 Pick up & Place with Height Classification
# ------------------------------------------------------------------

func get_hand_slot(side: String = "right") -> BoneAttachment3D:
	return slot_right if side.to_lower() == "right" else slot_left

func pickup_item_at(item_node: Node, target_pos: Vector3, height_category: String = "table", side: String = "right") -> Dictionary:
	if is_carrying:
		return {"ok": false, "reason": "hands_occupied_carrying"}
	
	# Clearance and reachability check
	var dist := Vector2(get_world_pos().x-target_pos.x,get_world_pos().z-target_pos.z).length()
	if dist > CONTACT_DISTANCE_MAX:
		return {"ok": false, "reason": "item_out_of_reach", "distance": dist}
	
	var slot := get_hand_slot(side)
	if slot == null:
		return {"ok": false, "reason": "missing_hand_slot"}
	
	# Reparent item exclusively: item cannot be on table and in hand simultaneously
	if item_node.get_parent() != null:
		item_node.get_parent().remove_child(item_node)
	slot.add_child(item_node)
	if item_node is Node3D:
		item_node.position = Vector3.ZERO
		item_node.rotation = Vector3.ZERO
	
	if side.to_lower() == "right": held_item_right = item_node
	else: held_item_left = item_node
	
	play_action("pickup")
	return {"ok": true, "height_category": height_category, "slot": slot.name}

func place_item_at(target_parent: Node, target_pos: Vector3, height_category: String = "table", side: String = "right") -> Dictionary:
	var item := held_item_right if side.to_lower() == "right" else held_item_left
	if item == null or not is_instance_valid(item):
		return {"ok": false, "reason": "no_item_held"}
	
	# Clearance and reachability check
	var dist := Vector2(get_world_pos().x-target_pos.x,get_world_pos().z-target_pos.z).length()
	if dist > CONTACT_DISTANCE_MAX:
		return {"ok": false, "reason": "target_surface_out_of_reach", "distance": dist}
	
	var slot := get_hand_slot(side)
	if slot != null and item.get_parent() == slot:
		slot.remove_child(item)
	
	target_parent.add_child(item)
	if item is Node3D:
		var i_node: Node3D = item as Node3D
		if i_node.is_inside_tree(): i_node.global_position = target_pos
		else: i_node.position = target_pos
	
	if side.to_lower() == "right": held_item_right = null
	else: held_item_left = null
	
	play_action("interact")
	return {"ok": true, "height_category": height_category, "placed_position": target_pos}

func cancel_item_operation(item_node: Node, rollback_parent: Node, rollback_pos: Vector3, side: String = "right") -> bool:
	if item_node == null: return false
	if item_node.get_parent() != null:
		item_node.get_parent().remove_child(item_node)
	rollback_parent.add_child(item_node)
	if item_node is Node3D:
		var i_node: Node3D = item_node as Node3D
		if i_node.is_inside_tree(): i_node.global_position = rollback_pos
		else: i_node.position = rollback_pos
	if side.to_lower() == "right" and held_item_right == item_node: held_item_right = null
	elif side.to_lower() == "left" and held_item_left == item_node: held_item_left = null
	play_action("idle")
	return true

# ------------------------------------------------------------------
# 5.10 Door and Valve Operation with Strict Stand/Contact Validation
# ------------------------------------------------------------------

func operate_mechanism(stand_slot: Vector3, contact_slot: Vector3, action_type: String = "operate", mechanism_obj: Object = null) -> Dictionary:
	if is_carrying:
		return {"ok": false, "reason": "hands_occupied_carrying"}
	
	# 1. Strict stand position validation: Reject distant operation!
	var stand_dist := get_world_pos().distance_to(stand_slot)
	if stand_dist > INTERACTION_DISTANCE_MAX:
		return {"ok": false, "reason": "not_at_stand_slot", "stand_dist": stand_dist, "limit": INTERACTION_DISTANCE_MAX}
	
	# 2. Strict contact position validation
	var contact_dist := get_world_pos().distance_to(contact_slot)
	if contact_dist > CONTACT_DISTANCE_MAX:
		return {"ok": false, "reason": "contact_point_unreachable", "contact_dist": contact_dist}
	
	# Align facing to contact point
	var dir := contact_slot - get_world_pos()
	dir.y = 0.0
	if dir.length_squared() > 0.001:
		face_world_direction(dir)
	
	play_action(action_type if action_type != "" else "operate")
	return {"ok": true, "action_type": action_type, "contact_dist": contact_dist}

# ------------------------------------------------------------------
# 5.11 Carrying & Clearance Constraints
# ------------------------------------------------------------------

func start_carrying(item_node: Node) -> Dictionary:
	if is_carrying:
		return {"ok": false, "reason": "already_carrying"}
	if held_item_right != null or held_item_left != null:
		return {"ok": false, "reason": "hands_already_occupied"}
	
	is_carrying = true
	carried_item_node = item_node
	if slot_right != null and item_node != null:
		if item_node.get_parent() != null:
			item_node.get_parent().remove_child(item_node)
		slot_right.add_child(item_node)
		if item_node is Node3D:
			item_node.position = Vector3(0.0, 0.0, 0.2)
	
	play_action("carry_idle")
	carry_state_changed.emit(true)
	return {"ok": true, "speed_modifier": carry_speed_modifier, "clearance_width": carry_clearance_width}

func stop_carrying() -> Node:
	if not is_carrying: return null
	is_carrying = false
	var item = carried_item_node
	carried_item_node = null
	if slot_right != null and item != null and item.get_parent() == slot_right:
		slot_right.remove_child(item)
	play_action("idle")
	carry_state_changed.emit(false)
	return item

func can_perform_action(action_name: String) -> Dictionary:
	if is_carrying:
		# Actions that require free hands are blocked while carrying two-handed objects
		if action_name in ["pickup", "interact", "operate", "lockpick", "open_door", "open_valve", "use_tool"]:
			return {"allowed": false, "reason": "hands_occupied_carrying"}
	return {"allowed": true, "reason": "ok"}

func check_passage_clearance(passage_width: float) -> Dictionary:
	var required_width := carry_clearance_width if is_carrying else (collision_radius * 2.0)
	if passage_width < required_width:
		return {
			"passable": false, 
			"reason": "passage_too_narrow_for_carrying" if is_carrying else "passage_too_narrow",
			"passage_width": passage_width,
			"required_width": required_width
		}
	return {"passable": true, "passage_width": passage_width, "required_width": required_width}

# ------------------------------------------------------------------
# Global Interruption & Action Clean Reset
# ------------------------------------------------------------------

func interrupt_action() -> void:
	is_transitioning = false
	if is_seated:
		release_seat()
	if is_lying:
		release_bed()
	if is_conversing:
		end_conversation()
	if action_queue != null:
		action_queue.cancel_current("interrupted")
	play_action("carry_idle" if is_carrying else "idle", 1.0)

func _on_animation_finished(anim_name: StringName) -> void:
	if native_seat_driver != null: native_seat_driver.notify_animation_finished(str(anim_name))
	is_transitioning = false
	if visual != null and visual.has_method("notify_visual_finished"):
		visual.notify_visual_finished(str(anim_name))
	match str(anim_name):
		"sit_down":
			play_action("sit_idle")
			action_completed.emit("sit_down")
		"stand_up":
			if native_seat_driver == null or not bool(native_seat_driver.active): release_seat()
			play_action("idle")
			action_completed.emit("stand_up")
		"lie_down":
			play_action("lie_idle")
			action_completed.emit("lie_down")
		"lie_stand_up":
			release_bed()
			play_action("idle")
			action_completed.emit("lie_stand_up")
		"eat", "pickup", "interact", "operate", "lockpick", "read_gauge", "observe_gauge", "read_scroll", "inspect_document", "sign_covenant", "show_scroll", "turn_wheel", "operate_valve", "release_valve", "open_latch", "unlock_latch":
			play_action("carry_idle" if is_carrying else "idle")
			action_completed.emit(str(anim_name))
		_:
			action_completed.emit(str(anim_name))

func rollback_action(reason: String = "blocked", fallback_parent: Node = null, fallback_pos: Vector3 = Vector3.ZERO) -> Dictionary:
	is_transitioning = false
	velocity = Vector3.ZERO
	set_movement_speed(0.0)
	
	if action_queue != null:
		action_queue.cancel_current(reason)
	
	if visual != null and visual.has_method("cancel_visual"):
		visual.cancel_visual()
	
	# If any item was mid-pickup and needs rollback
	if fallback_parent != null:
		if held_item_right != null and held_item_right.get_parent() == slot_right:
			slot_right.remove_child(held_item_right)
			fallback_parent.add_child(held_item_right)
			if held_item_right is Node3D:
				held_item_right.global_position = fallback_pos
			held_item_right = null
		if held_item_left != null and held_item_left.get_parent() == slot_left:
			slot_left.remove_child(held_item_left)
			fallback_parent.add_child(held_item_left)
			if held_item_left is Node3D:
				held_item_left.global_position = fallback_pos
			held_item_left = null
	
	play_action("carry_idle" if is_carrying else "idle", 1.0)
	action_rolled_back.emit(current_action, reason)
	return {
		"rolled_back": true,
		"reason": reason,
		"current_action": current_action,
		"is_carrying": is_carrying,
		"feet_on_ground": is_on_floor() if is_inside_tree() else true
	}

# ------------------------------------------------------------------
# Spatial Action & UI Integration Adapters (Tasks 5.01 - 5.12)
# ------------------------------------------------------------------

func get_spatial_contact_slots() -> Dictionary:
	return {
		"hand_right": marker_hand_right,
		"hand_left": marker_hand_left,
		"chest": marker_chest,
		"head": marker_head,
		"facing": marker_facing
	}

func resolve_actor_contact_slot(slot_id: String) -> Marker3D:
	match slot_id:
		"hand_right", "right_hand", "hand":
			return marker_hand_right
		"hand_left", "left_hand":
			return marker_hand_left
		"chest":
			return marker_chest
		"head":
			return marker_head
		"facing":
			return marker_facing
		_:
			return marker_facing if marker_facing != null else marker_head

func get_action_queue_snapshot() -> Dictionary:
	var curr: Dictionary = {}
	if action_queue != null and not action_queue.current_action.is_empty():
		var act = action_queue.current_action
		curr = {
			"id": str(act.get("id", "")),
			"label": str(act.get("label", act.get("id", ""))),
			"is_waiting": bool(act.get("is_waiting", false)),
			"wait_reason": str(act.get("wait_reason", ""))
		}
	var q_list: Array = []
	if action_queue != null:
		for item in action_queue.queue:
			q_list.append({
				"id": str(item.get("id", "")),
				"label": str(item.get("label", item.get("id", ""))),
				"wait_reason": str(item.get("wait_reason", ""))
			})
	return {
		"current_action": curr,
		"queued_actions": q_list
	}

func bind_to_ui_system(ui_sys: Object) -> void:
	if ui_sys == null: return
	if "action_queue_panel" in ui_sys:
		var panel = ui_sys.get("action_queue_panel")
		if panel != null and panel.has_method("apply_queue_snapshot"):
			if action_queue != null:
				action_queue.action_started.connect(func(_act):
					panel.apply_queue_snapshot(get_action_queue_snapshot())
				)
				action_queue.action_finished.connect(func(_act):
					panel.apply_queue_snapshot(get_action_queue_snapshot())
				)
				action_queue.action_cancelled.connect(func(_act, _r):
					panel.apply_queue_snapshot(get_action_queue_snapshot())
				)
			panel.apply_queue_snapshot(get_action_queue_snapshot())

func _physics_process(delta: float) -> void:
	if delta <= 0.0 or visual == null: return
	
	# Update active conversation tracking
	if is_conversing:
		update_conversation(delta)
	
	# Smooth yaw rotation without angular snapping or wall piercing
	var diff := angle_difference(visual.rotation.y, target_yaw)
	if absf(diff) > 0.001:
		visual.rotation.y = lerp_angle(visual.rotation.y, target_yaw, clampf(TURN_SPEED * delta, 0.0, 1.0))
	else:
		visual.rotation.y = target_yaw

func get_camera_anchor() -> Vector3:
	if is_instance_valid(_head_skeleton) and _head_bone >= 0:
		return (_head_skeleton.global_transform * _head_skeleton.get_bone_global_pose(_head_bone)) * _eye_bone_offset
	return marker_head.global_position if is_instance_valid(marker_head) and marker_head.is_inside_tree() else get_world_pos() + Vector3(0.0,EYE_HEIGHT,0.0)

func step_towards(target_pos: Vector3, delta: float, speed: float = 1.05) -> bool:
	if native_seat_driver != null and bool(native_seat_driver.active): return false
	if is_seated or is_lying or is_transitioning:
		return false
	
	var my_pos := get_world_pos()
	var diff := target_pos - my_pos
	var dist_3d := diff.length()
	var dist_xz := Vector2(diff.x, diff.z).length()
	
	if dist_3d <= 0.08:
		velocity = Vector3.ZERO
		set_movement_speed(0.0)
		return true
	
	if dist_xz > 0.01:
		face_world_direction(diff)
	
	var dir_3d := diff / dist_3d
	var step_speed := speed * (carry_speed_modifier if is_carrying else 1.0)
	var move_dist := minf(dist_3d, step_speed * delta)
	velocity = dir_3d * (move_dist / maxf(delta, 0.000001))
	set_movement_speed(step_speed)
	
	if is_inside_tree():
		# Velocity already contains the vertical component. Moving y manually
		# before move_and_slide counts it twice and oscillates around a waypoint
		# when fast-forwarding. Let the collision solver execute the one motion.
		move_and_slide()
		if absf(diff.y) <= 0.05: position.y = target_pos.y
	else:
		position += dir_3d * move_dist
		if absf(diff.y) <= 0.05:
			position.y = target_pos.y
	
	if get_world_pos().distance_to(target_pos) <= 0.08:
		velocity = Vector3.ZERO
		set_movement_speed(0.0)
		return true
	return false

func stop_and_hold_position() -> void:
	velocity = Vector3.ZERO
	set_movement_speed(0.0)
	play_action("carry_idle" if is_carrying else "idle", 1.0)
