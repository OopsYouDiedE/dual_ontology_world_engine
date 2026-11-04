extends RefCounted
const ARCH=preload("res://scenes/components/architecture/YiArchitecture.gd")
const FURNITURE=preload("res://scenes/components/world/YiFurniture.gd")
const OLD=preload("res://scenes/components/world/LivingWings.gd")
const PREFABS=preload("res://scenes/components/prefabs/PrefabLibrary.gd")
const INTERIOR_FINISH=preload("res://scenes/components/rendering/InteriorFinish.gd")
const ROOM_EXPANSION := 1.15
## Shared four-wing composition in metres, independent of production navigation.
## Existing recipes retain their furniture identity; repeated joinery uses prefabs.
const RECTS={
 "wc":[0,0,2.4,3.5],"foyer":[0,3.5,2.4,2.5],"store":[0,6,2.4,3.6],
 "hall":[2.4,0,4.4,6],"prep":[2.4,6,4.4,3.6],
 "north_2":[6.8,0,3.4,4],"north_1":[10.2,0,3.4,4],"north_0":[13.6,0,3.4,4],
 "corridor":[6.8,4,10.2,2],
 "south_2":[6.8,6,3.4,3.6],"south_1":[10.2,6,3.4,3.6],"south_0":[13.6,6,3.4,3.6]}

static func build(kit: Node3D, catalog: Array, selected: String) -> Vector2:
	var wing: String=selected.get_slice(":",0)
	var overview := selected.get_slice(":",1)=="overview"
	var palette: Array=OLD.PALETTES[wing]
	var size := Vector2(17,9.6)*ROOM_EXPANSION
	for entry in catalog:
		if entry.map!=wing or (not overview and entry.id!=selected): continue
		var region: String=entry.id.get_slice(":",1)
		var r: Array=RECTS[region]
		var dims := Vector2(r[2],r[3])*ROOM_EXPANSION
		var root := Node3D.new()
		root.name=wing+"_"+region
		kit.add_child(root)
		if overview: root.position=Vector3(r[0]+r[2]/2-8.5,0,r[1]+r[3]/2-4.8)*ROOM_EXPANSION
		kit.content_root=root
		kit.current_room_id=entry.id
		kit.rooms[entry.id]=root
		var openings: Dictionary={}
		if region.begins_with("north_"): openings={"south":[0.0]}
		elif region.begins_with("south_"): openings={"north":[0.0]}
		elif region=="corridor": openings={"north":[-3.4,0.0,3.4],"south":[-3.4,0.0,3.4],"west":[0.0]}
		elif region=="hall": openings={"west":[-(1.75*ROOM_EXPANSION-.40)/ROOM_EXPANSION],"east":[2.0],"south":[0.0]}
		elif region=="foyer": openings={"north":[0.0],"south":[0.0],"east":[-.40/ROOM_EXPANSION],"west":[0.0]}
		elif region in ["store","prep"]: openings={"north":[0.0]}
		elif region=="wc": openings={"south":[0.0]}
		for side in openings:
			for index in range(openings[side].size()): openings[side][index]*=ROOM_EXPANSION
		var formal := preload("res://scenes/components/world/WorldPortalLayout.gd").openings(str(entry.id), dims)
		if not formal.is_empty(): openings = formal
		ARCH.build(kit,dims,openings)
		var mat: Material=INTERIOR_FINISH.floor(entry)
		kit.box(root,Vector3(0,-.065,0),Vector3(dims.x,.13,dims.y),mat)
		ARCH._solid_box(root,"floor",Vector3(0,-.065,0),Vector3(dims.x,.13,dims.y))
		for prop in entry.furniture:
			var at := placement(region,prop,dims,entry.size)
			at = preload("res://scenes/components/world/WorldPortalLayout.gd").prop_position(str(entry.id), str(prop.name), at)
			var first := root.get_child_count()
			if FURNITURE.handles(prop.kind):
				FURNITURE.furnish(kit,prop.kind,at,prop.name,{} if wing=="zone_yi" else {"cloth":palette[0]})
			elif prop.kind=="range_stove":
				PREFABS.place_helper(kit,"wing-range-v1",at,prop.name,PREFABS.category_for(prop.kind),func(): stove(kit,Vector3.ZERO))
			elif prop.kind=="washstand":
				PREFABS.place_helper(kit,"wing-wash-cabinet-v1",at,prop.name,PREFABS.category_for(prop.kind),func(): washstand(kit,Vector3.ZERO))
			elif prop.kind=="bookcase_0":
				PREFABS.place_helper(kit,"wing-low-bookcase-v1",at,prop.name,PREFABS.category_for(prop.kind),func(): low_bookcase(kit,Vector3.ZERO))
			else:
				var item: Dictionary=entry.duplicate(true)
				item.id=wing+":detail"
				item.furniture=[{"kind":prop.kind,"at":[at.x,at.z],"name":prop.name}]
				OLD.furnish(kit,item)
			for i in range(first,root.get_child_count()):
				root.get_child(i).set_meta("source_prop",prop.name)
				if prop.kind in ["score","slate"]: root.get_child(i).position.y-=.10
				if prop.name in ["ZoneRules","ToolBench"]: root.get_child(i).rotation.y=PI/2
				if prop.name in ["InkShelf","MedCabinet","Screen"]: root.get_child(i).rotation.y=-PI/2
				if prop.kind=="bookcase_empty" or (region=="foyer" and prop.kind=="shoe_bench"): root.get_child(i).rotation.y=PI/2
			if prop.kind in ["desk_0","study_table","nightstand","worktable"]:
				var articles: Node3D=PREFABS.place_helper(kit,"wing-table-articles-v1/"+prop.kind,at,"TableArticles",&"decoration",
					func(): dress_table(kit,Vector3.ZERO,prop.kind,false))
				if prop.name=="ToolBench": articles.rotation.y=PI/2
		if region.begins_with("north_") or region.begins_with("south_"):
			rug(kit,Vector3(.18,.015,.3),Vector2(.9,1.5))
			if region.begins_with("north_"): ARCH.window(kit,Vector3(-.5,0,-dims.y/2+.12))
		elif region=="hall":
			rug(kit,Vector3(-.2,.015,.45),Vector2(2.45,2.9))
			ARCH.window(kit,Vector3(0,0,-dims.y/2+.12))
		elif region=="corridor": rug(kit,Vector3(0,.015,.3),Vector2(9.5,.65))
		elif region in ["foyer","store"]: rug(kit,Vector3(0,.015,.15),Vector2(1.2,.65))
		if region.begins_with("north_"):
			kit._sconce(Vector3(-dims.x/2+.18,2.15,0),PI/2)
			kit._sconce(Vector3(dims.x/2-.18,2.15,0),-PI/2)
		else:
			var sconces: Array=[-1.7*ROOM_EXPANSION,1.7*ROOM_EXPANSION] if region=="corridor" else [-dims.x*.34,dims.x*.34]
			for x in sconces: kit._sconce(Vector3(x,2.35,-dims.y/2+.19),0)
		root.set_meta("scope","reference-based 3D redesign; not production collision geometry")
		root.set_meta("room_dimensions_m",dims)
		root.set_meta("source_map",wing)
		if not overview: size=dims
	return size

