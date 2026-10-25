extends RefCounted
## Utility rooms share construction quality, not the salon's flower arrangements.
## All anchors are supplied by the production-derived art layout.

static func furnish(kit: Node3D, entry: Dictionary) -> void:
	for prop in entry.furniture:
		var at := Vector3(float(prop.at[0]), 0, float(prop.at[1]))
		var kind: String = prop.kind
		match kind:
			"kitchen_range": _range(kit, at, prop.name)
			"hood_chimney": _hood(kit, at, prop.name)
			"worktable": _prep_table(kit, at, prop.name)
			"long_table": _dining_table(kit, at, prop.name)
			"long_bench": kit._bench(at, 4.5)
			"ration_counter": _counter(kit, at, prop.name)
			"ledger_stand": _ledger(kit, at, prop.name)
			"dish_rack": _rack(kit, at, prop.name, false)
			"medicine_cabinet", "reagent_cabinet": _rack(kit, at, prop.name, true)
			"ward_bed": _ward_bed(kit, at, prop.name)
			"doctor_desk": _doctor_desk(kit, at, prop.name)
			"folding_screen": _screen(kit, at, prop.name)
			"wash_trough": _trough(kit, at, prop.name)
			"granary_bin": _grain_bin(kit, at, prop.name)
			"sack": _sacks(kit, at, prop.name)
			"barrel": _barrel(kit, at, prop.name)
			"crate": _crate(kit, at, prop.name)
			"planter_bed": _planter(kit, at, prop.name)
			"seedling_pots": _pots(kit, at, prop.name)
			"garden_tools": _tools(kit, at, prop.name)
			"hand_pump", "valve_station": _pump(kit, at, prop.name, kind == "valve_station")
			_: kit._prop(kind, at, prop.name)

static func _mat(kit: Node3D, key: String, color: String, kind: float=0.0, rough: float=0.75, metal: float=0.0) -> Material:
	return kit.material("service_" + key, color, kind, rough, metal)

static func _table(kit: Node3D, at: Vector3, title: String, size: Vector2, height: float=0.85) -> Node3D:
	var root: Node3D = kit.group(title, at)
	var wood: Material = _mat(kit,"oak","86613d",1,0.62)
	var dark: Material = _mat(kit,"darkwood","503c2c",1)
	for x in [-1.0,1.0]:
		for z in [-1.0,1.0]:
			kit.box(root,Vector3(x*(size.x*0.5-0.18),height*0.46,z*(size.y*0.5-0.16)),Vector3(0.12,height*0.92,0.12),dark)
	kit.box(root,Vector3(0,height-0.14,0),Vector3(size.x-0.1,0.18,size.y-0.12),dark)
	kit.box(root,Vector3(0,height,0),Vector3(size.x,0.10,size.y),wood)
	# Separate broad planks, narrow joints, and a mortised lower stretcher.
	for z in [-0.25,0.0,0.25]:
		if absf(z)<size.y*0.4: kit.box(root,Vector3(0,height+0.052,z),Vector3(size.x-0.04,0.003,0.008),dark)
	kit.box(root,Vector3(0,0.22,0),Vector3(size.x-0.28,0.10,0.10),dark)
	return root

static func _bowl(kit: Node3D, root: Node3D, at: Vector3, radius: float=0.15) -> void:
	var ceramic: Material = _mat(kit,"ceramic","d9d4bc",0,0.33)
	kit.cylinder(root,at+Vector3(0,0.055,0),radius*0.68,0.11,ceramic,radius)
	kit.cylinder(root,at+Vector3(0,0.113,0),radius*0.81,0.005,_mat(kit,"bowl_inside","999d88",0,0.52))
	_ring(kit,root,at+Vector3(0,0.115,0),radius*0.92,0.018,ceramic)

static func _ring(kit: Node3D, root: Node3D, at: Vector3, radius: float, thickness: float, mat: Material) -> MeshInstance3D:
	var mesh := MeshInstance3D.new()
	var torus := TorusMesh.new()
	torus.inner_radius=radius-thickness
	torus.outer_radius=radius+thickness
	torus.rings=24
	torus.ring_segments=12
	mesh.mesh=torus
	mesh.material_override=mat
	mesh.position=at
	root.add_child(mesh)
	return mesh

