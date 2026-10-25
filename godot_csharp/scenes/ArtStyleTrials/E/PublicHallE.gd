extends Node3D
## Isolated full-3D art trial. No Core, scene routing or gameplay state.
const SURFACE = preload("res://scenes/ArtStyleTrials/E/painted_surface.gdshader")
const FIRE = preload("res://scenes/ArtStyleTrials/E/fire.gdshader")
const TILE := 0.5
var camera: Camera3D
var key_light: SpotLight3D
var local_lights: Array[OmniLight3D] = []
var fire_materials: Array[ShaderMaterial] = []
var materials: Dictionary = {}
var figures: Array[Node3D] = []
var dust_motes: Array[MeshInstance3D] = []
var clock := 0.0
var lighting_mode := "normal"

func _ready() -> void:
	name = "PublicHallE"
	_environment()
	_architecture()
	_furnishings()
	_characters()
	_atmosphere()
	configure_lighting("normal")
	_refine()
	set_meta("render_metadata", {"route":"E / D geometry with A painterly palette", "target_size":Vector2i(1600,1600), "materials":"Object-space pigment washes, directional wood grain, woven cloth, GGX brass and ceramic, animated water and fire", "source":"D procedural geometry; A hall painting is visual reference only, not projected onto meshes", "limits":"Static three-head figures; isolated art scene, no production collision or DV session; procedural brushwork does not reproduce all authored ornament"})

func point(gx: float, gy: float, height: float = 0.0) -> Vector3:
	return Vector3((gx - 8.0) * TILE, height + (0.22 if gy < 15.5 else 0.0), (gy - 20.0) * TILE)

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
	shape.radial_segments = segments
	shape.rings = 5
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
	shape.radial_segments = 12
	mesh.mesh = shape
	mesh.position = at
	mesh.material_override = mat
	parent.add_child(mesh)
	return mesh

func limb(parent: Node3D, start: Vector3, end: Vector3, radius: float, mat: Material) -> MeshInstance3D:
	var result := cylinder(parent, (start + end) * 0.5, radius, start.distance_to(end), mat)
	result.quaternion = Quaternion(Vector3.UP, (end - start).normalized())
	return result

func group(label: String, at: Vector3) -> Node3D:
	var root := Node3D.new()
	root.name = label
	root.position = at
	add_child(root)
	return root

func _environment() -> void:
	var world := WorldEnvironment.new()
	world.name = "Environment"
	var env := Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = Color("252c32")
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color("b4bec5")
	env.ambient_light_energy = 0.55
	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	world.environment = env
	add_child(world)
	key_light = SpotLight3D.new()
	key_light.name = "WindowKey"
	key_light.spot_range = 30.0
	key_light.spot_angle = 69.0
	key_light.light_energy = 0.85
	key_light.light_color = Color("c5dcf2")
	key_light.shadow_enabled = true
	key_light.shadow_bias = 0.04
	add_child(key_light)
	var fill := DirectionalLight3D.new()
	fill.name = "SoftSky"
	fill.rotation_degrees = Vector3(-70, -25, 0)
	fill.light_color = Color("bbc8ce")
	fill.light_energy = 0.28
	fill.shadow_enabled = true
	add_child(fill)
	camera = Camera3D.new()
	camera.name = "Camera3D"
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.size = 16.8
	camera.position = Vector3(0, 22, 27)
	add_child(camera)
	camera.look_at(Vector3(0, 0.3, -0.9), Vector3.UP)
	camera.current = true
	camera.near = 0.1
	camera.far = 90.0

