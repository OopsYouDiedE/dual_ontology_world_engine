extends RefCounted
## Ceiling underside uses the supplied elevation; the slab extends upward.
## Callers supply the same authoritative hole rings as the adjacent slab.
const FloorSurface = preload("res://geometry/FloorSurface3D.gd")
const PhysicsLayers = preload("res://geometry/PhysicsLayers.gd")

static func create(id: String, outer: PackedVector2Array, holes: Array[PackedVector2Array], underside_m: float, thickness_m: float, material: Material, collision_enabled: bool = true) -> Node3D:
	if not is_finite(underside_m):
		push_error("CeilingSurface3D " + id + ": invalid-underside")
		return null
	var root: Node3D = FloorSurface.create(id, outer, holes, underside_m + thickness_m, thickness_m, material)
	if root == null: return null
	root.set_meta("kind", "ceiling")
	var collision_root := root.get_node("CollisionRoot")
	collision_root.get_node("StaticBody3D").collision_layer = PhysicsLayers.SOLID
	if not collision_enabled:
		collision_root.get_node("StaticBody3D/CollisionShape3D").disabled = true
	return root

static func set_visual_visible(root: Node3D, visible: bool) -> void:
	root.get_node("VisualRoot").visible = visible

