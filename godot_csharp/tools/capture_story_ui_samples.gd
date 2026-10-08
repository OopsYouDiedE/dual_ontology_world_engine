extends "res://tools/capture_player_transcript.gd"
## Level-two UI samples from an existing engine transcript. This is a production
## renderer replay, not a playthrough. It never dispatches Core actions.
## Godot --path godot_csharp --script res://tools/capture_story_ui_samples.gd -- --round=round-01
## Optional: --width=1280 --height=992. A renderer is required; never use --headless.

const SAMPLE_ROOT := "res://../artifacts/basic-ui-system/ui-rounds/"
const SAMPLE_SPECS := [
	{"slug": "01-c1-long-dialogue", "kind": "lines", "line_prefix": "s2_meet.", "prefer_longest": true},
	{"slug": "02-c1-person-dialogue", "kind": "lines", "line_prefix": "s2_tea.", "require_speaker": true, "prefer_longest": true},
	{"slug": "03-c1-evidence-card", "kind": "card", "card_id": "E1"},
	{"slug": "04-c1-defense-form", "kind": "form", "layout": "tabs", "field_ids": ["say", "show"]},
	{"slug": "05-c2-short-dialogue", "kind": "lines", "line_prefix": "chapter2.noon_dispute.", "require_speaker": true, "prefer_shortest": true},
	{"slug": "06-c2-council-options", "kind": "candidates", "option_prefix": "support:"},
	{"slug": "07-ending", "kind": "ending", "prefer_last": true},
]

var _round := ""
var _sample_dir := ""
var _sample_manifest: Array = []
var _sample_slug := ""
var _viewport_size := Vector2i(1280, 992)

func _run() -> void:
	if DisplayServer.get_name() == "headless":
		_fail("Level-two screenshots require the production renderer, not --headless.")
		return
	if not _parse_options(): return
	if not _read_packets(): return
	var selected := _select_samples()
	if _failed: return
	_sample_dir = SAMPLE_ROOT + _round + "/"
	var absolute_dir := ProjectSettings.globalize_path(_sample_dir)
	if DirAccess.make_dir_recursive_absolute(absolute_dir) != OK:
		_fail("Cannot create sample directory: " + absolute_dir)
		return
	output_file = FileAccess.open(absolute_dir.path_join("visible.jsonl"), FileAccess.WRITE)
	if output_file == null:
		_fail("Cannot open visible sample record.")
		return
	TranslationServer.set_locale("zh_CN")
	capture_viewport = SubViewport.new()
	capture_viewport.size = _viewport_size
	capture_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(capture_viewport)
	host = ReplayHost.new()
	host.name = "StoryUiSampleReplay"
	capture_viewport.add_child(host)
	observer = VIEW_TRACE.new()
	host.add_child(observer)
	observer.configure(host, _visible_sink, _input_sink)
	for chosen in selected:
		if _failed: break
		await _render_sample(chosen)
	if not _write_manifest(not _failed): _failed = true
	await _shutdown()
	quit(2 if _failed else 0)

func _parse_options() -> bool:
	var args := OS.get_cmdline_user_args()
	for index in args.size():
		var arg := str(args[index])
		if arg.begins_with("--round="):
			_round = arg.trim_prefix("--round=")
		elif arg.begins_with("--width="):
			_viewport_size.x = int(arg.trim_prefix("--width="))
		elif arg.begins_with("--height="):
			_viewport_size.y = int(arg.trim_prefix("--height="))
		elif arg in ["--width", "--height"] and index + 1 < args.size():
			if arg == "--width": _viewport_size.x = int(args[index + 1])
			else: _viewport_size.y = int(args[index + 1])
	if not RegEx.create_from_string("^[A-Za-z0-9][A-Za-z0-9_-]*$").search(_round):
		_fail("Pass a safe --round=name so separate review rounds have separate output directories.")
		return false
	if _viewport_size.x < 640 or _viewport_size.y < 480 or _viewport_size.x > 4096 or _viewport_size.y > 4096:
		_fail("Viewport dimensions must be between 640x480 and 4096x4096.")
		return false
	return true

