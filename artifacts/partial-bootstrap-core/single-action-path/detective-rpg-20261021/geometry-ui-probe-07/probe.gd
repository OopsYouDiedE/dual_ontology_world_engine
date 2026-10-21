extends SceneTree
## HISTORICAL GEOMETRY FIXTURE ONLY. No Main/GameEngine/DV/session is loaded.
const START := Vector2(2049.26440429688, 1089.23413085938)
const MAP_SCRIPT := preload("res://scenes/Chapter2/Field/CampusMapView.gd")
const ACTOR_SCRIPT := preload("res://scenes/Story/StoryActor.gd")
const WORLD_SCRIPT := preload("res://scenes/Shared/Field/WorldView.gd")
var output := ""
var origin := 0
var archive: Node
var target: SubViewport
var adapter: Node2D
var host: FixtureHost
var label: Label
var failures: Array[String] = []
var closing := false
var original_locale := ""
var preferences_before: Dictionary = {}

class FixtureHost extends Node2D:
	var _closing := false
	var snapshot: Dictionary = {} # Deliberately no world facts, options or answers.
	var player: CharacterBody2D
	var actors: Dictionary = {}
	func position_occupied(walker: CharacterBody2D, point: Vector2) -> bool:
		# Fixture-only approximation. The production passage_at collision claim
		# is separately recorded from the real CharacterBody2D.test_move query.
		for body in actors.values():
			if body != walker and body.position.distance_to(point) < 20.0: return true
		return false
	func occupied_positions(walker: CharacterBody2D) -> Array[Vector2]:
		var result: Array[Vector2] = []
		for body in actors.values():
			if body != walker: result.append(body.position)
		return result

func _initialize() -> void:
	origin = Time.get_ticks_usec()
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--out="): output = argument.trim_prefix("--out=")
	if output == "" or DirAccess.dir_exists_absolute(output):
		push_error("Require a NEW --out directory")
		quit(2)
		return
	DirAccess.make_dir_recursive_absolute(output)
	root.size = Vector2i(1000, 700)
	root.title = "07 historical geometry / UI fixture — no game session"
	_run.call_deferred()

func _process(_delta: float) -> bool:
	if Time.get_ticks_usec() - origin > 45000000:
		if original_locale != "": TranslationServer.set_locale(original_locale)
		push_error("Probe hard timeout; preserve partial evidence")
		quit(9)
	return false

func _record(row: Dictionary) -> void:
	row.real_us = Time.get_ticks_usec() - origin
	row.fixture = "historical-geometry-ui-07-no-session"
	archive.AppendRecords(output.path_join("probe.dvlog.partial"), JSON.stringify(row) + "\n")

func _preferences() -> Dictionary:
	var result := {}
	for path in ["user://language.cfg", "user://settings.cfg"]:
		result[path] = FileAccess.get_sha256(path) if FileAccess.file_exists(path) else "absent"
	return result

func _check(ok: bool, title: String, details: Dictionary = {}) -> void:
	_record({"kind": "observation", "ok": ok, "title": title, "details": details})
	if not ok: failures.append(title)

func _body(id: String, point: Vector2) -> CharacterBody2D:
	var body := CharacterBody2D.new()
	body.set_script(ACTOR_SCRIPT)
	body.standard = load("res://ShareAsset/Internal/Characters/PixelCast/detective.tres")
	body.name = id
	body.position = point
	adapter.objects_root().add_child(body)
	body.set_physics_process(false)
	host.actors[id] = body
	return body

func _query(title: String, source: Vector2, attempted: Vector2, from_room: String, to_room: String, expected: bool) -> void:
	host.player.position = source # Explicit fixture arrangement, never formal state.
	await physics_frame
	await physics_frame
	var proof: Dictionary = adapter.passage_at(source, attempted, from_room, to_room, host.player)
	var motion: Vector2 = attempted - source
	var details := {"source": [source.x, source.y], "attempted": [attempted.x, attempted.y], "from": from_room, "to": to_room,
		"source_cell": [floori(source.x / 32), floori(source.y / 32)], "target_cell": [floori(attempted.x / 32), floori(attempted.y / 32)],
		"actual_source_room": adapter.entered_room(source), "actual_target_room": adapter.entered_room(attempted),
		"source_walkable": adapter.walkable(source, host.player), "target_walkable": adapter.walkable(attempted, host.player),
		"body_test_move_blocked": host.player.test_move(host.player.global_transform, motion), "proof": proof}
	_record({"kind": "fixture_query_input", "title": title, "details": details})
	_check((not proof.is_empty()) == expected, title, details)

func _shot(name: String) -> void:
	await process_frame
	await RenderingServer.frame_post_draw
	var pixels := target.get_texture().get_image()
	pixels.convert(Image.FORMAT_RGBA8)
	archive.Enqueue(pixels.get_data(), pixels.get_width(), pixels.get_height(), output.path_join(name + ".png"), Time.get_ticks_usec() - origin)
	_record({"kind": "screenshot", "file": name + ".png"})

