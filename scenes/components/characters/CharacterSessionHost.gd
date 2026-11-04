extends Node3D
## Main-owned physical execution of DV actions.
const BRIDGE = preload("res://scenes/main/CharacterSessionBridge.cs")
const READER = preload("res://scenes/main/DvPresentationContent.cs")
const BODY = preload("res://scenes/components/characters/StoryBody3D.gd")
const RENDER_VISIBILITY = preload("res://scenes/components/architecture/RenderVisibility.gd")
var bridge: Node
var streaming: Node3D
var body: CharacterBody3D
var camera: Camera3D
var navigation: NavigationAgent3D
var definition: Dictionary
var settings: Dictionary
var command: Dictionary = {}
var state: Dictionary = {}
var phase := "loading"
var ready_for_play := false
var elapsed := 0.0
var target_object: Node3D
var cancelled := false
var cancelling_posture := false
var last_failure := ""
var facing_elapsed := 0.0
var facing_total := 0.0
var approach_progress_position := Vector3.ZERO
var approach_stalled := 0.0
var recovery_wait := 0.0
var pending_action := ""
var props: Node3D
var menu: PopupMenu
var actions: Array = []
var player_paused := false
var manual_path := PackedVector3Array()
var manual_rooms: Array[String] = []
var manual_progress := Vector3.ZERO
var manual_stalled := 0.0
var manual_target: Variant = null
var presentation_map: Dictionary = {}
var wall_occlusion: Node
var npc: CharacterBody3D
var npc_command: Dictionary = {}
var autonomous_actors: Dictionary = {}
var yield_last: Dictionary = {}
var perception_elapsed := 0.0
var actor_trace_elapsed := 0.0
var perception_inside: Dictionary = {}
var perception_revision: Dictionary = {}
var conversation: Dictionary = {}
var conversation_spec: Dictionary = {}
var conversation_phase := ""
var saved_phase := ""
var saved_target := Vector3.ZERO
var restoring_action := false
var restoring_conversation_id := 0
var conversation_elapsed := 0.0
var shown_line := -1
var resume_work_started := false
var encounter_sample := 0.0
var dialogue_layer: CanvasLayer
var dialogue_label: Label
var deduction_label: Label
var checkpoint_message: Label
var reach: RefCounted
var stair_path:=PackedVector3Array()
var stair_path_index:=0
var stair_rooms: Array=[]
var restored_commands: Array=[]
var restoring_checkpoint:=false
var order_panel
var order_button: Button
var order_queue: Array[String] = []
var order_running := ""
var story_label: Label
var work_bars: Dictionary = {}
var chapter_save_pending := false
var hud_elapsed := 0.0
var carried_actors: Dictionary = {}
var native_book_ui: Node
var door_clearance = preload("res://scenes/components/architecture/DoorFrameClearance.gd").new()

func configure(path: String, restored: Dictionary = {}) -> void:
	bridge = BRIDGE.new()
	add_child(bridge)
	definition = bridge.Open(path)
	if not definition.get("presentation") is Dictionary:
		last_failure = "剧情尚未配置正式场景与动作映射。"
		return
	if not restored.is_empty():
		if int(restored.get("version",0))!=2 or not restored.get("core") is String: last_failure="存档格式无效。";return
		var error: String=bridge.PrepareCheckpoint(str(restored.core),false)
		if not error.is_empty() or not bridge.CommitPreparedCheckpoint(): last_failure="存档无法恢复："+error;return
		restored_commands=bridge.RestoredCommands();restoring_checkpoint=true
	settings = definition.presentation
	add_child(preload("res://scenes/components/rendering/ObserverEnvironment3D.gd").new())
	body = BODY.new()
	add_child(body)
	var p: Array = definition.actors[0].placement.position
	if restoring_checkpoint:
		var saved: Dictionary=bridge.LogicalPlacement(str(definition.viewer))
		if saved.has("X"): p=[saved.X,saved.Y,saved.Z]
	body.position = Vector3(p[0],p[1],p[2])
	var art: Dictionary = READER.new().ReadRecord(settings.model_source,"layouts",settings.model_definition)
	var model: Dictionary = art.models[definition.viewer].duplicate(true)
	model.character_id = definition.viewer
	if not body.configure(model,float(model.height),float(settings.collision_radius_m)):
		last_failure = "character-model-unavailable"
		return
	if definition.actors.size()>1:
		var actor: Dictionary = definition.actors[1]
		npc = BODY.new()
		add_child(npc)
		var np: Array = actor.placement.position
		if restoring_checkpoint:
			var saved: Dictionary=bridge.LogicalPlacement(str(actor.id))
			if saved.has("X"): np=[saved.X,saved.Y,saved.Z]
		npc.position=Vector3(np[0],np[1],np[2])
		var npc_model: Dictionary=art.models[str(actor.get("model_key",actor.id))].duplicate(true)
		npc_model.character_id=actor.id
		if not npc.configure(npc_model,float(npc_model.height),float(settings.collision_radius_m)):
			last_failure="npc-model-unavailable";return
	preload("res://scenes/components/characters/SeatPoseModifier3D.gd").prepare_geometry(body)
	camera = preload("res://scenes/components/rendering/TopDownCamera3D.gd").new()
	add_child(camera)
	camera.follow(body.position)
	camera.current = true
	wall_occlusion = preload("res://scenes/components/architecture/WallOcclusionController.gd").new()
	add_child(wall_occlusion)
	wall_occlusion.configure(camera,body,str(settings.room_manifest))
	presentation_map=JSON.parse_string(FileAccess.get_file_as_string(str(settings.room_manifest)))
	streaming = preload("res://scenes/components/architecture/RoomStreamingController.cs").new()
	add_child(streaming)
	if streaming.has_method("ConfigureReceipts"): streaming.ConfigureReceipts(settings.receipt_directory,settings.receipt_scope)
	streaming.Configure(settings.room_manifest,camera)
	for index in range(1,definition.actors.size()):
		var actor: Dictionary=definition.actors[index]
		var actor_body: CharacterBody3D=npc if index==1 else BODY.new()
		if index>1:
			add_child(actor_body)
			var initial: Array=actor.placement.position
			if restoring_checkpoint:
				var saved: Dictionary=bridge.LogicalPlacement(str(actor.id))
				if saved.has("X"): initial=[saved.X,saved.Y,saved.Z]
			actor_body.position=Vector3(initial[0],initial[1],initial[2])
			var actor_model: Dictionary=art.models[str(actor.get("model_key",actor.id))].duplicate(true)
			actor_model.character_id=actor.id
			if not actor_body.configure(actor_model,float(actor_model.height),float(settings.collision_radius_m)):
				last_failure="npc-model-unavailable"; return
		RENDER_VISIBILITY.apply(actor_body.visual,false)
		var executor=preload("res://scenes/components/characters/AutonomousActorExecutor.gd").new()
		executor.configure(actor_body,bridge,streaming,settings,str(actor.id),camera)
		autonomous_actors[str(actor.id)]=executor
	for binding: Dictionary in settings.initial_door_bindings:
		var fact: Dictionary=bridge.PhysicalFact(str(binding.object),str(binding.facet),str(binding.field))
		streaming.InitializeDoor(str(binding.opening),bool(fact.get("Value",false)))
	if restoring_checkpoint and not streaming.RestoreLogicalDoors(bridge.LogicalDoors()): last_failure="存档门状态无效。";return
	streaming.SetSubject(body)
	streaming.BundleAttached.connect(collect_surfaces)
	navigation = NavigationAgent3D.new()
	navigation.path_desired_distance = 0.10
	navigation.target_desired_distance = float(settings.arrival_distance_m)
	body.add_child(navigation)
	menu = PopupMenu.new()
	add_child(menu)
	actions = definition.actors[0].actions
	menu.id_pressed.connect(select_action)
	dialogue_layer=CanvasLayer.new()
	add_child(dialogue_layer)
	var save_button:=Button.new()
	save_button.text="保存"
	save_button.position=Vector2(1160,24)
	save_button.theme=load("res://assets/ui/OverlayTheme.tres")
	save_button.pressed.connect(save_game)
	dialogue_layer.add_child(save_button)
	checkpoint_message=Label.new()
	checkpoint_message.position=Vector2(910,65)
	checkpoint_message.add_theme_font_size_override("font_size",18)
	dialogue_layer.add_child(checkpoint_message)
	dialogue_label=Label.new()
	dialogue_label.position=Vector2(60,60)
	dialogue_label.add_theme_font_size_override("font_size",24)
	dialogue_label.visible=false
	dialogue_layer.add_child(dialogue_label)
	deduction_label=Label.new()
	deduction_label.position=Vector2(60,120)
	deduction_label.add_theme_font_size_override("font_size",22)
	deduction_label.custom_minimum_size=Vector2(780,0)
	deduction_label.autowrap_mode=TextServer.AUTOWRAP_WORD_SMART
	deduction_label.visible=false
	dialogue_layer.add_child(deduction_label)
	story_label=Label.new()
	story_label.position=Vector2(24,24)
	story_label.add_theme_font_size_override("font_size",18)
	dialogue_layer.add_child(story_label)
	if definition.has("action_order_panel"):
		order_panel=preload("res://scenes/UIpanel/DeductionInterface/ActionOrderPanel.gd").new()
		dialogue_layer.add_child(order_panel)
		order_panel.configure(definition.action_order_panel)
		order_panel.submitted.connect(submit_action_order)
		order_button=Button.new()
		order_button.text=str(definition.action_order_panel.title)
		order_button.position=Vector2(24,100)
		order_button.pressed.connect(order_panel.show)
		dialogue_layer.add_child(order_button)
	for actor: Dictionary in definition.actors:
		var bar:=ProgressBar.new()
		bar.size=Vector2(80,8);bar.max_value=1;bar.show_percentage=false;bar.mouse_filter=Control.MOUSE_FILTER_IGNORE
		dialogue_layer.add_child(bar);work_bars[str(actor.id)]=bar
	props=preload("res://scenes/components/world/PhysicalPropView.gd").new()
	add_child(props)
	var actor_bodies: Dictionary={}
	for actor_id in autonomous_actors: actor_bodies[actor_id]=autonomous_actors[actor_id].body
	props.configure(bridge,body,streaming,str(definition.viewer),settings.fixtures,actor_bodies)
	actor_bodies[str(definition.viewer)]=body
	for actor_id in autonomous_actors: autonomous_actors[actor_id].bind_observation_context(actor_bodies,props)
	native_book_ui = preload("res://scenes/UIpanel/CasebookUI/NativeBookInterface.tscn").instantiate()
	add_child(native_book_ui)
	native_book_ui.configure(self, restored)
	prepare_navigation()

