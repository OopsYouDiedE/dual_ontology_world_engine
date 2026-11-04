@tool
extends Node3D
## CPU geometry node. Samples actual terrain, then builds a level arcade on variable foundations.
## No world rules, global environment, camera, textures or vertex-displaced architecture.
@export_range(1, 12) var bays := 4
@export_range(2.0, 6.0) var bay_width := 3.6
@export_range(1.8, 4.0) var spring_height := 2.5
@export_range(0.15, 0.6) var arch_thickness := 0.3
@export_range(8, 48) var arch_segments := 20
@export_flags_3d_physics var terrain_mask := 2
@export_range(1.0, 20.0) var sample_height := 10.0
@export_range(0.2, 4.0) var max_foundation_height := 2.0
@export var stone: Material
@export var trim: Material
@export var infill: Material
@export var generate_in_editor := false:
	set(value):
		generate_in_editor = false
		if value and is_inside_tree(): rebuild()
var generated: Node3D
var foundation_heights: Array[float] = []
var build_error := ""

func _ready() -> void:
	if Engine.is_editor_hint(): return
	await get_tree().physics_frame
	await get_tree().physics_frame
	rebuild()

func rebuild() -> bool:
	# Validate every foot before replacing the last valid output.
	var heights: Array[float] = []
	var highest_ground := -INF
	build_error = ""
	for i in range(bays+1):
		var foot := Vector3((i-bays*0.5)*bay_width,0,0)
		var lowest := INF
		var highest := -INF
		for offset in [Vector3(-0.4,0,-0.4),Vector3(0.4,0,-0.4),Vector3(-0.4,0,0.4),Vector3(0.4,0,0.4)]:
			var point := to_global(foot+offset)
			var ray := PhysicsRayQueryParameters3D.create(point+Vector3.UP*sample_height,point-Vector3.UP*sample_height,terrain_mask)
			var hit := get_world_3d().direct_space_state.intersect_ray(ray)
			if hit.is_empty():
				build_error = "Terrain missing below footing %d; previous geometry retained." % i
				push_warning(build_error)
				return false
			var local_y := to_local(hit.position).y
			lowest = minf(lowest,local_y)
			highest = maxf(highest,local_y)
		heights.append(lowest-0.08)
		highest_ground = maxf(highest_ground,highest)
		# Retain high point for common foundation plane; never shear the building.
		if highest-lowest > max_foundation_height:
			build_error = "Footing slope exceeds foundation limit."
			push_warning(build_error)
			return false
	var level := highest_ground+0.27
	if level-float(heights.min()) > max_foundation_height:
		build_error = "Terrain relief exceeds foundation limit."
		push_warning(build_error)
		return false
	if is_instance_valid(generated):
		remove_child(generated)
		generated.queue_free()
	generated = Node3D.new()
	generated.name = "GeneratedGeometry"
	add_child(generated)
	foundation_heights.clear()
	var radius := (bay_width-0.6)*0.5
	var crown := level+spring_height+radius+arch_thickness
	for i in range(bays+1):
		var x := (i-bays*0.5)*bay_width
		var height := level-heights[i]
		foundation_heights.append(height)
		box(Vector3(x,heights[i]+height*0.5,0),Vector3(0.86,height,0.86),stone)
		box(Vector3(x,level+0.08,0),Vector3(0.9,0.16,0.9),trim)
		cylinder(Vector3(x,level+spring_height*0.5,0),0.27,spring_height,stone)
		box(Vector3(x,level+spring_height-0.05,0),Vector3(0.78,0.18,0.78),trim)
		if i < bays:
			var center := Vector3(x+bay_width*0.5,level+spring_height,0)
			arch(center,radius,arch_thickness,0.65)
			# Raised voussoir mouldings are separate real geometry.
			arch(center+Vector3(0,0,-0.37),radius+arch_thickness,0.065,0.08)
	var length := bays*bay_width+1.0
	box(Vector3(0,crown+0.08,0),Vector3(length,0.16,0.9),trim)
	box(Vector3(0,crown+0.3,1.45),Vector3(length+0.35,0.22,3.8),infill)
	box(Vector3(0,(crown+float(heights.min()))*0.5,3.2),Vector3(length,crown-float(heights.min()),0.4),stone)
	# Repeat a parameterized relief frieze. Ornament changes the silhouette and casts shadows.
	var repeats := bays*8
	for i in range(repeats):
		var x := -length*0.5+(i+0.5)*length/repeats
		box(Vector3(x,crown+0.05,-0.49),Vector3(0.12,0.24,0.1),trim)
	return true

func arch(center: Vector3, radius: float, thickness: float, depth: float) -> void:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	st.set_smooth_group(-1)
	for i in range(arch_segments):
		var a := PI*i/arch_segments+0.004
		var b := PI*(i+1)/arch_segments-0.004
		var points := PackedVector3Array()
		for z in [-depth*0.5,depth*0.5]:
			for pair in [Vector2(a,radius),Vector2(b,radius),Vector2(b,radius+thickness),Vector2(a,radius+thickness)]:
				points.append(Vector3(cos(pair.x)*pair.y,sin(pair.x)*pair.y,z))
		for face in [[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]:
			for index in [0,1,2,0,2,3]: st.add_vertex(points[face[index]])
	st.generate_normals()
	add_mesh(st.commit(),center,trim)

func box(p: Vector3, size: Vector3, material: Material) -> void:
	var shape := BoxMesh.new()
	shape.size = size
	add_mesh(shape,p,material)

func cylinder(p: Vector3, radius: float, height: float, material: Material) -> void:
	var shape := CylinderMesh.new()
	shape.top_radius = radius
	shape.bottom_radius = radius
	shape.height = height
	shape.radial_segments = 40
	add_mesh(shape,p,material)

func add_mesh(shape: Mesh, p: Vector3, material: Material) -> void:
	var node := MeshInstance3D.new()
	node.mesh = shape
	node.material_override = material
	node.position = p
	node.gi_mode = GeometryInstance3D.GI_MODE_STATIC
	generated.add_child(node)
	node.create_trimesh_collision()
