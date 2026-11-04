extends RefCounted
## Authoring layout in metres. Runtime navigation remains owned by its formal contract.
const ARCH=preload("res://scenes/components/architecture/YiArchitecture.gd")
const PUBLIC=preload("res://scenes/components/world/PublicRooms.gd")
const WIDTH := 9.2
const HALL_LENGTH := 14.72
const BATH_LENGTH := 3.68
const PLATFORM_DEPTH := 4.60
const SPOTS={
 "Fireplace":Vector2(-1.65,-6.76),"BookcaseW":Vector2(-4.18,-5.90),"BookcaseE":Vector2(4.18,-5.90),
 "NoticeBoard":Vector2(1.85,-6.76),"ArmchairA":Vector2(-1.80,-4.80),"ArmchairB":Vector2(.05,-4.80),
 "FireSide":Vector2(-.88,-4.70),"ReadingLamp":Vector2(2.55,-4.50),"ShoeBenchN":Vector2(2.4,-3.45),
 "CoatStandN":Vector2(3.72,-3.70),"BenchN":Vector2(0,.10),"LongTable":Vector2(0,1.40),
 "BenchS":Vector2(0,2.70),"LampW":Vector2(-3.65,.65),"Piano":Vector2(3.25,4.45),
 "PianoStool":Vector2(3.25,5.25),"Chess":Vector2(-2.75,4.8),"ChessChairA":Vector2(-2.75,3.95),
 "ChessChairB":Vector2(-2.75,5.70),"Plant":Vector2(4.0,3.25),
 "Basins":Vector2(-2.25,-1.20),"Tub":Vector2(1.55,.20),"Heater":Vector2(3.77,-1.13),"LaundryTub":Vector2(-3.30,.85)}

static func build(kit: Node3D, catalog: Array, selected: String) -> Vector2:
	var overview := selected=="living_hall:overview"
	var result := Vector2(WIDTH,HALL_LENGTH+BATH_LENGTH)
	for entry in catalog:
		if entry.map!="living_hall" or (not overview and entry.id!=selected): continue
		var bath: bool=entry.id=="living_hall:bath"
		var dims := Vector2(WIDTH,BATH_LENGTH if bath else HALL_LENGTH)
		var root := Node3D.new()
		root.name="PublicBath" if bath else "PublicSalon"
		kit.add_child(root)
		if overview: root.position.z=HALL_LENGTH/2 if bath else -BATH_LENGTH/2
		kit.content_root=root
		kit.current_room_id=entry.id
		kit.rooms[entry.id]=root
		root.set_meta("room_dimensions_m",dims)
		root.set_meta("scope","reference layout; not production collision geometry")
		_walls(kit,dims,bath)
		var floor_mat := ShaderMaterial.new()
		floor_mat.shader=preload("res://assets/materials/surfaces/tile_floor.gdshader") if bath else preload("res://assets/materials/surfaces/parquet.gdshader")
		if not bath:
			floor_mat.set_shader_parameter("wood_value",.65)
			floor_mat.set_shader_parameter("finish_roughness",.35)
		if bath:
			floor_mat.set_shader_parameter("base_color",Color("c9c3aa"))
			floor_mat.set_shader_parameter("diamond_insets",true)
		kit.box(root,Vector3(0,-.07,0),Vector3(dims.x,.14,dims.y),floor_mat)
		ARCH._solid_box(root,"floor",Vector3(0,-.07,0),Vector3(dims.x,.14,dims.y))
		if not bath:
			kit.box(root,Vector3(0,.225,-HALL_LENGTH/2+PLATFORM_DEPTH/2),Vector3(WIDTH,.45,PLATFORM_DEPTH),floor_mat)
			ARCH._solid_box(root,"floor",Vector3(0,.225,-HALL_LENGTH/2+PLATFORM_DEPTH/2),Vector3(WIDTH,.45,PLATFORM_DEPTH))
			for step in range(3):
				var h: float=.45-step*.15
				var z: float=-HALL_LENGTH/2+PLATFORM_DEPTH+.15+step*.30
				kit.box(root,Vector3(0,h/2,z),Vector3(WIDTH,h,.30),kit.material("salon_stair_walnut","543b29",1,.38))
				ARCH._solid_box(root,"floor",Vector3(0,h/2,z),Vector3(WIDTH,h,.30))
				kit.box(root,Vector3(0,h+.008,z+.137),Vector3(WIDTH,.025,.045),kit.material("salon_nosing","805d36",1,.40))
			kit._rug(Vector3(-.9,.475,-4.65),Vector2(3.65,2.40))
			kit._rug(Vector3(0,.022,2.70),Vector2(1.65,7.70))
		else: kit._rug(Vector3(1.55,.022,1.05),Vector2(1.45,.65))
		for prop in entry.furniture:
			var at: Vector2=SPOTS.get(prop.name,Vector2.ZERO)
			var first: int=root.get_child_count()
			PUBLIC.furnish(kit,{"id":"public_redesign:detail","furniture":[{"name":prop.name,"kind":prop.kind,"at":[at.x,at.y]}]})
			for i in range(first,root.get_child_count()):
				var item: Node3D=root.get_child(i)
				item.set_meta("source_prop",prop.name)
				if not bath and at.y < -HALL_LENGTH/2+PLATFORM_DEPTH: item.position.y+=.45
				if prop.name=="BookcaseW": item.rotation.y=PI/2
				elif prop.name=="BookcaseE": item.rotation.y=-PI/2
				elif prop.name=="ArmchairA": item.rotation.y=.14
				elif prop.name=="ArmchairB": item.rotation.y=-.14
		if bath:
			_mirror(kit,Vector3(-2.25,1.70,-BATH_LENGTH/2+.16),Vector2(3.55,.9))
			for x in [-4.1,-.38,3.2]: kit._sconce(Vector3(x,2.23,-dims.y/2+.18),0)
		else:
			for x in [-2.70,2.70]: ARCH.window(kit,Vector3(x,.45,-dims.y/2+.12))
			for x in [-.75,.75]: kit._sconce(Vector3(x,2.55,-dims.y/2+.18),0)
			for z in [-3.8,.1,4.3]:
				kit._sconce(Vector3(-WIDTH/2+.16,2.25,z),PI/2)
				kit._sconce(Vector3(WIDTH/2-.16,2.25,z),-PI/2)
		if not overview: result=dims
	return result

