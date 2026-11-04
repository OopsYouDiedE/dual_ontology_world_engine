extends RefCounted
## Visual geometry only; dimensions, state, collisions and affordances belong to the caller.
const BLOCKS = preload("res://scenes/components/rendering/ChamferGeometry.gd")
const SURFACE = preload("res://scenes/components/rendering/FurnitureCandidateSurface.gdshader")
var triangle_count := 0
var report: Array = []
var _blocks = BLOCKS.new()
var _style := "wood"
var _wood: Material
var _dark: Material
var _brass: Material
var _iron: Material
var _cloth: Material
## Shared across every generator instance: furniture of the same finish reuses
## one material and identical parts reuse one mesh, so a room of beds, chairs
## and cabinets does not allocate a fresh ShaderMaterial and ArrayMesh per part.
static var _material_cache: Dictionary = {}
static var _primitive_cache: Dictionary = {}

func _material(color: Color, kind := 0, roughness := .8, metal := 0.0, axis := 1) -> ShaderMaterial:
	var key := "%s|%d|%.3f|%.3f|%d|%s" % [color.to_html(),kind,roughness,metal,axis,_style]
	if _material_cache.has(key): return _material_cache[key]
	var mat := ShaderMaterial.new()
	mat.shader = SURFACE
	mat.set_shader_parameter("pigment",color)
	mat.set_shader_parameter("surface_kind",kind)
	mat.set_shader_parameter("roughness_value",roughness)
	mat.set_shader_parameter("metallic_value",metal)
	mat.set_shader_parameter("grain_axis",axis)
	mat.set_shader_parameter("detail_strength",.35 if _style == "painted" else 1.0)
	_material_cache[key]=mat
	return mat

func _box(parent: Node3D, id: String, size: Vector3, center: Vector3, mat: Material, bevel := .008) -> MeshInstance3D:
	var node: MeshInstance3D = _blocks._block(parent,id,size,center,mat,bevel)
	node.set_meta("kind","furniture")
	return node

func _rod(parent: Node3D, id: String, start: Vector3, end: Vector3, radius: float, mat: Material) -> MeshInstance3D:
	var node := MeshInstance3D.new()
	var length := start.distance_to(end)
	var key := "rod|%.4f|%.4f" % [radius, length]
	var mesh: Mesh = _primitive_cache.get(key)
	if mesh == null:
		var cylinder := CylinderMesh.new()
		cylinder.top_radius=radius; cylinder.bottom_radius=radius; cylinder.height=length; cylinder.radial_segments=12
		mesh = cylinder
		_primitive_cache[key] = mesh
	node.name=id; node.mesh=mesh; node.position=(start+end)*.5
	node.quaternion=Quaternion(Vector3.UP,(end-start).normalized())
	node.material_override=mat
	node.set_meta("kind","furniture")
	parent.add_child(node)
	return node

func _ring(parent: Node3D, id: String, center: Vector3, radius: float, tube: float, mat: Material) -> void:
	var node := MeshInstance3D.new()
	var key := "ring|%.4f|%.4f" % [radius, tube]
	var mesh: Mesh = _primitive_cache.get(key)
	if mesh == null:
		var torus := TorusMesh.new()
		torus.inner_radius=radius-tube; torus.outer_radius=radius+tube; torus.rings=24; torus.ring_segments=8
		mesh = torus
		_primitive_cache[key] = mesh
	node.name=id; node.mesh=mesh; node.position=center; node.rotation_degrees.x=90
	node.material_override=mat; node.set_meta("kind","furniture"); parent.add_child(node)

func _refine(node: Node) -> void:
	for child in node.get_children(): _refine(child)
	if not node is MeshInstance3D: return
	var mesh_node := node as MeshInstance3D
	mesh_node.set_meta("kind","furniture")
	if not mesh_node.mesh is BoxMesh: return
	var size: Vector3 = mesh_node.mesh.size
	var label := str(mesh_node.name).to_lower()
	var old: Material = mesh_node.material_override
	var color := Color("886344")
	if old is ShaderMaterial:
		var pigment: Variant = old.get_shader_parameter("pigment")
		if pigment is Color: color=pigment
	elif old is StandardMaterial3D: color=old.albedo_color
	var kind := 1
	var roughness := .8
	var metal := 0.0
	if "handle" in label or "latch" in label:
		kind=0; roughness=.4; metal=.65; color=Color("ad8b51")
	elif "mattress" in label or "pillow" in label or "cushion" in label or "quilt" in label or "backrest" in label or "seat_" in label:
		kind=2; roughness=.94
	elif _style=="painted": color=color.lerp(Color("81908a"),.55)
	var axis := 0 if size.x>size.y and size.x>size.z else (2 if size.z>size.y else 1)
	var material := _material(color,kind,roughness,metal,axis)
	if kind==2:
		mesh_node.mesh=_soft_mesh(size)
		mesh_node.material_override=material
		return
	var temp := Node3D.new()
	var bevel := minf(.035 if kind==2 else .009,minf(size.x,minf(size.y,size.z))*.22)
	var generated: MeshInstance3D = _box(temp,"RoundedPart",size,Vector3.ZERO,material,bevel)
	mesh_node.mesh=generated.mesh; mesh_node.material_override=material
	temp.free()

