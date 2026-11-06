extends RefCounted
## Parametric steel hatch geometry. DV declares dimensions, hardware counts and finish.
## The shared hinged-door controller owns physical sweep and visual motion.
const HINGED = preload("res://scenes/components/architecture/HingedDoor3D.gd")
const WALL = preload("res://scenes/components/architecture/SmartWall3D.gd")
const LAYERS = preload("res://scenes/components/world/PhysicsLayers.gd")

static func create_frame(opening: Dictionary, material: Material) -> Node3D:
	var spec: Dictionary = opening.frame
	var width := float(opening.structural_width)
	var height := float(opening.structural_height)
	var depth := float(spec.depth)
	var radius := float(spec.clear_diameter_m)*.5
	var center := Vector2(width*.5,float(spec.center_height_m))
	var steps := int(spec.radial_segments)
	if radius <= 0 or steps < 24 or center.y-radius < 0 or center.y+radius > height or radius >= width*.5: return null
	var scene = load("res://scenes/components/architecture/CircularBlastDoor3D.tscn")
	var root: Node3D = scene.instantiate()
	root.name = "CircularBlastFrame"
	root.set_meta("world_object_id",str(spec.id))
	root.set_meta("kind","door_frame")
	root.set_meta("clear_width",radius*2)
	root.set_meta("clear_height",radius*2)
	root.set_meta("clearance_profile","circle")
	var angles: Array[float] = []
	for i in steps: angles.append(TAU*float(i)/steps)
	for corner in [Vector2.ZERO,Vector2(width,0),Vector2(width,height),Vector2(0,height)]:
		var theta := fposmod((corner-center).angle(),TAU)
		var duplicate := false
		for old in angles:
			if absf(theta-old)<.000001: duplicate=true
		if not duplicate: angles.append(theta)
	angles.sort()
	var faces := PackedVector3Array()
	var normals := PackedVector3Array()
	for i in angles.size():
		var a := Vector2(cos(angles[i]),sin(angles[i]))
		var b := Vector2(cos(angles[(i+1)%angles.size()]),sin(angles[(i+1)%angles.size()]))
		var inner_a := center+a*radius
		var inner_b := center+b*radius
		var outer_a := center+a*_rectangle_radius(center,width,height,a)
		var outer_b := center+b*_rectangle_radius(center,width,height,b)
		var ia := Vector3(inner_a.x,inner_a.y,depth*.5)
		var ib := Vector3(inner_b.x,inner_b.y,depth*.5)
		var oa := Vector3(outer_a.x,outer_a.y,depth*.5)
		var ob := Vector3(outer_b.x,outer_b.y,depth*.5)
		var back := Vector3(0,0,-depth)
		WALL._quad(faces,normals,ia,oa,ob,ib,Vector3.BACK)
		WALL._quad(faces,normals,ib+back,ob+back,oa+back,ia+back,Vector3.FORWARD)
		WALL._quad(faces,normals,ib,ib+back,ia+back,ia,-Vector3(a.x+b.x,a.y+b.y,0).normalized())
		WALL._quad(faces,normals,oa,oa+back,ob+back,ob,Vector3(a.x+b.x,a.y+b.y,0).normalized())
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX]=faces
	arrays[Mesh.ARRAY_NORMAL]=normals
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays)
	var panel := MeshInstance3D.new()
	panel.name="BulkheadCollar"
	panel.mesh=mesh
	panel.material_override=material
	root.add_child(panel)
	var body := StaticBody3D.new()
	body.name="CircularBoreCollision"
	body.collision_layer=LAYERS.SOLID
	body.collision_mask=0
	root.add_child(body)
	var collider := CollisionShape3D.new()
	var shape := ConcavePolygonShape3D.new()
	shape.set_faces(faces)
	collider.shape=shape
	body.add_child(collider)
	var dark := _metal(Color("252b29"),.7)
	for side in [-1.0,1.0]:
		_ring(root,"RaisedFlange",Vector3(center.x,center.y,side*(depth*.5+.025)),radius+.055,.035,material,steps)
		_ring(root,"RecessedGasket",Vector3(center.x,center.y,side*(depth*.5+.004)),radius+.008,.009,dark,steps)
		for i in int(spec.bolt_count):
			var theta := TAU*float(i)/float(spec.bolt_count)
			var p := Vector3(center.x+cos(theta)*(radius+.06),center.y+sin(theta)*(radius+.06),side*(depth*.5+.055))
			_cylinder(root,"FlangeBolt",p,.015,.018,dark,8)
	# Bearing brackets mount on the room-side face. The hinge line stands clear of the collar.
	var leaf: Dictionary = opening.door
	var pivot: Array = leaf.hinge_design
	var origin: Array = spec.origin_design
	var pivot_local := Vector3(float(pivot[0])-float(origin[0]),float(pivot[2])-float(origin[2]),-(float(pivot[1])-float(origin[1])))
	for fraction in [.25,.75]:
		var y := pivot_local.y+float(leaf.height)*float(fraction)
		_box(root,"BearingBracket",Vector3(pivot_local.x+.04,y,pivot_local.z*.5),Vector3(.18,.15,absf(pivot_local.z)+.07),material)
		var bearing := _cylinder(root,"HingeBearing",Vector3(pivot_local.x,y,pivot_local.z),.066,.24,dark,16)
		bearing.rotation.x=0
	return root

