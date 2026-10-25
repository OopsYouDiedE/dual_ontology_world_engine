extends SceneTree
## Isolated layout fixture. No session, clock, answers or world actions.
var output := ""
var observations: Array = []

func _initialize() -> void:
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--out="): output = arg.trim_prefix("--out=")
	_run.call_deferred()

func _run() -> void:
	if output.is_empty():
		quit(2)
		return
	DirAccess.make_dir_recursive_absolute(output)
	var contract: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://Contracts/geometry_contract.json"))
	var failed := false
	for key in ["zone_yi", "zone_jia", "zone_bing", "zone_ding", "living_hall", "logistics", "granary"]:
		var map: Node2D = load(str(contract.maps[key].scene)).instantiate()
		root.add_child(map)
		await physics_frame
		var spawn: Vector2 = map.get_node("Spawns").get_child(0).position
		for site in map.get_node("Sites").get_children():
			var body := CharacterBody2D.new()
			body.collision_layer = 2
			body.collision_mask = 1
			var shape := CollisionShape2D.new()
			var box := RectangleShape2D.new()
			box.size = Vector2(16,12)
			shape.shape = box
			shape.position.y = -6
			body.add_child(shape)
			body.position = spawn
			map.add_child(body)
			await physics_frame
			var target: Vector2 = site.position
			var queue: Array[Vector2] = [spawn]
			var parents: Dictionary = {spawn: spawn}
			var cursor := 0
			var found := Vector2.INF
			while cursor < queue.size() and cursor < 20000:
				var point := queue[cursor]
				cursor += 1
				if point.distance_to(target) <= 24 and not body.test_move(Transform2D(0,point),target-point):
					found = point
					break
				for direction in [Vector2.LEFT,Vector2.RIGHT,Vector2.UP,Vector2.DOWN]:
					var next: Vector2 = point + direction * 16
					if parents.has(next) or next.x < 0 or next.y < 0 or next.x >= map.get_meta("map_size").x or next.y >= map.get_meta("map_size").y: continue
					if body.test_move(Transform2D(0,point),next-point): continue
					parents[next] = point
					queue.append(next)
			var route: Array[Vector2] = []
			var ok := found != Vector2.INF
			if ok:
				var point := found
				while point != spawn:
					route.push_front(point)
					point = parents[point]
				route.append(target)
				for destination in route:
					if body.move_and_collide(destination-body.position) != null:
						ok = false
						break
			failed = failed or not ok
			observations.append({"map":key,"site":str(site.name),"spawn":spawn,"target":target,"route":route,"actual_end":body.position,"reached":ok,"searched":cursor})
			print(key, ":",site.name," reached=",ok)
			body.queue_free()
			await physics_frame
		map.queue_free()
		await physics_frame
	var bytes := var_to_bytes(observations)
	var hash := HashingContext.new()
	hash.start(HashingContext.HASH_SHA256)
	hash.update(bytes)
	var file := FileAccess.open(output.path_join("motion.dvlog.partial"),FileAccess.WRITE)
	file.store_buffer("DVMOTION1".to_ascii_buffer())
	file.store_64(bytes.size())
	file.store_buffer(hash.finish())
	file.store_buffer(bytes)
	file.close()
	DirAccess.rename_absolute(output.path_join("motion.dvlog.partial"),output.path_join("motion.dvlog"))
	quit(1 if failed else 0)