static func _prep_table(kit: Node3D, at: Vector3, title: String) -> void:
	var root: Node3D = _table(kit,at,title,Vector2(3.5,1.25))
	kit.box(root,Vector3(-0.55,0.92,0),Vector3(1.3,0.04,0.75),_mat(kit,"board","b39767",1))
	kit.cylinder(root,Vector3(0.55,0.96,-0.22),0.26,0.10,_mat(kit,"tray","c4b28d",1))
	_bowl(kit,root,Vector3(1.12,0.92,0.12),0.23)
	var rolling: MeshInstance3D = kit.cylinder(root,Vector3(-0.6,1.0,0.12),0.035,0.8,_mat(kit,"pin","c3a674",1))
	rolling.rotation.z=PI/2
	for i in range(7):
		kit.ellipsoid(root,Vector3(-0.93+(i%4)*0.23,0.96,-0.21+(i/4)*0.2),Vector3(0.17,0.035,0.15),_mat(kit,"dough","ded1a9",3))
	kit.box(root,Vector3(0.1,0.31,0),Vector3(0.7,0.16,0.7),_mat(kit,"folded_cloth","bac1a6",3))

static func _dining_table(kit: Node3D, at: Vector3, title: String) -> void:
	var root: Node3D = _table(kit,at,title,Vector2(4.8,1.25))
	var plate: Material = _mat(kit,"ceramic","d9d4bc",0,0.33)
	for x in [-1.65,-0.55,0.55,1.65]:
		for z in [-0.43,0.43]:
			kit.cylinder(root,Vector3(x,0.913,z),0.18,0.022,plate)
			_bowl(kit,root,Vector3(x,0.929,z),0.105)
			kit.box(root,Vector3(x+0.25,0.925,z),Vector3(0.025,0.012,0.24),_mat(kit,"cutlery","a8aaa2",0,0.3,0.6))
	kit.cylinder(root,Vector3(0,0.925,0),0.29,0.04,plate)
	kit.cylinder(root,Vector3(0.65,1.02,0),0.085,0.24,_mat(kit,"jug","788a74",0,0.38),0.07)

static func _range(kit: Node3D, at: Vector3, title: String) -> void:
	var root: Node3D = kit.group(title,at)
	var iron: Material = _mat(kit,"iron","3f4845",0,0.6,0.45)
	var brass: Material = _mat(kit,"brass","b0955d",0,0.4,0.7)
	kit.box(root,Vector3(0,0.51,0),Vector3(2.9,1.02,1.1),iron)
	kit.box(root,Vector3(0,1.05,0),Vector3(3.03,0.09,1.2),iron)
	for x in [-1.0,0.0,1.0]:
		kit.box(root,Vector3(x,0.54,0.57),Vector3(0.82,0.65,0.04),_mat(kit,"ovenface","555d54",0,0.6,0.5))
		kit.box(root,Vector3(x,0.77,0.62),Vector3(0.40,0.035,0.045),brass)
		for z in [-0.3,0.3]: _ring(kit,root,Vector3(x,1.105,z),0.25,0.024,iron)
	for x in [-0.85,0.85]:
		kit.cylinder(root,Vector3(x,1.26,0),0.27,0.30,_mat(kit,"copper","98714d",0,0.4,0.6))
		kit.cylinder(root,Vector3(x,1.42,0),0.29,0.025,iron)
		kit.ellipsoid(root,Vector3(x,1.46,0),Vector3(0.08,0.06,0.08),iron)

static func _hood(kit: Node3D, at: Vector3, title: String) -> void:
	var anchor: Node3D = kit.group(title,at)
	var root := Node3D.new()
	root.name="CanopyOverRange"
	# The 2D source's high hood foot is behind the stove's floor anchor.
	# Preserve that source anchor; position the real canopy over its cooktop.
	root.position.z=0.75
	anchor.add_child(root)
	var iron: Material = _mat(kit,"hood","5b6258",0,0.72,0.25)
	kit.box(root,Vector3(0,2.65,0),Vector3(3.2,0.20,1.4),iron)
	kit.box(root,Vector3(0,2.96,-0.20),Vector3(2.5,0.43,0.86),iron)
	kit.box(root,Vector3(0,3.48,-0.36),Vector3(0.65,0.7,0.55),iron)
	for x in [-1.35,1.35]:
		for z in [-0.45,0.45]: kit.cylinder(root,Vector3(x,3.29,z),0.018,1.1,iron)
	for x in [-1.3,-0.8,-0.3,0.3,0.8,1.3]:
		kit.ellipsoid(root,Vector3(x,2.65,0.72),Vector3.ONE*0.045,_mat(kit,"rivets","a29470",0,0.45,0.5))