func _soft_mesh(size: Vector3) -> ArrayMesh:
	var key:="soft|%.4f|%.4f|%.4f" % [size.x,size.y,size.z]
	var cached: ArrayMesh=_primitive_cache.get(key)
	if cached!=null: return cached
	var source:=SphereMesh.new()
	source.radius=1.0;source.height=2.0;source.radial_segments=24;source.rings=12
	var arrays: Array=source.surface_get_arrays(0)
	var points: PackedVector3Array=arrays[Mesh.ARRAY_VERTEX]
	var normals:=PackedVector3Array()
	var half:=size*.5
	var exponent:=.34
	var gradient_power:=2.0/exponent-1.0
	for i in points.size():
		var p: Vector3=points[i]
		var q:=Vector3(signf(p.x)*pow(absf(p.x),exponent),signf(p.y)*pow(absf(p.y),exponent),signf(p.z)*pow(absf(p.z),exponent))
		points[i]=q*half
		var normal:=Vector3(signf(q.x)*pow(absf(q.x),gradient_power)/half.x,signf(q.y)*pow(absf(q.y),gradient_power)/half.y,signf(q.z)*pow(absf(q.z),gradient_power)/half.z)
		normals.append(normal.normalized())
	arrays[Mesh.ARRAY_VERTEX]=points;arrays[Mesh.ARRAY_NORMAL]=normals;arrays[Mesh.ARRAY_TANGENT]=null
	var mesh:=ArrayMesh.new();mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays)
	_primitive_cache[key]=mesh
	return mesh

func _soft(parent: Node3D, id: String, size: Vector3, center: Vector3, mat: Material) -> void:
	var node:=MeshInstance3D.new();node.name=id;node.mesh=_soft_mesh(size);node.position=center;node.material_override=mat;node.set_meta("kind","furniture");parent.add_child(node)

func _finish_furniture(root: Node3D, kind: String, size: Vector3) -> void:
	if kind=="bed":
		var solid:=root.get_node_or_null("UnderBedSolid")
		if solid!=null: root.remove_child(solid);solid.free()
		for side: int in [-1,1]:
			_box(root,"BedSideRail",Vector3(size.x-.08,.12,.045),Vector3(0,size.y-.22,float(side)*(size.z*.5-.035)),_dark)
		_soft(root,"FoldedCover",Vector3(size.x*.60,.055,size.z-.12),Vector3(size.x*.15,size.y+.025,0),_cloth)
	elif kind=="sofa":
		var plinth:=root.get_node_or_null("SofaPlinth") as MeshInstance3D
		if plinth!=null:
			var temp:=Node3D.new();plinth.mesh=_box(temp,"Frame",Vector3(size.x-.04,.07,size.z-.04),Vector3.ZERO,_dark).mesh;temp.free();plinth.position.y=.135
		_box(root,"SeatSupportFrame",Vector3(size.x-.12,.14,size.z-.12),Vector3(0,.24,0),_dark)
		for x: int in [-1,1]:
			for z: int in [-1,1]:
				_box(root,"SofaFoot",Vector3(.07,.10,.07),Vector3(float(x)*(size.x*.5-.12),.05,float(z)*(size.z*.5-.10)),_dark)
	elif kind=="desk":
		for side: int in [-1,1]:
			_box(root,"DrawerStile",Vector3(.018,.10,.028),Vector3(float(side)*minf(.285,size.x*.23),size.y-.09,-size.z*.5+.02),_dark,.003)
	elif kind=="lamp":
		var shade:=root.get_node_or_null("Lampshade") as MeshInstance3D
		if shade!=null:
			var mesh:=CylinderMesh.new();mesh.top_radius=.075;mesh.bottom_radius=.14;mesh.height=.18;mesh.radial_segments=24
			shade.mesh=mesh;shade.position.y=size.y-.09
			shade.material_override=_material(Color("ede3ca"),2,.93)

