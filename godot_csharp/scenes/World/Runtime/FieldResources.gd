extends RefCounted
## Main-thread coordination for background resource I/O. SceneTree mutation stays
## with the caller; this cache contains resources only, never actors or DV state.

const MAX_RETAINED_RESOURCES := 64
static var _resources: Dictionary = {}
static var _order: Array[String] = []
static var _pending: Dictionary = {}
static var _failed: Dictionary = {}
static var _actor_paths: Dictionary = {}

## Asset identity is immutable for one running build. Resolve it during chapter
## preparation, not on the first frame in which somebody enters the doorway.
static func actor_path(sprite: String) -> String:
	if sprite == "": return ""
	if _actor_paths.has(sprite): return str(_actor_paths[sprite])
	for folder in ["Characters/PixelCast", "Characters/AnimeCast", "Props/GameplayFixtures"]:
		var path := "res://ShareAsset/Internal/%s/%s.tres" % [folder, sprite]
		if ResourceLoader.exists(path):
			_actor_paths[sprite] = path
			return path
	return ""

static func _remember(path: String, resource: Resource) -> Resource:
	if resource == null: return null
	_resources[path] = resource
	_order.erase(path)
	_order.append(path)
	while _order.size() > MAX_RETAINED_RESOURCES:
		_resources.erase(_order.pop_front())
	return resource

static func request(path: String) -> bool:
	if path == "" or _failed.has(path): return false
	if _resources.has(path) or _pending.has(path): return true
	if ResourceLoader.has_cached(path):
		return _remember(path, ResourceLoader.get_cached_ref(path)) != null
	if not ResourceLoader.exists(path): return false
	# Subthreads are intentionally off: background I/O should not fan out and
	# compete with the physics/main thread while a character is walking.
	if ResourceLoader.load_threaded_request(path, "", false) != OK:
		_failed[path] = true
		return false
	_pending[path] = true
	return true

static func ready(path: String) -> Resource:
	if _resources.has(path): return _remember(path, _resources[path])
	if _pending.has(path):
		var status := ResourceLoader.load_threaded_get_status(path)
		if status == ResourceLoader.THREAD_LOAD_LOADED:
			_pending.erase(path)
			return _remember(path, ResourceLoader.load_threaded_get(path))
		if status in [ResourceLoader.THREAD_LOAD_FAILED, ResourceLoader.THREAD_LOAD_INVALID_RESOURCE]:
			_pending.erase(path)
			_failed[path] = true
		return null
	if ResourceLoader.has_cached(path):
		return _remember(path, ResourceLoader.get_cached_ref(path))
	return null

## The returned batch pins every resource until the caller has assigned it to
## its nodes. A batch may be larger than the small idle-resource LRU.
static func wait_for(paths: Array[String], tree: SceneTree, cancelled: Callable) -> Dictionary:
	var resources := {}
	for path in paths:
		if not request(path): return {}
		var resource := ready(path)
		if resource != null: resources[path] = resource
	while true:
		if tree == null or not cancelled.is_valid() or cancelled.call(): return {}
		var complete := true
		for path in paths:
			if resources.has(path): continue
			var resource := ready(path)
			if resource != null:
				resources[path] = resource
				continue
			if _failed.has(path): return {}
			complete = false
		if complete: return resources
		# load_threaded_get is called only after THREAD_LOAD_LOADED, never as a
		# blocking substitute for waiting across rendered frames.
		await tree.process_frame
	return {}