static func create_leaf(spec: Dictionary, material: Material) -> Node3D:
	var diameter := float(spec.width)
	var thickness := float(spec.thickness)
	var radius := diameter*.5
	if not is_equal_approx(diameter,float(spec.height)): return null
	var root = HINGED.create(str(spec.id),diameter,diameter,thickness,-float(spec.angle_max),-float(spec.angle_min),material)
	if root == null: return null
	root.set_meta("door_profile","circular_blast")
	root.set_meta("locking_dogs_pose",str(spec.locking_dogs_pose))
	root.set_meta("clearance_profile","circle")
	var visual: MeshInstance3D=root.hinge.get_node("DoorLeafMesh")
	var disk := CylinderMesh.new()
	disk.top_radius=radius
	disk.bottom_radius=radius
	disk.height=thickness
	disk.radial_segments=int(spec.radial_segments)
	visual.mesh=disk
	visual.rotation.x=PI*.5
	visual.position=Vector3(radius,radius,0)
	for id in ["Handle_Outer","Handle_Inner","Latch_Inner"]: root.hinge.get_node(id).free()
	var points := PackedVector3Array()
	for i in int(spec.radial_segments):
		var theta := TAU*float(i)/float(spec.radial_segments)
		for side in [-1.0,1.0]: points.append(Vector3(cos(theta)*radius,sin(theta)*radius,side*thickness*.5))
	var hull := ConvexPolygonShape3D.new()
	hull.points=points
	root.box=hull
	root.body.get_child(0).shape=hull
	var dark := _metal(Color("292f2c"),.65)
	var hardware := _metal(Color("8b938b"),.4)
	var center := Vector3(radius,radius,0)
	for side in [-1.0,1.0]:
		_ring(root.hinge,"PlateEdgeRoll",center+Vector3(0,0,side*(thickness*.5+.008)),radius-.035,.018,material,int(spec.radial_segments))
		_cylinder(root.hinge,"DishedCenter",center+Vector3(0,0,side*(thickness*.5+.008)),radius*.82,.028,material,int(spec.radial_segments))
		for i in int(spec.dog_count):
			var theta := TAU*float(i)/float(spec.dog_count)
			var direction := Vector3(cos(theta),sin(theta),0)
			var rib := _box(root.hinge,"RadialStiffener",center+direction*radius*.56+Vector3(0,0,side*(thickness*.5+.042)),Vector3(radius*.64,.045,.055),material)
			rib.rotation.z=theta
			var dog := _box(root.hinge,"LockingDog",center+direction*radius*.86+Vector3(0,0,side*(thickness*.5+.065)),Vector3(.15,.075,.065),hardware)
			dog.rotation.z=theta
			_cylinder(root.hinge,"DogPivot",center+direction*radius*.86+Vector3(0,0,side*(thickness*.5+.105)),.018,.018,dark,10)
		var wheel_radius := float(spec.wheel_radius_m)
		var wheel_center := center+Vector3(0,0,side*(thickness*.5+.24))
		_cylinder(root.hinge,"WheelSpindle",center+Vector3(0,0,side*(thickness*.5+.12)),.045,.24,hardware,16)
		_ring(root.hinge,"OperatingWheel",wheel_center,wheel_radius,.022,dark,48)
		for i in 4:
			var theta := PI*.5*i
			var spoke := _box(root.hinge,"WheelSpoke",wheel_center+Vector3(cos(theta),sin(theta),0)*wheel_radius*.5,Vector3(wheel_radius,.027,.027),hardware)
			spoke.rotation.z=theta
		_cylinder(root.hinge,"RemovableCrankPin",wheel_center+Vector3(0,0,side*.025),.025,.045,hardware,12)
	for fraction in [.25,.75]:
		_box(root.hinge,"HingeStrap",Vector3(.20,diameter*float(fraction),0),Vector3(.44,.14,thickness+.05),material)
	for id in ["FaceTarget","Contact","Contact_Inner","Contact_Outer"]:
		var marker: Marker3D=root.get_node("InteractionSlots/"+id)
		marker.position=Vector3(radius,radius,0)
		if id == "Contact_Inner": marker.position.z=-thickness*.5-.24
		if id == "Contact_Outer": marker.position.z=thickness*.5+.24
	return root

