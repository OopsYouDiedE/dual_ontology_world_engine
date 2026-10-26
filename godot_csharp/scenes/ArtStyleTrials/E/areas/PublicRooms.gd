extends RefCounted
## Public furnishings share packed geometry; caller owns placement and room state.
const PREFABS=preload("res://scenes/ArtStyleTrials/E/prefabs/PrefabLibrary.gd")
const COMMON=preload("res://scenes/ArtStyleTrials/E/areas/YiFurniture.gd")
const MAPS = ["living_hall","central_hall","lecture","archive","resonance","counterweight"]

static func handles(map_id: String) -> bool:
	return map_id in MAPS

static func furnish(kit: Node3D, entry: Dictionary) -> void:
	for prop in entry.furniture:
		if prop.kind=="vault_door": continue
		var at := Vector3(float(prop.at[0]),0,float(prop.at[1]))
		if COMMON.handles(prop.kind):
			COMMON.furnish(kit,prop.kind,at,prop.name)
			continue
		if prop.kind in ["floor_lamp","fireplace"]:
			_furnish_uncached(kit,{"id":"detail","furniture":[prop]})
			continue
		var parent: Node3D=kit.content_root
		var kind: String=prop.kind
		var canonical: String="bookcase_study" if kind=="bookcase_study_b" else ("notice_board" if kind=="timetable_board" else kind)
		var instance: Node3D=PREFABS.instantiate("public/"+canonical,func() -> Node3D:
			var holder := Node3D.new()
			holder.name="Public_"+kind
			kit.content_root=holder
			_furnish_uncached(kit,{"id":"detail","furniture":[{"kind":kind,"at":[0,0],"name":kind}]})
			kit.content_root=parent
			return holder)
		if instance==null: continue
		parent.add_child(instance)
		instance.position=at
		instance.set_meta("source_prop",prop.name)
		instance.set_meta("prefab_kind",kind)
	if entry.id=="living_hall:hall":
		kit._rug(Vector3(-3.5,0.025,-9.65),Vector2(5.5,3.7))
		kit._rug(Vector3(0,0.025,6.1),Vector2(3.9,8.2))
	elif entry.id=="central_hall:central_hall":
		# Actual raised inlay geometry, kept low and away from source furniture feet.
		_ring(kit,kit.content_root,Vector3(0,0.014,0),3.1,0.025,_brass(kit),64)
		_ring(kit,kit.content_root,Vector3(0,0.014,0),3.3,0.025,_brass(kit),64)
	elif entry.id=="resonance:silent":
		# Unfurnished source room: no invented evidence or furniture.
		kit.content_root.set_meta("area_note","Silent room retains its empty source layout")