func _architecture() -> void:
	var walnut := material("walnut", "493227", 1)
	var cream := material("plaster", "c9bea0", 2)
	var brass := material("brass", "947748", 0, 0.3, 0.55)
	var stone := material("basalt", "55585c", 2)
	box(self, Vector3(0,-0.12,0), Vector3(8.1,0.24,20.1), stone, "Foundation")
	# Individually laid parquet pieces with fine joints; no raster upscaling.
	for row in range(60):
		for col in range(14):
			var x := -3.5 + col * 0.5 + 0.25
			var z := -9.5 + row * 0.25 + 0.125
			if z > 5.5: continue
			var tier := 0.22 if z < -2.25 else 0.0
			var tint: String = ["765438","79573b","735137","7c593d"][(row * 7 + col * 3) % 4]
			var plank := box(self,Vector3(x,tier+0.005,z),Vector3(0.493,0.035,0.243),material("plank"+str((row*7+col*3)%4),tint,1))
			plank.cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
			if (row / 2 + col) % 2 == 0: plank.rotation.y = PI
	# Visible platform riser, rather than painted elevation.
	box(self,Vector3(0,0.11,-2.24),Vector3(7.0,0.22,0.10),walnut,"HallStep")
	box(self,Vector3(0,0.235,-2.26),Vector3(7.0,0.035,0.16),brass)
	for yy in range(7):
		for xx in range(14):
			var ceramic := material("ceramic_white" if (xx+yy)%2==0 else "ceramic_green", "d4d3ba" if (xx+yy)%2==0 else "365b51",2,0.34)
			var tile := box(self, Vector3(-3.25+xx*0.5,0.015,6.25+yy*0.5), Vector3(0.486,0.04,0.486),ceramic)
			tile.cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	# North wall and front-elevation pilasters; side cutaways retain doorways.
	for x in [-2.25,2.25]:
		box(self,Vector3(x,1.52,-9.62),Vector3(3.0,2.6,0.18),cream)
		box(self,Vector3(x,0.58,-9.49),Vector3(3.0,0.75,0.09),walnut)
		box(self,Vector3(x,0.99,-9.43),Vector3(3.0,0.055,0.08),brass)
		for px in range(5):
			box(self,Vector3(x-1.35+px*0.67,0.58,-9.42),Vector3(0.045,0.64,0.025),brass)
	_door(self,Vector3(0,0.22,-9.45),false)
	for side in [-1,1]:
		for cell_y in range(1,39):
			if cell_y in [7,8,15,16,23,24]: continue
			var z := (cell_y-20)*0.5
			var tier := 0.22 if cell_y<15.5 else 0.0
			box(self,Vector3(side*3.63,tier+0.45,z),Vector3(0.16,0.9,0.49),cream)
			box(self,Vector3(side*3.57,tier+0.22,z),Vector3(0.08,0.43,0.49),walnut)
			box(self,Vector3(side*3.57,tier+0.91,z),Vector3(0.22,0.06,0.5),walnut)
		for cell_y in [7,15,23]:
			var jamb := group("SideDoor",point(0.65 if side<0 else 15.35,cell_y+0.5))
			jamb.rotation.y = PI/2.0 if side<0 else -PI/2.0
			_door(jamb,Vector3.ZERO,true)
	# Bathroom divider is low so all fixtures remain visible in the full-room view.
	for x in [-2.25,2.25]:
		box(self,Vector3(x,0.45,5.65),Vector3(3.0,0.9,0.16),cream)
		box(self,Vector3(x,0.92,5.65),Vector3(3.05,0.05,0.2),walnut)
	_door(self,Vector3(0,0,5.65),true)
	for gx in [3,13]:
		_window(point(gx,1.0,1.7))
	_rug(point(5,7,0.035),Vector2(2.9,1.5))
	_rug(point(8,24,0.04),Vector2(1.7,5.3))

func _door(parent: Node3D, at: Vector3, cutaway: bool) -> void:
	var wood := material("door","46352a",1)
	var trim := material("trim","957148",1)
	var brass := material("brass","947748",0,0.3,0.55)
	var h := 0.88 if cutaway else 1.95
	for dx in [-0.55,0.55]: box(parent,at+Vector3(dx,h*0.5,0),Vector3(0.10,h,0.18),trim)
	if not cutaway:
		box(parent,at+Vector3(0,h,0),Vector3(1.2,0.13,0.2),trim)
		for dx in [-0.245,0.245]:
			box(parent,at+Vector3(dx,0.95,-0.03),Vector3(0.47,1.84,0.08),wood)
			for y in [0.5,1.35]: box(parent,at+Vector3(dx,y,0.025),Vector3(0.35,0.58,0.018),trim)
			ellipsoid(parent,at+Vector3(dx*0.28,0.90,0.10),Vector3(0.06,0.06,0.04),brass)

func _window(at: Vector3) -> void:
	var wood := material("door","46352a",1)
	var glass := material("window","4c6d80",2,0.35)
	box(self,at,Vector3(0.9,1.15,0.09),wood)
	box(self,at+Vector3(0,0,0.055),Vector3(0.76,1.0,0.025),glass)
	for dx in [-0.4,0,0.4]: box(self,at+Vector3(dx,0,0.09),Vector3(0.035,1.03,0.04),wood)
	box(self,at+Vector3(0,0,0.09),Vector3(0.8,0.035,0.04),wood)
	box(self,at+Vector3(0,-0.61,0.10),Vector3(1.0,0.08,0.26),wood)

