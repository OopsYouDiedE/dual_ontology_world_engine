extends RefCounted
## HostPresentationComponent owns host-side presentation work; session access goes through Main.
var host: Node

func _init(main: Node) -> void:
	host = main

func _drain() -> void:
	if host.flow.draining or host.flow.closing or host.flow.opening_failed: return
	host.flow.transition("draining", true)
	var generation = host._session_generation
	var slice_started = Time.get_ticks_usec()
	var slice_frame = Engine.get_process_frames()
	while not host._queue.is_empty() and not host.flow.closing and not host.flow.view_faulted and generation == host._session_generation:
		# Reading can accumulate many completed NPC projections. Preserve every
		# event in order, but let rendering and input run between bounded batches.
		if Engine.get_process_frames() != slice_frame:
			slice_frame = Engine.get_process_frames()
			slice_started = Time.get_ticks_usec()
		elif Time.get_ticks_usec() - slice_started >= 2000:
			host._timing("presentation_queue_yield", {"remaining": host._queue.size()})
			await host.get_tree().process_frame
			slice_frame = Engine.get_process_frames()
			slice_started = Time.get_ticks_usec()
		while host.flow.book_reading and not host.flow.closing and generation == host._session_generation:
			await host.get_tree().process_frame
		if host.flow.closing or generation != host._session_generation: break
		if host._queue.is_empty(): break
		var ev: Dictionary = host._queue.pop_front()
		# A completed reading can expose an older queued observer snapshot while
		# an off-screen actor still owns an executing body. Let that real movement
		# finish before snapshot binding can acquire/reparent the same body.
		if str(ev.get("kind", "")) in ["state", "view"]:
			while host.flow.background_execution and not host.flow.closing and generation == host._session_generation:
				await host.get_tree().process_frame
			if host.flow.closing or generation != host._session_generation: break
		match str(ev.get("kind", "")):
			"dev_state":
				host._session_generation += 1
				host._preview_tokens.clear()
				host._dv_intent.clear()
				host._queue.clear()
				await host._view({"resource": ev.snapshot.resource, "snapshot": ev.snapshot, "replace_session": true}, true)
			"dv_action": await host._dv_execute(ev.intent, ev.get("execution_snapshot", {}))
			"dv_present":
				var result: Dictionary = ev.presentation
				# The preceding state has installed the authoritative result.
				# Release only this player's submitted action, never an NPC's.
				if is_instance_valid(host.field) and str(result.get("actor", "")) == str(host.field.snapshot.get("viewer", "")):
					host.field.spatial.release_submission()
				var lines = host._presentation_lines(result)
				if is_instance_valid(host.ui): host.ui.prepare_presentation(result)
				if not lines.is_empty():
					host.flow.transition("reading", true)
					host._timing("presentation_begin", {"execution_event": result.get("execution_event", "")})
					await host.ui.present({"kind": "lines", "lines": lines, "trace_id": str(result.get("execution_event", "")), "presentation_id": str(result.get("presentation_id", result.get("execution_event", "")))})
					if generation != host._session_generation: break
					host.flow.transition("reading", false)
					host._sync_world_ui_pause()
					host._timing("reading_finished", {"execution_event": result.get("execution_event", "")})
				if not bool(ev.get("advance_requested", false)):
					host.dispatch.call_deferred({"kind": "dv_next"})
			"view":
				if not await host._view(ev):
					if not host.flow.closing and generation == host._session_generation: host.flow.transition("view_faulted", true)
					host._queue.clear()
					break
			"state": await host._apply(ev.snapshot)
			"error":
				host.flow.transition("bookmark_requested", false)
				host._bookmark_next = {}
				host._view_error(str(ev.message), false)
			"load_error":
				host.flow.transition("bookmark_requested", false)
				host._bookmark_next = {}
				host.flow.transition("started", false)
				host.flow.transition("opening", false)
				if is_instance_valid(host.title_layer): host.title_layer.get_child(0).show_load_error(str(ev.message))
			_: push_error("Unknown DV host event: " + str(ev.get("kind", "")))
	host.flow.transition("draining", false)
	host._try_bookmark_save()
	if not host._queue.is_empty() and not host.flow.closing and not host.flow.view_faulted: host._drain.call_deferred()

## IDs name the actual execution and emitted field; source_refs retain authored provenance.
## Emitted line ordinals are not claimed to be original declaration line numbers.
func _presentation_line(result: Dictionary, text: String, field_name: String, index: int, paragraph: int) -> Dictionary:
	var event = str(result.get("execution_event", ""))
	return {"text": text, "speaker": str(result.get("speaker", result.get("actor", ""))), "speaker_name": str(result.get("speaker_name", "")),
		"trace_id": event, "execution_event": event, "source_id": str(result.get("action_id", "")),
		"line_id": "%s:%s:%d:%d" % [event, field_name, index, paragraph], "source_field": field_name,
		"emitted_line_index": index, "paragraph_index": paragraph, "source_refs": result.get("source_refs", [])}

func _presentation_lines(result: Dictionary) -> Array:
	var lines: Array = []
	if bool(result.get("visible", false)):
		if str(result.get("surface", "")) != "": lines.append(host._presentation_line(result, str(result.surface), "surface", 0, 0))
		var index = 0
		for text in result.get("lines", []):
			var paragraph = 0
			for part in str(text).split("\n", false):
				lines.append(host._presentation_line(result, part, "presentation.lines", index, paragraph))
				paragraph += 1
			index += 1
	return lines

