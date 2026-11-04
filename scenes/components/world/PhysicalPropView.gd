extends Node3D
## Physical display of declared facts. Ownership remains in the Core world.
const RENDER_VISIBILITY = preload("res://scenes/components/architecture/RenderVisibility.gd")
var bridge: Node
var body: CharacterBody3D
var streaming: Node3D
var viewer := ""
var specs: Array = []
var nodes: Dictionary = {}
var values: Dictionary = {}
var revision := -1
var work_prop: Node3D
var actors: Dictionary = {}

func configure(port: Node,actor: CharacterBody3D,rooms: Node3D,owner: String,declarations: Array, actor_bodies: Dictionary = {}) -> void:
	bridge=port;body=actor;streaming=rooms;viewer=owner;specs=declarations
	actors=actor_bodies.duplicate(); actors[owner]=actor

func fact(binding: Dictionary) -> Variant:
	var value: Dictionary=bridge.PhysicalFact(str(binding.object),str(binding.facet),str(binding.field))
	return value.get("Value") if bool(value.get("Known",false)) else null

func material(color: String,metal := false) -> StandardMaterial3D:
	var result:=StandardMaterial3D.new()
	result.albedo_color=Color(color)
	result.roughness=.5 if metal else .95
	result.metallic=.8 if metal else 0.0
	return result

func build(spec: Dictionary) -> Node3D:
	var root:=Node3D.new()
	var size: Array=spec.get("size",[.1,.1,.1])
	var primitive:=str(spec.primitive)
	if primitive=="writing":
		var label:=Label3D.new()
		label.text=str(spec.text)
		label.font_size=int(spec.get("font_size",24))
		label.pixel_size=float(spec.get("pixel_size",0.002))
		label.modulate=Color(str(spec.get("color","b4cde0")))
		label.horizontal_alignment=HORIZONTAL_ALIGNMENT_LEFT
		label.vertical_alignment=VERTICAL_ALIGNMENT_TOP
		root.add_child(label)
		return root
	var mesh_node:=MeshInstance3D.new()
	if primitive=="cloth":
		var surface:=SurfaceTool.new()
		surface.begin(Mesh.PRIMITIVE_TRIANGLES)
		for row in 8:
			for column in 6:
				for corner in [Vector2i(0,0),Vector2i(0,1),Vector2i(1,0),Vector2i(1,0),Vector2i(0,1),Vector2i(1,1)]:
					var u:=float(column+corner.x)/6.0
					var v:=float(row+corner.y)/8.0
					surface.set_uv(Vector2(u,v))
					surface.add_vertex(Vector3((u-.5)*float(size[0]),sin(u*15.0+v*2.0)*float(size[1])*.4,(v-.5)*float(size[2])))
		surface.generate_normals()
		mesh_node.mesh=surface.commit()
		var cloth:=material(str(spec.get("color","777365")))
		cloth.cull_mode=BaseMaterial3D.CULL_DISABLED
		mesh_node.material_override=cloth
	elif primitive=="bowl":
		var bowl:=SphereMesh.new()
		bowl.radius=.5;bowl.height=.5;bowl.radial_segments=24;bowl.rings=8
		mesh_node.mesh=bowl
		mesh_node.scale=Vector3(size[0],size[1]*2.0,size[2])
		mesh_node.material_override=material(str(spec.get("color","c3bbae")))
	elif primitive=="bread":
		var sphere:=SphereMesh.new()
		sphere.radius=.5;sphere.height=1.0;sphere.radial_segments=20;sphere.rings=10
		mesh_node.mesh=sphere
		mesh_node.scale=Vector3(size[0],size[1],size[2])
		mesh_node.material_override=material(str(spec.get("color","bb8745")))
	elif primitive=="hook":
		var ring:=TorusMesh.new()
		ring.inner_radius=.021;ring.outer_radius=.035;ring.rings=12;ring.ring_segments=8
		mesh_node.mesh=ring
		mesh_node.rotation.z=PI*.5
		mesh_node.material_override=material(str(spec.get("color","696c68")),true)
	else:
		var box:=BoxMesh.new();box.size=Vector3(size[0],size[1],size[2]);mesh_node.mesh=box
		mesh_node.material_override=material(str(spec.get("color","777365")))
	root.add_child(mesh_node)
	if bool(spec.get("solid",false)):
		var solid:=StaticBody3D.new()
		solid.collision_layer=preload("res://scenes/components/world/PhysicsLayers.gd").SOLID
		solid.collision_mask=0
		var collider:=CollisionShape3D.new()
		var box_shape:=BoxShape3D.new()
		box_shape.size=Vector3(size[0],size[1],size[2])
		collider.shape=box_shape;solid.add_child(collider);root.add_child(solid)
	return root