static func _rack(kit: Node3D, at: Vector3, title: String, medical: bool) -> void:
	var root: Node3D = kit.group(title,at)
	var wood: Material = _mat(kit,"cabinet","6a513a",1)
	var width := 1.7 if medical else 2.0
	kit.box(root,Vector3(0,1.2,-0.25),Vector3(width,2.4,0.12),wood)
	for x in [-width/2,width/2]: kit.box(root,Vector3(x,1.2,0),Vector3(0.10,2.4,0.65),wood)
	for y in [0.18,0.77,1.36,1.95,2.42]:
		kit.box(root,Vector3(0,y,0.02),Vector3(width+0.12,0.09,0.72),wood)
	for row in range(3):
		for col in range(5):
			var p := Vector3((col-2)*(width/5.5),0.84+row*0.59,0.10)
			if medical:
				var bottle: Material = _mat(kit,"bottle"+str(col%2),"596f58" if col%2 else "826640",0,0.3)
				kit.cylinder(root,p+Vector3(0,0.11,0),0.075,0.22,bottle)
				kit.cylinder(root,p+Vector3(0,0.25,0),0.034,0.07,_mat(kit,"cork","a28b63",1))
				kit.box(root,p+Vector3(0,0.12,0.075),Vector3(0.10,0.10,0.008),_mat(kit,"label","ddd4b8",3))
			else:
				var dish: MeshInstance3D = kit.cylinder(root,p+Vector3(0,0.16,0),0.20,0.035,_mat(kit,"ceramic","d9d4bc",0,0.33))
				dish.rotation.x=PI/2
	kit.box(root,Vector3(0,0.45,0.33),Vector3(width-0.08,0.44,0.07),wood)

static func _ward_bed(kit: Node3D, at: Vector3, title: String) -> void:
	var root: Node3D = kit.group(title,at)
	var iron: Material = _mat(kit,"bediron","b9bfb1",0,0.5,0.35)
	for x in [-0.60,0.60]:
		for z in [-1.12,1.12]:
			kit.cylinder(root,Vector3(x,0.61,z),0.035,1.22,iron)
			kit.ellipsoid(root,Vector3(x,1.23,z),Vector3.ONE*0.09,iron)
	for z in [-1.12,1.12]:
		kit.limb(root,Vector3(-0.60,1.10,z),Vector3(0.60,1.10,z),0.028,iron)
		for x in [-0.4,-0.2,0.0,0.2,0.4]: kit.cylinder(root,Vector3(x,0.85,z),0.018,0.48,iron)
	kit.box(root,Vector3(0,0.56,0),Vector3(1.23,0.13,2.25),iron)
	kit.box(root,Vector3(0,0.71,0),Vector3(1.18,0.19,2.18),_mat(kit,"linen","d9d7c5",3))
	kit.box(root,Vector3(0,0.83,0.38),Vector3(1.19,0.045,1.35),_mat(kit,"blanket","778e80",3))
	kit.ellipsoid(root,Vector3(0,0.88,-0.78),Vector3(0.93,0.18,0.42),_mat(kit,"linen","d9d7c5",3))
	for x in [-0.48,0.48]: kit.box(root,Vector3(x,0.855,0.38),Vector3(0.025,0.008,1.3),_mat(kit,"blanket_edge","bbc6af",3))

static func _doctor_desk(kit: Node3D, at: Vector3, title: String) -> void:
	var root: Node3D = _table(kit,at,title,Vector2(2.1,1.0))
	kit.box(root,Vector3(-0.4,0.92,0),Vector3(0.55,0.035,0.65),_mat(kit,"paper","ded6bd",3))
	kit.box(root,Vector3(0.40,0.94,0.1),Vector3(0.5,0.10,0.3),_mat(kit,"instrument_case","566451",1))
	_bowl(kit,root,Vector3(0.72,0.92,-0.25),0.13)

static func _screen(kit: Node3D, at: Vector3, title: String) -> void:
	var root: Node3D = kit.group(title,at)
	var iron: Material = _mat(kit,"bediron","b9bfb1",0,0.5,0.35)
	for index in range(3):
		var panel := Node3D.new()
		panel.position.x=(index-1)*0.73
		panel.rotation.y=0.12 if index%2 else -0.12
		root.add_child(panel)
		for x in [-0.35,0.35]: kit.cylinder(panel,Vector3(x,0.94,0),0.025,1.88,iron)
		kit.box(panel,Vector3(0,1.1,0),Vector3(0.64,1.47,0.025),_mat(kit,"screen","b8c1a9",3))
		for x in [-0.35,0.35]: kit.box(panel,Vector3(x,0.04,0),Vector3(0.08,0.08,0.48),iron)

