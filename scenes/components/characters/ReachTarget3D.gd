extends RefCounted
## Drives the skinned arm toward a world-space contact, then measures the hand.
var ik: Node
var hand: Node3D
var target := Vector3.ZERO
var elapsed := 0.0
var tolerance := 0.08
var timeout := 3.0
var last_error := INF
var contact_seconds := 0.0
var surface_error := INF
var surface_samples := 0
var transition_seconds := 0.8
var skeleton: Skeleton3D
var surfaces: Array = []

func begin(body: CharacterBody3D, point: Vector3, limit: float, seconds: float, transition: float = 0.8) -> bool:
	skeleton = body.visual.get_meta("skeleton", null)
	hand = body.get_hand_slot()
	if skeleton == null or hand == null or not ClassDB.class_exists("SkeletonIK3D"): return false
	var root := ""
	var tip := ""
	for index in skeleton.get_bone_count():
		var bone := str(skeleton.get_bone_name(index))
		var name := bone.trim_prefix("mixamorig:").trim_prefix("mixamorig_")
		if name in ["RightArm", "upperarm.r"]: root = bone
		if name in ["RightHand", "hand.r"]: tip = bone
	if root.is_empty() or tip.is_empty(): return false
	surfaces.clear()
	for mesh: MeshInstance3D in body.visual.find_children("*", "MeshInstance3D", true, false):
		if mesh.mesh == null or mesh.skin == null: continue
		for surface in mesh.mesh.get_surface_count():
			var arrays := mesh.mesh.surface_get_arrays(surface)
			var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
			var joints = arrays[Mesh.ARRAY_BONES]
			var weights: PackedFloat32Array = arrays[Mesh.ARRAY_WEIGHTS]
			if vertices.is_empty() or weights.size() != joints.size() or joints.size() % vertices.size() != 0: continue
			var count := int(joints.size() / vertices.size())
			var selected := PackedInt32Array()
			for vertex in vertices.size():
				var influence := 0.0
				for slot in count:
					var binding := int(joints[vertex * count + slot])
					if binding < 0 or binding >= mesh.skin.get_bind_count(): continue
					var bone := mesh.skin.get_bind_bone(binding)
					var bind_name := str(mesh.skin.get_bind_name(binding))
					if not bind_name.is_empty(): bone = skeleton.find_bone(bind_name)
					if bone < 0: continue
					var name := str(skeleton.get_bone_name(bone)).to_lower()
					if "righthand" in name or name == "hand.r": influence += float(weights[vertex * count + slot])
				if influence >= 0.5: selected.append(vertex)
			if not selected.is_empty(): surfaces.append({"mesh":mesh,"vertices":vertices,"joints":joints,"weights":weights,"count":count,"selected":selected})
	ik = ClassDB.instantiate("SkeletonIK3D")
	ik.set("root_bone", root)
	ik.set("tip_bone", tip)
	ik.set("target", Transform3D(Basis.IDENTITY, point))
	ik.set("interpolation", 0.0)
	skeleton.add_child(ik)
	ik.connect("modification_processed", measure_surface)
	ik.call("start")
	target = point
	tolerance = limit
	timeout = seconds
	transition_seconds = maxf(0.05, transition)
	elapsed = 0.0
	contact_seconds = 0.0
	last_error = INF
	return true

func step(delta: float) -> String:
	elapsed += delta
	if not is_instance_valid(ik) or not is_instance_valid(hand): return "unavailable"
	ik.set("interpolation", smoothstep(0.0, transition_seconds, elapsed))
	last_error = hand.global_position.distance_to(target)
	contact_seconds = contact_seconds + delta if elapsed >= transition_seconds and last_error <= tolerance else 0.0
	if contact_seconds >= 0.1: return "contact"
	return "timeout" if elapsed >= timeout else "reaching"

func measure_surface() -> void:
	surface_error = INF
	surface_samples = 0
	for data: Dictionary in surfaces:
		var mesh: MeshInstance3D = data.mesh
		if not is_instance_valid(mesh): continue
		var transforms: Array[Transform3D] = []
		for binding in mesh.skin.get_bind_count():
			var bone := mesh.skin.get_bind_bone(binding)
			var name := str(mesh.skin.get_bind_name(binding))
			if not name.is_empty(): bone = skeleton.find_bone(name)
			transforms.append(skeleton.get_bone_global_pose(bone) * mesh.skin.get_bind_pose(binding) if bone >= 0 else Transform3D.IDENTITY)
		for vertex: int in data.selected:
			var point := Vector3.ZERO
			for slot in int(data.count):
				var index: int = vertex * int(data.count) + slot
				var binding := int(data.joints[index])
				var weight := float(data.weights[index])
				if weight > 0.0 and binding >= 0 and binding < transforms.size(): point += (transforms[binding] * data.vertices[vertex]) * weight
			surface_error = minf(surface_error, (mesh.global_transform * point).distance_to(target))
			surface_samples += 1

func stop() -> void:
	if is_instance_valid(ik):
		ik.call("stop")
		ik.queue_free()
	ik = null
