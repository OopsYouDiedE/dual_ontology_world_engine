extends RefCounted
## Formal-host floor publication. No unlisted geometry or fallback is loaded.
const FloorSurface = preload("res://geometry/FloorSurface3D.gd")
const MeshDiagnostics = preload("res://geometry/MeshPublicationDiagnostics.gd")

static func create(engine: Object, piece_ids: Array) -> Dictionary:
	if not is_instance_valid(engine) or piece_ids.is_empty():
		return {"ok": false, "error": "Missing engine or floor manifest."}
	var projection: Dictionary = engine.ProjectFloorSurfaces(piece_ids)
	if not bool(projection.get("ok", false)):
		return {"ok": false, "error": str(projection.get("error", "Floor projection failed."))}
	var value: Dictionary = projection.get("value", {})
	var pieces: Array = value.get("pieces", [])
	if pieces.size() != piece_ids.size() or str(value.get("content_hash", "")).is_empty() or not value.has("world_datum") or not value.get("materials") is Dictionary:
		return {"ok": false, "error": "Incomplete floor projection."}
	var root := Node3D.new()
	root.name = "FloorSurfaces"
	var diagnostics: Array = []
	var materials := {}
	for piece in pieces:
		if not piece is Dictionary or not piece.has_all(["id", "rect", "z", "height", "collider_ref", "material_ref"]) or not piece.rect is Array or piece.rect.size() != 4:
			root.free()
			return {"ok": false, "error": "Invalid floor surface declaration."}
		var material_id := str(piece.material_ref)
		if not value.materials.has(material_id) or not value.materials[material_id] is Dictionary:
			root.free()
			return {"ok": false, "error": "Missing material for floor " + str(piece.id)}
		if not materials.has(material_id):
			var spec: Dictionary = value.materials[material_id]
			if not spec.has_all(["shader","base_color","floor_surface","roughness","texture_scale_m","status"]) or spec.shader != "framework_surface" or spec.floor_surface != true:
				root.free()
				return {"ok": false, "error": "Unsupported material " + material_id + " for floor " + str(piece.id)}
			var material := ShaderMaterial.new()
			material.shader = preload("res://rendering/shaders/framework_surface.gdshader")
			material.set_shader_parameter("base_color", Color(str(spec.base_color)))
			material.set_shader_parameter("floor_surface", spec.floor_surface)
			material.set_shader_parameter("surface_roughness", float(spec.roughness))
			material.set_shader_parameter("texture_scale_m", float(spec.texture_scale_m))
			materials[material_id] = material
		var r: Array = piece.rect
		var outer := PackedVector2Array([Vector2(r[0],r[1]), Vector2(r[2],r[1]), Vector2(r[2],r[3]), Vector2(r[0],r[3])])
		var holes: Array[PackedVector2Array] = []
		var node := FloorSurface.create(str(piece.id), outer, holes, float(piece.z) + float(piece.height), float(piece.height), materials[material_id])
		if node == null:
			root.free()
			return {"ok": false, "error": "Floor generation failed: " + str(piece.id)}
		var diagnostic: Dictionary = MeshDiagnostics.inspect(node)
		diagnostic["piece_id"] = str(piece.id)
		diagnostics.append(diagnostic)
		if not diagnostic.valid:
			node.free()
			root.free()
			return {"ok": false, "error": "Floor mesh failed diagnostics: " + str(piece.id), "diagnostics": diagnostics}
		root.add_child(node)
	return {"ok": true, "root": root, "content_hash": value.content_hash, "spatial_definition": value.spatial_definition, "world_datum": value.world_datum, "diagnostics": diagnostics}
