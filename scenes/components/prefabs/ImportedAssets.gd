extends RefCounted
## Approved CC0 geometry only; source files, checksums and uses live in the manifest.
const TREE := preload("res://assets/external/KenneyNature/tree_detailed_dark.glb")
const BUSH := preload("res://assets/external/KenneyNature/plant_bushDetailed.glb")
const TEA_TABLE := preload("res://assets/external/PolyHaven/ChineseTeaTable/chinese_tea_table_1k.gltf")
const MATERIALS := preload("res://scenes/components/rendering/MaterialLibrary.gd")
const FOLIAGE := preload("res://assets/materials/surfaces/foliage.gdshader")

static func _collect_bounds(node: Node3D, parent_transform: Transform3D, result: Array[AABB]) -> void:
	var local: Transform3D = parent_transform * node.transform
	if node is MeshInstance3D and node.mesh != null:
		result.append(local * node.get_aabb())
	for child in node.get_children():
		if child is Node3D: _collect_bounds(child, local, result)

static func _instantiate(packed: PackedScene, title: String, target_height: float = 0.0) -> Node3D:
	var model := packed.instantiate() as Node3D
	var bounds: Array[AABB] = []
	_collect_bounds(model, Transform3D.IDENTITY, bounds)
	if bounds.is_empty():
		model.free()
		push_error("Imported model has no geometry: " + packed.resource_path)
		return null
	var combined: AABB = bounds[0]
	for bound in bounds: combined = combined.merge(bound)
	if combined.size.y <= 0.0:
		model.free()
		push_error("Imported model has no measurable height: " + packed.resource_path)
		return null
	var factor: float = target_height / combined.size.y if target_height > 0.0 else 1.0
	var holder := Node3D.new()
	holder.name = title
	holder.add_child(model)
	model.position -= Vector3(combined.get_center().x, combined.position.y, combined.get_center().z)
	# Uniform scaling preserves the authored body and crown proportions.
	holder.scale = Vector3.ONE * factor
	holder.set_meta("asset_source", packed.resource_path)
	holder.set_meta("license", "CC0-1.0")
	holder.set_meta("source_dimensions", combined.size)
	holder.set_meta("surface_height_m", combined.size.y * factor)
	return holder

static func tea_table(parent: Node3D, at: Vector3, title: String) -> Node3D:
	var model := _instantiate(TEA_TABLE, title)
	if model == null: return null
	model.position = at
	parent.add_child(model)
	return model

static func vegetation(parent: Node3D, at: Vector3, height: float, shrub: bool, title: String) -> Node3D:
	var model := _instantiate(BUSH if shrub else TREE, title, height)
	if model == null: return null
	_style_vegetation(model)
	model.position = at
	parent.add_child(model)
	return model

static func _style_vegetation(node: Node) -> void:
	if node is MeshInstance3D:
		for index in range(node.mesh.get_surface_count()):
			var source: Material = node.mesh.surface_get_material(index)
			var label: String = source.resource_name.to_lower() if source != null else ""
			if "bark" in label:
				node.set_surface_override_material(index, MATERIALS.surface(Color("65513d"), 1, 0.9, 0.0))
			else:
				var leaves := ShaderMaterial.new()
				leaves.shader = FOLIAGE
				leaves.set_shader_parameter("pigment", Color("52694a"))
				node.set_surface_override_material(index, leaves)
	for child in node.get_children(): _style_vegetation(child)
