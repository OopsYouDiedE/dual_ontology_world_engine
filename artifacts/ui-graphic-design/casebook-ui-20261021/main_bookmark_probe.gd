extends SceneTree
## Temporary production-UI observation; intentionally outside the project tools.
## godot --path <project/godot_csharp> --script <absolute/this/file.gd> --
##   --outpath=<new absolute directory>
## No fixture, world-state injection, semantic dispatch, or existing-save load.
## Operation budget: 38s. Seal this archive by 44s, then use production shutdown.
## The 50s watchdog reports delay; it NEVER kills an in-progress world save.

const MAIN_SCENE := "res://scenes/Host/Main.tscn"
const ARCHIVE_SCRIPT := "res://scenes/Host/FrameArchive.cs"
const Sound := preload("res://scenes/Shared/Audio/Sound.gd")
const WORK_LIMIT_US := 38000000
const ARCHIVE_LIMIT_US := 44000000
const WATCHDOG_US := 50000000
const RESOLUTION := Vector2i(1280, 992)
const STREAMS := ["probe-input.dvlog", "probe-frames.dvlog", "probe-summary.dvlog"]

var output := ""
var origin := 0
var main: Node
var archive: Node
var phase := "initializing"
var pointer := Vector2(640, 496)
var finishing := false
var sealed := false
var watchdog_reported := false
var archive_failed := false
var failures: Array[String] = []
var pending_frames: Dictionary = {}
var frame_names: Dictionary = {}
var frames_written := 0
var pressed: Array[Dictionary] = []
var watched: Dictionary = {}
var own_saved_path := ""
var loaded_path := ""
var backup_path := ""
var persistence_error: Dictionary = {}
var existing_saves: Dictionary = {}
var existing_sessions: Dictionary = {}
var initial_field_id := 0
var restored_field_id := 0
var initial_generation := -1
var paused_key_packets := 0
var confirmation_observed := false
var main_shutdown_seen := false
var preference_before: Dictionary = {}

func _initialize() -> void:
	origin = Time.get_ticks_usec()
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--outpath="):
			output = argument.trim_prefix("--outpath=")
		else:
			_setup_error("Unknown probe argument: " + argument)
			return
	if output.is_empty() or not output.is_absolute_path() or DirAccess.dir_exists_absolute(output) or FileAccess.file_exists(output):
		_setup_error("Use --outpath=<new absolute directory>; existing evidence is never overwritten")
		return
	if DisplayServer.get_name() == "headless" or not is_equal_approx(Engine.time_scale, 1.0):
		_setup_error("This observation requires a rendered window and real-time Engine.time_scale = 1")
		return
	for argument in OS.get_cmdline_args():
		if argument.begins_with("--fixed-fps") or argument.begins_with("--write-movie"):
			_setup_error("Fixed-step/movie execution is outside this real-time observation")
			return
	var error := DirAccess.make_dir_recursive_absolute(output)
	if error != OK:
		_setup_error("Cannot create output directory: " + error_string(error))
		return
	_run.call_deferred()

func _setup_error(message: String) -> void:
	failures.append(message)
	push_error(message)
	_finish.call_deferred(2)

func _process(_delta: float) -> bool:
	_poll_archive()
	var elapsed := Time.get_ticks_usec() - origin
	if elapsed >= WORK_LIMIT_US and not finishing:
		_fail("38-second operation budget expired at " + phase)
		_finish(9)
	if elapsed >= WATCHDOG_US and not watchdog_reported:
		watchdog_reported = true
		push_error("BOOKMARK_PROBE_SHUTDOWN_DELAY elapsed_us=%d; production save is still allowed to finish" % elapsed)
	return false

func _plain(value: Variant) -> Variant:
	if value is Vector2 or value is Vector2i: return [value.x, value.y]
	if value is Dictionary:
		var result := {}
		for key in value: result[str(key)] = _plain(value[key])
		return result
	if value is Array:
		var result: Array = []
		for item in value: result.append(_plain(item))
		return result
	return value

func _record(value: Dictionary, stream := "probe-input.dvlog") -> void:
	if archive == null or sealed: return
	value.real_us = Time.get_ticks_usec() - origin
	value.phase = phase
	# JSON exists only in memory at the existing checked binary writer boundary.
	archive.AppendRecords(output.path_join(stream + ".partial"), JSON.stringify(_plain(value)) + "\n")

func _fail(message: String) -> void:
	failures.append(message)
	push_error(message)
	_record({"kind": "failure", "message": message})

