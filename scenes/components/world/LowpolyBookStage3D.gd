extends Node3D
## Reference composition: a real low-poly study and a physical, curved open book.
## The native Control texture is projected onto the same surface used for picking.
const PAPER_SIZE := Vector2(13.8, 7.55)
const PAPER_Y := 0.66
const TAB_COLORS := ["6a413c", "736d4e", "c6b49a", "505c65", "aa825c", "967b7e", "77795d", "70505a"]
var camera: Camera3D
var rig: Node3D
var desk: Node3D
var left_stack: Node3D
var cover_hinge: Node3D
var page_surface: MeshInstance3D
var turn_hinge: Node3D
var turn_surface: MeshInstance3D
var tabs: Array[MeshInstance3D] = []
var stamps: Array[MeshInstance3D] = []
var opened := 0.0
var desktop := true
var pull_amount := 1.0
var title_spread := true
var casebook_spread := false
var turn_lift := 0.0
var _world: WorldEnvironment
var _page_material: ShaderMaterial
var _turn_material: ShaderMaterial
var _aspect := 2.0
var _ui_scale := 1.0
var _zoom := 0.0
var _study: Node3D
var _hall: Node3D
var _candle_light: OmniLight3D
var _flame: Node3D
var _time := 0.0
var _materials: Dictionary = {}

func _ready() -> void:
	add_to_group("book_camera_stages")
	_build()
	apply_pose()

func _process(delta: float) -> void:
	_time += delta
	if is_instance_valid(_candle_light):
		_candle_light.light_energy = 2.35 + 0.045 * sin(_time * 5.3) + 0.025 * sin(_time * 8.7)
	if is_instance_valid(_flame):
		_flame.scale.x = 1.0 + sin(_time * 4.8) * 0.025
		_flame.rotation.z = sin(_time * 3.7) * 0.014

func _material(hex: String, rough := 0.88, metal := 0.0, unlit := false) -> StandardMaterial3D:
	var key := "%s/%s/%s/%s" % [hex, rough, metal, unlit]
	if _materials.has(key): return _materials[key]
	var material := StandardMaterial3D.new()
	material.albedo_color = Color(hex)
	material.roughness = rough
	material.metallic = metal
	if unlit: material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	_materials[key] = material
	return material

func _mesh_node(parent: Node3D, mesh: Mesh, material: Material, at := Vector3.ZERO, label := "") -> MeshInstance3D:
	var node := MeshInstance3D.new()
	if not label.is_empty(): node.name = label
	node.mesh = mesh
	node.material_override = material
	node.position = at
	parent.add_child(node)
	return node

func _box(parent: Node3D, dimensions: Vector3, at: Vector3, material: Material, label := "") -> MeshInstance3D:
	var mesh := BoxMesh.new()
	mesh.size = dimensions
	return _mesh_node(parent, mesh, material, at, label)

func _cylinder(parent: Node3D, radius: float, height: float, at: Vector3, material: Material, segments := 8, top_radius := -1.0) -> MeshInstance3D:
	var mesh := CylinderMesh.new()
	mesh.top_radius = radius if top_radius < 0.0 else top_radius
	mesh.bottom_radius = radius
	mesh.height = height
	mesh.radial_segments = segments
	mesh.rings = 1
	return _mesh_node(parent, mesh, material, at)

func _triangle(st: SurfaceTool, a: Vector3, b: Vector3, c: Vector3) -> void:
	var normal := (b - a).cross(c - a).normalized()
	for vertex in [a, b, c]:
		st.set_normal(normal)
		st.add_vertex(vertex)

func _prism(parent: Node3D, outline: PackedVector2Array, height: float, at: Vector3, material: Material, label := "") -> MeshInstance3D:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var center := Vector2.ZERO
	for point in outline: center += point
	center /= float(outline.size())
	for i in range(outline.size()):
		var p: Vector2 = outline[i]
		var q: Vector2 = outline[(i + 1) % outline.size()]
		var a := Vector3(p.x, height * 0.5, p.y)
		var b := Vector3(q.x, height * 0.5, q.y)
		var low_a := a - Vector3(0, height, 0)
		var low_b := b - Vector3(0, height, 0)
		_triangle(st, Vector3(center.x, height * 0.5, center.y), b, a)
		_triangle(st, Vector3(center.x, -height * 0.5, center.y), low_a, low_b)
		_triangle(st, low_a, a, b)
		_triangle(st, low_a, b, low_b)
	return _mesh_node(parent, st.commit(), material, at, label)