static func placement(region: String, prop: Dictionary, dims: Vector2, original: Array) -> Vector3:
	var p := Vector3(prop.at[0]*dims.x/float(original[0]),0,prop.at[1]*dims.y/float(original[1]))
	if region.begins_with("north_") or region.begins_with("south_"):
		var slots={"bed_0":Vector2(-.95,-.3),"wardrobe":Vector2(1.0,-dims.y/2+.42),
		 "nightstand":Vector2(-.18,-.9),"desk_0":Vector2(1.04,.4),"chair_back":Vector2(.99,1.08),
		 "coat_stand":Vector2(-.85,1.25),"bookcase_empty":Vector2(-1.43,1.37),
		 "shelf_dust_hand":Vector2(-1.43,1.37),"score":Vector2(1.04,.4),"crate":Vector2(1.0,1.0)}
		if slots.has(prop.kind): p=Vector3(slots[prop.kind].x,0,slots[prop.kind].y)
		if prop.kind=="bed_0": p.x=-dims.x/2+.75
		elif prop.kind in ["desk_0","score","chair_back"]: p.x=dims.x/2-.66
		elif prop.kind=="wardrobe": p.x=dims.x/2-.70
		elif prop.kind in ["bookcase_empty","shelf_dust_hand"]: p.x=-dims.x/2+.27
	elif region=="hall":
		var named={"StudyTable":Vector2(-.20,.50),"StudyChairNW":Vector2(-.7,-.45),"StudyChairNE":Vector2(.35,-.45),
		 "StudyChairSW":Vector2(-.7,1.35),"StudyChairSE":Vector2(.35,1.35),"HallBookcase":Vector2(-1.25,-2.65),
		 "ZoneRules":Vector2(-2.0,-1.5),"ToolBench":Vector2(1.65,-1.0),"Transmitter":Vector2(1.55,-2.55),
		 "HallLamp":Vector2(-1.75,1.9),"Armchair":Vector2(-1.7,-.55),"Slate":Vector2(.2,.5),
		 "InkShelf":Vector2(2.0,-1.15),"Screen":Vector2(1.95,-.95),
		 "MusicStand":Vector2(1.75,.8),"MedCabinet":Vector2(2.0,-1.15)}
		if named.has(prop.name): p=Vector3(named[prop.name].x,0,named[prop.name].y)
	elif region=="prep":
		var slots={"range_stove":Vector2(-1.25,-1.3),"sideboard":Vector2(1.25,-1.3),"worktable":Vector2(0,.5),"barrel":Vector2(-1.7,.8),"jar":Vector2(1.25,-1.3)}
		if slots.has(prop.kind): p=Vector3(slots[prop.kind].x,0,slots[prop.kind].y)
	elif region=="wc": p=Vector3(0,0,-dims.y/2+.45)
	elif region=="foyer" and prop.kind=="shoe_bench": p=Vector3(dims.x/2-.4,0,.64)
	elif region=="foyer" and prop.kind=="coat_stand": p=Vector3(-dims.x/2+.48,0,-dims.y/2+.45)
	return p

