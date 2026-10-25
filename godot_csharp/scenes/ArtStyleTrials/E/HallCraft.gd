extends Node3D
## Shared procedural modelling primitives and the retained three-head D cast.
const CAST = preload("res://scenes/ArtStyleTrials/E/CastE.gd")
const SURFACE = preload("res://scenes/ArtStyleTrials/E/painted_surface.gdshader")
var materials: Dictionary = {}
var figures: Array[Node3D] = []
var content_root: Node3D

func group(label: String, at: Vector3) -> Node3D:
	var node := Node3D.new()
	node.name = label
	node.set_meta("authoring_label",label)
	node.position = at
	content_root.add_child(node)
	return node

func point(gx: float, gy: float, height: float = 0.0) -> Vector3:
	return Vector3(gx, height, gy)

func material(key: String, color: String, kind: float = 0.0, rough: float = 0.8, metal: float = 0.0) -> ShaderMaterial:
	if materials.has(key): return materials[key]
	var result := ShaderMaterial.new()
	result.shader = SURFACE
	result.set_shader_parameter("pigment", Color(color))
	result.set_shader_parameter("surface_kind", kind)
	result.set_shader_parameter("surface_roughness", rough)
	result.set_shader_parameter("surface_metallic", metal)
	materials[key] = result
	return result

func box(parent: Node3D, at: Vector3, size: Vector3, mat: Material, label: String = "") -> MeshInstance3D:
	var mesh := MeshInstance3D.new()
	var shape := BoxMesh.new()
	shape.size = size
	mesh.mesh = shape
	mesh.material_override = mat
	mesh.position = at
	if not label.is_empty(): mesh.name = label
	parent.add_child(mesh)
	return mesh

func ellipsoid(parent: Node3D, at: Vector3, size: Vector3, mat: Material, segments: int = 12) -> MeshInstance3D:
	var mesh := MeshInstance3D.new()
	var shape := SphereMesh.new()
	shape.radius = 0.5
	shape.height = 1.0
	shape.radial_segments = maxi(segments, 20)
	shape.rings = 12
	mesh.mesh = shape
	mesh.scale = size
	mesh.position = at
	mesh.material_override = mat
	parent.add_child(mesh)
	return mesh

func cylinder(parent: Node3D, at: Vector3, radius: float, height: float, mat: Material, top_radius: float = -1.0) -> MeshInstance3D:
	var mesh := MeshInstance3D.new()
	var shape := CylinderMesh.new()
	shape.bottom_radius = radius
	shape.top_radius = radius if top_radius < 0 else top_radius
	shape.height = height
	shape.radial_segments = 24
	mesh.mesh = shape
	mesh.position = at
	mesh.material_override = mat
	parent.add_child(mesh)
	return mesh

func limb(parent: Node3D, start: Vector3, end: Vector3, radius: float, mat: Material) -> MeshInstance3D:
	var result := cylinder(parent, (start + end) * 0.5, radius, start.distance_to(end), mat)
	result.quaternion = Quaternion(Vector3.UP, (end - start).normalized())
	return result

func _character(label: String, gx: float, gy: float, identity: String, _body_scale: float) -> void:
	var root := group(label,point(gx,gy))
	root.scale=Vector3.ONE
	figures.append(root)
	var skin := material("skin","c9946c",0,0.86)
	var dark := material("eyes","252a29",0,0.8)
	var hair := material("hair_"+identity,CAST.PEOPLE[identity][2],1)
	var cloth_color: String = CAST.PEOPLE[identity][1]
	var cloth := material("cloth_"+identity,cloth_color,3)
	var boot := material("boots","3e322d",1)
	var trouser := material("trousers","3b4142",3)
	# Crown 1.32 / chin .88 = .44, total height 1.32: 3 anatomical heads.
	for side in [-1,1]:
		ellipsoid(root,Vector3(side*0.105,0.075,0.055),Vector3(0.17,0.15,0.29),boot,8)
		limb(root,Vector3(side*0.10,0.14,0),Vector3(side*0.11,0.43,0),0.075,trouser)
		cylinder(root,Vector3(side*0.11,0.22,0),0.080,0.16,boot)
	ellipsoid(root,Vector3(0,0.61,0),Vector3(0.38,0.48,0.25),cloth,10)
	ellipsoid(root,Vector3(0,1.09,0),Vector3(0.40,0.44,0.34),skin,12)
	# Ears, actual geometry eyes, eyebrows and nose. All faces look south.
	for side in [-1,1]:
		ellipsoid(root,Vector3(side*0.205,1.075,0),Vector3(0.065,0.105,0.06),skin,8)
		box(root,Vector3(side*0.075,1.105,0.158),Vector3(0.067,0.050,0.024),material("eye_white","ddd3bb"))
		box(root,Vector3(side*0.075,1.102,0.174),Vector3(0.028,0.040,0.012),dark)
		var brow := box(root,Vector3(side*0.075,1.155,0.168),Vector3(0.075,0.017,0.018),hair)
		brow.rotation.z=side*0.08
		limb(root,Vector3(side*0.18,0.78,0),Vector3(side*0.25,0.54,0.04),0.07,cloth)
		limb(root,Vector3(side*0.25,0.54,0.04),Vector3(side*0.22,0.44,0.10),0.055,cloth)
		ellipsoid(root,Vector3(side*0.22,0.43,0.105),Vector3(0.10,0.12,0.09),skin,8)
	ellipsoid(root,Vector3(0,1.055,0.183),Vector3(0.055,0.067,0.06),skin,8)
	box(root,Vector3(0,0.982,0.16),Vector3(0.075,0.012,0.012),material("mouth","805547"))
	CAST.dress(self,root,identity)
