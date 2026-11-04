extends RefCounted
## A separate motor state for each autonomous actor. Core owns timing and reservations.
var body: CharacterBody3D
var navigation: NavigationAgent3D
var bridge: Node
var streaming: Node
var settings: Dictionary
var command: Dictionary = {}
var phase := "idle"
var cancelled := false
var elapsed := 0.0
var stalled := 0.0
var progress := Vector3.ZERO
var held_rooms: Array = []
var actor_id := ""
var camera: Camera3D
var offscreen := false
var restoring_physics := false
var offline_elapsed := 0.0
var saved_collision_layer := 0
var saved_collision_mask := 0
var reentry_blocked := false
var reentry_anchor := Vector3.ZERO
var observation_actors: Dictionary = {}
var observation_props: Node
var stair_path:=PackedVector3Array()
var stair_path_index:=0
var stair_planned:=false
var stair_target_height:=0.0
var invalidated_guard:=false
var incapacitated_visual:=false
var idle_suspended:=false
var work_real_started_us := 0
var door_clearance = preload("res://scenes/components/architecture/DoorFrameClearance.gd").new()

func set_idle_suspended(suspend: bool) -> void:
	if offscreen: return
	suspend=suspend and command.is_empty()
	if suspend==idle_suspended: return
	idle_suspended=suspend
	if suspend:
		streaming.ReleaseActorPosition(actor_id)
		body.collision_layer=0
		body.collision_mask=0
		body.process_mode=Node.PROCESS_MODE_DISABLED
	else:
		body.process_mode=Node.PROCESS_MODE_INHERIT
		body.collision_layer=saved_collision_layer
		body.collision_mask=saved_collision_mask

func bind_observation_context(actors: Dictionary, props: Node) -> void:
	observation_actors=actors
	observation_props=props

func observation_target(motor: Dictionary) -> Node3D:
	if motor.has("observe_actor"): return observation_actors.get(str(motor.observe_actor))
	if motor.has("observe_fixture") and is_instance_valid(observation_props): return observation_props.nodes.get(str(motor.observe_fixture))
	if motor.has("observe_world_object"): return find_object(streaming,str(motor.observe_world_object))
	return null

func player_can_see_room(position: Vector3) -> bool:
	var room: String=streaming.RoomAt(position)
	return streaming.RoomVisible(room) if not room.is_empty() else camera.is_position_in_frustum(position+Vector3.UP)

func observation_valid(motor: Dictionary) -> bool:
	if motor.has("observe_group"):
		var eye:=body.global_position+Vector3.UP*float(motor.get("observation_eye_height_m",0.9))
		for id: String in motor.observe_group:
			var member: Node3D=observation_actors.get(id)
			if not is_instance_valid(member) or not member.is_visible_in_tree(): return false
			var point:=member.global_position+Vector3.UP*float(motor.get("observation_target_height_m",0.8))
			if eye.distance_to(point)>float(motor.observation_group_distance_m): return false
			var group_ray:=PhysicsRayQueryParameters3D.create(eye,point,1)
			if not body.get_world_3d().direct_space_state.intersect_ray(group_ray).is_empty(): return false
		for observed: Dictionary in motor.get("observed_group_actions",[]):
			if bridge.ActorWorkingAction(str(observed.actor))!=str(observed.action): return false
	if not motor.has("observe_actor") and not motor.has("observe_fixture") and not motor.has("observe_world_object"): return true
	var target:=observation_target(motor)
	if not is_instance_valid(target) or not target.is_visible_in_tree(): return false
	if motor.has("observed_actor_action") and bridge.ActorWorkingAction(str(motor.get("observe_actor","")))!=str(motor.observed_actor_action): return false
	if motor.has("evidence_fixture"):
		var evidence: Node3D=observation_props.nodes.get(str(motor.evidence_fixture))
		if not is_instance_valid(evidence) or not evidence.is_visible_in_tree(): return false
		var eye:=body.global_position+Vector3.UP*float(motor.get("observation_eye_height_m",0.9))
		if eye.distance_to(evidence.global_position)>float(motor.get("observation_distance_m",1.5)): return false
		var evidence_ray:=PhysicsRayQueryParameters3D.create(eye,evidence.global_position)
		evidence_ray.exclude=[body.get_rid()]
		var evidence_hit: Dictionary=body.get_world_3d().direct_space_state.intersect_ray(evidence_ray)
		if not evidence_hit.is_empty() and (evidence_hit.get("collider")!=target or (evidence_hit.position as Vector3).distance_to(evidence.global_position)>0.2): return false
	var from:=body.global_position+Vector3.UP*float(motor.get("observation_eye_height_m",0.9))
	var to:=target.global_position
	if motor.has("observe_world_object"):
		var leaf:=target.get_node_or_null("VisualRoot/Hinge/DoorLeafMesh") as Node3D
		if is_instance_valid(leaf): to=leaf.global_position
	if motor.has("observe_actor"): to+=Vector3.UP*float(motor.get("observation_target_height_m",0.8))
	if from.distance_to(to)>float(motor.get("observation_distance_m",1.5)): return false
	var ray:=PhysicsRayQueryParameters3D.create(from,to)
	ray.exclude=[body.get_rid()]
	var hit: Dictionary=body.get_world_3d().direct_space_state.intersect_ray(ray)
	var collider: Node=hit.get("collider")
	return hit.is_empty() or collider==target or (is_instance_valid(collider) and target.is_ancestor_of(collider))

