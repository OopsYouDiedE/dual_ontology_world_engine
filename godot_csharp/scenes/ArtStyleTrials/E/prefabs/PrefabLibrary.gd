extends RefCounted
## Shared, immutable PackedScene resources. Instances own placement, not geometry.
## Builders are authoring code only; they must not mutate gameplay/observer state.
static var _scenes: Dictionary = {}

static func instantiate(key: String, builder: Callable) -> Node3D:
	if not _scenes.has(key):
		var source: Node3D = builder.call()
		if source.get_parent(): source.get_parent().remove_child(source)
		_own_children(source, source)
		var packed := PackedScene.new()
		var error: Error = packed.pack(source)
		source.free()
		if error != OK:
			push_error("Cannot pack furniture prefab %s: %s" % [key, error])
			return null
		_scenes[key] = packed
	var instance: Node3D = (_scenes[key] as PackedScene).instantiate()
	instance.set_meta("prefab_id", key)
	return instance

static func _own_children(node: Node, root: Node) -> void:
	for child in node.get_children():
		child.owner = root
		_own_children(child, root)

static func save(key: String, path: String) -> Error:
	if not _scenes.has(key): return ERR_DOES_NOT_EXIST
	return ResourceSaver.save(_scenes[key], path)

static func keys() -> Array:
	return _scenes.keys()

static func place_helper(kit: Node3D, key: String, at: Vector3, title: String, builder: Callable) -> Node3D:
	# Adapts geometry helpers which add children (and optionally OmniLights) to
	# content_root. Capture at origin. Re-register *instance* lights, never the
	# temporary source lights. Fire/actor/authority helpers are intentionally excluded.
	var instance := instantiate(key, func() -> Node3D:
		var previous: Node3D = kit.content_root
		var source := Node3D.new()
		source.name = "FurniturePrefab"
		var previous_lights: int = kit.local_lights.size()
		kit.content_root = source
		builder.call()
		kit.content_root = previous
		kit.local_lights.resize(previous_lights)
		return source)
	if instance == null: return null
	instance.name = title
	instance.set_meta("authoring_label", title)
	instance.position = at
	kit.content_root.add_child(instance)
	_register_lights(kit, instance)
	return instance

static func _register_lights(kit: Node3D, node: Node) -> void:
	if node is OmniLight3D:
		node.set_meta("room", kit.current_room_id)
		kit.local_lights.append(node)
	for child in node.get_children(): _register_lights(kit, child)
