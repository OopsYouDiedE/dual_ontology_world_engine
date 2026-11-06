extends RefCounted
## Only the frame of the currently open crossing is exempted for this body.
var opening := ""
var bodies: Array[PhysicsBody3D] = []
var actor: PhysicsBody3D

func update(subject: PhysicsBody3D, rooms: Node, next_opening: String) -> void:
	if next_opening==opening: return
	clear()
	actor=subject
	if next_opening.is_empty() or not rooms.DoorOpen(next_opening): return
	opening=next_opening
	find_frame(rooms,"frame."+opening)

func find_frame(node: Node, id: String) -> void:
	if str(node.get_meta("world_object_id",""))==id and str(node.get_meta("kind",""))=="door_frame":
		add_bodies(node)
		return
	for child in node.get_children(): find_frame(child,id)

func add_bodies(node: Node) -> void:
	if node is PhysicsBody3D:
		actor.add_collision_exception_with(node)
		bodies.append(node)
	for child in node.get_children(): add_bodies(child)

func clear() -> void:
	if is_instance_valid(actor):
		for other in bodies:
			if is_instance_valid(other): actor.remove_collision_exception_with(other)
	bodies.clear();opening=""