func _pull(parent: Node3D, center: Vector3, width := .11) -> void:
	for side: int in [-1,1]:
		_rod(parent,"PullMount",center+Vector3(side*width*.5,0,.025),center+Vector3(side*width*.5,0,0),.009,_brass)
	_rod(parent,"PullBar",center+Vector3(-width*.5,0,0),center+Vector3(width*.5,0,0),.012,_brass)

## A wardrobe hangs clothes from a rail under a hat shelf; a cabinet stacks
## shelves at a usable pitch. Both share the carcass, panelled sides and doors.
func _cabinet(size: Vector3, opened: bool, dual_lock: bool, wardrobe := false) -> Node3D:
	var root := Node3D.new(); root.name="DetailedCabinet"
	var w:=size.x; var h:=size.y; var d:=size.z
	_box(root,"Plinth",Vector3(w,.11,d),Vector3(0,.055,0),_dark)
	_box(root,"TopCornice",Vector3(w+.035,.045,d+.025),Vector3(0,h-.0225,0),_wood)
	if h>1.2:
		# Stepped crown: a recessed frieze reads as joinery, not a capped box.
		_box(root,"CrownFrieze",Vector3(w+.012,.05,d+.008),Vector3(0,h-.07,0),_dark,.004)
	_box(root,"Back",Vector3(w-.08,h-.16,.025),Vector3(0,h*.5,d*.5-.0125),_dark)
	var carcass_h:=h-.15
	for side: int in [-1,1]:
		_box(root,"Side",Vector3(.035,carcass_h,d-.035),Vector3(side*(w*.5-.0175),.11+carcass_h*.5,0),_wood)
		# Raised field on the outer face so the long side is not a blank slab.
		var field_h:=carcass_h-(.20 if h>1.2 else .10)
		var field_d:=d-.035-.10
		if field_h>.12 and field_d>.10:
			_box(root,"SideField",Vector3(.012,field_h,field_d),Vector3(side*(w*.5+.006),.11+carcass_h*.5,.0),_dark,.004)
	if wardrobe and h>1.2:
		_box(root,"BottomShelf",Vector3(w-.065,.025,d-.065),Vector3(0,.12,0),_wood)
		_box(root,"HatShelf",Vector3(w-.065,.025,d-.065),Vector3(0,h-.38,0),_wood)
		_rod(root,"HangingRail",Vector3(-(w*.5-.04),h-.46,0),Vector3(w*.5-.04,h-.46,0),.013,_brass)
	else:
		var bays:=clampi(roundi((h-.22)/.38),1,4)
		for row in bays+1:
			var y:=.12+float(row)*(h-.22)/float(bays)
			_box(root,"Shelf",Vector3(w-.065,.025,d-.065),Vector3(0,y,0),_wood)
	for side: int in [-1,1]:
		var pivot:=Node3D.new(); pivot.name="LeftDoorPivot" if side<0 else "RightDoorPivot"
		pivot.position=Vector3(side*(w*.5-.02),.12,-d*.5-.008)
		pivot.rotation_degrees.y=-float(side)*68.0 if opened else 0.0
		root.add_child(pivot)
		var leaf_w:=w*.5-.026; var leaf_h:=h-.18; var cx:=-float(side)*leaf_w*.5
		_box(pivot,"DoorPanel",Vector3(leaf_w,leaf_h,.035),Vector3(cx,leaf_h*.5,0),_wood)
		_box(pivot,"InsetPanel",Vector3(leaf_w-.075,leaf_h-.12,.016),Vector3(cx,leaf_h*.5,-.023),_dark)
		for edge: int in [-1,1]:
			_box(pivot,"DoorStile",Vector3(.028,leaf_h-.02,.018),Vector3(cx+float(edge)*(leaf_w*.5-.021),leaf_h*.5,-.027),_wood)
		_pull(pivot,Vector3(-float(side)*(leaf_w-.055),leaf_h*.48,-.065),.075)
		if dual_lock: _keyhole(pivot,Vector3(-float(side)*(leaf_w-.09),leaf_h*.60,-.051))
	return root

func _keyhole(parent: Node3D, center: Vector3) -> void:
	_box(parent,"LockEscutcheon",Vector3(.065,.10,.012),center,_brass,.008)
	_ring(parent,"KeyholeRim",center+Vector3(0,.015,-.009),.011,.003,_iron)
	_box(parent,"KeyholeSlot",Vector3(.009,.025,.004),center+Vector3(0,-.005,-.009),_iron,.001)

