extends RefCounted
## Shared, immutable PackedScene resources. Instances own placement, not geometry.
## Builders are authoring code only; they must not mutate gameplay/observer state.
static var _scenes: Dictionary = {}
static var authoring := false
static var _physics: Dictionary = {}
static var _categories: Dictionary = {}
const DIRECTORY := "res://scenes/prefabs/"
const SLOTS := preload("res://scenes/components/prefabs/InteractionSlots.gd")

static func resource_path(key: String) -> String:
	var label := key.replace("/", "_").validate_filename().substr(0, 90)
	return DIRECTORY + label + "_" + key.sha256_text().substr(0, 8) + ".tscn"

static func category_for(kind: String) -> StringName:
	if _physics.is_empty():
		_physics = preload("res://scenes/main/DvPresentationContent.cs").new().ReadRecord("res://assets/data/presentation.dv", "layouts", "prop_physics")
	return &"furniture" if kind in _physics.get("furniture", []) else &"decoration"

static func instantiate(key: String, category: StringName, builder: Callable) -> Node3D:
	if _categories.has(key) and _categories[key] != category:
		push_error("Conflicting physics categories for prefab: " + key)
		return null
	if not _scenes.has(key):
		var source: Node3D
		if not authoring and ResourceLoader.exists(resource_path(key)):
			source = (load(resource_path(key)) as PackedScene).instantiate() as Node3D
		else:
			source = builder.call()
		if source == null: return null
		if source.get_parent(): source.get_parent().remove_child(source)
		SLOTS.attach(source, category)
		source.set_meta("prefab_id", key)
		_own_children(source, source)
		var packed := PackedScene.new()
		var error: Error = packed.pack(source)
		source.free()
		if error != OK:
			push_error("Cannot pack furniture prefab %s: %s" % [key, error])
			return null
		_scenes[key] = packed
		_categories[key] = category
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

static func place_helper(kit: Node3D, key: String, at: Vector3, title: String, category: StringName, builder: Callable) -> Node3D:
	# Adapts geometry helpers which add children (and optionally OmniLights) to
	# content_root. Capture at origin. Re-register *instance* lights, never the
	# temporary source lights. Fire/actor/authority helpers are intentionally excluded.
	var instance := instantiate(key, category, func() -> Node3D:
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
