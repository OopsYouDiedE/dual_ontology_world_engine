extends RefCounted
## Shared metre-scale joinery. No evidence marks or actor state.
const PREFABS = preload("res://scenes/ArtStyleTrials/E/prefabs/PrefabLibrary.gd")
static func handles(kind: String) -> bool:
	return kind in ["bed_0","wardrobe","chair_back","chair_front","desk_0","study_table","worktable","nightstand","shoe_bench","crate","barrel","bookcase_empty"]

static func furnish(kit: Node3D, kind: String, at: Vector3, title: String, palette: Dictionary = {}) -> Node3D:
	if not handles(kind): return null
	var canonical: String = "chair_back" if kind == "chair_front" else kind
	var colors: Dictionary = {"wood":"503725", "edge":"755034", "panel":"61452f", "brass":"aa8750", "cloth":"35574c", "linen":"d9d0b7"}
	for role in colors:
		if palette.has(role): colors[role] = palette[role]
	# Unused textile overrides must not split otherwise identical joinery.
	if canonical not in ["bed_0", "chair_back"]: colors.cloth = "35574c"
	if canonical != "bed_0": colors.linen = "d9d0b7"
	var key: String = "joinery-v1/" + canonical
	for role in colors: key += "/" + str(colors[role])
	var root: Node3D = PREFABS.instantiate(key, func() -> Node3D: return _build(kit, canonical, colors))
	if root == null: return null
	root.name = title
	root.set_meta("authoring_label", title)
	root.position = at
	if kind == "chair_front": root.rotation.y = PI
	kit.content_root.add_child(root)
	return root

static func _build(kit: Node3D, kind: String, palette: Dictionary) -> Node3D:
	var root := Node3D.new()
	root.name = "JoineryPrefab"
	var wood: Material=kit.material("joinery_wood_"+palette.wood,palette.wood,1,0.46)
	var edge: Material=kit.material("joinery_edge_"+palette.edge,palette.edge,1,0.43)
	var panel: Material=kit.material("joinery_panel_"+palette.panel,palette.panel,1,0.52)
	var brass: Material=kit.material("joinery_brass_"+palette.brass,palette.brass,0,0.36,0.70)
	var cloth: Material=kit.material("joinery_cloth_"+palette.cloth,palette.cloth,3,0.94)
	root.set_meta("construction","separate joinery members; metres")
	match kind:
		"bed_0": _bed(kit,root,wood,edge,panel,cloth,palette.linen)
		"wardrobe": _wardrobe(kit,root,wood,edge,panel,brass)
		"chair_back", "chair_front":
			if kind=="chair_front": root.rotation.y=PI
			_chair(kit,root,wood,edge,cloth)
		"desk_0", "study_table", "worktable", "nightstand":
			var size := Vector2(1.08,0.60)
			if kind=="study_table": size=Vector2(1.65,0.95)
			elif kind=="worktable": size=Vector2(1.25,0.65)
			elif kind=="nightstand": size=Vector2(0.45,0.42)
			_table(kit,root,size,0.60 if kind=="nightstand" else 0.76,wood,edge,panel,brass)
		"shoe_bench": _bench(kit,root,wood,edge)
		"crate": _crate(kit,root,wood,edge)
		"barrel": _barrel(kit,root,wood,edge)
		"bookcase_empty": _shelf(kit,root,wood,edge)
	return root

static func _panel(kit: Node3D, root: Node3D, at: Vector3, width: float, height: float, frame: Material, inset: Material) -> void:
	# Raised frame surrounds a thinner recessed solid wood field.
	kit.box(root,at,Vector3(width-0.06,height-0.06,0.025),inset)
	for x in [-1,1]: kit.box(root,at+Vector3(x*(width/2-0.025),0,0.018),Vector3(0.05,height,0.055),frame)
	for y in [-1,1]: kit.box(root,at+Vector3(0,y*(height/2-0.025),0.018),Vector3(width-0.10,0.05,0.055),frame)

