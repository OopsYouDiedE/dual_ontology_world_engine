extends SceneTree
## Finite, rendered UI observation. Never loads Main or starts a DV session.
## python mcp/godot_workspace.py --csharp --script res://tools/capture_casebook_ui.gd --
##   --outpath=<new absolute directory> [--frames]
## The source excerpts below are explicitly synthetic presentation inputs, not
## gameplay acquisition. Language previews never call persistent setters.

const SOFT_LIMIT_US := 41000000
const HARD_LIMIT_US := 44000000
const FRAME_INTERVAL_US := 80000
const RESOLUTION := Vector2i(1280, 992)
const FIXTURE_ID := "casebook-ui-presentation-v1"
const EVIDENCE_ID := "ui-fixture:c1_visible_E2"
const SLATE_ID := "ui-fixture:c1_visible_E7"
const PERSON_ID := "ui-fixture:person:huo"
const GALLERY_ID := "ui-fixture:gallery:slate"
const SOURCE_DV := "dv_core/modules/40-chapter1.dv"
const TITLE_SCENE := "res://scenes/UI/TitleUI.tscn"
const CASEBOOK_SCRIPT := "res://scenes/UI/CasebookUI.gd"
const DATA_SCRIPT := "res://scenes/UI/CasebookData.gd"
const INVESTIGATION_SCENE := "res://scenes/UI/InvestigationUI.tscn"
const ARCHIVE_SCRIPT := "res://tools/FrameArchive.cs"
# STORY-HARDCODE[CASEBOOK-UI-CAPTURE][text][fixture]
# Exact observer prose supplied by the coordinating task from production DV.
const E2_LINES := [
	"门内地面铺着一片扇形的灰，窄端贴着门缝，向门厅里散开。",
	"东南墙角躺着一枚木楔：上面一层灰，底面干净。楔尖有新的磨痕。",
]
const E7_TEXT := "一块小石板，粉笔速记两行。\n第一行：主楼：⊥门 ✕开 ◎\n第二行：一个小小的放大镜图样，后面是“⊥✓”。"
const NOTE_TITLE := "门内观察"
const NOTE_TEXT := "复核门缝灰迹与木楔磨痕。"
const NOTE_EDITED := "复核门缝灰迹与木楔磨痕。\n仍需现场复核。"

var output := ""
var origin := 0
var continuous_frames := false
var target: SubViewport
var book: Control
var casebook: CanvasLayer
var investigation: CanvasLayer
var model: RefCounted
var archive: Node
var pointer := Vector2(640, 496)
var phase := "initializing"
var original_locale := ""
var original_paused := false
var preference_before: Dictionary = {}
var emitted: Array[Dictionary] = []
var pressed: Array[Dictionary] = []
var watched: Dictionary = {}
var pending_frames: Dictionary = {}
var frame_names: Dictionary = {}
var failures: Array[String] = []
var observations: Array[Dictionary] = []
var frame_count := 0
var frames_written := 0
var archive_failed := false
var finishing := false
var hard_stopped := false
var note_id := ""

func _initialize() -> void:
	origin = Time.get_ticks_usec()
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--outpath="): output = argument.trim_prefix("--outpath=")
		elif argument.begins_with("--out="): output = argument.trim_prefix("--out=")
		elif argument == "--frames": continuous_frames = true
		else:
			push_error("Unknown capture argument: " + argument)
			quit(2)
			return
	if output == "" or not output.is_absolute_path() or DirAccess.dir_exists_absolute(output) or FileAccess.file_exists(output):
		push_error("Use --outpath=<new absolute directory>; existing evidence is never overwritten")
		quit(2)
		return
	if DisplayServer.get_name() == "headless" or not is_equal_approx(Engine.time_scale, 1.0):
		push_error("Capture requires a rendering window and real-time Engine.time_scale = 1")
		quit(2)
		return
	for argument in OS.get_cmdline_args():
		if argument.begins_with("--fixed-fps") or argument.begins_with("--write-movie"):
			push_error("Fixed-step/movie execution is not a real-time UI capture")
			quit(2)
			return
	var error := DirAccess.make_dir_recursive_absolute(output)
	if error != OK:
		push_error("Cannot create capture directory: " + error_string(error))
		quit(2)
		return
	root.size = RESOLUTION
	root.title = "安眠者有福 · 书本 UI 观察"
	_run.call_deferred()

