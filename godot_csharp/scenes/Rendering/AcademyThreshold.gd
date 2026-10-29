extends "res://geometry/AcademyJoinery.gd"
## Non-campaign visual specimen. Its arrangement does not declare playable world content.
var stone: Material
var trim: Material
var wood: Material
var bronze: Material
var snow: Material
var iron: Material

func _ready() -> void:
	stone = material("stone","777c91")
	trim = material("trim","b5ac9e")
	wood = material("wood","68402b",0.73,0.0,true)
	bronze = material("bronze","c1984e",0.38,0.72)
	snow = material("snow","c5d7ee",0.96)
	iron = material("iron","273139",0.6,0.55)
	var mortar := material("mortar","4b5263")
	var plaster := material("plaster","b7a78e")
	block("Ground",Vector3(0,-0.25,1),Vector3(160,0.4,160),snow)
	block("TerraceFoundation",Vector3(0,-0.08,2),Vector3(12,0.3,8),stone)
	for x in range(-8,9):
		for z in range(-1,9):
			var tone := material("pave%d" % posmod(x+z,3),["838792","777d89","8c8b93"][posmod(x+z,3)])
			block("Paving",Vector3(x*0.68,0.085,z*0.66),Vector3(0.665,0.12,0.645),tone)
	block("RaisedRoomFloor",Vector3(-0.3,0.28,-2.55),Vector3(10.6,0.6,5.6),wood)
	for x in range(26):
		block("FloorBoard",Vector3(-5.3+x*0.4,0.605,-2.6),Vector3(0.385,0.05,5.3),wood)
	for step in range(3):
		block("EntryStep",Vector3(-1.7,0.14+step*0.17,1.0-step*0.38),Vector3(3.6,0.2,0.48),trim)
	# Facade built around an actual opening; the arch has no opaque wall behind it.
	for span in [Vector2(-5.5,-3.3),Vector2(-0.1,5.5)]:
		block("WallCore",Vector3((span.x+span.y)/2,2.7,-0.12),Vector3(span.y-span.x,4.2,0.42),mortar)
		for row in range(8):
			var left: float = span.x
			while left < span.y-0.01:
				var width: float = minf(0.74 if row%2==0 else 0.92,span.y-left)
				block("Ashlar",Vector3(left+width/2,0.86+row*0.51,0.13),Vector3(width-0.022,0.486,0.2),stone)
				left += width
	arch(Vector3(-1.7,2.85,0.13),1.6,0.28,0.6,trim)
	for x in [-3.38,0.0,5.25]:
		block("Pilaster",Vector3(x,2.18,0.34),Vector3(0.34,3.2,0.32),trim)
		for y in [0.7,0.9,3.7,3.86]:
			block("CapitalBase",Vector3(x,y,0.35),Vector3(0.53,0.13,0.5),trim)
		for y in range(5):
			block("PilasterRecess",Vector3(x,1.2+y*0.45,0.51),Vector3(0.15,0.34,0.025),stone)
	block("Cornice",Vector3(0,4.87,0.13),Vector3(11.5,0.22,0.8),trim)
	block("CorniceSnow",Vector3(0,5.02,0.13),Vector3(11.5,0.09,0.8),snow)
	for x in range(22):
		block("CorniceDentil",Vector3(-5.25+x*0.5,4.65,0.25),Vector3(0.2,0.19,0.26),trim)
	for x in [-4.45,2.55]:
		block("WallPlinth",Vector3(x,0.73,0.3),Vector3(2.1 if x < 0 else 5.25,0.24,0.44),trim)
	block("BackWall",Vector3(-0.3,2.7,-5.1),Vector3(10.6,4.2,0.3),plaster)
	block("LeftWall",Vector3(-5.5,2.7,-2.6),Vector3(0.3,4.2,5),plaster)
	block("RoomCeiling",Vector3(-0.3,4.8,-2.6),Vector3(10.6,0.2,5.3),wood)
	for x in [-4.7,-2.7,-0.7,1.3,3.3,4.7]:
		block("WainscotStile",Vector3(x,1.3,-4.9),Vector3(0.1,1.4,0.1),wood)
	for y in [0.7,1.8]: block("WainscotRail",Vector3(0,y,-4.87),Vector3(10.4,0.12,0.15),wood)
	door(Vector3(-3.22,0.62,0.25),deg_to_rad(-67),wood,iron)
	_window(Vector3(3.35,2.65,0.28))
	table(Vector3(-1.8,0.63,-2.2),wood)
	var cushion := material("cushion","414e4d")
	for x in [-2.7,-1.0]:
		chair(Vector3(x,0.63,-3.05),0,wood,cushion)
		chair(Vector3(x,0.63,-1.3),PI,wood,cushion)
	chair(Vector3(-4.1,0.16,2.9),0.28,wood,cushion)
	chair(Vector3(-3.1,0.16,3.2),-0.2,wood,cushion)
	for x in [-2.7,-1.8,-0.9]:
		cylinder("Plate",Vector3(x,1.52,-2.2),0.2,0.035,material("ceramic","d7c6a3"))
		for j in range(3):
			var dumpling := SphereMesh.new()
			dumpling.radius = 0.065
			dumpling.height = 0.085
			dumpling.radial_segments = 8
			dumpling.rings = 4
			part("Dumpling",dumpling,Vector3(x-0.08+j*0.08,1.57,-2.2),material("dough","e7cf99"))
	_stove(Vector3(0.6,0.65,-3.7))
	_pipe(Vector3(1.1,0.6,0.65))
	_lantern(Vector3(1.25,3.35,0.75))
	for x in [0.9,4.7]:
		block("Planter",Vector3(x,0.49,2.1),Vector3(1.0,0.65,0.95),stone)
		block("PlanterRim",Vector3(x,0.83,2.1),Vector3(1.12,0.14,1.07),trim)
		block("Soil",Vector3(x,0.91,2.1),Vector3(0.85,0.05,0.8),mortar)
		var shrub := Node3D.new()
		shrub.set_script(preload("res://geometry/FoliageCluster.gd"))
		shrub.position = Vector3(x,0.94,2.1)
		shrub.radius = 0.8
		shrub.leaf_count = 65
		shrub.leaf_color = Color("456453")
		add_child(shrub)
	for z in [1.6,3.3,5.0]:
		block("BalustradePier",Vector3(5.45,0.67,z),Vector3(0.4,1.05,0.4),stone)
		block("PierCap",Vector3(5.45,1.22,z),Vector3(0.57,0.16,0.57),trim)
		block("PierSnow",Vector3(5.45,1.34,z),Vector3(0.57,0.08,0.57),snow)
	block("Railing",Vector3(5.45,0.9,3.3),Vector3(0.15,0.18,3.4),trim)
	for i in range(12):
		block("Baluster",Vector3(5.45,0.55,1.65+i*0.3),Vector3(0.09,0.65,0.09),stone)
	# Broad deliberate snow patches; no per-pixel random dirt.
	for i in range(12):
		block("SnowBank",Vector3(-5.3+i*0.93,0.19,5.3+sin(i)*0.22),Vector3(1.05,0.09,0.75),snow)
	for z in [1.9,3.2,4.5]:
		block("WallSnow",Vector3(-5.3,0.2,z),Vector3(0.65,0.1,1.3),snow)
	var probe := ReflectionProbe.new()
	probe.position = Vector3(0,2,0)
	probe.size = Vector3(15,10,16)
	probe.box_projection = true
	add_child(probe)
	# Meshes carry actual geometry collisions for camera/navigation integration.
	_collision_tree(self)