func _normal_path(path: String) -> String:
	var result := ProjectSettings.globalize_path(path).replace("\\", "/").simplify_path()
	return result.to_lower() if OS.get_name() == "Windows" else result

func _directory_names(path: String, directories := false) -> Dictionary:
	var result := {}
	var directory := DirAccess.open(path)
	if directory != null:
		var names := directory.get_directories() if directories else directory.get_files()
		for entry in names: result[_normal_path(path.path_join(entry))] = true
	return result

func _preferences() -> Dictionary:
	var result := {}
	for path in ["user://language.cfg", "user://settings.cfg"]:
		var exists := FileAccess.file_exists(path)
		result[path] = {"exists": exists, "sha256": FileAccess.get_sha256(path) if exists else ""}
	return result

func _source_hashes() -> Dictionary:
	var result := {}
	for path in [MAIN_SCENE, "res://scenes/Host/Main.gd", "res://scenes/Host/GameEngine.cs",
			"res://scenes/Host/CasebookPersistence.cs", "res://scenes/UI/TitleUI.gd",
			"res://scenes/UI/CasebookUI.gd", "res://scenes/UI/UISystem.gd",
			"res://scenes/Shared/Audio/GameAudio.gd", ARCHIVE_SCRIPT, get_script().resource_path]:
		result[path] = FileAccess.get_sha256(path) if FileAccess.file_exists(path) else "unavailable"
	return result

func _on_engine_events(events: Array) -> void:
	for event in events:
		var kind := str(event.get("kind", ""))
		if kind not in ["bookmark_saved", "bookmark_loaded", "bookmark_error", "error"]: continue
		var receipt := {"kind": "production_event", "event_kind": kind}
		for key in ["path", "backup_path", "operation", "message", "casebook_warning", "save_written", "casebook_written"]:
			if event.has(key): receipt[key] = event[key]
		_record(receipt)
		if kind == "bookmark_saved":
			var path := str(event.get("path", ""))
			if phase != "save_requested" or not own_saved_path.is_empty() or path.is_empty() or existing_saves.has(_normal_path(path)):
				_fail("Save receipt is not the unique newly requested bookmark")
				continue
			own_saved_path = path
			print("BOOKMARK_PROBE_OWN_SAVE ", own_saved_path)
		elif kind == "bookmark_loaded":
			loaded_path = str(event.get("path", ""))
			backup_path = str(event.get("backup_path", ""))
			if not confirmation_observed or phase != "load_confirmed" or own_saved_path.is_empty() or _normal_path(loaded_path) != _normal_path(own_saved_path):
				_fail("A load receipt was not bound to the explicitly confirmed own bookmark")
			print("BOOKMARK_PROBE_LOADED ", loaded_path, " backup=", backup_path)
		elif kind == "bookmark_error":
			persistence_error = receipt.duplicate(true)
			_fail("Production bookmark operation reported an error: " + str(event.get("message", "")))
		else:
			_fail("Production host reported an error during bookmark observation")

func _on_main_shutdown() -> void:
	main_shutdown_seen = true
	var candidates: Array[String] = []
	for path in _directory_names("user://dv-sessions", true):
		if not existing_sessions.has(path): candidates.append(str(path))
	# The harness archive was sealed before asking Sound to close. Production
	# close-result.dvlog remains the authority for completeness, not this signal.
	print("BOOKMARK_PROBE_MAIN_SHUTDOWN_COMPLETED elapsed_us=", Time.get_ticks_usec() - origin)
	for path in candidates:
		print("BOOKMARK_PROBE_NEW_SESSION_CANDIDATE ", path,
			" close_result_exists=", FileAccess.file_exists(path.path_join("close-result.dvlog")))
	if candidates.size() != 1:
		push_warning("New session-directory identity is ambiguous; inspect production close records")

func _title() -> Control:
	if not is_instance_valid(main) or not is_instance_valid(main.title_layer) or main.title_layer.get_child_count() == 0: return null
	return main.title_layer.get_child(0) as Control

func _book() -> Control:
	if not is_instance_valid(main) or not is_instance_valid(main.ui): return null
	var casebook: Node = main.ui.settings_ui
	return casebook.book if is_instance_valid(casebook) and casebook.is_active() else null

func _page_ready(book: Control, expected: String) -> bool:
	return is_instance_valid(book) and str(book._current_page) == expected and not book._turning \
		and not book._page_motion.is_transitioning() and not book.viewport.gui_disable_input \
		and not book._leaving and (expected != "cover" or book._menu_input_enabled)

