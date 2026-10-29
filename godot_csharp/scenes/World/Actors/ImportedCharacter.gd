extends RefCounted
## KayKit Adventurers + Animations importer and adapter (CC0 1.0 Universal)

const ANIM_LIB_HELPER := preload("res://scenes/World/Actors/KayKitAnimationLibrary.gd")
const NOMINAL_ACTOR_HEIGHT := 1.32
const BASE_RIG_HEIGHT := 2.180353
const COLLISION_RADIUS := 0.18
const EYE_HEIGHT := 1.15

static func resolve_model_path(path: String, character_id: String = "") -> String:
	# 1. Direct KayKit path
	if path.begins_with("res://imported_assets/kaykit/characters/"):
		if ResourceLoader.exists(path):
			return path
	
	# 2. Map by character ID or legacy model filename
	var id := character_id.to_lower()
	var fname := path.get_file().to_lower()
	
	if id == "detective" or fname == "model_06.glb":
		return "res://imported_assets/kaykit/characters/Knight.glb"
	elif id in ["huo", "engineer"] or fname in ["model_04.glb", "model_13.glb"]:
		return "res://imported_assets/kaykit/characters/Engineer.glb"
	elif id in ["shen", "chef"] or fname == "model_07.glb":
		return "res://imported_assets/kaykit/characters/Barbarian.glb"
	elif id in ["su", "musician", "veronica", "chemist", "moran", "painter", "lucian", "poet", "doctor", "mora"] or fname in ["model_02.glb", "model_10.glb", "model_12.glb", "model_15.glb", "model_16.glb"]:
		return "res://imported_assets/kaykit/characters/Mage.glb"
	elif id in ["quer", "thief", "bertrand", "marksman", "ruan", "chessplayer", "demon"] or fname in ["model_01.glb", "model_03.glb", "model_09.glb", "model_14.glb", "model_17.glb"]:
		return "res://imported_assets/kaykit/characters/Rogue.glb"
	elif id in ["selma", "naturalist", "gu", "archaeologist", "elena", "dancer"] or fname in ["model_05.glb", "model_08.glb", "model_11.glb"]:
		return "res://imported_assets/kaykit/characters/Druid.glb"
	
	# 3. If direct path exists, use it
	if ResourceLoader.exists(path):
		return path
	
	# 4. Default fallback to Knight
	return "res://imported_assets/kaykit/characters/Knight.glb"

static func attach(parent: Node3D, path: String, height: float = NOMINAL_ACTOR_HEIGHT, yaw_degrees: float = 0.0, character_id: String = "") -> bool:
	if height <= 0.0:
		height = NOMINAL_ACTOR_HEIGHT
	var resolved_path := resolve_model_path(path, character_id)
	var packed := load(resolved_path) as PackedScene
	if packed == null:
		push_error("Failed to load character scene: " + resolved_path)
		return false
	var model := packed.instantiate() as Node3D
	if model == null:
		return false
	
	# Unified uniform scaling: Proportions are preserved across X, Y, Z.
	# Feet origin remains exactly at Y = 0.0 on the floor.
	var scale_factor := height / BASE_RIG_HEIGHT
	model.scale = Vector3(scale_factor, scale_factor, scale_factor)
	model.position = Vector3.ZERO
	model.rotation.y = deg_to_rad(yaw_degrees)
	
	# Find Skeleton3D and create bone attachment slots for props and items
	var skel: Skeleton3D = _find_skeleton(model)
	var slot_r: BoneAttachment3D = null
	var slot_l: BoneAttachment3D = null
	if skel != null:
		var r_bone := "handslot.r" if skel.find_bone("handslot.r") != -1 else "hand.r"
		var l_bone := "handslot.l" if skel.find_bone("handslot.l") != -1 else "hand.l"
		
		slot_r = BoneAttachment3D.new()
		slot_r.name = "RightHandSlot"
		slot_r.bone_name = r_bone
		skel.add_child(slot_r)
		
		slot_l = BoneAttachment3D.new()
		slot_l.name = "LeftHandSlot"
		slot_l.bone_name = l_bone
		skel.add_child(slot_l)
	
	# Setup AnimationPlayer and load KayKit animation library
	var player := model.get_node_or_null("AnimationPlayer") as AnimationPlayer
	if player == null:
		player = AnimationPlayer.new()
		player.name = "AnimationPlayer"
		model.add_child(player)
	player.root_node = player.get_path_to(model)
	
	var lib: AnimationLibrary = ANIM_LIB_HELPER.get_library()
	if lib != null:
		if player.has_animation_library(""):
			player.remove_animation_library("")
		player.add_animation_library("", lib)
		player.playback_default_blend_time = 0.2
		player.play("idle")
	
	parent.add_child(model)
	parent.set_meta("model_source", resolved_path)
	parent.set_meta("presentation", "kaykit-animated-character")
	parent.set_meta("nominal_actor_height", height)
	parent.set_meta("collision_radius", COLLISION_RADIUS)
	parent.set_meta("eye_height", EYE_HEIGHT)
	parent.set_meta("feet_y", 0.0)
	parent.set_meta("animation_player", player)
	parent.set_meta("skeleton", skel)
	parent.set_meta("slot_right_hand", slot_r)
	parent.set_meta("slot_left_hand", slot_l)
	return true

static func _find_skeleton(node: Node) -> Skeleton3D:
	if node is Skeleton3D: return node
	for child in node.get_children():
		var found := _find_skeleton(child)
		if found != null: return found
	return null
