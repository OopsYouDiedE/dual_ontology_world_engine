extends Node
signal shutdown_completed
## 单一窗口：把输入交给会话端点，把输出交给视图或 GUI，不持有剧情与章节规则。

const ENGINE_PATH := "res://scenes/main/GameEngine.cs"
const PRESENTATION := preload("res://scenes/components/shared/HostPresentation.gd")
const UI_SCENE := preload("res://scenes/UIpanel/CasebookUI/UISystem.tscn")
const TITLE_SCENE := preload("res://scenes/UIpanel/TitleUI.tscn")
const FIELD_SCRIPT := preload("res://scenes/components/world/WorldView.gd")
const FIELD_EXECUTOR := preload("res://scenes/components/world/DvFieldExecutor.gd")
const CHARACTER_CONTROLLER := preload("res://scenes/components/characters/CharacterController3D.gd")
const VIEW_TRACE := preload("res://scenes/components/rendering/PlayerViewTrace.gd")
const DIAGNOSTICS_PATH := "res://tools/diagnostics/RuntimeDiagnostics.gd"
const HOST_FLOW := preload("res://scenes/components/shared/HostFlow.gd")
const DEV_CONSOLE_PATH := "res://tools/diagnostics/DevConsole.gd"
const CASEBOOK_DATA := preload("res://scenes/UIpanel/CasebookUI/CasebookData.gd")
const CAMPUS_MAP_VIEW := preload("res://scenes/components/world/CampusMapView.tscn")
const Sound := preload("res://scenes/components/shared/Sound.gd")
const NOTICE_THEME := preload("res://assets/ui/OverlayTheme.tres")

@export_enum("player", "ai") var controller := "player"
@export var campaign_options: Dictionary = {}
@export var viewer := ""
@export_range(0.25, 8.0, 0.25) var playback_speed := 1.0
## An isolated replay viewport supplies a read-only session and owns its lifetime.
var session_override: Node
var replay_mode := false
var flow := HOST_FLOW.new()
var dev_console: CanvasLayer
var diagnostics: Node
var audit_enabled := false
var navigation_sampling := false
var _preview_tokens: Dictionary = {}
var _npc_tick_at := 0
var _simulation_pulse_second := -1
var field_executor: Node
var character_controller3d: Node
var view_trace: Node
var _engine: Node
var ui: Node
var field: Node2D
var title_layer: CanvasLayer
var _queue: Array = []
var _dv_intent: Dictionary = {}
var _dv_serial := 0
var _view_resource := ""
var _interaction_id := ""
var _interaction_site := ""
var _pending_adapter: Node2D
var _casebook: RefCounted = CASEBOOK_DATA.new()
var _bookmark_next: Dictionary = {}
var _session_generation := 0
var _book_deferred_inputs: Array[Dictionary] = []
var _case_request_serial := 0
var _case_responses: Dictionary = {}
var _world_ui_paused := false
var simulation_paused := false
var character_session_path := ""
var content_unavailable := ""
var native_character_host: Node3D

func native_character_mode() -> bool:
	return not character_session_path.is_empty() or not content_unavailable.is_empty()

func native_character_observation() -> Dictionary:
	return native_character_host.observation() if is_instance_valid(native_character_host) else {"ready":false}

func native_character_physical() -> Dictionary:
	return native_character_host.physical_observation() if is_instance_valid(native_character_host) else {}

func return_native_title(load_saved := false) -> void:
	if is_instance_valid(native_character_host):
		native_character_host.shutdown()
		native_character_host.queue_free()
		native_character_host = null
	flow.transition("started", false)
	flow.transition("opening", false)
	_home()
	if load_saved: _start_native_character.call_deferred(true)