func _rug(at: Vector3, size: Vector2) -> void:
	var green := material("rug","294c43",3)
	var gold := material("rug_gold","967d51",3)
	box(self,at,Vector3(size.x,0.025,size.y),gold)
	var woven := ShaderMaterial.new()
	woven.shader = preload("res://scenes/ArtStyleTrials/E/rug.gdshader")
	woven.set_shader_parameter("rug_size", size)
	box(self,at+Vector3(0,0.016,0),Vector3(size.x-0.10,0.025,size.y-0.10),woven)
	for side in [-1,1]:
		box(self,at+Vector3(side*(size.x*0.5-0.13),0.034,0),Vector3(0.035,0.008,size.y-0.22),gold)
		box(self,at+Vector3(0,0.034,side*(size.y*0.5-0.13)),Vector3(size.x-0.22,0.008,0.035),gold)
	var emblem := box(self,at+Vector3(0,0.04,0),Vector3(0.28,0.01,0.28),gold)
	emblem.rotation.y=PI/4.0

func _furnishings() -> void:
	_fireplace(point(4,3))
	_bookcase(point(2,3),false)
	_bookcase(point(12,3),true)
	_board(point(11,3))
	_chair(point(3,6),true)
	_chair(point(6,6),true)
	_table(point(4.75,6),Vector2(0.65,0.65),0.62)
	_lamp(point(13,7))
	_coatstand(point(13,12))
	_bench(point(10,13),1.25)
	_table(point(8,22),Vector2(2.0,0.70),0.65)
	_bench(point(8,20),1.8)
	_bench(point(8,23),1.8)
	_lamp(point(2,21))
	_piano(point(12,27))
	_bench(point(12,28),0.65)
	_chess(point(3,27))
	var c1 := _chair(point(2,27),false)
	c1.rotation.y=PI/2.0
	var c2 := _chair(point(3,28),false)
	c2.rotation.y=PI
	_plant(point(14,30))
	_bathroom()

func _table(at: Vector3, size: Vector2, height: float) -> Node3D:
	var root := group("CarvedTable",at)
	var wood := material("walnut","493227",1)
	var top := material("tabletop","80563c",1,0.58)
	box(root,Vector3(0,height,0),Vector3(size.x,0.09,size.y),top)
	box(root,Vector3(0,height-0.11,0),Vector3(size.x-0.1,0.16,size.y-0.1),wood)
	for xx in [-1,1]:
		for zz in [-1,1]:
			cylinder(root,Vector3(xx*(size.x/2-0.12),height/2,zz*(size.y/2-0.12)),0.045,height,wood,0.065)
	var cloth := material("cloth","b4ac8c",3)
	box(root,Vector3(0,height+0.05,0),Vector3(size.x*0.35,0.012,size.y*1.02),cloth)
	return root

func _bench(at: Vector3, width: float) -> void:
	var root := group("Bench",at)
	var wood := material("walnut","493227",1)
	box(root,Vector3(0,0.38,0),Vector3(width,0.10,0.35),wood)
	for xx in [-1,1]:
		box(root,Vector3(xx*(width/2-0.13),0.18,0),Vector3(0.10,0.36,0.25),wood)
	box(root,Vector3(0,0.13,0),Vector3(width-0.16,0.06,0.06),wood)

func _chair(at: Vector3, padded: bool) -> Node3D:
	var root := group("Armchair" if padded else "Chair",at)
	var wood := material("walnut","493227",1)
	var fabric := material("upholstery","33594a",3)
	for xx in [-1,1]:
		for zz in [-1,1]: box(root,Vector3(xx*0.22,0.23,zz*0.22),Vector3(0.065,0.46,0.065),wood)
	box(root,Vector3(0,0.45,0),Vector3(0.56,0.09,0.57),wood)
	box(root,Vector3(0,0.76,-0.23),Vector3(0.57,0.58,0.095),wood)
	if padded:
		box(root,Vector3(0,0.52,0),Vector3(0.49,0.10,0.49),fabric)
		box(root,Vector3(0,0.79,-0.17),Vector3(0.47,0.43,0.12),fabric)
		for xx in [-1,1]: box(root,Vector3(xx*0.30,0.68,0),Vector3(0.12,0.17,0.57),fabric)
	return root

