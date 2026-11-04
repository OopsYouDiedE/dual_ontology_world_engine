extends RefCounted
## Retained map coordinates and ground; recovered buildings and vegetation are 3D.
const RESOURCE_CLOSURE = preload("res://scenes/components/world/outdoors-resources.gd")
const IMPORTED = preload("res://scenes/components/prefabs/ImportedAssets.gd")
const DATA_PATH = "res://assets/data/presentation.dv"
const FINISH = preload("res://scenes/components/world/OutdoorFinish.gd")
static var data: Dictionary={}
static var textures: Dictionary={}
static var roof_materials: Dictionary={}

static func _roof_material(warm: bool) -> ShaderMaterial:
	var key := "warm" if warm else "slate"
	if roof_materials.has(key): return roof_materials[key]
	var shader: Shader = preload("res://assets/materials/surfaces/roof_tiles.gdshader")
	var mat := ShaderMaterial.new()
	mat.shader=shader
	mat.set_shader_parameter("pigment",Color("805a48") if warm else Color("4e6060"))
	roof_materials[key]=mat
	return mat

static func _texture(path: String) -> Texture2D:
	if textures.has(path): return textures[path]
	var source: Texture2D=load(path)
	textures[path]=source
	return source

static func build(kit: Node3D, id: String) -> Dictionary:
	if data.is_empty(): data=preload("res://scenes/main/DvPresentationContent.cs").new().ReadRecord(DATA_PATH, "layouts", "exteriors")
	if not data.maps.has(id): return {"error":"Unknown outdoor map: "+id}
	var entry: Dictionary=data.maps[id]
	if FINISH.handles(id): return FINISH.build(kit, id, entry)
	var size := Vector2(entry.size_px[0]/24.0,entry.size_px[1]/24.0)
	var ground := MeshInstance3D.new()
	ground.name="RetainedRoadsAndCoast"
	var plane := PlaneMesh.new()
	plane.size=size
	ground.mesh=plane
	var mat := StandardMaterial3D.new()
	mat.albedo_texture=_texture(entry.ground)
	mat.roughness=1.0
	mat.texture_filter=BaseMaterial3D.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS_ANISOTROPIC
	ground.material_override=mat
	ground.set_meta("source_ground",entry.ground)
	kit.content_root.add_child(ground)
	_solid_box(ground, "Floor", Vector3(0, -0.1, 0), Vector3(size.x, 0.2, size.y))
	var counts := {"building_3d":0,"vegetation_3d":0,"retained_raster":0}
	for item in entry.objects:
		var at := Vector3(item.at[0]/24.0-size.x/2.0,0,item.at[1]/24.0-size.y/2.0)
		if item.representation=="building-3d":
			_building(kit,item,size)
			counts.building_3d+=1
		elif item.representation=="vegetation-3d":
			_tree(kit,item,at)
			counts.vegetation_3d+=1
		else:
			_retained(kit,item,at)
			counts.retained_raster+=1
	kit.content_root.set_meta("outdoor_representation",counts)
	kit.content_root.set_meta("source_sha256",entry.source_sha256)
	return {"size":size,"camera_size":maxf(size.x*1.08,size.y*0.85),
		"camera_target":Vector3(0,2,0),"camera_position":Vector3(0,150,170),
		"counts":counts,"limits":data.limits,"source":entry.source}

static func _building(kit: Node3D, item: Dictionary, map_size: Vector2) -> void:
	var footprint: Dictionary=item.footprint
	var width: float=footprint.size[0]/24.0
	var depth: float=footprint.size[1]/24.0
	var height: float=item.height
	var center := Vector3(footprint.at[0]/24.0-map_size.x/2.0,0,footprint.at[1]/24.0-map_size.y/2.0)
	var root: Node3D=kit.group(item.name,center)
	root.set_meta("source_collision",footprint.node)
	root.set_meta("representation","recovered-footprint-3d")
	var white: bool=item.name.contains("生活翼") or item.name in ["诊疗所与安全端","后勤院"]
	var wall: Material=kit.material("outdoor_white" if white else "outdoor_stone","c2bca5" if white else "686b62",2)
	var trim: Material=kit.material("outdoor_trim","a8a18c",2)
	var slate: Material=_roof_material(white)
	var wood: Material=kit.material("outdoor_wood","4b3c30",1)
	kit.box(root,Vector3(0,height/2.0,0),Vector3(width,height,depth),wall)
	# Exterior building masses are closed walls; interior doorways belong to room layouts.
	_solid_box(root, "Wall", Vector3(0, height / 2.0, 0), Vector3(width, height, depth))
	kit.box(root,Vector3(0,0.25,0),Vector3(width+0.25,0.5,depth+0.25),trim)
	kit.box(root,Vector3(0,height-0.13,0),Vector3(width+0.35,0.26,depth+0.35),trim)
	for x in [-width/2.0,width/2.0]:
		for z in [-depth/2.0,depth/2.0]:
			for n in range(int(height/0.5)):
				kit.box(root,Vector3(x,n*0.5+0.25,z),Vector3(0.33,0.40,0.33),trim)
	if item.name=="温室":
		var glass: Material=kit.material("outdoor_glass","75938c",0,0.28)
		for x in range(int(width/1.5)):
			kit.box(root,Vector3(-width/2.0+0.75+x*1.5,1.8,depth/2.0+0.03),Vector3(1.38,2.2,0.04),glass)
		_roof(root,width+0.4,depth+0.4,height,width*0.22,glass)
	else:
		_roof(root,width+0.6,depth+0.6,height,minf(width,depth)*0.25,slate)
		var windows: int=maxi(1,int(width/3.2))
		for i in range(windows):
			var x: float=-width/2.0+(i+0.5)*width/windows
			# Windows only; doors are intentionally deferred to cognition presentation.
			_window(kit,root,Vector3(x,height*0.51,depth/2.0+0.07),minf(1.1,width/windows*0.6),minf(2.2,height*0.45),wood,trim)
		if item.name.contains("生活翼") or item.name=="后勤院":
			kit.box(root,Vector3(width*0.27,height+1.4,-depth*0.12),Vector3(0.8,2.8,0.8),wall)
			kit.box(root,Vector3(width*0.27,height+2.85,-depth*0.12),Vector3(1.0,0.18,1.0),trim)
	# Ground already contains authored building shadows; avoid doubling them.
	_disable_shadow(root)