func configure(actor: CharacterBody3D, port: Node, map: Node, options: Dictionary, id: String, view: Camera3D) -> void:
	body=actor; bridge=port; streaming=map; settings=options
	actor_id=id; camera=view
	saved_collision_layer=body.collision_layer; saved_collision_mask=body.collision_mask
	navigation=NavigationAgent3D.new()
	navigation.path_desired_distance=0.1
	navigation.target_desired_distance=float(settings.get("npc_arrival_distance_m",settings.arrival_distance_m))
	body.add_child(navigation)
	preload("res://scenes/components/characters/SeatPoseModifier3D.gd").prepare_geometry(body)

func dispatch(next: Dictionary) -> void:
	if next.kind=="cancel":
		if int(command.get("execution",0))==int(next.execution): cancelled=true
		return
	command=next; phase="approaching"; elapsed=0; stalled=0; cancelled=false
	work_real_started_us=0
	invalidated_guard=false
	stair_path.clear();stair_path_index=0;stair_planned=false
	reentry_blocked=false
	held_rooms=command.declaration.get("route_rooms",[]).duplicate()
	for room in held_rooms: streaming.HoldActorRoom(str(room))
	progress=body.global_position
	var motor: Dictionary=command.declaration.motor
	if motor.get("target_current",false):
		navigation.target_position=body.global_position
	elif motor.has("observe_actor"):
		var observed:=observation_target(motor)
		if not is_instance_valid(observed): finish("observation-target-unavailable");return
		var away:=body.global_position-observed.global_position
		away.y=0
		if away.length_squared()<0.0001: away=Vector3.RIGHT
		navigation.target_position=observed.global_position+away.normalized()*float(motor.get("observation_approach_m",0.8))
	elif motor.has("target_slot"):
		navigation.target_position=streaming.OperationSlot(str(motor.object_id),str(motor.target_slot))
	else:
		var p: Array=motor.target
		navigation.target_position=Vector3(p[0],p[1],p[2])

func stop() -> void:
	body.velocity=Vector3.ZERO; body.set_movement_speed(0)

func finish(reason: String = "") -> void:
	door_clearance.clear()
	stop()
	if offscreen: bridge.EndJourney(int(command.execution)); restore_body()
	if not body.is_seated: body.play_action("idle")
	if cancelled: bridge.Cancelled(int(command.execution))
	elif reason.is_empty(): bridge.Complete(int(command.execution))
	else: bridge.Fail(int(command.execution),reason)
	for room in held_rooms: streaming.ReleaseActorRoom(str(room))
	held_rooms.clear()
	command={}; phase="idle"