func _start_native_character(load_saved := false) -> void:
	if not content_unavailable.is_empty():
		title_layer.get_child(0).show_start_error(content_unavailable, load_saved)
		return
	flow.transition("started",true)
	flow.transition("opening",true)
	title_layer.get_child(0).begin_opening("load" if load_saved else "start")
	var restored: Dictionary={}
	if load_saved:
		var save_port=load("res://scenes/main/CharacterSessionBridge.cs").new()
		var saved_text: String=save_port.ReadSavedGame()
		save_port.free()
		if saved_text.is_empty():
			flow.transition("started",false);flow.transition("opening",false);title_layer.get_child(0).show_start_error("尚无可读取的存档。",true);return
		var parsed: Variant=JSON.parse_string(saved_text)
		if not parsed is Dictionary:
			flow.transition("started",false);flow.transition("opening",false);title_layer.get_child(0).show_start_error("存档格式无效。",true);return
		restored=parsed
	var content_reader = load("res://scenes/main/DvPresentationContent.cs").new()
	var content_record: Dictionary = content_reader.ReadRecord(character_session_path,"data","runtime.character_session")
	content_reader = null
	var host_script := "res://scenes/components/characters/CharacterSessionHost.gd"
	if content_record.get("value",{}).has("campaign"):
		host_script = "res://scenes/components/characters/CampaignRoomHost.gd"
	native_character_host = load(host_script).new()
	add_child(native_character_host)
	native_character_host.configure(character_session_path,restored)
	while not native_character_host.ready_for_play:
		if not native_character_host.last_failure.is_empty():
			var failure: String=native_character_host.last_failure
			native_character_host.queue_free();native_character_host=null
			flow.transition("started",false)
			flow.transition("opening",false)
			title_layer.get_child(0).show_start_error(failure,true)
			return
		await get_tree().process_frame
	await title_layer.get_child(0).transition_to_field()
	title_layer.queue_free()
	title_layer = null
	flow.transition("opening",false)
var _presentation_seconds := 0.0
var _paused_nodes: Dictionary = {}
var _movement_detach_answers: Dictionary = {}
var _detached_movements: Dictionary = {}
var _detached_activities: Dictionary = {}
var _movement_schedule_pending := false
var _background_input_hold := false

var _view_component: RefCounted = preload("res://scenes/components/shared/HostViewComponent.gd").new(self)
var _action_component: RefCounted = preload("res://scenes/components/shared/HostActionComponent.gd").new(self)
var _presentation_component: RefCounted = preload("res://scenes/components/shared/HostPresentationComponent.gd").new(self)
var _archive_component: RefCounted = preload("res://scenes/components/shared/HostArchiveComponent.gd").new(self)
var _building_component: RefCounted = preload("res://scenes/components/architecture/BuildingInteractionComponent.gd").new(self)

func _timing(stage: String, details: Dictionary = {}) -> void:
	record_timing({"stage": stage, "interaction_id": _interaction_id, "details": details})

func _ready() -> void:
	if not replay_mode:
		var debug_reader = load("res://scenes/main/DvPresentationContent.cs").new()
		var debug_record: Dictionary = debug_reader.ReadRecord("res://config/coregodot-debug.dv","data","debug.coregodot")
		character_session_path = str(debug_record.get("value",{}).get("character_session",""))
		content_unavailable = str(debug_record.get("value",{}).get("content_unavailable",""))
		for argument in OS.get_cmdline_user_args():
			if argument.begins_with("--dv-content="):
				var content_path:=argument.trim_prefix("--dv-content=")
				if FileAccess.file_exists(content_path) and FileAccess.get_file_as_string(content_path).contains("data runtime.character_session"):
					character_session_path=content_path
		if native_character_mode():
			get_tree().auto_accept_quit = false
			add_to_group("lifecycle_hosts")
			_home()
			return
	if not replay_mode:
		get_tree().auto_accept_quit = false
		add_to_group("lifecycle_hosts")
		get_window().title = str(PRESENTATION.catalog().title)
	audit_enabled = not replay_mode and "--dv-audit" in OS.get_cmdline_user_args()
	if session_override != null: _engine = session_override
	else: _engine = load(ENGINE_PATH).new()
	_engine.name = "Engine"
	add_child(_engine)
	_engine.EventsReady.connect(_dv_events)
	_engine.TraceClosed.connect(func():
		flow.transition("shutdown_finished", true)
		shutdown_completed.emit()
	)
	view_trace = VIEW_TRACE.new()
	view_trace.name = "PlayerViewTrace"
	add_child(view_trace)
	view_trace.configure(self, record_visible, record_input, audit_enabled, not replay_mode)
	_install_developer_tools()
	set_diagnostics_enabled(audit_enabled)
	field_executor = FIELD_EXECUTOR.new()
	field_executor.name = "DvFieldExecutor"
	field_executor.timing_sink = record_timing
	field_executor.stage_factory = _assemble_stage
	add_child(field_executor)
	character_controller3d = CHARACTER_CONTROLLER.new()
	character_controller3d.name = "CharacterController3D"
	character_controller3d.presentation_clock = presentation_seconds
	character_controller3d.execution_completed.connect(_on_character_execution_completed)
	add_child(character_controller3d)
	_home()
	if not replay_mode and "--dv-auto-start" in OS.get_cmdline_user_args(): dispatch.call_deferred({"kind": "start"})