func _apply(snapshot: Dictionary) -> void:
	host._timing("ui_apply_begin")
	if host._view_resource != "" and str(snapshot.resource) != host._view_resource:
		if not await host._view({"resource": snapshot.resource, "snapshot": snapshot}, false) and not host.flow.closing:
			host.flow.transition("view_faulted", true)
			host._queue.clear()
		host._timing("ui_apply_end")
		return
	if is_instance_valid(host.ui): host.ui.apply_snapshot(snapshot)
	if is_instance_valid(host.field) and int(snapshot.get("state_version", -1)) >= int(host.field.snapshot.get("state_version", -1)):
		host.field.apply_snapshot(snapshot)
	host._timing("ui_apply_end")

func _dv_events(events: Array) -> void:
	for source: Dictionary in events:
		if source.has("snapshot") and not host.replay_mode:
			host.view_trace.record_snapshot(source.snapshot, host._casebook.export_data())
		if str(source.get("kind", "")) == "dv_present" and not host.replay_mode:
			host.view_trace.record_presentation(source.presentation)
		var ev: Dictionary = host.PRESENTATION.event(source)
		if str(ev.get("kind", "")) in ["movement_detached", "activity_detached"]:
			host._movement_detach_answers[str(ev.action_event)] = bool(ev.detached)
			continue
		if str(ev.get("kind", "")) == "dv_commit":
			host._action_component._finish_background_activity(str(ev.action_event))
			# Closing retains bodies and collision until the real rollback finishes.
			var settled: bool = await host.field_executor.settle(str(ev.action_event), str(ev.outcome), host.field, true, bool(ev.get("preserve_position", false)))
			if not settled:
				host.flow.transition("completion_pending", false)
				if not host.flow.closing: host.flow.transition("view_faulted", true)
				host.record_host_failure("Movement rollback did not complete for " + str(ev.action_event))
				return
			continue
		if host.flow.closing:
			if str(ev.get("kind", "")) in ["dv_present", "error"]: host.flow.transition("completion_pending", false)
			continue
		if str(ev.get("kind", "")) == "prepared_ahead":
			if int(ev.state_version) == int(host.observe_session().get("state_version", -1)):
				host._preview_tokens[str(ev.choice_id)] = ev
			continue
		if str(ev.get("kind", "")) == "selection_rejected":
			if is_instance_valid(host.field): host.field.spatial.release_submission()
			continue
		if str(ev.get("kind", "")) in ["clock_state", "locomotion_state"]:
			host._preview_tokens.clear()
			if is_instance_valid(host.ui): host.ui.apply_snapshot(ev.snapshot)
			if is_instance_valid(host.field): host.field.apply_snapshot(ev.snapshot)
			continue
		if str(ev.get("kind", "")) == "state":
			for choice in host._preview_tokens.keys():
				if int(host._preview_tokens[choice].state_version) != int(ev.snapshot.get("state_version", -1)): host._preview_tokens.erase(choice)
		if str(ev.get("kind", "")) in ["dev_inspect", "dev_error", "replay_loaded"]:
			if is_instance_valid(host.dev_console): host.dev_console.receive(ev)
			continue
		if str(ev.get("kind", "")) in ["view", "state", "dv_action", "dv_present", "error", "bookmark_loaded"]: host.flow.transition("player_options_open", false)
		if host.flow.view_faulted:
			if str(ev.get("kind", "")) == "error": host._show_error(str(ev.message))
			continue
		if str(ev.get("kind", "")) in ["dv_present", "error"] and not bool(ev.get("detached_movement", false)): host.flow.transition("completion_pending", false)
		if str(ev.get("kind", "")) == "bookmark_loaded":
			host._install_bookmark(ev)
			continue
		if str(ev.get("kind", "")) in ["bookmark_saved", "bookmark_error"]:
			if str(ev.get("kind", "")) == "bookmark_saved" or str(ev.get("operation", "")) == "save":
				host.flow.transition("bookmark_saving", false)
			if str(ev.get("kind", "")) == "bookmark_error" and str(ev.get("operation", "")) == "load":
				host.flow.transition("bookmark_loading", false)
			if is_instance_valid(host.ui): host.ui.set_bookmark_loading(host.flow.bookmark_loading or host.flow.bookmark_saving)
			if not host.flow.book_reading: host._on_book_reading_changed(false)
			if is_instance_valid(host.ui):
				var message = str(ev.get("message", ""))
				if message == "": message = tr("书签已保存。") if str(ev.kind) == "bookmark_saved" else tr("暂时无法保存或载入，请等待当前行动结束。")
				host.ui.show_bookmark_result(message, str(ev.kind) == "bookmark_saved")
			continue
		if host.flow.reading and str(ev.get("kind", "")) == "state" and is_instance_valid(host.field):
			host.field.apply_snapshot(ev.snapshot)
		host._queue.append(ev)
	host._drain.call_deferred()

func _has_deferred_next() -> bool:
	for input in host._book_deferred_inputs:
		if str(input.get("kind", "")) == "dv_next": return true
	return false

func _on_book_reading_changed(active: bool) -> void:
	host.flow.transition("book_reading", active)
	if is_instance_valid(host.view_trace): host.view_trace.record_page()
	if active or host.flow.closing or host.flow.bookmark_loading or host.flow.bookmark_saving: return
	var deferred = host._book_deferred_inputs.duplicate()
	host._book_deferred_inputs.clear()
	for input in deferred: host.dispatch.call_deferred(input)
	host._drain.call_deferred()