func _process(_delta: float) -> bool:
	if hard_stopped: return false
	var elapsed := Time.get_ticks_usec() - origin
	if elapsed >= HARD_LIMIT_US:
		hard_stopped = true
		_restore_ephemeral_state()
		push_error("UI capture reached its 44-second hard deadline; partial evidence retained")
		quit(9)
	elif elapsed >= SOFT_LIMIT_US and not finishing:
		_fail("UI capture exceeded its 41-second work budget")
		_finish(9)
	return false

func _plain(value: Variant) -> Variant:
	if value is Vector2 or value is Vector2i: return [value.x, value.y]
	if value is Vector3 or value is Vector3i: return [value.x, value.y, value.z]
	if value is Dictionary:
		var result := {}
		for key in value: result[str(key)] = _plain(value[key])
		return result
	if value is Array:
		var result: Array = []
		for item in value: result.append(_plain(item))
		return result
	return value

func _record(value: Dictionary, manifest := false) -> void:
	if archive == null or hard_stopped: return
	value.real_us = Time.get_ticks_usec() - origin
	var file := "capture-manifest.dvlog.partial" if manifest else "ui-input.dvlog.partial"
	# JSON is only the in-memory bridge to the existing checked binary writer.
	archive.AppendRecords(output.path_join(file), JSON.stringify(_plain(value)) + "\n")

func _fail(message: String) -> void:
	failures.append(message)
	push_error(message)
	_record({"kind": "failure", "phase": phase, "message": message})

func _observe(value: Dictionary) -> void:
	observations.append(value.duplicate(true))
	_record(value)

func _on_ui_event(event: Dictionary) -> void:
	var row := {"scope": phase, "event": event.duplicate(true)}
	emitted.append(row)
	_record({"kind": "ui_event", "scope": phase, "event": event})

func _on_pressed(id: int, path: String) -> void:
	var row := {"instance_id": id, "path": path}
	pressed.append(row)
	_record({"kind": "control_pressed", "control": row})

func _preferences() -> Dictionary:
	var result := {}
	for path in ["user://language.cfg", "user://settings.cfg"]:
		var exists := FileAccess.file_exists(path)
		result[path] = {"exists": exists, "sha256": FileAccess.get_sha256(path) if exists else ""}
	return result

func _source_hashes() -> Dictionary:
	var result := {}
	for path in [TITLE_SCENE, CASEBOOK_SCRIPT, DATA_SCRIPT, INVESTIGATION_SCENE, ARCHIVE_SCRIPT,
			"res://scenes/UI/TitleUI.gd", "res://scenes/UI/BookArchivePage.gd",
			"res://scenes/UI/BookSettingsPage.gd", "res://scenes/UI/DeskStage3D.gd",
			"res://scenes/UI/BookLeafTurn3D.gd", "res://scenes/UI/book_leaf_mesh_3d.gdshader",
			"res://scenes/UI/magnifier_lens.gdshader", "res://tools/capture_casebook_ui.gd"]:
		result[path] = FileAccess.get_sha256(path) if FileAccess.file_exists(path) else "unavailable"
	var source_path := str(ProjectSettings.get_setting("duelview_tools/repository")).path_join(SOURCE_DV)
	result[SOURCE_DV] = FileAccess.get_sha256(source_path) if FileAccess.file_exists(source_path) else "unavailable"
	return result

func _poll_archive() -> void:
	if archive == null: return
	for encoded in archive.PollCompleted():
		var result: Variant = JSON.parse_string(str(encoded))
		if not result is Dictionary:
			archive_failed = true
			push_error("Invalid FrameArchive completion")
			continue
		if result.get("error") != null and str(result.get("error")) != "":
			archive_failed = true
			push_error("FrameArchive: " + str(result))
			if result.get("kind", "") != "text-error": _record({"kind": "frame_write_error", "result": result})
			continue
		var file := str(result.get("file", ""))
		if pending_frames.has(file):
			var index: Dictionary = pending_frames[file]
			pending_frames.erase(file)
			index.kind = "frame_written"
			index.archive = result
			frames_written += 1
			_record(index)

func _page_texts(node: Node, result: Array) -> void:
	if node is Control and node.is_visible_in_tree():
		if node is Label or node is RichTextLabel or node is BaseButton or node is LineEdit or node is TextEdit:
			var rect: Rect2 = node.get_global_rect()
			result.append({"path": str(node.get_path()), "text": str(node.get("text")),
				"page_rect": [rect.position.x, rect.position.y, rect.size.x, rect.size.y]})
	for child in node.get_children(): _page_texts(child, result)