func _select_samples() -> Array:
	var result: Array = []
	for spec in SAMPLE_SPECS:
		var best: Dictionary = {}
	var best_length := -1
		for packet in packets:
			for index in packet.output.size():
				var ev: Dictionary = packet.output[index]
				if str(ev.get("kind", "")) != str(spec.kind): continue
				var matching_line := -1
				var length := 0
				if spec.kind == "lines":
					for line_index in ev.get("lines", []).size():
						var line: Dictionary = ev.lines[line_index]
						if not str(line.get("line_id", "")).begins_with(str(spec.line_prefix)): continue
						if bool(spec.get("require_speaker", false)) and str(line.get("speaker", "")).is_empty(): continue
						var current_length := str(line.get("text", "")).length()
						if matching_line < 0 or (bool(spec.get("prefer_longest", false)) and current_length > length) or (bool(spec.get("prefer_shortest", false)) and current_length < length):
							matching_line = line_index
							length = current_length
					if matching_line < 0: continue
				elif spec.kind == "card" and str(ev.get("id", "")) != str(spec.card_id): continue
				elif spec.kind == "form":
					if str(ev.get("layout", "")) != str(spec.layout): continue
					var ids: Array = []
					for field in ev.get("fields", []): ids.append(str(field.get("id", "")))
					var has_fields := true
					for id in spec.field_ids:
						if not ids.has(id): has_fields = false
					if not has_fields: continue
				elif spec.kind == "candidates":
					var found := false
					for option in ev.get("options", []):
						if str(option.get("id", "")).begins_with(str(spec.option_prefix)): found = true
					if not found: continue
				if best.is_empty() or bool(spec.get("prefer_last", false)) or (bool(spec.get("prefer_longest", false)) and length > best_length) or (bool(spec.get("prefer_shortest", false)) and length < best_length):
					best = {"spec": spec, "packet": packet, "output_index": index, "line_index": matching_line}
					best_length = length
		if best.is_empty():
			_fail("Source transcript has no sample matching " + str(spec.slug))
			return []
		result.append(best)
	return result

func _render_sample(chosen: Dictionary) -> void:
	var spec: Dictionary = chosen.spec
	var packet: Dictionary = chosen.packet
	var ev: Dictionary = packet.output[chosen.output_index]
	_sample_slug = str(spec.slug)
	source_sequence = int(packet.sequence)
	output_index = int(chosen.output_index)
	_pending_modal = {}
	_pending_modal_index = -1
	_defer_draw_until = -1
	_last_visible = {}
	var prior := _prior_context(source_sequence, output_index)
	if prior.is_empty(): return
	snapshot = prior.state
	_packet_snapshot = prior.state
	observer._last_frame = ""
	await _view(prior.view)
	if _failed: return
	if not prior.map_event.is_empty():
		var map_ev: Dictionary = prior.map_event
		if str(map_ev.kind) == "scene":
			if not _map_exists(str(map_ev.get("map", ""))): return
			await host.field.execute(map_ev)
		elif str(map_ev.kind) == "travel":
			await host.field.travel_to_node(map_ev.node)
	if _failed: return
	await _apply_context(packet.input)
	if _failed: return
	host.ui.apply_snapshot(snapshot)
	host.field.apply_snapshot(snapshot)
	var display_event := ev.duplicate(true)
	var line_id := ""
	if spec.kind == "lines":
		var selected_line: Dictionary = ev.lines[chosen.line_index]
		line_id = str(selected_line.get("line_id", ""))
		# One authored line is the short excerpt. The formal dialogue presenter
		# still reveals it using its production panel and text animation.
		display_event.lines = [selected_line]
		display_event.erase("choices")
		_present_done = false
		_start_present(display_event)
		var dialogue: CanvasLayer = host.ui.dialogue
		var deadline := Time.get_ticks_msec() + int(DEADLINE_SECONDS * 1000.0)
		while (not dialogue.active or dialogue.line == null or dialogue.busy) and not _failed:
			await _draw()
			if Time.get_ticks_msec() > deadline:
				_fail("Dialogue sample did not appear: " + _sample_slug)
				return
		if dialogue.body.visible_characters >= 0:
			var click := InputEventMouseButton.new()
			click.button_index = MOUSE_BUTTON_LEFT
			click.pressed = true
			dialogue.panel.gui_input.emit(click)
		await _draw()
	else:
		if spec.kind == "card" and str(display_event.get("picture", "")) != "" and not ResourceLoader.exists(str(display_event.picture)):
			_fail("Sample card picture is missing: " + str(display_event.picture))
			return
		# Modal presenters are synchronous in the production UI. An ending or
		# form remains open for the captured frame; no response is fabricated.
		await host.ui.present(display_event)
		await _draw()
	if _failed: return
	observer._last_frame = ""
	await _draw()
	if _last_visible.is_empty() or _last_visible.get("texts", []).is_empty():
		_fail("Production UI sample yielded no visible text: " + _sample_slug)
		return
	var expected_panel := str(spec.kind)
	if spec.kind == "lines": expected_panel = "dialogue"
	elif spec.kind == "candidates": expected_panel = "menu"
	if str(_last_visible.get("panel", "")) != expected_panel:
		_fail("Production UI sample did not display its selected panel: " + _sample_slug)
		return
	var path := ProjectSettings.globalize_path(_sample_dir + _sample_slug + ".png")
	var pixels: Image = capture_viewport.get_texture().get_image()
	if pixels == null or pixels.is_empty() or pixels.save_png(path) != OK:
		_fail("Cannot save rendered sample pixels: " + path)
		return
	var provenance := {"slug": _sample_slug, "screenshot": path, "source_sequence": source_sequence,
		"output_index": output_index, "event_kind": str(ev.kind), "trace_id": str(ev.get("trace_id", "")),
		"presentation_id": str(ev.get("presentation_id", "")), "line_id": line_id,
		"card_id": str(ev.get("id", "")) if spec.kind == "card" else "",
		"selector": spec, "view_sequence": prior.view_sequence,
		"state_sequence": prior.state_sequence, "map_sequence": prior.map_sequence,
		"viewport": [_viewport_size.x, _viewport_size.y], "panel": str(_last_visible.get("panel", "")),
		"capture_type": "production_ui_render_sample", "blind_playtest": false}
	_sample_manifest.append(provenance)
	_write("visible_sample", {"provenance": provenance, "visible": _last_visible})
	if spec.kind == "lines":
		# Finish the isolated one-line presentation through its real control so
		# the next sample does not inherit a suspended dialogue coroutine.
		await _press(host.ui.dialogue.next_button)
		if _failed: return
		var close_deadline := Time.get_ticks_msec() + int(DEADLINE_SECONDS * 1000.0)
		while not _present_done and not _failed:
			await _draw()
			if Time.get_ticks_msec() > close_deadline:
				_fail("Sample dialogue did not close: " + _sample_slug)
				return