func _beveled(parent: Node3D, width: float, depth: float, height: float, at: Vector3, material: Material, cut := 0.08, label := "") -> MeshInstance3D:
	var x := width * 0.5
	var z := depth * 0.5
	return _prism(parent, PackedVector2Array([
		Vector2(-x + cut, -z), Vector2(x - cut, -z), Vector2(x, -z + cut), Vector2(x, z - cut),
		Vector2(x - cut, z), Vector2(-x + cut, z), Vector2(-x, z - cut), Vector2(-x, -z + cut)
	]), height, at, material, label)

func _beam(parent: Node3D, a: Vector3, b: Vector3, width: float, depth: float, material: Material) -> MeshInstance3D:
	var beam := _box(parent, Vector3(width, a.distance_to(b), depth), (a + b) * 0.5, material)
	beam.quaternion = Quaternion(Vector3.UP, (b - a).normalized())
	return beam

func _build() -> void:
	var env := Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = Color("191d25")
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color("8293af")
	env.ambient_light_energy = 0.40
	env.tonemap_mode = Environment.TONE_MAPPER_LINEAR
	env.ssao_enabled = true
	env.ssao_radius = 0.45
	env.ssao_intensity = 0.85
	_world = WorldEnvironment.new()
	_world.name = "ReferenceAtmosphere"
	_world.environment = env
	add_child(_world)
	camera = Camera3D.new()
	camera.name = "ReferenceCamera"
	camera.projection = Camera3D.PROJECTION_PERSPECTIVE
	camera.fov = 30.0
	camera.near = 0.08
	camera.far = 160.0
	add_child(camera)
	camera.make_current()
	var key := DirectionalLight3D.new()
	key.name = "WarmArchitecturalFill"
	key.rotation_degrees = Vector3(-43, -38, -8)
	key.light_color = Color("edceac")
	key.light_energy = 0.62
	key.shadow_enabled = true
	key.shadow_blur = 1.5
	key.directional_shadow_max_distance = 65.0
	add_child(key)
	var moon := DirectionalLight3D.new()
	moon.name = "WindowBlueFill"
	moon.rotation_degrees = Vector3(-20, 135, 0)
	moon.light_color = Color("7795c7")
	moon.light_energy = 0.24
	add_child(moon)
	_study = Node3D.new()
	_study.name = "ProceduralStudy"
	add_child(_study)
	_build_study()
	_hall = Node3D.new()
	_hall.name = "ProceduralCloister"
	add_child(_hall)
	_build_hall()
	_build_book()