func save_game() -> void:
	if not ready_for_play: return
	var saved:=host_checkpoint()
	if saved.has("error"):
		checkpoint_message.text="暂时无法保存。";push_error("Checkpoint capture: "+str(saved.error));return
	var error: String=bridge.WriteSavedGame(JSON.stringify(saved))
	checkpoint_message.text="已保存" if error.is_empty() else "保存失败。"
	if error.is_empty() and is_instance_valid(native_book_ui): native_book_ui._write_save_metadata()
	if not error.is_empty(): push_error("Checkpoint write: "+error)

func prepare_navigation() -> void:
	while not streaming.IsInitialReady(): await get_tree().process_frame
	var region := NavigationRegion3D.new()
	var mesh: NavigationMesh=load(str(settings.navigation_mesh))
	var manifest: Dictionary=JSON.parse_string(FileAccess.get_file_as_string(str(settings.room_manifest)))
	if str(mesh.get_meta("source_sha256",""))!=str(manifest.source_sha256) or (mesh.has_meta("scope_sha256") and str(mesh.get_meta("scope_sha256"))!=str(manifest.scope_sha256)):
		last_failure="navigation-recompile-required"
		return
	region.navigation_mesh = mesh
	add_child(region)
	await get_tree().physics_frame
	await get_tree().physics_frame
	collect_surfaces(streaming)
	props.synchronize(0)
	if restoring_checkpoint: restore_logical_actions()
	if not last_failure.is_empty(): return
	if command.is_empty() and conversation.is_empty(): phase = "idle"
	ready_for_play = true
	streaming.BeginMeasuredRun()

func host_checkpoint() -> Dictionary:
	var doors: Dictionary=streaming.LogicalDoorStates()
	for id in doors:
		var door: Dictionary=doors[id]
		bridge.ReportDoor(str(id),float(door.Angle),float(door.Target),bool(door.Moving),float(door.CloseRemaining))
	var core: Dictionary=bridge.CaptureCheckpoint()
	if core.has("error"): return core
	var result := {"version":2,"core":str(core.json)}
	if is_instance_valid(native_book_ui):
		result["casebook"] = native_book_ui.archive.export_data()
		result["ui_knowledge"] = native_book_ui.export_knowledge()
	return result

func restore_logical_actions() -> void:
	for next: Dictionary in restored_commands:
		var actor:=str(next.actor)
		var motor: Dictionary=next.declaration.motor
		if actor!=str(definition.viewer):
			var executor=autonomous_actors.get(actor)
			if executor==null: last_failure="存档人物不存在。";return
			executor.dispatch(next)
			if bool(next.work_started):
				executor.phase="working"
				if str(motor.get("posture","standing"))=="sitting": executor.body.is_seated=true;executor.body.play_action("sit_idle")
			continue
		command=next;phase="working" if bool(next.work_started) else "approaching";resume_work_started=bool(next.work_started)
		if bool(next.work_started) and str(motor.get("posture","standing"))=="sitting": body.is_seated=true;body.play_action("sit_idle")
		if motor.get("target_current",false): navigation.target_position=body.global_position
		elif motor.has("observe_actor"):
			var observed:=observed_target(motor)
			if not is_instance_valid(observed):
				bridge.Fail(int(command.execution),"observation-target-unavailable");command={};phase="idle";continue
			var away:=body.global_position-observed.global_position
			away.y=0
			if away.length_squared()<0.0001: away=Vector3.RIGHT
			navigation.target_position=observed.global_position+away.normalized()*float(motor.get("observation_approach_m",0.8))
		elif motor.has("target_slot"): navigation.target_position=streaming.OperationSlot(str(motor.object_id),str(motor.target_slot))
		else:
			var point: Array=motor.target;navigation.target_position=Vector3(point[0],point[1],point[2])
		if phase=="approaching" and absf(navigation.target_position.y-body.global_position.y)>0.3:
			stair_rooms=next.declaration.get("route_rooms",[]).duplicate()
			for room: String in stair_rooms: streaming.HoldActorRoom(room)
			stair_path=preload("res://tools/building/BuildRoutes.gd").plan(navigation.get_navigation_map(),streaming.StairBuildings(),body.global_position,navigation.target_position)
			if stair_path.is_empty() or not streaming.CanTraversePath(stair_path): last_failure="存档行程暂不可达。";return
		target_object=find_object(self,str(motor.get("object_id","")))
	conversation=bridge.Conversation()
	if not conversation.is_empty():
		conversation_spec=bridge.ConversationSpec();var target=autonomous_actors.get(str(conversation_spec.get("target","")))
		if target==null: last_failure="存档对话对象不存在。";return
		npc=target.body;conversation_phase="speaking" if str(conversation.Phase)=="speaking" else "approach";shown_line=-1
		if conversation_phase=="speaking": body.start_mutual_conversation(npc)
	restored_commands.clear();restoring_checkpoint=false

func collect_surfaces(node: Node) -> void:
	if is_instance_valid(wall_occlusion): wall_occlusion.collect(node)
	hide_ceilings(node)

func hide_ceilings(node: Node) -> void:
	if node is Node3D and (bool(node.get_meta("ceiling_component",false)) or str(node.get_meta("kind","")) in ["ceiling","roof"]): node.visible=false
	for child in node.get_children(): hide_ceilings(child)

func find_object(node: Node,id: String) -> Node3D:
	if node is Node3D and (str(node.get_meta("world_object_id","")) == id or str(node.get_meta("opening_id","")) == id): return node
	for child in node.get_children():
		var result := find_object(child,id)
		if result != null: return result
	return null

func select_action(index: int) -> void:
	clear_manual_route()
	if index>=actions.size():
		var conversation_index:=index-actions.size()
		if conversation_index<0 or conversation_index>=definition.get("conversations",[]).size() or not conversation_in_context(definition.conversations[conversation_index]): return
		begin_conversation(index-actions.size())
		return
	if index<0 or index>=actions.size() or not action_in_context(actions[index]): return
	if not command.is_empty():
		pending_action=str(actions[index].id)
		bridge.Cancel()
		return
	last_failure = bridge.Submit(str(actions[index].id))

func action_in_context(action: Dictionary) -> bool:
	if bool(action.get("panel_only",false)): return false
	if not bridge.Available(str(action.id)): return false
	var motor: Dictionary=action.get("motor",{})
	var target:=body.global_position
	if motor.has("observe_actor"):
		var executor=autonomous_actors.get(str(motor.observe_actor))
		if executor==null or not is_instance_valid(executor.body) or not executor.body.is_visible_in_tree(): return false
		target=executor.body.global_position
	elif motor.has("target_slot"):
		target=streaming.OperationSlot(str(motor.object_id),str(motor.target_slot))
	elif motor.has("target"):
		var raw: Array=motor.target
		if raw.size()!=3: return false
		target=Vector3(raw[0],raw[1],raw[2])
	elif not motor.get("target_current",false): return false
	if motor.has("observe_fixture"):
		var fixture: Node3D=props.nodes.get(str(motor.observe_fixture))
		if not is_instance_valid(fixture) or not fixture.is_visible_in_tree(): return false
	if motor.has("observe_group") and not observation_valid(motor): return false
	var debug_global: bool="--dv-global-action-debug" in OS.get_cmdline_user_args()
	if not debug_global and body.global_position.distance_to(target)>float(settings.get("interaction_context_radius_m",2.0)): return false
	if absf(target.y-body.global_position.y)>0.5 and not debug_global: return false
	if body.global_position.distance_to(target)>0.15:
		var route:=NavigationServer3D.map_get_path(navigation.get_navigation_map(),body.global_position,target,true)
		if route.is_empty() or route[-1].distance_to(target)>0.35 or not streaming.CanTraversePath(route): return false
	return true

func conversation_in_context(item: Dictionary) -> bool:
	if not conversation.is_empty() or not bridge.ConversationAvailable(str(item.id)): return false
	var executor=autonomous_actors.get(str(item.target))
	if executor==null or not is_instance_valid(executor.body) or not executor.body.is_visible_in_tree(): return false
	var target: Vector3=executor.body.global_position
	if "--dv-global-action-debug" not in OS.get_cmdline_user_args() and body.global_position.distance_to(target)>float(settings.get("interaction_context_radius_m",2.0)): return false
	var route:=NavigationServer3D.map_get_path(navigation.get_navigation_map(),body.global_position,target,true)
	return not route.is_empty() and route[-1].distance_to(target)<=0.35 and streaming.CanTraversePath(route)

