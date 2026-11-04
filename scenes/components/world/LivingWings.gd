extends RefCounted
const FURNITURE=preload("res://scenes/components/world/YiFurniture.gd")
const PREFABS=preload("res://scenes/components/prefabs/PrefabLibrary.gd")
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
	for prop in entry.furniture:
		var at := Vector3(float(prop.at[0]),0,float(prop.at[1]))
		var kind: String=prop.kind
		if FURNITURE.handles(kind):
			FURNITURE.furnish(kit,kind,at,prop.name,{"cloth":palette[0]})
			continue
		if kind=="inner_bolt":
			kit.content_root.set_meta("deferred_door_prop",prop.name)
			continue
		var variant: String=wing if kind in ["washstand","sideboard","folding_screen"] else "common"
		PREFABS.place_helper(kit,"wing-detail-v2/"+variant+"/"+kind,at,prop.name,PREFABS.category_for(kind),
			func(): _detail(kit,kind,cloth,trim))
	var region: String=String(entry.id).get_slice(":",1)
	var sx: float=float(entry.size[0])
	var sz: float=float(entry.size[1])
	if region=="corridor":
		# Furniture-free passage remains clear; light fittings stay on the interior face.
		for x in [-sx*0.36,-sx*0.12,sx*0.12,sx*0.36]:
			kit._sconce(Vector3(x,2.30,-sz*0.5+0.18),0.0)
	elif region=="hall":
		kit._rug(Vector3(0,0.018,0.1),Vector2(minf(sx-1.0,2.9),minf(sz-1.0,3.5)))
	kit.content_root.set_meta("area_art","living-wings-shared-prefabs-v2")
	kit.content_root.set_meta("door_geometry_authored",false)

static func _detail(kit: Node3D, kind: String, cloth: Material, trim: Material) -> void:
	match kind:
		"washstand": _washstand(kit,Vector3.ZERO,trim)
		"transmitter": _transmitter(kit,Vector3.ZERO)
		"range_stove": _range(kit,Vector3.ZERO)
		"sideboard": _sideboard(kit,Vector3.ZERO,trim)
		"folding_screen": _screen(kit,Vector3.ZERO,cloth,trim)
		"music_stand": _music_stand(kit,Vector3.ZERO)
		"sack": _sack(kit,Vector3.ZERO)
		"score", "slate", "shelf_dust_hand", "ash_fan", "wedge", "jar": _small(kit,Vector3.ZERO,kind,kind)
		_: kit._prop(kind,Vector3.ZERO,kind)

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
