extends Node
signal shutdown_completed
## 单一窗口：把输入交给会话端点，把输出交给视图或 GUI，不持有剧情与章节规则。

const ENGINE_PATH := "res://scenes/Host/GameEngine.cs"
const PRESENTATION := preload("res://scenes/Host/HostPresentation.gd")
const UI_SCENE := preload("res://scenes/UI/UISystem.tscn")
const TITLE_SCENE := preload("res://scenes/UI/TitleUI.tscn")
const FIELD_SCRIPT := preload("res://scenes/World/Runtime/WorldView.gd")
const FIELD_EXECUTOR := preload("res://scenes/Host/DvFieldExecutor.gd")
const VIEW_TRACE := preload("res://scenes/Host/PlayerViewTrace.gd")
const DIAGNOSTICS := preload("res://scenes/Host/RuntimeDiagnostics.gd")
const HOST_FLOW := preload("res://scenes/Host/HostFlow.gd")
const DEV_CONSOLE := preload("res://scenes/Host/DevConsole.gd")
const CASEBOOK_DATA := preload("res://scenes/UI/CasebookData.gd")
const CAMPUS_MAP_VIEW := preload("res://scenes/World/Field/CampusMapView.tscn")
const Sound := preload("res://scenes/Shared/Audio/Sound.gd")
const NOTICE_THEME := preload("res://scenes/UI/Styles/OverlayTheme.tres")

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
var view_trace: Node
var engine: Node
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
var _continuous_selection: Dictionary = {}

func _timing(stage: String, details: Dictionary = {}) -> void:
	engine.RecordTiming({"stage": stage, "interaction_id": _interaction_id, "details": details})

func _ready() -> void:
	if not get_tree().get_meta("highlight_launch_consumed", false):
		for argument in OS.get_cmdline_user_args():
			if argument == "--highlights" or argument.begins_with("--highlight="):
				get_tree().set_meta("highlight_launch_consumed", true)
				get_tree().call_deferred("change_scene_to_file", "res://scenes/Highlights/Highlights.tscn")
				return
	if not replay_mode:
		get_tree().auto_accept_quit = false
		add_to_group("lifecycle_hosts")
		get_window().title = str(PRESENTATION.catalog().title)
	audit_enabled = not replay_mode and "--dv-audit" in OS.get_cmdline_user_args()
	if session_override != null: engine = session_override
	else: engine = load(ENGINE_PATH).new()
	engine.name = "Engine"
	add_child(engine)
	engine.EventsReady.connect(_dv_events)
	engine.TraceClosed.connect(func():
		flow.transition("shutdown_finished", true)
		shutdown_completed.emit()
	)
	view_trace = VIEW_TRACE.new()
	view_trace.name = "PlayerViewTrace"
	add_child(view_trace)
	view_trace.configure(self, engine.RecordVisible, engine.RecordInput, audit_enabled, not replay_mode)
	diagnostics = DIAGNOSTICS.new()
	add_child(diagnostics)
	diagnostics.configure(self, audit_enabled)
	engine.SetDiagnosticsEnabled(audit_enabled)
	if not replay_mode:
		dev_console = DEV_CONSOLE.new()
		add_child(dev_console)
		dev_console.configure(self, engine)
	field_executor = FIELD_EXECUTOR.new()
	field_executor.name = "DvFieldExecutor"
	field_executor.timing_sink = engine.RecordTiming
	add_child(field_executor)
	_home()
	if not replay_mode and "--dv-auto-start" in OS.get_cmdline_user_args(): dispatch.call_deferred({"kind": "start"})

func _dispose_views(keep_title := false) -> void:
	flow.transition("player_options_open", false)
	if is_instance_valid(field):
		if is_instance_valid(field_executor): field_executor.retain_field(field)
		field.begin_shutdown()
		remove_child(field)
		field.queue_free()
	field = null
	if is_instance_valid(ui):
		ui.begin_shutdown()
		remove_child(ui)
		ui.queue_free()
	ui = null
	if not keep_title and is_instance_valid(title_layer):
		remove_child(title_layer)
		title_layer.queue_free()
		title_layer = null

func _home() -> void:
	_dispose_views()
	if is_instance_valid(field_executor): field_executor.reset()
	title_layer = CanvasLayer.new()
	title_layer.layer = 30
	add_child(title_layer)
	var title: Control = TITLE_SCENE.instantiate()
	title.mode = "title"
	title.replay_mode = replay_mode
	title_layer.add_child(title)
	title.ui_event.connect(dispatch)
	if is_instance_valid(view_trace): move_child(view_trace, get_child_count() - 1)

