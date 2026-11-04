extends RefCounted
## HostViewComponent owns host-side presentation work; session access goes through Main.
var host: Node
var _notice: AcceptDialog

func _init(main: Node) -> void:
	host = main

func _dispose_views(keep_title := false) -> void:
	host.flow.transition("player_options_open", false)
	if is_instance_valid(host.character_controller3d): host.character_controller3d.cancel_all("native-spatial-view-replaced")
	if is_instance_valid(host.field):
		if is_instance_valid(host.field_executor): host.field_executor.retain_field(host.field)
		host.field.begin_shutdown()
		host.remove_child(host.field)
		host.field.queue_free()
	host.field = null
	if is_instance_valid(host.ui):
		host.ui.begin_shutdown()
		host.remove_child(host.ui)
		host.ui.queue_free()
	host.ui = null
	if not keep_title and is_instance_valid(host.title_layer):
		host.remove_child(host.title_layer)
		host.title_layer.queue_free()
		host.title_layer = null

func _home() -> void:
	host._dispose_views()
	if is_instance_valid(host.field_executor): host.field_executor.reset()
	host.title_layer = CanvasLayer.new()
	host.title_layer.layer = 30
	host.add_child(host.title_layer)
	var title: Control = host.TITLE_SCENE.instantiate()
	title.mode = "title"
	title.replay_mode = host.replay_mode
	title.native_character = host.native_character_mode()
	host.title_layer.add_child(title)
	title.ui_event.connect(host.dispatch)
	if is_instance_valid(host.view_trace): host.move_child(host.view_trace, host.get_child_count() - 1)

func _view(ev: Dictionary, begin := true) -> bool:
	var generation = host._session_generation
	var replacing = bool(ev.get("replace_session", false))
	var opening_title: Control = host.title_layer.get_child(0) if is_instance_valid(host.title_layer) else null
	host._timing("view_build_begin")
	var resource: PackedScene = host.CAMPUS_MAP_VIEW if str(ev.resource) == "res://scenes/components/world/CampusMapView.tscn" else (load(str(ev.resource)) as PackedScene)
	if resource == null:
		host._view_error("无法加载世界视图：" + str(ev.resource), replacing)
		return false
	var initial: Dictionary = ev.snapshot
	var presentation: Dictionary = initial.get("presentation", {})
	if str(presentation.get("player_actor", "")) == "" or str(presentation.get("player_sprite", "")) == "":
		host._view_error("开场快照缺少人物呈现绑定。", replacing)
		return false
	var adapter: Node2D = resource.instantiate()
	host._pending_adapter = adapter
	if adapter.get("presentation") != null and initial.has("presentation"):
		adapter.presentation = initial.presentation
	if is_instance_valid(opening_title): opening_title.set_loading_stage("map")
	host._timing("view_prepare_begin", {"map": initial.get("map", "")})
	var prepared = true
	if adapter.has_method("prepare_map"):
		prepared = await adapter.prepare_map(str(initial.get("map", "")), initial, host.get_tree())
	host._timing("view_prepare_end", {"ready": prepared})
	if not prepared or host.flow.closing or generation != host._session_generation:
		var preparation_error = str(adapter.get("preparation_error")) if adapter.get("preparation_error") != null else ""
		host._discard_adapter(adapter)
		if not prepared and not host.flow.closing and generation == host._session_generation:
			host._view_error(preparation_error if preparation_error != "" else "无法准备世界地图：" + str(initial.get("map", "")), replacing)
		return false
	# A prepared background map cannot replace a book the player is reading.
	# An explicit bookmark install owns the replacement and closes its old UI.
	while host.flow.book_reading and not replacing and not host.flow.closing and generation == host._session_generation:
		await host.get_tree().process_frame
	if host.flow.closing or generation != host._session_generation:
		host._discard_adapter(adapter)
		return false
	# Keep the title opaque while the actual field, actors and UI are assembled
	# and rendered behind it. In-game view replacement retains its own fade.
	if not is_instance_valid(opening_title) and is_instance_valid(host.ui) and host.ui.has_method("transition_out"):
		await host.ui.transition_out()
	if host.flow.closing or generation != host._session_generation:
		host._discard_adapter(adapter)
		return false
	if is_instance_valid(host.field) and not replacing:
		await host.field_executor.retain_active_movements(host.field)
	host._dispose_views(is_instance_valid(opening_title))
	if replacing: host.field_executor.reset()
	host._view_resource = str(ev.resource)
	host.ui = host.UI_SCENE.instantiate()
	if ev.has("casebook"):
		host._casebook.restore_data(ev.casebook, str(initial.get("viewer", "")))
	host.ui.casebook = host._casebook
	if host.replay_mode: host.ui.set_replay_mode(true)
	host.ui.host_input_blocked = true
	host.ui.start_covered = not is_instance_valid(opening_title)
	host.ui.name = "UI"
	host.add_child(host.ui)
	host.ui.ui_event.connect(host.dispatch)
	host.ui.casebook_reading_changed.connect(host._on_book_reading_changed)
	host.field = Node2D.new()
	host.field.set_script(host.FIELD_SCRIPT)
	host.field.name = "WorldView"
	host.field.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	host.add_child(host.field)
	host.field.ui_busy = host._field_input_busy
	host.field.formal_selection_ready = host._formal_selection_ready
	if host.replay_mode: host.field.input_direction = host.replay_input_direction
	host.field.fader = host.ui
	host.field.field_event.connect(host._field_input)
	host.field.hover_changed.connect(host.ui.show_hover)
	host.field.hint_changed.connect(host.ui.set_hint)
	host.field_executor.configure_visible(host.field, initial)
	host._pending_adapter = null
	if is_instance_valid(opening_title): opening_title.set_loading_stage("cast")
	if host.flow.opening:
		var cancelled := func(): return host.flow.closing or generation != host._session_generation
		if not await preload("res://scenes/components/characters/CharacterStage3D.gd").prepare_cast(host.get_tree(), host, cancelled):
			host._view_error("人物资源未能完成准备。", replacing)
			return false
	host.field.setup(adapter)
	if not is_instance_valid(host.field.player) or not is_instance_valid(host.field.spatial):
		host._view_error("现场人物或交互未能完成装配。", replacing)
		return false
	host.field_executor.bind_visible(host.field, initial)
	host.ui.apply_snapshot(initial)
	if is_instance_valid(host.view_trace): host.move_child(host.view_trace, host.get_child_count() - 1)
	if not await host.field.prepare_presentation(initial):
		if not host.flow.closing and generation == host._session_generation:
			host._view_error("现场未能完成准备：" + str(host.field.presentation_error), replacing)
		return false
	if host.flow.closing or generation != host._session_generation: return false
	if not await host.ui.prepare_for_reveal(): return false
	if host.flow.closing or generation != host._session_generation: return false
	if is_instance_valid(opening_title): opening_title.set_loading_stage("frame")
	# Layout, physics registration, sight/light updates and an actual draw must
	# all precede the reveal. Fixed timers and resource completion are not ready.
	await host.get_tree().process_frame
	if host.flow.closing or generation != host._session_generation: return false
	await host.get_tree().physics_frame
	if host.flow.closing or generation != host._session_generation: return false
	var rendered = DisplayServer.get_name() != "headless"
	if rendered: await RenderingServer.frame_post_draw
	else: await host.get_tree().process_frame
	if host.flow.closing or generation != host._session_generation: return false
	host._timing("view_build_end")
	host._timing("view_prepared_for_reveal", {"map": initial.get("map", ""), "actors": host.field.actors.size(), "frame": Engine.get_frames_drawn(), "rendered": rendered, "title_covering": is_instance_valid(opening_title)})
	if is_instance_valid(opening_title):
		host._timing("title_reveal_begin")
		var revealed: bool = await opening_title.transition_to_field()
		if not revealed or host.flow.closing or generation != host._session_generation: return false
		host._timing("title_reveal_end", {"frame": Engine.get_frames_drawn()})
		host.remove_child(host.title_layer)
		host.title_layer.queue_free()
		host.title_layer = null
	elif host.ui.has_method("transition_in"):
		await host.ui.transition_in()
	# 等场景加入树并应用快照后才开始脚本；这也是可见视图的普通握手。
	if host.flow.closing or generation != host._session_generation: return false
	host.flow.transition("opening", false)
	host.ui.set_host_input_blocked(false)
	if begin: host.dispatch.call_deferred({"kind": "view_ready"})
	if str(ev.get("casebook_warning", "")) != "":
		host._show_casebook_warning(str(ev.casebook_warning))
	return true

