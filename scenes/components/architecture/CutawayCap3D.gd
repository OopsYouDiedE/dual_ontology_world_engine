extends RefCounted
## Display-only sections of the actual solid mesh. Door voids are separate loops.
static var _geometry = preload("res://scenes/components/architecture/CutawayGeometry.cs").new()

static func create(source: MeshInstance3D, elevation: float, axis: int = 1, outward: Vector3 = Vector3.UP) -> MeshInstance3D:
	var mesh: ArrayMesh = _geometry.Build(source.mesh, source.global_transform, elevation, axis, outward)
	if mesh == null: return null
	var cap := MeshInstance3D.new()
	cap.name = "CutawaySection"
	cap.mesh = mesh
	cap.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	source.add_child(cap)
	return cap