func action_context_identity(action: Dictionary) -> String:
	var motor: Dictionary=action.get("motor",{})
	if motor.get("target_current",false): return "personal"
	if motor.has("observe_actor"): return "actor:"+str(motor.observe_actor)
	if motor.has("observe_fixture"): return "object:"+str(motor.observe_fixture)
	return "object:"+str(motor.get("object_id",action.id))

func action_context_position(action: Dictionary) -> Vector3:
	var motor: Dictionary=action.get("motor",{})
	if motor.has("observe_actor"):
		var executor=autonomous_actors.get(str(motor.observe_actor))
		if executor!=null: return executor.body.global_position
	if motor.has("observe_fixture"):
		var fixture: Node3D=props.nodes.get(str(motor.observe_fixture))
		if is_instance_valid(fixture): return fixture.global_position
	var object:=find_object(self,str(motor.get("object_id","")))
	if is_instance_valid(object): return object.global_position
	if motor.has("target_slot"): return streaming.OperationSlot(str(motor.object_id),str(motor.target_slot))
	if motor.has("target"):
		var raw: Array=motor.target
		if raw.size()==3: return Vector3(raw[0],raw[1],raw[2])
	return body.global_position

func refresh_menu(pointer:=Vector2.ZERO, use_pointer:=false) -> void:
	menu.clear()
	var candidates: Array=[]
	for index in actions.size():
		var action: Dictionary=actions[index]
		if action_in_context(action): candidates.append({"id":index,"label":str(action.label),"key":action_context_identity(action),"position":action_context_position(action)})
	for index in definition.get("conversations",[]).size():
		var item: Dictionary=definition.conversations[index]
		if conversation_in_context(item): candidates.append({"id":actions.size()+index,"label":str(item.label),"key":"actor:"+str(item.target),"position":autonomous_actors[str(item.target)].body.global_position})
	if candidates.is_empty(): return
	if not use_pointer:
		# Keyboard interaction must not hide a nearby clue behind a closer NPC.
		for group in ["object:","actor:","personal"]:
			var heading_added:=false
			for candidate: Dictionary in candidates:
				if not str(candidate.key).begins_with(group): continue
				if not heading_added:
					menu.add_separator("现场检视与操作" if group=="object:" else "附近人物" if group=="actor:" else "思考与排演")
					heading_added=true
				menu.add_item(str(candidate.label),int(candidate.id))
		return
	var has_object:=false
	for candidate: Dictionary in candidates:
		if str(candidate.key)!="personal": has_object=true;break
	var chosen: String=""
	var best:=INF
	for candidate: Dictionary in candidates:
		if has_object and str(candidate.key)=="personal": continue
		var point: Vector3=candidate.position
		if use_pointer and camera.is_position_behind(point): continue
		var distance: float=camera.unproject_position(point).distance_to(pointer) if use_pointer else body.global_position.distance_to(point)
		if distance<best: best=distance;chosen=str(candidate.key)
	if use_pointer and best>72.0: return
	for candidate: Dictionary in candidates:
		if str(candidate.key)==chosen: menu.add_item(str(candidate.label),int(candidate.id))

func _unhandled_input(event: InputEvent) -> void:
	if not ready_for_play: return
	if is_instance_valid(native_book_ui) and native_book_ui.is_modal_active(): return
	if not conversation.is_empty() and event.is_action_pressed("dialogue_continue"):
		var view: Dictionary=bridge.Conversation()
		if not view.is_empty() and str(view.Phase)=="speaking": bridge.AdvanceConversation(int(view.Id),int(view.LineIndex))
		get_viewport().set_input_as_handled();return
	if not conversation.is_empty() and event.is_action_pressed("ui_cancel"):
		bridge.InterruptConversation(int(conversation.Id))
		end_conversation()
		get_viewport().set_input_as_handled();return
	if event.is_action_pressed("interact"):
		refresh_menu()
		if menu.item_count>0:
			menu.position=Vector2i(get_viewport().get_visible_rect().size*0.5)
			menu.popup()
		get_viewport().set_input_as_handled()
	if event.is_action_pressed("ui_cancel"):
		bridge.Cancel()
		get_viewport().set_input_as_handled()
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_RIGHT:
		refresh_menu(event.position,true)
		if menu.item_count>0:
			menu.position = Vector2i(event.position)
			menu.popup()
		get_viewport().set_input_as_handled()
	if event is InputEventMouseButton and event.pressed and event.button_index in [MOUSE_BUTTON_WHEEL_UP, MOUSE_BUTTON_WHEEL_DOWN]:
		camera.adjust_view(0.0,-0.6 if event.button_index == MOUSE_BUTTON_WHEEL_UP else 0.6)
		get_viewport().set_input_as_handled()

