extends Node3D
const PhysicsLayers = preload("res://geometry/PhysicsLayers.gd")
const Furniture = preload("res://geometry/FrameworkFurniture.gd")
const Coordinates = preload("res://geometry/DesignCoordinates.gd")
const FloorSurface = preload("res://geometry/FloorSurface3D.gd")
const MeshDiagnostics = preload("res://geometry/MeshPublicationDiagnostics.gd")
const WallSurface = preload("res://geometry/generators/WallSurface3D.gd")
const OpeningFrame = preload("res://geometry/generators/OpeningFrame3D.gd")
const HingedDoor = preload("res://geometry/generators/HingedDoor3D.gd")
const HingedWindow = preload("res://geometry/generators/HingedWindow3D.gd")
const SkirtingBoard = preload("res://geometry/generators/SkirtingBoard3D.gd")
const StructuralMember = preload("res://geometry/generators/StructuralMember3D.gd")
const CeilingSurface = preload("res://geometry/generators/CeilingSurface3D.gd")
const StraightStair = preload("res://geometry/generators/StraightStair3D.gd")
const WorldObject = preload("res://geometry/WorldObject3D.gd")
const WorldObjectAdapter = preload("res://geometry/WorldObjectSpecAdapter.gd")
const PublicObjects = preload("res://geometry/PublicNarrativeObjects.gd")
const ExteriorRoof = preload("res://geometry/generators/ExteriorRoof3D.gd")
const RailingHandrail = preload("res://geometry/generators/RailingHandrail3D.gd")
## Reusable DV projection. No world layout or gameplay rules are encoded here.
var floors: Dictionary = {}
var collision_visuals: Dictionary = {}
var counts: Dictionary = {}
var floor_diagnostics: Array[Dictionary] = []
var study_floor_surfaces: Dictionary = {}
var wall_diagnostics: Array[Dictionary] = []
var ceiling_diagnostics: Array[Dictionary] = []
var stair_diagnostics: Array[Dictionary] = []
var study_stairs: Dictionary = {}
var build_completed := false
var study_doors: Dictionary = {}
var ceilings_visible := true

func configured_surface(spec: Dictionary, is_floor: bool) -> Material:
	var mat := surface(Color(str(spec.get("color","cbb896"))),is_floor)
	mat.set_shader_parameter("surface_roughness",float(spec.get("roughness",0.87)))
	mat.set_shader_parameter("texture_scale_m",maxf(0.05,float(spec.get("texture_scale_m",1.0))))
	return mat

func point(v: Array) -> Vector3:
	return Coordinates.to_godot(Vector3(float(v[0]), float(v[1]), float(v[2])))

func material(color: Color) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = color
	m.roughness = 0.85
	return m

func surface(color: Color, is_floor: bool) -> ShaderMaterial:
	var m := ShaderMaterial.new()
	m.shader = preload("res://rendering/shaders/framework_surface.gdshader")
	m.set_shader_parameter("base_color",color)
	m.set_shader_parameter("floor_surface",is_floor)
	return m

func prism_mesh(points: Array) -> ArrayMesh:
	var indices: Array[int] = []
	var n := points.size()/2
	for i in range(1,n-1):
		indices.append_array([0,i,i+1,n,n+i+1,n+i])
	for i in range(n):
		var j := (i+1)%n
		indices.append_array([i,n+i,n+j,i,n+j,j])
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	for i in indices:
		st.add_vertex(point(points[i]))
	st.generate_normals()
	return st.commit()

func visual(p: Dictionary, mat: Material) -> MeshInstance3D:
	var m := MeshInstance3D.new()
	if p.has("rect"):
		var r: Array = p.rect
		var box := BoxMesh.new()
		box.size = Coordinates.size_to_godot(Vector3(r[2]-r[0],r[3]-r[1],p.height))
		m.mesh = box
		m.position = point([(r[0]+r[2])/2.0,(r[1]+r[3])/2.0,p.z+p.height/2.0])
	else:
		m.mesh = prism_mesh(p.points)
	m.material_override = mat
	m.set_meta("kind",p.kind)
	return m