func _view(ev: Dictionary, begin := true) -> bool:
	var generation := _session_generation
	var replacing := bool(ev.get("replace_session", false))
	var opening_title: Control = title_layer.get_child(0) if is_instance_valid(title_layer) else null
	_timing("view_build_begin")
	var resource: PackedScene = CAMPUS_MAP_VIEW if str(ev.resource) == "res://scenes/World/Field/CampusMapView.tscn" else (load(str(ev.resource)) as PackedScene)
	if resource == null:
		_view_error("无法加载世界视图：" + str(ev.resource), replacing)
		return false
	var initial: Dictionary = ev.snapshot
	var presentation: Dictionary = initial.get("presentation", {})
	if str(presentation.get("player_actor", "")) == "" or str(presentation.get("player_sprite", "")) == "":
		_view_error("开场快照缺少人物呈现绑定。", replacing)
		return false
	var adapter: Node2D = resource.instantiate()
	_pending_adapter = adapter
	if adapter.get("presentation") != null and initial.has("presentation"):
		adapter.presentation = initial.presentation
	if is_instance_valid(opening_title): opening_title.set_loading_stage("map")
	_timing("view_prepare_begin", {"map": initial.get("map", "")})
	var prepared := true
	if adapter.has_method("prepare_map"):
		prepared = await adapter.prepare_map(str(initial.get("map", "")), initial, get_tree())
	_timing("view_prepare_end", {"ready": prepared})
	if not prepared or flow.closing or generation != _session_generation:
		_discard_adapter(adapter)
		if not prepared and not flow.closing and generation == _session_generation:
			_view_error("无法准备世界地图：" + str(initial.get("map", "")), replacing)
		return false
	# A prepared background map cannot replace a book the player is reading.
	# An explicit bookmark install owns the replacement and closes its old UI.
	while flow.book_reading and not replacing and not flow.closing and generation == _session_generation:
		await get_tree().process_frame
	if flow.closing or generation != _session_generation:
		_discard_adapter(adapter)
		return false
	# Keep the title opaque while the actual field, actors and UI are assembled
	# and rendered behind it. In-game view replacement retains its own fade.
	if not is_instance_valid(opening_title) and is_instance_valid(ui) and ui.has_method("transition_out"):
		await ui.transition_out()
	if flow.closing or generation != _session_generation:
		_discard_adapter(adapter)
		return false
	_dispose_views(is_instance_valid(opening_title))
	if replacing: field_executor.reset()
	_view_resource = str(ev.resource)
	ui = UI_SCENE.instantiate()
	if ev.has("casebook"):
		_casebook.restore_data(ev.casebook, str(initial.get("viewer", "")))
	ui.casebook = _casebook
	if replay_mode: ui.set_replay_mode(true)
	ui.host_input_blocked = true
	ui.start_covered = not is_instance_valid(opening_title)
	ui.name = "UI"
	add_child(ui)
	ui.ui_event.connect(dispatch)
	ui.casebook_reading_changed.connect(_on_book_reading_changed)
	field = Node2D.new()
	field.set_script(FIELD_SCRIPT)
	field.name = "WorldView"
	field.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	add_child(field)
	field.ui_busy = _field_input_busy
	field.formal_selection_ready = _formal_selection_ready
	if replay_mode: field.input_direction = engine.InputDirection
	field.fader = ui
	field.field_event.connect(_field_input)
	field.hover_changed.connect(ui.show_hover)
	field.hint_changed.connect(ui.set_hint)
	field_executor.configure_visible(field, initial)
	_pending_adapter = null
	if is_instance_valid(opening_title): opening_title.set_loading_stage("cast")
	field.setup(adapter)
	if not is_instance_valid(field.player) or not is_instance_valid(field.spatial):
		_view_error("现场人物或交互未能完成装配。", replacing)
		return false
	field_executor.bind_visible(field, initial)
	ui.apply_snapshot(initial)
	if is_instance_valid(view_trace): move_child(view_trace, get_child_count() - 1)
	if not await field.prepare_presentation(initial):
		if not flow.closing and generation == _session_generation:
			_view_error("现场未能完成准备：" + str(field.presentation_error), replacing)
		return false
	if flow.closing or generation != _session_generation: return false
	if not await ui.prepare_for_reveal(): return false
	if flow.closing or generation != _session_generation: return false
	if is_instance_valid(opening_title): opening_title.set_loading_stage("frame")
	# Layout, physics registration, sight/light updates and an actual draw must
	# all precede the reveal. Fixed timers and resource completion are not ready.
	await get_tree().process_frame
	if flow.closing or generation != _session_generation: return false
	await get_tree().physics_frame
	if flow.closing or generation != _session_generation: return false
	var rendered := DisplayServer.get_name() != "headless"
	if rendered: await RenderingServer.frame_post_draw
	else: await get_tree().process_frame
	if flow.closing or generation != _session_generation: return false
	_timing("view_build_end")
	_timing("view_prepared_for_reveal", {"map": initial.get("map", ""), "actors": field.actors.size(), "frame": Engine.get_frames_drawn(), "rendered": rendered, "title_covering": is_instance_valid(opening_title)})
	if is_instance_valid(opening_title):
		_timing("title_reveal_begin")
		var revealed: bool = await opening_title.transition_to_field()
		if not revealed or flow.closing or generation != _session_generation: return false
		_timing("title_reveal_end", {"frame": Engine.get_frames_drawn()})
		remove_child(title_layer)
		title_layer.queue_free()
		title_layer = null
	elif ui.has_method("transition_in"):
		await ui.transition_in()
	# 等场景加入树并应用快照后才开始脚本；这也是可见视图的普通握手。
	if flow.closing or generation != _session_generation: return false
	flow.transition("opening", false)
	ui.set_host_input_blocked(false)
	if begin: dispatch.call_deferred({"kind": "view_ready"})
	if str(ev.get("casebook_warning", "")) != "":
		_show_casebook_warning(str(ev.casebook_warning))
	return true

func _view_error(message: String, replacing: bool) -> void:
	if replacing: return # The bookmark owner reports its backup and holds the lock.
	if is_instance_valid(title_layer):
		flow.transition("opening", false)
		flow.transition("opening_failed", true)
		_queue.clear()
		title_layer.get_child(0).show_start_error(message, false)
		_timing("title_opening_failed", {"message": message})
		push_error(message)
	else:
		_show_error(message)

func _discard_adapter(adapter: Node2D) -> void:
	if _pending_adapter == adapter: _pending_adapter = null
	if is_instance_valid(adapter):
		if adapter.has_method("begin_shutdown"): adapter.begin_shutdown()
		adapter.free()

func _show_casebook_warning(message: String) -> void:
	_show_notice(tr("案卷提示"), message, Vector2i(640, 240), true)

func _show_bookmark_view_error(backup_path: String) -> void:
	var message := tr("书签已载入宿主，但现场未能完成装配。当前现场已暂停，请关闭游戏后重新打开，并从“续读”选择载入前备份。")
	if backup_path != "": message += "\n\n" + tr("载入前备份：%s") % backup_path
	if is_instance_valid(ui): ui.show_bookmark_result(message)
	_show_notice(tr("书签载入未完成"), message, Vector2i(680, 300), true)

