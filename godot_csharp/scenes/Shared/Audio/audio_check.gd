extends SceneTree
## 音频自动检查：脚本里引用的每个 Sound.music / Sound.sfx id 都有可加载的文件；
## 配乐能循环且长度合理；GameAudio 能完成一次交叉淡化。
##   godot --headless --path godot_csharp --script res://scenes/Shared/Audio/audio_check.gd

const GameAudioScript := preload("res://scenes/Shared/Audio/GameAudio.gd")

var failures: Array[String] = []

func _initialize() -> void:
	var refs := {"music": {}, "sfx": {}}
	var pattern := RegEx.create_from_string("Sound\\.(music|sfx)\\(\"([a-z_]+)\"")
	for path in _scripts("res://scenes"):
		for m in pattern.search_all(FileAccess.get_file_as_string(path)):
			refs[m.get_string(1)][m.get_string(2)] = path
	for id in refs.music:
		var stream := load(GameAudioScript.MUSIC_PATH % id) as AudioStreamOggVorbis
		if stream == null:
			failures.append("配乐 %s 无法加载（%s）" % [id, refs.music[id]])
			continue
		var looped: AudioStreamOggVorbis = stream.duplicate()
		looped.loop = true
		if not looped.loop or stream.get_length() < 30.0:
			failures.append("配乐 %s 循环或长度异常（%.1f 秒）" % [id, stream.get_length()])
	for id in refs.sfx:
		var stream := load(GameAudioScript.SFX_PATH % id) as AudioStream
		if stream == null or stream.get_length() <= 0.0 or stream.get_length() > 10.0:
			failures.append("音效 %s 无法加载或长度异常（%s）" % [id, refs.sfx[id]])
	var hub: Node = GameAudioScript.new()
	root.add_child(hub)
	await process_frame
	hub.play_music("door_side", 0.1)
	hub.play_music("confrontation", 0.1)
	await create_timer(0.3).timeout
	if hub.current_music() != "confrontation":
		failures.append("交叉淡化后当前曲目应为 confrontation，实际为 %s" % hub.current_music())
	hub.queue_free()
	await process_frame
	print("audio checks: %d music, %d sfx referenced" % [refs.music.size(), refs.sfx.size()])
	for f in failures:
		print("FAIL ", f)
	print("audio checks %s" % ("通过" if failures.is_empty() else "失败"))
	quit(0 if failures.is_empty() else 1)

func _scripts(dir: String) -> Array[String]:
	var out: Array[String] = []
	for file in DirAccess.get_files_at(dir):
		if file.ends_with(".gd"):
			out.append(dir.path_join(file))
	for sub in DirAccess.get_directories_at(dir):
		out.append_array(_scripts(dir.path_join(sub)))
	return out
