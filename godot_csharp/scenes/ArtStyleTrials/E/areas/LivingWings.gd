extends RefCounted
## Four living wings. Furniture anchors are supplied by the existing room recipe.
## This module owns neither walls, doors, actor state nor exploration visibility.
const PALETTES = {
	"zone_jia": ["56684e", "9b8153"],
	"zone_yi": ["355e57", "ab8951"],
	"zone_bing": ["496779", "a89d75"],
	"zone_ding": ["65715a", "a2a690"]}

static func furnish(kit: Node3D, entry: Dictionary) -> void:
	var wing: String=entry.map
	var palette: Array=PALETTES[wing]
	var cloth: Material=kit.material("wing_cloth_"+wing,palette[0],3,0.94)
	var trim: Material=kit.material("wing_trim_"+wing,palette[1],1,0.60)
	var occupied := false
	for prop in entry.furniture:
		if prop.kind=="desk_0": occupied=true
	for prop in entry.furniture:
		var at := Vector3(float(prop.at[0]),0,float(prop.at[1]))
		var kind: String=prop.kind
		match kind:
			"bed_0": _bed(kit,at,cloth,trim,occupied)
			"desk_0", "study_table", "worktable", "nightstand":
				var size := Vector2(1.6,0.70)
				if kind=="study_table": size=Vector2(2.2,0.92)
				elif kind=="nightstand": size=Vector2(0.62,0.48)
				_table(kit,at,size,kind,cloth,trim)
			"chair_back", "chair_front": _chair(kit,at,kind=="chair_front",cloth)
			"wardrobe": _wardrobe(kit,at,trim)
			"bookcase_empty": _empty_bookcase(kit,at)
			"washstand": _washstand(kit,at,trim)
			"transmitter": _transmitter(kit,at)
			"range_stove": _range(kit,at)
			"sideboard": _sideboard(kit,at,trim)
			"folding_screen": _screen(kit,at,cloth,trim)
			"music_stand": _music_stand(kit,at)
			"sack": _sack(kit,at)
			"inner_bolt":
				# Door hardware is deliberately deferred to the cognitive door renderer.
				kit.content_root.set_meta("deferred_door_prop",prop.name)
			"score", "slate", "shelf_dust_hand", "ash_fan", "wedge", "jar":
				_small(kit,at,kind,prop.name)
			_: kit._prop(kind,at,prop.name)
	var region: String=String(entry.id).get_slice(":",1)
	var sx: float=float(entry.size[0])
	var sz: float=float(entry.size[1])
	if region=="corridor":
		# Furniture-free passage remains clear; light fittings stay on the interior face.
		for x in [-sx*0.36,-sx*0.12,sx*0.12,sx*0.36]:
			kit._sconce(Vector3(x,2.30,-sz*0.5+0.18),0.0)
	elif region=="hall":
		kit._rug(Vector3(0,0.018,0.1),Vector2(minf(sx-1.0,2.9),minf(sz-1.0,3.5)))
	kit.content_root.set_meta("area_art","living-wings-v1")
	kit.content_root.set_meta("door_geometry_authored",false)

static func _bed(kit: Node3D, at: Vector3, cloth: Material, trim: Material, occupied: bool) -> void:
	var root: Node3D=kit.group("WingBed",at)
	var wood: Material=kit.material("wing_bed_wood","523b2c",1,0.62)
	var linen: Material=kit.material("wing_linen","d7ccb0",3,0.94)
	for x in [-0.56,0.56]:
		for z in [-0.91,0.91]:
			kit._turned_leg(root,Vector3(x,0,z),0.61,wood)
			kit.cylinder(root,Vector3(x,0.87 if z<0 else 0.60,z),0.045,0.76 if z<0 else 0.28,wood)
			kit.ellipsoid(root,Vector3(x,1.28 if z<0 else 0.77,z),Vector3.ONE*0.13,trim)
	kit.box(root,Vector3(0,0.46,0),Vector3(1.19,0.17,1.94),wood)
	kit.box(root,Vector3(0,0.59,0),Vector3(1.13,0.16,1.85),linen)
	kit.box(root,Vector3(0,1.00,-0.93),Vector3(1.13,0.44,0.11),wood)
	for x in [-0.35,0.0,0.35]:
		kit.box(root,Vector3(x,1.00,-0.86),Vector3(0.28,0.31,0.025),trim)
		kit.box(root,Vector3(x,1.00,-0.84),Vector3(0.23,0.26,0.026),wood)
	kit.box(root,Vector3(0,0.64,0.91),Vector3(1.13,0.22,0.10),wood)
	kit.ellipsoid(root,Vector3(0,0.75,-0.66),Vector3(0.92,0.19,0.37),linen)
	kit.box(root,Vector3(0,0.70,0.25),Vector3(1.15,0.045,1.20),cloth if occupied else linen)
	for x in [-0.52,0.52]:
		kit.box(root,Vector3(x,0.728,0.25),Vector3(0.024,0.008,1.18),trim)
		kit.box(root,Vector3(x*1.07,0.56,0.25),Vector3(0.027,0.28,1.18),cloth if occupied else linen)
	kit.box(root,Vector3(0,0.74,-0.30),Vector3(1.15,0.07,0.16),linen)
	if occupied:
		# The blanket fold is geometric, not a generated substitute for hand-painted art.
		kit.ellipsoid(root,Vector3(0,0.76,0.72),Vector3(1.10,0.11,0.23),cloth)

