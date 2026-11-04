extends RefCounted
## Presentation-only upper-body adaptation of the existing CC0 KayKit Interact.
## No root translation, lower-body replacement or Core state is generated here.
static var _motion: Dictionary = {}
static var _libraries: Dictionary = {}
static var build_count := 0
static var cache_hits := 0
static var build_usec := 0

static func install(player: AnimationPlayer) -> bool:
	if _motion.is_empty():
		var reader = preload("res://scenes/main/DvPresentationContent.cs").new()
		var art: Dictionary = reader.ReadRecord("res://assets/data/character_art.dv", "layouts", "character_art")
		_motion = art.get("conversation_motion", {}).duplicate(true)
	if _motion.is_empty() or not bool(_motion.get("enabled", false)): return false
	var source_name := str(_motion.get("source_clip", ""))
	if not player.has_animation(source_name): return false
	var source := player.get_animation(source_name)
	var key_parts := [source.get_instance_id(), _motion.hash()]
	for posture in ["standing", "seated"]:
		var base := str(_motion.get(posture + "_base", ""))
		key_parts.append(player.get_animation(base).get_instance_id() if player.has_animation(base) else 0)
	var cache_key := str(key_parts)
	var library: AnimationLibrary = _libraries.get(cache_key)
	if library == null:
		var started := Time.get_ticks_usec()
		library = _build_library(player, source)
		if _libraries.size() >= 64: _libraries.erase(_libraries.keys()[0])
		_libraries[cache_key] = library
		build_count += 1
		build_usec += Time.get_ticks_usec() - started
	else:
		cache_hits += 1
	var previous := player.current_animation
	var previous_time := player.current_animation_position if not previous.is_empty() else 0.0
	var was_playing := player.is_playing()
	player.stop()
	if player.has_animation_library("conversation"): player.remove_animation_library("conversation")
	player.add_animation_library("conversation", library)
	player.clear_caches()
	if was_playing and not previous.is_empty() and player.has_animation(previous):
		player.play(previous)
		player.seek(previous_time, true)
	return library.get_animation_list().size() > 0

static func _build_library(player: AnimationPlayer, source: Animation) -> AnimationLibrary:
	var library := AnimationLibrary.new()
	for posture in ["standing", "seated"]:
		var base_name := str(_motion.get(posture + "_base", ""))
		if not player.has_animation(base_name): continue
		var base := player.get_animation(base_name)
		for role in ["talk", "listen"]:
			for free_hands in [true, false]:
				var clip := _compose(base, source, role == "talk", free_hands)
				library.add_animation(role + "_" + posture + ("" if free_hands else "_occupied"), clip)
	return library

static func _compose(base: Animation, source: Animation, speaking: bool, free_hands: bool) -> Animation:
	# Build an independent, uncompressed animation. Never delete keys from a
	# duplicate of an imported/playing animation or call its native interpolator.
	var result := Animation.new()
	var pause := maxf(0.0, float(_motion.get("gesture_pause_seconds" if speaking else "listener_pause_seconds", 1.0)))
	result.length = maxf(0.1, source.length) + pause
	result.loop_mode = Animation.LOOP_LINEAR
	var samples := maxi(2, int(ceil(result.length * 24.0)))
	for track in base.get_track_count():
		var kind := base.track_get_type(track)
		if kind not in [Animation.TYPE_ROTATION_3D, Animation.TYPE_POSITION_3D, Animation.TYPE_SCALE_3D]: continue
		var output := result.add_track(kind)
		var path := base.track_get_path(track)
		result.track_set_path(output, path)
		result.track_set_interpolation_type(output, base.track_get_interpolation_type(track))
		var bone := str(path.get_subname(path.get_subname_count() - 1)) if path.get_subname_count() > 0 else ""
		bone = bone.trim_prefix("mixamorig:").trim_prefix("mixamorig_")
		var arm: bool = bone in _motion.get("arm_bones", [])
		var torso: bool = bone in _motion.get("torso_bones", [])
		var source_track := source.find_track(path, Animation.TYPE_ROTATION_3D) if kind == Animation.TYPE_ROTATION_3D else -1
		var weight := 0.0
		if torso: weight = float(_motion.get("speaker_torso_weight" if speaking else "listener_torso_weight", 0.0))
		elif arm and speaking and free_hands: weight = float(_motion.get("speaker_arm_weight", 0.0))
		if kind != Animation.TYPE_ROTATION_3D or source_track < 0 or weight <= 0.0:
			for key in base.track_get_key_count(track):
				result.track_insert_key(output, base.track_get_key_time(track, key), base.track_get_key_value(track, key), base.track_get_key_transition(track, key))
			continue
		var base_keys := _rotation_keys(base, track)
		var source_keys := _rotation_keys(source, source_track)
		if base_keys.is_empty() or source_keys.is_empty():
			for key in base.track_get_key_count(track):
				result.track_insert_key(output, base.track_get_key_time(track, key), base.track_get_key_value(track, key))
			continue
		for sample in range(samples + 1):
			var time := result.length * float(sample) / float(samples)
			var rest := _sample_rotation(base_keys, fmod(time, maxf(0.001, base.length)))
			var envelope := pow(sin(PI * clampf(time / maxf(0.001, source.length), 0.0, 1.0)), 2.0) if time < source.length else 0.0
			var gesture := _sample_rotation(source_keys, minf(time, source.length))
			result.rotation_track_insert_key(output, time, rest.slerp(gesture, clampf(weight * envelope, 0.0, 1.0)).normalized())
	return result

static func _rotation_keys(animation: Animation, track: int) -> Array:
	var result: Array = []
	if track < 0 or track >= animation.get_track_count(): return result
	for key in animation.track_get_key_count(track):
		var value = animation.track_get_key_value(track, key)
		if not value is Quaternion or not value.is_finite() or value.length_squared() < 0.0001: return []
		result.append({"time": animation.track_get_key_time(track, key), "rotation": value.normalized()})
	return result

static func _sample_rotation(keys: Array, time: float) -> Quaternion:
	for index in range(1, keys.size()):
		if time <= float(keys[index].time):
			var before: Dictionary = keys[index - 1]
			var after: Dictionary = keys[index]
			var span := float(after.time) - float(before.time)
			var weight := clampf((time - float(before.time)) / maxf(0.00001, span), 0.0, 1.0)
			return (before.rotation as Quaternion).slerp(after.rotation, weight).normalized()
	return keys.back().rotation