## 热点与跨房间进入DV；房间内坐标接近由现场消费，不进入此入口。
func dispatch(input: Dictionary) -> void:
	var input_kind := str(input.get("kind", ""))
	if flow.closing:
		if input_kind == "dv_complete": engine.Dispatch(input)
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
		engine.LoadBookmark(str(input.get("path", "")), _casebook.export_data())
		return
	if input_kind == "request_wait":
		if is_instance_valid(field) and is_instance_valid(field.spatial): field.spatial.request_wait()
		return
	if str(input.get("kind", "")) == "spatial_select":
		if is_instance_valid(field) and is_instance_valid(field.spatial): field.spatial.select(str(input.choice_id), str(input.site))
		return
	if str(input.get("kind", "")) == "quit":
		engine.RecordInput({"device": "ui_semantic", "kind": "quit"})
		if replay_mode: begin_shutdown()
		else: await Sound.quit_cleanly()
		return
	if str(input.get("kind", "")) == "start":
		if flow.started: return
		var options := _session_options()
		if options.is_empty():
			if is_instance_valid(title_layer): title_layer.get_child(0).cancel_opening()
			return
		engine.ConfigureSession(options)
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
	engine.Dispatch(input)

func _process(_delta: float) -> void:
	if flow.bookmark_requested: _try_bookmark_save()
	var can_background := flow.allows_background()
	var current_second := int(Time.get_ticks_msec() / 1000)
	if can_background and not replay_mode and engine.has_method("CanSimulationPulse") and not flow.background_execution and _dv_intent.is_empty() and not flow.completion_pending and current_second > _simulation_pulse_second and engine.CanSimulationPulse():
		_simulation_pulse_second = current_second
		engine.Dispatch({"kind": "simulation_pulse", "seconds": current_second})
		return
	if can_background and not flow.background_execution and _dv_intent.is_empty() and not flow.completion_pending and (flow.reading or _queue.is_empty()) and engine.CanBackgroundTick() and Time.get_ticks_msec() >= _npc_tick_at:
		_npc_tick_at = Time.get_ticks_msec() + 250
		engine.Dispatch({"kind": "npc_tick"})

func _try_bookmark_save() -> void:
	if not flow.bookmark_requested or not flow.started or flow.closing or flow.bookmark_loading or flow.bookmark_saving: return
	if not _bookmark_boundary_ready() or not engine.IsBookmarkBoundaryReady(): return
	flow.transition("bookmark_requested", false)
	flow.transition("bookmark_saving", true)
	# Once the safe boundary is owned, use the normal persistence continuation
	# queue. Its existing next-request deduplication also covers late callbacks.
	if not _bookmark_next.is_empty():
		if not _has_deferred_next(): _book_deferred_inputs.append(_bookmark_next)
		_bookmark_next = {}
	if is_instance_valid(ui): ui.set_bookmark_loading(true)
	engine.SaveBookmark(_casebook.export_data())

func _bookmark_boundary_ready() -> bool:
	# Session saves cannot reconstruct unread host presentation or in-flight maps.
	if flow.reading or flow.draining or not _queue.is_empty() or not _book_deferred_inputs.is_empty(): return false
	if is_instance_valid(_pending_adapter) or flow.background_execution or flow.completion_pending or not _dv_intent.is_empty(): return false
	if is_instance_valid(field_executor) and bool(field_executor.get("_busy")): return false
	if is_instance_valid(field):
		if is_instance_valid(field.adapter) and field.adapter.switching(): return false
		# Rejected continuous passages may still be physically returning to the
		# source after Complete. That unsaved host state is not a bookmark boundary.
		if not field.continuous_request().is_empty(): return false
		var jobs: Variant = field.get("_daily")
		if jobs is Dictionary and not jobs.is_empty(): return false
	return true

func _session_options() -> Dictionary:
	var speed := playback_speed
	var options := {"controller": controller, "campaign": campaign_options.duplicate(true), "audit": false}
	if viewer != "": options.viewer = viewer
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--dv-speed="):
			var value := arg.trim_prefix("--dv-speed=")
			if not value.is_valid_float():
				_show_error("Invalid --dv-speed: " + value)
				return {}
			speed = float(value)
		elif arg == "--dv-audit": options.audit = true
		elif arg == "--dv-dev": options.developer = true
		elif arg.begins_with("--dv-content="): options.content_path = arg.trim_prefix("--dv-content=")
		elif arg.begins_with("--dv-controller="): options.controller = arg.trim_prefix("--dv-controller=")
		elif arg.begins_with("--dv-viewer="): options.viewer = arg.trim_prefix("--dv-viewer=")
		elif arg.begins_with("--dv-profile="): options.campaign.profile = arg.trim_prefix("--dv-profile=")
		elif arg.begins_with("--dv-zone="): options.campaign.detective_zone = arg.trim_prefix("--dv-zone=")
		elif arg.begins_with("--dv-seed="):
			var value := arg.trim_prefix("--dv-seed=")
			if not value.is_valid_int():
				_show_error("Invalid --dv-seed: " + value)
				return {}
			options.campaign.seed = int(value)
	if str(options.controller) not in ["player", "ai"]:
		_show_error("Invalid --dv-controller: " + str(options.controller))
		return {}
	if not _apply_playback(speed, str(options.controller)): return {}
	return options

func _configure_load_playback() -> bool:
	var speed := playback_speed
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--dv-speed="):
			var value := argument.trim_prefix("--dv-speed=")
			if not value.is_valid_float():
				_show_error("Invalid --dv-speed: " + value)
				return false
			speed = float(value)
	# The saved session owns its controller; only host playback is configured.
	return _apply_playback(speed, "restored-session")

func _apply_playback(speed: float, control_mode: String) -> bool:
	if replay_mode: return true
	if not is_finite(speed) or speed < 0.25 or speed > 8.0:
		_show_error("Playback speed must be between 0.25 and 8")
		return false
	Engine.time_scale = speed
	Engine.physics_ticks_per_second = ceili(60.0 * maxf(1.0, speed))
	Engine.max_physics_steps_per_frame = ceili(8.0 * maxf(1.0, speed))
	engine.RecordInput({"device": "host_configuration", "controller": control_mode, "playback_speed": speed, "physics_ticks_per_second": Engine.physics_ticks_per_second, "max_physics_steps_per_frame": Engine.max_physics_steps_per_frame})
	return true