static func _window(kit: Node3D, root: Node3D, at: Vector3, w: float, h: float, wood: Material, trim: Material) -> void:
	kit.box(root,at,Vector3(w+0.20,h+0.20,0.16),trim)
	kit.box(root,at+Vector3(0,0,0.10),Vector3(w,h,0.04),kit.material("outdoor_window","516f74",0,0.3))
	for x in [-w/2.0,0.0,w/2.0]: kit.box(root,at+Vector3(x,0,0.14),Vector3(0.055,h+0.06,0.05),wood)
	for y in [-h/2.0,0.0,h/2.0]: kit.box(root,at+Vector3(0,y,0.14),Vector3(w+0.06,0.055,0.05),wood)
	kit.box(root,at+Vector3(0,-h/2.0-0.10,0.14),Vector3(w+0.35,0.14,0.30),trim)

static func _roof(root: Node3D, w: float, d: float, y: float, rise: float, mat: Material) -> void:
	var a := Vector3(-w/2.0,y,-d/2.0)
	var b := Vector3(w/2.0,y,-d/2.0)
	var c := Vector3(w/2.0,y,d/2.0)
	var e := Vector3(-w/2.0,y,d/2.0)
	var rn := Vector3(0,y+rise,-d/2.0)
	var rs := Vector3(0,y+rise,d/2.0)
	var triangles: Array=[[a,e,rs],[a,rs,rn],[rn,rs,c],[rn,c,b],[a,rn,b],[e,c,rs]]
	var surface := SurfaceTool.new()
	surface.begin(Mesh.PRIMITIVE_TRIANGLES)
	for tri in triangles:
		# Godot's front winding is clockwise. The first capture exposed box tops
		# because these roof faces were back-face culled.
		var normal: Vector3=(tri[1]-tri[0]).cross(tri[2]-tri[0]).normalized()
		for index in [2,1,0]:
			var p: Vector3=tri[index]
			surface.set_normal(normal)
			surface.set_uv(Vector2(p.x,p.z))
			surface.add_vertex(p)
	var mesh := MeshInstance3D.new()
	mesh.mesh=surface.commit()
	mesh.material_override=mat
	root.add_child(mesh)

static func _tree(kit: Node3D, item: Dictionary, at: Vector3) -> void:
	var shrub: bool=item.texture.contains("hydrangea")
	var height: float=clampf(item.image_size[1]/24.0,0.65,6.5)
	var root: Node3D=IMPORTED.vegetation(kit.content_root,at,height,shrub,item.name)
	if root == null: return
	root.set_meta("representation","approved-cc0-3d")
	# The retained ground already contains authored shadows.
	_disable_shadow(root)

static func _retained(kit: Node3D, item: Dictionary, at: Vector3) -> void:
	var sprite := Sprite3D.new()
	sprite.name=item.name
	sprite.texture=_texture(item.texture)
	sprite.pixel_size=1.0/24.0
	sprite.centered=false
	sprite.offset=Vector2(item.offset[0],-item.offset[1]-sprite.texture.get_height())
	sprite.position=at+Vector3(0,0.015,0)
	sprite.alpha_cut=SpriteBase3D.ALPHA_CUT_DISCARD
	sprite.shaded=true
	sprite.texture_filter=BaseMaterial3D.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS_ANISOTROPIC
	sprite.set_meta("representation","retained-raster-not-3d")
	sprite.set_meta("source",item.texture)
	kit.content_root.add_child(sprite)

static func _disable_shadow(root: Node) -> void:
	if root is GeometryInstance3D: root.cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	for child in root.get_children(): _disable_shadow(child)

static func _solid_box(parent: Node3D, category: String, center: Vector3, size: Vector3) -> void:
	var body := StaticBody3D.new()
	body.name = category + "Solid"
	body.collision_layer = 1
	body.collision_mask = 0
	body.set_meta("collision_category", category.to_lower())
	var shape := BoxShape3D.new()
	shape.size = size
	var collider := CollisionShape3D.new()
	collider.shape = shape
	collider.position = center
	body.add_child(collider)
	parent.add_child(body)