func _physics_process(delta: float) -> void:
	if player_paused: return
	if ready_for_play:
		var crossing_opening:=str(command.get("declaration",{}).get("motor",{}).get("door_opening","")) if phase=="crossing" else ""
		door_clearance.update(body,streaming,crossing_opening)
	if command.is_empty() and not stair_rooms.is_empty():
		for room: String in stair_rooms: streaming.ReleaseActorRoom(room)
		stair_rooms.clear();stair_path.clear()
	if not ready_for_play: return
	camera.adjust_view(-Input.get_action_strength("camera_left") * delta, (Input.get_action_strength("camera_down")-Input.get_action_strength("camera_up")) * 4.0 * delta)
	var axis := (Input.get_vector("ui_left","ui_right","ui_up","ui_down")+Input.get_vector("move_left","move_right","move_up","move_down")).limit_length()
	if not order_running.is_empty() or not order_queue.is_empty() or bool(state.get("resting",false)): axis=Vector2.ZERO
	if axis.length_squared()>0:
		pending_action=""
		clear_manual_route()
	if axis.length_squared()>0 and not conversation.is_empty():
		bridge.InterruptConversation(int(conversation.Id));end_conversation()
	if axis.length_squared()>0 and not command.is_empty() and conversation.is_empty(): bridge.Cancel()
	var core_started:=Time.get_ticks_usec()
	var update: Dictionary = bridge.Step(delta,command.is_empty() and manual_path.is_empty() and manual_target==null and axis.length_squared()==0,false)
	update_story_ui(update,delta)
	if not update.deduction.is_empty():
		var lines: PackedStringArray=[]
		for item: Dictionary in update.deduction.results:
			lines.append(str(item.label if str(item.Status)=="proved" else item.unknown_label))
		deduction_label.text="\n".join(lines)
		deduction_label.visible=true
	var core_ms:=float(Time.get_ticks_usec()-core_started)/1000.0
	state["game_seconds"]=update.game_seconds
	props.synchronize(int(update.world_revision))
	for binding: Dictionary in settings.get("dynamic_doors",[]):
		var allowed:=true
		for reference: Dictionary in binding.get("permissions",[binding.get("permission",{})]):
			var permission: Dictionary=bridge.PhysicalFact(str(reference.object),str(reference.facet),str(reference.field))
			if not bool(permission.get("Known",false)) or not bool(permission.get("Value",false)): allowed=false;break
		var door_open: bool=streaming.DoorOpen(str(binding.opening))
		if bool(binding.get("open_from_fact",false)):
			var declared: Dictionary=bridge.PhysicalFact(str(binding.object),str(binding.facet),str(binding.field))
			if bool(declared.get("Value",false)) and not door_open: streaming.BeginOpenDoor(str(binding.opening))
			continue
		if allowed or not door_open:
			bridge.ReportPhysicalBoolean(str(binding.object),str(binding.facet),str(binding.field),door_open)
	if command.is_empty() and not pending_action.is_empty():
		var error: String=bridge.Submit(pending_action)
		if error!="Actor is occupied.":
			pending_action=""
			last_failure=error
	for next: Dictionary in update.commands:
		if str(next.actor)!=str(definition.viewer):
			var executor=autonomous_actors.get(str(next.actor))
			if executor!=null: executor.dispatch(next)
			continue
		if next.kind == "cancel": cancelled=true
		else:
			command = next
			stair_path.clear();stair_path_index=0
			for room: String in stair_rooms: streaming.ReleaseActorRoom(room)
			stair_rooms.clear()
			last_failure=""
			elapsed=0
			cancelled=false
			cancelling_posture=false
			phase="approaching"
			approach_progress_position=body.global_position
			approach_stalled=0.0
			var motor: Dictionary=command.declaration.motor
			if not bool(motor.get("target_current",false)) and not motor.has("object_id"):
				bridge.Fail(int(command.execution),"missing-motor-object-id")
				command={};phase="idle";last_failure="missing-motor-object-id";continue
			target_object=find_object(self,str(motor.get("object_id","")))
			if command.declaration.motor.get("target_current",false):
				navigation.target_position=body.global_position
			elif motor.has("observe_actor"):
				var observed:=observed_target(motor)
				if not is_instance_valid(observed):
					bridge.Fail(int(command.execution),"observation-target-unavailable");command={};phase="idle";continue
				var away:=body.global_position-observed.global_position
				away.y=0
				if away.length_squared()<0.0001: away=Vector3.RIGHT
				navigation.target_position=observed.global_position+away.normalized()*float(motor.get("observation_approach_m",0.8))
			elif command.declaration.motor.has("target_slot"):
				navigation.target_position=streaming.OperationSlot(str(command.declaration.motor.object_id),str(command.declaration.motor.target_slot))
			else:
				var p: Array = command.declaration.motor.target
				navigation.target_position=Vector3(p[0],p[1],p[2])
			if absf(navigation.target_position.y-body.global_position.y)>0.3:
				stair_rooms=command.declaration.get("route_rooms",[]).duplicate()
				for room: String in stair_rooms: streaming.HoldActorRoom(room)
				stair_path=preload("res://tools/building/BuildRoutes.gd").plan(navigation.get_navigation_map(),streaming.StairBuildings(),body.global_position,navigation.target_position)
				if stair_path.is_empty() or not streaming.CanTraversePath(stair_path):
					bridge.Fail(int(command.execution),"cross-floor-route-unavailable");command={};phase="idle";last_failure="cross-floor-route-unavailable";continue
	var rate := float(update.world_speed)
	var step := float(update.simulation_seconds)
	if bool(update.get("resting",false)) or step>5:
		rate=1.0;step=delta
	var motor_started:=Time.get_ticks_usec()
	if not conversation.is_empty(): execute_conversation(step,rate)
	elif not command.is_empty(): execute(step,rate)
	elif manual_target!=null:
		var goal: Vector3=manual_target
		manual_target=null
		start_manual_route(goal)
	elif not manual_path.is_empty():
		step_manual_route(delta)
	elif axis.length_squared()>0:
		var direction := camera.global_basis.x*axis.x+camera.global_basis.z*axis.y
		direction.y=0
		move(direction.normalized(),delta,1.0)
	else: move(Vector3.ZERO,delta,1.0)
	bridge.RecordFrame(phase,core_ms,float(Time.get_ticks_usec()-motor_started)/1000.0,rate)
	if is_instance_valid(body.animation_player): body.animation_player.speed_scale=rate
	camera.follow(body.global_position)
	bridge.ReportPlacement(body.global_position.x,body.global_position.y,body.global_position.z,body.visual.rotation.y,streaming.CurrentRoom())
	var physically_needed: Dictionary={}
	update_actor_carries()
	var live_commands: Array=[command]
	for actor_id in autonomous_actors: live_commands.append(autonomous_actors[actor_id].command)
	for active: Dictionary in live_commands:
		if active.is_empty(): continue
		var motor: Dictionary=active.get("declaration",{}).get("motor",{})
		if motor.has("observe_actor"): physically_needed[str(motor.observe_actor)]=true
		for target: String in motor.get("observe_group",[]): physically_needed[target]=true
	if not conversation.is_empty(): physically_needed[str(conversation_spec.get("target",""))]=true
	for actor_id in autonomous_actors:
		var executor=autonomous_actors[actor_id]
		var actor_body: CharacterBody3D=executor.body
		if carried_actors.has(actor_id):
			var carrier: CharacterBody3D=autonomous_actors[carried_actors[actor_id].carrier].body
			actor_body.global_position=carrier.global_position+Vector3(0,0.7,0)+carrier.global_basis.x*0.3
			bridge.ReportActorPlacement(actor_id,actor_body.global_position.x,actor_body.global_position.y,actor_body.global_position.z,actor_body.visual.rotation.y,streaming.RoomAt(carrier.global_position))
			continue
		var actor_room: String=streaming.RoomAt(actor_body.global_position)
		var player_visible: bool=streaming.RoomVisible(actor_room)
		if actor_room.is_empty(): player_visible=camera.is_position_in_frustum(actor_body.global_position+Vector3.UP)
		RENDER_VISIBILITY.apply(actor_body.visual,player_visible)
		executor.set_idle_suspended(not player_visible and not physically_needed.has(actor_id))
		if not executor.offscreen and not executor.idle_suspended: streaming.UpdateActorRoom(actor_id,actor_body.global_position)
		executor.step(step,rate,not conversation.is_empty() and str(conversation_spec.target)==actor_id and conversation_phase=="speaking")
		if not executor.idle_suspended: bridge.ReportActorPlacement(actor_id,actor_body.global_position.x,actor_body.global_position.y,actor_body.global_position.z,actor_body.visual.rotation.y,streaming.RoomAt(actor_body.global_position))
		if is_instance_valid(actor_body.animation_player): actor_body.animation_player.speed_scale=rate
		if bool(settings.get("actor_yield_enabled",false)) and not definition.get("yield_stations",[]).is_empty() and executor.command.is_empty() and conversation.is_empty() and (axis.length_squared()>0 or phase=="approaching"):
			var now:=float(update.game_seconds)
			var nearby:=body.global_position.distance_to(actor_body.global_position)<=float(settings.get("yield_radius_m",1.3))
			if nearby and now-float(yield_last.get(actor_id,-100000.0))>=float(settings.get("yield_retry_seconds",8.0)):
				yield_last[actor_id]=now
				request_nearby_yield(actor_id,executor)
	actor_trace_elapsed+=delta
	if actor_trace_elapsed>=1.0:
		actor_trace_elapsed=0.0
		for actor_id in autonomous_actors:
			var executor=autonomous_actors[actor_id]
			var actor_body: CharacterBody3D=executor.body
			var resources: PackedStringArray=[]
			if not executor.command.is_empty():
				for resource in executor.command.declaration.get("resource_reservations",[]): resources.append(str(resource))
			bridge.RecordActorMotion(actor_id,int(executor.command.get("execution",0)),executor.phase,actor_body.global_position.x,actor_body.global_position.z,executor.navigation.target_position.x,executor.navigation.target_position.z,resources)
	perception_elapsed+=delta
	if perception_elapsed>=0.5:
		perception_elapsed=0.0
		sample_passive_perception()
	if is_instance_valid(npc):
		var target_id:=str(conversation_spec.get("target",definition.actors[1].id))
		npc_command=autonomous_actors[target_id].command
		if not conversation.is_empty():
			encounter_sample+=delta
			if encounter_sample>=0.5:
				encounter_sample=0.0
				bridge.RecordEncounter(int(conversation.Id),conversation_phase,body.global_position.x,body.global_position.z,npc.global_position.x,npc.global_position.z,int(npc_command.get("execution",0)))
	wall_occlusion.update_view()

func request_nearby_yield(actor_id: String,executor: RefCounted) -> void:
	var actor_body: CharacterBody3D=executor.body
	var room: String=str(streaming.RoomAt(actor_body.global_position))
	var map: RID=executor.navigation.get_navigation_map()
	var origin: Vector3=actor_body.global_position
	var away: Vector3=origin-body.global_position; away.y=0
	var best_id: String=""
	var best_score: float=-INF
	var examined:=0
	var diagnostics: Array[Dictionary]=[]
	for station: Dictionary in definition.get("yield_stations",[]):
		if str(station.room)!=room: continue
		if examined>=int(settings.get("yield_candidate_budget",4)): break
		examined+=1
		var raw: Array=station.target
		var target:=Vector3(raw[0],raw[1],raw[2])
		var displacement:=target-origin;displacement.y=0
		var detail: Dictionary={"station":str(station.id),"origin":[origin.x,origin.y,origin.z],"target":raw,"player":[body.global_position.x,body.global_position.y,body.global_position.z]}
		diagnostics.append(detail)
		if displacement.length()<0.4 or displacement.length()>float(settings.get("yield_max_distance_m",3.0)): detail.reason="distance";continue
		if displacement.dot(away)<=0: detail.reason="toward-player";continue
		var projected: Vector3=NavigationServer3D.map_get_closest_point(map,target)
		detail["projected"]=[projected.x,projected.y,projected.z]
		if projected.distance_to(target)>float(settings.get("yield_navigation_tolerance_m",0.3)): detail.reason="projection";continue
		var path:=NavigationServer3D.map_get_path(map,origin,target,true)
		if path.size()<2 or path[-1].distance_to(target)>float(settings.get("yield_navigation_tolerance_m",0.3)): detail.reason="path";continue
		var path_length:=0.0
		var safe:=true
		for i in range(1,path.size()):
			var segment: Vector3=path[i]-path[i-1]
			path_length+=segment.length()
			var ray:=PhysicsRayQueryParameters3D.create(path[i-1]+Vector3.UP*0.5,path[i]+Vector3.UP*0.5)
			ray.exclude=[actor_body.get_rid()]
			var hit: Dictionary=get_world_3d().direct_space_state.intersect_ray(ray)
			if not hit.is_empty(): safe=false;detail["collider"]=str(hit.collider);break
		if not safe or path_length>float(settings.get("yield_max_distance_m",3.0))*1.5: detail.reason="collision-or-path-length";continue
		detail.reason="candidate";detail["path_length"]=path_length
		var score:=target.distance_to(body.global_position)-path_length*0.25
		if score>best_score: best_score=score;best_id=str(station.id)
	bridge.RecordYieldCandidates(actor_id,room,diagnostics,best_id)
	if not best_id.is_empty(): bridge.RequestYield(actor_id,actor_id+".yield."+best_id)