func _show_error(message: String) -> void:
	push_error(message)
	if is_instance_valid(title_layer) and title_layer.get_child(0).has_method("show_start_error"):
		title_layer.get_child(0).show_start_error(message, not flow.started)
		return
	_show_notice("会话错误", message, Vector2i(640, 240))

func _show_notice(caption: String, message: String, size: Vector2i, always_process := false) -> void:
	var dialog := AcceptDialog.new()
	dialog.theme = NOTICE_THEME
	dialog.title = caption
	dialog.dialog_text = message
	if always_process: dialog.process_mode = Node.PROCESS_MODE_ALWAYS
	add_child(dialog)
	dialog.confirmed.connect(dialog.queue_free)
	dialog.popup_centered(size)

func _field_input_busy() -> bool:
	# Local movement only owns geometry. Background NPC work must not freeze
	# an otherwise free player's feet; formal actions have a separate gate.
	return (is_instance_valid(dev_console) and dev_console.is_open()) or flow.blocks_input() or not is_instance_valid(ui) or ui.is_busy()

func _formal_selection_ready() -> bool:
	return flow.allows_selection() and not _field_input_busy() and _queue.is_empty() \
		and not flow.completion_pending and not flow.background_execution and _book_deferred_inputs.is_empty() and _dv_intent.is_empty()

func _field_input(input: Dictionary) -> void:
	if flow.closing: return
	match str(input.get("kind", "")):
		"timing": engine.RecordTiming(input)
		"prepare_ahead":
			if _formal_selection_ready(): engine.Dispatch(input)
		"cancel_ahead":
			_preview_tokens.clear()
			engine.Dispatch(input)
		"select":
			if not _formal_selection_ready():
				field.reject_continuous_request(str(input.get("choice_id", "")), "selection_cycle_closed")
				return
			if is_instance_valid(ui): ui.set_player_options([])
			_interaction_id = str(input.interaction_id)
			_interaction_site = str(input.site)
			_continuous_selection = input.get("continuous_walk", {}).duplicate(true)
			var token: Dictionary = _preview_tokens.get(str(input.get("choice_id", "")), {})
			if not token.is_empty(): input.preview_token = str(token.token)
			_preview_tokens.clear()
			dispatch(input)
		"interaction_hint":
			if is_instance_valid(ui): ui.show_aside(str(input.get("text", "")))
		"spatial_choices":
			_interaction_id = str(input.interaction_id)
			_timing("local_choices_ready")
			ui.spatial_choices(input)

func _accept_execution(input: Dictionary) -> void:
	if _dv_intent.is_empty(): return
	var action: Dictionary = _dv_intent.action
	var destination: Dictionary = _dv_intent.get("destination", {})
	var arrived := str(input.get("kind", "")) == "daily" and str(input.get("status", "")) == "arrived"
	arrived = arrived and int(input.get("serial", -1)) == _dv_serial
	arrived = arrived and str(input.get("who", "")) == str(_dv_intent.actor) and str(input.get("action", "")) == str(action.id)
	arrived = arrived and str(input.get("room", "")) == str(destination.get("execution_room", destination.get("room", "")))
	var travel: Dictionary = action.get("travel", {})
	if travel.has("station"):
		arrived = arrived and str(input.get("station", "")) == str(travel.station)
		if str(input.get("execution_world", "")) != "room-graph":
			arrived = arrived and input.has("arrival_error") and float(input.get("arrival_error", 5.0)) <= 4.0
	var execution := _normalize_continuous_cancel(input)
	if execution.has("continuous_cancel") and is_instance_valid(ui):
		var locale := TranslationServer.get_locale()
		var message := "通過を取り消しました。元の位置にいます。処理終了後に方向をもう一度入力してください。" if locale.begins_with("ja") else "Passage cancelled; you remain in place. Enter a direction again after processing finishes." if locale.begins_with("en") else "过门已取消，仍在原处。处理结束后请重新输入方向。"
		ui.show_aside(message)
	_dv_complete("applied" if arrived else "rejected", execution)

func _finite_position(value: Variant) -> bool:
	if not value is Array or value.size() != 2: return false
	for component in value:
		if not (component is float or component is int) or not is_finite(float(component)): return false
	return true

