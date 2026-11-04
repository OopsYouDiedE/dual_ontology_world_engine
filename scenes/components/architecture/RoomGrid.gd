extends RefCounted
## One-scene-unit GridMap kit. Walls expose only the inward-facing surface.
static func library(floor_material: Material, plaster: Material, wood: Material, trim: Material, height: float) -> MeshLibrary:
	var lib := MeshLibrary.new()
	var floor_mesh := BoxMesh.new()
	floor_mesh.size = Vector3(1,0.12,1)
	floor_mesh.material = floor_material
	lib.create_item(0)
	lib.set_item_name(0,"Parquet / continuous world grain")
	lib.set_item_mesh(0,floor_mesh)
	lib.set_item_mesh_transform(0,Transform3D(Basis.IDENTITY,Vector3(0,-0.06,0)))
	var floor_shape := BoxShape3D.new()
	floor_shape.size=Vector3(1,0.12,1)
	lib.set_item_shapes(0,[floor_shape,Transform3D(Basis.IDENTITY,Vector3(0,-0.06,0))])
	# Build a wall tile as a single multi-surface mesh, with mouldings in geometry.
	var wall := ArrayMesh.new()
	_add_box(wall,Vector3(1,height,0.22),Vector3(0,height/2,0),plaster)
	_add_box(wall,Vector3(1,1.18,0.28),Vector3(0,0.59,0),wood)
	for y in [0.10,1.16,height-0.17,height-0.06]:
		_add_box(wall,Vector3(1,0.075,0.34),Vector3(0,y,0),trim)
	# Recessed panel face and four narrow raised frame bars on both room sides.
	for side in [1]:
		_add_box(wall,Vector3(0.76,0.77,0.025),Vector3(0,0.64,side*0.15),wood)
		for x in [-0.42,0.42]: _add_box(wall,Vector3(0.035,0.88,0.04),Vector3(x,0.64,side*0.17),trim)
		for y in [0.20,1.08]: _add_box(wall,Vector3(0.87,0.035,0.04),Vector3(0,y,side*0.17),trim)
	lib.create_item(1)
	lib.set_item_name(1,"Full height panelled wall")
	lib.set_item_mesh(1,wall)
	var wall_shape := BoxShape3D.new()
	wall_shape.size=Vector3(1,height,0.22)
	var wall_collision := Transform3D(Basis.IDENTITY,Vector3(0,height/2,0))
	lib.set_item_shapes(1,[wall_shape,wall_collision])
	var front := ArrayMesh.new()
	_add_box(front,Vector3(1,0.30,0.28),Vector3(0,0.15,0),wood)
	lib.create_item(2)
	lib.set_item_name(2,"Camera cutaway / full wall footprint")
	lib.set_item_mesh(2,front)
	lib.set_item_shapes(2,[wall_shape,wall_collision])
	return lib

static func _add_box(mesh: ArrayMesh, size: Vector3, at: Vector3, mat: Material) -> void:
	# Each relief layer is a single front-facing quad, with back-face culling.
	# Opposite rooms own their own inward surface at the shared boundary.
	var primitive := QuadMesh.new()
	primitive.size=Vector2(size.x,size.y)
	var arrays := primitive.get_mesh_arrays()
	var vertices: PackedVector3Array=arrays[Mesh.ARRAY_VERTEX]
	for i in vertices.size(): vertices[i]+=at+Vector3(0,0,size.z*0.5)
	arrays[Mesh.ARRAY_VERTEX]=vertices
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays)
	mesh.surface_set_material(mesh.get_surface_count()-1,mat)

static func make(parent: Node3D, lib: MeshLibrary, size: Vector2i, openings: Dictionary) -> Dictionary:
	var floor_grid := GridMap.new()
	floor_grid.name="FloorGrid"
	floor_grid.cell_size=Vector3.ONE
	floor_grid.cell_center_y=false
	floor_grid.collision_layer=1
	floor_grid.collision_mask=0
	floor_grid.mesh_library=lib
	floor_grid.position=Vector3(-size.x*0.5,0,-size.y*0.5)
	parent.add_child(floor_grid)
	for z in size.y:
		for x in size.x: floor_grid.set_cell_item(Vector3i(x,0,z),0)
	var result := {"floor":floor_grid}
	for side in ["north","south","west","east"]:
		var wall := GridMap.new()
		wall.name=side.capitalize()+"WallGrid"
		wall.cell_size=Vector3.ONE
		wall.cell_center_y=false
		wall.cell_center_z=false
		wall.collision_layer=1
		wall.collision_mask=0
		wall.mesh_library=lib
		var horizontal: bool=side in ["north","south"]
		var count: int=size.x if horizontal else size.y
		wall.position=Vector3(-size.x*0.5,0,-size.y*0.5) if horizontal else Vector3(-size.x*0.5,0,size.y*0.5)
		if side=="south":
			wall.position=Vector3(size.x*0.5,0,size.y*0.5)
			wall.rotation.y=PI
		elif side=="west": wall.rotation.y=PI/2.0
		elif side=="east":
			wall.position=Vector3(size.x*0.5,0,-size.y*0.5)
			wall.rotation.y=-PI/2.0
		parent.add_child(wall)
		for i in count:
			if i in openings.get(side,[]): continue
			wall.set_cell_item(Vector3i(i,0,0),2 if side=="south" else 1)
		result[side]=wall
	return result