static func _trough(kit: Node3D, at: Vector3, title: String) -> void:
	var root: Node3D = kit.group(title,at)
	var ceramic: Material = _mat(kit,"ceramic","d9d4bc",0,0.33)
	kit.box(root,Vector3(0,0.76,0),Vector3(2.2,0.20,0.8),ceramic)
	kit.box(root,Vector3(0,0.87,0),Vector3(1.95,0.01,0.57),_mat(kit,"basin","6c8b84",0,0.25))
	for x in [-0.9,0.9]: kit.box(root,Vector3(x,0.37,0),Vector3(0.12,0.74,0.6),ceramic)
	for x in [-0.65,0.65]:
		kit.cylinder(root,Vector3(x,1.01,-0.30),0.026,0.38,_mat(kit,"brass","b0955d",0,0.4,0.7))
		kit.limb(root,Vector3(x,1.17,-0.3),Vector3(x,1.17,-0.08),0.025,_mat(kit,"brass","b0955d",0,0.4,0.7))

static func _counter(kit: Node3D, at: Vector3, title: String) -> void:
	var root: Node3D = _table(kit,at,title,Vector2(2.1,1.2),1.0)
	kit.box(root,Vector3(0,0.50,0.49),Vector3(1.9,0.85,0.08),_mat(kit,"darkwood","503c2c",1))
	for x in [-0.55,0.55]: _bowl(kit,root,Vector3(x,1.06,0),0.25)

static func _ledger(kit: Node3D, at: Vector3, title: String) -> void:
	var root: Node3D = _table(kit,at,title,Vector2(0.8,0.6),0.94)
	kit.box(root,Vector3(0,1.02,0),Vector3(0.55,0.06,0.42),_mat(kit,"ledger_cover","42594d",3))
	kit.box(root,Vector3(0,1.055,0),Vector3(0.48,0.014,0.37),_mat(kit,"paper","ded6bd",3))

static func _crate(kit: Node3D, at: Vector3, title: String) -> void:
	var root: Node3D = kit.group(title,at)
	var wood: Material = _mat(kit,"crate","957448",1)
	kit.box(root,Vector3(0,0.48,0),Vector3(1.0,0.96,0.85),wood)
	for y in [0.16,0.36,0.56,0.76]: kit.box(root,Vector3(0,y,0.43),Vector3(0.98,0.015,0.012),_mat(kit,"darkwood","503c2c",1))
	for x in [-0.43,0.43]: kit.box(root,Vector3(x,0.48,0.46),Vector3(0.09,0.94,0.07),wood)

static func _barrel(kit: Node3D, at: Vector3, title: String) -> void:
	var root: Node3D = kit.group(title,at)
	var wood: Material = _mat(kit,"crate","957448",1)
	kit.cylinder(root,Vector3(0,0.50,0),0.40,1.0,wood,0.36)
	for y in [0.12,0.48,0.87]: _ring(kit,root,Vector3(0,y,0),0.40-y*0.04,0.028,_mat(kit,"iron","3f4845",0,0.6,0.45))
	for i in range(12):
		var a: float=i*TAU/12.0
		kit.limb(root,Vector3(sin(a)*0.40,0.04,cos(a)*0.40),Vector3(sin(a)*0.36,0.96,cos(a)*0.36),0.007,_mat(kit,"darkwood","503c2c",1))

static func _sacks(kit: Node3D, at: Vector3, title: String) -> void:
	var root: Node3D = kit.group(title,at)
	var fabric: Material = _mat(kit,"burlap","b1a17a",3)
	for i in range(3):
		var p := Vector3((i-1)*0.32,0.35,0.15 if i%2 else -0.1)
		kit.ellipsoid(root,p,Vector3(0.60,0.68,0.49),fabric)
		kit.cylinder(root,p+Vector3(0,0.36,0),0.08,0.14,fabric,0.055)
		_ring(kit,root,p+Vector3(0,0.37,0),0.077,0.012,_mat(kit,"rope","756243",3))
		kit.limb(root,p+Vector3(0.07,0.37,0),p+Vector3(0.15,0.17,0.10),0.009,_mat(kit,"rope","756243",3))