func sample_passive_perception() -> void:
	for sensor: Dictionary in definition.get("passive_sensors",[]):
		var actor_id:=str(sensor.actor)
		var actor_body: CharacterBody3D=body if actor_id==str(definition.viewer) else autonomous_actors[actor_id].body
		var p: Array=sensor.position
		var point:=Vector3(p[0],p[1],p[2])
		var observed_body: CharacterBody3D = null
		if sensor.has("target_actor"):
			var observed_executor = autonomous_actors.get(str(sensor.target_actor))
			if observed_executor==null: continue
			observed_body=observed_executor.body
			point=observed_body.global_position+Vector3.UP*float(sensor.eye_height_m)
		var key:=actor_id+":"+str(sensor.id)
		var origin:=actor_body.global_position+Vector3.UP*float(sensor.eye_height_m)
		var visible:=origin.distance_to(point)<=float(sensor.distance_m)
		if visible:
			var ray:=PhysicsRayQueryParameters3D.create(origin,point)
			ray.exclude=[actor_body.get_rid()]
			var hit: Dictionary=get_world_3d().direct_space_state.intersect_ray(ray)
			visible=hit.is_empty() or (is_instance_valid(observed_body) and hit.get("collider")==observed_body)
		if visible:
			var ref: Dictionary=sensor.ref
			var fact: Dictionary=bridge.PhysicalFact(str(ref.object),str(ref.facet),str(ref.field))
			var revision := str(fact.get("Revision","unknown"))+":"+str(fact.get("Known",false))
			if not bool(perception_inside.get(key,false)) or str(perception_revision.get(key,""))!=revision:
				bridge.Perceive(actor_id,str(ref.object),str(ref.facet),str(ref.field),str(sensor.id))
				perception_revision[key]=revision
		perception_inside[key]=visible

func move(direction: Vector3,delta: float,rate: float,max_distance: float = INF) -> void:
	var speed: float = minf(float(settings.movement_speed_mps)*rate,max_distance/maxf(delta,0.000001))
	body.velocity.x=direction.x*speed
	body.velocity.z=direction.z*speed
	if not body.is_on_floor(): body.velocity.y-=9.8*delta
	else: body.velocity.y=0
	body.move_and_slide()
	body.set_movement_speed(direction.length()*float(settings.movement_speed_mps))
	if direction.length_squared()>0: body.face_world_direction(direction)

func observed_target(motor: Dictionary) -> Node3D:
	if motor.has("observe_actor"):
		var executor=autonomous_actors.get(str(motor.observe_actor))
		return executor.body if executor!=null else null
	if motor.has("observe_fixture"): return props.nodes.get(str(motor.observe_fixture))
	if motor.has("observe_world_object"): return find_object(streaming,str(motor.observe_world_object))
	return null

func observation_valid(motor: Dictionary) -> bool:
	if motor.has("observe_group"):
		var eye:=body.global_position+Vector3.UP*float(motor.get("observation_eye_height_m",0.9))
		for id: String in motor.observe_group:
			var executor=autonomous_actors.get(id)
			if executor==null or not is_instance_valid(executor.body) or not executor.body.is_visible_in_tree(): return false
			var point: Vector3=executor.body.global_position+Vector3.UP*float(motor.get("observation_target_height_m",0.8))
			if eye.distance_to(point)>float(motor.observation_group_distance_m): return false
			var group_ray:=PhysicsRayQueryParameters3D.create(eye,point,1)
			if not body.get_world_3d().direct_space_state.intersect_ray(group_ray).is_empty(): return false
		for observed: Dictionary in motor.get("observed_group_actions",[]):
			if bridge.ActorWorkingAction(str(observed.actor))!=str(observed.action): return false
	if not motor.has("observe_actor") and not motor.has("observe_fixture") and not motor.has("observe_world_object"): return true
	var target:=observed_target(motor)
	if not is_instance_valid(target) or not target.is_visible_in_tree(): return false
	if motor.has("evidence_fixture"):
		var evidence: Node3D=props.nodes.get(str(motor.evidence_fixture))
		if not is_instance_valid(evidence) or not evidence.is_visible_in_tree(): return false
		var eye:=body.global_position+Vector3.UP*float(motor.get("observation_eye_height_m",0.9))
		if eye.distance_to(evidence.global_position)>float(motor.get("observation_distance_m",1.5)): return false
		var evidence_ray:=PhysicsRayQueryParameters3D.create(eye,evidence.global_position)
		evidence_ray.exclude=[body.get_rid()]
		var evidence_hit: Dictionary=body.get_world_3d().direct_space_state.intersect_ray(evidence_ray)
		if not evidence_hit.is_empty() and (evidence_hit.get("collider")!=target or (evidence_hit.position as Vector3).distance_to(evidence.global_position)>0.2): return false
	if motor.has("observed_actor_room") and streaming.RoomAt(target.global_position)!=str(motor.observed_actor_room): return false
	if motor.has("observed_actor_action"):
		var target_executor=autonomous_actors.get(str(motor.get("observe_actor","")))
		if target_executor==null or str(target_executor.command.get("declaration",{}).get("id",""))!=str(motor.observed_actor_action): return false
	var from:=body.global_position+Vector3.UP*float(motor.get("observation_eye_height_m",0.9))
	var to:=target.global_position
	if motor.has("observe_world_object"):
		var leaf:=target.get_node_or_null("VisualRoot/Hinge/DoorLeafMesh") as Node3D
		if is_instance_valid(leaf): to=leaf.global_position
	if motor.has("observe_actor"): to+=Vector3.UP*float(motor.get("observation_target_height_m",0.8))
	if from.distance_to(to)>float(motor.get("observation_distance_m",1.5)): return false
	var ray:=PhysicsRayQueryParameters3D.create(from,to)
	ray.exclude=[body.get_rid()]
	var hit: Dictionary=get_world_3d().direct_space_state.intersect_ray(ray)
	var collider: Node=hit.get("collider")
	return hit.is_empty() or collider==target or (is_instance_valid(collider) and target.is_ancestor_of(collider))

func begin_conversation(index: int) -> void:
	if not conversation.is_empty() or not is_instance_valid(npc): return
	conversation_spec=definition.conversations[index]
	var target_executor=autonomous_actors.get(str(conversation_spec.target))
	if target_executor==null: last_failure="conversation-target-unavailable"; return
	npc=target_executor.body
	var result: Dictionary=bridge.StartConversation(str(conversation_spec.id))
	if result.has("error"):
		last_failure=str(result.error);return
	conversation=result
	var activity: Variant=bridge.Snapshot().get("activity")
	resume_work_started=activity is Dictionary and activity.get("WorkStartedAt")!=null
	conversation_elapsed=0
	shown_line=-1
	saved_phase=phase
	saved_target=navigation.target_position
	conversation_phase="approach"
	if phase in ["sitting","standing"]: conversation_phase="finish_posture"
	elif body.is_seated:
		if not posture("standing"):
			bridge.InterruptConversation(int(conversation.Id));bridge.CompleteConversation(int(conversation.Id));conversation={};last_failure="conversation-stand-failed";return
		conversation_phase="stand"
	else: stop_work_visual()

func end_conversation() -> void:
	dialogue_label.visible=false
	body.end_conversation()
	if is_instance_valid(npc): npc.end_conversation()
	restoring_conversation_id=int(conversation.Id)
	conversation={}
	conversation_phase=""
	if saved_phase=="standing" and not command.is_empty() and not body.is_seated:
		release_conversation()
		if cancelled: bridge.Cancelled(int(command.execution))
		else: bridge.Complete(int(command.execution))
		command={};phase="idle";return
	if not command.is_empty():
		restoring_action=saved_phase in ["working","reaching","sitting"]
		navigation.target_position=saved_target
		phase="approaching"
		elapsed=0
		approach_progress_position=body.global_position
		approach_stalled=0
		if not restoring_action: release_conversation()
	else:
		phase="idle"
		release_conversation()

func release_conversation() -> void:
	if restoring_conversation_id!=0:
		bridge.CompleteConversation(restoring_conversation_id)
		restoring_conversation_id=0

func execute_conversation(delta: float,rate: float) -> void:
	conversation_elapsed+=delta
	if conversation_phase=="approach" and conversation_elapsed>float(settings.approach_timeout_seconds):
		last_failure="conversation-approach-timeout"
		bridge.InterruptConversation(int(conversation.Id));end_conversation();return
	if conversation_phase in ["finish_posture","stand"]:
		var posture_result: Dictionary=body.advance_native_posture(delta)
		if posture_result.is_empty(): return
		if not posture_result.ok:
			last_failure="conversation-posture:"+str(posture_result.reason)
			bridge.InterruptConversation(int(conversation.Id));saved_phase="";end_conversation()
			if not command.is_empty(): fail("conversation-posture-failed")
			return
		if body.is_seated:
			if not posture("standing"):
				bridge.InterruptConversation(int(conversation.Id));end_conversation();last_failure="conversation-stand-failed";return
			conversation_phase="stand";return
		conversation_phase="approach"
		stop_work_visual()
	if conversation_phase=="approach":
		var offset: Vector3=npc.global_position-body.global_position
		offset.y=0
		var distance:=offset.length()
		var desired:=float(conversation_spec.distance_m)
		if absf(distance-desired)>float(conversation_spec.tolerance_m):
			var toward:=offset.normalized() if distance>0.01 else Vector3.RIGHT
			var direction:=toward if distance>desired else -toward
			navigation.target_position=npc.global_position-toward*desired
			var waypoint:=navigation.get_next_path_position()-body.global_position
			waypoint.y=0
			if waypoint.length()<0.05: waypoint=direction
			move(waypoint.normalized(),delta,rate)
			return
		move(Vector3.ZERO,delta,rate)
		body.face_world_direction(offset.normalized())
		npc.face_world_direction(-offset.normalized())
		if absf(angle_difference(body.visual.rotation.y,body.target_yaw))>deg_to_rad(float(settings.facing_tolerance_degrees)): return
		if absf(angle_difference(npc.visual.rotation.y,npc.target_yaw))>deg_to_rad(float(settings.facing_tolerance_degrees)): return
		if bridge.AlignConversation(int(conversation.Id)):
			conversation_phase="speaking"
			body.start_mutual_conversation(npc)
	if conversation_phase=="speaking":
		var view: Dictionary=bridge.Conversation()
		if view.is_empty() or str(view.Phase) in ["finished","interrupted"]: end_conversation();return
		if body.global_position.distance_to(npc.global_position)>float(conversation_spec.distance_m)+float(conversation_spec.tolerance_m)*2:
			bridge.InterruptConversation(int(conversation.Id));end_conversation();last_failure="conversation-distance-lost";return
		if shown_line!=int(view.LineIndex):
			shown_line=int(view.LineIndex)
			body.set_conversation_speaking(str(view.Line.Speaker)==str(definition.viewer))
			npc.set_conversation_speaking(str(view.Line.Speaker)==str(conversation_spec.target))
		dialogue_label.text=str(view.Line.Label)+"："+str(view.Line.Text)+"\n[继续]"
		dialogue_label.visible=true

