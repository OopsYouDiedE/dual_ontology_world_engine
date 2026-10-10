extends Node
## 全量测试场景：子节点就是正式主场景 Main.tscn，主线怎么改这里就怎么跑。
## 让 AI 坐上玩家席位（按该角色 .story 中的操控规则），画面就是正式游戏画面。
## 记录本人可见的观察、决定、动作与画面上显示的内容，定时截取画面；
## 一旦没有公开进展、缺候选或出错就停下并如实记下原因。不注入状态、不改 Core。
## --time-scale 只整体加快引擎时间（物理步数同比提高，单步时长不变），事件顺序与正常游玩相同。
##
## godot --path godot_csharp res://scenes/Shared/check/FullTest/FullTest.tscn -- --output=<新目录>
##   [--seed=0] [--time-scale=1] [--max-seconds=1800] [--no-progress-seconds=60] [--shot-seconds=5] [--line-seconds=0.6]
const PilotScript := preload("res://scenes/Host/AgentPilot.gd")
const ARGS := ["--output", "--seed", "--time-scale", "--max-seconds", "--no-progress-seconds", "--shot-seconds", "--line-seconds", "--max-selections", "--max-observations"]

@onready var main: Node = $Main
var pilot: Node
var out_dir := ""
var trace: FileAccess
var started := 0
var progressed := 0
var last_shot := 0
var shots := 0
var sequence := 0
var seen := {}
var wall_limit := 1800.0
var idle_limit := 60.0
var shot_every := 5.0
var finished := false

func _ready() -> void:
	_run.call_deferred()

func quit(code: int) -> void:
	get_tree().quit(code)

func process_frame_wait() -> void:
	await get_tree().process_frame

func _run() -> void:
	var settings := {}
	for argument in OS.get_cmdline_user_args():
		var pieces := argument.split("=", true, 1)
		if pieces.size() != 2 or not pieces[0] in ARGS or settings.has(pieces[0]):
			push_error("Invalid or duplicate full-test argument: " + argument)
			quit(2)
			return
		settings[pieces[0]] = pieces[1]
	if not settings.has("--output"):
		push_error("Required: --output=<new directory>")
		quit(2)
		return
	out_dir = settings["--output"]
	if DirAccess.dir_exists_absolute(out_dir):
		push_error("Refusing to reuse an existing output directory: " + out_dir)
		quit(2)
		return
	DirAccess.make_dir_recursive_absolute(out_dir.path_join("shots"))
	trace = FileAccess.open(out_dir.path_join("trajectory.jsonl"), FileAccess.WRITE)
	wall_limit = float(settings.get("--max-seconds", "1800"))
	idle_limit = float(settings.get("--no-progress-seconds", "60"))
	shot_every = float(settings.get("--shot-seconds", "5"))
	var scale := float(settings.get("--time-scale", "1"))
	if scale < 1.0 or scale > 16.0:
		push_error("--time-scale must be within 1..16")
		quit(2)
		return
	Engine.time_scale = scale
	Engine.physics_ticks_per_second = int(round(60 * scale))
	Engine.max_physics_steps_per_frame = int(ceil(8 * scale))
	started = Time.get_ticks_msec()
	progressed = started
	await process_frame_wait()
	main.engine.ConfigureAiSeat(int(settings.get("--seed", "0")), int(settings.get("--max-selections", "100000")), int(settings.get("--max-observations", "1000000")))
	pilot = PilotScript.new()
	pilot.name = "AgentPilot"
	pilot.line_seconds = float(settings.get("--line-seconds", "0.6"))
	main.enable_pilot(pilot)
	pilot.recorded.connect(_on_recorded)
	pilot.stopped.connect(_on_stopped)
	_record("start", {"seed": int(settings.get("--seed", "0")), "time_scale": scale, "max_seconds": wall_limit, "no_progress_seconds": idle_limit})
	main.dispatch({"kind": "start"})
	pilot.start()
	while not finished:
		await process_frame_wait()
		var now := Time.get_ticks_msec()
		if float(now - last_shot) / 1000.0 >= shot_every: _shot("periodic")
		if float(now - started) / 1000.0 >= wall_limit: pilot.stop("wall_budget")
		elif float(now - progressed) / 1000.0 >= idle_limit: pilot.stop("no_public_progress")

func _on_recorded(kind: String, value: Variant) -> void:
	_record(kind, value)
	if kind != "observation": return
	# 公开进展：本人看到的台词、文字、候选或所在位置（按 64 像素格量化，与席位记走过的地方同尺度）有新内容。
	# 在同几格之间来回走不算进展。
	var state := {"lines": value.get("Lines", []), "text": value.get("Text", []), "complete": value.get("Complete", false), "offers": []}
	for offer in value.get("Candidates", []):
		if offer.Kind != "move": state.offers.append([offer.Label, offer.Kind, offer.New])
	if value.has("Position"): state.position = [floorf(value.Position[0] / 64.0), floorf(value.Position[1] / 64.0)]
	var key := JSON.stringify(state).sha256_text()
	if not seen.has(key):
		seen[key] = true
		progressed = Time.get_ticks_msec()

func _on_stopped(reason: String, summary: Dictionary) -> void:
	if finished: return
	finished = true
	summary["elapsed_seconds"] = float(Time.get_ticks_msec() - started) / 1000.0
	summary["screenshots"] = shots + 1
	_shot("stop")
	_record("stop", summary)
	trace.close()
	var file := FileAccess.open(out_dir.path_join("summary.json"), FileAccess.WRITE)
	file.store_string(JSON.stringify(summary, "\t"))
	file.close()
	print(JSON.stringify(summary))
	main.begin_shutdown()
	await get_tree().create_timer(0.3, true, false, true).timeout
	quit(1 if reason == "engine_error" else 0)

func _shot(label: String) -> void:
	last_shot = Time.get_ticks_msec()
	var image := get_viewport().get_texture().get_image()
	if image == null or image.is_empty(): return
	shots += 1
	var name := "%04d-%s.png" % [shots, label]
	image.save_png(out_dir.path_join("shots").path_join(name))
	_record("screenshot", {"file": name})

func _record(kind: String, value: Variant) -> void:
	if trace == null or not trace.is_open(): return
	sequence += 1
	trace.store_line(JSON.stringify({"sequence": sequence, "elapsed_ms": Time.get_ticks_msec() - started, "kind": kind, "value": value}))
	trace.flush()