func _dispose_views(keep_title := false) -> void:
	_view_component._dispose_views(keep_title)

func _home() -> void:
	_view_component._home()

func _view(ev: Dictionary, begin := true) -> bool:
	return await _view_component._view(ev, begin)

func _view_error(message: String, replacing: bool) -> void:
	_view_component._view_error(message, replacing)

func _discard_adapter(adapter: Node2D) -> void:
	_view_component._discard_adapter(adapter)

func _show_casebook_warning(message: String) -> void:
	_view_component._show_casebook_warning(message)

func _show_bookmark_view_error(backup_path: String) -> void:
	_view_component._show_bookmark_view_error(backup_path)

func dispatch(input: Dictionary) -> void:
	if native_character_mode():
		if str(input.get("kind",""))=="start" and not flow.started: _start_native_character()
		elif str(input.get("kind",""))=="load" and not flow.started: _start_native_character(true)
		elif str(input.get("kind",""))=="quit": await Sound.quit_cleanly()
		return
	var input_kind := str(input.get("kind", ""))
	if input_kind == "dv_next" and not _action_component._queued_player_input.is_empty(): return
	if flow.closing:
		if input_kind == "dv_complete": _submit_session(input)
		return
	if flow.opening_failed and input_kind != "quit": return
	if flow.opening and input_kind not in ["start", "load", "quit"]: return
	if flow.view_faulted and input_kind not in ["quit", "dv_complete"]: return
	# Keep accepted physical receipts deliverable while persistence waits on work.
	if flow.bookmark_loading and input_kind != "dv_complete": return
	# Finish already prepared actions and their presentation before saving.
	# Holding only the next Prepare keeps receipt delivery and reading live.
	if flow.bookmark_requested and input_kind == "dv_next":
		if _bookmark_next.is_empty(): _bookmark_next = input.duplicate(true)
		return
	if (flow.book_reading or flow.bookmark_saving) and input_kind in ["dv_next", "view_ready", "select"]:
		if input_kind != "dv_next" or not _has_deferred_next(): _book_deferred_inputs.append(input.duplicate(true))
		return
	if flow.bookmark_saving and input_kind != "dv_complete": return
	if (input_kind == "bookmark_load" or (input_kind == "bookmark_save" and flow.book_reading)) and not _bookmark_boundary_ready():
		_timing("bookmark_boundary_rejected", {"operation": input_kind})
		if is_instance_valid(ui):
			ui.show_bookmark_result(tr("请先读完当前正文并等待现场动作结束，再保存或载入书签。"))
		return
	if input_kind == "bookmark_save":
		flow.transition("bookmark_requested", true)
		_try_bookmark_save()
		return
	if input_kind == "bookmark_load":
		flow.transition("bookmark_loading", true)
		if is_instance_valid(ui): ui.set_bookmark_loading(true)
		_load_bookmark(str(input.get("path", "")), _casebook.export_data())
		return
	if input_kind == "request_wait":
		if is_instance_valid(field) and is_instance_valid(field.spatial): field.spatial.request_wait()
		return
	if input_kind == "case_reasoning_ui":
		_action_component._case_reasoning_ui(input)
		return
	if str(input.get("kind", "")) == "spatial_select":
		if is_instance_valid(field) and is_instance_valid(field.spatial): field.spatial.select(str(input.choice_id), str(input.site))
		return
	if str(input.get("kind", "")) == "quit":
		record_input({"device": "ui_semantic", "kind": "quit"})
		if replay_mode: begin_shutdown()
		else: await Sound.quit_cleanly()
		return
	if str(input.get("kind", "")) == "start":
		if flow.started: return
		var options := _session_options()
		if options.is_empty():
			if is_instance_valid(title_layer): title_layer.get_child(0).cancel_opening()
			return
		_configure_session(options)
		flow.transition("started", true)
	elif str(input.get("kind", "")) == "load":
		if flow.started: return
		if not _configure_load_playback():
			if is_instance_valid(title_layer): title_layer.get_child(0).cancel_opening()
			return
		flow.transition("started", true)
	if input_kind in ["start", "load"]:
		flow.transition("opening", true)
		if is_instance_valid(title_layer): title_layer.get_child(0).begin_opening(input_kind)
		_timing("title_opening_requested", {"kind": input_kind})
	if input_kind in ["start", "load", "view_ready", "dv_next", "select", "dv_complete"]: flow.transition("player_options_open", false)
	_submit_session(input)