func _fireplace(at: Vector3) -> void:
	var root := group("Fireplace",at)
	var stone := material("fireplace_stone","686e64",2)
	var soot := material("soot","282c2e",2)
	box(root,Vector3(0,0.56,-0.10),Vector3(1.36,1.12,0.48),stone)
	box(root,Vector3(0,0.46,0.16),Vector3(0.87,0.72,0.04),soot)
	box(root,Vector3(0,1.16,-0.05),Vector3(1.55,0.15,0.64),stone)
	box(root,Vector3(0,0.08,0.22),Vector3(1.5,0.13,0.68),stone)
	for dx in [-0.2,0.15]:
		var log := cylinder(root,Vector3(dx,0.21,0.22),0.08,0.52,material("charcoal","40322b",1))
		log.rotation.z=PI/2.0
	var flame := MeshInstance3D.new()
	var quad := QuadMesh.new()
	quad.size=Vector2(0.65,0.55)
	flame.mesh=quad
	flame.position=Vector3(0,0.44,0.23)
	var fm := ShaderMaterial.new()
	fm.shader=FIRE
	flame.material_override=fm
	fire_materials.append(fm)
	root.add_child(flame)
	_add_local_light(at+Vector3(0,0.7,0.45),Color("ffb057"),0.55,3.4)

func _bookcase(at: Vector3, alternate: bool) -> void:
	var root := group("Bookcase",at)
	var wood := material("walnut","493227",1)
	box(root,Vector3(0,0.83,-0.08),Vector3(0.95,1.65,0.30),wood)
	for shelf in range(4):
		var y := 0.14+shelf*0.38
		box(root,Vector3(0,y,0.12),Vector3(1.0,0.055,0.4),wood)
		for book in range(9):
			var height := 0.22+0.045*float((book+shelf)%3)
			var key := (book+shelf+(1 if alternate else 0))%4
			var bmat := material("book"+str(key),["435b4c","7c3f3a","bba373","464966"][key],3)
			var x := -0.40+book*0.095
			box(root,Vector3(x,y+height/2+0.03,0.17),Vector3(0.075,height,0.22),bmat)
			box(root,Vector3(x,y+0.09,0.285),Vector3(0.055,0.012,0.008),material("brass","947748",0,0.3,0.55))

func _board(at: Vector3) -> void:
	var root := group("NoticeBoard",at)
	var wood := material("walnut","493227",1)
	box(root,Vector3(0,1.10,-0.02),Vector3(1.15,0.95,0.10),wood)
	box(root,Vector3(0,1.10,0.04),Vector3(1.0,0.8,0.018),material("board","24483d",3))
	for i in range(4):
		box(root,Vector3(-0.35+i*0.23,1.12+(0.07 if i%2 else 0),0.06),Vector3(0.16,0.32,0.008),material("paper","d3c69c",3))

func _lamp(at: Vector3) -> void:
	var root := group("BrassFloorLamp",at)
	var brass := material("brass","947748",0,0.3,0.55)
	cylinder(root,Vector3(0,0.045,0),0.20,0.09,brass)
	cylinder(root,Vector3(0,0.68,0),0.028,1.30,brass)
	cylinder(root,Vector3(0,1.34,0),0.25,0.34,material("lampshade","cfbc7d",3,0.8),0.14)
	_add_local_light(at+Vector3(0,1.22,0),Color("ffe0a0"),0.38,3.5)

func _coatstand(at: Vector3) -> void:
	var root := group("CoatStand",at)
	var wood := material("walnut","493227",1)
	cylinder(root,Vector3(0,0.72,0),0.035,1.42,wood)
	for angle in [0.0,TAU/3.0,TAU*2.0/3.0]:
		limb(root,Vector3(0,0.15,0),Vector3(cos(angle)*0.28,0.04,sin(angle)*0.28),0.035,wood)
		limb(root,Vector3(0,1.2,0),Vector3(cos(angle)*0.22,1.40,sin(angle)*0.22),0.025,wood)
	box(root,Vector3(0.14,0.95,0.04),Vector3(0.24,0.52,0.07),material("hanging_coat","605344",3))

