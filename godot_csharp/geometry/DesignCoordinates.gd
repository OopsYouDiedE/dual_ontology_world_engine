extends RefCounted
## Design coordinates: metres, X east, Y north, Z up. Godot: X east, Y up, Z south.
static func to_godot(design: Vector3) -> Vector3:
	return Vector3(design.x, design.z, -design.y)

static func to_design(godot: Vector3) -> Vector3:
	return Vector3(godot.x, -godot.z, godot.y)

static func size_to_godot(size: Vector3) -> Vector3:
	return Vector3(size.x, size.z, size.y)

static func yaw_to_godot(degrees: float) -> float:
	return -degrees

## Datum is authored in DV; callers supply it without an implicit origin.
static func local_to_world(local: Vector3, datum: Vector3) -> Vector3:
	return local + datum

static func world_to_local(world: Vector3, datum: Vector3) -> Vector3:
	return world - datum
