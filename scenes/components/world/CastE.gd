extends RefCounted
## Shared anatomical dimensions; only costume, hair and accessories vary.
static var PEOPLE: Dictionary = _load_people()
static var MODELS: Dictionary = _load_models()

static func _load_models() -> Dictionary:
	var reader = preload("res://scenes/main/DvPresentationContent.cs").new()
	var art: Dictionary = reader.ReadRecord("res://assets/data/character_art.dv", "layouts", "character_art")
	return art.get("models", {})


static func _load_people() -> Dictionary:
	var reader = preload("res://scenes/main/DvPresentationContent.cs").new()
	var art: Dictionary = reader.ReadRecord("res://assets/data/character_art.dv", "layouts", "character_art")
	var people := {}
	for identity in art.get("cast", {}):
		var spec: Dictionary = art.cast[identity]
		var character: Dictionary = reader.ReadRecord("res://assets/data/world.dv", "characters", spec.character_id)
		people[identity] = [character.name, spec.cloth, spec.hair, spec.outfit]
	return people

static func _ring(kit: Node3D, parent: Node3D, at: Vector3, radius: float, mat: Material) -> void:
	for i in range(20):
		var a: float=TAU*i/20.0
		var b: float=TAU*(i+1)/20.0
		kit.limb(parent,at+Vector3(cos(a),sin(a),0)*radius,at+Vector3(cos(b),sin(b),0)*radius,0.009,mat)

static func _buttons(kit: Node3D, root: Node3D, mat: Material, x: float=0.0) -> void:
	for y in [0.53,0.63,0.73]: kit.ellipsoid(root,Vector3(x,y,0.151),Vector3(0.022,0.022,0.014),mat)

static func _hair(kit: Node3D, root: Node3D, id: String, mat: Material) -> void:
	# The skull is identical; hair is a removable outer shell, never a head scale.
	kit.ellipsoid(root,Vector3(0,1.255,-0.035),Vector3(0.421,0.19,0.355),mat)
	if id in ["lawyer","marksman","engineer","chef"]:
		for i in range(3):
			var lock: MeshInstance3D=kit.ellipsoid(root,Vector3(-0.10+i*0.09,1.24,0.115),Vector3(0.17,0.07,0.085),mat)
			lock.rotation.z=-0.20
		for side in [-1,1]: kit.ellipsoid(root,Vector3(side*0.18,1.18,-0.015),Vector3(0.055,0.15,0.10),mat)
	elif id in ["musician","chessplayer","chemist","doctor","dancer"]:
		for side in [-1,1]:
			var lock: MeshInstance3D=kit.ellipsoid(root,Vector3(side*0.105,1.235,0.12),Vector3(0.17,0.13,0.09),mat)
			lock.rotation.z=-side*0.35
		var bun_at := Vector3(0,1.28,-0.20)
		if id=="musician": bun_at.y=1.37
		if id=="doctor": bun_at.y=1.16
		kit.ellipsoid(root,bun_at,Vector3(0.18,0.18,0.17),mat)
		if id=="chessplayer":
			kit.ellipsoid(root,Vector3(0,1.05,-0.18),Vector3(0.30,0.42,0.10),mat)
	elif id in ["thief","naturalist","architect"]:
		for i in range(3):
			var lock: MeshInstance3D=kit.ellipsoid(root,Vector3(-0.11+i*0.10,1.23,0.12),Vector3(0.16,0.14,0.09),mat)
			lock.rotation.z=0.4
		var side: float=-1.0 if id=="naturalist" else 1.0
		for i in range(4): kit.ellipsoid(root,Vector3(side*(0.16+i*0.015),1.15-i*0.07,-0.12),Vector3(0.11,0.13,0.11),mat)
	else:
		for i in range(5):
			var lock: MeshInstance3D=kit.ellipsoid(root,Vector3(-0.16+i*0.075,1.23+0.01*(i%2),0.115),Vector3(0.11,0.14,0.09),mat)
			lock.rotation.z=0.3
		if id=="poet":
			for side in [-1,1]: kit.ellipsoid(root,Vector3(side*0.19,1.12,-0.02),Vector3(0.09,0.26,0.16),mat)