func _build_book() -> void:
	rig = Node3D.new()
	rig.name = "PhysicalBook"
	add_child(rig)
	var cloth := _material("303b32")
	var bevel := _material("253129")
	_beveled(rig, 7.25, 8.00, 0.15, Vector3(3.55, 0.04, 0), cloth, 0.10, "RightBoard")
	_beveled(rig, 7.30, 8.04, 0.065, Vector3(3.55, -0.065, 0), bevel, 0.10, "RightBoardLip")
	left_stack = Node3D.new()
	left_stack.name = "LeftLeaves"
	rig.add_child(left_stack)
	_beveled(left_stack, 7.25, 8.00, 0.15, Vector3(-3.55, 0.04, 0), cloth, 0.10, "LeftBoard")
	_beveled(left_stack, 7.30, 8.04, 0.065, Vector3(-3.55, -0.065, 0), bevel, 0.10, "LeftBoardLip")
	# Eighteen individually modelled, curved leaf edges on each side.
	for index in range(18):
		var offset := -0.027 - float(17 - index) * 0.025
		var paper_color := Color("9f856a").lerp(Color("d9c4aa"), float(index) / 17.0)
		var material := _material(paper_color.to_html(false), 0.98)
		_mesh_node(rig, _paper_mesh(0.501, 1.0, offset), material, Vector3.ZERO, "RightPaper_%02d" % index)
		_mesh_node(left_stack, _paper_mesh(0.0, 0.499, offset), material, Vector3.ZERO, "LeftPaper_%02d" % index)
	_beveled(rig, 0.32, 7.94, 0.34, Vector3(0, 0.12, 0), bevel, 0.08, "RoundedBinding")
	cover_hinge = Node3D.new()
	cover_hinge.name = "FrontCoverHinge"
	cover_hinge.position.y = 0.92
	rig.add_child(cover_hinge)
	_beveled(cover_hinge, 7.25, 8.00, 0.15, Vector3(3.55, 0, 0), cloth, 0.10, "PlainGreenCover")
	_beveled(cover_hinge, 7.13, 7.88, 0.026, Vector3(3.55, 0.087, 0), _material("384039"), 0.08, "ClothFace")
	# No gold corners, badge, or decorative center medallion: the reference is plain.
	var shader := Shader.new()
	shader.code = """shader_type spatial;
render_mode unshaded, cull_disabled;
uniform sampler2D page_texture : source_color, filter_linear;
uniform vec4 paper_tint : source_color = vec4(0.898,0.824,0.745,1.0);
void fragment() {
    vec4 ink = texture(page_texture, UV);
    vec3 paper = paper_tint.rgb;
    float crease = exp(-abs(UV.x-0.5)*75.0) * 0.145;
    float edge = pow(abs(UV.y-0.5)*2.0,10.0)*0.025;
    float grain = (fract(sin(dot(UV,vec2(12781.1,31117.7)))*43758.5453)-0.5)*0.006;
    ALBEDO = mix(paper, ink.rgb, ink.a) * (1.0-crease-edge+grain);
    ROUGHNESS = 1.0;
}"""
	_page_material = ShaderMaterial.new()
	_page_material.shader = shader
	page_surface = _mesh_node(rig, _paper_mesh(0.0, 1.0, 0.0), _page_material, Vector3.ZERO, "CurvedNativeControlPaper")
	turn_hinge = Node3D.new()
	turn_hinge.name = "TurningLeaf"
	turn_hinge.position.y = PAPER_Y
	rig.add_child(turn_hinge)
	var turn_shader := Shader.new()
	turn_shader.code = """shader_type spatial;
render_mode unshaded, cull_disabled;
uniform sampler2D page_texture : source_color, filter_linear;
uniform vec4 paper_tint : source_color = vec4(0.898,0.824,0.745,1.0);
void fragment(){
    vec4 ink=texture(page_texture,UV);
    ALBEDO=FRONT_FACING?mix(paper_tint.rgb,ink.rgb,ink.a):paper_tint.rgb*0.96;
    ROUGHNESS=1.0;
}"""
	_turn_material = ShaderMaterial.new()
	_turn_material.shader = turn_shader
	turn_surface = _mesh_node(turn_hinge, _paper_mesh(0.5, 1.0, 0.0), _turn_material, Vector3(0, -PAPER_Y, 0), "FlexibleTurningPaper")
	turn_hinge.visible = false
	for index in range(8):
		var at := tab_anchor(index)
		var outline := PackedVector2Array([Vector2(-0.63,-0.31), Vector2(0.56,-0.31), Vector2(0.66,-0.21), Vector2(0.59,0.18), Vector2(0.69,0.31), Vector2(-0.63,0.31)])
		var tab := _prism(rig, outline, 0.06, at - Vector3(0,0.035,0), _material(TAB_COLORS[index], 0.94, 0.0, true), "Bookmark_%02d" % index)
		tabs.append(tab)
		var edge := _box(tab, Vector3(1.05,0.008,0.016), Vector3(0,0.034,-0.287), _material(Color(TAB_COLORS[index]).lightened(0.16).to_html(false), 1.0, 0.0, true))
		edge.name = "StitchedTopEdge"
	for index in range(2):
		var at := stamp_anchor(index)
		var stamp := _beveled(rig, 1.34, 0.57, 0.065, at, _material("735047", 0.9,0.0,true), 0.17, "SavePlaque" if index == 0 else "ClosePlaque")
		_beveled(stamp, 1.24, 0.47, 0.012, Vector3(0,0.04,0), _material("79564c",1.0,0.0,true), 0.13)
		stamps.append(stamp)

func _paper_point(u: float, v: float, offset := 0.0) -> Vector3:
	var x := (u - 0.5) * PAPER_SIZE.x
	var fold := exp(-absf(x) * 1.65)
	var weight := clampf(1.0 + offset * 1.7, 0.15, 1.0)
	var arch := sin(absf(u - 0.5) * TAU) * 0.18
	var y := PAPER_Y + offset + (arch - fold * 0.23) * weight
	y -= pow(absf(v - 0.5) * 2.0, 6.0) * 0.027
	return Vector3(x, y, (v - 0.5) * PAPER_SIZE.y + fold * 0.40 * weight)