func _normalize_continuous_cancel(input: Dictionary) -> Dictionary:
	var result := input.duplicate(true)
	# An incoming receipt cannot self-certify its identity.
	result.erase("continuous_cancel")
	if not bool(input.get("source_cancel_candidate", false)) or _continuous_selection.is_empty(): return result
	if not is_instance_valid(field) or not is_instance_valid(field.player) or not is_instance_valid(field.adapter): return result
	var action: Dictionary = _dv_intent.get("action", {})
	var destination: Dictionary = _dv_intent.get("destination", {})
	var request := _continuous_selection
	var actor := str(_dv_intent.get("actor", ""))
	var action_id := str(action.get("id", ""))
	var event_id := str(_dv_intent.get("action_event", ""))
	var map_key := str(input.get("source_map", ""))
	var from_room := str(input.get("source_room", ""))
	var to_room := str(destination.get("execution_room", destination.get("room", "")))
	var geometry := str(input.get("source_geometry", ""))
	if str(action.get("kind", "")) != "move" or action.get("travel", {}).has("station"): return result
	if not bool(destination.get("continuous_walk", false)) or str(destination.get("continuous_request_id", "")) != str(request.get("request_id", "")): return result
	if event_id == "" or action_id == "" or actor != str(field.snapshot.get("viewer", "")) or actor != str(field._player_key): return result
	if not bool(field.snapshot.get("player_control", false)) or str(input.get("kind", "")) != "daily" or str(input.get("status", "")) != "blocked": return result
	if str(input.get("reason", "")) != "released" or not bool(input.get("continuous_walk", false)): return result
	if str(input.get("who", "")) != actor or str(input.get("action", "")) != action_id or int(input.get("serial", -1)) != _dv_serial: return result
	if str(request.get("actor", "")) != actor or str(request.get("choice_id", "")) != action_id or str(request.get("request_id", "")) == "": return result
	if str(input.get("request_id", "")) != str(request.request_id): return result
	if map_key == "" or from_room == "" or to_room == "" or from_room == to_room: return result
	if str(input.get("map", "")) != map_key or str(request.get("map", "")) != map_key or str(destination.get("map", "")) != map_key or field.adapter.map_key() != map_key: return result
	if str(input.get("room", "")) != from_room or str(input.get("from_room", "")) != from_room or str(request.get("from_room", "")) != from_room: return result
	if str(input.get("to_room", "")) != to_room or str(request.get("to_room", "")) != to_room: return result
	if geometry not in ["interior", "source-side-doorway"] or str(field.adapter.departure_room(field.player.position)) != from_room or field.adapter.switching(): return result
	var expected_geometry := "interior" if str(field.adapter.entered_room(field.player.position)) == from_room else "source-side-doorway"
	if geometry != expected_geometry: return result
	var native_request: Dictionary = field.continuous_request()
	if str(native_request.get("phase", "")) != "settling" or str(native_request.get("request_id", "")) != str(request.request_id): return result
	if int(native_request.get("serial", -1)) != _dv_serial or str(native_request.get("action", "")) != action_id or str(native_request.get("cancel_reason", "")) != "released": return result
	if not input.get("transitions", null) is Array or not input.transitions.is_empty(): return result
	if not _finite_position(input.get("source_position")) or not _finite_position(input.get("actual_position")): return result
	var source_position: Array = input.source_position
	var actual_position: Array = input.actual_position
	if float(source_position[0]) != float(actual_position[0]) or float(source_position[1]) != float(actual_position[1]): return result
	if float(actual_position[0]) != field.player.position.x or float(actual_position[1]) != field.player.position.y or field.player.velocity != Vector2.ZERO or field.player.travelling: return result
	result.continuous_cancel = {"version": "source-cancel-v1", "action_event": event_id, "request_id": str(request.request_id),
		"actor": actor, "action": action_id, "serial": _dv_serial, "map": map_key, "from_room": from_room, "to_room": to_room,
		"source_position": source_position.duplicate(), "actual_position": actual_position.duplicate(), "source_geometry": geometry,
		"transitions": [], "reason": "released", "identity_verified": true}
	return result

func _drain() -> void:
	if flow.draining or flow.closing or flow.opening_failed: return
	flow.transition("draining", true)
	var generation := _session_generation
	var slice_started := Time.get_ticks_usec()
	var slice_frame := Engine.get_process_frames()
	while not _queue.is_empty() and not flow.closing and not flow.view_faulted and generation == _session_generation:
		# Reading can accumulate many completed NPC projections. Preserve every
		# event in order, but let rendering and input run between bounded batches.
		if Engine.get_process_frames() != slice_frame:
			slice_frame = Engine.get_process_frames()
			slice_started = Time.get_ticks_usec()
		elif Time.get_ticks_usec() - slice_started >= 2000:
			_timing("presentation_queue_yield", {"remaining": _queue.size()})
			await get_tree().process_frame
			slice_frame = Engine.get_process_frames()
			slice_started = Time.get_ticks_usec()
		while flow.book_reading and not flow.closing and generation == _session_generation:
			await get_tree().process_frame
		if flow.closing or generation != _session_generation: break
		if _queue.is_empty(): break
		var ev: Dictionary = _queue.pop_front()
		# A completed reading can expose an older queued observer snapshot while
		# an off-screen actor still owns an executing body. Let that real movement
		# finish before snapshot binding can acquire/reparent the same body.
		if str(ev.get("kind", "")) in ["state", "view"]:
			while flow.background_execution and not flow.closing and generation == _session_generation:
				await get_tree().process_frame
			if flow.closing or generation != _session_generation: break
		match str(ev.get("kind", "")):
			"dev_state":
				_session_generation += 1
				_preview_tokens.clear()
				_dv_intent.clear()
				_continuous_selection.clear()
				_queue.clear()
				await _view({"resource": ev.snapshot.resource, "snapshot": ev.snapshot, "replace_session": true}, true)
			"dv_action": await _dv_execute(ev.intent, ev.get("execution_snapshot", {}))
			"dv_present":
				var result: Dictionary = ev.presentation
				# The preceding state has installed the authoritative result.
				# Release only this player's submitted action, never an NPC's.
				if is_instance_valid(field) and str(result.get("actor", "")) == str(field.snapshot.get("viewer", "")):
					field.spatial.release_submission()
				var lines := _presentation_lines(result)
				if is_instance_valid(ui): ui.prepare_presentation(result)
				if not lines.is_empty():
					flow.transition("reading", true)
					# Start the ordinary serialized Prepare while this frozen text is read.
					# Remote NPCs still execute real Godot movement and return Complete.
					ev.advance_requested = true
					dispatch.call_deferred({"kind": "dv_next", "schedule": "reading-background"})
					_timing("presentation_begin", {"execution_event": result.get("execution_event", "")})
					await ui.present({"kind": "lines", "lines": lines, "trace_id": str(result.get("execution_event", "")), "presentation_id": str(result.get("presentation_id", result.get("execution_event", "")))})
					if generation != _session_generation: break
					flow.transition("reading", false)
					_timing("reading_finished", {"execution_event": result.get("execution_event", "")})
				if not bool(ev.get("advance_requested", false)):
					dispatch.call_deferred({"kind": "dv_next"})
			"view":
				if not await _view(ev):
					if not flow.closing and generation == _session_generation: flow.transition("view_faulted", true)
					_queue.clear()
					break
			"state": await _apply(ev.snapshot)
			"error":
				flow.transition("bookmark_requested", false)
				_bookmark_next = {}
				_view_error(str(ev.message), false)
			"load_error":
				flow.transition("bookmark_requested", false)
				_bookmark_next = {}
				flow.transition("started", false)
				flow.transition("opening", false)
				if is_instance_valid(title_layer): title_layer.get_child(0).show_load_error(str(ev.message))
			_: push_error("Unknown DV host event: " + str(ev.get("kind", "")))
	flow.transition("draining", false)
	_try_bookmark_save()
	if not _queue.is_empty() and not flow.closing and not flow.view_faulted: _drain.call_deferred()

