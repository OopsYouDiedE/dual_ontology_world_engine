extends SceneTree
## 自动回归：对每张地图的每个出口，把侦探放进出口区域，确认切换到目标地图并落在目标出生点附近。
## 运行：godot --headless --path godot_csharp --script res://scenes/Chapter2/Field/check_transitions.gd

const FIELD := preload("res://scenes/Chapter2/Field/Chapter2Field.tscn")
const MAPS := ["living_hall", "zone_jia", "zone_yi", "zone_bing", "zone_ding", "central_hall", "lecture", "archive",
	"counterweight", "resonance", "clinic", "logistics", "granary", "greenhouse", "campus"]

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var field: Node2D = FIELD.instantiate()
	root.add_child(field)
	await process_frame
	var failures := 0
	var total := 0
	for key in MAPS:
		field.load_map(key, "")
		await process_frame
		var names: Array = []
		for e in field.map.get_node("Exits").get_children():
			names.append([e.name, String(e.get_meta("target")), String(e.get_meta("spawn"))])
		for item in names:
			total += 1
			field.load_map(key, "")
			await process_frame
			await process_frame
			field.armed = true
			var exit: Area2D = field.map.get_node("Exits/" + item[0])
			field.player.global_position = exit.global_position
			for i in 6:
				await physics_frame
			for i in 3:
				await process_frame
			var now := String(field.map.name)
			var marker := field.map.get_node_or_null("Spawns/" + item[2]) as Marker2D
			var ok: bool = now == item[1] and marker != null and field.player.position.distance_to(marker.position) < 4.0
			if not ok:
				failures += 1
				print("FAIL %s.%s -> %s/%s (now %s)" % [key, item[0], item[1], item[2], now])
	print("transitions checked: %d, failures: %d" % [total, failures])
	quit(1 if failures else 0)