func step(delta: float, rate: float, held: bool) -> void:
	var crossing_opening:=str(command.get("declaration",{}).get("motor",{}).get("door_opening","")) if phase=="departing" else ""
	door_clearance.update(body,streaming,crossing_opening)
	if idle_suspended: return
	if command.is_empty() and bridge.ActorIncapacitated(actor_id):
		if not incapacitated_visual:
			stop()
			body.play_action("lie_idle")
			incapacitated_visual=true
		return
	if incapacitated_visual:
		incapacitated_visual=false
		body.play_action("idle")
	if command.is_empty() or held: stop(); return
	if not command.declaration.get("knowledge_guards",[]).is_empty() and not bridge.PhysicalActionPermitted(int(command.execution)):
		if offscreen: invalidated_guard=true;restoring_physics=true
		else: finish("knowledge-guard-invalidated");return
	if offscreen:
		step_offscreen(delta)
		return
	for room in held_rooms:
		if not streaming.RoomActive(str(room)): stop(); return
	elapsed+=delta
	var motor: Dictionary=command.declaration.motor
	var movement_speed:=float(settings.movement_speed_mps)*float(motor.get("speed_multiplier",1.0))
	if cancelled:
		# Seated or transitioning actors must be recovered before confirming release.
		if body.is_seated or phase in ["sitting","standing"]:
			if phase=="sitting": body.cancel_native_posture("cancelled")
			var result: Dictionary=body.advance_native_posture(delta)
			if result.is_empty(): return
			if body.is_seated:
				if not posture("standing"): return
				phase="standing"; return
		finish(); return
	if phase in ["sitting","standing"]:
		var result: Dictionary=body.advance_native_posture(delta)
		if result.is_empty(): return
		if not result.ok: finish(str(result.reason)); return
		if phase=="standing": finish(); return
		if not bridge.BeginWork(int(command.execution)): finish("work-start-rejected"); return
		phase="working"
	if phase in ["approaching","departing","door_retreat"]:
		if phase=="approaching" and motor.has("observe_actor"):
			var observed:=observation_target(motor)
			if not is_instance_valid(observed): finish("observation-target-unavailable");return
			var away:=body.global_position-observed.global_position;away.y=0
			if away.length_squared()<0.0001: away=Vector3.RIGHT
			navigation.target_position=observed.global_position+away.normalized()*float(motor.get("observation_approach_m",0.8))
			if stair_planned and absf(stair_target_height-navigation.target_position.y)>1.0:
				stair_planned=false;stair_path.clear();stair_path_index=0
		if phase=="approaching" and absf(navigation.target_position.y-body.global_position.y)>0.3 and not stair_planned:
			stair_planned=true;stair_target_height=navigation.target_position.y
			stair_path=preload("res://tools/building/BuildRoutes.gd").plan(navigation.get_navigation_map(),streaming.StairBuildings(),body.global_position,navigation.target_position)
			if stair_path.is_empty() or not streaming.CanTraversePath(stair_path): finish("cross-floor-route-unavailable");return
		if phase=="approaching" and not stair_path.is_empty():
			while stair_path_index<stair_path.size() and body.global_position.distance_to(stair_path[stair_path_index])<0.18: stair_path_index+=1
			if stair_path_index<stair_path.size():
				if elapsed>float(settings.approach_timeout_seconds): finish("cross-floor-timeout");return
				if body.global_position.distance_to(progress)>=float(settings.approach_progress_m): progress=body.global_position;stalled=0
				else:
					stalled+=delta
					if stalled>float(settings.approach_stall_timeout_seconds): finish("cross-floor-blocked");return
				var target: Vector3=stair_path[stair_path_index]
				if not streaming.CanTraversePath(PackedVector3Array([body.global_position,target])): finish("cross-floor-portal-closed");return
				var direction: Vector3=target-body.global_position;direction.y=0
				var speed: float=minf(movement_speed*rate,direction.length()/maxf(delta,0.000001))
				body.velocity=direction.normalized()*speed
				if not body.is_on_floor(): body.velocity.y-=9.8*delta
				body.move_and_slide();body.set_movement_speed(movement_speed);body.face_world_direction(direction.normalized());return
			stair_path.clear()
			if absf(navigation.target_position.y-body.global_position.y)>0.3:
				if motor.has("observe_actor"): stair_planned=false;stair_path_index=0;return
				finish("cross-floor-height-mismatch");return
		if reentry_blocked and body.global_position.distance_to(reentry_anchor)>=float(settings.get("offscreen_reentry_hysteresis_m",1.0)):
			reentry_blocked=false
		if phase=="approaching" and str(command.declaration.get("kind",""))=="travel" and bool(settings.get("offscreen_simulation",false)) and not motor.has("door_opening") and not motor.has("cross_target") and not motor.has("observe_actor") and not motor.has("observe_fixture") and not motor.has("observe_world_object") and not motor.has("observe_group") and not reentry_blocked and not player_can_see_room(body.global_position):
			if begin_offscreen(): return
		if elapsed>float(settings.approach_timeout_seconds): finish("approach-timeout"); return
		var offset: Vector3=navigation.target_position-body.global_position; offset.y=0
		if offset.length()>float(settings.get("npc_arrival_distance_m",settings.arrival_distance_m)):
			if body.global_position.distance_to(progress)>=float(settings.approach_progress_m): progress=body.global_position; stalled=0
			else:
				stalled+=delta
				if stalled>float(settings.approach_stall_timeout_seconds): finish("approach-blocked"); return
			var direction: Vector3=navigation.get_next_path_position()-body.global_position; direction.y=0
			var final_step: bool = navigation.is_navigation_finished() and offset.length()<=float(settings.final_approach_distance_m)
			if phase=="departing" and motor.has("door_opening"): final_step=true
			if final_step: direction=offset
			direction=direction.normalized()
			var speed: float = minf(movement_speed*rate,offset.length()/maxf(delta,0.000001)) if final_step else movement_speed*rate
			body.velocity=direction*speed
			if not body.is_on_floor(): body.velocity.y-=9.8*delta
			body.move_and_slide(); body.set_movement_speed(movement_speed); body.face_world_direction(direction)
			return
		stop()
		if phase=="departing": finish(); return
		if phase=="door_retreat":
			if not streaming.BeginOpenDoor(str(motor.door_opening)): finish("door-motion-rejected"); return
			phase="door_opening";elapsed=0;return
		if command.declaration.timing.get("host_elapsed",false): finish(); return
		phase="facing"; elapsed=0
	if phase=="facing":
		if elapsed>float(settings.facing_timeout_seconds): finish("facing-timeout"); return
		var facing: Vector3
		if motor.has("observe_actor"):
			var observed:=observation_target(motor)
			if not is_instance_valid(observed): finish("observation-target-unavailable");return
			facing=observed.global_position
		elif motor.has("facing_slot"): facing=streaming.OperationSlot(str(motor.object_id),str(motor.facing_slot))
		else:
			var p: Array=motor.facing
			facing=Vector3(p[0],p[1],p[2])
		facing-=body.global_position; facing.y=0
		body.face_world_direction(facing.normalized())
		if absf(angle_difference(body.visual.rotation.y,body.target_yaw))>deg_to_rad(float(settings.facing_tolerance_degrees)): return
		if motor.posture=="sitting":
			if not posture("sitting"): finish("cannot-sit"); return
			phase="sitting"; return
		if not bool(motor.get("observe_after_door",false)) and not observation_valid(motor): finish("observation-not-visible");return
		if not bridge.BeginWork(int(command.execution)): finish("work-start-rejected"); return
		phase="working"
	if phase=="working":
		if work_real_started_us==0: work_real_started_us=Time.get_ticks_usec()
		body.play_action(str(motor.animation))
		var minimum_real:=float(command.declaration.get("minimum_real_work_seconds",0.0))
		if float(Time.get_ticks_usec()-work_real_started_us)/1000000.0>=minimum_real and bridge.WorkElapsed(int(command.execution)):
			if not bool(motor.get("observe_after_door",false)) and not observation_valid(motor): finish("observation-target-lost");return
			if body.is_seated:
				if not posture("standing"): finish("cannot-stand"); return
				phase="standing"
			elif motor.has("door_opening"):
				if motor.has("door_permission"):
					var permission: Dictionary = motor.door_permission
					var fact: Dictionary = bridge.PhysicalFact(str(permission.object),str(permission.facet),str(permission.field))
					if not bool(fact.get("Known",false)) or fact.get("Value") != permission.value: finish("door-permission-denied"); return
				if motor.has("door_retreat_target"):
					var retreat: Array=motor.door_retreat_target
					navigation.target_position=Vector3(retreat[0],retreat[1],retreat[2])
					phase="door_retreat";elapsed=0;stalled=0;progress=body.global_position;return
				if not streaming.BeginOpenDoor(str(motor.door_opening)): finish("door-motion-rejected"); return
				phase="door_opening"; elapsed=0
			elif motor.has("exit_target"):
				var p: Array=motor.exit_target
				navigation.target_position=Vector3(p[0],p[1],p[2])
				phase="departing"; elapsed=0; stalled=0; progress=body.global_position
			else: finish()
	if phase=="door_opening":
		if elapsed>float(settings.get("door_motion_timeout_seconds",10.0)): finish("door-motion-timeout"); return
		if streaming.DoorOpen(str(motor.door_opening)):
			if bool(motor.get("observe_after_door",false)) and not observation_valid(motor): finish("observation-not-visible");return
			if motor.has("cross_target"):
				var p: Array=motor.cross_target
				navigation.target_position=Vector3(p[0],p[1],p[2])
				phase="departing"; elapsed=0; stalled=0; progress=body.global_position
			else: finish()

