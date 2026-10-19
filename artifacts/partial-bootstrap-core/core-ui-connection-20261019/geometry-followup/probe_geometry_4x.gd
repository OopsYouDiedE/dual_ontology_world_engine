extends SceneTree
## Geometry-only fixture: no DvSession, Prepare, Complete, knowledge or gameplay result.
const FIELD := preload("res://scenes/Shared/Field/WorldView.gd")
const BRIDGE := preload("res://scenes/Host/DvFieldExecutor.gd")
const MAP := preload("res://scenes/Chapter2/Field/CampusMapView.tscn")
var output := ""
var bindings: Dictionary = {}
var bridge: Node
var field: Node2D
var receipts: Array = []
var timed_out := false
var watchdog: Timer

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	var source := ""
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--probe-out="): output = arg.trim_prefix("--probe-out=")
		elif arg.begins_with("--bindings-from="): source = arg.trim_prefix("--bindings-from=")
	if output == "" or source == "" or DirAccess.dir_exists_absolute(output):
		push_error("Probe requires existing binding source and new output directory")
		quit(2)
		return
	Engine.time_scale = 4.0
	Engine.physics_ticks_per_second = 240
	print(JSON.stringify({"scope": "geometry-only-fixture", "playback_speed": 4.0, "physics_ticks_per_second": 240}))
	bindings = JSON.parse_string(FileAccess.get_file_as_string(source)).get("geometry_bindings", {})
	DirAccess.make_dir_recursive_absolute(output)
	_watchdog()
	await _map_probe("zone_yi", "zone_yi:north_1", "", "site:bed", [
		["E1", "zone_yi:foyer"], ["E2", "zone_yi:foyer"], ["E6", "zone_yi:north_2"],
		["E7", "zone_yi:hall"], ["E10", "zone_yi:prep"], ["E11", "zone_yi:north_1"]])
	await _map_probe("campus_n", "campus_n:campus_n", "hall_north", "", [
		["outdoor_meter", "campus_n:campus_n"], ["outdoor_high", "campus_n:campus_n"]])
	await _map_probe("campus_sw", "campus_sw:campus_sw", "living_north", "", [
		["lane_east", "campus_sw:campus_sw"]])
	var file := FileAccess.open(output.path_join("receipts.json"), FileAccess.WRITE)
	file.store_string(JSON.stringify({"scope": "geometry-only-fixture", "timed_out": timed_out, "receipts": receipts}, "  "))
	file.close()
	watchdog.stop()
	watchdog.queue_free()
	await process_frame
	quit(2 if timed_out else 0)

func _watchdog() -> void:
	watchdog = Timer.new()
	watchdog.one_shot = true
	watchdog.ignore_time_scale = true
	root.add_child(watchdog)
	watchdog.timeout.connect(func():
		timed_out = true
		print("geometry_fixture_timeout")
		if is_instance_valid(bridge): bridge.begin_shutdown()
		if is_instance_valid(field): field.begin_shutdown())
	watchdog.start(100.0)

func _map_probe(map_name: String, room: String, spawn: String, initial_anchor: String, targets: Array) -> void:
	if timed_out: return
	bridge = BRIDGE.new()
	root.add_child(bridge)
	var snapshot := {"resource": "res://scenes/Chapter2/Field/CampusMapView.tscn", "map": map_name,
		"execution_room": room, "spawn": spawn, "initial_anchor": initial_anchor,
		"presentation": {"player_actor": "geometry_probe", "player_sprite": "detective"}, "people": [],
		"access": {"maps": {"living_hall": false}}, "geometry_bindings": bindings}
	field = Node2D.new()
	field.set_script(FIELD)
	root.add_child(field)
	bridge.configure_visible(field, snapshot)
	var adapter := MAP.instantiate()
	adapter.presentation = snapshot.presentation
	field.setup(adapter)
	bridge.bind_visible(field, snapshot)
	await physics_frame
	await physics_frame
	for target in targets:
		if timed_out: break
		var station := str(target[0]) if str(target[0]) == "lane_east" else "site:" + str(target[0])
		var action := "geometry-only:" + str(target[0])
		var destination := {"map": map_name, "room": target[1], "execution_room": target[1], "departure_station": station}
		if station == "lane_east":
			destination.erase("departure_station")
			destination.anchor = station
		var intent := {"actor": "geometry_probe", "action": {"id": action, "kind": "move", "travel": {} if station == "lane_east" else {"station": station}}, "destination": destination}
		if field.player != null: snapshot.execution_room = adapter.entered_room(field.player.position)
		var receipt: Dictionary = await bridge.execute(intent, field, snapshot, receipts.size() + 1)
		receipts.append(receipt)
		print(JSON.stringify(receipt))
		await RenderingServer.frame_post_draw
		root.get_texture().get_image().save_png(output.path_join(str(target[0]) + ".png"))
	bridge.retain_field(field)
	field.begin_shutdown()
	root.remove_child(field)
	field.queue_free()
	bridge.reset()
	root.remove_child(bridge)
	bridge.queue_free()
	await process_frame
