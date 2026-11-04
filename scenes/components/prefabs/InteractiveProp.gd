extends Node3D
## The host moves and authorizes actors before requesting a visual clip.
signal visual_completed(actor: Node3D, slot: StringName, clip: StringName)
var _playing := false
var _generation := 0
var _finished := false
var _active_player: AnimationPlayer
var _active_clip: StringName

func _animation_finished(name: StringName, expected: StringName, generation: int) -> void:
	if _playing and generation == _generation and name == expected:
		_finished = true

func cancel_visual() -> void:
	if is_instance_valid(_active_player) and _active_player.current_animation == _active_clip:
		_active_player.stop()
	_active_player = null
	_generation += 1
	_playing = false
	_finished = false

func _exit_tree() -> void:
	cancel_visual()

func play_visual(actor: Node3D, slot: StringName, clip: StringName) -> bool:
	var marker := get_node_or_null(NodePath("InteractionSlots/" + str(slot))) as Marker3D
	if _playing or marker == null or not is_instance_valid(actor) or not is_inside_tree(): return false
	if actor.global_position.distance_to(marker.global_position) > 0.15: return false
	var player := actor.get_node_or_null("AnimationPlayer") as AnimationPlayer
	if player == null or not player.has_animation(clip): return false
	if player.get_animation(clip).loop_mode != Animation.LOOP_NONE: return false
	_generation += 1
	var generation := _generation
	_playing = true
	_active_player = player
	_active_clip = clip
	_finished = false
	var finished_callback := _animation_finished.bind(clip, generation)
	player.animation_finished.connect(finished_callback)
	actor.global_rotation.y = marker.global_rotation.y
	player.play(clip, 0.12)
	var tree := get_tree()
	while is_inside_tree() and generation == _generation and is_instance_valid(actor) and is_instance_valid(player):
		if _finished: break
		if not player.is_playing() or player.current_animation != clip: break
		await tree.process_frame
	var completed := generation == _generation and _finished and is_inside_tree() and is_instance_valid(actor)
	if is_instance_valid(player) and player.animation_finished.is_connected(finished_callback):
		player.animation_finished.disconnect(finished_callback)
	if generation == _generation:
		_active_player = null
		_playing = false
		_finished = false
	if completed: visual_completed.emit(actor, slot, clip)
	return completed