func synchronize(world_revision: int) -> void:
	var changed:=revision!=world_revision
	for spec: Dictionary in specs:
		var id:=str(spec.id)
		if changed:
			values[id]={"location":fact(spec.location) if spec.has("location") else "","supply":fact(spec.supply) if spec.has("supply") else 1,"state":fact(spec.state) if spec.has("state") else null,"visible_when":fact(spec.visible_when) if spec.has("visible_when") else true}
		var location:=str(values[id].location)
		var holder: CharacterBody3D=actors.get(location)
		var held:=is_instance_valid(holder)
		var placement: Dictionary=spec.get("placements",{}).get(location,{})
		var room:=str(placement.get("room",spec.room))
		if held: room=streaming.RoomAt(holder.global_position)
		if spec.has("follow_actor") and is_instance_valid(actors.get(str(spec.follow_actor))):
			room=streaming.RoomAt(actors[str(spec.follow_actor)].global_position)
		var mapped: bool=not spec.has("placements") or not placement.is_empty()
		var wanted: bool=held or mapped and not room.is_empty() and streaming.RoomResident(room)
		var node: Node3D=nodes.get(id)
		if not wanted:
			if is_instance_valid(node):
				bridge.RecordProp(id,location,"",node.global_position.x,node.global_position.y,node.global_position.z,"released")
				for existing: CharacterBody3D in actors.values():
					if existing.carried_item_node==node: existing.stop_carrying()
				node.free();nodes.erase(id)
			continue
		var created:=not is_instance_valid(node)
		if created:
			node=build(spec);node.set_meta("world_object_id",id);add_child(node);nodes[id]=node
		if spec.has("state") and (changed or created):
			var value: Variant=values[id].state
			if str(spec.primitive)=="writing":
				var label:=node.get_child(0) as Label3D
				if is_instance_valid(label): label.text=str(spec.get("text",""))+("?" if value==null else str(value))
			else:
				var color:=str(spec.get("unknown_color","777777")) if value==null else str(spec.get("true_color","6a9868") if bool(value) else spec.get("false_color","a45f54"))
				var visual:=node.get_child(0) as MeshInstance3D
				if is_instance_valid(visual) and visual.material_override is StandardMaterial3D:
					var existing:=visual.material_override as StandardMaterial3D
					var desired:=Color(color)
					if existing.albedo_color!=desired: existing.albedo_color=desired
		if held:
			if node.get_parent()!=holder.get_hand_slot():
				for existing: CharacterBody3D in actors.values():
					if existing.carried_item_node==node: existing.stop_carrying()
				holder.start_carrying(node)
				attach_in_metres(node,spec,holder)
			if str(spec.get("held_mode", "rigid")) == "drape":
				attach_draped_in_metres(node,spec,holder)
		else:
			for existing: CharacterBody3D in actors.values():
				if existing.carried_item_node==node: existing.stop_carrying();add_child(node)
			var p: Array=placement.get("position",spec.position)
			if spec.has("state_positions") and values[id].state!=null:
				p=spec.state_positions.get("true" if bool(values[id].state) else "false",p)
			node.scale=Vector3.ONE
			node.global_position=Vector3(p[0],p[1],p[2])
			if spec.has("follow_actor") and is_instance_valid(actors.get(str(spec.follow_actor))):
				var owner: CharacterBody3D=actors[str(spec.follow_actor)]
				var offset: Array=spec.get("actor_offset",[0,0,0])
				node.global_position=owner.global_transform*Vector3(offset[0],offset[1],offset[2])
				room=streaming.RoomAt(owner.global_position)
			var r: Array=placement.get("rotation",spec.get("rotation",[0,0,0]));node.rotation_degrees=Vector3(r[0],r[1],r[2])
		node.visible=(held or streaming.RoomActive(room)) and float(values[id].supply)>0 and bool(values[id].visible_when)
		if bool(spec.get("solid",false)):
			for child in node.get_children():
				if child is StaticBody3D: child.collision_layer=preload("res://scenes/components/world/PhysicsLayers.gd").SOLID if node.visible else 0
		RENDER_VISIBILITY.apply(node,streaming.RoomVisible(room))
		if changed or created:
			bridge.RecordProp(id,location,location if held else "",node.global_position.x,node.global_position.y,node.global_position.z,"attached" if held else "placed")
	revision=world_revision

