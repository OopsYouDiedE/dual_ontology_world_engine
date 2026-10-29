extends Node3D
## Reusable visual construction functions, in metres. No world rules or environment.
const SURFACE = preload("res://rendering/rpg/StylizedSurface.gdshader")
var palette: Dictionary = {}

func material(key: String, color: String, roughness := 0.8, metal := 0.0, wood := false) -> Material:
	if palette.has(key): return palette[key]
	var m := ShaderMaterial.new()
	m.shader = SURFACE
	m.set_shader_parameter("pigment", Color(color))
	m.set_shader_parameter("surface_roughness", roughness)
	m.set_shader_parameter("surface_metallic", metal)
	m.set_shader_parameter("wood", wood)
	palette[key] = m
	return m

func part(label: String, shape: Mesh, p: Vector3, mat: Material, parent: Node3D = self) -> MeshInstance3D:
	var n := MeshInstance3D.new()
	n.name = label
	n.mesh = shape
	n.position = p
	n.material_override = mat
	n.gi_mode = GeometryInstance3D.GI_MODE_STATIC
	parent.add_child(n)
	return n

func block(label: String, p: Vector3, s: Vector3, mat: Material, parent: Node3D = self) -> MeshInstance3D:
	# Shared small physical bevel catches light on stone, joinery and metal alike.
	var bevel := minf(0.025,minf(s.x,minf(s.y,s.z))*0.16)
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var rings: Array = []
	for level in range(4):
		var inset: float = bevel if level == 0 or level == 3 else 0.0
		var x := s.x/2-inset
		var z := s.z/2-inset
		var c := minf(bevel,minf(x,z)*0.4)
		var y: float = [-s.y/2,-s.y/2+bevel,s.y/2-bevel,s.y/2][level]
		rings.append([Vector3(-x+c,y,-z),Vector3(x-c,y,-z),Vector3(x,y,-z+c),Vector3(x,y,z-c),Vector3(x-c,y,z),Vector3(-x+c,y,z),Vector3(-x,y,z-c),Vector3(-x,y,-z+c)])
	for level in range(3):
		for i in range(8):
			var j := (i+1)%8
			for v in [rings[level][i],rings[level+1][j],rings[level+1][i],rings[level][i],rings[level][j],rings[level+1][j]]: st.add_vertex(v)
	for i in range(8):
		var j := (i+1)%8
		for v in [Vector3(0,s.y/2,0),rings[3][i],rings[3][j],Vector3(0,-s.y/2,0),rings[0][j],rings[0][i]]: st.add_vertex(v)
	st.generate_normals()
	return part(label,st.commit(),p,mat,parent)

func cylinder(label: String, p: Vector3, radius: float, height: float, mat: Material, parent: Node3D = self, top := -1.0) -> MeshInstance3D:
	var mesh := CylinderMesh.new()
	mesh.top_radius = radius if top < 0 else top
	mesh.bottom_radius = radius
	mesh.height = height
	mesh.radial_segments = 12
	return part(label, mesh, p, mat, parent)

func group(label: String, p: Vector3, angle := 0.0) -> Node3D:
	var n := Node3D.new()
	n.name = label
	n.position = p
	n.rotation.y = angle
	add_child(n)
	return n

func chair(p: Vector3, angle: float, timber: Material, accent: Material) -> void:
	var root := group("JoineryChair", p, angle)
	for x in [-0.25,0.25]:
		for z in [-0.23,0.23]:
			block("Leg", Vector3(x,0.25,z), Vector3(0.085,0.5,0.085),timber,root)
		block("SideStretcher",Vector3(x,0.18,0),Vector3(0.055,0.055,0.5),timber,root)
		block("BackPost",Vector3(x,0.83,-0.23),Vector3(0.075,0.65,0.075),timber,root)
	block("SeatRim",Vector3(0,0.48,0),Vector3(0.61,0.1,0.58),timber,root)
	block("SeatPad",Vector3(0,0.545,0.015),Vector3(0.49,0.06,0.45),accent,root)
	for x in [-0.14,0.0,0.14]:
		block("BackSplat",Vector3(x,0.84,-0.23),Vector3(0.065,0.48,0.045),timber,root)
	block("CrestRail",Vector3(0,1.12,-0.23),Vector3(0.62,0.12,0.09),timber,root)
	block("FrontStretcher",Vector3(0,0.22,0.23),Vector3(0.5,0.06,0.055),timber,root)

func table(p: Vector3, timber: Material) -> void:
	var root := group("JoineryTable",p)
	for x in [-1.1,1.1]:
		for z in [-0.42,0.42]:
			block("Leg",Vector3(x,0.38,z),Vector3(0.13,0.76,0.13),timber,root)
		block("Trestle",Vector3(x,0.18,0),Vector3(0.12,0.12,0.96),timber,root)
	block("LongStretcher",Vector3(0,0.2,0),Vector3(2.2,0.1,0.1),timber,root)
	for z in [-0.45,0.45]:
		block("Apron",Vector3(0,0.66,z),Vector3(2.35,0.16,0.08),timber,root)
	for z in range(5):
		block("TopPlank",Vector3(0,0.81,(z-2)*0.225),Vector3(2.65,0.12,0.217),timber,root)

func door(p: Vector3, angle: float, timber: Material, metal: Material) -> void:
	var root := group("PanelledDoor",p,angle)
	for x in [0.08,1.22]:
		block("Stile",Vector3(x,1.15,0),Vector3(0.16,2.3,0.16),timber,root)
	for y in [0.12,1.02,2.18]:
		block("Rail",Vector3(0.65,y,0),Vector3(1.15,0.2,0.16),timber,root)
	for y in [0.56,1.6]:
		block("RecessedPanel",Vector3(0.65,y,-0.035),Vector3(1.0,0.79,0.065),timber,root)
		for x in [0.2,1.1]:
			block("RaisedMoulding",Vector3(x,y,0.035),Vector3(0.04,0.8,0.045),timber,root)
		for dy in [-0.38,0.38]:
			block("RaisedMoulding",Vector3(0.65,y+dy,0.035),Vector3(0.94,0.04,0.045),timber,root)
	for y in [0.38,1.9]:
		block("StrapHinge",Vector3(0.23,y,0.1),Vector3(0.45,0.07,0.045),metal,root)
	block("LatchPlate",Vector3(1.1,1.08,0.1),Vector3(0.1,0.22,0.035),metal,root)
	block("Handle",Vector3(1.03,1.1,0.16),Vector3(0.2,0.045,0.055),metal,root)

func arch(p: Vector3, radius: float, thickness: float, depth: float, mat: Material) -> void:
	# Actual wedge geometry: both soffit and outer curve have depth.
	var root := group("VoussoirArch",p)
	for i in range(13):
		var a := PI*i/13.0+0.006
		var b := PI*(i+1)/13.0-0.006
		var points: Array[Vector3] = []
		for z in [-depth/2,depth/2]:
			for r in [radius,radius+thickness]:
				for angle in [a,b]: points.append(Vector3(cos(angle)*r,sin(angle)*r,z))
		var st := SurfaceTool.new()
		st.begin(Mesh.PRIMITIVE_TRIANGLES)
		for face in [[0,1,3,2],[4,6,7,5],[0,4,5,1],[2,3,7,6],[0,2,6,4],[1,5,7,3]]:
			for index in [0,1,2,0,2,3]: st.add_vertex(points[face[index]])
		st.generate_normals()
		part("Wedge",st.commit(),Vector3.ZERO,mat,root)