## IDs name the actual execution and emitted field; source_refs retain authored provenance.
## Emitted line ordinals are not claimed to be original declaration line numbers.
func _presentation_line(result: Dictionary, text: String, field_name: String, index: int, paragraph: int) -> Dictionary:
	var event := str(result.get("execution_event", ""))
	return {"text": text, "speaker": str(result.get("speaker", result.get("actor", ""))), "speaker_name": str(result.get("speaker_name", "")),
		"trace_id": event, "execution_event": event, "source_id": str(result.get("action_id", "")),
		"line_id": "%s:%s:%d:%d" % [event, field_name, index, paragraph], "source_field": field_name,
		"emitted_line_index": index, "paragraph_index": paragraph, "source_refs": result.get("source_refs", [])}

func _presentation_lines(result: Dictionary) -> Array:
	var lines: Array = []
	if bool(result.get("visible", false)):
		if str(result.get("surface", "")) != "": lines.append(_presentation_line(result, str(result.surface), "surface", 0, 0))
		var index := 0
		for text in result.get("lines", []):
			var paragraph := 0
			for part in str(text).split("\n", false):
				lines.append(_presentation_line(result, part, "presentation.lines", index, paragraph))
				paragraph += 1
			index += 1
	var judgment = result.get("judgment", false)
	if judgment is Dictionary:
		var line := _presentation_line(result, "本次答辩已获接受。" if str(judgment.status) == "answer_accepted" else "本次答辩未获接受。", "judgment.status", 0, 0)
		line.speaker = ""
		line.judgment_event = str(judgment.get("event_id", ""))
		lines.append(line)
	return lines

func _apply(snapshot: Dictionary) -> void:
	_timing("ui_apply_begin")
	if _view_resource != "" and str(snapshot.resource) != _view_resource:
		if not await _view({"resource": snapshot.resource, "snapshot": snapshot}, false) and not flow.closing:
			flow.transition("view_faulted", true)
			_queue.clear()
		_timing("ui_apply_end")
		return
	if is_instance_valid(ui): ui.apply_snapshot(snapshot)
	if is_instance_valid(field) and int(snapshot.get("state_version", -1)) >= int(field.snapshot.get("state_version", -1)):
		field.apply_snapshot(snapshot)
	_timing("ui_apply_end")

func begin_shutdown() -> void:
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
	# Prepared intents that never entered execution stay pending in the trace.
	# Core saves the last committed state; shutdown does not invent an attempt.
	_queue.clear()
	# Freeze new input while the existing worker, physical receipt and commit
	# still own their scene resources. Teardown starts at the drained boundary.
	while not _dv_intent.is_empty() or flow.completion_pending or (is_instance_valid(field_executor) and field_executor._busy) or (is_instance_valid(engine) and not engine.IsDispatchIdle()):
		await get_tree().process_frame
	if is_instance_valid(view_trace): view_trace.begin_shutdown()
	if is_instance_valid(ui): ui.begin_shutdown()
	if is_instance_valid(field_executor): field_executor.begin_shutdown()
	if is_instance_valid(_pending_adapter) and _pending_adapter.has_method("begin_shutdown"):
		_pending_adapter.begin_shutdown()
	if is_instance_valid(field): field.begin_shutdown()
	if is_instance_valid(engine):
		engine.UpdateCasebook(_casebook.export_data())
		engine.CloseTrace()

func wait_for_shutdown() -> void:
	begin_shutdown()
	if flow.shutdown_finished: return
	await shutdown_completed

func _exit_tree() -> void:
	begin_shutdown()

func _dv_events(events: Array) -> void:
	for source: Dictionary in events:
		if source.has("snapshot") and not replay_mode:
			view_trace.record_snapshot(source.snapshot, _casebook.export_data())
		if str(source.get("kind", "")) == "dv_present" and not replay_mode:
			view_trace.record_presentation(source.presentation)
		var ev: Dictionary = PRESENTATION.event(source)
		if str(ev.get("kind", "")) == "dv_commit":
			# Closing retains bodies and collision until the real rollback finishes.
			var settled: bool = await field_executor.settle(str(ev.action_event), str(ev.outcome), field)
			if not settled:
				flow.transition("completion_pending", false)
				if not flow.closing: flow.transition("view_faulted", true)
				engine.RecordHostFailure("Movement rollback did not complete for " + str(ev.action_event))
				return
			continue
		if flow.closing:
			if str(ev.get("kind", "")) in ["dv_present", "error"]: flow.transition("completion_pending", false)
			continue
		if str(ev.get("kind", "")) == "prepared_ahead":
			if int(ev.state_version) == int(engine.Snapshot().get("state_version", -1)):
				_preview_tokens[str(ev.choice_id)] = ev
			continue
		if str(ev.get("kind", "")) == "state":
			for choice in _preview_tokens.keys():
				if int(_preview_tokens[choice].state_version) != int(ev.snapshot.get("state_version", -1)): _preview_tokens.erase(choice)
		if str(ev.get("kind", "")) in ["dev_inspect", "dev_error", "replay_loaded"]:
			if is_instance_valid(dev_console): dev_console.receive(ev)
			continue
		if str(ev.get("kind", "")) in ["view", "state", "dv_action", "dv_present", "error", "bookmark_loaded"]: flow.transition("player_options_open", false)
		if flow.view_faulted:
			if str(ev.get("kind", "")) == "error": _show_error(str(ev.message))
			continue
		if str(ev.get("kind", "")) in ["dv_present", "error"]: flow.transition("completion_pending", false)
		if str(ev.get("kind", "")) == "bookmark_loaded":
			_install_bookmark(ev)
			continue
		if str(ev.get("kind", "")) in ["bookmark_saved", "bookmark_error"]:
			if str(ev.get("kind", "")) == "bookmark_saved" or str(ev.get("operation", "")) == "save":
				flow.transition("bookmark_saving", false)
			if str(ev.get("kind", "")) == "bookmark_error" and str(ev.get("operation", "")) == "load":
				flow.transition("bookmark_loading", false)
			if is_instance_valid(ui): ui.set_bookmark_loading(flow.bookmark_loading or flow.bookmark_saving)
			if not flow.book_reading: _on_book_reading_changed(false)
			if is_instance_valid(ui):
				var message := str(ev.get("message", ""))
				if message == "": message = tr("书签已保存。") if str(ev.kind) == "bookmark_saved" else tr("暂时无法保存或载入，请等待当前行动结束。")
				ui.show_bookmark_result(message, str(ev.kind) == "bookmark_saved")
			continue
		if flow.reading and str(ev.get("kind", "")) == "state" and is_instance_valid(field):
			field.apply_snapshot(ev.snapshot)
		if not flow.book_reading and flow.reading and str(ev.get("kind", "")) == "dv_action" and _background_npc_intent(ev):
			_execute_reading_npc(ev)
		elif not flow.book_reading and flow.reading and str(ev.get("kind", "")) == "dv_present" and _presentation_lines(ev.presentation).is_empty():
			# Empty presentation has no reading work. Remember that its next cue was
			# already sent so the foreground drain cannot prepare the actor twice.
			ev.advance_requested = true
			_queue.append(ev)
			dispatch.call_deferred({"kind": "dv_next", "schedule": "reading-background"})
		else:
			_queue.append(ev)
	_drain.call_deferred()

