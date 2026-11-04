extends RefCounted
## Metre-scale room-owned wall shells. Local +Z faces into the owning room.
## BoxMesh's original triangle winding is preserved; outward (-Z) faces removed.
const WALL_HEIGHT := 3.0
const WALL_THICKNESS := 0.22
const DOOR_WIDTH := 0.9
const DOOR_HEIGHT := 2.1

static func build(kit: Node3D, size: Vector2, openings: Dictionary) -> void:
	var shell := Node3D.new()
	shell.name="YiOwnedInteriorWalls"
	shell.set_meta("room_owner",kit.content_root.name)
	shell.set_meta("outside_faces",false)
	shell.set_meta("door_models",false)
	kit.content_root.add_child(shell)
	for side in ["north","south","west","east"]:
		var wall := Node3D.new()
		wall.name=String(side).capitalize()+"OwnedWall"
		wall.set_meta("room_owner",kit.content_root.name)
		var horizontal: bool=side in ["north","south"]
		var length: float=size.x if horizontal else size.y
		var height: float=1.05 if side=="south" else WALL_HEIGHT
		var collision_height: float=WALL_HEIGHT
		match side:
			"north": wall.position=Vector3(0,0,-size.y/2)
			"south":
				wall.position=Vector3(0,0,size.y/2)
				wall.rotation.y=PI
			"west":
				wall.position=Vector3(-size.x/2,0,0)
				wall.rotation.y=PI/2
			"east":
				wall.position=Vector3(size.x/2,0,0)
				wall.rotation.y=-PI/2
		shell.add_child(wall)
		var centers: Array=openings.get(side,[]).duplicate()
		centers.sort()
		if side=="north" and centers.is_empty() and (kit.current_room_id.get_slice(":",1).begins_with("north_") or kit.current_room_id.get_slice(":",1)=="hall"):
			var wc: float=-.5 if kit.current_room_id.get_slice(":",1).begins_with("north_") else 0.0
			_span(kit,wall,-length/2,wc-.60,height)
			_span(kit,wall,wc+.60,length/2,height)
			_span(kit,wall,wc-.60,wc+.60,1.30)
			_open_box(wall,Vector3(wc,2.775,0),Vector3(1.2,.45,WALL_THICKNESS),_plaster(kit))
			_solid_box(wall,"wall",Vector3(wc,2.775,0),Vector3(1.2,.45,WALL_THICKNESS))
			_crown(kit,wall,wc,1.2,height)
			continue
		var cursor: float=-length/2
		for value in centers:
			var center := clampf(float(value),-length/2+DOOR_WIDTH/2,length/2-DOOR_WIDTH/2)
			var left: float=center-DOOR_WIDTH/2
			var right: float=center+DOOR_WIDTH/2
			if left>cursor: _span(kit,wall,cursor,left,height,collision_height)
			if height>DOOR_HEIGHT:
				_open_box(wall,Vector3(center,(height+DOOR_HEIGHT)/2,0),Vector3(DOOR_WIDTH,height-DOOR_HEIGHT,WALL_THICKNESS),_plaster(kit))
				_crown(kit,wall,center,DOOR_WIDTH,height)
			if collision_height>DOOR_HEIGHT:
				_solid_box(wall,"wall",Vector3(center,(collision_height+DOOR_HEIGHT)/2,0),Vector3(DOOR_WIDTH,collision_height-DOOR_HEIGHT,WALL_THICKNESS))
			_door_reveal(kit,wall,center,height)
			cursor=maxf(cursor,right)
		if cursor<length/2: _span(kit,wall,cursor,length/2,height,collision_height)

static func _plaster(kit: Node3D) -> Material:
	if kit.current_room_id.get_slice(":",1) in ["wc","prep","foyer"]:
		return kit.material("yi_plaster_ivory","c9c0a8",2,.86)
	var mat := ShaderMaterial.new()
	mat.shader=preload("res://scenes/components/world/yi_material.gdshader")
	mat.set_shader_parameter("kind",2)
	return mat

static func _span(kit: Node3D, wall: Node3D, left: float, right: float, height: float, collision_height: float=-1.0) -> void:
	var width := right-left
	if width<0.001: return
	var center := (left+right)/2
	var wood: Material=kit.material("yi_arch_walnut","493429",1,0.52)
	var trim: Material=kit.material("yi_arch_frame","77563d",1,0.49)
	var cream: Material=kit.material("yi_arch_cream","d8c6a2",0,0.85)
	_open_box(wall,Vector3(center,height/2,0),Vector3(width,height,WALL_THICKNESS),_plaster(kit))
	var solid_height: float=height if collision_height<0 else collision_height
	_solid_box(wall,"wall",Vector3(center,solid_height/2,0),Vector3(width,solid_height,WALL_THICKNESS))
	_open_box(wall,Vector3(center,0.46,0.125),Vector3(width,0.92,0.055),wood)
	for y in [0.08,0.90]:
		_open_box(wall,Vector3(center,y,0.17),Vector3(width,0.065,0.06),trim)
	_open_box(wall,Vector3(center,0.99,0.155),Vector3(width,0.11,0.08),cream)
	var count := maxi(1,int(ceil(width/0.82)))
	var step := width/count
	for i in range(count):
		var x := left+step*(i+0.5)
		var w := maxf(0.04,step-0.09)
		_open_box(wall,Vector3(x,0.49,0.17),Vector3(w,0.65,0.03),trim)
		_open_box(wall,Vector3(x,0.49,0.195),Vector3(maxf(0.015,w-0.065),0.58,0.025),wood)
	_crown(kit,wall,center,width,height)