static func rug(kit: Node3D, at: Vector3, size: Vector2) -> void:
	var mat: Material=kit.material("plain_woven_rug","28483d",3,.96)
	var root: Node3D=kit.group("YiBoundWovenRug",at)
	root.set_meta("ornament_status","deferred")
	kit.box(root,Vector3.ZERO,Vector3(size.x,.018,size.y),kit.material("yi_carpet_edge","19372f",3))
	var mesh := MeshInstance3D.new()
	var plane := PlaneMesh.new()
	plane.size=size
	mesh.mesh=plane
	mesh.position.y=.010
	mesh.material_override=mat
	root.add_child(mesh)

static func stove(kit: Node3D, at: Vector3) -> void:
	var root: Node3D=kit.group("YiCastIronRange",at)
	var iron: Material=kit.material("yi_range","262d2b",0,.53,.55)
	var brass: Material=kit.material("yi_range_brass","a47b3d",0,.32,.70)
	for x in [-.49,.49]:
		for z in [-.25,.25]: kit.box(root,Vector3(x,.12,z),Vector3(.07,.24,.07),iron)
	kit.box(root,Vector3(0,.48,0),Vector3(1.12,.64,.66),iron)
	kit.box(root,Vector3(0,.83,0),Vector3(1.2,.065,.72),iron)
	for x in [-.31,.31]:
		kit.cylinder(root,Vector3(x,.868,0),.205,.012,brass)
		kit.cylinder(root,Vector3(x,.878,0),.188,.012,iron)
		kit.box(root,Vector3(x,.48,.341),Vector3(.46,.42,.035),brass)
		kit.box(root,Vector3(x,.48,.363),Vector3(.41,.37,.026),iron)
		kit.limb(root,Vector3(x-.09,.58,.40),Vector3(x+.09,.58,.40),.013,brass)
		for y in [.34,.61]: kit.cylinder(root,Vector3(x-.20,y,.39),.019,.06,iron)
	kit.cylinder(root,Vector3(.37,1.74,-.24),.073,1.75,iron)
	for y in [1.0,1.8,2.5]: kit.cylinder(root,Vector3(.37,y,-.24),.084,.035,brass)
	for x in [-.45,-.2,.05]:
		kit.box(root,Vector3(x,.27,.35),Vector3(.12,.014,.012),brass)

