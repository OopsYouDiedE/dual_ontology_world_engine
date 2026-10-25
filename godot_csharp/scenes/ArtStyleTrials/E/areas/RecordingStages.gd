extends RefCounted
## Six recording sets. Static art only: no lines, action cues or outcomes are executed.
const IDS = ["shura_poet","scholar_relic","chess_god","demon_miracle","kitchen_dumplings","last_transfer"]

static func build(kit: Node3D, id: String) -> Dictionary:
	if not id in IDS: return {}
	kit.current_room_id=id
	match id:
		"shura_poet": _gallery(kit)
		"scholar_relic": _relic(kit)
		"chess_god": _chess(kit)
		"demon_miracle": _altar(kit)
		"kitchen_dumplings": _kitchen(kit)
		"last_transfer": _transfer(kit)
	kit.content_root.set_meta("recording_set",id)
	kit.content_root.set_meta("static_art_only",true)
	return {"size":Vector2(14,10),"camera_size":16.0 if id=="shura_poet" else 14.5,
		"target":Vector3(0,0.65,-0.4),"actors_are_static":true,
		"doors":false,"wall_faces":"interior-only",
		"source":"Contracts/highlight_stages.json and Highlights.gd current stage composition"}

static func _mat(k: Node3D, key: String, color: String, kind: float=0.0, rough: float=0.8, metal: float=0.0) -> Material:
	return k.material("recording_"+key,color,kind,rough,metal)

static func _floor(k: Node3D, size: Vector2, stone: bool=false) -> void:
	var root: Node3D=k.content_root
	var mat: Material=_mat(k,"stone" if stone else "wood","555957" if stone else "725039",2 if stone else 1)
	if not stone:
		var parquet := ShaderMaterial.new()
		parquet.shader=preload("res://scenes/ArtStyleTrials/E/parquet.gdshader")
		k.box(root,Vector3(0,-0.08,0),Vector3(size.x,0.15,size.y),parquet)
	else:
		for x in range(int(size.x)):
			for z in range(int(size.y)):
				k.box(root,Vector3(x-size.x/2+0.5,-0.08,z-size.y/2+0.5),Vector3(0.985,0.15,0.985),mat)
	for side in [-1,1]:
		k.box(root,Vector3(side*(size.x/2-0.12),0.005,0),Vector3(0.045,0.012,size.y-0.1),_mat(k,"inlay","a48c60",1))

static func _wall_plane(k: Node3D, x: float, z: float, width: float, height: float=3.8) -> void:
	var mesh := MeshInstance3D.new()
	var quad := QuadMesh.new()
	quad.size=Vector2(width,height)
	mesh.mesh=quad
	mesh.position=Vector3(x,height/2,z)
	mesh.material_override=_mat(k,"plaster","c7bca4",2)
	k.content_root.add_child(mesh)
	for y in [0.12,1.05,3.63]:
		k.box(k.content_root,Vector3(x,y,z+0.04),Vector3(width,0.075,0.08),_mat(k,"walnut","49392d",1))
	for px in range(int(width)):
		k.box(k.content_root,Vector3(x-width/2+px+0.5,0.56,z+0.025),Vector3(0.91,0.88,0.025),_mat(k,"panel","654a34",1))

static func _back_with_window(k: Node3D) -> void:
	_wall_plane(k,-3.25,-4.8,4.5)
	_wall_plane(k,3.25,-4.8,4.5)
	k._window(Vector3(0,0,-4.8))

static func _rail(k: Node3D, at: Vector3, span: float) -> void:
	var root: Node3D=k.group("RivetedGalleryRailing",at)
	var iron: Material=_mat(k,"iron","4a5559",0,0.56,0.55)
	for x in range(int(span/0.36)+1):
		var px: float=x*0.36-span/2
		k.cylinder(root,Vector3(px,0.45,0),0.018,0.9,iron)
		k.ellipsoid(root,Vector3(px,0.86,0),Vector3.ONE*0.065,iron)
	for y in [0.16,0.95]: k.box(root,Vector3(0,y,0),Vector3(span,0.045,0.055),iron)