func _frame_state() -> Dictionary:
	var result := {"scope": phase, "locale": TranslationServer.get_locale(), "tree_paused": paused,
		"pointer": pointer, "fixture_id": FIXTURE_ID, "ui_only_fixture": true}
	if is_instance_valid(book):
		var stage: Node3D = book._desk
		var camera: Camera3D = stage.camera
		result.merge({"page": book._current_page, "turning": book._turning, "open_amount": book._open_amount,
			"mode": book.mode, "reduce_motion": book._reduce_motion,
			"camera_position": camera.position, "camera_rotation_degrees": camera.rotation_degrees,
			"camera_fov_degrees": camera.fov, "stage_pointer": stage.pointer,
			"light_energy": stage.lamp_light.light_energy, "wind": stage.wind,
			"light_intensity": stage.light_intensity,
			"cover_progress": book._cover_leaf.progress,
			"frozen_cover_update_mode": book._cover_snapshot_viewport.render_target_update_mode,
			"frozen_page_update_mode": book._snapshot_viewport.render_target_update_mode})
		if is_instance_valid(book._page_leaf): result.page_progress = book._page_leaf.progress
		if stage.has_method("get_magnifier_optics"): result.magnifier_optics = stage.get_magnifier_optics()
		var texts: Array = []
		_page_texts(book._cover_root, texts)
		# These are native control contents; clipping/occlusion is judged in PNGs.
		result.page_controls = texts
	if is_instance_valid(investigation) and investigation.is_active():
		result.investigation_panel = investigation.current_panel()
	return result

func _shot(name: String) -> bool:
	if finishing or hard_stopped: return false
	if frame_names.has(name):
		_fail("Duplicate frame name: " + name)
		return false
	var deadline := Time.get_ticks_usec() + 1100000
	while not archive.HasCapacity():
		_poll_archive()
		if finishing or hard_stopped: return false
		if Time.get_ticks_usec() >= deadline:
			_fail("PNG backpressure exceeded 1.1 seconds at " + name)
			return false
		await process_frame
	await RenderingServer.frame_post_draw
	if finishing or hard_stopped: return false
	var captured := Time.get_ticks_usec() - origin
	var pixels: Image = target.get_texture().get_image()
	if pixels == null or pixels.is_empty():
		_fail("No rendered image at " + name)
		return false
	pixels.convert(Image.FORMAT_RGBA8)
	var row := _frame_state()
	row.file = name + ".png"
	row.captured_us = captured
	pending_frames[row.file] = row
	frame_names[name] = true
	archive.Enqueue(pixels.get_data(), pixels.get_width(), pixels.get_height(), output.path_join(row.file), captured)
	frame_count += 1
	_record({"kind": "frame_queued", "file": row.file, "captured_us": captured})
	_poll_archive()
	print("UI_FRAME ", name, " at_us=", captured)
	return not archive_failed

func _move(point: Vector2) -> void:
	var event := InputEventMouseMotion.new()
	event.position = point
	event.global_position = point
	event.relative = point - pointer
	pointer = point
	_record({"kind": "mouse_motion", "position": point, "relative": event.relative})
	target.push_input(event, true)

func _mouse_button(point: Vector2, index: MouseButton, down: bool, label: String) -> void:
	var event := InputEventMouseButton.new()
	event.position = point
	event.global_position = point
	event.button_index = index
	event.button_mask = MOUSE_BUTTON_MASK_LEFT if down and index == MOUSE_BUTTON_LEFT else 0
	event.pressed = down
	_record({"kind": "mouse_button", "position": point, "button_index": index, "pressed": down, "target": label})
	target.push_input(event, true)

func _scroll_to(control: Control) -> bool:
	if not is_instance_valid(book) or not book._leaf_scroll.is_ancestor_of(control): return true
	for attempt in range(18):
		if finishing or hard_stopped or not is_instance_valid(control): return false
		var clip: Rect2 = book._leaf_scroll.get_global_rect().grow(-12)
		var center := control.get_global_rect().get_center()
		if clip.has_point(center): return true
		var point: Vector2 = book.project_button(book._leaf_scroll)
		_move(point)
		var direction := MOUSE_BUTTON_WHEEL_DOWN if center.y > clip.end.y else MOUSE_BUTTON_WHEEL_UP
		_mouse_button(point, direction, true, "page_scroll")
		_mouse_button(point, direction, false, "page_scroll")
		await process_frame
		await process_frame
	_fail("Could not scroll the native page to " + str(control.name))
	return false