func posture(to: String) -> bool:
	var spec := {"to":to,"seat_slot":"sit","seat_surface_slot":"seat_surface","left_foot_slot":"foot_left","right_foot_slot":"foot_right","exit_slot":"exit","timeout_s":8.0,"position_tolerance_m":0.04,"foot_tolerance_m":0.04}
	spec["pelvis_forward_m"]=float(command.declaration.motor.get("pelvis_forward_m",0.0))
	spec["foot_forward_m"]=float(command.declaration.motor.get("foot_forward_m",0.0))
	spec["ignore_target_collision"]=bool(command.declaration.motor.get("ignore_target_collision",false))
	if not is_instance_valid(target_object):
		last_failure="seat-object-unavailable"
		return false
	if not body.begin_native_posture(target_object,spec):
		last_failure=str(body.get_meta("native_posture_start_failure","seat-posture-failed"))
		return false
	body.play_action("sit_down" if to=="sitting" else "stand_up")
	return true

func execute(delta: float,rate: float) -> void:
	elapsed+=delta
	if phase=="seat_recovery_wait":
		recovery_wait+=delta
		if recovery_wait>=float(settings.seat_recovery_retry_seconds):
			recovery_wait=0.0
			if body.native_seat_driver.retry_recovery(): phase="seat_recovery"
		return
	if phase=="seat_recovery":
		var recovery: Dictionary=body.advance_native_posture(delta)
		if recovery.is_empty(): return
		if bool(recovery.measurement.get("recovered_to_standing",false)):
			bridge.Cancelled(int(command.execution));command={};phase="idle"
		else:
			last_failure=str(recovery.reason)
			phase="seat_recovery_wait"
		return
	if cancelled and phase=="sitting" and not cancelling_posture:
		cancelling_posture=true
		body.cancel_native_posture("cancelled")
	if cancelled and phase not in ["sitting","standing"]:
		if body.is_seated:
			if not posture("standing"): fail("cannot-stand-for-cancellation"); return
			phase="standing"
		else:
			stop_work_visual()
			bridge.Cancelled(int(command.execution)); command={};phase="idle"
		return
	if phase in ["sitting","standing"]:
		var result: Dictionary = body.advance_native_posture(delta)
		if result.is_empty(): return
		if cancelling_posture and bool(result.measurement.get("recovered_to_standing",false)):
			bridge.Cancelled(int(command.execution)); command={};phase="idle";return
		if not result.ok:
			if body.native_seat_driver != null and is_instance_valid(body.native_seat_driver.modifier):
				last_failure=str(result.reason)
				bridge.Cancel()
				cancelled=true
				recovery_wait=0.0
				phase="seat_recovery_wait"
			else: fail(str(result.reason))
			return
		if phase=="standing":
			if cancelled: bridge.Cancelled(int(command.execution))
			else: bridge.Complete(int(command.execution))
			command={};phase="idle";return
		if restoring_action:
			release_conversation()
			if not resume_work_started: bridge.BeginWork(int(command.execution))
		else: bridge.BeginWork(int(command.execution))
		restoring_action=false;props.begin_work(command.declaration.motor);phase="working";return
	if phase=="reaching":
		var reach_status: String = reach.step(delta)
		if reach_status=="reaching": return
		if reach_status!="contact": fail("hand-contact-"+reach_status);return
		if restoring_action:
			release_conversation()
			if not resume_work_started: bridge.BeginWork(int(command.execution))
		else: bridge.BeginWork(int(command.execution))
		restoring_action=false;phase="working";return
	if phase=="approaching":
		if not stair_path.is_empty():
			while stair_path_index<stair_path.size() and body.global_position.distance_to(stair_path[stair_path_index])<0.18: stair_path_index+=1
			if stair_path_index<stair_path.size():
				if elapsed>float(settings.approach_timeout_seconds): fail("cross-floor-timeout");return
				if body.global_position.distance_to(approach_progress_position)>=float(settings.approach_progress_m):
					approach_progress_position=body.global_position;approach_stalled=0
				else:
					approach_stalled+=delta
					if approach_stalled>float(settings.approach_stall_timeout_seconds): fail("cross-floor-blocked");return
				var target: Vector3=stair_path[stair_path_index]
				if not streaming.CanTraversePath(PackedVector3Array([body.global_position,target])): fail("cross-floor-portal-closed");return
				var direction: Vector3=target-body.global_position
				direction.y=0
				move(direction.normalized(),delta,rate,direction.length())
				return
			stair_path.clear()
			if absf(navigation.target_position.y-body.global_position.y)>0.3: fail("cross-floor-height-mismatch");return
		if command.declaration.motor.has("observe_actor"):
			var observed:=observed_target(command.declaration.motor)
			if not is_instance_valid(observed): fail("observation-target-unavailable");return
			var away:=body.global_position-observed.global_position;away.y=0
			if away.length_squared()<0.0001: away=Vector3.RIGHT
			navigation.target_position=observed.global_position+away.normalized()*float(command.declaration.motor.get("observation_approach_m",0.8))
		if elapsed>float(settings.approach_timeout_seconds): fail("approach-timeout");return
		var offset: Vector3 = navigation.target_position-body.global_position
		offset.y=0
		if offset.length()>float(settings.arrival_distance_m):
			if body.global_position.distance_to(approach_progress_position)>=float(settings.approach_progress_m):
				approach_progress_position=body.global_position
				approach_stalled=0.0
			else:
				approach_stalled+=delta
				if approach_stalled>float(settings.approach_stall_timeout_seconds): fail("approach-blocked");return
			var waypoint := navigation.get_next_path_position()-body.global_position
			waypoint.y=0
			var final_step: bool = navigation.is_navigation_finished() and offset.length()<=float(settings.final_approach_distance_m)
			if final_step: waypoint=offset
			move(waypoint.normalized(),delta,rate,offset.length() if final_step else INF)
			return
		move(Vector3.ZERO,delta,rate)
		if command.declaration.motor.get("target_current",false):
			if not bridge.BeginWork(int(command.execution)): fail("work-start-rejected"); return
			phase="working"; return
		phase="facing"
		facing_elapsed=0
		facing_total=0
	if phase=="facing":
		facing_total+=delta
		if facing_total>float(settings.facing_timeout_seconds): fail("facing-timeout");return
		var facing: Vector3
		if command.declaration.motor.has("observe_actor"):
			var observed:=observed_target(command.declaration.motor)
			if not is_instance_valid(observed): fail("observation-target-unavailable");return
			facing=observed.global_position-body.global_position
		elif command.declaration.motor.has("facing_slot"):
			facing=streaming.OperationSlot(str(command.declaration.motor.object_id),str(command.declaration.motor.facing_slot))-body.global_position
		else:
			var p: Array = command.declaration.motor.facing
			facing=Vector3(p[0],p[1],p[2])-body.global_position
		facing.y=0
		body.face_world_direction(facing.normalized())
		if absf(angle_difference(body.visual.rotation.y,body.target_yaw))>deg_to_rad(float(settings.facing_tolerance_degrees)):
			facing_elapsed=0
			return
		facing_elapsed+=delta
		if facing_elapsed<0.35: return
		if not bool(command.declaration.motor.get("observe_after_door",false)) and not observation_valid(command.declaration.motor): fail("observation-not-visible");return
		if command.declaration.motor.posture=="sitting":
			target_object=find_object(self,str(command.declaration.motor.object_id))
			if not posture("sitting"): fail("cannot-sit");return
			phase="sitting"
		elif command.declaration.motor.has("hand_contact"):
			var raw: Array = command.declaration.motor.hand_contact
			reach=preload("res://scenes/components/characters/ReachTarget3D.gd").new()
			var transition: float = maxf(0.05, float(command.declaration.motor.get("reach_transition_seconds", 0.8)))
			if not reach.begin(body,Vector3(raw[0],raw[1],raw[2]),float(command.declaration.motor.get("hand_tolerance_m",0.08)),transition + 2.2,transition): fail("hand-contact-unavailable");return
			body.play_action(str(command.declaration.motor.animation))
			phase="reaching";return
		else:
			if restoring_action:
				release_conversation()
				if not resume_work_started: bridge.BeginWork(int(command.execution))
			else: bridge.BeginWork(int(command.execution))
			restoring_action=false;props.begin_work(command.declaration.motor);phase="working"
	if phase=="working": body.play_action(str(command.declaration.motor.animation))
	if phase=="working" and bridge.WorkElapsed(int(command.execution)):
		if not bool(command.declaration.motor.get("observe_after_door",false)) and not observation_valid(command.declaration.motor): fail("observation-target-lost");return
		if command.declaration.motor.has("door_opening"):
			if not bridge.PhysicalActionPermitted(int(command.execution)): fail("door-objective-rejected"); return
			if command.declaration.motor.has("door_retreat_target"):
				var retreat: Array=command.declaration.motor.door_retreat_target
				navigation.target_position=Vector3(retreat[0],retreat[1],retreat[2])
				stop_work_visual();phase="door_retreat";elapsed=0;approach_stalled=0;approach_progress_position=body.global_position
				return
			if not streaming.BeginOpenDoor(str(command.declaration.motor.door_opening)): fail("door-motion-rejected"); return
			phase="door_opening"; return
		if body.is_seated:
			if not posture("standing"): fail("cannot-stand");return
			phase="standing"
		else:
			stop_work_visual()
			bridge.Complete(int(command.execution));command={};phase="idle"
	if phase=="door_opening" and streaming.DoorOpen(str(command.declaration.motor.door_opening)):
		if bool(command.declaration.motor.get("observe_after_door",false)) and not observation_valid(command.declaration.motor): fail("observation-not-visible");return
		stop_work_visual()
		if command.declaration.motor.has("cross_target"):
			var p: Array=command.declaration.motor.cross_target
			navigation.target_position=Vector3(p[0],p[1],p[2])
			phase="crossing";elapsed=0;approach_stalled=0;approach_progress_position=body.global_position
		else:
			bridge.Complete(int(command.execution));command={};phase="idle"
	if phase in ["crossing","door_retreat"]:
		if elapsed>float(settings.approach_timeout_seconds): fail("door-crossing-timeout"); return
		var offset: Vector3=navigation.target_position-body.global_position
		offset.y=0
		if offset.length()<=float(settings.arrival_distance_m):
			move(Vector3.ZERO,delta,rate)
			if phase=="door_retreat":
				if not streaming.BeginOpenDoor(str(command.declaration.motor.door_opening)): fail("door-motion-rejected"); return
				phase="door_opening";elapsed=0;return
			bridge.Complete(int(command.execution));command={};phase="idle";return
		if body.global_position.distance_to(approach_progress_position)>=float(settings.approach_progress_m):
			approach_progress_position=body.global_position;approach_stalled=0
		else:
			approach_stalled+=delta
			if approach_stalled>float(settings.approach_stall_timeout_seconds): fail("door-crossing-blocked");return
		var waypoint:=navigation.get_next_path_position()-body.global_position
		waypoint.y=0
		if phase=="crossing": waypoint=offset
		var final_step: bool = navigation.is_navigation_finished() and offset.length()<=float(settings.final_approach_distance_m)
		if final_step: waypoint=offset
		move(waypoint.normalized(),delta,rate,offset.length() if final_step else INF)

