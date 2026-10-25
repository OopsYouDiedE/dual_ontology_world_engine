extends "res://scenes/ArtStyleTrials/E/HallCraft.gd"
## E art rehearsal. Room selection never performs a world action.
const KIT = preload("res://scenes/ArtStyleTrials/E/RoomGrid.gd")
const FIRE = preload("res://scenes/ArtStyleTrials/E/fire.gdshader")
var rooms: Dictionary = {}
var room_sizes: Dictionary = {}
var room_origins: Dictionary = {}
var camera: Camera3D
var key_light: DirectionalLight3D
var local_lights: Array[OmniLight3D] = []
var fire_materials: Array[ShaderMaterial] = []
var active_room := "hall"
var lighting_mode := "normal"
var clock := 0.0
var library: MeshLibrary
var current_room_id := "hall"
var panel: CanvasLayer
var label: Label
var texture_cache: Dictionary = {}
var door_models_enabled := false

func _painted_texture(source: Texture2D) -> Texture2D:
	if texture_cache.has(source.resource_path): return texture_cache[source.resource_path]
	var pixels := source.get_image()
	pixels.generate_mipmaps()
	var texture := ImageTexture.create_from_image(pixels)
	texture_cache[source.resource_path]=texture
	return texture

func _ready() -> void:
	_environment()
	var floor_mat := ShaderMaterial.new()
	floor_mat.shader=preload("res://scenes/ArtStyleTrials/E/parquet.gdshader")
	var wallpaper := StandardMaterial3D.new()
	wallpaper.albedo_texture=_painted_texture(preload("res://ShareAsset/Internal/ArtStyleTrials/E/wallpaper.png"))
	wallpaper.uv1_triplanar=true
	wallpaper.uv1_world_triplanar=true
	wallpaper.uv1_scale=Vector3(0.65,0.65,0.65)
	wallpaper.roughness=0.94
	wallpaper.texture_filter=BaseMaterial3D.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS_ANISOTROPIC
	library=KIT.library(floor_mat,wallpaper,material("walnut","493227",1),material("moulding","735334",1),3.8)
	_build_hall()
	_build_bath()
	# Four wings are separate room records, placed against the hall boundary.
	# These are reception/bedroom art specimens, not replacements for the sixteen logical rooms.
	for spec in [["jia",Vector3(-8,0,-4)],["ding",Vector3(8,0,-4)],["bing",Vector3(-8,0,4)],["yi",Vector3(8,0,4)]]:
		_build_wing(spec[0],spec[1])
	_ui()
	show_room("hall")
	set_meta("render_metadata", {"route":"E / single-room GridMap salon", "target_size":Vector2i(1600,1600), "construction":"MeshLibrary floor and 3.8-high panelled wall GridMaps, complete per-room boundaries; west/east wing walls share hall x=-5/+5, bath shares hall z=7", "reference":"A layout hierarchy and ornament; D real geometry and three-head cast", "surfaces":"continuous world-space herringbone parquet, authored raster botanical carpet, damask wall, modelled mouldings, tufted upholstery and turned joinery", "limits":"Isolated art specimens, no Core or traversal. Four wing specimens do not replace all sixteen production bedrooms. South walls are camera cutaways; high walls retained on other sides. No full character animation."})

func _environment() -> void:
	key_light=$Daylight
	camera=$Camera3D
	get_viewport().msaa_3d=Viewport.MSAA_4X

func set_surface_effects(enabled: bool) -> void:
	var env: Environment=$WorldEnvironment.environment
	env.ssao_enabled=enabled
	env.ssil_enabled=enabled
	env.glow_enabled=enabled

func render_settings_snapshot() -> Dictionary:
	var env: Environment=$WorldEnvironment.environment
	return {"renderer":RenderingServer.get_current_rendering_method(),"ssao":env.ssao_enabled,"ssao_radius":env.ssao_radius,"ssao_intensity":env.ssao_intensity,"ssil":env.ssil_enabled,"ssil_intensity":env.ssil_intensity,"glow":env.glow_enabled,"tonemap":env.tonemap_mode,"ambient_energy":env.ambient_light_energy,"camera_projection":camera.projection,"camera_size":camera.size,"near":camera.near,"far":camera.far,"camera_transform":camera.global_transform,"msaa":get_viewport().msaa_3d}

func _room(id: String, origin: Vector3, size: Vector2i, openings: Dictionary) -> void:
	current_room_id=id
	content_root=Node3D.new()
	content_root.name=id.capitalize()+"Room"
	content_root.position=origin
	add_child(content_root)
	rooms[id]=content_root
	room_sizes[id]=size
	room_origins[id]=origin
	KIT.make(content_root,library,size,openings)
	box(content_root,Vector3(0,-0.19,0),Vector3(size.x+0.36,0.28,size.y+0.36),material("foundation","343d3e",2))
	# Continuous perimeter inlay is independent of the herringbone field.
	for side in [-1,1]:
		for offset in [0.16,0.25]:
			box(content_root,Vector3(side*(size.x*0.5-offset),0.012,0),Vector3(0.028,0.008,size.y-0.22),material("inlay","b49c70",1))
			box(content_root,Vector3(0,0.012,side*(size.y*0.5-offset)),Vector3(size.x-0.22,0.008,0.028),material("inlay","b49c70",1))

