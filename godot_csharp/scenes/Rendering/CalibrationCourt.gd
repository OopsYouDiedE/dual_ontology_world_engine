extends Node3D
## Original non-campaign material/light calibration set. No camera or global environment.
const SURFACE = preload("res://rendering/rpg/StylizedSurface.gdshader")
const Arcade = preload("res://geometry/TerrainArcade.gd")
var stone: Material
var trim: Material
var bronze: Material
var dark: Material
var lamps: Array[OmniLight3D] = []
var probes: Array[ReflectionProbe] = []
var arcades: Array[Node3D] = []

func _ready() -> void:
	stone = surface(Color("abb8b4"), 0.8)
	trim = surface(Color("ded5b6"), 0.72)
	bronze = surface(Color("af8748"), 0.38, 0.8)
	dark = surface(Color("284d54"), 0.7)
	var floor_mat := ShaderMaterial.new()
	floor_mat.shader = preload("res://rendering/shaders/surfaces/tile_floor.gdshader")
	floor_mat.set_shader_parameter("base_color", Color("989c93"))
	floor_mat.set_shader_parameter("accent_color", Color("374e4c"))
	floor_mat.set_shader_parameter("tile_size", 0.8)
	box("Foundation", Vector3(0,-0.24,0), Vector3(25,0.45,25), dark)
	box("Paving", Vector3(0,-0.035,0), Vector3(23,0.08,23), floor_mat)
	# Dedicated physical terrain layer is sampled by the architecture nodes.
	for child in get_children():
		if child is MeshInstance3D:
			for body in child.get_children():
				if body is StaticBody3D: body.collision_layer = 2
	_build_slope()
	for side in [-1, 1]:
		var arcade := Node3D.new()
		arcade.set_script(Arcade)
		arcade.name = "TerrainArcadeNorth" if side < 0 else "TerrainArcadeSouth"
		arcade.position = Vector3(0,0,side*7)
		arcade.rotation.y = PI if side < 0 else 0.0
		arcade.stone = stone
		arcade.trim = trim
		arcade.infill = dark
		add_child(arcade)
		arcades.append(arcade)
		for x in [-5.4, -1.8, 1.8, 5.4]:
			lamp(Vector3(x,2.6,float(side)*9.55))
	# Water basin with physical rim, reflecting off-screen architecture via probe.
	box("Basin", Vector3(0,0.12,0), Vector3(4.4,0.24,7.4), dark)
	for x in [-2.3,2.3]: box("PoolRim", Vector3(x,0.26,0), Vector3(0.32,0.5,7.9), trim)
	for z in [-3.8,3.8]: box("PoolRim", Vector3(0,0.26,z), Vector3(4.9,0.5,0.32), trim)
	var water := ShaderMaterial.new()
	water.shader = preload("res://rendering/shaders/surfaces/water.gdshader")
	box("WaterSurface", Vector3(0,0.3,0), Vector3(4.25,0.025,7.25), water, false)
	cylinder("Plinth", Vector3(0,0.55,0), 0.65, 0.6, trim)
	cylinder("PlinthBand", Vector3(0,0.88,0), 0.72, 0.08, bronze)
	cylinder("Shaft", Vector3(0,1.2,0), 0.2, 0.6, bronze)
	for i in range(3):
		var ring := TorusMesh.new()
		ring.inner_radius = 0.85
		ring.outer_radius = 0.91
		ring.rings = 64
		ring.ring_segments = 12
		var node := mesh("ArmillaryRing", ring, Vector3(0,2.15,0), bronze)
		node.rotation_degrees = Vector3(60+i*35, i*45, 18)
	var orb := SphereMesh.new()
	orb.radius = 0.38
	orb.height = 0.76
	mesh("Globe", orb, Vector3(0,2.15,0), bronze)
	# Roughness / dielectric / metal specimens at known physical sizes.
	for i in range(5):
		var x := -5.0 + i*2.5
		cylinder("SamplePlinth", Vector3(x,0.45,5.0), 0.46,0.9,trim)
		var sample := StandardMaterial3D.new()
		sample.albedo_color = Color("b9c7c4") if i < 3 else Color("b19460")
		sample.metallic = 0.0 if i < 3 else 1.0
		sample.roughness = [0.12,0.45,0.9,0.12,0.65][i]
		var ball := SphereMesh.new()
		ball.radius = 0.37
		ball.height = 0.74
		mesh("PbrSample",ball,Vector3(x,1.3,5),sample)
	# Benches expose wood grain, contact shadows and under-seat bounce.
	for side in [-1,1]:
		var wood := surface(Color("886443"),0.7,0,true)
		for z in [-3.5,1.5]:
			for plank in range(4):
				box("BenchSlat",Vector3(side*5.1,0.48,z+plank*0.15),Vector3(2.2,0.08,0.13),wood)
			for offset in [-0.8,0.8]:
				box("BenchLeg",Vector3(side*5.1+offset,0.22,z+0.23),Vector3(0.12,0.44,0.5),bronze)
	for x in [-9.2,9.2]:
		for z in [-4.5,0.0,4.5]:
			cylinder("Planter",Vector3(x,0.38,z),0.72,0.76,dark)
			cylinder("PlanterLip",Vector3(x,0.77,z),0.78,0.1,trim)
			var foliage := Node3D.new()
			foliage.set_script(load("res://geometry/FoliageCluster.gd"))
			foliage.position = Vector3(x,0.82,z)
			add_child(foliage)
	var probe := ReflectionProbe.new()
	probe.name = "CourtyardReflection"
	probe.position = Vector3(0,2,0)
	probe.size = Vector3(22,9,22)
	probe.box_projection = true
	probe.max_distance = 35
	add_child(probe)
	probes.append(probe)
	# Geometry nodes build after their terrain enters the physics server.
	await get_tree().physics_frame
	await get_tree().physics_frame
	await get_tree().physics_frame
	refresh_probes()

