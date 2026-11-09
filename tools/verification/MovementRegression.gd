extends Node
## Real-host regression: locomotion, animation planting, empty-settings guard,
## fast-forward isolation and cross-room exploration progression.

const MAIN := preload("res://scenes/main/Main.tscn")
var main: Node
var host: Node
var body: CharacterBody3D
var t := 0.0
var stage := "boot"
var results: Array = []
var distance_ok := false
var speed_ok := false
var clock_ok := false
var anim_ok := false
var guard_ok := false
var start_pos := Vector3.ZERO
var start_seconds := 0.0
var max_anim := 0.0
var press_start := 0.0
var room_before := ""
var walk_started := 0.0
var wake_tried := false
var leave_tried := false
var verdict := "FAIL_TIMEOUT"
var began := false
var guard_observed: Dictionary = {}

func _ready() -> void:
	main = MAIN.instantiate()
	add_child(main)
	await get_tree().process_frame
	main.start_new_game()

func _physics_process(delta: float) -> void:
	t += delta
	if host == null:
		host = main.get("field")
		if host == null: return
	if not bool(host.get("ready_for_play")):
		if t > 15.0: _finish("FAIL_BOOT: " + str(host.get("last_failure")))
		return
	if not is_instance_valid(body):
		var current = host.get("body")
		if current == null: return
		body = current
		if not began:
			began = true
			_begin()
	match stage:
		"guard":
			_test_guard()
		"walk":
			max_anim = maxf(max_anim, float(body.animation_player.speed_scale) if body.animation_player else 0.0)
			if t - press_start >= 1.0:
				Input.action_release("move_right")
				var dist := body.global_position.distance_to(start_pos)
				var game_delta := float(host.get("state").get("game_seconds", 0.0)) - start_seconds
				distance_ok = dist > 1.35 and dist < 2.55
				speed_ok = float(body.movement_speed) > 1.4 and float(body.movement_speed) < 2.6
				clock_ok = game_delta > 0.6 and game_delta < 2.6
				anim_ok = max_anim >= 0.8 and max_anim <= 1.36
				_record("locomotion", {"distance": snappedf(dist, 0.01), "game_delta": snappedf(game_delta, 0.01), "max_anim": snappedf(max_anim, 0.01), "speed": snappedf(body.movement_speed, 0.01)})
				host.set_speed(120.0)
				stage = "wake"
				wake_tried = true
				var error: String = host.request_action("detective.wake")
				_record("wake-submit", {"error": error})
		"wake":
			if host.available_actions().any(func(a): return str(a.get("id", "")) == "detective.leave"):
				host.set_speed(1.0)
				room_before = str(host.get("room_id"))
				leave_tried = true
				var error: String = host.request_action("detective.leave")
				_record("leave-submit", {"error": error, "choices": host.available_actions().map(func(a): return str(a.get("id", "")))})
				walk_started = t
				stage = "leave"
			elif t > 25.0:
				_finish("FAIL_WAKE")
		"leave":
			if str(host.get("room_id")) != room_before:
				_record("room-transition", {"from": room_before, "to": str(host.get("room_id")), "seconds": snappedf(t - walk_started, 0.01)})
				_finish("")
			elif t - walk_started > 30.0:
				_record("leave-timeout", {"phase": str(host.get("phase")), "feedback": str(host.get("feedback")), "path": host.get("_path").size(), "room": str(host.get("room_id")), "activity": host.call("_activity")})
				_finish("FAIL_LEAVE_TIMEOUT")

func _begin() -> void:
	stage = "guard"
	t = 0.0
	start_pos = body.global_position
	start_seconds = float(host.get("state").get("game_seconds", 0.0))

func _test_guard() -> void:
	var saved: Dictionary = host.get("settings")
	host.set("settings", {})
	var observed: Dictionary = host.observation()
	guard_observed = observed
	guard_ok = observed.get("ready", true) == false
	host.set("settings", saved)
	start_pos = body.global_position
	start_seconds = float(host.get("state").get("game_seconds", 0.0))
	Input.action_press("move_right")
	press_start = t
	stage = "walk"
	_record("empty-settings-guard", {"ok": guard_ok, "observed": observed})

func _record(name: String, data: Dictionary) -> void:
	results.append({"name": name, "data": data})
	print("REGRESSION ", JSON.stringify({"name": name, "data": data}))

func _finish(error: String) -> void:
	var ok := error.is_empty() and distance_ok and speed_ok and clock_ok and anim_ok and guard_ok and leave_tried
	print("REGRESSION_RESULT ", JSON.stringify({
		"verdict": "PASS" if ok else ("FAIL_" + error if not error.is_empty() else "FAIL_ASSERT"),
		"locomotion": {"distance": distance_ok, "speed": speed_ok, "clock": clock_ok, "animation": anim_ok},
		"empty_settings_guard": guard_ok,
		"guard_observed": guard_observed,
		"exploration_cross_room": str(host.get("room_id")) != room_before if leave_tried else false,
		"events": results
	}))
	get_tree().quit(0 if ok else 1)