static func _grain_bin(kit: Node3D, at: Vector3, title: String) -> void:
	var root: Node3D = kit.group(title,at)
	var wood: Material = _mat(kit,"crate","957448",1)
	for x in [-1.05,1.05]: kit.box(root,Vector3(x,0.75,0),Vector3(0.12,1.50,1.5),wood)
	for z in [-0.71,0.71]: kit.box(root,Vector3(0,0.75,z),Vector3(2.2,1.5,0.09),wood)
	kit.box(root,Vector3(0,1.36,0),Vector3(2.08,0.10,1.38),_mat(kit,"grain","bda16a",3))
	for x in [-0.9,-0.45,0.0,0.45,0.9]: kit.box(root,Vector3(x,0.75,0.77),Vector3(0.055,1.52,0.055),_mat(kit,"darkwood","503c2c",1))
	for i in range(120):
		var gx: float=-0.95+fposmod(i*0.618034,1.0)*1.9
		var gz: float=-0.61+fposmod(i*0.414214,1.0)*1.22
		var kernel: MeshInstance3D=kit.ellipsoid(root,Vector3(gx,1.425,gz),Vector3(0.045,0.022,0.075),_mat(kit,"grain_high"+str(i%2),"c7ae78" if i%2 else "aa905e",3))
		kernel.rotation.y=i*2.39996

static func _planter(kit: Node3D, at: Vector3, title: String) -> void:
	var root: Node3D = kit.group(title,at)
	var wood: Material = _mat(kit,"planter","735b3e",1)
	kit.box(root,Vector3(0,0.53,0),Vector3(4.2,0.85,1.45),wood)
	kit.box(root,Vector3(0,0.97,0),Vector3(4.0,0.025,1.25),_mat(kit,"soil","514638",2))
	for x in [-1.95,1.95]: kit.box(root,Vector3(x,0.51,0),Vector3(0.09,1.02,1.56),wood)
	for row in range(2):
		for col in range(8):
			var p := Vector3(-1.7+col*0.48,0.99,-0.36+row*0.68)
			var stem_height: float=0.37+0.08*((col+row)%3)
			kit.cylinder(root,p+Vector3(0,stem_height/2,0),0.013,stem_height,_mat(kit,"stem","596b3d",3))
			for level in range(2):
				for side in [-1,1]:
					var leaf: MeshInstance3D=kit.ellipsoid(root,p+Vector3(side*0.08,0.18+level*0.15,level*0.04),Vector3(0.30-level*0.05,0.06,0.18),_mat(kit,"leaf"+str(col%2),"68824d" if col%2 else "4c6b45",3))
					leaf.rotation.z=side*(0.35+level*0.15)
					leaf.rotation.y=0.24*(col%3-1)

static func _pots(kit: Node3D, at: Vector3, title: String) -> void:
	var root: Node3D = _table(kit,at,title,Vector2(1.5,0.85),0.75)
	for i in range(6):
		var p := Vector3((i%3-1)*0.42,0.86,-0.22+(i/3)*0.4)
		kit.cylinder(root,p,0.10,0.19,_mat(kit,"terracotta","a46c4c",2),0.14)
		kit.ellipsoid(root,p+Vector3(0,0.19,0),Vector3(0.23,0.25,0.22),_mat(kit,"leaf","68824d",3))

static func _tools(kit: Node3D, at: Vector3, title: String) -> void:
	var root: Node3D = kit.group(title,at)
	for i in range(3):
		kit.limb(root,Vector3(i*0.20-0.2,0.12,0.12),Vector3(i*0.2-0.28,1.65,-0.08),0.024,_mat(kit,"toolwood","a08051",1))
		kit.box(root,Vector3(i*0.2-0.2,0.12,0.12),Vector3(0.17,0.25,0.04),_mat(kit,"iron","3f4845",0,0.6,0.45))
	kit.box(root,Vector3(0,0.7,-0.12),Vector3(0.85,0.09,0.16),_mat(kit,"toolwood","a08051",1))

static func _pump(kit: Node3D, at: Vector3, title: String, valve: bool) -> void:
	var root: Node3D = kit.group(title,at)
	var metal: Material = _mat(kit,"pump","466d62",0,0.6,0.45)
	kit.cylinder(root,Vector3(0,0.60,0),0.12,1.20,metal)
	kit.cylinder(root,Vector3(0,0.08,0),0.30,0.16,metal)
	kit.limb(root,Vector3(0,0.94,0),Vector3(0,0.94,0.44),0.065,metal)
	if valve:
		var wheel: MeshInstance3D = _ring(kit,root,Vector3(0,1.31,0),0.31,0.033,_mat(kit,"brass","b0955d",0,0.4,0.7))
		wheel.rotation.x=PI/2
		for i in range(4):
			var a: float=i*PI/2
			kit.limb(root,Vector3(0,1.31,0),Vector3(cos(a)*0.30,1.31+sin(a)*0.30,0),0.018,metal)
	else:
		kit.limb(root,Vector3(0,1.20,0),Vector3(-0.46,1.48,-0.07),0.035,metal)
		kit.limb(root,Vector3(-0.46,1.48,-0.07),Vector3(-0.65,1.20,-0.07),0.032,metal)
