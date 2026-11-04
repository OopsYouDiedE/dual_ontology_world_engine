extends RefCounted

## Meter-based, DV-authored fixed valve and mechanical linkage structure.
## This mesh does not submit valve state or claim a completed manipulation.
const Layers = preload("res://scenes/components/world/PhysicsLayers.gd")

static func _point(raw: Array) -> Vector3:
	return Vector3(float(raw[0]), float(raw[2]), -float(raw[1]))

static func _member(root: Node3D, id: String, mesh: Mesh, at: Vector3, material: Material, axis := Vector3.UP) -> void:
	var part := MeshInstance3D.new()
	part.name = id
	part.mesh = mesh
	part.material_override = material
	part.position = at
	part.quaternion = Quaternion(Vector3.UP, axis.normalized())
	root.add_child(part)
	var body := StaticBody3D.new()
	body.name = id + "_solid"
	body.collision_layer = Layers.SOLID
	body.collision_mask = 0
	part.add_child(body)
	var collision := CollisionShape3D.new()
	collision.shape = mesh.create_trimesh_shape()
	body.add_child(collision)

static func create(spec: Dictionary, material: Material) -> Node3D:
	var scene = load("res://scenes/components/architecture/ValveAssembly3D.tscn")
	var root: Node3D = scene.instantiate()
	root.name = "ValveLinkage"
	for raw in spec.get("rods", []):
		var a := _point(raw.from)
		var b := _point(raw.to)
		var radius := float(raw.radius_m)
		if not a.is_finite() or not b.is_finite() or a.distance_to(b) <= 0.0 or radius <= 0.0:
			root.free()
			return null
		var mesh := CylinderMesh.new()
		mesh.top_radius = radius
		mesh.bottom_radius = radius
		mesh.height = a.distance_to(b)
		mesh.radial_segments = 16
		_member(root, str(raw.id), mesh, (a+b)*0.5, material, b-a)
	for raw in spec.get("plates", []):
		var mesh := BoxMesh.new()
		mesh.size = Vector3(float(raw.size[0]),float(raw.size[2]),float(raw.size[1]))
		if mesh.size.x <= 0.0 or mesh.size.y <= 0.0 or mesh.size.z <= 0.0:
			root.free()
			return null
		_member(root, str(raw.id), mesh, _point(raw.center), material)
	for raw in spec.get("wheels", []):
		var mesh := TorusMesh.new()
		var radius := float(raw.radius_m)
		var thickness := float(raw.tube_radius_m)
		if thickness <= 0.0 or radius <= thickness:
			root.free()
			return null
		mesh.inner_radius = radius-thickness
		mesh.outer_radius = radius+thickness
		mesh.rings = 24
		mesh.ring_segments = 8
		_member(root, str(raw.id), mesh, _point(raw.center), material, _point(raw.axis))
	for id in spec.get("contacts", {}):
		var marker := Marker3D.new()
		marker.name = str(id)
		marker.position = _point(spec.contacts[id])
		root.add_child(marker)
	return root