func _process(_delta: float) -> void:
	if native_character_mode(): return
	_sync_world_ui_pause()
	if not _world_ui_paused: _presentation_seconds += _delta
	if flow.bookmark_requested: _try_bookmark_save()
	if _action_component.flush_player_input(): return
	if _movement_schedule_pending and not _world_ui_paused and _dv_intent.is_empty() and not flow.draining and not flow.completion_pending and _queue.is_empty():
		_movement_schedule_pending = false
		dispatch({"kind": "dv_next"})
		return
	var can_background := flow.allows_background() and not _world_ui_paused and not _background_input_hold
	var current_second := int(Time.get_ticks_msec() / 1000)
	if can_background and not replay_mode and not flow.background_execution and _dv_intent.is_empty() and not flow.completion_pending and current_second > _simulation_pulse_second and _can_simulation_pulse():
		_simulation_pulse_second = current_second
		_submit_session({"kind": "simulation_pulse", "seconds": current_second})
		return
	if can_background and not flow.background_execution and _dv_intent.is_empty() and not flow.completion_pending and (flow.reading or _queue.is_empty()) and _can_background_tick() and Time.get_ticks_msec() >= _npc_tick_at:
		_npc_tick_at = Time.get_ticks_msec() + 250
		_submit_session({"kind": "npc_tick"})

func presentation_seconds() -> float:
	return _presentation_seconds

func hold_background_for_input(active: bool) -> void:
	_background_input_hold = active

func presentation_duration(intent: Dictionary) -> float:
	var timing: Dictionary = intent.get("presentation_timing", {})
	var rate := float(timing.get("game_minutes_per_real_second", 0.0))
	var minimum := float(timing.get("minimum_seconds", -1.0))
	if not is_finite(rate) or rate <= 0.0 or not is_finite(minimum) or minimum < 0.0: return -1.0
	return maxf(minimum, float(intent.get("execution_cost", {}).get("time", 0.0)) / rate)