static func _bed(kit: Node3D, root: Node3D, wood: Material, edge: Material, panel: Material, cloth: Material, linen_color: String) -> void:
	var linen: Material=kit.material("joinery_linen_"+linen_color,linen_color,3,0.96)
	# 1.04 x 2.12 external frame, a 0.94 x 1.98 mattress, open below.
	for x in [-0.48,0.48]:
		for z in [-1.00,1.00]:
			kit.box(root,Vector3(x,0.40,z),Vector3(0.08,0.80,0.08),wood)
			kit.box(root,Vector3(x,0.12,z),Vector3(0.10,0.045,0.10),edge)
			var top: float=1.02 if z<0 else 0.75
			kit.cylinder(root,Vector3(x,(0.72+top)/2,z),0.045,top-0.72,wood)
			kit.ellipsoid(root,Vector3(x,top+0.025,z),Vector3(0.095,0.075,0.095),edge)
		kit.box(root,Vector3(x,0.39,0),Vector3(0.055,0.15,1.94),wood)
	for z in [-0.97,0.97]: kit.box(root,Vector3(0,0.39,z),Vector3(0.91,0.15,0.055),wood)
	for i in range(13): kit.box(root,Vector3(0,0.43,-0.90+i*0.15),Vector3(0.93,0.025,0.075),panel)
	_panel(kit,root,Vector3(0,0.77,-1.00),0.88,0.40,edge,panel)
	_panel(kit,root,Vector3(0,0.57,1.00),0.88,0.24,edge,panel)
	for z in [-1.0,1.0]: kit.box(root,Vector3(0,0.995 if z<0 else 0.715,z),Vector3(1.02,0.055,0.12),edge)
	kit.box(root,Vector3(0,0.52,0),Vector3(0.94,0.16,1.98),linen)
	# Mattress piping follows all four edges; blanket hangs over the sides.
	for x in [-0.47,0.47]: kit.limb(root,Vector3(x,0.58,-0.96),Vector3(x,0.58,0.96),0.009,linen)
	kit.box(root,Vector3(0,0.614,0.26),Vector3(0.96,0.028,1.40),cloth)
	for x in [-0.49,0.49]: kit.box(root,Vector3(x,0.52,0.26),Vector3(0.025,0.20,1.40),cloth)
	kit.ellipsoid(root,Vector3(0,0.65,-0.71),Vector3(0.77,0.15,0.36),linen)
	kit.box(root,Vector3(0,0.64,-0.36),Vector3(0.96,0.035,0.17),linen)
	root.set_meta("dimensions_m",Vector3(1.04,1.10,2.12))

static func _table(kit: Node3D, root: Node3D, size: Vector2, h: float, wood: Material, edge: Material, panel: Material, brass: Material) -> void:
	var lx: float=size.x/2-0.07
	var lz: float=size.y/2-0.07
	for x in [-lx,lx]:
		for z in [-lz,lz]:
			kit.cylinder(root,Vector3(x,(h-0.13)/2,z),0.025,h-0.13,wood,0.037)
			kit.box(root,Vector3(x,h-0.13,z),Vector3(0.072,0.18,0.072),wood)
			kit.cylinder(root,Vector3(x,0.07,z),0.030,0.025,edge)
	# Thin independent aprons; no solid block filling the knee space.
	for x in [-lx,lx]: kit.box(root,Vector3(x,h-0.10,0),Vector3(0.045,0.14,size.y-0.12),wood)
	for z in [-lz,lz]: kit.box(root,Vector3(0,h-0.10,z),Vector3(size.x-0.12,0.14,0.035),wood)
	kit.box(root,Vector3(0,h-0.026,0),Vector3(size.x,0.052,size.y),edge)
	for i in range(4):
		kit.box(root,Vector3(0,h+0.002,-size.y/2+(i+0.5)*size.y/4),Vector3(size.x-0.025,0.014,size.y/4-0.003),panel)
	var drawers: int=2 if size.x>1.3 else 1
	for i in drawers:
		var dw: float=(size.x-0.22)/drawers
		var dx: float=(i-(drawers-1)/2.0)*dw
		_panel(kit,root,Vector3(dx,h-0.11,size.y/2-0.045),dw-0.025,0.12,wood,panel)
		for x in [-0.035,0.035]: kit.cylinder(root,Vector3(dx+x,h-0.11,size.y/2+0.002),0.012,0.022,brass).rotation.x=PI/2
		kit.limb(root,Vector3(dx-0.035,h-0.125,size.y/2+0.018),Vector3(dx+0.035,h-0.125,size.y/2+0.018),0.009,brass)
	root.set_meta("dimensions_m",Vector3(size.x,h+0.009,size.y))