func _piano(at: Vector3) -> void:
	var root := group("UprightPiano",at)
	var wood := material("piano","282a2b",1,0.45)
	var ivory := material("ivory","d8d3b9",0,0.35)
	box(root,Vector3(0,0.60,-0.08),Vector3(1.17,1.20,0.40),wood)
	box(root,Vector3(0,0.66,0.22),Vector3(1.22,0.08,0.31),wood)
	for i in range(22):
		var x := -0.53+i*0.05
		box(root,Vector3(x,0.712,0.27),Vector3(0.045,0.02,0.19),ivory)
		if i%7 not in [2,6]: box(root,Vector3(x+0.024,0.738,0.22),Vector3(0.025,0.034,0.11),wood)
	for x in [-0.50,0.50]: cylinder(root,Vector3(x,0.28,0.30),0.045,0.55,wood)
	box(root,Vector3(0,1.23,-0.08),Vector3(1.28,0.065,0.48),wood)
	box(root,Vector3(0,0.98,0.15),Vector3(0.40,0.25,0.035),material("paper","d3c69c",3))

func _chess(at: Vector3) -> void:
	var root := _table(at,Vector2(0.7,0.7),0.63)
	for y in range(8):
		for x in range(8):
			box(root,Vector3((x-3.5)*0.065,0.686,(y-3.5)*0.065),Vector3(0.064,0.01,0.064),material("chess_white" if (x+y)%2==0 else "chess_black","c9bc95" if (x+y)%2==0 else "37433c",0,0.5))
	for i in range(6):
		cylinder(root,Vector3((i-2.5)*0.065,0.73,-0.16 if i%2==0 else 0.16),0.021,0.075,material("chess_piece","a18b5c",0,0.3))

func _plant(at: Vector3) -> void:
	var root := group("PottedPlant",at)
	var pot := material("pot","946d49",2,0.6)
	cylinder(root,Vector3(0,0.23,0),0.18,0.42,pot,0.24)
	cylinder(root,Vector3(0,0.44,0),0.25,0.055,pot)
	var green := material("foliage","355b3d",3)
	for i in range(9):
		var angle := i*TAU/9
		var leaf := ellipsoid(root,Vector3(cos(angle)*0.18,0.65+0.06*(i%3),sin(angle)*0.18),Vector3(0.17,0.50,0.12),green,8)
		leaf.rotation.z=cos(angle)*0.7
		leaf.rotation.x=sin(angle)*0.7

func _bathroom() -> void:
	var white := material("bath_ceramic","d1d3c6",2,0.28)
	var metal := material("pipe","928c70",0,0.28,0.7)
	var pool := material("water","4d817d",0,0.2)
	var basin := group("WashBasins",point(4,34))
	box(basin,Vector3(0,0.62,0),Vector3(1.30,0.15,0.58),white)
	for x in [-0.39,0.39]:
		box(basin,Vector3(x,0.71,0.02),Vector3(0.42,0.04,0.33),material("basin_inside","9aaea4",2,0.3))
		cylinder(basin,Vector3(x,0.83,-0.18),0.023,0.25,metal)
		limb(basin,Vector3(x,0.94,-0.18),Vector3(x,0.94,0.0),0.025,metal)
	for x in [-0.52,0.52]: box(basin,Vector3(x,0.30,0),Vector3(0.10,0.60,0.42),white)
	var tub := group("Bathtub",point(11,36))
	box(tub,Vector3(0,0.20,0),Vector3(1.13,0.20,1.65),white)
	box(tub,Vector3(0,0.32,0),Vector3(0.88,0.04,1.40),pool)
	for x in [-0.54,0.54]: box(tub,Vector3(x,0.42,0),Vector3(0.13,0.34,1.65),white)
	for z in [-0.77,0.77]: box(tub,Vector3(0,0.42,z),Vector3(1.13,0.34,0.13),white)
	cylinder(tub,Vector3(0.36,0.76,-0.6),0.025,0.5,metal)
	var heater := group("WaterHeater",point(14,34))
	cylinder(heater,Vector3(0,0.66,0),0.30,1.12,material("heater","697468",0,0.5,0.3))
	limb(heater,Vector3(0.20,1.2,0),Vector3(0.20,1.55,0),0.04,metal)
	var wash := group("LaundryTub",point(3,37))
	cylinder(wash,Vector3(0,0.32,0),0.33,0.58,material("laundry_wood","76614a",1),0.39)
	cylinder(wash,Vector3(0,0.62,0),0.37,0.03,pool)
	for yy in [0.15,0.47]: cylinder(wash,Vector3(0,yy,0),0.357,0.035,metal)