func _build_slope() -> void:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	for x in range(-9,9):
		for z in range(-11,-5):
			for corner in [Vector2(0,0),Vector2(1,0),Vector2(1,1),Vector2(0,0),Vector2(1,1),Vector2(0,1)]:
				var px: float = x+corner.x
				st.add_vertex(Vector3(px,0.04+maxf(0,px+4)*0.075,z+corner.y))
	st.generate_normals()
	var terrain := mesh("SlopingTerrain",st.commit(),Vector3.ZERO,stone)
	terrain.create_trimesh_collision()
	for body in terrain.get_children():
		if body is StaticBody3D: body.collision_layer = 2

func refresh_probes() -> void:
	# UPDATE_ONCE captures again on re-entry after a deliberate lighting preset change.
	for probe in probes:
		remove_child(probe)
		add_child(probe)

func lamp(p: Vector3) -> void:
	box("LampBracket",p+Vector3(0,0,0.15),Vector3(0.35,0.65,0.12),bronze)
	var emissive := StandardMaterial3D.new()
	emissive.albedo_color = Color("f2d091")
	emissive.emission_enabled = true
	emissive.emission = Color("f2d091")
	emissive.emission_energy_multiplier = 4.0
	box("LampGlass",p,Vector3(0.18,0.4,0.18),emissive,false)
	var light := OmniLight3D.new()
	light.position = p+Vector3(0,-0.1,-signf(p.z)*0.25)
	light.light_color = Color("f2d091")
	light.light_energy = 3.0
	light.omni_range = 5.0
	light.omni_attenuation = 1.5
	light.shadow_enabled = true
	light.light_size = 0.14
	add_child(light)
	lamps.append(light)

func mesh(label: String, shape: Mesh, p: Vector3, material: Material) -> MeshInstance3D:
	var node := MeshInstance3D.new()
	node.name = label
	node.mesh = shape
	node.material_override = material
	node.position = p
	node.gi_mode = GeometryInstance3D.GI_MODE_STATIC
	add_child(node)
	return node

func box(label: String, p: Vector3, size: Vector3, material: Material, collision := true) -> void:
	var shape := BoxMesh.new()
	shape.size = size
	var node := mesh(label,shape,p,material)
	if collision: node.create_trimesh_collision()

func cylinder(label: String, p: Vector3, radius: float, height: float, material: Material) -> void:
	var shape := CylinderMesh.new()
	shape.top_radius = radius
	shape.bottom_radius = radius
	shape.height = height
	shape.radial_segments = 40
	var node := mesh(label,shape,p,material)
	node.create_trimesh_collision()

func surface(color: Color, roughness: float, metallic := 0.0, wood := false) -> ShaderMaterial:
	var material := ShaderMaterial.new()
	material.shader = SURFACE
	material.set_shader_parameter("pigment",color)
	material.set_shader_parameter("surface_roughness",roughness)
	material.set_shader_parameter("surface_metallic",metallic)
	material.set_shader_parameter("wood",wood)
	return material