static func _furnish_uncached(kit: Node3D, entry: Dictionary) -> void:
	for prop in entry.furniture:
		var at := Vector3(float(prop.at[0]),0,float(prop.at[1]))
		var kind: String=prop.kind
		if kind=="vault_door": continue # Current observer's doorway is a separate host concern.
		match kind:
			"long_table":
				var table: Node3D=kit._table(at,Vector2(4.5,1.6),.75)
				kit.box(table,Vector3(0,.22,0),Vector3(4.1,.08,.09),_wood(kit))
				for x in [-1.9,1.9]: kit.box(table,Vector3(x,.22,0),Vector3(.08,.08,1.25),_wood(kit))
			"long_bench": kit._bench(at,4.4)
			"notice_board", "timetable_board":
				kit._notice(at)
				var board: Node3D=kit.content_root.get_child(kit.content_root.get_child_count()-1)
				for x in [-0.56,0.56]:
					kit.box(board,Vector3(x,0.57,0),Vector3(0.075,1.14,0.10),_wood(kit))
					kit.box(board,Vector3(x,0.045,0),Vector3(0.18,0.09,0.52),_wood(kit))
			"chess_table", "upright_piano":
				var before: int=kit.content_root.get_child_count()
				if kind=="chess_table": kit._chess(at)
				else: kit._piano(at)
				# The source already provides explicit chairs/stool; remove helper-added bench.
				for index in range(kit.content_root.get_child_count()-1,before-1,-1):
					var child: Node=kit.content_root.get_child(index)
					if child.get_meta("authoring_label","")=="CarvedBench":
						kit.content_root.remove_child(child)
						child.queue_free()
			"lecture_desk_row":
				var desk := _desk(kit,at,Vector2(4.3,0.82),0.87,prop.name)
				for x in [-1.35,0.0,1.35]:
					_paper(kit,desk,Vector3(x,0.925,0),Vector2(0.38,0.28))
					_ink(kit,desk,Vector3(x+0.28,0.95,-0.18))
				kit._bench(at+Vector3(0,0,0.62),4.0)
			"copy_desk":
				var desk := _desk(kit,at,Vector2(2.25,1.0),0.9,prop.name)
				_paper(kit,desk,Vector3(-0.35,0.96,0),Vector2(0.75,0.53))
				_ink(kit,desk,Vector3(0.25,0.98,-0.22))
				kit.box(desk,Vector3(0.73,1.0,0.1),Vector3(0.35,0.10,0.5),_wood(kit))
				kit._candlestick(desk,Vector3(-0.86,0.96,-0.28))
			"lectern", "music_stand": _stand(kit,at,kind=="lectern",prop.name)
			"blackboard_large": _blackboard(kit,at,prop.name)
			"seal_rack": _seal_rack(kit,at,prop.name)
			"plan_cabinet": _plan_drawers(kit,at,prop.name)
			"counterweight_engine": _counterweight(kit,at,prop.name)
			"tuning_frame": _tuning_frame(kit,at,prop.name)
			"relay_column", "mouthpiece_stand": _relay(kit,at,kind=="relay_column",prop.name)
			"notice_pillar":
				var root: Node3D=kit.group(prop.name,at)
				kit.box(root,Vector3(0,0.13,0),Vector3(0.85,0.26,0.85),_stone(kit))
				kit.cylinder(root,Vector3(0,0.93,0),0.16,1.6,_wood(kit))
				kit.box(root,Vector3(0,1.68,0),Vector3(0.95,0.65,0.12),_wood(kit))
				kit.box(root,Vector3(0,1.69,0.07),Vector3(0.78,0.48,0.02),_paper_mat(kit))
			"basin_row": _basins(kit,at,prop.name)
			"bathtub": _bath(kit,at,prop.name)
			"laundry_tub": _laundry(kit,at,prop.name)
			"water_heater": _heater(kit,at,prop.name)
			"side_table_plain":
				var desk := _desk(kit,at,Vector2(0.85,0.7),0.72,prop.name)
				kit.cylinder(desk,Vector3(0,0.79,0),0.13,0.07,_brass(kit))
				for x in [-0.18,0.18]: kit.cylinder(desk,Vector3(x,0.85,0),0.06,0.12,_paper_mat(kit))
			"chair_front", "chair_back": _chair(kit,at,kind=="chair_front",prop.name)
			_: kit._prop(kind,at,prop.name)

static func _wood(kit: Node3D) -> Material:
	return kit.material("public_walnut","60432f",1,0.62)
static func _brass(kit: Node3D) -> Material:
	return kit.material("public_brass","ae925d",0,0.38,0.65)
static func _iron(kit: Node3D) -> Material:
	return kit.material("public_iron","485251",0,0.65,0.45)
static func _stone(kit: Node3D) -> Material:
	return kit.material("public_stone","77796e",2,0.9)
static func _paper_mat(kit: Node3D) -> Material:
	return kit.material("public_paper","ded3b6",3,0.95)

static func _desk(kit: Node3D, at: Vector3, size: Vector2, height: float, title: String) -> Node3D:
	var root: Node3D=kit.group(title,at)
	var wood := _wood(kit)
	kit.box(root,Vector3(0,height,0),Vector3(size.x,0.09,size.y),wood)
	kit.box(root,Vector3(0,height-0.18,0),Vector3(size.x-0.16,0.25,size.y-0.13),wood)
	for x in [-1,1]:
		for z in [-1,1]: kit._turned_leg(root,Vector3(x*(size.x*0.5-0.15),0,z*(size.y*0.5-0.12)),height-0.08,wood)
	kit.box(root,Vector3(0,0.25,0),Vector3(size.x-0.18,0.07,0.07),wood)
	return root