static func _table(kit: Node3D, at: Vector3, size: Vector2, kind: String, cloth: Material, trim: Material) -> void:
	var root: Node3D=kit.group("Wing_"+kind,at)
	var wood: Material=kit.material("wing_desk_wood","65482f",1,0.56)
	var metal: Material=kit.material("wing_brass","ae935c",0,0.42,0.60)
	var h := 0.83 if kind!="nightstand" else 0.64
	for x in [-1,1]:
		for z in [-1,1]: kit._turned_leg(root,Vector3(x*(size.x/2-0.09),0,z*(size.y/2-0.08)),h-0.10,wood)
	kit.box(root,Vector3(0,h-0.11,0),Vector3(size.x-0.08,0.18,size.y-0.06),wood)
	kit.box(root,Vector3(0,h,0),Vector3(size.x+0.04,0.08,size.y+0.04),trim)
	kit.box(root,Vector3(0,h+0.03,0),Vector3(size.x,0.04,size.y),wood)
	kit.box(root,Vector3(0,h-0.11,size.y/2),Vector3(size.x*0.65,0.12,0.025),trim)
	kit.ellipsoid(root,Vector3(0,h-0.11,size.y/2+0.025),Vector3(0.09,0.025,0.035),metal)
	if kind=="worktable":
		kit.box(root,Vector3(0,h+0.067,0),Vector3(size.x*0.66,0.025,size.y*0.65),kit.material("wing_board","b7a078",1))
		kit.cylinder(root,Vector3(0.38,h+0.13,0),0.11,0.14,kit.material("wing_crock","bdac88",2))
	elif kind=="nightstand":
		kit._candlestick(root,Vector3(0,h+0.06,0))
	else:
		kit.box(root,Vector3(0,h+0.06,0),Vector3(size.x*0.72,0.016,size.y*0.55),cloth)
		kit.box(root,Vector3(-size.x*0.20,h+0.09,0),Vector3(0.28,0.05,0.35),kit.material("wing_book","394e42",3))
		kit.box(root,Vector3(0.06,h+0.08,0.04),Vector3(0.25,0.012,0.29),kit.material("wing_paper","dcd1b7",3))
		kit.cylinder(root,Vector3(size.x*0.27,h+0.10,-0.08),0.047,0.08,kit.material("wing_ink","354757",0,0.30))
		kit.limb(root,Vector3(size.x*0.27,h+0.13,-0.08),Vector3(size.x*0.27+0.04,h+0.38,-0.11),0.009,wood)

static func _chair(kit: Node3D, at: Vector3, front: bool, cloth: Material) -> void:
	var root: Node3D=kit.group("WingChair",at)
	if front: root.rotation.y=PI
	var wood: Material=kit.material("wing_chair_wood","4d392b",1)
	for x in [-0.23,0.23]:
		for z in [-0.22,0.22]: kit._turned_leg(root,Vector3(x,0,z),0.47,wood)
	kit.box(root,Vector3(0,0.47,0),Vector3(0.58,0.07,0.56),wood)
	kit.ellipsoid(root,Vector3(0,0.53,0),Vector3(0.52,0.11,0.50),cloth)
	for x in [-0.25,0.25]: kit.cylinder(root,Vector3(x,0.79,-0.23),0.032,0.63,wood)
	kit.box(root,Vector3(0,1.06,-0.23),Vector3(0.58,0.11,0.08),wood)
	for x in [-0.12,0.0,0.12]: kit.cylinder(root,Vector3(x,0.85,-0.23),0.019,0.35,wood)

static func _wardrobe(kit: Node3D, at: Vector3, trim: Material) -> void:
	kit._cabinet(at)
	var root: Node3D=kit.group("WardrobeCrown",at)
	var wood: Material=kit.material("wing_chair_wood","4d392b",1)
	kit.box(root,Vector3(0,1.99,0),Vector3(1.26,0.075,0.59),trim)
	kit.box(root,Vector3(0,2.06,0),Vector3(1.15,0.075,0.49),wood)
	for x in [-0.52,0.52]: kit.box(root,Vector3(x,1.01,0.27),Vector3(0.035,1.72,0.035),trim)