func _characters() -> void:
	_character("Detective",7,21,"detective",1.0)
	_character("Engineer",5,19,"engineer",1.03)
	_character("Chef",9,19,"chef",1.02)
	_character("Musician",10,25,"musician",0.98)
	_character("Thief",10,21,"thief",0.95)

func _character(label: String, gx: float, gy: float, identity: String, body_scale: float) -> void:
	var root := group(label,point(gx,gy))
	root.scale=Vector3.ONE*body_scale
	figures.append(root)
	var skin := material("skin","c9946c",0,0.86)
	var dark := material("eyes","252a29",0,0.8)
	var hair := material("hair_"+identity,"34332d" if identity in ["chef","musician"] else "4f3428",1)
	var cloth_color: String = {"detective":"654633","engineer":"a35c36","chef":"d4c6a0","musician":"285852","thief":"684754"}[identity]
	var cloth := material("cloth_"+identity,cloth_color,3)
	var boot := material("boots","3e322d",1)
	var trouser := material("trousers","3b4142",3)
	# Crown 1.32 / chin .88 = .44, total height 1.32: 3 anatomical heads.
	for side in [-1,1]:
		ellipsoid(root,Vector3(side*0.105,0.075,0.055),Vector3(0.17,0.15,0.29),boot,8)
		limb(root,Vector3(side*0.10,0.14,0),Vector3(side*0.11,0.43,0),0.075,trouser)
		cylinder(root,Vector3(side*0.11,0.22,0),0.080,0.16,boot)
	ellipsoid(root,Vector3(0,0.61,0),Vector3(0.47 if identity=="chef" else 0.38,0.48,0.25),cloth,10)
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
	# Faceted hair cap and asymmetrical locks; head is never rescaled separately.
	ellipsoid(root,Vector3(0,1.27,-0.025),Vector3(0.43,0.23,0.36),hair,10)
	for i in range(5):
		var lock := ellipsoid(root,Vector3(-0.16+i*0.075,1.22+0.015*(i%2),0.115),Vector3(0.12,0.20,0.10),hair,6)
		lock.rotation.z=0.35
	var ivory := material("ivory","d8d3b9",0,0.35)
	for side in [-1,1]:
		var collar := box(root,Vector3(side*0.065,0.81,0.13),Vector3(0.10,0.12,0.028),ivory)
		collar.rotation.z=side*0.35
	if identity=="detective":
		box(root,Vector3(0,0.62,0.145),Vector3(0.15,0.33,0.028),material("waistcoat","465745",3))
		for y in [0.51,0.61,0.71]: ellipsoid(root,Vector3(0,y,0.168),Vector3(0.018,0.018,0.012),material("brass","947748",0,0.3,0.55),6)
		for side in [-1,1]: box(root,Vector3(side*0.15,0.45,-0.045),Vector3(0.14,0.30,0.21),cloth)
	elif identity=="engineer":
		box(root,Vector3(0,0.53,0.15),Vector3(0.27,0.44,0.035),material("apron_work","5b5549",3))
		for side in [-1,1]: box(root,Vector3(side*0.095,0.75,0.15),Vector3(0.04,0.20,0.03),boot)
		box(root,Vector3(0.13,0.42,0.19),Vector3(0.12,0.12,0.05),boot)
	elif identity=="chef":
		box(root,Vector3(0,0.51,0.16),Vector3(0.32,0.43,0.035),material("apron_chef","4b4940",3))
		var grey := material("grey_hair","77786b",1)
		for side in [-1,1]:
			ellipsoid(root,Vector3(side*0.05,1.015,0.192),Vector3(0.10,0.045,0.025),grey,8)
			ellipsoid(root,Vector3(side*0.18,1.18,0),Vector3(0.06,0.12,0.10),grey,8)
		ellipsoid(root,Vector3(0,0.963,0.14),Vector3(0.12,0.10,0.06),grey,8)
	elif identity=="musician":
		var robe := cylinder(root,Vector3(0,0.38,0),0.24,0.55,cloth,0.17)
		robe.scale.z=0.72
		ellipsoid(root,Vector3(0,1.34,-0.12),Vector3(0.20,0.19,0.18),hair,10)
		box(root,Vector3(0.02,0.60,0.15),Vector3(0.035,0.42,0.02),material("embroidery","a08a55",3))
	elif identity=="thief":
		for i in range(4): ellipsoid(root,Vector3(0.11,1.20-i*0.09,-0.17-i*0.025),Vector3(0.12,0.19,0.12),hair,8)
		box(root,Vector3(0,0.50,0.16),Vector3(0.36,0.065,0.03),boot)
		box(root,Vector3(0.22,0.44,0.04),Vector3(0.15,0.18,0.13),material("toolbag","886647",1))
		for i in range(3): box(root,Vector3(0.18+i*0.035,0.55,0.10),Vector3(0.015,0.10,0.02),material("pipe","928c70",0,0.28,0.7))