static func _paper(kit: Node3D, root: Node3D, at: Vector3, size: Vector2) -> void:
	kit.box(root,at,Vector3(size.x,0.012,size.y),_paper_mat(kit))
	var ink: Material=kit.material("public_ink","465154",0)
	for i in range(5): kit.box(root,at+Vector3(-size.x*0.05,0.008,(i-2)*size.y*0.13),Vector3(size.x*(0.62 if i%2==0 else 0.74),0.004,0.007),ink)

static func _ink(kit: Node3D, root: Node3D, at: Vector3) -> void:
	kit.cylinder(root,at,0.05,0.09,kit.material("ink_glass","354f64",0,0.22))
	kit.limb(root,at+Vector3(0,0.02,0),at+Vector3(0.12,0.23,-0.06),0.008,_wood(kit))

static func _stand(kit: Node3D, at: Vector3, lectern: bool, title: String) -> void:
	var root: Node3D=kit.group(title,at)
	var width := 1.1 if lectern else 0.68
	kit.cylinder(root,Vector3(0,0.56,0),0.09 if lectern else 0.035,1.1,_wood(kit))
	for angle in [0.0,TAU/3,TAU*2/3]: kit.limb(root,Vector3.ZERO,Vector3(cos(angle)*0.34,0.04,sin(angle)*0.34),0.035,_wood(kit))
	var top: Node3D=Node3D.new()
	top.position=Vector3(0,1.12,0)
	top.rotation.x=-0.25
	root.add_child(top)
	kit.box(top,Vector3.ZERO,Vector3(width,0.06,0.55),_wood(kit))
	kit.box(top,Vector3(0,0.055,0.26),Vector3(width,0.06,0.035),_brass(kit))
	_paper(kit,top,Vector3(0,0.04,0),Vector2(width*0.72,0.4))

static func _blackboard(kit: Node3D, at: Vector3, title: String) -> void:
	var root: Node3D=kit.group(title,at)
	kit.box(root,Vector3(0,1.6,0),Vector3(5.5,2.1,0.16),_wood(kit))
	kit.box(root,Vector3(0,1.6,0.095),Vector3(5.25,1.86,0.025),kit.material("chalkboard","294c43",2))
	for x in [-2.4,2.4]: kit.box(root,Vector3(x,0.55,0),Vector3(0.12,1.1,0.16),_wood(kit))
	kit.box(root,Vector3(0,0.56,0.20),Vector3(5.5,0.08,0.27),_wood(kit))
	# Abstract chalk strokes, no fabricated narrative or formula content.
	for i in range(6): kit.box(root,Vector3(-0.7,2.15-i*0.22,0.114),Vector3(2.4+0.2*(i%3),0.014,0.007),_paper_mat(kit))
	for i in range(3): kit.box(root,Vector3(1.8+i*0.11,0.61,0.23),Vector3(0.08,0.02,0.03),_paper_mat(kit))

static func _seal_rack(kit: Node3D, at: Vector3, title: String) -> void:
	var root: Node3D=kit.group(title,at)
	for x in [-0.78,0.78]: kit.box(root,Vector3(x,1.12,0),Vector3(0.10,2.24,0.50),_wood(kit))
	for row in range(4):
		var y: float=0.22+row*0.52
		kit.box(root,Vector3(0,y,0),Vector3(1.65,0.075,0.56),_wood(kit))
		for col in range(3):
			var x: float=(col-1)*0.49
			kit.box(root,Vector3(x,y+0.21,0),Vector3(0.42,0.36,0.42),kit.material("archive_boxes","526558",3))
			kit.box(root,Vector3(x,y+0.2,0.22),Vector3(0.16,0.09,0.012),_brass(kit))
	kit.box(root,Vector3(0,2.3,0),Vector3(1.77,0.12,0.61),_wood(kit))

static func _plan_drawers(kit: Node3D, at: Vector3, title: String) -> void:
	var root: Node3D=kit.group(title,at)
	kit.box(root,Vector3(0,0.7,0),Vector3(1.9,1.4,0.85),_wood(kit))
	for row in range(6):
		var y: float=0.18+row*0.21
		kit.box(root,Vector3(0,y,0.44),Vector3(1.73,0.17,0.04),kit.material("drawer_face","80603f",1))
		for x in [-0.50,0.50]: kit.box(root,Vector3(x,y,0.48),Vector3(0.17,0.025,0.04),_brass(kit))