static func _washstand(kit: Node3D, at: Vector3, trim: Material) -> void:
	var root: Node3D=kit.group("WingWashstand",at)
	var ceramic: Material=kit.material("wing_ceramic","ded9c7",0,0.29)
	var wood: Material=kit.material("wing_chair_wood","4d392b",1)
	for x in [-0.43,0.43]:
		for z in [-0.20,0.20]: kit._turned_leg(root,Vector3(x,0,z),0.69,wood)
	kit.box(root,Vector3(0,0.70,0),Vector3(1.06,0.10,0.62),ceramic)
	kit.cylinder(root,Vector3(-0.17,0.79,0.02),0.23,0.09,ceramic,0.26)
	kit.cylinder(root,Vector3(-0.17,0.84,0.02),0.215,0.006,kit.material("wing_basin_water","72948c",0,0.2))
	kit.ellipsoid(root,Vector3(0.29,0.94,-0.09),Vector3(0.22,0.32,0.22),ceramic)
	kit.cylinder(root,Vector3(0.29,1.10,-0.09),0.067,0.10,ceramic,0.09)
	kit.cylinder(root,Vector3(0.29,1.153,-0.09),0.066,0.006,kit.material("wing_jug_interior","8a8e7d",0,0.5))
	for side in [-1,1]:
		kit.cylinder(root,Vector3(side*0.30,1.18,-0.25),0.023,0.91,wood)
	for i in range(8):
		var a: float=-PI/2+i*PI/8
		var b: float=-PI/2+(i+1)*PI/8
		kit.limb(root,Vector3(0.38+cos(a)*0.10,0.96+sin(a)*0.12,-0.09),Vector3(0.38+cos(b)*0.10,0.96+sin(b)*0.12,-0.09),0.017,ceramic)
	kit.box(root,Vector3(0,1.50,-0.25),Vector3(0.65,0.82,0.08),wood)
	kit.box(root,Vector3(0,1.50,-0.20),Vector3(0.56,0.72,0.018),kit.material("wing_mirror","8eaaa3",0,0.13,0.65))
	kit.box(root,Vector3(-0.45,0.54,0.32),Vector3(0.23,0.38,0.025),kit.material("wing_linen","d7ccb0",3))

static func _empty_bookcase(kit: Node3D, at: Vector3) -> void:
	var root: Node3D=kit.group("EmptyBookcase",at)
	var wood: Material=kit.material("wing_chair_wood","4d392b",1)
	var edge: Material=kit.material("wing_empty_shelf_trim","88633d",1)
	kit.box(root,Vector3(0,0.95,-0.17),Vector3(1.10,1.90,0.08),wood)
	for side in [-1,1]: kit.box(root,Vector3(side*0.55,0.99,0),Vector3(0.075,1.98,0.42),wood)
	for y in [0.13,0.48,0.83,1.18,1.53,1.88]:
		kit.box(root,Vector3(0,y,0.02),Vector3(1.10,0.045,0.42),wood)
		kit.box(root,Vector3(0,y,0.24),Vector3(1.13,0.06,0.025),edge)
	kit.box(root,Vector3(0,2.01,0),Vector3(1.25,0.11,0.49),wood)
	root.set_meta("shelves_empty",true)

static func _transmitter(kit: Node3D, at: Vector3) -> void:
	var root: Node3D=kit.group("WingVoicepipe",at)
	var brass: Material=kit.material("wing_brass","ae935c",0,0.42,0.60)
	kit.cylinder(root,Vector3(0,0.72,0),0.045,1.44,brass)
	var bell: MeshInstance3D=kit.cylinder(root,Vector3(0,1.42,0.12),0.15,0.22,brass,0.045)
	bell.rotation.x=PI/2
	kit.cylinder(root,Vector3(0,0.05,0),0.20,0.10,brass)
	kit.box(root,Vector3(0,1.02,0.06),Vector3(0.17,0.13,0.025),kit.material("wing_label","d3c5a2",2))

static func _range(kit: Node3D, at: Vector3) -> void:
	kit._prop("range_stove",at,"WingRange")
	var root: Node3D=kit.group("RangeDetails",at)
	var iron: Material=kit.material("wing_iron","434a47",0,0.60,0.45)
	var brass: Material=kit.material("wing_brass","ae935c",0,0.42,0.60)
	for x in [-0.43,0.43]:
		kit.box(root,Vector3(x,0.48,0.46),Vector3(0.28,0.30,0.03),iron)
		kit.box(root,Vector3(x,0.58,0.49),Vector3(0.16,0.025,0.025),brass)
	kit.cylinder(root,Vector3(-0.40,1.15,0),0.16,0.30,iron)
	kit.cylinder(root,Vector3(-0.40,1.32,0),0.18,0.04,iron)
	kit.ellipsoid(root,Vector3(-0.40,1.36,0),Vector3(0.05,0.05,0.05),brass)