func _click(control: Control, label: String, projected := true) -> bool:
	if finishing or hard_stopped or not is_instance_valid(control) or not control.is_visible_in_tree():
		_fail("Unavailable control for native input: " + label)
		return false
	if control is BaseButton and control.disabled:
		_fail("Disabled control for native input: " + label)
		return false
	if projected and not await _scroll_to(control): return false
	var id := control.get_instance_id()
	if control is BaseButton and not watched.has(id):
		control.pressed.connect(_on_pressed.bind(id, str(control.get_path())))
		watched[id] = true
	var point: Vector2 = book.project_button(control) if projected else control.get_global_transform_with_canvas() * (control.size * 0.5)
	_move(point)
	await create_timer(0.07, true, false, true).timeout
	if finishing or hard_stopped or not is_instance_valid(control): return false
	point = book.project_button(control) if projected else control.get_global_transform_with_canvas() * (control.size * 0.5)
	if not Rect2(Vector2.ZERO, Vector2(RESOLUTION)).has_point(point):
		_fail("Projected input target is outside capture viewport: " + label)
		return false
	_move(point)
	var before := pressed.size()
	_mouse_button(point, MOUSE_BUTTON_LEFT, true, label)
	_mouse_button(point, MOUSE_BUTTON_LEFT, false, label)
	if control is BaseButton and (pressed.size() != before + 1 or int(pressed.back().instance_id) != id):
		_fail("Native input did not activate exactly the intended button: " + label)
		return false
	return true

func _key(key: Key, unicode_value := 0, ctrl := false) -> void:
	for down in [true, false]:
		var event := InputEventKey.new()
		event.keycode = key
		event.unicode = unicode_value
		event.ctrl_pressed = ctrl
		event.pressed = down
		_record({"kind": "key", "keycode": key, "unicode": unicode_value, "ctrl": ctrl, "pressed": down})
		target.push_input(event, true)

func _type(control: Control, text: String, label: String) -> bool:
	if not await _click(control, label): return false
	if book.viewport.gui_get_focus_owner() != control:
		_fail("Native editor did not receive focus: " + label)
		return false
	_key(KEY_A, 0, true)
	for index in text.length():
		var character := text.unicode_at(index)
		_key(KEY_ENTER if character == 10 else KEY_NONE, 0 if character == 10 else character)
	await process_frame
	if finishing or hard_stopped or not is_instance_valid(control): return false
	if str(control.get("text")) != text:
		_fail("Native text input did not preserve the requested draft: " + label)
		return false
	return true

func _find_button(node: Node, caption: String) -> Button:
	if node is Button and node.text == caption: return node
	for child in node.get_children():
		var found := _find_button(child, caption)
		if found != null: return found
	return null

func _archive_page() -> Node:
	return book._leaf_body.get_node_or_null("ArchivePage")

func _index_button(section: String) -> Button:
	var labels := {"evidence": "证据", "people": "人物", "gallery": "图版", "hypotheses": "猜想"}
	return _find_button(book._leaf_body, "%s\n%02d" % [TranslationServer.translate(labels[section]), model.list_section(section).size()])

func _await_page(expected: String, prefix: String, key_angles := false) -> bool:
	var deadline := Time.get_ticks_usec() + 3600000
	var checkpoints: Array[float] = [0.16, 0.40, 0.63, 0.85]
	var checkpoint := 0
	var sequence := 0
	var next_sample := Time.get_ticks_usec()
	var saw_animation := false
	while is_instance_valid(book):
		if finishing or hard_stopped: return false
		if Time.get_ticks_usec() >= deadline:
			_fail("Book did not land on " + expected + " during " + prefix)
			return false
		var page_ready: bool = not book._turning and str(book._current_page) == expected and not book.viewport.gui_disable_input
		var entry_ready: bool = not book._page_motion.is_transitioning()
		var cover_ready: bool = expected != "cover" or book._menu_input_enabled
		if page_ready and entry_ready and cover_ready: break
		if book._turning:
			saw_animation = true
			var leaf: Node = book._page_leaf if is_instance_valid(book._page_leaf) else book._cover_leaf
			var progress := float(leaf.progress)
			var sample := continuous_frames and Time.get_ticks_usec() >= next_sample
			if key_angles and checkpoint < checkpoints.size() and progress >= checkpoints[checkpoint]:
				sample = true
				checkpoint += 1
			if sample:
				if not await _shot("%s-turn-%03d" % [prefix, sequence]): return false
				sequence += 1
				next_sample = Time.get_ticks_usec() + FRAME_INTERVAL_US
		await process_frame
	if not is_instance_valid(book):
		_fail("Book was removed before page landed: " + prefix)
		return false
	await process_frame
	await process_frame
	_observe({"kind": "page_landed", "page": book._current_page, "expected": expected,
		"scope": phase, "saw_turning_state": saw_animation, "turn_frames": sequence,
		"reduce_motion": book._reduce_motion, "key_angles_requested": key_angles})
	return true