static func dress(kit: Node3D, root: Node3D, id: String) -> void:
	var spec: Array=PEOPLE[id]
	var cloth: Material=kit.material("cloth_"+id,spec[1],3)
	var hair: Material=kit.material("hair_"+id,spec[2],1)
	var leather: Material=kit.material("cast_leather","594333",1)
	var brass: Material=kit.material("cast_brass","b89758",0,0.4,0.65)
	var ivory: Material=kit.material("cast_ivory","d8d3b9",3)
	root.set_meta("actor_id",id)
	root.set_meta("body_template","E-common-three-head-v1")
	root.set_meta("body_dimensions",{"head":Vector3(0.40,0.44,0.34),"torso":Vector3(0.38,0.48,0.25),"anatomical_height":1.32,"scale":Vector3.ONE})
	_hair(kit,root,id,hair)
	# Small raised collars sit clear of the torso and below the chin.
	if id in ["poet","lawyer","detective","painter"]:
		for side in [-1,1]:
			var collar: MeshInstance3D=kit.box(root,Vector3(side*0.058,0.797,0.124),Vector3(0.075,0.075,0.028),ivory)
			collar.rotation.z=side*0.30
	else:
		kit.box(root,Vector3(0,0.801,0.115),Vector3(0.11,0.034,0.033),ivory)
	if spec[3] in ["robe","skirt"]:
		var skirt: MeshInstance3D=kit.cylinder(root,Vector3(0,0.355,0),0.245,0.53,cloth,0.17)
		skirt.scale.z=0.76
		for x in [-0.12,0.12]: kit.limb(root,Vector3(x,0.13,0.16),Vector3(x*0.7,0.51,0.13),0.008,kit.material("hem_"+id,spec[1],0,0.95))
	elif spec[3]=="cape":
		var cape: MeshInstance3D=kit.cylinder(root,Vector3(0,0.56,-0.06),0.29,0.58,cloth,0.13)
		cape.scale.z=0.62
		kit.ellipsoid(root,Vector3(0,0.83,0.113),Vector3(0.045,0.035,0.025),brass)
	elif spec[3]=="coat":
		for side in [-1,1]:
			var tail: MeshInstance3D=kit.box(root,Vector3(side*0.13,0.40,-0.01),Vector3(0.12,0.26,0.23),cloth)
			tail.rotation.z=-side*0.035
			tail.set_meta("art_layer", "garment")
		_buttons(kit,root,brass)
	if id=="poet":
		kit.box(root,Vector3(0,0.747,0.157),Vector3(0.075,0.14,0.035),ivory)
		kit.box(root,Vector3(0.26,0.43,0.12),Vector3(0.18,0.23,0.06),leather)
		for i in range(3): kit.box(root,Vector3(-0.20+i*0.045,0.29,0.11),Vector3(0.022,0.04,0.025),kit.material("poet_fray","ac8061",3))
	elif id=="archaeologist":
		kit.ellipsoid(root,Vector3(0,0.95,0.17),Vector3(0.18,0.22,0.075),hair)
		kit.cylinder(root,Vector3(-0.27,0.32,0.12),0.023,0.64,leather)
		kit.ellipsoid(root,Vector3(-0.27,0.66,0.12),Vector3(0.08,0.06,0.07),brass)
		kit.box(root,Vector3(0.06,0.62,0.14),Vector3(0.022,0.35,0.018),brass)
	elif id=="detective":
		kit.box(root,Vector3(0,0.62,0.143),Vector3(0.14,0.30,0.025),kit.material("waistcoat","465745",3))
		_buttons(kit,root,brass)
		_ring(kit,root,Vector3(-0.27,0.54,0.17),0.072,brass)
		kit.limb(root,Vector3(-0.27,0.47,0.17),Vector3(-0.25,0.39,0.16),0.015,leather)
	elif id in ["engineer","chef"]:
		var apron: Material=kit.material("apron_"+id,"75604c" if id=="engineer" else "dfd2b0",3)
		kit.box(root,Vector3(0,0.51,0.155),Vector3(0.28,0.43,0.04),apron)
		for side in [-1,1]: kit.box(root,Vector3(side*0.095,0.735,0.122),Vector3(0.037,0.17,0.028),apron)
		kit.box(root,Vector3(0,0.48,0.183),Vector3(0.17,0.10,0.028),leather if id=="engineer" else apron)
		if id=="chef":
			for side in [-1,1]: kit.ellipsoid(root,Vector3(side*0.045,1.016,0.192),Vector3(0.09,0.035,0.022),hair)
			kit.ellipsoid(root,Vector3(0,0.963,0.16),Vector3(0.11,0.09,0.05),hair)
		else:
			kit.limb(root,Vector3(0.22,0.46,0.17),Vector3(0.24,0.65,0.16),0.018,brass)
	elif id=="musician":
		kit.box(root,Vector3(0.05,0.60,0.147),Vector3(0.022,0.39,0.017),brass)
		for x in [-0.02,0.02]: kit.limb(root,Vector3(-0.26+x,0.48,0.16),Vector3(-0.26+x,0.64,0.16),0.009,brass)
		kit.limb(root,Vector3(-0.26,0.39,0.16),Vector3(-0.26,0.48,0.16),0.012,brass)
	elif id=="thief":
		kit.box(root,Vector3(0,0.50,0.15),Vector3(0.36,0.055,0.035),leather)
		kit.box(root,Vector3(0.23,0.42,0.07),Vector3(0.15,0.18,0.13),leather)
		for i in range(3): kit.box(root,Vector3(0.18+i*0.034,0.54,0.11),Vector3(0.012,0.12,0.02),brass)
	elif id=="lawyer":
		kit.box(root,Vector3(0,0.71,0.16),Vector3(0.032,0.17,0.03),ivory)
		kit.box(root,Vector3(0.27,0.35,0.08),Vector3(0.23,0.20,0.11),leather)
	elif id=="naturalist":
		kit.box(root,Vector3(0.23,0.40,0.08),Vector3(0.19,0.22,0.12),leather)
		kit.limb(root,Vector3(-0.15,0.77,0.14),Vector3(0.19,0.43,0.16),0.016,leather)
		for i in range(3): kit.ellipsoid(root,Vector3(0.20+i*0.035,0.58+i*0.025,0.10),Vector3(0.05,0.12,0.025),kit.material("specimen_leaves","73865b",3))
	elif id=="painter":
		var beret: MeshInstance3D=kit.ellipsoid(root,Vector3(0.02,1.355,0),Vector3(0.46,0.12,0.37),cloth)
		beret.rotation.z=-0.13
		kit.ellipsoid(root,Vector3(0.28,0.46,0.16),Vector3(0.23,0.15,0.04),leather)
		for i in range(3):
			kit.limb(root,Vector3(-0.25+i*0.02,0.41,0.11),Vector3(-0.24+i*0.02,0.65,0.12),0.008,brass)
			kit.ellipsoid(root,Vector3(0.22+i*0.05,0.48,0.186),Vector3(0.025,0.025,0.012),kit.material("paint_"+str(i),["8a4334","4d746b","c4ae75"][i],0))
	elif id=="chemist":
		for side in [-1,1]: _ring(kit,root,Vector3(side*0.077,1.11,0.195),0.047,brass)
		kit.box(root,Vector3(0,1.11,0.195),Vector3(0.055,0.015,0.015),brass)
		kit.cylinder(root,Vector3(0.24,0.49,0.14),0.036,0.13,kit.material("bottle_green","568578",0,0.24))
		kit.cylinder(root,Vector3(0.24,0.57,0.14),0.019,0.035,leather)
	elif id=="marksman":
		kit.limb(root,Vector3(-0.16,0.78,0.15),Vector3(0.15,0.45,0.16),0.023,leather)
		for i in range(4): kit.box(root,Vector3(-0.08+i*0.06,0.49,0.18),Vector3(0.045,0.08,0.035),leather)
	elif id=="architect":
		kit.cylinder(root,Vector3(0.25,0.49,0.12),0.045,0.32,ivory)
		kit.box(root,Vector3(-0.23,0.45,0.12),Vector3(0.16,0.21,0.04),leather)
		kit.limb(root,Vector3(-0.28,0.53,0.15),Vector3(-0.17,0.53,0.15),0.009,brass)
		kit.limb(root,Vector3(-0.28,0.53,0.15),Vector3(-0.28,0.43,0.15),0.009,brass)
	elif id=="doctor":
		kit.box(root,Vector3(0,0.52,0.15),Vector3(0.29,0.43,0.04),ivory)
		kit.box(root,Vector3(0.28,0.34,0.10),Vector3(0.23,0.18,0.13),leather)
		_ring(kit,root,Vector3(0.28,0.46,0.10),0.04,leather)
	elif id=="chessplayer":
		kit.ellipsoid(root,Vector3(0.24,0.46,0.13),Vector3(0.14,0.16,0.10),leather)
	elif id=="dancer":
		kit.box(root,Vector3(0,0.53,0.15),Vector3(0.35,0.045,0.025),ivory)
		kit.ellipsoid(root,Vector3(0.13,1.30,-0.14),Vector3(0.07,0.05,0.07),ivory)