func _build_hall() -> void:
	_room("hall",Vector3.ZERO,Vector2i(10,14),{"north":[1,2,4,5,7,8],"west":[2,3,10,11],"east":[2,3,10,11],"south":[4,5]})
	_portal(Vector3(0,0,-7),0,"PUBLIC SALON",true)
	_window(Vector3(-3,0,-7))
	_window(Vector3(3,0,-7))
	for spec in [[-5.0,-4.0,"JIA"],[-5.0,4.0,"BING"],[5.0,-4.0,"DING"],[5.0,4.0,"YI"]]:
		_portal(Vector3(spec[0],0,spec[1]),PI/2 if spec[0]<0 else -PI/2,spec[2],true)
	_portal(Vector3(0,0,7),0,"BATH",false)
	# A's hierarchy: fireplace reading nook, central gathering, piano and games.
	_fireplace(Vector3(-2.0,0,-5.8))
	_bookcase(Vector3(-4.05,0,-5.85))
	_bookcase(Vector3(4.05,0,-5.85))
	_notice(Vector3(1.65,0,-6.65))
	_rug(Vector3(-1.8,0.025,-3.4),Vector2(3.8,2.53))
	_armchair(Vector3(-2.9,0,-3.7),0.13)
	_armchair(Vector3(-0.8,0,-3.7),-0.13)
	_round_table(Vector3(-1.85,0,-3.25))
	_lamp(Vector3(1.2,0,-3.6))
	_coatstand(Vector3(4.0,0,-1.7))
	_bench(Vector3(2.65,0,-0.9),2.05)
	# The shallow two-step dais belongs to the hall, not a second visible room.
	for i in range(2):
		box(content_root,Vector3(0,0.045+0.045*i,-0.15-0.18*i),Vector3(9.4,0.09,0.18),material("step","63472e",1))
	_rug(Vector3(0,0.026,3.2),Vector2(2.8,4.2))
	_table(Vector3(0,0,2.25),Vector2(3.7,1.1),0.90)
	_bench(Vector3(0,0,1.3),3.4)
	_bench(Vector3(0,0,3.3),3.4)
	_piano(Vector3(3.65,0,3.3))
	_chess(Vector3(-3.5,0,4.3))
	_lamp(Vector3(-4.05,0,1.25))
	for at in [Vector3(-4.1,0,-0.7),Vector3(4.1,0,5.75),Vector3(4.0,0,-3.8)]: _plant(at)
	for at in [Vector3(-4.82,2.4,0.6),Vector3(4.82,2.4,0.6)]: _sconce(at,PI/2 if at.x<0 else -PI/2)
	_character("Detective",-0.65,4.35,"detective",1.0)
	_character("Engineer",-1.25,0.65,"engineer",1.03)
	_character("Chef",1.2,0.70,"chef",1.02)
	_character("Musician",2.6,3.8,"musician",0.98)
	_character("Thief",1.25,4.5,"thief",0.95)

func _portal(at: Vector3, angle: float, title: String, full: bool) -> void:
	# Door art is postponed. Later, observer-owned current-room models bind here.
	if not door_models_enabled: return
	var root := group("Door_"+title,at)
	root.rotation.y=angle
	var wood := material("door","493627",1)
	var trim := material("door_trim","80613c",1)
	var gold := material("brass","a78d58",0,0.44,0.55)
	var h := 2.65 if full else 0.3
	for x in [-0.96,0.96]:
		box(root,Vector3(x,h*0.5,0),Vector3(0.16,h,0.34),wood)
		box(root,Vector3(x,h*0.5,0.19),Vector3(0.045,h,0.035),trim)
		box(root,Vector3(x,0.13,0),Vector3(0.24,0.26,0.40),trim)
	if not full: return
	for y in [2.65,2.76]: box(root,Vector3(0,y,0),Vector3(2.25,0.10,0.42),trim)
	box(root,Vector3(0,3.30,0),Vector3(2,1.0,0.22),material("plaster","c6bda5",2))
	for x in [-0.44,0.44]:
		box(root,Vector3(x,1.26,0),Vector3(0.84,2.52,0.12),wood)
		for y in [0.5,1.5,2.21]:
			var height := 0.43 if y>2 else 0.64
			box(root,Vector3(x,y,0.076),Vector3(0.65,height,0.03),trim)
			box(root,Vector3(x,y,0.096),Vector3(0.58,height-0.08,0.025),wood)
		cylinder(root,Vector3(x*0.24,1.17,0.14),0.035,0.20,gold)
	var plaque := Label3D.new()
	plaque.text=title
	plaque.font_size=32
	plaque.pixel_size=0.006
	plaque.position=Vector3(0,2.98,0.17)
	plaque.modulate=Color("bca36d")
	plaque.outline_size=0
	root.add_child(plaque)