static func _ring(kit: Node3D, root: Node3D, at: Vector3, radius: float, thickness: float, mat: Material, count: int=32) -> void:
	for i in range(count):
		var a: float=TAU*i/count
		var b: float=TAU*(i+1)/count
		kit.limb(root,at+Vector3(cos(a)*radius,0,sin(a)*radius),at+Vector3(cos(b)*radius,0,sin(b)*radius),thickness,mat)

static func _counterweight(kit: Node3D, at: Vector3, title: String) -> void:
	var root: Node3D=kit.group(title,at)
	kit.box(root,Vector3(0,0.16,0),Vector3(3.8,0.32,2.6),_stone(kit))
	for x in [-1.35,1.35]:
		kit.box(root,Vector3(x,1.7,0),Vector3(0.18,3.2,0.24),_iron(kit))
		kit.box(root,Vector3(x,0.38,0),Vector3(0.62,0.12,0.70),_iron(kit))
	kit.box(root,Vector3(0,3.40,-0.08),Vector3(3.05,0.16,0.24),_iron(kit))
	for x in [-0.78,0.78]:
		kit.cylinder(root,Vector3(x,1.35,0),0.32,0.90,_iron(kit))
		kit.limb(root,Vector3(x,1.8,0),Vector3(x,3.0,0),0.025,_brass(kit))
		var wheel := Node3D.new()
		root.add_child(wheel)
		wheel.position=Vector3(x,3.03,0.18)
		wheel.rotation.x=PI/2
		_ring(kit,wheel,Vector3.ZERO,0.27,0.035,_brass(kit))
		for angle in [0.0,PI/2]: kit.limb(wheel,Vector3(cos(angle)*-0.27,0,sin(angle)*-0.27),Vector3(cos(angle)*0.27,0,sin(angle)*0.27),0.025,_brass(kit))

static func _tuning_frame(kit: Node3D, at: Vector3, title: String) -> void:
	var root: Node3D=kit.group(title,at)
	for x in [-0.85,0.85]:
		kit.cylinder(root,Vector3(x,1.15,0),0.065,2.3,_brass(kit))
		kit.box(root,Vector3(x,0.08,0),Vector3(0.45,0.16,0.55),_wood(kit))
	kit.box(root,Vector3(0,2.25,0),Vector3(1.9,0.12,0.18),_wood(kit))
	for i in range(9):
		var h: float=1.6-i*0.11
		kit.cylinder(root,Vector3(-0.66+i*0.165,2.14-h/2,0),0.035,h,_brass(kit))

static func _relay(kit: Node3D, at: Vector3, central: bool, title: String) -> void:
	var root: Node3D=kit.group(title,at)
	var h := 2.8 if central else 1.4
	kit.cylinder(root,Vector3(0,0.15,0),0.55 if central else 0.30,0.30,_stone(kit))
	kit.cylinder(root,Vector3(0,h/2,0),0.22 if central else 0.075,h,_brass(kit))
	for side in [-1,1]:
		kit.limb(root,Vector3(0,h*0.68,0),Vector3(side*0.50,h*0.70,0.10),0.07,_brass(kit))
		var horn: MeshInstance3D=kit.cylinder(root,Vector3(side*0.50,h*0.70,0.17),0.15,0.22,_brass(kit),0.07)
		horn.rotation.x=PI/2
	if central:
		_ring(kit,root,Vector3(0,h-0.25,0),0.42,0.035,_brass(kit))
		kit.ellipsoid(root,Vector3(0,h,0),Vector3(0.39,0.39,0.39),_brass(kit))

