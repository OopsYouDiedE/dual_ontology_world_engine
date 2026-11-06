class_name BuildRoutes
extends RefCounted

## 跨楼层/楼梯寻路规划器（BuildRoutes.gd）

static func plan(map: RID, _stairs: Variant, start: Vector3, target: Vector3) -> PackedVector3Array:
	if not map.is_valid():
		return PackedVector3Array()
	return NavigationServer3D.map_get_path(map, start, target, true)