func _window(at: Vector3) -> void:
	var root := group("TallSashWindow",at)
	var wood := material("walnut","493227",1)
	var trim := material("window_trim","9b8053",1)
	box(root,Vector3(0,0.48,0),Vector3(2,0.96,0.22),wood)
	box(root,Vector3(0,3.52,0),Vector3(2,0.56,0.22),material("plaster","c6bda5",2))
	var glass := StandardMaterial3D.new()
	glass.albedo_color=Color("748e91")
	glass.roughness=0.25
	glass.emission_enabled=true
	glass.emission=Color("536c70")
	glass.emission_energy_multiplier=0.25
	box(root,Vector3(0,2.14,-0.025),Vector3(1.74,2.34,0.05),glass)
	for x in [-0.92,-0.45,0.0,0.45,0.92]: box(root,Vector3(x,2.15,0.06),Vector3(0.045,2.45,0.08),wood)
	for y in [0.94,1.68,2.43,3.37]: box(root,Vector3(0,y,0.07),Vector3(1.90,0.06,0.10),wood)
	box(root,Vector3(0,0.94,0.18),Vector3(2.1,0.09,0.45),trim)
	var cloth := material("curtain","254a3b",3)
	for side in [-1,1]:
		for fold in range(7):
			var x: float = side*(0.73+fold*0.048)
			cylinder(root,Vector3(x,2.02,0.20+sin(fold*2.2)*0.035),0.055,2.55,cloth)
		box(root,Vector3(side*0.90,1.65,0.28),Vector3(0.35,0.055,0.06),trim)
		for tassel in range(4): cylinder(root,Vector3(side*0.9+tassel*0.025,1.53,0.30),0.012,0.20,trim)
	box(root,Vector3(0,3.41,0.16),Vector3(2.2,0.14,0.23),wood)

func _rug(at: Vector3, size: Vector2) -> void:
	var root := group("BotanicalWovenCarpet",at)
	var mesh := MeshInstance3D.new()
	var plane := PlaneMesh.new()
	plane.size=Vector2(size.y,size.x) if size.x>size.y else size
	mesh.rotation.y=PI/2 if size.x>size.y else 0.0
	mesh.mesh=plane
	var mat := StandardMaterial3D.new()
	mat.albedo_texture=_painted_texture(preload("res://ShareAsset/Internal/ArtStyleTrials/E/salon-rug.png"))
	mat.roughness=0.96
	mat.texture_filter=BaseMaterial3D.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS_ANISOTROPIC
	mesh.material_override=mat
	root.add_child(mesh)
	var fringe := material("fringe","a69673",3)
	for side in [-1,1]:
		for i in int(size.x/0.055):
			box(root,Vector3(-size.x/2+i*0.055,0.001,side*(size.y/2+0.04)),Vector3(0.018,0.008,0.10),fringe)

func _turned_leg(root: Node3D, at: Vector3, height: float, mat: Material) -> void:
	for i in range(8):
		var radii := [0.065,0.045,0.080,0.055,0.042,0.060,0.080,0.065]
		cylinder(root,at+Vector3(0,height*(i+0.5)/8.0,0),radii[i],height/8.0,mat)

func _armchair(at: Vector3, angle: float) -> void:
	var root := group("TuftedReadingChair",at)
	root.rotation.y=angle
	var wood := material("walnut","493227",1)
	var green := material("velvet","315345",3)
	var seam := material("velvet_seam","20392f",3)
	for x in [-0.37,0.37]:
		for z in [-0.31,0.31]: _turned_leg(root,Vector3(x,0,z),0.47,wood)
	box(root,Vector3(0,0.46,0),Vector3(0.90,0.10,0.85),wood)
	ellipsoid(root,Vector3(0,0.58,0),Vector3(0.90,0.22,0.82),green)
	ellipsoid(root,Vector3(0,1.02,-0.33),Vector3(0.96,1.12,0.21),wood)
	ellipsoid(root,Vector3(0,1.02,-0.22),Vector3(0.84,0.97,0.23),green)
	for row in range(4):
		for col in range(3):
			var x := (col-1)*0.24+(0.07 if row%2 else 0.0)
			ellipsoid(root,Vector3(x,0.75+row*0.20,-0.08),Vector3(0.038,0.038,0.017),seam)
	for side in [-1,1]:
		ellipsoid(root,Vector3(side*0.46,0.80,0.02),Vector3(0.19,0.22,0.91),green)
		limb(root,Vector3(side*0.46,0.47,0.29),Vector3(side*0.46,0.79,0.29),0.043,wood)