func _has_deferred_next() -> bool:
	for input in _book_deferred_inputs:
		if str(input.get("kind", "")) == "dv_next": return true
	return false

func _on_book_reading_changed(active: bool) -> void:
	flow.transition("book_reading", active)
	if is_instance_valid(view_trace): view_trace.record_page()
	if active or flow.closing or flow.bookmark_loading or flow.bookmark_saving: return
	var deferred := _book_deferred_inputs
	_book_deferred_inputs = []
	for input in deferred: dispatch.call_deferred(input)
	_drain.call_deferred()

func _install_bookmark(event: Dictionary) -> void:
	_session_generation += 1
	flow.transition("bookmark_requested", false)
	_bookmark_next = {}
	flow.transition("view_faulted", false)
	flow.transition("player_options_open", false)
	var generation := _session_generation
	# The host already owns the restored session. Bind its archive immediately,
	# so closing the window during map preparation cannot save the old notes.
	# The departing UI keeps its old object until disposal; it cannot mutate this.
	_casebook = CASEBOOK_DATA.new()
	_casebook.restore_data(event.get("casebook", {}), str(event.snapshot.get("viewer", "")))
	flow.transition("reading", false)
	_queue.clear()
	_book_deferred_inputs.clear()
	_dv_intent.clear()
	_continuous_selection.clear()
	_interaction_id = ""
	_interaction_site = ""
	# The host only emits this after persistence has refused pending execution.
	var installed: bool = await _view({"resource": event.snapshot.resource, "snapshot": event.snapshot,
		"casebook_warning": event.get("casebook_warning", ""), "replace_session": true}, false)
	if flow.closing or generation != _session_generation: return
	if not installed:
		flow.transition("bookmark_loading", true)
		get_tree().paused = true
		if is_instance_valid(ui): ui.set_bookmark_loading(true)
		_timing("bookmark_view_failed", {"path": event.get("path", ""), "backup_path": event.get("backup_path", "")})
		_show_bookmark_view_error(str(event.get("backup_path", "")))
		return
	flow.transition("bookmark_loading", false)
	if is_instance_valid(ui): ui.set_bookmark_loading(false)
	if not flow.closing: dispatch.call_deferred({"kind": "view_ready"})

func _background_npc_intent(ev: Dictionary) -> bool:
	if flow.background_execution or not is_instance_valid(field): return false
	var intent: Dictionary = ev.get("intent", {})
	return str(intent.get("status", "")) == "prepared" and str(intent.get("actor", "")) != str(field.snapshot.get("viewer", ""))

func _execute_reading_npc(ev: Dictionary) -> void:
	flow.transition("background_execution", true)
	_timing("reading_npc_execution_begin", {"actor": ev.intent.actor, "action_event": ev.intent.action_event})
	await _dv_execute(ev.intent, ev.get("execution_snapshot", {}))
	_timing("reading_npc_execution_end", {"actor": ev.intent.actor, "action_event": ev.intent.action_event})
	flow.transition("background_execution", false)

func _dv_execute(intent: Dictionary, execution_snapshot: Dictionary) -> void:
	if str(intent.get("status", "")) == "awaiting_input":
		if not _continuous_selection.is_empty():
			field.reject_continuous_request(str(_continuous_selection.get("choice_id", "")), "prepare_returned_to_input")
			_continuous_selection.clear()
		field.spatial.set_options(intent.options)
		flow.transition("player_options_open", true)
		if is_instance_valid(ui): ui.set_player_options(intent.options)
		_timing("player_options_ready", {"options": intent.options.size()})
		return
	if str(intent.get("status", "")) != "prepared":
		if not _continuous_selection.is_empty() and is_instance_valid(field):
			field.reject_continuous_request(str(_continuous_selection.get("choice_id", "")), "prepare_not_ready")
			_continuous_selection.clear()
		return
	_dv_intent = intent
	flow.transition("player_options_open", false)
	_dv_serial += 1
	var action: Dictionary = intent.action
	_timing("execution_begin", {"action_event": intent.action_event, "actor": intent.actor})
	if not _continuous_selection.is_empty() and str(intent.actor) == str(field.snapshot.get("viewer", "")):
		if _verified_continuous_action(intent, execution_snapshot):
			# Only the host adds this execution mode after matching the independently
			# prepared DV intent. The request itself is never an arrival receipt.
			intent = intent.duplicate(false)
			intent.destination = intent.destination.duplicate(true)
			intent.destination.continuous_walk = true
			intent.destination.continuous_request_id = str(_continuous_selection.get("request_id", ""))
			_dv_intent = intent
		else:
			field.reject_continuous_request(str(_continuous_selection.get("choice_id", "")), "prepared_passage_mismatch")
			_continuous_selection.clear()
			_dv_complete("rejected", {"executor": "godot", "reason": "prepared_passage_mismatch", "context": field.context()})
			return
	if str(action.kind) == "move" or action.get("travel", {}).has("station"):
		var receipt: Dictionary = await field_executor.execute(intent, field, execution_snapshot, _dv_serial)
		_accept_execution(receipt)
	else:
		var site := _interaction_site if str(intent.actor) == str(field.snapshot.get("viewer", "")) else ""
		var contact := true
		if site != "" and site != "self":
			contact = field.spatial._in_reach(site)
		_dv_complete("applied" if contact else "rejected", {"executor": "dv-world", "site": site, "contact": contact, "context": field.context()})