static func _walls(kit: Node3D, dims: Vector2, bath: bool) -> void:
	# Every room owns all four inner faces. The paired hall/bath wall overlaps exactly.
	var shell := Node3D.new()
	shell.name="OwnedInteriorWalls"
	shell.set_meta("outside_faces",false)
	shell.set_meta("room_owner",kit.current_room_id)
	kit.content_root.add_child(shell)
	for side in ["north","south","west","east"]:
		var wall := Node3D.new()
		wall.name=side.capitalize()+"Interior"
		var length: float=dims.x if side in ["north","south"] else dims.y
		var height: float=1.05 if side=="south" else 3.20
		match side:
			"north": wall.position=Vector3(0,0,-dims.y/2)
			"south":
				wall.position=Vector3(0,0,dims.y/2)
				wall.rotation.y=PI
			"west":
				wall.position=Vector3(-dims.x/2,0,0)
				wall.rotation.y=PI/2
			"east":
				wall.position=Vector3(dims.x/2,0,0)
				wall.rotation.y=-PI/2
		shell.add_child(wall)
		if side=="north" and not bath:
			# Actual window voids, sill 1.30m above the raised salon platform.
			for span in [[-length/2,-3.30],[-2.10,-.80],[.80,2.10],[3.30,length/2]]:
				ARCH._span(kit,wall,span[0],span[1],height)
			for x in [-2.70,2.70]:
				ARCH._span(kit,wall,x-.60,x+.60,1.75)
				ARCH._open_box(wall,Vector3(x,3.10,0),Vector3(1.20,.20,.22),kit.material("salon_cream","c5baa0",2,.85))
				ARCH._solid_box(wall,"wall",Vector3(x,3.10,0),Vector3(1.20,.20,.22))
				ARCH._crown(kit,wall,x,1.2,height)
			ARCH._open_box(wall,Vector3(0,2.925,0),Vector3(1.6,.55,.22),kit.material("salon_cream","c5baa0",2,.85))
			ARCH._solid_box(wall,"wall",Vector3(0,2.925,0),Vector3(1.6,.55,.22))
			ARCH._crown(kit,wall,0,1.6,height)
		elif side in ["north","south"]:
			ARCH._span(kit,wall,-length/2,-.80,height)
			ARCH._span(kit,wall,.80,length/2,height)
			if height>2.1:
				ARCH._open_box(wall,Vector3(0,(height+2.1)/2,0),Vector3(1.6,height-2.1,.22),kit.material("salon_cream","c5baa0",2,.85))
				ARCH._solid_box(wall,"wall",Vector3(0,(height+2.1)/2,0),Vector3(1.6,height-2.1,.22))
				ARCH._crown(kit,wall,0,1.6,height)
		else:
			var openings: Dictionary = preload("res://scenes/components/world/WorldPortalLayout.gd").openings(str(kit.current_room_id), dims)
			var cursor := -length/2
			for center in openings.get(side, []):
				var left: float = maxf(-length/2, float(center)-0.60)
				var right: float = minf(length/2, float(center)+0.60)
				if left > cursor: ARCH._span(kit,wall,cursor,left,height)
				if height > 2.1:
					ARCH._open_box(wall,Vector3((left+right)/2,(height+2.1)/2,0),Vector3(right-left,height-2.1,.22),kit.material("salon_cream","c5baa0",2,.85))
					ARCH._solid_box(wall,"wall",Vector3((left+right)/2,(height+2.1)/2,0),Vector3(right-left,height-2.1,.22))
				cursor = maxf(cursor,right)
			if cursor < length/2: ARCH._span(kit,wall,cursor,length/2,height)

static func _mirror(kit: Node3D, at: Vector3, size: Vector2) -> void:
	var root: Node3D=kit.group("WashroomMirror",at)
	var trim: Material=kit.material("salon_mirror_frame","85673b",1,.40)
	kit.box(root,Vector3.ZERO,Vector3(size.x,size.y,.08),trim)
	kit.box(root,Vector3(0,0,.045),Vector3(size.x-.12,size.y-.12,.012),kit.material("salon_mirror","8a9b94",0,.16,.72))
	for x in [-size.x/6,size.x/6]: kit.box(root,Vector3(x,0,.06),Vector3(.035,size.y-.08,.035),trim)