func _sync_world_ui_pause() -> void:
	var active: bool = not replay_mode and not flow.closing and simulation_paused
	_world_ui_paused = active
	if is_instance_valid(_engine) and _engine.has_method("SetWorldPaused"): _engine.SetWorldPaused(active)
	for node in _paused_nodes.keys():
		if not is_instance_valid(node): _paused_nodes.erase(node)
	for node in [field, field_executor, character_controller3d]:
		if not is_instance_valid(node): continue
		if active:
			if not _paused_nodes.has(node): _paused_nodes[node] = node.process_mode
			node.process_mode = Node.PROCESS_MODE_DISABLED
		elif not active and _paused_nodes.has(node):
			node.process_mode = _paused_nodes[node]
			_paused_nodes.erase(node)

func _try_bookmark_save() -> void:
	_archive_component._try_bookmark_save()

func _bookmark_boundary_ready() -> bool:
	return _archive_component._bookmark_boundary_ready()

func _session_options() -> Dictionary:
	return _archive_component._session_options()

func _configure_load_playback() -> bool:
	return _archive_component._configure_load_playback()

func _apply_playback(speed: float, control_mode: String) -> bool:
	return _archive_component._apply_playback(speed, control_mode)

func _show_error(message: String) -> void:
	_view_component._show_error(message)

func _show_notice(caption: String, message: String, size: Vector2i, always_process := false) -> void:
	_view_component._show_notice(caption, message, size, always_process)

func _field_input_busy() -> bool:
	return _action_component._field_input_busy()

func _formal_selection_ready() -> bool:
	return _action_component._formal_selection_ready()

func _field_input(input: Dictionary) -> void:
	_action_component._field_input(input)

func _accept_execution(input: Dictionary) -> void:
	_action_component._accept_execution(input)

func _finite_position(value: Variant) -> bool:
	return _action_component._finite_position(value)

func _drain() -> void:
	await _presentation_component._drain()

func _presentation_line(result: Dictionary, text: String, field_name: String, index: int, paragraph: int) -> Dictionary:
	return _presentation_component._presentation_line(result, text, field_name, index, paragraph)

func _presentation_lines(result: Dictionary) -> Array:
	return _presentation_component._presentation_lines(result)

func _apply(snapshot: Dictionary) -> void:
	_building_component.apply_projection(snapshot)
	await _presentation_component._apply(snapshot)

func register_building_view(view: Node3D, bindings: Dictionary) -> void:
	_building_component.register_view(view, bindings)

func unregister_building_view(view: Node3D) -> void:
	_building_component.unregister_view(view)

func begin_shutdown() -> void:
	if native_character_mode():
		if flow.shutdown_finished: return
		flow.transition("closing",true)
		if is_instance_valid(native_character_host):
			native_character_host.shutdown()
			native_character_host.free()
		flow.transition("shutdown_finished",true)
		shutdown_completed.emit()
		return
	if flow.closing: return
	flow.transition("closing", true)
	flow.transition("bookmark_requested", false)
	_bookmark_next = {}
	flow.transition("reading", false)
	if is_instance_valid(diagnostics): diagnostics.set_enabled(false)
	if is_instance_valid(dev_console): dev_console.begin_shutdown()
	if is_instance_valid(ui): ui.prepare_shutdown_drain()
	if is_instance_valid(field): field.suspend_player_input()
	# Books and a failed bookmark view can pause the SceneTree. Already entered
	# movement and rollback still need physics frames after input is suspended.
	if not replay_mode: get_tree().paused = false
	_sync_world_ui_pause()
	if is_instance_valid(character_controller3d): character_controller3d.cancel_all("native-spatial-host-closing")
	# Prepared intents that never entered execution stay pending in the trace.
	# Core saves the last committed state; shutdown does not invent an attempt.
	_queue.clear()
	# Freeze new input while the existing worker, physical receipt and commit
	# still own their scene resources. Teardown starts at the drained boundary.
	while not _dv_intent.is_empty() or not _detached_movements.is_empty() or not _detached_activities.is_empty() or flow.completion_pending or (is_instance_valid(field_executor) and (field_executor._busy or not field_executor._executing_actors.is_empty())) or (is_instance_valid(_engine) and not session_is_idle()):
		await get_tree().process_frame
	if is_instance_valid(view_trace): view_trace.begin_shutdown()
	if is_instance_valid(ui): ui.begin_shutdown()
	if is_instance_valid(field_executor): field_executor.begin_shutdown()
	if is_instance_valid(_pending_adapter) and _pending_adapter.has_method("begin_shutdown"):
		_pending_adapter.begin_shutdown()
	if is_instance_valid(field): field.begin_shutdown()
	if is_instance_valid(_engine):
		_update_casebook(_casebook.export_data())
		_close_session()