func begin_work(motor: Dictionary) -> void:
	end_work()
	if not motor.has("work_prop"): return
	work_prop=build(motor.work_prop)
	body.get_hand_slot().add_child(work_prop)
	attach_in_metres(work_prop,motor.work_prop)

func attach_in_metres(node: Node3D,spec: Dictionary, holder: CharacterBody3D = null) -> void:
	if holder==null: holder=body
	var slot: Node3D=holder.get_hand_slot()
	var rotation: Array=spec.get("held_rotation",[0,0,0])
	var offset: Array=spec.get("held_offset",[0,0,.035])
	var hand_basis:=slot.global_basis.orthonormalized()
	node.global_basis=hand_basis*Basis.from_euler(Vector3(deg_to_rad(float(rotation[0])),deg_to_rad(float(rotation[1])),deg_to_rad(float(rotation[2]))))
	node.global_position=slot.global_position+hand_basis*Vector3(offset[0],offset[1],offset[2])

func attach_draped_in_metres(node: Node3D,spec: Dictionary, holder: CharacterBody3D = null) -> void:
	if holder==null: holder=body
	var slot: Node3D=holder.get_hand_slot()
	var offset: Array=spec.get("held_offset",[0,0,0])
	var anchor:=slot.global_position+slot.global_basis.orthonormalized()*Vector3(offset[0],offset[1],offset[2])
	var horizontal: Vector3=holder.visual.global_basis.x
	horizontal.y=0.0
	if horizontal.length_squared()<0.0001: horizontal=Vector3.RIGHT
	horizontal=horizontal.normalized()
	var down:=Vector3.DOWN
	var normal:=down.cross(horizontal).normalized()
	var size: Array=spec.get("size",[0.1,0.1,0.1])
	node.global_transform=Transform3D(Basis(horizontal,normal,down),anchor+down*float(size[2])*0.5)

func observation() -> Dictionary:
	var result: Dictionary={}
	for id in nodes:
		var node: Node3D=nodes[id]
		if not is_instance_valid(node): continue
		var holder: String=""
		for actor_id in actors:
			if is_instance_valid(actors[actor_id]) and actors[actor_id].carried_item_node==node: holder=str(actor_id);break
		result[id]={"instance_id":node.get_instance_id(),"parent":str(node.get_parent().get_path()),"position":[node.global_position.x,node.global_position.y,node.global_position.z],"world_scale":node.global_basis.get_scale(),"visible":node.is_visible_in_tree(),"carried":not holder.is_empty(),"holder":holder}
	return result

func end_work() -> void:
	if is_instance_valid(work_prop): work_prop.queue_free()
	work_prop=null
