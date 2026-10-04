extends SceneTree
const OUT := "C:/Users/iii/Desktop/dual_ontology_world_engine/artifacts/remake-chapter2/shared-runtime/"
var game: Node
func require(ok: bool, label: String) -> void:
	if not ok:
		push_error("FAIL " + label)
		quit(1)
	else:
		print("PASS " + label)
func _initialize() -> void:
	call_deferred("run")
func settle() -> void:
	for i in 8: await process_frame
	await RenderingServer.frame_post_draw
func run() -> void:
	var original := GDScript.new()
	original.source_code = FileAccess.get_file_as_string("C:/Users/iii/Desktop/dual_ontology_world_engine/tmp/original-awakening-content.gd")
	require(original.reload() == OK, "original content source parses")
	var constants: Dictionary = original.get_script_constant_map()
	var data: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://scenes/Awakening/AwakeningContent.json"))
	for key in data:
		require(JSON.stringify(constants[key]) == JSON.stringify(data[key]), "content migration exact " + key)
	game = load("res://scenes/Chapter2/Game/Chapter2Game.tscn").instantiate()
	root.add_child(game)
	var deadline := Time.get_ticks_msec() + 12000
	while game.busy and Time.get_ticks_msec() < deadline: await process_frame
	require(not game.busy, "chapter2 bridge starts")
	await settle()
	game.close_modal()
	var real: Dictionary = game.bridge.Snapshot().duplicate(true)
	var map_name := String(game.map.name)
	var pos: Vector2 = game.player.position
	var stage: Node = load("res://scenes/Chapter2/Game/ReplayStage.gd").new()
	game.add_child(stage)
	stage.host = game
	stage._setup()
	stage._apply({"place":"Clinic", "figure":"身影", "text":"自动验证重演　心声：切图不应改变现实"}, {})
	await settle()
	require(String(game.map.name) == "clinic", "replay switches displayed map")
	require(game.bridge.Snapshot().det_place == real.det_place, "replay map does not change real location")
	root.get_texture().get_image().save_png(OUT + "replay-clinic.png")
	stage._teardown()
	await settle()
	require(String(game.map.name) == map_name and game.player.position == pos, "replay restores map and position")
	require(game.bridge.Snapshot().det_place == real.det_place, "replay teardown preserves real location")
	root.get_texture().get_image().save_png(OUT + "replay-restored.png")
	game.load_map("campus_n", "", Vector2(300,300), true)
	await settle()
	var unknown: Dictionary = game.bridge.Snapshot()
	require(unknown.room.actions.is_empty() and unknown.room.people.is_empty(), "unknown map snapshot has no previous room actions")
	root.get_texture().get_image().save_png(OUT + "unknown-map.png")
	print("AUTO VALIDATION ONLY; INJECTED REPLAY FRAME; NOT BLIND PLAYTEST")
	game.queue_free()
	quit(0)
