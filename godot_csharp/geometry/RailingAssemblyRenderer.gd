extends RefCounted
## Builds an explicit set of authored paths atomically. The caller must supply
## a validated publication; this renderer neither selects nor replaces objects.
const Railing = preload("res://geometry/generators/CurveRailing3D.gd")
const MeshDiagnostics = preload("res://geometry/MeshPublicationDiagnostics.gd")

static func create(spec: Dictionary, material: Material) -> Dictionary:
	if material == null or not spec.has_all(["paths","junctions","max_post_spacing_m","post_radius_m","rail_radius_m","guard_thickness_m","arc_tolerance_m","junction_tolerance_m"]):
		return {"ok":false,"error":"missing-railing-assembly-fields"}
	if not spec.paths is Array or spec.paths.is_empty() or not spec.junctions is Array:
		return {"ok":false,"error":"invalid-railing-assembly"}
	var root := Node3D.new()
	root.name = "RailingAssembly"
	var members: Array = []
	var ids := {}
	for item in spec.paths:
		if not item is Dictionary or not item.has_all(["id","points_design","control_heights_m","own_start_post","own_end_post","support","start_guard_cut_m","end_guard_cut_m"]):
			root.free()
			return {"ok":false,"error":"missing-railing-path-fields"}
		if item.id is not String or str(item.id).is_empty() or ids.has(str(item.id)) or not item.points_design is Array or item.points_design.size() < 2 or not item.control_heights_m is Array or not item.support is Dictionary or typeof(item.own_start_post) != TYPE_BOOL or typeof(item.own_end_post) != TYPE_BOOL or not item.start_guard_cut_m is Array or not item.end_guard_cut_m is Array:
			root.free()
			return {"ok":false,"error":"invalid-or-duplicate-railing-id"}
		ids[str(item.id)] = true
		var curve := Curve3D.new()
		var in_handles: Array = item.get("in_handles_design", [])
		var out_handles: Array = item.get("out_handles_design", [])
		if (not in_handles.is_empty() or not out_handles.is_empty()) and (in_handles.size() != item.points_design.size() or out_handles.size() != item.points_design.size()):
			root.free()
			return {"ok":false,"error":"invalid-railing-handle-count:" + str(item.id)}
		for index in range(item.points_design.size()):
			var point = item.points_design[index]
			if not point is Array or point.size() != 3:
				root.free()
				return {"ok":false,"error":"invalid-railing-control-point:" + str(item.id)}
			var incoming := Vector3.ZERO
			var outgoing := Vector3.ZERO
			if not in_handles.is_empty():
				var a = in_handles[index]
				var b = out_handles[index]
				if not a is Array or not b is Array or a.size() != 3 or b.size() != 3:
					root.free()
					return {"ok":false,"error":"invalid-railing-handle:" + str(item.id)}
				incoming = Vector3(float(a[0]),float(a[1]),float(a[2]))
				outgoing = Vector3(float(b[0]),float(b[1]),float(b[2]))
			curve.add_point(Vector3(float(point[0]),float(point[1]),float(point[2])),incoming,outgoing)
		var support: Dictionary = item.support.duplicate(true)
		if bool(support.get("enabled",false)):
			if not support.has_all(["size_design","offset_design"]) or not support.size_design is Array or not support.offset_design is Array or support.size_design.size() != 3 or support.offset_design.size() != 3:
				root.free()
				return {"ok":false,"error":"invalid-railing-support:" + str(item.id)}
			support.size_design = Vector3(float(support.size_design[0]),float(support.size_design[1]),float(support.size_design[2]))
			support.offset_design = Vector3(float(support.offset_design[0]),float(support.offset_design[1]),float(support.offset_design[2]))
		var start: Array = item.start_guard_cut_m
		var end: Array = item.end_guard_cut_m
		if start.size() != 2 or end.size() != 2:
			root.free()
			return {"ok":false,"error":"invalid-railing-cuts:" + str(item.id)}
		var member := Railing.create(str(item.id),curve,float(spec.max_post_spacing_m),float(spec.post_radius_m),float(spec.rail_radius_m),float(spec.guard_thickness_m),material,float(spec.arc_tolerance_m),item.control_heights_m,bool(item.own_start_post),bool(item.own_end_post),support,item.get("control_post_heights_m", []),Vector2(float(start[0]),float(start[1])),Vector2(float(end[0]),float(end[1])))
		if member == null:
			root.free()
			return {"ok":false,"error":"railing-generation-failed:" + str(item.id)}
		root.add_child(member)
		members.append(member)
	var assembly_error: String = Railing.validate_cut_assembly(members,spec.junctions,float(spec.junction_tolerance_m))
	if not assembly_error.is_empty():
		root.free()
		return {"ok":false,"error":assembly_error}
	var diagnostic: Dictionary = MeshDiagnostics.inspect(root)
	if not diagnostic.valid:
		root.free()
		return {"ok":false,"error":"railing-mesh-diagnostics-failed","diagnostic":diagnostic}
	return {"ok":true,"root":root,"diagnostic":diagnostic}