static func _chair(kit: Node3D, root: Node3D, wood: Material, edge: Material, cloth: Material) -> void:
	for x in [-0.19,0.19]:
		kit.limb(root,Vector3(x*1.08,0.025,0.19),Vector3(x,0.43,0.17),0.024,wood)
		kit.limb(root,Vector3(x,0.025,-0.18),Vector3(x,0.91,-0.25),0.025,wood)
		kit.limb(root,Vector3(x,0.20,-0.19),Vector3(x,0.20,0.18),0.016,wood)
	kit.limb(root,Vector3(-0.19,0.19,0),Vector3(0.19,0.19,0),0.016,wood)
	for z in [-0.18,0.18]: kit.box(root,Vector3(0,0.40,z),Vector3(0.43,0.075,0.045),wood)
	for x in [-0.19,0.19]: kit.box(root,Vector3(x,0.40,0),Vector3(0.045,0.075,0.36),wood)
	kit.box(root,Vector3(0,0.445,0),Vector3(0.46,0.045,0.44),edge)
	kit.ellipsoid(root,Vector3(0,0.48,0),Vector3(0.42,0.07,0.40),cloth)
	kit.box(root,Vector3(0,0.89,-0.25),Vector3(0.47,0.075,0.065),edge)
	kit.box(root,Vector3(0,0.59,-0.22),Vector3(0.39,0.045,0.045),wood)
	for x in [-0.10,0.10]: kit.limb(root,Vector3(x,0.60,-0.22),Vector3(x,0.855,-0.25),0.015,wood)

static func _wardrobe(kit: Node3D, root: Node3D, wood: Material, edge: Material, panel: Material, brass: Material) -> void:
	for x in [-0.46,0.46]:
		for z in [-0.24,0.24]: kit.box(root,Vector3(x,0.10,z),Vector3(0.095,0.20,0.095),wood)
	# Actual carcass: side boards, back, base and crown, with closed framed doors.
	for x in [-0.49,0.49]: kit.box(root,Vector3(x,1.04,0),Vector3(0.045,1.76,0.56),wood)
	kit.box(root,Vector3(0,1.04,-0.27),Vector3(0.95,1.76,0.025),panel)
	for y in [0.18,1.91]: kit.box(root,Vector3(0,y,0),Vector3(1.06,0.08,0.61),edge)
	kit.box(root,Vector3(0,1.99,0),Vector3(1.10,0.075,0.64),wood)
	for x in [-0.245,0.245]:
		_panel(kit,root,Vector3(x,1.07,0.285),0.475,1.64,wood,panel)
		_panel(kit,root,Vector3(x,1.43,0.313),0.35,0.72,edge,panel)
		_panel(kit,root,Vector3(x,0.69,0.313),0.35,0.60,edge,panel)
		kit.ellipsoid(root,Vector3(x*0.18,1.04,0.365),Vector3(0.035,0.055,0.035),brass)
		for y in [0.48,1.62]: kit.cylinder(root,Vector3(signf(x)*0.477,y,0.33),0.011,0.08,brass)
	root.set_meta("dimensions_m",Vector3(1.10,2.03,0.64))