static func _plain_table(k: Node3D, at: Vector3, size: Vector2, height: float=0.9) -> Node3D:
	var root: Node3D=k.group("PurposeBuiltTable",at)
	var wood: Material=_mat(k,"table","715236",1)
	for x in [-1,1]:
		for z in [-1,1]: k._turned_leg(root,Vector3(x*(size.x/2-0.16),0,z*(size.y/2-0.14)),height,wood)
	k.box(root,Vector3(0,height,0),Vector3(size.x,0.10,size.y),wood)
	k.box(root,Vector3(0,height-0.16,0),Vector3(size.x-0.12,0.20,size.y-0.1),wood)
	return root

static func _actor(k: Node3D, id: String, x: float, z: float, yaw: float=0.0) -> void:
	k._character(id,x,z,id,1.0)
	var root: Node3D=k.figures.back()
	root.rotation.y=yaw
	root.set_meta("recording_static_pose",true)

static func _gallery(k: Node3D) -> void:
	_floor(k,Vector2(14,6),true)
	var stone: Material=_mat(k,"gallery_stone","9a998d",2)
	var iron: Material=_mat(k,"iron","4a5559",0,0.56,0.55)
	for x in [-6.3,-3.15,0.0,3.15,6.3]:
		k.box(k.content_root,Vector3(x,1.75,-2.85),Vector3(0.26,3.5,0.32),stone)
		k.box(k.content_root,Vector3(x,3.45,-2.85),Vector3(0.5,0.18,0.48),stone)
		k.limb(k.content_root,Vector3(x-1.4,2.65,-2.86),Vector3(x,3.35,-2.86),0.055,iron)
		k.limb(k.content_root,Vector3(x+1.4,2.65,-2.86),Vector3(x,3.35,-2.86),0.055,iron)
	k.box(k.content_root,Vector3(0,3.48,-2.85),Vector3(14,0.20,0.30),iron)
	k.box(k.content_root,Vector3(0,2.63,-2.86),Vector3(14,0.10,0.16),iron)
	_rail(k,Vector3(0,0,2.8),13.6)
	_rail(k,Vector3(0,0,-2.8),13.6)
	for x in [-4.6,4.6]: k._lamp(Vector3(x,0,-1.7))
	_actor(k,"detective",-2.3,0.6,PI/2)
	_actor(k,"poet",2.0,0.6,-PI/2)
	_actor(k,"thief",4.0,0.1,-PI/2)
	var sword: Node3D=k.group("CeremonialSword_PropOnly",Vector3(1.6,0.65,0.65))
	k.box(sword,Vector3(0,0.38,0),Vector3(0.035,0.78,0.015),_mat(k,"blade","aeb8b6",0,0.25,0.85))
	k.box(sword,Vector3(0,0,0),Vector3(0.24,0.03,0.035),_mat(k,"brass","b89758",0,0.4,0.65))
	sword.rotation.z=-1.0

static func _basalt(k: Node3D) -> void:
	var rock: Material=_mat(k,"basalt","404946",2)
	for i in range(15):
		var height: float=1.3+fmod(float(i*7),6.0)*0.33
		var mesh: MeshInstance3D=k.cylinder(k.content_root,Vector3(-6.6+i*0.94,height/2,-4.4),0.64,height,rock)
		var shape := CylinderMesh.new()
		shape.top_radius=0.64
		shape.bottom_radius=0.68
		shape.height=height
		shape.radial_segments=6
		mesh.mesh=shape

static func _relic(k: Node3D) -> void:
	_floor(k,Vector2(12,10),true)
	_basalt(k)
	var obsidian: Material=_mat(k,"obsidian","262d31",0,0.28,0.18)
	k.box(k.content_root,Vector3(0,0.18,-1.7),Vector3(3.5,0.36,1.7),_mat(k,"stone","555957",2))
	k.box(k.content_root,Vector3(0,1.85,-1.9),Vector3(2.4,3.2,0.48),obsidian)
	# Shallow carved bands represent worn inscription texture, not readable invented text.
	for row in range(12):
		for col in range(9):
			if (col+row)%4==0: continue
			k.box(k.content_root,Vector3(-0.95+col*0.23,0.65+row*0.22,-1.647),Vector3(0.09,0.035,0.008),_mat(k,"stone_groove","676e67",2))
	_actor(k,"archaeologist",1.35,0.7)
	_actor(k,"detective",-1.4,3.0)
	k._prop("hand_pump",Vector3(4,0,-2.7),"ExistingWatercoursePump")
	k._add_light(Vector3(-3,2.4,-1.0),1.2,6.0)