func _field_ready() -> bool:
	if not is_instance_valid(main) or not is_instance_valid(main.ui) or not is_instance_valid(main.field): return false
	if is_instance_valid(main.title_layer) or is_instance_valid(main._pending_adapter): return false
	if main._bookmark_loading or main._bookmark_saving or main._book_reading or paused: return false
	if not main.field.is_visible_in_tree() or not is_instance_valid(main.field.adapter): return false
	return not main.ui.is_busy() and main._bookmark_boundary_ready()

func _host_state() -> Dictionary:
	var result := {"tree_paused": paused, "locale": TranslationServer.get_locale(), "fixture": false}
	if not is_instance_valid(main): return result
	result.merge({"field_id": main.field.get_instance_id() if is_instance_valid(main.field) else 0,
		"pending_view": is_instance_valid(main._pending_adapter), "bookmark_loading": main._bookmark_loading,
		"bookmark_saving": main._bookmark_saving, "book_reading": main._book_reading,
		"reading": main._reading, "draining": main._draining, "session_generation": main._session_generation})
	if is_instance_valid(main.ui): result.ui_context = main.ui.view_trace_context()
	var book := _book()
	if is_instance_valid(book):
		result.merge({"page": book._current_page, "turning": book._turning,
			"page_motion": book._page_motion.is_transitioning(), "page_input_disabled": book.viewport.gui_disable_input})
	return result

func _wait_until(check: Callable, label: String, seconds: float, stable_us := 0) -> bool:
	var deadline := mini(origin + WORK_LIMIT_US, Time.get_ticks_usec() + int(seconds * 1000000.0))
	var ready_since := 0
	while not finishing:
		if archive_failed or not persistence_error.is_empty(): return false
		var now := Time.get_ticks_usec()
		if now >= deadline:
			_fail("Timed out waiting for " + label)
			_record({"kind": "wait_timeout", "label": label, "host": _host_state()})
			return false
		if check.call():
			if ready_since == 0: ready_since = now
			if now - ready_since >= stable_us:
				await RenderingServer.frame_post_draw
				if finishing: return false
				if check.call(): return true
				ready_since = 0
		else:
			ready_since = 0
		await process_frame
	return false

func _key_packet(key: Key, down: bool, shift := false) -> void:
	var event := InputEventKey.new()
	event.keycode = key
	event.physical_keycode = key
	event.pressed = down
	event.shift_pressed = shift
	event.echo = false
	_record({"kind": "native_key_packet", "key": OS.get_keycode_string(key), "keycode": key,
		"pressed": down, "shift": shift, "tree_paused": paused, "method": "Input.parse_input_event"})
	if paused: paused_key_packets += 1
	Input.parse_input_event(event)

func _key(key: Key) -> void:
	_key_packet(key, true)
	_key_packet(key, false)

func _move(point: Vector2) -> void:
	var event := InputEventMouseMotion.new()
	event.position = point
	event.global_position = point
	event.relative = point - pointer
	pointer = point
	_record({"kind": "native_mouse_motion", "position": point, "relative": event.relative})
	Input.parse_input_event(event)

func _mouse(point: Vector2, button: MouseButton, down: bool, label: String) -> void:
	var event := InputEventMouseButton.new()
	event.position = point
	event.global_position = point
	event.button_index = button
	event.button_mask = MOUSE_BUTTON_MASK_LEFT if down and button == MOUSE_BUTTON_LEFT else 0
	event.pressed = down
	_record({"kind": "native_mouse_button", "position": point, "button": button,
		"pressed": down, "target": label, "tree_paused": paused, "method": "Input.parse_input_event"})
	Input.parse_input_event(event)

func _on_pressed(id: int, path: String) -> void:
	var row := {"instance_id": id, "control_path": path}
	pressed.append(row)
	_record({"kind": "production_control_pressed", "control": row})

func _scroll_to(book: Control, control: Control) -> bool:
	if not book._leaf_scroll.is_ancestor_of(control): return true
	for attempt in range(24):
		if finishing or not is_instance_valid(control): return false
		var clip: Rect2 = book._leaf_scroll.get_global_rect().grow(-14)
		var center := control.get_global_rect().get_center()
		if clip.has_point(center): return true
		var point: Vector2 = book.project_button(book._leaf_scroll)
		_move(point)
		var direction := MOUSE_BUTTON_WHEEL_DOWN if center.y > clip.end.y else MOUSE_BUTTON_WHEEL_UP
		_mouse(point, direction, true, "bookmark_scroll")
		_mouse(point, direction, false, "bookmark_scroll")
		await process_frame
		await process_frame
	_fail("Own bookmark did not become visible through native scrolling")
	return false