func _paper_mesh(u_start: float, u_end: float, offset: float) -> ArrayMesh:
	var columns := 64 if u_end-u_start > 0.6 else 32
	var rows := 10
	var vertices := PackedVector3Array()
	var normals := PackedVector3Array()
	var uv := PackedVector2Array()
	var indices := PackedInt32Array()
	for row in range(rows + 1):
		var v := float(row) / rows
		for col in range(columns + 1):
			var u := lerpf(u_start, u_end, float(col) / columns)
			var along_x := _paper_point(u+0.0001,v,offset)-_paper_point(u-0.0001,v,offset)
			var along_z := _paper_point(u,v+0.0001,offset)-_paper_point(u,v-0.0001,offset)
			vertices.append(_paper_point(u,v,offset))
			normals.append(along_z.cross(along_x).normalized())
			uv.append(Vector2(u,v))
	for row in range(rows):
		for col in range(columns):
			var a := row*(columns+1)+col
			var b := a+1
			var c := a+columns+1
			var d := c+1
			indices.append_array(PackedInt32Array([a,c,b,b,c,d]))
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = vertices
	arrays[Mesh.ARRAY_NORMAL] = normals
	arrays[Mesh.ARRAY_TEX_UV] = uv
	arrays[Mesh.ARRAY_INDEX] = indices
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	# Each paper layer has real fore-edge and head/tail thickness, not a painted line.
	var edge := SurfaceTool.new()
	edge.begin(Mesh.PRIMITIVE_TRIANGLES)
	for boundary in range(4):
		var steps := columns if boundary < 2 else rows
		for index in range(steps):
			var t0 := float(index)/steps
			var t1 := float(index+1)/steps
			var a: Vector3
			var b: Vector3
			if boundary < 2:
				a = _paper_point(lerpf(u_start,u_end,t0), float(boundary), offset)
				b = _paper_point(lerpf(u_start,u_end,t1), float(boundary), offset)
			else:
				a = _paper_point(u_start if boundary == 2 else u_end, t0, offset)
				b = _paper_point(u_start if boundary == 2 else u_end, t1, offset)
			if boundary == 0 or boundary == 3:
				var swap := a
				a = b
				b = swap
			_triangle(edge,a,b,b-Vector3(0,0.023,0))
			_triangle(edge,a,b-Vector3(0,0.023,0),a-Vector3(0,0.023,0))
	edge.commit(mesh)
	return mesh