static func _demon(k: Node3D, at: Vector3) -> void:
	# Separate non-student stage stand-in; it never changes the sixteen-body template.
	var root: Node3D=k.group("DemonStageFigure",at)
	var suit: Material=_mat(k,"demon_suit","0e0c10",3)
	var skin: Material=_mat(k,"demon_skin","c9c5b6",0)
	for side in [-1,1]:
		k.limb(root,Vector3(side*0.10,0.12,0),Vector3(side*0.10,0.48,0),0.07,suit)
		k.ellipsoid(root,Vector3(side*0.1,0.07,0.05),Vector3(0.16,0.14,0.26),suit)
		k.limb(root,Vector3(side*0.18,0.78,0),Vector3(side*0.23,0.43,0.08),0.06,suit)
		k.ellipsoid(root,Vector3(side*0.23,0.42,0.08),Vector3(0.09,0.11,0.09),skin)
	k.ellipsoid(root,Vector3(0,0.62,0),Vector3(0.38,0.47,0.25),suit)
	k.ellipsoid(root,Vector3(0,1.09,0),Vector3(0.40,0.44,0.34),skin)
	k.ellipsoid(root,Vector3(0,1.27,-0.02),Vector3(0.42,0.18,0.35),suit)
	for side in [-1,1]: k.box(root,Vector3(side*0.075,1.1,0.17),Vector3(0.014,0.055,0.015),_mat(k,"demon_eye","d9a13a",0))
	k.box(root,Vector3(0,0.76,0.14),Vector3(0.14,0.17,0.025),_mat(k,"shirt","e2ddcf",3))
	root.set_meta("recording_static_pose",true)
	root.set_meta("not_registered_student",true)

static func _chess(k: Node3D) -> void:
	_floor(k,Vector2(12,10))
	_back_with_window(k)
	k._rug(Vector3(0,0.02,0),Vector2(4.0,3.0))
	var table: Node3D=_plain_table(k,Vector3(0,0,0),Vector2(1.8,1.7),0.72)
	var board: Material=_mat(k,"go_wood","b39a65",1)
	k.box(table,Vector3(0,0.80,0),Vector3(1.3,0.07,1.3),board)
	var line: Material=_mat(k,"go_ink","3d342b",0)
	for i in range(19):
		var p: float=-0.57+i*1.14/18.0
		k.box(table,Vector3(p,0.84,0),Vector3(0.006,0.005,1.14),line)
		k.box(table,Vector3(0,0.84,p),Vector3(1.14,0.005,0.006),line)
	for x in [-0.38,0.0,0.38]:
		for z in [-0.38,0.0,0.38]: k.cylinder(table,Vector3(x,0.844,z),0.015,0.006,line)
	for side in [-1,1]:
		k.cylinder(table,Vector3(side*0.77,0.85,0.35),0.13,0.15,_mat(k,"go_bowl","65482e",1))
	_actor(k,"chessplayer",-1.3,0.1,PI/2)
	_demon(k,Vector3(1.3,0,0.1))
	k.content_root.get_child(k.content_root.get_child_count()-1).rotation.y=-PI/2
	_actor(k,"detective",0,3.1)
	k._lamp(Vector3(-4,0,-2.8))
	k._plant(Vector3(4.5,0,-3.8))
	k._prop("folding_screen",Vector3(-3,0,-3.6),"TeaScreen")