static func washstand(kit: Node3D, at: Vector3) -> void:
	var root: Node3D=kit.group("YiMarbleWashCabinet",at)
	var wood: Material=kit.material("yi_wash_wood","513926",1,.47)
	var china: Material=kit.material("yi_china","ded8c5",0,.22)
	for x in [-.43,.43]:
		for z in [-.21,.21]: kit._turned_leg(root,Vector3(x,0,z),.24,wood)
	kit.box(root,Vector3(0,.47,0),Vector3(.96,.50,.53),wood)
	for x in [-.235,.235]:
		kit.box(root,Vector3(x,.48,.28),Vector3(.43,.43,.035),wood)
		kit.box(root,Vector3(x,.48,.304),Vector3(.33,.32,.016),kit.material("yi_panel","735033",1,.48))
		kit.ellipsoid(root,Vector3(x*.35,.57,.335),Vector3(.035,.035,.03),kit.material("yi_knob","af8c4d",0,.3,.7))
	kit.box(root,Vector3(0,.75,0),Vector3(1.05,.06,.62),china)
	# Open basin built as annular ceramic sections, not a solid capped cylinder.
	for i in range(48):
		var a: float=TAU*i/48.0
		var b: float=TAU*(i+1)/48.0
		kit.limb(root,Vector3(cos(a)*.22,.81,sin(a)*.18),Vector3(cos(b)*.22,.81,sin(b)*.18),.025,china)
	kit.ellipsoid(root,Vector3(0,.785,0),Vector3(.40,.035,.31),kit.material("yi_basin","879e91",0,.18))
	kit.box(root,Vector3(0,1.32,-.27),Vector3(.70,.85,.065),wood)
	kit.box(root,Vector3(0,1.32,-.23),Vector3(.60,.75,.015),kit.material("yi_glass","82958c",0,.12,.75))
	for x in [-.63,.63]:
		kit.limb(root,Vector3(x,.75,-.20),Vector3(x,.75,.20),.017,wood)
		kit.limb(root,Vector3(x,.75,-.20),Vector3(x*.7,.75,-.20),.017,wood)

static func low_bookcase(kit: Node3D, at: Vector3) -> void:
	var root: Node3D=kit.group("YiHallLowBookcase",at)
	var wood: Material=kit.material("yi_shelf","513a29",1,.5)
	kit.box(root,Vector3(0,.5,-.16),Vector3(1.35,.9,.035),wood)
	for x in [-.66,.66]: kit.box(root,Vector3(x,.5,0),Vector3(.05,1.0,.4),wood)
	for y in [.12,.52,.95]: kit.box(root,Vector3(0,y,0),Vector3(1.4,.045,.44),wood)
	for row in range(2):
		for i in range(11):
			var h: float=.24+.025*(i%3)
			var book: Material=kit.material("yi_book_"+str(i%3),["45574a","79543c","40545c"][i%3],3)
			kit.box(root,Vector3(-.55+i*.105,.15+row*.4+h/2,.05),Vector3(.065,h,.26),book)

static func dress_table(kit: Node3D, at: Vector3, kind: String, turn: bool) -> void:
	var root: Node3D=kit.group("YiTableArticles",at)
	if turn: root.rotation.y=PI/2
	var height: float=.609 if kind=="nightstand" else .769
	var brass: Material=kit.material("yi_table_brass","a78c52",0,.35,.65)
	if kind!="worktable":
		var x: float=0.0 if kind=="nightstand" else -.30
		kit.cylinder(root,Vector3(x,height+.025,0),.075,.05,brass)
		kit.ellipsoid(root,Vector3(x,height+.095,0),Vector3(.12,.12,.12),brass)
		kit.cylinder(root,Vector3(x,height+.20,0),.029,.12,kit.material("yi_lamp_glass","e0c28b",0,.25),.022)
		kit.cylinder(root,Vector3(x,height+.29,0),.12,.11,kit.material("yi_lamp_shade","cbbb88",3,.86),.055)
		kit._add_light(at+Vector3(x,height+.23,.03),.19,1.8)
	if kind!="nightstand":
		var paper: Material=kit.material("yi_page","cbbf9f",3,.96)
		kit.box(root,Vector3(.22,height+.025,.07),Vector3(.22,.035,.28),kit.material("yi_cover","354a3c",3))
		kit.box(root,Vector3(.22,height+.045,.07),Vector3(.19,.009,.25),paper)
		if kind=="study_table":
			for side in [-1,1]:
				var page: MeshInstance3D=kit.box(root,Vector3(side*.095,height+.015,-.25),Vector3(.18,.012,.22),paper)
				page.rotation.z=side*.06