func _click(book: Control, control: Button, label: String) -> bool:
	if finishing or not is_instance_valid(book) or not is_instance_valid(control) or not control.is_visible_in_tree() or control.disabled:
		_fail("Unavailable production button: " + label)
		return false
	if not await _scroll_to(book, control): return false
	var id := control.get_instance_id()
	if not watched.has(id):
		control.pressed.connect(_on_pressed.bind(id, str(control.get_path())))
		watched[id] = true
	_move(book.project_button(control))
	await create_timer(0.07, true, false, true).timeout
	if finishing or not is_instance_valid(control): return false
	var point: Vector2 = book.project_button(control)
	if not root.get_visible_rect().has_point(point):
		_fail("Projected production button is outside the window: " + label)
		return false
	_move(point)
	var before := pressed.size()
	_mouse(point, MOUSE_BUTTON_LEFT, true, label)
	_mouse(point, MOUSE_BUTTON_LEFT, false, label)
	if not await _wait_until(func(): return pressed.size() > before, "native activation of " + label, 0.8): return false
	if pressed.size() != before + 1 or int(pressed.back().instance_id) != id:
		_fail("Native input activated a different production button: " + label)
		return false
	return true

func _find_button(node: Node, source_caption: String) -> Button:
	if not is_instance_valid(node): return null
	if node is Button and node.text == TranslationServer.translate(source_caption): return node
	for child in node.get_children():
		var result := _find_button(child, source_caption)
		if result != null: return result
	return null

func _own_path_controls(node: Node, buttons_only: bool, result: Array) -> void:
	if not is_instance_valid(node): return
	if node is Control and node.has_meta("bookmark_display_path") and (not buttons_only or node is Button):
		if _normal_path(str(node.get_meta("bookmark_display_path"))) == _normal_path(own_saved_path): result.append(node)
	for child in node.get_children(): _own_path_controls(child, buttons_only, result)

func _own_button() -> Button:
	var book := _book()
	if not is_instance_valid(book) or own_saved_path.is_empty(): return null
	var matches: Array = []
	_own_path_controls(book._leaf_body, true, matches)
	return matches[0] if matches.size() == 1 else null

func _confirmation_ready() -> bool:
	var book := _book()
	if not _page_ready(book, "load") or own_saved_path.is_empty(): return false
	var matches: Array = []
	_own_path_controls(book._leaf_body, false, matches)
	return matches.size() == 1 and not matches[0] is Button \
		and is_instance_valid(_find_button(book._leaf_body, "载入此书签"))

func _poll_archive() -> void:
	if archive == null or sealed: return
	for encoded in archive.PollCompleted():
		var row: Variant = JSON.parse_string(str(encoded))
		if not row is Dictionary or (row.get("error") != null and str(row.get("error")) != ""):
			archive_failed = true
			push_error("Bookmark probe archive error: " + str(row))
			continue
		var file := str(row.get("file", ""))
		if pending_frames.has(file):
			var index: Dictionary = pending_frames[file]
			pending_frames.erase(file)
			index.kind = "frame_written"
			index.archive = row
			frames_written += 1
			_record(index, "probe-frames.dvlog")

func _shot(name: String) -> bool:
	if finishing or frame_names.has(name): return false
	if not await _wait_until(func(): return archive.HasCapacity(), "PNG queue capacity", 1.1): return false
	await RenderingServer.frame_post_draw
	if finishing: return false
	var captured := Time.get_ticks_usec() - origin
	var image: Image = root.get_texture().get_image()
	if image == null or image.is_empty():
		_fail("Rendered window image is empty at " + name)
		return false
	image.convert(Image.FORMAT_RGBA8)
	var row := {"file": name + ".png", "captured_us": captured, "host": _host_state(),
		"own_saved_path": own_saved_path, "loaded_path": loaded_path}
	pending_frames[row.file] = row
	frame_names[name] = true
	archive.Enqueue(image.get_data(), image.get_width(), image.get_height(), output.path_join(row.file), captured)
	_record({"kind": "frame_queued", "file": row.file, "captured_us": captured})
	print("BOOKMARK_PROBE_FRAME ", name, " at_us=", captured)
	return true