func _collision_tree(node: Node) -> void:
	for child in node.get_children():
		if child is MeshInstance3D: child.create_trimesh_collision()
		elif child is Node3D and not child.get_script() == preload("res://geometry/FoliageCluster.gd"):
			_collision_tree(child)

func _window(p: Vector3) -> void:
	var root := group("RecessedWindow",p)
	block("DarkGlazing",Vector3.ZERO,Vector3(1.6,2.15,0.08),material("glass","263b4b",0.25),root)
	for x in [-0.86,0.86]: block("StoneReveal",Vector3(x,0,0.02),Vector3(0.2,2.5,0.34),trim,root)
	for y in [-1.2,1.2]: block("StoneLintel",Vector3(0,y,0.06),Vector3(1.94,0.2,0.4),trim,root)
	for x in [-0.7,0,0.7]: block("Mullion",Vector3(x,0,0.11),Vector3(0.065,2.1,0.09),wood,root)
	for y in [-0.9,0,0.9]: block("Transom",Vector3(0,y,0.11),Vector3(1.5,0.07,0.09),wood,root)
	block("SillSnow",Vector3(0,-1.07,0.18),Vector3(1.9,0.08,0.47),snow,root)

func _pipe(p: Vector3) -> void:
	var root := group("SpeakingTube",p)
	cylinder("Riser",Vector3(0,1.4,0),0.055,2.8,bronze,root)
	for y in [0.2,0.8,1.7,2.65]: cylinder("Collar",Vector3(0,y,0),0.085,0.08,bronze,root)
	var mouth := cylinder("BellMouth",Vector3(0,1.8,0.15),0.15,0.28,bronze,root,0.06)
	mouth.rotation.x = PI/2
	var bore := cylinder("MouthBore",Vector3(0,1.8,0.296),0.12,0.005,iron,root)
	bore.rotation.x = PI/2
	var torus := TorusMesh.new()
	torus.inner_radius = 0.16
	torus.outer_radius = 0.195
	torus.rings = 16
	torus.ring_segments = 6
	var wheel := part("ValveWheel",torus,Vector3(0,0.85,0.12),bronze,root)
	wheel.rotation.x = PI/2
	for angle in [0.0,PI/3,2*PI/3]:
		var spoke := block("Spoke",Vector3(0,0.85,0.12),Vector3(0.34,0.025,0.025),bronze,root)
		spoke.rotation.z = angle

