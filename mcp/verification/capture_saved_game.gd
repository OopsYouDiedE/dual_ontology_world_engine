extends "res://tools/dv_level3_capture.gd"
## Resume real saves and collect new bookmarks only at the production host boundary.
## Checkpoints pause command dispatch while writing; they never choose world actions.
var _saved_load_path := ""
var _saved_checkpoint_times: Array[float] = []
var _saved_checkpoint_index := 0
var _saved_checkpoint_successes := 0
var _saved_checkpoint_copy_failed := false
var _saved_checkpoint_inflight := false
var _saved_checkpoint_retry_at := 0

func _run() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--load-save="):
			_saved_load_path = argument.trim_prefix("--load-save=")
		elif argument.begins_with("--checkpoint-at="):
			for value in argument.trim_prefix("--checkpoint-at=").split(",", false):
				if value.is_valid_float() and float(value) >= 0.0:
					_saved_checkpoint_times.append(float(value))
	_saved_checkpoint_times.sort()
	if _saved_load_path != "" and "--dv-auto-start" in OS.get_cmdline_user_args():
		push_error("Choose --load-save or --dv-auto-start")
		quit(2)
		return
	super._run()
	if not is_instance_valid(host) or not is_instance_valid(archive): return
	host.engine.EventsReady.connect(_saved_events)
	_write("checkpoint-config.dvlog", {"load_path": _saved_load_path,
		"requested_real_seconds": _saved_checkpoint_times, "world_actions": "production controller only"})
	if _saved_load_path != "":
		host.dispatch.call_deferred({"kind": "load", "path": _saved_load_path})

func _process(delta: float) -> bool:
	var result := super._process(delta)
	if not is_instance_valid(host) or finishing: return result
	_saved_request_checkpoint()
	return result

func _saved_request_checkpoint() -> void:
	if _saved_checkpoint_copy_failed or _saved_checkpoint_inflight or _saved_checkpoint_index >= _saved_checkpoint_times.size(): return
	var now := Time.get_ticks_msec()
	if now < _saved_checkpoint_retry_at: return
	if now - started < _saved_checkpoint_times[_saved_checkpoint_index] * 1000.0: return
	_saved_checkpoint_retry_at = now + 250
	if not host.flow.started or host.flow.book_reading: return
	var snapshot: Dictionary = host.engine.Snapshot()
	if snapshot.is_empty(): return
	_saved_checkpoint_inflight = true
	_append("checkpoint-events.dvlog", {"kind": "requested", "real_ms": now - started,
		"index": _saved_checkpoint_index + 1, "snapshot": snapshot})
	host.dispatch({"kind": "bookmark_save"})

func _saved_events(events: Array) -> void:
	for event: Dictionary in events:
		var kind := str(event.get("kind", ""))
		if kind == "view" and _saved_load_path != "":
			# Restoring preserves the saved controller. The capture follows it.
			mode = "player" if bool(event.get("snapshot", {}).get("player_control", true)) else "detective"
		if not _saved_checkpoint_inflight: continue
		if kind == "bookmark_error" and str(event.get("operation", "")) == "save":
			_saved_checkpoint_inflight = false
			_saved_checkpoint_retry_at = Time.get_ticks_msec() + 1000
			_append("checkpoint-events.dvlog", {"kind": "rejected", "real_ms": Time.get_ticks_msec() - started, "event": event})
		elif kind == "bookmark_saved":
			_saved_checkpoint_inflight = false
			var checkpoint_dir := directory.path_join("checkpoints")
			DirAccess.make_dir_recursive_absolute(checkpoint_dir)
			var source := str(event.path)
			var target := checkpoint_dir.path_join("checkpoint-%03d.dvsave" % (_saved_checkpoint_index + 1))
			var sidecar := source.get_basename() + ".casebook.dvlog"
			var target_sidecar := target.get_basename() + ".casebook.dvlog"
			var copy_error := ERR_FILE_NOT_FOUND if not FileAccess.file_exists(sidecar) else OK
			if copy_error == OK:
				copy_error = ERR_ALREADY_EXISTS if FileAccess.file_exists(target) or FileAccess.file_exists(target_sidecar) else DirAccess.copy_absolute(source, target)
			if copy_error == OK: copy_error = DirAccess.copy_absolute(sidecar, target_sidecar)
			if copy_error == OK and FileAccess.get_sha256(source) != FileAccess.get_sha256(target): copy_error = ERR_FILE_CORRUPT
			if copy_error == OK: _saved_checkpoint_successes += 1
			_append("checkpoint-events.dvlog", {"kind": "saved" if copy_error == OK else "copy_failed", "real_ms": Time.get_ticks_msec() - started,
				"index": _saved_checkpoint_index + 1, "original_path": source, "path": target,
				"copy_error": copy_error, "sha256": FileAccess.get_sha256(source), "snapshot": host.engine.Snapshot()})
			if copy_error != OK:
				_saved_checkpoint_copy_failed = true
				push_error("Could not copy checkpoint: " + error_string(copy_error))
			else:
				_saved_checkpoint_index += 1

func _finish() -> void:
	if finishing: return
	_write("checkpoint-summary.dvlog", {"requested": _saved_checkpoint_times,
		"saved": _saved_checkpoint_successes, "inflight": _saved_checkpoint_inflight,
		"load_path": _saved_load_path, "copy_failed": _saved_checkpoint_copy_failed})
	await super._finish()