func _add_local_light(at: Vector3, color: Color, energy: float, radius: float) -> void:
	var light := OmniLight3D.new()
	light.name="PracticalLamp"
	light.position=at
	light.light_color=color
	light.light_energy=energy
	light.omni_range=radius
	light.shadow_enabled=true
	light.set_meta("normal_energy",energy)
	add_child(light)
	local_lights.append(light)

func _atmosphere() -> void:
	var dust_mat := StandardMaterial3D.new()
	dust_mat.albedo_color=Color(0.78,0.73,0.56)
	dust_mat.shading_mode=BaseMaterial3D.SHADING_MODE_UNSHADED
	for i in range(28):
		var dust := ellipsoid(self,Vector3(-2.8+float((i*17)%53)*0.10,0.6+float(i%7)*0.19,-7.0+float((i*13)%49)*0.12),Vector3.ONE*0.012,dust_mat,4)
		dust.cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		dust_motes.append(dust)

func configure_lighting(mode: String) -> void:
	lighting_mode=mode
	key_light.position=Vector3(3.8,5.8,2.0) if mode=="shift" else Vector3(-3.8,5.8,-6.0)
	key_light.look_at(Vector3(0,0,-1.0),Vector3.UP)
	key_light.light_color=Color("f4cc92") if mode=="shift" else Color("c5dcf2")
	for light in local_lights:
		light.light_energy=0.0 if mode=="off" else float(light.get_meta("normal_energy"))
	for fm in fire_materials: fm.set_shader_parameter("activity",0.0 if mode=="off" else 1.0)

func _process(delta: float) -> void:
	clock+=delta
	for i in range(dust_motes.size()):
		dust_motes[i].position.y+=delta*0.018
		if dust_motes[i].position.y>2.2: dust_motes[i].position.y=0.5
	if lighting_mode!="off":
		for i in range(local_lights.size()):
			var light := local_lights[i]
			light.light_energy=float(light.get_meta("normal_energy"))*(1.0+sin(clock*3.2+i*1.4)*0.035)

func _refine() -> void:
	# Woven ornament follows each rug's own UVs, independent of camera or light.
	for mat_key in ["rug", "upholstery"]:
		materials[mat_key].set_shader_parameter("pigment", Color("294b40"))
	var water := ShaderMaterial.new()
	water.shader = preload("res://scenes/ArtStyleTrials/E/water.gdshader")
	_replace_water(self, water)
	var green := material("curtains", "284c40", 3)
	var gold := material("aged_trim", "998253", 0, 0.48, 0.45)
	for gx in [3,13]:
		var center := point(gx,1.0,1.7)
		for side in [-1,1]:
			for fold in range(4):
				cylinder(self,center+Vector3(side*0.47+float(fold)*0.025,0,0.10),0.045,1.35,green)
			box(self,center+Vector3(side*0.50,-0.31,0.15),Vector3(0.15,0.055,0.06),gold)
		box(self,center+Vector3(0,0.72,0.10),Vector3(1.27,0.06,0.10),gold)
	# Repeated joinery is real geometry; no painted shadows on these faces.
	for side in [-1,1]:
		for row in range(19):
			var z := -9.0+float(row)*0.75
			if row in [3,4,8,9,14,15]: continue
			box(self,Vector3(side*3.50,0.28+(0.22 if z < -2.25 else 0.0),z),Vector3(0.025,0.035,0.55),gold)

func _replace_water(node: Node, water: Material) -> void:
	if node is MeshInstance3D and node.material_override == materials.get("water"):
		node.material_override = water
	for child in node.get_children(): _replace_water(child, water)
