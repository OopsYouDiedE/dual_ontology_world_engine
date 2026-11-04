extends RefCounted
## HostArchiveComponent owns host-side presentation work; session access goes through Main.
var host: Node

func _init(main: Node) -> void:
	host = main

func _try_bookmark_save() -> void:
	if not host.flow.bookmark_requested or not host.flow.started or host.flow.closing or host.flow.bookmark_loading or host.flow.bookmark_saving: return
	if not host._bookmark_boundary_ready() or not host._session_bookmark_ready(): return
	host.flow.transition("bookmark_requested", false)
	host.flow.transition("bookmark_saving", true)
	# Once the safe boundary is owned, use the normal persistence continuation
	# queue. Its existing next-request deduplication also covers late callbacks.
	if not host._bookmark_next.is_empty():
		if not host._has_deferred_next(): host._book_deferred_inputs.append(host._bookmark_next)
		host._bookmark_next = {}
	if is_instance_valid(host.ui): host.ui.set_bookmark_loading(true)
	host._save_bookmark(host._casebook.export_data())

func _bookmark_boundary_ready() -> bool:
	# Session saves cannot reconstruct unread host presentation or in-flight maps.
	if host.flow.reading or host.flow.draining or not host._queue.is_empty() or not host._book_deferred_inputs.is_empty(): return false
	if is_instance_valid(host._pending_adapter) or host.flow.background_execution or host.flow.completion_pending or not host._dv_intent.is_empty(): return false
	if is_instance_valid(host.field_executor) and bool(host.field_executor.get("_busy")): return false
	if is_instance_valid(host.field):
		if is_instance_valid(host.field.adapter) and host.field.adapter.switching(): return false
		# Rejected continuous passages may still be physically returning to the
		# source after Complete. That unsaved host state is not a bookmark boundary.
		var jobs: Variant = host.field.get("_daily")
		if jobs is Dictionary and not jobs.is_empty(): return false
	return true

func _session_options() -> Dictionary:
	var speed = host.playback_speed
	var options = {"controller": host.controller, "campaign": host.campaign_options.duplicate(true), "audit": false}
	if host.viewer != "": options.viewer = host.viewer
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--dv-speed="):
			var value = arg.trim_prefix("--dv-speed=")
			if not value.is_valid_float():
				host._show_error("Invalid --dv-speed: " + value)
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
			var value = arg.trim_prefix("--dv-seed=")
			if not value.is_valid_int():
				host._show_error("Invalid --dv-seed: " + value)
				return {}
			options.campaign.seed = int(value)
	if str(options.controller) not in ["player", "ai"]:
		host._show_error("Invalid --dv-controller: " + str(options.controller))
		return {}
	if not host._apply_playback(speed, str(options.controller)): return {}
	return options

func _configure_load_playback() -> bool:
	var speed = host.playback_speed
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--dv-speed="):
			var value = argument.trim_prefix("--dv-speed=")
			if not value.is_valid_float():
				host._show_error("Invalid --dv-speed: " + value)
				return false
			speed = float(value)
	# The saved session owns its controller; only host playback is configured.
	return host._apply_playback(speed, "restored-session")

func _apply_playback(speed: float, control_mode: String) -> bool:
	if host.replay_mode: return true
	if not is_finite(speed) or speed < 0.25 or speed > 8.0:
		host._show_error("Playback speed must be between 0.25 and 8")
		return false
	Engine.time_scale = speed
	Engine.physics_ticks_per_second = ceili(60.0 * maxf(1.0, speed))
	Engine.max_physics_steps_per_frame = ceili(8.0 * maxf(1.0, speed))
	host.record_input({"device": "host_configuration", "controller": control_mode, "playback_speed": speed, "physics_ticks_per_second": Engine.physics_ticks_per_second, "max_physics_steps_per_frame": Engine.max_physics_steps_per_frame})
	return true

func _install_bookmark(event: Dictionary) -> void:
	host._session_generation += 1
	host.flow.transition("bookmark_requested", false)
	host._bookmark_next = {}
	host.flow.transition("view_faulted", false)
	host.flow.transition("player_options_open", false)
	var generation = host._session_generation
	# The host already owns the restored session. Bind its archive immediately,
	# so closing the window during map preparation cannot save the old notes.
	# The departing UI keeps its old object until disposal; it cannot mutate this.
	host._casebook = host.CASEBOOK_DATA.new()
	host._casebook.restore_data(event.get("casebook", {}), str(event.snapshot.get("viewer", "")))
	host.flow.transition("reading", false)
	host._queue.clear()
	host._book_deferred_inputs.clear()
	host._dv_intent.clear()
	host._interaction_id = ""
	host._interaction_site = ""
	# The host only emits this after persistence has refused pending execution.
	var installed: bool = await host._view({"resource": event.snapshot.resource, "snapshot": event.snapshot,
		"casebook_warning": event.get("casebook_warning", ""), "replace_session": true}, false)
	if host.flow.closing or generation != host._session_generation: return
	if not installed:
		host.flow.transition("bookmark_loading", true)
		host.get_tree().paused = true
		if is_instance_valid(host.ui): host.ui.set_bookmark_loading(true)
		host._timing("bookmark_view_failed", {"path": event.get("path", ""), "backup_path": event.get("backup_path", "")})
		host._show_bookmark_view_error(str(event.get("backup_path", "")))
		return
	host.flow.transition("bookmark_loading", false)
	if is_instance_valid(host.ui): host.ui.set_bookmark_loading(false)
	if not host.flow.closing: host.dispatch.call_deferred({"kind": "view_ready"})

func capture_replay_bodies() -> Array:
	var result: Array = []
	if not is_instance_valid(host.field): return result
	for who in host.field.actors:
		var body: CharacterBody2D = host.field.actors[who]
		if not is_instance_valid(body): continue
		result.append({"actor": str(who), "map": str(host.field.adapter.map_key()),
			"room": str(host.field.adapter.room_of(body.position)), "position": [body.position.x, body.position.y],
			"velocity": [body.velocity.x, body.velocity.y], "seated": bool(body.seated)})
	return result

func install_replay_snapshot(value: Dictionary, archive: Dictionary) -> bool:
	host.flow.transition("started", true)
	var snapshot: Dictionary = host.PRESENTATION.snapshot(value)
	if not archive.is_empty(): host._casebook.restore_data(archive, str(value.viewer))
	var rebuild = not is_instance_valid(host.field) or str(host.field.adapter.map_key()) != str(snapshot.map)
	if rebuild:
		var installed: bool = await host._view({"resource": snapshot.resource, "snapshot": snapshot,
			"casebook": archive if not archive.is_empty() else host._casebook.export_data(), "replace_session": true}, false)
		if not installed: return false
	else:
		await host._apply(snapshot)
	var pending: Dictionary = snapshot.get("pending", {})
	var options: Array = pending.get("options", []) if str(pending.get("status", "")) == "awaiting_input" else []
	host.field.spatial.set_options(options)
	host.ui.set_player_options(options)
	host.flow.transition("player_options_open", true)
	host.flow.transition("opening", false)
	return true

## Main is the sole mutable session gateway for Godot components.
