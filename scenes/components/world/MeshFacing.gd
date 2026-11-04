extends RefCounted
## Godot front faces are clockwise. Preserve normals/UVs and change only indices.
static var _cache: Dictionary={}

static func correct(source: Mesh) -> Dictionary:
	var key:=source.get_instance_id()
	if _cache.has(key) and _cache[key].source==source: return _cache[key]
	var surfaces: Array=[]
	var flipped:=0
	for si in source.get_surface_count():
		var arrays: Array=source.surface_get_arrays(si)
		if (source is ArrayMesh and source.surface_get_primitive_type(si)!=Mesh.PRIMITIVE_TRIANGLES) or arrays.is_empty(): return {"mesh":source,"source":source,"flipped":0}
		var vertices: PackedVector3Array=arrays[Mesh.ARRAY_VERTEX]
		var normals: PackedVector3Array=arrays[Mesh.ARRAY_NORMAL] if arrays[Mesh.ARRAY_NORMAL]!=null else PackedVector3Array()
		var indices: PackedInt32Array=arrays[Mesh.ARRAY_INDEX] if arrays[Mesh.ARRAY_INDEX]!=null else PackedInt32Array()
		if indices.is_empty():
			indices.resize(vertices.size())
			for i in vertices.size(): indices[i]=i
		else: indices=indices.duplicate()
		for i in range(0,indices.size()-2,3):
			var a:=indices[i];var b:=indices[i+1];var c:=indices[i+2]
			if a>=normals.size() or b>=normals.size() or c>=normals.size(): continue
			var expected: Vector3=normals[a]+normals[b]+normals[c]
			var geometric: Vector3=(vertices[b]-vertices[a]).cross(vertices[c]-vertices[a])
			if geometric.length_squared()>1e-20 and geometric.dot(expected)>1e-12:
				indices[i+1]=c;indices[i+2]=b;flipped+=1
		arrays[Mesh.ARRAY_INDEX]=indices
		surfaces.append(arrays)
	var result: Mesh=source
	if flipped>0:
		var rebuilt:=ArrayMesh.new()
		for si in surfaces.size():
			rebuilt.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,surfaces[si])
			rebuilt.surface_set_material(si,source.surface_get_material(si))
		result=rebuilt
	var record: Dictionary={"mesh":result,"source":source,"flipped":flipped}
	if _cache.size()>=256: _cache.erase(_cache.keys()[0])
	_cache[key]=record
	return record