func _navigate(button: Button, expected: String, name: String, key_angles := false, capture := true) -> bool:
	if not await _click(button, name): return false
	if not await _await_page(expected, name, key_angles): return false
	return await _shot(name) if capture else true

func _preview_locale(code: String, shot_name: String) -> bool:
	if finishing or hard_stopped: return false
	TranslationServer.set_locale(code)
	_record({"kind": "locale_preview", "requested": code, "actual": TranslationServer.get_locale(),
		"method": "TranslationServer.set_locale", "language_button_clicked": false, "persistent_setter_used": false})
	await process_frame
	await process_frame
	return await _shot(shot_name) if shot_name != "" else true

func _make_fixture() -> void:
	model = load(DATA_SCRIPT).new()
	var source_refs := [{"source_file": SOURCE_DV, "source_id": "c1_visible_E2", "source_line": 159}]
	model.apply_snapshot({"viewer": "ui-only-fixture-observer", "cast": {"huo": "engineer"},
		"casebook": {"viewer": "ui-only-fixture-observer", "people": [
			{"id": PERSON_ID, "title": "霍铎", "text": "", "speaker": "huo", "acquired": true,
				"image": "res://ShareAsset/Internal/Characters/DialogueCast/engineer_calm.tres", "related": []}],
			"gallery": [{"id": GALLERY_ID, "title": "石板速记（图版）", "text": E7_TEXT,
				"image": "res://ShareAsset/Internal/Props/AwakeningEvidence/slate.png",
				"related": [SLATE_ID], "acquired": true, "source": "c1_visible_E7"}]}})
	var lines: Array = []
	for index in E2_LINES.size():
		lines.append({"text": E2_LINES[index], "source_id": "c1_visible_E2",
			"fixture_excerpt_index": index, "source_refs": source_refs.duplicate(true)})
	model.record_presentation({"visible": true, "status": "applied", "execution_event": EVIDENCE_ID,
		"action_id": "ui-fixture:inspect:E2", "action_kind": "inspect", "voice": "visible-inspection", "source_refs": source_refs}, lines)
	model.record_presentation({"visible": true, "status": "applied", "execution_event": SLATE_ID,
		"title": "石板速记", "action_id": "ui-fixture:inspect:E7", "action_kind": "inspect", "voice": "visible-inspection",
		"source_refs": [{"source_file": SOURCE_DV, "source_id": "c1_visible_E7", "source_line": 199}]},
		[{"text": E7_TEXT, "source_id": "c1_visible_E7", "fixture_excerpt_index": 0}])
	_record({"kind": "fixture_installed", "fixture_id": FIXTURE_ID, "ui_only_fixture": true,
		"synthetic_status": "visible/applied and acquired flags are UI fixture inputs, not gameplay receipts",
		"model": model.export_data()})

func _fixture_manifest() -> Dictionary:
	return {"fixture_id": FIXTURE_ID, "ui_only_fixture": true,
		"scope": "production UI presentation only; no Main, GameEngine, DV/Core/session or acquisition test",
		"prose": [{"source_file": SOURCE_DV, "source_id": "c1_visible_E2", "source_line": 159, "text": E2_LINES},
			{"source_file": SOURCE_DV, "source_id": "c1_visible_E7", "source_line": 199, "text": E7_TEXT}],
		"projected_images": [{"id": PERSON_ID, "speaker": "huo", "title": "霍铎", "resource": "res://ShareAsset/Internal/Characters/DialogueCast/engineer_calm.tres"},
			{"id": GALLERY_ID, "title": "石板速记（图版）", "resource": "res://ShareAsset/Internal/Props/AwakeningEvidence/slate.png"}],
		"status_boundary": "The harness supplies visible/applied and acquired values solely to exercise presentation. No formal execution or unlocking is claimed.",
		"note_boundary": "Personal test writing is entered by native keyboard input and stays in an in-memory CasebookData; no save-slot or session persistence test."}