func build(data: Dictionary) -> void:
	build_completed = false
	var stone := surface(Color("cbb896"),false)
	var wall_base := surface(Color("73634f"),false)
	var slab := surface(Color("344c5a"),true)
	var stair := material(Color("598c85"))
	var debug_box := material(Color("d89e55"))
	var debug_floor := material(Color("4b9eae"))
	var debug_convex := material(Color("c46488"))
	for f in data.floors:
		var root := Node3D.new()
		root.name = f.id
		add_child(root)
		var meshes := Node3D.new()
		meshes.name = "VisibleStructure"
		root.add_child(meshes)
		var physics := Node3D.new()
		physics.name = "SeparateCollisionBodies"
		root.add_child(physics)
		var overlay := Node3D.new()
		overlay.name = "CollisionReviewMeshes"
		root.add_child(overlay)
		var floor_colliders := {}
		var replaced_pieces := {}
		var replaced_colliders := {}
		for wall in f.get("wall_segments", []):
			for id in wall.replaces_pieces: replaced_pieces[str(id)] = true
			for id in wall.replaces_colliders: replaced_colliders[str(id)] = true
		for flight in f.get("straight_stairs", []):
			for id in flight.replaces_pieces: replaced_pieces[str(id)] = true
			for id in flight.replaces_colliders: replaced_colliders[str(id)] = true
		for ceiling in f.get("ceiling_surfaces", []):
			for id in ceiling.replaces_pieces: replaced_pieces[str(id)] = true
			for id in ceiling.replaces_colliders: replaced_colliders[str(id)] = true
		for opening in f.openings:
			if not opening.has("door"): continue
			for id in opening.door.replaces_pieces: replaced_pieces[str(id)] = true
			for id in opening.door.replaces_colliders: replaced_colliders[str(id)] = true
		for p in f.pieces:
			if replaced_pieces.has(str(p.id)): continue
			var mat: Material = slab if p.kind=="floor" else stair if p.kind=="stairs" else wall_base if p.kind=="wall_lower" else stone
			if p.has("surface"):
				mat = configured_surface(p.surface,p.kind in ["floor","surface_finish"])
			var mesh: Node3D
			if p.kind == "floor" and p.has("rect"):
				if not p.has("collider_ref"):
					push_error("Floor lacks collision reference: " + str(p.id))
					return
				var r: Array = p.rect
				var outer := PackedVector2Array([Vector2(r[0],r[1]), Vector2(r[2],r[1]), Vector2(r[2],r[3]), Vector2(r[0],r[3])])
				var holes: Array[PackedVector2Array] = []
				mesh = FloorSurface.create(str(p.id), outer, holes, float(p.z) + float(p.height), float(p.height), mat)
				if mesh == null: return
				var diagnostic: Dictionary = MeshDiagnostics.inspect(mesh)
				diagnostic["floor_id"] = str(f.id)
				diagnostic["piece_id"] = str(p.id)
				floor_diagnostics.append(diagnostic)
				if not diagnostic.valid:
					push_error("Floor mesh diagnostic failed: " + JSON.stringify(diagnostic))
					mesh.free()
					return
				floor_colliders[str(p.collider_ref)] = true
				study_floor_surfaces[str(p.id)] = {"node":mesh,"rect":r,"elevation":float(p.z)+float(p.height),"thickness":float(p.height),"floor_id":str(f.id)}
			else:
				mesh = visual(p,mat)
			mesh.set_meta("kind",p.kind)
			if p.has("room"): mesh.set_meta("room",p.room)
			meshes.add_child(mesh)
		for wall in f.get("wall_segments", []):
			var apertures: Array = []
			for opening_id in wall.openings:
				var found := false
				for opening in f.openings:
					if str(opening.id) != str(opening_id): continue
					if str(opening.get("wall_ref", "")) != str(wall.id):
						push_error("Wall opening binding mismatch: " + str(opening_id))
						return
					apertures.append({"id":opening.id,"from":opening.from,"to":opening.to,"bottom":float(opening.bottom)-float(wall.start[2]),"top":float(opening.top)-float(wall.start[2])})
					found = true
					break
				if not found:
					push_error("Missing wall opening: " + str(opening_id))
					return
			var a: Array = wall.start
			var b: Array = wall.end
			var sc: Array = wall.start_cut
			var ec: Array = wall.end_cut
			var built := WallSurface.create(str(wall.id), Vector3(a[0],a[1],a[2]), Vector3(b[0],b[1],b[2]), float(wall.thickness), float(wall.height), apertures, Vector2(sc[0],sc[1]), Vector2(ec[0],ec[1]), wall_base)
			if built == null: return
			var wall_report: Dictionary = MeshDiagnostics.inspect(built)
			wall_report["wall_id"] = str(wall.id)
			wall_diagnostics.append(wall_report)
			if not wall_report.valid:
				push_error("Wall mesh diagnostic failed: " + JSON.stringify(wall_report))
				built.free()
				return
			built.set_meta("kind", "wall_component")
			meshes.add_child(built)
		for flight in f.get("straight_stairs", []):
			var stair_spec: Dictionary = {}
			for stair_entry in data.effective_stairs:
				if str(stair_entry.id) == str(flight.stair_ref): stair_spec = stair_entry; break
			if stair_spec.is_empty() or int(flight.segment_index) >= stair_spec.segments.size():
				push_error("Invalid stair source: " + str(flight.id))
				return
			var segment: Dictionary = stair_spec.segments[int(flight.segment_index)]
			var a: Array = segment.points[int(flight.from_point_index)]
			var b: Array = segment.points[int(flight.to_point_index)]
			var delta := Vector2(float(b[0])-float(a[0]),float(b[1])-float(a[1]))
			var built := StraightStair.create(str(flight.id), delta.length(), float(b[2])-float(a[2]), float(segment.segment_widths[int(flight.width_index)]), int(segment[str(flight.count_field)]), float(flight.tread_thickness), float(flight.ramp_thickness), stair)
			if built == null: return
			built.position = point(a)
			var direction := Coordinates.to_godot(Vector3(delta.x,delta.y,0)).normalized()
			built.rotation.y = atan2(-direction.z,direction.x)
			var diagnostic: Dictionary = MeshDiagnostics.inspect(built)
			diagnostic["stair_id"] = str(flight.id)
			stair_diagnostics.append(diagnostic)
			if not diagnostic.valid:
				push_error("Stair diagnostic failed: " + JSON.stringify(diagnostic))
				built.free()
				return
			built.set_meta("kind", "stairs")
			built.set_meta("clearance_max_spacing", float(flight.clearance_max_spacing))
			meshes.add_child(built)
			study_stairs[str(flight.id)] = built
		for ceiling in f.get("ceiling_surfaces", []):
			var room_spec: Dictionary = {}
			for room in f.rooms:
				if str(room.id) == str(ceiling.room_ref): room_spec = room; break
			if room_spec.is_empty():
				push_error("Missing ceiling room: " + str(ceiling.room_ref))
				return
			var r: Array = room_spec.bbox
			var outer := PackedVector2Array([Vector2(r[0],r[1]), Vector2(r[2],r[1]), Vector2(r[2],r[3]), Vector2(r[0],r[3])])
			var holes: Array[PackedVector2Array] = []
			for hole_id in ceiling.hole_refs:
				var void_spec: Dictionary = {}
				for item in data.voids:
					if str(item.id) == str(hole_id): void_spec = item; break
				if void_spec.is_empty():
					push_error("Missing ceiling void: " + str(hole_id))
					return
				var stair_spec: Dictionary = {}
				for stair_entry in data.effective_stairs:
					if str(stair_entry.id) == str(void_spec.stair_ref): stair_spec = stair_entry; break
				if stair_spec.is_empty() or int(void_spec.segment_index) >= stair_spec.segments.size():
					push_error("Invalid ceiling stair source: " + str(hole_id))
					return
				var segment: Dictionary = stair_spec.segments[int(void_spec.segment_index)]
				if not segment.has(str(void_spec.source_field)):
					push_error("Missing ceiling hole source: " + str(hole_id))
					return
				var hole: Array = segment[str(void_spec.source_field)]
				holes.append(PackedVector2Array([Vector2(hole[0],hole[1]),Vector2(hole[2],hole[1]),Vector2(hole[2],hole[3]),Vector2(hole[0],hole[3])]))
			var surface: Dictionary = {}
			for piece in f.pieces:
				if str(piece.id) == str(ceiling.replaces_pieces[0]): surface = piece.get("surface", {}); break
			if surface.is_empty():
				push_error("Missing ceiling material: " + str(ceiling.id))
				return
			var built := CeilingSurface.create(str(ceiling.id), outer, holes, float(ceiling.underside), float(ceiling.thickness), configured_surface(surface, false), bool(ceiling.collision_enabled))
			if built == null: return
			var diagnostic: Dictionary = MeshDiagnostics.inspect(built)
			diagnostic["ceiling_id"] = str(ceiling.id)
			ceiling_diagnostics.append(diagnostic)
			if not diagnostic.valid:
				push_error("Ceiling diagnostic failed: " + JSON.stringify(diagnostic))
				built.free()
				return
			built.set_meta("ceiling_component", true)
			meshes.add_child(built)
		for opening in f.openings:
			if not opening.has("frame"): continue
			var frame: Dictionary = opening.frame
			var frame_origin: Array = frame.origin_design
			var frame_node := OpeningFrame.create(str(frame.id), float(opening.structural_width), float(opening.structural_height), float(frame.depth), float(frame.thickness), configured_surface(frame.material, false))
			if frame_node == null: return
			frame_node.position = point(frame_origin)
			frame_node.rotation_degrees.y = Coordinates.yaw_to_godot(float(frame.orientation_yaw) - float(frame.model_forward_yaw))
			frame_node.set_meta("kind", "door_frame")
			var frame_report: Dictionary = MeshDiagnostics.inspect(frame_node)
			frame_report["object_id"] = str(frame.id)
			wall_diagnostics.append(frame_report)
			if not frame_report.valid:
				push_error("Door frame mesh diagnostic failed: " + JSON.stringify(frame_report))
				frame_node.free()
				return
			meshes.add_child(frame_node)
			if not opening.has("door"): continue
			var door: Dictionary = opening.door
			var leaf := HingedDoor.create(str(door.id), float(door.width), float(door.height), float(door.thickness), Coordinates.yaw_to_godot(float(door.angle_max)), Coordinates.yaw_to_godot(float(door.angle_min)), configured_surface(door.material, false))
			if leaf == null: return
			leaf.position = point(door.hinge_design)
			leaf.rotation_degrees.y = Coordinates.yaw_to_godot(float(door.orientation_yaw) - float(door.model_forward_yaw))
			leaf.set_meta("kind", "door_panel")
			leaf.set_meta("review_open_angle", Coordinates.yaw_to_godot(float(door.open_angle)))
			leaf.set_meta("review_control_radius_m", float(door.review_control_radius_m))
			leaf.set_meta("review_motion_speed", absf(float(door.open_angle)) / float(door.motion_duration_seconds))
			leaf.set_meta("opening_id", str(opening.id))
			leaf.set_meta("clear_width", float(frame.net_width))
			leaf.set_meta("clearance_measurement", opening.clearance_measurement)
			var leaf_report: Dictionary = MeshDiagnostics.inspect(leaf)
			leaf_report["object_id"] = str(door.id)
			wall_diagnostics.append(leaf_report)
			if not leaf_report.valid:
				push_error("Door leaf mesh diagnostic failed: " + JSON.stringify(leaf_report))
				leaf.free()
				return
			meshes.add_child(leaf)
			study_doors[str(door.id)] = leaf
		for p in f.get("furniture",[]):
			var visual_node: Node3D = null
			var kind_str := str(p.get("kind", ""))
			var p_id := str(p.get("id", "")).to_lower()
			var resolved_kind := kind_str
			if kind_str == "source_furniture":
				if "bed" in p_id: resolved_kind = "bed"
				elif "nightstand" in p_id: resolved_kind = "nightstand"
				elif "desk" in p_id: resolved_kind = "desk"
				elif "chair" in p_id: resolved_kind = "chair"
				elif "wardrobe" in p_id or "closet" in p_id: resolved_kind = "wardrobe"
				elif "wash" in p_id or "basin" in p_id: resolved_kind = "washstand"
				elif "sofa" in p_id or "couch" in p_id or "armchair" in p_id: resolved_kind = "sofa"
				elif "tea" in p_id or "coffee" in p_id: resolved_kind = "coffeetable"
				elif "lamp" in p_id or "light" in p_id: resolved_kind = "lamp"
				elif "book" in p_id: resolved_kind = "book"
				elif "paper" in p_id or "letter" in p_id: resolved_kind = "paper"
				elif "carpet" in p_id or "rug" in p_id: resolved_kind = "carpet"
				elif "curtain" in p_id: resolved_kind = "curtains"
				elif "painting" in p_id or "picture" in p_id or "frame" in p_id: resolved_kind = "painting"
				elif "plate" in p_id or "sign" in p_id: resolved_kind = "doorplate"
				elif "evidence" in p_id: resolved_kind = "evidence"

			if resolved_kind in ["chair", "bench", "desk"] and not p.has("size"):
				visual_node = Furniture.create(resolved_kind)
			elif resolved_kind in [
				"bed", "cabinet", "nightstand", "desk", "chair", "counter", "stove", "sink", "table",
				"wardrobe", "washstand", "sofa", "coffeetable", "lamp", "book", "paper",
				"evidence", "carpet", "curtains", "painting", "doorplate", "pillow", "quilt"
			]:
				var sz := Vector3(1.0, 1.0, 1.0)
				if p.has("size"):
					sz = Coordinates.size_to_godot(Vector3(float(p.size[0]), float(p.size[1]), float(p.size[2])))
				visual_node = Furniture.create_sized(resolved_kind, sz)
			else:
				var r_w: float = float(p.size[0]) if p.has("size") else 1.0
				var r_d: float = float(p.size[1]) if p.has("size") else 1.0
				var r_h: float = float(p.size[2]) if p.has("size") else 1.0
				visual_node = visual({"rect":[-r_w/2.0,-r_d/2.0,r_w/2.0,r_d/2.0],"z":0,"height":r_h,"kind":"furniture"},wall_base)

			if visual_node == null:
				push_error("Invalid furniture declaration: " + str(p))
				continue

			var prop: Node3D = null
			if p.has("interaction") and p.interaction.get("status") == "ready":
				var adapted: Dictionary = WorldObjectAdapter.convert(p, Vector3(0, 460, 48))
				if not adapted.is_empty():
					var wo := WorldObject.new()
					if wo.build(adapted, visual_node):
						prop = wo
			if prop == null:
				prop = Node3D.new()
				prop.name = str(p.id)
				prop.add_child(visual_node)

			prop.position = point(p.position)
			prop.rotation_degrees.y = 0.0 if bool(p.get("source_box_axis_aligned", false)) else Coordinates.yaw_to_godot(float(p.yaw))
			prop.set_meta("kind", "furniture")
			prop.set_meta("world_object_id", str(p.id))
			meshes.add_child(prop)
		var box_count := 0
		var convex_count := 0
		for c in f.colliders:
			if replaced_colliders.has(str(c.id)):
				overlay.add_child(visual(c,debug_box))
				continue
			if floor_colliders.has(str(c.id)):
				overlay.add_child(visual(c,debug_floor))
				continue
			var body := StaticBody3D.new()
			body.name = "Solid_"+str(c.id).replace("-","_")
			body.collision_layer = PhysicsLayers.SOLID
			body.collision_mask = 0
			physics.add_child(body)
			var shape := CollisionShape3D.new()
			if c.shape=="box":
				var r: Array = c.rect
				var box := BoxShape3D.new()
				box.size = Coordinates.size_to_godot(Vector3(r[2]-r[0],r[3]-r[1],c.height))
				shape.shape = box
				body.position = point([(r[0]+r[2])/2.0,(r[1]+r[3])/2.0,c.z+c.height/2.0])
				box_count += 1
			else:
				var pts := PackedVector3Array()
				var center := Vector3.ZERO
				for p in c.points:
					center += point(p)/c.points.size()
				for p in c.points:
					pts.append(point(p)-center)
				var convex := ConvexPolygonShape3D.new()
				convex.points = pts
				shape.shape = convex
				body.position = center
				convex_count += 1
			body.add_child(shape)
			overlay.add_child(visual(c,debug_convex if c.shape=="convex" else debug_floor if c.kind=="floor" else debug_box))
		floors[f.id] = meshes
		collision_visuals[f.id] = overlay
		counts[f.id] = {"box":box_count,"convex":convex_count+f.get("straight_stairs", []).size(),"stair_mesh_convex":f.get("straight_stairs", []).size(),"concave":floor_colliders.size()+f.get("wall_segments", []).size()+f.get("ceiling_surfaces", []).size(),"floor_mesh_concave":floor_colliders.size(),"wall_mesh_concave":f.get("wall_segments", []).size(),"ceiling_mesh_concave":f.get("ceiling_surfaces", []).size(),"box_excludes_generated_mesh":true}
		print("FRAMEWORK_IMPORTED ", f.id, " ", counts[f.id])
	build_completed = true

func show_floor(id: String, collision_mode: bool, cutaway: bool) -> void:
	for key in floors:
		var selected: bool = id=="ALL" or key==id
		floors[key].visible = not collision_mode
		collision_visuals[key].visible = collision_mode
		for group in [floors[key],collision_visuals[key]]:
			for mesh in group.get_children():
				var kind: String = mesh.get_meta("kind")
				var connecting: bool = key=="1F" and id=="2F" and kind in ["stairs","landing","stair_support","handrail","stair_ramp","rail_guard"]
				if mesh.has_meta("ceiling_component"):
					mesh.visible = selected or connecting
					CeilingSurface.set_visual_visible(mesh, not cutaway and ceilings_visible)
				else:
					mesh.visible = (selected or connecting) and not (cutaway and kind in ["wall_upper","roof","ceiling"]) and not (kind=="ceiling" and not ceilings_visible)