func begin_offscreen() -> bool:
	var remaining: Vector3=navigation.target_position-body.global_position
	remaining.y=0
	if remaining.length()<=float(settings.get("npc_arrival_distance_m",settings.arrival_distance_m)): return false
	var map:=navigation.get_navigation_map()
	var path:=NavigationServer3D.map_get_path(map,body.global_position,navigation.target_position,true)
	if path.size()<2: return false
	path[0]=body.global_position
	if not streaming.CanTraversePath(path): return false
	var length:=0.0
	for i in range(1,path.size()): length+=path[i-1].distance_to(path[i])
	if length<=0.001: return false
	if not bridge.BeginJourney(int(command.execution),path): return false
	for room in held_rooms: streaming.ReleaseActorRoom(str(room))
	held_rooms.clear()
	streaming.ReleaseActorPosition(actor_id)
	offscreen=true; restoring_physics=false; offline_elapsed=0.0
	body.collision_layer=0; body.collision_mask=0; body.visible=false
	body.process_mode=Node.PROCESS_MODE_DISABLED
	stop()
	return true

func step_offscreen(delta: float) -> void:
	if cancelled or player_can_see_room(body.global_position): restoring_physics=true
	if restoring_physics:
		if phase=="offscreen_exit_pending": phase="working"
		if held_rooms.is_empty():
			held_rooms=command.declaration.get("route_rooms",[]).duplicate()
			for room in held_rooms: streaming.HoldActorRoom(str(room))
		for room in held_rooms:
			if not streaming.RoomActive(str(room)): return
		bridge.EndJourney(int(command.execution))
		restore_body()
		if invalidated_guard: finish("knowledge-guard-invalidated");return
		reentry_blocked=true; reentry_anchor=body.global_position
		progress=body.global_position; stalled=0
		return
	if phase=="offscreen_working":
		if bridge.WorkElapsed(int(command.execution)): phase="offscreen_exit_pending"
		return
	if phase=="offscreen_exit_pending":
		var motor: Dictionary=command.declaration.motor
		if not motor.has("exit_target"):
			restoring_physics=true
			return
		var p: Array=motor.exit_target
		navigation.target_position=Vector3(p[0],p[1],p[2])
		var path:=NavigationServer3D.map_get_path(navigation.get_navigation_map(),body.global_position,navigation.target_position,true)
		if path.size()<2:
			restoring_physics=true
			return
		path[0]=body.global_position
		if not streaming.CanTraversePath(path):
			restoring_physics=true
			return
		if not bridge.BeginJourney(int(command.execution),path):
			restoring_physics=true
			return
		phase="departing"; offline_elapsed=0.0
		return
	offline_elapsed+=delta
	if offline_elapsed<float(settings.get("offscreen_step_seconds",0.5)): return
	var speed:=float(settings.movement_speed_mps)*float(command.declaration.motor.get("speed_multiplier",1.0))
	var preview: Vector3=bridge.PreviewJourney(int(command.execution),offline_elapsed,speed)
	if not streaming.CanTraversePath(PackedVector3Array([body.global_position,preview])):
		restoring_physics=true
		return
	if player_can_see_room(preview):
		restoring_physics=true
		return
	var next: Dictionary=bridge.AdvanceJourney(int(command.execution),offline_elapsed,speed)
	offline_elapsed=0.0
	if next.is_empty(): return
	var position: Vector3=next.position
	body.global_position=position
	if bool(next.arrived):
		var motor: Dictionary=command.declaration.motor
		if phase=="approaching" and command.declaration.timing.get("work_after_arrival",false) and motor.posture=="standing" and not motor.has("hand_contact"):
			bridge.EndJourney(int(command.execution))
			if not bridge.BeginWork(int(command.execution)):
				restoring_physics=true
			else: phase="offscreen_working"
		else: restoring_physics=true