func _run() -> void:
	archive = load("res://scenes/Host/FrameArchive.cs").new()
	root.add_child(archive)
	archive.Configure(0)
	original_locale = TranslationServer.get_locale()
	preferences_before = _preferences()
	_record({"kind": "preferences_before", "locale": original_locale, "sha256": preferences_before})
	_record({"kind": "scope", "statement": "Production adapter / authored map / StoryActor collision fixture. No Main, DV, formal action or progress.",
		"source_reference": "blind-04-open-boundary-closed-evidence.dvlog", "history_reference": "carry-forward-03/carry-forward.dvlog"})
	target = SubViewport.new()
	target.size = Vector2i(1000, 700)
	target.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(target)
	host = FixtureHost.new()
	target.add_child(host)
	adapter = MAP_SCRIPT.new()
	adapter.field = host
	host.add_child(adapter)
	adapter.set_process(false)
	adapter.load_map("living_floor", "")
	host.player = _body("fixture_body", START)
	var camera := Camera2D.new()
	camera.position = START
	camera.zoom = Vector2(3, 3)
	host.add_child(camera)
	camera.make_current()
	var canvas := CanvasLayer.new()
	target.add_child(canvas)
	label = Label.new()
	label.position = Vector2(20, 20)
	label.text = "HISTORICAL GEOMETRY FIXTURE — NO GAME SESSION / NO PROGRESS\nProduction map, adapter and body collision. Query only; body is not traversing."
	canvas.add_child(label)
	await _query("open floor adjacent crossing", START, START + Vector2(-12, 0), "zone_yi:corridor", "zone_yi:hall", true)
	await _shot("01-open-floor-query")
	await _query("non-adjacent cells rejected", START, START + Vector2(-80, 0), "zone_yi:corridor", "zone_yi:hall", false)
	await _query("wrong source room rejected", START, START + Vector2(-12, 0), "zone_yi:hall", "zone_yi:corridor", false)
	await _query("authored wall endpoint rejected", Vector2(2049.2644, 1039), Vector2(2037.2644, 1039), "zone_yi:corridor", "zone_yi:hall", false)
	host.player.position = START
	var blocker := _body("fixture_dynamic_blocker", START + Vector2(-23, 0))
	await physics_frame
	await physics_frame
	await _query("dynamic body blocks adjacent crossing", START, START + Vector2(-12, 0), "zone_yi:corridor", "zone_yi:hall", false)
	_check(host.player.test_move(host.player.global_transform, Vector2(-12, 0)), "dynamic blocker really intersects body motion")
	label.text += "\nDynamic blocker is an explicit fixture body. No NPC was moved in a game."
	await _shot("02-dynamic-body-blocked")
	blocker.queue_free()
	host.actors.erase("fixture_dynamic_blocker")
	# Production phase-to-text helper only. These dictionaries are UI fixtures,
	# never submitted to a host, executor or Session.
	var spatial = WORLD_SCRIPT.SpatialInputInner.new()
	for locale in ["zh_CN", "en", "ja"]:
		TranslationServer.set_locale(locale)
		for phase in ["requested", "prepared", "arrived", "settling", "reconciling"]:
			spatial._continuous = {"phase": phase, "cancelled": false}
			_check(spatial.continuous_hint() != "", "phase text exists", {"locale": locale, "phase": phase, "text": spatial.continuous_hint()})
		spatial._continuous = {"phase": "requested", "cancelled": true}
		_check(spatial.continuous_hint() != "", "cancel text exists", {"locale": locale, "text": spatial.continuous_hint()})
	spatial._continuous.clear()
	_check(spatial.continuous_hint() == "", "cleared request restores no phase text")
	spatial.free()
	TranslationServer.set_locale(original_locale)
	var preferences_after := _preferences()
	_check(preferences_before == preferences_after, "preferences bytes unchanged", {"before": preferences_before, "after": preferences_after, "restored_locale": TranslationServer.get_locale()})
	_record({"kind": "summary", "failures": failures, "limitation": "No formal Prepare/physical crossing/Complete, input routing, modal integration or gameplay acceptance."})
	closing = true
	while not archive.IsIdle(): await process_frame
	for encoded in archive.PollCompleted():
		var result: Dictionary = JSON.parse_string(str(encoded))
		if result.get("error") != null: failures.append(str(result.error))
		_record({"kind": "archive_completion", "details": result})
	while not archive.IsIdle(): await process_frame
	var error := DirAccess.rename_absolute(output.path_join("probe.dvlog.partial"), output.path_join("probe.dvlog"))
	print("GEOMETRY_UI_PROBE_COMPLETE failures=", failures, " rename=", error)
	quit(0 if failures.is_empty() and error == OK else 4)