func _build_study() -> void:
	var stone := _material("303037")
	var dark_stone := _material("25272c")
	var oak := _material("30211f")
	# The window is an actual opening. Distant forms remain visible between lead bars.
	_box(_study,Vector3(11.0,17.0,0.9),Vector3(-14.0,6.0,-8.5),stone,"WestWall")
	_box(_study,Vector3(25.0,17.0,0.9),Vector3(11.2,6.0,-8.5),dark_stone,"RightWall")
	_box(_study,Vector3(7.0,4.0,0.9),Vector3(-4.8,-0.8,-8.5),stone,"WindowApron")
	_box(_study,Vector3(7.0,4.0,0.9),Vector3(-4.8,14.0,-8.5),stone,"WindowLintel")
	var sky := _material("526e98",1.0,0.0,true)
	_box(_study,Vector3(11.0,15.0,0.25),Vector3(-4.8,6.8,-15.0),sky,"BlueEveningSky")
	_build_hill(_study,[Vector2(-10.2,0.0),Vector2(-9.0,2.4),Vector2(-7.0,3.4),Vector2(-5.8,5.0),Vector2(-3.7,4.3),Vector2(-1.8,3.1),Vector2(0.8,2.8)],-13.1,"3b5574")
	_build_hill(_study,[Vector2(-10.2,-0.5),Vector2(-8.1,0.9),Vector2(-5.8,2.4),Vector2(-4.1,2.7),Vector2(-2.9,4.0),Vector2(-0.8,3.4),Vector2(0.8,2.0)],-11.5,"29445d")
	var distant := _material("243544",0.98)
	_box(_study,Vector3(1.65,1.50,0.75),Vector3(-2.35,4.35,-11.9),distant,"DistantAcademy")
	for x in [-3.0,-2.35,-1.65]:
		_box(_study,Vector3(0.46,2.05,0.65),Vector3(x,4.68,-11.75),distant)
		_cylinder(_study,0.42,0.95,Vector3(x,6.18,-11.75),_material("25384d"),4,0.0)
		for y in [4.35,4.95]:
			_box(_study,Vector3(0.10,0.19,0.035),Vector3(x+0.04,y,-11.395),_material("d1aa62",1.0,0.0,true))
	var frame := _material("212932")
	for x in [-8.35,-1.25]:
		_box(_study,Vector3(0.48,11.4,0.8),Vector3(x,6.45,-7.77),frame)
	for y in [0.95,11.95]:
		_box(_study,Vector3(7.6,0.43,0.95),Vector3(-4.8,y,-7.65),frame)
	_box(_study,Vector3(7.9,0.30,1.65),Vector3(-4.8,0.68,-7.15),_material("353b47"),"DeepWindowSill")
	# Continuous clipped diamond grid; no bars extend outside the opening.
	for slope in [-1.0,1.0]:
		for intercept in range(-4,8):
			var hits: Array[Vector2] = []
			var d := float(intercept)*2.4
			for x in [-8.10,-1.50]:
				var y: float = float(slope)*(float(x)+8.10)*1.6+d
				if y>=1.20 and y<=11.70: hits.append(Vector2(x,y))
			for y in [1.20,11.70]:
				var x: float = (float(y)-d)/(float(slope)*1.6)-8.10
				if x>-8.10 and x< -1.50: hits.append(Vector2(x,y))
			if hits.size()>=2:
				_beam(_study,Vector3(hits[0].x,hits[0].y,-7.43),Vector3(hits[1].x,hits[1].y,-7.43),0.13,0.13,frame)
	# Tall cropped bookcase on the left of the composition.
	_box(_study,Vector3(6.8,15.8,0.32),Vector3(-14.0,5.5,-7.7),oak)
	for x in [-17.2,-10.8]:
		_box(_study,Vector3(0.40,16.0,1.9),Vector3(x,5.5,-7.0),oak)
	for row in range(6):
		var y := -1.8+row*2.62
		_box(_study,Vector3(6.8,0.26,2.10),Vector3(-14.0,y,-6.8),oak)
		for index in range(10):
			var height := 1.65+float(posmod(index*7+row*3,5))*0.14
			var color: String = ["533b31","514336","28372e","3d2c2c","31343c"][posmod(index+row,5)]
			var x := -16.7+float(index)*0.58
			var volume := _box(_study,Vector3(0.44,height,1.32),Vector3(x,y+height*0.5+0.14,-6.7),_material(color))
			if index==2 or index==8: volume.rotation.z = -0.045
			for band in [0.22,height-0.20]:
				_box(_study,Vector3(0.45,0.045,0.025),Vector3(x,y+band,-6.025),_material("7d6849"))
	desk = Node3D.new()
	desk.name = "WritingTable"
	_study.add_child(desk)
	var table := Node3D.new()
	table.rotation_degrees.y = -8.0
	desk.add_child(table)
	_box(table,Vector3(35.0,0.50,17.0),Vector3(-2.0,-0.34,1.4),_material("39261f"))
	for row in range(9):
		for col in range(4):
			var color: String = ["5a382c","51332c","634033","53362e","49332b"][posmod(row*3+col*2,5)]
			_box(table,Vector3(8.64,0.075,1.87),Vector3(-15.0+col*8.72,-0.048,-6.10+row*1.89),_material(color))
	_build_candle(desk,Vector3(-9.0,0.0,-2.4))
	var ink := _material("252831",0.48)
	_cylinder(desk,0.70,0.90,Vector3(-5.65,0.46,-1.75),ink,7)
	_cylinder(desk,0.70,0.30,Vector3(-5.65,1.06,-1.75),ink,7,0.43)
	_cylinder(desk,0.34,0.27,Vector3(-5.65,1.34,-1.75),_material("2e2a2c"),8)
	_cylinder(desk,0.48,0.19,Vector3(-5.65,1.53,-1.75),_material("5f4c3d",0.72),6)