static func _crown(kit: Node3D, wall: Node3D, center: float, width: float, height: float) -> void:
	var cap: Material=kit.material("yi_arch_cap","302a2d",1,.75)
	if height>1.1:
		_open_box(wall,Vector3(center,height-.12,.14),Vector3(width,.065,.07),kit.material("yi_arch_cream","d8c6a2",0,.85))
	var offset: float=.002 if String(wall.name).begins_with("East") or String(wall.name).begins_with("West") else 0.0
	_open_box(wall,Vector3(center,height+.025+offset,0),Vector3(width,.05,.28),cap)

static func _door_reveal(kit: Node3D, wall: Node3D, center: float, height: float) -> void:
	var trim: Material=kit.material("yi_arch_cream","d8c6a2",0,0.85)
	var h := minf(height,DOOR_HEIGHT)
	for side in [-1,1]:
		_open_box(wall,Vector3(center+side*(DOOR_WIDTH/2+0.035),h/2,0.14),Vector3(0.07,h,0.10),trim)
	if height>DOOR_HEIGHT:
		_open_box(wall,Vector3(center,DOOR_HEIGHT+0.045,0.14),Vector3(DOOR_WIDTH+0.18,0.09,0.12),trim)
	_open_box(wall,Vector3(center,0.008,0),Vector3(DOOR_WIDTH+0.04,0.016,0.28),trim)

static func _open_box(parent: Node3D, at: Vector3, size: Vector3, mat: Material) -> MeshInstance3D:
	var primitive := BoxMesh.new()
	primitive.size=size
	var arrays := primitive.get_mesh_arrays()
	var normals: PackedVector3Array=arrays[Mesh.ARRAY_NORMAL]
	var indices: PackedInt32Array=arrays[Mesh.ARRAY_INDEX]
	var kept := PackedInt32Array()
	for i in range(0,indices.size(),3):
		if normals[indices[i]].z < -0.5: continue
		kept.append(indices[i])
		kept.append(indices[i+1])
		kept.append(indices[i+2])
	arrays[Mesh.ARRAY_INDEX]=kept
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays)
	var instance := MeshInstance3D.new()
	instance.mesh=mesh
	instance.material_override=mat
	instance.position=at
	instance.set_meta("outside_faces",false)
	parent.add_child(instance)
	return instance

static func _solid_box(parent: Node3D, category: String, center: Vector3, size: Vector3) -> void:
	var body := StaticBody3D.new()
	body.name=category.capitalize()+"Solid"
	body.collision_layer=1
	body.collision_mask=0
	body.set_meta("collision_category",category)
	var shape := BoxShape3D.new()
	shape.size=size
	var collider := CollisionShape3D.new()
	collider.shape=shape
	collider.position=center
	body.add_child(collider)
	parent.add_child(body)

static func window(kit: Node3D, at: Vector3) -> void:
	var root: Node3D=kit.group("YiRecessedSashWindow",at)
	var wood: Material=kit.material("yi_window_oak","64482e",1,0.48)
	var cream: Material=kit.material("yi_arch_cream","d8c6a2",0,0.85)
	var glass: Material=kit.material("yi_window_glass","9aafb1",0,0.45,0.0)
	var curtain: Material=kit.material("yi_window_curtain","763d34",3,0.97)
	# Dimensions: opening 1.20 x 1.25m; sill at 1.30m above the floor.
	_open_box(root,Vector3(0,1.925,0.13),Vector3(1.20,1.25,0.025),glass)
	for x in [-0.64,0.64]:
		_open_box(root,Vector3(x,1.925,0.19),Vector3(0.085,1.43,0.15),cream)
	for y in [1.26,2.59]:
		_open_box(root,Vector3(0,y,0.19),Vector3(1.36,0.09,0.15),cream)
	for x in [-0.60,0,0.60]:
		_open_box(root,Vector3(x,1.925,0.22),Vector3(0.035,1.25,0.055),wood)
	for y in [1.30,1.91,2.55]:
		_open_box(root,Vector3(0,y,0.22),Vector3(1.20,0.035,0.055),wood)
	_open_box(root,Vector3(0,1.27,0.28),Vector3(1.47,0.08,0.34),cream)
	kit.limb(root,Vector3(-0.88,2.66,0.28),Vector3(0.88,2.66,0.28),0.025,wood)
	for side in [-1,1]:
		for i in range(5):
			var x: float=side*(0.62+i*0.04)
			# Closely spaced narrow tubes give the heavy cloth a real pleated silhouette.
			kit.cylinder(root,Vector3(x,1.98,0.28),0.033,1.30,curtain)
		_open_box(root,Vector3(side*0.70,1.62,0.31),Vector3(0.22,0.035,0.04),wood)
	root.set_meta("opening_metres",Vector2(1.20,1.25))
	root.set_meta("outside_faces",false)
