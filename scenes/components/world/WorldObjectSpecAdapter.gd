extends RefCounted
## One conversion boundary for ready DV object declarations. Positions are
## building-local design coordinates; shape sizes follow object-local design axes.
const Coordinates = preload("res://scenes/components/world/DesignCoordinates.gd")

static func convert(spec: Dictionary, world_datum: Vector3) -> Dictionary:
	if not world_datum.is_finite(): return {}
	if not spec.has_all(["id", "name", "position", "yaw", "model_forward_yaw", "collision", "interaction"]): return {}
	if not spec.interaction is Dictionary: return {}
	var source: Dictionary = spec.interaction
	if source.get("status", "") != "ready" or source.get("coordinate_space", "") != "building-local-meters" or source.get("size_space", "") != "design-local-meters": return {}
	if not source.has_all(["approach", "stand", "face_target", "contact", "label_position", "area_center", "area_size", "collision_shapes", "actions", "resources", "state_visuals"]): return {}
	var origin := _vector(spec.position)
	if not origin.is_finite(): return {}
	var rotation := Coordinates.yaw_to_godot(float(spec.yaw) - float(spec.model_forward_yaw))
	var basis := Basis(Vector3.UP, deg_to_rad(rotation))
	var result: Dictionary = spec.duplicate(true)
	var target: Dictionary = result.interaction
	for key in ["approach", "stand", "face_target", "contact", "label_position", "area_center"]:
		var point := _vector(source[key])
		if not point.is_finite(): return {}
		target[key] = basis.inverse() * Coordinates.to_godot(point - origin)
	for opt_key in ["sit", "lie", "exit", "placement", "seat_surface", "foot_left", "foot_right"]:
		if source.has(opt_key):
			var opt_pt := _vector(source[opt_key])
			if opt_pt.is_finite():
				target[opt_key] = basis.inverse() * Coordinates.to_godot(opt_pt - origin)
	var area_size := _vector(source.area_size)
	if not area_size.is_finite(): return {}
	target.area_size = Coordinates.size_to_godot(area_size)
	for shape in target.collision_shapes:
		var center := _vector(shape.get("center", []))
		var size := _vector(shape.get("size", []))
		if not center.is_finite() or not size.is_finite(): return {}
		shape.center = basis.inverse() * Coordinates.to_godot(center - origin)
		shape.size = Coordinates.size_to_godot(size)
	result["godot_position"] = Coordinates.to_godot(origin)
	result["godot_world_position"] = Coordinates.to_godot(Coordinates.local_to_world(origin, world_datum))
	result["godot_yaw_degrees"] = rotation
	return result

static func _vector(value: Variant) -> Vector3:
	if not value is Array or value.size() != 3: return Vector3.INF
	for component in value:
		if not (component is float or component is int) or not is_finite(float(component)): return Vector3.INF
	return Vector3(float(value[0]), float(value[1]), float(value[2]))