func _run() -> void:
	original_locale = TranslationServer.get_locale()
	original_paused = paused
	preference_before = _preferences()
	var archive_script := load(ARCHIVE_SCRIPT)
	if archive_script == null:
		_fail("FrameArchive unavailable; build the Godot .NET project before capture")
		await _finish(3)
		return
	archive = archive_script.new()
	if archive == null:
		_fail("FrameArchive could not be instantiated")
		await _finish(3)
		return
	root.add_child(archive)
	archive.process_mode = Node.PROCESS_MODE_ALWAYS
	archive.Configure(Time.get_ticks_usec() - origin)
	var manifest := _fixture_manifest()
	manifest.merge({"kind": "capture_start", "resolution": RESOLUTION, "soft_limit_us": SOFT_LIMIT_US,
		"hard_limit_us": HARD_LIMIT_US, "real_time_frames": continuous_frames, "frame_interval_us": FRAME_INTERVAL_US,
		"source_sha256": _source_hashes(), "preference_before": preference_before, "original_locale": original_locale,
		"locale_test": "temporary TranslationServer relabel only, not persistent language-button activation"})
	_record(manifest.duplicate(true))
	_record(manifest, true)
	target = SubViewport.new()
	target.name = "CasebookUICapture"
	target.size = RESOLUTION
	target.process_mode = Node.PROCESS_MODE_ALWAYS
	target.handle_input_locally = true
	target.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(target)
	var presentation := TextureRect.new()
	presentation.texture = target.get_texture()
	presentation.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	presentation.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	presentation.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(presentation)
	root.show()
	var succeeded := await _title_capture()
	if succeeded and not finishing: succeeded = await _casebook_capture()
	if succeeded and not finishing: succeeded = await _reasoning_capture()
	await _finish(0 if succeeded else 4)

func _title_capture() -> bool:
	phase = "title"
	TranslationServer.set_locale("zh_CN")
	var packed := load(TITLE_SCENE) as PackedScene
	if packed == null:
		_fail("Production TitleUI scene is unavailable")
		return false
	book = packed.instantiate()
	book.ui_event.connect(_on_ui_event)
	target.add_child(book)
	if not await _await_page("cover", "title-ready"): return false
	_move(Vector2(640, 496))
	await create_timer(0.25, true, false, true).timeout
	if not await _shot("title-cover-lens"): return false
	_move(Vector2(75, 100))
	await create_timer(0.22, true, false, true).timeout
	if not await _shot("title-cover-lens-left"): return false
	_move(Vector2(1205, 892))
	await create_timer(0.22, true, false, true).timeout
	if not await _shot("title-cover-lens-right"): return false
	if not await _navigate(book.settings_button, "settings", "title-settings-zh_CN", true): return false
	for locale in ["en", "ja"]:
		if not await _preview_locale(locale, "title-settings-" + locale): return false
	if not await _preview_locale("zh_CN", ""): return false
	if not await _navigate(book._leaf_back, "cover", "title-cover-return", true): return false
	book.queue_free()
	book = null
	await process_frame
	return true