func _build_hill(parent: Node3D, skyline: Array, z: float, color: String) -> void:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	for i in range(skyline.size()-1):
		var a: Vector2 = skyline[i]
		var b: Vector2 = skyline[i+1]
		var low_a := Vector3(a.x,-2.0,z)
		var low_b := Vector3(b.x,-2.0,z)
		var top_a := Vector3(a.x,a.y,z)
		var top_b := Vector3(b.x,b.y,z)
		_triangle(st,low_a,low_b,top_b)
		_triangle(st,low_a,top_b,top_a)
		_triangle(st,top_a,top_b,top_b+Vector3(0,-0.35,-1.6))
		_triangle(st,top_a,top_b+Vector3(0,-0.35,-1.6),top_a+Vector3(0,-0.35,-1.6))
	_mesh_node(parent,st.commit(),_material(color,1.0,0.0,true))

func _build_candle(parent: Node3D, at: Vector3) -> void:
	var base := Node3D.new()
	base.name = "FacetedBrassCandlestick"
	base.position = at
	parent.add_child(base)
	var brass := _material("a77942",0.64,0.22)
	var pale_brass := _material("bb8b50",0.64,0.18)
	_cylinder(base,1.12,0.23,Vector3(0,0.16,0),brass,8)
	_cylinder(base,0.96,0.30,Vector3(0,0.40,0),pale_brass,8,0.46)
	_cylinder(base,0.29,0.95,Vector3(0,1.00,0),brass,8,0.22)
	_cylinder(base,0.31,0.25,Vector3(0,1.55,0),brass,8,0.50)
	_cylinder(base,0.70,0.16,Vector3(0,1.77,0),pale_brass,8)
	_cylinder(base,0.62,0.08,Vector3(0,1.90,0),brass,8)
	_cylinder(base,0.40,2.48,Vector3(0,3.13,0),_material("e9c998",0.99),7,0.37)
	_cylinder(base,0.047,0.18,Vector3(0,4.45,0),_material("35231d"),5)
	_flame = Node3D.new()
	_flame.name = "AngularFlame"
	_flame.position = Vector3(0,4.48,0)
	base.add_child(_flame)
	var fire := _material("efb35e",1.0,0.0,true)
	_cylinder(_flame,0.28,0.62,Vector3(0,0.35,0),fire,5,0.37)
	_cylinder(_flame,0.37,0.92,Vector3(0,1.10,0),_material("ffc67a",1.0,0.0,true),5,0.0)
	_cylinder(_flame,0.16,0.70,Vector3(0,0.42,0.08),_material("fff0bf",1.0,0.0,true),5,0.0)
	_candle_light = OmniLight3D.new()
	_candle_light.name = "CandleWarmPool"
	_candle_light.position = Vector3(0,4.8,0.35)
	_candle_light.light_color = Color("ffc181")
	_candle_light.light_energy = 2.35
	_candle_light.omni_range = 19.0
	_candle_light.omni_attenuation = 1.45
	_candle_light.shadow_enabled = true
	_candle_light.shadow_blur = 1.6
	_candle_light.light_size = 0.65
	base.add_child(_candle_light)

