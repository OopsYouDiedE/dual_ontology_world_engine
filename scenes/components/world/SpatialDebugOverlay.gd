extends RefCounted
## Spatial Debug Visualization Overlay (Task 4.12).
## Visualizes collision bodies, interaction slots (Stand, Approach, Contact),
## and WorldObject IDs without altering gameplay state or polluting player UI.
const PhysicsLayers = preload("res://scenes/components/world/PhysicsLayers.gd")

static func toggle(stage_root: Node3D, enabled: bool, show_colliders: bool = true, show_slots: bool = true, show_ids: bool = true) -> Dictionary:
	if stage_root == null or not is_instance_valid(stage_root):
		return {"active": false, "colliders_shown": 0, "slots_shown": 0, "ids_shown": 0}
	
	var overlay: Node3D = stage_root.get_node_or_null("_SpatialDebugOverlay") as Node3D
	if not enabled:
		if overlay != null:
			stage_root.remove_child(overlay)
			overlay.queue_free()
		return {"active": false, "colliders_shown": 0, "slots_shown": 0, "ids_shown": 0}
	
	if overlay != null:
		stage_root.remove_child(overlay)
		overlay.queue_free()
	
	overlay = Node3D.new()
	overlay.name = "_SpatialDebugOverlay"
	stage_root.add_child(overlay)
	
	var stats := {"active": true, "colliders_shown": 0, "slots_shown": 0, "ids_shown": 0}
	_collect_debug_nodes(stage_root, overlay, show_colliders, show_slots, show_ids, stats)
	return stats

static func _safe_xform(target: Node3D, root_ref: Node3D) -> Transform3D:
	if target.is_inside_tree() and root_ref.is_inside_tree():
		return root_ref.global_transform.affine_inverse() * target.global_transform
	var cur: Node = target
	var xform := Transform3D.IDENTITY
	var path_nodes: Array[Node3D] = []
	while cur != null and cur != root_ref:
		if cur is Node3D:
			path_nodes.append(cur as Node3D)
		cur = cur.get_parent()
	for i in range(path_nodes.size() - 1, -1, -1):
		xform = xform * path_nodes[i].transform
	return xform

static func _collect_debug_nodes(node: Node, overlay: Node3D, show_colliders: bool, show_slots: bool, show_ids: bool, stats: Dictionary) -> void:
	if node == overlay:
		return
	
	# 1. Collision Visuals (CollisionShape3D)
	if show_colliders and node is CollisionShape3D:
		var col_shape := node as CollisionShape3D
		if col_shape.shape != null and not col_shape.disabled:
			var dbg_mesh := MeshInstance3D.new()
			dbg_mesh.name = "DbgCol_" + str(node.get_instance_id())
			var wire_mat := StandardMaterial3D.new()
			wire_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
			wire_mat.albedo_color = Color(0.2, 0.9, 0.9, 0.35)
			wire_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
			
			if col_shape.shape is BoxShape3D:
				var box: BoxShape3D = col_shape.shape as BoxShape3D
				var mesh := BoxMesh.new()
				mesh.size = box.size
				dbg_mesh.mesh = mesh
				dbg_mesh.material_override = wire_mat
				dbg_mesh.transform = _safe_xform(col_shape, overlay)
				overlay.add_child(dbg_mesh)
				stats["colliders_shown"] = int(stats["colliders_shown"]) + 1
			elif col_shape.shape is CapsuleShape3D:
				var cap: CapsuleShape3D = col_shape.shape as CapsuleShape3D
				var mesh := CapsuleMesh.new()
				mesh.radius = cap.radius
				mesh.height = cap.height
				dbg_mesh.mesh = mesh
				dbg_mesh.material_override = wire_mat
				dbg_mesh.transform = _safe_xform(col_shape, overlay)
				overlay.add_child(dbg_mesh)
				stats["colliders_shown"] = int(stats["colliders_shown"]) + 1
	
	# 2. Slot Markers (Marker3D in InteractionSlots)
	if show_slots and node is Marker3D:
		var marker := node as Marker3D
		var m_name := str(marker.name)
		if m_name in ["Stand", "Approach", "Contact", "Sit", "Lie", "Sit_Left", "Sit_Right"]:
			var pin := MeshInstance3D.new()
			pin.name = "DbgSlot_" + str(marker.get_instance_id())
			var slot_mat := StandardMaterial3D.new()
			slot_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
			slot_mat.albedo_color = Color.GREEN if "Stand" in m_name else (Color.YELLOW if "Approach" in m_name else (Color.MAGENTA if "Contact" in m_name else Color.CYAN))
			var sphere := SphereMesh.new()
			sphere.radius = 0.08
			sphere.height = 0.16
			pin.mesh = sphere
			pin.material_override = slot_mat
			pin.transform = _safe_xform(marker, overlay)
			overlay.add_child(pin)
			stats["slots_shown"] = int(stats["slots_shown"]) + 1
	
	# 3. Object IDs
	if show_ids and node.has_meta("world_object_id"):
		var obj_id := str(node.get_meta("world_object_id"))
		if not obj_id.is_empty():
			var label := Label3D.new()
			label.name = "DbgLabel_" + str(node.get_instance_id())
			label.text = "[%s]" % obj_id
			label.font_size = 18
			label.modulate = Color.YELLOW
			label.no_depth_test = true
			label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
			if node is Node3D:
				label.transform = _safe_xform(node as Node3D, overlay).translated_local(Vector3(0.0, 1.2, 0.0))
			overlay.add_child(label)
			stats["ids_shown"] = int(stats["ids_shown"]) + 1
	
	for child in node.get_children():
		_collect_debug_nodes(child, overlay, show_colliders, show_slots, show_ids, stats)