func _table(at: Vector3, size: Vector2, height: float) -> Node3D:
	var root := group("CarvedCommunalTable",at)
	var wood := material("table","63452e",1,0.54)
	var edge := material("table_edge","a18552",1,0.52)
	for x in [-1,1]:
		for z in [-1,1]: _turned_leg(root,Vector3(x*(size.x/2-0.18),0,z*(size.y/2-0.17)),height-0.08,wood)
	box(root,Vector3(0,height-0.17,0),Vector3(size.x-0.10,0.20,size.y-0.11),wood)
	box(root,Vector3(0,height-0.015,0),Vector3(size.x+0.045,0.07,size.y+0.045),edge)
	box(root,Vector3(0,height+0.025,0),Vector3(size.x,0.045,size.y),wood)
	var runner := MeshInstance3D.new()
	var plane := PlaneMesh.new()
	plane.size=Vector2(size.y*0.43,size.x-0.30)
	runner.rotation.y=PI/2
	runner.mesh=plane
	runner.position.y=height+0.052
	var mat := StandardMaterial3D.new()
	mat.albedo_texture=_painted_texture(preload("res://ShareAsset/Internal/ArtStyleTrials/E/salon-rug.png"))
	mat.roughness=0.94
	mat.texture_filter=BaseMaterial3D.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS_ANISOTROPIC
	var strip: float=(size.y*0.43)/(size.x-0.30)*1.5
	mat.uv1_scale=Vector3(strip,1,1)
	mat.uv1_offset=Vector3((1.0-strip)*0.5,0,0)
	runner.material_override=mat
	root.add_child(runner)
	for side in [-1,1]: _candlestick(root,Vector3(side*size.x*0.30,height+0.055,0))
	var book := box(root,Vector3(size.x*0.14,height+0.08,0.1),Vector3(0.25,0.055,0.32),material("book_green","334d3b",3))
	book.rotation.y=0.22
	cylinder(root,Vector3(0,height+0.20,0),0.11,0.30,material("vase","b5ad89",2),0.075)
	for i in range(7):
		var tip := Vector3(sin(i*2.4)*0.20,height+0.49+0.07*sin(i),cos(i*2.4)*0.15)
		limb(root,Vector3(0,height+0.30,0),tip,0.009,material("stem","4c5e3b",3))
		ellipsoid(root,tip,Vector3(0.13,0.09,0.13),material("flower","cec3a1",3))
	return root

func _candlestick(root: Node3D, at: Vector3) -> void:
	var metal := material("brass","a78d58",0,0.44,0.55)
	cylinder(root,at+Vector3(0,0.025,0),0.10,0.05,metal)
	cylinder(root,at+Vector3(0,0.12,0),0.025,0.20,metal)
	cylinder(root,at+Vector3(0,0.24,0),0.065,0.035,metal)
	cylinder(root,at+Vector3(0,0.35,0),0.028,0.21,material("wax","d9ceab",3))

func _round_table(at: Vector3) -> void:
	var root := group("TeaTable",at)
	var wood := material("table","63452e",1)
	cylinder(root,Vector3(0,0.68,0),0.40,0.07,wood)
	_turned_leg(root,Vector3.ZERO,0.65,wood)
	for i in range(3):
		var a := i*TAU/3
		limb(root,Vector3(0,0.15,0),Vector3(cos(a)*0.31,0.055,sin(a)*0.31),0.04,wood)
	for x in [-0.16,0.16]:
		cylinder(root,Vector3(x,0.727,0),0.09,0.015,material("porcelain","cfc5a7",2,0.36))
		cylinder(root,Vector3(x,0.77,0),0.05,0.08,material("porcelain","cfc5a7",2,0.36),0.055)

func _bench(at: Vector3, width: float) -> void:
	var root := group("CarvedBench",at)
	var wood := material("walnut","493227",1)
	box(root,Vector3(0,0.48,0),Vector3(width,0.10,0.43),wood)
	for x in [-width*0.40,width*0.40]:
		for z in [-0.13,0.13]: _turned_leg(root,Vector3(x,0,z),0.45,wood)
	box(root,Vector3(0,0.20,0),Vector3(width-0.2,0.07,0.07),wood)
	for z in [-0.22,0.22]: box(root,Vector3(0,0.51,z),Vector3(width,0.025,0.02),material("edge","95764b",1))