func wait_for_shutdown() -> void:
	begin_shutdown()
	if flow.shutdown_finished: return
	await shutdown_completed

func _exit_tree() -> void:
	begin_shutdown()

func _dv_events(events: Array) -> void:
	var presentation_events: Array = []
	for event: Dictionary in events:
		if str(event.get("kind", "")) == "case_reasoning":
			var request_id := str(event.get("request_id", ""))
			if _case_responses.has(request_id): _case_responses[request_id] = event
		else: presentation_events.append(event)
	if not presentation_events.is_empty(): await _presentation_component._dv_events(presentation_events)

func request_case_reasoning(request: Dictionary) -> Dictionary:
	if not flow.started or flow.closing or flow.opening or flow.view_faulted:
		return {"error": "Start a healthy campaign before reasoning"}
	if str(request.get("operation", "")) not in ["info", "questions", "infer", "check", "prove", "check_goal", "check_natural", "review", "strategy", "travel_actor"]:
		return {"error": "Input proof requests only inspect the current actor's knowledge"}
	_case_request_serial += 1
	var request_id := "case-%d" % _case_request_serial
	var operation := request.duplicate(true)
	operation.kind = "case_reasoning"
	operation.request_id = request_id
	_case_responses[request_id] = {}
	_submit_session(operation)
	var expires := Time.get_ticks_msec() + 15000
	while _case_responses[request_id].is_empty() and Time.get_ticks_msec() < expires and not flow.closing:
		await get_tree().process_frame
	var response: Dictionary = _case_responses[request_id]
	_case_responses.erase(request_id)
	if response.is_empty(): return {"error": "Proof request timed out; no result certified"}
	if response.has("error"): return {"error": str(response.error)}
	return {"result": JSON.parse_string(str(response.get("result_json", "null")))}

func _has_deferred_next() -> bool:
	return _presentation_component._has_deferred_next()

func _on_book_reading_changed(active: bool) -> void:
	_presentation_component._on_book_reading_changed(active)
	_sync_world_ui_pause()

func _install_bookmark(event: Dictionary) -> void:
	await _archive_component._install_bookmark(event)

func _background_npc_intent(ev: Dictionary) -> bool:
	return _action_component._background_npc_intent(ev)

func _execute_reading_npc(ev: Dictionary) -> void:
	await _action_component._execute_reading_npc(ev)

func _dv_execute(intent: Dictionary, execution_snapshot: Dictionary) -> void:
	await _action_component._dv_execute(intent, execution_snapshot)

func _dv_complete(outcome: String, execution: Dictionary) -> void:
	_action_component._dv_complete(outcome, execution)

func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_CLOSE_REQUEST and not replay_mode and not flow.closing:
		await Sound.quit_cleanly()


func set_frame_audit(enabled: bool) -> void:
	audit_enabled = enabled
	view_trace.set_enabled(enabled)
	if is_instance_valid(diagnostics): diagnostics.set_enabled(enabled)
	set_diagnostics_enabled(enabled or (is_instance_valid(dev_console) and dev_console.performance_open()))

func console_action(choice_id: String) -> void:
	if not is_instance_valid(field) or not flow.player_options_open: return
	for option: Dictionary in field.spatial.options:
		if str(option.get("id", "")) != choice_id: continue
		var interaction: Dictionary = option.get("interaction", {})
		var kind := str(interaction.get("kind", ""))
		var site := "self" if kind == "self" else "person:" + str(interaction.get("person", "")) if kind == "person" else str(interaction.get("site", ""))
		dev_console.close()
		if kind == "doorway": field.spatial._submit(option, "", "developer-console")
		else: field.spatial.select(choice_id, site)
		return