static func _sideboard(kit: Node3D, at: Vector3, trim: Material) -> void:
	var root: Node3D=kit.group("WingSideboard",at)
	var wood: Material=kit.material("wing_desk_wood","65482f",1,0.56)
	kit.box(root,Vector3(0,0.43,0),Vector3(1.45,0.78,0.55),wood)
	kit.box(root,Vector3(0,0.85,0),Vector3(1.52,0.08,0.61),trim)
	for x in [-0.46,0.0,0.46]:
		kit.box(root,Vector3(x,0.48,0.29),Vector3(0.40,0.60,0.03),trim)
		kit.box(root,Vector3(x,0.48,0.32),Vector3(0.34,0.54,0.02),wood)
		kit.ellipsoid(root,Vector3(x,0.60,0.35),Vector3.ONE*0.035,trim)
	for x in [-0.40,0.0,0.40]:
		kit.cylinder(root,Vector3(x,0.94,0),0.11,0.12,kit.material("wing_crock","bdac88",2))
		kit.cylinder(root,Vector3(x,1.002,0),0.084,0.005,kit.material("wing_crock_interior","81735a",2))

static func _screen(kit: Node3D, at: Vector3, cloth: Material, trim: Material) -> void:
	var root: Node3D=kit.group("WingScreen",at)
	for x in [-0.58,0.0,0.58]:
		kit.box(root,Vector3(x,1.00,0),Vector3(0.56,1.90,0.07),trim)
		kit.box(root,Vector3(x,1.08,0.045),Vector3(0.46,1.57,0.025),cloth)
		for z in [-0.17,0.17]: kit.box(root,Vector3(x,0.07,z),Vector3(0.10,0.14,0.35),trim)

static func _music_stand(kit: Node3D, at: Vector3) -> void:
	var root: Node3D=kit.group("WingMusicStand",at)
	var wood: Material=kit.material("wing_chair_wood","4d392b",1)
	kit.cylinder(root,Vector3(0,0.55,0),0.034,1.1,wood)
	for i in range(3):
		var a: float=i*TAU/3.0
		kit.limb(root,Vector3(0,0.16,0),Vector3(cos(a)*0.32,0.035,sin(a)*0.32),0.025,wood)
	var desk: MeshInstance3D=kit.box(root,Vector3(0,1.12,0),Vector3(0.64,0.45,0.035),wood)
	desk.rotation.x=-0.30
	kit.box(root,Vector3(0,0.93,0.08),Vector3(0.67,0.04,0.09),wood)

static func _sack(kit: Node3D, at: Vector3) -> void:
	var root: Node3D=kit.group("WingSack",at)
	var cloth: Material=kit.material("wing_sacking","b5a27c",3)
	kit.ellipsoid(root,Vector3(0,0.35,0),Vector3(0.56,0.70,0.47),cloth)
	kit.cylinder(root,Vector3(0,0.69,0),0.08,0.16,cloth,0.05)
	kit.cylinder(root,Vector3(0,0.68,0),0.087,0.035,kit.material("wing_rope","786544",3))

static func _small(kit: Node3D, at: Vector3, kind: String, title: String) -> void:
	var root: Node3D=kit.group(title,at)
	var paper: Material=kit.material("wing_paper","dcd1b7",3)
	match kind:
		"score":
			kit.box(root,Vector3(0,0.87,0),Vector3(0.32,0.012,0.24),paper)
			root.set_meta("authoring_detail","blank score surface; notation not invented")
		"slate": kit.box(root,Vector3(0,0.90,0),Vector3(0.34,0.03,0.25),kit.material("wing_slate","4b5550",2))
		"jar":
			kit.ellipsoid(root,Vector3(0,1.12,0),Vector3(0.31,0.50,0.31),kit.material("wing_crock","bdac88",2))
			kit.cylinder(root,Vector3(0,1.37,0),0.10,0.04,kit.material("wing_crock","bdac88",2))
		"wedge":
			var wedge: MeshInstance3D=kit.box(root,Vector3(0,0.025,0),Vector3(0.13,0.05,0.24),kit.material("wing_desk_wood","65482f",1))
			wedge.rotation.x=-0.08
		"ash_fan", "shelf_dust_hand":
			# These are author-specific evidence marks. Preserve identity and anchor only;
			# no replacement handprint or directional evidence is fabricated.
			root.set_meta("requires_authored_evidence_surface",kind)