func _fireplace(at: Vector3) -> void:
	var root := group("CarvedStoneFireplace",at)
	var stone := material("marble","586259",2,0.52)
	var pale := material("stone_trim","8e927b",2,0.50)
	box(root,Vector3(0,0.085,0.12),Vector3(2.25,0.17,0.95),stone)
	box(root,Vector3(0,0.82,-0.24),Vector3(2.0,1.48,0.45),stone)
	box(root,Vector3(0,0.65,0.01),Vector3(1.28,1.02,0.055),material("hearth","272c2c",2))
	for side in [-1,1]:
		box(root,Vector3(side*0.84,0.76,0.03),Vector3(0.22,1.20,0.40),pale)
		for y in [0.22,1.29]: box(root,Vector3(side*0.84,y,0.06),Vector3(0.34,0.12,0.46),stone)
	for y in [1.42,1.52]: box(root,Vector3(0,y,0.0),Vector3(2.30,0.08,0.70),pale)
	for i in range(9):
		var x := (i-4)*0.13
		cylinder(root,Vector3(x,0.43,0.29),0.018,0.40,material("brass","a78d58",0,0.44,0.55))
	box(root,Vector3(0,0.58,0.29),Vector3(1.22,0.025,0.025),material("brass","a78d58",0,0.44,0.55))
	var flame := MeshInstance3D.new()
	var quad := QuadMesh.new()
	quad.size=Vector2(1.05,0.78)
	flame.mesh=quad
	flame.position=Vector3(0,0.62,0.17)
	var fm := ShaderMaterial.new()
	fm.shader=FIRE
	flame.material_override=fm
	root.add_child(flame)
	fire_materials.append(fm)
	_add_light(at+Vector3(0,0.85,0.6),0.55,3.3)
	# Mantel clock and paired ornaments provide visible silhouette detail.
	var clock_root := Node3D.new()
	root.add_child(clock_root)
	box(clock_root,Vector3(0,1.80,-0.02),Vector3(0.42,0.49,0.20),material("clockwood","684a31",1))
	var dial := cylinder(clock_root,Vector3(0,1.84,0.10),0.15,0.018,material("ivory","d4c6a3",0))
	dial.rotation.x=PI/2
	limb(clock_root,Vector3(0,1.84,0.12),Vector3(0.06,1.91,0.12),0.008,material("ink","343932",0))
	limb(clock_root,Vector3(0,1.84,0.12),Vector3(-0.08,1.85,0.12),0.008,material("ink","343932",0))
	for x in [-0.73,0.73]: _candlestick(root,Vector3(x,1.57,0))

func _bookcase(at: Vector3) -> void:
	var root := group("LibraryCabinet",at)
	var wood := material("walnut","493227",1)
	box(root,Vector3(0,1.30,-0.15),Vector3(1.35,2.6,0.18),wood)
	for side in [-1,1]: box(root,Vector3(side*0.66,1.30,0.06),Vector3(0.10,2.6,0.55),wood)
	for y in [0.08,2.60,2.69]: box(root,Vector3(0,y,0.02),Vector3(1.55,0.09,0.60),material("moulding","735334",1))
	for row in range(6):
		var y := 0.14+row*0.40
		box(root,Vector3(0,y,0.08),Vector3(1.32,0.045,0.54),wood)
		for i in range(12):
			var h := 0.25+float((i*3+row)%4)*0.025
			var x := -0.55+i*0.10
			var mat := material("spine"+str((i+row)%5),["486456","754b39","a18a61","414d54","665c45"][(i+row)%5],3)
			var book := box(root,Vector3(x,y+h*0.5+0.03,0.14),Vector3(0.078,h,0.30),mat)
			book.rotation.z=0.06 if i==10 else 0
			for offset in [0.07,h-0.035]: box(root,Vector3(x,y+offset,0.296),Vector3(0.060,0.008,0.008),material("spinegold","ad9867",0,0.65))

func _notice(at: Vector3) -> void:
	var root := group("NoticeBoard",at)
	box(root,Vector3(0,1.78,0),Vector3(1.45,1.35,0.12),material("moulding","735334",1))
	box(root,Vector3(0,1.78,0.075),Vector3(1.26,1.17,0.025),material("felt","2c493a",3))
	for i in range(4):
		var x := -0.42+(i%2)*0.52
		var y := 1.55+floori(i/2.0)*0.48
		box(root,Vector3(x,y,0.10),Vector3(0.34,0.39,0.01),material("paper","ccc09b",3))
		for line in range(5): box(root,Vector3(x,y-0.12+line*0.05,0.108),Vector3(0.23-0.02*(line%2),0.007,0.003),material("ink","343932",0))

func _lamp(at: Vector3) -> void:
	var root := group("PleatedBrassLamp",at)
	var gold := material("brass","a78d58",0,0.44,0.55)
	cylinder(root,Vector3(0,0.035,0),0.25,0.07,gold)
	cylinder(root,Vector3(0,0.12,0),0.16,0.13,gold,0.07)
	cylinder(root,Vector3(0,0.85,0),0.025,1.45,gold)
	var silk := material("lamp_silk","d9cba5",3)
	cylinder(root,Vector3(0,1.65,0),0.34,0.44,silk,0.14)
	for i in range(32):
		var a := i*TAU/32
		limb(root,Vector3(cos(a)*0.34,1.43,sin(a)*0.34),Vector3(cos(a)*0.14,1.87,sin(a)*0.14),0.009,material("pleat","b9aa80",3))
	_add_light(at+Vector3(0,1.43,0),0.48,3.7)