func _build_hall() -> void:
	var stone := _material("4b4747")
	var cap := _material("62574d")
	var dark := _material("302f33")
	for row in range(24):
		for col in range(12):
			var color := "6b6057" if (row+col)%2 == 0 else "3d414a"
			_box(_hall,Vector3(2.10,0.09,2.10),Vector3(-11.55+col*2.10,0.40,-34.0+row*2.10),_material(color))
	for side in [-1.0,1.0]:
		_box(_hall,Vector3(0.8,20.0,49.0),Vector3(side*12.6,10.0,-10.0),dark)
		for bay in range(6):
			var z := 5.0-float(bay)*7.0
			for block in range(7):
				_box(_hall,Vector3(1.35,1.52,1.60),Vector3(side*10.4,1.30+block*1.54,z),stone)
			_box(_hall,Vector3(2.1,0.45,2.25),Vector3(side*10.4,0.72,z),cap)
			_box(_hall,Vector3(1.85,0.42,2.0),Vector3(side*10.4,11.55,z),cap)
			# Recessed cold window visible along the aisle.
			var window := _box(_hall,Vector3(0.08,8.0,4.55),Vector3(side*12.10,7.9,z-3.4),_material("425c7d",1.0,0.0,true))
			window.name = "MoonlitCloisterWindow"
			for yy in [4.2,7.9,11.6]:
				_box(_hall,Vector3(0.30,0.18,4.8),Vector3(side*11.94,yy,z-3.4),dark)
			for zz in [-1.60,0.0,1.60]:
				_box(_hall,Vector3(0.30,8.3,0.18),Vector3(side*11.94,7.9,z-3.4+zz),dark)
	for bay in range(6):
		var z := 5.0-float(bay)*7.0
		_arch(_hall,10.4,11.7,z,0.68,stone)
		if bay>=2:
			_box(_hall,Vector3(0.06,1.8,0.06),Vector3(0,18.2,z),dark)
			var lantern := _cylinder(_hall,0.40,0.88,Vector3(0,16.95,z),_material("e1b572",1.0,0.0,true),6,0.30)
			_cylinder(_hall,0.48,0.15,lantern.position+Vector3(0,0.52,0),_material("302a25"),6,0.15)
			_cylinder(_hall,0.36,0.20,lantern.position-Vector3(0,0.53,0),_material("302a25"),6,0.0)
	_box(_hall,Vector3(25.0,22.0,0.7),Vector3(0,10,-39),dark)

func _arch(parent: Node3D, radius: float, spring: float, z: float, thickness: float, material: Material) -> void:
	for step in range(16):
		var a := float(step)/16.0*PI
		var b := float(step+1)/16.0*PI
		var st := SurfaceTool.new()
		st.begin(Mesh.PRIMITIVE_TRIANGLES)
		var points: Array[Vector3] = []
		for depth in [-0.46,0.46]:
			for r in [radius,radius+thickness]:
				for angle in [a,b]: points.append(Vector3(cos(angle)*r,spring+sin(angle)*r,z+depth))
		for face in [[0,2,3,1],[4,5,7,6],[0,1,5,4],[2,6,7,3],[0,4,6,2],[1,3,7,5]]:
			_triangle(st,points[face[0]],points[face[1]],points[face[2]])
			_triangle(st,points[face[0]],points[face[2]],points[face[3]])
		_mesh_node(parent,st.commit(),material)

func set_page_texture(texture: Texture2D) -> void:
	if _page_material != null: _page_material.set_shader_parameter("page_texture",texture)

func set_context(is_desktop: bool) -> void:
	desktop = is_desktop
	title_spread = is_desktop
	casebook_spread = not is_desktop
	apply_pose()

func set_open(value: float) -> void:
	opened = clampf(value,0.0,1.0)
	apply_pose()

func set_pull(value: float) -> void:
	pull_amount = clampf(value,0.0,1.0)
	apply_pose()

func set_zoom(value: float) -> void:
	_zoom = clampf(value,0.0,1.0)
	apply_pose()

func resize_view(dimensions: Vector2, ui_scale := 1.0) -> void:
	_aspect = dimensions.x/maxf(1.0,dimensions.y)
	_ui_scale = clampf(ui_scale,0.85,1.25)
	apply_pose()

func apply_pose() -> void:
	if not is_instance_valid(rig): return
	var progress := smoothstep(0.0,1.0,opened)
	left_stack.visible = opened>0.35
	page_surface.visible = opened>0.72
	cover_hinge.visible = opened<0.995
	cover_hinge.rotation.z = PI*opened
	for tab in tabs: tab.visible = opened>0.82
	for stamp in stamps: stamp.visible = opened>0.82
	_study.visible = desktop and opened<0.48
	_hall.visible = not desktop or opened>=0.48
	var close_position := Vector3(-7.86,0.06,4.41)
	var read_position := Vector3(-0.30,5.80,1.30)
	rig.position = close_position.lerp(read_position,progress)
	rig.rotation_degrees = Vector3(58.0*progress,27.0*(1.0-progress),0.0)
	if not desktop:
		rig.position = read_position+Vector3(0,-(1.0-pull_amount)*12.0,(1.0-pull_amount)*1.0)
		rig.rotation_degrees = Vector3(58.0+(1.0-pull_amount)*12.0,0,0)
	camera.position = Vector3(0,10.0,22.0).lerp(Vector3(0,7.0,20.0),progress)
	var focus := Vector3(0,1.9,0).lerp(Vector3(0,5.5,0),progress)
	camera.look_at(focus,Vector3.UP)
	camera.fov = 30.0*(1.0-_zoom*0.10)
	# The viewport owns the 2:1 safe frame; preserve its reference composition.
	camera.keep_aspect = Camera3D.KEEP_HEIGHT