static func _altar(k: Node3D) -> void:
	_floor(k,Vector2(12,10),true)
	_basalt(k)
	var stone: Material=_mat(k,"altar_stone","747b73",2)
	for step in range(3): k.box(k.content_root,Vector3(0,0.06+step*0.08,-1.0),Vector3(6-step*0.5,0.12,4-step*0.5),stone)
	for x in [-3.3,3.3]:
		for z in [-2.6,2.6]:
			var root: Node3D=k.group("ClosedAnchorPedestal",Vector3(x,0,z))
			k.cylinder(root,Vector3(0,0.3,0),0.43,0.6,stone)
			var iron: Material=_mat(k,"iron","4a5559",0,0.56,0.55)
			k.cylinder(root,Vector3(0,0.83,0),0.055,0.7,iron)
			k.box(root,Vector3(0,0.95,0),Vector3(0.54,0.055,0.07),iron)
			for side in [-1,1]: k.limb(root,Vector3(0,0.49,0),Vector3(side*0.27,0.68,0),0.05,iron)
	_demon(k,Vector3(0,0.3,-0.9))
	_actor(k,"detective",0,3.5)
	var table: Node3D=_plain_table(k,Vector3(1.5,0.3,-0.5),Vector2(0.7,0.6),0.65)
	k.cylinder(table,Vector3(0,0.75,0),0.065,0.12,_mat(k,"cup","dfd7bf",0,0.28))
	# Static clear-air staging; no poison tide or miracle outcome is simulated.
	k._add_light(Vector3(0,3.5,-1.0),1.5,7.0)

static func _kitchen(k: Node3D) -> void:
	_floor(k,Vector2(12,10),true)
	_back_with_window(k)
	var table: Node3D=_plain_table(k,Vector3(0,0,0.3),Vector2(4.3,1.5),0.86)
	var dough: Material=_mat(k,"dough","e3d6b3",0)
	for row in range(3):
		for col in range(7):
			var at := Vector3(-1.35+col*0.31,0.96,-0.36+row*0.3)
			k.ellipsoid(table,at,Vector3(0.21,0.085,0.11),dough)
			for fold in range(5):
				var ridge := Vector3((fold-2)*0.035,0.045,0.012-0.005*abs(fold-2))
				k.ellipsoid(table,at+ridge,Vector3(0.045,0.04,0.035),dough)
	k.cylinder(table,Vector3(1.55,0.99,0),0.25,0.18,_mat(k,"bowl","9a8058",2))
	k.ellipsoid(table,Vector3(1.55,1.08,0),Vector3(0.44,0.04,0.44),_mat(k,"filling","8e8b60",2))
	k._prop("kitchen_range",Vector3(3.7,0,-3.1),"Cookstove")
	k._prop("crate",Vector3(-3.8,0,-3.4),"FlourStock")
	_sack(k,Vector3(-3.0,0,-3.2))
	k._cabinet(Vector3(-4.7,0,-1.4))
	var copper: Material=_mat(k,"copper_pans","9c6f43",0,0.42,0.7)
	var iron: Material=_mat(k,"iron","4a5559",0,0.56,0.55)
	k.box(k.content_root,Vector3(3.4,2.4,-4.55),Vector3(3.1,0.055,0.08),iron)
	for i in range(4):
		var x: float=2.3+i*0.7
		var pan: MeshInstance3D=k.cylinder(k.content_root,Vector3(x,1.95,-4.43),0.23,0.07,copper)
		pan.rotation_degrees.x=90
		k.box(k.content_root,Vector3(x,2.25,-4.42),Vector3(0.055,0.33,0.07),copper)
	var prep: Node3D=_plain_table(k,Vector3(-3.3,0,-4.0),Vector2(2.5,0.65),0.85)
	for i in range(4): k.cylinder(prep,Vector3(-0.8+i*0.48,0.97,0),0.14,0.17,_mat(k,"crock","aaa186",2))
	k.box(prep,Vector3(0.5,0.92,0),Vector3(0.65,0.035,0.36),_mat(k,"linen","d3c8a9",3))
	_actor(k,"chef",0,-1.0)
	_actor(k,"musician",-2.8,0.3,PI/2)
	_actor(k,"thief",2.8,0.3,-PI/2)
	_actor(k,"detective",0,3.3)
	# The contract's window apparition remains a named anchor, not a new corporeal student.
	var apparition := Marker3D.new()
	apparition.name="DemonWindowApparitionAnchor"
	apparition.position=Vector3(0,2,-5)
	k.content_root.add_child(apparition)
	k._add_light(Vector3(0,3.3,0),1.5,7.0)