func _sconce(at: Vector3, angle: float) -> void:
	var root := group("WallSconce",at)
	root.rotation.y=angle
	var brass := material("brass","a78d58",0,0.44,0.55)
	ellipsoid(root,Vector3.ZERO,Vector3(0.16,0.27,0.06),brass)
	limb(root,Vector3.ZERO,Vector3(0,-0.10,0.22),0.025,brass)
	cylinder(root,Vector3(0,0.06,0.22),0.10,0.24,material("opal","d7cba5",2,0.4),0.07)
	_add_light(at,0.22,2.5)

func _coatstand(at: Vector3) -> void:
	var root := group("BentwoodCoatStand",at)
	var wood := material("walnut","493227",1)
	cylinder(root,Vector3(0,0.85,0),0.035,1.70,wood)
	for i in range(4):
		var a := i*TAU/4
		limb(root,Vector3(0,0.18,0),Vector3(cos(a)*0.30,0.03,sin(a)*0.30),0.035,wood)
		limb(root,Vector3(0,1.35,0),Vector3(cos(a)*0.22,1.60,sin(a)*0.22),0.026,wood)
		ellipsoid(root,Vector3(cos(a)*0.22,1.60,sin(a)*0.22),Vector3.ONE*0.07,wood)

func _piano(at: Vector3) -> void:
	var root := group("UprightPiano",at)
	var wood := material("piano","3c342b",1,0.46)
	var edge := material("piano_trim","9d8352",1)
	box(root,Vector3(0,0.78,-0.10),Vector3(1.65,1.55,0.45),wood)
	box(root,Vector3(0,0.82,0.23),Vector3(1.76,0.09,0.48),wood)
	for x in [-0.71,0.71]:
		_turned_leg(root,Vector3(x,0,0.35),0.78,wood)
		box(root,Vector3(x,1.18,0.15),Vector3(0.05,0.50,0.04),edge)
	for i in range(30):
		var x := -0.73+i*0.05
		box(root,Vector3(x,0.875,0.34),Vector3(0.046,0.022,0.25),material("ivory","d4c6a3",0))
		if i%7 not in [2,6]: box(root,Vector3(x+0.024,0.901,0.27),Vector3(0.026,0.035,0.14),material("ink","343932",0))
	for y in [1.04,1.46,1.59]: box(root,Vector3(0,y,0.13),Vector3(1.72,0.028,0.06),edge)
	box(root,Vector3(0,1.20,0.20),Vector3(0.55,0.30,0.035),material("paper","ccc09b",3))
	for row in range(5): box(root,Vector3(0,1.10+row*0.04,0.223),Vector3(0.45,0.005,0.003),material("ink","343932",0))
	_bench(at+Vector3(0,0,0.90),0.75)

func _chess(at: Vector3) -> void:
	var root := group("ChessTable",at)
	var wood := material("table","63452e",1)
	box(root,Vector3(0,0.72,0),Vector3(0.92,0.08,0.92),wood)
	for x in [-0.33,0.33]:
		for z in [-0.33,0.33]: _turned_leg(root,Vector3(x,0,z),0.68,wood)
	for z in range(8):
		for x in range(8): box(root,Vector3((x-3.5)*0.085,0.767,(z-3.5)*0.085),Vector3(0.085,0.01,0.085),material("chess"+str((x+z)%2),"c6b58c" if (x+z)%2 else "34483a",0))
	for i in range(8):
		for side in [-1,1]:
			var mat := material("pieces"+str(side),"c5b17d" if side>0 else "3d4033",0,0.46)
			cylinder(root,Vector3((i-3.5)*0.085,0.81,side*0.255),0.024,0.08,mat,0.014)
			ellipsoid(root,Vector3((i-3.5)*0.085,0.86,side*0.255),Vector3.ONE*0.042,mat)
	_bench(at+Vector3(-0.85,0,0),0.60)

func _plant(at: Vector3) -> void:
	var root := group("IndoorFern",at)
	var pot := material("pot","65716b",2,0.45)
	cylinder(root,Vector3(0,0.25,0),0.21,0.47,pot,0.30)
	cylinder(root,Vector3(0,0.49,0),0.31,0.06,pot)
	for i in range(11):
		var a := i*TAU/11
		var direction := Vector3(cos(a),0,sin(a))
		var green := material("leaf"+str(i%3),["3e5c3b","4c6941","5a7049"][i%3],3)
		var tip := direction*0.50+Vector3(0,0.85+0.12*sin(i),0)
		limb(root,Vector3(0,0.50,0),tip,0.012,green)
		for j in range(5):
			var mid := Vector3(0,0.5,0).lerp(tip,(j+1)/6.0)
			for side in [-1,1]:
				var leaf := ellipsoid(root,mid+Vector3(-sin(a),0,cos(a))*side*0.065,Vector3(0.075,0.025,0.25),green)
				leaf.rotation.y=-a+side*0.8

