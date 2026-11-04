extends RefCounted
## Building views send requests here through Main; no direct Core references are exposed.
var _host: WeakRef
var _views: Dictionary = {}

func _init(main: Node) -> void:
	_host = weakref(main)

func register_view(view: Node3D, bindings: Dictionary) -> void:
	var id := view.get_instance_id()
	if _views.has(id):
		unregister_view(view)
	var callback := _request.bind(id)
	view.interaction_requested.connect(callback)
	var furniture_callback := _request_furniture.bind(id)
	if view.has_signal("furniture_requested"):
		view.furniture_requested.connect(furniture_callback)
	_views[id] = {"view": weakref(view), "bindings": bindings.duplicate(true), "callback": callback, "furniture_callback": furniture_callback}

func unregister_view(view: Node3D) -> void:
	var id := view.get_instance_id()
	if not _views.has(id):
		return
	var callback: Callable = _views[id].callback
	if view.interaction_requested.is_connected(callback):
		view.interaction_requested.disconnect(callback)
	var furniture_callback: Callable = _views[id].furniture_callback
	if view.has_signal("furniture_requested") and view.furniture_requested.is_connected(furniture_callback):
		view.furniture_requested.disconnect(furniture_callback)
	_views.erase(id)

func _request(building_id: String, portal_id: String, view_id: int) -> void:
	var host: Node = _host.get_ref()
	if host == null or not _views.has(view_id) or not host._formal_selection_ready():
		return
	var binding: Dictionary = _views[view_id].bindings.get(building_id + "/" + portal_id, {})
	var action_id := str(binding.get("action_id", ""))
	var site_id := str(binding.get("site_id", ""))
	if action_id.is_empty() or site_id.is_empty():
		return
	# Reuse the existing approach/contact/Complete pipeline. Never synthesize completion.
	if not is_instance_valid(host.field) or not is_instance_valid(host.field.spatial):
		return
	for option: Dictionary in host.field.spatial.options:
		if str(option.get("id", "")) == action_id:
			host.dispatch({"kind": "spatial_select", "choice_id": action_id, "site": site_id})
			return

func apply_projection(snapshot: Dictionary) -> void:
	# Only already accepted observer projection is consumed. Absence is not an open door.
	var states: Dictionary = snapshot.get("building_doors", {})
	for id in _views.keys():
		var view: Node3D = _views[id].view.get_ref()
		if view == null:
			_views.erase(id)
			continue
		var selected := {}
		for key: String in _views[id].bindings:
			var reference := str(_views[id].bindings[key].get("projection_key", key))
			if states.has(reference):
				selected[key] = states[reference].duplicate(true)
		view.apply_door_projection(selected)
		if view.has_method("apply_furniture_projection"):
			view.apply_furniture_projection(snapshot.get("building_furniture", {}))

func _request_furniture(building_id: String, object_id: String, slot_id: String, view_id: int) -> void:
	var host: Node = _host.get_ref()
	if host == null or not _views.has(view_id) or not host._formal_selection_ready():
		return
	var view: Node3D = _views[view_id].view.get_ref()
	if view == null or not is_instance_valid(host.field) or not is_instance_valid(host.field.spatial):
		return
	var binding: Dictionary = view.furniture_bindings.get(building_id + "/" + object_id, {}).get(slot_id, {})
	var action_id := str(binding.get("action_id", ""))
	var site_id := str(binding.get("site_id", ""))
	if action_id.is_empty() or site_id.is_empty():
		return
	for option: Dictionary in host.field.spatial.options:
		if str(option.get("id", "")) == action_id:
			host.dispatch({"kind": "spatial_select", "choice_id": action_id, "site": site_id})
			return