func _emission(key: String, color: String) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = Color(color)
	m.emission_enabled = true
	m.emission = Color(color)
	m.emission_energy_multiplier = 3.0
	palette[key] = m
	return m

func _light(p: Vector3, color: String, energy: float, reach: float) -> void:
	var light := OmniLight3D.new()
	light.position = p
	light.light_color = Color(color)
	light.light_energy = energy
	light.omni_range = reach
	light.shadow_enabled = true
	light.light_size = 0.18
	add_child(light)

func _lantern(p: Vector3) -> void:
	var root := group("GasMantleLantern",p)
	block("Bracket",Vector3(0,0.35,-0.16),Vector3(0.08,0.08,0.6),iron,root)
	block("Glass",Vector3.ZERO,Vector3(0.23,0.37,0.23),_emission("mantle","f6e4b3"),root)
	for x in [-0.15,0.15]:
		for z in [-0.15,0.15]: block("Frame",Vector3(x,0,z),Vector3(0.035,0.43,0.035),iron,root)
	cylinder("Hood",Vector3(0,0.28,0),0.29,0.18,iron,root,0.09)
	block("Foot",Vector3(0,-0.25,0),Vector3(0.36,0.08,0.36),iron,root)
	_light(p+Vector3(0,-0.1,0.4),"fff0cb",2.8,5.5)

func _stove(p: Vector3) -> void:
	var root := group("IronStove",p)
	block("Body",Vector3(0,0.5,0),Vector3(0.85,0.8,0.65),iron,root)
	block("FireWindow",Vector3(0,0.47,0.335),Vector3(0.51,0.33,0.025),_emission("fire","ff8d36"),root)
	for x in [-0.23,-0.08,0.08,0.23]: block("FireGrate",Vector3(x,0.47,0.36),Vector3(0.04,0.4,0.05),iron,root)
	block("Hob",Vector3(0,0.95,0),Vector3(1.02,0.1,0.8),iron,root)
	cylinder("Flue",Vector3(0.24,2.0,-0.22),0.12,2.0,iron,root)
	cylinder("Pot",Vector3(-0.12,1.11,0.02),0.24,0.25,bronze,root)
	_light(p+Vector3(0,1.1,0.65),"ffb463",2.5,7.0)
	_light(Vector3(-2.5,2.5,-2.5),"ffd69b",1.4,6.0)
