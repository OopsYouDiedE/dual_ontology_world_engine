extends SceneTree
## Isolated audio/presentation fixture. Does not load Main, DV or a saved world.
const Policy := preload("res://scenes/Shared/Audio/MusicContext.gd")
const Sound := preload("res://scenes/Shared/Audio/Sound.gd")
var failed := false

func _initialize() -> void:
	_run.call_deferred()

func check(condition: bool, description: String) -> void:
	if not condition:
		failed = true
		push_error(description)
	else:
		print("MUSIC_CONTEXT: " + description)

func _run() -> void:
	check(Policy.background({}) == "", "empty context leaves title alone")
	for time in [0, 540, 720, 900, 1200, 1500]:
		check(Policy.background({"chapter": 1, "game_time": time}) == "door_side", "chapter 1 investigation at %d minutes" % time)
	var times := {420: "day", 690: "day_noon", 810: "day_afternoon", 1050: "day_evening", 1260: "night", 1800: "night"}
	for time in times:
		check(Policy.background({"chapter": 2, "game_time": time}) == times[time], "chapter 2 time-of-day selection %d" % time)
	check(not Policy.is_formal_confrontation({"visible": true, "surface": "准备对质", "pending": {"kind": "answer"}}), "ordinary text and candidate actions cannot start confrontation")
	check(not Policy.is_formal_confrontation({"judgment": false}), "no judgment means no confrontation")
	check(not Policy.is_formal_confrontation({"judgment": {"status": "unknown"}}), "unknown judgment is not inferred")
	var audio: Node = root.get_node("GameAudio")
	Sound.scene_music({"chapter": 1, "game_time": 540})
	check(audio.current_music() == "door_side", "production Sound entry starts chapter 1 background")
	var active: int = audio.get("_active")
	Sound.scene_music({"chapter": 1, "game_time": 545})
	check(int(audio.get("_active")) == active, "same background does not restart on snapshot updates")
	Sound.music("confrontation", 0.01)
	check(audio.current_music() == "door_side", "unscoped confrontation request cannot become background")
	var token: int = Sound.begin_presentation_music({"judgment": {"status": "answer_accepted", "event_id": "audio-fixture-only"}})
	check(token > 0 and audio.current_music() == "confrontation", "delivered formal judgment opens a scoped cue")
	Sound.scene_music({"chapter": 2, "game_time": 840})
	check(audio.current_music() == "confrontation", "ambient update during reading does not cut the scoped cue")
	Sound.end_presentation_music(token + 1)
	check(audio.current_music() == "confrontation", "stale callback cannot close another presentation")
	Sound.end_presentation_music(token)
	check(audio.current_music() == "day_afternoon", "reading completion restores latest ambient context")
	Sound.scene_music({"chapter": 1, "game_time": 540})
	token = Sound.begin_presentation_music({"judgment": {"status": "answer_rejected", "event_id": "audio-fixture-only"}})
	Sound.end_presentation_music(token)
	check(audio.current_music() == "door_side", "rejected defense also returns to investigation")
	token = Sound.begin_presentation_music({"judgment": {"status": "answer_accepted"}})
	Sound.music("main_theme", 0.01)
	Sound.end_presentation_music(token)
	check(audio.current_music() == "main_theme", "return to title invalidates old callbacks")
	for cue in ["main_theme", "door_side", "confrontation", "night", "day", "day_noon", "day_afternoon", "day_evening", "rite"]:
		var path := "res://ShareAsset/Internal/Props/Audio/Music/%s.ogg" % cue
		var stream := load(path) as AudioStreamOggVorbis
		check(stream != null and stream.get_length() > 1.0, "native audio resource loads: " + cue)
	var drained: bool = await audio.release_audio_and_wait_for_mix()
	check(drained, "audio driver drained before fixture shutdown")
	await process_frame
	print("MUSIC_CONTEXT_DIAGNOSTIC_COMPLETE; no gameplay session executed")
	quit(1 if failed else 0)