func _casebook_capture() -> bool:
	phase = "casebook"
	_make_fixture()
	casebook = load(CASEBOOK_SCRIPT).new()
	casebook.archive = model
	casebook.ui_event.connect(_on_ui_event)
	casebook.reading_changed.connect(func(active: bool): _record({"kind": "reading_changed", "active": active, "tree_paused": paused}))
	target.add_child(casebook)
	_record({"kind": "harness_open", "section": "index", "entry": "CasebookUI.open", "previous_paused": paused})
	casebook.open("index")
	book = casebook.book
	if not await _await_page("index", "casebook-open", true): return false
	if not paused:
		_fail("Opening CasebookUI did not pause its SceneTree")
		return false
	if not await _shot("casebook-index"): return false
	if not await _navigate(_find_button(book._leaf_body, TranslationServer.translate("凡例与设置")), "settings", "casebook-settings-zh_CN"): return false
	for locale in ["en", "ja"]:
		if not await _preview_locale(locale, "casebook-settings-" + locale): return false
	if not await _preview_locale("zh_CN", ""): return false
	if not await _navigate(book._leaf_index, "index", "index-before-evidence", false, false): return false
	if not await _navigate(_index_button("evidence"), "evidence", "casebook-evidence"): return false
	var evidence: Dictionary = model.find_entry(EVIDENCE_ID)
	if not await _navigate(_find_button(book._leaf_body, "01　" + str(evidence.title)), "entry:" + EVIDENCE_ID, "casebook-evidence-detail", true): return false
	if not await _navigate(_find_button(book._leaf_body, TranslationServer.translate("据此写下猜想")), "write:" + EVIDENCE_ID, "casebook-note-editor", false, false): return false
	var editor := _archive_page()
	if not await _type(editor.get_node("NoteTitle"), NOTE_TITLE, "note-title"): return false
	if not await _type(editor.get_node("NoteText"), NOTE_TEXT, "note-text"): return false
	if not await _shot("casebook-note-draft-zh_CN"): return false
	if not await _preview_locale("ja", "casebook-note-draft-ja"): return false
	editor = _archive_page()
	if str(editor.get_node("NoteTitle").text) != NOTE_TITLE or str(editor.get_node("NoteText").text) != NOTE_TEXT:
		_fail("Locale refresh lost the unsaved native note draft")
		return false
	if not await _preview_locale("zh_CN", ""): return false
	editor = _archive_page()
	if not await _click(editor.get_node("SaveNote"), "save-note"): return false
	if model.notes.size() != 1:
		_fail("Saving the native note editor did not create one personal note")
		return false
	note_id = str(model.notes[0].id)
	if not await _await_page("entry:" + note_id, "note-save"): return false
	if not _check_note(NOTE_TEXT): return false
	if not await _shot("casebook-note-saved"): return false
	for specification in [["people", PERSON_ID, "霍铎", "casebook-person-portrait"], ["gallery", GALLERY_ID, "石板速记（图版）", "casebook-gallery-image"]]:
		if not await _navigate(book._leaf_index, "index", "index-before-" + specification[0], false, false): return false
		if not await _navigate(_index_button(specification[0]), specification[0], "casebook-" + specification[0]): return false
		if not await _navigate(_find_button(book._leaf_body, "01　" + specification[2]), "entry:" + specification[1], specification[3]): return false
	if not await _navigate(book._leaf_index, "index", "index-before-hypotheses", false, false): return false
	if not await _navigate(_index_button("hypotheses"), "hypotheses", "casebook-hypotheses"): return false
	if not await _navigate(_find_button(book._leaf_body, "01　" + NOTE_TITLE), "entry:" + note_id, "casebook-linked-note"): return false
	if not await _navigate(_find_button(book._leaf_body, TranslationServer.translate("编辑猜想")), "write:" + note_id, "casebook-note-edit", false, false): return false
	editor = _archive_page()
	if not await _type(editor.get_node("NoteText"), NOTE_EDITED, "note-edit-text"): return false
	if not await _navigate(editor.get_node("SaveNote"), "entry:" + note_id, "casebook-note-edited"): return false
	if not _check_note(NOTE_EDITED): return false
	if not await _leave_casebook("after-note-save"): return false
	_record({"kind": "harness_open", "section": "hypotheses", "entry": "CasebookUI.open", "previous_paused": paused})
	casebook.open("hypotheses")
	book = casebook.book
	if not await _await_page("hypotheses", "casebook-reopen"): return false
	if not await _shot("casebook-hypotheses-reopened"): return false
	if not _check_note(NOTE_EDITED): return false
	if not await _navigate(_find_button(book._leaf_body, "01　" + NOTE_TITLE), "entry:" + note_id, "casebook-note-reopened"): return false
	if not await _navigate(_find_button(book._leaf_body, TranslationServer.translate("编辑猜想")), "write:" + note_id, "casebook-reasoning-link", false, false): return false
	var before := emitted.size()
	if not await _click(_find_button(book._leaf_body, TranslationServer.translate("打开推演界面")), "open-reasoning"): return false
	if emitted.size() != before + 1 or str(emitted.back().event.get("kind", "")) != "open_reasoning":
		_fail("The native reasoning link did not emit open_reasoning")
		return false
	return await _leave_casebook("before-independent-reasoning")

func _check_note(expected_text: String) -> bool:
	var note: Dictionary = model.find_entry(note_id)
	var related: Array = note.get("related", [])
	if model.notes.size() != 1 or str(note.get("text", "")) != expected_text or str(note.get("title", "")) != NOTE_TITLE or related != [EVIDENCE_ID] or not bool(note.get("personal", false)):
		_fail("Native note save/edit/reopen changed its text, identity or evidence link")
		return false
	_observe({"kind": "personal_note_observation", "note": note.duplicate(true), "storage": "in-memory CasebookData only"})
	return true