## Recorded positions belong to the host's real bodies. They are replay data,
## never authority, observations, or inferred screen visibility.
func capture_replay_bodies() -> Array:
	return _archive_component.capture_replay_bodies()

func set_navigation_sampling(active: bool) -> void:
	navigation_sampling = active
	if is_instance_valid(field): field.navigation_sampling = active
	if is_instance_valid(field_executor): field_executor.set_navigation_sampling(active)

## Replay owns a private scene graph and never enters the live action queue.
func install_replay_snapshot(value: Dictionary, archive: Dictionary) -> bool:
	return await _archive_component.install_replay_snapshot(value, archive)

func _submit_session(input: Dictionary) -> Array:
	_sync_world_ui_pause()
	return _engine.Dispatch(input)

func observe_session() -> Dictionary:
	if native_character_mode(): return native_character_observation().get("state",{})
	return _engine.Snapshot().duplicate(true)

func session_is_idle() -> bool:
	if native_character_mode(): return not flow.opening
	return _engine.IsDispatchIdle()

func _can_background_tick() -> bool:
	return _engine.CanBackgroundTick()

func _can_simulation_pulse() -> bool:
	return _engine.has_method("CanSimulationPulse") and _engine.CanSimulationPulse()

func _session_bookmark_ready() -> bool:
	return _engine.IsBookmarkBoundaryReady()

func _configure_session(options: Dictionary) -> void:
	_engine.ConfigureSession(options)

func set_player_controlled(value: bool) -> void:
	_engine.SetPlayerControlled(value)

func _load_bookmark(path: String, data: Dictionary) -> void:
	_engine.LoadBookmark(path, data)

func _save_bookmark(data: Dictionary) -> void:
	_engine.SaveBookmark(data)

func _update_casebook(data: Dictionary) -> void:
	_engine.UpdateCasebook(data)

func read_casebook() -> Dictionary:
	return _engine.ReadCasebook()

func _close_session() -> void:
	_engine.CloseTrace()

func record_visible(value: Dictionary) -> void:
	_engine.RecordVisible(value)

func record_input(value: Dictionary) -> void:
	_engine.RecordInput(value)

func record_timing(value: Dictionary) -> void:
	_engine.RecordTiming(value)

func record_host_failure(message: String) -> void:
	_engine.RecordHostFailure(message)

func timing_clock_us() -> int:
	return _engine.TimingClockUs()

func poll_timings() -> Array:
	return _engine.PollTimings()

func set_diagnostics_enabled(enabled: bool) -> void:
	_engine.SetDiagnosticsEnabled(enabled)

func can_inspect_session() -> bool:
	return _engine.CanInspect()

func is_developer_session() -> bool:
	return _engine.IsDeveloperMode()

func developer_command(command: String) -> void:
	_engine.DevCommand(command)

func performance_summary() -> Dictionary:
	return _engine.PerformanceSummary()

func performance_snapshot() -> Dictionary:
	return _engine.PerformanceSnapshot()

func load_replay(path: String) -> void:
	_engine.LoadReplay(path)

func play_replay(speed: float = 1.0) -> void:
	_engine.ReplayPlay(speed)

func pause_replay() -> void:
	_engine.ReplayPause()

func step_replay() -> Dictionary:
	return _engine.ReplayStep()

func advance_replay(seconds: float) -> Array:
	return _engine.ReplayAdvance(seconds)

func replay_input_direction() -> Vector2:
	return _engine.InputDirection()

func project_spatial_objects(object_ids: Array) -> Dictionary:
	return _engine.ProjectSpatialObjects(object_ids)

func project_floor_surfaces(piece_ids: Array) -> Dictionary:
	return _engine.ProjectFloorSurfaces(piece_ids)