func _prior_context(sequence: int, event_index: int) -> Dictionary:
	var view: Dictionary = {}
	var state: Dictionary = {}
	var map_event: Dictionary = {}
	var view_sequence := -1
	var state_sequence := -1
	var map_sequence := -1
	for packet in packets:
		if int(packet.sequence) > sequence: break
		var limit: int = packet.output.size() if int(packet.sequence) < sequence else event_index + 1
		for index in limit:
			var ev: Dictionary = packet.output[index]
			match str(ev.get("kind", "")):
				"view":
					view = ev
					view_sequence = int(packet.sequence)
					map_event = {}
					map_sequence = -1
				"state":
					state = ev.snapshot
					state_sequence = int(packet.sequence)
				"scene", "travel":
					map_event = ev
					map_sequence = int(packet.sequence)
	# The production host applies the state from the entire dispatch packet
	# before presenting output; preserve that packet-local behavior.
	var selected_packet: Dictionary = packets[sequence - 1]
	for ev in selected_packet.output:
		if str(ev.get("kind", "")) == "state":
			state = ev.snapshot
			state_sequence = sequence
	if view.is_empty() or state.is_empty():
		_fail("Sample lacks a prior production view or recorded state at sequence " + str(sequence))
		return {}
	return {"view": view, "state": state, "map_event": map_event, "view_sequence": view_sequence,
		"state_sequence": state_sequence, "map_sequence": map_sequence}

func _visible_sink(data: Dictionary) -> void:
	if _closing or _failed: return
	_last_visible = data.duplicate(true)
	_last_visible.source_sequence = source_sequence
	_last_visible.output_index = output_index
	_last_visible.capture_type = "production_ui_render_sample"

func _input_sink(_data: Dictionary) -> void:
	pass

func _write_manifest(finished: bool) -> bool:
	var path := ProjectSettings.globalize_path(_sample_dir + "manifest.json")
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		push_error("Cannot write sample manifest: " + path)
		return false
	file.store_string(JSON.stringify({"finished": finished, "round": _round, "source": ProjectSettings.globalize_path(SOURCE),
		"source_sha256": FileAccess.get_sha256(ProjectSettings.globalize_path(SOURCE)),
		"viewport": [_viewport_size.x, _viewport_size.y], "samples": _sample_manifest,
		"capture_type": "production_ui_render_sample", "blind_playtest": false}, "  "))
	file.close()
	return true