static func _bench(kit: Node3D, root: Node3D, wood: Material, edge: Material) -> void:
	for x in [-0.47,0.47]:
		for z in [-0.15,0.15]: kit.limb(root,Vector3(x*1.03,0.02,z*1.1),Vector3(x,0.43,z),0.028,wood)
		kit.limb(root,Vector3(x,0.17,-0.15),Vector3(x,0.17,0.15),0.020,wood)
	kit.limb(root,Vector3(-0.47,0.18,0),Vector3(0.47,0.18,0),0.022,wood)
	for z in [-0.12,0.0,0.12]: kit.box(root,Vector3(0,0.45,z),Vector3(1.15,0.045,0.11),edge)

static func _crate(kit: Node3D, root: Node3D, wood: Material, edge: Material) -> void:
	for i in range(4):
		var y: float=0.075+i*0.135
		for z in [-0.275,0.275]: kit.box(root,Vector3(0,y,z),Vector3(0.65,0.129,0.028),wood)
		for x in [-0.31,0.31]: kit.box(root,Vector3(x,y,0),Vector3(0.028,0.129,0.53),edge)
	for i in range(5): kit.box(root,Vector3(-0.26+i*0.13,0.57,0),Vector3(0.124,0.03,0.58),wood)
	for x in [-0.24,0.24]:
		kit.box(root,Vector3(x,0.596,0),Vector3(0.07,0.025,0.58),edge)
		for z in [-0.30,0.30]: kit.box(root,Vector3(x,0.29,z),Vector3(0.06,0.58,0.025),edge)

static func _barrel(kit: Node3D, root: Node3D, wood: Material, edge: Material) -> void:
	var iron: Material=kit.material("yi_barrel_iron","3b403a",0,0.57,0.55)
	# Twenty curved staves; five bands approximate each stave's coopered bow.
	for i in range(20):
		var a: float=i*TAU/20.0
		for j in range(5):
			var y: float=0.075+j*0.15
			var radius: float=0.27+0.045*sin((j+0.5)*PI/5.0)
			var stave: MeshInstance3D=kit.box(root,Vector3(cos(a)*radius,y,sin(a)*radius),Vector3(0.028,0.151,0.087),wood if i%3 else edge)
			stave.rotation.y=-a
	for y in [0.075,0.20,0.55,0.675]:
		var radius: float=0.27+0.045*sin(y*PI/0.75)
		var hoop := MeshInstance3D.new()
		var torus := TorusMesh.new()
		torus.inner_radius=radius+0.010
		torus.outer_radius=radius+0.025
		torus.rings=40
		torus.ring_segments=8
		hoop.mesh=torus
		hoop.material_override=iron
		hoop.position.y=y
		hoop.scale.y=2.6
		root.add_child(hoop)
	kit.cylinder(root,Vector3(0,0.735,0),0.269,0.025,wood)
	for x in [-0.15,0.0,0.15]:
		var length: float=2.0*sqrt(0.26*0.26-x*x)
		kit.box(root,Vector3(x,0.75,0),Vector3(0.003,0.002,length),iron)

static func _shelf(kit: Node3D, root: Node3D, wood: Material, edge: Material) -> void:
	for x in [-0.47,0.47]: kit.box(root,Vector3(x,0.91,0),Vector3(0.055,1.82,0.32),wood)
	kit.box(root,Vector3(0,0.91,-0.15),Vector3(0.90,1.82,0.018),wood)
	for y in [0.12,0.47,0.82,1.17,1.52,1.82]:
		kit.box(root,Vector3(0,y,0),Vector3(0.90,0.028,0.31),wood)
		kit.box(root,Vector3(0,y,0.165),Vector3(0.92,0.04,0.025),edge)
	kit.box(root,Vector3(0,1.87,0),Vector3(1.04,0.08,0.38),edge)
	root.set_meta("shelves_empty",true)