func restore_body() -> void:
	offscreen=false; restoring_physics=false; offline_elapsed=0.0
	body.process_mode=Node.PROCESS_MODE_INHERIT
	body.collision_layer=saved_collision_layer; body.collision_mask=saved_collision_mask
	body.visible=true

func posture(to: String) -> bool:
	var motor: Dictionary=command.declaration.motor
	var target: Node3D=find_object(streaming,str(motor.object_id))
	if not is_instance_valid(target): return false
	var spec={"to":to,"seat_slot":"sit","seat_surface_slot":"seat_surface","left_foot_slot":"foot_left","right_foot_slot":"foot_right","exit_slot":"exit","timeout_s":8.0,"position_tolerance_m":0.04,"foot_tolerance_m":0.04,"pelvis_forward_m":float(motor.get("pelvis_forward_m",0.0)),"foot_forward_m":float(motor.get("foot_forward_m",0.0)),"ignore_target_collision":bool(motor.get("ignore_target_collision",false))}
	if not body.begin_native_posture(target,spec): return false
	body.play_action("sit_down" if to=="sitting" else "stand_up")
	return true

func find_object(node: Node,id: String) -> Node3D:
	if node is Node3D and (str(node.get_meta("world_object_id",""))==id or str(node.get_meta("opening_id",""))==id): return node
	for child in node.get_children():
		var found:=find_object(child,id)
		if found!=null: return found
	return null