func _build_bath() -> void:
	_room("bath",Vector3(0,0,9),Vector2i(10,4),{"north":[4,5]})
	_portal(Vector3(0,0,-2),0,"PUBLIC SALON",true)
	var tile := material("tile","b9c1af",2,0.37)
	for z in range(8):
		for x in range(20):
			box(content_root,Vector3(-4.75+x*0.5,0.018,-1.75+z*0.5),Vector3(0.485,0.02,0.485),tile)
			var dot := box(content_root,Vector3(-4.5+x*0.5,0.03,-1.5+z*0.5),Vector3(0.10,0.012,0.10),material("bathgreen","35594d",2,0.42))
			dot.rotation.y=PI/4
	var ceramic := material("porcelain","cfc5a7",2,0.36)
	var basin := group("WashBasins",Vector3(-2.8,0,-0.9))
	box(basin,Vector3(0,0.80,0),Vector3(2.5,0.13,0.64),ceramic)
	for x in [-0.80,0,0.80]:
		ellipsoid(basin,Vector3(x,0.87,0.06),Vector3(0.56,0.13,0.46),material("basin","a0b1a2",2,0.28))
		limb(basin,Vector3(x,0.87,-0.20),Vector3(x,1.13,-0.20),0.022,material("brass","a78d58",0,0.44,0.55))
	for x in [-1.0,1.0]: _turned_leg(basin,Vector3(x,0,0),0.76,ceramic)
	box(basin,Vector3(0,1.80,-0.37),Vector3(2.4,0.85,0.08),material("moulding","735334",1))
	box(basin,Vector3(0,1.80,-0.32),Vector3(2.25,0.70,0.015),material("mirror","7b9391",0,0.18,0.65))
	var tub := group("RollTopBath",Vector3(2.45,0,0.1))
	_tub_shell(tub,ceramic)
	var water := cylinder(tub,Vector3(0,0.60,0),0.80,0.012,material("water","4d817d",0,0.22))
	water.scale=Vector3(1.05,1,0.48)
	var water_shader := ShaderMaterial.new()
	water_shader.shader=preload("res://scenes/ArtStyleTrials/E/water.gdshader")
	water.material_override=water_shader
	limb(tub,Vector3(0.55,0.45,-0.40),Vector3(0.55,1.02,-0.40),0.025,material("brass","a78d58",0,0.44,0.55))
	limb(tub,Vector3(0.55,1.02,-0.40),Vector3(0.55,1.02,-0.18),0.025,material("brass","a78d58",0,0.44,0.55))
	for x in [-0.70,0.70]:
		for z in [-0.30,0.30]: ellipsoid(tub,Vector3(x,0.13,z),Vector3(0.16,0.25,0.18),material("brass","a78d58",0,0.44,0.55))
	_lamp(Vector3(4.2,0,-1.2))

func _build_wing(id: String, origin: Vector3) -> void:
	_room(id,origin,Vector2i(6,8),{"north":[2,3],"east":[3,4]} if origin.x<0 else {"north":[2,3],"west":[3,4]})
	_window(Vector3(0,0,-4))
	_portal(Vector3(3 if origin.x<0 else -3,0,0),-PI/2 if origin.x<0 else PI/2,id.to_upper(),true)
	var bed := group("PrivateBed",Vector3(-1.65,0,-0.25))
	var wood := material("walnut","493227",1)
	for x in [-0.63,0.63]:
		for z in [-1.08,1.08]:
			cylinder(bed,Vector3(x,0.56,z),0.055,1.1,wood)
			ellipsoid(bed,Vector3(x,1.14,z),Vector3.ONE*0.14,wood)
	box(bed,Vector3(0,0.45,0),Vector3(1.35,0.17,2.25),wood)
	ellipsoid(bed,Vector3(0,0.63,0),Vector3(1.30,0.30,2.18),material("linen","c9bf9f",3))
	box(bed,Vector3(0,0.80,0.30),Vector3(1.29,0.045,1.45),material("bedcover","4a6452",3))
	ellipsoid(bed,Vector3(0,0.85,-0.76),Vector3(0.96,0.21,0.40),material("linen","c9bf9f",3))
	box(bed,Vector3(0,0.94,-1.10),Vector3(1.34,0.63,0.12),wood)
	_rug(Vector3(0.55,0.025,1),Vector2(1.65,2.475))
	_table(Vector3(1.6,0,-1.7),Vector2(1.8,0.65),0.85)
	_bench(Vector3(1.6,0,-0.9),0.7)
	_bookcase(Vector3(1.95,0,2.3))
	_lamp(Vector3(-0.35,0,-2.45))

func _add_light(at: Vector3, energy: float, radius: float) -> void:
	var light := OmniLight3D.new()
	light.position=at
	light.light_color=Color("f2d091")
	light.light_energy=energy
	light.omni_range=radius
	light.shadow_enabled=true
	light.light_size=0.12
	light.set_meta("normal_energy",energy)
	light.set_meta("room",current_room_id)
	content_root.add_child(light)
	local_lights.append(light)

