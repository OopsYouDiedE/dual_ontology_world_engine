extends SceneTree
## Permanent character-authoring export; no gameplay state or test fixtures.
const STUDIO := preload("res://scenes/World/Actors/CharacterGallery.tscn")
const CAST := preload("res://scenes/World/Art/CastE.gd")

func _initialize() -> void:
	call_deferred("_export")

func _export() -> void:
	var studio := STUDIO.instantiate()
	root.add_child(studio)
	await process_frame
	var directory := ProjectSettings.globalize_path("res://../.runtime/character-art/E_sources")
	DirAccess.make_dir_recursive_absolute(directory)
	var recipe: Dictionary = preload("res://scenes/Host/DvPresentationContent.cs").new().ReadRecord("res://Content/character_art.dv", "layouts", "character_art")
	var recipe_file := FileAccess.open(directory.path_join("recipe.json"), FileAccess.WRITE)
	recipe_file.store_string(JSON.stringify(recipe, "\t", true))
	recipe_file.close()
	var ids: Array = CAST.PEOPLE.keys()
	if studio.figures.size() != ids.size():
		push_error("Character art source count mismatch")
		quit(1)
		return
	for index in ids.size():
		var source: Node3D = studio.figures[index].duplicate()
		source.position = Vector3.ZERO
		source.name = ids[index]
		_standard_materials(source)
		var document := GLTFDocument.new()
		var state := GLTFState.new()
		var error := document.append_from_scene(source, state)
		if error == OK: error = document.write_to_filesystem(state, directory.path_join(str(ids[index])+".glb"))
		source.free()
		if error != OK:
			push_error("Character art export failed: %s (%s)" % [ids[index], error])
			quit(1)
			return
	print("Exported %d original E character art sources" % ids.size())
	studio.queue_free()
	await process_frame
	quit(0)

func _standard_materials(node: Node) -> void:
	if node.get_meta("art_layer", "") == "garment": node.name = "ART_GARMENT_" + node.name
	if node is MeshInstance3D and node.material_override is ShaderMaterial:
		var source: ShaderMaterial = node.material_override
		var material := StandardMaterial3D.new()
		var pigment: Variant = source.get_shader_parameter("pigment")
		var roughness: Variant = source.get_shader_parameter("surface_roughness")
		material.albedo_color = pigment if pigment is Color else Color.WHITE
		material.roughness = float(roughness) if roughness != null else 0.8
		material.resource_name = source.resource_name
		node.material_override = material
	for child in node.get_children(): _standard_materials(child)
