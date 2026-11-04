extends RefCounted
## Shared closed chamfered mesh primitives in local metres.
const BLOCK_CACHE_LIMIT := 4096
static var _block_cache: Dictionary = {}
var triangle_count := 0
var materials: Array[StandardMaterial3D] = []
var _stone: StandardMaterial3D
var _wood: StandardMaterial3D
var _vertices := PackedVector3Array()
var _normals := PackedVector3Array()
var _uvs := PackedVector2Array()

func _material(color: Color, roughness: float) -> StandardMaterial3D:
	var mat:=StandardMaterial3D.new()
	mat.albedo_color=color; mat.roughness=roughness
	mat.metallic_specular=0.3
	materials.append(mat)
	return mat

func _triangle(a: Vector3,b: Vector3,c: Vector3) -> void:
	var cross_value: Vector3=(b-a).cross(c-a)
	if cross_value.length_squared()<1e-12: return
	var normal: Vector3=cross_value.normalized()
	# Outward CCW input is reversed for Godot's clockwise front faces.
	for p: Vector3 in [a,c,b]:
		_vertices.append(p); _normals.append(normal); _uvs.append(Vector2(p.x,p.y))
	triangle_count+=1

func _ring(width: float,depth: float,y: float,cut: float) -> Array[Vector3]:
	var x:=width*0.5; var z:=depth*0.5
	var c:=minf(cut,minf(x,z)*0.45)
	return [Vector3(-x+c,y,-z),Vector3(x-c,y,-z),Vector3(x,y,-z+c),Vector3(x,y,z-c),Vector3(x-c,y,z),Vector3(-x+c,y,z),Vector3(-x,y,z-c),Vector3(-x,y,-z+c)]

func _prism(parent: Node3D,identity: String,sections: Array,position: Vector3,mat: Material,cut := 0.025) -> MeshInstance3D:
	_vertices=PackedVector3Array(); _normals=PackedVector3Array(); _uvs=PackedVector2Array()
	var rings: Array=[]
	for section: Vector3 in sections: rings.append(_ring(section.x,section.z,section.y,cut))
	for j in range(rings.size()-1):
		for i in 8:
			var next: int=(i+1)%8
			var a: Vector3=rings[j][i]; var b: Vector3=rings[j+1][i]
			var c: Vector3=rings[j+1][next]; var d: Vector3=rings[j][next]
			# Ascending rings and this perimeter order produce outward normals.
			_triangle(a,b,c); _triangle(a,c,d)
	for end_index: int in [0,rings.size()-1]:
		var ring: Array=rings[end_index]
		var center:=Vector3(0,Vector3(ring[0]).y,0)
		for i in 8:
			var a: Vector3=ring[i]; var b: Vector3=ring[(i+1)%8]
			if end_index==0: _triangle(center,a,b)
			else: _triangle(center,b,a)
	var arrays:=[]; arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX]=_vertices; arrays[Mesh.ARRAY_NORMAL]=_normals; arrays[Mesh.ARRAY_TEX_UV]=_uvs
	var mesh:=ArrayMesh.new(); mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays)
	var node:=MeshInstance3D.new(); node.name=identity; node.mesh=mesh; node.material_override=mat; node.position=position
	parent.add_child(node)
	return node

func _block(parent: Node3D,identity: String,size: Vector3,center: Vector3,mat: Material,bevel := 0.012) -> MeshInstance3D:
	var b:=minf(bevel,minf(size.x,minf(size.y,size.z))*0.22)
	# Identical blocks share one immutable ArrayMesh; the block is centred, so
	# the cache key needs only the size and the bevel. Millimetre keys merge
	# float noise from authored dimensions without changing visible geometry.
	var key:="%.4f|%.4f|%.4f|%.4f" % [size.x,size.y,size.z,b]
	var cached: Array=_block_cache.get(key,[])
	if not cached.is_empty():
		var node:=MeshInstance3D.new(); node.name=identity; node.mesh=cached[0]; node.material_override=mat; node.position=center
		parent.add_child(node)
		triangle_count+=int(cached[1])
		return node
	var before:=triangle_count
	var sections: Array=[Vector3(size.x-b*2,-size.y*0.5,size.z-b*2),Vector3(size.x,-size.y*0.5+b,size.z),Vector3(size.x,size.y*0.5-b,size.z),Vector3(size.x-b*2,size.y*0.5,size.z-b*2)]
	var created:=_prism(parent,identity,sections,center,mat,b)
	if _block_cache.size()<BLOCK_CACHE_LIMIT: _block_cache[key]=[created.mesh,triangle_count-before]
	return created