func stop_work_visual() -> void:
	if reach != null: reach.stop();reach=null
	props.end_work()
	if not body.is_seated:
		body.is_transitioning=false
		body.play_action("idle")

func fail(reason: String) -> void:
	release_conversation()
	restoring_action=false
	if phase in ["sitting","standing"]:
		if reach != null: reach.stop();reach=null
		props.end_work()
	else:
		stop_work_visual()
	body.velocity=Vector3.ZERO
	last_failure=reason+(":"+last_failure if not last_failure.is_empty() else "")
	bridge.Fail(int(command.execution),reason)
	command={};phase="idle"

func observation() -> Dictionary:
	if is_instance_valid(bridge): state=bridge.Snapshot()
	var result := {"ready":ready_for_play,"phase":phase,"last_failure":last_failure,"state":state}
	var physical_facts: Array = []
	if is_instance_valid(bridge):
		for binding: Dictionary in settings.get("diagnostic_facts",[]):
			physical_facts.append({"ref":binding,"fact":bridge.PhysicalFact(str(binding.object),str(binding.facet),str(binding.field))})
	result["diagnostic_physical_facts"]=physical_facts
	if is_instance_valid(props): result["props"]=props.observation()
	if is_instance_valid(body): result["seat_surface_audit"]=body.get_meta("seat_surface_audit",{})
	if is_instance_valid(body): result["seat_penetration"]=body.get_meta("seat_penetration",{})
	if is_instance_valid(body): result["seat_body_overlap"]=body.get_meta("seat_body_overlap",{})
	if is_instance_valid(body) and body.native_seat_driver!=null: result["seat_measurement"]=body.native_seat_driver.last_measurement
	if reach != null: result["hand_contact"]={"distance_m":reach.last_error,"stable_seconds":reach.contact_seconds,"surface_vertex_distance_m":reach.surface_error,"surface_vertex_samples":reach.surface_samples}
	if is_instance_valid(body): result["position"]=[body.position.x,body.position.y,body.position.z]
	if is_instance_valid(npc):
		result["npc"]={"id":definition.actors[1].id,"position":[npc.global_position.x,npc.global_position.y,npc.global_position.z],
			"action":npc_command.get("action",""),"execution":npc_command.get("execution",0),"speed_mps":npc.velocity.length()}
	var actor_views: Array=[]
	for actor_id in autonomous_actors:
		var executor=autonomous_actors[actor_id]
		var actor_body: CharacterBody3D=executor.body
		actor_views.append({"id":actor_id,"position":[actor_body.global_position.x,actor_body.global_position.y,actor_body.global_position.z],
			"action":executor.command.get("action",""),"phase":executor.phase,"speed_mps":actor_body.velocity.length(),"execution":executor.command.get("execution",0),"target":[executor.navigation.target_position.x,executor.navigation.target_position.y,executor.navigation.target_position.z],"progress":bridge.WorkProgress(actor_id),"cognition":bridge.ActorDiagnostics(actor_id)})
	result["autonomous_actors"]=actor_views
	result["conversation_phase"]=conversation_phase
	if ready_for_play: result["interface"]=interface_observation()
	if is_instance_valid(streaming): result["streaming"]=streaming.Observation()
	return result

func shutdown() -> void:
	clear_manual_route()
	ready_for_play=false
	set_physics_process(false)
	if not conversation.is_empty():
		bridge.InterruptConversation(int(conversation.Id))
		bridge.CompleteConversation(int(conversation.Id))
	release_conversation()
	bridge.Cancel()
	if is_instance_valid(body): body.free()
	if not command.is_empty(): bridge.Cancelled(int(command.execution))
	bridge.Step(0.0,true,false)
	for child in get_children():
		if child!=streaming: child.free()
	if is_instance_valid(streaming): streaming.free()

func physical_observation() -> Dictionary:
	if not is_instance_valid(body): return {}
	return {"time_us":Time.get_ticks_usec(),"physics_frame":Engine.get_physics_frames(),
		"position":[body.global_position.x,body.global_position.y,body.global_position.z],
		"phase":phase,"on_floor":body.is_on_floor(),"speed_mps":body.velocity.length(),
		"yaw":body.visual.rotation.y,"target_yaw":body.target_yaw,"seated":body.is_seated,
		"animation":body.current_action,"game_seconds":state.get("game_seconds",0)}

func submit_action_order(order: Array[String]) -> void:
	var error: String=bridge.CheckActionOrder(JSON.stringify(order))
	order_panel.message.text=error
	if not error.is_empty() or not order_running.is_empty() or not order_queue.is_empty(): return
	var spec: Dictionary=definition.action_order_panel
	var ref: Dictionary=spec.step_ref
	var done: Dictionary=bridge.PhysicalFact(str(ref.object),str(ref.facet),str(ref.field))
	for index in range(int(done.get("Value",0)),order.size()): order_queue.append(order[index])
	order_queue.append(str(spec.result_action))
	order_panel.hide()