func tab_anchor(index: int) -> Vector3:
	return Vector3(7.28,0.70,-3.05+float(index)*0.86)

func stamp_anchor(index: int) -> Vector3:
	return Vector3(-5.85 if index==0 else 5.85,0.46,4.07)

func anchor_screen(at: Vector3) -> Vector2:
	return camera.unproject_position(rig.to_global(at))

func page_screen(point: Vector2, texture_size: Vector2) -> Vector2:
	var uv := point/texture_size
	return anchor_screen(_paper_point(uv.x,uv.y))

func page_point(screen: Vector2, texture_size: Vector2) -> Vector2:
	var origin := rig.to_local(camera.project_ray_origin(screen))
	var direction := rig.global_basis.inverse()*camera.project_ray_normal(screen)
	if absf(direction.y)<0.000001: return Vector2(-9999,-9999)
	var distance := (PAPER_Y-origin.y)/direction.y
	if distance<0.0: return Vector2(-9999,-9999)
	# Solve the same curved paper surface, rather than intersecting a flat proxy.
	var hit := origin+direction*distance
	for iteration in range(8):
		var u := hit.x/PAPER_SIZE.x+0.5
		var fold := exp(-absf(hit.x)*1.65)
		var v := (hit.z-fold*0.40)/PAPER_SIZE.y+0.5
		var surface := _paper_point(u,v)
		distance = (surface.y-origin.y)/direction.y
		hit = origin+direction*distance
	var u := hit.x/PAPER_SIZE.x+0.5
	var v := (hit.z-exp(-absf(hit.x)*1.65)*0.40)/PAPER_SIZE.y+0.5
	return Vector2(u,v)*texture_size

func begin_turn(texture: Texture2D) -> void:
	_turn_material.set_shader_parameter("page_texture",texture)
	turn_hinge.rotation.z = 0.0
	turn_surface.mesh = _turn_mesh(0.0)
	turn_hinge.visible = true

func set_turn(value: float) -> void:
	turn_lift = sin(value*PI)
	turn_surface.mesh = _turn_mesh(value)
	turn_hinge.rotation.z = PI*value
	turn_hinge.position.y = PAPER_Y+0.018+turn_lift*0.075
	if value>=0.999: turn_hinge.visible = false

func _turn_point(u: float, v: float, progress: float) -> Vector3:
	var point := _paper_point(0.5+u*0.5,v)
	var lift := sin(progress*PI)
	var bend := lift*0.64
	if bend>0.0001:
		# Arc length is retained while the free edge rolls gently toward the spine.
		point.x = sin(u*bend)/bend*PAPER_SIZE.x*0.5
		point.y += (1.0-cos(u*bend))/bend*PAPER_SIZE.x*0.375
		point.y += sin(v*PI)*u*u*lift*0.10
		point.z += (v-0.5)*u*u*lift*0.10
	return point

func _turn_mesh(progress: float) -> ArrayMesh:
	var columns := 40
	var rows := 12
	var vertices := PackedVector3Array()
	var normals := PackedVector3Array()
	var uv := PackedVector2Array()
	var indices := PackedInt32Array()
	for row in range(rows+1):
		var v := float(row)/rows
		for col in range(columns+1):
			var u := float(col)/columns
			vertices.append(_turn_point(u,v,progress))
			var dx := _turn_point(u+0.0001,v,progress)-_turn_point(u-0.0001,v,progress)
			var dz := _turn_point(u,v+0.0001,progress)-_turn_point(u,v-0.0001,progress)
			normals.append(dz.cross(dx).normalized())
			uv.append(Vector2(0.5+u*0.5,v))
	for row in range(rows):
		for col in range(columns):
			var a := row*(columns+1)+col
			var b := a+1
			var c := a+columns+1
			var d := c+1
			indices.append_array(PackedInt32Array([a,c,b,b,c,d]))
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = vertices
	arrays[Mesh.ARRAY_NORMAL] = normals
	arrays[Mesh.ARRAY_TEX_UV] = uv
	arrays[Mesh.ARRAY_INDEX] = indices
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays)
	return mesh
