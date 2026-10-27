extends Node3D
## The host moves and authorizes actors before requesting a visual clip.
signal visual_completed(actor: Node3D, slot: StringName, clip: StringName)
var _playing := false

func play_visual(actor: Node3D, slot: StringName, clip: StringName) -> bool:
	var marker := get_node_or_null(NodePath("InteractionSlots/" + str(slot))) as Marker3D
	if _playing or marker == null or not is_instance_valid(actor): return false
	if actor.global_position.distance_to(marker.global_position) > 0.15: return false
	var player := actor.get_node_or_null("AnimationPlayer") as AnimationPlayer
	if player == null or not player.has_animation(clip): return false
	if player.get_animation(clip).loop_mode != Animation.LOOP_NONE: return false
	_playing = true
	actor.global_rotation.y = marker.global_rotation.y
	player.play(clip, 0.12)
	await player.animation_finished
	_playing = false
	if is_instance_valid(actor): visual_completed.emit(actor, slot, clip)
	return true