func _verified_continuous_action(intent: Dictionary, source: Dictionary) -> bool:
	if not is_instance_valid(field) or not is_instance_valid(field.adapter): return false
	var action: Dictionary = intent.get("action", {})
	var destination: Dictionary = intent.get("destination", {})
	var request := _continuous_selection
	var map_key := str(source.get("map", ""))
	var from_room := str(source.get("execution_room", ""))
	var to_room := str(destination.get("execution_room", destination.get("room", "")))
	if str(action.get("kind", "")) != "move" or action.get("travel", {}).has("station"): return false
	if str(request.get("request_id", "")).is_empty() or str(intent.get("actor", "")) != str(request.get("actor", "")): return false
	if str(action.get("id", "")) != str(request.get("choice_id", "")): return false
	if map_key.is_empty() or from_room.is_empty() or to_room.is_empty() or from_room == to_room: return false
	if str(source.get("resource", "")) != field.execution_resource: return false
	if map_key != str(destination.get("map", "")) or map_key != str(field.adapter.map_key()): return false
	if str(request.get("map", "")) != map_key or str(request.get("from_room", "")) != from_room or str(request.get("to_room", "")) != to_room: return false
	return field.is_continuous_action(str(action.id), destination)

func _dv_complete(outcome: String, execution: Dictionary) -> void:
	_timing("execution_end", {"outcome": outcome, "action_event": _dv_intent.action_event})
	var receipt := {"action_event": _dv_intent.action_event, "outcome": outcome, "actual": _dv_intent.execution_cost, "executor": "room-graph" if str(execution.get("execution_world", "")) == "room-graph" else "godot", "field": execution}
	if str(_dv_intent.get("actor", "")) == str(_continuous_selection.get("actor", "")) and str(_dv_intent.get("action", {}).get("id", "")) == str(_continuous_selection.get("choice_id", "")):
		if outcome != "applied" and is_instance_valid(field):
			var phase := str(field.continuous_request().get("phase", ""))
			if phase in ["requested", "settling"]:
				# The executor can reject before execute_move accepts the request.
				# Such a request will never receive a spatial prepared/settling
				# transition, so release it alongside its one rejected receipt.
				field.reject_continuous_request(str(_continuous_selection.get("choice_id", "")), "execution_rejected")
		_continuous_selection.clear()
	flow.transition("player_options_open", false)
	# NPCs may finish before a queued player action. Keep that player's
	# contact until their own receipt, so delayed inspection is still checked.
	if str(_dv_intent.get("actor", "")) == str(field.snapshot.get("viewer", "")):
		_interaction_site = ""
	_dv_intent = {}
	flow.transition("completion_pending", true)
	dispatch.call_deferred({"kind": "dv_complete", "receipt": receipt})

func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_CLOSE_REQUEST and not replay_mode and not flow.closing:
		await Sound.quit_cleanly()


func set_frame_audit(enabled: bool) -> void:
	audit_enabled = enabled
	view_trace.set_enabled(enabled)
	diagnostics.set_enabled(enabled)
	engine.SetDiagnosticsEnabled(enabled or (is_instance_valid(dev_console) and dev_console.performance_open()))

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
	var result: Array = []
	if not is_instance_valid(field): return result
	for who in field.actors:
		var body: CharacterBody2D = field.actors[who]
		if not is_instance_valid(body): continue
		result.append({"actor": str(who), "map": str(field.adapter.map_key()),
			"room": str(field.adapter.room_of(body.position)), "position": [body.position.x, body.position.y],
			"velocity": [body.velocity.x, body.velocity.y], "seated": bool(body.seated)})
	return result

func set_navigation_sampling(active: bool) -> void:
	navigation_sampling = active
	if is_instance_valid(field): field.navigation_sampling = active
	if is_instance_valid(field_executor): field_executor.set_navigation_sampling(active)

## Replay owns a private scene graph and never enters the live action queue.
func install_replay_snapshot(value: Dictionary, archive: Dictionary) -> bool:
	flow.transition("started", true)
	var snapshot: Dictionary = PRESENTATION.snapshot(value)
	if not archive.is_empty(): _casebook.restore_data(archive, str(value.viewer))
	var rebuild := not is_instance_valid(field) or str(field.adapter.map_key()) != str(snapshot.map)
	if rebuild:
		var installed: bool = await _view({"resource": snapshot.resource, "snapshot": snapshot,
			"casebook": archive if not archive.is_empty() else _casebook.export_data(), "replace_session": true}, false)
		if not installed: return false
	else:
		await _apply(snapshot)
	var pending: Dictionary = snapshot.get("pending", {})
	var options: Array = pending.get("options", []) if str(pending.get("status", "")) == "awaiting_input" else []
	field.spatial.set_options(options)
	ui.set_player_options(options)
	flow.transition("player_options_open", true)
	flow.transition("opening", false)
	return true
