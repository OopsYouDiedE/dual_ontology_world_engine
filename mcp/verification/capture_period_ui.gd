extends SceneTree
## Level-two presentation review: a real opening excerpt, then isolated production
## UI components. Layout samples are recorded as samples and never reach the Core.
## No route, answer, or world action is selected by this tool.
var target: SubViewport
var archive: Node
var host: Node
var review: Control
var output := ""
var origin := 0
var finishing := false
var host_closed := false
var failures: Array[String] = []
var emitted: Array[Dictionary] = []
var snapshot: Dictionary = {}
var presentation: Dictionary = {}
var original_locale := ""
var saved_settings: Dictionary = {}
var rendered_frames := 0
var archive_failed := false

func _initialize() -> void:
	origin = Time.get_ticks_usec()
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--out="): output = argument.trim_prefix("--out=")
	if output.is_empty() or not output.is_absolute_path() or DirAccess.dir_exists_absolute(output):
		push_error("Use --out=<new absolute directory>; existing evidence is never overwritten.")
		quit(2)
		return
	if DirAccess.make_dir_recursive_absolute(output) != OK:
		quit(2)
		return
	root.size = Vector2i(1280, 720)
	root.title = "安眠者有福 · 印刷界面观察"
	_run.call_deferred()

func _process(_delta: float) -> bool:
	if Time.get_ticks_usec() - origin > 150000000 and not finishing:
		_fail("Presentation review reached its safety deadline.")
		_finish(9)
	return false

func _record(data: Dictionary) -> void:
	if archive == null: return
	data["real_us"] = Time.get_ticks_usec() - origin
	archive.AppendRecords(output.path_join("ui-review.dvlog"), JSON.stringify(data) + "\n")

func _fail(message: String) -> void:
	failures.append(message)
	push_error(message)
	_record({"kind": "review_failure", "message": message})

func _settle(seconds := 0.42) -> void:
	await create_timer(seconds, true, false, true).timeout
	await RenderingServer.frame_post_draw

func _press(key: Key) -> void:
	for down in [true, false]:
		var event := InputEventKey.new()
		event.keycode = key
		event.pressed = down
		_record({"kind": "keyboard_input", "keycode": key, "pressed": down})
		target.push_input(event, true)
	await process_frame

func _click(button: Button) -> void:
	if button == null or button.disabled or not button.is_visible_in_tree():
		_fail("Requested review control is unavailable.")
		return
	var point := button.get_global_rect().get_center()
	for down in [true, false]:
		var event := InputEventMouseButton.new()
		event.button_index = MOUSE_BUTTON_LEFT
		event.button_mask = MOUSE_BUTTON_MASK_LEFT if down else 0
		event.position = point
		event.global_position = point
		event.pressed = down
		_record({"kind": "mouse_input", "control": str(button.get_path()), "position": [point.x, point.y], "pressed": down})
		target.push_input(event, true)
	await process_frame

func _shot(name: String, scope: String) -> void:
	await RenderingServer.frame_post_draw
	var pixels := target.get_texture().get_image()
	if pixels == null or pixels.is_empty():
		_fail("No rendered image: " + name)
		return
	var result := pixels.save_png(output.path_join(name + ".png"))
	if result != OK: _fail("Cannot save " + name + ": " + error_string(result))
	rendered_frames += 1
	_record({"kind": "rendered_frame", "file": name + ".png", "scope": scope, "size": [pixels.get_width(), pixels.get_height()], "locale": TranslationServer.get_locale()})
	print("UI_FRAME ", name, " ", scope)

func _wait_guide() -> void:
	var deadline := Time.get_ticks_usec() + 8000000
	while review.settings_ui.is_active() and not review.settings_ui.can_interact() and Time.get_ticks_usec() < deadline:
		await process_frame
	await _settle(0.15)

func _wait_manuscript_return() -> void:
	var deadline := Time.get_ticks_usec() + 8000000
	while Time.get_ticks_usec() < deadline:
		if not review.settings_ui.is_active() and review.investigation_ui.current_panel() == "replay" and review.investigation_ui.can_interact():
			await _settle(0.1)
			return
		await process_frame
	_fail("The nested guide did not restore a readable manuscript.")

func _ui_event(data: Dictionary) -> void:
	emitted.append(data.duplicate(true))
	_record({"kind": "isolated_ui_event", "event": data})

func _panel_geometry(label: String) -> void:
	if not review.investigation_ui.is_active(): return
	var surface: Control = review.investigation_ui.modal_box
	var rect := surface.get_global_rect()
	var screen := Rect2(Vector2.ZERO, Vector2(target.size))
	var contained := screen.encloses(rect)
	_record({"kind": "sheet_geometry", "label": label, "rect": [rect.position.x, rect.position.y, rect.size.x, rect.size.y], "contained": contained})
	if not contained: _fail("Sheet extends outside the rendered viewport: " + label)