func _door(kind: String, size: Vector3, opened: bool) -> Node3D:
	var root:=Node3D.new(); root.name="Detailed"+kind.capitalize()
	var w:=size.x;var h:=size.y;var t:=size.z
	var industrial:=kind=="bulkhead"
	var frame_mat: Material=_iron if industrial else _dark
	for side: int in [-1,1]:
		_box(root,"Jamb",Vector3(.12,h+.12,.18),Vector3(float(side)*(w*.5+.06),h*.5,0),frame_mat,.014)
	_box(root,"Lintel",Vector3(w+.24,.12,.18),Vector3(0,h+.06,0),frame_mat,.014)
	var leaf:=Node3D.new();leaf.name="LeafPivot";leaf.position.x=-w*.5
	leaf.rotation_degrees.y=-72.0 if opened else 0.0
	root.add_child(leaf)
	_box(leaf,"Leaf",Vector3(w-.025,h-.025,t),Vector3(w*.5,h*.5,0),_iron if industrial else _wood,.016)
	if industrial:
		for x: float in [.06,w-.06]:
			_box(leaf,"SealVertical",Vector3(.035,h-.10,.025),Vector3(x,h*.5,-t*.5-.012),_dark,.004)
		for y: float in [.055,h-.055]:
			_box(leaf,"SealHorizontal",Vector3(w-.10,.035,.025),Vector3(w*.5,y,-t*.5-.012),_dark,.004)
		for y: float in [h*.23,h*.74]:
			_box(leaf,"StiffeningRib",Vector3(w-.16,.065,.035),Vector3(w*.5,y,-t*.5-.025),_iron,.009)
		var center:=Vector3(w*.64,1.05,-t*.5-.09)
		_ring(leaf,"LockHandwheel",center,.155,.017,_brass)
		for i in 4:
			var a:=float(i)*TAU/4.0
			_rod(leaf,"WheelSpoke",center,center+Vector3(cos(a)*.14,sin(a)*.14,0),.009,_brass)
		_rod(leaf,"VerticalLockRod",Vector3(w*.64,.18,-t*.5-.04),Vector3(w*.64,h-.18,-t*.5-.04),.018,_brass)
		for y: float in [.32,h-.32]:
			_box(leaf,"LockRodGuide",Vector3(.08,.07,.055),Vector3(w*.64,y,-t*.5-.045),_iron)
		var gauge_center:=Vector3(w*.24,1.63,-t*.5-.04)
		_rod(leaf,"GaugeBezel",gauge_center,gauge_center+Vector3(0,0,-.045),.085,_brass)
		_rod(leaf,"GaugeFace",gauge_center+Vector3(0,0,-.046),gauge_center+Vector3(0,0,-.05),.071,_material(Color("dbd8c6"),0,.9))
		_rod(leaf,"GaugeNeedle",gauge_center+Vector3(0,0,-.053),gauge_center+Vector3(-.035,.028,-.053),.004,_iron)
	else:
		for y: float in [h*.28,h*.72]:
			_box(leaf,"RaisedPanel",Vector3(w-.16,h*.35,.024),Vector3(w*.5,y,-t*.5-.014),_dark,.008)
		_keyhole(leaf,Vector3(w*.85,.96,-t*.5-.021))
		_pull(leaf,Vector3(w*.85,1.08,-t*.5-.065),.11)
	for y: float in [.28,h*.5,h-.28]:
		_rod(leaf,"HingeBarrel",Vector3(.008,y-.055,-t*.5-.016),Vector3(.008,y+.055,-t*.5-.016),.026,frame_mat)
	var contact:=Marker3D.new(); contact.name="HandleContact";contact.position=Vector3(w*.64 if industrial else w*.85,1.05,-t*.5-.10);leaf.add_child(contact)
	return root

func configure_style(style: String = "wood") -> void:
	_style=style
	_wood=_material(Color("98764c") if _style!="painted" else Color("83938b"),1,.79,0,0)
	_dark=_material(Color("604832") if _style!="painted" else Color("52685f"),1,.82)
	_brass=_material(Color("b69a5d"),0,.42,.62)
	_iron=_material(Color("596366"),0,.65,.45)
	_cloth=_material(Color("888f7d"),2,.94)

func upgrade(root: Node3D, kind: String, size: Vector3) -> Node3D:
	if root==null: return null
	configure_style()
	if kind in ["cabinet","wardrobe"]:
		var replacement:=_cabinet(size,false,false,kind=="wardrobe")
		replacement.name=root.name
		root.free()
		return replacement
	_refine(root)
	_finish_furniture(root,kind,size)
	return root