static func _rectangle_radius(center: Vector2, width: float, height: float, direction: Vector2) -> float:
	var tx := INF
	var ty := INF
	if absf(direction.x)>.0000001: tx=((width-center.x) if direction.x>0 else -center.x)/direction.x
	if absf(direction.y)>.0000001: ty=((height-center.y) if direction.y>0 else -center.y)/direction.y
	return minf(tx,ty)

static func _metal(color: Color, roughness: float) -> ShaderMaterial:
	var material := ShaderMaterial.new()
	material.shader=preload("res://assets/materials/painterly_surface.gdshader")
	material.set_shader_parameter("base_color",color)
	material.set_shader_parameter("surface_kind",3)
	material.set_shader_parameter("surface_roughness",roughness)
	material.set_shader_parameter("texture_scale_m",.6)
	material.set_shader_parameter("detail_scale_m",.12)
	return material

static func _box(parent: Node3D, id: String, position: Vector3, size: Vector3, material: Material) -> MeshInstance3D:
	var node := MeshInstance3D.new()
	node.name=id
	var mesh := BoxMesh.new()
	mesh.size=size
	node.mesh=mesh
	node.material_override=material
	node.position=position
	parent.add_child(node)
	return node

static func _cylinder(parent: Node3D, id: String, position: Vector3, radius: float, depth: float, material: Material, segments: int) -> MeshInstance3D:
	var node := MeshInstance3D.new()
	node.name=id
	var mesh := CylinderMesh.new()
	mesh.top_radius=radius
	mesh.bottom_radius=radius
	mesh.height=depth
	mesh.radial_segments=segments
	node.mesh=mesh
	node.material_override=material
	node.position=position
	node.rotation.x=PI*.5
	parent.add_child(node)
	return node

static func _ring(parent: Node3D, id: String, position: Vector3, radius: float, half_width: float, material: Material, segments: int) -> MeshInstance3D:
	var node := MeshInstance3D.new()
	node.name=id
	var mesh := TorusMesh.new()
	mesh.inner_radius=radius-half_width
	mesh.outer_radius=radius+half_width
	mesh.rings=segments
	mesh.ring_segments=10
	node.mesh=mesh
	node.material_override=material
	node.position=position
	node.rotation.x=PI*.5
	parent.add_child(node)
	return node
