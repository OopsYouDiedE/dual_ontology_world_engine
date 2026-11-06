extends RefCounted
## Presentation-only closure: real architecture still occludes light when cut away.
## The camera excludes layer 20; reflection probes capture that complete shell.
const CAPTURE_LAYER := 1 << 19
var root: Node3D
var sources: Array = []
var shadow_groups := 0
var reflection_groups := 0
var triangles := 0
var _enabled := false

func rebuild(content: Node3D, world: Node3D) -> void:
	if is_instance_valid(root): root.get_parent().remove_child(root);root.free()
	root=Node3D.new();root.name="CutawayLightingShell"
	root.set_meta("render_only_shadow_proxy",true)
	world.add_child(root)
	sources=[];shadow_groups=0;reflection_groups=0;triangles=0
	var shadows := {}
	var reflections := {}
	var material_keys := {}
	var world_inverse: Transform3D = _local_chain_transform(world).affine_inverse()
	for node in content.find_children("*","MeshInstance3D",true,false):
		if node.mesh==null: continue
		var material: Material=node.get_meta("build_original_material",node.material_override)
		if material==null: material=node.get_active_material(0)
		if material is ShaderMaterial and material.shader!=null and "VegetationNodes" in material.shader.resource_path: continue
		var original: int=node.get_meta("lighting_original_shadow",node.cast_shadow)
		sources.append({"node":node,"shadow":original})
		var mesh_to_world: Transform3D = world_inverse * _local_chain_transform(node)
		var center: Vector3=mesh_to_world*node.get_aabb().get_center()
		var cell: String="%d_%d_%d" % [floori(center.x/16.0),floori(center.y/8.0),floori(center.z/16.0)]
		for si in node.mesh.get_surface_count():
			var mat: Material=material if material!=null else node.get_active_material(si)
			if mat==null: continue
			var opaque: bool=not (mat is BaseMaterial3D and mat.transparency!=BaseMaterial3D.TRANSPARENCY_DISABLED)
			var arrays: Array=node.mesh.surface_get_arrays(si)
			var format_key: String=""
			for attribute in [Mesh.ARRAY_NORMAL,Mesh.ARRAY_TANGENT,Mesh.ARRAY_COLOR,Mesh.ARRAY_TEX_UV,Mesh.ARRAY_TEX_UV2]: format_key+="1" if arrays[attribute]!=null else "0"
			var material_id: int=mat.get_instance_id()
			if not material_keys.has(material_id): material_keys[material_id]=_material_key(mat)
			var key: String=cell+"_"+material_keys[material_id]+"_"+format_key
			if not reflections.has(key): reflections[key]={"surface":_surface(),"material":mat}
			reflections[key].surface.append_from(node.mesh,si,mesh_to_world)
			if opaque and original!=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF:
				if not shadows.has(cell): shadows[cell]=_surface()
				_append_shadow(shadows[cell],arrays,mesh_to_world)
	var shadow_material:=StandardMaterial3D.new()
	shadow_material.cull_mode=BaseMaterial3D.CULL_BACK
	for key: String in shadows:
		var mesh: ArrayMesh=shadows[key].commit()
		var node:=MeshInstance3D.new();node.name="Occluder_"+key
		node.mesh=mesh;node.material_override=shadow_material
		node.cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_SHADOWS_ONLY
		node.gi_mode=GeometryInstance3D.GI_MODE_DISABLED
		root.add_child(node);shadow_groups+=1
	for key: String in reflections:
		var mesh: ArrayMesh=reflections[key].surface.commit()
		var node:=MeshInstance3D.new();node.name="Capture_"+key
		node.mesh=mesh;node.material_override=reflections[key].material
		node.layers=CAPTURE_LAYER
		node.cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		node.gi_mode=GeometryInstance3D.GI_MODE_DISABLED
		root.add_child(node);reflection_groups+=1
	_enabled=false

func _local_chain_transform(node: Node) -> Transform3D:
	# Rebuild also runs while imported branches are outside SceneTree.
	# Compose authored transforms without requesting global_transform.
	var result := Transform3D.IDENTITY
	var current: Node = node
	while current != null:
		if current is Node3D:
			result = current.transform * result
			if current.top_level:
				break
		current = current.get_parent()
	return result

func _surface() -> SurfaceTool:
	var surface:=SurfaceTool.new();surface.begin(Mesh.PRIMITIVE_TRIANGLES);return surface

func _material_key(material: Material) -> String:
	# Cutaway clones share their authored parameters; batch identical clones.
	if material is ShaderMaterial and material.shader!=null:
		var signature: String=material.shader.resource_path
		for uniform: Dictionary in material.shader.get_shader_uniform_list():
			signature+="|"+str(uniform.name)+":"+var_to_str(material.get_shader_parameter(uniform.name))
		return signature.sha256_text()
	return str(material.get_instance_id())

func _append_shadow(surface: SurfaceTool, arrays: Array, transform: Transform3D) -> void:
	var vertices: PackedVector3Array=arrays[Mesh.ARRAY_VERTEX]
	var indices: PackedInt32Array=arrays[Mesh.ARRAY_INDEX] if arrays[Mesh.ARRAY_INDEX]!=null else PackedInt32Array()
	for i in (indices.size() if not indices.is_empty() else vertices.size()):
		surface.add_vertex(transform*vertices[indices[i] if not indices.is_empty() else i])

func set_enabled(value: bool) -> void:
	_enabled=value
	if is_instance_valid(root): root.visible=value
	for source: Dictionary in sources:
		if not is_instance_valid(source.node): continue
		if not source.node.has_meta("lighting_original_shadow"): source.node.set_meta("lighting_original_shadow",source.shadow)
		source.node.cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF if value else source.shadow

func receipt() -> Dictionary:
	return {"enabled":_enabled,"shadow_only_groups":shadow_groups,"reflection_capture_groups":reflection_groups,"capture_layer":20,"camera_surface_visible":false,"uses_real_meshes":true,"physics_added":false}
