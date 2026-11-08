extends SceneTree
func _init():
	call_deferred('_run')
func _run():
	var root = get_root()
	var main = load('res://scenes/main/Main.tscn').instantiate()
	root.add_child(main)
	await process_frame
	await process_frame
	await main.start_new_game()
	await process_frame
	await process_frame
	var field = main.field
	var point = Vector2(450, 340)
	var origin = field.camera.project_ray_origin(point)
	var normal = field.camera.project_ray_normal(point)
	var query = PhysicsRayQueryParameters3D.create(origin, origin + normal * 150.0, 7)
	var hit = field.get_world_3d().direct_space_state.intersect_ray(query)
	var node = hit.collider
	while node != null:
		print('NODE: ', node.name, ' meta campaign_actor: ', node.has_meta('campaign_actor'), ' meta campaign_component: ', node.has_meta('campaign_component'))
		if node.has_meta('campaign_component'):
			var id = str(node.get_meta('campaign_component'))
			var c = field._components.get(id)
			print('  comp id: ', id, ' comp: ', c)
			if c:
				print('  comp_kind: ', c.get('component_kind'))
		node = node.get_parent()
	quit()
