extends RefCounted
## Geometry authoring only. The host decides occupancy, reachability and permission.
static func _bounds(node: Node3D, transform: Transform3D, output: Array[AABB]) -> void:
	var combined := transform * node.transform
	if node is MeshInstance3D and node.mesh != null:
		output.append(combined * node.get_aabb())
	for child in node.get_children():
		if child is Node3D: _bounds(child, combined, output)

static func attach(root: Node3D, category: StringName) -> void:
	root.set_meta("world_category", category)
	# Normalize previously exported scenes as well as freshly built geometry.
	for path in ["Solid", "InteractionSlots"]:
		var previous := root.get_node_or_null(NodePath(path))
		if previous != null:
			root.remove_child(previous)
			previous.free()
	if root.get_script() == null:
		root.set_script(preload("res://scenes/World/Prefabs/InteractiveProp.gd"))
	if category in [&"furniture", &"floor", &"wall"]: _add_collision(root)
	var pieces: Array[AABB] = []
	# Slot positions are local to the prefab root. Exclude its transform so a
	# positioned/scaled builder root is not applied again to child geometry.
	for child in root.get_children():
		if child is Node3D: _bounds(child, Transform3D.IDENTITY, pieces)
	if pieces.is_empty(): return
	var bounds: AABB = pieces[0]
	for piece in pieces: bounds = bounds.merge(piece)
	var slots := Node3D.new()
	slots.name = "InteractionSlots"
	root.add_child(slots)
	var center := bounds.get_center()
	for side in [-1, 1]:
		var marker := Marker3D.new()
		marker.name = "Front" if side == 1 else "Back"
		marker.position = Vector3(center.x, 0, center.z + side * (bounds.size.z * 0.5 + 0.45))
		marker.rotation.y = PI if side == 1 else 0.0
		marker.set_meta("clearance_radius_m", 0.3)
		marker.set_meta("pose", "stand")
		marker.set_meta("availability", "host_validation_required")
		slots.add_child(marker)
	var target := Marker3D.new()
	target.name = "ContactTarget"
	target.position = Vector3(center.x, minf(bounds.end.y, 1.15), center.z)
	slots.add_child(target)

static func _faces(node: Node3D, parent_transform: Transform3D, triangles: Array[Vector3]) -> void:
	# Nested furniture retains its own body; decorative prefab children stay non-solid.
	if node.has_node("Solid") or node.get_meta("world_category", &"") == &"decoration": return
	var transform := parent_transform * node.transform
	if node is MeshInstance3D and node.mesh != null:
		for vertex in node.mesh.get_faces(): triangles.append(transform * vertex)
	for child in node.get_children():
		if child is Node3D: _faces(child, transform, triangles)

static func _add_collision(node: Node3D) -> void:
	var triangles: Array[Vector3] = []
	# Collision faces are stored in the root body's local space.
	for child in node.get_children():
		if child is Node3D: _faces(child, Transform3D.IDENTITY, triangles)
	if triangles.is_empty(): return
	var body := StaticBody3D.new()
	body.name = "Solid"
	body.collision_layer = 1
	body.collision_mask = 0
	body.set_meta("collision_category", node.get_meta("world_category"))
	var collision := CollisionShape3D.new()
	var shape := ConcavePolygonShape3D.new()
	shape.backface_collision = true
	shape.set_faces(PackedVector3Array(triangles))
	collision.shape = shape
	body.add_child(collision)
	node.add_child(body)