static func _chair(kit: Node3D, at: Vector3, reverse: bool, title: String) -> void:
	var root: Node3D=kit.group(title,at)
	if reverse: root.rotation.y=PI
	for x in [-0.23,0.23]:
		for z in [-0.22,0.22]: kit.cylinder(root,Vector3(x,0.25,z),0.035,0.5,_wood(kit))
	kit.box(root,Vector3(0,0.51,0),Vector3(0.57,0.09,0.55),_wood(kit))
	for x in [-0.23,0.23]: kit.cylinder(root,Vector3(x,0.87,-0.23),0.033,0.7,_wood(kit))
	for y in [0.83,1.1]: kit.box(root,Vector3(0,y,-0.23),Vector3(0.52,0.10,0.06),_wood(kit))

static func _basins(kit: Node3D, at: Vector3, title: String) -> void:
	var root: Node3D=kit.group(title,at)
	var ceramic: Material=kit.material("public_ceramic","d9dbcd",0,0.25)
	kit.box(root,Vector3(0,0.78,0),Vector3(3.6,0.16,0.8),_stone(kit))
	for x in [-1.2,0.0,1.2]:
		kit.cylinder(root,Vector3(x,0.35,0),0.10,0.7,ceramic)
		kit.ellipsoid(root,Vector3(x,0.87,0),Vector3(0.9,0.20,0.64),ceramic)
		kit.ellipsoid(root,Vector3(x,0.96,0),Vector3(0.65,0.025,0.43),kit.material("basin_shadow","82978c",0,0.3))
		kit.limb(root,Vector3(x,0.89,-0.29),Vector3(x,1.16,-0.29),0.025,_brass(kit))
		kit.limb(root,Vector3(x,1.16,-0.29),Vector3(x,1.16,-0.09),0.025,_brass(kit))

static func _heater(kit: Node3D, at: Vector3, title: String) -> void:
	var root: Node3D=kit.group(title,at)
	kit.cylinder(root,Vector3(0,1.1,0),0.42,1.9,_brass(kit))
	for y in [0.30,1.85]: kit.cylinder(root,Vector3(0,y,0),0.45,0.065,_iron(kit))
	for x in [-0.32,0.32]: kit.cylinder(root,Vector3(x,0.16,0),0.045,0.32,_iron(kit))
	kit.limb(root,Vector3(0.36,1.75,0),Vector3(0.65,1.75,0),0.045,_brass(kit))
	kit.limb(root,Vector3(0.65,1.75,0),Vector3(0.65,0.25,0),0.045,_brass(kit))

static func _bath(kit: Node3D, at: Vector3, title: String) -> void:
	var root: Node3D=kit.group(title,at)
	var shell := Node3D.new()
	shell.position.y=0.12
	root.add_child(shell)
	kit._tub_shell(shell,kit.material("public_ceramic","d9dbcd",0,0.25))
	for x in [-0.65,0.65]:
		for z in [-0.32,0.32]:
			kit.ellipsoid(root,Vector3(x,0.22,z),Vector3(0.14,0.44,0.16),_brass(kit))
	kit.limb(root,Vector3(0.80,0,-0.40),Vector3(0.80,1.15,-0.40),0.028,_brass(kit))
	kit.limb(root,Vector3(0.80,1.15,-0.40),Vector3(0.65,1.15,-0.16),0.028,_brass(kit))
	for x in [0.70,0.90]:
		kit.limb(root,Vector3(x,0.99,-0.40),Vector3(x,1.07,-0.40),0.015,_brass(kit))
		kit.limb(root,Vector3(x-0.05,1.07,-0.40),Vector3(x+0.05,1.07,-0.40),0.013,_brass(kit))

static func _laundry(kit: Node3D, at: Vector3, title: String) -> void:
	var root: Node3D=kit.group(title,at)
	var wood: Material=kit.material("laundry_staves","92704a",1)
	kit.cylinder(root,Vector3(0,0.20,0),0.40,0.40,wood)
	for i in range(24):
		var a: float=TAU*i/24
		var stave: MeshInstance3D=kit.box(root,Vector3(cos(a)*0.38,0.40,sin(a)*0.38),Vector3(0.10,0.42,0.045),wood)
		stave.rotation.y=PI/2-a
	kit.cylinder(root,Vector3(0,0.415,0),0.345,0.012,kit.material("laundry_inside","665c48",2))
	for y in [0.15,0.53]: _ring(kit,root,Vector3(0,y,0),0.41,0.025,_iron(kit),32)