func _run() -> void:
	root.size = RESOLUTION
	var archive_resource: Script = load(ARCHIVE_SCRIPT)
	if archive_resource == null or not archive_resource.can_instantiate():
		_fail("Build the Godot .NET project before using FrameArchive")
		await _finish(3)
		return
	archive = archive_resource.new()
	archive.process_mode = Node.PROCESS_MODE_ALWAYS
	root.add_child(archive)
	archive.Configure(Time.get_ticks_usec() - origin)
	preference_before = _preferences()
	existing_saves = _directory_names("user://saves")
	existing_sessions = _directory_names("user://dv-sessions", true)
	_record({"kind": "probe_begin", "fixture": false, "world_action_selection": false,
		"source_hashes": _source_hashes(), "locale": TranslationServer.get_locale(),
		"resolution": RESOLUTION, "preferences": preference_before,
		"work_limit_us": WORK_LIMIT_US, "archive_limit_us": ARCHIVE_LIMIT_US, "watchdog_us": WATCHDOG_US,
		"production_evidence_root": ProjectSettings.globalize_path("user://dv-sessions"),
		"input_method": "Input.parse_input_event", "existing_save_count": existing_saves.size()}, "probe-summary.dvlog")
	_record({"kind": "frame_index_begin", "fixture": false}, "probe-frames.dvlog")
	var scene: PackedScene = load(MAIN_SCENE)
	if scene == null:
		_fail("Production Main.tscn could not be loaded")
		await _finish(3)
		return
	main = scene.instantiate()
	root.add_child(main)
	main.shutdown_completed.connect(_on_main_shutdown)
	main.engine.EventsReady.connect(_on_engine_events)
	if not main.has_method("wait_for_shutdown"):
		_fail("Production Main lacks the finalized wait_for_shutdown interface")
		await _finish(3)
		return
	var success := await _exercise()
	await _finish(0 if success and failures.is_empty() else 5)

func _exercise() -> bool:
	phase = "title_cover"
	if not await _wait_until(func(): return _page_ready(_title(), "cover"), "interactive title cover", 7.0): return false
	_key(KEY_ENTER)
	phase = "new_session_field"
	if not await _wait_until(_field_ready, "new session's idle visible field", 18.0, 180000): return false
	initial_field_id = main.field.get_instance_id()
	initial_generation = int(main._session_generation)
	_record({"kind": "view_build_observed_complete", "which": "initial", "host": _host_state(),
		"basis": "actual field/ui installed, pending adapter absent, transition and presentation boundary idle after draw",
		"production_timing_stage_for_crosscheck": "view_build_end"})
	if not await _shot("01-new-session-field"): return false

	phase = "casebook_settings"
	_key(KEY_F1)
	if not await _wait_until(func(): return _page_ready(_book(), "settings") and main.ui.settings_ui.can_interact(), "F1 settings page", 5.0): return false
	if not paused:
		_fail("Production casebook did not pause the tree")
		return false
	_record({"kind": "paused_input_marker_begin", "host": _host_state(),
		"raw_recorder_verification": "inspect production inputs.dvlog after normal close"})
	_key(KEY_TAB)
	_key_packet(KEY_SHIFT, true, true)
	_key_packet(KEY_TAB, true, true)
	_key_packet(KEY_TAB, false, true)
	_key_packet(KEY_SHIFT, false, false)
	await process_frame
	_record({"kind": "paused_input_marker_end", "packets_sent": paused_key_packets,
		"raw_engine_capture_claimed": false, "tree_paused": paused})
	var book := _book()
	if not await _click(book, book._leaf_next, "settings_to_bookmarks"): return false
	phase = "bookmark_list"
	if not await _wait_until(func(): return _page_ready(_book(), "load") and main.ui.settings_ui.can_interact(), "bookmark page", 4.0): return false
	book = _book()
	phase = "save_requested"
	if not await _click(book, _find_button(book._leaf_body, "保存书签"), "save_new_bookmark"): return false
	if not await _wait_until(func(): return not own_saved_path.is_empty() and _page_ready(_book(), "load") \
		and main.ui.settings_ui.can_interact() and is_instance_valid(_own_button()), "own save receipt and refreshed native list", 6.0): return false
	if not FileAccess.file_exists(own_saved_path):
		_fail("bookmark_saved did not identify a published file")
		return false
	phase = "saved_bookmark_list"
	book = _book()
	if not await _scroll_to(book, _own_button()): return false
	if not await _shot("02-bookmark-list-own-save"): return false
	if not await _click(book, _own_button(), "review_own_bookmark"): return false
	phase = "bookmark_confirmation"
	if not await _wait_until(_confirmation_ready, "explicit confirmation bound to own path", 2.0): return false
	if not loaded_path.is_empty():
		_fail("Bookmark loaded before the explicit confirmation click")
		return false
	confirmation_observed = true
	_record({"kind": "confirmation_observed", "path": own_saved_path, "load_event_received": false})
	book = _book()
	var confirm := _find_button(book._leaf_body, "载入此书签")
	if not await _scroll_to(book, confirm): return false
	if not await _shot("03-bookmark-load-confirmation"): return false
	phase = "load_confirmed"
	if not await _click(book, confirm, "confirm_load_own_bookmark"): return false
	if not await _wait_until(func(): return not loaded_path.is_empty() and _normal_path(loaded_path) == _normal_path(own_saved_path) \
		and _field_ready() and main.field.get_instance_id() != initial_field_id \
		and int(main._session_generation) > initial_generation, "loaded bookmark's new idle field after view build", 8.0, 180000): return false
	restored_field_id = main.field.get_instance_id()
	phase = "loaded_field"
	_record({"kind": "view_build_observed_complete", "which": "restored", "host": _host_state(),
		"old_field_id": initial_field_id, "new_field_id": restored_field_id, "path": loaded_path,
		"basis": "bookmark_loaded own path, replacement field/ui, loading lock released, idle boundary after draw",
		"production_timing_stage_for_crosscheck": "view_build_end"})
	if not await _shot("04-bookmark-loaded-field"): return false
	return true

