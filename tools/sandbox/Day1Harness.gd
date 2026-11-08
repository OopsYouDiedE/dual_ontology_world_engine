extends Node
## Day-1 verification harness (Godot host, engine AI controller).
## Runs the real Main scene and CampaignRoomHost, polls host state, and writes
## .runtime/day1/progress.log and .runtime/day1/result.json.
## Run headless (fast, no rasteriser cost):
##   godot --headless --path . res://tools/sandbox/Day1Harness.tscn -- --dv-controller=ai --dv-seed=1 --dv-viewer=detective
## Optional: --day1-target-minutes=1440  --day1-wall-limit=2400

const MAIN := preload("res://scenes/main/Main.tscn")
var main: Node
var started_usec := 0
var target_minutes := 1440.0
var wall_limit := 2400.0
var log_file: FileAccess
var last_minutes := -1.0
var stalled_since := 0.0
var rooms_seen := {}
var actions_seen := 0

func _ready() -> void:
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--day1-target-minutes="): target_minutes = float(arg.split("=")[1])
		if arg.begins_with("--day1-wall-limit="): wall_limit = float(arg.split("=")[1])
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://.runtime/day1"))
	log_file = FileAccess.open("res://.runtime/day1/progress.log", FileAccess.WRITE)
	started_usec = Time.get_ticks_usec()
	main = MAIN.instantiate()
	add_child(main)
	await get_tree().process_frame
	main.start_new_game()
	var timer := Timer.new(); timer.wait_time = 2.0; timer.autostart = true; add_child(timer)
	timer.timeout.connect(_poll)

func _host() -> Node:
	return main.get("field") if main != null else null

func _poll() -> void:
	var wall := (Time.get_ticks_usec() - started_usec) / 1e6
	var host := _host()
	if host == null:
		_line("wall=%.0f host=missing" % wall); return
	var bridge = host.get("bridge")
	var snap: Dictionary = bridge.Snapshot() if bridge != null else {}
	var minutes := float(snap.get("game_seconds", 0.0)) / 60.0
	var room := str(host.get("room_id"))
	rooms_seen[room] = true
	var portal = host.get("_portal")
	var stage := str(portal.get("stage", "")) if portal is Dictionary else ""
	var failure := str(host.get("last_failure"))
	var info := "wall=%.0f day=%d time=%02d:%02d room=%s ready=%s busy=%s portal=%s phys=%s paused=%s finished=%s steps=%s fail=%s" % [
		wall, int(minutes / 1440.0) + 1, int(fmod(minutes, 1440.0)) / 60, int(fmod(minutes, 60.0)), room,
		host.get("ready_for_play"), host.get("_transition_busy"), stage, host.get("_physical_clock_pending"),
		host.get("player_paused"), host.get("_finished"), host.get("_step_count"), failure]
	_line(info)
	if minutes > last_minutes + 0.01:
		last_minutes = minutes; stalled_since = wall
	var verdict := ""
	if minutes >= target_minutes: verdict = "PASS"
	elif not failure.is_empty() and failure != "<null>": verdict = "FAIL_HOST_FAILURE"
	elif wall - stalled_since > 180.0: verdict = "FAIL_STALLED"
	elif wall > wall_limit: verdict = "FAIL_WALL_LIMIT"
	if verdict != "":
		var result := {"verdict": verdict, "game_minutes": minutes, "target_minutes": target_minutes, "wall_seconds": wall,
			"rooms_visited": rooms_seen.keys(), "last_state": info, "campaign_progress": snap.get("campaign_progress", {})}
		var out := FileAccess.open("res://.runtime/day1/result.json", FileAccess.WRITE)
		out.store_string(JSON.stringify(result, "  ")); out.close()
		_line("VERDICT " + verdict); print("DAY1_VERDICT ", verdict)
		get_tree().quit(0 if verdict == "PASS" else 1)

func _line(text: String) -> void:
	print(text)
	if log_file: log_file.store_line(text); log_file.flush()