func _view_error(message: String, replacing: bool) -> void:
	if replacing: return # The bookmark owner reports its backup and holds the lock.
	if is_instance_valid(host.title_layer):
		host.flow.transition("opening", false)
		host.flow.transition("opening_failed", true)
		host._queue.clear()
		host.title_layer.get_child(0).show_start_error(message, false)
		host._timing("title_opening_failed", {"message": message})
		push_error(message)
	else:
		host._show_error(message)

func _discard_adapter(adapter: Node2D) -> void:
	if host._pending_adapter == adapter: host._pending_adapter = null
	if is_instance_valid(adapter):
		if adapter.has_method("begin_shutdown"): adapter.begin_shutdown()
		adapter.free()

func _show_casebook_warning(message: String) -> void:
	host._show_notice(tr("案卷提示"), message, Vector2i(640, 240), true)

func _show_bookmark_view_error(backup_path: String) -> void:
	var message = tr("书签已载入宿主，但现场未能完成装配。当前现场已暂停，请关闭游戏后重新打开，并从“续读”选择载入前备份。")
	if backup_path != "": message += "\n\n" + tr("载入前备份：%s") % backup_path
	if is_instance_valid(host.ui): host.ui.show_bookmark_result(message)
	host._show_notice(tr("书签载入未完成"), message, Vector2i(680, 300), true)

## 热点与跨房间进入DV；房间内坐标接近由现场消费，不进入此入口。
func _show_error(message: String) -> void:
	push_error(message)
	if is_instance_valid(host.title_layer) and host.title_layer.get_child(0).has_method("show_start_error"):
		host.title_layer.get_child(0).show_start_error(message, not host.flow.started)
		return
	host._show_notice("会话错误", message, Vector2i(640, 240))

func _show_notice(caption: String, message: String, size: Vector2i, always_process := false) -> void:
	if not is_instance_valid(_notice) or _notice.is_queued_for_deletion():
		_notice = AcceptDialog.new()
		_notice.theme = host.NOTICE_THEME
		host.add_child(_notice)
		_notice.confirmed.connect(_notice.queue_free)
		_notice.canceled.connect(_notice.queue_free)
	_notice.title = caption
	_notice.dialog_text = message
	if always_process: _notice.process_mode = Node.PROCESS_MODE_ALWAYS
	if not _notice.visible: _notice.popup_centered(size)

func has_open_notice() -> bool:
	return is_instance_valid(_notice) and not _notice.is_queued_for_deletion() and _notice.visible

