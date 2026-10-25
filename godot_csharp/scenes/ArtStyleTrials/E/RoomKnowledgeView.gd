extends Node3D
## Presentation only. The caller supplies observer-owned room knowledge.
## Registering geometry never marks a room explored or publishes observations.
const DIM=preload("res://scenes/ArtStyleTrials/E/memory_dim.gdshader")
const FOG=preload("res://scenes/ArtStyleTrials/E/explored_fog.gdshader")
var live: Dictionary={}
var memories: Dictionary={}
var doors: Dictionary={}
var map_ids: Dictionary={}
var current_id := ""

func register_room(id: String, map_id: String, room: Node3D, door_model: Node3D=null) -> void:
	live[id]=room
	map_ids[id]=map_id
	if door_model: doors[id]=door_model
	room.visible=false
	if door_model: door_model.visible=false

func supply_observed_snapshot(id: String, snapshot: Node3D, floor_rects: Array) -> void:
	# Snapshot must come from the observer's last delivered view, not hidden live state.
	if memories.has(id):
		remove_child(memories[id])
		memories[id].queue_free()
	var frozen: Node3D=snapshot.duplicate()
	frozen.name="Remembered_"+id.replace(":","_")
	freeze(frozen)
	add_child(frozen)
	memories[id]=frozen
	for rect in floor_rects:
		var plane := MeshInstance3D.new()
		var shape := PlaneMesh.new()
		shape.size=Vector2(rect[2],rect[3])
		plane.mesh=shape
		plane.position=Vector3(rect[0]+rect[2]/2.0,0.09,rect[1]+rect[3]/2.0)
		var mat := ShaderMaterial.new()
		mat.shader=FOG
		plane.material_override=mat
		plane.cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		frozen.add_child(plane)
	frozen.visible=false

func apply_observer_view(map_id: String, current: String, explored: Array) -> void:
	current_id=current
	for id in live:
		var active: bool=map_ids[id]==map_id and id==current
		live[id].visible=active
		live[id].process_mode=Node.PROCESS_MODE_INHERIT if active else Node.PROCESS_MODE_DISABLED
	for id in memories:
		memories[id].visible=map_ids.get(id,"")==map_id and id!=current and id in explored
	for id in doors: doors[id].visible=map_ids.get(id,"")==map_id and id==current

static func dim_material(source: Material) -> Material:
	if source==null: return null
	var copy: Material=source.duplicate()
	var overlay := ShaderMaterial.new()
	overlay.shader=DIM
	copy.next_pass=overlay
	return copy

static func freeze(node: Node) -> void:
	node.process_mode=Node.PROCESS_MODE_DISABLED
	if node is Node3D and (node.has_meta("actor_id") or node.has_meta("door_model") or node is Light3D or node is GPUParticles3D):
		node.visible=false
		return
	if node is MeshInstance3D:
		# Animated effects are not static furniture and must not survive in memory.
		for surface in node.mesh.get_surface_count():
			var active: Material=node.get_active_material(surface)
			if active is ShaderMaterial and active.shader.code.contains("TIME"):
				node.visible=false
				return
		if node.material_override: node.material_override=dim_material(node.material_override)
		else:
			for i in node.mesh.get_surface_count(): node.set_surface_override_material(i,dim_material(node.get_active_material(i)))
	elif node is GridMap:
		var lib: MeshLibrary=node.mesh_library.duplicate()
		for item in lib.get_item_list():
			var mesh: Mesh=lib.get_item_mesh(item).duplicate()
			if mesh is PrimitiveMesh: mesh.material=dim_material(mesh.material)
			else:
				for i in mesh.get_surface_count(): mesh.surface_set_material(i,dim_material(mesh.surface_get_material(i)))
			lib.set_item_mesh(item,mesh)
		node.mesh_library=lib
	for child in node.get_children(): freeze(child)