func update_story_ui(update: Dictionary,delta: float) -> void:
	if is_instance_valid(native_book_ui): native_book_ui.on_update(update)
	state["resting"]=bool(update.get("resting",false))
	for receipt: Dictionary in update.get("receipts",[]):
		if str(receipt.Actor)!=str(definition.viewer): continue
		if str(receipt.Action)==order_running:
			order_running=""
			if not bool(receipt.Committed):
				order_queue.clear();order_panel.message.text="排演已中断，请重新提交。";order_panel.show()
		if bool(receipt.Committed):
			for action: Dictionary in actions:
				if str(action.id)!=str(receipt.Action): continue
				if action.has("completion_text") or action.has("completion_lines"):
					deduction_label.text=bridge.CompletionText(str(action.id));deduction_label.show()
				if bool(action.get("chapter_save",false)): chapter_save_pending=true
	if order_running.is_empty() and not order_queue.is_empty() and command.is_empty():
		var error: String=bridge.Submit(order_queue[0])
		if error.is_empty(): order_running=order_queue.pop_front()
		elif error!="Actor is occupied.":
			order_queue.clear();order_panel.message.text=error;order_panel.show()
	if chapter_save_pending and not bool(update.get("resting",false)):
		var preferences := get_node_or_null("/root/GameSettings")
		if preferences == null or str(preferences.get_value("gameplay","autosave_policy")) == "chapter": save_game()
		chapter_save_pending=false
	hud_elapsed+=delta
	if hud_elapsed<0.2: return
	hud_elapsed=0
	var minutes:=int(float(update.game_seconds)/60)
	var text:="第%d日  %02d:%02d" % [minutes/1440+1,(minutes/60)%24,minutes%60]
	if definition.has("scenario_label"): text=str(definition.scenario_label)+"\n"+text
	if bool(update.get("resting",false)): text+="  休息中…"
	for objective: Dictionary in definition.get("objectives",[]):
		if bridge.KnowsCondition(JSON.stringify(objective.when)):
			text+="\n"+str(objective.text);break
	story_label.text=text
	if is_instance_valid(order_button):
		var panel_spec: Dictionary=definition.action_order_panel
		order_button.visible=not panel_spec.has("visible_when") or bridge.KnowsCondition(JSON.stringify(panel_spec.visible_when))
		if not order_button.visible: order_panel.hide()
	for actor_id in work_bars:
		var actor_body: Node3D=body if actor_id==str(definition.viewer) else autonomous_actors[actor_id].body
		var progress: Dictionary=bridge.WorkProgress(actor_id)
		var bar: ProgressBar=work_bars[actor_id]
		var point:=actor_body.global_position+Vector3(0,2.05,0)
		bar.visible=not progress.is_empty() and actor_body.visible and streaming.RoomVisible(streaming.RoomAt(actor_body.global_position)) and not camera.is_position_behind(point) and actor_body.global_position.distance_to(body.global_position)<16
		if bar.visible:
			bar.position=camera.unproject_position(point)-Vector2(40,0);bar.value=float(progress.progress)
			if autonomous_actors.has(actor_id):
				var worker=autonomous_actors[actor_id]
				var minimum_real:=float(worker.command.get("declaration",{}).get("minimum_real_work_seconds",0.0))
				if minimum_real>0 and worker.work_real_started_us>0: bar.value=minf(bar.value,float(Time.get_ticks_usec()-worker.work_real_started_us)/1000000.0/minimum_real)

func update_actor_carries() -> void:
	for victim in carried_actors.keys():
		var link: Dictionary=carried_actors[victim]
		var carrier=autonomous_actors[link.carrier]
		if not carrier.command.is_empty() and int(carrier.command.execution)==int(link.execution): continue
		var target=autonomous_actors[victim]
		target.body.global_position=carrier.body.global_position
		target.body.collision_layer=int(link.layer);target.body.collision_mask=int(link.mask)
		target.body.visual.rotation.z=0;target.body.play_action("idle")
		carried_actors.erase(victim)
	for carrier_id in autonomous_actors:
		var carrier=autonomous_actors[carrier_id]
		if carrier.command.is_empty(): continue
		var motor: Dictionary=carrier.command.declaration.get("motor",{})
		var victim:=str(motor.get("carry_actor",""))
		if victim.is_empty() or carried_actors.has(victim) or not autonomous_actors.has(victim): continue
		var target=autonomous_actors[victim]
		if carrier.body.global_position.distance_to(target.body.global_position)>float(motor.get("carry_distance_m",1.8)):
			bridge.CancelActor(carrier_id);continue
		bridge.CancelActor(victim)
		if not target.command.is_empty():
			target.cancelled=true;target.finish()
		target.set_idle_suspended(false)
		carried_actors[victim]={"carrier":carrier_id,"execution":carrier.command.execution,"layer":target.body.collision_layer,"mask":target.body.collision_mask}
		target.body.collision_layer=0;target.body.collision_mask=0;target.body.velocity=Vector3.ZERO
		target.body.visual.rotation.z=PI/2;target.body.play_action("lie_idle")

# Both the HTML view and the native UI submit to these same player methods.
func clear_manual_route() -> void:
	manual_path.clear()
	manual_target=null
	for room: String in manual_rooms: streaming.ReleaseActorRoom(room)
	manual_rooms.clear()

func start_manual_route(target: Vector3) -> String:
	clear_manual_route()
	var map:=navigation.get_navigation_map()
	var nearest:=NavigationServer3D.map_get_closest_point(map,target)
	if nearest.distance_to(target)>0.6: return "目标不在可行走区域。"
	var path:=preload("res://tools/building/BuildRoutes.gd").plan(map,streaming.StairBuildings(),body.global_position,nearest)
	if path.is_empty() or not streaming.CanTraversePath(path):
		last_failure="通路尚未打开或目标不可达。"
		return last_failure
	manual_path=path
	for point: Vector3 in path:
		var room: String=streaming.RoomAt(point)
		if not room.is_empty() and room not in manual_rooms:
			manual_rooms.append(room);streaming.HoldActorRoom(room)
	manual_progress=body.global_position;manual_stalled=0
	last_failure=""
	return ""

func step_manual_route(delta: float) -> void:
	for room: String in manual_rooms:
		if not streaming.RoomActive(room): move(Vector3.ZERO,delta,1);return
	while not manual_path.is_empty() and body.global_position.distance_to(manual_path[0])<0.2: manual_path.remove_at(0)
	if manual_path.is_empty(): clear_manual_route();move(Vector3.ZERO,delta,1);return
	if not streaming.CanTraversePath(PackedVector3Array([body.global_position,manual_path[0]])):
		last_failure="行走途中通路关闭。";clear_manual_route();return
	var offset:=manual_path[0]-body.global_position;offset.y=0
	move(offset.normalized(),delta,1,offset.length())
	if body.global_position.distance_to(manual_progress)>0.08:
		manual_progress=body.global_position;manual_stalled=0
	else: manual_stalled+=delta
	if manual_stalled>float(settings.approach_stall_timeout_seconds):
		last_failure="行走受阻。";clear_manual_route()

func interface_observation() -> Dictionary:
	var choices: Array=[]
	for action: Dictionary in actions:
		if action_in_context(action): choices.append({"kind":"action","id":action.id,"label":action.label,"target":action_context_identity(action)})
	for item: Dictionary in definition.get("conversations",[]):
		if conversation_in_context(item): choices.append({"kind":"conversation","id":item.id,"label":item.label,"target":"actor:"+str(item.target)})
	var labels: Dictionary={}
	for item: Dictionary in definition.get("conversations",[]):
		for line: Dictionary in item.get("lines",[]):
			if line.has("speaker") and line.has("label"): labels[str(line.speaker)]=str(line.label)
	var action_labels: Dictionary={}
	for actor: Dictionary in definition.actors:
		for action: Dictionary in actor.actions: action_labels[str(action.id)]=str(action.get("label",action.id))
	var order: Dictionary={}
	if is_instance_valid(order_button) and order_button.visible: order=definition.action_order_panel
	return {"choices":choices,"labels":labels,"action_labels":action_labels,"order":order,"order_message":order_panel.message.text if is_instance_valid(order_panel) else "",
		"objective":story_label.text,"feedback":deduction_label.text,"save_message":checkpoint_message.text,
		"paused":player_paused,"player_action":command.get("action",""),"player_execution":command.get("execution",0),"player_progress":bridge.WorkProgress(str(definition.viewer)),
		"map":{"rooms":presentation_map.rooms,"room_elevations":presentation_map.room_elevations,"openings":presentation_map.openings,"floor_connections":presentation_map.get("floor_connections",[]),"object_anchors":presentation_map.get("object_anchors",{})},
		"doors":streaming.LogicalDoorStates(),"manual_route":manual_route_points(),"content_path":str(get_parent().character_session_path)}

func interface_command(request: Dictionary) -> String:
	if not ready_for_play: return "会话尚未就绪。"
	var kind:=str(request.get("command",""))
	if kind=="pause":
		player_paused=bool(request.get("paused",false));return ""
	if kind=="action":
		for index in actions.size():
			if str(actions[index].id)==str(request.get("action_id","")) and action_in_context(actions[index]):
				select_action(index);return ""
		return "该动作当前不可用，请重新观察。"
	if kind=="conversation":
		for index in definition.get("conversations",[]).size():
			var item: Dictionary=definition.conversations[index]
			if str(item.id)==str(request.get("action_id","")) and conversation_in_context(item):
				clear_manual_route();begin_conversation(index);return ""
		return "该对话当前不可用。"
	if kind=="order":
		if not is_instance_valid(order_button) or not order_button.visible: return "当前阶段没有排演。"
		var order: Array[String]=[]
		for value in request.get("order",[]): order.append(str(value))
		submit_action_order(order);return str(order_panel.message.text)
	if kind=="continue":
		var view: Dictionary=bridge.Conversation()
		if view.is_empty() or str(view.Phase)!="speaking": return "当前没有可继续的对白。"
		bridge.AdvanceConversation(int(view.Id),int(view.LineIndex));return ""
	if kind=="cancel":
		clear_manual_route();pending_action=""
		order_queue.clear();order_running=""
		if not conversation.is_empty(): bridge.InterruptConversation(int(conversation.Id));end_conversation()
		bridge.Cancel();return ""
	if kind=="save": save_game();return ""
	if kind=="walk":
		var point: Array=request.get("position",[])
		if point.size()!=3: return "需要三维目标坐标。"
		var target:=Vector3(float(point[0]),float(point[1]),float(point[2]))
		if not target.is_finite(): return "目标坐标无效。"
		if bool(state.get("resting",false)) or not order_running.is_empty() or not order_queue.is_empty(): return "请等待当前休息或排演结束。"
		if not conversation.is_empty(): bridge.InterruptConversation(int(conversation.Id));end_conversation()
		if not command.is_empty():
			clear_manual_route();manual_target=target;bridge.Cancel();return ""
		return start_manual_route(target)
	return "未知玩家操作。"

func manual_route_points() -> Array:
	var result: Array=[]
	for point: Vector3 in manual_path: result.append([point.x,point.y,point.z])
	return result