func _preview_setting(section: String, key: String, value: Variant) -> void:
	var settings := root.get_node_or_null("GameSettings")
	if settings == null: return
	var token := section + "/" + key
	if not saved_settings.has(token): saved_settings[token] = settings.get_value(section, key)
	# Presentation preview only. Do not change the player's persisted preferences.
	settings._values[section][key] = value
	settings.setting_changed.emit(section, key, value)
	_record({"kind": "temporary_presentation_setting", "section": section, "key": key, "value": value})

func _restore_settings() -> void:
	var settings := root.get_node_or_null("GameSettings")
	if settings != null:
		for token in saved_settings:
			var parts := str(token).split("/")
			settings._values[parts[0]][parts[1]] = saved_settings[token]
			settings.setting_changed.emit(parts[0], parts[1], saved_settings[token])
	saved_settings.clear()
	if not original_locale.is_empty(): TranslationServer.set_locale(original_locale)

func _close_host() -> void:
	if not is_instance_valid(host): return
	host.begin_shutdown()
	var deadline := Time.get_ticks_usec() + 15000000
	while not host_closed and Time.get_ticks_usec() < deadline:
		await process_frame
	_record({"kind": "opening_session_closed", "completed": host_closed})
	if not host_closed: _fail("Opening session did not finish its shutdown within the review window.")
	target.remove_child(host)
	host.queue_free()
	host = null
	await process_frame