static func _transfer(k: Node3D) -> void:
	_floor(k,Vector2(12,10),true)
	_wall_plane(k,0,-4.8,12)
	var wood: Material=_mat(k,"deck","725039",1)
	k.box(k.content_root,Vector3(2.9,0.7,-2.7),Vector3(5.3,1.4,3.5),wood)
	for step in range(7): k.box(k.content_root,Vector3(4.7,0.10+step*0.2,1.4-step*0.43),Vector3(1.15,0.2,0.46),wood)
	_rail(k,Vector3(2.2,1.4,-0.95),3.7)
	var iron: Material=_mat(k,"iron","4a5559",0,0.56,0.55)
	for x in [0.8,2.5]: k.box(k.content_root,Vector3(x,2.1,-2.0),Vector3(0.14,4.2,0.18),iron)
	k.box(k.content_root,Vector3(1.65,4.1,-2.0),Vector3(2.2,0.19,0.23),iron)
	for x in [1.05,2.25]:
		k.cylinder(k.content_root,Vector3(x,2.5,-1.8),0.023,3.0,_mat(k,"rope","b7a57b",3))
		var wheel: MeshInstance3D=k.cylinder(k.content_root,Vector3(x,3.9,-1.8),0.19,0.10,iron)
		wheel.rotation_degrees.x=90
	k.box(k.content_root,Vector3(1.65,1.02,-1.8),Vector3(1.6,0.12,1.1),wood)
	_seedling_tray(k,Vector3(1.65,1.1,-1.8))
	for at in [Vector3(-4,0,-3.4),Vector3(-3,0,-3.4),Vector3(-4,0.8,-3.4)]: k._prop("crate",at,"GrainCrate")
	for at in [Vector3(-3,0,1.4),Vector3(-1.0,0,1.4)]: _sack(k,at)
	_actor(k,"chef",-3,2.8)
	_actor(k,"detective",-1,2.8)
	k._add_light(Vector3(-2,3.3,-1),1.5,7.0)

static func _sack(k: Node3D, at: Vector3) -> void:
	var root: Node3D=k.group("TiedLinenGrainSack",at)
	var cloth: Material=_mat(k,"grain_linen","b2a17e",3)
	k.ellipsoid(root,Vector3(0,0.34,0),Vector3(0.67,0.72,0.50),cloth)
	k.ellipsoid(root,Vector3(0,0.71,0),Vector3(0.22,0.18,0.18),cloth)
	k.cylinder(root,Vector3(0,0.66,0),0.115,0.035,_mat(k,"rope","b7a57b",3))
	for side in [-1,1]: k.limb(root,Vector3(side*0.24,0.10,0.15),Vector3(side*0.08,0.64,0.11),0.009,_mat(k,"sack_seam","877657",3))

static func _seedling_tray(k: Node3D, at: Vector3) -> void:
	var root: Node3D=k.group("YoungSeedlingTray",at)
	var wood: Material=_mat(k,"crate","85613d",1)
	k.box(root,Vector3(0,0.07,0),Vector3(1.42,0.14,0.94),wood)
	k.box(root,Vector3(0,0.16,0),Vector3(1.30,0.06,0.82),_mat(k,"soil","4f4937",2))
	for side in [-1,1]:
		k.box(root,Vector3(side*0.68,0.17,0),Vector3(0.055,0.24,0.96),wood)
		k.box(root,Vector3(0,0.17,side*0.45),Vector3(1.4,0.24,0.055),wood)
	var green: Material=_mat(k,"seedling_green","587346",3)
	for row in range(3):
		for col in range(5):
			var p := Vector3(-0.52+col*0.26,0.20,-0.28+row*0.28)
			k.limb(root,p,p+Vector3(0,0.2,0),0.009,green)
			for side in [-1,1]:
				var leaf: MeshInstance3D=k.ellipsoid(root,p+Vector3(side*0.065,0.14,0),Vector3(0.15,0.035,0.07),green)
				leaf.rotation.z=side*0.5