func show_room(id: String) -> bool:
	if not rooms.has(id): return false
	active_room=id
	for room_id in rooms:
		rooms[room_id].visible=room_id==id
		rooms[room_id].process_mode=Node.PROCESS_MODE_INHERIT if room_id==id else Node.PROCESS_MODE_DISABLED
	var origin: Vector3=room_origins[id]
	var size: Vector2i=room_sizes[id]
	camera.size=18.2 if id=="hall" else 12.8
	camera.position=origin+Vector3(0,22,25)
	camera.look_at(origin+Vector3(0,0.8,-0.65),Vector3.UP)
	configure_lighting(lighting_mode)
	if label: label.text={"hall":"公共大厅", "bath":"独立盥洗室", "jia":"甲翼 · 寝室样片", "ding":"丁翼 · 寝室样片", "bing":"丙翼 · 寝室样片", "yi":"乙翼 · 寝室样片"}[id]
	return true

func room_visibility_snapshot() -> Dictionary:
	var shown: Array=[]
	var illuminated: Array=[]
	for id in rooms:
		if rooms[id].is_visible_in_tree(): shown.append(id)
	for light in local_lights:
		if light.is_visible_in_tree() and light.light_energy>0: illuminated.append(light.get_meta("room"))
	return {"active":active_room,"visible_rooms":shown,"lit_rooms":illuminated,"hall_west":-5.0,"jia_east":room_origins["jia"].x+3,"hall_east":5.0,"ding_west":room_origins["ding"].x-3,"bing_east":room_origins["bing"].x+3,"yi_west":room_origins["yi"].x-3,"hall_south":7.0,"bath_north":room_origins["bath"].z-2}

func configure_lighting(mode: String) -> void:
	lighting_mode=mode
	key_light.rotation_degrees=Vector3(-60,35,0) if mode=="shift" else Vector3(-68,-28,0)
	key_light.light_color=Color("eacda3") if mode=="shift" else Color("e6ddc6")
	for light in local_lights:
		light.visible=light.get_meta("room")==active_room
		light.light_energy=0 if mode=="off" else float(light.get_meta("normal_energy"))
	for fm in fire_materials: fm.set_shader_parameter("activity",0.0 if mode=="off" else 1.0)

func _ui() -> void:
	panel=CanvasLayer.new()
	add_child(panel)
	var row := HBoxContainer.new()
	row.position=Vector2(24,20)
	panel.add_child(row)
	for pair in [["hall","公共大厅"],["jia","甲翼"],["yi","乙翼"],["bing","丙翼"],["ding","丁翼"],["bath","盥洗室"]]:
		var button := Button.new()
		button.text=pair[1]
		button.pressed.connect(show_room.bind(pair[0]))
		row.add_child(button)
	label=Label.new()
	label.position=Vector2(24,64)
	panel.add_child(label)
	# Capture uses a SubViewport and deliberately excludes preview controls.
	panel.visible=get_viewport()==get_tree().root

func _process(delta: float) -> void:
	clock+=delta
	if lighting_mode=="off": return
	for light in local_lights:
		if light.visible: light.light_energy=float(light.get_meta("normal_energy"))*(1.0+sin(clock*2.8)*0.025)

func _tub_shell(parent: Node3D, ceramic: Material) -> void:
	var profile := [Vector2(0.05,0.24),Vector2(0.70,0.24),Vector2(0.95,0.42),Vector2(1.0,0.73),Vector2(0.98,0.78),Vector2(0.88,0.78),Vector2(0.83,0.65),Vector2(0.68,0.34),Vector2(0.05,0.34)]
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	for row in range(profile.size()-1):
		for i in range(64):
			var a := float(i)*TAU/64
			var b := float(i+1)*TAU/64
			var p: Vector2=profile[row]
			var q: Vector2=profile[row+1]
			var corners := [Vector3(cos(a)*p.x*1.12,p.y,sin(a)*p.x*0.57),Vector3(cos(b)*p.x*1.12,p.y,sin(b)*p.x*0.57),Vector3(cos(b)*q.x*1.12,q.y,sin(b)*q.x*0.57),Vector3(cos(a)*q.x*1.12,q.y,sin(a)*q.x*0.57)]
			for index in [0,1,2,0,2,3]: st.add_vertex(corners[index])
	st.generate_normals()
	var mesh := MeshInstance3D.new()
	mesh.mesh=st.commit()
	mesh.material_override=ceramic
	parent.add_child(mesh)

func inspect_detail(id: String) -> void:
	show_room("hall")
	var target := Vector3(-1.5,0.85,-3.8) if id=="reading" else Vector3(0,0.60,2.3)
	camera.size=8.4
	camera.position=target+Vector3(0,22,25)
	camera.look_at(target,Vector3.UP)