func _leave_casebook(label: String) -> bool:
	# Detail/editor back returns one page. The visible index action clears that
	# history; only the index's return action closes the shared book.
	if not await _click(book._leaf_index, "return-to-index:" + label): return false
	if not await _await_page("index", "index-before-close-" + label): return false
	if not await _click(book._leaf_back, "return-to-field:" + label): return false
	var deadline := Time.get_ticks_usec() + 1600000
	while casebook.is_active():
		if finishing or hard_stopped: return false
		if Time.get_ticks_usec() >= deadline:
			_fail("CasebookUI did not close after its native return button")
			return false
		await process_frame
	book = null
	_observe({"kind": "book_closed", "label": label, "tree_paused": paused, "expected_paused": original_paused})
	if paused != original_paused:
		_fail("CasebookUI did not restore the previous SceneTree pause state")
		return false
	return true

func _reasoning_capture() -> bool:
	phase = "independent-investigation"
	var packed := load(INVESTIGATION_SCENE) as PackedScene
	if packed == null:
		_fail("Production InvestigationUI is unavailable")
		return false
	investigation = packed.instantiate()
	target.add_child(investigation)
	investigation.restore_previous_focus = false
	_record({"kind": "harness_open", "entry": "InvestigationUI.open_manuscript", "ui_only_fixture": true,
		"boundary": "Independent production modal with personal UI draft only; this harness does not claim UISystem/Main dispatch or a DV reasoning result"})
	investigation.open_manuscript({"personal_notes": model.notes.duplicate(true)})
	var deadline := Time.get_ticks_usec() + 1600000
	while not investigation.can_interact():
		if finishing or hard_stopped: return false
		if Time.get_ticks_usec() >= deadline:
			_fail("InvestigationUI did not become interactive")
			return false
		await process_frame
	if not await _shot("independent-reasoning-workbench"): return false
	if not await _click(investigation.modal_close_button, "close-independent-reasoning", false): return false
	deadline = Time.get_ticks_usec() + 1600000
	while investigation.is_active():
		if finishing or hard_stopped: return false
		if Time.get_ticks_usec() >= deadline:
			_fail("Independent InvestigationUI did not close")
			return false
		await process_frame
	_observe({"kind": "reasoning_closed", "tree_paused": paused, "book_active": casebook.is_active(),
		"manuscript_owner": "InvestigationUI", "book_page_created": false})
	return true

func _restore_ephemeral_state() -> void:
	if is_instance_valid(casebook): casebook.begin_shutdown()
	if is_instance_valid(investigation): investigation.begin_shutdown()
	paused = original_paused
	if original_locale != "": TranslationServer.set_locale(original_locale)

func _drain(deadline: int) -> bool:
	while archive != null:
		_poll_archive()
		if archive.IsIdle():
			_poll_archive()
			if archive.IsIdle(): return true
		if Time.get_ticks_usec() >= deadline or hard_stopped: return false
		await process_frame
	return true

func _finish(code: int) -> void:
	if finishing: return
	finishing = true
	_restore_ephemeral_state()
	if archive == null:
		quit(code if code != 0 else 3)
		return
	var after := _preferences()
	if preference_before != after:
		_fail("Saved language/settings fingerprints changed during UI observation")
		if code == 0: code = 7
	var deadline := mini(origin + HARD_LIMIT_US - 200000, Time.get_ticks_usec() + 2400000)
	var drained := await _drain(deadline)
	if not drained or archive_failed or not pending_frames.is_empty():
		if code == 0: code = 6
	var close := {"kind": "capture_close", "code": code, "fixture_id": FIXTURE_ID, "ui_only_fixture": true,
		"frames_queued": frame_count, "frames_written": frames_written, "pending_frames": pending_frames.keys(),
		"archive_drained_before_close": drained, "archive_failed": archive_failed, "failures": failures,
		"observations": observations, "ui_events": emitted, "native_button_events": pressed,
		"locale_restored": TranslationServer.get_locale(), "preference_before": preference_before,
		"preference_after": after, "saved_preferences_unchanged_before_exit": preference_before == after,
		"gameplay_acquisition_test": false, "session_persistence_test": false, "uisystem_dispatch_test": false}
	_record(close.duplicate(true))
	_record(close, true)
	if not await _drain(deadline) or archive_failed:
		if code == 0: code = 6
	else:
		for file in ["ui-input.dvlog", "capture-manifest.dvlog"]:
			var error := DirAccess.rename_absolute(output.path_join(file + ".partial"), output.path_join(file))
			if error != OK:
				push_error("Could not publish " + file + ": " + error_string(error))
				if code == 0: code = 6
	print("CASEBOOK_UI_CAPTURE_COMPLETE code=", code, " frames=", frames_written, "/", frame_count, " elapsed_us=", Time.get_ticks_usec() - origin)
	quit(code)