func _drain(deadline: int) -> bool:
	while Time.get_ticks_usec() < deadline:
		_poll_archive()
		if archive.IsIdle():
			_poll_archive()
			if archive.IsIdle(): return pending_frames.is_empty() and not archive_failed
		await process_frame
	return false

func _settle_archive() -> void:
	# An exceptional slow filesystem may exceed the planned archive window.
	# Preserve the timeout result, but do not destroy a worker still writing.
	while true:
		_poll_archive()
		if archive.IsIdle():
			_poll_archive()
			if archive.IsIdle(): return
		await process_frame

func _finish(code: int) -> void:
	if finishing: return
	finishing = true
	if archive != null:
		var after := _preferences()
		if not preference_before.is_empty() and preference_before != after:
			_fail("Saved language/settings fingerprints changed during the bookmark observation")
			if code == 0: code = 7
		var drained := await _drain(origin + ARCHIVE_LIMIT_US - 700000)
		if not drained:
			if code == 0: code = 6
			await _settle_archive()
		_record({"kind": "probe_close", "code": code, "fixture": false, "world_actions_selected": 0,
			"own_saved_path": own_saved_path, "loaded_path": loaded_path, "backup_path": backup_path,
			"confirmation_observed": confirmation_observed, "initial_field_id": initial_field_id,
			"restored_field_id": restored_field_id, "paused_native_key_packets_sent": paused_key_packets,
			"raw_paused_input_recording_requires_production_dvlog_review": true,
			"frames_queued": frame_names.size(), "frames_written": frames_written,
			"pending_frames": pending_frames.keys(), "archive_drained": drained,
			"archive_failed": archive_failed, "failures": failures, "native_button_events": pressed,
			"preferences_unchanged": preference_before == after, "host": _host_state(),
			"exit_route": "Sound.quit_cleanly -> Main.wait_for_shutdown -> audio drain",
			"shutdown_completed_at_archive_seal": main_shutdown_seen,
			"production_close_result_reviewed": false}, "probe-summary.dvlog")
		if await _drain(origin + ARCHIVE_LIMIT_US) and not archive_failed:
			for file in STREAMS:
				var error := DirAccess.rename_absolute(output.path_join(file + ".partial"), output.path_join(file))
				if error != OK:
					push_error("Could not publish probe stream " + file + ": " + error_string(error))
					if code == 0: code = 6
		else:
			push_error("Probe archive is incomplete; .partial evidence retained")
			if code == 0: code = 6
			await _settle_archive()
	sealed = true
	print("BOOKMARK_PROBE_SAFE_CLOSE_REQUEST code=", code, " frames=", frames_written, "/", frame_names.size(),
		" own_save=", own_saved_path, " loaded=", loaded_path, " elapsed_us=", Time.get_ticks_usec() - origin)
	# Never quit the tree here. The production close owner waits for Main's C#
	# archive and save work before audio teardown, including when this probe fails.
	await Sound.quit_cleanly(code)