func _install_developer_tools() -> void:
	if not OS.is_debug_build():
		return
	if ResourceLoader.exists(DIAGNOSTICS_PATH):
		diagnostics = load(DIAGNOSTICS_PATH).new()
		add_child(diagnostics)
		diagnostics.configure(self, audit_enabled)
	if not replay_mode and ResourceLoader.exists(DEV_CONSOLE_PATH):
		dev_console = load(DEV_CONSOLE_PATH).new()
		add_child(dev_console)
		dev_console.configure(self)

var _architectures: Dictionary = {}
var _presentation_generation := 0

func _assemble_stage(view: Node2D) -> Node3D:
	var architecture := preload("res://scenes/components/architecture/ArchitectureController.gd").new()
	architecture.name = "ArchitectureController"
	architecture.map_id = str(view.snapshot.get("map", view.execution_binding.get("map", "")))
	architecture.execution_only = view.execution_only
	view.add_child(architecture)
	var stage := preload("res://scenes/components/characters/CharacterStage3D.gd").new()
	stage.name = "WorldStage3D"
	stage.field = view
	stage.map_port = architecture
	stage.room_unloader = architecture.unload_room_visuals
	stage.room_reloader = architecture.reload_room_visuals
	stage.room_loader = _load_architecture_room.bind(stage, architecture)
	_architectures[view.get_instance_id()] = weakref(architecture)
	var reader := preload("res://scenes/main/DvPresentationContent.cs").new()
	var spatial: Dictionary = reader.ReadPublishedData("95-configurable-building.data.dv", "space.configurable_building").value
	var profile: Dictionary = spatial.host_actor_profile
	stage.definitions = {"nominal_actor_height": float(profile.nominal_actor_height_m), "collision_radius": float(profile.collision_radius_m), "rooms": {}}
	stage.spatial_execution_profile = spatial.host_spatial_execution_profile
	var art: Dictionary = reader.ReadRecord("res://assets/data/character_art.dv", "layouts", "character_art")
	for identity in art.cast: stage.cast[str(art.cast[identity].character_id)] = art.models[identity]
	var context := preload("res://scenes/components/rendering/SharedPresentationContext3D.gd").new()
	context.name = "SharedPresentationContext3D"
	_presentation_generation += 1
	context.configure(view, architecture, stage, _session_generation, _presentation_generation)
	view.add_child(context)
	stage.camera = context.camera
	stage.observer_changed = context.update_observer
	view.add_child(stage)
	assert(architecture.get_world_3d() == stage.get_world_3d() and context.get_world_3d() == stage.get_world_3d(), "Visible scene components must share World3D")
	return stage


func _load_architecture_room(identity: String, stage: Node3D, architecture: Node3D) -> Dictionary:
	architecture.map_id = str(stage.field.adapter.map_key())
	architecture.execution_only = stage.field.execution_only
	architecture.definitions = stage.definitions
	var record: Dictionary = architecture.ensure_room(identity)
	stage.definitions = architecture.definitions.duplicate(true)
	return architecture.room_view(identity) if not record.is_empty() else {}


func _on_character_execution_completed(receipt: Dictionary) -> void:
	if int(receipt.epoch) != _session_generation: return # Superseded session.
	if _detached_movements.has(str(receipt.action_event)):
		_action_component._complete_detached_movement(receipt)
		return
	assert(str(receipt.action_event) == str(_dv_intent.action_event), "Physical receipt identity mismatch")
	assert(not receipt.get("duplicate", false), "A character execution completed twice")
	if str(receipt.receipt_kind) == "movement":
		_accept_execution(receipt.movement)
	else:
		_dv_complete(str(receipt.outcome), receipt.field)



func execute_character_movement(intent: Dictionary, state: Dictionary, serial: int) -> void:
	await character_controller3d.execute_movement(intent, _run_character_movement.bind(intent, state, serial), _session_generation)


func _run_character_movement(intent: Dictionary, state: Dictionary, serial: int) -> Dictionary:
	return await field_executor.execute(intent, field, state, serial)