func _run() -> void:
	original_locale = TranslationServer.get_locale()
	var archive_script := load("res://tools/FrameArchive.cs")
	archive = archive_script.new()
	if archive == null:
		push_error("Build the .NET host before running this review.")
		quit(3)
		return
	root.add_child(archive)
	archive.Configure(0)
	_record({"kind": "review_start", "scope": "real opening plus isolated UI presentation; no world actions, route selection or puzzle answers"})
	target = SubViewport.new()
	target.name = "PeriodUIReview"
	target.size = Vector2i(1280, 720)
	target.handle_input_locally = true
	target.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(target)
	var screen := TextureRect.new()
	screen.texture = target.get_texture()
	screen.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	screen.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	screen.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(screen)
	root.show()
	host = load("res://scenes/Host/Main.tscn").instantiate()
	host.shutdown_completed.connect(func(): host_closed = true)
	target.add_child(host)
	await process_frame
	_record({"kind": "public_start_request"})
	host.dispatch({"kind": "start"})
	var opening_deadline := Time.get_ticks_usec() + 45000000
	while Time.get_ticks_usec() < opening_deadline:
		if is_instance_valid(host.ui) and host.ui.dialogue_ui.is_dialogue_active(): break
		await process_frame
	if not is_instance_valid(host.ui) or not host.ui.dialogue_ui.is_dialogue_active():
		_fail("The production opening did not supply a dialogue excerpt.")
		await _shot("01-opening-unavailable", "production opening")
		await _close_host()
		await _finish(4)
		return
	await _settle(0.8)
	snapshot = host.ui.snapshot.duplicate(true)
	presentation = host.ui._trace_display.duplicate(true)
	_record({"kind": "production_excerpt", "snapshot": snapshot, "presentation": presentation})
	if host.ui.dialogue_ui.body_label.visible_ratio < 1.0:
		await _press(KEY_SPACE)
	await _settle(0.3)
	await _shot("01-dialogue-production", "actual opening dialogue")
	await _click(host.ui.dialogue_ui.history_button)
	await _settle()
	await _shot("02-history-production", "actual opening transcript")
	await _press(KEY_ESCAPE)
	await _settle()
	if host.ui.dialogue_ui.is_history_active(): _fail("Escape did not close dialogue history.")
	var backdrop_image := target.get_texture().get_image()
	await _close_host()
	var backdrop := TextureRect.new()
	backdrop.texture = ImageTexture.create_from_image(backdrop_image)
	backdrop.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	backdrop.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	backdrop.mouse_filter = Control.MOUSE_FILTER_IGNORE
	target.add_child(backdrop)
	review = load("res://scenes/UI/UISystem.tscn").instantiate()
	target.add_child(review)
	review.ui_event.connect(_ui_event)
	review.apply_snapshot(snapshot)
	review.hud_ui.hide()
	review.open_manuscript()
	await _settle()
	_panel_geometry("manuscript-1280x720")
	await _shot("03-manuscript-production-data", "isolated manuscript; unchanged actual opening projection")
	var original_scroll: int = review.investigation_ui.modal_scroll.scroll_vertical
	await _press(KEY_F1)
	await _settle(0.7)
	await _wait_guide()
	await _shot("04-nested-guide", "guide opened above manuscript through F1")
	await _press(KEY_ESCAPE)
	await _wait_manuscript_return()
	var returned: bool = review.investigation_ui.current_panel() == "replay" and review.investigation_ui.is_open()
	_record({"kind": "nested_return", "returned_to_manuscript": returned, "scroll_before": original_scroll, "scroll_after": review.investigation_ui.modal_scroll.scroll_vertical, "world_actions_emitted": emitted})
	if not returned: _fail("Closing the nested guide did not return to the manuscript.")
	if not emitted.is_empty(): _fail("Opening or closing a presentation layer emitted an action.")
	await _shot("05-manuscript-return", "native Escape returns to previous sheet")
	review.close_modal(false)
	await _settle()
	var actual_text := ""
	for line in presentation.get("lines", []):
		if line is Dictionary: actual_text += str(line.get("text", "")) + "\n\n"
		else: actual_text += str(line) + "\n\n"
	if actual_text.is_empty(): actual_text = "排版样张 · Typography specimen · 組版見本"
	var sample := actual_text.repeat(10)
	_record({"kind": "layout_sample", "purpose": "long text and independent scrolling", "source": "repeated opening text; not a game record"})
	review._request_modal("manuscript", {"hypotheses": [sample], "layout_sample": true})
	await _settle()
	_panel_geometry("long-manuscript")
	await _shot("06-long-manuscript-layout-sample", "isolated layout sample; repeated source text")
	review.investigation_ui.modal_scroll.scroll_vertical = 180
	await _press(KEY_F1)
	await _settle(0.6)
	await _wait_guide()
	await _press(KEY_ESCAPE)
	await _wait_manuscript_return()
	_record({"kind": "long_sheet_return", "scroll": review.investigation_ui.modal_scroll.scroll_vertical})
	if review.investigation_ui.modal_scroll.scroll_vertical < 170: _fail("Nested return lost a long sheet's reading position.")
	review.close_modal(false)
	await _settle()
	_record({"kind": "layout_sample", "purpose": "wrapping and unavailable choice; no Core connection"})
	review.spatial_choices({"title": "交互排版样张", "site": "layout_sample", "options": [
		{"id": "layout_choice_only", "label": "查看这一行在较窄纸面上的换行与阅读间距，确认完整内容仍能自然读完。", "available": true},
		{"id": "layout_unavailable", "label": "暂不可用的选项 · Unavailable entry", "available": false},
		{"id": "layout_leave", "label": "收起这页记录", "available": true}]})
	await _settle()
	_panel_geometry("choice-wrap")
	await _shot("07-choice-layout-sample", "isolated choice layout sample")
	var first_choice: Button = review.investigation_ui.modal_body.get_child(0)
	await _click(first_choice)
	await _settle()
	if emitted.size() != 1 or str(emitted[0].get("choice_id", "")) != "layout_choice_only":
		_fail("An isolated native choice click did not dispatch exactly its displayed ID.")
	for locale in ["en", "ja"]:
		TranslationServer.set_locale(locale)
		review.open_manuscript()
		await _settle()
		_panel_geometry("manuscript-" + locale)
		await _shot("08-manuscript-" + locale, "localized UI chrome with unchanged source projection")
		review.close_modal(false)
		await _settle()
	TranslationServer.set_locale("zh_CN")
	_preview_setting("accessibility", "reduce_motion", true)
	review.open_manuscript()
	await _settle(0.2)
	_panel_geometry("reduced-motion-manuscript")
	await _shot("09-reduced-motion-manuscript", "temporary reduced-motion preference; no preferences saved")
	review.close_modal(false)
	await _settle(0.1)
	_restore_settings()
	target.size = Vector2i(1600, 900)
	root.size = Vector2i(1600, 900)
	review.open_manuscript()
	await _settle()
	_panel_geometry("manuscript-1600x900")
	await _shot("10-manuscript-1600x900", "larger rendered viewport")
	review.close_modal(false)
	await _settle()
	review.dialogue_ui.play_dialogue(presentation)
	await _settle(0.7)
	if review.dialogue_ui.body_label.visible_ratio < 1.0: await _press(KEY_SPACE)
	await _settle(0.2)
	await _shot("11-dialogue-1600x900", "isolated dialogue using unchanged production excerpt")
	await _finish(0 if failures.is_empty() else 5)

func _finish(code: int) -> void:
	if finishing: return
	finishing = true
	_restore_settings()
	if is_instance_valid(review): review.begin_shutdown()
	if is_instance_valid(host): await _close_host()
	_record({"kind": "review_close", "code": code, "frames": rendered_frames, "failures": failures})
	if archive != null:
		var deadline := Time.get_ticks_usec() + 5000000
		while not archive.IsIdle() and Time.get_ticks_usec() < deadline:
			await process_frame
		for encoded in archive.PollCompleted():
			var result = JSON.parse_string(str(encoded))
			if result is Dictionary and result.get("error") != null and str(result.get("error")) != "":
				archive_failed = true
		if not archive.IsIdle() or archive_failed: code = 6
	print("PERIOD_UI_REVIEW_COMPLETE code=", code, " frames=", rendered_frames, " failures=", failures)
	quit(code)
